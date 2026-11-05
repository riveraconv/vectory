using Godot;

namespace Vectory.Ship;

// Cabina de la nave en primera persona: la cámara y la capa 2D de la cabina (CockpitLayer), que va fija en pantalla
// mientras el mundo se ladea, vibra o se abre con el boost.
// Es independiente de la nave exterior (Spaceship): Player decide cuál de las dos se ve.
// La cabina y el encuadre salen del perfil visual de la nave (ShipDefinition).
public partial class Cockpit : Node3D
{
	private CockpitLayer _layer;

	// Número de aristas de la cabina: cada una puede parpadear por su cuenta con los daños
	public int EdgeCount => _layer.EdgeCount;

	public Camera3D Camera { get; private set; }

	// Perfil visual de la nave. Se asigna al crear el nodo.
	public ShipVisualProfile Visual { get; set; } = ShipDefinitions.Default.Visual;

	private Vector3 CameraPosition => ShipLineMesh.ToGodot(Visual.Cockpit.Eye);
	private float BaseFov => Visual.Cockpit.Fov;
	private float Pitch => Mathf.DegToRad(Visual.Cockpit.PitchDegrees);

	// Sacudida por impacto y efectos de cámara (inclinación, vibración, empuje y FOV del boost)
	private Vector2 _shakeOffset;
	private float _shakeRoll;
	private float _bank;
	private Vector2 _vibration;
	private float _push;
	private float _fovExtra;

	public override void _Ready()
	{
		// Cámara de cabina (primera persona). La de tercera persona la gestiona Player.
		Camera = new Camera3D
		{
			Name = "Camera3D",
			Position = CameraPosition,
			Rotation = new Vector3(Pitch, 0f, 0f),
			Fov = BaseFov
		};
		AddChild(Camera);

		_layer = new CockpitLayer { Name = "CockpitLayer" };
		_layer.Setup(Visual.BuildCockpit());
		AddChild(_layer);

		SetLayerVisible(false);
	}

	// Cambia de cabina y de encuadre en plena carrera (panel de ajuste). Después hay que volver a pasar el parpadeo.
	public void SetVisual(ShipVisualProfile visual)
	{
		if (visual == Visual)
			return;
		bool newLayout = visual.BuildCockpit != Visual.BuildCockpit;
		Visual = visual;
		if (newLayout)
			_layer.Setup(visual.BuildCockpit());
		ApplyCamera();
	}

	// Sacudida de la cámara de cabina (desplazamiento en el plano de la vista, en m, y giro sobre la vista, en rad)
	public void SetShake(Vector2 offset, float roll)
	{
		_shakeOffset = offset;
		_shakeRoll = roll;
		ApplyCamera();
	}

	// Efectos de cámara (spec docs/camara.md): inclinación en curva (rad), vibración (m, en el plano de la vista),
	// empuje hacia atrás (m) y FOV extra (grados) del boost
	public void SetEffects(float bank, Vector2 vibration, float push, float fovExtra)
	{
		_bank = bank;
		_vibration = vibration;
		_push = push;
		_fovExtra = fovExtra;
		ApplyCamera();
	}

	private void ApplyCamera()
	{
		Vector2 offset = _shakeOffset + _vibration;
		Camera.Position = CameraPosition + new Vector3(offset.X, offset.Y, _push);
		Camera.Rotation = new Vector3(Pitch, 0f, _shakeRoll + _bank);
		Camera.Fov = BaseFov + _fovExtra;
	}

	// La capa solo se ve en primera persona
	public void SetLayerVisible(bool visible)
	{
		_layer.SetShown(visible);
	}

	public void UpdateHud(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_layer.UpdateHud(input, light, dt);
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		_layer.ShowEdgeFlicker(flicker, dimOpacity);
	}
}
