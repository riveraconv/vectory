using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Vectory.Ship;

// Lo que pinta el cristal HUD además del estado del HUD de cabina
public readonly record struct CombinerInput(float SpeedKmh, float TopKmh, float HeadingDegrees, float Turn);

// Dibujo de las pantallas en estilo militar, sin Godot: base, corners, T, osb, drawCombiner, drawSpeed3, drawRadar3 y
// drawMap3 del prototipo de BLADE, con las mismas posiciones, tamaños, grosores, opacidades y textos
// (spec docs/cabinas-3d-2-blade.md, 6). El estado sale de CockpitHudState y aquí no hay reglas. Lógica pura.
// Lo que en el prototipo va recortado (cintas) se recorta por cálculo: las primitivas no se recortan solas.
public static class MilitaryScreenDrawing
{
	public const int CombinerWidth = 512;
	public const int CombinerHeight = 302;
	public const int SpeedWidth = 300;
	public const int SpeedHeight = 400;
	public const int RadarWidth = 512;
	public const int RadarHeight = 420;
	public const int MapWidth = 300;
	public const int MapHeight = 400;

	private const int ScanSpacing = 3;
	private const float CornerLength = 16f;
	private const float CornerMargin = 6f;
	private const float CornerWidth = 2f;

	// Cristal HUD
	public const float CombinerCenterY = CombinerHeight / 2f + 6f;   // 157
	public const float SpeedTapePxPerKmh = 1.7f;
	public const float HeadingPxPerDegree = 4f;
	public const float FlightPathPxPerTurn = 34f;
	private const float TapeLeft = 10f, TapeRight = 102f, TapeTop = 56f, TapeBottom = CombinerHeight - 36f;
	private const float TapeLineX = 96f;
	private const float SpeedBoxHalf = 20f;
	private const float HeadingLeft = 140f, HeadingRight = 372f;
	private const float ChevronX = 448f;

	// Pantalla izquierda: escala de velocidad
	public const float ScaleX = 30f, ScaleTop = 130f, ScaleBottom = 336f, ScaleWidth = 34f;
	private const int ScaleCuts = 20;

	// Radar
	private const float RadarCenterX = RadarWidth / 2f, RadarCenterY = 236f, RadarRadius = 160f;
	private const float RadarTapeLeft = 96f, RadarTapeRight = RadarWidth - 96f;

	// Minimapa: barra de progreso
	public const float ProgressX = 20f, ProgressY = 348f, ProgressWidth = MapWidth - 40f, ProgressHeight = 12f;

	private static Vector4 C(uint rgb, float alpha = 1f) => MilitaryPalette.Rgba(rgb, alpha);
	private static Vector4 Main => C(MilitaryPalette.Main);
	private static Vector4 Label(float alpha = 1f) => C(MilitaryPalette.Label, alpha);

	// ---------- Piezas comunes: base, corners, T ----------

	// Fondo, líneas de barrido (base) y esquinas (corners)
	public static void Base(List<ScreenPrimitive> output, int width, int height)
	{
		output.Add(FillRect(0f, 0f, width, height, C(MilitaryPalette.ScreenBackground)));
		Vector4 scan = C(MilitaryPalette.Scan, MilitaryPalette.ScanOpacity);
		for (int y = 0; y < height; y += ScanSpacing)
			output.Add(FillRect(0f, y, width, 1f, scan));
		Corners(output, width, height);
	}

	public static void Corners(List<ScreenPrimitive> output, int width, int height)
	{
		Vector4 frame = C(MilitaryPalette.Frame, MilitaryPalette.FrameOpacity);
		Corner(output, CornerMargin, CornerMargin, 1f, 1f, frame);
		Corner(output, width - CornerMargin, CornerMargin, -1f, 1f, frame);
		Corner(output, CornerMargin, height - CornerMargin, 1f, -1f, frame);
		Corner(output, width - CornerMargin, height - CornerMargin, -1f, -1f, frame);
	}

	// Texto fijo: fondo y líneas centradas (pilotos MASTER y placa)
	public static void Label(List<ScreenPrimitive> output, Cockpit3DLabel label)
	{
		output.Add(FillRect(0f, 0f, label.Width, label.Height, C(label.Background)));
		foreach (Cockpit3DLabelLine line in label.Lines)
			output.Add(Text(line.Text, label.Width * 0.5f, line.Y, label.FontSize, C(label.TextColor), ScreenTextAlign.Center, 0f));
	}

	// ---------- Cristal HUD (drawCombiner): se pinta cada frame ----------

	// En suma: no se pinta fondo (el negro del prototipo no se ve, y opaco taparía el mundo). La caja de la velocidad tapa
	// la cinta con su relleno negro: aquí, la cinta no se pinta donde está la caja.
	public static void Combiner(List<ScreenPrimitive> output, CockpitHudState hud, in CombinerInput input)
	{
		Vector4 main = Main;
		float cy = CombinerCenterY;
		float hdg = input.HeadingDegrees;

		// Rumbo arriba: marca cada 5°, número cada 10° (en decenas, dos cifras), recortado a x 140–372
		for (float d = MathF.Floor(hdg / 5f) * 5f - 30f; d <= hdg + 30f; d += 5f)
		{
			float x = 256f + (d - hdg) * HeadingPxPerDegree;
			bool ten = IsMultiple(d, 10f);
			if (x >= HeadingLeft && x <= HeadingRight)
				output.Add(Line(x, 34f, x, ten ? 24f : 29f, 2f, main, 6f));
			if (ten && x - 8f >= HeadingLeft && x + 8f <= HeadingRight)
				output.Add(Text(HeadingLabel(d), x, 20f, 16f, main, ScreenTextAlign.Center, 6f));
		}
		output.Add(Triangle(new Vector2(256f, 36f), new Vector2(250f, 44f), new Vector2(262f, 44f), main));

		// Cinta de velocidad a la izquierda, recortada a x 10–102 e y 56–266, con la caja encima
		float v = input.SpeedKmh;
		bool OnTape(float y) => y >= TapeTop && y <= TapeBottom && MathF.Abs(y - cy) >= SpeedBoxHalf;
		output.Add(Line(TapeLineX, TapeTop, TapeLineX, cy - SpeedBoxHalf, 2f, main, 6f));
		output.Add(Line(TapeLineX, cy + SpeedBoxHalf, TapeLineX, TapeBottom, 2f, main, 6f));
		for (float s = MathF.Floor((v - 80f) / 10f) * 10f; s <= v + 80f; s += 10f)
		{
			if (s < 0f)
				continue;
			float y = SpeedTapeY(s, v);
			if (!OnTape(y))
				continue;
			bool fifty = IsMultiple(s, 50f);
			output.Add(Line(TapeLineX, y, fifty ? 82f : 88f, y, 2f, main, 6f));
			if (fifty && OnTape(y + 6f) && OnTape(y - 6f))
				output.Add(Text(Integer(s), 76f, y + 6f, 17f, main, ScreenTextAlign.Right, 6f));
		}
		// Triángulo de la punta: el prototipo lo recorta en x = 102, así que solo asoma la punta (de 98 a 102)
		float topY = SpeedTapeY(input.TopKmh, v);
		if (OnTape(topY))
			output.Add(Triangle(new Vector2(98f, topY), new Vector2(TapeRight, topY - 2f), new Vector2(TapeRight, topY + 2f),
				C(MilitaryPalette.Over)));

		// Caja con la velocidad
		Vector4 speed = hud.SpeedColor;
		Polyline(output, new[]
		{
			new Vector2(12f, cy - 20f), new Vector2(92f, cy - 20f), new Vector2(104f, cy), new Vector2(92f, cy + 20f),
			new Vector2(12f, cy + 20f), new Vector2(12f, cy - 20f)
		}, 2f, speed, 8f);
		output.Add(Text(hud.SpeedText, 86f, cy + 10f, 30f, speed, ScreenTextAlign.Right, 10f));
		output.Add(Text("KM/H", 54f, cy + 40f, 13f, WithAlpha(main, 0.8f), ScreenTextAlign.Center, 4f));

		// Símbolo de la nave y marcador de trayectoria
		Polyline(output, new[]
		{
			new Vector2(226f, cy), new Vector2(244f, cy), new Vector2(250f, cy + 8f), new Vector2(256f, cy),
			new Vector2(262f, cy + 8f), new Vector2(268f, cy), new Vector2(286f, cy)
		}, 2f, main, 6f);
		Vector2 fpm = FlightPathMarker(input.Turn);
		output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Ring, fpm, default, 8f, 2f, main, 0f));
		output.Add(Line(fpm.X - 22f, fpm.Y, fpm.X - 8f, fpm.Y, 2f, main, 6f));
		output.Add(Line(fpm.X + 8f, fpm.Y, fpm.X + 22f, fpm.Y, 2f, main, 6f));
		output.Add(Line(fpm.X, fpm.Y - 8f, fpm.X, fpm.Y - 16f, 2f, main, 6f));

		// Boost: tres galones de abajo arriba
		output.Add(Text("BST", ChevronX, 82f, 16f, WithAlpha(main, 0.85f), ScreenTextAlign.Center, 4f));
		for (int i = 0; i < CockpitHudState.SlotCount; i++)
			Chevron(output, ChevronX, 150f - i * 34f + 40f, hud.Orb(i));
		output.Add(Text(hud.BoostText, ChevronX, 232f, 15f, BoostTextColor(hud), ScreenTextAlign.Center, 6f));
		output.Add(Text("TOP " + Integer(input.TopKmh), ChevronX, CombinerHeight - 22f, 13f, WithAlpha(main, 0.7f), ScreenTextAlign.Center, 3f));
	}

	// y de una velocidad en la cinta: 1,7 px por km/h, la velocidad actual en el centro
	public static float SpeedTapeY(float speedKmh, float currentKmh) => CombinerCenterY - (speedKmh - currentKmh) * SpeedTapePxPerKmh;

	// Marcador de trayectoria: desplazado entrada de giro × 34 px, 34 px por encima del centro
	public static Vector2 FlightPathMarker(float turn) => new(256f + turn * FlightPathPxPerTurn, CombinerCenterY - 34f);

	// Número de una marca de rumbo: en decenas, con dos cifras (350° → 35, 0° → 00)
	public static string HeadingLabel(float degrees)
	{
		int wrapped = (((int)MathF.Round(degrees) % 360) + 360) % 360;
		return (wrapped / 10).ToString("00", CultureInfo.InvariantCulture);
	}

	// Galón: dos brazos rellenos y su contorno, con el estilo de la carga
	private static void Chevron(List<ScreenPrimitive> output, float x, float y, CockpitHudPaint paint)
	{
		var a = new Vector2(x - 24f, y + 10f);
		var top = new Vector2(x, y - 8f);
		var b = new Vector2(x + 24f, y + 10f);
		var bOut = new Vector2(x + 24f, y + 18f);
		var inner = new Vector2(x, y);
		var aOut = new Vector2(x - 24f, y + 18f);
		output.Add(Triangle(a, top, inner, paint.Fill));
		output.Add(Triangle(a, inner, aOut, paint.Fill));
		output.Add(Triangle(top, b, inner, paint.Fill));
		output.Add(Triangle(inner, b, bOut, paint.Fill));
		Polyline(output, new[] { a, top, b, bOut, inner, aOut, a }, 2f, paint.Stroke, 0f);
	}

	// ---------- Pantalla izquierda (drawSpeed3): solo se pinta si cambia ----------

	public static void Speed(List<ScreenPrimitive> output, CockpitHudState hud)
	{
		const int w = SpeedWidth;
		Base(output, w, SpeedHeight);
		output.Add(Text("SPD", 18f, 30f, 15f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text("KM/H", w - 18f, 30f, 15f, Label(0.9f), ScreenTextAlign.Right, 0f));
		output.Add(Text(hud.SpeedText, w / 2f + 50f, 96f, 68f, hud.SpeedColor, ScreenTextAlign.Right, 12f));

		// Escala vertical: se rellena alto × f, cortada en 20 tramos, con 11 marcas y la línea de la punta
		float fill = ScaleFillTop(hud.SpeedFraction);
		output.Add(FillRect(ScaleX, ScaleTop, ScaleWidth, ScaleBottom - ScaleTop, C(MilitaryPalette.Bar)));
		output.Add(FillRect(ScaleX, fill, ScaleWidth, ScaleBottom - fill, hud.SpeedColor, 10f));
		Vector4 background = C(MilitaryPalette.ScreenBackground);
		for (int i = 1; i < ScaleCuts; i++)
		{
			float y = ScaleBottom - (ScaleBottom - ScaleTop) * i / ScaleCuts;
			output.Add(Line(ScaleX, y, ScaleX + ScaleWidth, y, 3f, background, 0f));
		}
		Vector4 tick = Label(0.7f);
		for (int i = 0; i <= 10; i++)
		{
			float y = ScaleBottom - (ScaleBottom - ScaleTop) * i / 10f;
			output.Add(Line(ScaleX + ScaleWidth + 3f, y, ScaleX + ScaleWidth + (i % 5 == 0 ? 13f : 8f), y, 1.5f, tick, 0f));
		}
		float top = ScaleTopLine(hud.TopFraction);
		output.Add(Line(ScaleX - 4f, top, ScaleX + ScaleWidth + 4f, top, 3f, C(MilitaryPalette.Over), 0f));

		// Boost: tres celdas de abajo arriba
		output.Add(Text("BST", 120f, 152f, 15f, Label(0.9f), ScreenTextAlign.Center, 0f));
		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			CockpitHudPaint paint = hud.Orb(i);
			float cy = 318f - i * 58f;
			output.Add(FillRect(100f, cy - 44f, 40f, 44f, paint.Fill, paint.Glow * 12f));
			output.Add(StrokeRect(100f, cy - 44f, 40f, 44f, 2f, paint.Stroke));
		}

		// Daño: columna de tres bloques de abajo arriba; en TIME TRIAL, apagados y sin texto
		output.Add(Text("DMG", 230f, 152f, 15f, Label(0.9f), ScreenTextAlign.Center, 0f));
		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			CockpitHudPaint paint = hud.Segment(i);
			float cy = 318f - i * 58f;
			if (paint.Fill.W > 0f)
				output.Add(FillRect(204f, cy - 44f, 52f, 44f, paint.Fill, paint.Glow * 10f));
			output.Add(StrokeRect(204f, cy - 44f, 52f, 44f, 2f, paint.Stroke));
			output.Add(Line(204f, cy - 44f, 256f, cy, 2f, WithAlpha(Opaque(paint.Stroke), 0.25f), 0f));
		}

		output.Add(Text(hud.BoostText, 120f, 364f, 16f, BoostTextColor(hud), ScreenTextAlign.Center, 6f));
		if (hud.DamageEnabled)
		{
			float alpha = hud.DamageLevel >= DamageModel.MaxLevel && !hud.BlinkOn ? CockpitHudState.BlinkDimOpacity : 1f;
			output.Add(Text(hud.DamageText, 230f, 364f, 16f, WithAlpha(hud.DamageTextColor, alpha), ScreenTextAlign.Center, 6f));
		}
	}

	// y del borde de arriba del relleno: alto × f desde abajo
	public static float ScaleFillTop(float speedFraction) => ScaleBottom - (ScaleBottom - ScaleTop) * speedFraction;

	// y de la línea de la punta: TOP / MAX del alto
	public static float ScaleTopLine(float topFraction) => ScaleBottom - (ScaleBottom - ScaleTop) * topFraction;

	// Texto del boost: crit si está bloqueado, over con el impulso activo, main si no
	public static Vector4 BoostTextColor(CockpitHudState hud) =>
		hud.BoostText == "LOCKED" ? C(MilitaryPalette.Crit) : hud.BoostText == "ACTIVE" ? C(MilitaryPalette.Over) : Main;

	// ---------- Radar (drawRadar3) ----------

	// Lo fijo: fondo, esquinas, rótulos de los anillos y de los botones, NRST y SWP
	public static void RadarBase(List<ScreenPrimitive> output, float sweepPeriod)
	{
		const int w = RadarWidth;
		Base(output, w, RadarHeight);
		output.Add(Text("NRST", 16f, 130f, 15f, Label(0.7f), ScreenTextAlign.Left, 0f));
		output.Add(Text("SWP " + sweepPeriod.ToString("0.0", CultureInfo.InvariantCulture) + "S", w - 16f, 104f, 15f, Label(0.7f),
			ScreenTextAlign.Right, 0f));
		Osb(output, w, RadarHeight, new[] { "RDR", "RNG 120", "AUTO", "DCL" }, "RDR");
	}

	// Lo vivo, encima del radar: cinta de rumbo, rótulos de los anillos, TGT y la distancia a la más cercana
	public static void RadarLive(List<ScreenPrimitive> output, float headingDegrees, int targets, float? nearest, bool nearestClose)
	{
		Vector4 label = Label();
		float hdg = headingDegrees;
		for (float d = MathF.Floor(hdg / 5f) * 5f - 40f; d <= hdg + 40f; d += 5f)
		{
			float x = RadarCenterX + (d - hdg) * HeadingPxPerDegree;
			bool ten = IsMultiple(d, 10f);
			if (x >= RadarTapeLeft && x <= RadarTapeRight)
				output.Add(Line(x, 62f, x, ten ? 50f : 56f, 2f, label, 0f));
			if (ten && x - 8f >= RadarTapeLeft && x + 8f <= RadarTapeRight)
				output.Add(Text(HeadingLabel(d), x, 46f, 16f, label, ScreenTextAlign.Center, 0f));
		}
		output.Add(Triangle(new Vector2(RadarCenterX, 64f), new Vector2(RadarCenterX - 7f, 74f), new Vector2(RadarCenterX + 7f, 74f), Main));

		Vector4 ring = Label(0.8f);
		output.Add(Text("40", RadarCenterX + 6f, RadarCenterY - RadarRadius / 3f - 4f, 14f, ring, ScreenTextAlign.Left, 0f));
		output.Add(Text("80", RadarCenterX + 6f, RadarCenterY - 2f * RadarRadius / 3f - 4f, 14f, ring, ScreenTextAlign.Left, 0f));
		output.Add(Text("120", RadarCenterX + 6f, RadarCenterY - RadarRadius - 4f, 14f, ring, ScreenTextAlign.Left, 0f));

		output.Add(Text("TGT " + Math.Min(targets, 99).ToString("00", CultureInfo.InvariantCulture), 16f, 104f, 17f, Label(0.9f),
			ScreenTextAlign.Left, 0f));
		output.Add(Text(NearestText(nearest), 16f, 154f, 20f, nearest.HasValue && nearestClose ? C(MilitaryPalette.Crit) : C(MilitaryPalette.Rival),
			ScreenTextAlign.Left, 6f));
	}

	// Distancia a la más cercana: tres cifras y M, o --- sin ninguna
	public static string NearestText(float? distance) =>
		distance.HasValue ? Math.Min((int)MathF.Round(distance.Value), 999).ToString("000", CultureInfo.InvariantCulture) + " M" : "---";

	// Rótulos junto a los botones de abajo: el seleccionado, en negativo sobre fósforo
	private static void Osb(List<ScreenPrimitive> output, int width, int height, string[] bottom, string selected)
	{
		float y = height - 12f;
		for (int i = 0; i < bottom.Length; i++)
		{
			string s = bottom[i];
			float x = width * (i + 0.5f) / bottom.Length;
			if (s == selected)
			{
				float tw = s.Length * 8.5f + 8f;
				output.Add(FillRect(x - tw / 2f, y - 14f, tw, 18f, WithAlpha(Main, 0.85f)));
				output.Add(Text(s, x, y, 15f, C(MilitaryPalette.ScreenBackground), ScreenTextAlign.Center, 0f));
			}
			else
				output.Add(Text(s, x, y, 15f, Label(0.95f), ScreenTextAlign.Center, 0f));
		}
	}

	// ---------- Minimapa (drawMap3) ----------

	public static void MapBase(List<ScreenPrimitive> output, float windowMeters)
	{
		const int w = MapWidth;
		Base(output, w, MapHeight);
		output.Add(Text("MAP", 18f, 30f, 15f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text("TRK UP", w - 18f, 30f, 15f, Label(0.9f), ScreenTextAlign.Right, 0f));
		output.Add(Text(Integer(windowMeters) + " M", w / 2f, 334f, 14f, Label(0.8f), ScreenTextAlign.Center, 0f));
	}

	// Progreso de la vuelta: barra, LAP n/m y porcentaje
	public static void MapLive(List<ScreenPrimitive> output, string lapText, float lapFraction)
	{
		float f = Math.Clamp(lapFraction, 0f, 1f);
		output.Add(FillRect(ProgressX, ProgressY, ProgressWidth, ProgressHeight, C(MilitaryPalette.Bar)));
		if (f > 0f)
			output.Add(FillRect(ProgressX, ProgressY, ProgressWidth * f, ProgressHeight, Main, 6f));
		output.Add(Text(lapText ?? "", ProgressX, 384f, 15f, Label(0.9f), ScreenTextAlign.Left, 0f));
		output.Add(Text(CockpitInstruments.LapPercent(f).ToString(CultureInfo.InvariantCulture) + " %", MapWidth - 20f, 384f, 15f, Main,
			ScreenTextAlign.Right, 4f));
	}

	// ---------- Primitivas ----------

	private static bool IsMultiple(float value, float step) => MathF.Abs(value - MathF.Round(value / step) * step) < 0.001f;

	private static string Integer(float value) => ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture);

	private static void Corner(List<ScreenPrimitive> output, float x, float y, float sx, float sy, Vector4 color)
	{
		output.Add(Line(x, y + sy * CornerLength, x, y, CornerWidth, color, 0f));
		output.Add(Line(x, y, x + sx * CornerLength, y, CornerWidth, color, 0f));
	}

	private static void Polyline(List<ScreenPrimitive> output, Vector2[] points, float width, Vector4 color, float glow)
	{
		for (int i = 0; i < points.Length - 1; i++)
			output.Add(new ScreenPrimitive(ScreenPrimitiveKind.Line, points[i], points[i + 1], 0f, width, color, glow));
	}

	private static ScreenPrimitive FillRect(float x, float y, float width, float height, Vector4 color, float glow = 0f) =>
		new(ScreenPrimitiveKind.FillRect, new Vector2(x, y), new Vector2(width, height), 0f, 0f, color, glow);

	private static ScreenPrimitive StrokeRect(float x, float y, float width, float height, float stroke, Vector4 color) =>
		new(ScreenPrimitiveKind.StrokeRect, new Vector2(x, y), new Vector2(width, height), 0f, stroke, color, 0f);

	private static ScreenPrimitive Line(float x0, float y0, float x1, float y1, float width, Vector4 color, float glow) =>
		new(ScreenPrimitiveKind.Line, new Vector2(x0, y0), new Vector2(x1, y1), 0f, width, color, glow);

	private static ScreenPrimitive Triangle(Vector2 a, Vector2 b, Vector2 c, Vector4 color) =>
		new(ScreenPrimitiveKind.Triangle, a, b, 0f, 0f, color, 0f, C: c);

	private static ScreenPrimitive Text(string text, float x, float y, float size, Vector4 color, ScreenTextAlign align, float glow) =>
		new(ScreenPrimitiveKind.Text, new Vector2(x, y), default, size, 0f, color, glow, text, 0f, align);

	private static Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, color.W * alpha);

	private static Vector4 Opaque(Vector4 color) => new(color.X, color.Y, color.Z, 1f);
}
