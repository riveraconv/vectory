using System;
using System.Collections.Generic;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Settings;
using Vectory.Ship;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace Vectory.Effects;

// Nodo de los efectos de choque de la carrera (spec docs/efectos-1-choques.md, 10): recibe los contactos de Main,
// llama a CollisionFx y solo dibuja. Chispas en una malla de líneas con color por vértice en suma; brillos de los
// destellos en cuadrados de cara a la cámara en suma, y sus rayos y ondas en otra malla de líneas; humo en cuadrados de
// cara a la cámara con mezcla normal. También los efectos del derrape (spec docs/efectos-2-derrape.md): sus marcas en
// otra malla de líneas en suma. Las mallas se rehacen cada frame en _Process, así la pausa lo congela todo.
// Sin reglas propias.
public partial class CollisionEffects : Node3D
{
	private static readonly CollisionFxSettings D = CollisionFxSettings.Default;

	[ExportGroup("Punto de contacto")]
	[Export] public float BarrierContactAhead { get; set; } = D.BarrierContactAhead;   // m hacia el morro desde el centro de la nave

	[ExportGroup("Chispas")]
	[Export] public int BarrierGrazeRate { get; set; } = (int)D.BarrierGraze.Rate;
	[Export] public int BarrierMediumSparks { get; set; } = D.BarrierMedium.Count;
	[Export] public int BarrierMediumEmbers { get; set; } = D.BarrierMedium.Embers;
	[Export] public int BarrierStrongSparks { get; set; } = D.BarrierStrong.Count;
	[Export] public int BarrierStrongEmbers { get; set; } = D.BarrierStrong.Embers;
	[Export] public float BarrierInherit { get; set; } = D.BarrierInherit;
	[Export] public float SparkGravity { get; set; } = D.Gravity;
	[Export] public float SparkAirDamping { get; set; } = D.AirDamping;
	[Export] public float GroundBounceVertical { get; set; } = D.GroundBounceVertical;
	[Export] public float GroundBounceHorizontal { get; set; } = D.GroundBounceHorizontal;
	[Export] public float BarrierBounce { get; set; } = D.BarrierBounce;
	[Export] public float StreakTime { get; set; } = D.StreakTime;
	[Export] public float StreakMax { get; set; } = D.StreakMax;
	[Export] public int MaxSparks { get; set; } = D.MaxSparks;

	[ExportGroup("Destello")]
	[Export] public float FlashGrazeSize { get; set; } = D.FlashGraze.Size;
	[Export] public float FlashGrazeDuration { get; set; } = D.FlashGraze.Duration;
	[Export] public float FlashMediumSize { get; set; } = D.FlashMedium.Size;
	[Export] public float FlashMediumDuration { get; set; } = D.FlashMedium.Duration;
	[Export] public float FlashStrongSize { get; set; } = D.FlashStrong.Size;
	[Export] public float FlashStrongDuration { get; set; } = D.FlashStrong.Duration;
	[Export] public float GrazeFlashRate { get; set; } = D.GrazeFlashRate;
	[Export] public float RingEndRadius { get; set; } = D.RingEndRadius;
	[Export] public float RingDuration { get; set; } = D.RingDuration;
	[Export] public int MaxFlashes { get; set; } = D.MaxFlashes;

	[ExportGroup("Humo")]
	[Export] public int SmokeMediumPuffs { get; set; } = D.SmokeMedium.Count;
	[Export] public int SmokeStrongPuffs { get; set; } = D.SmokeStrong.Count;
	[Export] public float SmokeBarrierInherit { get; set; } = D.SmokeBarrierInherit;
	[Export] public float SmokeOpacity { get; set; } = D.SmokeOpacity;
	[Export] public int MaxPuffs { get; set; } = D.MaxPuffs;

	[ExportGroup("Derrape")]
	[Export] public float DriftStartAngle { get; set; } = D.Drift.StartAngle;        // grados: hasta aquí, sin efectos
	[Export] public float DriftFullAngle { get; set; } = D.Drift.FullAngle;          // grados: intensidad completa
	[Export] public float DriftTipFraction { get; set; } = D.Drift.TipFraction;      // marcas: fracción del semiancho
	[Export] public float DriftMarkSpacing { get; set; } = D.Drift.MarkSpacing;      // m entre muestras
	[Export] public float DriftMarkLift { get; set; } = D.Drift.MarkLift;            // m sobre la superficie
	[Export] public float DriftMarkLife { get; set; } = D.Drift.MarkLife;            // s
	[Export] public float DriftMarkFadeFrom { get; set; } = D.Drift.MarkFadeFrom;    // fracción de la vida
	[Export] public float DriftMarkBrightness { get; set; } = D.Drift.MarkBrightness;
	[Export] public float DriftMarkMinFactor { get; set; } = D.Drift.MarkMinFactor;  // brillo × (esto + (1 − esto) · d)
	[Export] public int DriftMaxMarkSamples { get; set; } = D.Drift.MaxMarkSamples;  // por nave

	private const int Bucket = 256;       // las mallas de líneas usan arrays de tamaño múltiplo de esto, para no crearlos cada frame
	private const int QuadBucket = 32;    // ídem en cuadrados

	private CollisionFx _fx;
	private DriftMarks _drift;
	private CollisionFxSettings _settings;
	private ICrashGround _ground;
	private TrackFrame _frame;
	private float _barrierOffset;
	private float _quality = -1f;
	private NVector3 _reference;

	private MeshInstance3D _sparkView;
	private MeshInstance3D _lineView;
	private MeshInstance3D _glowView;
	private MeshInstance3D _smokeView;
	private MeshInstance3D _markView;
	private ArrayMesh _sparkMesh;
	private ArrayMesh _lineMesh;
	private ArrayMesh _glowMesh;
	private ArrayMesh _smokeMesh;
	private ArrayMesh _markMesh;
	private readonly Dictionary<int, (Vector3[] V, Color[] C)> _lineArrays = new();
	private readonly Dictionary<int, (Vector3[] V, Color[] C, Vector2[] U)> _quadArrays = new();

	public CollisionFx Fx => _fx;

	public void Setup(Track track)
	{
		_frame = track.Frame;
		_barrierOffset = track.BarrierOffset;
		_ground = new TrackCrashGround(_frame, _barrierOffset);
		_settings = BuildSettings();
		_fx = new CollisionFx(_settings, new Random());
		_drift = new DriftMarks(_settings.Drift);
		ApplyQuality();
		BuildViews();
	}

	// Velocidad de la nave del jugador (planta): los trazos se alargan con la velocidad relativa a ella
	public void SetReference(NVector2 velocity) => _reference = new NVector3(velocity.X, 0f, velocity.Y);

	// El contacto con la barrera de una nave en este paso de física
	public void BarrierContact(int shipId, in BarrierTouch touch, NVector2 forward, NVector2 velocity, float dt)
	{
		if (_fx == null || !touch.Contact)
			return;
		CrashEvent? e = CrashEvent.Barrier(_frame, _barrierOffset, shipId, touch.Side, touch.Level, touch.IsNewImpact,
			touch.AxisDistance, forward, velocity, _settings);
		if (e.HasValue)
			_fx.Crash(e.Value, dt);
	}

	// Un contacto entre dos naves en este paso de física. velocity: la que heredan (ver CrashEvent.Ships)
	public void ShipContact(in Contact.ShipContactEvent e, NVector2 velocity, float dt)
	{
		if (_fx == null)
			return;
		CrashEvent? crash = CrashEvent.Ships(_frame, e.A, e.B, e.Level, e.IsNewImpact, e.Graze, e.Facing, e.Point, velocity, _settings);
		if (crash.HasValue)
			_fx.Crash(crash.Value, dt);
	}

	// El derrape de una nave en este paso de física (spec docs/efectos-2-derrape.md). slipAngle: el de ShipDynamics, en
	// radianes; position: la de la nave; forward: su morro en planta.
	public void DriftShip(int shipId, bool drifting, bool airborne, float slipAngle, NVector3 position, NVector2 forward,
		float halfWidth, DriftContactShape contact)
	{
		if (_drift == null)
			return;
		DriftState state = DriftIntensity.Evaluate(drifting, airborne, slipAngle, _settings.Drift);
		if (!state.Active)
		{
			_drift.Update(shipId, state, default, default);
			return;
		}
		(NVector3 left, NVector3 right) = DriftContact.MarkPoints(_frame, position, forward, halfWidth, contact.TipZ, _settings.Drift);
		_drift.Update(shipId, state, left, right);
	}

	// Recolocación: el trazo de esa nave se corta y las marcas se quedan
	public void CutDrift(int shipId) => _drift?.Cut(shipId);

	public override void _Process(double delta)
	{
		if (_fx == null)
			return;
		ApplyQuality();
		_fx.Update((float)delta, _ground);
		_drift.Advance((float)delta);
		DrawDriftMarks();
		Camera3D camera = GetViewport().GetCamera3D();
		if (camera == null)
			return;
		Basis basis = camera.GlobalBasis;
		Vector3 right = basis.X.Normalized();
		Vector3 up = basis.Y.Normalized();
		DrawSparks();
		DrawFlashLines(right, up);
		DrawGlows(right, up);
		DrawSmoke(right, up);
	}

	private void ApplyQuality()
	{
		float quality = ActiveSettings.Particles;
		if (quality == _quality)
			return;
		_quality = quality;
		_fx.SetQuality(quality);
	}

	private CollisionFxSettings BuildSettings()
	{
		var s = CollisionFxSettings.Default;
		s.BarrierContactAhead = BarrierContactAhead;
		s.BarrierGraze.Rate = BarrierGrazeRate;
		s.BarrierMedium.Count = BarrierMediumSparks;
		s.BarrierMedium.Embers = BarrierMediumEmbers;
		s.BarrierStrong.Count = BarrierStrongSparks;
		s.BarrierStrong.Embers = BarrierStrongEmbers;
		s.BarrierInherit = BarrierInherit;
		s.Gravity = SparkGravity;
		s.AirDamping = SparkAirDamping;
		s.GroundBounceVertical = GroundBounceVertical;
		s.GroundBounceHorizontal = GroundBounceHorizontal;
		s.BarrierBounce = BarrierBounce;
		s.StreakTime = StreakTime;
		s.StreakMax = StreakMax;
		s.MaxSparks = MaxSparks;
		s.FlashGraze = new FlashLevel(FlashGrazeSize, FlashGrazeDuration);
		s.FlashMedium = new FlashLevel(FlashMediumSize, FlashMediumDuration);
		s.FlashStrong = new FlashLevel(FlashStrongSize, FlashStrongDuration);
		s.GrazeFlashRate = GrazeFlashRate;
		s.RingEndRadius = RingEndRadius;
		s.RingDuration = RingDuration;
		s.MaxFlashes = MaxFlashes;
		s.SmokeMedium.Count = SmokeMediumPuffs;
		s.SmokeStrong.Count = SmokeStrongPuffs;
		s.SmokeBarrierInherit = SmokeBarrierInherit;
		s.SmokeOpacity = SmokeOpacity;
		s.MaxPuffs = MaxPuffs;
		s.Drift.StartAngle = DriftStartAngle;
		s.Drift.FullAngle = DriftFullAngle;
		s.Drift.TipFraction = DriftTipFraction;
		s.Drift.MarkSpacing = DriftMarkSpacing;
		s.Drift.MarkLift = DriftMarkLift;
		s.Drift.MarkLife = DriftMarkLife;
		s.Drift.MarkFadeFrom = DriftMarkFadeFrom;
		s.Drift.MarkBrightness = DriftMarkBrightness;
		s.Drift.MarkMinFactor = DriftMarkMinFactor;
		s.Drift.MaxMarkSamples = DriftMaxMarkSamples;
		Color orange = Palette.Orange;
		s.Drift.MarkColor = new NVector3(orange.R, orange.G, orange.B);
		return s;
	}

	private void BuildViews()
	{
		var lines = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		var glow = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = Radial(new[] { 0f, 0.25f, 1f }, new[] { 1f, 0.55f, 0f }),
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		var smoke = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = Radial(new[] { 0f, 0.5f, 1f }, new[] { 0.9f, 0.45f, 0f }),
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		_sparkView = View("Sparks", lines, out _sparkMesh);
		_lineView = View("FlashLines", lines, out _lineMesh);
		_glowView = View("FlashGlows", glow, out _glowMesh);
		_smokeView = View("Smoke", smoke, out _smokeMesh);
		_markView = View("DriftMarks", lines, out _markMesh);
	}

	private MeshInstance3D View(string name, Material material, out ArrayMesh mesh)
	{
		mesh = new ArrayMesh();
		var view = new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false
		};
		AddChild(view);
		return view;
	}

	// Punto suave: blanco con el alfa de la maqueta en cada parada
	private static GradientTexture2D Radial(float[] offsets, float[] alphas)
	{
		var colors = new Color[alphas.Length];
		for (int i = 0; i < alphas.Length; i++)
			colors[i] = new Color(1f, 1f, 1f, alphas[i]);
		return new GradientTexture2D
		{
			Width = 64,
			Height = 64,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
			Gradient = new Gradient { Offsets = offsets, Colors = colors }
		};
	}

	private void DrawSparks()
	{
		SparkField sparks = _fx.Sparks;
		int n = sparks.Count;
		if (!Begin(_sparkView, _sparkMesh, n))
			return;
		(Vector3[] v, Color[] c) = Lines(n * 2);
		for (int i = 0; i < n; i++)
		{
			Spark p = sparks[i];
			NVector3 head = p.Position;
			NVector3 tail = head + SparkField.Tail(p.Velocity, _reference, _settings);
			NVector3 color = sparks.ColorOf(p);
			v[2 * i] = G(head);
			v[2 * i + 1] = G(tail);
			c[2 * i] = new Color(color.X, color.Y, color.Z);
			c[2 * i + 1] = Colors.Black;
		}
		FillLines(v, c, n * 2);
		Commit(_sparkMesh, Mesh.PrimitiveType.Lines, v, c, null);
	}

	// Marcas del derrape: entre muestras seguidas del mismo trazo, una línea por punta, con el brillo de cada muestra
	private void DrawDriftMarks()
	{
		float now = _drift.Now;
		int count = 0;
		foreach (DriftMarkTrail trail in _drift.Trails)
		{
			trail.Prune(now);
			count += Math.Max(0, trail.Count - 1);
		}
		if (!Begin(_markView, _markMesh, count))
			return;
		(Vector3[] v, Color[] c) = Lines(count * 4);
		NVector3 color = _settings.Drift.MarkColor;
		int k = 0;
		foreach (DriftMarkTrail trail in _drift.Trails)
		{
			for (int i = 1; i < trail.Count; i++)
			{
				DriftMarkSample a = trail[i - 1];
				DriftMarkSample b = trail[i];
				if (a.Stroke != b.Stroke)
					continue;
				float wa = trail.BrightnessOf(a, now);
				float wb = trail.BrightnessOf(b, now);
				if (wa <= 0f && wb <= 0f)
					continue;
				Color ca = C(color * wa), cb = C(color * wb);
				v[k] = G(a.Left); c[k++] = ca;
				v[k] = G(b.Left); c[k++] = cb;
				v[k] = G(a.Right); c[k++] = ca;
				v[k] = G(b.Right); c[k++] = cb;
			}
		}
		FillLines(v, c, k);
		Commit(_markMesh, Mesh.PrimitiveType.Lines, v, c, null);
	}

	private void DrawFlashLines(Vector3 right, Vector3 up)
	{
		IReadOnlyList<CrashFlash> flashes = _fx.Flashes.All;
		int count = 0;
		foreach (CrashFlash f in flashes)
			count += (CrashFlashes.Intensity(f) > 0f ? f.Rays.Length : 0) + (_fx.Flashes.RingStrength(f) > 0f ? _settings.RingSegments : 0);
		if (!Begin(_lineView, _lineMesh, count))
			return;
		(Vector3[] v, Color[] c) = Lines(count * 2);
		int k = 0;
		CrashFlashes logic = _fx.Flashes;
		foreach (CrashFlash f in flashes)
		{
			Vector3 center = G(f.Position);
			float intensity = CrashFlashes.Intensity(f);
			if (intensity > 0f)
			{
				float end = logic.RayEnd(f);
				Color ray = C(_settings.RayColor * intensity);
				foreach (float angle in f.Rays)
				{
					Vector3 dir = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
					v[k] = center + dir * _settings.RayStart;
					c[k++] = ray;
					v[k] = center + dir * end;
					c[k++] = Colors.Black;
				}
			}
			float ring = logic.RingStrength(f);
			if (ring > 0f)
			{
				float radius = logic.RingRadius(f);
				Color color = C(_settings.RingColor * ring);
				int segments = _settings.RingSegments;
				for (int j = 0; j < segments; j++)
				{
					float a0 = j * Mathf.Tau / segments;
					float a1 = (j + 1) * Mathf.Tau / segments;
					v[k] = center + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * radius;
					c[k++] = color;
					v[k] = center + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * radius;
					c[k++] = color;
				}
			}
		}
		FillLines(v, c, k);
		Commit(_lineMesh, Mesh.PrimitiveType.Lines, v, c, null);
	}

	private void DrawGlows(Vector3 right, Vector3 up)
	{
		IReadOnlyList<CrashFlash> flashes = _fx.Flashes.All;
		int count = 0;
		foreach (CrashFlash f in flashes)
			if (CrashFlashes.Intensity(f) > 0f)
				count++;
		if (!Begin(_glowView, _glowMesh, count))
			return;
		(Vector3[] v, Color[] c, Vector2[] u) = Quads(count);
		int q = 0;
		foreach (CrashFlash f in flashes)
		{
			float intensity = CrashFlashes.Intensity(f);
			if (intensity <= 0f)
				continue;
			// El tamaño de la tabla es el del sprite entero (la maqueta escala el sprite a ese lado)
			Quad(v, c, u, q++, G(f.Position), right, up, CrashFlashes.GlowSize(f) / 2f, C(_settings.FlashColor * intensity));
		}
		FillQuads(v, c, u, q);
		Commit(_glowMesh, Mesh.PrimitiveType.Triangles, v, c, u);
	}

	private void DrawSmoke(Vector3 right, Vector3 up)
	{
		CrashSmoke smoke = _fx.Smoke;
		int count = 0;
		for (int i = 0; i < smoke.Count; i++)
			if (CrashSmoke.IsVisible(smoke[i]))
				count++;
		if (!Begin(_smokeView, _smokeMesh, count))
			return;
		(Vector3[] v, Color[] c, Vector2[] u) = Quads(count);
		int q = 0;
		NVector3 col = _settings.SmokeColor;
		for (int i = 0; i < smoke.Count; i++)
		{
			SmokePuff p = smoke[i];
			if (!CrashSmoke.IsVisible(p))
				continue;
			Quad(v, c, u, q++, G(p.Position), right, up, CrashSmoke.Radius(p), new Color(col.X, col.Y, col.Z, smoke.Opacity(p)));
		}
		FillQuads(v, c, u, q);
		Commit(_smokeMesh, Mesh.PrimitiveType.Triangles, v, c, u);
	}

	// Sin nada que dibujar, la malla se vacía una vez y se oculta
	private static bool Begin(MeshInstance3D view, ArrayMesh mesh, int count)
	{
		if (count > 0)
		{
			view.Visible = true;
			return true;
		}
		if (view.Visible)
		{
			mesh.ClearSurfaces();
			view.Visible = false;
		}
		return false;
	}

	private (Vector3[] V, Color[] C) Lines(int vertices)
	{
		int size = (vertices + Bucket - 1) / Bucket * Bucket;
		if (!_lineArrays.TryGetValue(size, out var arrays))
			_lineArrays[size] = arrays = (new Vector3[size], new Color[size]);
		return arrays;
	}

	private (Vector3[] V, Color[] C, Vector2[] U) Quads(int quads)
	{
		// Múltiplo de 6 vértices: los triángulos tienen que salir enteros
		int size = (quads + QuadBucket - 1) / QuadBucket * QuadBucket * 6;
		if (!_quadArrays.TryGetValue(size, out var arrays))
			_quadArrays[size] = arrays = (new Vector3[size], new Color[size], new Vector2[size]);
		return arrays;
	}

	// Lo que sobra del array: líneas de largo cero en negro (no suman nada) sobre el primer punto
	private static void FillLines(Vector3[] v, Color[] c, int used)
	{
		Vector3 at = used > 0 ? v[0] : Vector3.Zero;
		for (int i = used; i < v.Length; i++)
		{
			v[i] = at;
			c[i] = Colors.Black;
		}
	}

	// Lo que sobra: triángulos sin área y transparentes sobre el primer punto
	private static void FillQuads(Vector3[] v, Color[] c, Vector2[] u, int usedQuads)
	{
		int used = usedQuads * 6;
		Vector3 at = used > 0 ? v[0] : Vector3.Zero;
		for (int i = used; i < v.Length; i++)
		{
			v[i] = at;
			c[i] = new Color(0f, 0f, 0f, 0f);
			u[i] = Vector2.Zero;
		}
	}

	private static void Quad(Vector3[] v, Color[] c, Vector2[] u, int q, Vector3 center, Vector3 right, Vector3 up, float half, Color color)
	{
		Vector3 r = right * half;
		Vector3 t = up * half;
		int o = q * 6;
		Vector3 a = center - r - t, b = center + r - t, cc = center + r + t, d = center - r + t;
		v[o] = a; u[o] = new Vector2(0f, 1f);
		v[o + 1] = b; u[o + 1] = new Vector2(1f, 1f);
		v[o + 2] = cc; u[o + 2] = new Vector2(1f, 0f);
		v[o + 3] = a; u[o + 3] = new Vector2(0f, 1f);
		v[o + 4] = cc; u[o + 4] = new Vector2(1f, 0f);
		v[o + 5] = d; u[o + 5] = new Vector2(0f, 0f);
		for (int i = 0; i < 6; i++)
			c[o + i] = color;
	}

	private static void Commit(ArrayMesh mesh, Mesh.PrimitiveType primitive, Vector3[] v, Color[] c, Vector2[] u)
	{
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = v;
		arrays[(int)Mesh.ArrayType.Color] = c;
		if (u != null)
			arrays[(int)Mesh.ArrayType.TexUV] = u;
		mesh.ClearSurfaces();
		mesh.AddSurfaceFromArrays(primitive, arrays);
	}

	private static Vector3 G(NVector3 v) => new(v.X, v.Y, v.Z);
	private static Color C(NVector3 v) => new(v.X, v.Y, v.Z);
}
