using System.Collections.Generic;
using Godot;
using Vectory.Core;

namespace Vectory.Menu;

// Miniatura 3D de un circuito para SELECT CIRCUIT: los bordes de la pista (cian), la línea central discontinua
// (azul) y la meta (naranja), sobre una rejilla cuadrada, inclinada y girando despacio. Solo dibuja.
//
// Vive en el mundo 3D del menú, lejos del fondo (FarAway), con su propia cámara: al abrir la pantalla se pone
// esa cámara y al cerrarla vuelve la del fondo. El trazado se escala para medir siempre lo mismo en pantalla y
// se centra por su caja; la malla se rehace solo al cambiar de circuito y el giro sigue con su ángulo.
// Medidas de pantalla sobre 1280×720 (el stretch del proyecto hace el resto).
public partial class CircuitMiniature : Node3D
{
	[Export] public float RotationPeriod = 50f;           // segundos por vuelta completa
	[Export] public float TiltDegrees = 62f;              // inclinación respecto a la vista cenital
	[Export] public float ScreenSize = 700f;              // diámetro de la miniatura en pantalla (px sobre 1280×720)
	[Export] public Vector2 ScreenCenter = new(410f, 430f);   // centro de la miniatura en pantalla (px sobre 1280×720)
	[Export] public float CameraFov = 36f;                // vertical, en grados
	[Export] public int CenterDashes = 70;                // trazos de la línea central en toda la vuelta
	[Export] public int GridCells = 10;                   // celdas de la rejilla por lado
	[Export] public float GridExtent = 1.1f;              // lado de la rejilla respecto al diámetro del trazado

	// Lejos del fondo del menú (nave, rejilla), fuera de la vista de su cámara
	private static readonly Vector3 FarAway = new(0f, -2000f, 0f);

	private const float Radius = 10f;   // radio del trazado escalado, en unidades del mundo

	private Node3D _pivot;
	private MeshInstance3D _track;
	private Camera3D _camera;

	public override void _Ready()
	{
		Position = FarAway;
		Visible = false;

		_pivot = new Node3D { Name = "Pivot" };
		AddChild(_pivot);

		_pivot.AddChild(new MeshInstance3D { Name = "Grid", Mesh = BuildGridMesh() });
		_track = new MeshInstance3D { Name = "Track" };
		_pivot.AddChild(_track);

		// Cámara inclinada TiltDegrees respecto a la vertical, a la distancia con la que el radio del trazado
		// mide ScreenSize / 2 px de los 720 de alto
		float halfFov = Mathf.DegToRad(CameraFov) * 0.5f;
		float distance = Radius * (360f / (ScreenSize * 0.5f)) / Mathf.Tan(halfFov);
		float tilt = Mathf.DegToRad(TiltDegrees);
		_camera = new Camera3D { Name = "Camera3D", Near = 0.1f, Far = 500f };
		AddChild(_camera);
		_camera.Position = new Vector3(0f, Mathf.Cos(tilt), Mathf.Sin(tilt)) * distance;
		_camera.LookAt(GlobalPosition, Vector3.Up);
		ApplyLensShift();
		GetViewport().SizeChanged += ApplyLensShift;
	}

	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= ApplyLensShift;
	}

	public override void _Process(double delta)
	{
		if (Visible && RotationPeriod > 0f)
			_pivot.RotateY(Mathf.Tau * (float)delta / RotationPeriod);
	}

	// Se ve (con su cámara) o se oculta
	public void SetActive(bool active)
	{
		Visible = active;
		if (active)
			_camera.MakeCurrent();
	}

	// Cambia el trazado (sin tocar el giro). Eje vacío o null = sin trazado.
	public void ShowCircuit(IReadOnlyList<System.Numerics.Vector3> centerline, float trackWidth)
	{
		_track.Mesh = centerline != null && centerline.Count >= 3 ? BuildTrackMesh(centerline, trackWidth) : null;
	}

	// Proyección frustum con el encuadre desplazado para que el centro de la miniatura caiga en ScreenCenter
	private void ApplyLensShift()
	{
		Vector2 size = GetViewport().GetVisibleRect().Size;
		float aspect = size.Y > 0f ? size.X / size.Y : 16f / 9f;

		float near = _camera.Near;
		float height = 2f * near * Mathf.Tan(Mathf.DegToRad(CameraFov) * 0.5f);
		float halfWidth = height * 0.5f * aspect;

		float fx = ScreenCenter.X / 1280f;
		float fy = ScreenCenter.Y / 720f;
		var shift = new Vector2(-(fx * 2f - 1f) * halfWidth, (fy * 2f - 1f) * height * 0.5f);
		_camera.SetFrustum(height, shift, near, _camera.Far);
	}

	private ArrayMesh BuildTrackMesh(IReadOnlyList<System.Numerics.Vector3> centerline, float trackWidth)
	{
		int n = centerline.Count;
		float hw = trackWidth * 0.5f;

		// Bordes en metros del circuito
		var points = new Vector3[n];
		var rights = new Vector3[n];
		for (int i = 0; i < n; i++)
		{
			System.Numerics.Vector3 p = centerline[i];
			points[i] = new Vector3(p.X, p.Y, p.Z);
		}
		for (int i = 0; i < n; i++)
		{
			Vector3 tangent = (points[(i + 1) % n] - points[(i - 1 + n) % n]).Normalized();
			rights[i] = tangent.Cross(Vector3.Up).Normalized();
		}

		// Caja de los bordes: centro y radio para escalar
		Vector3 min = Vector3.Inf;
		Vector3 max = -Vector3.Inf;
		for (int i = 0; i < n; i++)
		{
			foreach (Vector3 e in new[] { points[i] - rights[i] * hw, points[i] + rights[i] * hw })
			{
				min = min.Min(e);
				max = max.Max(e);
			}
		}
		var center = new Vector3((min.X + max.X) * 0.5f, points[0].Y, (min.Z + max.Z) * 0.5f);
		float radius = 0f;
		for (int i = 0; i < n; i++)
		{
			radius = Mathf.Max(radius, Flat(points[i] - rights[i] * hw - center).Length());
			radius = Mathf.Max(radius, Flat(points[i] + rights[i] * hw - center).Length());
		}
		float scale = radius > 0f ? Radius / radius : 1f;
		Vector3 Map(Vector3 p) => (p - center) * scale;

		var verts = new List<Vector3>();
		var colors = new List<Color>();
		void Line(Vector3 a, Vector3 b, Color c)
		{
			verts.Add(Map(a));
			verts.Add(Map(b));
			colors.Add(c);
			colors.Add(c);
		}

		float lap = 0f;
		var cumulative = new float[n + 1];
		for (int i = 0; i < n; i++)
		{
			cumulative[i + 1] = cumulative[i] + points[i].DistanceTo(points[(i + 1) % n]);
			lap = cumulative[i + 1];
		}
		float dash = CenterDashes > 0 ? lap / (CenterDashes * 2f) : lap;

		for (int i = 0; i < n; i++)
		{
			int j = (i + 1) % n;
			Line(points[i] - rights[i] * hw, points[j] - rights[j] * hw, Palette.MiniatureEdge);
			Line(points[i] + rights[i] * hw, points[j] + rights[j] * hw, Palette.MiniatureEdge);

			// Línea central: un tramo sí y otro no, por la distancia de su punto medio
			float middle = (cumulative[i] + cumulative[i + 1]) * 0.5f;
			if (Mathf.FloorToInt(middle / dash) % 2 == 0)
				Line(points[i], points[j], Palette.MiniatureCenter);
		}

		Line(points[0] - rights[0] * hw, points[0] + rights[0] * hw, Palette.MiniatureFinish);

		return LinesMesh(verts, colors);
	}

	private ArrayMesh BuildGridMesh()
	{
		var verts = new List<Vector3>();
		var colors = new List<Color>();
		float half = Radius * GridExtent;
		int cells = Mathf.Max(1, GridCells);
		const float Y = -0.05f;   // justo por debajo del trazado

		for (int k = 0; k <= cells; k++)
		{
			float t = -half + 2f * half * k / cells;
			verts.Add(new Vector3(t, Y, -half));
			verts.Add(new Vector3(t, Y, half));
			verts.Add(new Vector3(-half, Y, t));
			verts.Add(new Vector3(half, Y, t));
			for (int c = 0; c < 4; c++)
				colors.Add(Palette.MiniatureGrid);
		}
		return LinesMesh(verts, colors);
	}

	private static ArrayMesh LinesMesh(List<Vector3> verts, List<Color> colors)
	{
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true
		});
		return mesh;
	}

	private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);
}
