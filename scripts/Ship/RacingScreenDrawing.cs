using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

public enum ScreenPrimitiveKind { FillRect, StrokeRect, Line, Disc, Ring, Text }

public enum ScreenTextAlign { Left, Right, Center }

// Una primitiva de las pantallas de la cabina 3D, en sus píxeles. Color con su opacidad; Glow en px (0: sin halo).
//   FillRect y StrokeRect: esquina A y tamaño B (Width: el grueso)
//   Line: de A a B con Width
//   Disc y Ring: centro A y Radius (Width: el grueso)
//   Text: línea base en A, tamaño Radius y Spacing entre letras
public readonly record struct ScreenPrimitive(
	ScreenPrimitiveKind Kind, Vector2 A, Vector2 B, float Radius, float Width, Vector4 Color, float Glow,
	string Text = null, float Spacing = 0f, ScreenTextAlign Align = ScreenTextAlign.Left);

// Dibujo de las pantallas en estilo carreras, sin Godot: drawLeft, drawRight y screenBase del prototipo, con las mismas
// posiciones, tamaños, grosores, opacidades y textos (spec docs/cabinas-3d-1-vector.md, 6.2). El estado sale de
// CockpitHudState y aquí no hay reglas. Lógica pura.
public static class RacingScreenDrawing
{
	public const int LargeWidth = 640;
	public const int LargeHeight = 200;
	public const int SmallSize = 256;

	private const float TextScale = 1.15f;
	private const float Glow = 8f;
	private const int ScanSpacing = 3;
	private const float CornerMargin = 14f;
	private const float CornerLength = 20f;
	private const float CornerWidth = 2.5f;

	private const float SegmentWidth = 150f;
	private const float SegmentHeight = 44f;
	private const float SegmentGap = 22f;
	private const float SegmentX = 60f;
	private const float SegmentY = 78f;

	private const float OrbX = 64f;
	private const float OrbStep = 52f;
	private const float OrbY = 104f;
	private const float OrbRadius = 17f;
	private const float OrbRingGap = 7f;

	public const float BarX = 40f;
	public const float BarY = 152f;
	public const float BarWidth = LargeWidth - 80f;
	public const float BarHeight = 10f;
	private const int BarTicks = 10;

	// Tamaño real de un texto del prototipo: se redondea como Math.round
	public static float FontSize(float size) => MathF.Round(size * TextScale, MidpointRounding.AwayFromZero);

	// La parte fija de cualquier pantalla: fondo, líneas de barrido y esquinas
	public static void Base(List<ScreenPrimitive> output, int width, int height)
	{
		output.Add(FillRect(0f, 0f, width, height, RacingPalette.Rgba(RacingPalette.ScreenBackground)));
		Vector4 scan = RacingPalette.Rgba(RacingPalette.Scan, RacingPalette.ScanOpacity);
		for (int y = 0; y < height; y += ScanSpacing)
			output.Add(FillRect(0f, y, width, 1f, scan));

		Vector4 frame = RacingPalette.Rgba(RacingPalette.Frame, RacingPalette.FrameOpacity);
		Corner(output, CornerMargin, CornerMargin, 1f, 1f, frame);
		Corner(output, width - CornerMargin, CornerMargin, -1f, 1f, frame);
		Corner(output, CornerMargin, height - CornerMargin, 1f, -1f, frame);
		Corner(output, width - CornerMargin, height - CornerMargin, -1f, -1f, frame);
	}

	// Pantalla izquierda: estado del casco
	public static void Left(List<ScreenPrimitive> output, CockpitHudState hud)
	{
		Vector4 label = RacingPalette.Rgba(RacingPalette.Label);
		output.Add(Text("HULL STATUS", 40f, 56f, 24f, WithAlpha(label, 0.85f), ScreenTextAlign.Left, 3f, 0f));
		if (hud.DamageText.Length > 0)
			output.Add(Text(hud.DamageText, LargeWidth - 40f, 56f, 26f, WithAlpha(hud.DamageTextColor, 0.95f), ScreenTextAlign.Right, 4f, Glow * 0.6f));

		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			CockpitHudPaint paint = hud.Segment(i);
			float x = SegmentX + i * (SegmentWidth + SegmentGap);
			if (paint.Fill.W > 0f)
				output.Add(FillRect(x, SegmentY, SegmentWidth, SegmentHeight, paint.Fill, paint.Glow * Glow));
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.StrokeRect, new Vector2(x, SegmentY), new Vector2(SegmentWidth, SegmentHeight),
				0f, 2.5f, paint.Stroke, 0f));
		}

		output.Add(Text("INTEGRITY", 60f, 166f, 22f, WithAlpha(label, 0.7f), ScreenTextAlign.Left, 3f, 0f));
		output.Add(Line(250f, 160f, LargeWidth - 40f, 160f, 2f, RacingPalette.Rgba(RacingPalette.Frame, 0.5f)));
	}

	// Pantalla derecha: boost y velocidad
	public static void Right(List<ScreenPrimitive> output, CockpitHudState hud)
	{
		Vector4 label = RacingPalette.Rgba(RacingPalette.Label);
		output.Add(Text("BOOST", 40f, 56f, 24f, WithAlpha(label, 0.85f), ScreenTextAlign.Left, 3f, 0f));
		output.Add(Text(hud.BoostText, LargeWidth - 40f, 56f, 24f, RacingPalette.Rgba(RacingPalette.Over, 0.9f), ScreenTextAlign.Right, 3f, Glow * 0.5f));

		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			CockpitHudPaint paint = hud.Orb(i);
			var center = new Vector2(OrbX + i * OrbStep, OrbY);
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Ring, center, default, OrbRadius + OrbRingGap, 2f, WithAlpha(label, 0.5f), 0f));
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Disc, center, default, OrbRadius, 0f, paint.Fill, paint.Glow * Glow));
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Ring, center, default, OrbRadius, 2.5f, paint.Stroke, 0f));
		}

		output.Add(Text(hud.SpeedText, LargeWidth - 140f, 134f, 88f, hud.SpeedColor, ScreenTextAlign.Right, 0f, Glow));
		output.Add(Text("KM/H", LargeWidth - 128f, 134f, 24f, WithAlpha(label, 0.9f), ScreenTextAlign.Left, 4f, 0f));

		output.Add(FillRect(BarX, BarY, BarWidth, BarHeight, RacingPalette.Rgba(RacingPalette.Bar)));
		output.Add(FillRect(BarX, BarY, BarWidth * hud.SpeedFraction, BarHeight, hud.SpeedColor));
		Vector4 tick = WithAlpha(label, 0.7f);
		for (int i = 0; i <= BarTicks; i++)
		{
			float x = BarX + BarWidth * i / BarTicks;
			output.Add(Line(x, BarY + BarHeight + 4f, x, BarY + BarHeight + (i % 5 == 0 ? 18f : 10f), 2f, tick));
		}

		float top = BarX + BarWidth * hud.TopFraction;
		output.Add(Line(top, BarY - 7f, top, BarY + BarHeight + 7f, 2f, RacingPalette.Rgba(RacingPalette.Over, 0.9f)));
	}

	private static void Corner(List<ScreenPrimitive> output, float x, float y, float sx, float sy, Vector4 color)
	{
		output.Add(Line(x, y + sy * CornerLength, x, y, CornerWidth, color));
		output.Add(Line(x, y, x + sx * CornerLength, y, CornerWidth, color));
	}

	private static ScreenPrimitive FillRect(float x, float y, float width, float height, Vector4 color, float glow = 0f) =>
		new(ScreenPrimitiveKind.FillRect, new Vector2(x, y), new Vector2(width, height), 0f, 0f, color, glow);

	private static ScreenPrimitive Line(float x0, float y0, float x1, float y1, float width, Vector4 color) =>
		new(ScreenPrimitiveKind.Line, new Vector2(x0, y0), new Vector2(x1, y1), 0f, width, color, 0f);

	private static ScreenPrimitive Text(string text, float x, float y, float size, Vector4 color, ScreenTextAlign align, float spacing, float glow) =>
		new(ScreenPrimitiveKind.Text, new Vector2(x, y), default, FontSize(size), 0f, color, glow, text, spacing, align);

	private static Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, color.W * alpha);
}
