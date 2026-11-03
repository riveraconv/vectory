using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Circuit;

// Datos de un circuito para SELECT CIRCUIT (spec selector-circuito 5), calculados a partir de su eje
public sealed class TrackData
{
	public float Length { get; init; }               // m, eje cerrado
	public bool Clockwise { get; init; }             // sentido visto desde arriba (por el área con signo en planta)
	public int Turns { get; init; }
	public float LongestStraight { get; init; }      // m (0 si no hay ninguna recta)
	public float TrackWidth { get; init; }           // m
	public int Sectors { get; init; }                // checkpoints + 1
	public int GridSlots { get; init; }
	public float MaxBanking { get; init; }           // grados, valor absoluto (lo da el marco de la pista)

	// Perfil de elevación: alturas (m, respecto a la meta) de la salida (0) a la meta (último), repartidas por distancia
	public float[] Profile { get; init; } = Array.Empty<float>();

	// Escala vertical del perfil: de -ProfileScale a +ProfileScale (m)
	public float ProfileScale { get; init; }

	// Posición de cada checkpoint sobre la vuelta, de 0 (meta) a 1 (vuelta completa)
	public float[] CheckpointFractions { get; init; } = Array.Empty<float>();
}

// Cálculo puro de los datos de un trazado (sin Godot). Curvas y rectas en planta (sin la altura):
//  - curvatura de cada punto suavizada sobre una ventana de SmoothingWindow metros;
//  - punto de curva si su radio es menor que CurveRadius; de recta si es mayor o igual;
//  - curva = tramo de puntos de curva con giro del mismo sentido; cuenta si gira MinTurnDegrees o más;
//    dos tramos del mismo sentido separados por menos de MergeGap metros de recta son la misma curva;
//  - recta = tramo de puntos de recta; el que cruza la meta se une con el del principio (circuito cerrado).
public static class TrackAnalysis
{
	// Umbrales de curvas y rectas (spec 5.4): se ajustan aquí si el número de curvas no convence
	public const float CurveRadius = 350f;
	public const float MinTurnDegrees = 15f;
	public const float MergeGap = 30f;
	public const float SmoothingWindow = 20f;

	public const int DefaultProfileSamples = 120;
	public const float DefaultMinProfileScale = 20f;

	private const float ResampleStep = 1f;   // metros entre puntos al remuestrear el eje para las curvas
	private const float ProfileScaleStep = 10f;

	public static TrackData Analyze(IReadOnlyList<Vector3> centerline, float trackWidth,
		IReadOnlyList<float> checkpointDistances, int gridSlots,
		int profileSamples = DefaultProfileSamples, float minProfileScale = DefaultMinProfileScale, float maxBanking = 0f)
	{
		if (centerline == null)
			throw new ArgumentNullException(nameof(centerline));
		if (centerline.Count < 3)
			throw new ArgumentException("El eje necesita al menos 3 puntos.", nameof(centerline));

		float[] cumulative = CenterlineBuilder.CumulativeDistances(centerline);
		float length = cumulative[centerline.Count];

		CountTurnsAndStraights(centerline, cumulative, out int turns, out float longestStraight);
		float[] profile = Profile(centerline, cumulative, Math.Max(2, profileSamples));

		int checkpoints = checkpointDistances?.Count ?? 0;
		var fractions = new float[checkpoints];
		for (int i = 0; i < checkpoints; i++)
			fractions[i] = length > 0f ? checkpointDistances[i] / length : 0f;

		return new TrackData
		{
			Length = length,
			Clockwise = SignedArea(centerline) >= 0f,
			Turns = turns,
			LongestStraight = longestStraight,
			TrackWidth = trackWidth,
			Sectors = checkpoints + 1,
			GridSlots = gridSlots,
			MaxBanking = maxBanking,
			Profile = profile,
			ProfileScale = ProfileScale(centerline, minProfileScale),
			CheckpointFractions = fractions
		};
	}

	// Área con signo en planta (X, Z). Con Z hacia abajo en la vista cenital (como en la imagen del circuito),
	// positiva = sentido horario.
	public static float SignedArea(IReadOnlyList<Vector3> centerline)
	{
		double sum = 0.0;
		int n = centerline.Count;
		for (int i = 0; i < n; i++)
		{
			Vector3 a = centerline[i];
			Vector3 b = centerline[(i + 1) % n];
			sum += (double)a.X * b.Z - (double)b.X * a.Z;
		}
		return (float)(sum * 0.5);
	}

	// Escala del perfil: la mayor altura absoluta (respecto a la meta) redondeada hacia arriba a 10 m, como mínimo minScale
	public static float ProfileScale(IReadOnlyList<Vector3> centerline, float minScale)
	{
		float baseY = centerline[0].Y;
		float max = 0f;
		foreach (Vector3 p in centerline)
			max = MathF.Max(max, MathF.Abs(p.Y - baseY));

		float rounded = MathF.Ceiling(max / ProfileScaleStep - 1e-4f) * ProfileScaleStep;
		return MathF.Max(minScale, rounded);
	}

	// Altura respecto a la meta en "samples" puntos repartidos por distancia, de la salida (0) a la meta (vuelta completa)
	private static float[] Profile(IReadOnlyList<Vector3> centerline, float[] cumulative, int samples)
	{
		int n = centerline.Count;
		float length = cumulative[n];
		float baseY = centerline[0].Y;
		var profile = new float[samples];

		int i = 0;
		for (int k = 0; k < samples; k++)
		{
			float d = length * k / (samples - 1);
			while (i < n - 1 && cumulative[i + 1] < d)
				i++;

			float segment = cumulative[i + 1] - cumulative[i];
			float t = segment > 0f ? Math.Clamp((d - cumulative[i]) / segment, 0f, 1f) : 0f;
			float y = centerline[i].Y + (centerline[(i + 1) % n].Y - centerline[i].Y) * t;
			profile[k] = y - baseY;
		}
		return profile;
	}

	// ---------------------------------------------------------------
	// Curvas y rectas
	// ---------------------------------------------------------------

	// Curvas por el signo del giro en planta (Plus / Minus): solo importa que sean sentidos distintos
	private enum Kind { Straight, Plus, Minus }

	private sealed class Run
	{
		public Kind Kind;
		public float Length;   // m
		public float Turn;     // radianes, con signo
	}

	private static void CountTurnsAndStraights(IReadOnlyList<Vector3> centerline, float[] cumulative,
		out int turns, out float longestStraight)
	{
		float length = cumulative[centerline.Count];
		int count = Math.Max(8, (int)MathF.Round(length / ResampleStep));
		float ds = length / count;

		// Puntos repartidos por igual sobre el eje, en planta
		var points = new Vector2[count];
		int seg = 0;
		int n = centerline.Count;
		for (int k = 0; k < count; k++)
		{
			float d = ds * k;
			while (seg < n - 1 && cumulative[seg + 1] < d)
				seg++;
			float segment = cumulative[seg + 1] - cumulative[seg];
			float t = segment > 0f ? Math.Clamp((d - cumulative[seg]) / segment, 0f, 1f) : 0f;
			Vector3 a = centerline[seg];
			Vector3 b = centerline[(seg + 1) % n];
			points[k] = new Vector2(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
		}

		// Rumbo de cada tramo y giro local en cada punto (entre el tramo anterior y el siguiente)
		var heading = new float[count];
		for (int k = 0; k < count; k++)
		{
			Vector2 dir = points[(k + 1) % count] - points[k];
			heading[k] = MathF.Atan2(dir.Y, dir.X);
		}
		var localTurn = new float[count];
		for (int k = 0; k < count; k++)
			localTurn[k] = WrapAngle(heading[k] - heading[(k - 1 + count) % count]);

		// Suma acumulada del giro (con vuelta) para la curvatura suavizada
		var prefix = new double[count + 1];
		for (int k = 0; k < count; k++)
			prefix[k + 1] = prefix[k] + localTurn[k];

		// Giro de los puntos from+1..to (to > from; from puede ser negativo y to pasar de count: el eje es cerrado)
		double TurnBetween(int from, int to)
		{
			int span = to - from;
			int f = ((from % count) + count) % count;
			int rest = span % count;
			double total = (span / count) * prefix[count];
			int end = f + rest;
			if (end <= count)
				total += prefix[end] - prefix[f];
			else
				total += (prefix[count] - prefix[f]) + prefix[end - count];
			return total;
		}

		int half = Math.Max(1, (int)MathF.Round(SmoothingWindow * 0.5f / ds));
		float curvatureLimit = 1f / CurveRadius;
		var kinds = new Kind[count];
		for (int k = 0; k < count; k++)
		{
			double turn = TurnBetween(k - half, k + half);
			double curvature = turn / (2 * half * ds);
			if (Math.Abs(curvature) < curvatureLimit)
				kinds[k] = Kind.Straight;
			else
				kinds[k] = curvature > 0 ? Kind.Plus : Kind.Minus;
		}

		List<Run> runs = BuildRuns(kinds, localTurn, ds);
		MergeCloseCurves(runs);

		float minTurn = MinTurnDegrees * MathF.PI / 180f;
		turns = 0;
		longestStraight = 0f;
		foreach (Run run in runs)
		{
			if (run.Kind == Kind.Straight)
				longestStraight = MathF.Max(longestStraight, run.Length);
			else if (MathF.Abs(run.Turn) >= minTurn - 1e-4f)
				turns++;
		}
	}

	// Tramos seguidos del mismo tipo, en orden. El circuito es cerrado: se empieza en un cambio de tipo,
	// así el tramo que cruza la meta queda entero.
	private static List<Run> BuildRuns(Kind[] kinds, float[] localTurn, float ds)
	{
		int count = kinds.Length;
		int start = -1;
		for (int k = 0; k < count; k++)
		{
			if (kinds[k] != kinds[(k - 1 + count) % count])
			{
				start = k;
				break;
			}
		}

		var runs = new List<Run>();
		if (start < 0)
		{
			// Todo del mismo tipo (p. ej. un círculo)
			float total = 0f;
			foreach (float t in localTurn)
				total += t;
			runs.Add(new Run { Kind = kinds[0], Length = ds * count, Turn = total });
			return runs;
		}

		Run current = null;
		for (int i = 0; i < count; i++)
		{
			int k = (start + i) % count;
			if (current == null || current.Kind != kinds[k])
			{
				current = new Run { Kind = kinds[k] };
				runs.Add(current);
			}
			current.Length += ds;
			current.Turn += localTurn[k];
		}
		return runs;
	}

	// Une curva + recta corta (< MergeGap) + curva del mismo sentido en una sola curva (con vuelta)
	private static void MergeCloseCurves(List<Run> runs)
	{
		bool merged = true;
		while (merged && runs.Count >= 3)
		{
			merged = false;
			for (int i = 0; i < runs.Count; i++)
			{
				Run gap = runs[i];
				if (gap.Kind != Kind.Straight || gap.Length >= MergeGap)
					continue;

				int prev = (i - 1 + runs.Count) % runs.Count;
				int next = (i + 1) % runs.Count;
				if (prev == next || runs[prev].Kind == Kind.Straight || runs[prev].Kind != runs[next].Kind)
					continue;

				var joined = new Run
				{
					Kind = runs[prev].Kind,
					Length = runs[prev].Length + gap.Length + runs[next].Length,
					Turn = runs[prev].Turn + gap.Turn + runs[next].Turn
				};

				// Se quitan los tres (de mayor a menor índice) y la curva unida va en su sitio. Si el trío da
				// la vuelta a la lista, va al principio: la lista es cíclica y los vecinos siguen siendo los mismos.
				var remove = new List<int> { prev, i, next };
				remove.Sort();
				for (int r = remove.Count - 1; r >= 0; r--)
					runs.RemoveAt(remove[r]);
				runs.Insert(prev < next ? prev : 0, joined);

				merged = true;
				break;
			}
		}
	}

	private static float WrapAngle(float angle)
	{
		while (angle > MathF.PI)
			angle -= 2f * MathF.PI;
		while (angle < -MathF.PI)
			angle += 2f * MathF.PI;
		return angle;
	}
}
