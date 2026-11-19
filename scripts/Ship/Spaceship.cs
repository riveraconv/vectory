using Godot;
using Vectory.Effects;

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

	// Las luces del casco 3D giran con la nave (carrera) o quedan fijas en el mundo (holograma del selector)
	public bool Hull3DLightsFollowShip { get; set; } = true;

	// Capa del casco 3D y si lleva sus propias luces: los rivales van en ShipHull3D.RivalLayer, con un juego de luces
	// común (HullLightRig). Antes de _Ready.
	public uint Hull3DLayer { get; set; } = ShipHull3D.HullLayer;
	public bool Hull3DOwnLights { get; set; } = true;

	// Pintura (por estilo de casco: combinación de su catálogo) y daño del casco 3D: se guardan para volver a ponerlos
	// si F10 lo rehace
	private readonly System.Collections.Generic.Dictionary<HullStyle, int> _hullPaints = new();
	private int _hullDamage;
	private System.Numerics.Vector4 _hullWear;
	private bool _hullTimeTrial;
	private int _wireEdgeCount;

	// Llamas de las toberas (spec docs/efectos-6-toberas.md): solo si la nave las pide (carrera, fantasma y menú; el
	// holograma y las naves de prueba, no)
	private ThrusterFlames _thrusters;
	private ThrusterFxSettings _thrusterSettings;
	private float _thrusterPhase;
	private bool _thrusterFull;

	// Estelas de las puntas de las alas (spec docs/efectos-7-estelas.md): solo en carrera, tú y los rivales
	private WingTrails _trails;
	private WingTrailSettings _trailSettings;
	private WingTrailColors _trailColors;

	// Número de aristas que pueden parpadear por su cuenta con los daños: las líneas del wireframe o, con casco 3D,
	// sus luces (14 en VECTOR, 9 en BLADE)
	public int EdgeCount => _hull3D != null ? _hull3D.FlickerLightCount : _wireEdgeCount;

	public bool HasHull3D => _hull3D != null;

	public override void _Ready()
	{
		_meshInstance = new MeshInstance3D { Name = "MeshInstance3D", Mesh = BuildMesh() };
		AddChild(_meshInstance);
		if (_thrusterSettings != null)
			CreateThrusters();
		if (_trailSettings != null)
			CreateTrails();
		RebuildHull3D();
	}

	// Enciende las llamas de las toberas, con sus ajustes y un desfase de tiempo propio; full: con diamantes, calor y luz
	// (carrera), si no solo la llama (fantasma y menú). Antes o después de _Ready.
	public void EnableThrusters(ThrusterFxSettings settings, float phase, bool full = true)
	{
		_thrusterSettings = settings ?? ThrusterFxSettings.Default;
		_thrusterPhase = phase;
		_thrusterFull = full;
		if (_meshInstance != null && _thrusters == null)
		{
			CreateThrusters();
			_thrusters.Hull = _hull3D;
			ApplyVisibility();
		}
	}

	private void CreateThrusters()
	{
		_thrusters = new ThrusterFlames { Name = "Thrusters" };
		_thrusters.Setup(_thrusterSettings, Visual.Thrusters, _thrusterPhase, _thrusterFull, Hull3DLayer);
		AddChild(_thrusters);
	}

	// Enciende las estelas de las alas, con sus ajustes y la pareja de colores de la nave. Antes de _Ready.
	public void EnableTrails(WingTrailSettings settings, WingTrailColors colors)
	{
		_trailSettings = settings ?? WingTrailSettings.Default;
		_trailColors = colors;
	}

	private void CreateTrails()
	{
		_trails = new WingTrails { Name = "WingTrails", Ship = this, Thrusters = _thrusters };
		_trails.Setup(_trailSettings, _trailColors, Visual.TrailTip);
		AddChild(_trails);
	}

	// Movimiento de la nave en este paso de física, para la carga de sus estelas
	public void FeedTrails(float dt, in WingTrailMotion motion) => _trails?.Move(dt, motion);

	// Recolocación: sin estela
	public void ResetTrails() => _trails?.ResetState();

	// Lo que hace la nave en este paso, para sus toberas
	public void FeedThrusters(in ThrusterInput input) => _thrusters?.Feed(input);

	// Recolocación: las toberas, al ralentí al momento
	public void ResetThrusters() => _thrusters?.ResetState();

	// Intensidad de las llamas (el fantasma: su transparencia)
	public void SetThrusterOpacity(float opacity)
	{
		if (_thrusters != null)
			_thrusters.Opacity = opacity;
	}

	public void SetHull3DSettings(Hull3DSettings settings)
	{
		_hullSettings = settings;
		_hull3D?.ApplySettings(settings);
	}

	// Pintura de los cascos 3D de un estilo: una combinación de su catálogo (HullPaints). Se aplica si el casco actual es
	// de ese estilo; si no, se guarda para cuando lo sea.
	public void SetHullPaint(HullStyle style, int combination)
	{
		_hullPaints[style] = combination;
		if (_hull3D != null && _hull3D.Profile.Style == style)
			_hull3D.SetPaint(combination);
	}

	// Combinación puesta para un estilo, o la de por defecto si no se ha puesto ninguna
	public int HullPaint(HullStyle style) =>
		_hullPaints.TryGetValue(style, out int index) && index >= 0 ? HullPaints.Step(style, index, 0) : HullPaints.DefaultIndex(style);

	// Rozaduras acumuladas del casco 3D (izquierda, derecha, morro, popa; spec docs/efectos-5-danos-casco.md)
	public void SetHullWear(System.Numerics.Vector4 wear)
	{
		_hullWear = wear;
		_hull3D?.SetWear(wear);
	}

	// Daño visible del casco 3D: hollín y grietas según el nivel (en TIME TRIAL, nada)
	public void SetHullDamage(int level, bool timeTrial)
	{
		_hullDamage = level;
		_hullTimeTrial = timeTrial;
		_hull3D?.SetDamage(level, timeTrial);
	}

	// Opacidad del casco 3D (parpadeo tras recolocar); el wireframe no la usa
	public void SetHullOpacity(float opacity)
	{
		_hull3D?.SetOpacity(opacity);
	}

	// Tiempo de la nave para la anticolisión
	public void UpdateHullEffects(float time)
	{
		_hull3D?.UpdateEffects(time);
	}

	// Balizas del techo del casco 3D (HAMMER): intensidad de la ámbar y de la roja; sin balizas no hace nada
	public void SetHullBeacons(float boost, float damage)
	{
		_hull3D?.SetBeacons(boost, damage);
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
			_hull3D = new ShipHull3D
			{
				Name = "Hull3D",
				Profile = profile,
				LightsFollowShip = Hull3DLightsFollowShip,
				Layer = Hull3DLayer,
				OwnLights = Hull3DOwnLights
			};
			_hull3D.ApplySettings(_hullSettings);
			_hull3D.SetPaint(_hullPaints.TryGetValue(profile.Style, out int paint) ? paint : -1);
			AddChild(_hull3D);
			_hull3D.SetDamage(_hullDamage, _hullTimeTrial);
			_hull3D.SetWear(_hullWear);
		}
		_hull3D?.SetDamageShape(Visual.SmokeFoci, Visual.CollisionHalfWidth, Visual.CollisionFront, Visual.CollisionBack);
		if (_thrusters != null)
			_thrusters.Hull = _hull3D;
		ApplyVisibility();
	}

	private void ApplyVisibility()
	{
		_meshInstance.Visible = _exteriorVisible && _hull3D == null;
		if (_hull3D != null)
			_hull3D.Visible = _exteriorVisible;
		if (_thrusters != null)
			_thrusters.Visible = _exteriorVisible;
		if (_trails != null)
			_trails.Visible = _exteriorVisible;
	}

	// Cambia la nave dibujada. Con el mismo perfil no rehace nada. El material sustituido sigue puesto; el de parpadeo
	// se quita porque está hecho para las aristas del casco anterior: hay que volver a llamar a ShowEdgeFlicker.
	public void SetVisual(ShipVisualProfile visual)
	{
		if (visual == Visual)
			return;
		Visual = visual;
		_meshInstance.Mesh = BuildMesh();
		_thrusters?.SetShape(visual.Thrusters);
		_trails?.SetTip(visual.TrailTip);
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
		// Con casco 3D titilan sus luces (bajan al 20 %, spec docs/cascos-3d-1-vector.md, 6.5)
		if (_hull3D != null)
		{
			_hull3D.ShowLightFlicker(flicker);
			return;
		}
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
		_wireEdgeCount = edgeCount;
		return mesh;
	}
}
