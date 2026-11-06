using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Radar;

// La línea central en planta y la meta, preparadas una vez al cargar el circuito (spec docs/radar-minimapa.md, 5.3).
// Las coordenadas son (X, Z) del mundo.
public sealed class MinimapPath
{
	public const int DefaultPointCount = 400;
	private const float FinishHalfWidths = 3f;   // la raya de meta mide ±3 semianchos de pista

	public Vector2[] Points { get; }
	public Vector2 FinishA { get; }
	public Vector2 FinishB { get; }

	public MinimapPath(TrackFrame frame, float trackWidth, int pointCount = DefaultPointCount)
	{
		Points = new Vector2[pointCount];
		for (int i = 0; i < pointCount; i++)
		{
			Vector3 p = frame.At(frame.LapLength * i / pointCount).Position;
			Points[i] = new Vector2(p.X, p.Z);
		}

		FramePoint finish = frame.At(0f);
		var center = new Vector2(finish.Position.X, finish.Position.Z);
		var right = new Vector2(finish.PlanRight.X, finish.PlanRight.Z);
		float half = trackWidth * 0.5f * FinishHalfWidths;
		FinishA = center - right * half;
		FinishB = center + right * half;
	}
}

// Del mundo a la pantalla del minimapa: tu nave en el centro y tu frente arriba
public readonly struct MinimapProjection
{
	private readonly Vector2 _self;
	private readonly Vector2 _forward;
	private readonly Vector2 _center;
	private readonly float _scale;

	// self y planForward en (X, Z) del mundo; scale en px por metro
	public MinimapProjection(Vector2 self, Vector2 planForward, Vector2 center, float scale)
	{
		_self = self;
		_forward = planForward;
		_center = center;
		_scale = scale;
	}

	public Vector2 ToScreen(Vector2 world)
	{
		float dx = world.X - _self.X;
		float dy = world.Y - _self.Y;
		// Derecha en planta = (-frente.Z, frente.X), la misma de RadarProjection
		float x = dx * -_forward.Y + dy * _forward.X;
		float y = dx * _forward.X + dy * _forward.Y;
		return new Vector2(_center.X + x * _scale, _center.Y - y * _scale);
	}
}

// Recorte de un segmento a un círculo, calculado (sin recortar en el motor, que obligaría a pintar por un búfer aparte)
public static class CircleClip
{
	// t0 y t1: el tramo del segmento (de 0 a 1) que queda dentro. False si no entra nada.
	public static bool Segment(Vector2 a, Vector2 b, Vector2 center, float radius, out float t0, out float t1)
	{
		t0 = 0f;
		t1 = 1f;
		Vector2 d = b - a;
		Vector2 f = a - center;
		float qa = d.LengthSquared();
		float qc = f.LengthSquared() - radius * radius;
		if (qa <= 0f)
			return qc <= 0f;

		float qb = Vector2.Dot(f, d);
		float discriminant = qb * qb - qa * qc;
		if (discriminant <= 0f)
			return false;

		float root = MathF.Sqrt(discriminant);
		t0 = MathF.Max(0f, (-qb - root) / qa);
		t1 = MathF.Min(1f, (-qb + root) / qa);
		return t0 < t1;
	}
}

// Un tramo seguido del trazado dentro del círculo: un rango de Points
public readonly record struct MinimapRun(int Start, int Count);

// Con qué se dibuja una línea del trazado. Color con su opacidad.
public readonly record struct MinimapStroke(Vector4 Color, float Width, float Glow);

// Paleta y medidas con las que se dibuja el minimapa, como RadarStyle
public sealed record MinimapStyle
{
	// Medidas del HUD de pantalla, en px de 1280×720 (spec 5.2)
	private const float HudBandWidth = 7f;
	private const float HudBandOpacity = 0.55f;
	private const float HudLineWidth = 1.6f;
	private const float HudLineOpacity = 0.9f;
	private const float HudLineGlow = 5f;
	private const float HudFinishWidth = 2.5f;
	private const float HudShipRadius = 3.2f;
	private const float HudShipGlow = 8f;
	private const float HudSelfMinSize = 5.5f;
	private const float HudSelfSizeFactor = 0.06f;
	private const float HudSelfGlow = 6f;
	private const float HudUpMarkWidth = 10f;
	private const float HudUpMarkHeight = 7f;
	private const float HudUpMarkGap = 2f;
	private const float HudUpMarkOpacity = 0.8f;
	private const float HudClipInset = 1f;

	public RadarPalette Palette { get; init; }
	public Vector2 Center { get; init; }
	public float Radius { get; init; }
	public float ClipInset { get; init; }           // el trazado se recorta este tanto por dentro del borde
	public float BackgroundOpacity { get; init; }
	public float BorderWidth { get; init; }
	public float BorderOpacity { get; init; }
	public float BorderGlow { get; init; }
	public float BandWidth { get; init; }
	public float BandOpacity { get; init; }
	public float LineWidth { get; init; }
	public float LineOpacity { get; init; }
	public float LineGlow { get; init; }
	public float FinishWidth { get; init; }
	public float ShipRadius { get; init; }
	public float ShipGlow { get; init; }
	public float SelfSize { get; init; }
	public float SelfGlow { get; init; }
	public float UpMarkWidth { get; init; }         // 0: sin marca de "arriba"
	public float UpMarkHeight { get; init; }
	public float UpMarkGap { get; init; }           // de la punta al borde
	public float UpMarkOpacity { get; init; }

	// Estilo de la pantalla pequeña de la cabina 3D, en sus 256 px (spec docs/cabinas-3d-1-vector.md, 6.3)
	public static MinimapStyle CockpitScreen(RadarPalette palette)
	{
		return new MinimapStyle
		{
			Palette = palette,
			Center = new Vector2(128f, 128f),
			Radius = 106f,
			ClipInset = 0f,
			BackgroundOpacity = 0f,
			BorderWidth = 2f,
			BorderOpacity = 0.8f,
			BorderGlow = 0f,
			BandWidth = 14f,
			BandOpacity = 0.35f,
			LineWidth = 3f,
			LineOpacity = 0.9f,
			LineGlow = 4.8f,
			FinishWidth = 4f,
			ShipRadius = 6f,
			ShipGlow = 8f,
			SelfSize = 11f,
			SelfGlow = 6f,
			UpMarkWidth = 0f
		};
	}

	// Estilo del HUD de pantalla: radius en px de 1280×720 y scale el factor hasta los px reales. Centrado en el origen.
	public static MinimapStyle Hud(RadarPalette palette, float radius, float scale)
	{
		return new MinimapStyle
		{
			Palette = palette,
			Center = Vector2.Zero,
			Radius = radius * scale,
			ClipInset = HudClipInset * scale,
			BackgroundOpacity = RadarStyle.HudBackgroundOpacity,
			BorderWidth = RadarStyle.HudBorderWidth * scale,
			BorderOpacity = RadarStyle.HudBorderOpacity,
			BorderGlow = RadarStyle.HudBorderGlow * scale,
			BandWidth = HudBandWidth * scale,
			BandOpacity = HudBandOpacity,
			LineWidth = HudLineWidth * scale,
			LineOpacity = HudLineOpacity,
			LineGlow = HudLineGlow * scale,
			FinishWidth = HudFinishWidth * scale,
			ShipRadius = HudShipRadius * scale,
			ShipGlow = HudShipGlow * scale,
			SelfSize = MathF.Max(HudSelfMinSize, radius * HudSelfSizeFactor) * scale,
			SelfGlow = HudSelfGlow * scale,
			UpMarkWidth = HudUpMarkWidth * scale,
			UpMarkHeight = HudUpMarkHeight * scale,
			UpMarkGap = HudUpMarkGap * scale,
			UpMarkOpacity = HudUpMarkOpacity
		};
	}
}

// Lo que se dibuja en un frame: el trazado por tramos, sus dos pasadas y el resto en primitivas. Se reutiliza de un frame a otro.
public sealed class MinimapPicture
{
	public readonly List<Vector2> Points = new();
	public readonly List<MinimapRun> Runs = new();
	public readonly List<RadarPrimitive> Primitives = new();
	public MinimapStroke Band;
	public MinimapStroke Line;

	public void Clear()
	{
		Points.Clear();
		Runs.Clear();
		Primitives.Clear();
	}
}

// Dibuja el minimapa sin Godot (spec 5): la parte fija una vez y la viva cada frame
public static class MinimapDrawing
{
	// Fondo, borde y marca de "arriba"
	public static void Base(List<RadarPrimitive> output, MinimapStyle style)
	{
		RadarPalette palette = style.Palette;
		Vector2 c = style.Center;
		float r = style.Radius;

		if (style.BackgroundOpacity > 0f)
			output.Add(RadarDrawing.Disc(c, r, palette.Background, style.BackgroundOpacity, 0f));
		output.Add(RadarDrawing.Ring(c, r, style.BorderWidth, palette.Border, style.BorderOpacity, style.BorderGlow));

		if (style.UpMarkWidth > 0f)
		{
			output.Add(new RadarPrimitive(RadarPrimitiveKind.DownTriangle, new Vector2(c.X, c.Y - r - style.UpMarkGap), default,
				style.UpMarkWidth * 0.5f, style.UpMarkHeight, 0f, 0f, new Vector4(palette.Border, style.UpMarkOpacity), 0f, null));
		}
	}

	// Trazado, meta, naves y la nave del jugador, con tu nave en el centro y tu frente arriba.
	// self y planForward en (X, Z) del mundo; windowRadius: los metros que caben del centro al borde.
	public static void Live(MinimapPicture picture, MinimapStyle style, MinimapPath path, Vector2 self, Vector2 planForward,
		float windowRadius, IReadOnlyList<RaceShip> ships)
	{
		RadarPalette palette = style.Palette;
		Vector2 c = style.Center;
		float clip = style.Radius - style.ClipInset;
		var projection = new MinimapProjection(self, planForward, c, style.Radius / windowRadius);

		picture.Clear();
		picture.Band = new MinimapStroke(new Vector4(palette.Band, style.BandOpacity), style.BandWidth, 0f);
		picture.Line = new MinimapStroke(new Vector4(palette.Track, style.LineOpacity), style.LineWidth, style.LineGlow);
		TracePath(picture, path.Points, projection, c, clip);

		Vector2 a = projection.ToScreen(path.FinishA);
		Vector2 b = projection.ToScreen(path.FinishB);
		if (CircleClip.Segment(a, b, c, clip, out float t0, out float t1))
		{
			picture.Primitives.Add(RadarDrawing.Line(Vector2.Lerp(a, b, t0), Vector2.Lerp(a, b, t1), style.FinishWidth, palette.Finish, 1f));
		}

		for (int i = 0; i < ships.Count; i++)
		{
			Vector3 position = ships[i].Position;
			Vector2 ship = projection.ToScreen(new Vector2(position.X, position.Z));
			if (Vector2.DistanceSquared(ship, c) < clip * clip)
				picture.Primitives.Add(RadarDrawing.Disc(ship, style.ShipRadius, palette.Rival, 1f, style.ShipGlow));
		}

		picture.Primitives.Add(new RadarPrimitive(RadarPrimitiveKind.Arrow, c, default, style.SelfSize, 0f, 0f, 0f,
			new Vector4(palette.Self, 1f), style.SelfGlow, null));
	}

	// Parte la vuelta cerrada en los tramos que caen dentro del círculo
	private static void TracePath(MinimapPicture picture, Vector2[] world, in MinimapProjection projection, Vector2 center, float radius)
	{
		int n = world.Length;

		// Se empieza en un punto de fuera, así ningún tramo queda partido por el principio de la lista
		int start = -1;
		for (int i = 0; i < n && start < 0; i++)
		{
			if (Vector2.DistanceSquared(projection.ToScreen(world[i]), center) > radius * radius)
				start = i;
		}

		// La vuelta entera cabe dentro
		if (start < 0)
		{
			for (int i = 0; i <= n; i++)
				picture.Points.Add(projection.ToScreen(world[i % n]));
			picture.Runs.Add(new MinimapRun(0, n + 1));
			return;
		}

		int runStart = -1;
		Vector2 previous = projection.ToScreen(world[start]);
		for (int i = 1; i <= n; i++)
		{
			Vector2 current = projection.ToScreen(world[(start + i) % n]);
			if (CircleClip.Segment(previous, current, center, radius, out float t0, out float t1))
			{
				if (runStart < 0)
				{
					runStart = picture.Points.Count;
					picture.Points.Add(Vector2.Lerp(previous, current, t0));
				}
				picture.Points.Add(Vector2.Lerp(previous, current, t1));

				// Sale del círculo en este segmento
				if (t1 < 1f)
				{
					picture.Runs.Add(new MinimapRun(runStart, picture.Points.Count - runStart));
					runStart = -1;
				}
			}
			previous = current;
		}

		if (runStart >= 0)
			picture.Runs.Add(new MinimapRun(runStart, picture.Points.Count - runStart));
	}
}
