using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Vectory.Ship;

// Dibujo de las pantallas CRT en estilo industrial, sin Godot: base, crtFx, corners, T, lapBar, drawSpeed2, drawDamage2,
// drawRadar2 y drawMap2 del prototipo de HAMMER, con las mismas posiciones, tamaños, grosores, opacidades y textos, y la
// esfera de los relojes pequeños (miniGauge) (spec docs/cabinas-3d-3-hammer.md, 4.6 y 6). El estado sale de
// CockpitHudState y aquí no hay reglas. Lógica pura.
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

	// crtFx: viñeta del tubo (de transparente en el 30 % del lado menor a negro al 65 % en el 62 % del lado mayor) y una
	// banda de 6 px que baja a 60 px/s
	public const float VignetteInner = 0.3f, VignetteOuter = 0.62f, VignetteOpacity = 0.65f;
	public const float BandHeight = 6f, BandSpeed = 60f, BandOpacity = 0.05f;

	// Tira de velocidad: 48 segmentos de x 24 a 744, y 104, alto 24, separación 3
	public const int SpeedSegments = 48;
	public const float SegmentLeft = 24f, SegmentRight = SpeedWidth - 24f, SegmentY = 104f, SegmentHeight = 24f, SegmentGap = 3f;

	// Casco: tres sectores de 220 × 46, de abajo arriba
	public const float SectorX = 40f, SectorWidth = 220f, SectorHeight = 46f, SectorBottomY = 208f, SectorStep = 58f;
	private static readonly string[] SectorNames = { "SECT I", "SECT II", "SECT III" };

	// Minimapa: barra de progreso de la vuelta
	public const float ProgressLeft = 104f, ProgressRight = MapWidth - 104f, ProgressY = 262f, ProgressHeight = 10f;

	// Reloj pequeño: lienzo de 128 px, centro (64, 70), radio 52, escala de 0,8π a 2,2π
	public const int GaugeSize = 128;
	private const float GaugeCenterX = 64f, GaugeCenterY = 70f, GaugeRadius = 52f;
	private const float GaugeStart = MathF.PI * 0.8f, GaugeEnd = MathF.PI * 2.2f;
	private const float GaugeRedFrom = 0.8f;   // arco rojo en el último 20 %
	private const int GaugeArcSegments = 16;

	private static Vector4 C(uint rgb, float alpha = 1f) => IndustrialPalette.Rgba(rgb, alpha);
	private static Vector4 Main => C(IndustrialPalette.Main);
	private static Vector4 Label(float alpha = 1f) => C(IndustrialPalette.Label, alpha);

	// ---------- Piezas comunes: base, corners, crtFx ----------

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

	// Efecto CRT, al final de cada pantalla: viñeta y banda. time es el de la cabina: la pausa lo congela.
	public static void CrtFx(List<ScreenPrimitive> output, int width, int height, float time)
	{
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Vignette, Vector2.Zero, new Vector2(width, height),
			MathF.Min(width, height) * VignetteInner, MathF.Max(width, height) * VignetteOuter, C(0x000000, VignetteOpacity), 0f));
		output.Add(FillRect(0f, BandY(time, height), width, BandHeight, C(IndustrialPalette.Main, BandOpacity)));
	}

	// Donde está la banda: baja a 60 px/s y vuelve a empezar arriba
	public static float BandY(float time, int height)
	{
		float y = time * BandSpeed % height;
		return y < 0f ? y + height : y;
	}

	// Opacidad de la viñeta en un píxel: de 0 en el radio interior a VignetteOpacity en el exterior, lineal (como el
	// degradado radial del lienzo), medida desde el centro
	public static float VignetteAlpha(float x, float y, float width, float height)
	{
		float inner = MathF.Min(width, height) * VignetteInner;
		float outer = MathF.Max(width, height) * VignetteOuter;
		float d = Vector2.Distance(new Vector2(x, y), new Vector2(width * 0.5f, height * 0.5f));
		return VignetteOpacity * Math.Clamp((d - inner) / (outer - inner), 0f, 1f);
	}

	// ---------- Tira de velocidad (drawSpeed2), 768 × 144 ----------

	// Lo fijo: base, esquinas y rótulos
	public static void SpeedBase(List<ScreenPrimitive> output)
	{
		Base(output, SpeedWidth, SpeedHeight);
		Corners(output, SpeedWidth, SpeedHeight);
		output.Add(Text("SPD", 24f, 42f, 34f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text("BST", 24f, 84f, 30f, Label(0.8f), ScreenTextAlign.Left, 0f));
		output.Add(Text("KM/H", 622f, 92f, 30f, Label(0.9f), ScreenTextAlign.Left, 0f));
	}

	// Lo vivo: texto del boost, número, segmentos, marca de TOP y efecto CRT
	public static void Speed(List<ScreenPrimitive> output, CockpitHudState hud, bool boostActive, float time)
	{
		output.Add(Text(hud.BoostText, 92f, 84f, 34f, BoostTextColor(hud.BoostText, boostActive), ScreenTextAlign.Left, 8f));
		output.Add(Text(hud.SpeedText, 610f, 92f, 112f, hud.SpeedColor, ScreenTextAlign.Right, 14f));

		int lit = LitSegments(hud.SpeedFraction);
		float sw = (SegmentRight - SegmentLeft) / SpeedSegments;
		Vector4 bar = C(IndustrialPalette.Bar);
		for (int i = 0; i < SpeedSegments; i++)
		{
			bool on = i < lit;
			output.Add(FillRect(SegmentLeft + i * sw + SegmentGap * 0.5f, SegmentY, sw - SegmentGap, SegmentHeight,
				on ? hud.SpeedColor : bar, on ? 8f : 0f));
		}

		float tx = TopMarkX(hud.TopFraction);
		output.Add(Line(new Vector2(tx, SegmentY - 6f), new Vector2(tx, SegmentY + SegmentHeight + 6f), 3f, C(IndustrialPalette.Over)));
		CrtFx(output, SpeedWidth, SpeedHeight, time);
	}

	// Segmentos encendidos: redondeo(48 × f), con las mitades hacia arriba como Math.round
	public static int LitSegments(float fraction) => Math.Clamp((int)MathF.Floor(SpeedSegments * fraction + 0.5f), 0, SpeedSegments);

	// La marca de TOP, en TOP / MAX del ancho de los segmentos
	public static float TopMarkX(float topFraction) => SegmentLeft + (SegmentRight - SegmentLeft) * topFraction;

	// Texto del boost: crit si es LOCKED, over con el boost activo, main si no (tabla 6.1)
	public static Vector4 BoostTextColor(string boostText, bool boostActive) =>
		boostText == "LOCKED" ? C(IndustrialPalette.Crit) : boostActive ? C(IndustrialPalette.Over) : Main;

	// ---------- Casco (drawDamage2), 300 × 300 ----------

	public static void DamageBase(List<ScreenPrimitive> output)
	{
		Base(output, DamageSize, DamageSize);
		Corners(output, DamageSize, DamageSize);
		output.Add(Text("HULL", DamageSize * 0.5f, 46f, 36f, Label(0.9f), ScreenTextAlign.Center, 0f));
	}

	// Tres sectores de abajo arriba: encendidos tantos como el nivel; con nivel 3, parpadeando. En TIME TRIAL, apagados
	// con borde dim y sin texto.
	public static void Damage(List<ScreenPrimitive> output, CockpitHudState hud, float time)
	{
		bool tt = !hud.DamageEnabled;
		int level = hud.DamageLevel;
		float a = level >= DamageModel.MaxLevel ? (hud.BlinkOn ? 1f : CockpitHudState.BlinkDimOpacity) : 1f;
		Vector4 color = hud.DamageTextColor;
		for (int i = 0; i < 3; i++)
		{
			float y = SectorBottomY - i * SectorStep;
			bool lit = !tt && i < level;
			if (lit)
				output.Add(FillRect(SectorX, y, SectorWidth, SectorHeight, WithAlpha(color, a), 12f));
			Vector4 stroke = lit ? color : tt ? C(IndustrialPalette.Dim, 0.6f) : Label(0.6f);
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.StrokeRect, new Vector2(SectorX, y), new Vector2(SectorWidth, SectorHeight),
				0f, 2.5f, stroke, 0f));
			output.Add(Text(SectorNames[i], DamageSize * 0.5f, y + 34f, 30f,
				lit ? C(IndustrialPalette.ScreenBackground, a) : Label(0.6f), ScreenTextAlign.Center, 0f));
		}
		if (!tt)
			output.Add(Text(hud.DamageText, DamageSize * 0.5f, 288f, 34f, WithAlpha(color, a), ScreenTextAlign.Center, 10f));
		CrtFx(output, DamageSize, DamageSize, time);
	}

	// ---------- Radar (drawRadar2), 384 × 294: el radar lo pinta RadarView con RadarStyle.IndustrialScreen ----------

	public static void RadarBase(List<ScreenPrimitive> output)
	{
		Base(output, RadarWidth, RadarHeight);
		Corners(output, RadarWidth, RadarHeight);
		output.Add(Text("RDR", 18f, 34f, 28f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text("120 M", RadarWidth - 18f, 34f, 28f, Label(0.9f), ScreenTextAlign.Right, 0f));
	}

	// TGT con las naves dentro de los 120 m y la distancia a la más cercana, encima del radar, y el efecto CRT
	public static void RadarLive(List<ScreenPrimitive> output, int targets, float? nearest, bool nearestClose, float time)
	{
		output.Add(Text("TGT " + Math.Min(targets, 99).ToString("00", CultureInfo.InvariantCulture), 18f, RadarHeight - 14f, 26f,
			Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text(MilitaryScreenDrawing.NearestText(nearest), RadarWidth - 18f, RadarHeight - 14f, 28f,
			nearest.HasValue && nearestClose ? C(IndustrialPalette.Crit) : C(IndustrialPalette.Rival), ScreenTextAlign.Right, 6f));
		CrtFx(output, RadarWidth, RadarHeight, time);
	}

	// ---------- Minimapa (drawMap2), 384 × 294: el mapa lo pinta MinimapView con MinimapStyle.IndustrialScreen ----------

	public static void MapBase(List<ScreenPrimitive> output)
	{
		Base(output, MapWidth, MapHeight);
		Corners(output, MapWidth, MapHeight);
		output.Add(Text("MAP", 18f, 34f, 28f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text("TRK UP", MapWidth - 18f, 34f, 28f, Label(0.9f), ScreenTextAlign.Right, 0f));
	}

	// Progreso de la vuelta (lapBar) y el efecto CRT
	public static void MapLive(List<ScreenPrimitive> output, string lapText, float lapFraction, float time)
	{
		float f = Math.Clamp(lapFraction, 0f, 1f);
		float width = ProgressRight - ProgressLeft;
		output.Add(FillRect(ProgressLeft, ProgressY, width, ProgressHeight, C(IndustrialPalette.Bar)));
		if (f > 0f)
			output.Add(FillRect(ProgressLeft, ProgressY, width * f, ProgressHeight, Main, 6f));
		output.Add(Text(lapText ?? "", 18f, 276f, 24f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text(CockpitInstruments.LapPercent(f).ToString(CultureInfo.InvariantCulture) + " %", MapWidth - 18f, 276f, 24f, Main,
			ScreenTextAlign.Right, 6f));
		CrtFx(output, MapWidth, MapHeight, time);
	}

	// ---------- Esfera de los relojes pequeños (miniGauge) ----------

	// Fondo, arco rojo en el último 20 %, 9 marcas (largas cada 4), rótulo y aguja fija. Decorativa: se pinta una vez.
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
		output.Add(Text(label, GaugeCenterX, GaugeCenterY + 34f, 20f, cream, ScreenTextAlign.Center, 0f));

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

	private static ScreenPrimitive FillRect(float x, float y, float width, float height, Vector4 color, float glow = 0f) =>
		new(ScreenPrimitiveKind.FillRect, new Vector2(x, y), new Vector2(width, height), 0f, 0f, color, glow);

	private static ScreenPrimitive Line(Vector2 a, Vector2 b, float width, Vector4 color) =>
		new(ScreenPrimitiveKind.Line, a, b, 0f, width, color, 0f);

	private static ScreenPrimitive Disc(Vector2 center, float radius, Vector4 color) =>
		new(ScreenPrimitiveKind.Disc, center, default, radius, 0f, color, 0f);

	private static ScreenPrimitive Text(string text, float x, float y, float size, Vector4 color, ScreenTextAlign align, float glow) =>
		new(ScreenPrimitiveKind.Text, new Vector2(x, y), default, size, 0f, color, glow, text, 0f, align);

	private static Vector4 WithAlpha(Vector4 color, float factor) => new(color.X, color.Y, color.Z, color.W * factor);
}
