using Godot;

namespace Vectory.Ship;

// Cabina de la nave en primera persona: la cámara del mundo y, según el perfil visual, la capa 2D (CockpitLayer) o la
// cabina 3D (Cockpit3DView). Las dos van fijas en pantalla mientras el mundo se ladea, vibra o se abre con el boost.
// Es independiente de la nave exterior (Spaceship): Player decide cuál de las dos se ve.
// La cabina y el encuadre salen del perfil visual de la nave (ShipDefinition).
public partial class Cockpit : Node3D
{
	private CockpitLayer _layer;
	private System.Func<CockpitLayout> _layerBuilder;
	private Cockpit3DView _view3D;
	private CockpitViewRules _rules;

	// Número de aristas de la cabina 2D: cada una puede parpadear por su cuenta con los daños
	public int EdgeCount => Uses3D ? 0 : _layer.EdgeCount;

	public Camera3D Camera { get; private set; }

	// Perfil visual de la nave. Se asigna al crear el nodo.
	public ShipVisualProfile Visual { get; set; } = ShipDefinitions.Default.Visual;

	// Ajustes iniciales de la cabina 3D. Se asignan al crear el nodo.
	public Cockpit3DSettings Settings3D { get; set; }

	public bool Uses3D => Visual.Cockpit3D != null;

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

		EnsureCockpit();
		ApplyRules(CockpitViewRules.For(CameraView.Chase, Uses3D));
	}

	// Cambia de cabina y de encuadre en plena carrera (panel de ajuste). Después hay que volver a pasar el parpadeo y
	// las reglas de la vista.
	public void SetVisual(ShipVisualProfile visual)
	{
		if (visual == Visual)
			return;
		Visual = visual;
		EnsureCockpit();
		ApplyCamera();
	}

	// Crea lo que necesite la cabina actual. La cabina 3D no se destruye al cambiar de nave: se queda parada, sin dibujarse,
	// para no dar un tirón al volver.
	private void EnsureCockpit()
	{
		if (Uses3D)
		{
			if (_view3D == null)
			{
				_view3D = new Cockpit3DView { Name = "Cockpit3DView" };
				_view3D.Setup(Visual.Cockpit3D, Settings3D);
				AddChild(_view3D);
			}
			return;
		}

		if (_layerBuilder == Visual.BuildCockpit)
			return;

		_layerBuilder = Visual.BuildCockpit;
		bool isNew = _layer == null;
		_layer ??= new CockpitLayer { Name = "CockpitLayer" };
		_layer.Setup(Visual.BuildCockpit());
		if (isNew)
			AddChild(_layer);
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

	// Movimiento propio de la cámara de la cabina 3D y sus ajustes. Sin cabina 3D, nada.
	public void Update3D(in CockpitCameraPose pose, in Cockpit3DSettings settings)
	{
		Settings3D = settings;
		if (_view3D == null || !_view3D.IsNodeReady())
			return;
		_view3D.SetCameraPose(pose);
		_view3D.Apply(settings);
	}

	// Qué se enseña en la vista actual
	public void ApplyRules(in CockpitViewRules rules)
	{
		_rules = rules;
		_layer?.SetShown(rules.CockpitLayer);
		_view3D?.SetShown(rules.Cockpit3D);
	}

	public void UpdateHud(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_layer?.UpdateHud(input, light, dt);
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		if (!Uses3D)
			_layer.ShowEdgeFlicker(flicker, dimOpacity);
	}
}
