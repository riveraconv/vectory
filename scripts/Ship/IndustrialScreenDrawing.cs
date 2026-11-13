using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Dibujo de las pantallas en estilo industrial, sin Godot: base y corners del prototipo de HAMMER, las imágenes de prueba
// de la etapa 2 y la esfera de los relojes pequeños (miniGauge), con las mismas posiciones, tamaños, grosores, opacidades
// y textos (spec docs/cabinas-3d-3-hammer.md, 4.6 y 6.2). Lógica pura.
public static class IndustrialScreenDrawing
{
	public const int SpeedWidth = 768;
	public const int SpeedHeight = 144;
	public const int RadarWidth = 384;
	public const int RadarHeight = 294;
	public const int MapWidth = 384;
	public const int MapHeight = 294;
	public const int DamageSize = 300;

	private const int ScanSpacing = 3;
	private const float CornerWidth = 2.5f;
	public const float CornerLength = 18f, CornerMargin = 8f;
	public const float SpeedCornerLength = 14f, SpeedCornerMargin = 6f;   // la tira de velocidad, más baja

	// Imagen de prueba: rótulo grande en el centro, que quepa a lo ancho (letra monoespaciada de ~0,6 em)
	private const float TestHeightFraction = 0.4f;
	private const float TestWidthFraction = 0.8f;
	private const float MonoAdvance = 0.6f;
	private const float TestBaselineFraction = 0.35f;
	private const float TestGlow = 8f;

	// Reloj pequeño: lienzo de 128 px, centro (64, 70), radio 52, escala de 0,8π a 2,2π
	public const int GaugeSize = 128;
	private const float GaugeCenterX = 64f, GaugeCenterY = 70f, GaugeRadius = 52f;
	private const float GaugeStart = MathF.PI * 0.8f, GaugeEnd = MathF.PI * 2.2f;
	private const float GaugeRedFrom = 0.8f;   // arco rojo en el último 20 %
	private const int GaugeArcSegments = 16;

	private static Vector4 C(uint rgb, float alpha = 1f) => IndustrialPalette.Rgba(rgb, alpha);

	// Fondo y línea de barrido en Main al 7 %, de 1 px cada 3 px (base)
	public static void Base(List<ScreenPrimitive> output, int width, int height)
	{
		output.Add(FillRect(0f, 0f, width, height, C(IndustrialPalette.ScreenBackground)));
		Vector4 scan = C(IndustrialPalette.Main, IndustrialPalette.ScanOpacity);
		for (int y = 0; y < height; y += ScanSpacing)
			output.Add(FillRect(0f, y, width, 1f, scan));
	}

	// Esquinas en Frame al 75 %, 2,5 px (corners)
	public static void Corners(List<ScreenPrimitive> output, int width, int height, float length, float margin)
	{
		Vector4 frame = C(IndustrialPalette.Frame, IndustrialPalette.FrameOpacity);
		Corner(output, margin, margin, 1f, 1f, length, frame);
		Corner(output, width - margin, margin, -1f, 1f, length, frame);
		Corner(output, margin, height - margin, 1f, -1f, length, frame);
		Corner(output, width - margin, height - margin, -1f, -1f, length, frame);
	}

	// Las de la tira de velocidad son más cortas y más pegadas al borde
	public static void Corners(List<ScreenPrimitive> output, int width, int height)
	{
		bool speed = width == SpeedWidth && height == SpeedHeight;
		Corners(output, width, height, speed ? SpeedCornerLength : CornerLength, speed ? SpeedCornerMargin : CornerMargin);
	}

	// Imagen de prueba (etapa 2): base, esquinas y el rótulo grande en ámbar. Sirve para comprobar orientación, tamaño y
	// brillo.
	public static void TestImage(List<ScreenPrimitive> output, int width, int height, string label)
	{
		Base(output, width, height);
		Corners(output, width, height);
		float size = TestSize(width, height, label);
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Text, new Vector2(width * 0.5f, height * 0.5f + size * TestBaselineFraction),
			default, size, 0f, C(IndustrialPalette.Main), TestGlow, label, 0f, ScreenTextAlign.Center));
	}

	public static float TestSize(int width, int height, string label) =>
		MathF.Round(MathF.Min(height * TestHeightFraction, width * TestWidthFraction / (Math.Max(1, label.Length) * MonoAdvance)));

	// Esfera de un reloj pequeño (miniGauge): fondo, arco rojo en el último 20 %, 9 marcas (largas cada 4), rótulo y aguja
	// fija. Decorativa: se pinta una vez.
	public static void GaugeFace(List<ScreenPrimitive> output, string label, float value)
	{
		output.Add(FillRect(0f, 0f, GaugeSize, GaugeSize, C(IndustrialPalette.GaugeFace)));

		Vector4 red = C(IndustrialPalette.GaugeRed);
		float r = GaugeRadius - 6f;
		float from = GaugeAngle(GaugeRedFrom);
		for (int i = 0; i < GaugeArcSegments; i++)
		{
			float a0 = from + (GaugeEnd - from) * i / GaugeArcSegments;
			float a1 = from + (GaugeEnd - from) * (i + 1) / GaugeArcSegments;
			output.Add(Line(Polar(a0, r), Polar(a1, r), 6f, red));
		}

		Vector4 cream = C(IndustrialPalette.Cream);
		for (int i = 0; i <= 8; i++)
		{
			float a = GaugeAngle(i / 8f);
			output.Add(Line(Polar(a, GaugeRadius - 2f), Polar(a, GaugeRadius - (i % 4 == 0 ? 16f : 10f)), 2f, cream));
		}
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Text, new Vector2(GaugeCenterX, GaugeCenterY + 34f), default, 20f, 0f, cream,
			0f, label, 0f, ScreenTextAlign.Center));

		// Aguja de 4 px con las puntas redondas y el eje encima
		Vector4 needle = C(IndustrialPalette.Needle);
		Vector2 center = new(GaugeCenterX, GaugeCenterY);
		Vector2 tip = Polar(GaugeAngle(value), GaugeRadius - 10f);
		output.Add(Line(center, tip, 4f, needle));
		output.Add(Disc(tip, 2f, needle));
		output.Add(Disc(center, 6f, C(IndustrialPalette.GaugeHub)));
	}

	public static float GaugeAngle(float fraction) => GaugeStart + (GaugeEnd - GaugeStart) * fraction;

	private static Vector2 Polar(float angle, float radius) =>
		new(GaugeCenterX + MathF.Cos(angle) * radius, GaugeCenterY + MathF.Sin(angle) * radius);

	private static void Corner(List<ScreenPrimitive> output, float x, float y, float sx, float sy, float length, Vector4 color)
	{
		output.Add(Line(new Vector2(x, y + sy * length), new Vector2(x, y), CornerWidth, color));
		output.Add(Line(new Vector2(x, y), new Vector2(x + sx * length, y), CornerWidth, color));
	}

	private static ScreenPrimitive FillRect(float x, float y, float width, float height, Vector4 color) =>
		new(ScreenPrimitiveKind.FillRect, new Vector2(x, y), new Vector2(width, height), 0f, 0f, color, 0f);

	private static ScreenPrimitive Line(Vector2 a, Vector2 b, float width, Vector4 color) =>
		new(ScreenPrimitiveKind.Line, a, b, 0f, width, color, 0f);

	private static ScreenPrimitive Disc(Vector2 center, float radius, Vector4 color) =>
		new(ScreenPrimitiveKind.Disc, center, default, radius, 0f, color, 0f);
}
