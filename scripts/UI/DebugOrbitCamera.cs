using Godot;
using Vectory.Core;
using Vectory.Ship;

namespace Vectory.UI;

// Cámara orbital de depuración para mirar la nave por todos lados (casco 3D, pinturas, librea, daño). F4 la activa o la
// quita; con el botón izquierdo se arrastra para girar y con la rueda se acerca o aleja. Gira con la nave, así no se
// pierde al moverse. Main solo la crea en compilaciones de depuración. Sin reglas: solo mueve su cámara.
public partial class DebugOrbitCamera : Node3D
{
	// Nombres de acciones y de parámetros de shader creados una vez: con un texto, cada llamada crea un StringName
	// nuevo (con finalizador), y en cada frame eso alargaba las pausas del recolector de .NET
	private static readonly StringName SnDebugOrbitCamera = "debug_orbit_camera";

	private const float RadiansPerPixel = 0.008f;
	private const float MinDistance = 3f;
	private const float MaxDistance = 30f;
	private const float ZoomStep = 1.12f;
	private const float MinPitch = -1.45f;
	private const float MaxPitch = 1.45f;
	private const float TargetHeight = 0.3f;
	private const float Fov = 40f;

	// Tres cuartos desde delante a la izquierda, como la vista TRES CUARTOS del prototipo
	private float _yaw = -2.35f;
	private float _pitch = 0.33f;
	private float _distance = 11f;
	private bool _dragging;
	private Camera3D _camera;
	private Camera3D _previous;
	private Label _label;

	public Player Player { get; set; }

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_camera = new Camera3D { Name = "OrbitCamera", Fov = Fov, Near = 0.05f, Far = 4000f };
		AddChild(_camera);

		var layer = new CanvasLayer { Name = "OrbitLabel", Layer = 11 };
		AddChild(layer);
		_label = new Label
		{
			Text = "ORBIT CAMERA · DRAG · WHEEL · F4",
			Position = new Vector2(40f, 690f) * UiScale.Factor,
			LabelSettings = MenuFonts.Settings(MenuFonts.Mono, Mathf.RoundToInt(14 * UiScale.Factor), Palette.Orange, Colors.Transparent, 0f),
			Visible = false
		};
		layer.AddChild(_label);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed(SnDebugOrbitCamera))
		{
			Toggle();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!_camera.Current)
			return;

		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				_dragging = button.Pressed;
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
				_distance = Mathf.Max(MinDistance, _distance / ZoomStep);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
				_distance = Mathf.Min(MaxDistance, _distance * ZoomStep);
				break;
			case InputEventMouseMotion motion when _dragging:
				_yaw -= motion.Relative.X * RadiansPerPixel;
				_pitch = Mathf.Clamp(_pitch + motion.Relative.Y * RadiansPerPixel, MinPitch, MaxPitch);
				break;
		}
	}

	private void Toggle()
	{
		if (Player == null)
			return;
		if (_camera.Current)
		{
			_previous?.MakeCurrent();
			_label.Visible = false;
			_dragging = false;
			GD.Print("[Orbit] Off");
			return;
		}
		_previous = GetViewport().GetCamera3D();
		_camera.MakeCurrent();
		_label.Visible = true;
		GD.Print(Player.View == CameraView.Cockpit
			? "[Orbit] On (cockpit view: press C for the exterior hull)"
			: "[Orbit] On");
	}

	public override void _Process(double delta)
	{
		if (!_camera.Current || Player == null)
			return;
		// En el espacio del rumbo de la nave: gira con ella
		float heading = Player.GlobalRotation.Y;
		Vector3 target = Player.GlobalPosition + Vector3.Up * TargetHeight;
		var offset = new Vector3(
			Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
			Mathf.Sin(_pitch),
			Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * _distance;
		offset = offset.Rotated(Vector3.Up, heading);
		_camera.GlobalPosition = target + offset;
		_camera.LookAt(target, Vector3.Up);
	}
}
