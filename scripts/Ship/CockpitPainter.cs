using System.Collections.Generic;
using Godot;
using Vectory.UI;
using NMatrix = System.Numerics.Matrix3x2;
using NVec2 = System.Numerics.Vector2;
using NVec4 = System.Numerics.Vector4;

namespace Vectory.Ship;

// Dibuja un elemento de la cabina sobre un CanvasItem. Pasa el espacio del diseño (1280×720) a la pantalla base del
// proyecto calculando cada punto, sin escalar el lienzo, para que los textos salgan nítidos a su tamaño real.
public static class CockpitPainter
{
	private const int GradientTextureSize = 128;
	private const float HaloPx = 2.4f;        // el glowSoft del diseño: se aproxima con una copia algo mayor y tenue
	private const float HaloOpacity = 0.25f;
	private const int CircleArcPoints = 24;

	private static readonly Dictionary<CockpitGradientId, GradientTexture2D> Textures = new();
	private static readonly Dictionary<(int Size, int Spacing), Font> Fonts = new();

	// dimOpacity: opacidad del trazo si su arista está apagada por el parpadeo de daños (dimmed)
	public static void Draw(CanvasItem item, CockpitElement e, float scale, bool dimmed, float dimOpacity)
	{
		NMatrix toScreen = e.Transform * NMatrix.CreateScale(scale);
		float strokeFactor = dimmed ? dimOpacity : 1f;

		switch (e.Kind)
		{
			case CockpitElementKind.Polygon:
				DrawFill(item, e, e.Points, Bounds(e.Points), toScreen, scale);
				DrawOutline(item, e, e.Points, true, toScreen, scale, strokeFactor);
				break;
			case CockpitElementKind.Polyline:
				DrawOutline(item, e, e.Points, false, toScreen, scale, strokeFactor);
				break;
			case CockpitElementKind.Segments:
				DrawPairs(item, e, e.Points, toScreen, scale, strokeFactor);
				break;
			case CockpitElementKind.Circle:
				DrawCircle(item, e, toScreen, scale, strokeFactor);
				break;
			case CockpitElementKind.Ellipse:
				DrawFill(item, e, RectPoints(e.Center - e.Radii, e.Radii * 2f), (e.Center - e.Radii, e.Radii * 2f), toScreen, scale);
				break;
			case CockpitElementKind.Rect:
				DrawRect(item, e, toScreen, scale, strokeFactor);
				break;
			case CockpitElementKind.Text:
				DrawText(item, e, toScreen, scale);
				break;
		}
	}

	private static void DrawRect(CanvasItem item, CockpitElement e, NMatrix toScreen, float scale, float strokeFactor)
	{
		if (e.Size.X <= 0f || e.Size.Y <= 0f)
			return;

		IReadOnlyList<NVec2> points = e.CornerRadius > 0f
			? CockpitGeometry.RoundedRect(e.Position, e.Size, e.CornerRadius)
			: RectPoints(e.Position, e.Size);

		if (e.Glow > 0f)
		{
			var halo = RectPoints(e.Position - new NVec2(HaloPx), e.Size + new NVec2(HaloPx * 2f));
			Color glow = Multiply(ColorOf(e.FillRgba, e.Fill?.Color), HaloOpacity * e.Glow * e.Opacity);
			item.DrawColoredPolygon(ToScreen(halo, toScreen), glow);
		}

		DrawFill(item, e, points, (e.Position, e.Size), toScreen, scale);
		DrawOutline(item, e, points, true, toScreen, scale, strokeFactor);
	}

	// Relleno de color o de degradado sobre su caja (bounds, en el espacio del elemento)
	private static void DrawFill(CanvasItem item, CockpitElement e, IReadOnlyList<NVec2> points, (NVec2 Origin, NVec2 Size) bounds, NMatrix toScreen, float scale)
	{
		if (e.Fill is not { } fill)
			return;

		float opacity = e.FillOpacity * e.Opacity;
		if (fill.Gradient == CockpitGradientId.Scan)
		{
			Color scan = Multiply(Palette(CockpitColor.Cyan), ScanOpacity * opacity);
			List<NVec2> lines = CockpitGeometry.ScanLines(points);
			if (lines.Count > 0)
				item.DrawMultiline(ToScreen(lines, toScreen), scan, scale, false);
			return;
		}

		if (fill.Gradient is { } gradient)
		{
			if (opacity <= 0f)
				return;
			var uvs = new Vector2[points.Count];
			for (int i = 0; i < points.Count; i++)
			{
				NVec2 uv = (points[i] - bounds.Origin) / bounds.Size;
				uvs[i] = new Vector2(uv.X, uv.Y);
			}
			item.DrawPolygon(ToScreen(points, toScreen), new[] { new Color(1f, 1f, 1f, opacity) }, uvs, TextureOf(gradient));
			return;
		}

		Color color = Multiply(ColorOf(e.FillRgba, fill.Color), opacity);
		if (color.A > 0f)
			item.DrawColoredPolygon(ToScreen(points, toScreen), color);
	}

	private const float ScanOpacity = 0.08f;

	private static void DrawOutline(CanvasItem item, CockpitElement e, IReadOnlyList<NVec2> points, bool closed, NMatrix toScreen, float scale, float strokeFactor)
	{
		if (e.Stroke == null && e.StrokeRgba == null)
			return;

		if (e.Dash.Count == 2)
		{
			DrawPairs(item, e, CockpitGeometry.DashPairs(points, e.Dash[0], e.Dash[1]), toScreen, scale, strokeFactor);
			return;
		}

		int count = points.Count + (closed ? 1 : 0);
		var line = new Vector2[count];
		Vector2[] screen = ToScreen(points, toScreen);
		screen.CopyTo(line, 0);
		if (closed)
			line[count - 1] = screen[0];

		Color color = StrokeColor(e, strokeFactor);
		if (color.A > 0f)
			item.DrawPolyline(line, color, e.StrokeWidth * scale, true);
	}

	private static void DrawPairs(CanvasItem item, CockpitElement e, IReadOnlyList<NVec2> pairs, NMatrix toScreen, float scale, float strokeFactor)
	{
		Color color = StrokeColor(e, strokeFactor);
		if (pairs.Count > 0 && color.A > 0f)
			item.DrawMultiline(ToScreen(pairs, toScreen), color, e.StrokeWidth * scale, true);
	}

	private static void DrawCircle(CanvasItem item, CockpitElement e, NMatrix toScreen, float scale, float strokeFactor)
	{
		NVec2 c = NVec2.Transform(e.Center, toScreen);
		var center = new Vector2(c.X, c.Y);
		float radius = e.Radii.X * scale;

		if (e.Fill is { } fill)
		{
			Color color = ColorOf(e.FillRgba, fill.Color);
			if (e.Glow > 0f)
				item.DrawCircle(center, radius + HaloPx * scale, Multiply(color, HaloOpacity * e.Glow * e.Opacity), true, -1f, true);
			color = Multiply(color, e.FillOpacity * e.Opacity);
			if (color.A > 0f)
				item.DrawCircle(center, radius, color, true, -1f, true);
		}

		if (e.Stroke != null || e.StrokeRgba != null)
		{
			Color color = StrokeColor(e, strokeFactor);
			if (color.A > 0f)
				item.DrawArc(center, radius, 0f, Mathf.Tau, CircleArcPoints, color, e.StrokeWidth * scale, true);
		}
	}

	private static void DrawText(CanvasItem item, CockpitElement e, NMatrix toScreen, float scale)
	{
		if (string.IsNullOrEmpty(e.Text))
			return;

		int size = Mathf.Max(1, Mathf.RoundToInt(e.FontSize * scale));
		Font font = FontOf(size, e.LetterSpacing * scale);
		float width = font.GetStringSize(e.Text, HorizontalAlignment.Left, -1f, size).X;
		float x = e.Anchor switch
		{
			CockpitTextAnchor.Middle => -width * 0.5f,
			CockpitTextAnchor.End => -width,
			_ => 0f
		};

		// Giro del grupo alrededor de la base del texto, sin la escala: el tamaño ya es el real
		NVec2 origin = NVec2.Transform(e.Position, toScreen);
		item.DrawSetTransformMatrix(new Transform2D(
			new Vector2(e.Transform.M11, e.Transform.M12),
			new Vector2(e.Transform.M21, e.Transform.M22),
			new Vector2(origin.X, origin.Y)));

		Color color = ColorOf(e.FillRgba, e.Fill?.Color);
		if (e.Glow > 0f)
		{
			Color glow = Multiply(color, HaloOpacity * e.Glow * e.Opacity);
			item.DrawStringOutline(font, new Vector2(x, 0f), e.Text, HorizontalAlignment.Left, -1f, size, Mathf.RoundToInt(HaloPx * scale * 2f), glow);
		}
		item.DrawString(font, new Vector2(x, 0f), e.Text, HorizontalAlignment.Left, -1f, size, Multiply(color, e.Opacity));
		item.DrawSetTransformMatrix(Transform2D.Identity);
	}

	private static Color StrokeColor(CockpitElement e, float strokeFactor) =>
		Multiply(ColorOf(e.StrokeRgba, e.Stroke), e.StrokeOpacity * e.Opacity * strokeFactor);

	private static Color ColorOf(NVec4? rgba, CockpitColor? role)
	{
		if (rgba is { } v)
			return new Color(v.X, v.Y, v.Z, v.W);
		return role is { } r ? Palette(r) : Colors.Transparent;
	}

	private static Color Palette(CockpitColor color) => CockpitPalette.Get(color);

	private static Color Multiply(Color c, float alpha) => new(c.R, c.G, c.B, c.A * alpha);

	private static Vector2[] ToScreen(IReadOnlyList<NVec2> points, NMatrix toScreen)
	{
		var result = new Vector2[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			NVec2 p = NVec2.Transform(points[i], toScreen);
			result[i] = new Vector2(p.X, p.Y);
		}
		return result;
	}

	private static NVec2[] RectPoints(NVec2 position, NVec2 size) => new[]
	{
		position,
		position + new NVec2(size.X, 0f),
		position + size,
		position + new NVec2(0f, size.Y)
	};

	private static (NVec2 Origin, NVec2 Size) Bounds(IReadOnlyList<NVec2> points)
	{
		var min = new NVec2(float.MaxValue);
		var max = new NVec2(float.MinValue);
		foreach (NVec2 p in points)
		{
			min = NVec2.Min(min, p);
			max = NVec2.Max(max, p);
		}
		return (min, NVec2.Max(max - min, new NVec2(0.001f)));
	}

	private static Font FontOf(int size, float spacingPx)
	{
		var key = (size, Mathf.RoundToInt(spacingPx));
		if (!Fonts.TryGetValue(key, out Font font))
		{
			font = MenuFonts.Make(MenuFonts.Mono, 400, size, spacingPx / size);
			Fonts[key] = font;
		}
		return font;
	}

	private static GradientTexture2D TextureOf(CockpitGradientId id)
	{
		if (Textures.TryGetValue(id, out GradientTexture2D texture))
			return texture;

		CockpitGradient definition = CockpitKit.Gradients[id];
		var gradient = new Gradient();
		var offsets = new float[definition.Stops.Count];
		var colors = new Color[definition.Stops.Count];
		for (int i = 0; i < definition.Stops.Count; i++)
		{
			CockpitGradientStop stop = definition.Stops[i];
			offsets[i] = stop.Offset;
			colors[i] = Multiply(Palette(stop.Color), stop.Opacity);
		}
		gradient.Offsets = offsets;
		gradient.Colors = colors;

		texture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = GradientTextureSize,
			Height = GradientTextureSize,
			Fill = definition.Radial ? GradientTexture2D.FillEnum.Radial : GradientTexture2D.FillEnum.Linear,
			FillFrom = new Vector2(definition.From.X, definition.From.Y),
			FillTo = new Vector2(definition.To.X, definition.To.Y)
		};
		Textures[id] = texture;
		return texture;
	}
}
