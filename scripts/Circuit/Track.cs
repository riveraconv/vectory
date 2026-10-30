using Godot;
using System.Collections.Generic;
using Vectory.Core;
using Vectory.Game;

namespace Vectory.Circuit;

// Circuito cerrado de VECTORY, generado por código.
// El trazado sale de un TrackDefinition (archivo .tres por circuito): puntos de control
// medidos sobre la imagen del circuito (en píxeles), que se convierten a metros,
// se suavizan con una spline cerrada y se dibujan en estilo wireframe de neón:
// bordes cian, rejilla azul tenue, una línea de meta (medio hexágono), checkpoints
// (medio rectángulo) y las marcas de la parrilla de salida (corchetes detrás de la meta).
public partial class Track : Node3D
{
	// Circuito que se usa si no se asigna ninguno en el Inspector
	// Circuito por defecto: el primero de la lista de circuitos
	private static string DefaultDefinitionPath => CircuitCatalog.Default;

	// ---- Circuito ----
	[Export] public TrackDefinition Definition;

	// ---- Ajustes comunes a todos los circuitos ----
	[Export] public float TrackY = -2f;          // altura de la pista (la nave flota 2 m por encima)
	[Export] public float RungSpacing = 8f;      // separación de las líneas transversales
	[Export] public float GateHeight = 12f;      // altura de la línea de meta
	[Export] public float CheckpointHeight = 6f; // altura de cada checkpoint

	[ExportGroup("Parrilla")]
	[Export] public int GridSlots = 8;               // participantes de una carrera (una marca por cada uno)
	[Export] public float GridFirstDistance = 12f;   // metros de la meta a la marca 1 (hacia atrás)
	[Export] public float GridRowSpacing = 16f;      // metros entre filas de una misma columna
	[Export] public float GridEvenOffset = 8f;       // cuánto más atrás va cada marca par
	[Export] public float GridLateralPercent = 25f;  // desplazamiento lateral de las columnas (% del ancho de pista)
	[Export] public float GridMarkWidth = 4f;        // ancho de la línea transversal de cada marca
	[Export] public float GridMarkLegLength = 2f;    // largo de las patas de cada marca

	// ---- Datos del circuito actual (salen del TrackDefinition) ----
	public string CircuitId => Definition?.Id ?? "";
	public string DisplayName => Definition?.DisplayName ?? "";
	public bool Clockwise => Definition.Clockwise;
	public float TrackWidth => Definition.TrackWidth;
	public int CheckpointCount => Definition.CheckpointCount;

	// La nave flota esta altura por encima de la pista
	private const float ShipHover = 2f;

	private const float SampleStep = 4f;         // metros entre muestras de la spline

	// Índice de la línea central donde está la meta (el primer punto de control)
	public const int FinishIndex = 0;

	// Línea central de la pista (para vueltas, checkpoints e IA de rivales más adelante)
	public List<Vector3> Centerline { get; private set; }

	// El circuito se ha generado. Es false si no había TrackDefinition o no era válido:
	// en ese caso no hay línea central y no se puede montar la carrera.
	public bool IsBuilt => Centerline != null;

	// Índices de la línea central donde está cada checkpoint, en orden de carrera
	public List<int> CheckpointIndices { get; } = new();

	private readonly List<Vector3> _verts = new();
	private readonly List<Color> _colors = new();

	public override void _Ready()
	{
		// Prioridad: el circuito de la sesión (lo deja el menú), luego el del Inspector y luego el de por defecto
		string sessionPath = GameSession.CircuitPath;
		if (!string.IsNullOrEmpty(sessionPath))
		{
			var sessionDefinition = ResourceLoader.Exists(sessionPath) ? GD.Load<TrackDefinition>(sessionPath) : null;
			if (sessionDefinition != null)
				Definition = sessionDefinition;
			else
				GD.PushError($"Track: no se encuentra el circuito de la sesión '{sessionPath}'; se usa el del Inspector.");
		}

		if (Definition == null)
		{
			Definition = GD.Load<TrackDefinition>(DefaultDefinitionPath);
			if (Definition == null)
			{
				GD.PushError($"Track: no hay TrackDefinition asignado y no se encuentra {DefaultDefinitionPath}");
				return;
			}
		}

		if (!Definition.IsValid(out string error))
		{
			GD.PushError($"Track: el circuito '{Definition.DisplayName}' no es válido: {error}");
			return;
		}

		var control = new List<Vector3>();
		foreach (Vector2 px in Definition.ControlPoints)
		{
			Vector2 m = (px - Definition.ImageCenter) * Definition.MetersPerPixel;
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
		BuildGridMarks();

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
	}

	// ---------------------------------------------------------------
	// Parrilla de salida
	// ---------------------------------------------------------------

	private GridSettings GridSettings => new()
	{
		FirstDistance = GridFirstDistance,
		RowSpacing = GridRowSpacing,
		EvenOffset = GridEvenOffset,
		LateralFraction = GridLateralPercent / 100f
	};

	// Dónde colocar la nave en la marca de la posición 1..GridSlots: centrada en la marca,
	// con el morro (a noseDistance del origen de la nave) justo detrás de la línea transversal,
	// mirando en el sentido de la marcha.
	public void GetGridStart(int position, float noseDistance, out Vector3 shipPosition, out float yaw)
	{
		GetGridMark(position, out Vector3 front, out Vector3 forward, out _);

		const float NoseGap = 0.5f;
		Vector3 p = front - forward * (noseDistance + NoseGap);
		shipPosition = new Vector3(p.X, TrackY + ShipHover, p.Z);
		yaw = Mathf.Atan2(-forward.X, -forward.Z);
	}

	// Salida lanzada (TIME TRIAL): sobre el eje del circuito, centrada en la pista, "distance" metros
	// antes de la meta (medidos sobre la línea central) y mirando en el sentido de la marcha.
	public void GetFlyingStart(float distance, out Vector3 shipPosition, out float yaw)
	{
		PointBehindFinish(distance, out Vector3 point, out Vector3 forward);
		shipPosition = new Vector3(point.X, TrackY + ShipHover, point.Z);
		yaw = Mathf.Atan2(-forward.X, -forward.Z);
	}

	// Centro de la línea transversal de una marca, y las direcciones de la marcha y derecha en ese punto
	private void GetGridMark(int position, out Vector3 front, out Vector3 forward, out Vector3 right)
	{
		GridSlot slot = GridLayout.Slot(position, TrackWidth, GridSettings);
		PointBehindFinish(slot.Distance, out Vector3 point, out forward);
		right = forward.Cross(Vector3.Up).Normalized();
		front = point + right * slot.Lateral;
	}

	// Punto de la línea central a "distance" metros por detrás de la meta (en sentido contrario
	// al de la marcha) y la dirección de la marcha en ese punto. Sigue la curva del circuito.
	private void PointBehindFinish(float distance, out Vector3 point, out Vector3 forward)
	{
		int n = Centerline.Count;
		int i = FinishIndex;
		float remaining = Mathf.Max(0f, distance);

		for (int step = 0; step < n; step++)
		{
			int prev = (i - 1 + n) % n;
			Vector3 a = Centerline[i];
			Vector3 b = Centerline[prev];
			float segment = a.DistanceTo(b);

			if (remaining <= segment)
			{
				point = a.Lerp(b, segment > 0f ? remaining / segment : 0f);
				forward = (a - b).Normalized();
				return;
			}

			remaining -= segment;
			i = prev;
		}

		// Más largo que el circuito entero: se queda en la meta
		point = Centerline[FinishIndex];
		forward = TangentAt(FinishIndex);
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
			BuildArch(index, CheckpointHeight, Palette.Checkpoint, false);
		}
	}

	// Marcas de parrilla: un corchete por posición, con la línea transversal delante (donde va
	// el morro de la nave) y dos patas cortas hacia atrás. Son parte fija del circuito.
	private void BuildGridMarks()
	{
		float hw = GridMarkWidth * 0.5f;

		for (int position = 1; position <= GridSlots; position++)
		{
			GetGridMark(position, out Vector3 front, out Vector3 forward, out Vector3 right);

			Vector3 frontLeft = front - right * hw;
			Vector3 frontRight = front + right * hw;
			Vector3 back = forward * GridMarkLegLength;

			Line(frontLeft, frontRight, Palette.GridMark);
			Line(frontLeft, frontLeft - back, Palette.GridMark);
			Line(frontRight, frontRight - back, Palette.GridMark);
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
