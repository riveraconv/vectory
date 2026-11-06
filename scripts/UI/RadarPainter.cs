using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Radar;
using NVec2 = System.Numerics.Vector2;
using NVec3 = System.Numerics.Vector3;
using NVec4 = System.Numerics.Vector4;

namespace Vectory.UI;

// Traduce las primitivas del radar a llamadas de dibujo de Godot. Sin reglas. El halo es una copia más ancha y tenue:
// el glow del entorno no afecta al 2D.
public static class RadarPainter
{
	private const int RingPoints = 64;
	private const int ArcPoints = 16;
	private const float OuterHaloSize = 1f;        // × Glow, sumado al grueso
	private const float OuterHaloOpacity = 0.10f;
	private const float InnerHaloSize = 0.5f;
	private const float InnerHaloOpacity = 0.20f;
	private const float ArrowEdgeWidth = 1f;       // borde suavizado de la flecha

	// La paleta del HUD de pantalla sale de Palette
	public static RadarPalette HudPalette() => new(
		ToNumerics(Palette.Background), ToNumerics(Palette.Cyan), ToNumerics(Palette.Blue), ToNumerics(Palette.Cyan),
		ToNumerics(Palette.Magenta), ToNumerics(Palette.Orange), ToNumerics(Palette.Cyan), ToNumerics(Palette.BlueGray));

	// Búferes reutilizados: el radar se pinta cada frame
	private static readonly Vector2[] Arrow = new Vector2[4];
	private static readonly Vector2[] ArrowOutline = new Vector2[5];

	public static void Draw(CanvasItem canvas, List<RadarPrimitive> primitives, Font font)
	{
		foreach (RadarPrimitive primitive in primitives)
		{
			switch (primitive.Kind)
			{
				case RadarPrimitiveKind.Disc:
					DrawDisc(canvas, primitive);
					break;
				case RadarPrimitiveKind.Ring:
					DrawStroke(canvas, primitive, 0f, Mathf.Tau, RingPoints);
					break;
				case RadarPrimitiveKind.Arc:
					DrawStroke(canvas, primitive, primitive.StartAngle, primitive.EndAngle, ArcPoints);
					break;
				case RadarPrimitiveKind.Line:
					canvas.DrawLine(ToGodot(primitive.A), ToGodot(primitive.B), ToColor(primitive.Color), primitive.Width, true);
					break;
				case RadarPrimitiveKind.Arrow:
					DrawArrow(canvas, primitive);
					break;
				case RadarPrimitiveKind.Text:
					canvas.DrawString(font, ToGodot(primitive.A), primitive.Text, HorizontalAlignment.Left, -1f,
						Mathf.RoundToInt(primitive.Radius), ToColor(primitive.Color));
					break;
			}
		}
	}

	private static void DrawDisc(CanvasItem canvas, in RadarPrimitive primitive)
	{
		Color color = ToColor(primitive.Color);
		Vector2 center = ToGodot(primitive.A);
		if (primitive.Glow > 0f)
		{
			canvas.DrawCircle(center, primitive.Radius + primitive.Glow * OuterHaloSize * 0.5f, Faded(color, OuterHaloOpacity), true, -1f, true);
			canvas.DrawCircle(center, primitive.Radius + primitive.Glow * InnerHaloSize * 0.5f, Faded(color, InnerHaloOpacity), true, -1f, true);
		}
		canvas.DrawCircle(center, primitive.Radius, color, true, -1f, true);
	}

	private static void DrawStroke(CanvasItem canvas, in RadarPrimitive primitive, float from, float to, int points)
	{
		Color color = ToColor(primitive.Color);
		Vector2 center = ToGodot(primitive.A);
		if (primitive.Glow > 0f)
		{
			canvas.DrawArc(center, primitive.Radius, from, to, points, Faded(color, OuterHaloOpacity), primitive.Width + primitive.Glow * OuterHaloSize, true);
			canvas.DrawArc(center, primitive.Radius, from, to, points, Faded(color, InnerHaloOpacity), primitive.Width + primitive.Glow * InnerHaloSize, true);
		}
		canvas.DrawArc(center, primitive.Radius, from, to, points, color, primitive.Width, true);
	}

	private static void DrawArrow(CanvasItem canvas, in RadarPrimitive primitive)
	{
		Color color = ToColor(primitive.Color);
		if (primitive.Glow > 0f)
			DrawArrowShape(canvas, primitive.A, primitive.Radius + primitive.Glow * InnerHaloSize, Faded(color, InnerHaloOpacity), false);
		DrawArrowShape(canvas, primitive.A, primitive.Radius, color, true);
	}

	private static void DrawArrowShape(CanvasItem canvas, NVec2 center, float size, Color color, bool smoothEdge)
	{
		RadarDrawing.ArrowPoints(center, size, out NVec2 tip, out NVec2 right, out NVec2 notch, out NVec2 left);
		Arrow[0] = ToGodot(tip);
		Arrow[1] = ToGodot(right);
		Arrow[2] = ToGodot(notch);
		Arrow[3] = ToGodot(left);
		canvas.DrawColoredPolygon(Arrow, color);

		if (!smoothEdge)
			return;
		Arrow.CopyTo(ArrowOutline, 0);
		ArrowOutline[4] = Arrow[0];
		canvas.DrawPolyline(ArrowOutline, color, ArrowEdgeWidth, true);
	}

	private static Color Faded(Color color, float opacity) => new(color, color.A * opacity);

	private static NVec3 ToNumerics(Color color) => new(color.R, color.G, color.B);

	private static Vector2 ToGodot(NVec2 v) => new(v.X, v.Y);

	private static Color ToColor(NVec4 c) => new(c.X, c.Y, c.Z, c.W);
}
