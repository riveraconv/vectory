using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Rol de color de la cabina. El nodo que la dibuja lo traduce con Palette (CockpitPalette).
public enum CockpitColor
{
	Cyan,
	Blue,
	Orange,
	BlueGray,
	Green,
	Yellow,
	Red,
	Black,
	Highlight,
	GlassReflection,
	PanelLight,
	PanelMid,
	PanelDark,
	ConsoleLight,
	ConsoleDark,
	FrontLight,
	FrontDark,
	PillarLight,
	NoseLight,
	CowlLight,
	Step,
	Sink,
	SinkWall,
	Screen,
	Vent,
	Screw,
	DialShadow
}

// Degradados y trama de las pantallas. Se dibujan sobre la caja de cada polígono, como hace el SVG por defecto.
public enum CockpitGradientId
{
	Top,
	Console,
	Front,
	Pillar,
	Side,
	NoseLeft,
	NoseRight,
	ScreenGlow,
	TintOrange,
	TintRed,
	ShadowDown,
	OcclusionLeft,
	OcclusionRight,
	Cowl,
	BezelIn,
	DeckFar,
	FootShade,
	Scan
}

public readonly record struct CockpitGradientStop(float Offset, CockpitColor Color, float Opacity = 1f);

// Degradado lineal (de From a To) o radial (centro From, radio hasta To), en fracciones de la caja
public sealed record CockpitGradient(bool Radial, Vector2 From, Vector2 To, IReadOnlyList<CockpitGradientStop> Stops);

// Relleno de un elemento: un color o un degradado
public readonly record struct CockpitFill(CockpitColor? Color, CockpitGradientId? Gradient)
{
	public static implicit operator CockpitFill(CockpitColor color) => new(color, null);
	public static implicit operator CockpitFill(CockpitGradientId gradient) => new(null, gradient);
}

public enum CockpitElementKind
{
	Polygon,
	Polyline,
	Segments,   // pares de puntos sueltos, como el path "M..L.." del diseño
	Circle,
	Ellipse,
	Rect,
	Text,
	Arc,        // de StartAngle a EndAngle, en grados con Y hacia abajo (crecen en el sentido de las agujas)
	Curve       // Bézier cuadrática: tres puntos en Points
}

public enum CockpitTextAnchor
{
	Start,
	Middle,
	End
}

// Parte viva a la que corresponde un elemento: la rellena el estado del HUD (CockpitHudPieces.Resolve)
public enum CockpitSlot
{
	None,
	ScreenGlow,
	BoostOrb,
	DamageSegment,
	DamageText,
	BoostText,
	SpeedNumber,
	SpeedBar,
	SpeedBarMark,
	BoostTint,
	DamageTint,
	GaugeBack,
	GaugeTop,
	GaugeSpeed,
	GaugeTicks,
	GaugeNumber,
	GaugeNeedle
}

// Primitiva 2D de la cabina, en el espacio del diseño (1280×720, Y hacia abajo). Lógica pura.
public sealed record CockpitElement
{
	public required CockpitElementKind Kind { get; init; }

	public IReadOnlyList<Vector2> Points { get; init; } = Array.Empty<Vector2>();   // polígono, polilínea, pares de Segments
	public Vector2 Center { get; init; }      // círculo y elipse
	public Vector2 Radii { get; init; }       // en un círculo, los dos iguales
	public Vector2 Position { get; init; }    // rectángulo (esquina) y texto (base de la línea)
	public Vector2 Size { get; init; }        // rectángulo
	public float CornerRadius { get; init; }  // rectángulo
	public float StartAngle { get; init; }    // arco y piezas del dial
	public float EndAngle { get; init; }

	public CockpitFill? Fill { get; init; }
	public CockpitColor? Stroke { get; init; }
	public float StrokeWidth { get; init; } = 1f;
	public float Opacity { get; init; } = 1f;
	public float FillOpacity { get; init; } = 1f;
	public float StrokeOpacity { get; init; } = 1f;
	public IReadOnlyList<float> Dash { get; init; } = Array.Empty<float>();
	public Matrix3x2 Transform { get; init; } = Matrix3x2.Identity;

	public string Text { get; init; } = "";
	public float FontSize { get; init; }
	public float LetterSpacing { get; init; }
	public CockpitTextAnchor Anchor { get; init; }

	public CockpitSlot Slot { get; init; }
	public int SlotIndex { get; init; }

	// Número de arista para el parpadeo de daños; -1 si no parpadea (rellenos, textos y partes vivas)
	public int Edge { get; init; } = -1;

	// Valores que pone el estado del HUD a una parte viva, con la transición ya aplicada (RGBA)
	public Vector4? FillRgba { get; init; }
	public Vector4? StrokeRgba { get; init; }
	public float Glow { get; init; }

	public bool IsLive => Slot != CockpitSlot.None;
}

// Una cabina: todos sus elementos en el orden del diseño (lo de delante tapa a lo de detrás)
public sealed record CockpitLayout(IReadOnlyList<CockpitElement> Elements, int EdgeCount);

// Lo menudo que puede ir dibujado de antemano en una textura común (CockpitSpriteAtlas): círculos pequeños de color
// liso y ranuras cortas horizontales, sin nada que cambie durante la carrera. La opacidad del elemento se aplica al
// dibujarlo. Lógica pura.
public readonly record struct CockpitSpriteKey(
	bool IsLine, float Radius, float Length,
	CockpitColor? Fill, float FillOpacity,
	CockpitColor? Stroke, float StrokeWidth, float StrokeOpacity)
{
	public const float MaxRadius = 4f;
	public const float MaxLineLength = 8f;

	public static bool TryFrom(CockpitElement e, out CockpitSpriteKey key)
	{
		key = default;
		if (e.IsLive || e.Edge >= 0)
			return false;

		if (e.Kind == CockpitElementKind.Circle)
		{
			if (e.Radii.X > MaxRadius || e.Fill is not { Color: not null })
				return false;
			key = new CockpitSpriteKey(false, e.Radii.X, 0f, e.Fill.Value.Color, e.FillOpacity,
				e.Stroke, e.Stroke.HasValue ? e.StrokeWidth : 0f, e.StrokeOpacity);
			return true;
		}

		if (e.Kind == CockpitElementKind.Polyline && e.Points.Count == 2 && e.Dash.Count == 0 && e.Stroke.HasValue
			&& e.Points[0].Y == e.Points[1].Y && e.Transform.M12 == 0f)
		{
			float length = MathF.Abs(e.Points[1].X - e.Points[0].X);
			if (length > MaxLineLength)
				return false;
			key = new CockpitSpriteKey(true, 0f, length, null, 1f, e.Stroke, e.StrokeWidth, e.StrokeOpacity);
			return true;
		}

		return false;
	}
}

// Un tramo de elementos seguidos: todos fijos o todos vivos
public sealed record CockpitRun(IReadOnlyList<CockpitElement> Elements, bool IsLive);

// Reparte la cabina en tramos, conservando el orden del diseño. Los fijos van en trozos pequeños: cuando parpadea una
// arista se redibuja solo su trozo, no toda la cabina. Lógica pura.
public static class CockpitRunPlan
{
	public const int MaxFixedElements = 30;

	public static List<CockpitRun> Split(IReadOnlyList<CockpitElement> elements)
	{
		var runs = new List<CockpitRun>();
		List<CockpitElement> current = null;
		bool currentLive = false;
		foreach (CockpitElement element in elements)
		{
			bool full = !element.IsLive && current != null && current.Count >= MaxFixedElements;
			if (current == null || currentLive != element.IsLive || full)
			{
				current = new List<CockpitElement>();
				currentLive = element.IsLive;
				runs.Add(new CockpitRun(current, currentLive));
			}
			current.Add(element);
		}
		return runs;
	}
}

// Lista de elementos que se va construyendo, con los grupos desplazados o girados del diseño. Lógica pura.
public sealed class CockpitCanvas
{
	private readonly List<CockpitElement> _elements = new();
	private readonly Stack<Matrix3x2> _groups = new();
	private Matrix3x2 _transform = Matrix3x2.Identity;
	private int _edges;
	private bool _detail;

	// Grupo: lo que se añada hasta PopGroup se transforma, además, por transform
	public void PushGroup(Matrix3x2 transform)
	{
		_groups.Push(_transform);
		_transform = transform * _transform;
	}

	public void PopGroup()
	{
		_transform = _groups.Pop();
	}

	// Detalle menudo (tornillos, remaches, arañazos, marcas): se dibuja igual, pero no parpadea con los daños
	public void AddDetail(Action draw)
	{
		bool previous = _detail;
		_detail = true;
		draw();
		_detail = previous;
	}

	public void Add(CockpitElement element)
	{
		bool isEdge = element.Stroke.HasValue && !element.IsLive && !_detail;
		_elements.Add(element with { Transform = _transform, Edge = isEdge ? _edges++ : -1 });
	}

	public void Polygon(IEnumerable<Vector2> points, CockpitFill? fill, CockpitColor? stroke = null, float width = 1f, float opacity = 1f, float strokeOpacity = 1f)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Polygon,
			Points = new List<Vector2>(points),
			Fill = fill,
			Stroke = stroke,
			StrokeWidth = width,
			Opacity = opacity,
			StrokeOpacity = strokeOpacity
		});
	}

	public void Line(IEnumerable<Vector2> points, CockpitColor color, float width, float opacity, params float[] dash)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Polyline,
			Points = new List<Vector2>(points),
			Stroke = color,
			StrokeWidth = width,
			Opacity = opacity,
			Dash = dash
		});
	}

	public void Line(Vector2 a, Vector2 b, CockpitColor color, float width, float opacity)
	{
		Line(new[] { a, b }, color, width, opacity);
	}

	public void Segments(IEnumerable<Vector2> pairs, CockpitColor color, float width, float opacity)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Segments,
			Points = new List<Vector2>(pairs),
			Stroke = color,
			StrokeWidth = width,
			Opacity = opacity
		});
	}

	public void Circle(float x, float y, float radius, CockpitFill? fill, CockpitColor? stroke = null, float width = 1f, float opacity = 1f)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Circle,
			Center = new Vector2(x, y),
			Radii = new Vector2(radius, radius),
			Fill = fill,
			Stroke = stroke,
			StrokeWidth = width,
			Opacity = opacity
		});
	}

	public void Ellipse(Vector2 center, Vector2 radii, CockpitFill? fill, float opacity = 1f)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Ellipse,
			Center = center,
			Radii = radii,
			Fill = fill,
			Opacity = opacity
		});
	}

	public void Arc(Vector2 center, float radius, float startAngle, float endAngle, CockpitColor color, float width, float opacity)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Arc,
			Center = center,
			Radii = new Vector2(radius, radius),
			StartAngle = startAngle,
			EndAngle = endAngle,
			Stroke = color,
			StrokeWidth = width,
			Opacity = opacity
		});
	}

	public void Curve(Vector2 p0, Vector2 p1, Vector2 p2, CockpitColor color, float width, float opacity)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Curve,
			Points = new[] { p0, p1, p2 },
			Stroke = color,
			StrokeWidth = width,
			Opacity = opacity
		});
	}

	public void Rect(float x, float y, float width, float height, CockpitFill? fill, CockpitColor? stroke = null, float strokeWidth = 1f, float opacity = 1f, float strokeOpacity = 1f, float cornerRadius = 0f)
	{
		Add(new CockpitElement
		{
			Kind = CockpitElementKind.Rect,
			Position = new Vector2(x, y),
			Size = new Vector2(width, height),
			Fill = fill,
			Stroke = stroke,
			StrokeWidth = strokeWidth,
			Opacity = opacity,
			StrokeOpacity = strokeOpacity,
			CornerRadius = cornerRadius
		});
	}

	public CockpitLayout Finish() => new(_elements, _edges);
}

// Cálculos de dibujo que no dependen de Godot
public static class CockpitGeometry
{
	public static Vector2 Centroid(IReadOnlyList<Vector2> points)
	{
		Vector2 sum = Vector2.Zero;
		foreach (Vector2 p in points)
			sum += p;
		return sum / points.Count;
	}

	// Mueve cada vértice d píxeles hacia el centroide (d negativo: hacia fuera)
	public static Vector2[] Inset(IReadOnlyList<Vector2> points, float d)
	{
		Vector2 center = Centroid(points);
		var result = new Vector2[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			Vector2 toCenter = center - points[i];
			float length = toCenter.Length();
			result[i] = points[i] + toCenter / (length > 0f ? length : 1f) * d;
		}
		return result;
	}

	public static Vector2[] Offset(IReadOnlyList<Vector2> points, float dx, float dy)
	{
		var result = new Vector2[points.Count];
		for (int i = 0; i < points.Count; i++)
			result[i] = points[i] + new Vector2(dx, dy);
		return result;
	}

	// Curva de Bézier cuadrática con n tramos (n + 1 puntos)
	public static Vector2[] Bezier(Vector2 p0, Vector2 p1, Vector2 p2, int n)
	{
		var result = new Vector2[n + 1];
		for (int i = 0; i <= n; i++)
		{
			float t = (float)i / n;
			float a = (1f - t) * (1f - t);
			float b = 2f * (1f - t) * t;
			float c = t * t;
			result[i] = a * p0 + b * p1 + c * p2;
		}
		return result;
	}

	// Puntos de un arco, de startAngle a endAngle (grados, con Y hacia abajo)
	public static Vector2[] ArcPoints(Vector2 center, float radius, float startAngle, float endAngle)
	{
		const float StepDegrees = 3f;
		int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(endAngle - startAngle) / StepDegrees));
		var result = new Vector2[steps + 1];
		for (int i = 0; i <= steps; i++)
		{
			float angle = (startAngle + (endAngle - startAngle) * i / steps) * MathF.PI / 180f;
			result[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
		}
		return result;
	}

	// Contorno de una elipse o un círculo
	public static Vector2[] EllipsePoints(Vector2 center, Vector2 radii)
	{
		const int Sides = 40;
		var result = new Vector2[Sides];
		for (int i = 0; i < Sides; i++)
		{
			float angle = MathF.Tau * i / Sides;
			result[i] = center + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y);
		}
		return result;
	}

	// Espejo respecto al centro de la pantalla de 1280 px
	public static Vector2[] MirrorX(IReadOnlyList<Vector2> points)
	{
		var result = new Vector2[points.Count];
		for (int i = 0; i < points.Count; i++)
			result[i] = new Vector2(1280f - points[i].X, points[i].Y);
		return result;
	}

	// Trama de una pantalla (polígono convexo): una línea de 1 px cada PeriodPx. Devuelve los pares de extremos.
	public const float ScanPeriodPx = 3f;

	public static List<Vector2> ScanLines(IReadOnlyList<Vector2> polygon)
	{
		var pairs = new List<Vector2>();
		if (polygon.Count == 0)
			return pairs;

		float top = float.MaxValue, bottom = float.MinValue;
		foreach (Vector2 p in polygon)
		{
			top = MathF.Min(top, p.Y);
			bottom = MathF.Max(bottom, p.Y);
		}

		for (float y = MathF.Floor(top / ScanPeriodPx) * ScanPeriodPx + 0.5f; y < bottom; y += ScanPeriodPx)
		{
			if (y < top)
				continue;
			float left = float.MaxValue, right = float.MinValue;
			for (int i = 0; i < polygon.Count; i++)
			{
				Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
				if ((a.Y <= y) == (b.Y <= y))
					continue;
				float x = a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X);
				left = MathF.Min(left, x);
				right = MathF.Max(right, x);
			}
			if (right > left)
			{
				pairs.Add(new Vector2(left, y));
				pairs.Add(new Vector2(right, y));
			}
		}
		return pairs;
	}

	// Línea discontinua: pares de extremos de los trozos encendidos
	public static List<Vector2> DashPairs(IReadOnlyList<Vector2> points, float on, float off)
	{
		var pairs = new List<Vector2>();
		bool drawing = true;
		float left = on;
		for (int i = 0; i + 1 < points.Count; i++)
		{
			Vector2 a = points[i], b = points[i + 1];
			float length = Vector2.Distance(a, b);
			float walked = 0f;
			while (length > 0f && walked < length)
			{
				float step = MathF.Min(left, length - walked);
				if (drawing)
				{
					pairs.Add(a + (b - a) * (walked / length));
					pairs.Add(a + (b - a) * ((walked + step) / length));
				}
				walked += step;
				left -= step;
				if (left <= 0f)
				{
					drawing = !drawing;
					left = drawing ? on : off;
				}
			}
		}
		return pairs;
	}

	// Contorno de un rectángulo con las esquinas redondeadas
	public static Vector2[] RoundedRect(Vector2 position, Vector2 size, float radius)
	{
		const int CornerSteps = 3;
		var corners = new[]
		{
			(new Vector2(position.X + size.X - radius, position.Y + radius), -MathF.PI / 2f),
			(new Vector2(position.X + size.X - radius, position.Y + size.Y - radius), 0f),
			(new Vector2(position.X + radius, position.Y + size.Y - radius), MathF.PI / 2f),
			(new Vector2(position.X + radius, position.Y + radius), MathF.PI)
		};
		var result = new Vector2[corners.Length * (CornerSteps + 1)];
		int n = 0;
		foreach ((Vector2 center, float start) in corners)
		{
			for (int i = 0; i <= CornerSteps; i++)
			{
				float angle = start + (MathF.PI / 2f) * i / CornerSteps;
				result[n++] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
			}
		}
		return result;
	}
}
