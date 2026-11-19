using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vectory.Ship;

// Una pieza viva de la cabina 3D, tal como la apunta el manifiesto
public sealed class CockpitManifestPiece
{
	[JsonPropertyName("name")] public string Name { get; set; } = "";
	[JsonPropertyName("width")] public float? Width { get; set; }
	[JsonPropertyName("height")] public float? Height { get; set; }
	[JsonPropertyName("emission")] public string Emission { get; set; }
	[JsonPropertyName("intensity")] public float? Intensity { get; set; }
}

public sealed class CockpitManifestAdditive
{
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("opacity")] public float Opacity { get; set; }
}

// Cuánto refleja un material respecto a lo normal (envMapIntensity del prototipo)
public sealed class CockpitManifestReflection
{
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("factor")] public float Factor { get; set; } = 1f;
}

public sealed class CockpitManifestBounds
{
	[JsonPropertyName("min")] public float[] Min { get; set; } = System.Array.Empty<float>();
	[JsonPropertyName("max")] public float[] Max { get; set; } = System.Array.Empty<float>();
}

// Una pieza de la cabina sacada del casco 3D (spec docs/cabinas-3d-4-morros.md, 5): su nombre en la cabina, el que
// tiene en el .glb del casco, su material del casco (en la cabina, con el prefijo de MaterialPrefix) y si es viva
public sealed class CockpitManifestHullPiece
{
	[JsonPropertyName("name")] public string Name { get; set; } = "";
	[JsonPropertyName("hull")] public string Hull { get; set; } = "";
	[JsonPropertyName("material")] public string Material { get; set; } = "";
	[JsonPropertyName("live")] public bool Live { get; set; }
}

// Copia del morro del casco dentro de la cabina (spec docs/cabinas-3d-4-morros.md, 3.1): ojo en coordenadas del casco,
// corte (z por detrás del ojo), piezas del grupo Nose y, en BLADE, las del casco que van enteras en Frame (barrotes)
public sealed class CockpitManifestNose
{
	[JsonPropertyName("hull")] public string Hull { get; set; } = "";
	[JsonPropertyName("eye")] public float[] Eye { get; set; } = System.Array.Empty<float>();
	[JsonPropertyName("cut")] public float Cut { get; set; }
	[JsonPropertyName("materialPrefix")] public string MaterialPrefix { get; set; } = "";
	[JsonPropertyName("pieces")] public List<CockpitManifestHullPiece> Pieces { get; set; } = new();
	[JsonPropertyName("frame")] public List<CockpitManifestHullPiece> Frame { get; set; } = new();
	[JsonPropertyName("bounds")] public CockpitManifestBounds Bounds { get; set; } = new();
}

// Lo que el script de Blender apunta y glTF no lleva (spec docs/cabinas-3d-1-vector.md, 3.7). Lógica pura.
public sealed class CockpitManifest
{
	[JsonPropertyName("blender")] public string Blender { get; set; } = "";
	[JsonPropertyName("root")] public string Root { get; set; } = "";
	[JsonPropertyName("pieces")] public List<CockpitManifestPiece> Pieces { get; set; } = new();
	[JsonPropertyName("noShadow")] public List<string> NoShadow { get; set; } = new();
	[JsonPropertyName("lowReflectionMaterials")] public List<string> LowReflectionMaterials { get; set; } = new();
	[JsonPropertyName("lowReflectionFactor")] public float LowReflectionFactor { get; set; } = 1f;
	[JsonPropertyName("additiveMaterials")] public List<CockpitManifestAdditive> AdditiveMaterials { get; set; } = new();
	// Solo en las cabinas que lo necesitan (BLADE): todos los reflejos distintos de 1 y los materiales transparentes
	[JsonPropertyName("reflections")] public List<CockpitManifestReflection> Reflections { get; set; } = new();
	[JsonPropertyName("transparentMaterials")] public List<CockpitManifestAdditive> TransparentMaterials { get; set; } = new();
	[JsonPropertyName("triangles")] public int Triangles { get; set; }
	[JsonPropertyName("bounds")] public CockpitManifestBounds Bounds { get; set; } = new();
	// Copia del morro del casco (spec docs/cabinas-3d-4-morros.md); grupos de VECTOR y centro del cristal HUD de BLADE
	[JsonPropertyName("nose")] public CockpitManifestNose Nose { get; set; } = new();
	[JsonPropertyName("zones")] public List<string> Zones { get; set; } = new();
	[JsonPropertyName("combinerCenterY")] public float? CombinerCenterY { get; set; }

	// Null si el texto no se puede leer
	public static CockpitManifest Parse(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<CockpitManifest>(json);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public CockpitManifestPiece Piece(string name) => Pieces.FirstOrDefault(p => p.Name == name);

	// Cuánto refleja un material: la lista completa si el manifiesto la trae; si no, la de los que reflejan menos
	public float ReflectionFactor(string material)
	{
		if (Reflections.Count > 0)
			return Reflections.FirstOrDefault(r => r.Material == material)?.Factor ?? 1f;
		return LowReflectionMaterials.Contains(material) ? LowReflectionFactor : 1f;
	}

	// Piezas del contrato de VECTOR que no aparecen entre estos nombres
	public static IEnumerable<string> Missing(IEnumerable<string> present) => Missing(present, Cockpit3DPieces.Contract);

	// Piezas de un contrato que no aparecen entre estos nombres
	public static IEnumerable<string> Missing(IEnumerable<string> present, IEnumerable<string> contract)
	{
		var names = new HashSet<string>(present);
		return contract.Where(n => !names.Contains(n));
	}
}

// Nombres de las piezas de la cabina 3D: el contrato con el script de Blender (spec, 3.7). Godot las busca así.
public static class Cockpit3DPieces
{
	public const int ShiftLightCount = 15;
	public const int ConsoleLedCount = 4;

	public const string ScreenLeft = "Screen_L";
	public const string ScreenRight = "Screen_R";
	public const string ScreenRadar = "Screen_Radar";
	public const string ScreenMap = "Screen_Map";
	public const string GlassSmudge = "Glass_Smudge";

	public static readonly string[] LargeScreens = { ScreenLeft, ScreenRight };
	public static readonly string[] SmallScreens = { ScreenRadar, ScreenMap };

	public static readonly string[] Accents = { "Accent_Visor", "Accent_Screen_L", "Accent_Screen_R", "Accent_Radar", "Accent_Map" };

	// Las luces que titilan con daño (spec, 6.5): ni las shift lights ni las pantallas
	public static readonly IReadOnlyList<string> FlickerLights = BuildFlickerLights();

	public static string ShiftLight(int index) => $"ShiftLight_{index:00}";
	public static string ShiftLightBase(int index) => $"ShiftLight_Base_{index:00}";
	public static string ConsoleLed(int index) => $"Led_Console_{index}";
	public static string Shade(string screen) => screen + "_Shade";

	public static readonly IReadOnlyList<string> Contract = BuildContract();

	private static IReadOnlyList<string> BuildFlickerLights()
	{
		// Las luces del morro ya no son de la cabina: son las de la copia del casco (spec docs/cabinas-3d-4-morros.md)
		var names = new List<string>(Accents);
		for (int i = 0; i < ConsoleLedCount; i++)
			names.Add(ConsoleLed(i));
		return names;
	}

	private static IReadOnlyList<string> BuildContract()
	{
		var names = new List<string>();
		foreach (string screen in LargeScreens.Concat(SmallScreens))
		{
			names.Add(screen);
			names.Add(Shade(screen));
		}
		names.AddRange(Accents);
		names.Add(GlassSmudge);
		for (int i = 0; i < ShiftLightCount; i++)
		{
			names.Add(ShiftLight(i));
			names.Add(ShiftLightBase(i));
		}
		for (int i = 0; i < ConsoleLedCount; i++)
			names.Add(ConsoleLed(i));
		return names;
	}
}
