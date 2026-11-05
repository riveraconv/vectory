using System;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Autopilot;

// Trazada del piloto para un circuito y una nave (spec docs/naves-6-equilibrio.md, 4.2): el método iterativo sencillo de
// la trazada de mínima curvatura. Empieza en el centro de la pista y cada punto se mueve a lo largo del lado de la
// superficie hasta que su curvatura es la media de la de sus vecinos, primero con vecinos lejanos y luego con los
// cercanos. Sale la línea de siempre: abre antes de la curva, toca el interior en el vértice y sale abierta.
// El lateral nunca pasa de MaxOffset, así que una nave ancha tiene menos sitio, y menos radio. En las rectas largas la
// línea va por el centro. Lógica pura: no depende de Godot.
public sealed class RacingLine
{
	public const float Spacing = 4f;   // m entre puntos, sobre el eje

	private const int FirstStride = 64;       // puntos: se empieza con vecinos a 256 m y se va dividiendo entre dos
	private const int IterationsPerLevel = 100;
	private const double Probe = 0.1;         // m: lo que se mueve un punto para ver cuánto cambia su curvatura
	private const int CurvatureStride = 3;    // la curvatura final se mide con vecinos a 12 m
	private const int CenterDistance = 40;    // puntos: a más de 160 m de una curva, la línea va por el centro
	private const float StraightCurvature = 1f / 500f;

	private readonly float _lapLength;

	public int Count { get; }
	public float PointSpacing { get; }      // la de verdad, ajustada para cerrar la vuelta
	public float[] Lateral { get; }         // m sobre la superficie, positivo = derecha
	public Vector2[] Points { get; }        // en planta (X, Z de Godot)
	public float[] Curvature { get; }       // 1/m en planta, positiva = a la izquierda

	private RacingLine(float lapLength, int count, float[] lateral, Vector2[] points, float[] curvature)
	{
		_lapLength = lapLength;
		Count = count;
		PointSpacing = lapLength / count;
		Lateral = lateral;
		Points = points;
		Curvature = curvature;
	}

	// Lo más lejos del eje que puede ir la línea: medio ancho − semiancho de colisión − margen
	public static float MaxOffset(float trackWidth, float halfWidth, float barrierMargin) =>
		MathF.Max(0f, trackWidth * 0.5f - halfWidth - barrierMargin);

	public static RacingLine Build(TrackFrame frame, float trackWidth, float halfWidth, float barrierMargin)
	{
		double maxOffset = MaxOffset(trackWidth, halfWidth, barrierMargin);
		int count = PointCount(frame);
		var bases = new Vector2[count];
		var rights = new Vector2[count];
		Sample(frame, count, bases, rights);
		bool[] pinned = FarFromCurves(bases);

		var solver = new Solver(bases, rights, pinned, maxOffset);
		for (int stride = FirstStride; stride >= 1; stride /= 2)
		{
			int iterations = IterationsPerLevel * Math.Max(1, (int)Math.Sqrt(stride));
			for (int iteration = 0; iteration < iterations; iteration++)
				solver.Relax(stride);
			if (stride > 1)
				solver.Interpolate(stride);
		}

		var lateral = new float[count];
		var points = new Vector2[count];
		for (int i = 0; i < count; i++)
		{
			lateral[i] = (float)solver.Lateral[i];
			points[i] = bases[i] + rights[i] * lateral[i];
		}
		return new RacingLine(frame.LapLength, count, lateral, points, MeasureCurvature(points));
	}

	// Línea por el centro del eje, sin suavizar (la de la calibración)
	public static RacingLine Center(TrackFrame frame)
	{
		int count = PointCount(frame);
		var bases = new Vector2[count];
		Sample(frame, count, bases, new Vector2[count]);
		return new RacingLine(frame.LapLength, count, new float[count], bases, MeasureCurvature(bases));
	}

	public int IndexAt(float distance)
	{
		float s = distance % _lapLength;
		if (s < 0f)
			s += _lapLength;
		return Math.Min(Count - 1, (int)(s / PointSpacing));
	}

	public int Wrap(int index) => Wrap(index, Count);

	// Puntos múltiplos del tramo: al recorrer con vecinos lejanos, la vuelta tiene que cerrar en un múltiplo
	private static int PointCount(TrackFrame frame)
	{
		int count = Math.Max(16, (int)MathF.Round(frame.LapLength / Spacing));
		return (count + FirstStride - 1) / FirstStride * FirstStride;
	}

	private static int Wrap(int index, int count) => ((index % count) + count) % count;

	private static void Sample(TrackFrame frame, int count, Vector2[] bases, Vector2[] rights)
	{
		for (int i = 0; i < count; i++)
		{
			FramePoint f = frame.At(frame.LapLength * i / count);
			bases[i] = new Vector2(f.Position.X, f.Position.Z);
			rights[i] = new Vector2(f.Right.X, f.Right.Z);
		}
	}

	// Puntos de recta lejos de cualquier curva: ahí la línea va por el centro. En una recta larga, a la curvatura le da
	// igual el lateral, así que sin esto se quedaría abierta de una curva a otra.
	private static bool[] FarFromCurves(Vector2[] center)
	{
		float[] curvature = MeasureCurvature(center);
		int count = center.Length;
		var far = new bool[count];
		for (int i = 0; i < count; i++)
		{
			far[i] = true;
			for (int k = -CenterDistance; k <= CenterDistance && far[i]; k++)
				far[i] = MathF.Abs(curvature[Wrap(i + k, count)]) < StraightCurvature;
		}
		return far;
	}

	// Curvatura del círculo que pasa por cada punto y sus dos vecinos
	private static float[] MeasureCurvature(Vector2[] points)
	{
		int count = points.Length;
		var curvature = new float[count];
		for (int i = 0; i < count; i++)
		{
			curvature[i] = (float)Menger(
				points[Wrap(i - CurvatureStride, count)].X, points[Wrap(i - CurvatureStride, count)].Y,
				points[i].X, points[i].Y,
				points[Wrap(i + CurvatureStride, count)].X, points[Wrap(i + CurvatureStride, count)].Y);
		}
		return curvature;
	}

	// Curvatura con signo (positiva = a la izquierda) del círculo que pasa por tres puntos
	private static double Menger(double ax, double ay, double bx, double by, double cx, double cy)
	{
		double ux = bx - ax, uy = by - ay;
		double vx = cx - bx, vy = cy - by;
		double wx = cx - ax, wy = cy - ay;
		double denominator = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy) * (wx * wx + wy * wy));
		return denominator > 0.0 ? 2.0 * (vx * uy - vy * ux) / denominator : 0.0;
	}

	// El cálculo, en doble precisión: se comparan curvaturas muy pequeñas con coordenadas de miles de metros
	private sealed class Solver
	{
		private readonly double[] _bx, _by, _rx, _ry;
		private readonly bool[] _pinned;
		private readonly double _max;
		private readonly int _count;

		public double[] Lateral { get; }

		public Solver(Vector2[] bases, Vector2[] rights, bool[] pinned, double max)
		{
			_count = bases.Length;
			_bx = new double[_count];
			_by = new double[_count];
			_rx = new double[_count];
			_ry = new double[_count];
			for (int i = 0; i < _count; i++)
			{
				_bx[i] = bases[i].X;
				_by[i] = bases[i].Y;
				_rx[i] = rights[i].X;
				_ry[i] = rights[i].Y;
			}
			_pinned = pinned;
			_max = max;
			Lateral = new double[_count];
		}

		private double X(int i) => _bx[i] + _rx[i] * Lateral[i];
		private double Y(int i) => _by[i] + _ry[i] * Lateral[i];

		// Una pasada por los puntos múltiplos de stride
		public void Relax(int stride)
		{
			for (int i = 0; i < _count; i += stride)
			{
				if (_pinned[i])
				{
					Lateral[i] = 0.0;
					continue;
				}

				int previous = Wrap(i - stride, _count);
				int next = Wrap(i + stride, _count);
				int beforePrevious = Wrap(i - 2 * stride, _count);
				int afterNext = Wrap(i + 2 * stride, _count);

				double px = X(previous), py = Y(previous);
				double nx = X(next), ny = Y(next);
				double x = X(i), y = Y(i);

				// Curvatura que le toca: la de los dos vecinos, cada una según lo cerca que está
				double before = Menger(X(beforePrevious), Y(beforePrevious), px, py, x, y);
				double after = Menger(x, y, nx, ny, X(afterNext), Y(afterNext));
				double toPrevious = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
				double toNext = Math.Sqrt((x - nx) * (x - nx) + (y - ny) * (y - ny));
				double target = (toNext * before + toPrevious * after) / (toNext + toPrevious);

				// El lateral con el que el punto queda en la recta entre sus vecinos: ahí su curvatura es cero
				double cx = nx - px, cy = ny - py;
				double cross = _rx[i] * cy - _ry[i] * cx;
				if (Math.Abs(cross) < 1e-9)
					continue;
				double straight = -((_bx[i] - px) * cy - (_by[i] - py) * cx) / cross;

				// Cerca de ahí, la curvatura cambia en línea recta con el lateral
				double probeX = _bx[i] + _rx[i] * (straight + Probe);
				double probeY = _by[i] + _ry[i] * (straight + Probe);
				double perMeter = Menger(px, py, probeX, probeY, nx, ny) / Probe;
				if (Math.Abs(perMeter) < 1e-9)
					continue;

				Lateral[i] = Math.Clamp(straight + target / perMeter, -_max, _max);
			}
		}

		// Los puntos de en medio, en línea recta entre los múltiplos de stride, para seguir con vecinos más cercanos
		public void Interpolate(int stride)
		{
			for (int i = 0; i < _count; i += stride)
			{
				double a = Lateral[i];
				double b = Lateral[Wrap(i + stride, _count)];
				for (int k = 1; k < stride; k++)
					Lateral[Wrap(i + k, _count)] = a + (b - a) * k / stride;
			}
		}
	}
}
