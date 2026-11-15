using System.Collections.Generic;

namespace Vectory.Ship;

// Ring: circunferencia de radio Radius y W px de grueso. Polygon: relleno con Points.
public enum DecalShape { RoundRect, Circle, Rect, Text, Ring, Polygon }

// Fuente de un texto: Orbitron Black (VECTOR) o Black Ops One (BLADE)
public enum DecalFont { Orbitron, BlackOps }

// Línea base del texto, como textBaseline del lienzo del prototipo
public enum DecalBaseline { Middle, Alphabetic }

// Una primitiva de una calcomanía, en píxeles de su lienzo (origen arriba a la izquierda). Los textos van centrados
// en X; MaxWidth > 0 los estrecha si no caben (el maxWidth de fillText).
public readonly record struct DecalPrimitive(
	DecalShape Shape,
	float X, float Y, float W, float H, float Radius,
	uint Color,
	string Text = null,
	float FontSize = 0f,
	bool Italic = false,
	DecalBaseline Baseline = DecalBaseline.Alphabetic,
	float MaxWidth = 0f,
	DecalFont Font = DecalFont.Orbitron,
	System.Numerics.Vector2[] Points = null);

// Lienzo de una calcomanía: tamaño en píxeles (fondo transparente) y lo que se pinta, en orden
public sealed record DecalCanvas(int Width, int Height, IReadOnlyList<DecalPrimitive> Primitives);

// Dibujo de las calcomanías del casco 3D (spec docs/cascos-3d-1-vector.md, 6.3): numberPlate, numberRoundel, sponsorTex y
// checkerTex del prototipo, con sus mismas medidas. Fuente Orbitron Black, en cursiva donde el prototipo la pone.
// Lógica pura: un nodo de Godot pinta estas primitivas una vez en una textura.
public static class HullDecalDrawing
{
	public const string SponsorFlank = "NULLDRIVE";
	public const string SponsorFlankSubtitle = "KAIROS GRID · VX-7";
	public const string SponsorPod = "VECTORY";
	public const uint CheckerLight = 0xF2F2F2;
	public const uint CheckerDark = 0x0B0B0B;

	// Dorsal rectangular (góndolas)
	public static DecalCanvas NumberPlate(HullLivery livery)
	{
		const int W = 640, H = 384;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.RoundRect, 6, 6, W - 12, H - 12, 46, livery.Stripe),
			new DecalPrimitive(DecalShape.RoundRect, 26, 26, W - 52, H - 52, 34, livery.NumberBackground),
			new DecalPrimitive(DecalShape.Text, W / 2f, H / 2f + 10, 0, 0, 0, livery.NumberColor, livery.Number, 250, true, DecalBaseline.Middle)
		});
	}

	// Dorsal redondo (nariz)
	public static DecalCanvas NumberRoundel(HullLivery livery)
	{
		const int W = 512, H = 512;
		float size = livery.Number.Length > 1 ? 200 : 250;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Circle, W / 2f, H / 2f, 0, 0, 250, livery.Stripe),
			new DecalPrimitive(DecalShape.Circle, W / 2f, H / 2f, 0, 0, 222, livery.NumberBackground),
			new DecalPrimitive(DecalShape.Text, W / 2f, H / 2f + 10, 0, 0, 0, livery.NumberColor, livery.Number, size, true, DecalBaseline.Middle)
		});
	}

	// Pegatina de texto con barra de color, todo centrado: una línea en cursiva y, si la hay, otra debajo
	public static DecalCanvas Sponsor(string title, string subtitle, uint text, uint bar)
	{
		const int W = 1024, H = 256;
		bool two = !string.IsNullOrEmpty(subtitle);
		var list = new List<DecalPrimitive>
		{
			new(DecalShape.Rect, 0, H - 46, W, 22, 0, bar),
			new(DecalShape.Rect, W * 0.19f, H - 18, W * 0.62f, 10, 0, bar),
			new(DecalShape.Text, W / 2f, two ? 112 : 176, 0, 0, 0, text, title, two ? 104 : 150, true, DecalBaseline.Alphabetic, W - 24)
		};
		if (two)
			list.Add(new DecalPrimitive(DecalShape.Text, W / 2f, 186, 0, 0, 0, text, subtitle, 64, false, DecalBaseline.Alphabetic, W - 24));
		return new DecalCanvas(W, H, list);
	}

	// Damero de cuadros de 32 px
	public static DecalCanvas Checker()
	{
		const int W = 512, H = 128, Q = 32;
		var list = new List<DecalPrimitive>();
		for (int i = 0; i < W / Q; i++)
			for (int j = 0; j < H / Q; j++)
				list.Add(new DecalPrimitive(DecalShape.Rect, i * Q, j * Q, Q, Q, 0, (i + j) % 2 == 1 ? CheckerDark : CheckerLight));
		return new DecalCanvas(W, H, list);
	}

	// El dibujo de cada calcomanía del contrato con una pintura (solo se ven con librea)
	public static IReadOnlyDictionary<string, DecalCanvas> For(HullPaintLook look)
	{
		DecalCanvas plate = NumberPlate(look.Livery);
		DecalCanvas flank = Sponsor(SponsorFlank, SponsorFlankSubtitle, look.TextColor, look.StripeColor);
		DecalCanvas pod = Sponsor(SponsorPod, null, look.TextColor, look.StripeColor);
		DecalCanvas checker = Checker();
		return new Dictionary<string, DecalCanvas>
		{
			["Decal_Number_Nose"] = NumberRoundel(look.Livery),
			["Decal_Number_Pod_L"] = plate,
			["Decal_Number_Pod_R"] = plate,
			["Decal_Sponsor_L"] = flank,
			["Decal_Sponsor_R"] = flank,
			["Decal_Sponsor_Pod_L"] = pod,
			["Decal_Sponsor_Pod_R"] = pod,
			["Decal_Checker_L"] = checker,
			["Decal_Checker_R"] = checker
		};
	}
}
