using Godot;

namespace Vectory.Ship;

// Nave wireframe de VECTORY (solo el aspecto exterior; la cabina vive en Cockpit). Dibuja el casco de su perfil
// visual: la geometría está en la definición de cada nave (ShipDefinition), no aquí.
public partial class Spaceship : Node3D
{
	private MeshInstance3D _meshInstance;
	private EdgeFlickerMaterial _flickerMaterial;
	private ShipHull3D _hull3D;
	private bool _exteriorVisible = true;
	private Hull3DSettings _hullSettings = new();

	// Perfil visual de la nave. Se asigna al crear el nodo; después de _Ready, con SetVisual.
	public ShipVisualProfile Visual { get; set; } = ShipDefinitions.Default.Visual;

	// Con casco 3D en el perfil, se dibuja ese en lugar del wireframe. Solo la nave del jugador lo pide: el fantasma,
	// el menú y las naves de prueba siguen con el wireframe (spec docs/cascos-3d-1-vector.md, 4.7). Antes de _Ready.
	public bool Use3DHull { get; set; }

	// Número de aristas (líneas) de la nave: cada una puede parpadear por su cuenta con los daños
	public int EdgeCount { get; private set; }

	public override void _Ready()
	{
		_meshInstance = new MeshInstance3D { Name = "MeshInstance3D", Mesh = BuildMesh() };
		AddChild(_meshInstance);
		RebuildHull3D();
	}

	public void SetHull3DSettings(Hull3DSettings settings)
	{
		_hullSettings = settings;
		_hull3D?.ApplySettings(settings);
	}

	// Pone o quita el casco 3D según el perfil actual; el wireframe se oculta mientras haya uno
	private void RebuildHull3D()
	{
		ShipHull3DProfile profile = Use3DHull ? Visual.Hull3D : null;
		if (_hull3D != null && _hull3D.Profile != profile)
		{
			_hull3D.QueueFree();
			_hull3D = null;
		}
		if (profile != null && _hull3D == null)
		{
			_hull3D = new ShipHull3D { Name = "Hull3D", Profile = profile };
			AddChild(_hull3D);
			_hull3D.ApplySettings(_hullSettings);
		}
		ApplyVisibility();
	}

	private void ApplyVisibility()
	{
		_meshInstance.Visible = _exteriorVisible && _hull3D == null;
		if (_hull3D != null)
			_hull3D.Visible = _exteriorVisible;
	}

	// Cambia la nave dibujada. Con el mismo perfil no rehace nada. El material sustituido sigue puesto; el de parpadeo
	// se quita porque está hecho para las aristas del casco anterior: hay que volver a llamar a ShowEdgeFlicker.
	public void SetVisual(ShipVisualProfile visual)
	{
		if (visual == Visual)
			return;
		Visual = visual;
		_meshInstance.Mesh = BuildMesh();
		if (_flickerMaterial != null)
		{
			_flickerMaterial = null;
			_meshInstance.SetSurfaceOverrideMaterial(0, null);
		}
		RebuildHull3D();
	}

	// La nave exterior solo se ve en tercera persona (en cabina, el casco 3D tampoco: ni él ni sus luces)
	public void SetExteriorVisible(bool visible)
	{
		_exteriorVisible = visible;
		ApplyVisibility();
	}

	// Sustituye el material de todas las líneas (la nave fantasma lo pinta de un solo color translúcido).
	// null vuelve a los colores normales. Solo después de _Ready (al estar ya en el árbol).
	public void SetMaterialOverride(Material material)
	{
		_meshInstance.MaterialOverride = material;
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity. La primera llamada cambia el material
	// de la nave por el de parpadeo; sin llamarla (menú, fantasma) la nave conserva su material de siempre.
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		if (_flickerMaterial == null)
		{
			_flickerMaterial = new EdgeFlickerMaterial(EdgeCount);
			_meshInstance.SetSurfaceOverrideMaterial(0, _flickerMaterial.Material);
		}
		_flickerMaterial.SetDimOpacity(dimOpacity);
		_flickerMaterial.Apply(flicker);
	}

	private ArrayMesh BuildMesh()
	{
		ArrayMesh mesh = ShipLineMesh.Build(Visual.BuildHull(), out int edgeCount);
		EdgeCount = edgeCount;
		return mesh;
	}
}
