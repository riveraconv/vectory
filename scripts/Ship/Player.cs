using Godot;
using Vectory.Circuit;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.Ship;

// Nave del jugador. Este nodo solo CONECTA piezas: lee el teclado, pasa la entrada al modelo
// de conducción (ShipDynamics), mueve el cuerpo en Godot, aplica los límites de la pista
// (TrackLimits), actualiza la nave visual y las cámaras, y le dice a la sesión de carrera
// (RaceSession) dónde está para contar vueltas. El boost por cargas vive en ShipBoost.
// Las reglas viven en las clases puras.
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

	[ExportGroup("Límites de pista")]
	[Export] public float LimitMargin = 3.4f;    // distancia que se respeta hasta el borde (media envergadura de la nave)
	[Export] public float WallBrake = 0.5f;      // velocidad que pierde por cada m/s del GOLPE inicial contra el límite (0 = no pierde nada)
	[Export] public float WallScrape = 4f;       // m/s que pierde cada segundo mientras roza el límite (0 = sin fricción; mejor que sea menor que Acceleration)

	[ExportGroup("Cámara de tercera persona")]
	[Export] public float CameraFollow = 6f;     // más alto = cámara más pegada, menos efecto dron
	[Export] public float CameraHeight = 1.8f;
	[Export] public float CameraDistance = 7f;
	[Export] public float CameraExtraDistance = 3f; // distancia extra a máxima velocidad
	[Export] public float BaseFov = 75f;
	[Export] public float MaxFov = 95f;

	// Tiempo (s) sin tocar el límite tras el cual el siguiente contacto cuenta como golpe nuevo.
	// Mientras dura un roce continuo, el contacto se renueva y no se vuelve a contar como golpe.
	private const float WallContactGrace = 0.15f;

	private readonly ShipDynamics _dynamics = new();
	private readonly ShipBoost _boost = new();
	private Spaceship _ship;
	private Cockpit _cockpit;
	private ChaseCamera _chaseCam;
	private Track _track;
	private RaceSession _race;

	private bool _chaseView = true;
	private float _bank = 0f;
	private float _wallContactTimer = 0f;

	// Velocidad real actual en m/s (la usa el HUD)
	public float Speed => _dynamics.RealSpeed;

	// Velocidad máxima con un boost desde la punta (m/s): tope de la barra de velocidad del HUD
	public float BoostedTopSpeed => MaxSpeed + BoostSpeedGain / KmhPerMs;

	// Cargas de boost disponibles y si el cooldown está activo (las lee el HUD junto con BoostMaxCharges)
	public int BoostCharges => _boost.Charges;
	public bool BoostCooldownActive => _boost.IsCooldownActive;

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

	// Coloca la nave en una posición y orientación (yaw en radianes) y la deja parada
	public void PlaceAt(Vector3 position, float yaw)
	{
		GlobalPosition = position;
		_dynamics.Reset(yaw);
		_boost.Reset(BuildBoostTuning());
		_bank = 0f;
		_wallContactTimer = 0f;
		Rotation = new Vector3(0f, yaw, 0f);
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings());
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

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// 1. Boost por cargas y reglas de conducción (lógica pura).
		// Sin carrera en marcha no hay input ni boost (ni recarga): en la previa la nave está
		// quieta y, al terminar, se cancela el impulso y se para sola con el Drag.
		bool canDrive = _race == null || _race.CanDrive;
		ShipInput input = canDrive ? ReadInput() : default;
		float boostGain = 0f;
		if (canDrive)
		{
			BoostTuning boostTuning = BuildBoostTuning();
			if (Input.IsActionJustPressed("boost"))
				_boost.TryActivate(boostTuning);
			if (input.Brake)
				_boost.CancelImpulse();
			boostGain = _boost.Update(boostTuning, dt);
		}
		else
		{
			_boost.CancelImpulse();
		}

		_dynamics.Step(input, BuildTuning(), dt, boostGain, _boost.IsImpulseActive);
		Rotation = new Vector3(0f, _dynamics.Yaw, 0f);

		// 2. Mover el cuerpo en Godot con la velocidad real
		Velocity = new Vector3(_dynamics.Velocity.X, 0f, _dynamics.Velocity.Y);
		MoveAndSlide();
		_dynamics.SetVelocity(new NVec2(Velocity.X, Velocity.Z));

		// 3. Límites laterales de la pista
		ApplyTrackLimits(dt);

		// 4. Si choca contra algo, el motor también pierde velocidad
		if (GetSlideCollisionCount() > 0)
			_dynamics.AbsorbCollision();

		// 5. Inclinación visual de la nave al girar
		float bankTarget = _dynamics.Steer * MaxBank * _dynamics.TurnFactor;
		_bank = Mathf.Lerp(_bank, bankTarget, 1f - Mathf.Exp(-8f * dt));
		var roll = new Vector3(0f, 0f, _bank);
		_ship.Rotation = roll;
		_cockpit.Rotation = roll;

		// 6. Cámara de tercera persona
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
			MaxSpeed = MaxSpeed,
			Acceleration = Acceleration,
			BrakeDeceleration = BrakeDeceleration,
			Drag = Drag,
			OverspeedDecay = OverspeedDecay / KmhPerMs,
			TurnRate = TurnRate,
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

	// Si la nave sobrepasa el borde de la pista, TrackLimits la devuelve al límite.
	// El golpe (WallBrake) se aplica una sola vez al empezar el contacto; mientras la nave
	// sigue rozando solo pierde la fricción suave de WallScrape y se desliza a lo largo del borde.
	private void ApplyTrackLimits(float dt)
	{
		_wallContactTimer = Mathf.Max(0f, _wallContactTimer - dt);

		if (_track == null)
			return;

		int i = _track.NearestIndex(GlobalPosition);
		Vector3 center = _track.Centerline[i];
		Vector3 right = _track.RightAt(i);

		LimitResult r = TrackLimits.Apply(
			new NVec2(GlobalPosition.X, GlobalPosition.Z),
			_dynamics.Velocity,
			new NVec2(center.X, center.Z),
			new NVec2(right.X, right.Z),
			_track.TrackWidth * 0.5f,
			LimitMargin);

		if (!r.Hit)
			return;

		bool newImpact = _wallContactTimer <= 0f;
		_wallContactTimer = WallContactGrace;

		GlobalPosition = new Vector3(r.Position.X, GlobalPosition.Y, r.Position.Y);
		_dynamics.SetVelocity(r.Velocity);

		if (newImpact)
			_dynamics.ReduceSpeed(r.ImpactSpeed * WallBrake);

		_dynamics.ReduceSpeed(WallScrape * dt);
	}
}
