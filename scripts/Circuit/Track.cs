using Godot;
using System.Collections.Generic;
using Vectory.Core;
using Vectory.Game;
using Vectory.Ghost;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Circuit;

// Circuito cerrado de VECTORY, generado por código.
// El trazado sale de un TrackDefinition (archivo .tres por circuito): puntos de control
// medidos sobre la imagen del circuito (en píxeles), que se convierten a metros,
// se suavizan con una spline cerrada y se dibujan en estilo wireframe de neón: bordes, carriles,
// línea central y transversales cada 10 m en el suelo, y las barreras. Meta, checkpoints, semáforo, parrilla, túneles,
// avisos de curva y señales de cresta (spec docs/elementos-circuito.md) los monta TrackElements, un hijo
// de este nodo, con los parámetros de abajo.
// Pista 3D (spec docs/pista-3d.md): con las claves de altura y peralte del TrackDefinition, todo se construye
// sobre el marco local de la pista (TrackFrame: adelante, arriba y lado), así que sube, baja y se ladea con ella.
public partial class Track : Node3D
{
	// Circuito que se usa si no se asigna ninguno en el Inspector
	// Circuito por defecto: el primero de la lista de circuitos
	private static string DefaultDefinitionPath => CircuitCatalog.Default;

	// ---- Circuito ----
	[Export] public TrackDefinition Definition;

	// ---- Ajustes comunes a todos los circuitos ----
	[Export] public float TrackY = DefaultTrackY; // altura de la pista (la nave flota 2 m por encima)

	[ExportGroup("Suelo")]
	[Export] public float RungSpacing = 10f;      // separación de las transversales, sobre el eje
	[Export(PropertyHint.Range, "0,1,0.01")] public float RungOpacity = 0.65f;
	[Export] public float LaneDashLength = 5f;    // trazos de los carriles discontinuos
	[Export] public float LaneDashPeriod = 10f;   // un trazo cada tantos metros, sobre el eje
	[Export(PropertyHint.Range, "0,1,0.01")] public float LaneOpacity = 0.85f;   // carriles y línea central

	[ExportGroup("Meta")]
	[Export] public float FinishHeight = 12f;         // altura de los medios hexágonos
	[Export] public float FinishSpacing = 4f;         // separación entre los dos arcos, sobre el eje
	[Export] public float FinishClearance = 2f;       // holgura de los pilares por fuera del borde de la pista
	[Export] public float FinishFlashDuration = 0.6f; // segundos del destello al cruzar

	[ExportGroup("Checkpoints")]
	[Export] public float CheckpointHeight = 9f;
	[Export] public float CheckpointSpacing = 3f;
	[Export] public float CheckpointClearance = 2f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float CheckpointIdleBrightness = 0.5f;
	[Export] public float CheckpointFlashDuration = 0.45f;
	[Export] public float CheckpointPanelTime = 2f;    // segundos con el tiempo del sector visible en el panel

	[ExportGroup("Parrilla")]
	[Export] public int GridSlots = DefaultGridSlots;   // participantes de una carrera (una marca por cada uno)
	[Export] public float GridFirstDistance = 12f;   // metros de la meta a la marca 1 (hacia atrás)
	[Export] public float GridRowSpacing = 16f;      // metros entre filas de una misma columna
	[Export] public float GridEvenOffset = 8f;       // cuánto más atrás va cada marca par
	[Export] public float GridLateralPercent = 25f;  // desplazamiento lateral de las columnas (% del ancho de pista)
	[Export] public float GridFrameWidth = 3.6f;     // marco de luz de cada puesto (m)
	[Export] public float GridFrameLength = 6.4f;
	[Export] public float GridFadeTime = 0.5f;       // segundos en apagarse desde el GO

	[ExportGroup("Semáforo")]
	[Export] public float StartLightsFlashDuration = 0.7f;   // destello cian de la salida

	[ExportGroup("Túneles")]
	[Export] public float TunnelHeight = 11f;
	[Export] public float TunnelClearance = 2f;
	[Export] public float TunnelRingSpacing = 8f;
	[Export] public float TunnelPulseSpeed = 120f;   // m/s
	[Export] public int TunnelPulseCount = 2;

	[ExportGroup("Avisos de curva")]
	[Export] public float ChevronSpacing = 9f;
	[Export] public float ChevronWaveFrequency = 2.2f;   // Hz

	[ExportGroup("Cresta")]
	[Export] public float[] CrestBeaconDistances = { 150f, 100f, 50f };   // m antes de la cresta
	[Export] public float CrestBarrierHalfLength = 45f;                   // barrera naranja: m antes y después
	[Export] public float CrestBarrierRaise = 0.35f;                      // segunda línea de remate, por encima de la barrera

	[ExportGroup("Barrera")]
	[Export] public float BarrierGap = 0.5f;          // separación de la barrera respecto al borde de la pista
	[Export] public float BarrierHeight = 3f;         // altura de la barrera (remate)
	[Export] public float BarrierBottomBand = 0.4f;   // alto de la franja inferior
	[Export] public float BarrierTopBand = 0.6f;      // alto de la franja superior
	[Export] public float BarrierMiddleLine = 1.4f;   // altura de la línea media del relleno
	[Export] public float BarrierPostSpacing = 4f;    // separación entre verticales y travesaños, sobre el eje (se ajusta a la vuelta)
	[Export] public float BarrierThickGap = 0.04f;    // separación vertical de las dos líneas que forman cada borde grueso (base y remate)

	[ExportGroup("Pista 3D")]
	[Export] public float MaxBankWarning = 35f;    // grados: por encima se avisa en consola al cargar el circuito
	[Export] public float MaxSlopeWarning = 15f;   // grados

	[ExportGroup("Destello de la barrera")]
	[Export] public float GrazeFlashRadius = 3f;      // metros sobre el eje
	[Export(PropertyHint.Range, "0,1,0.01")] public float GrazeFlashIntensity = 0.4f;
	[Export] public float GrazeFlashFade = 0.2f;      // segundos en apagarse al dejar de rozar
	[Export] public float MediumFlashRadius = 7f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float MediumFlashIntensity = 1f;
	[Export] public float MediumFlashDuration = 0.4f;
	[Export] public float StrongFlashRadius = 10f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float StrongFlashIntensity = 1f;
	[Export] public float StrongFlashDuration = 0.6f;
	[Export] public float FlashGlow = 1.5f;           // brillo extra del naranja en el punto de impacto (halo más fuerte)

	// ---- Datos del circuito actual (salen del TrackDefinition) ----
	public string CircuitId => Definition?.Id ?? "";
	public string DisplayName => Definition?.DisplayName ?? "";
	public bool Clockwise => Definition.Clockwise;
	public float TrackWidth => Definition.TrackWidth;
	public int CheckpointCount => Definition.CheckpointCount;

	// Desplazamiento lateral de las barreras respecto al eje (igual a los dos lados)
	public float BarrierOffset => BarrierGeometry.Offset(TrackWidth, BarrierGap);

	// Longitud de la vuelta medida sobre el eje (m)
	public float LapLength { get; private set; }

	// Destello de cada nivel, con los valores del Inspector
	public BarrierFlashSettings BarrierFlashSettings => new()
	{
		Graze = new FlashLevelSettings { Radius = GrazeFlashRadius, Intensity = GrazeFlashIntensity, Duration = GrazeFlashFade },
		Medium = new FlashLevelSettings { Radius = MediumFlashRadius, Intensity = MediumFlashIntensity, Duration = MediumFlashDuration },
		Strong = new FlashLevelSettings { Radius = StrongFlashRadius, Intensity = StrongFlashIntensity, Duration = StrongFlashDuration }
	};

	// Valores por defecto de TrackY y GridSlots: los usa también SELECT CIRCUIT, que calcula el eje y los datos
	// del circuito sin montar la carrera
	public const float DefaultTrackY = -2f;
	public const int DefaultGridSlots = 8;

	// La nave flota esta altura por encima de la pista (en vertical)
	public const float ShipHover = 2f;

	// Índice de la línea central donde está la meta (el primer punto de control)
	public const int FinishIndex = 0;

	// Línea central de la pista en planta, a la altura TrackY (para vueltas, checkpoints e IA de rivales más adelante)
	public List<Vector3> Centerline { get; private set; }

	// El circuito se ha generado. Es false si no había TrackDefinition o no era válido:
	// en ese caso no hay línea central y no se puede montar la carrera.
	public bool IsBuilt => Centerline != null;

	// Índices de la línea central donde está cada checkpoint, en orden de carrera
	public List<int> CheckpointIndices { get; } = new();

	// Marco 3D de la pista (altura y peralte sobre la línea central en planta)
	public TrackFrame Frame { get; private set; }

	// Y más baja de la superficie (en el centro de la pista), para dejar la rejilla de fondo por debajo
	public float LowestSurfaceY => TrackY + (Frame?.MinElevation ?? 0f);

	// Firma del trazado para los fantasmas (con la altura y, si lo hay, el peralte)
	public string Signature => ComputeSignature(Frame, TrackWidth);

	// Elementos del circuito ya validados (spec docs/elementos-circuito.md, 7.1, 8.1 y 9.1)
	public IReadOnlyList<TrackSpan> Tunnels { get; private set; } = new List<TrackSpan>();
	public IReadOnlyList<TrackSpan> CurveWarnings { get; private set; } = new List<TrackSpan>();
	public IReadOnlyList<float> CrestWarnings { get; private set; } = new List<float>();

	// Distancia sobre el eje de cada checkpoint, en orden de carrera
	public List<float> CheckpointDistances { get; } = new();

	// Meta, checkpoints, semáforo, parrilla, túneles y avisos (null si el circuito no se ha generado)
	public TrackElements Elements { get; private set; }

	// Una cresta dentro de un túnel o a menos de estos metros de su entrada da aviso
	private const float CrestTunnelMargin = 50f;

	// Un túnel más corto da aviso
	private const float MinTunnelLength = 40f;

	private readonly List<Vector3> _verts = new();
	private readonly List<Color> _colors = new();

	// Distancia acumulada sobre el eje hasta cada muestra (la última entrada es la vuelta completa)
	private float[] _cumulative;

	// Marco de la pista en cada muestra de la línea central
	private FramePoint[] _samples;

	private ShaderMaterial _barrierMaterial;

	// Barreras: las líneas llevan en UV la distancia sobre el eje (x) y el lado (y: 0 izquierda, 1 derecha),
	// para que el shader pinte el destello sin rehacer la malla. Los colores de los vértices se usan
	// tal cual, como en el resto de la pista.
	private const string BarrierShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;

uniform float lap_length = 1.0;
uniform vec3 flash_color = vec3(1.0, 0.6, 0.2);
uniform float flash_glow = 1.5;
uniform vec3 left_flash = vec3(0.0);   // x: punto de impacto, y: radio, z: intensidad
uniform vec3 right_flash = vec3(0.0);
uniform float crest_beat = 1.0;        // brillo del tramo naranja de las crestas (UV2.x = 1)

float flash_at(float d, vec3 f) {
	if (f.z <= 0.0 || f.y <= 0.0)
		return 0.0;
	float dd = abs(d - f.x);
	dd = min(dd, lap_length - dd);
	return f.z * clamp(1.0 - dd / f.y, 0.0, 1.0);
}

void fragment() {
	float k = UV.y < 0.5 ? flash_at(UV.x, left_flash) : flash_at(UV.x, right_flash);
	vec3 base = UV2.x > 0.5 ? COLOR.rgb * crest_beat : COLOR.rgb;
	ALBEDO = mix(base, flash_color * (1.0 + flash_glow * k), k);
	ALPHA = mix(COLOR.a, 1.0, k);
}
";

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

		// El eje sale de CenterlineBuilder (lógica pura), el mismo que usan la miniatura y los datos de SELECT CIRCUIT;
		// el marco 3D le suma la altura y el peralte. Los avisos de la validación no impiden generar el circuito.
		var warnings = new List<string>();
		Frame = BuildFrame(Definition, TrackY, warnings);
		Frame.CheckLimits(MaxBankWarning, MaxSlopeWarning, warnings);
		foreach (string warning in warnings)
			GD.PushWarning($"Track: '{Definition.DisplayName}': {warning}");

		Centerline = BuildCenterline(Definition, TrackY).ConvertAll(p => new Vector3(p.X, p.Y, p.Z));

		int count = Centerline.Count;
		_cumulative = new float[count + 1];
		for (int i = 0; i < count; i++)
			_cumulative[i + 1] = _cumulative[i] + Centerline[i].DistanceTo(Centerline[(i + 1) % count]);
		LapLength = _cumulative[count];

		_samples = new FramePoint[count];
		for (int i = 0; i < count; i++)
			_samples[i] = Frame.At(_cumulative[i]);

		ValidateElements();
		BuildTrackLines();
		FindCheckpoints();

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

		AddChild(new MeshInstance3D { Name = "BarrierMesh", Mesh = BuildBarrierMesh() });

		Elements = new TrackElements { Name = "Elements" };
		AddChild(Elements);
		Elements.Build(this);
	}

	// Túneles, avisos de curva y crestas del TrackDefinition: lo que no vale se ignora con aviso en consola
	private void ValidateElements()
	{
		var warnings = new List<string>();
		Tunnels = TrackElementValidation.ValidateSpans(ToNumerics(Definition.Tunnels), LapLength, "Tunnels", MinTunnelLength, warnings);
		CurveWarnings = TrackElementValidation.ValidateSpans(ToNumerics(Definition.CurveWarnings), LapLength, "CurveWarnings", 0f, warnings);
		CrestWarnings = TrackElementValidation.ValidateCrests(Definition.CrestWarnings ?? System.Array.Empty<float>(), LapLength, warnings);
		TrackElementValidation.CheckCrestsNearTunnels(CrestWarnings, Tunnels, CrestTunnelMargin, warnings);
		foreach (string warning in warnings)
			GD.PushWarning($"Track: '{Definition.DisplayName}': {warning}");
	}

	// ---------------------------------------------------------------
	// Eje del circuito: punto más cercano, recolocación y barreras
	// ---------------------------------------------------------------

	// Punto de la pista justo debajo de una posición (en vertical): punto del eje, tangente y derecha en planta
	// (sentido de la marcha), desplazamiento lateral sobre la superficie (positivo = derecha), distancia sobre el
	// eje desde la meta y la superficie en ese punto (marco, altura, pendiente, curvatura del perfil y peralte).
	// En pista plana es la proyección en planta de siempre. Supone que el nodo Track está en el origen sin rotar ni escalar.
	public AxisPoint ProjectOnAxis(Vector3 position)
	{
		SurfacePoint p = Frame.SurfaceUnder(position.X, position.Z, position.Y);
		FramePoint f = p.Frame;
		return new AxisPoint(ToGodot(f.Position), ToGodot(f.PlanTangent), ToGodot(f.PlanRight), p.Lateral, f.Distance,
			ToGodot(f.Normal), ToGodot(f.Right), p.SurfaceY, f.Slope, f.Curvature, f.Bank);
	}

	// Recolocación: en el punto del eje más cercano, centrada, a la altura de vuelo sobre la superficie y
	// mirando en el sentido de la marcha
	public void GetRespawn(Vector3 position, out Vector3 shipPosition, out float yaw)
	{
		AxisPoint axis = ProjectOnAxis(position);
		shipPosition = new Vector3(axis.Center.X, axis.Center.Y + ShipHover, axis.Center.Z);
		yaw = Mathf.Atan2(-axis.Tangent.X, -axis.Tangent.Z);
	}

	// Brillo del tramo de barrera naranja de las crestas (latido; lo pasa TrackElements cada frame)
	public void SetCrestBarrierBrightness(float brightness)
	{
		_barrierMaterial?.SetShaderParameter("crest_beat", brightness);
	}

	// Pasa el destello actual al shader de la barrera (no rehace la malla)
	public void SetBarrierFlash(BarrierFlash flash)
	{
		if (_barrierMaterial == null)
			return;

		_barrierMaterial.SetShaderParameter("left_flash", FlashUniform(flash, BarrierSide.Left));
		_barrierMaterial.SetShaderParameter("right_flash", FlashUniform(flash, BarrierSide.Right));
	}

	private static Vector3 FlashUniform(BarrierFlash flash, BarrierSide side)
	{
		if (flash == null || !flash.IsActive(side))
			return Vector3.Zero;
		return new Vector3(flash.Center(side), flash.Radius(side), flash.Peak(side));
	}

	private static Vector3 ToGodot(NVec3 v) => new(v.X, v.Y, v.Z);

	// Altura (Y) de la superficie a "distance" m de la meta y "lateral" m del eje sobre la superficie
	private float SurfaceY(float distance, float lateral) => Frame.PointAt(distance, lateral, 0f).Y;

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
	// mirando en el sentido de la marcha, a la altura de vuelo sobre la superficie.
	public void GetGridStart(int position, float noseDistance, out Vector3 shipPosition, out float yaw)
	{
		GetGridMark(position, out Vector3 front, out Vector3 forward, out _);

		const float NoseGap = 0.5f;
		float back = noseDistance + NoseGap;
		Vector3 p = front - forward * back;
		GridSlot slot = GridLayout.Slot(position, TrackWidth, GridSettings);
		shipPosition = new Vector3(p.X, SurfaceY(LapLength - slot.Distance - back, slot.Lateral) + ShipHover, p.Z);
		yaw = Mathf.Atan2(-forward.X, -forward.Z);
	}

	// Salida lanzada (TIME TRIAL): sobre el eje del circuito, centrada en la pista, "distance" metros
	// antes de la meta (medidos sobre la línea central) y mirando en el sentido de la marcha.
	public void GetFlyingStart(float distance, out Vector3 shipPosition, out float yaw)
	{
		PointBehindFinish(distance, out Vector3 point, out Vector3 forward);
		shipPosition = new Vector3(point.X, SurfaceY(LapLength - Mathf.Max(0f, distance), 0f) + ShipHover, point.Z);
		yaw = Mathf.Atan2(-forward.X, -forward.Z);
	}

	// Centro de la nave colocada en la marca de la posición 1..GridSlots (como GetGridStart): distancia sobre
	// el eje desde la meta y lateral sobre la superficie. Para el marco de luz de cada puesto.
	public void GetGridSlotCenter(int position, float noseDistance, out float distance, out float lateral)
	{
		const float NoseGap = 0.5f;
		GridSlot slot = GridLayout.Slot(position, TrackWidth, GridSettings);
		distance = LapLength - slot.Distance - (noseDistance + NoseGap);
		lateral = slot.Lateral;
	}

	// Centro de la línea transversal de una marca (sobre la superficie), y las direcciones de la marcha
	// y derecha en ese punto
	private void GetGridMark(int position, out Vector3 front, out Vector3 forward, out Vector3 right)
	{
		GridSlot slot = GridLayout.Slot(position, TrackWidth, GridSettings);
		PointBehindFinish(slot.Distance, out Vector3 point, out forward);
		right = forward.Cross(Vector3.Up).Normalized();
		front = point + right * slot.Lateral;
		front.Y = SurfaceY(LapLength - slot.Distance, slot.Lateral);
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

	// Eje de un circuito en coordenadas de System.Numerics, sin montar la escena (spline cerrada, empezando en la meta)
	public static List<System.Numerics.Vector3> BuildCenterline(TrackDefinition definition, float trackY)
	{
		var control = new List<System.Numerics.Vector2>(definition.ControlPoints.Length);
		foreach (Vector2 px in definition.ControlPoints)
			control.Add(new System.Numerics.Vector2(px.X, px.Y));

		var center = new System.Numerics.Vector2(definition.ImageCenter.X, definition.ImageCenter.Y);
		return CenterlineBuilder.Build(control, center, definition.MetersPerPixel, definition.Clockwise, trackY);
	}

	// Marco 3D de un circuito sin montar la escena: su eje en planta más los perfiles de altura y peralte.
	// Las claves que no valen (fuera de la vuelta o desordenadas) se ignoran con un aviso en warnings.
	public static TrackFrame BuildFrame(TrackDefinition definition, float trackY, ICollection<string> warnings = null)
	{
		List<NVec3> plan = BuildCenterline(definition, trackY);
		float lap = CenterlineBuilder.CumulativeDistances(plan)[plan.Count];
		var elevation = new TrackProfile(ToNumerics(definition.ElevationKeys), lap, warnings, "ElevationKeys");
		var bank = new TrackProfile(ToNumerics(definition.BankKeys), lap, warnings, "BankKeys");
		return new TrackFrame(plan, elevation, bank);
	}

	// Firma del trazado (fantasmas): puntos del eje con su altura, ancho y, solo si lo hay, el peralte
	public static string ComputeSignature(TrackFrame frame, float trackWidth)
	{
		return TrackSignature.Compute(frame.Points, trackWidth, frame.SampleBanks);
	}

	private static List<System.Numerics.Vector2> ToNumerics(Vector2[] keys)
	{
		var list = new List<System.Numerics.Vector2>();
		if (keys != null)
		{
			foreach (Vector2 k in keys)
				list.Add(new System.Numerics.Vector2(k.X, k.Y));
		}
		return list;
	}

	private Vector3 TangentAt(int i)
	{
		int n = Centerline.Count;
		return (Centerline[(i + 1) % n] - Centerline[(i - 1 + n) % n]).Normalized();
	}

	// Derecha en planta en una muestra de la línea central
	public Vector3 RightAt(int i)
	{
		return TangentAt(i).Cross(Vector3.Up).Normalized();
	}

	// Índice de la muestra de la línea central más cercana a una posición (en el plano XZ).
	// Supone que el nodo Track está en el origen sin rotar ni escalar.
	public int NearestIndex(Vector3 position)
	{
		return Frame.NearestIndex(position.X, position.Z);
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

	// Punto de la superficie a "lateral" m del eje (y "height" m por encima, a lo largo de N)
	private static Vector3 Surface(in FramePoint f, float lateral, float height = 0f) => ToGodot(f.PointAt(lateral, height));

	// Suelo (spec docs/elementos-circuito.md, 2, diseño 0): bordes cian continuos, dos carriles azules discontinuos
	// a ±1/3 del medio ancho (LaneDashLength m cada LaneDashPeriod m), línea central azul continua y una transversal
	// de borde a borde cada RungSpacing m, azul más tenue. Siguen la altura y se ladean con el peralte.
	// Solo visual: la firma no cambia.
	private void BuildTrackLines()
	{
		int n = Centerline.Count;
		float hw = TrackWidth * 0.5f;
		Color lane = Dim(Palette.TrackLane, LaneOpacity);

		for (int i = 0; i < n; i++)
		{
			int j = (i + 1) % n;
			Line(Surface(_samples[i], -hw), Surface(_samples[j], -hw), Palette.TrackEdge);
			Line(Surface(_samples[i], hw), Surface(_samples[j], hw), Palette.TrackEdge);
			Line(Surface(_samples[i], 0f), Surface(_samples[j], 0f), lane);
		}

		// Carriles: cada trazo se parte en tramos de ~1 m para que siga la curva y la altura
		const float DashStep = 1f;
		float period = Mathf.Max(1f, LaneDashPeriod);
		float dash = Mathf.Clamp(LaneDashLength, 0.1f, period);
		for (float d = 0f; d < LapLength - 0.5f; d += period)
		{
			float end = Mathf.Min(d + dash, LapLength);
			int pieces = Mathf.Max(1, Mathf.CeilToInt((end - d) / DashStep));
			for (int k = 0; k < pieces; k++)
			{
				FramePoint a = Frame.At(d + (end - d) * k / pieces);
				FramePoint b = Frame.At(d + (end - d) * (k + 1) / pieces);
				Line(Surface(a, -hw / 3f), Surface(b, -hw / 3f), lane);
				Line(Surface(a, hw / 3f), Surface(b, hw / 3f), lane);
			}
		}

		Color rung = Dim(Palette.TrackRung, RungOpacity);
		float spacing = Mathf.Max(1f, RungSpacing);
		for (float d = 0f; d < LapLength - 0.5f; d += spacing)
		{
			FramePoint f = Frame.At(d);
			Line(Surface(f, -hw), Surface(f, hw), rung);
		}
	}

	// Color multiplicado por una opacidad (opaco: la "opacidad" se aplica oscureciendo el color)
	private static Color Dim(Color c, float k) => new(c.R * k, c.G * k, c.B * k, 1f);

	// Barreras: un muro a cada lado, a BarrierOffset del eje, con una franja cian arriba y otra abajo y un
	// relleno ligero entre ellas (línea media y verticales azules). Las horizontales siguen el eje con la
	// misma resolución que el borde; la base y el remate son gruesos (dos líneas a BarrierThickGap, hacia
	// dentro de la franja). Verticales y travesaños, repartidos por igual en la vuelta (BarrierGeometry).
	// Las alturas se miden a lo largo de N: el muro es perpendicular a la superficie y se ladea con el peralte.
	// Malla aparte con su shader para el destello.
	// Cresta (spec docs/elementos-circuito.md, 9.3): de CrestBarrierHalfLength m antes a otros tantos después de cada
	// cresta señalizada, la barrera se dibuja en naranja (no encima de la cian: la sustituye), con una segunda
	// línea de remate CrestBarrierRaise m más arriba y las verticales subiendo hasta ella. Esas líneas llevan
	// UV2.x = 1 para que el shader les aplique el latido.
	private ArrayMesh BuildBarrierMesh()
	{
		var verts = new List<Vector3>();
		var colors = new List<Color>();
		var uvs = new List<Vector2>();
		var uv2s = new List<Vector2>();

		void Add(Vector3 a, float da, Vector3 b, float db, float side, Color c, bool crest = false)
		{
			verts.Add(a);
			verts.Add(b);
			colors.Add(c);
			colors.Add(c);
			uvs.Add(new Vector2(da, side));
			uvs.Add(new Vector2(db, side));
			Vector2 flag = crest ? Vector2.Right : Vector2.Zero;
			uv2s.Add(flag);
			uv2s.Add(flag);
		}

		bool InCrest(float d) => TrackElementShapes.InCrestBarrier(d, CrestWarnings, CrestBarrierHalfLength, LapLength);
		Color CrestColor(Color c) => new(Palette.CrestBarrier, c.A);

		int n = Centerline.Count;
		float offset = BarrierOffset;
		BarrierLineHeights h = BarrierGeometry.LineHeights(BarrierHeight, BarrierBottomBand, BarrierTopBand, BarrierMiddleLine);
		float[] posts = BarrierGeometry.PostDistances(LapLength, BarrierPostSpacing);

		var horizontals = new (float Height, Color Color)[]
		{
			(h.Base, Palette.BarrierBand),
			(h.Base + BarrierThickGap, Palette.BarrierBand),
			(h.BottomBandTop, Palette.BarrierBandInner),
			(h.Middle, Palette.BarrierFill),
			(h.TopBandBottom, Palette.BarrierBandInner),
			(h.Top - BarrierThickGap, Palette.BarrierBand),
			(h.Top, Palette.BarrierBand)
		};
		var verticals = new (float From, float To, Color Color)[]
		{
			(h.Base, h.BottomBandTop, Palette.BarrierBandTie),
			(h.BottomBandTop, h.TopBandBottom, Palette.BarrierPost),
			(h.TopBandBottom, h.Top, Palette.BarrierBandTie)
		};

		foreach (float sign in new[] { -1f, 1f })
		{
			float side = sign > 0f ? 1f : 0f;
			float lateral = sign * offset;

			for (int i = 0; i < n; i++)
			{
				int j = (i + 1) % n;
				// El tramo del cierre llega a la vuelta completa (no a 0) para que la distancia sea continua
				float da = _cumulative[i];
				float db = _cumulative[i + 1];
				bool crest = InCrest((da + db) * 0.5f);

				foreach (var line in horizontals)
				{
					Color c = crest ? CrestColor(line.Color) : line.Color;
					Add(Surface(_samples[i], lateral, line.Height), da, Surface(_samples[j], lateral, line.Height), db, side, c, crest);
				}
				if (crest)
				{
					float raised = h.Top + CrestBarrierRaise;
					Add(Surface(_samples[i], lateral, raised), da, Surface(_samples[j], lateral, raised), db, side, Palette.CrestBarrier, true);
				}
			}

			foreach (float d in posts)
			{
				FramePoint f = Frame.At(d);
				bool crest = InCrest(d);
				foreach (var line in verticals)
				{
					Color c = crest ? CrestColor(line.Color) : line.Color;
					Add(Surface(f, lateral, line.From), d, Surface(f, lateral, line.To), d, side, c, crest);
				}
				if (crest)
					Add(Surface(f, lateral, h.Top), d, Surface(f, lateral, h.Top + CrestBarrierRaise), d, side, CrestColor(Palette.BarrierBandTie), true);
			}
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
		arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
		arrays[(int)Mesh.ArrayType.TexUV2] = uv2s.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

		_barrierMaterial = new ShaderMaterial { Shader = new Shader { Code = BarrierShaderCode } };
		_barrierMaterial.SetShaderParameter("lap_length", LapLength);
		_barrierMaterial.SetShaderParameter("flash_color", new Vector3(Palette.BarrierFlash.R, Palette.BarrierFlash.G, Palette.BarrierFlash.B));
		_barrierMaterial.SetShaderParameter("flash_glow", FlashGlow);
		mesh.SurfaceSetMaterial(0, _barrierMaterial);
		return mesh;
	}

	// Checkpoints repartidos a distancias iguales a lo largo del circuito (sin contar la meta, que está en
	// FinishIndex). Sus índices quedan guardados, en orden de carrera, en CheckpointIndices, y sus distancias
	// en CheckpointDistances. Sus pórticos los dibuja TrackElements.
	private void FindCheckpoints()
	{
		CheckpointIndices.Clear();
		CheckpointDistances.Clear();

		var numerics = Centerline.ConvertAll(p => new System.Numerics.Vector3(p.X, p.Y, p.Z));
		foreach (int index in CenterlineBuilder.CheckpointIndices(numerics, CheckpointCount))
		{
			CheckpointIndices.Add(index);
			CheckpointDistances.Add(_cumulative[index]);
		}
	}
}

// Punto del eje del circuito bajo una posición (ver Track.ProjectOnAxis)
public readonly struct AxisPoint
{
	public readonly Vector3 Center;        // punto del eje (sobre la superficie, con su altura)
	public readonly Vector3 Tangent;       // sentido de la marcha en planta (unitario, horizontal)
	public readonly Vector3 Right;         // hacia la derecha de la marcha en planta (unitario, horizontal)
	public readonly float Lateral;         // desplazamiento lateral de la posición sobre la superficie: positivo = derecha
	public readonly float Distance;        // metros sobre el eje (en planta) desde la meta
	public readonly Vector3 Normal;        // "arriba" de la superficie (N)
	public readonly Vector3 SurfaceRight;  // lado de la superficie (B), ladeado con el peralte
	public readonly float SurfaceY;        // Y de la superficie justo debajo de la posición
	public readonly float Slope;           // dh/ds del eje
	public readonly float Curvature;       // d²h/ds² del perfil de altura (negativa en las crestas)
	public readonly float Bank;            // peralte en radianes (positivo = borde derecho más alto)

	public AxisPoint(Vector3 center, Vector3 tangent, Vector3 right, float lateral, float distance,
		Vector3 normal, Vector3 surfaceRight, float surfaceY, float slope, float curvature, float bank)
	{
		Center = center;
		Tangent = tangent;
		Right = right;
		Lateral = lateral;
		Distance = distance;
		Normal = normal;
		SurfaceRight = surfaceRight;
		SurfaceY = surfaceY;
		Slope = slope;
		Curvature = curvature;
		Bank = bank;
	}
}
