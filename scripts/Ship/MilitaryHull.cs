using System;
using System.Collections.Generic;
using System.Linq;

namespace Vectory.Ship;

// Nombres de las piezas del casco 3D de BLADE, variante ASALTO: el contrato con art/blender/hull_blade.py (spec
// docs/cascos-3d-2-blade.md, 3.8). Godot las busca así.
public static class MilitaryHullPieces
{
	public const string Root = "HullBlade";
	public const string Probe = "Nose_Pitot";

	public static readonly string[] Painted = { "Fuselage_Upper", "Prong_L_Base", "Prong_R_Base", "Pod_L_Hump", "Pod_R_Hump", "Fairing" };
	public static readonly string[] Tips = { "Prong_L_Tip", "Prong_R_Tip" };
	public static readonly string[] Armor =
	{
		"Armor_0_L_Plate", "Armor_0_R_Plate", "Armor_1_L_Plate", "Armor_1_R_Plate",
		"Armor_2_L_Plate", "Armor_2_R_Plate", "Armor_3_L_Plate", "Armor_3_R_Plate"
	};
	public static readonly string[] Bottom = { "Fuselage_Lower" };
	public static readonly string[] Accents = { "Accent_FormNose_L", "Accent_FormNose_R", "Accent_FormProng_L", "Accent_FormProng_R" };
	public static readonly string[] NavLights = { "Light_Nav_L", "Light_Nav_R", "Light_Tip_L", "Light_Tip_R" };
	public static readonly string[] TailLights = { "Light_Tail" };
	public static readonly string[] Strobes = { "Light_Strobe_Top", "Light_Strobe_Bottom" };
	public static readonly string[] NozzleLights = { "Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R" };
	public static readonly string[] Glass = { "Canopy_Glass", "Sensor_Ball" };
	public static readonly string[] Decals = { "Decal_Number_L", "Decal_Number_R", "Decal_Insignia_L", "Decal_Insignia_R" };
	// Suelo de la cabina: el color base de la pintura, liso, para que el camuflaje no se vea a través del cristal
	public static readonly string[] Floor = { "Canopy_Floor" };

	// Las 9 luces que titilan con el daño (spec 6.5): la anticolisión y las toberas no
	public static readonly IReadOnlyList<string> FlickerLights = Accents.Concat(NavLights).Concat(TailLights).ToList();

	public static readonly IReadOnlyList<string> Contract = Painted.Concat(Tips).Concat(Armor).Concat(Bottom).Concat(Accents)
		.Concat(NavLights).Concat(TailLights).Concat(Strobes).Concat(NozzleLights).Concat(Glass).Concat(Decals).Append(Probe).Concat(Floor).ToList();
}

// Una pintura de camuflaje de tres tonos (base, claro y oscuro) en 0xRRGGBB. Las lisas llevan los tres iguales.
// Satin: acabado SATINADO; si no, MATE. Los nombres son internos.
public sealed record MilitaryPaint(string Id, string Name, uint Base, uint Light, uint Dark, bool Satin = false)
{
	public bool IsCamo => Base != Light || Base != Dark;
}

// Una de las 40 combinaciones: una pintura, sin marcas o con marcas (número, distintivo y franjas en las puntas)
public readonly record struct MilitaryPaintChoice(MilitaryPaint Paint, bool WithMarkings)
{
	public string DisplayName => WithMarkings ? $"{Paint.Name} · MARCAS" : Paint.Name;
}

// Catálogo de pinturas del casco 3D de BLADE (PAINTS del prototipo, spec docs/cascos-3d-2-blade.md, 6.1): 10
// camuflajes y 10 lisas, cada una sin y con marcas. Datos puros, con su paleta propia como las cabinas, que no sale de
// Palette. Por ahora no se elige en ningún menú ni se guarda.
public static class MilitaryPaintCatalog
{
	private static uint C(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

	private static MilitaryPaint Camo(string id, string name, (int, int, int) c0, (int, int, int) c1, (int, int, int) c2) =>
		new(id, name, C(c0.Item1, c0.Item2, c0.Item3), C(c1.Item1, c1.Item2, c1.Item3), C(c2.Item1, c2.Item2, c2.Item3));

	private static MilitaryPaint Plain(string id, string name, (int, int, int) c, bool satin = false)
	{
		uint color = C(c.Item1, c.Item2, c.Item3);
		return new MilitaryPaint(id, name, color, color, color, satin);
	}

	public static readonly IReadOnlyList<MilitaryPaint> Paints = new[]
	{
		Camo("woodland", "BOSQUE", (86, 96, 64), (118, 122, 96), (44, 51, 38)),
		Camo("desert", "DESIERTO", (196, 170, 122), (160, 128, 88), (112, 92, 64)),
		Camo("arctic", "ÁRTICO", (222, 224, 222), (176, 182, 186), (96, 102, 108)),
		Camo("urban", "URBANO", (128, 130, 132), (92, 94, 98), (52, 54, 58)),
		Camo("night", "NOCHE", (40, 44, 52), (62, 66, 76), (24, 26, 30)),
		Camo("jungle", "SELVA", (58, 82, 46), (92, 104, 60), (32, 48, 30)),
		Camo("tundra", "TUNDRA", (150, 148, 128), (184, 182, 166), (74, 78, 68)),
		Camo("naval_split", "MARINO", (92, 110, 130), (150, 160, 170), (56, 70, 88)),
		Camo("autumn", "OTOÑO", (120, 92, 56), (146, 132, 90), (56, 46, 34)),
		Camo("ash", "CENIZA", (100, 96, 90), (140, 136, 128), (62, 60, 58)),
		Plain("olive", "OLIVA", (86, 94, 62)),
		Plain("sand", "ARENA", (194, 170, 124)),
		Plain("field_grey", "GRIS CAMPAÑA", (104, 110, 104)),
		Plain("ops_black", "NEGRO OPERACIONES", (30, 32, 34), true),
		Plain("nato_green", "VERDE OTAN", (64, 74, 56)),
		Plain("earth", "MARRÓN TIERRA", (110, 86, 60)),
		Plain("navy_grey", "GRIS NAVAL", (118, 126, 134), true),
		Plain("winter_white", "BLANCO INVIERNO", (214, 216, 212)),
		Plain("slate_blue", "AZUL PIZARRA", (58, 70, 84)),
		Plain("khaki", "CAQUI", (150, 138, 100))
	};

	// Las 20 sin marcas y luego las 20 con marcas
	public static readonly IReadOnlyList<MilitaryPaintChoice> Combinations =
		Paints.Select(p => new MilitaryPaintChoice(p, false)).Concat(Paints.Select(p => new MilitaryPaintChoice(p, true))).ToList();

	// BOSQUE con marcas (la 21), la que sale al abrir el prototipo
	public static int DefaultIndex => Paints.Count;
	public static MilitaryPaintChoice Default => Combinations[DefaultIndex];

	public static MilitaryPaint Find(string id) => Paints.FirstOrDefault(p => p.Id == id);
}

// Acabado de una pintura militar (spec 6.2): metal, factor sobre la textura de rugosidad y barniz
public readonly record struct MilitaryFinish(string Name, float Metal, float RoughnessFactor, float Coat, float CoatRoughness)
{
	public static readonly MilitaryFinish Matte = new("MATE", 0.10f, 1.00f, 0f, 0f);
	public static readonly MilitaryFinish Satin = new("SATINADO", 0.20f, 0.75f, 0.25f, 0.35f);
	// El blindaje lleva 0,05 más de metal
	public const float ArmorExtraMetal = 0.05f;
}

// Lo que hay que poner en cada grupo de piezas del casco de BLADE con una pintura (spec 6.2 y 6.3). Lógica pura: el casco
// 3D lo recibe y lo pinta. Antirreflejos, metal, titanio, cristal, sensores y luces no cambian con la pintura.
public sealed record MilitaryHullLook
{
	public const uint MarkDark = 0x141612;
	public const uint MarkLight = 0xDEDECE;
	// Suma R + G + B de la media de la pintura a partir de la cual las marcas van oscuras
	public const double LightPaintSum = 360;

	public required uint[] Tones { get; init; }
	public required uint[] ArmorTones { get; init; }
	public required MilitaryFinish Finish { get; init; }
	public required uint Bottom { get; init; }
	public required uint Floor { get; init; }
	public required bool MarkingsVisible { get; init; }
	// Puntas traseras: franjas de aviso con marcas; la pintura sin ellas
	public required bool TipsHazard { get; init; }
	public required uint MarkColor { get; init; }

	public float ArmorMetal => Finish.Metal + MilitaryFinish.ArmorExtraMetal;

	public static MilitaryHullLook For(MilitaryPaintChoice choice)
	{
		MilitaryPaint p = choice.Paint;
		return new MilitaryHullLook
		{
			Tones = MilitaryHullPaint.Tones(p),
			ArmorTones = MilitaryHullPaint.ArmorTones(p),
			Finish = p.Satin ? MilitaryFinish.Satin : MilitaryFinish.Matte,
			Bottom = MilitaryHullPaint.Bottom(p),
			Floor = p.Base,
			MarkingsVisible = choice.WithMarkings,
			TipsHazard = choice.WithMarkings,
			MarkColor = MarkColorFor(p)
		};
	}

	// markCol: oscuras sobre pintura clara, claras sobre oscura
	public static uint MarkColorFor(MilitaryPaint p) => MilitaryHullPaint.Average(p).Sum() > LightPaintSum ? MarkDark : MarkLight;
}

// Cómo se aplica una pintura militar (spec 6.2), sin Godot: los tonos del blindaje (× 0,85), el color de la panza
// (botOf) y el color del camuflaje a partir de su máscara (camoSet). Lógica pura.
public static class MilitaryHullPaint
{
	// Pintura de por defecto: BOSQUE, la que sale al abrir el prototipo
	public static MilitaryPaint Woodland => MilitaryPaintCatalog.Paints[0];

	// Imprimación metálica que asoma en los desconchones
	public const uint ChipMetal = 0x767874;
	public const float ArmorDarkening = 0.85f;
	// Tono neutro hacia el que se aclara la panza, y cuánto
	public const uint BottomNeutral = 0x969691;
	public const float BottomMix = 0.35f;
	// Rugosidad de la panza: la textura (≈ 0,25 de media) × 2,6 del prototipo; Godot no admite factores mayores que 1
	public const float BottomRoughness = 0.65f;

	private static int R(uint c) => (int)((c >> 16) & 0xFF);
	private static int G(uint c) => (int)((c >> 8) & 0xFF);
	private static int B(uint c) => (int)(c & 0xFF);
	private static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

	// Math.round de JavaScript: la mitad hacia arriba
	private static int JsRound(double v) => (int)Math.Floor(v + 0.5);

	public static uint[] Tones(MilitaryPaint p) => new[] { p.Base, p.Light, p.Dark };

	// Los tres tonos × 0,85, redondeados
	public static uint[] ArmorTones(MilitaryPaint p) =>
		Tones(p).Select(c => Rgb(JsRound(R(c) * 0.85), JsRound(G(c) * 0.85), JsRound(B(c) * 0.85))).ToArray();

	// Media (2·base + claro + oscuro) / 4, por canal
	public static double[] Average(MilitaryPaint p)
	{
		Func<uint, int>[] ch = { R, G, B };
		return ch.Select(f => (f(p.Base) * 2.0 + f(p.Light) + f(p.Dark)) / 4.0).ToArray();
	}

	// botOf: la media aclarada un 35 % hacia el gris neutro
	public static uint Bottom(MilitaryPaint p)
	{
		double[] avg = Average(p);
		int[] neutral = { R(BottomNeutral), G(BottomNeutral), B(BottomNeutral) };
		int[] c = avg.Select((v, i) => JsRound(v + (neutral[i] - v) * BottomMix)).ToArray();
		return Rgb(c[0], c[1], c[2]);
	}

	// Color del camuflaje desde su máscara RGB8 (R tono: 0 base, 128 claro, 255 oscuro · G desconchón · B grano):
	// (desconchón ? metal : tono) × (0,9 + 0,2 × B), como camoSet del prototipo. Devuelve RGB8 del mismo tamaño.
	public static byte[] BakeCamo(byte[] mask, uint[] tones)
	{
		var out_ = new byte[mask.Length];
		for (int i = 0; i + 2 < mask.Length; i += 3)
		{
			byte tone = mask[i];
			uint c = mask[i + 1] > 127 ? ChipMetal : tones[tone < 64 ? 0 : tone < 192 ? 1 : 2];
			double k = 0.9 + 0.2 * mask[i + 2] / 255.0;
			out_[i] = Channel(R(c) * k);
			out_[i + 1] = Channel(G(c) * k);
			out_[i + 2] = Channel(B(c) * k);
		}
		return out_;
	}

	// Como Uint8ClampedArray: recorta a 0–255 y redondea a par
	private static byte Channel(double v) => (byte)Math.Round(Math.Clamp(v, 0.0, 255.0), MidpointRounding.ToEven);
}

// Dibujo de las calcomanías del casco de BLADE (spec 6.3): numberTex e insigniaTex del prototipo, con sus mismas
// medidas, en el color de las marcas. El distintivo es inventado: no es de ningún país ni organización. Lógica pura.
public static class MilitaryDecalDrawing
{
	public const string Number = "B-02";

	// Número en Black Ops One de 230 px, centrado en (512, 168) con la línea base en el medio; se estrecha si no cabe
	public static DecalCanvas NumberTex(uint color)
	{
		const int W = 1024, H = 320;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Text, W / 2f, H / 2f + 8, 0, 0, 0, color, Number, 230, false, DecalBaseline.Middle, W - 40, DecalFont.BlackOps)
		});
	}

	// Anillo de radio 228 y 26 px de grueso; dentro, la hoja en punta de flecha y una barra. Sin relleno.
	public static DecalCanvas InsigniaTex(uint color)
	{
		const int W = 512, H = 512;
		const float C = W / 2f;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Ring, C, C, 26, 0, 228, color),
			new DecalPrimitive(DecalShape.Polygon, 0, 0, 0, 0, 0, color, Points: new System.Numerics.Vector2[]
			{
				new(C, 70), new(C + 120, 330), new(C, 270), new(C - 120, 330)
			}),
			new DecalPrimitive(DecalShape.Rect, C - 150, 360, 300, 34, 0, color)
		});
	}

	// El dibujo de cada calcomanía del contrato (solo se ven con marcas); las de los dos lados comparten lienzo
	public static IReadOnlyDictionary<string, DecalCanvas> For(MilitaryHullLook look)
	{
		DecalCanvas number = NumberTex(look.MarkColor);
		DecalCanvas insignia = InsigniaTex(look.MarkColor);
		return new Dictionary<string, DecalCanvas>
		{
			["Decal_Number_L"] = number,
			["Decal_Number_R"] = number,
			["Decal_Insignia_L"] = insignia,
			["Decal_Insignia_R"] = insignia
		};
	}
}

// Anticolisión de BLADE (spec 6.4): destellos cortos y rojos; la de abajo, desfasada, así se alternan. No titila con
// el daño y la pausa la congela (el tiempo lo lleva la nave). Lógica pura.
public sealed record StrobeSettings
{
	public float Period { get; init; } = 1.2f;
	public float Duration { get; init; } = 0.06f;
	public float Offset { get; init; } = 0.6f;
	public float Intensity { get; init; } = 9f;
}

public static class StrobeLights
{
	// Intensidad de la luz número index (0 arriba, 1 abajo) en el tiempo t (s)
	public static float Intensity(float time, int index, StrobeSettings settings)
	{
		if (settings.Period <= 0f)
			return 0f;
		float phase = (time + index * settings.Offset) % settings.Period;
		if (phase < 0f)
			phase += settings.Period;
		return phase < settings.Duration ? settings.Intensity : 0f;
	}
}

// Las pinturas de los cascos 3D por estilo, como índices de sus 40 combinaciones (spec 6.2, depuración con F5)
public static class HullPaints
{
	public static int Count(HullStyle style) =>
		style == HullStyle.Military ? MilitaryPaintCatalog.Combinations.Count : HullPaintCatalog.Combinations.Count;

	// VECTOR: GRAFITO lisa; BLADE: BOSQUE con marcas
	public static int DefaultIndex(HullStyle style) =>
		style == HullStyle.Military ? MilitaryPaintCatalog.DefaultIndex : HullPaintCatalog.IndexOf(HullPaintCatalog.Default);

	public static string DisplayName(HullStyle style, int index) => style == HullStyle.Military
		? MilitaryPaintCatalog.Combinations[Wrap(style, index)].DisplayName
		: HullPaintCatalog.Combinations[Wrap(style, index)].DisplayName;

	// Siguiente o anterior combinación, con vuelta
	public static int Step(HullStyle style, int index, int delta) => Wrap(style, index + delta);

	private static int Wrap(HullStyle style, int index)
	{
		int n = Count(style);
		return (index % n + n) % n;
	}
}
