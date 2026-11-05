using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Circuit;

// Marco local de la pista en un punto del eje (spec docs/pista-3d.md, 2.3)
public readonly struct FramePoint
{
	public readonly float Distance;       // m sobre el eje en planta desde la meta
	public readonly Vector3 Position;     // punto del eje: el de planta más la altura
	public readonly Vector3 Tangent;      // adelante (T): tangente 3D, con la pendiente
	public readonly Vector3 Normal;       // arriba (N): perpendicular a T, girada el peralte alrededor de T
	public readonly Vector3 Right;        // lado (B): hacia la derecha de la marcha, sobre la superficie
	public readonly Vector3 PlanTangent;  // tangente en planta (horizontal, unitaria)
	public readonly Vector3 PlanRight;    // derecha en planta (horizontal, unitaria)
	public readonly float Slope;          // dh/ds: subida por metro en planta
	public readonly float Curvature;      // d²h/ds² (1/m): negativa en las crestas
	public readonly float Bank;           // peralte en radianes (positivo = el borde derecho más alto)

	public FramePoint(float distance, Vector3 position, Vector3 tangent, Vector3 normal, Vector3 right,
		Vector3 planTangent, Vector3 planRight, float slope, float curvature, float bank)
	{
		Distance = distance;
		Position = position;
		Tangent = tangent;
		Normal = normal;
		Right = right;
		PlanTangent = planTangent;
		PlanRight = planRight;
		Slope = slope;
		Curvature = curvature;
		Bank = bank;
	}

	// Punto de la pista a "lateral" m sobre la superficie (positivo = derecha) y "height" m por encima (a lo largo de N)
	public Vector3 PointAt(float lateral, float height) => Position + Right * lateral + Normal * height;
}

// Punto de la pista más cercano a una posición: marco en ese punto, desplazamiento lateral sobre la
// superficie, altura de la posición sobre ella y altura (Y) del punto de la superficie
public readonly struct SurfacePoint
{
	public readonly FramePoint Frame;
	public readonly float Lateral;
	public readonly float Height;
	public readonly float SurfaceY;

	public SurfacePoint(FramePoint frame, float lateral, float height, float surfaceY)
	{
		Frame = frame;
		Lateral = lateral;
		Height = height;
		SurfaceY = surfaceY;
	}

	public float Distance => Frame.Distance;
}

// Marco de la pista en 3D (spec docs/pista-3d.md, 2.3), sin Godot: eje en planta (el de CenterlineBuilder)
// más los perfiles de altura y de peralte. Lo usan Track (geometría, colisión, recolocación, parrilla),
// la nave (superficie bajo ella) y SELECT CIRCUIT (eje con altura y peralte máximo).
//
// Las distancias son siempre sobre el eje en planta, como el resto del circuito, así que la altura no
// mueve meta, checkpoints ni claves. Entre muestras del eje se interpola (posición lineal, tangente
// normalizada), sin saltos de un tramo a otro. Sin altura ni peralte (IsFlat), el marco es el de siempre
// (N hacia arriba) y el punto más cercano se calcula exactamente igual que antes de la pista 3D.
public sealed class TrackFrame
{
	private static readonly Vector3 Up = Vector3.UnitY;

	private readonly Vector3[] _plan;
	private readonly Vector3[] _planTangents;
	private readonly float[] _cumulative;

	public TrackProfile Elevation { get; }
	public TrackProfile Bank { get; }   // grados

	public int Count => _plan.Length;
	public float LapLength { get; }

	// Eje en planta, sin la altura de los perfiles (empieza en la meta)
	public IReadOnlyList<Vector3> Plan => _plan;

	// Distancia acumulada en planta hasta cada muestra (la última entrada es la vuelta completa)
	public IReadOnlyList<float> Cumulative => _cumulative;

	// Eje con la altura sumada en cada muestra (el de la firma, el perfil y la miniatura)
	public IReadOnlyList<Vector3> Points { get; }

	// Peralte de cada muestra en grados (el de la firma)
	public IReadOnlyList<float> SampleBanks { get; }

	public bool IsFlat => Elevation.IsZero && Bank.IsZero;

	// Peralte máximo en valor absoluto (grados) y altura mínima respecto a la meta (m)
	public float MaxAbsBank => Bank.MaxAbsValue;
	public float MinElevation => MathF.Min(0f, Elevation.MinValue);

	public TrackFrame(IReadOnlyList<Vector3> planCenterline, TrackProfile elevation = null, TrackProfile bank = null)
	{
		if (planCenterline == null)
			throw new ArgumentNullException(nameof(planCenterline));
		if (planCenterline.Count < 3)
			throw new ArgumentException("El eje necesita al menos 3 puntos.", nameof(planCenterline));

		int n = planCenterline.Count;
		_plan = new Vector3[n];
		for (int i = 0; i < n; i++)
			_plan[i] = planCenterline[i];

		_cumulative = CenterlineBuilder.CumulativeDistances(planCenterline);
		LapLength = _cumulative[n];

		_planTangents = new Vector3[n];
		for (int i = 0; i < n; i++)
			_planTangents[i] = Normalize(_plan[(i + 1) % n] - _plan[(i - 1 + n) % n]);

		Elevation = elevation ?? new TrackProfile(null, LapLength);
		Bank = bank ?? new TrackProfile(null, LapLength);

		var points = new Vector3[n];
		var banks = new float[n];
		for (int i = 0; i < n; i++)
		{
			points[i] = _plan[i] + Up * Elevation.ValueAt(_cumulative[i]);
			banks[i] = Bank.ValueAt(_cumulative[i]);
		}
		Points = points;
		SampleBanks = banks;
	}

	// Avisos de la validación (spec 2.2): peralte o pendiente por encima del máximo
	public void CheckLimits(float maxBankDegrees, float maxSlopeDegrees, ICollection<string> warnings)
	{
		if (warnings == null)
			return;
		if (Bank.MaxAbsValue > maxBankDegrees)
			warnings.Add($"banking reaches {Bank.MaxAbsValue:0.#}° (max {maxBankDegrees:0.#}°)");

		float slope = MathF.Atan(Elevation.MaxAbsSlope()) * 180f / MathF.PI;
		if (slope > maxSlopeDegrees)
			warnings.Add($"slope reaches {slope:0.#}° (max {maxSlopeDegrees:0.#}°)");
	}

	// Marco en una distancia cualquiera sobre la vuelta
	public FramePoint At(float distance)
	{
		int n = _plan.Length;
		float s = PosMod(distance, LapLength);
		int i = Segment(s);
		int j = (i + 1) % n;
		float segment = _cumulative[i + 1] - _cumulative[i];
		float t = segment > 0f ? Math.Clamp((s - _cumulative[i]) / segment, 0f, 1f) : 0f;

		Vector3 plan = Vector3.Lerp(_plan[i], _plan[j], t);
		Vector3 planTangent = Normalize(Vector3.Lerp(_planTangents[i], _planTangents[j], t));
		return Build(s, plan, planTangent);
	}

	// Punto de la pista a (distancia, lateral sobre la superficie, altura a lo largo de N)
	public Vector3 PointAt(float distance, float lateral, float height) => At(distance).PointAt(lateral, height);

	// Punto de la superficie justo debajo (en vertical) de la posición (x, z): el que usa la nave.
	// Height es la altura vertical de "y" sobre ese punto (pásale la Y de la posición, o 0 si no importa).
	public SurfacePoint SurfaceUnder(float x, float z, float y = 0f)
	{
		ProjectPlan(x, z, out int i, out float t, out float lateral);
		float s = _cumulative[i] + t * (_cumulative[i + 1] - _cumulative[i]);

		if (IsFlat)
		{
			// Igual que antes de la pista 3D: proyección en planta sobre el tramo más cercano
			int j = (i + 1) % _plan.Length;
			Vector3 plan = Vector3.Lerp(_plan[i], _plan[j], t);
			Vector3 planTangent = Normalize(Vector3.Lerp(_planTangents[i], _planTangents[j], t));
			FramePoint flat = Build(s, plan, planTangent);
			return new SurfacePoint(flat, lateral, y - flat.Position.Y, flat.Position.Y);
		}

		// Con altura y peralte: (x, z) = planta del eje(s) + planta de B · lateral. Unas pocas iteraciones
		// de Newton (el error baja con la curvatura × el lateral en cada una) dan s y lateral sin saltos.
		FramePoint f = At(s);
		for (int iteration = 0; iteration < 5; iteration++)
		{
			SolvePlan(f, x, z, out float ds, out lateral);
			if (MathF.Abs(ds) < 1e-4f)
				break;
			f = At(f.Distance + ds);
		}
		SolvePlan(f, x, z, out _, out lateral);

		float surfaceY = f.Position.Y + f.Right.Y * lateral;
		return new SurfacePoint(f, lateral, y - surfaceY, surfaceY);
	}

	// Punto de la pista más cercano a una posición en 3D: (distancia, lateral, altura a lo largo de N)
	public SurfacePoint Closest(Vector3 position)
	{
		FramePoint f = SurfaceUnder(position.X, position.Z, position.Y).Frame;
		if (!IsFlat)
		{
			for (int iteration = 0; iteration < 8; iteration++)
			{
				float along = Vector3.Dot(position - f.Position, f.Tangent);
				// T avanza sqrt(1 + pendiente²) m por cada metro en planta
				float ds = along / MathF.Sqrt(1f + f.Slope * f.Slope);
				if (MathF.Abs(ds) < 1e-4f)
					break;
				f = At(f.Distance + ds);
			}
		}

		Vector3 rel = position - f.Position;
		float lateral = Vector3.Dot(rel, f.Right);
		float height = Vector3.Dot(rel, f.Normal);
		return new SurfacePoint(f, lateral, height, f.Position.Y + f.Right.Y * lateral);
	}

	// Índice de la muestra del eje más cercana a (x, z), en planta
	public int NearestIndex(float x, float z)
	{
		int best = 0;
		float bestDist = float.MaxValue;
		for (int i = 0; i < _plan.Length; i++)
		{
			float dx = x - _plan[i].X;
			float dz = z - _plan[i].Z;
			float d = dx * dx + dz * dz;
			if (d < bestDist)
			{
				bestDist = d;
				best = i;
			}
		}
		return best;
	}

	// Marco con la posición y la tangente en planta ya interpoladas
	private FramePoint Build(float s, Vector3 plan, Vector3 planTangent)
	{
		Elevation.Evaluate(s, out float height, out float slope, out float curvature);
		float bank = Bank.ValueAt(s) * MathF.PI / 180f;

		Vector3 planRight = Normalize(Vector3.Cross(planTangent, Up));
		Vector3 position = plan + Up * height;

		if (slope == 0f && bank == 0f)
			return new FramePoint(s, position, planTangent, Normalize(Vector3.Cross(planRight, planTangent)), planRight,
				planTangent, planRight, 0f, curvature, 0f);

		Vector3 tangent = Normalize(planTangent + Up * slope);
		Vector3 right0 = Normalize(Vector3.Cross(tangent, Up));
		Vector3 normal0 = Vector3.Cross(right0, tangent);

		float cos = MathF.Cos(bank);
		float sin = MathF.Sin(bank);
		Vector3 right = right0 * cos + normal0 * sin;
		Vector3 normal = normal0 * cos - right0 * sin;
		return new FramePoint(s, position, tangent, normal, right, planTangent, planRight, slope, curvature, bank);
	}

	// Proyección en planta sobre los dos tramos que tocan la muestra más cercana (la de siempre)
	private void ProjectPlan(float x, float z, out int segment, out float t, out float lateral)
	{
		int n = _plan.Length;
		int nearest = NearestIndex(x, z);

		ProjectOnSegment(x, z, (nearest - 1 + n) % n, out float tA, out float distA, out float latA);
		ProjectOnSegment(x, z, nearest, out float tB, out float distB, out float latB);
		if (distB < distA)
		{
			segment = nearest;
			t = tB;
			lateral = latB;
		}
		else
		{
			segment = (nearest - 1 + n) % n;
			t = tA;
			lateral = latA;
		}
	}

	private void ProjectOnSegment(float x, float z, int i, out float t, out float distSq, out float lateral)
	{
		int j = (i + 1) % _plan.Length;
		Vector3 a = _plan[i];
		Vector3 b = _plan[j];

		var ab = new Vector2(b.X - a.X, b.Z - a.Z);
		var ap = new Vector2(x - a.X, z - a.Z);
		float lengthSq = ab.LengthSquared();
		t = lengthSq > 0f ? Math.Clamp(Vector2.Dot(ap, ab) / lengthSq, 0f, 1f) : 0f;

		Vector3 center = Vector3.Lerp(a, b, t);
		Vector3 tangent = Normalize(Vector3.Lerp(_planTangents[i], _planTangents[j], t));
		Vector3 right = Normalize(Vector3.Cross(tangent, Up));
		float ox = x - center.X;
		float oz = z - center.Z;
		distSq = ox * ox + oz * oz;
		lateral = ox * right.X + oz * right.Z;
	}

	// Resuelve en planta (x, z) - eje(s) = tangente en planta · ds + planta de B · lateral
	private static void SolvePlan(in FramePoint f, float x, float z, out float ds, out float lateral)
	{
		float rx = x - f.Position.X;
		float rz = z - f.Position.Z;
		float tx = f.PlanTangent.X;
		float tz = f.PlanTangent.Z;
		float bx = f.Right.X;
		float bz = f.Right.Z;

		float det = tx * bz - tz * bx;
		if (MathF.Abs(det) < 1e-6f)
		{
			ds = 0f;
			lateral = rx * f.PlanRight.X + rz * f.PlanRight.Z;
			return;
		}
		ds = (rx * bz - rz * bx) / det;
		lateral = (tx * rz - tz * rx) / det;
	}

	// Tramo del eje que contiene la distancia s (búsqueda binaria en las distancias acumuladas)
	private int Segment(float s)
	{
		int lo = 0;
		int hi = _plan.Length - 1;
		while (lo < hi)
		{
			int mid = (lo + hi + 1) / 2;
			if (_cumulative[mid] <= s)
				lo = mid;
			else
				hi = mid - 1;
		}
		return lo;
	}

	private static Vector3 Normalize(Vector3 v)
	{
		float length = v.Length();
		return length > 0f ? v / length : Vector3.Zero;
	}

	private static float PosMod(float x, float m)
	{
		if (m <= 0f)
			return x;
		float r = x % m;
		return r < 0f ? r + m : r;
	}
}
