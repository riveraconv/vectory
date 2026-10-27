using Godot;
using System.Collections.Generic;
using Vectory.Core;

namespace Vectory.Circuit;

// Circuito cerrado de VECTORY, generado por código.
// El trazado sale de puntos de control medidos sobre la imagen del circuito
// (en píxeles). Se convierten a metros, se suavizan con una spline cerrada
// y se dibuja en estilo wireframe de neón: bordes cian, rejilla azul tenue,
// una línea de meta (medio hexágono) y checkpoints (medio rectángulo).
public partial class Track : Node3D
{
	// ---- Ajustes ----
	[Export] public bool Clockwise = true;       // sentido de carrera (ver nota en el chat)
	[Export] public float TrackWidth = 28f;      // ancho de la pista en metros
	[Export] public float TrackY = -2f;          // altura de la pista (la nave flota 2 m por encima)
	[Export] public float RungSpacing = 8f;      // separación de las líneas transversales
	[Export] public float GateHeight = 12f;      // altura de la línea de meta
	[Export] public int CheckpointCount = 5;     // número de checkpoints repartidos por el circuito
	[Export] public float CheckpointHeight = 6f; // altura de cada checkpoint

	private const float MetersPerPixel = 1.4f;   // con 1.4 sale un circuito de ~4.2 km
	private const float SampleStep = 4f;         // metros entre muestras de la spline
	private static readonly Vector2 ImageCenter = new Vector2(744f, 444f);

	// Cian de la pista al 50 % de intensidad: más tenue que la meta
	private static readonly Color CheckpointColor = new Color("1B737F");

	// Índice de la línea central donde está la meta
	public const int FinishIndex = 0;

	// Puntos de control (píxeles de la imagen), en sentido horario visto desde arriba,
	// empezando en la línea de salida y avanzando hacia el oeste por la recta principal.
	private static readonly Vector2[] ControlPixels =
	{
		new(780, 684), new(680, 684), new(580, 684), new(480, 684), new(385, 684),
		new(320, 672), new(270, 640), new(225, 590), new(175, 545), new(135, 500),
		new(118, 452), new(135, 402), new(175, 368), new(235, 345), new(320, 322),
		new(410, 298), new(490, 262), new(555, 220), new(625, 204), new(700, 215),
		new(790, 240), new(880, 262), new(970, 274), new(1045, 282), new(1095, 305),
		new(1118, 340), new(1095, 372), new(1058, 398), new(1048, 435), new(1068, 470),
		new(1110, 488), new(1170, 497), new(1235, 510), new(1295, 535), new(1345, 575),
		new(1368, 625), new(1355, 665), new(1320, 684), new(1250, 684), new(1150, 684),
		new(1050, 684), new(950, 684), new(870, 684)
	};

	// Línea central de la pista (para vueltas, checkpoints e IA de rivales más adelante)
	public List<Vector3> Centerline { get; private set; }

	// Índices de la línea central donde está cada checkpoint, en orden de carrera
	public List<int> CheckpointIndices { get; } = new();

	// Dónde y hacia dónde colocar la nave al empezar (30 m antes de la meta)
	public Vector3 StartPosition { get; private set; }
	public float StartYaw { get; private set; }

	private readonly List<Vector3> _verts = new();
	private readonly List<Color> _colors = new();

	public override void _Ready()
	{
		var control = new List<Vector3>();
		foreach (Vector2 px in ControlPixels)
		{
			Vector2 m = (px - ImageCenter) * MetersPerPixel;
			control.Add(new Vector3(m.X, TrackY, m.Y));
		}

		if (!Clockwise)
		{
			// Mismo circuito en sentido contrario, manteniendo el primer punto como salida
			Vector3 first = control[0];
			control.RemoveAt(0);
			control.Reverse();
			control.Insert(0, first);
		}

		Centerline = SampleSpline(control);

		BuildTrackLines();
		BuildFinishLine();
		BuildCheckpoints();

		var mesh = new ArrayMesh();
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = _verts.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true
		});

		AddChild(new MeshInstance3D { Name = "TrackMesh", Mesh = mesh });

		// Posición de salida
		Vector3 forward = TangentAt(0);
		Vector3 start = Centerline[0] - forward * 30f;
		StartPosition = new Vector3(start.X, TrackY + 2f, start.Z);
		StartYaw = Mathf.Atan2(-forward.X, -forward.Z);
	}

	// ---------------------------------------------------------------
	// Spline cerrada (Hermite con tangentes escaladas por la longitud del tramo)
	// ---------------------------------------------------------------

	private static List<Vector3> SampleSpline(List<Vector3> pts)
	{
		int n = pts.Count;
		var dirs = new Vector3[n];
		for (int i = 0; i < n; i++)
			dirs[i] = (pts[(i + 1) % n] - pts[(i - 1 + n) % n]).Normalized();

		var result = new List<Vector3>();
		for (int i = 0; i < n; i++)
		{
			Vector3 p0 = pts[i];
			Vector3 p1 = pts[(i + 1) % n];
			float d = p0.DistanceTo(p1);
			Vector3 m0 = dirs[i] * d;
			Vector3 m1 = dirs[(i + 1) % n] * d;

			int steps = Mathf.Max(2, Mathf.CeilToInt(d / SampleStep));
			for (int s = 0; s < steps; s++)
			{
				float t = s / (float)steps;
				float t2 = t * t;
				float t3 = t2 * t;
				Vector3 p =
					(2f * t3 - 3f * t2 + 1f) * p0 +
					(t3 - 2f * t2 + t) * m0 +
					(-2f * t3 + 3f * t2) * p1 +
					(t3 - t2) * m1;
				result.Add(p);
			}
		}
		return result;
	}

	private Vector3 TangentAt(int i)
	{
		int n = Centerline.Count;
		return (Centerline[(i + 1) % n] - Centerline[(i - 1 + n) % n]).Normalized();
	}

	public Vector3 RightAt(int i)
	{
		return TangentAt(i).Cross(Vector3.Up).Normalized();
	}

	// Índice de la muestra de la línea central más cercana a una posición (en el plano XZ).
	// Supone que el nodo Track está en el origen sin rotar ni escalar.
	public int NearestIndex(Vector3 position)
	{
		int best = 0;
		float bestDist = float.MaxValue;
		for (int i = 0; i < Centerline.Count; i++)
		{
			float dx = position.X - Centerline[i].X;
			float dz = position.Z - Centerline[i].Z;
			float d = dx * dx + dz * dz;
			if (d < bestDist)
			{
				bestDist = d;
				best = i;
			}
		}
		return best;
	}

	// ---------------------------------------------------------------
	// Geometría
	// ---------------------------------------------------------------

	private void Line(Vector3 a, Vector3 b, Color c)
	{
		_verts.Add(a);
		_colors.Add(c);
		_verts.Add(b);
		_colors.Add(c);
	}

	private void BuildTrackLines()
	{
		int n = Centerline.Count;
		float hw = TrackWidth * 0.5f;
		float distSinceRung = 0f;

		for (int i = 0; i < n; i++)
		{
			int j = (i + 1) % n;
			Vector3 ri = RightAt(i);
			Vector3 rj = RightAt(j);
			Vector3 ci = Centerline[i];
			Vector3 cj = Centerline[j];

			// Bordes brillantes
			Line(ci - ri * hw, cj - rj * hw, Palette.TrackEdge);
			Line(ci + ri * hw, cj + rj * hw, Palette.TrackEdge);

			// Dos líneas interiores tenues (dan el aspecto de rejilla de la imagen)
			Line(ci - ri * (hw / 3f), cj - rj * (hw / 3f), Palette.BlueDim);
			Line(ci + ri * (hw / 3f), cj + rj * (hw / 3f), Palette.BlueDim);

			// Líneas transversales cada RungSpacing metros
			distSinceRung += ci.DistanceTo(cj);
			if (distSinceRung >= RungSpacing)
			{
				distSinceRung = 0f;
				Line(ci - ri * hw, ci + ri * hw, Palette.BlueDim);
			}
		}
	}

	// Línea de meta: medio hexágono (mitad superior) en la muestra FinishIndex de la línea central.
	private void BuildFinishLine()
	{
		BuildArch(FinishIndex, GateHeight, Palette.Orange, true);
	}

	// Checkpoints: medio rectángulo, repartidos a distancias iguales a lo largo del circuito
	// (sin contar la meta, que está en FinishIndex). Sus índices quedan guardados, en orden
	// de carrera, en CheckpointIndices.
	private void BuildCheckpoints()
	{
		CheckpointIndices.Clear();

		if (CheckpointCount <= 0)
			return;

		int n = Centerline.Count;

		// Distancia acumulada a lo largo de la línea central
		var cumulative = new float[n + 1];
		for (int i = 0; i < n; i++)
			cumulative[i + 1] = cumulative[i] + Centerline[i].DistanceTo(Centerline[(i + 1) % n]);
		float total = cumulative[n];

		int index = 0;
		for (int k = 1; k <= CheckpointCount; k++)
		{
			float target = total * k / (CheckpointCount + 1);
			while (index < n - 1 && cumulative[index + 1] < target)
				index++;

			CheckpointIndices.Add(index);
			BuildArch(index, CheckpointHeight, CheckpointColor, false);
		}
	}

	// Arco en el plano perpendicular a la pista, apoyado en el suelo y cerrado con una línea base.
	// hexagon = true: mitad superior de un hexágono; false: mitad superior de un rectángulo.
	private void BuildArch(int index, float height, Color color, bool hexagon)
	{
		Vector3 c = Centerline[index];
		Vector3 r = RightAt(index);
		float hw = TrackWidth * 0.5f;
		Vector3 up = Vector3.Up * height;

		Vector3 leftBase = c - r * hw;
		Vector3 rightBase = c + r * hw;

		Vector3 leftTop;
		Vector3 rightTop;
		if (hexagon)
		{
			// Los vértices superiores quedan a la mitad del ancho
			leftTop = c - r * (hw * 0.5f) + up;
			rightTop = c + r * (hw * 0.5f) + up;
		}
		else
		{
			leftTop = leftBase + up;
			rightTop = rightBase + up;
		}

		Line(leftBase, leftTop, color);
		Line(leftTop, rightTop, color);
		Line(rightTop, rightBase, color);
		Line(rightBase, leftBase, color);
	}
}
