using System.Collections.Generic;
using System.Linq;
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
	// Brillo de levitación (1 = el del prototipo, 0 = apagado) y fuerza del cambio de color de perla y camaleón
	public float HoverGlowIntensity { get; init; } = 1f;
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
	// Capa visual propia del casco: sus luces solo afectan a ella y solo ella proyecta su sombra
	public const uint HullLayer = 1u << 10;

	private const float ShadowMaxDistance = 40f;
	private const float GlowLift = 0.03f;   // m por encima de la pista
	private const int SmokeMaxParticles = 40;

	private static readonly string[] ScorchPaths =
	{
		"res://assets/ships/hull_scorch_1.png", "res://assets/ships/hull_scorch_2.png", "res://assets/ships/hull_scorch_3.png"
	};
	private static readonly Texture2D[] ScorchTextures = new Texture2D[3];

	// Luces del prototipo: desde dónde llegan (hacia el centro de la nave), color y grosor de la sombra
	private static readonly Vector3 KeySource = new(4f, 10f, -5f);
	private static readonly Vector3 RimSource = new(-6f, 2.5f, 9f);
	private static readonly Vector3 FillSource = new(8f, 3f, 2f);
	private static readonly Color KeyColor = Color.FromHtml("#FFF1E0");
	private static readonly Color RimColor = Color.FromHtml("#FF9D50");
	private static readonly Color FillColor = Color.FromHtml("#5F8CFF");
	private static readonly Color HemisphereSky = Color.FromHtml("#B0B8C8");
	private static readonly Color HemisphereGround = Color.FromHtml("#101010");

	// Cambio de color con el ángulo de vista (capa fina): una pasada que suma sobre el material, atenuada en lo quemado
	private const string IridescenceShader = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never;
uniform float strength = 0.0;
uniform sampler2D scorch : hint_default_transparent, filter_linear_mipmap, repeat_enable;
varying vec2 uv_scorch;
void vertex() { uv_scorch = UV2; }
void fragment() {
	float c = clamp(abs(dot(normalize(NORMAL), normalize(VIEW))), 0.0, 1.0);
	float t = 1.0 - c;
	vec3 film = 0.5 + 0.5 * cos(6.28318 * (vec3(0.0, 0.33, 0.67) + t * 1.6 + 0.12));
	float edge = 0.08 + 0.92 * pow(t, 2.0);
	ALBEDO = film * edge * strength * (1.0 - texture(scorch, uv_scorch).a);
}";

	// Brillo bajo la nave: aditivo, caída radial (1 − d²)²
	private const string GlowShader = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;
uniform float strength = 0.32;
uniform vec3 tint = vec3(0.14, 0.36, 1.0);
void fragment() {
	vec2 d = (UV - 0.5) * 2.0;
	float k = max(0.0, 1.0 - dot(d, d));
	ALBEDO = tint * k * k * strength;
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
	private DirectionalLight3D _key;
	private DirectionalLight3D _rim;
	private DirectionalLight3D _fill;
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
	private CpuParticles3D _smoke;
	private MeshInstance3D _glow;
	private ShaderMaterial _glowMaterial;
	private int _scorchLevel;

	public ShipHull3DProfile Profile { get; set; }

	// En carrera las luces van colgadas de la nave; en el holograma, fijas en el mundo (respecto a la pantalla)
	public bool LightsFollowShip { get; set; } = true;

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
		BuildSmoke();
		BuildGlow();
		ApplySettings(_settings);
		if (IsLoaded)
			SetPaint(_paintIndex);
		// Las máscaras se preparan ya: así el primer golpe de la carrera no da un tirón
		for (int level = 1; level <= DamageModel.MaxLevel; level++)
			ScorchTexture(level);
		// Al final del árbol: la WorldEnvironment de la escena se crea en el _Ready de Main, después de este nodo
		CallDeferred(MethodName.SetUpWorldReflections);
	}

	public void ApplySettings(Hull3DSettings settings)
	{
		_settings = settings;
		if (_key == null)
			return;
		_key.LightEnergy = settings.KeyEnergy;
		_rim.LightEnergy = settings.RimEnergy;
		_fill.LightEnergy = settings.FillEnergy;
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

		foreach (string name in new[] { "Fuselage_Upper", "Pod_L_Cowl", "Pod_R_Cowl" })
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
	// los bordes duros del camuflaje no centellean de lejos)
	private Texture2D CamoTexture(string material, uint[] tones)
	{
		HullManifestCamoMask entry = _manifest.CamoMasks.FirstOrDefault(m => m.Material == material);
		if (entry == null)
		{
			GD.PushError($"[Hull3D] No camo mask for {material} in {Profile.ManifestPath}");
			return null;
		}
		string path = Profile.ManifestPath.GetBaseDir().PathJoin(entry.Mask);
		string key = path + ":" + string.Join(",", tones);
		if (CamoTextures.TryGetValue(key, out ImageTexture cached))
			return cached;
		var source = GD.Load<Texture2D>(path);
		if (source == null)
		{
			GD.PushError($"[Hull3D] Cannot load camo mask {path}");
			return null;
		}
		Image mask = source.GetImage();
		if (mask.IsCompressed())
			mask.Decompress();
		if (mask.HasMipmaps())
			mask.ClearMipmaps();
		mask.Convert(Image.Format.Rgb8);
		byte[] color = MilitaryHullPaint.BakeCamo(mask.GetData(), tones);
		Image image = Image.CreateFromData(mask.GetWidth(), mask.GetHeight(), false, Image.Format.Rgb8, color);
		image.GenerateMipmaps();
		return CamoTextures[key] = ImageTexture.CreateFromImage(image);
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

	// Textura de color horneada una vez desde las dos máscaras sin pérdidas que hay junto al manifiesto
	private Texture2D PlateTexture(HullManifestPaintMask entry, uint paint)
	{
		string dir = Profile.ManifestPath.GetBaseDir();
		string mulPath = dir.PathJoin(entry.Mul), addPath = dir.PathJoin(entry.Add);
		string key = mulPath + ":" + paint.ToString("X6");
		if (PlateTextures.TryGetValue(key, out ImageTexture cached))
			return cached;
		Image mul = MaskImage(mulPath, Image.Format.L8);
		Image add = MaskImage(addPath, Image.Format.Rgb8);
		if (mul == null || add == null || mul.GetSize() != add.GetSize())
		{
			GD.PushError($"[Hull3D] Cannot use paint masks {mulPath} and {addPath}");
			return null;
		}
		byte[] color = IndustrialHullPaint.Bake(mul.GetData(), add.GetData(), paint);
		Image image = Image.CreateFromData(mul.GetWidth(), mul.GetHeight(), false, Image.Format.Rgb8, color);
		image.GenerateMipmaps();
		return PlateTextures[key] = ImageTexture.CreateFromImage(image);
	}

	private static Image MaskImage(string path, Image.Format format)
	{
		var source = GD.Load<Texture2D>(path);
		if (source == null)
			return null;
		Image image = source.GetImage();
		if (image.IsCompressed())
			image.Decompress();
		if (image.HasMipmaps())
			image.ClearMipmaps();
		image.Convert(format);
		return image;
	}

	// Perla, camaleón, la cúpula de VECTOR y la bola de sensores de BLADE: cambio de color con el ángulo
	// (StandardMaterial3D no tiene iridiscencia)
	private void ApplyIridescence()
	{
		if (!IsLoaded)
			return;
		foreach ((ShaderMaterial pass, float factor) in _materialIris)
			pass.SetShaderParameter("strength", IridescenceOfCanopy * factor * _settings.IridescenceStrength);
		if (Profile.Style != HullStyle.Racing)
			return;
		float paint = _paint.Paint.Finish.Iridescence * _settings.IridescenceStrength;
		foreach (string name in new[] { "Fuselage_Upper", "Pod_L_Cowl", "Pod_R_Cowl" })
			SetIridescence(MaterialOf(name), paint);
		bool endplatesPainted = !_paint.WithLivery;
		foreach (string name in new[] { "Endplate_L", "Endplate_R" })
			SetIridescence(MaterialOf(name), endplatesPainted ? paint : 0f);
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
			material.NextPass = null;
			return;
		}
		if (material.NextPass is not ShaderMaterial pass)
		{
			pass = NewIridescencePass();
			material.NextPass = pass;
		}
		pass.SetShaderParameter("strength", amount * IridescenceOfPaint);
		pass.SetShaderParameter("scorch", ScorchTexture(_scorchLevel));
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

	// Nivel de daño (0–3; en TIME TRIAL, 0): quemaduras y humo
	public void SetDamage(int level, bool timeTrial)
	{
		_scorchLevel = HullDamage.ScorchMask(level, timeTrial);
		Texture2D scorch = ScorchTexture(_scorchLevel);
		foreach (StandardMaterial3D material in _scorchMaterials)
		{
			material.DetailEnabled = scorch != null;
			material.DetailAlbedo = scorch;
		}
		foreach (ShaderMaterial pass in _irisPasses)
			pass.SetShaderParameter("scorch", scorch);

		float rate = HullDamage.SmokePerSecond(level, timeTrial);
		if (_smoke != null)
		{
			_smoke.Emitting = rate > 0f;
			if (rate > 0f)
			{
				_smoke.Amount = Mathf.Clamp(Mathf.CeilToInt(rate * HullDamage.SmokeLifetime) + 1, 1, SmokeMaxParticles);
				_smoke.Color = HullDecalPainter.ToColor(HullDamage.SmokeColor(level));
			}
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

	// La recolocación borra el humo que había
	public void ClearSmoke()
	{
		if (_smoke == null)
			return;
		bool emitting = _smoke.Emitting;
		_smoke.Restart();
		_smoke.Emitting = emitting;
	}

	// Cada frame: brillo bajo la nave (sobre la pista, con su inclinación), tamaño del humo según la velocidad y
	// anticolisión (time: el de la nave, que la pausa congela)
	public void UpdateEffects(Transform3D glowTransform, float glowStrength, float speed, float time)
	{
		IReadOnlyList<string> strobes = Profile.Contract.Strobes;
		for (int i = 0; i < strobes.Count; i++)
		{
			StandardMaterial3D material = MaterialOf(strobes[i]);
			if (material != null)
				material.EmissionEnergyMultiplier = StrobeLights.Intensity(time, i, _settings.Strobe);
		}
		if (_glow != null)
		{
			_glow.GlobalTransform = glowTransform;
			_glow.Visible = glowStrength > 0.001f;
			_glowMaterial.SetShaderParameter("strength", glowStrength);
		}
		if (_smoke != null)
		{
			float final = HullDamage.SmokeFinalSize(speed);
			_smoke.ScaleAmountMin = final;
			_smoke.ScaleAmountMax = final;
		}
	}

	private static Texture2D ScorchTexture(int level)
	{
		if (level <= 0)
			return null;
		int i = level - 1;
		if (ScorchTextures[i] != null)
			return ScorchTextures[i];
		// Color casi negro con la máscara en el alfa: el detalle de Godot mezcla con el alfa de su textura por la UV2
		var source = GD.Load<Texture2D>(ScorchPaths[i]);
		if (source == null)
			return null;
		Image mask = source.GetImage();
		if (mask.IsCompressed())
			mask.Decompress();
		if (mask.HasMipmaps())
			mask.ClearMipmaps();
		mask.Convert(Image.Format.L8);
		byte[] gray = mask.GetData();
		var rgba = new byte[gray.Length * 4];
		uint dark = HullDamage.ScorchColor;
		for (int p = 0; p < gray.Length; p++)
		{
			rgba[p * 4] = (byte)((dark >> 16) & 0xFF);
			rgba[p * 4 + 1] = (byte)((dark >> 8) & 0xFF);
			rgba[p * 4 + 2] = (byte)(dark & 0xFF);
			rgba[p * 4 + 3] = gray[p];
		}
		Image image = Image.CreateFromData(mask.GetWidth(), mask.GetHeight(), false, Image.Format.Rgba8, rgba);
		image.GenerateMipmaps();
		ScorchTextures[i] = ImageTexture.CreateFromImage(image);
		return ScorchTextures[i];
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
			mesh.Layers = HullLayer;
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
				{
					material.DetailBlendMode = BaseMaterial3D.BlendModeEnum.Mix;
					material.DetailUVLayer = BaseMaterial3D.DetailUV.UV2;
					_scorchMaterials.Add(material);
				}
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

	// Colgadas de la nave: giran con ella, así el casco se ve igual en cualquier curva
	private void BuildLights()
	{
		_key = NewLight("Key", KeySource, KeyColor, true);
		_rim = NewLight("Rim", RimSource, RimColor, false);
		_fill = NewLight("Fill", FillSource, FillColor, false);
	}

	private DirectionalLight3D NewLight(string name, Vector3 source, Color color, bool shadow)
	{
		var light = new DirectionalLight3D
		{
			Name = name,
			LightColor = color,
			LightCullMask = HullLayer,
			ShadowCasterMask = HullLayer,
			ShadowEnabled = shadow,
			ShadowBlur = 1.5f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
			DirectionalShadowMaxDistance = ShadowMaxDistance
		};
		// Fijas en el mundo: no siguen el giro de la nave. La cámara del holograma mira hacia −Z, así que el juego de luces
		// del prototipo se gira media vuelta para que la principal llegue del lado de la cámara
		light.TopLevel = !LightsFollowShip;
		if (!LightsFollowShip)
			source = new Vector3(-source.X, source.Y, -source.Z);
		AddChild(light);
		// Mira desde la fuente hacia el centro, en el espacio de la nave (o del mundo, si no la sigue)
		light.Transform = Transform3D.Identity.LookingAt(-source, Vector3.Up);
		return light;
	}

	// Humo con daño, desde Smoke, en el espacio del mundo: se queda atrás al avanzar. La pausa lo congela.
	private void BuildSmoke()
	{
		var texture = new GradientTexture2D
		{
			Width = 64,
			Height = 64,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
			Gradient = new Gradient { Colors = new[] { new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f) }, Offsets = new[] { 0f, 1f } }
		};
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = texture,
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
		};
		var scale = new Curve();
		scale.AddPoint(new Vector2(0f, HullDamage.SmokeStartSize / HullDamage.SmokeEndSize));
		scale.AddPoint(new Vector2(1f, 1f));
		var fade = new Gradient
		{
			Colors = new[] { new Color(1f, 1f, 1f, HullDamage.SmokeStartOpacity), new Color(1f, 1f, 1f, 0f) },
			Offsets = new[] { 0f, 1f }
		};
		float spread = Mathf.RadToDeg(Mathf.Atan2(HullDamage.SmokeSideSpread, (HullDamage.SmokeRiseMin + HullDamage.SmokeRiseMax) / 2f));
		_smoke = new CpuParticles3D
		{
			Name = "Smoke",
			Emitting = false,
			Amount = 1,
			Lifetime = HullDamage.SmokeLifetime,
			LocalCoords = false,
			Mesh = new QuadMesh { Size = Vector2.One, Material = material },
			Direction = Vector3.Up,
			Spread = spread,
			Gravity = Vector3.Zero,
			InitialVelocityMin = HullDamage.SmokeRiseMin,
			InitialVelocityMax = HullDamage.SmokeRiseMax,
			ScaleAmountMin = HullDamage.SmokeEndSize,
			ScaleAmountMax = HullDamage.SmokeEndSize,
			ScaleAmountCurve = scale,
			ColorRamp = fade,
			Position = EmptyPosition(HullPieces.Smoke)
		};
		AddChild(_smoke);
	}

	// Brillo de levitación: un plano sobre la pista; lo coloca la nave cada frame (no sigue su inclinación)
	private void BuildGlow()
	{
		_glowMaterial = new ShaderMaterial { Shader = new Shader { Code = GlowShader } };
		_glowMaterial.SetShaderParameter("tint", new Vector3(HoverGlow.ColorR, HoverGlow.ColorG, HoverGlow.ColorB));
		var mesh = new PlaneMesh { Size = new Vector2(Profile.Glow.Width, Profile.Glow.Length), Material = _glowMaterial };
		_glow = new MeshInstance3D
		{
			Name = "HoverGlow",
			Mesh = mesh,
			TopLevel = true,
			Visible = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_glow);
	}

	public static float GlowLiftAboveTrack => GlowLift;

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
