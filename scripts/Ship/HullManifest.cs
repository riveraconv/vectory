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

// Lo que el script de Blender del casco apunta y glTF no lleva (spec docs/cascos-3d-1-vector.md, 3.8, y
// docs/cascos-3d-2-blade.md, 3.8). Lógica pura.
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
	[JsonPropertyName("iridescence")] public List<HullManifestIridescence> Iridescence { get; set; } = new();

	// Solo BLADE: grupos de pintura, luces, sonda, máscaras del camuflaje y caja sin la sonda
	[JsonPropertyName("tips")] public List<string> Tips { get; set; } = new();
	[JsonPropertyName("armor")] public List<string> Armor { get; set; } = new();
	[JsonPropertyName("bottom")] public List<string> Bottom { get; set; } = new();
	[JsonPropertyName("navLights")] public List<string> NavLights { get; set; } = new();
	[JsonPropertyName("strobes")] public List<string> Strobes { get; set; } = new();
	[JsonPropertyName("flickerLights")] public List<string> FlickerLights { get; set; } = new();
	[JsonPropertyName("probe")] public string Probe { get; set; } = "";
	[JsonPropertyName("floor")] public List<string> Floor { get; set; } = new();
	[JsonPropertyName("repeats")] public List<HullManifestRepeat> Repeats { get; set; } = new();
	[JsonPropertyName("camoMasks")] public List<HullManifestCamoMask> CamoMasks { get; set; } = new();
	[JsonPropertyName("boundsWithoutProbe")] public CockpitManifestBounds BoundsWithoutProbe { get; set; } = new();

	// Solo HAMMER: grupos de pintura (casco y cabina), bordes del canard, luces, balizas, cristales, máscaras de la
	// pintura y caja sin los extras del techo
	[JsonPropertyName("paintedBody")] public List<string> PaintedBody { get; set; } = new();
	[JsonPropertyName("paintedCab")] public List<string> PaintedCab { get; set; } = new();
	[JsonPropertyName("leadingEdges")] public HullManifestLeadingEdges LeadingEdges { get; set; } = new();
	[JsonPropertyName("amberLights")] public List<string> AmberLights { get; set; } = new();
	[JsonPropertyName("workLights")] public List<string> WorkLights { get; set; } = new();
	[JsonPropertyName("beacons")] public List<HullManifestBeacon> Beacons { get; set; } = new();
	[JsonPropertyName("glass")] public List<string> Glass { get; set; } = new();
	[JsonPropertyName("paintMasks")] public List<HullManifestPaintMask> PaintMasks { get; set; } = new();
	[JsonPropertyName("boundsWithoutRoofExtras")] public CockpitManifestBounds BoundsWithoutRoofExtras { get; set; } = new();

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

	// Repetición de la textura de un material, ya multiplicada en sus UV (1 si no está)
	public float RepeatOf(string material) => Repeats.FirstOrDefault(r => r.Material == material)?.Repeat ?? 1f;

	public float IridescenceFactor(string material) => Iridescence.FirstOrDefault(i => i.Material == material)?.Factor ?? 0f;

	// Piezas del contrato de VECTOR que no aparecen entre estos nombres
	public static IEnumerable<string> Missing(IEnumerable<string> present) => CockpitManifest.Missing(present, HullPieces.Contract);

	// Piezas de un contrato que no aparecen entre estos nombres
	public static IEnumerable<string> Missing(IEnumerable<string> present, HullContract contract) =>
		CockpitManifest.Missing(present, contract.Pieces);
}

// Contrato de un casco 3D con su script de Blender: raíz, piezas que Godot busca por nombre y luces que titilan con el
// daño. Sale del estilo del casco. Lógica pura.
public sealed record HullContract(string Root, IReadOnlyList<string> Pieces, IReadOnlyList<string> FlickerLights,
	IReadOnlyList<string> Strobes)
{
	// Los puntos vacíos son los mismos en todos los cascos
	public static IReadOnlyList<string> Empties => HullPieces.Empties;

	public static readonly HullContract Racing = new(HullPieces.Root, HullPieces.Contract, HullPieces.FlickerLights.ToList(),
		System.Array.Empty<string>());
	public static readonly HullContract Military = new(MilitaryHullPieces.Root, MilitaryHullPieces.Contract,
		MilitaryHullPieces.FlickerLights, MilitaryHullPieces.Strobes);

	public static readonly HullContract Industrial = new(IndustrialHullPieces.Root, IndustrialHullPieces.Contract,
		IndustrialHullPieces.FlickerLights, System.Array.Empty<string>());

	public static HullContract For(HullStyle style) => style switch
	{
		HullStyle.Military => Military,
		HullStyle.Industrial => Industrial,
		_ => Racing
	};
}

// Iridiscencia de un material (cúpula de VECTOR, bola de sensores de BLADE): Godot no la tiene
public sealed class HullManifestIridescence
{
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("factor")] public float Factor { get; set; }
}

// Repetición de la textura de un material (BLADE), ya multiplicada en sus UV
public sealed class HullManifestRepeat
{
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("repeat")] public float Repeat { get; set; } = 1f;
}

// Máscara del camuflaje de un material (BLADE): archivo junto al manifiesto
public sealed class HullManifestCamoMask
{
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("mask")] public string Mask { get; set; } = "";
}

// Bordes del canard de HAMMER: con marcas un material, sin marcas otro, con la escala de UV que pasa de la repetición
// de las franjas (con la que van horneadas sus UV) a la del otro
public sealed class HullManifestLeadingEdges
{
	[JsonPropertyName("pieces")] public List<string> Pieces { get; set; } = new();
	[JsonPropertyName("withLivery")] public string WithLivery { get; set; } = "";
	[JsonPropertyName("withoutLivery")] public string WithoutLivery { get; set; } = "";
	[JsonPropertyName("uvScaleWithoutLivery")] public float UvScaleWithoutLivery { get; set; } = 1f;
}

// Baliza del techo de HAMMER: su lente, para qué sirve (boost o daño) y su color
public sealed class HullManifestBeacon
{
	[JsonPropertyName("name")] public string Name { get; set; } = "";
	[JsonPropertyName("role")] public string Role { get; set; } = "";
	[JsonPropertyName("color")] public string Color { get; set; } = "";
}

// Máscaras de un grupo de pintura de HAMMER (casco o cabina): color = pintura × MUL × mulScale + ADD. Archivos junto
// al manifiesto
public sealed class HullManifestPaintMask
{
	[JsonPropertyName("group")] public string Group { get; set; } = "";
	[JsonPropertyName("mulScale")] public float MulScale { get; set; } = 1f;
	[JsonPropertyName("seed")] public int Seed { get; set; }
	[JsonPropertyName("mul")] public string Mul { get; set; } = "";
	[JsonPropertyName("add")] public string Add { get; set; } = "";
	[JsonPropertyName("materials")] public List<string> Materials { get; set; } = new();
}

// Nombres de las piezas del casco 3D de VECTOR: el contrato con el script de Blender (spec, 3.8). Godot las busca así.
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
