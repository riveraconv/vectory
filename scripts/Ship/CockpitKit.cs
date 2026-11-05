using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Piezas comunes de las cabinas, traducidas de designs/lib.js: mismos puntos, orden, colores y opacidades.
// Los degradados son los de sus <defs>. Lógica pura.
public static class CockpitKit
{
	public static readonly IReadOnlyDictionary<CockpitGradientId, CockpitGradient> Gradients = new Dictionary<CockpitGradientId, CockpitGradient>
	{
		[CockpitGradientId.Top] = Vertical((0f, CockpitColor.PanelLight), (0.25f, CockpitColor.PanelMid), (1f, CockpitColor.PanelDark)),
		[CockpitGradientId.Console] = Vertical((0f, CockpitColor.ConsoleLight), (1f, CockpitColor.ConsoleDark)),
		[CockpitGradientId.Front] = Vertical((0f, CockpitColor.FrontLight), (1f, CockpitColor.FrontDark)),
		[CockpitGradientId.Pillar] = Horizontal((0f, CockpitColor.PillarLight), (1f, CockpitColor.ConsoleDark)),
		[CockpitGradientId.Side] = Horizontal((0f, CockpitColor.PanelDark), (1f, CockpitColor.FrontDark)),
		[CockpitGradientId.NoseLeft] = Horizontal((0f, CockpitColor.ConsoleDark), (1f, CockpitColor.PanelLight)),
		[CockpitGradientId.NoseRight] = Horizontal((0f, CockpitColor.NoseLight), (1f, CockpitColor.FrontLight)),
		[CockpitGradientId.ScreenGlow] = new CockpitGradient(true, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new[]
		{
			new CockpitGradientStop(0f, CockpitColor.Cyan, 0.22f),
			new CockpitGradientStop(1f, CockpitColor.Cyan, 0f)
		}),
		[CockpitGradientId.TintOrange] = new CockpitGradient(true, new Vector2(0.5f, 0.55f), new Vector2(1.25f, 0.55f), new[]
		{
			new CockpitGradientStop(0.55f, CockpitColor.Orange, 0f),
			new CockpitGradientStop(1f, CockpitColor.Orange, 0.35f)
		}),
		[CockpitGradientId.TintRed] = new CockpitGradient(true, new Vector2(0.5f, 0.55f), new Vector2(1.25f, 0.55f), new[]
		{
			new CockpitGradientStop(0.5f, CockpitColor.Red, 0f),
			new CockpitGradientStop(1f, CockpitColor.Red, 0.4f)
		}),
		[CockpitGradientId.ShadowDown] = new CockpitGradient(false, Vector2.Zero, Vector2.UnitY, new[]
		{
			new CockpitGradientStop(0f, CockpitColor.Black, 0.7f),
			new CockpitGradientStop(1f, CockpitColor.Black, 0f)
		}),
		[CockpitGradientId.OcclusionLeft] = new CockpitGradient(false, Vector2.Zero, Vector2.UnitX, new[]
		{
			new CockpitGradientStop(0f, CockpitColor.Black, 0.55f),
			new CockpitGradientStop(1f, CockpitColor.Black, 0f)
		}),
		[CockpitGradientId.OcclusionRight] = new CockpitGradient(false, Vector2.UnitX, Vector2.Zero, new[]
		{
			new CockpitGradientStop(0f, CockpitColor.Black, 0.55f),
			new CockpitGradientStop(1f, CockpitColor.Black, 0f)
		}),
		[CockpitGradientId.Cowl] = Vertical((0f, CockpitColor.CowlLight), (0.5f, CockpitColor.NoseLight), (1f, CockpitColor.FrontLight)),
		[CockpitGradientId.DeckFar] = Vertical((0f, CockpitColor.PanelMid), (1f, CockpitColor.ConsoleLight)),
		[CockpitGradientId.BezelIn] = new CockpitGradient(false, Vector2.Zero, Vector2.UnitY, new[]
		{
			new CockpitGradientStop(0f, CockpitColor.Black, 0.8f),
			new CockpitGradientStop(1f, CockpitColor.Black, 0f)
		})
	};

	private static CockpitGradient Vertical(params (float Offset, CockpitColor Color)[] stops) =>
		new(false, Vector2.Zero, Vector2.UnitY, Array.ConvertAll(stops, s => new CockpitGradientStop(s.Offset, s.Color)));

	private static CockpitGradient Horizontal(params (float Offset, CockpitColor Color)[] stops) =>
		new(false, Vector2.Zero, Vector2.UnitX, Array.ConvertAll(stops, s => new CockpitGradientStop(s.Offset, s.Color)));

	// Reflejos tenues en el cristal
	public static void Glass(this CockpitCanvas c)
	{
		const float Opacity = 0.07f;
		c.Line(new Vector2(380f, 120f), new Vector2(520f, 40f), CockpitColor.GlassReflection, 2f, Opacity);
		c.Line(new Vector2(410f, 140f), new Vector2(580f, 44f), CockpitColor.GlassReflection, 2f, Opacity);
		c.Line(new Vector2(900f, 260f), new Vector2(1000f, 200f), CockpitColor.GlassReflection, 2f, Opacity);
	}

	// Panel con volumen: cara, bisel interior, arista luminosa y brillo superior. depth > 0 añade una cara frontal por debajo.
	public static void Panel(this CockpitCanvas c, IReadOnlyList<Vector2> a, CockpitFill? fill = null, float depth = 0f, float? bevel = 7f, IReadOnlyList<Vector2> highlight = null)
	{
		if (depth > 0f)
			c.Polygon(CockpitGeometry.Offset(a, 0f, depth), CockpitGradientId.Front, CockpitColor.Blue, 1f, strokeOpacity: 0.6f);
		c.Polygon(a, fill ?? CockpitGradientId.Top, CockpitColor.Cyan, 1.6f);
		if (bevel.HasValue)
			c.Polygon(CockpitGeometry.Inset(a, bevel.Value), null, CockpitColor.Blue, 1f, opacity: 0.5f);
		if (highlight != null)
			c.Line(highlight, CockpitColor.Highlight, 1f, 0.35f);
	}

	public static void Screw(this CockpitCanvas c, float x, float y)
	{
		c.ScrewCircle(x, y);
		c.Line(new Vector2(x - 2f, y), new Vector2(x + 2f, y), CockpitColor.Blue, 0.8f, 1f);
	}

	// El diseño pasa aquí la x como texto ya redondeado a un decimal, así que su "x + 2" concatena ("123.4" + 2 = 123.42)
	// y la ranura del tornillo sale por la mitad. Se respeta: manda el archivo de diseño.
	public static void ScrewRoundedX(this CockpitCanvas c, float x, float y)
	{
		float rounded = MathF.Round(x * 10f) / 10f;
		c.ScrewCircle(rounded, y);
		c.Line(new Vector2(rounded - 2f, y), new Vector2(rounded + 0.02f, y), CockpitColor.Blue, 0.8f, 1f);
	}

	private static void ScrewCircle(this CockpitCanvas c, float x, float y)
	{
		c.Circle(x, y, 3f, CockpitColor.Screw, CockpitColor.Blue, 1f, 0.9f);
	}

	public static void Vent(this CockpitCanvas c, float x, float y, float w, float h, int n, float slant)
	{
		c.Rect(x, y, w, h, CockpitColor.Vent, CockpitColor.Blue, 1f, 0.9f);
		var pairs = new List<Vector2>(n * 2);
		for (int i = 0; i < n; i++)
		{
			float xx = x + (i + 0.5f) * w / n;
			pairs.Add(new Vector2(xx - slant, y + h));
			pairs.Add(new Vector2(xx + slant, y));
		}
		c.Segments(pairs, CockpitColor.Blue, 1.2f, 0.55f);
	}

	public static void Switches(this CockpitCanvas c, float x, float y, int n, float gap, params int[] lit)
	{
		for (int i = 0; i < n; i++)
		{
			float xx = x + i * gap;
			bool on = Array.IndexOf(lit, i) >= 0;
			c.Rect(xx, y, 10f, 14f, CockpitColor.Screw, CockpitColor.Blue, cornerRadius: 1.5f);
			c.Rect(xx + 3f, y + 3f, 4f, 5f, CockpitColor.BlueGray, opacity: 0.45f);
			c.Circle(xx + 5f, y - 5f, 1.6f, on ? CockpitColor.Cyan : CockpitColor.Blue, opacity: on ? 0.9f : 0.5f);
		}
	}

	public static void Seam(this CockpitCanvas c, Vector2 a, Vector2 b)
	{
		c.Line(new[] { a, b }, CockpitColor.Blue, 1f, 0.4f, 6f, 5f);
	}

	// Marco de pantalla con esquinas
	public static void Brackets(this CockpitCanvas c, float x, float y, float w, float h, float l)
	{
		void Corner(Vector2 a, Vector2 b, Vector2 d) => c.Line(new[] { a, b, d }, CockpitColor.Cyan, 1.2f, 0.8f);

		Corner(new Vector2(x, y + l), new Vector2(x, y), new Vector2(x + l, y));
		Corner(new Vector2(x + w - l, y), new Vector2(x + w, y), new Vector2(x + w, y + l));
		Corner(new Vector2(x, y + h - l), new Vector2(x, y + h), new Vector2(x + l, y + h));
		Corner(new Vector2(x + w - l, y + h), new Vector2(x + w, y + h), new Vector2(x + w, y + h - l));
	}

	// Pantalla: fondo y líneas de barrido; con glow, antes el halo vivo que ilumina el panel de alrededor
	public static void Screen(this CockpitCanvas c, IReadOnlyList<Vector2> polygon, Vector2? glow = null)
	{
		if (glow.HasValue)
		{
			c.Add(new CockpitElement
			{
				Kind = CockpitElementKind.Ellipse,
				Center = CockpitGeometry.Centroid(polygon),
				Radii = glow.Value,
				Fill = CockpitGradientId.ScreenGlow,
				Slot = CockpitSlot.ScreenGlow
			});
		}
		c.Polygon(polygon, CockpitColor.Screen, CockpitColor.Blue);
		c.Polygon(polygon, CockpitGradientId.Scan, opacity: 0.5f);
	}

	public static void Micro(this CockpitCanvas c, float x, float y, string text, CockpitTextAnchor anchor = CockpitTextAnchor.Start, CockpitColor color = CockpitColor.BlueGray, float opacity = 0.6f, float size = 9f, CockpitSlot slot = CockpitSlot.None)
	{
		c.Text(x, y, text, size, 1.5f, anchor, color, opacity, slot);
	}

	public static void Label(this CockpitCanvas c, float x, float y, string text, float size = 11f, CockpitTextAnchor anchor = CockpitTextAnchor.Middle, CockpitColor color = CockpitColor.BlueGray, float opacity = 0.85f)
	{
		c.Text(x, y, text, size, 2f, anchor, color, opacity);
	}

	public static void Text(this CockpitCanvas c, float x, float y, string text, float size, float spacing, CockpitTextAnchor anchor, CockpitColor color, float opacity, CockpitSlot slot = CockpitSlot.None)
	{
		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Text,
			Position = new Vector2(x, y),
			Text = text,
			FontSize = size,
			LetterSpacing = spacing,
			Anchor = anchor,
			Fill = color,
			Opacity = opacity,
			Slot = slot
		});
	}

	// Hueco en el frontal: las carcasas del HUD quedan hundidas
	public static void Sink(this CockpitCanvas c, IReadOnlyList<Vector2> frame, float grow)
	{
		Vector2[] rim = CockpitGeometry.Inset(frame, -grow);
		c.Polygon(rim, CockpitColor.Sink, CockpitColor.Cyan, 1.4f);

		// Paredes interiores del hueco: arriba en sombra, lados algo más claros, abajo con un poco de luz
		(int I, int J, CockpitFill Fill, float Opacity)[] walls =
		{
			(0, 1, CockpitGradientId.BezelIn, 1f),
			(1, 2, CockpitColor.FrontLight, 0.9f),
			(3, 0, CockpitColor.FrontLight, 0.9f),
			(2, 3, CockpitColor.SinkWall, 0.8f)
		};
		foreach ((int i, int j, CockpitFill fill, float opacity) in walls)
			c.Polygon(new[] { rim[i], rim[j], frame[j], frame[i] }, fill, CockpitColor.Blue, 0.8f, opacity, 0.5f);

		c.Line(rim[0], rim[1], CockpitColor.Highlight, 1f, 0.3f);
	}

	// Tintes de luz: boost (naranja) y daño fuerte (rojo) desde los bordes
	public static void Tints(this CockpitCanvas c)
	{
		foreach ((CockpitGradientId gradient, CockpitSlot slot) in new[]
		{
			(CockpitGradientId.TintOrange, CockpitSlot.BoostTint),
			(CockpitGradientId.TintRed, CockpitSlot.DamageTint)
		})
		{
			c.Add(new CockpitElement
			{
				Kind = CockpitElementKind.Rect,
				Size = new Vector2(1280f, 720f),
				Fill = gradient,
				Slot = slot
			});
		}
	}
}
