using Godot;

// Control arcade de la nave de VECTORY.
// W / Flecha arriba: acelerar    S / Flecha abajo: frenar
// A,D / Flechas izq.-der.: girar Shift: boost
// C: alternar cabina / tercera persona
public partial class Player : CharacterBody3D
{
	// --- Ajustes de conducción: toca estos números para afinar la sensación ---
	[Export] public float MaxSpeed = 60f;
	[Export] public float BoostMaxSpeed = 100f;
	[Export] public float Acceleration = 25f;
	[Export] public float BoostAcceleration = 60f;
	[Export] public float BrakeDeceleration = 45f;
	[Export] public float Drag = 8f;
	[Export] public float TurnRate = 1.6f;       // rad/s de giro máximo
	[Export] public float SteerResponse = 6f;    // lo rápido que llega el giro a su valor máximo
	[Export] public float MaxBank = 0.6f;        // inclinación visual (rad) al girar

	// --- Ajustes de la cámara de tercera persona ---
	[Export] public float CameraFollow = 6f;     // más alto = cámara más pegada, menos efecto dron
	[Export] public float CameraHeight = 1.8f;
	[Export] public float CameraDistance = 7f;
	[Export] public float CameraExtraDistance = 3f; // distancia extra a máxima velocidad
	[Export] public float BaseFov = 75f;
	[Export] public float MaxFov = 95f;

	private Spaceship _ship;
	private Camera3D _chaseCam;
	private bool _chaseView = true;

	private float _speed = 0f;
	private float _steer = 0f;
	private float _yaw = 0f;
	private float _bank = 0f;
	private float _camYaw = 0f;

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

		_chaseCam = new Camera3D { Name = "ChaseCamera", TopLevel = true, Fov = BaseFov };
		AddChild(_chaseCam);

		_camYaw = _yaw;
		PlaceChaseCamera();
		ApplyView();
	}

	// Velocidad actual en m/s (la usa el HUD)
	public float Speed => _speed;

	// Coloca la nave en una posición y orientación (yaw en radianes) y la deja parada
	public void PlaceAt(Vector3 position, float yaw)
	{
		GlobalPosition = position;
		_yaw = yaw;
		_camYaw = yaw;
		_speed = 0f;
		_steer = 0f;
		_bank = 0f;
		Rotation = new Vector3(0f, _yaw, 0f);
		PlaceChaseCamera();
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
		_ship.ShowCockpit(!_chaseView);
		if (_chaseView)
			_chaseCam.MakeCurrent();
		else
			_ship.CockpitCam.MakeCurrent();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		bool accel = Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up);
		bool brake = Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down);
		bool left  = Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left);
		bool right = Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right);
		bool boost = Input.IsPhysicalKeyPressed(Key.Shift);

		// Velocidad
		if (boost)
			_speed = Mathf.MoveToward(_speed, BoostMaxSpeed, BoostAcceleration * dt);
		else if (accel)
			_speed = Mathf.MoveToward(_speed, MaxSpeed, Acceleration * dt);
		else if (brake)
			_speed = Mathf.MoveToward(_speed, 0f, BrakeDeceleration * dt);
		else
			_speed = Mathf.MoveToward(_speed, 0f, Drag * dt);

		// Giro: izquierda = positivo. Si estás casi parado, apenas gira.
		float steerTarget = (left ? 1f : 0f) - (right ? 1f : 0f);
		_steer = Mathf.MoveToward(_steer, steerTarget, SteerResponse * dt);
		float turnFactor = Mathf.Clamp(_speed / 10f, 0f, 1f);

		_yaw += _steer * TurnRate * turnFactor * dt;
		Rotation = new Vector3(0f, _yaw, 0f);

		// Movimiento
		Vector3 forward = -GlobalTransform.Basis.Z;
		Velocity = forward * _speed;
		MoveAndSlide();
		_speed = Mathf.Max(0f, Velocity.Dot(forward)); // si choca, pierde velocidad

		// Inclinación visual de la nave al girar
		float bankTarget = _steer * MaxBank * turnFactor;
		_bank = Mathf.Lerp(_bank, bankTarget, 1f - Mathf.Exp(-8f * dt));
		_ship.Rotation = new Vector3(0f, 0f, _bank);

		UpdateChaseCamera(dt);
	}

	// Coloca la cámara de tercera persona de golpe (sin suavizado)
	private void PlaceChaseCamera()
	{
		float speedFactor = Mathf.Clamp(_speed / BoostMaxSpeed, 0f, 1f);
		var camBasis = new Basis(Vector3.Up, _camYaw);
		var offset = new Vector3(0f, CameraHeight, CameraDistance + speedFactor * CameraExtraDistance);

		_chaseCam.GlobalPosition = GlobalPosition + camBasis * offset;
		_chaseCam.LookAt(GlobalPosition + camBasis * new Vector3(0f, 0.3f, -10f), Vector3.Up);
	}

	// La cámara sigue a la nave sin retraso en posición, pero su orientación
	// se retrasa un poco al girar: eso da el efecto de dron siguiéndola.
	private void UpdateChaseCamera(float dt)
	{
		float t = 1f - Mathf.Exp(-CameraFollow * dt);
		_camYaw = Mathf.LerpAngle(_camYaw, _yaw, t);

		float speedFactor = Mathf.Clamp(_speed / BoostMaxSpeed, 0f, 1f);
		var camBasis = new Basis(Vector3.Up, _camYaw);
		var offset = new Vector3(0f, CameraHeight, CameraDistance + speedFactor * CameraExtraDistance);

		_chaseCam.GlobalPosition = GlobalPosition + camBasis * offset;
		_chaseCam.LookAt(GlobalPosition + camBasis * new Vector3(0f, 0.3f, -10f), Vector3.Up);

		float targetFov = Mathf.Lerp(BaseFov, MaxFov, speedFactor);
		_chaseCam.Fov = Mathf.Lerp(_chaseCam.Fov, targetFov, t);
	}
}
