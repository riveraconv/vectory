using Godot;
using Vectory.Core;
using Vectory.Settings;
using Vectory.Ship;

namespace Vectory.Effects;

// Visión túnel en pantalla (spec docs/efectos-9-vision-tunel.md, 2–4; designs/efectos-vision-tunel.html, TunnelShader):
// desenfoque radial con desdoble de color, viñeta y halo ámbar sobre lo ya pintado: solo el mundo, con su glow. Va en la
// capa 1, por debajo de la cabina 3D (capa 2) y del HUD (capa 3): el interior de la cabina y el HUD quedan nítidos y el
// túnel se ve por el parabrisas (decisión del usuario).
//
// Sin reglas: el Player le pasa la intensidad suavizada, el boost y la cámara (SetSource), y aquí se juntan con
// TUNNEL VISION y PARTICLES mediante TunnelVisionCurves. Se procesa también en pausa, así un cambio de SETTINGS se ve
// al momento. El shader está siempre puesto (con intensidad 0 deja la imagen igual): no se cambia en plena carrera.
// Los parámetros solo se escriben cuando cambian.
public partial class TunnelVisionView : CanvasLayer
{
	public const int LayerIndex = 1;

	private static readonly StringName SnIntensity = "intensity";
	private static readonly StringName SnAberration = "aberration";
	private static readonly StringName SnHalo = "halo";
	private static readonly StringName SnSamples = "samples";
	private static readonly StringName SnBlur = "blur";
	private static readonly StringName SnDarken = "darken";
	private static readonly StringName SnMask = "mask";
	private static readonly StringName SnHaloColor = "halo_color";
	private static readonly StringName SnFadeEnd = "fade_end";

	// Del prototipo (TunnelShader): la viñeta y el halo llegan enteros pasada la esquina (r = 1,15), y el halo empieza en
	// el 55 %. El oscurecimiento y el halo se aplican en lineal, como en la maqueta (allí, antes del tonemap).
	private const float FadeEnd = 1.15f;
	private const float HaloStart = 0.55f;

	private const string ShaderCode = @"
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform float intensity = 0.0;
uniform float blur = 0.11;
uniform float darken = 0.62;
uniform float aberration = 0.0;
uniform float halo = 0.0;
uniform vec3 halo_color = vec3(1.0, 0.34, 0.03);
// x, y: máscara de bordes (empieza, completa); z: viñeta desde; w: halo desde
uniform vec4 mask = vec4(0.22, 0.95, 0.42, 0.55);
uniform float fade_end = 1.15;
uniform int samples = 16;

void fragment() {
	vec2 c = SCREEN_UV - 0.5;
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 ca = c * vec2(aspect, 1.0);
	float r = length(ca) / length(vec2(aspect * 0.5, 0.5));   // 0 en el centro, 1 en la esquina
	float m = smoothstep(mask.x, mask.y, r) * intensity;
	float str = blur * m;
	float ab = aberration * m;
	vec3 col;
	if (str > 0.0004 || ab > 0.00004) {
		vec3 acc = vec3(0.0);
		int n = max(samples, 1);
		float last = float(max(n - 1, 1));
		for (int i = 0; i < n; i++) {
			float s = 1.0 - str * float(i) / last;
			acc += vec3(textureLod(screen_tex, 0.5 + c * (s + ab), 0.0).r,
				textureLod(screen_tex, 0.5 + c * s, 0.0).g,
				textureLod(screen_tex, 0.5 + c * (s - ab), 0.0).b);
		}
		col = acc / float(n);
	} else {
		col = textureLod(screen_tex, SCREEN_UV, 0.0).rgb;
	}
	if (intensity > 0.0) {
		vec3 lin = pow(max(col, vec3(0.0)), vec3(2.2));
		lin *= 1.0 - smoothstep(mask.z, fade_end, r) * darken * min(intensity, 1.0);
		lin += halo_color * halo * smoothstep(mask.w, fade_end, r);
		col = pow(lin, vec3(1.0 / 2.2));
	}
	COLOR = vec4(col, 1.0);
}
";

	private ShaderMaterial _material;
	private TunnelVisionSettings _settings = TunnelVisionSettings.Default;

	private float _intensity;
	private float _boost;
	private CameraView _view = CameraView.Chase;

	// Lo último escrito en el shader (NaN: nada aún)
	private float _shownIntensity = float.NaN;
	private float _shownAberration = float.NaN;
	private float _shownHalo = float.NaN;
	private int _shownSamples = -1;
	private float _shownBlur = float.NaN;
	private float _shownDarken = float.NaN;
	private float _shownFadeEnd = float.NaN;
	private Vector4 _shownMask = new(float.NaN, 0f, 0f, 0f);

	public override void _Ready()
	{
		Layer = LayerIndex;
		ProcessMode = ProcessModeEnum.Always;
		_material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
		Color orange = Palette.Orange.SrgbToLinear();
		_material.SetShaderParameter(SnHaloColor, new Vector3(orange.R, orange.G, orange.B));
		ApplySettings();
		var rect = new ColorRect
		{
			Name = "TunnelVisionRect",
			Material = _material,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(rect);
		Push();
	}

	// Ajustes del Inspector (grupo Visión túnel del Player)
	public void SetSettings(TunnelVisionSettings settings)
	{
		_settings = settings;
		if (_material != null)
			ApplySettings();
	}

	// Intensidad suavizada (sin cámara ni SETTINGS), boost (0–1) y cámara activa
	public void SetSource(float intensity, float boost, CameraView view)
	{
		_intensity = intensity;
		_boost = boost;
		_view = view;
	}

	public override void _Process(double delta)
	{
		Push();
	}

	// Solo lo que ha cambiado (el Player los pasa en cada paso, por si se tocan en el Inspector)
	private void ApplySettings()
	{
		if (Changed(_settings.Blur, ref _shownBlur))
			_material.SetShaderParameter(SnBlur, _settings.Blur);
		if (Changed(_settings.Darken, ref _shownDarken))
			_material.SetShaderParameter(SnDarken, _settings.Darken);
		var mask = new Vector4(_settings.MaskStart, _settings.MaskFull, _settings.VignetteStart, HaloStart);
		if (mask != _shownMask)
		{
			_shownMask = mask;
			_material.SetShaderParameter(SnMask, mask);
		}
		if (Changed(FadeEnd, ref _shownFadeEnd))
			_material.SetShaderParameter(SnFadeEnd, FadeEnd);
	}

	private void Push()
	{
		TunnelVisionFrame frame = TunnelVisionCurves.Frame(_intensity, _boost, _view, ActiveSettings.TunnelVision,
			ActiveSettings.ParticlesLevel, _settings);
		if (Changed(frame.Intensity, ref _shownIntensity))
			_material.SetShaderParameter(SnIntensity, frame.Intensity);
		if (Changed(frame.Aberration, ref _shownAberration))
			_material.SetShaderParameter(SnAberration, frame.Aberration);
		if (Changed(frame.Halo, ref _shownHalo))
			_material.SetShaderParameter(SnHalo, frame.Halo);
		if (frame.Samples != _shownSamples)
		{
			_shownSamples = frame.Samples;
			_material.SetShaderParameter(SnSamples, frame.Samples);
		}
	}

	private static bool Changed(float value, ref float shown)
	{
		if (value == shown)
			return false;
		shown = value;
		return true;
	}
}
