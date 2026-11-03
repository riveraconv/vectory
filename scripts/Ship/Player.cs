using Godot;
using Vectory.Circuit;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.Ship;

// Nave del jugador. Este nodo solo CONECTA piezas: lee el teclado, pasa la entrada al modelo
// de conducción (ShipDynamics), mueve el cuerpo en Godot, aplica las barreras (BarrierCollision,
// con la corrección tras un impacto fuerte en ImpactRecovery), actualiza la nave visual y las cámaras
// (con la sacudida de CameraShake), y le dice a la sesión de carrera (RaceSession) dónde está para
// contar vueltas. El boost por cargas vive en ShipBoost. Las reglas viven en las clases puras.
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

	// Distancia del origen de la nave a su morro (borde delantero de la caja de colisión)
	public const float NoseDistance = 4f;

	// --- Ajustes de conducción: se editan en el Inspector (agrupados) ---
	[ExportGroup("Velocidad")]
	[Export] public float MaxSpeed = 60f;
	[Export] public float Acceleration = 25f;
	[Export] public float BrakeDeceleration = 45f;
	[Export] public float Drag = 8f;

	[ExportGroup("Boost")]
	[Export] public int BoostMaxCharges = 3;
	[Export] public int BoostInitialCharges = 0;
	[Export] public float BoostRechargeTime = 35f;   // segundos para ganar una carga
	[Export] public float BoostSpeedGain = 100f;     // km/h que suma cada carga
	[Export] public float BoostRampTime = 0.5f;      // segundos de subida (en los que se reparte el impulso)
	[Export] public float BoostHoldTime = 2f;        // segundos que se mantiene la velocidad alcanzada
	[Export] public float BoostCooldown = 10f;       // segundos entre usos
	[Export] public float OverspeedDecay = 20f;      // km/h que pierde cada segundo por encima de MaxSpeed

	[ExportGroup("Dirección")]
	[Export] public float TurnRate = 1.6f;       // rad/s de giro máximo
	[Export] public float SteerResponse = 6f;    // lo rápido que llega el giro a su valor máximo
	[Export] public float LateralGrip = 5f;      // más alto = menos inercia lateral (la nave se "agarra" antes)

	[ExportGroup("Derrape")]
	[Export] public float DriftEnterSpeed = 120f;      // km/h mínimos para entrar en derrape
	[Export] public float DriftExitSpeed = 60f;        // km/h por debajo de los que se sale
	[Export(PropertyHint.Range, "0,1,0.01")] public float DriftGrip = 0f;                 // agarre lateral en derrape
	[Export(PropertyHint.Range, "0,1,0.01")] public float DriftCountersteerGrip = 0.5f;   // agarre contravolanteando
	[Export] public float DriftTurnMultiplier = 1.4f;  // giro del rumbo en derrape
	[Export] public float DriftFixedLoss = 5f;         // km/h que se pierden cada segundo derrapando
	[Export] public float DriftSlipLoss = 10f;         // km/h por segundo extra al ángulo máximo
	[Export(PropertyHint.Range, "0,1,0.01")] public float DriftAcceleration = 0.5f;       // aceleración con W en derrape
	[Export] public float DriftMaxSlipAngle = 60f;     // grados de deslizamiento máximo
	[Export] public float DriftGripRecoveryTime = 3f;  // segundos para volver al agarre normal al salir

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
	[Export] public float Gravity = 14f;                 // m/s², hacia abajo en el mundo (ajustado en pista; la spec proponía 6)
	[Export(PropertyHint.Range, "0,1,0.01")] public float AirTurn = 0.3f;   // giro en el aire respecto al normal
	[Export] public float AirMaxPitch = 20f;             // grados de inclinación máxima del morro en el aire
	[Export] public float AirAttitudeSmoothing = 0.3f;   // s: suavizado de la orientación en el aire
	[Export] public float LandingAlignTime = 0.15f;      // s: suavizado para volver a la superficie al aterrizar
	[Export(PropertyHint.Range, "0,1,0.01")] public float LandingSpeedLoss = 0.1f;   // fracción de velocidad que se pierde al aterrizar
	[Export] public float LandingMediumSpeed = 3f;       // m/s de caída desde los que hay sacudida media
	[Export] public float LandingStrongSpeed = 8f;       // m/s de caída desde los que es fuerte
	[Export] public float LandingCompression = 0.3f;     // m que baja la nave al aterrizar (a intensidad máxima)
	[Export] public float LandingCompressionTime = 0.2f; // s en bajar y volver

	[ExportGroup("Pendiente y peralte")]
	[Export] public float GroundAttitudeSmoothing = 0.1f;   // s: suavizado de la orientación en el suelo
	[Export] public float SlopeReference = 10f;             // grados: pendiente a la que el efecto es completo
	[Export(PropertyHint.Range, "0,1,0.01")] public float SlopeEffectAmount = 0.3f;   // punta y aceleración: −30 % subiendo, +30 % bajando
	[Export(PropertyHint.Range, "0,1,0.01")] public float BankGripBonus = 0.4f;       // agarre extra con 25° de peralte a favor
	[Export(PropertyHint.Range, "0,1,0.01")] public float BankTurnBonus = 0.15f;      // giro extra con 25° de peralte a favor

	[ExportGroup("Colisiones")]
	[Export] public float CollisionHalfWidth = 3.4f;   // semiancho de colisión con la barrera (m; las puntas de las alas)
	[Export] public float ImpactMediumSpeed = 30f;     // km/h contra la barrera desde los que el impacto es medio
	[Export] public float ImpactStrongSpeed = 90f;     // km/h contra la barrera desde los que el impacto es fuerte
	[Export(PropertyHint.Range, "0,1,0.01")] public float GrazeStraighten = 0.3f;   // fracción del ángulo que se quita al empezar un roce
	[Export(PropertyHint.Range, "0,1,0.01")] public float GrazeEntryLoss = 0.04f;   // fracción de velocidad que se pierde al empezar un roce
	[Export] public float GrazeFriction = 20f;         // km/h que se pierden cada segundo mientras roza
	[Export(PropertyHint.Range, "0,1,0.01")] public float MediumRestitution = 0.4f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float StrongRestitution = 0.5f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float MediumSpeedLoss = 0.2f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float StrongSpeedLoss = 0.45f;
	[Export] public float RecoveryDuration = 1f;       // segundos de control reducido tras un impacto fuerte
	[Export(PropertyHint.Range, "0,1,0.01")] public float RecoveryControl = 0.5f;   // giro y aceleración al empezar la corrección
	[Export] public float ImpactCooldown = 0.3f;       // segundos sin contar otro impacto medio o fuerte en el mismo lado

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

	[ExportGroup("Cámara de tercera persona")]
	[Export] public float CameraFollow = 6f;     // más alto = cámara más pegada, menos efecto dron
	[Export] public float CameraHeight = 1.8f;
	[Export] public float CameraDistance = 7f;
	[Export] public float CameraExtraDistance = 3f; // distancia extra a máxima velocidad
	[Export] public float BaseFov = 75f;
	[Export] public float MaxFov = 95f;
	[Export] public float CameraUpSmoothing = 0.3f;   // s: el "arriba" de la cámara sigue al de la superficie (y al del mundo en el aire)

	// Pista 3D: peralte de referencia y tope del agarre por peralte (spec 5.2)
	private const float BankReferenceDegrees = 25f;
	private const float BankMaxDegrees = 35f;

	private readonly ShipDynamics _dynamics = new();
	private readonly ShipBoost _boost = new();
	private readonly BarrierCollision _collision = new();
	private readonly ImpactRecovery _recovery = new();
	private readonly ShipBank _bank = new();
	private readonly CockpitBank _cockpitBank = new();
	private readonly CameraVibration _vibration = new();
	private readonly BoostCameraEffect _boostCamera = new();
	private readonly CameraEffectSettings _cameraEffects = new();
	private readonly ShipVertical _vertical = new();
	private readonly ShipAttitude _attitude = new();
	private readonly LandingCompression _compression = new();
	private CameraShake _shake;
	private Spaceship _ship;
	private Cockpit _cockpit;
	private ChaseCamera _chaseCam;
	private Track _track;
	private RaceSession _race;

	private bool _chaseView = true;
	private bool _inputBlocked;

	// Superficie de la pista bajo la nave en el último paso (sin circuito: ninguna, la nave no sube ni baja)
	private AxisPoint _axis;
	private bool _hasAxis;

	// Parpadeo de las aristas dañadas de la nave y de la cabina (null hasta que hay daño)
	private EdgeFlicker _shipFlicker;
	private EdgeFlicker _cockpitFlicker;

	// Factores de la velocidad punta y del giro (los daños los bajan; 1 = sin efecto).
	// Se aplican sobre MaxSpeed y TurnRate al pasar los parámetros al modelo, sin cambiarlos.
	public float TopSpeedFactor { get; set; } = 1f;
	public float TurnRateFactor { get; set; } = 1f;

	// Si se puede activar el boost (los daños de nivel 3 lo bloquean): las cargas se siguen ganando
	// y un impulso en curso no se corta
	public bool BoostAllowed
	{
		get => _boost.ActivationAllowed;
		set => _boost.ActivationAllowed = value;
	}

	// Velocidad real actual en m/s (la usa el HUD)
	public float Speed => _dynamics.RealSpeed;

	// Contacto con la barrera en el último paso de física (lo leen Main para el destello y la recolocación)
	public BarrierTouch LastBarrierTouch { get; private set; }

	// Velocidad máxima con un boost desde la punta (m/s): tope de la barra de velocidad del HUD
	public float BoostedTopSpeed => MaxSpeed + BoostSpeedGain / KmhPerMs;

	// Cargas de boost disponibles y si el cooldown está activo (las lee el HUD junto con BoostMaxCharges)
	public int BoostCharges => _boost.Charges;
	public bool BoostCooldownActive => _boost.IsCooldownActive;
	public bool BoostBlocked => !_boost.ActivationAllowed;

	// La nave va por el aire (tras despegar en una cresta): el atasco no cuenta
	public bool IsAirborne => _vertical.Airborne;

	// Rotación de la nave visible en el mundo (rumbo + inclinación al girar): la graba el fantasma
	public Quaternion ShipRotation => _ship.GlobalBasis.GetRotationQuaternion();

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;

		AddChild(new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Shape = new BoxShape3D { Size = new Vector3(6.8f, 1.2f, 7.2f) },
			Position = new Vector3(0f, 0.2f, -0.4f)
		});

		_ship = new Spaceship { Name = "Spaceship" };
		AddChild(_ship);

		_cockpit = new Cockpit { Name = "Cockpit" };
		AddChild(_cockpit);

		_chaseCam = new ChaseCamera { Name = "ChaseCamera" };
		AddChild(_chaseCam);

		_dynamics.Reset(0f);
		_boost.Reset(BuildBoostTuning());
		_shake = new CameraShake(BuildShakeSettings());
		_vertical.Reset(GlobalPosition.Y);
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings(), Vector3.Up);
		ApplyView();
	}

	// Le dice al jugador sobre qué circuito conducir (sin circuito no hay límites)
	public void SetTrack(Track track)
	{
		_track = track;
	}

	// Carrera en la que participa: la nave le pasa su posición cada paso y le pregunta si puede conducir
	public void SetRaceSession(RaceSession race)
	{
		_race = race;
	}

	// Salida lanzada (GO! de TIME TRIAL): la nave pasa de golpe a "speed" m/s hacia donde apunta
	// (a lo largo de la superficie: sigue en el suelo)
	public void Launch(float speed)
	{
		_dynamics.Reset(_dynamics.Yaw, speed);
	}

	// Coloca la nave en una posición y orientación (yaw en radianes), parada o avanzando ya a initialSpeed m/s.
	// Queda en el suelo, alineada con la superficie.
	public void PlaceAt(Vector3 position, float yaw, float initialSpeed = 0f)
	{
		GlobalPosition = position;
		_dynamics.Reset(yaw, initialSpeed);
		_boost.Reset(BuildBoostTuning());
		_bank.Reset();
		_collision.Reset();
		_recovery.Cancel();
		ResetCameraEffects();
		SettleOnSurface();
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings(), SurfaceUp());
	}

	// Recolocación automática (atasco o fuera de pista): al instante en la posición dada, parada,
	// mirando según yaw, en el suelo y alineada con la superficie (si estaba en el aire, se cancela el vuelo).
	// El boost no cambia; se cancela la corrección de un impacto fuerte.
	public void Respawn(Vector3 position, float yaw)
	{
		GlobalPosition = position;
		_dynamics.Reset(yaw, 0f);
		_bank.Reset();
		_collision.Reset();
		_recovery.Cancel();
		ResetCameraEffects();
		LastBarrierTouch = default;
		SettleOnSurface();
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings(), SurfaceUp());
	}

	// En el suelo a la altura actual, sin velocidad vertical, con la orientación de la superficie al momento
	private void SettleOnSurface()
	{
		_vertical.Reset(GlobalPosition.Y);
		_compression.Reset();
		_hasAxis = _track != null && _track.IsBuilt;
		if (_hasAxis)
		{
			_axis = _track.ProjectOnAxis(GlobalPosition);
			ShipAttitude.SurfaceAngles(ToNumerics(_axis.Normal), _dynamics.Yaw, out float pitch, out float roll);
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
		if (level <= 0 && _shipFlicker == null)
			return;

		if (_shipFlicker == null)
		{
			var random = new System.Random();
			_shipFlicker = new EdgeFlicker(_ship.EdgeCount, BuildFlickerSettings(), random);
			_cockpitFlicker = new EdgeFlicker(_cockpit.EdgeCount, BuildFlickerSettings(), random);
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
		}
	}

	private void ApplyView()
	{
		_ship.SetExteriorVisible(_chaseView);
		_cockpit.SetFrameVisible(!_chaseView);
		if (_chaseView)
			_chaseCam.MakeCurrent();
		else
			_cockpit.Camera.MakeCurrent();
	}

	// Parpadeo de las aristas dañadas (con la pausa no avanza: queda congelado)
	public override void _Process(double delta)
	{
		if (_shipFlicker == null)
			return;

		float dt = (float)delta;
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
		ShipInput input = canDrive ? ReadInput() : default;

		// Tras la pausa, nada cuenta hasta que se suelten las teclas que siguieran pulsadas
		if (_inputBlocked)
		{
			if (input.Accelerate || input.Brake || input.SteerLeft || input.SteerRight || input.Drift || Input.IsActionPressed("boost"))
				input = default;
			else
				_inputBlocked = false;
		}

		// En el aire, W y S no hacen nada (tampoco cortan el boost)
		bool airborne = _vertical.Airborne;
		if (airborne)
		{
			input.Accelerate = false;
			input.Brake = false;
		}

		float boostGain = 0f;
		if (canDrive)
		{
			BoostTuning boostTuning = BuildBoostTuning();
			if (!_inputBlocked && Input.IsActionJustPressed("boost"))
				_boost.TryActivate(boostTuning);
			if (input.Brake)
				_boost.CancelImpulse();
			boostGain = _boost.Update(boostTuning, dt);
		}
		else
		{
			_boost.CancelImpulse();
		}

		// Tras un impacto fuerte, el giro y la aceleración van reducidos un momento.
		// En el suelo, la pendiente cambia la punta y la aceleración, y el peralte el agarre y el giro
		// (en llano los factores son 1); en el aire, sin W/S ni agarre y con el giro reducido.
		_recovery.Update(dt);
		ShipTuning baseTuning = BuildTuning();
		ShipTuning tuning = airborne ? baseTuning : ApplyTrackSurface(baseTuning);
		if (airborne)
			_dynamics.StepAirborne(input, tuning, dt, boostGain, AirTurn, _recovery.Factor);
		else
			_dynamics.Step(input, tuning, BuildDriftTuning(), dt, boostGain, _boost.IsImpulseActive, _recovery.Factor);
		ApplyBodyRotation();

		// 2. Mover el cuerpo en Godot con la velocidad real (en planta; la altura la pone la capa vertical)
		Velocity = new Vector3(_dynamics.Velocity.X, 0f, _dynamics.Velocity.Y);
		MoveAndSlide();
		_dynamics.SetVelocity(new NVec2(Velocity.X, Velocity.Z));

		// 3. Barreras: contacto, roce o rebote (también en el aire: la barrera es infinitamente alta)
		UpdateSurface();
		ApplyBarriers(dt);

		// 4. Si choca contra algo, el motor también pierde velocidad
		if (GetSlideCollisionCount() > 0)
			_dynamics.AbsorbCollision();

		// 5. Capa vertical: pegada a la superficie, despegue en crestas, vuelo y aterrizaje
		UpdateVertical(dt);

		// 6. Orientación con la pista e inclinación visual de la nave al girar (solo la geometría exterior)
		float speedRatio = baseTuning.MaxSpeed > 0f ? _dynamics.RealSpeed / baseTuning.MaxSpeed : 0f;
		BankSettings bankSettings = BuildBankSettings();
		_bank.Update(_dynamics.Steer, speedRatio, _dynamics.Drifting, bankSettings, dt);
		UpdateAttitude(dt);
		_compression.Update(dt);
		ApplyShipVisual(_bank.Angle);

		// 7. Efectos de cámara, sacudida (más suave en cabina) y cámara de tercera persona
		float shipBankTarget = ShipBank.Target(_dynamics.Steer, speedRatio, _dynamics.Drifting, bankSettings);
		UpdateCameraEffects(shipBankTarget, baseTuning.MaxSpeed, dt);
		UpdateShake(dt);
		_chaseCam.UpdateFollow(dt, GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings(), SurfaceUp());

		// 8. Carrera: cronómetro, checkpoints, meta y vueltas
		_race?.Update(GlobalPosition, dt);
	}

	// ---------------------------------------------------------------
	// Pista 3D
	// ---------------------------------------------------------------

	// Superficie bajo la nave en su posición actual (en planta)
	private void UpdateSurface()
	{
		_hasAxis = _track != null && _track.IsBuilt;
		if (_hasAxis)
			_axis = _track.ProjectOnAxis(GlobalPosition);
	}

	// Pendiente (punta y aceleración) y peralte (agarre y giro) de la superficie bajo la nave, sobre los
	// parámetros que ya llevan el daño. La pendiente es la del eje en el sentido de la marcha (según hacia
	// dónde apunta la nave respecto al eje); el peralte cuenta según el giro actual.
	private ShipTuning ApplyTrackSurface(ShipTuning t)
	{
		if (!_hasAxis)
			return t;

		var tangent = new NVec2(_axis.Tangent.X, _axis.Tangent.Z);
		float direction = NVec2.Dot(_dynamics.Forward, tangent) >= 0f ? 1f : -1f;
		float slope = direction * Mathf.Atan(_axis.Slope);
		float slopeFactor = SlopeEffect.Factor(slope, new SlopeSettings
		{
			ReferenceAngle = Mathf.DegToRad(SlopeReference),
			Effect = SlopeEffectAmount
		});
		t.MaxSpeed *= slopeFactor;
		t.Acceleration *= slopeFactor;

		var bankSettings = new BankGripSettings
		{
			GripBonus = BankGripBonus,
			TurnBonus = BankTurnBonus,
			ReferenceBank = Mathf.DegToRad(BankReferenceDegrees),
			MaxBank = Mathf.DegToRad(BankMaxDegrees)
		};
		t.LateralGrip *= BankGrip.GripFactor(_axis.Bank, _dynamics.Steer, true, bankSettings);
		t.TurnRate *= BankGrip.TurnFactor(_axis.Bank, _dynamics.Steer, true, bankSettings);
		return t;
	}

	// Altura de la nave: en el suelo, a la altura de vuelo sobre la superficie; en el aire, con la gravedad.
	// Al aterrizar: velocidad contra la superficie fuera (y la pérdida, si la hay), sacudida y compresión
	// según la velocidad de caída. Sin daño ni impacto contra la barrera.
	private void UpdateVertical(float dt)
	{
		if (!_hasAxis)
			return;

		var surface = new SurfaceSample(_axis.SurfaceY, ToNumerics(_axis.Normal),
			new NVec2(_axis.Tangent.X, _axis.Tangent.Z), _axis.Curvature);
		NVec2 velocity = _vertical.Update(dt, surface, _dynamics.Velocity, new VerticalSettings
		{
			Gravity = Gravity,
			Hover = Track.ShipHover
		});

		if (_vertical.Landed)
		{
			float keep = 1f - Mathf.Clamp(LandingSpeedLoss, 0f, 1f);
			_dynamics.SetVelocity(velocity * keep);
			if (keep < 1f)
				_dynamics.ReduceSpeed(_dynamics.Speed * (1f - keep));

			LandingSettings landing = BuildLandingSettings();
			LandingLevel level = LandingImpact.Level(_vertical.LandingSpeed, landing);
			if (level != LandingLevel.None)
				ShakeCamera(level == LandingLevel.Strong ? ImpactLevel.Strong : ImpactLevel.Medium);
			_compression.Trigger(LandingImpact.Intensity(_vertical.LandingSpeed, landing));
		}

		Vector3 p = GlobalPosition;
		GlobalPosition = new Vector3(p.X, _vertical.Height, p.Z);
	}

	// Cabeceo y ladeo con la superficie (en el suelo) o hacia el movimiento (en el aire), suavizados
	private void UpdateAttitude(float dt)
	{
		if (!_hasAxis)
			return;

		_attitude.Update(_vertical.Grounded, _vertical.Landed, ToNumerics(_axis.Normal), _dynamics.Yaw,
			_vertical.VerticalSpeed, _dynamics.RealSpeed, new AttitudeSettings
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
		Rotation = new Vector3(_attitude.Pitch, _dynamics.Yaw, _attitude.Roll);
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
		return _hasAxis && _vertical.Grounded ? _axis.Normal : Vector3.Up;
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

	// Copia los parámetros del Inspector al modelo (cada paso, así los cambios en vivo se notan)
	private ShipTuning BuildTuning()
	{
		return new ShipTuning
		{
			MaxSpeed = MaxSpeed * TopSpeedFactor,
			Acceleration = Acceleration,
			BrakeDeceleration = BrakeDeceleration,
			Drag = Drag,
			OverspeedDecay = OverspeedDecay / KmhPerMs,
			TurnRate = TurnRate * TurnRateFactor,
			SteerResponse = SteerResponse,
			LateralGrip = LateralGrip
		};
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
		return new ChaseCameraSettings
		{
			FollowRate = CameraFollow,
			Height = CameraHeight,
			Distance = CameraDistance,
			ExtraDistance = CameraExtraDistance,
			BaseFov = BaseFov,
			MaxFov = MaxFov,
			UpSmoothTime = CameraUpSmoothing
		};
	}

	// 0 parada ... 1 a velocidad máxima con boost (para alejar la cámara y abrir el FOV)
	private float SpeedFactor()
	{
		return Mathf.Clamp(_dynamics.Speed / BoostedTopSpeed, 0f, 1f);
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
		bool boosting = _boost.IsImpulseActive;
		_cockpitBank.Update(shipBankTarget, s, dt);
		_vibration.Update(_dynamics.RealSpeed, topSpeed, boosting, s, dt);
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

	// Barreras (BarrierCollision): con la posición ya movida, se mira el desplazamiento lateral sobre la
	// superficie respecto al punto del eje bajo la nave. Si toca, la nave vuelve justo al límite (solo la
	// parte lateral de la posición, a lo largo del lado de la superficie) y se aplican el roce o el rebote
	// a la velocidad, la dirección y la velocidad del motor. Igual en el aire.
	// El boost no se toca: las pérdidas se aplican sobre la velocidad que lleve, también con impulso.
	private void ApplyBarriers(float dt)
	{
		if (!_hasAxis)
		{
			LastBarrierTouch = default;
			return;
		}

		AxisPoint axis = _axis;
		var tangent = new NVec2(axis.Tangent.X, axis.Tangent.Z);
		var right = new NVec2(axis.Right.X, axis.Right.Z);
		NVec2 velocity = _dynamics.Velocity;
		NVec2 forward = _dynamics.Forward;

		var input = new BarrierContactInput
		{
			Lateral = axis.Lateral,
			HalfWidth = CollisionHalfWidth,
			BarrierOffset = _track.BarrierOffset,
			VelocityAlong = NVec2.Dot(velocity, tangent),
			VelocityLateral = NVec2.Dot(velocity, right),
			HeadingAlong = NVec2.Dot(forward, tangent),
			HeadingLateral = NVec2.Dot(forward, right)
		};

		BarrierContactResult r = _collision.Resolve(input, BuildCollisionSettings(), dt);
		LastBarrierTouch = new BarrierTouch(r.Contact, r.Side, r.Level, r.IsNewImpact, axis.Distance, axis.Lateral);

		if (!r.Contact)
			return;

		// En planta, el lateral sobre la superficie avanza a lo largo de la parte horizontal de su lado (en llano, Right)
		var side = new Vector3(axis.SurfaceRight.X, 0f, axis.SurfaceRight.Z);
		GlobalPosition += side * (r.Lateral - axis.Lateral);
		UpdateSurface();

		NVec2 newVelocity = tangent * r.VelocityAlong + right * r.VelocityLateral;
		NVec2 newForward = tangent * r.HeadingAlong + right * r.HeadingLateral;
		float yaw = Mathf.Atan2(-newForward.X, -newForward.Y);
		_dynamics.ApplyImpact(newVelocity, yaw, r.SpeedScale, r.SpeedLoss);
		ApplyBodyRotation();

		// Un impacto medio o fuerte corta el derrape (el roce no)
		if (r.IsNewImpact && r.Level != ImpactLevel.Graze)
			_dynamics.EndDrift();

		if (r.IsNewImpact && r.Level == ImpactLevel.Strong)
		{
			_recovery.Duration = RecoveryDuration;
			_recovery.MinFactor = RecoveryControl;
			_recovery.Start();
		}
	}
}

// Contacto de la nave con la barrera en un paso de física: lado, nivel, si es un impacto medio o fuerte
// que cuenta, punto del eje (m desde la meta) y desplazamiento lateral antes de corregirlo
public readonly record struct BarrierTouch(
	bool Contact, BarrierSide Side, ImpactLevel Level, bool IsNewImpact, float AxisDistance, float Lateral);
