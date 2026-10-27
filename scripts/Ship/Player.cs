using Godot;
using Vectory.Circuit;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.Ship;

// Nave del jugador. Este nodo solo CONECTA piezas: lee el teclado, pasa la entrada al modelo
// de conducción (ShipDynamics), mueve el cuerpo en Godot, aplica los límites de la pista
// (TrackLimits), actualiza la nave visual y las cámaras, y le dice a la sesión de carrera
// (RaceSession) dónde está para contar vueltas. Las reglas viven en las clases puras.
//
// W / Flecha arriba: acelerar    S / Flecha abajo: frenar
// A,D / Flechas izq.-der.: girar Shift: boost
// C: alternar cabina / tercera persona
public partial class Player : CharacterBody3D
{
	// --- Ajustes de conducción: se editan en el Inspector (agrupados) ---
	[ExportGroup("Velocidad")]
	[Export] public float MaxSpeed = 60f;
	[Export] public float BoostMaxSpeed = 100f;
	[Export] public float Acceleration = 25f;
	[Export] public float BoostAcceleration = 60f;
	[Export] public float BrakeDeceleration = 45f;
	[Export] public float Drag = 8f;

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
		_chaseCam.Snap(GlobalPosition, _dynamics.Yaw, SpeedFactor(), CameraSettings());
		ApplyView();
	}

	// Le dice al jugador sobre qué circuito conducir (sin circuito no hay límites ni carrera)
	public void SetTrack(Track track)
	{
		_track = track;
		_race = new RaceSession(track);
	}

	// Coloca la nave en una posición y orientación (yaw en radianes) y la deja parada
	public void PlaceAt(Vector3 position, float yaw)
	{
		GlobalPosition = position;
		_dynamics.Reset(yaw);
		_bank = 0f;
		_wallContactTimer = 0f;
		Rotation = new Vector3(0f, yaw, 0f);
		_race?.Reset();
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

		// 1. Reglas de conducción (lógica pura)
		_dynamics.Step(ReadInput(), BuildTuning(), dt);
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
		_race?.Update(GlobalPosition, Speed, dt);
	}

	// Traduce el teclado a intenciones de conducción
	private static ShipInput ReadInput()
	{
		return new ShipInput
		{
			Accelerate = Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up),
			Brake = Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down),
			SteerLeft = Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left),
			SteerRight = Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right),
			Boost = Input.IsPhysicalKeyPressed(Key.Shift)
		};
	}

	// Copia los parámetros del Inspector al modelo (cada paso, así los cambios en vivo se notan)
	private ShipTuning BuildTuning()
	{
		return new ShipTuning
		{
			MaxSpeed = MaxSpeed,
			BoostMaxSpeed = BoostMaxSpeed,
			Acceleration = Acceleration,
			BoostAcceleration = BoostAcceleration,
			BrakeDeceleration = BrakeDeceleration,
			Drag = Drag,
			TurnRate = TurnRate,
			SteerResponse = SteerResponse,
			LateralGrip = LateralGrip
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
		return Mathf.Clamp(_dynamics.Speed / BoostMaxSpeed, 0f, 1f);
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