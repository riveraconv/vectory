using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Radar;

// Colores del radar y el minimapa (spec docs/radar-minimapa.md, 7): RGB sin opacidad (las opacidades van en el estilo).
// La del HUD de pantalla sale de Palette (RadarPainter.HudPalette); la lógica no conoce ningún color.
public sealed record RadarPalette(
	Vector3 Background, Vector3 Border, Vector3 Rings, Vector3 Beam,
	Vector3 Rival, Vector3 Close, Vector3 Self, Vector3 Label,
	Vector3 Track, Vector3 Band, Vector3 Finish);

// Paleta y medidas con las que se dibuja el radar. El tamaño solo no basta: otra pantalla (cabina 3D) no es esto a escala.
public sealed record RadarStyle
{
	// Medidas del HUD de pantalla, en px de 1280×720 (spec 4.3)
	internal const float HudBackgroundOpacity = 0.72f;   // fondo y borde: los mismos en el minimapa
	internal const float HudBorderWidth = 2f;
	internal const float HudBorderOpacity = 0.85f;
	internal const float HudBorderGlow = 6f;
	private const float HudRingWidth = 1f;
	private const float HudRingOpacity = 0.55f;
	private const float HudCrossOpacity = 0.35f;
	private const float HudBeamWidth = 2f;
	private const float HudBeamOpacity = 0.4f;
	private const float HudEchoRadius = 3.4f;
	private const float HudEchoCloseRadius = 4.2f;
	private const float HudEchoGlow = 8f;
	private const float HudWarningOffset = 4f;
	private const float HudWarningWidth = 4f;
	private const float HudWarningGlow = 10f;
	private const float HudSelfMinSize = 6f;
	private const float HudSelfSizeFactor = 0.075f;
	private const float HudSelfGlow = 6f;
	private const float HudLabelSize = 11f;
	private const float HudLabelOpacity = 0.8f;
	private const float HudLabelX = 0.72f;      // × radio, desde el centro
	private const float HudLabelY = 0.98f;

	public RadarPalette Palette { get; init; }
	public Vector2 Center { get; init; }
	public float Radius { get; init; }
	public float BackgroundOpacity { get; init; }   // 0: sin fondo propio
	public float BorderWidth { get; init; }
	public float BorderOpacity { get; init; }
	public float BorderGlow { get; init; }
	public float RingWidth { get; init; }
	public float RingOpacity { get; init; }
	public float CrossOpacity { get; init; }
	public int BeamLines { get; init; }
	public float BeamStep { get; init; }            // rad entre líneas del haz
	public float BeamWidth { get; init; }
	public float BeamOpacity { get; init; }
	public float EchoRadius { get; init; }
	public float EchoCloseRadius { get; init; }
	public float EchoGlow { get; init; }
	public float WarningOffset { get; init; }
	public float WarningWidth { get; init; }
	public float WarningHalfAngle { get; init; }    // rad
	public float WarningGlow { get; init; }
	public float SelfSize { get; init; }
	public float SelfGlow { get; init; }
	public float LabelSize { get; init; }
	public float LabelOpacity { get; init; }
	public Vector2 LabelOffset { get; init; }       // desde el centro: el inicio de la línea base del texto

	// Estilo del HUD de pantalla: radius en px de 1280×720 y scale el factor hasta los px reales. Centrado en el origen.
	public static RadarStyle Hud(RadarPalette palette, float radius, float scale)
	{
		return new RadarStyle
		{
			Palette = palette,
			Center = Vector2.Zero,
			Radius = radius * scale,
			BackgroundOpacity = HudBackgroundOpacity,
			BorderWidth = HudBorderWidth * scale,
			BorderOpacity = HudBorderOpacity,
			BorderGlow = HudBorderGlow * scale,
			RingWidth = HudRingWidth * scale,
			RingOpacity = HudRingOpacity,
			CrossOpacity = HudCrossOpacity,
			BeamLines = 28,
			BeamStep = 0.035f,
			BeamWidth = HudBeamWidth * scale,
			BeamOpacity = HudBeamOpacity,
			EchoRadius = HudEchoRadius * scale,
			EchoCloseRadius = HudEchoCloseRadius * scale,
			EchoGlow = HudEchoGlow * scale,
			WarningOffset = HudWarningOffset * scale,
			WarningWidth = HudWarningWidth * scale,
			WarningHalfAngle = 0.42f,
			WarningGlow = HudWarningGlow * scale,
			SelfSize = MathF.Max(HudSelfMinSize, radius * HudSelfSizeFactor) * scale,
			SelfGlow = HudSelfGlow * scale,
			LabelSize = HudLabelSize * scale,
			LabelOpacity = HudLabelOpacity,
			LabelOffset = new Vector2(radius * HudLabelX, radius * HudLabelY) * scale
		};
	}
}

public enum RadarPrimitiveKind { Disc, Ring, Line, Arc, Arrow, Text, DownTriangle }

// Una primitiva de dibujo, en el espacio del estilo. Color con su opacidad; Glow en px (0: sin halo).
//   Disc y Ring: centro A y Radius (Width, el grueso del anillo)
//   Line: de A a B con Width
//   Arc: centro A, Radius, de StartAngle a EndAngle, con Width
//   Arrow: centro A y tamaño Radius, apuntando arriba (ArrowPoints)
//   Text: inicio de la línea base en A y tamaño Radius
//   DownTriangle: punta abajo en A, medio ancho Radius y alto Width
public readonly record struct RadarPrimitive(
	RadarPrimitiveKind Kind, Vector2 A, Vector2 B, float Radius, float Width, float StartAngle, float EndAngle,
	Vector4 Color, float Glow, string Text);

// Dibuja el radar como lista de primitivas, sin Godot (spec 7). La parte fija y la viva van aparte: la fija solo se
// construye una vez. Los que la pintan solo traducen.
public static class RadarDrawing
{
	// Fondo, borde, anillos, cruz y rótulo
	public static void Base(List<RadarPrimitive> output, RadarStyle style, float range)
	{
		RadarPalette palette = style.Palette;
		Vector2 c = style.Center;
		float r = style.Radius;

		if (style.BackgroundOpacity > 0f)
			output.Add(Disc(c, r, palette.Background, style.BackgroundOpacity, 0f));

		output.Add(Ring(c, r, style.BorderWidth, palette.Border, style.BorderOpacity, style.BorderGlow));
		output.Add(Ring(c, r / 3f, style.RingWidth, palette.Rings, style.RingOpacity, 0f));
		output.Add(Ring(c, r * 2f / 3f, style.RingWidth, palette.Rings, style.RingOpacity, 0f));
		output.Add(Line(c - new Vector2(r, 0f), c + new Vector2(r, 0f), style.RingWidth, palette.Rings, style.CrossOpacity));
		output.Add(Line(c - new Vector2(0f, r), c + new Vector2(0f, r), style.RingWidth, palette.Rings, style.CrossOpacity));

		output.Add(new RadarPrimitive(RadarPrimitiveKind.Text, c + style.LabelOffset, default, style.LabelSize, 0f, 0f, 0f,
			WithOpacity(palette.Label, style.LabelOpacity), 0f, RangeLabel(range)));
	}

	// Haz, ecos, avisos de nave pegada y la nave del jugador. No guarda nada: se llama cada frame.
	public static void Live(List<RadarPrimitive> output, RadarStyle style, RadarSweep sweep)
	{
		RadarPalette palette = style.Palette;
		Vector2 c = style.Center;
		float r = style.Radius;

		// El haz llega justo al borde: ya queda recortado al círculo
		for (int k = 0; k < style.BeamLines; k++)
		{
			float angle = sweep.Angle - k * style.BeamStep - MathF.PI / 2f;
			var tip = new Vector2(c.X + MathF.Cos(angle) * r, c.Y + MathF.Sin(angle) * r);
			output.Add(Line(c, tip, style.BeamWidth, palette.Beam, style.BeamOpacity * (1f - (float)k / style.BeamLines)));
		}

		float range = sweep.Settings.Range;
		IReadOnlyList<RadarEcho> echoes = sweep.Echoes;
		for (int i = 0; i < echoes.Count; i++)
		{
			RadarEcho echo = echoes[i];
			var position = new Vector2(c.X + echo.X / range * r, c.Y - echo.Y / range * r);
			output.Add(Disc(position, echo.IsClose ? style.EchoCloseRadius : style.EchoRadius,
				echo.IsClose ? palette.Close : palette.Rival, sweep.EchoOpacity(in echo), style.EchoGlow));
		}

		IReadOnlyList<RadarWarning> warnings = sweep.Warnings;
		for (int i = 0; i < warnings.Count; i++)
		{
			RadarWarning warning = warnings[i];
			output.Add(new RadarPrimitive(RadarPrimitiveKind.Arc, c, default, r + style.WarningOffset, style.WarningWidth,
				warning.Angle - style.WarningHalfAngle, warning.Angle + style.WarningHalfAngle,
				WithOpacity(palette.Close, warning.Opacity), style.WarningGlow, null));
		}

		output.Add(new RadarPrimitive(RadarPrimitiveKind.Arrow, c, default, style.SelfSize, 0f, 0f, 0f,
			WithOpacity(palette.Self, 1f), style.SelfGlow, null));
	}

	public static string RangeLabel(float range) => $"{MathF.Round(range):0} M";

	// Los cuatro vértices de la flecha, apuntando arriba, como en la página de referencia
	public static void ArrowPoints(Vector2 center, float size, out Vector2 tip, out Vector2 right, out Vector2 notch, out Vector2 left)
	{
		tip = new Vector2(center.X, center.Y - size);
		right = new Vector2(center.X + size * 0.62f, center.Y + size * 0.7f);
		notch = new Vector2(center.X, center.Y + size * 0.35f);
		left = new Vector2(center.X - size * 0.62f, center.Y + size * 0.7f);
	}

	internal static RadarPrimitive Disc(Vector2 center, float radius, Vector3 color, float opacity, float glow)
		=> new(RadarPrimitiveKind.Disc, center, default, radius, 0f, 0f, 0f, WithOpacity(color, opacity), glow, null);

	internal static RadarPrimitive Ring(Vector2 center, float radius, float width, Vector3 color, float opacity, float glow)
		=> new(RadarPrimitiveKind.Ring, center, default, radius, width, 0f, 0f, WithOpacity(color, opacity), glow, null);

	internal static RadarPrimitive Line(Vector2 a, Vector2 b, float width, Vector3 color, float opacity)
		=> new(RadarPrimitiveKind.Line, a, b, 0f, width, 0f, 0f, WithOpacity(color, opacity), 0f, null);

	private static Vector4 WithOpacity(Vector3 color, float opacity) => new(color, opacity);
}
