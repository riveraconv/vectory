using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Vectory.Ship;

// Ajustes de la cabina 3D, en unidades de Godot (spec docs/cabinas-3d-1-vector.md, 7)
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
	float ScreenBrightness);

// Cabina 3D pintada encima del mundo con su propia cámara: un SubViewport con su mundo, su luz y sus reflejos, cuya
// imagen va a pantalla completa en el sitio de la capa 2D (spec docs/cabinas-3d-1-vector.md, 4). Así no se deforma con
// el FOV de la cámara del mundo. Sin reglas: recibe el movimiento de la cámara y los ajustes, y los pinta.
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
	private const float IdleScreenGlow = 0.35f;   // brillo de las pantallas parado; la etapa 3 lo hace subir con la velocidad

	private static readonly Vector3 KeySource = new(0.7f, 2.2f, -1.6f);
	private static readonly Vector3 KeyTarget = new(0f, -0.3f, -0.4f);
	private static readonly Vector3 LampPosition = new(0f, 0.32f, -0.25f);
	private static readonly Vector2I LargeScreenPixels = new(640, 200);
	private static readonly Vector2I SmallScreenPixels = new(256, 256);

	private readonly Dictionary<string, MeshInstance3D> _pieces = new();
	private readonly List<OmniLight3D> _largeScreenLights = new();
	private readonly List<OmniLight3D> _smallScreenLights = new();
	private readonly List<StandardMaterial3D> _screenMaterials = new();

	private Cockpit3DProfile _profile;
	private SubViewport _viewport;
	private Camera3D _camera;
	private DirectionalLight3D _key;
	private OmniLight3D _lamp;
	private Godot.Environment _environment;
	private Cockpit3DSettings _settings;
	private bool _shown;

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
			BuildScreens();
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
	}

	public void SetCameraPose(in CockpitCameraPose pose)
	{
		_camera.Position = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
		_camera.Rotation = new Vector3(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z);
	}

	public void Apply(in Cockpit3DSettings s)
	{
		_settings = s;
		_camera.Fov = s.Fov;
		_key.LightEnergy = s.KeyEnergy;
		_lamp.LightEnergy = s.LampEnergy;
		_environment.AmbientLightEnergy = s.AmbientEnergy;
		_environment.GlowIntensity = s.GlowIntensity;
		_environment.GlowStrength = s.GlowStrength;
		_environment.GlowHdrThreshold = s.GlowThreshold;
		foreach (OmniLight3D light in _largeScreenLights)
			light.LightEnergy = IdleScreenGlow * s.LargeScreenLightEnergy;
		foreach (OmniLight3D light in _smallScreenLights)
			light.LightEnergy = IdleScreenGlow * s.SmallScreenLightEnergy;
		foreach (StandardMaterial3D material in _screenMaterials)
			material.AlbedoColor = ScreenTint(s.ScreenBrightness);
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

		_lamp = new OmniLight3D
		{
			Name = "Lamp",
			Position = LampPosition,
			LightColor = ToColor(RacingPalette.LightWhite),
			OmniRange = LampRange,
			OmniAttenuation = LampDecay
		};
		_viewport.AddChild(_lamp);
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
			ApplyClearcoat(material);
			if (manifest.LowReflectionMaterials.Contains(material.ResourceName))
				LowerReflections(material, manifest.LowReflectionFactor);

			CockpitManifestAdditive additive = manifest.AdditiveMaterials.FirstOrDefault(a => a.Material == material.ResourceName);
			if (additive != null)
				MakeAdditive(material, additive.Opacity);
		}

		foreach (CockpitManifestPiece piece in manifest.Pieces.Where(p => p.Intensity.HasValue))
		{
			if (_pieces.TryGetValue(piece.Name, out MeshInstance3D mesh) && mesh.GetActiveMaterial(0) is StandardMaterial3D material)
			{
				material.EmissionEnabled = true;
				material.EmissionEnergyMultiplier = piece.Intensity.Value;
			}
		}

		// Las shift lights se quedan apagadas hasta la etapa 3
		for (int i = 0; i < Cockpit3DPieces.ShiftLightCount; i++)
		{
			if (_pieces.TryGetValue(Cockpit3DPieces.ShiftLight(i), out MeshInstance3D mesh) && mesh.GetActiveMaterial(0) is StandardMaterial3D material)
				material.EmissionEnergyMultiplier = 0f;
		}

		// La sombra interior de las pantallas no recibe luz
		foreach (string screen in Cockpit3DPieces.LargeScreens.Concat(Cockpit3DPieces.SmallScreens))
		{
			if (_pieces.TryGetValue(Cockpit3DPieces.Shade(screen), out MeshInstance3D mesh) && mesh.GetActiveMaterial(0) is StandardMaterial3D material)
				material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		}
	}

	// Godot no lee KHR_materials_clearcoat al importar: el barniz se pone aquí, con los valores de la tabla de materiales
	private static void ApplyClearcoat(StandardMaterial3D material)
	{
		(float coat, float roughness) = material.ResourceName switch
		{
			"M_Carbon" or "M_CarbonNoseSide" => (1f, 0.06f),
			"M_CarbonNose" => (0.6f, 0.06f),
			"M_Paint" => (1f, 0.05f),
			_ => (0f, 0f)
		};
		if (coat <= 0f)
			return;
		material.ClearcoatEnabled = true;
		material.Clearcoat = coat;
		material.ClearcoatRoughness = roughness;
	}

	// glTF no tiene "intensidad de los reflejos": se baja el reflejo de la superficie y el del barniz
	private static void LowerReflections(StandardMaterial3D material, float factor)
	{
		material.MetallicSpecular *= factor;
		if (material.ClearcoatEnabled)
			material.Clearcoat *= factor;
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
		AddScreen(Cockpit3DPieces.ScreenLeft, "L", LargeScreenPixels, LargeScreenDepth, _largeScreenLights);
		AddScreen(Cockpit3DPieces.ScreenRight, "R", LargeScreenPixels, LargeScreenDepth, _largeScreenLights);
		AddScreen(Cockpit3DPieces.ScreenRadar, "RADAR", SmallScreenPixels, SmallScreenDepth, _smallScreenLights);
		AddScreen(Cockpit3DPieces.ScreenMap, "MAP", SmallScreenPixels, SmallScreenDepth, _smallScreenLights);
	}

	private void AddScreen(string pieceName, string text, Vector2I pixels, float depth, List<OmniLight3D> lights)
	{
		var screenViewport = new SubViewport
		{
			Name = pieceName + "_Viewport",
			Size = pixels,
			Disable3D = true,
			HandleInputLocally = false,
			GuiDisableInput = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once
		};
		var picture = new CockpitTestScreen { Text = text, Size = pixels };
		screenViewport.AddChild(picture);
		AddChild(screenViewport);

		MeshInstance3D piece = _pieces[pieceName];
		var material = new StandardMaterial3D
		{
			ResourceName = "Screen_" + text,
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
	}
}
