using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Dibujo de las pantallas en estilo militar, sin Godot: base y corners del prototipo de BLADE, la imagen de prueba de la
// etapa 2 y los textos fijos (pilotos MASTER y placa), con las mismas posiciones, tamaños y opacidades
// (spec docs/cabinas-3d-2-blade.md, 4.6). Lógica pura.
public static class MilitaryScreenDrawing
{
	private const int ScanSpacing = 3;
	private const float CornerLength = 16f;
	private const float CornerMargin = 6f;
	private const float CornerWidth = 2f;
	private const float TestLabelFraction = 0.22f;   // alto del rótulo de prueba respecto al lienzo
	private const float TestGlow = 8f;

	// Fondo, líneas de barrido (base) y esquinas (corners)
	public static void Base(List<ScreenPrimitive> output, int width, int height)
	{
		output.Add(FillRect(0f, 0f, width, height, MilitaryPalette.Rgba(MilitaryPalette.ScreenBackground)));
		Vector4 scan = MilitaryPalette.Rgba(MilitaryPalette.Scan, MilitaryPalette.ScanOpacity);
		for (int y = 0; y < height; y += ScanSpacing)
			output.Add(FillRect(0f, y, width, 1f, scan));
		Corners(output, width, height);
	}

	public static void Corners(List<ScreenPrimitive> output, int width, int height)
	{
		Vector4 frame = MilitaryPalette.Rgba(MilitaryPalette.Frame, MilitaryPalette.FrameOpacity);
		Corner(output, CornerMargin, CornerMargin, 1f, 1f, frame);
		Corner(output, width - CornerMargin, CornerMargin, -1f, 1f, frame);
		Corner(output, CornerMargin, height - CornerMargin, 1f, -1f, frame);
		Corner(output, width - CornerMargin, height - CornerMargin, -1f, -1f, frame);
	}

	// Imagen de prueba: sirve para comprobar orientación, tamaño y brillo. El cristal HUD va en suma: sin fondo (el negro
	// del prototipo, que en suma no se ve; uno opaco taparía el mundo al juntar la cabina con él) y sin barrido.
	public static void TestImage(List<ScreenPrimitive> output, int width, int height, string label, bool additive)
	{
		if (additive)
			Corners(output, width, height);
		else
			Base(output, width, height);

		float size = height * TestLabelFraction;
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Text, new Vector2(width * 0.5f, height * 0.5f + size * 0.35f), default, size,
			0f, MilitaryPalette.Rgba(MilitaryPalette.Main), TestGlow, label, 0f, ScreenTextAlign.Center));
	}

	// Texto fijo: fondo y líneas centradas
	public static void Label(List<ScreenPrimitive> output, Cockpit3DLabel label)
	{
		output.Add(FillRect(0f, 0f, label.Width, label.Height, MilitaryPalette.Rgba(label.Background)));
		foreach (Cockpit3DLabelLine line in label.Lines)
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Text, new Vector2(label.Width * 0.5f, line.Y), default, label.FontSize, 0f,
				MilitaryPalette.Rgba(label.TextColor), 0f, line.Text, 0f, ScreenTextAlign.Center));
	}

	private static void Corner(List<ScreenPrimitive> output, float x, float y, float sx, float sy, Vector4 color)
	{
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Line, new Vector2(x, y + sy * CornerLength), new Vector2(x, y), 0f,
			CornerWidth, color, 0f));
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Line, new Vector2(x, y), new Vector2(x + sx * CornerLength, y), 0f,
			CornerWidth, color, 0f));
	}

	private static ScreenPrimitive FillRect(float x, float y, float width, float height, Vector4 color) =>
		new(ScreenPrimitiveKind.FillRect, new Vector2(x, y), new Vector2(width, height), 0f, 0f, color, 0f);
}
