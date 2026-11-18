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
// otra malla de líneas en suma, sus chispas con las de los choques y su foco (núcleo y halo de cara a la cámara) en
// cuadrados en suma. Y los del aterrizaje (spec docs/efectos-3-aterrizaje.md): sus chispas con las de los choques y su
// polvo con el humo, más ancho que alto. Las mallas se rehacen cada frame en _Process, así la pausa lo congela todo.
// Sin reglas propias.
public partial class CollisionEffects : Node3D
{
	// Nombres de parámetros de shader creados una vez (con un texto, cada llamada crea un StringName nuevo)
	private static readonly StringName SnAtlas = "atlas";
	private static readonly StringName SnSprite = "sprite";
	private static readonly StringName SnCoreColor = "core_color";
	private static readonly StringName SnMidColor = "mid_color";
	private static readonly StringName SnEdgeColor = "edge_color";

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
	[Export] public float DriftSparkRate { get; set; } = D.Drift.SparkRate;          // chispas por segundo × d, en HIGH
	[Export] public float DriftSparkInherit { get; set; } = D.Drift.SparkInherit;    // de la velocidad de la nave
	[Export] public float DriftSparkOutMin { get; set; } = D.Drift.SparkOutMin;      // m/s hacia fuera
	[Export] public float DriftSparkOutMax { get; set; } = D.Drift.SparkOutMax;
	[Export] public float DriftSparkUpMin { get; set; } = D.Drift.SparkUpMin;        // m/s hacia arriba
	[Export] public float DriftSparkUpMax { get; set; } = D.Drift.SparkUpMax;
	[Export] public float DriftSparkLifeMin { get; set; } = D.Drift.SparkLifeMin;    // s
	[Export] public float DriftSparkLifeMax { get; set; } = D.Drift.SparkLifeMax;
	[Export] public float DriftJetFraction { get; set; } = D.Drift.JetFraction;      // de las chispas, las del foco
	[Export] public float DriftEmberRate { get; set; } = D.Drift.EmberRate;          // brasas por segundo × d
	[Export] public float DriftFocusDrop { get; set; } = D.Drift.FocusDrop;          // m: foco y chispas, más bajos que el borde
	[Export] public float DriftCoreSize { get; set; } = D.Drift.CoreSize;            // m: núcleo del foco
	[Export] public float DriftCoreIntensity { get; set; } = D.Drift.CoreIntensity;
	[Export] public float DriftFocusHaloSize { get; set; } = D.Drift.FocusHaloSize;  // m: halo del foco
	[Export] public float DriftFocusHaloIntensity { get; set; } = D.Drift.FocusHaloIntensity;

	[ExportGroup("Aterrizaje")]
	[Export] public int LandingMediumSparks { get; set; } = D.Landing.Medium.Sparks;
	[Export] public int LandingStrongSparks { get; set; } = D.Landing.Strong.Sparks;
	[Export] public int LandingStrongEmbers { get; set; } = D.Landing.Strong.Embers;
	[Export] public float LandingSparkBellyDrop { get; set; } = D.Landing.SparkBellyDrop;   // m por debajo de la panza
	[Export] public float LandingSparkInherit { get; set; } = D.Landing.SparkInherit;
	[Export] public int LandingMediumPuffs { get; set; } = D.Landing.Medium.Puffs;
	[Export] public int LandingStrongPuffs { get; set; } = D.Landing.Strong.Puffs;
	[Export] public float LandingDustLift { get; set; } = D.Landing.DustLift;               // m sobre la superficie
	[Export] public float LandingDustInherit { get; set; } = D.Landing.DustInherit;
	[Export] public float LandingDustOpacity { get; set; } = D.Landing.Dust.Opacity;

	[ExportGroup("Humo de daños")]
	[Export] public float DamageSmokeMediumRate { get; set; } = D.DamageSmoke.Medium.Rate;         // por segundo en Smoke
	[Export] public float DamageSmokeStrongRate { get; set; } = D.DamageSmoke.Strong.Rate;
	[Export] public float DamageSmokeStrongSecondaryRate { get; set; } = D.DamageSmoke.Strong.SecondaryRate;   // en Smoke2 y Smoke3
	[Export] public float DamageSmokeMediumOpacity { get; set; } = D.DamageSmoke.Medium.Opacity;
	[Export] public float DamageSmokeStrongOpacity { get; set; } = D.DamageSmoke.Strong.Opacity;
	[Export] public float DamageSmokeLagFraction { get; set; } = D.DamageSmoke.LagFraction;        // de la velocidad de la nave
	[Export] public float DamageSmokeLagMin { get; set; } = D.DamageSmoke.LagMin;                  // m/s
	[Export] public float DamageSmokeLagMax { get; set; } = D.DamageSmoke.LagMax;                  // m/s
	[Export] public float DamageSmokeSwirlSide { get; set; } = D.DamageSmoke.SwirlSide;            // m/s
	[Export] public float DamageSmokeSwirlUp { get; set; } = D.DamageSmoke.SwirlUp;                // m/s
	[Export] public int DamageSmokeMaxPuffs { get; set; } = D.DamageSmoke.MaxPuffs;
	[Export] public bool DamageSmokeWrap { get; set; } = D.DamageSmoke.Wrap;   // B · ENVUELTO (va con la nave); false: A · PENACHO

	private const int Bucket = 256;       // las mallas de líneas usan arrays de tamaño múltiplo de esto, para no crearlos cada frame
	private const int QuadBucket = 32;    // ídem en cuadrados

	private CollisionFx _fx;
	private DriftMarks _drift;
	private DriftSparkEmitter _driftSparks;
	private DamageSmoke _damageSmoke;
	private DamageEmbers _damageEmbers;
	// Naves con fuego (nivel 3) en su último paso: la nave que se ve y sus focos. Sin las que no se ven (tu nave en cabina).
	private readonly Dictionary<int, (Transform3D Hull, DamageSmokeFoci Foci, NVector3 Velocity)> _fires = new();
	private float _fireTime;
	private MultiMeshInstance3D _flameView, _haloView;
	private MultiMesh _flameMulti, _haloMulti;
	private float[] _flameBuffer, _haloBuffer;
	private MeshInstance3D _emberView;
	private ImmediateMesh _emberMesh;
	private const int FireStride = 20;    // como el humo: transformación 12, color 4, datos propios 4 (medio ancho y medio alto)
	private const int MaxFireShips = 16;
	// Recolocaciones de cada nave vistas la última vez: al cambiar, se borra su humo de daños
	private readonly Dictionary<int, int> _respawns = new();
	private int[] _smokeOrder;
	private float[] _smokeDepth;
	private NVector3[] _smokeWorld;
	// La nave que se ve de cada una en su último paso: el humo de B · ENVUELTO va en sus ejes
	private readonly Dictionary<int, ShipPose> _smokePoses = new();
	// Foco del derrape de cada nave en su último paso: intensidades del núcleo y del halo, y dónde está
	private readonly Dictionary<int, (float Core, float Halo, NVector3 Point)> _driftFoci = new();
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
	private MeshInstance3D _driftGlowView;
	private MultiMeshInstance3D _damageSmokeView;
	private MultiMesh _damageSmokeMulti;
	private float[] _damageSmokeBuffer;
	private const int SmokeStride = 20;   // floats por bocanada: transformación 12, color 4, datos propios 4
	private ArrayMesh _sparkMesh;
	private ArrayMesh _lineMesh;
	private ArrayMesh _glowMesh;
	private ArrayMesh _smokeMesh;
	private ArrayMesh _markMesh;
	private ArrayMesh _driftGlowMesh;
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
		_driftSparks = new DriftSparkEmitter(_settings.Drift, new Random());
		_damageSmoke = new DamageSmoke(_settings.DamageSmoke, new Random());
		_damageEmbers = new DamageEmbers(_settings.DamageSmoke, new Random());
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

	// El derrape de una nave en este paso de física de dt segundos (spec docs/efectos-2-derrape.md). slipAngle: el de
	// ShipDynamics, en radianes; position: la de la nave; forward y velocity: su morro y su velocidad en planta; hull:
	// la nave que se ve, con su inclinación (de ahí nacen chispas y foco).
	public void DriftShip(int shipId, bool drifting, bool airborne, float slipAngle, NVector3 position, NVector2 forward,
		NVector2 velocity, Transform3D hull, float halfWidth, DriftContactShape contact, float dt)
	{
		if (_drift == null)
			return;
		DriftFxSettings s = _settings.Drift;
		DriftState state = DriftIntensity.Evaluate(drifting, airborne, slipAngle, s);
		var edge = new DriftEdge(halfWidth, contact.EdgeStartZ, contact.EdgeStartFraction, contact.EdgeEndZ,
			contact.EdgeEndFraction, contact.EdgeY - s.FocusDrop, contact.FocusZ, contact.FocusFraction);
		Basis b = hull.Basis;
		var pose = new ShipPose(N(hull.Origin), N(b.X.Normalized()), N(b.Y.Normalized()), N(b.Z.Normalized()));
		_driftSparks.Emit(shipId, state, _frame, pose, velocity, edge, _fx.Quality, dt, _fx.Sparks);
		if (!state.Active)
		{
			_drift.Update(shipId, state, default, default);
			_driftFoci.Remove(shipId);
			return;
		}
		(NVector3 left, NVector3 right) = DriftContact.MarkPoints(_frame, position, forward, halfWidth, contact.TipZ, s);
		_drift.Update(shipId, state, left, right);
		float now = _drift.Now;
		_driftFoci[shipId] = (DriftFocus.Core(state, now, shipId, s), DriftFocus.Halo(state, now, shipId, s),
			pose.Point(DriftContact.FocusPoint(edge, state.Side)));
	}

	// El aterrizaje de una nave en este paso de física (spec docs/efectos-3-aterrizaje.md). fallSpeed: su velocidad de
	// caída contra la superficie; thresholds: los umbrales de pista-3d; hull: la nave que se ve, con su inclinación;
	// velocity: su velocidad en planta.
	public void Landing(float fallSpeed, in LandingSettings thresholds, Transform3D hull, NVector2 velocity,
		ShipVisualProfile visual)
	{
		if (_fx == null)
			return;
		LandingLevel level = LandingImpact.Level(fallSpeed, thresholds);
		if (level == LandingLevel.None)
			return;
		Basis b = hull.Basis;
		var pose = new ShipPose(N(hull.Origin), N(b.X.Normalized()), N(b.Y.Normalized()), N(b.Z.Normalized()));
		_fx.Land(level, _frame, pose, velocity, visual.CollisionHalfWidth, visual.CollisionBack, visual.BellyY);
	}

	// El humo de daños de una nave en este paso de física (spec docs/efectos-4-humo-danos.md): level, su nivel de daño
	// (0 fuera de RACE); respawns, cuántas veces se ha recolocado (al cambiar, su humo y sus brasas se borran; el fuego
	// sigue, porque va con la nave); hull: la nave que se ve; velocity: su velocidad en el mundo; showFire: false para tu
	// nave en cabina (el casco no se dibuja y el fuego va pegado a él).
	public void DamageShip(int shipId, int level, int respawns, Transform3D hull, NVector3 velocity, ShipVisualProfile visual,
		bool showFire, float dt)
	{
		if (_damageSmoke == null)
			return;
		if (_respawns.TryGetValue(shipId, out int seen) && seen != respawns)
		{
			_damageSmoke.Clear(shipId);
			_damageEmbers.Clear(shipId);
		}
		_respawns[shipId] = respawns;
		if (DamageFire.Burns(level) && showFire)
			_fires[shipId] = (hull, visual.SmokeFoci, velocity);
		else
			_fires.Remove(shipId);
		Basis b = hull.Basis;
		var pose = new ShipPose(N(hull.Origin), N(b.X.Normalized()), N(b.Y.Normalized()), N(b.Z.Normalized()));
		_smokePoses[shipId] = pose;
		if (level < 2)
			return;
		_damageSmoke.Emit(shipId, level, visual.SmokeFoci, pose, velocity, _fx.Quality, dt);
		_damageEmbers.Emit(shipId, level, visual.SmokeFoci, pose, velocity, _fx.Quality, dt);
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
		_damageSmoke.Update((float)delta);
		_damageEmbers.Update((float)delta);
		if (delta > 0)
			_fireTime += (float)delta;
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
		DrawDamageSmoke(camera.GlobalPosition);
		DrawFire();
		DrawEmbers();
		DrawDriftFoci(right, up);
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
		s.Drift.FocusHaloColor = s.Drift.MarkColor;
		s.Drift.SparkRate = DriftSparkRate;
		s.Drift.SparkInherit = DriftSparkInherit;
		s.Drift.SparkOutMin = DriftSparkOutMin;
		s.Drift.SparkOutMax = DriftSparkOutMax;
		s.Drift.SparkUpMin = DriftSparkUpMin;
		s.Drift.SparkUpMax = DriftSparkUpMax;
		s.Drift.SparkLifeMin = DriftSparkLifeMin;
		s.Drift.SparkLifeMax = DriftSparkLifeMax;
		s.Drift.JetFraction = DriftJetFraction;
		s.Drift.EmberRate = DriftEmberRate;
		s.Drift.FocusDrop = DriftFocusDrop;
		s.Drift.CoreSize = DriftCoreSize;
		s.Drift.CoreIntensity = DriftCoreIntensity;
		s.Drift.FocusHaloSize = DriftFocusHaloSize;
		s.Drift.FocusHaloIntensity = DriftFocusHaloIntensity;
		s.Landing.Medium.Sparks = LandingMediumSparks;
		s.Landing.Strong.Sparks = LandingStrongSparks;
		s.Landing.Strong.Embers = LandingStrongEmbers;
		s.Landing.SparkBellyDrop = LandingSparkBellyDrop;
		s.Landing.SparkInherit = LandingSparkInherit;
		s.Landing.Medium.Puffs = LandingMediumPuffs;
		s.Landing.Strong.Puffs = LandingStrongPuffs;
		s.Landing.DustLift = LandingDustLift;
		s.Landing.DustInherit = LandingDustInherit;
		s.Landing.Dust.Opacity = LandingDustOpacity;
		s.DamageSmoke.Medium.Rate = DamageSmokeMediumRate;
		s.DamageSmoke.Strong.Rate = DamageSmokeStrongRate;
		s.DamageSmoke.Strong.SecondaryRate = DamageSmokeStrongSecondaryRate;
		s.DamageSmoke.Medium.Opacity = DamageSmokeMediumOpacity;
		s.DamageSmoke.Strong.Opacity = DamageSmokeStrongOpacity;
		s.DamageSmoke.LagFraction = DamageSmokeLagFraction;
		s.DamageSmoke.LagMin = DamageSmokeLagMin;
		s.DamageSmoke.LagMax = DamageSmokeLagMax;
		s.DamageSmoke.SwirlSide = DamageSmokeSwirlSide;
		s.DamageSmoke.SwirlUp = DamageSmokeSwirlUp;
		s.DamageSmoke.MaxPuffs = DamageSmokeMaxPuffs;
		s.DamageSmoke.Wrap = DamageSmokeWrap;
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
		var driftGlow = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = Radial(new[] { 0f, 0.3f, 1f }, new[] { 1f, 0.5f, 0f }),
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		_driftGlowView = View("DriftGlows", driftGlow, out _driftGlowMesh);
		BuildDamageSmokeView();
		BuildFireViews();
	}

	// Halos: cuadrados de cara a la cámara, en suma, con medio ancho y medio alto propios
	private const string FireShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform sampler2D sprite : source_color, filter_linear_mipmap, repeat_disable;
varying vec4 tint;
void vertex() {
	vec2 p = VERTEX.xy * 2.0 * INSTANCE_CUSTOM.xy;
	vec3 center = MODEL_MATRIX[3].xyz;
	vec3 world = center + INV_VIEW_MATRIX[0].xyz * p.x + INV_VIEW_MATRIX[1].xyz * p.y;
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world, 1.0);
	tint = COLOR;
}
void fragment() {
	vec4 t = texture(sprite, UV);
	ALBEDO = tint.rgb * t.rgb;
	ALPHA = tint.a * t.a;
}
";

	// Llamas: un cuadrado a lo largo del eje de cada una (el Y de su transformación: de la vertical a casi hacia atrás con
	// el viento), girado de lado hacia la cámara alrededor de él, con la base en el foco. Forma y color salen de un ruido
	// que corre por el eje: base ancha, cola que se rompe en lenguas, núcleo amarillo claro, naranja y bordes rojos.
	// Datos propios: medio ancho, medio largo, tiempo del ruido (el de la carrera: la pausa lo congela) y semilla.
	private const string FlameShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec3 core_color;
uniform vec3 mid_color;
uniform vec3 edge_color;
varying vec4 tint;
varying vec2 noise_data;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * value_noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		a *= 0.5;
	}
	return v;
}
void vertex() {
	vec3 center = MODEL_MATRIX[3].xyz;
	vec3 up = normalize(MODEL_MATRIX[1].xyz);
	vec3 to_camera = INV_VIEW_MATRIX[3].xyz - center;
	vec3 right = cross(up, to_camera);
	right = length(right) > 1e-4 ? normalize(right) : INV_VIEW_MATRIX[0].xyz;
	vec2 p = VERTEX.xy * 2.0 * INSTANCE_CUSTOM.xy;
	vec3 world = center + right * p.x + up * p.y;
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world, 1.0);
	tint = COLOR;
	noise_data = INSTANCE_CUSTOM.zw;
}
void fragment() {
	vec2 uv = vec2(UV.x, 1.0 - UV.y);
	// La llama ondula de lado, más hacia la cola, con un ruido que corre por el eje
	float wave = fbm(vec2(uv.y * 3.0 - noise_data.x * 1.3, noise_data.y));
	uv.x += (wave - 0.5) * 0.45 * uv.y;
	float n = fbm(vec2(uv.x * 3.0 + noise_data.y, uv.y * 2.5 - noise_data.x));
	float dx = abs(uv.x - 0.5) * 2.0;
	// Base ancha y redonda, se afila hacia la cola; bordes suaves
	float profile = pow(max(1.0 - uv.y, 0.0), 0.55) * (0.65 + 0.7 * n);
	float body = 1.0 - smoothstep(profile * 0.35, profile, dx);
	// La cola se rompe en lenguas sueltas que se apagan
	float tongues = fbm(vec2(uv.x * 6.0 - noise_data.y, uv.y * 5.0 - noise_data.x * 1.6));
	float breakup = smoothstep(0.2, 0.62, tongues + (1.0 - uv.y) * 0.55);
	float top = 1.0 - smoothstep(0.35, 1.0, uv.y + (n - 0.5) * 0.5);
	float bottom = smoothstep(0.0, 0.04, uv.y);
	float heat = clamp(body * top * bottom * breakup, 0.0, 1.0);
	vec3 color = mix(edge_color, mid_color, smoothstep(0.1, 0.5, heat));
	color = mix(color, core_color, smoothstep(0.55, 0.95, heat));
	ALBEDO = color;
	ALPHA = clamp(heat * 1.3, 0.0, 1.0) * tint.a;
}
";

	private void BuildFireViews()
	{
		DamageFireSettings fire = _settings.DamageSmoke.Fire;
		int foci = MaxFireShips * 3;
		var flame = new ShaderMaterial { Shader = new Shader { Code = FlameShader }, RenderPriority = 1 };
		flame.SetShaderParameter(SnCoreColor, G(fire.CoreColor));
		flame.SetShaderParameter(SnMidColor, G(fire.MidColor));
		flame.SetShaderParameter(SnEdgeColor, G(fire.EdgeColor));
		(_flameView, _flameMulti, _flameBuffer) = FireView("DamageFlames", flame, foci);
		// Después del humo: las mallas no tienen un centro con sentido para ordenarlas por distancia, y con el humo oscuro
		// encima el fuego se apagaba
		var halo = new ShaderMaterial { Shader = new Shader { Code = FireShader }, RenderPriority = 1 };
		halo.SetShaderParameter(SnSprite, Radial(new[] { 0f, 0.3f, 1f }, new[] { 1f, 0.5f, 0f }));
		(_haloView, _haloMulti, _haloBuffer) = FireView("DamageHalos", halo, foci);

		// Brasas: líneas en suma; un ImmediateMesh se rellena vértice a vértice, sin arrays nuevos cada frame
		_emberMesh = new ImmediateMesh();
		_emberView = new MeshInstance3D
		{
			Name = "DamageEmbers",
			Mesh = _emberMesh,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				BlendMode = BaseMaterial3D.BlendModeEnum.Add,
				VertexColorUseAsAlbedo = true,
				DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				RenderPriority = 1
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)),
			Visible = false
		};
		AddChild(_emberView);
	}

	private (MultiMeshInstance3D, MultiMesh, float[]) FireView(string name, ShaderMaterial material, int max)
	{
		var multi = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			UseCustomData = true,
			Mesh = new QuadMesh { Size = Vector2.One },
			InstanceCount = max,
			VisibleInstanceCount = 0
		};
		var view = new MultiMeshInstance3D
		{
			Name = name,
			Multimesh = multi,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)),
			Visible = false
		};
		AddChild(view);
		return (view, multi, new float[max * FireStride]);
	}

	// Cuadrado de cara a la cámara, girado y con su nube del atlas de 2 × 2, desde los datos de cada instancia
	private const string DamageSmokeShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
uniform sampler2D atlas : source_color, filter_linear_mipmap, repeat_disable;
varying vec4 tint;
void vertex() {
	float a = INSTANCE_CUSTOM.x;
	float tile = INSTANCE_CUSTOM.y;
	float radius = INSTANCE_CUSTOM.z;
	vec2 p = VERTEX.xy * 2.0 * radius;
	vec2 q = vec2(p.x * cos(a) - p.y * sin(a), p.x * sin(a) + p.y * cos(a));
	vec3 center = MODEL_MATRIX[3].xyz;
	vec3 world = center + INV_VIEW_MATRIX[0].xyz * q.x + INV_VIEW_MATRIX[1].xyz * q.y;
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world, 1.0);
	UV = vec2(mod(tile, 2.0), floor(tile / 2.0)) * 0.5 + UV * 0.5;
	tint = COLOR;
}
void fragment() {
	vec4 t = texture(atlas, UV);
	ALBEDO = tint.rgb * t.rgb;
	ALPHA = tint.a * t.a;
}
";

	private void BuildDamageSmokeView()
	{
		int max = Math.Max(1, _settings.DamageSmoke.MaxPuffs);
		var material = new ShaderMaterial { Shader = new Shader { Code = DamageSmokeShader } };
		material.SetShaderParameter(SnAtlas, CloudAtlas());
		_damageSmokeMulti = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			UseCustomData = true,
			Mesh = new QuadMesh { Size = Vector2.One },
			InstanceCount = max,
			VisibleInstanceCount = 0
		};
		_damageSmokeBuffer = new float[max * SmokeStride];
		_smokeOrder = new int[max];
		_smokeDepth = new float[max];
		_smokeWorld = new NVector3[max];
		_damageSmokeView = new MultiMeshInstance3D
		{
			Name = "DamageSmoke",
			Multimesh = _damageSmokeMulti,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			// El cuadrado se coloca en el shader: sin una caja grande, Godot lo descartaría fuera de su sitio
			CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)),
			Visible = false
		};
		AddChild(_damageSmokeView);
	}

	// Las 4 nubes del humo de daños en una textura de 2 × 2, con mipmaps
	private static ImageTexture CloudAtlas()
	{
		int n = CloudTexture.Size;
		var data = new byte[4 * n * n * 4];
		for (int k = 0; k < CloudTexture.Seeds.Length; k++)
		{
			(float Light, float Alpha)[] pixels = CloudTexture.Pixels(CloudTexture.Seeds[k]);
			int ox = (k % 2) * n, oy = (k / 2) * n;
			for (int j = 0; j < n; j++)
				for (int i = 0; i < n; i++)
				{
					(float light, float alpha) = pixels[j * n + i];
					int o = ((oy + j) * 2 * n + ox + i) * 4;
					byte l = (byte)Math.Round(255f * light);
					data[o] = l;
					data[o + 1] = l;
					data[o + 2] = l;
					data[o + 3] = (byte)Math.Round(255f * alpha);
				}
		}
		Image image = Image.CreateFromData(2 * n, 2 * n, false, Image.Format.Rgba8, data);
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
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

	// Foco del derrape: el halo naranja y, encima, el núcleo blanco caliente, de cara a la cámara
	private void DrawDriftFoci(Vector3 right, Vector3 up)
	{
		int count = _driftFoci.Count * 2;
		if (!Begin(_driftGlowView, _driftGlowMesh, count))
			return;
		(Vector3[] v, Color[] c, Vector2[] u) = Quads(count);
		DriftFxSettings s = _settings.Drift;
		int q = 0;
		foreach ((float core, float halo, NVector3 point) in _driftFoci.Values)
		{
			Vector3 center = G(point);
			Quad(v, c, u, q++, center, right, up, s.FocusHaloSize * 0.5f, C(s.FocusHaloColor * halo));
			Quad(v, c, u, q++, center, right, up, s.CoreSize * 0.5f, C(s.CoreColor * core));
		}
		FillQuads(v, c, u, q);
		Commit(_driftGlowMesh, Mesh.PrimitiveType.Triangles, v, c, u);
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
			float r = CrashSmoke.Radius(p);
			Quad(v, c, u, q++, G(p.Position), right, up, r, r * CrashSmoke.HeightScale(p), new Color(col.X, col.Y, col.Z, smoke.Opacity(p)));
		}
		FillQuads(v, c, u, q);
		Commit(_smokeMesh, Mesh.PrimitiveType.Triangles, v, c, u);
	}

	// Humo de daños: de lejos a cerca de la cámara (es oscuro y con mezcla normal: el orden se nota), cada bocanada con su
	// nube del atlas y su giro. Va en un MultiMesh con un búfer de floats que no cambia de tamaño: rehacer una malla con
	// arrays de Godot crea objetos en cada frame, y el humo se dibuja durante toda la carrera.
	private void DrawDamageSmoke(Vector3 cameraPosition)
	{
		DamageSmoke smoke = _damageSmoke;
		int count = Math.Min(smoke.Count, _damageSmokeMulti.InstanceCount);
		if (count == 0)
		{
			if (_damageSmokeView.Visible)
			{
				_damageSmokeMulti.VisibleInstanceCount = 0;
				_damageSmokeView.Visible = false;
			}
			return;
		}
		_damageSmokeView.Visible = true;
		NVector3 eye = N(cameraPosition);
		for (int i = 0; i < count; i++)
		{
			DamagePuff puff = smoke[i];
			// Las de B · ENVUELTO, con su nave de ahora; si ya no está, en el sitio de siempre y sin verse
			_smokeWorld[i] = puff.Wrap && _smokePoses.TryGetValue(puff.ShipId, out ShipPose pose)
				? DamageSmoke.WorldPosition(puff, pose)
				: puff.Position;
			_smokeOrder[i] = i;
			_smokeDepth[i] = -NVector3.DistanceSquared(_smokeWorld[i], eye);
		}
		Array.Sort(_smokeDepth, _smokeOrder, 0, count);
		float[] buffer = _damageSmokeBuffer;
		for (int q = 0; q < count; q++)
		{
			int index = _smokeOrder[q];
			DamagePuff p = smoke[index];
			NVector3 at = _smokeWorld[index];
			NVector3 col = smoke.Color(p);
			float opacity = p.Wrap && !_smokePoses.ContainsKey(p.ShipId) ? 0f : smoke.Opacity(p);
			int o = q * SmokeStride;
			// Transformación (base identidad y el centro), color y datos propios: giro, nube y radio
			buffer[o] = 1f; buffer[o + 1] = 0f; buffer[o + 2] = 0f; buffer[o + 3] = at.X;
			buffer[o + 4] = 0f; buffer[o + 5] = 1f; buffer[o + 6] = 0f; buffer[o + 7] = at.Y;
			buffer[o + 8] = 0f; buffer[o + 9] = 0f; buffer[o + 10] = 1f; buffer[o + 11] = at.Z;
			buffer[o + 12] = col.X; buffer[o + 13] = col.Y; buffer[o + 14] = col.Z; buffer[o + 15] = opacity;
			buffer[o + 16] = DamageSmoke.RotationOf(p); buffer[o + 17] = p.Texture; buffer[o + 18] = smoke.Radius(p); buffer[o + 19] = 0f;
		}
		RenderingServer.MultimeshSetBuffer(_damageSmokeMulti.GetRid(), new ReadOnlySpan<float>(buffer));
		_damageSmokeMulti.VisibleInstanceCount = count;
	}

	// Fuego del nivel 3: en cada foco, su llama y su halo, con la nave que se ve en su último paso (va con la nave)
	private void DrawFire()
	{
		DamageFireSettings s = _settings.DamageSmoke.Fire;
		int flames = 0, halos = 0;
		foreach (KeyValuePair<int, (Transform3D Hull, DamageSmokeFoci Foci, NVector3 Velocity)> pair in _fires)
		{
			if (halos + 3 > _haloMulti.InstanceCount)
				break;
			Transform3D hull = pair.Value.Hull;
			float time = _fireTime + DamageFire.Phase(pair.Key, s);
			// Hacia atrás: contra su velocidad en planta, o contra el morro si va parada
			NVector3 v = pair.Value.Velocity;
			var plan = new NVector3(v.X, 0f, v.Z);
			NVector3 back = plan.LengthSquared() > 0.25f
				? -NVector3.Normalize(plan)
				: NVector3.Normalize(new NVector3(hull.Basis.Z.X, 0f, hull.Basis.Z.Z) + new NVector3(0f, 0f, 1e-6f));
			float speed = v.Length();
			for (int f = 0; f < 3; f++)
			{
				SmokeFocus focus = DamageSmokeFocusRules.At(f, pair.Value.Foci, _settings.DamageSmoke);
				Vector3 at = hull * G(focus.FireBase);
				FireSprite flame = DamageFire.Flame(f, focus.Size, time, speed, s);
				Vector3 axis = G(DamageFire.Axis(back, flame.Lean, s));
				WriteFire(_flameBuffer, flames++, at + axis * (0.5f * flame.Length - DamageFire.Sink(flame.Lean, focus.Size, s)), axis,
					NVector3.One, flame);
				FireSprite halo = DamageFire.Halo(f, focus.Size, time, s);
				WriteFire(_haloBuffer, halos++, at + Vector3.Up * halo.Rise, Vector3.Up, s.HaloColor, halo);
			}
		}
		Show(_flameView, _flameMulti, _flameBuffer, flames);
		Show(_haloView, _haloMulti, _haloBuffer, halos);
	}

	// La transformación lleva el eje de la llama como su Y (el resto, de la identidad): el shader lo lee de ahí
	private static void WriteFire(float[] buffer, int index, Vector3 position, Vector3 axis, NVector3 color, in FireSprite sprite)
	{
		int o = index * FireStride;
		buffer[o] = 1f; buffer[o + 1] = axis.X; buffer[o + 2] = 0f; buffer[o + 3] = position.X;
		buffer[o + 4] = 0f; buffer[o + 5] = axis.Y; buffer[o + 6] = 0f; buffer[o + 7] = position.Y;
		buffer[o + 8] = 0f; buffer[o + 9] = axis.Z; buffer[o + 10] = 1f; buffer[o + 11] = position.Z;
		buffer[o + 12] = color.X; buffer[o + 13] = color.Y; buffer[o + 14] = color.Z; buffer[o + 15] = sprite.Opacity;
		buffer[o + 16] = sprite.Width * 0.5f; buffer[o + 17] = sprite.Length * 0.5f;
		buffer[o + 18] = sprite.NoiseTime; buffer[o + 19] = sprite.Seed;
	}

	private static void Show(MultiMeshInstance3D view, MultiMesh multi, float[] buffer, int count)
	{
		if (count == 0)
		{
			if (view.Visible)
			{
				multi.VisibleInstanceCount = 0;
				view.Visible = false;
			}
			return;
		}
		view.Visible = true;
		RenderingServer.MultimeshSetBuffer(multi.GetRid(), new ReadOnlySpan<float>(buffer));
		multi.VisibleInstanceCount = count;
	}

	// Brasas: un trazo corto cada una, de su color a negro, con la cola según su velocidad relativa a tu nave
	private void DrawEmbers()
	{
		DamageEmbers embers = _damageEmbers;
		_emberMesh.ClearSurfaces();
		if (embers.Count == 0)
		{
			_emberView.Visible = false;
			return;
		}
		_emberView.Visible = true;
		_emberMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
		for (int i = 0; i < embers.Count; i++)
		{
			DamageEmber e = embers[i];
			NVector3 color = embers.ColorOf(e);
			_emberMesh.SurfaceSetColor(new Color(color.X, color.Y, color.Z));
			_emberMesh.SurfaceAddVertex(G(e.Position));
			_emberMesh.SurfaceSetColor(Colors.Black);
			_emberMesh.SurfaceAddVertex(G(e.Position + embers.Tail(e, _reference)));
		}
		_emberMesh.SurfaceEnd();
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

	private static void Quad(Vector3[] v, Color[] c, Vector2[] u, int q, Vector3 center, Vector3 right, Vector3 up, float half, Color color) =>
		Quad(v, c, u, q, center, right, up, half, half, color);

	// Con alto y ancho distintos (el polvo del aterrizaje, más ancho que alto)
	private static void Quad(Vector3[] v, Color[] c, Vector2[] u, int q, Vector3 center, Vector3 right, Vector3 up, float halfWidth,
		float halfHeight, Color color)
	{
		Vector3 r = right * halfWidth;
		Vector3 t = up * halfHeight;
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

	// Siempre los mismos dos arrays de Godot (con y sin UV): crear uno por malla y frame dejaba basura todo el rato
	// mientras hubiera efectos (con el humo de daños, toda la carrera) y adelantaba las pausas del recolector
	private readonly Godot.Collections.Array _surfaceLines = NewSurfaceArrays();
	private readonly Godot.Collections.Array _surfaceQuads = NewSurfaceArrays();
	// También los opcionales de AddSurfaceFromArrays: sin pasarlos, Godot crea los suyos en cada llamada
	private readonly Godot.Collections.Array<Godot.Collections.Array> _noBlendShapes = new();
	private readonly Godot.Collections.Dictionary _noLods = new();

	private static Godot.Collections.Array NewSurfaceArrays()
	{
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		return arrays;
	}

	private void Commit(ArrayMesh mesh, Mesh.PrimitiveType primitive, Vector3[] v, Color[] c, Vector2[] u)
	{
		Godot.Collections.Array arrays = u != null ? _surfaceQuads : _surfaceLines;
		arrays[(int)Mesh.ArrayType.Vertex] = v;
		arrays[(int)Mesh.ArrayType.Color] = c;
		if (u != null)
			arrays[(int)Mesh.ArrayType.TexUV] = u;
		mesh.ClearSurfaces();
		mesh.AddSurfaceFromArrays(primitive, arrays, _noBlendShapes, _noLods);
	}

	private static Vector3 G(NVector3 v) => new(v.X, v.Y, v.Z);
	private static NVector3 N(Vector3 v) => new(v.X, v.Y, v.Z);
	private static Color C(NVector3 v) => new(v.X, v.Y, v.Z);
}
