using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vectory.Ship;

// Un punto vacío del casco (bocas de las toberas, estelas, humo), en coordenadas de la nave
public sealed class HullManifestEmpty
{
	[JsonPropertyName("name")] public string Name { get; set; } = "";
	[JsonPropertyName("position")] public float[] Position { get; set; } = System.Array.Empty<float>();

	public Vector3 Point => Position.Length == 3 ? new Vector3(Position[0], Position[1], Position[2]) : Vector3.Zero;
}

// Barniz de un material: glTF lo lleva, pero Godot no lo lee al importar
public sealed class HullManifestCoat
{
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("coat")] public float Coat { get; set; }
	[JsonPropertyName("roughness")] public float Roughness { get; set; }
}

// Lo que el script de Blender del casco apunta y glTF no lleva (spec docs/cascos-3d-1-vector.md, 3.8). Lógica pura.
public sealed class HullManifest
{
	[JsonPropertyName("blender")] public string Blender { get; set; } = "";
	[JsonPropertyName("root")] public string Root { get; set; } = "";
	[JsonPropertyName("environment")] public string Environment { get; set; } = "";
	[JsonPropertyName("pieces")] public List<CockpitManifestPiece> Pieces { get; set; } = new();
	[JsonPropertyName("empties")] public List<HullManifestEmpty> Empties { get; set; } = new();
	[JsonPropertyName("painted")] public List<string> Painted { get; set; } = new();
	[JsonPropertyName("accents")] public List<string> Accents { get; set; } = new();
	[JsonPropertyName("tailLights")] public List<string> TailLights { get; set; } = new();
	[JsonPropertyName("nozzleLights")] public List<string> NozzleLights { get; set; } = new();
	[JsonPropertyName("stripes")] public List<string> Stripes { get; set; } = new();
	[JsonPropertyName("pins")] public List<string> Pins { get; set; } = new();
	[JsonPropertyName("decals")] public List<string> Decals { get; set; } = new();
	[JsonPropertyName("livery")] public List<string> Livery { get; set; } = new();
	[JsonPropertyName("noShadow")] public List<string> NoShadow { get; set; } = new();
	[JsonPropertyName("coat")] public List<HullManifestCoat> Coat { get; set; } = new();
	[JsonPropertyName("reflections")] public List<CockpitManifestReflection> Reflections { get; set; } = new();
	[JsonPropertyName("transparentMaterials")] public List<CockpitManifestAdditive> TransparentMaterials { get; set; } = new();
	[JsonPropertyName("decalMaterials")] public List<string> DecalMaterials { get; set; } = new();
	[JsonPropertyName("scorchMaterials")] public List<string> ScorchMaterials { get; set; } = new();
	[JsonPropertyName("triangles")] public int Triangles { get; set; }
	[JsonPropertyName("trianglesWithLivery")] public int TrianglesWithLivery { get; set; }
	[JsonPropertyName("bounds")] public CockpitManifestBounds Bounds { get; set; } = new();
	[JsonPropertyName("boundsWithLivery")] public CockpitManifestBounds BoundsWithLivery { get; set; } = new();
	[JsonPropertyName("halfWidth")] public float HalfWidth { get; set; }

	// Null si el texto no se puede leer
	public static HullManifest Parse(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<HullManifest>(json);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public CockpitManifestPiece Piece(string name) => Pieces.FirstOrDefault(p => p.Name == name);

	public HullManifestEmpty Empty(string name) => Empties.FirstOrDefault(e => e.Name == name);

	public float ReflectionFactor(string material) => Reflections.FirstOrDefault(r => r.Material == material)?.Factor ?? 1f;

	public HullManifestCoat CoatOf(string material) => Coat.FirstOrDefault(c => c.Material == material);

	// Piezas del contrato que no aparecen entre estos nombres
	public static IEnumerable<string> Missing(IEnumerable<string> present) => CockpitManifest.Missing(present, HullPieces.Contract);
}

// Nombres de las piezas del casco 3D: el contrato con el script de Blender (spec, 3.8). Godot las busca así.
public static class HullPieces
{
	public const string Root = "HullVector";
	public const string Canopy = "Canopy_Glass";
	public const string Smoke = "Smoke";

	public static readonly string[] Exhausts = { "Exhaust_L", "Exhaust_R" };
	public static readonly string[] Trails = { "Trail_L", "Trail_R" };
	public static readonly string[] Empties = { "Exhaust_L", "Exhaust_R", "Trail_L", "Trail_R", Smoke };

	public static readonly string[] Painted = { "Fuselage_Upper", "Endplate_L", "Endplate_R", "Pod_L_Cowl", "Pod_R_Cowl" };
	public static readonly string[] AccentEdges =
	{
		"Accent_Chine_L", "Accent_Chine_R", "Accent_WingLE_L", "Accent_WingLE_R",
		"Accent_Endplate_L", "Accent_Endplate_R", "Accent_PodBand_L", "Accent_PodBand_R"
	};
	public static readonly string[] AccentLights = { "Light_Nose_L", "Light_Nose_R", "Light_Fin_L", "Light_Fin_R" };
	public static readonly string[] TailLights = { "Light_Tail_L", "Light_Tail_R" };
	public static readonly string[] NozzleLights = { "Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R" };
	public static readonly string[] Stripes =
	{
		"Livery_TwinStripe_R_Front", "Livery_TwinStripe_R_Rear", "Livery_TwinStripe_L_Front", "Livery_TwinStripe_L_Rear",
		"Livery_WingBand_L", "Livery_WingBand_R"
	};
	public static readonly string[] Pins = { "Livery_WingPin_L", "Livery_WingPin_R" };
	public static readonly string[] Decals =
	{
		"Decal_Number_Nose", "Decal_Number_Pod_L", "Decal_Number_Pod_R", "Decal_Sponsor_L", "Decal_Sponsor_R",
		"Decal_Sponsor_Pod_L", "Decal_Sponsor_Pod_R", "Decal_Checker_L", "Decal_Checker_R"
	};

	// Las 14 luces que llevan el color de acento o el rojo de los pilotos (las que titilan con daño, spec 6.5)
	public static IEnumerable<string> FlickerLights => AccentEdges.Concat(AccentLights).Concat(TailLights);

	// Las piezas que solo se ven con librea
	public static IEnumerable<string> LiveryPieces => Stripes.Concat(Pins).Concat(Decals);

	public static readonly IReadOnlyList<string> Contract = Painted.Concat(AccentEdges).Concat(AccentLights)
		.Concat(TailLights).Concat(NozzleLights).Append(Canopy).Concat(Stripes).Concat(Pins).Concat(Decals).ToList();
}
