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
// W / Flecha arriba: acelerar    S / Flecha abajo: frenar (también corta el impulso del boost)
// A,D / Flechas izq.-der.: girar Espacio (acción "boost"): gastar una carga de boost
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
	[Export] public float MaxBank = 0.6f;        // inclinación visual (rad) al girar
	[Export] public float LateralGrip = 5f;      // más alto = menos inercia lateral (la nave se "agarra" antes)

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
	[Export] public float FlickerMinIntervalLevel1 = 1.5f;   // segundos entre parpadeos de una arista (aleatorio entre mínimo y máximo)
	[Export] public float FlickerMaxIntervalLevel1 = 3f;
	[Export] public float FlickerMinIntervalLevel2 = 0.8f;
	[Export] public float FlickerMaxIntervalLevel2 = 1.6f;
	[Export] public float FlickerMinIntervalLevel3 = 0.3f;
	[Export] public float FlickerMaxIntervalLevel3 = 0.8f;
	[Export] public float FlickerMinDuration = 0.06f;        // segundos que dura un parpadeo
	[Export] public float FlickerMaxDuration = 0.12f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlickerOpacity = 0.2f;   // opacidad de la arista apagada

	[ExportGroup("Cámara de tercera persona")]
	[Export] public float CameraFollow = 6f;     // más alto = cámara más pegada, menos efecto dron
	[Export] public float CameraHeight = 1.8f;
	[Export] public float CameraDistance = 7f;
	[Export] public float CameraExtraDistance = 3f; // distancia extra a máxima velocidad
	[Export] public float BaseFov = 75f;
	[Export] public float MaxFov = 95f;

	private readonly ShipDynamics _dynamics = new();
	private readonly ShipBoost _boost = new();
	private readonly BarrierCollision _collision = new();
	private readonly ImpactRecovery _recovery = new();
	private CameraShake _shake;
	private Spaceship _ship;
	private Cockpit _cockpit;
	private ChaseCamera _chaseCam;
	private Track _track;
	private RaceSession _race;

	private bool _chaseView = true;
	private float _bank = 0f;
	private bool _inputBlocked;

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
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings());
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
	public void Launch(float speed)
	{
		_dynamics.Reset(_dynamics.Yaw, speed);
	}

	// Coloca la nave en una posición y orientación (yaw en radianes), parada o avanzando ya a initialSpeed m/s
	public void PlaceAt(Vector3 position, float yaw, float initialSpeed = 0f)
	{
		GlobalPosition = position;
		_dynamics.Reset(yaw, initialSpeed);
		_boost.Reset(BuildBoostTuning());
		_bank = 0f;
		_collision.Reset();
		_recovery.Cancel();
		Rotation = new Vector3(0f, yaw, 0f);
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings());
	}

	// Recolocación automática (atasco o fuera de pista): al instante en la posición dada, parada y
	// mirando según yaw. El boost no cambia; se cancela la corrección de un impacto fuerte.
	public void Respawn(Vector3 position, float yaw)
	{
		GlobalPosition = position;
		_dynamics.Reset(yaw, 0f);
		_bank = 0f;
		_collision.Reset();
		_recovery.Cancel();
		LastBarrierTouch = default;
		Rotation = new Vector3(0f, yaw, 0f);
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings());
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
			if (input.Accelerate || input.Brake || input.SteerLeft || input.SteerRight || Input.IsActionPressed("boost"))
				input = default;
			else
				_inputBlocked = false;
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

		// Tras un impacto fuerte, el giro y la aceleración van reducidos un momento
		_recovery.Update(dt);
		_dynamics.Step(input, BuildTuning(), dt, boostGain, _boost.IsImpulseActive, _recovery.Factor);
		Rotation = new Vector3(0f, _dynamics.Yaw, 0f);

		// 2. Mover el cuerpo en Godot con la velocidad real
		Velocity = new Vector3(_dynamics.Velocity.X, 0f, _dynamics.Velocity.Y);
		MoveAndSlide();
		_dynamics.SetVelocity(new NVec2(Velocity.X, Velocity.Z));

		// 3. Barreras: contacto, roce o rebote
		ApplyBarriers(dt);

		// 4. Si choca contra algo, el motor también pierde velocidad
		if (GetSlideCollisionCount() > 0)
			_dynamics.AbsorbCollision();

		// 5. Inclinación visual de la nave al girar
		float bankTarget = _dynamics.Steer * MaxBank * _dynamics.TurnFactor;
		_bank = Mathf.Lerp(_bank, bankTarget, 1f - Mathf.Exp(-8f * dt));
		var roll = new Vector3(0f, 0f, _bank);
		_ship.Rotation = roll;
		_cockpit.Rotation = roll;

		// 6. Sacudida (más suave en cabina) y cámara de tercera persona
		UpdateShake(dt);
		_chaseCam.UpdateFollow(dt, GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings());

		// 7. Carrera: cronómetro, checkpoints, meta y vueltas
		_race?.Update(GlobalPosition, dt);
	}

	// Traduce el teclado a intenciones de conducción
	private static ShipInput ReadInput()
	{
		return new ShipInput
		{
			Accelerate = Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up),
			Brake = Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down),
			SteerLeft = Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left),
			SteerRight = Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)
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
			MaxFov = MaxFov
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

	// La sacudida da los valores de la cámara exterior; en cabina se escalan. Se pasa a las dos
	// cámaras, así cambiar de vista (C) durante la sacudida sigue con la amplitud de la nueva.
	private void UpdateShake(float dt)
	{
		_shake.Update(dt);
		var offset = new Vector2(_shake.Offset.X, _shake.Offset.Y);
		_chaseCam.SetShake(offset, _shake.Roll);
		_cockpit.SetShake(offset * CockpitShakeFactor, _shake.Roll * CockpitShakeFactor);
	}

	// Barreras (BarrierCollision): con la posición ya movida, se mira el desplazamiento lateral respecto
	// al punto más cercano del eje. Si toca, la nave vuelve justo al límite (solo la parte lateral de la
	// posición) y se aplican el roce o el rebote a la velocidad, la dirección y la velocidad del motor.
	// El boost no se toca: las pérdidas se aplican sobre la velocidad que lleve, también con impulso.
	private void ApplyBarriers(float dt)
	{
		if (_track == null || !_track.IsBuilt)
		{
			LastBarrierTouch = default;
			return;
		}

		AxisPoint axis = _track.ProjectOnAxis(GlobalPosition);
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

		GlobalPosition += axis.Right * (r.Lateral - axis.Lateral);

		NVec2 newVelocity = tangent * r.VelocityAlong + right * r.VelocityLateral;
		NVec2 newForward = tangent * r.HeadingAlong + right * r.HeadingLateral;
		float yaw = Mathf.Atan2(-newForward.X, -newForward.Y);
		_dynamics.ApplyImpact(newVelocity, yaw, r.SpeedScale, r.SpeedLoss);
		Rotation = new Vector3(0f, _dynamics.Yaw, 0f);

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
