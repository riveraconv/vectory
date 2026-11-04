using Godot;

namespace Vectory.Ship;

// Cabina de la nave en primera persona: la cámara y el marco de líneas pegado a ella.
// Es independiente de la nave exterior (Spaceship): Player decide cuál de las dos se ve.
// El marco y el encuadre salen del perfil visual de la nave (ShipDefinition).
public partial class Cockpit : Node3D
{
	private MeshInstance3D _frame;
	private EdgeFlickerMaterial _flickerMaterial;

	// Número de aristas del marco: cada una puede parpadear por su cuenta con los daños
	public int EdgeCount { get; private set; }

	public Camera3D Camera { get; private set; }

	// Perfil visual de la nave. Se asigna al crear el nodo.
	public ShipVisualProfile Visual { get; set; } = ShipDefinitions.Default.Visual;

	private Vector3 CameraPosition => ShipLineMesh.ToGodot(Visual.Cockpit.Eye);
	private float BaseFov => Visual.Cockpit.Fov;

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
			Fov = BaseFov
		};
		AddChild(Camera);

		// Marco minimalista de la cabina, pegado a la cámara
		_frame = new MeshInstance3D { Name = "CockpitFrame", Mesh = ShipLineMesh.Build(Visual.BuildCockpit(), out int edgeCount) };
		EdgeCount = edgeCount;
		Camera.AddChild(_frame);

		SetFrameVisible(false);
	}

	// Sacudida de la cámara de cabina (desplazamiento en el plano de la vista, en m, y giro sobre la vista, en rad).
	// El marco va pegado a la cámara y se mueve con ella.
	public void SetShake(Vector2 offset, float roll)
	{
		_shakeOffset = offset;
		_shakeRoll = roll;
		ApplyCamera();
	}

	// Efectos de cámara (spec docs/camara.md): inclinación en curva (rad, ladea cámara y marco juntos),
	// vibración (m, en el plano de la vista), empuje hacia atrás (m) y FOV extra (grados) del boost
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
		Camera.Rotation = new Vector3(0f, 0f, _shakeRoll + _bank);
		Camera.Fov = BaseFov + _fovExtra;

		// El marco está dibujado para el FOV base. Al abrirse el FOV con el boost se vería más pequeño
		// y dejaría hueco hasta los bordes de la pantalla, así que se escala en X e Y con la misma
		// proporción que crece el plano visible a 1 m: en pantalla se queda igual y solo se abre el mundo.
		float k = Mathf.Tan(Mathf.DegToRad(Camera.Fov) * 0.5f) / Mathf.Tan(Mathf.DegToRad(BaseFov) * 0.5f);
		_frame.Scale = new Vector3(k, k, 1f);
	}

	// El marco solo se ve en primera persona
	public void SetFrameVisible(bool visible)
	{
		_frame.Visible = visible;
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity. La primera llamada cambia el material
	// del marco por el de parpadeo; sin llamarla el marco conserva su material de siempre.
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		if (_flickerMaterial == null)
		{
			_flickerMaterial = new EdgeFlickerMaterial(EdgeCount);
			_frame.SetSurfaceOverrideMaterial(0, _flickerMaterial.Material);
		}
		_flickerMaterial.SetDimOpacity(dimOpacity);
		_flickerMaterial.Apply(flicker);
	}
}
