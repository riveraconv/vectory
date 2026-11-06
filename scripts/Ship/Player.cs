using Godot;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Game;
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
public partial class Player : CharacterBody3D
{
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

	[ExportGroup("Sacudida de cámara")]
	[Export] public float MediumShakeAmplitude = 0.06f;   // m (cámara exterior)
	[Export] public float MediumShakeRoll = 0.8f;         // grados
	[Export] public float MediumShakeDuration = 0.25f;
	[Export] public float StrongShakeAmplitude = 0.15f;
	[Export] public float StrongShakeRoll = 2f;
	[Export] public float StrongShakeDuration = 0.4f;
	[Export] public float ShakeFrequency = 25f;           // Hz de la vibración
	[Export(PropertyHint.Range, "0,1,0.01")] public float CockpitShakeFactor = 0.5f;   // en cabina, todo a este factor

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

	[ExportGroup("Cabina 3D")]
	[Export] public float Cockpit3DFov = 70f;                 // grados, en vertical: no cambia con los efectos del mundo
	[Export] public float Cockpit3DSway = 0.03f;              // m de balanceo lateral al girar
	[Export] public float Cockpit3DVibrationPosition = 1.6f;
	[Export] public float Cockpit3DVibrationRotation = 0.35f;
	[Export] public float Cockpit3DCounterRoll = 0.25f;       // contragiro respecto a lo que se ladea el mundo
	[Export] public float Cockpit3DBoostPush = 0.05f;         // m hacia atrás con el boost
	[Export] public float Cockpit3DKeyEnergy = 0.83f;
	[Export] public float Cockpit3DAmbientEnergy = 0.32f;
	[Export] public float Cockpit3DLampEnergy = 0.025f;
	[Export] public float Cockpit3DLargeScreenLight = 0.095f;
	[Export] public float Cockpit3DSmallScreenLight = 0.064f;
	[Export] public float Cockpit3DGlowIntensity = 0.55f;
	[Export] public float Cockpit3DGlowStrength = 0.9f;
	[Export] public float Cockpit3DGlowThreshold = 0.8f;
	[Export] public float Cockpit3DScreenBrightness = 1.35f;

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
	private CameraShake _shake;
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

	private ShipDynamics Dynamics => _physics.Dynamics;
	private ShipBoost Boost => _physics.Boost;
	private ShipVertical Vertical => _physics.Vertical;

	// Parpadeo de las aristas dañadas de la nave y de la cabina (null hasta que hay daño)
	private EdgeFlicker _shipFlicker;
	private EdgeFlicker _cockpitFlicker;
	private readonly System.Random _flickerRandom = new();
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

	// El cuadro de mando enseña el daño (RACE). En TIME TRIAL no hay daños: se queda apagado.
	public bool CockpitDamageEnabled { get; set; }

	// Vista de cámara actual y su cambio con la tecla C
	public CameraView View => _chaseView ? CameraView.Chase : CameraView.Cockpit;
	public event System.Action<CameraView> ViewChanged;

	// Qué se enseña en la vista actual, según la cabina de la nave que se conduce
	public CockpitViewRules ViewRules => CockpitViewRules.For(View, _cockpit.Uses3D);

	// Rotación de la nave visible en el mundo (rumbo + inclinación al girar): la graba el fantasma
	public Quaternion ShipRotation => _ship.GlobalBasis.GetRotationQuaternion();

	// Hacia dónde apunta el morro, en planta (X, Z del mundo): el frente del radar y el minimapa
	public System.Numerics.Vector2 PlanForward => Dynamics.Forward;

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;

		AddChild(new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Shape = new BoxShape3D { Size = ShipLineMesh.ToGodot(Definition.Visual.PhysicsBoxSize) },
			Position = ShipLineMesh.ToGodot(Definition.Visual.PhysicsBoxCenter)
		});

		_ship = new Spaceship { Name = "Spaceship", Visual = Definition.Visual };
		AddChild(_ship);

		_cockpit = new Cockpit { Name = "Cockpit", Visual = Definition.Visual, Settings3D = BuildCockpit3DSettings() };
		AddChild(_cockpit);

		_chaseCam = new ChaseCamera { Name = "ChaseCamera" };
		AddChild(_chaseCam);

		_physics.Place(ToNumerics(GlobalPosition), 0f, 0f, BuildBoostTuning());
		_shake = new CameraShake(BuildShakeSettings());
		_chaseCam.Snap(GlobalPosition, Dynamics.Yaw, SpeedFactor(), CameraSettings(), Vector3.Up);
		ApplyView();
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
		_cockpit.ShowEdgeFlicker(_cockpitFlicker, FlickerOpacity);
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
		_bank.Reset();
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
		_bank.Reset();
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
		_cockpit.ShowEdgeFlicker(_cockpitFlicker, FlickerOpacity);
	}

	// Ignora el input de conducción (y el boost) hasta que se suelten todas sus teclas.
	// Se usa al salir de la pausa: las teclas pulsadas en ella no deben llegar a la nave.
	public void BlockInputUntilReleased()
	{
		_inputBlocked = true;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.C })
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

		if (_shipFlicker == null)
			return;

		EdgeFlickerSettings settings = BuildFlickerSettings();
		_shipFlicker.Settings = settings;
		_cockpitFlicker.Settings = settings;
		if (_shipFlicker.Update(dt))
			_ship.ShowEdgeFlicker(_shipFlicker, FlickerOpacity);
		if (_cockpitFlicker.Update(dt))
			_cockpit.ShowEdgeFlicker(_cockpitFlicker, FlickerOpacity);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

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
			if (input.Accelerate || input.Brake || input.SteerLeft || input.SteerRight || input.Drift || Input.IsActionPressed("boost"))
				input = default;
			else
				_inputBlocked = false;
		}

		ShipTuning baseTuning = BuildTuning();
		ShipPhysicsSettings physics = BuildPhysicsSettings();
		bool boostPressed = !AutopilotActive && !_inputBlocked && Input.IsActionJustPressed("boost");
		_physics.BeginStep(input, boostPressed, canDrive, baseTuning, physics, dt);
		ApplyBodyRotation();

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
			Vertical.VerticalSpeed, Dynamics.RealSpeed, new AttitudeSettings
			{
				GroundSmoothTime = GroundAttitudeSmoothing,
				AirSmoothTime = AirAttitudeSmoothing,
				LandingSmoothTime = LandingAlignTime,
				AirMaxPitch = Mathf.DegToRad(AirMaxPitch)
			}, dt);
		ApplyBodyRotation();
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

	// Traduce el teclado a intenciones de conducción
	private static ShipInput ReadInput()
	{
		return new ShipInput
		{
			Accelerate = Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up),
			Brake = Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down),
			SteerLeft = Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left),
			SteerRight = Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right),
			Drift = Input.IsActionPressed("drift")
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
			DamageEnabled = CockpitDamageEnabled
		};
	}

	// El perfil técnico de la nave con los factores de los daños
	private ShipTuning BuildTuning()
	{
		ShipTuning tuning = Technical;
		tuning.MaxSpeed *= TopSpeedFactor;
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

	private BankSettings BuildBankSettings()
	{
		return new BankSettings
		{
			MaxNormal = Mathf.DegToRad(BankMaxNormal),
			MaxDrift = Mathf.DegToRad(BankMaxDrift),
			SmoothTime = BankSmoothing
		};
	}

	// Parámetros de la física de un paso, con los valores del Inspector (m/s y radianes)
	private ShipPhysicsSettings BuildPhysicsSettings()
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

	private EdgeFlickerSettings BuildFlickerSettings()
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
			MaxFov = MaxFov,
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

	// Copia los parámetros del Inspector al objeto de ajustes (ángulos a radianes)
	private CameraEffectSettings BuildCameraEffects()
	{
		CameraEffectSettings s = _cameraEffects;
		s.CockpitBankFactor = CockpitBankFactor;
		s.CockpitBankMax = Mathf.DegToRad(CockpitBankMax);
		s.CockpitBankSmoothTime = CockpitBankSmoothing;
		s.VibrationThreshold = VibrationThreshold;
		s.VibrationAmplitudeCockpit = VibrationAmplitudeCockpit;
		s.VibrationAmplitudeChase = VibrationAmplitudeChase;
		s.VibrationBoostMultiplier = VibrationBoostMultiplier;
		s.VibrationFrequency = VibrationFrequency;
		s.VibrationSmoothTime = VibrationSmoothing;
		s.BoostFovExtra = BoostFovExtra;
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
	}

	// Recolocación y colocación: todos los efectos a su valor base al momento
	private void ResetCameraEffects()
	{
		_cockpitBank.Reset();
		_vibration.Reset();
		_boostCamera.Reset();
		ApplyCameraEffects(BuildCameraEffects());
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
		var settings = new CockpitCameraSettings(Cockpit3DSway, Cockpit3DVibrationPosition, Cockpit3DVibrationRotation, Cockpit3DCounterRoll, Cockpit3DBoostPush);
		_cockpit.Update3D(CockpitCameraMotion.Compute(input, settings), BuildCockpit3DSettings());
	}

	private Cockpit3DSettings BuildCockpit3DSettings()
	{
		return new Cockpit3DSettings(Cockpit3DFov, Cockpit3DKeyEnergy, Cockpit3DAmbientEnergy, Cockpit3DLampEnergy,
			Cockpit3DLargeScreenLight, Cockpit3DSmallScreenLight, Cockpit3DGlowIntensity, Cockpit3DGlowStrength,
			Cockpit3DGlowThreshold, Cockpit3DScreenBrightness);
	}

	// La sacudida da los valores de la cámara exterior; en cabina se escalan. Se pasa a las dos
	// cámaras, así cambiar de vista (C) durante la sacudida sigue con la amplitud de la nueva.
	private void UpdateShake(float dt)
	{
		_shake.Update(dt);
		var offset = new Vector2(_shake.Offset.X, _shake.Offset.Y);
		_chaseCam.SetShake(offset, _shake.Roll);
		_cockpit.SetShake(offset * CockpitShakeFactor, _shake.Roll * CockpitShakeFactor);
	}
}
