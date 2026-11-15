using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Vectory.Ship;

// Luces del casco 3D (grupo Casco 3D del Inspector del Player). Las del prototipo como punto de partida (spec
// docs/cascos-3d-1-vector.md, 4.3), entre π: three.js divide la luz difusa entre π y Godot no, así que con el mismo
// número Godot pinta unas tres veces más fuerte. Se ajustan a ojo con el tonemap y el glow del juego.
public sealed class Hull3DSettings
{
	public float KeyEnergy { get; init; } = 2.4f / Mathf.Pi;
	public float RimEnergy { get; init; } = 1.3f / Mathf.Pi;
	public float FillEnergy { get; init; } = 0.55f / Mathf.Pi;
	public float AmbientEnergy { get; init; } = 0.3f;
}

// Casco 3D de una nave (spec docs/cascos-3d-1-vector.md, 4 y 5): carga el .glb, aplica lo que dice su manifiesto y
// lo ilumina con sus propias luces, que solo tocan su capa visual. Sin reglas: no sabe de carrera ni de daño. Cada
// instancia lleva sus propios materiales, así dos naves pueden ir pintadas distinto a la vez.
public partial class ShipHull3D : Node3D
{
	// Capa visual propia del casco: sus luces solo afectan a ella y solo ella proyecta su sombra
	public const uint HullLayer = 1u << 10;

	// Rugosidad final de GRAFITO (SATINADO): Godot no admite el factor 1,7 sobre la textura (spec, 6.1)
	private const float PaintRoughness = 0.42f;
	private const float ShadowMaxDistance = 40f;

	// Luces del prototipo: desde dónde llegan (hacia el centro de la nave), color y grosor de la sombra
	private static readonly Vector3 KeySource = new(4f, 10f, -5f);
	private static readonly Vector3 RimSource = new(-6f, 2.5f, 9f);
	private static readonly Vector3 FillSource = new(8f, 3f, 2f);
	private static readonly Color KeyColor = Color.FromHtml("#FFF1E0");
	private static readonly Color RimColor = Color.FromHtml("#FF9D50");
	private static readonly Color FillColor = Color.FromHtml("#5F8CFF");
	private static readonly Color HemisphereSky = Color.FromHtml("#B0B8C8");
	private static readonly Color HemisphereGround = Color.FromHtml("#101010");

	private readonly Dictionary<string, MeshInstance3D> _pieces = new();
	private readonly Dictionary<string, Node3D> _empties = new();
	private DirectionalLight3D _key;
	private DirectionalLight3D _rim;
	private DirectionalLight3D _fill;
	private Hull3DSettings _settings = new();

	public ShipHull3DProfile Profile { get; set; }

	public bool IsLoaded { get; private set; }

	public override void _Ready()
	{
		IsLoaded = LoadModel();
		BuildLights();
		ApplySettings(_settings);
		// Al final del árbol: la WorldEnvironment de la escena se crea en el _Ready de Main, después de este nodo
		CallDeferred(MethodName.SetUpWorldReflections);
	}

	public void ApplySettings(Hull3DSettings settings)
	{
		_settings = settings;
		if (_key == null)
			return;
		_key.LightEnergy = settings.KeyEnergy;
		_rim.LightEnergy = settings.RimEnergy;
		_fill.LightEnergy = settings.FillEnergy;
		ApplyAmbient();
	}

	// Punto vacío del modelo (bocas, estelas, humo) en coordenadas de la nave; cero si no está
	public Vector3 EmptyPosition(string name) => _empties.TryGetValue(name, out Node3D node) ? node.Position : Vector3.Zero;

	private bool LoadModel()
	{
		var scene = GD.Load<PackedScene>(Profile.ModelPath);
		string json = FileAccess.GetFileAsString(Profile.ManifestPath);
		HullManifest manifest = string.IsNullOrEmpty(json) ? null : HullManifest.Parse(json);
		if (scene == null || manifest == null)
		{
			GD.PushError($"[Hull3D] Cannot load {Profile.ModelPath} or {Profile.ManifestPath}");
			return false;
		}

		Node model = scene.Instantiate();
		AddChild(model);
		Collect(model);

		List<string> missing = HullManifest.Missing(_pieces.Keys).Concat(HullPieces.Empties.Where(e => !_empties.ContainsKey(e))).ToList();
		if (missing.Count > 0)
			GD.PushError("[Hull3D] Missing hull pieces: " + string.Join(", ", missing));

		ApplyManifest(manifest);
		return missing.Count == 0;
	}

	private void Collect(Node node)
	{
		if (node is MeshInstance3D mesh)
			_pieces[mesh.Name] = mesh;
		else if (node is Node3D n && HullPieces.Empties.Contains((string)node.Name))
			_empties[node.Name] = n;
		foreach (Node child in node.GetChildren())
			Collect(child);
	}

	// Lo que glTF no lleva o Godot no lee (spec, 4.1), con materiales propios de esta instancia
	private void ApplyManifest(HullManifest manifest)
	{
		var noShadow = new HashSet<string>(manifest.NoShadow);
		foreach (MeshInstance3D mesh in _pieces.Values)
		{
			mesh.Layers = HullLayer;
			mesh.CastShadow = noShadow.Contains(mesh.Name)
				? GeometryInstance3D.ShadowCastingSetting.Off
				: GeometryInstance3D.ShadowCastingSetting.On;
			for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
			{
				if (mesh.GetActiveMaterial(i) is not StandardMaterial3D source)
					continue;
				var material = (StandardMaterial3D)source.Duplicate();
				material.ResourceName = source.ResourceName;
				Prepare(material, manifest);
				mesh.SetSurfaceOverrideMaterial(i, material);
			}
		}

		// Sin librea, sus piezas no se dibujan (la librea y las calcomanías llegan con las pinturas)
		foreach (string name in HullPieces.LiveryPieces)
		{
			if (_pieces.TryGetValue(name, out MeshInstance3D mesh))
				mesh.Visible = false;
		}

		foreach (CockpitManifestPiece piece in manifest.Pieces.Where(p => p.Intensity.HasValue))
		{
			if (_pieces.TryGetValue(piece.Name, out MeshInstance3D mesh) && mesh.GetActiveMaterial(0) is StandardMaterial3D material)
			{
				material.EmissionEnabled = true;
				material.EmissionEnergyMultiplier = piece.Intensity.Value;
			}
		}
	}

	private static void Prepare(StandardMaterial3D material, HullManifest manifest)
	{
		string name = material.ResourceName;
		// El metal es un valor por material, y la textura de rugosidad es gris: leída como metal, lo cambiaría
		material.MetallicTexture = null;

		HullManifestCoat coat = manifest.CoatOf(name);
		if (coat != null && coat.Coat > 0f)
		{
			material.ClearcoatEnabled = true;
			material.Clearcoat = coat.Coat;
			material.ClearcoatRoughness = coat.Roughness;
		}

		if (name == "M_Paint")
		{
			material.RoughnessTexture = null;
			material.Roughness = PaintRoughness;
		}

		float reflection = manifest.ReflectionFactor(name);
		if (reflection != 1f)
		{
			// glTF no tiene "intensidad de los reflejos" y Godot tampoco por material: se cambia lo que refleja
			material.MetallicSpecular = Mathf.Min(1f, material.MetallicSpecular * reflection);
			material.Metallic = Mathf.Min(1f, material.Metallic * reflection);
		}

		CockpitManifestAdditive transparent = manifest.TransparentMaterials.FirstOrDefault(t => t.Material == name);
		if (transparent != null)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
			Color albedo = material.AlbedoColor;
			material.AlbedoColor = new Color(albedo.R, albedo.G, albedo.B, transparent.Opacity);
		}
	}

	// Colgadas de la nave: giran con ella, así el casco se ve igual en cualquier curva
	private void BuildLights()
	{
		_key = NewLight("Key", KeySource, KeyColor, true);
		_rim = NewLight("Rim", RimSource, RimColor, false);
		_fill = NewLight("Fill", FillSource, FillColor, false);
	}

	private DirectionalLight3D NewLight(string name, Vector3 source, Color color, bool shadow)
	{
		var light = new DirectionalLight3D
		{
			Name = name,
			LightColor = color,
			LightCullMask = HullLayer,
			ShadowCasterMask = HullLayer,
			ShadowEnabled = shadow,
			ShadowBlur = 1.5f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
			DirectionalShadowMaxDistance = ShadowMaxDistance
		};
		AddChild(light);
		// Mira desde la fuente hacia el centro, en el espacio de la nave
		light.Transform = Transform3D.Identity.LookingAt(-source, Vector3.Up);
		return light;
	}

	// Reflejos y luz ambiente del casco: el cielo del entorno, solo para reflejos, con el fondo de color de siempre.
	// El resto del mundo de carrera no recibe luz (todo es unshaded), así que no cambia.
	private void SetUpWorldReflections()
	{
		Godot.Environment env = FindEnvironment();
		if (env == null)
			return;
		if (env.Sky == null)
		{
			env.Sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = GD.Load<Texture2D>(Profile.EnvironmentPath) } };
			// El centro del panorama mira a −Z del prototipo y en Godot el centro de la imagen cae en +Z
			env.SkyRotation = new Vector3(0f, Mathf.Pi, 0f);
		}
		env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
		env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		ApplyAmbient();
	}

	// Hemisferio del prototipo en un solo color: la mitad de la luz llega de arriba y la mitad de abajo
	private void ApplyAmbient()
	{
		Godot.Environment env = FindEnvironment();
		if (env == null || env.AmbientLightSource != Godot.Environment.AmbientSource.Color)
			return;
		Color mixed = (HemisphereSky.SrgbToLinear() + HemisphereGround.SrgbToLinear()) * (0.5f * _settings.AmbientEnergy);
		env.AmbientLightColor = new Color(mixed.R, mixed.G, mixed.B).LinearToSrgb();
		env.AmbientLightEnergy = 1f;
	}

	private Godot.Environment FindEnvironment()
	{
		if (!IsInsideTree())
			return null;
		Godot.Environment env = GetViewport().FindWorld3D()?.Environment;
		if (env != null)
			return env;
		foreach (Node node in GetTree().Root.FindChildren("*", nameof(WorldEnvironment), true, false))
		{
			if (node is WorldEnvironment world && world.Environment != null)
				return world.Environment;
		}
		return null;
	}
}
