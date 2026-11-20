using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Vectory.Ship;

// Luces del casco 3D (grupo Casco 3D del Inspector del Player). Las del prototipo como punto de partida (spec
// docs/cascos-3d-1-vector.md, 4.3), entre π: three.js divide la luz difusa entre π y Godot no, así que con el mismo
// número Godot pinta unas tres veces más fuerte. Se ajustan a ojo con el tonemap y el glow del juego.
public sealed class Hull3DSettings
{
	public float KeyEnergy { get; init; } = 2.4f / Mathf.Pi;
	public float RimEnergy { get; init; } = 1.3f / Mathf.Pi;
	public float FillEnergy { get; init; } = 0.55f / Mathf.Pi;
	public float AmbientEnergy { get; init; } = 0.3f;
	// Fuerza del cambio de color de perla y camaleón
	public float IridescenceStrength { get; init; } = 1f;
	// Anticolisión (solo los cascos que la tienen: BLADE)
	public StrobeSettings Strobe { get; init; } = new();
}

// Casco 3D de una nave (specs docs/cascos-3d-1-vector.md, docs/cascos-3d-2-blade.md y docs/cascos-3d-3-hammer.md, 4, 5 y 6): carga el .glb, aplica
// lo que dice su manifiesto y lo ilumina con sus propias luces, que solo tocan su capa visual. Las piezas que busca salen
// del contrato de su estilo (HullContract). Recibe pintura, daño, parpadeo y levitación y los pinta; sin reglas. Cada
// instancia lleva sus propios materiales, así dos naves pueden ir pintadas distinto a la vez.
public partial class ShipHull3D : Node3D
{
	// Nombres de acciones y de parámetros de shader creados una vez: con un texto, cada llamada crea un StringName
	// nuevo (con finalizador), y en cada frame eso alargaba las pausas del recolector de .NET
	private static readonly StringName SnStrength = "strength";
	private static readonly StringName SnWorldToHull = "world_to_hull";
	private static readonly StringName SnWear = "wear";
	private static readonly StringName SnExtent = "extent";
	private static readonly StringName SnSoot0 = "soot0";
	private static readonly StringName SnSoot1 = "soot1";
	private static readonly StringName SnSoot2 = "soot2";
	private static readonly StringName SnSootStrength = "soot_strength";
	private static readonly StringName SnCracks = "cracks";
	private static readonly StringName SnCrackReach = "crack_reach";
	private static readonly StringName SnCrackTime = "crack_time";

	// Capa visual propia del casco: sus luces solo afectan a ella y solo ella proyecta su sombra
	public const uint HullLayer = 1u << 10;
	// Capa de los cascos de los rivales, con su propio juego de luces común (spec docs/rivales-1-parrilla.md)
	public const uint RivalLayer = 1u << 11;



	private static readonly Color HemisphereSky = Color.FromHtml("#B0B8C8");
	private static readonly Color HemisphereGround = Color.FromHtml("#101010");

	// Daño visible (spec docs/efectos-5-danos-casco.md): una pasada encima de la pintura, en los ejes del casco
	// (world_to_hull, la inversa de la raíz del modelo: las piezas traen cada una su transformación). Rozaduras de metal
	// en la zona que ha chocado (izquierda, derecha, morro, popa), hollín alrededor de los focos del humo y grietas
	// incandescentes con daño 3. Sin nada que pintar, descarta el píxel.
	private const string WearShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;
uniform mat4 world_to_hull;
uniform vec4 wear = vec4(0.0);
uniform vec3 extent = vec3(2.6, 4.0, 3.3);
uniform vec4 soot0 = vec4(0.0);
uniform vec4 soot1 = vec4(0.0);
uniform vec4 soot2 = vec4(0.0);
uniform vec3 soot_strength = vec3(0.0);
uniform float crack_reach = 0.6;
uniform float cracks = 0.0;
uniform float crack_time = 0.0;
uniform vec3 metal_color : source_color = vec3(0.80, 0.81, 0.82);
uniform vec3 scuff_color : source_color = vec3(0.22, 0.21, 0.20);
uniform vec3 soot_color : source_color = vec3(0.022, 0.018, 0.016);
uniform vec3 crack_color : source_color = vec3(1.0, 0.36, 0.07);
varying vec3 hp;
varying vec3 hn;
float hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float vnoise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1.0, 0.0, 0.0)), f.x),
			mix(hash(i + vec3(0.0, 1.0, 0.0)), hash(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
		mix(mix(hash(i + vec3(0.0, 0.0, 1.0)), hash(i + vec3(1.0, 0.0, 1.0)), f.x),
			mix(hash(i + vec3(0.0, 1.0, 1.0)), hash(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
}
float fbm(vec3 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * vnoise(p);
		p = p * 2.02 + vec3(3.1, 1.7, 5.3);
		a *= 0.5;
	}
	return v;
}
float soot_of(vec4 s, float strength, float n) {
	if (s.w <= 0.0) return 0.0;
	float d = length(hp - s.xyz) + (n - 0.5) * s.w * 0.7;
	return strength * (1.0 - smoothstep(s.w * 0.3, s.w, d));
}
float crack_of(vec4 s) {
	if (s.w <= 0.0) return 0.0;
	float d = length(hp - s.xyz);
	return 1.0 - smoothstep(s.w * crack_reach * 0.5, s.w * crack_reach, d);
}
vec4 over(vec4 dst, vec3 col, float k) {
	float a = k + dst.a * (1.0 - k);
	if (a <= 0.0) return vec4(0.0);
	return vec4((col * k + dst.rgb * dst.a * (1.0 - k)) / a, a);
}
void vertex() {
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	hp = (world_to_hull * vec4(w, 1.0)).xyz;
	hn = normalize(mat3(world_to_hull) * normalize(mat3(MODEL_MATRIX) * NORMAL));
}
void fragment() {
	// Zonas: costados por el lado y lo de lado que mira la superficie (sin el signo de la normal: algunas piezas la
	// traen hacia dentro, y por eso la pasada va por las dos caras); morro y popa por el largo
	float facing = smoothstep(0.05, 0.45, abs(hn.x));
	float left = wear.x * facing * smoothstep(0.05, 0.3, -hp.x);
	float right = wear.y * facing * smoothstep(0.05, 0.3, hp.x);
	float front = wear.z * smoothstep(0.5, 0.85, -hp.z / extent.y);
	float rear = wear.w * smoothstep(0.5, 0.85, hp.z / extent.z);
	float zone = max(max(left, right), max(front, rear));
	// Rozaduras: manchas que crecen con el desgaste y, dentro, arañazos largos a lo largo de la nave
	float patch = fbm(hp * vec3(2.0, 3.0, 0.5));
	float covered = smoothstep(1.0 - zone * 0.75, 1.08 - zone * 0.75, patch);
	// Arañazos largos a lo largo de la nave (de unos centímetros de ancho, para que se vean de lejos)
	float streak = vnoise(vec3(hp.z * 0.6, (hp.y + hp.x) * 18.0, 0.0)) * 0.6 + vnoise(vec3(hp.z * 1.8, (hp.y - hp.x) * 40.0, 1.0)) * 0.4;
	float lines = smoothstep(0.6, 0.66, streak) * covered;
	vec4 c = vec4(0.0);
	c = over(c, scuff_color, covered * 0.2);
	c = over(c, metal_color, lines);
	float metal = lines;
	// Hollín alrededor de los focos
	float n = fbm(hp * 3.2);
	float soot = max(soot_of(soot0, soot_strength.x, n), max(soot_of(soot1, soot_strength.y, n), soot_of(soot2, soot_strength.z, n)));
	c = over(c, soot_color, soot);
	metal *= 1.0 - soot;
	// Grietas incandescentes (nivel 3), dentro del hollín
	float reach = max(crack_of(soot0), max(crack_of(soot1), crack_of(soot2))) * cracks;
	float v = abs(fbm(hp * 7.5) - 0.5);
	float line = (1.0 - smoothstep(0.006, 0.018, v)) * reach;
	float pulse = 0.6 + 0.4 * sin(crack_time * 5.0 + fbm(hp * 1.7) * 12.0);
	c = over(c, vec3(0.04, 0.03, 0.025), line);
	if (c.a < 0.004) discard;
	ALBEDO = c.rgb;
	ALPHA = c.a;
	// Metal claro y poco metálico: muy metálico solo reflejaría el entorno oscuro y saldría negro
	METALLIC = metal * 0.35;
	ROUGHNESS = mix(0.85, 0.45, metal);
	EMISSION = crack_color * line * pulse * 2.2;
}";

	// Cambio de color con el ángulo de vista (capa fina): una pasada que suma sobre el material
	private const string IridescenceShader = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never;
uniform float strength = 0.0;
void fragment() {
	float c = clamp(abs(dot(normalize(NORMAL), normalize(VIEW))), 0.0, 1.0);
	float t = 1.0 - c;
	vec3 film = 0.5 + 0.5 * cos(6.28318 * (vec3(0.0, 0.33, 0.67) + t * 1.6 + 0.12));
	float edge = 0.08 + 0.92 * pow(t, 2.0);
	ALBEDO = film * edge * strength;
}";

	private readonly Dictionary<string, MeshInstance3D> _pieces = new();
	private readonly Dictionary<string, Node3D> _empties = new();
	private readonly Dictionary<string, float> _lightBase = new();
	private readonly List<StandardMaterial3D> _scorchMaterials = new();
	private readonly List<ShaderMaterial> _irisPasses = new();
	// Pasadas de iridiscencia de los materiales que la llevan en el manifiesto, con su factor
	private readonly List<(ShaderMaterial Pass, float Factor)> _materialIris = new();
	// Camuflaje ya horneado por máscara y tonos: lo comparten todas las instancias con la misma pintura
	private static readonly Dictionary<string, ImageTexture> CamoTextures = new();
	// Pintura de HAMMER ya horneada por máscara y color: igual, compartida
	private static readonly Dictionary<string, ImageTexture> PlateTextures = new();
	private HullManifest _manifest;
	private HullLightRig _lights;
	private bool _built;   // ya pasó por _Ready: luces creadas
	private Hull3DSettings _settings = new();
	private HullPaintChoice _paint = HullPaintCatalog.Default;
	// Combinación de pintura del catálogo de su estilo; -1 = la de por defecto
	private int _paintIndex = -1;
	// Puntas traseras de BLADE pintadas (sin marcas): copia de la pintura con la repetición de sus UV, que van
	// horneadas con la de las franjas de aviso
	private readonly Dictionary<string, (StandardMaterial3D Hazard, StandardMaterial3D Paint)> _tips = new();
	// Bordes del canard de HAMMER: franjas con marcas; sin ellas, copia de la chapa oscura con la escala de sus UV
	private readonly Dictionary<string, (StandardMaterial3D Hazard, StandardMaterial3D Plate)> _leadingEdges = new();
	private int _paintVersion;
	private int _scorchLevel;
	// Cristal de la cabina por fuera (spec docs/efectos-5-danos-casco.md, 7): sus pasadas y la semilla de sus grietas
	private readonly List<ShaderMaterial> _glassPasses = new();
	private readonly int _glassSeed = System.Random.Shared.Next();
	private int _glassLevel;
	private static readonly CockpitGlassSettings GlassSettings = CockpitGlassSettings.Hull;
	// Daño visible: la pasada (una por casco, en todos los materiales pintados), la raíz del modelo, focos y medidas
	private ShaderMaterial _wearPass;
	private Node3D _modelRoot;
	private DamageSmokeFoci _foci;
	private HullWearSettings _wearSettings = HullWearSettings.Default;
	private Vector4 _wear;
	private float _wearTime;
	private bool _wearActive;
	// Copia del morro en la cabina (spec docs/cabinas-3d-4-morros.md, 4.2) y su pasada del daño, con sus propios ejes
	private HullNoseCopy _noseCopy;
	private ShaderMaterial _copyWearPass;
	private HullLightRig _copyLights;

	public ShipHull3DProfile Profile { get; set; }

	// En carrera las luces van colgadas de la nave; en el holograma, fijas en el mundo (respecto a la pantalla)
	public bool LightsFollowShip { get; set; } = true;

	// Capa visual de sus piezas y si lleva sus propias luces (los rivales no: comparten un HullLightRig en RivalLayer).
	// Antes de _Ready.
	public uint Layer { get; set; } = HullLayer;
	public bool OwnLights { get; set; } = true;

	// Manifiesto de un casco 3D; null si no se puede leer
	public static HullManifest LoadManifest(ShipHull3DProfile profile)
	{
		string json = FileAccess.GetFileAsString(profile.ManifestPath);
		return string.IsNullOrEmpty(json) ? null : HullManifest.Parse(json);
	}

	public bool IsLoaded { get; private set; }

	// Número de luces que titilan con el daño (14 en VECTOR, 9 en BLADE, 12 en HAMMER)
	public int FlickerLightCount => Profile.Contract.FlickerLights.Count;

	public override void _Ready()
	{
		IsLoaded = LoadModel();
		BuildLights();
		_built = true;
		ApplySettings(_settings);
		if (IsLoaded)
			SetPaint(_paintIndex);
		// La pasada del daño va puesta desde el principio (sin nada que pintar): así su shader se compila al cargar y el
		// primer golpe no da un tirón
		AttachWear();
		AttachGlass();
		ApplyScorch();
		SyncNoseCopy();
		// Al final del árbol: la WorldEnvironment de la escena se crea en el _Ready de Main, después de este nodo
		CallDeferred(MethodName.SetUpWorldReflections);
	}

	public void ApplySettings(Hull3DSettings settings)
	{
		_settings = settings;
		if (!_built)
			return;
		_lights?.Apply(settings);
		_copyLights?.Apply(settings);
		ApplyAmbient();
		ApplyIridescence();
	}

	// Punto vacío del modelo (bocas, estelas, humo) en coordenadas de la nave; cero si no está
	public Vector3 EmptyPosition(string name) => _empties.TryGetValue(name, out Node3D node) ? node.Position : Vector3.Zero;

	// ---------------------------------------------------------------
	// Pintura y librea (spec 6.2 y 6.3)
	// ---------------------------------------------------------------

	// Una de las 40 combinaciones del catálogo de su estilo (HullPaints); -1 = la de por defecto
	public void SetPaint(int combination)
	{
		int index = combination < 0 ? HullPaints.DefaultIndex(Profile.Style) : HullPaints.Step(Profile.Style, combination, 0);
		_paintIndex = index;
		if (Profile.Style == HullStyle.Military)
		{
			if (IsLoaded)
				ApplyMilitaryPaint(MilitaryPaintCatalog.Combinations[index]);
			return;
		}
		if (Profile.Style == HullStyle.Industrial)
		{
			if (IsLoaded)
				ApplyIndustrialPaint(IndustrialPaintCatalog.Combinations[index]);
			return;
		}
		_paint = HullPaintCatalog.Combinations[index];
		if (!IsLoaded)
			return;
		HullPaintLook look = HullPaintLook.For(_paint);

		// las tomas dorsales del retro, con la pintura del casco (spec docs/efectos-8-luces.md, 3.4)
		foreach (string name in new[] { "Fuselage_Upper", "Pod_L_Cowl", "Pod_R_Cowl", "Retro_L_Scoop", "Retro_R_Scoop" })
			ApplyFinish(MaterialOf(name), look.PaintColor, look.PaintFinish);
		foreach (string name in new[] { "Endplate_L", "Endplate_R" })
			ApplyFinish(MaterialOf(name), look.EndplateColor, look.EndplateFinish);
		foreach (string name in HullPieces.Stripes)
			ApplyFinish(MaterialOf(name), look.StripeColor, HullPaintLook.StripeFinish);
		foreach (string name in HullPieces.Pins)
			ApplyFinish(MaterialOf(name), look.PinColor, HullPaintLook.StripeFinish);

		foreach (string name in HullPieces.AccentEdges.Concat(HullPieces.AccentLights).Concat(HullPieces.TailLights).Concat(HullPieces.NozzleLights))
		{
			StandardMaterial3D material = MaterialOf(name);
			if (material != null)
				material.Emission = HullDecalPainter.ToColor(look.LightColor(name));
		}

		foreach (string name in HullPieces.LiveryPieces)
		{
			if (_pieces.TryGetValue(name, out MeshInstance3D mesh))
				mesh.Visible = look.LiveryVisible;
		}
		ApplyIridescence();
		if (look.LiveryVisible)
			PaintDecals(HullDecalDrawing.For(look));
	}

	private static void ApplyFinish(StandardMaterial3D material, uint color, HullFinish finish)
	{
		if (material == null)
			return;
		material.AlbedoColor = HullDecalPainter.ToColor(color);
		material.Metallic = finish.Metal;
		material.RoughnessTexture = null;
		material.Roughness = finish.Roughness;
		material.ClearcoatEnabled = finish.Coat > 0f;
		material.Clearcoat = finish.Coat;
		material.ClearcoatRoughness = finish.CoatRoughness;
	}

	// Las calcomanías se pintan una vez por pintura; las de los dos lados comparten textura
	private void PaintDecals(IReadOnlyDictionary<string, DecalCanvas> decals)
	{
		int version = ++_paintVersion;
		var done = new Dictionary<DecalCanvas, List<string>>();
		foreach ((string piece, DecalCanvas canvas) in decals)
		{
			if (!done.TryGetValue(canvas, out List<string> pieces))
				done[canvas] = pieces = new List<string>();
			pieces.Add(piece);
		}
		foreach ((DecalCanvas canvas, List<string> pieces) in done)
		{
			HullDecalPainter.Paint(this, canvas, texture =>
			{
				if (version != _paintVersion)
					return;   // llegó tarde: ya hay otra pintura
				foreach (string piece in pieces)
				{
					StandardMaterial3D material = MaterialOf(piece);
					if (material != null)
					{
						material.AlbedoTexture = texture;
						material.AlbedoColor = Colors.White;
					}
				}
			});
		}
	}

	// Pintura de BLADE (spec 6.2 y 6.3): el camuflaje sale de la máscara de cada material con los tonos de la pintura,
	// con su acabado; la panza y el suelo de la cabina, de color liso. Con marcas se ven las calcomanías y las puntas
	// llevan las franjas de aviso. Antirreflejos, metal, titanio, cristal, sensores y luces no cambian.
	private void ApplyMilitaryPaint(MilitaryPaintChoice choice)
	{
		MilitaryHullLook look = MilitaryHullLook.For(choice);
		foreach (string name in MilitaryHullPieces.Floor)
		{
			StandardMaterial3D material = MaterialOf(name);
			if (material != null)
				material.AlbedoColor = HullDecalPainter.ToColor(look.Floor);
		}
		Texture2D camo = CamoTexture("M_Paint", look.Tones);
		foreach (string name in MilitaryHullPieces.Painted)
			ApplyCamo(MaterialOf(name), camo, look.Finish, look.Finish.Metal);
		Texture2D armor = CamoTexture("M_PaintArmor", look.ArmorTones);
		foreach (string name in MilitaryHullPieces.Armor)
			ApplyCamo(MaterialOf(name), armor, look.Finish, look.ArmorMetal);
		foreach (string name in MilitaryHullPieces.Bottom)
		{
			StandardMaterial3D material = MaterialOf(name);
			if (material == null)
				continue;
			material.AlbedoColor = HullDecalPainter.ToColor(look.Bottom);
			material.RoughnessTexture = null;
			material.Roughness = MilitaryHullPaint.BottomRoughness;
		}
		foreach (KeyValuePair<string, (StandardMaterial3D Hazard, StandardMaterial3D Paint)> tip in _tips)
		{
			ApplyCamo(tip.Value.Paint, camo, look.Finish, look.Finish.Metal);
			_pieces[tip.Key].SetSurfaceOverrideMaterial(0, look.TipsHazard ? tip.Value.Hazard : tip.Value.Paint);
		}
		foreach (string name in MilitaryHullPieces.Decals)
		{
			if (_pieces.TryGetValue(name, out MeshInstance3D mesh))
				mesh.Visible = look.MarkingsVisible;
		}
		if (look.MarkingsVisible)
			PaintDecals(MilitaryDecalDrawing.For(look));
		SyncNoseCopy();
	}

	private static void ApplyCamo(StandardMaterial3D material, Texture2D camo, MilitaryFinish finish, float metal)
	{
		if (material == null)
			return;
		if (camo != null)
		{
			material.AlbedoTexture = camo;
			material.AlbedoColor = Colors.White;
		}
		material.Metallic = metal;
		material.Roughness = finish.RoughnessFactor;
		material.ClearcoatEnabled = finish.Coat > 0f;
		material.Clearcoat = finish.Coat;
		material.ClearcoatRoughness = finish.CoatRoughness;
	}

	// Copia de la pintura para cada punta, con las UV pasadas de la repetición de las franjas a la de la pintura
	private void PrepareTips(HullManifest manifest)
	{
		if (Profile.Style != HullStyle.Military)
			return;
		StandardMaterial3D paint = MaterialOf(MilitaryHullPieces.Painted[0]);
		float paintRepeat = manifest.RepeatOf("M_Paint");
		foreach (string name in MilitaryHullPieces.Tips)
		{
			StandardMaterial3D hazard = MaterialOf(name);
			if (paint == null || hazard == null)
				continue;
			var copy = (StandardMaterial3D)paint.Duplicate();
			copy.ResourceName = paint.ResourceName;
			float k = paintRepeat / manifest.RepeatOf(hazard.ResourceName);
			copy.Uv1Scale = new Vector3(k, k, 1f);
			_scorchMaterials.Add(copy);
			_tips[name] = (hazard, copy);
		}
	}

	// Textura de color horneada una vez desde la máscara sin pérdidas que hay junto al manifiesto (con mipmaps: así
	// los bordes duros del camuflaje no centellean de lejos). Si ya la calculó PrewarmPaints en otro hilo, solo se sube.
	private Texture2D CamoTexture(string material, uint[] tones)
	{
		HullManifestCamoMask entry = _manifest.CamoMasks.FirstOrDefault(m => m.Material == material);
		if (entry == null)
		{
			GD.PushError($"[Hull3D] No camo mask for {material} in {Profile.ManifestPath}");
			return null;
		}
		string path = Profile.ManifestPath.GetBaseDir().PathJoin(entry.Mask);
		string key = CamoKey(path, tones);
		if (CamoTextures.TryGetValue(key, out ImageTexture cached))
		{
			BakedImages.TryRemove(key, out _);   // por si el otro hilo la calculó también
			return cached;
		}
		if (BakedImages.TryRemove(key, out Image baked))
			return CamoTextures[key] = ImageTexture.CreateFromImage(baked);
		MaskData mask = Mask(path, Image.Format.Rgb8);
		if (mask == null)
		{
			GD.PushError($"[Hull3D] Cannot load camo mask {path}");
			return null;
		}
		return CamoTextures[key] = ImageTexture.CreateFromImage(BakeCamoImage(mask, tones));
	}

	private static string CamoKey(string maskPath, uint[] tones) => maskPath + ":" + string.Join(",", tones);

	private static Image BakeCamoImage(MaskData mask, uint[] tones)
	{
		Image image = Image.CreateFromData(mask.Width, mask.Height, false, Image.Format.Rgb8, MilitaryHullPaint.BakeCamo(mask.Data, tones));
		image.GenerateMipmaps();
		image.Convert(Image.Format.Rgba8);   // la GPU no tiene RGB8: así no se convierte al subirla, en el hilo principal
		return image;
	}

	// Pintura de HAMMER (spec 6.2 y 6.3): el color del casco y el de la cabina salen de sus máscaras (casco: M_Paint*;
	// cabina: M_PaintCab); la rugosidad y la normal son las del modelo, que no dependen del color. Con marcas se ven las
	// calcomanías y las franjas de las aletas y del compartimento, y el borde del canard lleva franjas; sin ellas, nada
	// de eso y el borde en chapa oscura.
	private void ApplyIndustrialPaint(IndustrialPaintChoice choice)
	{
		IndustrialHullLook look = IndustrialHullLook.For(choice);
		foreach (HullManifestPaintMask entry in _manifest.PaintMasks)
		{
			bool isCab = entry.Group == "cab";
			Texture2D texture = PlateTexture(entry, isCab ? look.Cab : look.Body);
			if (texture == null)
				continue;
			foreach (string name in isCab ? IndustrialHullPieces.PaintedCab : IndustrialHullPieces.PaintedBody)
			{
				StandardMaterial3D material = MaterialOf(name);
				if (material == null)
					continue;
				material.AlbedoTexture = texture;
				material.AlbedoColor = Colors.White;
			}
		}
		foreach (KeyValuePair<string, (StandardMaterial3D Hazard, StandardMaterial3D Plate)> edge in _leadingEdges)
			_pieces[edge.Key].SetSurfaceOverrideMaterial(0, look.LeadingEdgeHazard ? edge.Value.Hazard : edge.Value.Plate);
		foreach (string name in IndustrialHullPieces.LiveryPieces)
		{
			if (_pieces.TryGetValue(name, out MeshInstance3D mesh))
				mesh.Visible = look.MarkingsVisible;
		}
		if (look.MarkingsVisible)
			PaintDecals(IndustrialDecalDrawing.For(look));
		SyncNoseCopy();
	}

	// Copia de la chapa oscura para cada borde del canard, con las UV pasadas de la repetición de las franjas a la suya
	private void PrepareLeadingEdges(HullManifest manifest)
	{
		if (Profile.Style != HullStyle.Industrial)
			return;
		StandardMaterial3D plate = _pieces.Values.Select(m => m.GetSurfaceOverrideMaterial(0) as StandardMaterial3D)
			.FirstOrDefault(m => m != null && m.ResourceName == manifest.LeadingEdges.WithoutLivery);
		foreach (string name in manifest.LeadingEdges.Pieces)
		{
			StandardMaterial3D hazard = MaterialOf(name);
			if (plate == null || hazard == null)
				continue;
			var copy = (StandardMaterial3D)plate.Duplicate();
			copy.ResourceName = plate.ResourceName;
			float k = manifest.LeadingEdges.UvScaleWithoutLivery;
			copy.Uv1Scale = new Vector3(k, k, 1f);
			_scorchMaterials.Add(copy);
			_leadingEdges[name] = (hazard, copy);
		}
	}

	private static readonly string[][] NozzlePieces =
	{
		new[] { "Nozzle_Glow_L", "Nozzle_Core_L" },
		new[] { "Nozzle_Glow_R", "Nozzle_Core_R" }
	};

	// Boca de una tobera (0 izquierda, 1 derecha; spec docs/efectos-6-toberas.md, 4.6): fondo y núcleo luminosos con el
	// color medio de la llama y su emisión de siempre × factor (con potencia de crucero y sin boost, 1)
	public void SetNozzleGlow(int side, Color color, float factor)
	{
		string[] pieces = NozzlePieces[side];
		for (int i = 0; i < pieces.Length; i++)
		{
			StandardMaterial3D material = MaterialOf(pieces[i]);
			if (material == null || !_lightBase.TryGetValue(pieces[i], out float baseEnergy))
				continue;
			material.Emission = color;
			material.EmissionEnergyMultiplier = baseEnergy * factor;
		}
	}

	// Balizas del techo (HAMMER, spec 6.4): intensidad de cada lente según su papel; sin balizas no hace nada. No
	// titilan con el daño.
	public void SetBeacons(float boost, float damage)
	{
		if (_manifest == null)
			return;
		foreach (HullManifestBeacon beacon in _manifest.Beacons)
		{
			StandardMaterial3D material = MaterialOf(beacon.Name);
			if (material != null)
				material.EmissionEnergyMultiplier = beacon.Role == "boost" ? boost : damage;
		}
	}

	// Textura de color horneada una vez desde las dos máscaras sin pérdidas que hay junto al manifiesto. Si ya la calculó
	// PrewarmPaints en otro hilo, solo se sube.
	private Texture2D PlateTexture(HullManifestPaintMask entry, uint paint)
	{
		string dir = Profile.ManifestPath.GetBaseDir();
		string mulPath = dir.PathJoin(entry.Mul), addPath = dir.PathJoin(entry.Add);
		string key = PlateKey(mulPath, paint);
		if (PlateTextures.TryGetValue(key, out ImageTexture cached))
		{
			BakedImages.TryRemove(key, out _);   // por si el otro hilo la calculó también
			return cached;
		}
		if (BakedImages.TryRemove(key, out Image baked))
			return PlateTextures[key] = ImageTexture.CreateFromImage(baked);
		MaskData mul = Mask(mulPath, Image.Format.L8);
		MaskData add = Mask(addPath, Image.Format.Rgb8);
		if (mul == null || add == null || mul.Width != add.Width || mul.Height != add.Height)
		{
			GD.PushError($"[Hull3D] Cannot use paint masks {mulPath} and {addPath}");
			return null;
		}
		return PlateTextures[key] = ImageTexture.CreateFromImage(BakePlateImage(mul, add, paint));
	}

	private static string PlateKey(string mulPath, uint paint) => mulPath + ":" + paint.ToString("X6");

	private static Image BakePlateImage(MaskData mul, MaskData add, uint paint)
	{
		Image image = Image.CreateFromData(mul.Width, mul.Height, false, Image.Format.Rgb8, IndustrialHullPaint.Bake(mul.Data, add.Data, paint));
		image.GenerateMipmaps();
		image.Convert(Image.Format.Rgba8);   // la GPU no tiene RGB8: así no se convierte al subirla, en el hilo principal
		return image;
	}

	// Una máscara ya leída: se lee y se descomprime una sola vez por ejecución
	private sealed record MaskData(int Width, int Height, byte[] Data);

	private static readonly Dictionary<string, MaskData> Masks = new();

	private static MaskData Mask(string path, Image.Format format)
	{
		string key = path + ":" + format;
		if (Masks.TryGetValue(key, out MaskData cached))
			return cached;
		var source = GD.Load<Texture2D>(path);
		if (source == null)
			return null;
		Image image = source.GetImage();
		if (image.IsCompressed())
			image.Decompress();
		if (image.HasMipmaps())
			image.ClearMipmaps();
		image.Convert(format);
		return Masks[key] = new MaskData(image.GetWidth(), image.GetHeight(), image.GetData());
	}

	// Colores de camuflaje o de chapa calculados en otro hilo y aún sin subir, por clave
	private static readonly ConcurrentDictionary<string, Image> BakedImages = new();

	// Calcula en otro hilo los colores de las 20 pinturas de un casco que aún no estén hechos (BLADE y HAMMER; VECTOR no
	// hornea nada), para que cambiar de pintura en la pantalla PAINT no dé tirones. Las máscaras se leen aquí, en el
	// hilo principal; si se pide una pintura antes de que esté, se hornea en el momento como siempre.
	public static void PrewarmPaints(ShipHull3DProfile profile)
	{
		if (profile == null || profile.Style == HullStyle.Racing)
			return;
		HullManifest manifest = LoadManifest(profile);
		if (manifest == null)
			return;
		string dir = profile.ManifestPath.GetBaseDir();
		var jobs = new List<(string Key, System.Func<Image> Bake)>();
		var keys = new HashSet<string>();

		void Add(string key, System.Func<Image> bake)
		{
			if (keys.Add(key) && !CamoTextures.ContainsKey(key) && !PlateTextures.ContainsKey(key) && !BakedImages.ContainsKey(key))
				jobs.Add((key, bake));
		}

		if (profile.Style == HullStyle.Military)
		{
			foreach (MilitaryPaintChoice choice in MilitaryPaintCatalog.Combinations)
			{
				MilitaryHullLook look = MilitaryHullLook.For(choice);
				foreach ((string material, uint[] tones) in new[] { ("M_Paint", look.Tones), ("M_PaintArmor", look.ArmorTones) })
				{
					HullManifestCamoMask entry = manifest.CamoMasks.FirstOrDefault(m => m.Material == material);
					MaskData mask = entry == null ? null : Mask(dir.PathJoin(entry.Mask), Image.Format.Rgb8);
					if (mask != null)
						Add(CamoKey(dir.PathJoin(entry.Mask), tones), () => BakeCamoImage(mask, tones));
				}
			}
		}
		else
		{
			foreach (IndustrialPaintChoice choice in IndustrialPaintCatalog.Combinations)
			{
				IndustrialHullLook look = IndustrialHullLook.For(choice);
				foreach (HullManifestPaintMask entry in manifest.PaintMasks)
				{
					uint paint = entry.Group == "cab" ? look.Cab : look.Body;
					MaskData mul = Mask(dir.PathJoin(entry.Mul), Image.Format.L8);
					MaskData add = Mask(dir.PathJoin(entry.Add), Image.Format.Rgb8);
					if (mul != null && add != null && mul.Width == add.Width && mul.Height == add.Height)
						Add(PlateKey(dir.PathJoin(entry.Mul), paint), () => BakePlateImage(mul, add, paint));
				}
			}
		}

		if (jobs.Count == 0)
			return;
		Task.Run(() =>
		{
			foreach ((string key, System.Func<Image> bake) in jobs)
				BakedImages.TryAdd(key, bake());
		});
	}

	// Perla, camaleón, la cúpula de VECTOR y la bola de sensores de BLADE: cambio de color con el ángulo
	// (StandardMaterial3D no tiene iridiscencia)
	private void ApplyIridescence()
	{
		if (!IsLoaded)
			return;
		foreach ((ShaderMaterial pass, float factor) in _materialIris)
			pass.SetShaderParameter(SnStrength, IridescenceOfCanopy * factor * _settings.IridescenceStrength);
		if (Profile.Style == HullStyle.Racing)
		{
			float paint = _paint.Paint.Finish.Iridescence * _settings.IridescenceStrength;
			foreach (string name in new[] { "Fuselage_Upper", "Pod_L_Cowl", "Pod_R_Cowl", "Retro_L_Scoop", "Retro_R_Scoop" })
				SetIridescence(MaterialOf(name), paint);
			bool endplatesPainted = !_paint.WithLivery;
			foreach (string name in new[] { "Endplate_L", "Endplate_R" })
				SetIridescence(MaterialOf(name), endplatesPainted ? paint : 0f);
		}
		SyncNoseCopy();
	}

	// Fuerza de la pasada sobre la pintura y sobre la cúpula, a ojo con el prototipo
	private const float IridescenceOfPaint = 0.16f;
	private const float IridescenceOfCanopy = 0.18f;

	private void SetIridescence(StandardMaterial3D material, float amount)
	{
		if (material == null)
			return;
		if (amount <= 0f)
		{
			material.NextPass = _wearPass;
			return;
		}
		if (material.NextPass is not ShaderMaterial pass || pass == _wearPass)
		{
			pass = NewIridescencePass();
			material.NextPass = pass;
		}
		pass.NextPass = _wearPass;
		pass.SetShaderParameter(SnStrength, amount * IridescenceOfPaint);
	}

	private ShaderMaterial NewIridescencePass()
	{
		var pass = new ShaderMaterial { Shader = new Shader { Code = IridescenceShader } };
		_irisPasses.Add(pass);
		return pass;
	}

	// ---------------------------------------------------------------
	// Daño visible (spec 6.5)
	// ---------------------------------------------------------------

	// Nivel de daño (0–3; en TIME TRIAL, 0): hollín y grietas
	public void SetDamage(int level, bool timeTrial)
	{
		_scorchLevel = HullDamage.ScorchMask(level, timeTrial);
		ApplyScorch();
		ApplyGlass();
	}

	// Focos del humo de la nave (el hollín sale de ellos) y medidas de su caja (para morro, popa y costados)
	public void SetDamageShape(in DamageSmokeFoci foci, float halfWidth, float front, float back)
	{
		_foci = foci;
		_wearPass ??= NewWearPass();
		SetWearParameter(SnExtent, new Vector3(halfWidth, front, back));
		ApplyScorch();
	}

	// Rozaduras acumuladas: izquierda, derecha, morro y popa (0–1)
	public void SetWear(System.Numerics.Vector4 wear)
	{
		_wear = new Vector4(wear.X, wear.Y, wear.Z, wear.W);
		_wearPass ??= NewWearPass();
		SetWearParameter(SnWear, _wear);
		UpdateWearActive();
	}

	// Hollín y grietas del nivel de ahora
	private void ApplyScorch()
	{
		_wearPass ??= NewWearPass();
		Vector3 strength = Vector3.Zero;
		for (int f = 0; f < 3; f++)
		{
			SootSpot spot = HullWear.Soot(_scorchLevel, f, _foci, _wearSettings);
			_wearPass.SetShaderParameter(f == 0 ? SnSoot0 : f == 1 ? SnSoot1 : SnSoot2,
				new Vector4(spot.Center.X, spot.Center.Y, spot.Center.Z, spot.Radius));
			strength[f] = spot.Strength;
		}
		SetWearParameter(SnSootStrength, strength);
		SetWearParameter(SnCracks, HullWear.Cracks(_scorchLevel) ? 1f : 0f);
		SetWearParameter(SnCrackReach, _wearSettings.CrackReach);
		UpdateWearActive();
	}

	private void UpdateWearActive()
	{
		_wearActive = _scorchLevel >= 2 || _wear.X > 0f || _wear.Y > 0f || _wear.Z > 0f || _wear.W > 0f;
		if (_wearActive)
			UpdateWearTransform();
	}

	// La pasada sigue los ejes del casco: la inversa de la raíz del modelo, cada frame mientras haya algo que pintar
	public override void _Process(double delta)
	{
		if (!_wearActive || _wearPass == null)
			return;
		_wearTime += (float)delta;
		UpdateWearTransform();
		if (_scorchLevel >= 3)
			SetWearParameter(SnCrackTime, _wearTime);
	}

	private void UpdateWearTransform()
	{
		if (_modelRoot == null || !_modelRoot.IsInsideTree())
			return;
		_wearPass.SetShaderParameter(SnWorldToHull, new Projection(_modelRoot.GlobalTransform.AffineInverse()));
	}

	private ShaderMaterial NewWearPass() => new() { Shader = new Shader { Code = WearShader } };

	private void SetWearParameter(StringName name, Variant value)
	{
		_wearPass.SetShaderParameter(name, value);
		_copyWearPass?.SetShaderParameter(name, value);
	}

	// ---------------------------------------------------------------
	// Copia del morro en la cabina (spec docs/cabinas-3d-4-morros.md, 4.2)
	// ---------------------------------------------------------------

	// La cabina que lleva la copia de este casco; null, o una copia de otro casco, la quita
	public void SetNoseCopy(HullNoseCopy copy)
	{
		HullNoseCopy next = copy != null && copy.Matches(Profile) ? copy : null;
		if (next == _noseCopy)
			return;
		_noseCopy = next;
		_copyWearPass = null;
		_copyLights?.QueueFree();
		_copyLights = null;
		SyncNoseCopy();
		ApplyNoseEnvironment();
	}

	// Sus luces se quedan en el mundo de la cabina: se van con el casco (F10 crea otro)
	public override void _ExitTree()
	{
		_copyLights?.QueueFree();
		_copyLights = null;
	}

	// La cámara de la pasada de la copia, con el entorno de la carrera: sin tonemap, con su glow, sus reflejos y la luz
	// ambiente del casco
	private void ApplyNoseEnvironment()
	{
		if (_noseCopy?.Camera == null)
			return;
		Godot.Environment env = FindEnvironment();
		if (env != null)
			_noseCopy.Camera.Environment = (Godot.Environment)env.Duplicate();
	}

	// Pone en la copia lo que lleva ahora cada pieza del casco: si se ve y sus materiales. Las luces y las calcomanías
	// comparten el material del casco (así titilan, se encienden y se dibujan a la vez); el resto lleva una copia, con
	// la pasada del daño de la copia y sus reflejos. Solo al cargar y al cambiar de pintura: no cada frame.
	private void SyncNoseCopy()
	{
		if (_noseCopy == null || !IsLoaded || _wearPass == null)
			return;
		if (_copyWearPass == null)
		{
			_copyWearPass = (ShaderMaterial)_wearPass.Duplicate();
			_copyWearPass.SetShaderParameter(SnWorldToHull, new Projection(_noseCopy.WorldToHull));
		}
		if (_copyLights == null && _noseCopy.LightRoot != null)
		{
			_copyLights = new HullLightRig { Name = "HullLights", Layer = _noseCopy.Layer, FollowParent = true };
			_copyLights.Apply(_settings);
			_noseCopy.LightRoot.AddChild(_copyLights);
		}
		foreach ((MeshInstance3D mesh, string hullPiece) in _noseCopy.Pieces)
		{
			if (!_pieces.TryGetValue(hullPiece, out MeshInstance3D source))
				continue;
			mesh.Visible = source.Visible;
			int surfaces = Mathf.Min(mesh.GetSurfaceOverrideMaterialCount(), source.GetSurfaceOverrideMaterialCount());
			for (int i = 0; i < surfaces; i++)
				mesh.SetSurfaceOverrideMaterial(i, CopyMaterial(source.GetSurfaceOverrideMaterial(i)));
		}
	}

	private Material CopyMaterial(Material material)
	{
		if (material is not StandardMaterial3D source)
			return material;
		if (HullNoseCopyRules.SharesMaterial(source.EmissionEnabled,
				source.Transparency != BaseMaterial3D.TransparencyEnum.Disabled, source.NextPass != null))
			return source;
		var copy = (StandardMaterial3D)source.Duplicate();
		copy.ResourceName = source.ResourceName;
		copy.NextPass = CopyPass(source.NextPass);
		float reflections = _noseCopy.Reflections;
		if (reflections != 1f)
		{
			copy.MetallicSpecular = Mathf.Min(1f, copy.MetallicSpecular * reflections);
			copy.Metallic = Mathf.Min(1f, copy.Metallic * reflections);
		}
		return copy;
	}

	// La cadena de pasadas de un material de la copia: la del daño es la de la copia; las demás (iridiscencia), copias
	private Material CopyPass(Material pass)
	{
		if (pass == null)
			return null;
		if (pass == _wearPass)
			return _copyWearPass;
		if (pass is not ShaderMaterial shader)
			return pass;
		var copy = (ShaderMaterial)shader.Duplicate();
		copy.NextPass = CopyPass(shader.NextPass);
		return copy;
	}

	// Las piezas de cristal de la cabina: la cúpula de VECTOR y BLADE, las ventanas de HAMMER
	private void AttachGlass()
	{
		if (_manifest == null || _glassPasses.Count > 0)
			return;
		if (_manifest.Glass.Count > 0)
		{
			foreach (string name in _manifest.Glass)
				CockpitGlass.AttachHull(_pieces.GetValueOrDefault(name), _glassPasses);
		}
		else
			CockpitGlass.AttachHull(_pieces.GetValueOrDefault(HullPieces.Canopy), _glassPasses);   // VECTOR y BLADE la llaman igual
		_glassLevel = -1;
		ApplyGlass();
	}

	// Grietas del nivel de ahora; solo cuando cambia (rehace sus listas de marcas)
	private void ApplyGlass()
	{
		if (_glassLevel == _scorchLevel)
			return;
		_glassLevel = _scorchLevel;
		for (int i = 0; i < _glassPasses.Count; i++)
			CockpitGlass.Apply(_glassPasses[i], _scorchLevel, _glassSeed + i * 7919, GlassSettings);
	}

	// La pasada del daño detrás de cada material pintado (y detrás de su iridiscencia, si la tiene)
	private void AttachWear()
	{
		_wearPass ??= NewWearPass();
		foreach (StandardMaterial3D material in _scorchMaterials)
		{
			if (material.NextPass is ShaderMaterial iris && iris != _wearPass)
				iris.NextPass = _wearPass;
			else
				material.NextPass = _wearPass;
		}
	}

	// Parpadeo de las luces de su contrato: el mismo EdgeFlicker de los daños, con una "arista" por luz
	public void ShowLightFlicker(EdgeFlicker flicker)
	{
		int i = 0;
		foreach (string name in Profile.Contract.FlickerLights)
		{
			StandardMaterial3D material = MaterialOf(name);
			if (material != null && _lightBase.TryGetValue(name, out float baseEnergy))
				material.EmissionEnergyMultiplier = baseEnergy * (flicker.IsDimmed(i) ? HullDamage.DimFactor : 1f);
			i++;
		}
	}

	// Opacidad de todo el casco (1 = normal), para el parpadeo de la nave atravesable tras recolocarla (spec
	// docs/rivales-3-contacto.md, 5.2): la transparencia de cada malla, sin tocar sus materiales
	public void SetOpacity(float opacity)
	{
		float transparency = Mathf.Clamp(1f - opacity, 0f, 1f);
		foreach (MeshInstance3D mesh in _pieces.Values)
			mesh.Transparency = transparency;
	}

	// Cada frame: anticolisión (time: el de la nave, que la pausa congela)
	public void UpdateEffects(float time)
	{
		IReadOnlyList<string> strobes = Profile.Contract.Strobes;
		for (int i = 0; i < strobes.Count; i++)
		{
			StandardMaterial3D material = MaterialOf(strobes[i]);
			if (material != null)
				material.EmissionEnergyMultiplier = StrobeLights.Intensity(time, i, _settings.Strobe);
		}
	}

	// ---------------------------------------------------------------
	// Carga
	// ---------------------------------------------------------------

	private StandardMaterial3D MaterialOf(string piece) =>
		_pieces.TryGetValue(piece, out MeshInstance3D mesh) ? mesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D : null;

	private bool LoadModel()
	{
		var scene = GD.Load<PackedScene>(Profile.ModelPath);
		HullManifest manifest = LoadManifest(Profile);
		if (scene == null || manifest == null)
		{
			GD.PushError($"[Hull3D] Cannot load {Profile.ModelPath} or {Profile.ManifestPath}");
			return false;
		}
		_manifest = manifest;

		Node model = scene.Instantiate();
		AddChild(model);
		_modelRoot = model as Node3D;
		Collect(model);

		List<string> missing = HullManifest.Missing(_pieces.Keys, Profile.Contract)
			.Concat(HullContract.Empties.Where(e => !_empties.ContainsKey(e))).ToList();
		if (missing.Count > 0)
			GD.PushError("[Hull3D] Missing hull pieces: " + string.Join(", ", missing));

		ApplyManifest(manifest);
		PrepareTips(manifest);
		PrepareLeadingEdges(manifest);
		return missing.Count == 0;
	}

	private void Collect(Node node)
	{
		if (node is MeshInstance3D mesh)
			_pieces[mesh.Name] = mesh;
		else if (node is Node3D n && HullContract.Empties.Contains((string)node.Name))
			_empties[node.Name] = n;
		foreach (Node child in node.GetChildren())
			Collect(child);
	}

	// Lo que glTF no lleva o Godot no lee (spec, 4.1), con materiales propios de esta instancia
	private void ApplyManifest(HullManifest manifest)
	{
		var noShadow = new HashSet<string>(manifest.NoShadow);
		var scorch = new HashSet<string>(manifest.ScorchMaterials);
		var decals = new HashSet<string>(manifest.DecalMaterials);
		foreach (MeshInstance3D mesh in _pieces.Values)
		{
			mesh.Layers = Layer;
			mesh.CastShadow = noShadow.Contains(mesh.Name)
				? GeometryInstance3D.ShadowCastingSetting.Off
				: GeometryInstance3D.ShadowCastingSetting.On;
			for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
			{
				if (mesh.GetActiveMaterial(i) is not StandardMaterial3D source)
					continue;
				var material = (StandardMaterial3D)source.Duplicate();
				material.ResourceName = source.ResourceName;
				Prepare(material, manifest);
				if (scorch.Contains(material.ResourceName))
					_scorchMaterials.Add(material);
				if (decals.Contains(material.ResourceName))
					MakeDecal(material);
				float iris = manifest.IridescenceFactor(material.ResourceName);
				if (iris > 0f)
				{
					ShaderMaterial pass = NewIridescencePass();
					material.NextPass = pass;
					_materialIris.Add((pass, iris));
				}
				mesh.SetSurfaceOverrideMaterial(i, material);
			}
		}

		foreach (CockpitManifestPiece piece in manifest.Pieces.Where(p => p.Intensity.HasValue))
		{
			StandardMaterial3D material = MaterialOf(piece.Name);
			if (material != null)
			{
				material.EmissionEnabled = true;
				if (!string.IsNullOrEmpty(piece.Emission))
					material.Emission = Color.FromHtml(piece.Emission);
				material.EmissionEnergyMultiplier = piece.Intensity.Value;
				_lightBase[piece.Name] = piece.Intensity.Value;
			}
		}
	}

	private static void Prepare(StandardMaterial3D material, HullManifest manifest)
	{
		string name = material.ResourceName;
		// El metal es un valor por material, y la textura de rugosidad es gris: leída como metal, lo cambiaría
		material.MetallicTexture = null;

		HullManifestCoat coat = manifest.CoatOf(name);
		if (coat != null && coat.Coat > 0f)
		{
			material.ClearcoatEnabled = true;
			material.Clearcoat = coat.Coat;
			material.ClearcoatRoughness = coat.Roughness;
		}

		float reflection = manifest.ReflectionFactor(name);
		if (reflection != 1f)
		{
			// glTF no tiene "intensidad de los reflejos" y Godot tampoco por material: se cambia lo que refleja
			material.MetallicSpecular = Mathf.Min(1f, material.MetallicSpecular * reflection);
			material.Metallic = Mathf.Min(1f, material.Metallic * reflection);
		}

		CockpitManifestAdditive transparent = manifest.TransparentMaterials.FirstOrDefault(t => t.Material == name);
		if (transparent != null)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
			Color albedo = material.AlbedoColor;
			material.AlbedoColor = new Color(albedo.R, albedo.G, albedo.B, albedo.A < 1f ? albedo.A : transparent.Opacity);
		}
	}

	// Calcomanía: su dibujo con alfa, sin escribir profundidad, por encima de la pintura. Invisible hasta que llega
	// su dibujo (se pinta en el frame siguiente).
	private static void MakeDecal(StandardMaterial3D material)
	{
		material.AlbedoColor = new Color(1f, 1f, 1f, 0f);
		material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
		material.RenderPriority = 1;
	}

	// Colgadas de la nave: giran con ella, así el casco se ve igual en cualquier curva. Sin luces propias, nada.
	private void BuildLights()
	{
		if (!OwnLights)
			return;
		_lights = new HullLightRig { Name = "Lights", Layer = Layer, FollowParent = LightsFollowShip };
		_lights.Apply(_settings);
		AddChild(_lights);
	}

	// Reflejos y luz ambiente del casco: el cielo del entorno, solo para reflejos, con el fondo de color de siempre.
	// El resto del mundo de carrera no recibe luz (todo es unshaded), así que no cambia.
	private void SetUpWorldReflections()
	{
		Godot.Environment env = FindEnvironment();
		if (env == null)
			return;
		if (env.Sky == null)
		{
			env.Sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = GD.Load<Texture2D>(Profile.EnvironmentPath) } };
			// El centro del panorama mira a −Z del prototipo y en Godot el centro de la imagen cae en +Z
			env.SkyRotation = new Vector3(0f, Mathf.Pi, 0f);
		}
		env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
		env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		ApplyAmbient();
		ApplyNoseEnvironment();
	}

	// Hemisferio del prototipo en un solo color: la mitad de la luz llega de arriba y la mitad de abajo
	private void ApplyAmbient()
	{
		Godot.Environment env = FindEnvironment();
		if (env == null || env.AmbientLightSource != Godot.Environment.AmbientSource.Color)
			return;
		Color mixed = (HemisphereSky.SrgbToLinear() + HemisphereGround.SrgbToLinear()) * (0.5f * _settings.AmbientEnergy);
		env.AmbientLightColor = new Color(mixed.R, mixed.G, mixed.B).LinearToSrgb();
		env.AmbientLightEnergy = 1f;
	}

	private Godot.Environment FindEnvironment()
	{
		if (!IsInsideTree())
			return null;
		Godot.Environment env = GetViewport().FindWorld3D()?.Environment;
		if (env != null)
			return env;
		foreach (Node node in GetTree().Root.FindChildren("*", nameof(WorldEnvironment), true, false))
		{
			if (node is WorldEnvironment world && world.Environment != null)
				return world.Environment;
		}
		return null;
	}
}
