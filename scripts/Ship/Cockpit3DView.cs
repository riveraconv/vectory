using System.Collections.Generic;
using System.Linq;
using Godot;
using Vectory.Radar;
using Vectory.UI;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.Ship;

// Ajustes de la cabina 3D, en unidades de Godot salvo las intensidades de boost y daño, que multiplican el valor del
// diseño (spec docs/cabinas-3d-1-vector.md, 7)
public readonly record struct Cockpit3DSettings(
	float Fov,
	float KeyEnergy,
	float AmbientEnergy,
	float LampEnergy,
	float LargeScreenLightEnergy,
	float SmallScreenLightEnergy,
	float GlowIntensity,
	float GlowStrength,
	float GlowThreshold,
	float ScreenBrightness,
	float Clearcoat,
	float GlowWide,
	float Reflections,
	float CarbonSpecular,
	CockpitLightIntensities Lights);

// Cabina 3D pintada encima del mundo con su propia cámara: un SubViewport con su mundo, su luz y sus reflejos, cuya
// imagen va a pantalla completa en el sitio de la capa 2D (spec docs/cabinas-3d-1-vector.md, 4). Así no se deforma con
// el FOV de la cámara del mundo. Sin reglas: recibe estados y los pinta.
//
// Redibujar es lo que cuesta: las pantallas grandes solo se pintan si cambia lo que enseñan, las pequeñas cada frame
// (el haz se mueve siempre, y el mapa gira contigo) y, en la cámara exterior, ninguna.
public partial class Cockpit3DView : CanvasLayer
{
	private const int LayerIndex = 0;   // el HUD está en la capa 1

	// Del prototipo
	private const float NearPlane = 0.02f;
	private const float FarPlane = 30f;
	private const float TonemapExposure = 1.05f / 0.6f;   // el ACES de three.js divide la exposición entre 0,6; el de Godot, no
	private const float HemisphereStrength = 0.6f;
	private const float AmbientStrength = 0.45f;
	private const float ShadowMaxDistance = 6f;
	private const float LampRange = 1.6f;
	private const float LampDecay = 1.4f;
	private const float ScreenLightRange = 0.9f;
	private const float ScreenLightDecay = 1.6f;
	private const float ScreenLightDistance = 0.06f;
	private const float LargeScreenDepth = 0.045f;
	private const float SmallScreenDepth = 0.035f;
	private const float ScreenSurfaceOffset = 0.004f;
	private const float BoostFrontRange = 3.5f;
	private const float BoostRearRange = 4f;
	private const float BoostLightDecay = 1.2f;
	private const float DamageLightRange = 2.5f;
	private const float DamageLightDecay = 1.3f;
	private const float FlickerDimFactor = 0.2f;   // las luces que titilan bajan al 20 %

	// Una omni del prototipo en cd da, sobre una superficie, intensidad / π; en Godot, su energía tal cual
	private const float PrototypeLightToEnergy = 1f / Mathf.Pi;

	// Niveles del glow de Godot: los estrechos son fijos y GlowWide reparte el halo como el bloom del prototipo
	private static readonly float[] GlowNarrowLevels = { 0f, 1f, 1f, 0f, 0f, 0f, 0f };
	private static readonly float[] GlowWideLevels = { 0f, 0f, 0f, 1f, 1f, 0.5f, 0f };

	private static readonly Vector3 KeySource = new(0.7f, 2.2f, -1.6f);
	private static readonly Vector3 KeyTarget = new(0f, -0.3f, -0.4f);
	private static readonly Vector3 LampPosition = new(0f, 0.32f, -0.25f);
	private static readonly Vector3 BoostFrontPosition = new(0f, 0.25f, 0.45f);
	private static readonly Vector3 BoostRearPosition = new(0f, -0.2f, -1.8f);
	private static readonly Vector3 DamageLightPosition = new(0f, 0.1f, -0.35f);

	private readonly Dictionary<string, MeshInstance3D> _pieces = new();
	private readonly List<OmniLight3D> _largeScreenLights = new();
	private readonly List<OmniLight3D> _smallScreenLights = new();
	private readonly List<StandardMaterial3D> _screenMaterials = new();
	private readonly StandardMaterial3D[] _shiftMaterials = new StandardMaterial3D[ShiftLights.Count];
	private readonly ShiftLight[] _shiftShown = new ShiftLight[ShiftLights.Count];
	private readonly StandardMaterial3D[] _flickerMaterials = new StandardMaterial3D[Cockpit3DPieces.FlickerLights.Count];
	private readonly float[] _flickerBase = new float[Cockpit3DPieces.FlickerLights.Count];
	private readonly bool[] _flickerDimmed = new bool[Cockpit3DPieces.FlickerLights.Count];
	private readonly CockpitHudState _hud = new(RacingPalette.Role, RacingPalette.Look);
	private readonly CockpitLightMix _lightMix = new();
	private readonly List<(StandardMaterial3D Material, float Coat)> _coated = new();
	private readonly List<StandardMaterial3D> _carbon = new();
	private bool _applied;

	private Cockpit3DProfile _profile;
	private SubViewport _viewport;
	private Camera3D _camera;
	private DirectionalLight3D _key;
	private OmniLight3D _lamp;
	private OmniLight3D _boostFront;
	private OmniLight3D _boostRear;
	private OmniLight3D _damageLight;
	private TextureRect _boostTint;
	private TextureRect _damageTint;
	private Godot.Environment _environment;
	private Cockpit3DSettings _settings;

	private SubViewport _leftViewport;
	private SubViewport _rightViewport;
	private SubViewport _radarViewport;
	private SubViewport _mapViewport;
	private RacingScreenCanvas _left;
	private RacingScreenCanvas _right;
	private RadarView _radar;
	private MinimapView _minimap;

	private float _time;
	private bool _shown;
	private bool _screensDirty = true;
	private bool _built;

	// Una luz por arista del parpadeo: cada una titila por su cuenta con los daños
	public int FlickerCount => _flickerMaterials.Length;

	// 0xRRGGBB a color de Godot
	public static Color ToColor(uint rgb, float alpha = 1f)
	{
		return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
	}

	public void Setup(Cockpit3DProfile profile, in Cockpit3DSettings settings)
	{
		_profile = profile;
		_settings = settings;
	}

	public override void _Ready()
	{
		Layer = LayerIndex;
		BuildViewport();
		BuildImage();
		BuildLights();
		if (LoadModel())
		{
			BuildScreens();
			_built = true;
		}
		Apply(_settings);
		SetShown(_shown);
		GetWindow().SizeChanged += FitToWindow;
	}

	public override void _ExitTree()
	{
		GetWindow().SizeChanged -= FitToWindow;
	}

	// Sin dibujarse en la cámara exterior: un SubViewport oculto seguiría costando lo mismo
	public void SetShown(bool shown)
	{
		_shown = shown;
		if (_viewport == null)
			return;
		Visible = shown;
		_viewport.RenderTargetUpdateMode = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
		if (!_built)
			return;

		SubViewport.UpdateMode small = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
		_radarViewport.RenderTargetUpdateMode = small;
		_mapViewport.RenderTargetUpdateMode = small;
		if (_radar != null)
			_radar.Visible = shown;
		if (_minimap != null)
			_minimap.Visible = shown;
		if (shown)
			_screensDirty = true;
	}

	public void SetCameraPose(in CockpitCameraPose pose)
	{
		_camera.Position = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
		_camera.Rotation = new Vector3(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z);
	}

	public void Apply(in Cockpit3DSettings s)
	{
		if (_applied && s == _settings)
			return;
		_applied = true;
		_settings = s;
		_camera.Fov = s.Fov;
		foreach ((StandardMaterial3D material, float coat) in _coated)
			material.Clearcoat = coat * s.Clearcoat;
		foreach (StandardMaterial3D material in _carbon)
			material.MetallicSpecular = s.CarbonSpecular;
		_environment.BackgroundEnergyMultiplier = s.Reflections;
		for (int level = 0; level < GlowWideLevels.Length; level++)
			_environment.SetGlowLevel(level, GlowWideLevels[level] * s.GlowWide + GlowNarrowLevels[level]);
		_key.LightEnergy = s.KeyEnergy;
		_lamp.LightEnergy = s.LampEnergy;
		_environment.AmbientLightEnergy = s.AmbientEnergy;
		_environment.GlowIntensity = s.GlowIntensity;
		_environment.GlowStrength = s.GlowStrength;
		_environment.GlowHdrThreshold = s.GlowThreshold;
		foreach (StandardMaterial3D material in _screenMaterials)
			material.AlbedoColor = ScreenTint(s.ScreenBrightness);
	}

	// El radar y el minimapa: su lógica es la de siempre; aquí solo cambian el tamaño y la paleta (spec, 6.3)
	public void SetRadar(RadarSweep sweep, MinimapPath path, float windowRadius, IReadOnlyList<RaceShip> ships)
	{
		if (!_built || _radar != null)
			return;

		_radar = new RadarView { Name = "Radar" };
		_radar.Setup(sweep, RadarStyle.CockpitScreen(RacingPalette.Radar()), RacingScreenCanvas.FontWith(2));
		_radarViewport.AddChild(_radar);

		_minimap = new MinimapView { Name = "Minimap" };
		_minimap.Setup(path, MinimapStyle.CockpitScreen(RacingPalette.Radar()), windowRadius, ships);
		_mapViewport.AddChild(_minimap);

		_radar.Visible = _shown;
		_minimap.Visible = _shown;
	}

	public void SetMinimapPose(NVec2 self, NVec2 planForward)
	{
		_minimap?.SetPose(self, planForward);
	}

	// HUD de las pantallas, shift lights y luz. Con la pausa no se llama: queda todo congelado.
	public void UpdateHud(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_time += dt;
		_hud.Update(input, CockpitLightSettings.Default, dt);
		_lightMix.Update(input.BoostActive, _hud.OverTop, dt);
		if (!_built)
			return;

		UpdateShiftLights(input);
		UpdateLights(light);
		UpdateTints(light);
		if (_shown)
			UpdateLargeScreens();
	}

	// Daños: las luces apagadas del parpadeo bajan al 20 %. Las shift lights y las pantallas no titilan.
	public void ShowEdgeFlicker(EdgeFlicker flicker)
	{
		foreach (int edge in flicker.AffectedEdges)
		{
			if (edge >= _flickerMaterials.Length)
				continue;
			bool dimmed = flicker.IsDimmed(edge);
			if (dimmed == _flickerDimmed[edge] || _flickerMaterials[edge] == null)
				continue;
			_flickerDimmed[edge] = dimmed;
			_flickerMaterials[edge].EmissionEnergyMultiplier = _flickerBase[edge] * (dimmed ? FlickerDimFactor : 1f);
		}
	}

	private void UpdateShiftLights(in CockpitHudInput input)
	{
		for (int i = 0; i < ShiftLights.Count; i++)
		{
			ShiftLight lit = ShiftLights.Light(i, input.SpeedKmh, input.TopKmh, _hud.OverTop, _time);
			if (lit == _shiftShown[i] || _shiftMaterials[i] == null)
				continue;
			_shiftShown[i] = lit;
			_shiftMaterials[i].Emission = ToColor(lit.Color);
			_shiftMaterials[i].EmissionEnergyMultiplier = lit.Intensity;
		}
	}

	private void UpdateLights(in CockpitLightSettings light)
	{
		float glow = _hud.ScreenGlow * light.ScreenGlow;
		foreach (OmniLight3D l in _largeScreenLights)
			l.LightEnergy = glow * _settings.LargeScreenLightEnergy;
		foreach (OmniLight3D l in _smallScreenLights)
			l.LightEnergy = glow * _settings.SmallScreenLightEnergy;

		CockpitLightValues values = _lightMix.Values(_hud.DamageTint, _settings.Lights);
		_boostFront.LightEnergy = values.BoostFront * PrototypeLightToEnergy;
		_boostRear.LightEnergy = values.BoostRear * PrototypeLightToEnergy;
		_damageLight.LightEnergy = values.Damage * PrototypeLightToEnergy;
	}

	// Los tintes de los bordes de pantalla son los de la cabina 2D: comunes a todas las cabinas
	private void UpdateTints(in CockpitLightSettings light)
	{
		SetTint(_boostTint, _hud.BoostTint * light.BoostTint);
		SetTint(_damageTint, _hud.DamageTint * light.DamageTint);
	}

	private static void SetTint(TextureRect tint, float opacity)
	{
		tint.Visible = opacity > 0f;
		if (opacity > 0f)
			tint.Modulate = new Color(1f, 1f, 1f, opacity);
	}

	private void UpdateLargeScreens()
	{
		RacingScreenDrawing.Left(_left.Next, _hud);
		bool leftChanged = _left.Commit();
		RacingScreenDrawing.Right(_right.Next, _hud);
		bool rightChanged = _right.Commit();

		if (leftChanged || _screensDirty)
			_leftViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		if (rightChanged || _screensDirty)
			_rightViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		_screensDirty = false;
	}

	// El tinte es sRGB y el brillo del prototipo es lineal
	private static Color ScreenTint(float brightness) => new Color(brightness, brightness, brightness).LinearToSrgb();

	private void BuildViewport()
	{
		_viewport = new SubViewport
		{
			Name = "Viewport",
			OwnWorld3D = true,
			TransparentBg = true,
			HandleInputLocally = false,
			GuiDisableInput = true,
			Msaa3D = GetViewport().Msaa3D,
			ScreenSpaceAA = GetViewport().ScreenSpaceAA,
			PositionalShadowAtlasSize = 0   // ninguna omni proyecta sombra
		};
		AddChild(_viewport);
		FitToWindow();

		_environment = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Sky,
			Sky = new Sky
			{
				SkyMaterial = new PanoramaSkyMaterial { Panorama = GD.Load<Texture2D>(_profile.EnvironmentPath) }
			},
			// El centro del panorama mira a −Z del prototipo y en Godot el centro de la imagen cae en +Z
			SkyRotation = new Vector3(0f, Mathf.Pi, 0f),
			ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = AmbientColor(),
			TonemapMode = Godot.Environment.ToneMapper.Aces,
			TonemapExposure = TonemapExposure,
			GlowEnabled = true,
			GlowBloom = 0f,
			GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive
		};

		_camera = new Camera3D
		{
			Name = "Camera3D",
			Near = NearPlane,
			Far = FarPlane,
			Environment = _environment,
			Current = true
		};
		_viewport.AddChild(_camera);
	}

	// Hemisferio y ambiente del prototipo en un solo color: la mitad del hemisferio llega de arriba y la mitad de abajo
	private static Color AmbientColor()
	{
		Color sky = ToColor(RacingPalette.HemisphereSky).SrgbToLinear();
		Color ground = ToColor(RacingPalette.HemisphereGround).SrgbToLinear();
		Color ambient = ToColor(RacingPalette.Ambient).SrgbToLinear();
		Color mixed = (sky + ground) * (0.5f * HemisphereStrength) + ambient * AmbientStrength;
		return new Color(mixed.R, mixed.G, mixed.B).LinearToSrgb();
	}

	private void BuildImage()
	{
		var image = new TextureRect
		{
			Name = "Image",
			Texture = _viewport.GetTexture(),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			// La imagen ya viene multiplicada por su alfa, y así el halo también se ve sobre el mundo
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha }
		};
		image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(image);

		_boostTint = NewTint("BoostTint", CockpitGradientId.TintOrange);
		_damageTint = NewTint("DamageTint", CockpitGradientId.TintRed);
	}

	private TextureRect NewTint(string name, CockpitGradientId gradient)
	{
		var tint = new TextureRect
		{
			Name = name,
			Texture = CockpitPainter.TextureOf(gradient),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		tint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(tint);
		return tint;
	}

	// Del tamaño real de la imagen del juego en pantalla, con las bandas negras fuera
	private void FitToWindow()
	{
		Window window = GetWindow();
		Vector2I content = window.ContentScaleSize;
		Vector2I size = window.Size;
		if (content.X <= 0 || content.Y <= 0)
		{
			_viewport.Size = new Vector2I(Mathf.Max(size.X, 1), Mathf.Max(size.Y, 1));
			return;
		}
		float scale = Mathf.Min((float)size.X / content.X, (float)size.Y / content.Y);
		_viewport.Size = new Vector2I(Mathf.Max(Mathf.RoundToInt(content.X * scale), 1), Mathf.Max(Mathf.RoundToInt(content.Y * scale), 1));
	}

	private void BuildLights()
	{
		_key = new DirectionalLight3D
		{
			Name = "Key",
			LightColor = ToColor(RacingPalette.LightWhite),
			ShadowEnabled = true,
			ShadowBlur = 1.5f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
			DirectionalShadowMaxDistance = ShadowMaxDistance
		};
		_viewport.AddChild(_key);
		_key.LookAtFromPosition(KeySource, KeyTarget, Vector3.Up);

		_lamp = NewOmni("Lamp", LampPosition, RacingPalette.LightWhite, LampRange, LampDecay);
		_boostFront = NewOmni("BoostFront", BoostFrontPosition, RacingPalette.BoostLight, BoostFrontRange, BoostLightDecay);
		_boostRear = NewOmni("BoostRear", BoostRearPosition, RacingPalette.BoostLight, BoostRearRange, BoostLightDecay);
		_damageLight = NewOmni("Damage", DamageLightPosition, RacingPalette.DamageLight, DamageLightRange, DamageLightDecay);
	}

	private OmniLight3D NewOmni(string name, Vector3 position, uint color, float range, float decay)
	{
		var light = new OmniLight3D
		{
			Name = name,
			Position = position,
			LightColor = ToColor(color),
			OmniRange = range,
			OmniAttenuation = decay,
			LightEnergy = 0f
		};
		_viewport.AddChild(light);
		return light;
	}

	private bool LoadModel()
	{
		var scene = GD.Load<PackedScene>(_profile.ModelPath);
		string json = FileAccess.GetFileAsString(_profile.ManifestPath);
		CockpitManifest manifest = string.IsNullOrEmpty(json) ? null : CockpitManifest.Parse(json);
		if (scene == null || manifest == null)
		{
			GD.PushError($"[Cockpit3D] Cannot load {_profile.ModelPath} or {_profile.ManifestPath}");
			return false;
		}

		Node model = scene.Instantiate();
		_viewport.AddChild(model);
		CollectPieces(model);

		List<string> missing = CockpitManifest.Missing(_pieces.Keys).ToList();
		if (missing.Count > 0)
			GD.PushError("[Cockpit3D] Missing cockpit pieces: " + string.Join(", ", missing));

		ApplyManifest(manifest);
		return missing.Count == 0;
	}

	private void CollectPieces(Node node)
	{
		if (node is MeshInstance3D mesh)
			_pieces[mesh.Name] = mesh;
		foreach (Node child in node.GetChildren())
			CollectPieces(child);
	}

	private StandardMaterial3D MaterialOf(string piece)
	{
		return _pieces.TryGetValue(piece, out MeshInstance3D mesh) ? mesh.GetActiveMaterial(0) as StandardMaterial3D : null;
	}

	// Lo que glTF no lleva (spec, 4.1)
	private void ApplyManifest(CockpitManifest manifest)
	{
		foreach (MeshInstance3D mesh in _pieces.Values)
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		foreach (string name in manifest.NoShadow)
		{
			if (_pieces.TryGetValue(name, out MeshInstance3D mesh))
				mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}

		var done = new HashSet<StandardMaterial3D>();
		foreach (MeshInstance3D mesh in _pieces.Values)
		{
			if (mesh.GetActiveMaterial(0) is not StandardMaterial3D material || !done.Add(material))
				continue;

			// El metal es un valor por material, y la textura de rugosidad es gris: leída como metal, lo rebajaría
			material.MetallicTexture = null;
			float coat = ClearcoatOf(material);
			if (manifest.LowReflectionMaterials.Contains(material.ResourceName))
			{
				// glTF no tiene "intensidad de los reflejos" y Godot tampoco por material: se baja lo que refleja
				material.MetallicSpecular *= manifest.LowReflectionFactor;
				material.Metallic *= manifest.LowReflectionFactor;
				coat *= manifest.LowReflectionFactor;
			}
			if (coat > 0f)
				_coated.Add((material, coat));
			if (material.ResourceName == "M_Carbon")
				_carbon.Add(material);

			CockpitManifestAdditive additive = manifest.AdditiveMaterials.FirstOrDefault(a => a.Material == material.ResourceName);
			if (additive != null)
				MakeAdditive(material, additive.Opacity);
		}

		foreach (CockpitManifestPiece piece in manifest.Pieces.Where(p => p.Intensity.HasValue))
		{
			StandardMaterial3D material = MaterialOf(piece.Name);
			if (material != null)
			{
				material.EmissionEnabled = true;
				material.EmissionEnergyMultiplier = piece.Intensity.Value;
			}
		}

		for (int i = 0; i < _flickerMaterials.Length; i++)
		{
			_flickerMaterials[i] = MaterialOf(Cockpit3DPieces.FlickerLights[i]);
			_flickerBase[i] = manifest.Piece(Cockpit3DPieces.FlickerLights[i])?.Intensity ?? 1f;
		}

		// Las shift lights empiezan apagadas
		for (int i = 0; i < ShiftLights.Count; i++)
		{
			_shiftMaterials[i] = MaterialOf(Cockpit3DPieces.ShiftLight(i));
			if (_shiftMaterials[i] != null)
				_shiftMaterials[i].EmissionEnergyMultiplier = 0f;
			_shiftShown[i] = new ShiftLight(ShiftLights.BaseColor(i), 0f);
		}

		// La sombra interior de las pantallas no recibe luz
		foreach (string screen in Cockpit3DPieces.LargeScreens.Concat(Cockpit3DPieces.SmallScreens))
		{
			StandardMaterial3D material = MaterialOf(Cockpit3DPieces.Shade(screen));
			if (material != null)
				material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		}
	}

	// Godot no lee KHR_materials_clearcoat al importar: el barniz se pone aquí, con los valores de la tabla de materiales.
	// Devuelve su fuerza, que Apply multiplica por el ajuste: Godot lo pinta más flojo que el prototipo.
	private static float ClearcoatOf(StandardMaterial3D material)
	{
		(float coat, float roughness) = material.ResourceName switch
		{
			"M_Carbon" or "M_CarbonNoseSide" => (1f, 0.06f),
			"M_CarbonNose" => (0.6f, 0.06f),
			"M_Paint" => (1f, 0.05f),
			_ => (0f, 0f)
		};
		if (coat > 0f)
		{
			material.ClearcoatEnabled = true;
			material.ClearcoatRoughness = roughness;
		}
		return coat;
	}

	private static void MakeAdditive(StandardMaterial3D material, float opacity)
	{
		material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		material.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
		material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
		material.AlbedoColor = new Color(1f, 1f, 1f, opacity);
	}

	private void BuildScreens()
	{
		var large = new Vector2I(RacingScreenDrawing.LargeWidth, RacingScreenDrawing.LargeHeight);
		var small = new Vector2I(RacingScreenDrawing.SmallSize, RacingScreenDrawing.SmallSize);

		_leftViewport = AddScreen(Cockpit3DPieces.ScreenLeft, large, LargeScreenDepth, _largeScreenLights);
		_rightViewport = AddScreen(Cockpit3DPieces.ScreenRight, large, LargeScreenDepth, _largeScreenLights);
		_radarViewport = AddScreen(Cockpit3DPieces.ScreenRadar, small, SmallScreenDepth, _smallScreenLights);
		_mapViewport = AddScreen(Cockpit3DPieces.ScreenMap, small, SmallScreenDepth, _smallScreenLights);

		_left = NewLiveCanvas(_leftViewport, large);
		_right = NewLiveCanvas(_rightViewport, large);
	}

	private static RacingScreenCanvas NewLiveCanvas(SubViewport viewport, Vector2I pixels)
	{
		var canvas = new RacingScreenCanvas { Name = "Live", Size = pixels };
		viewport.AddChild(canvas);
		return canvas;
	}

	private SubViewport AddScreen(string pieceName, Vector2I pixels, float depth, List<OmniLight3D> lights)
	{
		var screenViewport = new SubViewport
		{
			Name = pieceName + "_Viewport",
			Size = pixels,
			Disable3D = true,
			HandleInputLocally = false,
			GuiDisableInput = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
		};

		// La parte fija: se pinta una vez
		var background = new RacingScreenCanvas { Name = "Base", Size = pixels };
		RacingScreenDrawing.Base(background.Next, pixels.X, pixels.Y);
		background.Commit();
		screenViewport.AddChild(background);
		AddChild(screenViewport);

		MeshInstance3D piece = _pieces[pieceName];
		var material = new StandardMaterial3D
		{
			ResourceName = pieceName,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoTexture = screenViewport.GetTexture(),
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		piece.MaterialOverride = material;
		_screenMaterials.Add(material);

		var light = new OmniLight3D
		{
			Name = pieceName + "_Light",
			LightColor = ToColor(RacingPalette.LightWhite),
			OmniRange = ScreenLightRange,
			OmniAttenuation = ScreenLightDecay
		};
		piece.AddChild(light);
		// 6 cm por delante de la boca de la pantalla, como en el prototipo
		light.Position = new Vector3(0f, 0f, depth - ScreenSurfaceOffset + ScreenLightDistance);
		lights.Add(light);

		return screenViewport;
	}
}
