using Godot;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Effects;
using Vectory.Game;
using Vectory.Settings;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.Ship;

// Nave del jugador. Este nodo solo CONECTA piezas: lee el teclado, le pasa la entrada a la física de cada paso
// (ShipPhysics: boost, dinámica, barreras y capa vertical), mueve el cuerpo en Godot, actualiza la nave visual y
// las cámaras (con la sacudida de CameraShake), y le dice a la sesión de carrera (RaceSession) dónde está para
// contar vueltas. Las reglas viven en las clases puras.
// Los daños (solo RACE) le llegan desde Main como factores de punta y giro, boost permitido o no y
// nivel de parpadeo de las aristas (EdgeFlicker); la nave no conoce los puntos ni las reglas del daño.
//
// Solo acepta input con la carrera en marcha (RaceSession.CanDrive): en la cuenta atrás la nave
// está quieta y el boost no recarga; al terminar se suelta todo y la nave se para sola.
//
// El derrape (spec docs/conduccion-avanzada.md) vive en ShipDynamics; la inclinación visual en ShipBank.
// Los efectos de cámara (spec docs/camara.md: inclinación de la cabina, vibración y reacción al boost)
// viven en CockpitBank, CameraVibration y BoostCameraEffect; aquí solo se aplican a las dos cámaras.
//
// Pista 3D (spec docs/pista-3d.md): la dinámica sigue en planta, sin cambiar sus reglas; encima va la capa
// vertical (ShipVertical: en el suelo pegada a la superficie, despegue en crestas, vuelo y aterrizaje), la
// orientación con la pista (ShipAttitude: cabeceo y ladeo sobre el rumbo, en el nodo Player, así la cabina
// va con ella), el efecto de la pendiente (SlopeEffect) y el agarre por peralte (BankGrip). En el aire se
// usa ShipDynamics.StepAirborne. En pista plana todo da exactamente lo mismo que antes.
//
// W / Flecha arriba: acelerar    S / Flecha abajo: frenar (también corta el impulso del boost)
// A,D / Flechas izq.-der.: girar
// Espacio (acción "drift") + girar: derrapar    Shift (acción "boost"): gastar una carga de boost
// C: alternar cabina / tercera persona
public partial class Player : CharacterBody3D, IContactShip
{
	// Nombres de acciones y de parámetros de shader creados una vez: con un texto, cada llamada crea un StringName
	// nuevo (con finalizador), y en cada frame eso alargaba las pausas del recolector de .NET
	private static readonly StringName SnAccelerate = "accelerate";
	private static readonly StringName SnBoost = "boost";
	private static readonly StringName SnBrake = "brake";
	private static readonly StringName SnCamera = "camera";
	private static readonly StringName SnDrift = "drift";
	private static readonly StringName SnSteerLeft = "steer_left";
	private static readonly StringName SnSteerRight = "steer_right";

	// Misma conversión que usa el HUD para mostrar la velocidad
	private const float KmhPerMs = 3.6f;

	// La nave en uso: su conducción y su forma salen solo de aquí (spec docs/naves-1-definicion.md)
	public ShipDefinition Definition { get; } = ShipDefinitions.Resolve(GameSession.ShipDesignId, message => GD.PushWarning(message));

	// Perfil técnico y visual que manda el panel de ajuste, en lugar de los de la definición (solo en pruebas)
	private ShipTuning? _labTuning;
	private ShipVisualProfile _labVisual;

	private ShipTuning Technical => _labTuning ?? Definition.Technical;

	// De este perfil salen el encuadre exterior y el ancho de colisión. La caja física y la colocación siguen saliendo
	// de Definition.
	private ShipVisualProfile ExteriorVisual => _labVisual ?? Definition.Visual;

	// Velocidad punta de la nave, sin daños (m/s)
	public float MaxSpeed => Technical.MaxSpeed;

	// --- Ajustes comunes a todas las naves: se editan en el Inspector (agrupados) ---
	[ExportGroup("Boost")]
	[Export] public int BoostMaxCharges = BoostTuning.DefaultMaxCharges;
	[Export] public int BoostInitialCharges = ShipPhysicsDefaults.BoostInitialCharges;
	[Export] public float BoostRechargeTime = ShipPhysicsDefaults.BoostRechargeTime;   // segundos para ganar una carga
	[Export] public float BoostSpeedGain = BoostTuning.DefaultSpeedGainKmh;     // km/h que suma cada carga
	[Export] public float BoostRampTime = ShipPhysicsDefaults.BoostRampTime;      // segundos de subida (en los que se reparte el impulso)
	[Export] public float BoostHoldTime = ShipPhysicsDefaults.BoostHoldTime;      // segundos que se mantiene la velocidad alcanzada
	[Export] public float BoostCooldown = ShipPhysicsDefaults.BoostCooldown;      // segundos entre usos

	[ExportGroup("Derrape")]
	[Export] public float DriftEnterSpeed = ShipPhysicsDefaults.DriftEnterSpeed;      // km/h mínimos para entrar en derrape
	[Export] public float DriftExitSpeed = ShipPhysicsDefaults.DriftExitSpeed;        // km/h por debajo de los que se sale
	[Export(PropertyHint.Range, "0,1,0.01")] public float DriftGrip = ShipPhysicsDefaults.DriftGrip;                 // agarre lateral en derrape
	[Export(PropertyHint.Range, "0,1,0.01")] public float DriftCountersteerGrip = ShipPhysicsDefaults.DriftCountersteerGrip;   // agarre contravolanteando
	[Export] public float DriftTurnMultiplier = ShipPhysicsDefaults.DriftTurnMultiplier;  // giro del rumbo en derrape
	[Export] public float DriftFixedLoss = ShipPhysicsDefaults.DriftFixedLoss;        // km/h que se pierden cada segundo derrapando
	[Export] public float DriftSlipLoss = ShipPhysicsDefaults.DriftSlipLoss;          // km/h por segundo extra al ángulo máximo
	[Export(PropertyHint.Range, "0,1,0.01")] public float DriftAcceleration = ShipPhysicsDefaults.DriftAcceleration;       // aceleración con W en derrape
	[Export] public float DriftMaxSlipAngle = ShipPhysicsDefaults.DriftMaxSlipAngle;  // grados de deslizamiento máximo
	[Export] public float DriftGripRecoveryTime = ShipPhysicsDefaults.DriftGripRecoveryTime;  // segundos para volver al agarre normal al salir

	[ExportGroup("Inclinación")]
	[Export] public float BankMaxNormal = 25f;   // grados a giro máximo y velocidad punta
	[Export] public float BankMaxDrift = 20f;    // grados en derrape
	[Export] public float BankSmoothing = 0.15f; // constante de tiempo del suavizado (s)

	[ExportGroup("Efectos de cámara")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float CockpitBankFactor = 0.4f;   // inclinación de la cabina respecto a la de la nave (0 = no se inclina)
	[Export] public float CockpitBankMax = 8f;            // grados
	[Export] public float CockpitBankSmoothing = 0.35f;   // constante de tiempo del suavizado (s)
	[Export(PropertyHint.Range, "0,1,0.01")] public float VibrationThreshold = 0.85f;   // fracción de la punta desde la que vibra
	[Export] public float VibrationAmplitudeCockpit = 0.03f;    // m
	[Export] public float VibrationAmplitudeChase = 0.018f;    // m
	[Export] public float VibrationBoostMultiplier = 1.5f;
	[Export] public float VibrationFrequency = 8f;              // Hz base
	[Export] public float VibrationSmoothing = 0.2f;            // constante de tiempo de la intensidad (s)
	[Export] public float BoostFovExtra = 8f;                   // grados sobre el FOV base de cada cámara
	[Export] public float BoostPushCockpit = 0.05f;             // m hacia atrás
	[Export] public float BoostPushChase = 0.6f;                // m hacia atrás
	[Export] public float BoostEffectEnterTime = 0.25f;         // s
	[Export] public float BoostEffectExitTime = 0.5f;           // s
	[Export(PropertyHint.Range, "0,1,0.01")] public float CockpitTrackBankFactor = 1f;   // peralte de la pista que se ve en cabina (1 = completo)

	[ExportGroup("Vuelo")]
	[Export] public float Gravity = ShipPhysicsDefaults.Gravity;   // m/s², hacia abajo en el mundo (ajustado en pista; la spec proponía 6)
	[Export(PropertyHint.Range, "0,1,0.01")] public float AirTurn = ShipPhysicsDefaults.AirTurn;   // giro en el aire respecto al normal
	[Export] public float AirMaxPitch = 20f;             // grados de inclinación máxima del morro en el aire
	[Export] public float AirAttitudeSmoothing = 0.3f;   // s: suavizado de la orientación en el aire
	[Export] public float LandingAlignTime = 0.15f;      // s: suavizado para volver a la superficie al aterrizar
	[Export(PropertyHint.Range, "0,1,0.01")] public float LandingSpeedLoss = ShipPhysicsDefaults.LandingSpeedLoss;   // fracción de velocidad que se pierde al aterrizar
	[Export] public float LandingMediumSpeed = 3f;       // m/s de caída desde los que hay sacudida media
	[Export] public float LandingStrongSpeed = 8f;       // m/s de caída desde los que es fuerte
	[Export] public float LandingCompression = 0.3f;     // m que baja la nave al aterrizar (a intensidad máxima)
	[Export] public float LandingCompressionTime = 0.2f; // s en bajar y volver

	[ExportGroup("Pendiente y peralte")]
	[Export] public float GroundAttitudeSmoothing = 0.1f;   // s: suavizado de la orientación en el suelo
	[Export] public float SlopeReference = ShipPhysicsDefaults.SlopeReference;   // grados: pendiente a la que el efecto es completo
	[Export(PropertyHint.Range, "0,1,0.01")] public float SlopeEffectAmount = ShipPhysicsDefaults.SlopeEffectAmount;   // punta y aceleración: −30 % subiendo, +30 % bajando
	[Export(PropertyHint.Range, "0,1,0.01")] public float BankGripBonus = ShipPhysicsDefaults.BankGripBonus;       // agarre extra con 25° de peralte a favor
	[Export(PropertyHint.Range, "0,1,0.01")] public float BankTurnBonus = ShipPhysicsDefaults.BankTurnBonus;       // giro extra con 25° de peralte a favor

	[ExportGroup("Colisiones")]
	[Export] public float ImpactMediumSpeed = ShipPhysicsDefaults.ImpactMediumSpeed;   // km/h contra la barrera desde los que el impacto es medio
	[Export] public float ImpactStrongSpeed = ShipPhysicsDefaults.ImpactStrongSpeed;   // km/h contra la barrera desde los que el impacto es fuerte
	[Export(PropertyHint.Range, "0,1,0.01")] public float GrazeStraighten = ShipPhysicsDefaults.GrazeStraighten;   // fracción del ángulo que se quita al empezar un roce
	[Export(PropertyHint.Range, "0,1,0.01")] public float GrazeEntryLoss = ShipPhysicsDefaults.GrazeEntryLoss;     // fracción de velocidad que se pierde al empezar un roce
	[Export] public float GrazeFriction = ShipPhysicsDefaults.GrazeFriction;           // km/h que se pierden cada segundo mientras roza
	[Export(PropertyHint.Range, "0,1,0.01")] public float MediumRestitution = ShipPhysicsDefaults.MediumRestitution;
	[Export(PropertyHint.Range, "0,1,0.01")] public float StrongRestitution = ShipPhysicsDefaults.StrongRestitution;
	[Export(PropertyHint.Range, "0,1,0.01")] public float MediumSpeedLoss = ShipPhysicsDefaults.MediumSpeedLoss;
	[Export(PropertyHint.Range, "0,1,0.01")] public float StrongSpeedLoss = ShipPhysicsDefaults.StrongSpeedLoss;
	[Export] public float RecoveryDuration = ShipPhysicsDefaults.RecoveryDuration;     // segundos de control reducido tras un impacto fuerte
	[Export(PropertyHint.Range, "0,1,0.01")] public float RecoveryControl = ShipPhysicsDefaults.RecoveryControl;   // giro y aceleración al empezar la corrección
	[Export] public float ImpactCooldown = ShipPhysicsDefaults.ImpactCooldown;         // segundos sin contar otro impacto medio o fuerte en el mismo lado
	// Choques entre naves (spec docs/rivales-3-contacto.md): umbrales, restituciones, roce y pérdidas son los de arriba
	[Export] public float ShipContactMaxHeight = ShipContactSettings.DefaultMaxHeightDifference;   // m de diferencia de altura sobre la pista desde los que no chocan
	[Export] public float ShipContactCullDistance = ShipContactSettings.DefaultCullDistance;       // m entre naves desde los que no se miran
	[Export(PropertyHint.Range, "0,1,0.01")] public float ShipContactMutualMargin = ShipContactSettings.DefaultMutualMargin;   // golpe mutuo si la aportación menor llega a esta fracción de la mayor
	[Export(PropertyHint.Range, "0,90,1")] public float ShipContactSideGrazeAngle = ShipContactSettings.DefaultSideGrazeAngle;   // grados entre la normal y la marcha: por encima, roce de lado (frena); si no, por detrás (no frena)
	[Export] public float ShipContactCooldown = ShipPhysicsDefaults.ImpactCooldown;   // segundos sin contar otro golpe medio o fuerte de la misma pareja

	// Rebufo de todas las naves (spec docs/rivales-4-personalidades.md, 4.1): los rivales usan estos mismos valores
	[ExportGroup("Rebufo")]
	[Export] public float SlipstreamMaxDistance = SlipstreamSettings.DefaultMaxDistance;    // m de tu morro a su cola: más lejos, nada
	[Export] public float SlipstreamFullDistance = SlipstreamSettings.DefaultFullDistance;  // m: más cerca, rebufo entero
	[Export] public float SlipstreamMinSpeed = SlipstreamSettings.DefaultMinSpeedKmh;       // km/h, las dos naves
	[Export(PropertyHint.Range, "0,0.2,0.005")] public float SlipstreamTopGain = SlipstreamSettings.DefaultTopGain;   // punta × (1 + esto × r)
	[Export(PropertyHint.Range, "0,1,0.01")] public float SlipstreamDragCut = SlipstreamSettings.DefaultDragCut;      // Drag × (1 − esto × r)
	[Export] public float SlipstreamRiseTime = SlipstreamSettings.DefaultRiseTime;          // s de 0 a 1
	[Export] public float SlipstreamFallTime = SlipstreamSettings.DefaultFallTime;          // s de 1 a 0

	[ExportGroup("Recolocación")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float RespawnBlinkOpacity = RespawnBlink.DefaultOpacity;   // la nave atravesable tras recolocarla baja a esta opacidad
	[Export] public float RespawnBlinkFrequency = RespawnBlink.DefaultFrequency;                              // Hz del parpadeo

	[ExportGroup("Sacudida de cámara")]
	[Export] public float MediumShakeAmplitude = 0.06f;   // m (cámara exterior)
	[Export] public float MediumShakeRoll = 0.8f;         // grados
	[Export] public float MediumShakeDuration = 0.25f;
	[Export] public float StrongShakeAmplitude = 0.15f;
	[Export] public float StrongShakeRoll = 2f;
	[Export] public float StrongShakeDuration = 0.4f;
	[Export] public float ShakeFrequency = 25f;           // Hz de la vibración
	[Export(PropertyHint.Range, "0,1,0.01")] public float CockpitShakeFactor = 0.5f;   // en cabina, todo a este factor

	// Cómo se nota un choque desde la cabina (spec docs/efectos-1-choques.md, 14): luz de las chispas por el lado tocado
	// y temblor fino mientras se roza (este, con COCKPIT SHAKE)
	[ExportGroup("Cabina: choques")]
	[Export] public float CrashLightEnergy = 1.2f;       // energía de la luz de cada lado con el roce más fuerte
	[Export] public float CrashLightTint = 0.8f;         // opacidad del tinte del borde con el roce más fuerte
	[Export] public float CrashEndLightEnergy = 0.5f;    // energía de la luz de un alcance (detrás o delante), más suave
	[Export(PropertyHint.Range, "0,1,0.01")] public float CrashRearGrazeRumble = 0.5f;   // temblor con un roce por detrás
	[Export] public float CrashRumbleAmplitude = 0.012f; // m
	[Export] public float CrashRumbleFrequency = 31f;    // Hz

	[ExportGroup("Daños (parpadeo)")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlickerFractionLevel1 = 0.10f;   // proporción de aristas que parpadean
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlickerFractionLevel2 = 0.20f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlickerFractionLevel3 = 0.35f;
	[Export] public float FlickerMinIntervalLevel1 = 0.5f;   // segundos entre parpadeos de una arista (aleatorio entre mínimo y máximo)
	[Export] public float FlickerMaxIntervalLevel1 = 1f;
	[Export] public float FlickerMinIntervalLevel2 = 0.5f;
	[Export] public float FlickerMaxIntervalLevel2 = 1f;
	[Export] public float FlickerMinIntervalLevel3 = 0.5f;
	[Export] public float FlickerMaxIntervalLevel3 = 1f;
	[Export] public float FlickerMinDuration = 6f;           // segundos que dura un parpadeo
	[Export] public float FlickerMaxDuration = 15f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlickerOpacity = 0.1f;   // opacidad de la arista apagada

	[ExportGroup("Cabina")]
	[Export(PropertyHint.Range, "0,2,0.01")] public float CockpitBoostTintIntensity = 1f;    // multiplica el tinte naranja del diseño (0 = apagado)
	[Export(PropertyHint.Range, "0,2,0.01")] public float CockpitDamageTintIntensity = 1f;   // multiplica el tinte rojo del diseño (0 = apagado)
	[Export(PropertyHint.Range, "0,2,0.01")] public float CockpitScreenGlowIntensity = 1f;   // multiplica el brillo de las pantallas (0 = apagado)
	[Export(PropertyHint.Range, "8,12,1")] public int AirLineCount = AirLineSettings.DefaultCount;                 // líneas de aire del rebufo
	[Export(PropertyHint.Range, "0,1,0.01")] public float AirLineIntensity = AirLineSettings.DefaultIntensity;   // su brillo con rebufo entero

	[ExportGroup("Cabina 3D")]
	[Export] public float Cockpit3DFov = 70f;                 // grados, en vertical: no cambia con los efectos del mundo
	[Export] public float Cockpit3DSway = 0.03f;              // m de balanceo lateral al girar
	[Export] public float Cockpit3DVibrationPosition = 1.6f;
	[Export] public float Cockpit3DVibrationRotation = 0.35f;
	[Export] public float Cockpit3DCounterRoll = 0.25f;       // contragiro respecto a lo que se ladea el mundo
	[Export] public float Cockpit3DBoostPush = 0.05f;         // m hacia atrás con el boost
	[Export] public float Cockpit3DKeyEnergy = 0.65f;
	[Export] public float Cockpit3DAmbientEnergy = 0.32f;
	[Export] public float Cockpit3DLampEnergy = 0.025f;
	[Export] public float Cockpit3DLargeScreenLight = 0.2f;
	[Export] public float Cockpit3DSmallScreenLight = 0.13f;
	[Export] public float Cockpit3DGlowIntensity = 0.8f;
	[Export] public float Cockpit3DGlowStrength = 0.9f;
	[Export] public float Cockpit3DGlowThreshold = 0.8f;
	[Export] public float Cockpit3DScreenBrightness = 1.35f;
	[Export] public float Cockpit3DClearcoat = 1f;            // fuerza del barniz del carbono y la pintura
	[Export] public float Cockpit3DGlowWide = 1f;             // cuánto se reparte el halo de LEDs y pantallas
	[Export] public float Cockpit3DReflections = 1f;          // fuerza del panorama en los reflejos
	[Export(PropertyHint.Range, "0,1,0.01")] public float Cockpit3DCarbonSpecular = 1f;   // brillo del carbono de salpicadero, consola y techo
	[Export] public float Cockpit3DBoostFrontLight = 0.85f;      // multiplica el valor del diseño (0 = apagada)
	[Export] public float Cockpit3DBoostRearLight = 0.85f;
	[Export] public float Cockpit3DDamageLight = 0.7f;

	// Setas del boost y balizas del larguero (spec docs/cabinas-3d-3-hammer.md, 6.5); las balizas también valen para las
	// del techo del casco 3D (spec docs/cascos-3d-3-hammer.md, 6.4)
	[ExportGroup("Cabina 3D: HAMMER")]
	[Export] public float HammerSetaFull = IndustrialLampSettings.Default.SetaFull;
	[Export] public float HammerSetaCooldown = IndustrialLampSettings.Default.SetaCooldown;
	[Export] public float HammerSetaLocked = IndustrialLampSettings.Default.SetaLocked;
	[Export] public float HammerSetaEmpty = IndustrialLampSettings.Default.SetaEmpty;
	[Export] public float HammerBoostBeaconOn = IndustrialLampSettings.Default.BoostBeaconOn;
	[Export] public float HammerBoostBeaconPulse = IndustrialLampSettings.Default.BoostBeaconPulse;   // ± sobre la encendida
	[Export] public float HammerDamageBeaconOn = IndustrialLampSettings.Default.DamageBeaconOn;
	[Export] public float HammerBeaconOff = IndustrialLampSettings.Default.BeaconOff;

	[ExportGroup("Casco 3D")]   // luces propias del casco 3D (spec docs/cascos-3d-1-vector.md, 4.3)
	[Export] public float HullKeyEnergy = Hull3DDefaults.KeyEnergy;
	[Export] public float HullRimEnergy = Hull3DDefaults.RimEnergy;
	[Export] public float HullFillEnergy = Hull3DDefaults.FillEnergy;
	[Export] public float HullAmbientEnergy = Hull3DDefaults.AmbientEnergy;
	[Export(PropertyHint.Range, "0,3,0.01")] public float HullIridescence = Hull3DDefaults.IridescenceStrength; // perla, camaleón y cúpula
	// Anticolisión de BLADE (spec docs/cascos-3d-2-blade.md, 6.4): destello de Duration s cada Period s; la de abajo, Offset s después
	[Export] public float HullStrobePeriod = Hull3DDefaults.Strobe.Period;
	[Export] public float HullStrobeDuration = Hull3DDefaults.Strobe.Duration;
	[Export] public float HullStrobeOffset = Hull3DDefaults.Strobe.Offset;
	[Export] public float HullStrobeIntensity = Hull3DDefaults.Strobe.Intensity;

	private static readonly Hull3DSettings Hull3DDefaults = new();

	// Luces, brillo de levitación, iridiscencia y anticolisión del casco 3D (grupo Casco 3D): también los de los rivales
	public Hull3DSettings BuildHull3DSettings()
	{
		return new Hull3DSettings
		{
			KeyEnergy = HullKeyEnergy,
			RimEnergy = HullRimEnergy,
			FillEnergy = HullFillEnergy,
			AmbientEnergy = HullAmbientEnergy,
			IridescenceStrength = HullIridescence,
			Strobe = new StrobeSettings
			{
				Period = HullStrobePeriod,
				Duration = HullStrobeDuration,
				Offset = HullStrobeOffset,
				Intensity = HullStrobeIntensity
			}
		};
	}

	[ExportGroup("Toberas")]   // llama de las toberas (spec docs/efectos-6-toberas.md, 10); también la de los rivales
	[Export] public float ThrusterBrightness = ThrusterDefaults.Brightness;       // ajuste a ojo con el glow
	[Export] public float ThrusterPowerRise = ThrusterDefaults.PowerRise;         // s
	[Export] public float ThrusterPowerFall = ThrusterDefaults.PowerFall;         // s, sin gas
	[Export] public float ThrusterPowerBrakeFall = ThrusterDefaults.PowerBrakeFall; // s, frenando
	[Export] public float ThrusterBoostRise = ThrusterDefaults.BoostRise;         // s
	[Export] public float ThrusterBoostFall = ThrusterDefaults.BoostFall;         // s
	[Export] public float ThrusterKickGain = ThrusterDefaults.KickGain;           // largo extra al empezar un boost
	[Export] public float ThrusterKickTau = ThrusterDefaults.KickTau;             // s
	[Export] public float ThrusterLengthBase = ThrusterDefaults.LengthBase;       // m
	[Export] public float ThrusterLengthPower = ThrusterDefaults.LengthPower;     // m × potencia
	[Export] public float ThrusterLengthBoost = ThrusterDefaults.LengthBoost;     // m × boost
	[Export] public float ThrusterOuterPush = ThrusterDefaults.OuterPush;         // la de fuera, más larga al girar
	[Export] public float ThrusterBendDrift = ThrusterDefaults.BendDrift;         // doblado de la punta al derrapar, × largo
	[Export] public float ThrusterMaxSlip = ThrusterDefaults.MaxSlipDegrees;      // grados: doblada del todo desde aquí
	[Export] public float ThrusterRingSize = ThrusterDefaults.RingEnd;            // m, tamaño final del anillo del boost
	[Export] public float ThrusterRingDuration = ThrusterDefaults.RingDuration;   // s
	[Export] public float ThrusterDiamonds = ThrusterDefaults.DiamondGlow;        // brillo de los diamantes de choque
	[Export] public float ThrusterHeat = ThrusterDefaults.HeatDisplacement;       // desplazamiento del calor (pantalla)
	[Export] public float ThrusterHeatReference = ThrusterDefaults.HeatReferenceDistance; // m: más lejos, el calor baja con la distancia
	[Export] public float ThrusterHeatFadeStart = ThrusterDefaults.HeatFadeStart;  // m: desde aquí se apaga
	[Export] public float ThrusterHeatFadeEnd = ThrusterDefaults.HeatFadeEnd;      // m: aquí ya no hay calor
	[Export] public float ThrusterLightEnergy = ThrusterDefaults.LightEnergy;     // E0 de la luz de las toberas en el casco

	private static readonly ThrusterFxSettings ThrusterDefaults = new();

	[ExportGroup("Luces")]   // luces de la nave (spec docs/efectos-8-luces.md, 12); también las de los rivales
	[Export] public float LightProfileBoost = LightDefaults.ProfileBoost;     // realce de las bandas de perfil (VECTOR)
	[Export] public float LightNoseBoost = LightDefaults.NoseBoost;           // luces del morro (VECTOR)
	[Export] public float LightFinBoost = LightDefaults.FinBoost;             // luces de las aletas (VECTOR)
	[Export] public float LightBrakeBoost = LightDefaults.BrakeBoost;         // pilotos y luces de freno
	[Export] public float LightNavBoost = LightDefaults.NavBoost;             // luces de posición (BLADE)
	[Export] public float LightFormBoost = LightDefaults.FormBoost;           // tiras de formación (BLADE)
	[Export] public float LightTailWhiteBoost = LightDefaults.TailWhiteBoost; // cola blanca (BLADE)
	[Export] public float LightAmberBoost = LightDefaults.AmberBoost;         // balizas ámbar (HAMMER)
	[Export] public float LightWorkBoost = LightDefaults.WorkBoost;           // focos de trabajo (HAMMER)
	[Export] public float LightHeadBoost = LightDefaults.HeadBoost;           // faros
	[Export] public float LightNoseHalo = LightDefaults.NoseHalo;             // m, tamaño de los halos por tipo
	[Export] public float LightFinHalo = LightDefaults.FinHalo;
	[Export] public float LightBrakeHalo = LightDefaults.BrakeHalo;
	[Export] public float LightNavHalo = LightDefaults.NavHalo;
	[Export] public float LightTailWhiteHalo = LightDefaults.TailWhiteHalo;
	[Export] public float LightAmberHalo = LightDefaults.AmberHalo;
	[Export] public float LightWorkHalo = LightDefaults.WorkHalo;
	[Export] public float LightHeadHalo = LightDefaults.HeadHalo;
	[Export] public float LightStrobeHalo = LightDefaults.StrobeHalo;
	[Export] public float LightBeaconHalo = LightDefaults.BeaconHalo;
	[Export] public float LightHaloScale = LightDefaults.HaloScale;           // k: opacidad del halo = 0,32 × k × I
	[Export] public float LightHaloOffset = LightDefaults.HaloOffset;         // m, hacia fuera de la pieza
	[Export] public float LightBulbHalo = LightDefaults.BulbHalo;             // halo de las bombillas de posición, × su diámetro
	[Export] public float LightBrakeRise = LightDefaults.BrakeRise;           // s
	[Export] public float LightBrakeFall = LightDefaults.BrakeFall;           // s
	[Export] public float LightBrakeGain = LightDefaults.BrakeGain;           // a tope, × (1 + esto)
	[Export] public float LightBoostGain = LightDefaults.BoostGain;           // con boost, × (1 + esto)
	[Export] public float LightNewBrakeRest = LightDefaults.NewBrakeRest;     // freno nuevo de BLADE y HAMMER en reposo
	[Export] public float LightTurnTau = LightDefaults.TurnTau;               // s
	[Export] public float LightTurnInner = LightDefaults.TurnInner;           // luces de dentro, × (1 + esto × G)
	[Export] public float LightHullBrakeEnergy = LightDefaults.HullBrakeEnergy; // EB: luz de freno sobre el casco
	[Export] public float LightTrackEnergyScale = LightDefaults.TrackEnergyScale; // luz en pista: unidades de la maqueta → Godot
	[Export] public float LightSpotEnergyScale = LightDefaults.SpotEnergyScale;   // focos: unidades de la maqueta → Godot
	[Export] public float LightBeamOpacity = LightDefaults.BeamOpacity;         // haz visible de los faros
	[Export] public float LightWorkBeamOpacity = LightDefaults.WorkBeamOpacity; // haz de los focos de trabajo (HAMMER)
	[Export] public float LightRetroEnergy = LightDefaults.RetroLightEnergy;    // ER: luz del retro sobre el morro

	private static readonly ShipLightSettings LightDefaults = new();

	// Ajustes de las luces de la nave (grupo Luces): también los de los rivales
	public ShipLightSettings BuildLightSettings()
	{
		return new ShipLightSettings
		{
			ProfileBoost = LightProfileBoost,
			NoseBoost = LightNoseBoost,
			FinBoost = LightFinBoost,
			BrakeBoost = LightBrakeBoost,
			NavBoost = LightNavBoost,
			FormBoost = LightFormBoost,
			TailWhiteBoost = LightTailWhiteBoost,
			AmberBoost = LightAmberBoost,
			WorkBoost = LightWorkBoost,
			HeadBoost = LightHeadBoost,
			NoseHalo = LightNoseHalo,
			FinHalo = LightFinHalo,
			BrakeHalo = LightBrakeHalo,
			NavHalo = LightNavHalo,
			TailWhiteHalo = LightTailWhiteHalo,
			AmberHalo = LightAmberHalo,
			WorkHalo = LightWorkHalo,
			HeadHalo = LightHeadHalo,
			StrobeHalo = LightStrobeHalo,
			BeaconHalo = LightBeaconHalo,
			HaloScale = LightHaloScale,
			HaloOffset = LightHaloOffset,
			BulbHalo = LightBulbHalo,
			BrakeRise = LightBrakeRise,
			BrakeFall = LightBrakeFall,
			BrakeGain = LightBrakeGain,
			BoostGain = LightBoostGain,
			NewBrakeRest = LightNewBrakeRest,
			TurnTau = LightTurnTau,
			TurnInner = LightTurnInner,
			HullBrakeEnergy = LightHullBrakeEnergy,
			TrackEnergyScale = LightTrackEnergyScale,
			SpotEnergyScale = LightSpotEnergyScale,
			BeamOpacity = LightBeamOpacity,
			WorkBeamOpacity = LightWorkBeamOpacity,
			RetroLightEnergy = LightRetroEnergy
		};
	}

	// Ajustes de las toberas (grupo Toberas): también los de los rivales
	public ThrusterFxSettings BuildThrusterSettings()
	{
		return new ThrusterFxSettings
		{
			Brightness = ThrusterBrightness,
			PowerRise = ThrusterPowerRise,
			PowerFall = ThrusterPowerFall,
			PowerBrakeFall = ThrusterPowerBrakeFall,
			BoostRise = ThrusterBoostRise,
			BoostFall = ThrusterBoostFall,
			KickGain = ThrusterKickGain,
			KickTau = ThrusterKickTau,
			LengthBase = ThrusterLengthBase,
			LengthPower = ThrusterLengthPower,
			LengthBoost = ThrusterLengthBoost,
			OuterPush = ThrusterOuterPush,
			BendDrift = ThrusterBendDrift,
			MaxSlipDegrees = ThrusterMaxSlip,
			RingEnd = ThrusterRingSize,
			RingDuration = ThrusterRingDuration,
			DiamondGlow = ThrusterDiamonds,
			HeatDisplacement = ThrusterHeat,
			HeatReferenceDistance = ThrusterHeatReference,
			HeatFadeStart = ThrusterHeatFadeStart,
			HeatFadeEnd = ThrusterHeatFadeEnd,
			LightEnergy = ThrusterLightEnergy
		};
	}

	[ExportGroup("Estelas")]   // estelas de las puntas de las alas (spec docs/efectos-7-estelas.md, 8); también las de los rivales
	[Export] public float TrailBrightness = TrailDefaults.Brightness;                 // ajuste a ojo con el glow
	[Export] public float TrailLoadAcceleration = TrailDefaults.LoadFullAcceleration; // m/s²: carga completa al girar
	[Export] public float TrailLoadDrift = TrailDefaults.LoadDrift;                   // carga derrapando con β = βmáx
	[Export] public float TrailLoadTau = TrailDefaults.LoadTau;                       // s
	[Export] public float TrailVisibleFrom = TrailDefaults.VisibleFrom;               // fracción de la punta
	[Export] public float TrailVisibleFull = TrailDefaults.VisibleFull;               // fracción de la punta
	[Export] public float TrailLifeMax = TrailDefaults.LifeMax;                       // s
	[Export] public float TrailLength = TrailDefaults.LifeScale;                      // largo: la vida × esto
	[Export] public float TrailWidth = TrailDefaults.WidthScale;                      // el ancho × esto
	[Export] public float TrailBoostMix = TrailDefaults.BoostMix;                     // mezcla al color de boost

	private static readonly WingTrailSettings TrailDefaults = new();

	// Ajustes de las estelas (grupo Estelas): también los de los rivales
	public WingTrailSettings BuildTrailSettings()
	{
		return new WingTrailSettings
		{
			Brightness = TrailBrightness,
			LoadFullAcceleration = TrailLoadAcceleration,
			LoadDrift = TrailLoadDrift,
			LoadTau = TrailLoadTau,
			MaxSlipDegrees = ThrusterMaxSlip,
			VisibleFrom = TrailVisibleFrom,
			VisibleFull = TrailVisibleFull,
			LifeMax = TrailLifeMax,
			LifeScale = TrailLength,
			WidthScale = TrailWidth,
			BoostMix = TrailBoostMix
		};
	}

	[ExportGroup("Visión túnel")]   // spec docs/efectos-9-vision-tunel.md, 9
	[Export(PropertyHint.Range, "0,1,0.01")] public float TunnelSpeedStart = TunnelDefaults.SpeedStart;   // fracción de la punta base desde la que entra
	[Export] public float TunnelSpeedWeight = TunnelDefaults.SpeedWeight;
	[Export] public float TunnelBoostWeight = TunnelDefaults.BoostWeight;
	[Export] public float TunnelMax = TunnelDefaults.Max;
	[Export] public float TunnelSmoothing = TunnelDefaults.SmoothTime;              // s
	[Export] public float TunnelBlur = TunnelDefaults.Blur;
	[Export(PropertyHint.Range, "0,1,0.01")] public float TunnelDarken = TunnelDefaults.Darken;
	[Export] public float TunnelHalo = TunnelDefaults.Halo;                         // halo ámbar con boost
	[Export(PropertyHint.Range, "0,1.5,0.01")] public float TunnelMaskStart = TunnelDefaults.MaskStart;   // fracción del centro a la esquina
	[Export(PropertyHint.Range, "0,1.5,0.01")] public float TunnelMaskFull = TunnelDefaults.MaskFull;
	[Export(PropertyHint.Range, "0,1.5,0.01")] public float TunnelVignetteStart = TunnelDefaults.VignetteStart;
	[Export(PropertyHint.Range, "1,32,1")] public int TunnelSamples = TunnelDefaults.Samples;
	[Export(PropertyHint.Range, "1,32,1")] public int TunnelSamplesLow = TunnelDefaults.SamplesLow;     // con PARTICLES en LOW
	[Export] public float TunnelAberrationMax = TunnelDefaults.AberrationMax;      // desdoble con boost completo
	[Export(PropertyHint.Range, "0,1,0.01")] public float TunnelAberrationBase = TunnelDefaults.AberrationBase;   // parte sin boost
	[Export(PropertyHint.Range, "0,1,0.01")] public float TunnelCockpitFactor = TunnelDefaults.CockpitFactor;
	[Export(PropertyHint.Range, "0,1,0.01")] public float TunnelChaseFactor = TunnelDefaults.ChaseFactor;

	private static readonly TunnelVisionSettings TunnelDefaults = new();
	private readonly TunnelVisionSettings _tunnelSettings = new();

	// Copia el grupo Visión túnel al objeto de ajustes (siempre el mismo: sin basura por paso)
	private TunnelVisionSettings BuildTunnelSettings()
	{
		TunnelVisionSettings s = _tunnelSettings;
		s.SpeedStart = TunnelSpeedStart;
		s.SpeedWeight = TunnelSpeedWeight;
		s.BoostWeight = TunnelBoostWeight;
		s.Max = TunnelMax;
		s.SmoothTime = TunnelSmoothing;
		s.Blur = TunnelBlur;
		s.Darken = TunnelDarken;
		s.Halo = TunnelHalo;
		s.MaskStart = TunnelMaskStart;
		s.MaskFull = TunnelMaskFull;
		s.VignetteStart = TunnelVignetteStart;
		s.Samples = TunnelSamples;
		s.SamplesLow = TunnelSamplesLow;
		s.AberrationMax = TunnelAberrationMax;
		s.AberrationBase = TunnelAberrationBase;
		s.CockpitFactor = TunnelCockpitFactor;
		s.ChaseFactor = TunnelChaseFactor;
		return s;
	}

	[ExportGroup("Cámara de tercera persona")]
	[Export] public float CameraFollow = 6f;     // más alto = cámara más pegada, menos efecto dron
	[Export] public float BaseFov = 75f;
	[Export] public float MaxFov = 95f;
	[Export] public float CameraUpSmoothing = 0.3f;   // s: el "arriba" de la cámara sigue al de la superficie (y al del mundo en el aire)

	// La física de cada paso (boost, dinámica, barreras y capa vertical)
	private readonly ShipPhysics _physics = new();
	private readonly ShipBank _bank = new();
	private readonly CockpitBank _cockpitBank = new();
	private readonly CameraVibration _vibration = new();
	private readonly BoostCameraEffect _boostCamera = new();
	private readonly CameraEffectSettings _cameraEffects = new();
	private readonly ShipAttitude _attitude = new();
	private readonly LandingCompression _compression = new();
	private readonly TunnelVisionState _tunnel = new();
	private TunnelVisionView _tunnelView;
	private CameraShake _shake;
	private CockpitCrashFeel _crashFeel;
	private Spaceship _ship;
	private Cockpit _cockpit;
	private ChaseCamera _chaseCam;
	private RaceSession _race;

	private bool _chaseView = true;
	private bool _inputBlocked;

	private AutopilotDriver _autopilot;
	private TrackFrame _trackFrame;
	private float _trackWidth;

	public bool AutopilotActive => _autopilot != null;
	public bool AutopilotUsed { get; private set; }

	// Choque con las otras naves (IContactShip): el jugador es el 0
	public int Id => 0;
	public ShipShape Shape => ShipShape.From(ExteriorVisual);
	public ShipPhysics Physics => _physics;
	// Node3D ya tiene Position (la local): esta es la del contacto, en el mundo
	System.Numerics.Vector3 IContactShip.Position => ToNumerics(GlobalPosition);
	public System.Numerics.Vector3 PreviousPosition { get; private set; }
	public PassableState Passable { get; } = new();

	// Su nave dibujada (las chispas de sus luces las suelta Main)
	public Spaceship ShipView => _ship;

	// Rebufo (spec docs/rivales-4-personalidades.md, 4.1): lo actualiza RivalField con las demás naves; lo usa el paso
	// siguiente al montar ShipTuning. En TIME TRIAL no hay nadie: siempre 0.
	public Slipstream Slipstream { get; } = new();
	private float _airLineTime;
	public bool CanCollide => _race != null && _race.CanDrive;
	private float _blinkTime;
	private float _hullOpacity = 1f;

	// Separación de otra nave: la barrera otra vez, sin que pase tiempo
	public void MoveBy(NVec2 delta)
	{
		System.Numerics.Vector3 position = ToNumerics(GlobalPosition);
		position.X += delta.X;
		position.Z += delta.Y;
		_physics.AfterContactMove(ref position);
		GlobalPosition = new Vector3(position.X, position.Y, position.Z);
	}

	private ShipDynamics Dynamics => _physics.Dynamics;
	private ShipBoost Boost => _physics.Boost;
	private ShipVertical Vertical => _physics.Vertical;

	// Parpadeo de las aristas dañadas de la nave y de la cabina (null hasta que hay daño)
	private EdgeFlicker _shipFlicker;
	private EdgeFlicker _cockpitFlicker;
	private readonly System.Random _flickerRandom = new();
	private readonly int _glassSeed = System.Random.Shared.Next();   // sitio de las grietas del cristal, una vez por carrera
	private int _damageLevel;

	// Factores de la velocidad punta y del giro (los daños los bajan; 1 = sin efecto).
	// Se aplican sobre MaxSpeed y TurnRate al pasar los parámetros al modelo, sin cambiarlos.
	public float TopSpeedFactor { get; set; } = 1f;
	public float TurnRateFactor { get; set; } = 1f;

	// Si se puede activar el boost (los daños de nivel 3 lo bloquean): las cargas se siguen ganando
	// y un impulso en curso no se corta
	public bool BoostAllowed
	{
		get => Boost.ActivationAllowed;
		set => Boost.ActivationAllowed = value;
	}

	// Velocidad real actual en m/s (la usa el HUD)
	public float Speed => Dynamics.RealSpeed;

	// Contacto con la barrera en el último paso de física (lo leen Main para el destello y la recolocación)
	public BarrierTouch LastBarrierTouch => _physics.LastBarrierTouch;

	// Velocidad máxima con un boost desde la punta (m/s): tope de la barra de velocidad del HUD
	public float BoostedTopSpeed => MaxSpeed + BoostSpeedGain / KmhPerMs;

	// Cargas de boost disponibles y si el cooldown está activo (las lee el HUD junto con BoostMaxCharges)
	public int BoostCharges => Boost.Charges;
	public bool BoostCooldownActive => Boost.IsCooldownActive;
	public bool BoostBlocked => !Boost.ActivationAllowed;

	// La nave va por el aire (tras despegar en una cresta): el atasco no cuenta
	public bool IsAirborne => Vertical.Airborne;
	// En el último paso de física: si aterrizó y con qué velocidad de caída contra la superficie (m/s)
	public bool JustLanded => Vertical.Landed;
	public float LandingSpeed => Vertical.LandingSpeed;
	// m/s, positiva hacia arriba (con la planta, la velocidad en el mundo)
	public float VerticalSpeed => Vertical.VerticalSpeed;
	// Umbrales del aterrizaje (spec docs/pista-3d.md, 3.4), también para los efectos de los rivales
	public LandingSettings LandingThresholds => BuildLandingSettings();

	// El cuadro de mando enseña el daño (RACE). En TIME TRIAL no hay daños: se queda apagado.
	public bool CockpitDamageEnabled { get; set; }

	// Vista de cámara actual y su cambio con la tecla C
	public CameraView View => _chaseView ? CameraView.Chase : CameraView.Cockpit;
	public event System.Action<CameraView> ViewChanged;

	// Qué se enseña en la vista actual, según la cabina de la nave que se conduce
	public CockpitViewRules ViewRules => CockpitViewRules.For(View, _cockpit.Screens3DShowHud);

	// Rotación de la nave visible en el mundo (rumbo + inclinación al girar): la graba el fantasma
	public Quaternion ShipRotation => _ship.GlobalBasis.GetRotationQuaternion();

	// Hacia dónde apunta el morro, en planta (X, Z del mundo): el frente del radar y el minimapa
	public System.Numerics.Vector2 PlanForward => Dynamics.Forward;

	// Rozaduras del casco (spec docs/efectos-5-danos-casco.md): las pone Main con los golpes de la carrera (solo RACE)
	private readonly HullWear _hullWear = new(HullWearSettings.Default);
	private int _hullWearVersion;

	public void WearBarrier(in BarrierTouch touch, float dt)
	{
		if (!touch.Contact)
			return;
		_hullWear.Barrier(touch.Side, touch.Level, touch.IsNewImpact, dt);
		PushHullWear();
	}

	public void WearShip(NVec2 point, ImpactLevel level, bool isNewImpact, float dt)
	{
		ShipVisualProfile visual = ExteriorVisual;
		_hullWear.ContactAt(point, new NVec2(GlobalPosition.X, GlobalPosition.Z), Dynamics.Yaw, visual.CollisionFront,
			visual.CollisionBack, level, isNewImpact, dt);
		PushHullWear();
	}

	private void PushHullWear()
	{
		if (_hullWear.Version == _hullWearVersion)
			return;
		_hullWearVersion = _hullWear.Version;
		_ship.SetHullWear(_hullWear.Zones);
	}

	// Velocidad real en planta (X, Z del mundo): la que heredan las chispas de los choques
	public System.Numerics.Vector2 PlanVelocity => Dynamics.Velocity;

	// Derrape, para sus efectos: si derrapa y su ángulo de deslizamiento (radianes; positivo = desliza a la derecha)
	public bool Drifting => Dynamics.Drifting;
	public float SlipAngle => Dynamics.SlipAngle;

	// La nave que se ve (con F10, la del panel): su semiancho y dónde roza al derrapar
	public float CollisionHalfWidth => ExteriorVisual.CollisionHalfWidth;
	// Perfil visual de la nave que se ve (F10 lo cambia)
	public ShipVisualProfile ExteriorProfile => ExteriorVisual;
	public DriftContactShape DriftContact => ExteriorVisual.DriftContact;

	// La nave que se ve en el mundo, con su inclinación y su cabeceo (de ahí nacen las chispas del derrape)
	public Transform3D HullTransform => _ship.GlobalTransform;

	// Distancia sobre el eje de la pista desde la meta (m), la de la superficie bajo la nave
	public float TrackDistance => _physics.HasSurface ? _physics.Surface.Frame.Distance : 0f;

	// Vuelta para las pantallas de la cabina: el texto del HUD y lo recorrido (0–1). Los pone Main.
	private string _cockpitLapText = "";
	private float _cockpitLapFraction;

	public void SetCockpitLap(string text, float fraction)
	{
		_cockpitLapText = text;
		_cockpitLapFraction = fraction;
	}

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;

		AddChild(new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Shape = new BoxShape3D { Size = ShipLineMesh.ToGodot(Definition.Visual.PhysicsBoxSize) },
			Position = ShipLineMesh.ToGodot(Definition.Visual.PhysicsBoxCenter)
		});

		_ship = new Spaceship { Name = "Spaceship", Visual = Definition.Visual, Use3DHull = true };
		_ship.SetHull3DSettings(BuildHull3DSettings());
		_ship.EnableThrusters(BuildThrusterSettings(), 0f);
		_ship.EnableLights(BuildLightSettings());
		// La pintura aún no decide el color de la estela (spec docs/efectos-7-estelas.md, 7)
		_ship.EnableTrails(BuildTrailSettings(), WingTrailPalette.For(Definition.Id, -1));
		AddChild(_ship);

		_cockpit = new Cockpit { Name = "Cockpit", Visual = Definition.Visual, Settings3D = BuildCockpit3DSettings() };
		AddChild(_cockpit);
		_ship.SetNoseCopy(_cockpit.NoseCopy);

		_chaseCam = new ChaseCamera { Name = "ChaseCamera" };
		AddChild(_chaseCam);

		_tunnelView = new TunnelVisionView { Name = "TunnelVision" };
		_tunnelView.SetSettings(BuildTunnelSettings());
		AddChild(_tunnelView);

		_physics.Place(ToNumerics(GlobalPosition), 0f, 0f, BuildBoostTuning());
		_shake = new CameraShake(BuildShakeSettings());
		_chaseCam.Snap(GlobalPosition, Dynamics.Yaw, SpeedFactor(), CameraSettings(), Vector3.Up);
		ApplyView();
	}

	// Radar y minimapa para las pantallas de la cabina 3D: los lleva Main; aquí solo se pasan
	public void SetCockpitRadar(Vectory.Radar.RadarSweep sweep, Vectory.Radar.MinimapPath path, float windowRadius, System.Collections.Generic.IReadOnlyList<Vectory.Radar.RaceShip> ships)
	{
		_cockpit.SetRadar(sweep, path, windowRadius, ships);
	}

	public void SetCockpitMinimapPose(NVec2 self, NVec2 planForward)
	{
		_cockpit.SetMinimapPose(self, planForward);
	}

	// Le dice al jugador sobre qué circuito conducir (sin circuito no hay límites)
	public void SetTrack(Track track)
	{
		if (track != null && track.IsBuilt)
		{
			_physics.SetTrack(track.Frame, track.BarrierOffset);
			_trackFrame = track.Frame;
			_trackWidth = track.TrackWidth;
		}
		else
		{
			_physics.SetTrack(null, 0f);
			_trackFrame = null;
		}
	}

	// Piloto automático (solo depuración, spec docs/naves-6-equilibrio.md, 4.7): conduce en lugar del teclado, sin boost.
	// Una vez usado, esta carrera no guarda récord ni fantasma, aunque se quite.
	public void SetAutopilot(bool active)
	{
		_autopilot = active && _trackFrame != null ? BuildAutopilot() : null;
		AutopilotUsed |= _autopilot != null;
	}

	private AutopilotDriver BuildAutopilot()
	{
		return AutopilotDriver.Create(_trackFrame, _trackWidth, ExteriorVisual.CollisionHalfWidth, Technical,
			BuildPhysicsSettings(), AutopilotSettings.Default);
	}

	private ShipInput AutopilotInput()
	{
		Vector3 p = GlobalPosition;
		return _autopilot.Drive(new PilotState(new NVec2(p.X, p.Z), Dynamics.Velocity, Dynamics.Yaw, Dynamics.Steer,
			Dynamics.Speed, _physics.Surface.Distance, _physics.Surface.Frame.Bank));
	}

	// Carrera en la que participa: la nave le pasa su posición cada paso y le pregunta si puede conducir
	public void SetRaceSession(RaceSession race)
	{
		_race = race;
	}

	// Panel de ajuste: conducción, ancho, casco, cabina y encuadres de otra nave o con otros valores, en plena carrera.
	// Conserva posición, dirección, velocidad, boost y daño; si la velocidad pasa de la nueva punta, se pierde con la
	// regla de siempre. La caja física y la posición no cambian.
	public void ApplyTuningLab(ShipTuning technical, ShipVisualProfile visual)
	{
		_labTuning = technical;
		_labVisual = visual;
		if (AutopilotActive)
			_autopilot = BuildAutopilot();

		if (visual == _ship.Visual)
			return;
		_ship.SetVisual(visual);
		_cockpit.SetVisual(visual);
		_ship.SetNoseCopy(_cockpit.NoseCopy);
		RebuildFlicker();
		ApplyView();
		ViewChanged?.Invoke(View);
	}

	// El casco y la cabina nuevos tienen otro número de aristas: mismo nivel de daño, con las aristas sorteadas de nuevo
	private void RebuildFlicker()
	{
		if (_shipFlicker == null)
			return;

		int level = _shipFlicker.Level;
		EdgeFlickerSettings settings = BuildFlickerSettings();
		_shipFlicker = new EdgeFlicker(_ship.EdgeCount, settings, _flickerRandom);
		_cockpitFlicker = new EdgeFlicker(_cockpit.EdgeCount, settings, _flickerRandom);
		_shipFlicker.SetLevel(level);
		_cockpitFlicker.SetLevel(level);
		_ship.ShowEdgeFlicker(_shipFlicker, FlickerOpacity);
		_cockpit.ShowEdgeFlicker(_cockpitFlicker);
	}

	// Salida lanzada (GO! de TIME TRIAL): la nave pasa de golpe a "speed" m/s hacia donde apunta
	// (a lo largo de la superficie: sigue en el suelo)
	public void Launch(float speed)
	{
		Dynamics.Reset(Dynamics.Yaw, speed);
	}

	// Coloca la nave en una posición y orientación (yaw en radianes), parada o avanzando ya a initialSpeed m/s.
	// Queda en el suelo, alineada con la superficie.
	public void PlaceAt(Vector3 position, float yaw, float initialSpeed = 0f)
	{
		GlobalPosition = position;
		_physics.Place(ToNumerics(GlobalPosition), yaw, initialSpeed, BuildBoostTuning());
		Slipstream.Reset();
		PreviousPosition = ToNumerics(GlobalPosition);
		_bank.Reset();
		_ship.ResetThrusters();
		_ship.ResetTrails();
		ResetCameraEffects();
		SettleOnSurface();
		_chaseCam.Snap(GlobalPosition, Dynamics.Yaw, SpeedFactor(), CameraSettings(), SurfaceUp());
	}

	// Recolocación automática (atasco o fuera de pista): al instante en la posición dada, parada,
	// mirando según yaw, en el suelo y alineada con la superficie (si estaba en el aire, se cancela el vuelo).
	// El boost no cambia; se cancela la corrección de un impacto fuerte.
	public void Respawn(Vector3 position, float yaw)
	{
		GlobalPosition = position;
		_physics.Respawn(ToNumerics(GlobalPosition), yaw);
		PreviousPosition = ToNumerics(GlobalPosition);
		Passable.Respawned();
		Slipstream.Reset();
		_bank.Reset();
		_ship.ResetThrusters();
		_ship.ResetTrails();
		ResetCameraEffects();
		SettleOnSurface();
		_chaseCam.Snap(GlobalPosition, Dynamics.Yaw, SpeedFactor(), CameraSettings(), SurfaceUp());
	}

	// Con la nave ya en el suelo (ShipPhysics): sin compresión y con la orientación de la superficie al momento
	private void SettleOnSurface()
	{
		_compression.Reset();
		if (_physics.HasSurface)
		{
			ShipAttitude.SurfaceAngles(_physics.Surface.Frame.Normal, Dynamics.Yaw, out float pitch, out float roll);
			_attitude.Reset(pitch, roll);
		}
		else
		{
			_attitude.Reset();
		}
		ApplyBodyRotation();
		ApplyShipVisual(_bank.Angle);
	}

	// Sacude la cámara por un impacto (el roce no sacude; uno más débil que el que esté en curso se ignora)
	public void ShakeCamera(ImpactLevel level)
	{
		_shake.Settings = BuildShakeSettings();
		_shake.Trigger(level);
	}

	// Nivel de daño visible (0–3): algunas aristas de la nave y de la cabina parpadean. Solo sube.
	public void SetFlickerLevel(int level)
	{
		_damageLevel = level;
		_ship.SetHullDamage(level, !CockpitDamageEnabled);
		_cockpit.SetGlassDamage(CockpitDamageEnabled ? level : 0, _glassSeed);
		if (level <= 0 && _shipFlicker == null)
			return;

		if (_shipFlicker == null)
		{
			_shipFlicker = new EdgeFlicker(_ship.EdgeCount, BuildFlickerSettings(), _flickerRandom);
			_cockpitFlicker = new EdgeFlicker(_cockpit.EdgeCount, BuildFlickerSettings(), _flickerRandom);
		}
		_shipFlicker.SetLevel(level);
		_cockpitFlicker.SetLevel(level);
		_ship.ShowEdgeFlicker(_shipFlicker, FlickerOpacity);
		_cockpit.ShowEdgeFlicker(_cockpitFlicker);
	}

	// Ignora el input de conducción (y el boost) hasta que se suelten todas sus teclas.
	// Se usa al salir de la pausa: las teclas pulsadas en ella no deben llegar a la nave.
	public void BlockInputUntilReleased()
	{
		_inputBlocked = true;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event.IsActionPressed(SnCamera))
		{
			_chaseView = !_chaseView;
			ApplyView();
			ViewChanged?.Invoke(View);
		}
	}

	private void ApplyView()
	{
		_ship.SetExteriorVisible(_chaseView);
		_cockpit.ApplyRules(ViewRules);
		if (_chaseView)
			_chaseCam.MakeCurrent();
		else
			_cockpit.Camera.MakeCurrent();
		PushTunnel();   // el factor de cámara cambia al momento
	}

	// HUD de cabina y parpadeo de las aristas dañadas (con la pausa no avanzan: quedan congelados)
	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_cockpit.UpdateHud(BuildCockpitHudInput(), new CockpitLightSettings
		{
			BoostTint = CockpitBoostTintIntensity,
			DamageTint = CockpitDamageTintIntensity,
			ScreenGlow = CockpitScreenGlowIntensity
		}, dt);
		UpdateHullEffects(dt);
		UpdateRespawnBlink(dt);

		if (_shipFlicker == null)
			return;

		EdgeFlickerSettings settings = BuildFlickerSettings();
		_shipFlicker.Settings = settings;
		_cockpitFlicker.Settings = settings;
		if (_shipFlicker.Update(dt))
			_ship.ShowEdgeFlicker(_shipFlicker, FlickerOpacity);
		if (_cockpitFlicker.Update(dt))
			_cockpit.ShowEdgeFlicker(_cockpitFlicker);
	}

	// Atravesable tras recolocarla: el casco parpadea (en cabina no se ve). Con la pausa no avanza.
	private void UpdateRespawnBlink(float dt)
	{
		float opacity = 1f;
		if (Passable.FromRespawn)
		{
			_blinkTime += dt;
			opacity = new RespawnBlink(RespawnBlinkOpacity, RespawnBlinkFrequency).At(_blinkTime);
		}
		else
		{
			_blinkTime = 0f;
		}
		if (opacity == _hullOpacity)
			return;
		_hullOpacity = opacity;
		_ship.SetHullOpacity(opacity);
	}

	public RespawnBlink BuildRespawnBlink() => new(RespawnBlinkOpacity, RespawnBlinkFrequency);

	public AirLineSettings BuildAirLineSettings()
	{
		AirLineSettings settings = AirLineSettings.Default;
		settings.Count = ActiveSettings.SpeedLines ? AirLineCount : 0;   // SPEED LINES de SETTINGS
		settings.Intensity = AirLineIntensity;
		return settings;
	}

	public SlipstreamSettings BuildSlipstreamSettings() => new()
	{
		MaxDistance = SlipstreamMaxDistance,
		FullDistance = SlipstreamFullDistance,
		MinSpeed = SlipstreamMinSpeed / KmhPerMs,
		TopGain = SlipstreamTopGain,
		DragCut = SlipstreamDragCut,
		RiseTime = SlipstreamRiseTime,
		FallTime = SlipstreamFallTime
	};

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		PreviousPosition = ToNumerics(GlobalPosition);

		// 1. Boost por cargas y reglas de conducción (lógica pura).
		// Sin carrera en marcha no hay input ni boost (ni recarga): en la previa la nave está
		// quieta y, al terminar, se cancela el impulso y se para sola con el Drag.
		bool canDrive = _race == null || _race.CanDrive;
		ShipInput input = !canDrive ? default : AutopilotActive ? AutopilotInput() : ReadInput();

		// Tras la pausa, nada cuenta hasta que se suelten las teclas que siguieran pulsadas
		if (AutopilotActive)
		{
			_inputBlocked = false;
		}
		else if (_inputBlocked)
		{
			if (input.Accelerate || input.Brake || input.SteerLeft || input.SteerRight || input.Drift || Input.IsActionPressed(SnBoost))
				input = default;
			else
				_inputBlocked = false;
		}

		ShipTuning baseTuning = BuildTuning();
		ShipPhysicsSettings physics = BuildPhysicsSettings();
		bool boostPressed = !AutopilotActive && !_inputBlocked && Input.IsActionJustPressed(SnBoost);
		_physics.BeginStep(input, boostPressed, canDrive, baseTuning, physics, dt);
		ApplyBodyRotation();
		FeedThrusters(input, canDrive);
		_ship.FeedTrails(dt, new WingTrailMotion
		{
			Velocity = Dynamics.Velocity,
			Drifting = Dynamics.Drifting,
			SlipAngle = Dynamics.SlipAngle
		});

		// 2. Mover el cuerpo en Godot con la velocidad real (en planta; la altura la pone la capa vertical)
		Velocity = new Vector3(Dynamics.Velocity.X, 0f, Dynamics.Velocity.Y);
		MoveAndSlide();

		// 3. Barreras (contacto, roce o rebote) y capa vertical (pegada a la superficie, despegue en crestas,
		// vuelo y aterrizaje), con la nave ya movida
		System.Numerics.Vector3 position = ToNumerics(GlobalPosition);
		_physics.AfterMove(ref position, new NVec2(Velocity.X, Velocity.Z), GetSlideCollisionCount() > 0,
			ExteriorVisual.CollisionHalfWidth, physics, dt);
		GlobalPosition = new Vector3(position.X, position.Y, position.Z);
		ApplyBodyRotation();

		// 4. Al aterrizar: sacudida y compresión según la velocidad de caída
		if (Vertical.Landed)
		{
			LandingSettings landing = BuildLandingSettings();
			LandingLevel level = LandingImpact.Level(Vertical.LandingSpeed, landing);
			if (level != LandingLevel.None)
				ShakeCamera(level == LandingLevel.Strong ? ImpactLevel.Strong : ImpactLevel.Medium);
			_compression.Trigger(LandingImpact.Intensity(Vertical.LandingSpeed, landing));
		}

		// 5. Orientación con la pista e inclinación visual de la nave al girar (solo la geometría exterior)
		float speedRatio = baseTuning.MaxSpeed > 0f ? Dynamics.RealSpeed / baseTuning.MaxSpeed : 0f;
		BankSettings bankSettings = BuildBankSettings();
		_bank.Update(Dynamics.Steer, speedRatio, Dynamics.Drifting, bankSettings, dt);
		UpdateAttitude(dt);
		_compression.Update(dt);
		ApplyShipVisual(_bank.Angle);

		// 6. Efectos de cámara, sacudida (más suave en cabina) y cámara de tercera persona
		float shipBankTarget = ShipBank.Target(Dynamics.Steer, speedRatio, Dynamics.Drifting, bankSettings);
		UpdateCameraEffects(shipBankTarget, baseTuning.MaxSpeed, dt);
		_airLineTime += dt;
		_cockpit.UpdateAirLines(Slipstream.Strength, Dynamics.RealSpeed, _airLineTime, BuildAirLineSettings());
		UpdateShake(dt);
		_chaseCam.UpdateFollow(dt, GlobalPosition, Dynamics.Yaw, SpeedFactor(), CameraSettings(), SurfaceUp());

		// 7. Carrera: cronómetro, checkpoints, meta y vueltas
		_race?.Update(GlobalPosition, dt);
	}

	// ---------------------------------------------------------------
	// Pista 3D
	// ---------------------------------------------------------------

	// Cabeceo y ladeo con la superficie (en el suelo) o hacia el movimiento (en el aire), suavizados
	private void UpdateAttitude(float dt)
	{
		if (!_physics.HasSurface)
			return;

		_attitude.Update(Vertical.Grounded, Vertical.Landed, _physics.Surface.Frame.Normal, Dynamics.Yaw,
			Vertical.VerticalSpeed, Dynamics.RealSpeed, BuildAttitudeSettings(), dt);
		ApplyBodyRotation();
	}

	// Orientación con la pista y en el aire (grupos Vuelo y Pendiente y peralte): también la de los rivales
	public AttitudeSettings BuildAttitudeSettings()
	{
		return new AttitudeSettings
		{
			GroundSmoothTime = GroundAttitudeSmoothing,
			AirSmoothTime = AirAttitudeSmoothing,
			LandingSmoothTime = LandingAlignTime,
			AirMaxPitch = Mathf.DegToRad(AirMaxPitch)
		};
	}

	// Rumbo, cabeceo y ladeo del cuerpo (orden YXZ de Godot): la nave visible y la cabina van con él
	private void ApplyBodyRotation()
	{
		Rotation = new Vector3(_attitude.Pitch, Dynamics.Yaw, _attitude.Roll);
	}

	// Inclinación visual por giro y compresión del aterrizaje (solo visual; la cabina también baja)
	private void ApplyShipVisual(float bank)
	{
		float drop = _compression.Offset(BuildLandingSettings());
		_ship.Rotation = new Vector3(0f, 0f, bank);
		_ship.Position = new Vector3(0f, -drop, 0f);
		_cockpit.Position = new Vector3(0f, -drop, 0f);
	}

	// Pintura del casco 3D de un estilo: la guardada al montar la carrera y, en depuración, F5 del panel de ajuste
	public void SetHullPaint(HullStyle style, int combination)
	{
		_ship.SetHullPaint(style, combination);
	}

	// Combinación de pintura que lleva el casco 3D de un estilo
	public int HullPaint(HullStyle style) => _ship.HullPaint(style);

	// El tiempo de la nave para la anticolisión. Con la pausa no avanza: queda congelado.
	private void UpdateHullEffects(float dt)
	{
		if (!_ship.HasHull3D || !_physics.HasSurface)
			return;
		_hoverTime += dt;
		_ship.UpdateHullEffects(_hoverTime);
		// Balizas del techo (HAMMER): las mismas reglas y ajustes que las de su cabina 3D
		(float boostBeacon, float damageBeacon) = IndustrialHullBeacons.Intensities(Boost.IsImpulseActive, _damageLevel,
			CockpitDamageEnabled, _hoverTime, BuildIndustrialLampSettings());
		_ship.SetHullBeacons(boostBeacon, damageBeacon);
	}

	private float _hoverTime;

	// "Arriba" para la cámara exterior: el de la superficie en el suelo, el del mundo en el aire
	private Vector3 SurfaceUp()
	{
		if (!_physics.HasSurface || !Vertical.Grounded)
			return Vector3.Up;
		System.Numerics.Vector3 n = _physics.Surface.Frame.Normal;
		return new Vector3(n.X, n.Y, n.Z);
	}

	private LandingSettings BuildLandingSettings()
	{
		return new LandingSettings
		{
			MediumSpeed = LandingMediumSpeed,
			StrongSpeed = LandingStrongSpeed,
			CompressionDepth = LandingCompression,
			CompressionTime = LandingCompressionTime
		};
	}

	private static System.Numerics.Vector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

	// Lo que hace la nave, para la llama de sus toberas (spec docs/efectos-6-toberas.md, 3 y 8). En la cuenta atrás la
	// nave no recibe input, pero la llama sí ruge con W (solo visual); terminada, sin gas.
	private void FeedThrusters(in ShipInput input, bool canDrive)
	{
		ShipInput shown = input;
		if (!canDrive && _race != null && _race.State == RaceState.PreStart && !AutopilotActive)
			shown = ReadInput();
		_ship.FeedThrusters(new ThrusterInput
		{
			Accelerate = shown.Accelerate,
			Brake = shown.Brake,
			Steer = ThrusterInput.SteerOf(shown),
			Drifting = Dynamics.Drifting,
			SlipAngle = Dynamics.SlipAngle,
			Boosting = Boost.IsImpulseActive,
			Speed = Dynamics.RealSpeed,
			TopSpeed = MaxSpeed,
			DamageLevel = CockpitDamageEnabled ? _damageLevel : 0
		});
		_ship.FeedLightContext(new ShipLightContext
		{
			Altitude = Physics.HasSurface ? Physics.Surface.Height - Circuit.Track.ShipHover : 0f,
			OnGrid = _race != null && _race.State == RaceState.PreStart,
			Passable = Passable.FromRespawn,
			TimeTrial = _race != null && _race.IsFlyingStart
		});
	}

	// Traduce el teclado a intenciones de conducción (acciones del mapa de entrada; SETTINGS cambia sus teclas)
	private static ShipInput ReadInput()
	{
		return new ShipInput
		{
			Accelerate = Input.IsActionPressed(SnAccelerate),
			Brake = Input.IsActionPressed(SnBrake),
			SteerLeft = Input.IsActionPressed(SnSteerLeft),
			SteerRight = Input.IsActionPressed(SnSteerRight),
			Drift = Input.IsActionPressed(SnDrift)
		};
	}

	private CockpitHudInput BuildCockpitHudInput()
	{
		return new CockpitHudInput
		{
			SpeedKmh = Speed * KmhPerMs,
			TopKmh = MaxSpeed * KmhPerMs,
			MaxKmh = BoostedTopSpeed * KmhPerMs,
			BoostActive = Boost.IsImpulseActive,
			Charges = BoostCharges,
			Cooldown = BoostCooldownActive,
			DamageLevel = _damageLevel,
			DamageEnabled = CockpitDamageEnabled,
			HeadingDegrees = CockpitInstruments.HeadingDegrees(PlanForward),
			Turn = -Dynamics.Steer,   // la dinámica gira positivo a la izquierda; la pantalla, a la derecha
			LapText = _cockpitLapText,
			LapFraction = _cockpitLapFraction
		};
	}

	// El perfil técnico de la nave con los factores de los daños y del rebufo (la punta del rebufo sale de la ya reducida)
	private ShipTuning BuildTuning()
	{
		ShipTuning tuning = Technical;
		tuning.MaxSpeed *= TopSpeedFactor * Slipstream.TopFactor;
		tuning.Drag *= Slipstream.DragFactor;
		tuning.TurnRate *= TurnRateFactor;
		return tuning;
	}

	// Parámetros del derrape del Inspector, con las velocidades en m/s y los ángulos en radianes
	private DriftTuning BuildDriftTuning()
	{
		return new DriftTuning
		{
			Enabled = true,
			EnterSpeed = DriftEnterSpeed / KmhPerMs,
			ExitSpeed = DriftExitSpeed / KmhPerMs,
			Grip = DriftGrip,
			CountersteerGrip = DriftCountersteerGrip,
			TurnMultiplier = DriftTurnMultiplier,
			FixedLoss = DriftFixedLoss / KmhPerMs,
			SlipLoss = DriftSlipLoss / KmhPerMs,
			AccelerationFactor = DriftAcceleration,
			MaxSlipAngle = Mathf.DegToRad(DriftMaxSlipAngle),
			GripRecoveryTime = DriftGripRecoveryTime
		};
	}

	public BankSettings BuildBankSettings()
	{
		return new BankSettings
		{
			MaxNormal = Mathf.DegToRad(BankMaxNormal),
			MaxDrift = Mathf.DegToRad(BankMaxDrift),
			SmoothTime = BankSmoothing
		};
	}

	// Parámetros de la física de un paso, con los valores del Inspector (m/s y radianes): también los de los rivales
	public ShipPhysicsSettings BuildPhysicsSettings()
	{
		return new ShipPhysicsSettings
		{
			Boost = BuildBoostTuning(),
			Drift = BuildDriftTuning(),
			Slope = new SlopeSettings
			{
				ReferenceAngle = Mathf.DegToRad(SlopeReference),
				Effect = SlopeEffectAmount
			},
			BankGrip = new BankGripSettings
			{
				GripBonus = BankGripBonus,
				TurnBonus = BankTurnBonus,
				ReferenceBank = Mathf.DegToRad(ShipPhysicsDefaults.BankReference),
				MaxBank = Mathf.DegToRad(ShipPhysicsDefaults.BankMax)
			},
			Collision = BuildCollisionSettings(),
			Vertical = new VerticalSettings
			{
				Gravity = Gravity,
				Hover = Track.ShipHover
			},
			AirTurn = AirTurn,
			LandingSpeedLoss = LandingSpeedLoss,
			RecoveryDuration = RecoveryDuration,
			RecoveryControl = RecoveryControl
		};
	}

	// Parámetros del boost del Inspector, con las velocidades pasadas de km/h a m/s
	private BoostTuning BuildBoostTuning()
	{
		return new BoostTuning
		{
			MaxCharges = BoostMaxCharges,
			InitialCharges = BoostInitialCharges,
			RechargeTime = BoostRechargeTime,
			SpeedGain = BoostSpeedGain / KmhPerMs,
			RampDuration = BoostRampTime,
			HoldDuration = BoostHoldTime,
			Cooldown = BoostCooldown
		};
	}

	public EdgeFlickerSettings BuildFlickerSettings()
	{
		return new EdgeFlickerSettings
		{
			Level1 = new FlickerLevelSettings { Fraction = FlickerFractionLevel1, MinInterval = FlickerMinIntervalLevel1, MaxInterval = FlickerMaxIntervalLevel1 },
			Level2 = new FlickerLevelSettings { Fraction = FlickerFractionLevel2, MinInterval = FlickerMinIntervalLevel2, MaxInterval = FlickerMaxIntervalLevel2 },
			Level3 = new FlickerLevelSettings { Fraction = FlickerFractionLevel3, MinInterval = FlickerMinIntervalLevel3, MaxInterval = FlickerMaxIntervalLevel3 },
			MinDuration = FlickerMinDuration,
			MaxDuration = FlickerMaxDuration
		};
	}

	private ChaseCameraSettings CameraSettings()
	{
		ChaseFraming chase = ExteriorVisual.Chase;
		return new ChaseCameraSettings
		{
			FollowRate = CameraFollow,
			Height = chase.Height,
			Distance = chase.Distance,
			ExtraDistance = chase.ExtraDistance,
			LookTarget = ShipLineMesh.ToGodot(chase.LookTarget),
			BaseFov = BaseFov,
			MaxFov = ActiveSettings.ScaledMaxFov(BaseFov, MaxFov),   // SPEED FOV de SETTINGS
			UpSmoothTime = CameraUpSmoothing
		};
	}

	// 0 parada ... 1 a velocidad máxima con boost (para alejar la cámara y abrir el FOV)
	private float SpeedFactor()
	{
		return Mathf.Clamp(Dynamics.Speed / BoostedTopSpeed, 0f, 1f);
	}

	private BarrierCollisionSettings BuildCollisionSettings()
	{
		return new BarrierCollisionSettings
		{
			MediumSpeed = ImpactMediumSpeed / KmhPerMs,
			StrongSpeed = ImpactStrongSpeed / KmhPerMs,
			GrazeStraighten = GrazeStraighten,
			GrazeEntryLoss = GrazeEntryLoss,
			GrazeFriction = GrazeFriction / KmhPerMs,
			MediumRestitution = MediumRestitution,
			StrongRestitution = StrongRestitution,
			MediumSpeedLoss = MediumSpeedLoss,
			StrongSpeedLoss = StrongSpeedLoss,
			ImpactCooldown = ImpactCooldown
		};
	}

	// Choques entre naves: los umbrales, restituciones, roce, pérdidas y corrección de la barrera, más lo propio
	public ShipContactSettings BuildShipContactSettings()
	{
		return new ShipContactSettings
		{
			MaxHeightDifference = ShipContactMaxHeight,
			CullDistance = ShipContactCullDistance,
			MediumSpeed = ImpactMediumSpeed / KmhPerMs,
			StrongSpeed = ImpactStrongSpeed / KmhPerMs,
			MediumRestitution = MediumRestitution,
			StrongRestitution = StrongRestitution,
			GrazeEntryLoss = GrazeEntryLoss,
			GrazeFriction = GrazeFriction / KmhPerMs,
			MediumSpeedLoss = MediumSpeedLoss,
			StrongSpeedLoss = StrongSpeedLoss,
			MutualMargin = ShipContactMutualMargin,
			SideGrazeAngle = Mathf.DegToRad(ShipContactSideGrazeAngle),
			PairCooldown = ShipContactCooldown,
			RecoveryDuration = RecoveryDuration,
			RecoveryControl = RecoveryControl
		};
	}

	private CameraShakeSettings BuildShakeSettings()
	{
		return new CameraShakeSettings
		{
			Medium = new ShakeLevelSettings
			{
				Amplitude = MediumShakeAmplitude,
				Roll = Mathf.DegToRad(MediumShakeRoll),
				Duration = MediumShakeDuration
			},
			Strong = new ShakeLevelSettings
			{
				Amplitude = StrongShakeAmplitude,
				Roll = Mathf.DegToRad(StrongShakeRoll),
				Duration = StrongShakeDuration
			},
			Frequency = ShakeFrequency
		};
	}

	// Copia los parámetros del Inspector al objeto de ajustes (ángulos a radianes), con los multiplicadores de SETTINGS:
	// COCKPIT SHAKE en la vibración de las dos cámaras y SPEED FOV en el FOV extra del boost
	private CameraEffectSettings BuildCameraEffects()
	{
		CameraEffectSettings s = _cameraEffects;
		s.CockpitBankFactor = CockpitBankFactor;
		s.CockpitBankMax = Mathf.DegToRad(CockpitBankMax);
		s.CockpitBankSmoothTime = CockpitBankSmoothing;
		s.VibrationThreshold = VibrationThreshold;
		s.VibrationAmplitudeCockpit = VibrationAmplitudeCockpit * ActiveSettings.CockpitShake;
		s.VibrationAmplitudeChase = VibrationAmplitudeChase * ActiveSettings.CockpitShake;
		s.VibrationBoostMultiplier = VibrationBoostMultiplier;
		s.VibrationFrequency = VibrationFrequency;
		s.VibrationSmoothTime = VibrationSmoothing;
		s.BoostFovExtra = BoostFovExtra * ActiveSettings.SpeedFov;
		s.BoostPushCockpit = BoostPushCockpit;
		s.BoostPushChase = BoostPushChase;
		s.BoostEnterTime = BoostEffectEnterTime;
		s.BoostExitTime = BoostEffectExitTime;
		s.CockpitTrackBankFactor = CockpitTrackBankFactor;
		return s;
	}

	// Efectos de cámara (spec docs/camara.md), solo visuales. Se calculan siempre para las dos cámaras,
	// así cambiar de vista con C no da saltos. La vibración usa la punta actual (con daño, reducida).
	private void UpdateCameraEffects(float shipBankTarget, float topSpeed, float dt)
	{
		CameraEffectSettings s = BuildCameraEffects();
		bool boosting = Boost.IsImpulseActive;
		_cockpitBank.Update(shipBankTarget, s, dt);
		_vibration.Update(Dynamics.RealSpeed, topSpeed, boosting, s, dt);
		_boostCamera.Update(boosting, s, dt);
		ApplyCameraEffects(s);

		// Visión túnel (spec docs/efectos-9-vision-tunel.md): f con la punta base, b el de la cámara (0,25 s / 0,5 s)
		TunnelVisionSettings tunnel = BuildTunnelSettings();
		_tunnel.Update(Speed, MaxSpeed, _boostCamera.Amount, tunnel, dt);
		_tunnelView.SetSettings(tunnel);
		PushTunnel();
	}

	private void PushTunnel()
	{
		_tunnelView?.SetSource(_tunnel.Intensity, _boostCamera.Amount, View);
	}

	// Recolocación y colocación: todos los efectos a su valor base al momento
	private void ResetCameraEffects()
	{
		_cockpitBank.Reset();
		_vibration.Reset();
		_boostCamera.Reset();
		_tunnel.Reset();
		ApplyCameraEffects(BuildCameraEffects());
		PushTunnel();
	}

	private void ApplyCameraEffects(CameraEffectSettings s)
	{
		float fovExtra = _boostCamera.FovExtra(s);
		System.Numerics.Vector2 chase = _vibration.Offset(CameraView.Chase, s);
		System.Numerics.Vector2 cockpit = _vibration.Offset(CameraView.Cockpit, s);
		_chaseCam.SetEffects(new Vector2(chase.X, chase.Y), _boostCamera.Push(CameraView.Chase, s), fovExtra);
		// La cabina hereda el ladeo de la pista del cuerpo; con un factor menor que 1 se quita esa parte
		float trackBank = (1f - s.CockpitTrackBankFactor) * _attitude.Roll;
		_cockpit.SetEffects(_cockpitBank.Angle - trackBank, new Vector2(cockpit.X, cockpit.Y), _boostCamera.Push(CameraView.Cockpit, s), fovExtra);

		// La cabina 3D se mueve por su cuenta a partir de los mismos datos, sin sus metros
		var input = new CockpitCameraInput(Dynamics.Steer, Mathf.Clamp(Speed / BoostedTopSpeed, 0f, 1f), _cockpitBank.Angle,
			_vibration.Intensity, _boostCamera.Eased, _vibration.Time);
		float shake = ActiveSettings.CockpitShake;
		var settings = new CockpitCameraSettings(Cockpit3DSway, Cockpit3DVibrationPosition * shake, Cockpit3DVibrationRotation * shake,
			Cockpit3DCounterRoll, Cockpit3DBoostPush);
		CockpitCameraPose pose = CockpitCameraMotion.Compute(input, settings);
		// El temblor del roce también mueve la cabina por dentro, a la mitad (el mundo ya se mueve con SetShake)
		System.Numerics.Vector2 rumble = CrashFeel.RumbleOffset(ActiveSettings.CockpitShake * 0.5f);
		pose = pose with { Position = pose.Position + new System.Numerics.Vector3(rumble.X, rumble.Y, 0f) };
		_cockpit.Update3D(pose, BuildCockpit3DSettings());
	}

	private Cockpit3DSettings BuildCockpit3DSettings()
	{
		return new Cockpit3DSettings(Cockpit3DFov, Cockpit3DKeyEnergy, Cockpit3DAmbientEnergy, Cockpit3DLampEnergy,
			Cockpit3DLargeScreenLight, Cockpit3DSmallScreenLight, Cockpit3DGlowIntensity * ActiveSettings.Bloom, Cockpit3DGlowStrength,
			Cockpit3DGlowThreshold, Cockpit3DScreenBrightness, Cockpit3DClearcoat, Cockpit3DGlowWide,
			Cockpit3DReflections, Cockpit3DCarbonSpecular,
			new CockpitLightIntensities(Cockpit3DBoostFrontLight, Cockpit3DBoostRearLight, Cockpit3DDamageLight),
			BuildIndustrialLampSettings());
	}

	// Setas y balizas de HAMMER: las de la cabina 3D y las del techo del casco 3D comparten ajustes
	public IndustrialLampSettings BuildIndustrialLampSettings() =>
		new(HammerSetaFull, HammerSetaCooldown, HammerSetaLocked, HammerSetaEmpty,
			HammerBoostBeaconOn, HammerBoostBeaconPulse, HammerDamageBeaconOn, HammerBeaconOff);

	// La sacudida da los valores de la cámara exterior; en cabina se escalan. Se pasa a las dos
	// cámaras, así cambiar de vista (C) durante la sacudida sigue con la amplitud de la nueva.
	// El temblor del roce solo va en cabina, encima de la sacudida.
	private void UpdateShake(float dt)
	{
		_shake.Update(dt);
		var offset = new Vector2(_shake.Offset.X, _shake.Offset.Y);
		_chaseCam.SetShake(offset, _shake.Roll);
		System.Numerics.Vector2 rumble = CrashFeel.RumbleOffset(ActiveSettings.CockpitShake);
		_cockpit.SetShake(offset * CockpitShakeFactor + new Vector2(rumble.X, rumble.Y), _shake.Roll * CockpitShakeFactor);
		_cockpit.SetCrashLight(CrashFeel.Light(BarrierSide.Left), CrashFeel.Light(BarrierSide.Right), CrashLightEnergy, CrashLightTint,
			CrashFeel.Light(CrashLightSide.Front), CrashFeel.Light(CrashLightSide.Rear), CrashEndLightEnergy);
	}

	private CockpitCrashFeel CrashFeel => _crashFeel ??= new CockpitCrashFeel(new CockpitCrashFeelSettings
	{
		RumbleAmplitude = CrashRumbleAmplitude,
		RumbleFrequency = CrashRumbleFrequency,
		RearGrazeRumble = CrashRearGrazeRumble
	}, new System.Random());

	// Contactos de este paso para la luz y el temblor de la cabina: los marca Main (barrera y otras naves) y luego
	// llama a StepCrashFeel
	public void FeelBarrier(in BarrierTouch touch)
	{
		if (touch.Contact)
			CrashFeel.Contact(touch.Side, touch.Level, touch.IsNewImpact);
	}

	// Contacto con otra nave en point (planta): el lado sale de dónde queda respecto a tu nave
	// Un roce por detrás solo tiembla, más suave; un golpe por detrás (facing Rear) se enciende delante o detrás según
	// quede el punto, y uno de lado, en ese lado.
	public void FeelShip(System.Numerics.Vector2 point, ImpactLevel level, bool isNewImpact, GrazeKind graze, GrazeKind facing)
	{
		bool impact = isNewImpact && level != ImpactLevel.Graze;
		if (!impact)
		{
			if (graze == GrazeKind.Rear)
				CrashFeel.RearGraze();
			else if (graze == GrazeKind.Side)
				CrashFeel.Contact(SideOf(point), ImpactLevel.Graze, false);
			return;
		}
		if (facing == GrazeKind.Rear)
		{
			var rel = new NVec2(point.X - GlobalPosition.X, point.Y - GlobalPosition.Z);
			CrashLightSide end = NVec2.Dot(rel, Dynamics.Forward) >= 0f ? CrashLightSide.Front : CrashLightSide.Rear;
			CrashFeel.Contact(end, level, true);
			return;
		}
		CrashFeel.Contact(SideOf(point), level, true);
	}

	private BarrierSide SideOf(System.Numerics.Vector2 point)
	{
		NVec2 forward = Dynamics.Forward;
		var right = new NVec2(-forward.Y, forward.X);
		var rel = new NVec2(point.X - GlobalPosition.X, point.Y - GlobalPosition.Z);
		return NVec2.Dot(rel, right) >= 0f ? BarrierSide.Right : BarrierSide.Left;
	}

	public void StepCrashFeel(float dt) => CrashFeel.Step(dt);
}
