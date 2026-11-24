using Godot;
using Vectory.Core;
using Vectory.Settings;
using Vectory.Ship;

namespace Vectory.Effects;

// Cono de vapor de un rival (spec docs/efectos-9-vision-tunel.md, 5.1): la campana de persecución del jugador, con el
// mismo shader y las mismas curvas, mucho más sutil y con un fundido cerca de la cámara para no tapar un adelantamiento.
// Hijo de su nave. La malla está siempre puesta: con opacidad 0 el shader la encoge a un punto. Se procesa también en
// pausa (un cambio de SETTINGS se ve al momento); Feed, no.
public partial class RivalSonicCone : MeshInstance3D
{
	private static readonly StringName SnOpacity = "opacity";
	private static readonly StringName SnFlow = "flow";
	private static readonly StringName SnColor = "line_color";
	private static readonly StringName SnFloor = "floor_y";
	private static readonly StringName SnNearFade = "near_fade";

	private readonly SonicConeState _state = new();
	private SonicConeSettings _settings;
	private ShaderMaterial _material;
	private float _boost;
	private float _shownOpacity = float.NaN;
	private float _shownFlow = float.NaN;
	private float _shownMix = float.NaN;
	private Vector2 _shownNear = new(float.NaN, 0f);

	// Antes de añadirlo al árbol. floorY: el suelo en los ejes de la nave.
	public void Setup(ShipVisualProfile visual, SonicConeSettings settings, float floorY)
	{
		_settings = settings;
		ProcessMode = ProcessModeEnum.Always;
		CastShadow = ShadowCastingSetting.Off;
		Mesh = SonicConeView.BuildChaseMesh(visual);
		_material = new ShaderMaterial { Shader = SonicConeView.SharedShader };
		_material.SetShaderParameter(SnFloor, floorY);
		MaterialOverride = _material;
	}

	// Un paso de física
	public void Feed(float speed, float baseTopSpeed, bool boosting, float dt)
	{
		_boost = SonicConeCurves.StepBoost(_boost, boosting, _settings, dt);
		_state.Update(speed, baseTopSpeed, _boost, _settings, dt);
	}

	// Recolocación: sin cono al momento
	public void ResetState()
	{
		_boost = 0f;
		_state.Reset();
	}

	public override void _Process(double delta)
	{
		float opacity = SonicConeCurves.RivalOpacity(_state.Amount, ActiveSettings.SpeedLines, _settings);
		if (opacity != _shownOpacity)
		{
			_shownOpacity = opacity;
			_material.SetShaderParameter(SnOpacity, opacity);
		}
		if (opacity <= 0f)
			return;
		if (_state.Flow != _shownFlow)
		{
			_shownFlow = _state.Flow;
			_material.SetShaderParameter(SnFlow, _shownFlow);
		}
		var near = new Vector2(_settings.RivalFadeFrom, _settings.RivalFadeFull);
		if (near != _shownNear)
		{
			_shownNear = near;
			_material.SetShaderParameter(SnNearFade, near);
		}
		float mix = _settings.BoostMix * _boost;
		if (mix != _shownMix)
		{
			_shownMix = mix;
			Color c = Palette.SonicVapor.Lerp(Palette.SonicVaporBoost, mix).SrgbToLinear();
			_material.SetShaderParameter(SnColor, new Vector3(c.R, c.G, c.B));
		}
	}
}
