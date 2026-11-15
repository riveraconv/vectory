using System;
using System.Collections.Generic;
using System.Linq;

namespace Vectory.Ship;

// Acabado de una pintura (FINISH del prototipo, spec docs/cascos-3d-1-vector.md, 6.1). Roughness es el valor final:
// el prototipo multiplica la textura (≈ 0,25 de media) por un factor y Godot no admite factores mayores que 1.
public sealed record HullFinish(string Name, float Metal, float RoughnessFactor, float Roughness, float Coat,
	float CoatRoughness, float Iridescence);

// Datos de la librea de una pintura: franjas, fondo y color del número del dorsal, y el número
public sealed record HullLivery(uint Stripe, uint NumberBackground, uint NumberColor, string Number);

// Una pintura del catálogo: color en 0xRRGGBB, acabado, color de acento y su librea. Los nombres son internos.
public sealed record HullPaint(string Id, string Name, uint Color, HullFinish Finish, uint Accent, HullLivery Livery);

// Una de las 40 combinaciones: una pintura, lisa o con librea de carreras
public readonly record struct HullPaintChoice(HullPaint Paint, bool WithLivery)
{
	public string DisplayName => WithLivery ? $"{Paint.Name} #{Paint.Livery.Number}" : Paint.Name;
}

// Catálogo de pinturas del casco 3D de VECTOR (FINISH y PAINTS del prototipo). Datos puros: cada pintura con su paleta
// propia, como las cabinas, que no sale de Palette. Por ahora no se elige en ningún menú ni se guarda.
public static class HullPaintCatalog
{
	public static readonly HullFinish Gloss = new("BRILLO", 0.1f, 1.0f, 0.25f, 1.0f, 0.04f, 0f);
	public static readonly HullFinish Metallic = new("METALIZADO", 0.6f, 1.4f, 0.35f, 0.8f, 0.10f, 0f);
	public static readonly HullFinish Satin = new("SATINADO", 0.6f, 1.7f, 0.42f, 0.5f, 0.22f, 0f);
	public static readonly HullFinish Matte = new("MATE", 0.2f, 3.2f, 0.80f, 0f, 0.50f, 0f);
	public static readonly HullFinish Chrome = new("CROMADO", 1.0f, 0.5f, 0.12f, 1.0f, 0.03f, 0f);
	public static readonly HullFinish Pearl = new("PERLA", 0.25f, 1.0f, 0.25f, 1.0f, 0.04f, 0.35f);
	public static readonly HullFinish Chameleon = new("CAMALEÓN", 0.5f, 1.0f, 0.25f, 1.0f, 0.04f, 1.0f);

	public static readonly IReadOnlyList<HullFinish> Finishes = new[] { Gloss, Metallic, Satin, Matte, Chrome, Pearl, Chameleon };

	private static HullPaint P(string id, string name, uint color, HullFinish finish, uint accent,
		uint stripe, uint numberBackground, uint numberColor, string number)
		=> new(id, name, color, finish, accent, new HullLivery(stripe, numberBackground, numberColor, number));

	public static readonly IReadOnlyList<HullPaint> Paints = new[]
	{
		P("graphite_red", "GRAFITO", 0x222429, Satin, 0xFF2D2D, 0xFF2D2D, 0xF2F2F2, 0x111111, "07"),
		P("piano_cyan", "NEGRO PIANO", 0x0A0B0D, Gloss, 0x36E7FF, 0x36E7FF, 0x36E7FF, 0x050505, "21"),
		P("black_chrome", "CROMO NEGRO", 0x2A2C31, Chrome, 0xFF9D32, 0xFF9D32, 0x111111, 0xFF9D32, "88"),
		P("stealth_matte", "SIGILO", 0x2B2E33, Matte, 0xFF2D2D, 0xFF2D2D, 0xFF2D2D, 0x111111, "00"),
		P("pearl_white", "BLANCO PERLA", 0xE4E6EA, Pearl, 0xFF2D2D, 0x1A42B0, 0xFFFFFF, 0x1A42B0, "14"),
		P("liquid_silver", "PLATA LÍQUIDA", 0xC3C8CF, Chrome, 0x3D7BFF, 0xB5121B, 0xFFFFFF, 0xB5121B, "3"),
		P("titanium", "TITANIO", 0x6A6E75, Metallic, 0xFFB000, 0xFFB000, 0x111111, 0xFFB000, "42"),
		P("racing_red", "ROJO CARRERAS", 0xB5121B, Gloss, 0xFFF4E6, 0xF2F2F2, 0xF2F2F2, 0xB5121B, "1"),
		P("lava", "LAVA", 0x5A0A0C, Metallic, 0xFF9D32, 0xFF9D32, 0xFF9D32, 0x1A0505, "66"),
		P("amber", "ÁMBAR", 0xE0701A, Gloss, 0xFFFFFF, 0x111111, 0x111111, 0xFFFFFF, "27"),
		P("taxi", "AMARILLO TAXI", 0xE8B414, Gloss, 0xFF2D2D, 0x111111, 0x111111, 0xE8B414, "99"),
		P("gold", "ORO", 0xC8963A, Chrome, 0xFFF4E6, 0x111111, 0x111111, 0xC8963A, "24"),
		P("acid_green", "VERDE ÁCIDO", 0x7CC81A, Gloss, 0xFFFFFF, 0x111111, 0xFFFFFF, 0x111111, "55"),
		P("british_green", "VERDE INGLÉS", 0x0F3A28, Metallic, 0x3DFF8A, 0xE8D9A8, 0xE8D9A8, 0x0F3A28, "9"),
		P("cobalt", "COBALTO", 0x1A42B0, Metallic, 0x36E7FF, 0xF2F2F2, 0xF2F2F2, 0x1A42B0, "12"),
		P("midnight", "MEDIANOCHE", 0x0E1634, Metallic, 0x6F9BFF, 0xC8963A, 0xC8963A, 0x0E1634, "33"),
		P("ice", "HIELO", 0x9FD8E8, Pearl, 0x36E7FF, 0x1A42B0, 0xFFFFFF, 0x1A42B0, "71"),
		P("violet", "VIOLETA", 0x4B1A82, Metallic, 0xFF43D0, 0xE8B414, 0xE8B414, 0x4B1A82, "18"),
		P("neon_pink", "ROSA NEÓN", 0xD42E7C, Gloss, 0x36E7FF, 0x111111, 0x36E7FF, 0x111111, "23"),
		P("chameleon", "CAMALEÓN", 0x1C1630, Chameleon, 0xFF43D0, 0xE4E6EA, 0xE4E6EA, 0x1C1630, "13")
	};

	// Las 20 lisas y luego las 20 con librea
	public static readonly IReadOnlyList<HullPaintChoice> Combinations =
		Paints.Select(p => new HullPaintChoice(p, false)).Concat(Paints.Select(p => new HullPaintChoice(p, true))).ToList();

	// GRAFITO lisa
	public static HullPaintChoice Default => Combinations[0];

	public static HullPaint Find(string id) => Paints.FirstOrDefault(p => p.Id == id);

	public static int IndexOf(HullPaintChoice choice) =>
		Paints.ToList().IndexOf(choice.Paint) + (choice.WithLivery ? Paints.Count : 0);

	// Siguiente o anterior combinación, con vuelta
	public static int Step(int index, int delta)
	{
		int n = Combinations.Count;
		return ((index + delta) % n + n) % n;
	}
}

// Lo que hay que poner en cada grupo de piezas del casco con una pintura (spec 6.2 y 6.3). Lógica pura: el casco 3D lo
// recibe y lo pinta. El lomo, las alas, la panza, las toberas y el resto no cambian con la pintura.
public sealed record HullPaintLook
{
	// Pilotos traseros y fondo de las toberas: fijos, no siguen al acento
	public const uint TailColor = 0xFF1A1A;
	public const uint NozzleGlowColor = 0x245BFF;
	public const uint NozzleCoreColor = 0x7FA6FF;
	public const uint TextDark = 0x111111;
	public const uint TextLight = 0xF2F2F2;
	// Suma R + G + B (0–255 cada uno) a partir de la cual la pintura es clara y el texto va oscuro
	public const int LightPaintSum = 360;

	public required uint PaintColor { get; init; }
	public required HullFinish PaintFinish { get; init; }
	public required uint AccentColor { get; init; }
	// Derivas: la pintura sin librea; con librea, el color y el acabado de las franjas
	public required uint EndplateColor { get; init; }
	public required HullFinish EndplateFinish { get; init; }
	public required bool LiveryVisible { get; init; }
	public required uint StripeColor { get; init; }
	public required uint PinColor { get; init; }
	public required uint TextColor { get; init; }
	public required HullLivery Livery { get; init; }

	// Franjas, filete y derivas con librea: BRILLO sobre la textura neutra, con el metal del prototipo (stripeMat)
	public static readonly HullFinish StripeFinish = HullPaintCatalog.Gloss;

	public static HullPaintLook For(HullPaintChoice choice)
	{
		HullPaint p = choice.Paint;
		bool livery = choice.WithLivery;
		return new HullPaintLook
		{
			PaintColor = p.Color,
			PaintFinish = p.Finish,
			AccentColor = p.Accent,
			EndplateColor = livery ? p.Livery.Stripe : p.Color,
			EndplateFinish = livery ? StripeFinish : p.Finish,
			LiveryVisible = livery,
			StripeColor = p.Livery.Stripe,
			PinColor = p.Livery.NumberBackground,
			TextColor = TextColorFor(p.Color),
			Livery = p.Livery
		};
	}

	public static uint TextColorFor(uint paint)
	{
		int sum = (int)((paint >> 16) & 0xFF) + (int)((paint >> 8) & 0xFF) + (int)(paint & 0xFF);
		return sum > LightPaintSum ? TextDark : TextLight;
	}

	// Color de emisión de cada luz viva del casco
	public uint LightColor(string piece)
	{
		if (HullPieces.TailLights.Contains(piece))
			return TailColor;
		if (piece.StartsWith("Nozzle_Glow", StringComparison.Ordinal))
			return NozzleGlowColor;
		if (piece.StartsWith("Nozzle_Core", StringComparison.Ordinal))
			return NozzleCoreColor;
		return AccentColor;
	}
}
