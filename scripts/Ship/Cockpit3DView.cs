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
	CockpitLightIntensities Lights,
	IndustrialLampSettings StatusLamps);

// Cabina 3D pintada encima del mundo con su propia cámara: un SubViewport con su mundo, su luz y sus reflejos, cuya
// imagen va a pantalla completa por debajo del HUD (spec docs/cabinas-3d-1-vector.md, 4). Así no se deforma con
// el FOV de la cámara del mundo. Sin reglas: recibe estados y los pinta. No sabe de ninguna nave: piezas, pantallas,
// luces y colores salen de la disposición viva de su perfil (Cockpit3DLayout; spec docs/cabinas-3d-2-blade.md, 5).
//
// Redibujar es lo que cuesta: las pantallas grandes solo se pintan si cambia lo que enseñan, las pequeñas cada frame
// (el haz se mueve siempre, y el mapa gira contigo), las CRT de HAMMER cada frame (su banda baja siempre), los
// textos fijos una vez y, en la cámara exterior, ninguna.
public partial class Cockpit3DView : CanvasLayer
{
	private const int LayerIndex = 0;   // el HUD está en la capa 1

	// Del prototipo
	private const float NearPlane = 0.02f;
	private const float FarPlane = 30f;
	private const float TonemapExposure = 1.05f / 0.6f;   // el ACES de three.js divide la exposición entre 0,6; el de Godot, no
	private const float ShadowMaxDistance = 6f;
	private const float LampRange = 1.6f;
	private const float LampDecay = 1.4f;
	private const float ScreenLightRange = 0.9f;
	private const float ScreenLightDecay = 1.6f;
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
	private readonly List<(OmniLight3D Light, Cockpit3DScreenLight Group, float Factor)> _screenLights = new();
	private readonly List<StandardMaterial3D> _screenMaterials = new();
	private readonly List<SubViewport> _paintOnce = new();   // pantallas de prueba y textos fijos
	private StandardMaterial3D[] _shiftMaterials = System.Array.Empty<StandardMaterial3D>();
	private ShiftLight[] _shiftShown = System.Array.Empty<ShiftLight>();
	private StandardMaterial3D[] _flickerMaterials = System.Array.Empty<StandardMaterial3D>();
	private float[] _flickerBase = System.Array.Empty<float>();
	private bool[] _flickerDimmed = System.Array.Empty<bool>();
	// Estado del HUD de cabina con los colores del estilo de la cabina (se rehace en Setup)
	private CockpitHudState _hud = new(RacingPalette.Role, RacingPalette.Look);
	private readonly List<(StandardMaterial3D Material, Cockpit3DLabel Label, float Factor)> _lamps = new();
	private readonly CockpitLightMix _lightMix = new();
	private readonly List<(StandardMaterial3D Material, float Coat)> _coated = new();
	private readonly List<StandardMaterial3D> _carbon = new();
	private bool _applied;

	private Cockpit3DProfile _profile;
	private Cockpit3DLayout _layout;
	private float _pitch;   // rad, la misma que la cámara del mundo
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
	private Cockpit3DScreenContent _radarContent;
	private Cockpit3DScreenContent _mapContent;

	// Pantallas de BLADE: cristal HUD y velocidad, que se pintan si cambian; radar y mapa, con su parte viva encima
	private SubViewport _combinerViewport;
	private SubViewport _speedViewport;
	private RacingScreenCanvas _combiner;
	private RacingScreenCanvas _speed;
	private RacingScreenCanvas _radarOverlay;
	private RacingScreenCanvas _mapBase;
	private RacingScreenCanvas _mapOverlay;
	private RadarSweep _sweep;
	private CockpitHudInput _input;

	// Pantallas CRT de HAMMER: lo vivo encima de lo fijo, cada frame en la vista de cabina (la banda del CRT baja siempre)
	private readonly List<SubViewport> _alwaysViewports = new();
	private RacingScreenCanvas _industrialSpeed;
	private RacingScreenCanvas _industrialDamage;
	private RacingScreenCanvas _industrialRadar;
	private RacingScreenCanvas _industrialMap;

	// Setas y balizas de HAMMER: material, cómo se enciende y lo último que se le puso
	private readonly List<(StandardMaterial3D Material, Cockpit3DStatusLamp Lamp, IndustrialLamp Shown)> _statusLamps = new();

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

	public void Setup(Cockpit3DProfile profile, in Cockpit3DSettings settings, float pitchRadians)
	{
		_profile = profile;
		_layout = profile.Layout;
		_settings = settings;
		_pitch = pitchRadians;
		_hud = new CockpitHudState(_layout.Role, _layout.Look);

		int shift = _layout.ShiftLightCount;
		_shiftMaterials = new StandardMaterial3D[shift];
		_shiftShown = new ShiftLight[shift];
		int flicker = _layout.FlickerLights.Count;
		_flickerMaterials = new StandardMaterial3D[flicker];
		_flickerBase = new float[flicker];
		_flickerDimmed = new bool[flicker];
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
		if (_radarViewport != null)
			_radarViewport.RenderTargetUpdateMode = small;
		if (_mapViewport != null)
			_mapViewport.RenderTargetUpdateMode = small;
		foreach (SubViewport always in _alwaysViewports)
			always.RenderTargetUpdateMode = small;
		if (_radar != null)
			_radar.Visible = shown;
		if (_minimap != null)
			_minimap.Visible = shown;
		if (shown)
		{
			_screensDirty = true;
			foreach (SubViewport once in _paintOnce)
				once.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}
	}

	// La inclinación de la vista va sumada al movimiento propio de la cámara, como en el prototipo
	public void SetCameraPose(in CockpitCameraPose pose)
	{
		CockpitCameraPose pitched = CockpitCameraMotion.Pitched(pose, _pitch);
		_camera.Position = new Vector3(pitched.Position.X, pitched.Position.Y, pitched.Position.Z);
		_camera.Rotation = new Vector3(pitched.Rotation.X, pitched.Rotation.Y, pitched.Rotation.Z);
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
		_key.LightEnergy = s.KeyEnergy * _layout.Scene.KeyFactor;
		_lamp.LightEnergy = s.LampEnergy * _layout.Scene.LampFactor;
		_environment.AmbientLightEnergy = s.AmbientEnergy;
		_environment.GlowIntensity = s.GlowIntensity;
		_environment.GlowStrength = s.GlowStrength;
		_environment.GlowHdrThreshold = s.GlowThreshold;
		foreach (StandardMaterial3D material in _screenMaterials)
			material.AlbedoColor = ScreenTint(s.ScreenBrightness);
	}

	// El radar y el minimapa: su lógica es la de siempre; aquí solo cambian el tamaño y la paleta (spec, 6.3). Sin
	// pantallas de radar y minimapa en la disposición, nada.
	public void SetRadar(RadarSweep sweep, MinimapPath path, float windowRadius, IReadOnlyList<RaceShip> ships)
	{
		if (!_built || _radar != null || _radarViewport == null || _mapViewport == null)
			return;

		_sweep = sweep;
		RadarStyle radarStyle = _radarContent switch
		{
			Cockpit3DScreenContent.MilitaryRadar => RadarStyle.MilitaryScreen(MilitaryPalette.Radar()),
			Cockpit3DScreenContent.IndustrialRadar => RadarStyle.IndustrialScreen(IndustrialPalette.Radar()),
			_ => RadarStyle.CockpitScreen(RacingPalette.Radar())
		};
		_radar = new RadarView { Name = "Radar" };
		_radar.Setup(sweep, radarStyle, RacingScreenCanvas.FontWith(2));
		_radarViewport.AddChild(_radar);
		_radarViewport.MoveChild(_radar, 1);   // encima de la parte fija y debajo de lo vivo de la pantalla

		MinimapStyle mapStyle = _mapContent switch
		{
			Cockpit3DScreenContent.MilitaryMap => MinimapStyle.MilitaryScreen(MilitaryPalette.Radar()),
			Cockpit3DScreenContent.IndustrialMap => MinimapStyle.IndustrialScreen(IndustrialPalette.Radar()),
			_ => MinimapStyle.CockpitScreen(RacingPalette.Radar())
		};
		_minimap = new MinimapView { Name = "Minimap" };
		_minimap.Setup(path, mapStyle, windowRadius, ships);
		_mapViewport.AddChild(_minimap);
		_mapViewport.MoveChild(_minimap, 1);
		if (_mapBase != null)
		{
			MilitaryScreenDrawing.MapBase(_mapBase.Next, windowRadius);
			_mapBase.Commit();
		}

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

		_input = input;
		UpdateShiftLights(input);
		UpdateLights(light);
		UpdateTints(light);
		UpdateLamps();
		UpdateStatusLamps();
		if (_shown && _leftViewport != null && _rightViewport != null)
			UpdateLargeScreens();
		if (_shown)
		{
			UpdateMilitaryScreens();
			UpdateIndustrialScreens();
		}
	}

	// Pantallas CRT de HAMMER: cambian cada frame por la banda del CRT, que va con el tiempo de la cabina (la pausa la congela)
	private void UpdateIndustrialScreens()
	{
		if (_industrialSpeed != null)
		{
			IndustrialScreenDrawing.Speed(_industrialSpeed.Next, _hud, _input.BoostActive, _time);
			_industrialSpeed.Commit();
		}
		if (_industrialDamage != null)
		{
			IndustrialScreenDrawing.Damage(_industrialDamage.Next, _hud, _time);
			_industrialDamage.Commit();
		}
		if (_industrialRadar != null && _sweep != null)
		{
			IndustrialScreenDrawing.RadarLive(_industrialRadar.Next, _sweep.TargetCount, _sweep.NearestDistance, _sweep.NearestIsClose, _time);
			_industrialRadar.Commit();
		}
		if (_industrialMap != null)
		{
			IndustrialScreenDrawing.MapLive(_industrialMap.Next, _input.LapText, _input.LapFraction, _time);
			_industrialMap.Commit();
		}
	}

	// Setas y balizas de HAMMER (IndustrialLamps): color e intensidad de su emisión. No titilan con el daño.
	private void UpdateStatusLamps()
	{
		IndustrialLampSettings settings = _settings.StatusLamps;
		for (int i = 0; i < _statusLamps.Count; i++)
		{
			(StandardMaterial3D material, Cockpit3DStatusLamp lamp, IndustrialLamp shown) = _statusLamps[i];
			IndustrialLamp lit = lamp.Kind switch
			{
				Cockpit3DStatusLampKind.Seta => IndustrialLamps.Seta(lamp.Index, _input.Charges, _input.Cooldown, _hud.DamageLevel,
					_hud.DamageEnabled, settings),
				Cockpit3DStatusLampKind.BoostBeacon => new IndustrialLamp(IndustrialPalette.Beacon,
					IndustrialLamps.BoostBeacon(_input.BoostActive, _time, settings)),
				_ => new IndustrialLamp(IndustrialPalette.BeaconRed,
					IndustrialLamps.DamageBeacon(_hud.DamageLevel, _hud.DamageEnabled, _hud.BlinkOn, settings))
			};
			if (lit == shown)
				continue;
			material.Emission = ToColor(lit.Color);
			material.EmissionEnergyMultiplier = lit.Intensity;
			_statusLamps[i] = (material, lamp, lit);
		}
	}

	// Cristal HUD y velocidad: solo se pintan si cambia lo que enseñan. Lo vivo del radar y del mapa, cada frame, como
	// sus pantallas.
	private void UpdateMilitaryScreens()
	{
		if (_combiner != null)
		{
			MilitaryScreenDrawing.Combiner(_combiner.Next, _hud,
				new CombinerInput(_input.SpeedKmh, _input.TopKmh, _input.HeadingDegrees, _input.Turn));
			if (_combiner.Commit() || _screensDirty)
				_combinerViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}
		if (_speed != null)
		{
			MilitaryScreenDrawing.Speed(_speed.Next, _hud);
			if (_speed.Commit() || _screensDirty)
				_speedViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}
		if (_radarOverlay != null && _sweep != null)
		{
			MilitaryScreenDrawing.RadarLive(_radarOverlay.Next, _input.HeadingDegrees, _sweep.TargetCount, _sweep.NearestDistance,
				_sweep.NearestIsClose);
			_radarOverlay.Commit();
		}
		if (_mapOverlay != null)
		{
			MilitaryScreenDrawing.MapLive(_mapOverlay.Next, _input.LapText, _input.LapFraction);
			_mapOverlay.Commit();
		}
		if (_combiner != null || _speed != null)
			_screensDirty = false;
	}

	// Pilotos MASTER: encendidos o apagados según el daño; WARN parpadea con el daño. Con la pausa no se llama.
	private void UpdateLamps()
	{
		for (int i = 0; i < _lamps.Count; i++)
		{
			(StandardMaterial3D material, Cockpit3DLabel label, float shown) = _lamps[i];
			float factor = MasterLamps.For(label.Lamp, _hud.DamageLevel, _hud.DamageEnabled, _hud.BlinkOn);
			if (factor == shown)
				continue;
			material.AlbedoColor = LampColor(label.LampColor ?? 0xFFFFFF, factor);
			_lamps[i] = (material, label, factor);
		}
	}

	// El color del piloto (lineal) × su factor, que puede pasar de 1 para que brille
	private static Color LampColor(uint rgb, float factor)
	{
		Color c = ToColor(rgb).SrgbToLinear() * factor;
		return new Color(c.R, c.G, c.B).LinearToSrgb();
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
		for (int i = 0; i < _shiftMaterials.Length; i++)
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
		foreach ((OmniLight3D l, Cockpit3DScreenLight group, float factor) in _screenLights)
		{
			float energy = group == Cockpit3DScreenLight.Large ? _settings.LargeScreenLightEnergy : _settings.SmallScreenLightEnergy;
			l.LightEnergy = glow * energy * factor;
		}

		// BLADE: el brillo de las pantallas sube con la velocidad, × (0,8 + 0,4 × brillo)
		if (_layout.BrightnessFollowsGlow)
		{
			Color tint = ScreenTint(_settings.ScreenBrightness * (0.8f + 0.4f * glow));
			foreach (StandardMaterial3D material in _screenMaterials)
				material.AlbedoColor = tint;
		}

		CockpitLightValues values = _lightMix.Values(_hud.DamageTint, _settings.Lights, _layout.Scene.DamageLightFactor);
		_boostFront.LightEnergy = values.BoostFront * PrototypeLightToEnergy;
		_boostRear.LightEnergy = values.BoostRear * PrototypeLightToEnergy;
		_damageLight.LightEnergy = values.Damage * PrototypeLightToEnergy;
	}

	// Los tintes de los bordes de pantalla (CockpitTints): comunes a todas las cabinas
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
			AmbientLightColor = AmbientColor(_layout.Scene),
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
			Current = true,
			Rotation = new Vector3(_pitch, 0f, 0f)
		};
		_viewport.AddChild(_camera);
	}

	// Hemisferio y ambiente del prototipo en un solo color: la mitad del hemisferio llega de arriba y la mitad de abajo
	private static Color AmbientColor(Cockpit3DScene scene)
	{
		Color sky = ToColor(scene.HemisphereSky).SrgbToLinear();
		Color ground = ToColor(scene.HemisphereGround).SrgbToLinear();
		Color ambient = ToColor(scene.Ambient).SrgbToLinear();
		Color mixed = (sky + ground) * (0.5f * scene.HemisphereStrength) + ambient * scene.AmbientStrength;
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

		_boostTint = NewTint("BoostTint", CockpitTints.Boost);
		_damageTint = NewTint("DamageTint", CockpitTints.Damage);
	}

	private TextureRect NewTint(string name, Texture2D texture)
	{
		var tint = new TextureRect
		{
			Name = name,
			Texture = texture,
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
		Cockpit3DScene scene = _layout.Scene;
		_key = new DirectionalLight3D
		{
			Name = "Key",
			LightColor = ToColor(scene.KeyLight),
			ShadowEnabled = true,
			ShadowBlur = 1.5f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
			DirectionalShadowMaxDistance = ShadowMaxDistance
		};
		_viewport.AddChild(_key);
		_key.LookAtFromPosition(KeySource, KeyTarget, Vector3.Up);

		_lamp = NewOmni("Lamp", LampPosition, scene.Lamp, LampRange, LampDecay);
		_boostFront = NewOmni("BoostFront", BoostFrontPosition, scene.BoostLight, BoostFrontRange, BoostLightDecay);
		_boostRear = NewOmni("BoostRear", BoostRearPosition, scene.BoostLight, BoostRearRange, BoostLightDecay);
		_damageLight = NewOmni("Damage", DamageLightPosition, scene.DamageLight, DamageLightRange, DamageLightDecay);
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

		List<string> missing = CockpitManifest.Missing(_pieces.Keys, _layout.Contract).ToList();
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
			float reflection = manifest.ReflectionFactor(material.ResourceName);
			if (reflection != 1f)
			{
				// glTF no tiene "intensidad de los reflejos" y Godot tampoco por material: se cambia lo que refleja
				material.MetallicSpecular = Mathf.Min(1f, material.MetallicSpecular * reflection);
				material.Metallic = Mathf.Min(1f, material.Metallic * reflection);
				coat *= reflection;
			}
			if (coat > 0f)
				_coated.Add((material, coat));
			if (material.ResourceName == "M_Carbon")
				_carbon.Add(material);

			CockpitManifestAdditive additive = manifest.AdditiveMaterials.FirstOrDefault(a => a.Material == material.ResourceName);
			if (additive != null)
				MakeAdditive(material, additive.Opacity);
			else
			{
				CockpitManifestAdditive transparent = manifest.TransparentMaterials.FirstOrDefault(a => a.Material == material.ResourceName);
				if (transparent != null)
					MakeTransparent(material, transparent.Opacity);
			}
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
			_flickerMaterials[i] = MaterialOf(_layout.FlickerLights[i]);
			_flickerBase[i] = manifest.Piece(_layout.FlickerLights[i])?.Intensity ?? 1f;
		}

		// Las shift lights empiezan apagadas
		for (int i = 0; i < _shiftMaterials.Length; i++)
		{
			_shiftMaterials[i] = MaterialOf(Cockpit3DPieces.ShiftLight(i));
			if (_shiftMaterials[i] != null)
				_shiftMaterials[i].EmissionEnergyMultiplier = 0f;
			_shiftShown[i] = new ShiftLight(ShiftLights.BaseColor(i), 0f);
		}

		// La sombra interior de las pantallas no recibe luz
		foreach (string shade in _layout.Shades)
		{
			StandardMaterial3D material = MaterialOf(shade);
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

	// Cristales y sombras de pantalla: con su opacidad y sin escribir profundidad, así lo de detrás se sigue viendo
	private static void MakeTransparent(StandardMaterial3D material, float opacity)
	{
		material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
		Color albedo = material.AlbedoColor;
		material.AlbedoColor = new Color(albedo.R, albedo.G, albedo.B, opacity);
	}

	private void BuildScreens()
	{
		foreach (Cockpit3DScreen screen in _layout.Screens)
		{
			SubViewport viewport = AddScreen(screen);
			switch (screen.Content)
			{
				case Cockpit3DScreenContent.RacingLeft:
					_leftViewport = viewport;
					_left = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height));
					break;
				case Cockpit3DScreenContent.RacingRight:
					_rightViewport = viewport;
					_right = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height));
					break;
				case Cockpit3DScreenContent.Radar:
					_radarViewport = viewport;
					_radarContent = screen.Content;
					break;
				case Cockpit3DScreenContent.Minimap:
					_mapViewport = viewport;
					_mapContent = screen.Content;
					break;
				case Cockpit3DScreenContent.MilitaryCombiner:
					_combinerViewport = viewport;
					_combiner = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.MilitarySpeed:
					_speedViewport = viewport;
					_speed = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.MilitaryRadar:
					_radarViewport = viewport;
					_radarContent = screen.Content;
					_radarOverlay = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.MilitaryMap:
					_mapViewport = viewport;
					_mapContent = screen.Content;
					_mapBase = (RacingScreenCanvas)viewport.GetChild(0);
					_mapOverlay = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.IndustrialSpeed:
					_alwaysViewports.Add(viewport);
					_industrialSpeed = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.IndustrialDamage:
					_alwaysViewports.Add(viewport);
					_industrialDamage = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.IndustrialRadar:
					_radarViewport = viewport;
					_radarContent = screen.Content;
					_industrialRadar = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
				case Cockpit3DScreenContent.IndustrialMap:
					_mapViewport = viewport;
					_mapContent = screen.Content;
					_industrialMap = NewLiveCanvas(viewport, new Vector2I(screen.Width, screen.Height), _layout.Font);
					break;
			}
		}
		foreach (Cockpit3DStatusLamp lamp in _layout.StatusLampList)
		{
			StandardMaterial3D material = MaterialOf(lamp.Piece);
			if (material == null)
				continue;
			material.EmissionEnabled = true;
			_statusLamps.Add((material, lamp, default));
		}
		foreach (Cockpit3DLabel label in _layout.Labels)
			AddLabel(label);
		foreach (Cockpit3DGauge gauge in _layout.GaugeFaces)
			AddGauge(gauge);
	}

	private static RacingScreenCanvas NewLiveCanvas(SubViewport viewport, Vector2I pixels,
		Cockpit3DFont font = Cockpit3DFont.Rajdhani)
	{
		var canvas = new RacingScreenCanvas { Name = "Live", Size = pixels, FontFamily = font };
		viewport.AddChild(canvas);
		return canvas;
	}

	private SubViewport AddScreen(Cockpit3DScreen screen)
	{
		var pixels = new Vector2I(screen.Width, screen.Height);
		SubViewport screenViewport = NewCanvasViewport(screen.Piece + "_Viewport", pixels);
		// En suma, lo que no se pinta no debe tapar nada: fondo transparente
		screenViewport.TransparentBg = screen.Additive;

		// La parte fija: se pinta una vez. El cristal HUD y la pantalla de velocidad de BLADE se pintan enteras cada vez.
		var background = new RacingScreenCanvas { Name = "Base", Size = pixels, FontFamily = _layout.Font };
		switch (screen.Content)
		{
			case Cockpit3DScreenContent.RacingLeft:
			case Cockpit3DScreenContent.RacingRight:
			case Cockpit3DScreenContent.Radar:
			case Cockpit3DScreenContent.Minimap:
				RacingScreenDrawing.Base(background.Next, pixels.X, pixels.Y);
				break;
			case Cockpit3DScreenContent.MilitaryRadar:
				MilitaryScreenDrawing.RadarBase(background.Next, RadarSettings.DefaultSweepPeriod);
				break;
			case Cockpit3DScreenContent.MilitaryMap:
				MilitaryScreenDrawing.Base(background.Next, pixels.X, pixels.Y);
				break;
			case Cockpit3DScreenContent.IndustrialSpeed:
				IndustrialScreenDrawing.SpeedBase(background.Next);
				break;
			case Cockpit3DScreenContent.IndustrialDamage:
				IndustrialScreenDrawing.DamageBase(background.Next);
				break;
			case Cockpit3DScreenContent.IndustrialRadar:
				IndustrialScreenDrawing.RadarBase(background.Next);
				break;
			case Cockpit3DScreenContent.IndustrialMap:
				IndustrialScreenDrawing.MapBase(background.Next);
				break;
		}
		background.Commit();
		screenViewport.AddChild(background);

		MeshInstance3D piece = _pieces[screen.Piece];
		var material = new StandardMaterial3D
		{
			ResourceName = screen.Piece,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoTexture = screenViewport.GetTexture(),
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		piece.MaterialOverride = material;
		if (screen.Additive)
		{
			// Cristal HUD: en suma y sin escribir profundidad; el negro no se ve
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			material.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
			material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
		}
		else
			_screenMaterials.Add(material);

		if (screen.Light != Cockpit3DScreenLight.None)
		{
			var light = new OmniLight3D
			{
				Name = screen.Piece + "_Light",
				LightColor = ToColor(_layout.Scene.ScreenLight),
				OmniRange = ScreenLightRange,
				OmniAttenuation = ScreenLightDecay
			};
			piece.AddChild(light);
			// 6 cm por delante de la boca de la pantalla, como en el prototipo
			light.Position = new Vector3(0f, 0f, screen.LightOffset);
			_screenLights.Add((light, screen.Light, screen.LightFactor));
		}

		return screenViewport;
	}

	// Texto fijo (pilotos MASTER y placa): se pinta una vez y no cambia
	private void AddLabel(Cockpit3DLabel label)
	{
		var pixels = new Vector2I(label.Width, label.Height);
		SubViewport viewport = NewCanvasViewport(label.Piece + "_Viewport", pixels);
		var canvas = new RacingScreenCanvas { Name = "Label", Size = pixels, FontFamily = _layout.LabelsFont, Bold = label.Bold };
		MilitaryScreenDrawing.Label(canvas.Next, label);
		canvas.Commit();
		viewport.AddChild(canvas);
		_paintOnce.Add(viewport);

		if (!_pieces.TryGetValue(label.Piece, out MeshInstance3D piece))
			return;
		if (label.LampColor is uint lamp)
		{
			// Piloto: sin luz, multiplicando su color; de salida, apagado
			var material = new StandardMaterial3D
			{
				ResourceName = label.Piece,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoTexture = viewport.GetTexture(),
				AlbedoColor = LampColor(lamp, MasterLamps.Off),
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			piece.MaterialOverride = material;
			_lamps.Add((material, label, MasterLamps.Off));
		}
		else
			PaintOnModel(piece, viewport);
	}

	// Placa y rótulos: el material del modelo (rugosidad y metal), con el lienzo como color
	private static void PaintOnModel(MeshInstance3D piece, SubViewport viewport)
	{
		if (piece.GetActiveMaterial(0) is not StandardMaterial3D model)
			return;
		var material = (StandardMaterial3D)model.Duplicate();
		material.AlbedoTexture = viewport.GetTexture();
		material.AlbedoColor = Colors.White;
		material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		piece.MaterialOverride = material;
	}

	// Esfera de un reloj pequeño (HAMMER): decorativa, se pinta una vez sobre el material del modelo
	private void AddGauge(Cockpit3DGauge gauge)
	{
		var pixels = new Vector2I(IndustrialScreenDrawing.GaugeSize, IndustrialScreenDrawing.GaugeSize);
		SubViewport viewport = NewCanvasViewport(gauge.Piece + "_Viewport", pixels);
		var canvas = new RacingScreenCanvas { Name = "Gauge", Size = pixels, FontFamily = _layout.LabelsFont };
		IndustrialScreenDrawing.GaugeFace(canvas.Next, gauge.Label, gauge.Value);
		canvas.Commit();
		viewport.AddChild(canvas);
		_paintOnce.Add(viewport);

		if (_pieces.TryGetValue(gauge.Piece, out MeshInstance3D piece))
			PaintOnModel(piece, viewport);
	}

	private SubViewport NewCanvasViewport(string name, Vector2I pixels)
	{
		var viewport = new SubViewport
		{
			Name = name,
			Size = pixels,
			Disable3D = true,
			HandleInputLocally = false,
			GuiDisableInput = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
		};
		AddChild(viewport);
		return viewport;
	}
}
