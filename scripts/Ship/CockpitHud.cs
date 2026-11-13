using System;
using System.Numerics;

namespace Vectory.Ship;

// Rol de color del HUD de cabina. Cada estilo de cabina 3D lo traduce a sus colores (RacingPalette, MilitaryPalette,
// IndustrialPalette).
public enum CockpitColor
{
	Cyan,       // velocidad normal, carga lista en BLADE y HAMMER
	Blue,       // carga vacía
	Orange,     // por encima de la punta o con boost, carga lista en VECTOR, cooldown
	BlueGray,   // rotulaciones, segmentos apagados
	Green,      // daño 0
	Yellow,     // daño 1
	Red,        // daño 2–3, carga bloqueada
	Dim         // apagado tenue
}

// Lo que da el juego para el HUD de cabina
public struct CockpitHudInput
{
	public float SpeedKmh;
	public float TopKmh;           // punta base de la nave, sin daño
	public float MaxKmh;           // punta + impulso del boost
	public bool BoostActive;
	public int Charges;
	public bool Cooldown;
	public int DamageLevel;        // 0–3
	public bool DamageEnabled;     // false en TIME TRIAL

	// Lo que piden las pantallas de la cabina 3D de BLADE (spec docs/cabinas-3d-2-blade.md, 6.1)
	public float HeadingDegrees;   // rumbo: 0 hacia −Z del mundo, en sentido horario visto desde arriba, de 0 a 360
	public float Turn;             // entrada de giro suavizada, de −1 a 1, positiva a la derecha
	public string LapText;         // LAP 2/3, LAP 4, OUT LAP: el texto del HUD de pantalla
	public float LapFraction;      // lo recorrido de la vuelta, de 0 a 1
}

// Intensidad de cada efecto de luz: multiplica el valor del diseño. A 0, se apaga.
public struct CockpitLightSettings
{
	public float BoostTint;
	public float DamageTint;
	public float ScreenGlow;

	public static CockpitLightSettings Default => new() { BoostTint = 1f, DamageTint = 1f, ScreenGlow = 1f };
}

// Color o valor que pasa a otro en un tiempo fijo cuando cambia su objetivo
public sealed class CockpitFade
{
	private readonly float _duration;
	private Vector4 _from;
	private Vector4 _target;
	private float _time;
	private bool _started;

	public CockpitFade(float duration)
	{
		_duration = duration;
	}

	public Vector4 Value { get; private set; }

	public void Update(Vector4 target, float dt)
	{
		if (!_started)
		{
			_started = true;
			_from = _target = Value = target;
			_time = _duration;
			return;
		}

		if (target != _target)
		{
			_from = Value;
			_target = target;
			_time = 0f;
		}

		_time = MathF.Min(_duration, _time + dt);
		Value = _duration > 0f ? Vector4.Lerp(_from, _target, _time / _duration) : _target;
	}
}

// Una esfera de boost o un segmento de daño, ya con su transición
public readonly record struct CockpitHudPaint(Vector4 Fill, Vector4 Stroke, float Glow);

// Cómo se pinta lo apagado: cambia de un estilo de cabina a otro, las reglas no.
// Sin SegmentOffColor, un segmento apagado va con el color del nivel de daño.
public readonly record struct CockpitHudLook
{
	public CockpitColor EmptyOrbColor { get; init; }
	public float EmptyOrbFill { get; init; }
	public float EmptyOrbStroke { get; init; }
	public CockpitColor? SegmentOffColor { get; init; }
	public float SegmentOffStroke { get; init; }
	public CockpitColor DisabledSegmentColor { get; init; }
	public float DisabledSegmentStroke { get; init; }
	public CockpitColor FullOrbColor { get; init; }       // carga lista
	public CockpitColor CooldownOrbColor { get; init; }   // carga en cooldown
}

// Estado del HUD de cabina y su luz (spec docs/naves-3b-cabinas.md, 4.2 y 5), común a todas las cabinas 3D. Lógica pura;
// la paleta y lo apagado se inyectan: son los de cada estilo.
public sealed class CockpitHudState
{
	public const float HudTransitionTime = 0.25f;
	public const float BoostTintTransitionTime = 0.3f;
	public const float BlinkPeriod = 0.5f;          // daño 3: 100 % / 25 %, a 2 Hz
	public const float BlinkDimOpacity = 0.25f;
	public const float DamagePulsePeriod = 1f;      // tinte rojo con daño 3: de 0,45 a 0,20 y vuelta
	public const float DamagePulseHigh = 0.45f;
	public const float DamagePulseLow = 0.2f;
	public const float DamageTintLevel2 = 0.15f;
	public const float BoostTintActive = 0.55f;
	public const float BoostTintOverTop = 0.25f;
	public const float ScreenGlowBase = 0.35f;      // + este mismo valor × fracción de velocidad
	public const float OverTopMarginKmh = 1f;       // margen del diseño para que la punta exacta no cambie el color

	public const int SlotCount = 3;

	private readonly Func<CockpitColor, Vector4> _palette;
	private readonly CockpitHudLook _look;
	private readonly CockpitFade _speedColor = new(HudTransitionTime);
	private readonly CockpitFade[] _orbFill = NewFades();
	private readonly CockpitFade[] _orbStroke = NewFades();
	private readonly CockpitFade[] _orbGlow = NewFades();
	private readonly CockpitFade[] _segmentFill = NewFades();
	private readonly CockpitFade[] _segmentStroke = NewFades();
	private readonly CockpitFade[] _segmentGlow = NewFades();
	private readonly CockpitFade _boostTint = new(BoostTintTransitionTime);
	private float _time;

	public CockpitHudState(Func<CockpitColor, Vector4> palette, CockpitHudLook look)
	{
		_palette = palette;
		_look = look;
	}

	// Por encima de la punta o con boost
	public bool OverTop { get; private set; }

	public float SpeedFraction { get; private set; }
	public float TopFraction { get; private set; } = 396f / 496f;
	public float MaxKmh { get; private set; } = 496f;
	public string SpeedText { get; private set; } = "0";
	public Vector4 SpeedColor => _speedColor.Value;
	public string BoostText { get; private set; } = "";
	public string DamageText { get; private set; } = "";
	public Vector4 DamageTextColor { get; private set; }
	public float ScreenGlow { get; private set; }
	public float BoostTint => _boostTint.Value.X;
	public float DamageTint { get; private set; }
	public bool SegmentsBlinking { get; private set; }

	// Fase del parpadeo del daño a 2 Hz: encendido la primera mitad de cada medio segundo
	public bool BlinkOn => _time % BlinkPeriod < BlinkPeriod * 0.5f;
	public int DamageLevel { get; private set; }
	public bool DamageEnabled { get; private set; }

	public CockpitHudPaint Orb(int i) => new(_orbFill[i].Value, _orbStroke[i].Value, _orbGlow[i].Value.X);

	public CockpitHudPaint Segment(int i)
	{
		float blink = SegmentsBlinking && !BlinkOn ? BlinkDimOpacity : 1f;
		return new CockpitHudPaint(WithAlpha(_segmentFill[i].Value, blink), WithAlpha(_segmentStroke[i].Value, blink), _segmentGlow[i].Value.X * blink);
	}

	// Avanza dt segundos. La pausa no lo llama: queda todo congelado.
	public void Update(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_time += dt;

		float max = input.MaxKmh > 0f ? input.MaxKmh : 1f;
		MaxKmh = max;
		SpeedFraction = Math.Clamp(input.SpeedKmh / max, 0f, 1f);
		TopFraction = Math.Clamp(input.TopKmh / max, 0f, 1f);
		SpeedText = MathF.Round(input.SpeedKmh).ToString(System.Globalization.CultureInfo.InvariantCulture);

		bool overTop = input.SpeedKmh > input.TopKmh + OverTopMarginKmh || input.BoostActive;
		OverTop = overTop;
		_speedColor.Update(_palette(overTop ? CockpitColor.Orange : CockpitColor.Cyan), dt);

		UpdateBoost(input, dt);
		UpdateDamage(input, light, dt);

		ScreenGlow = (ScreenGlowBase + ScreenGlowBase * SpeedFraction) * light.ScreenGlow;
		float tint = input.BoostActive ? BoostTintActive : overTop ? BoostTintOverTop : 0f;
		_boostTint.Update(new Vector4(tint * light.BoostTint, 0f, 0f, 0f), dt);
	}

	private void UpdateBoost(in CockpitHudInput input, float dt)
	{
		bool blocked = input.DamageEnabled && input.DamageLevel >= DamageModel.MaxLevel;
		for (int i = 0; i < SlotCount; i++)
		{
			bool has = i < input.Charges;
			(CockpitColor color, float fill, float stroke, float glow) =
				has && blocked ? (CockpitColor.Red, 0.3f, 0.55f, 0f)
				: has && input.Cooldown ? (_look.CooldownOrbColor, 0.3f, 0.55f, 0f)
				: has ? (_look.FullOrbColor, 1f, 1f, 1f)
				: (_look.EmptyOrbColor, _look.EmptyOrbFill, _look.EmptyOrbStroke, 0f);
			_orbFill[i].Update(WithAlpha(_palette(color), fill), dt);
			_orbStroke[i].Update(WithAlpha(_palette(color), stroke), dt);
			_orbGlow[i].Update(new Vector4(glow, 0f, 0f, 0f), dt);
		}

		BoostText = blocked && input.Charges > 0 ? "LOCKED"
			: input.BoostActive ? "ACTIVE"
			: input.Cooldown && input.Charges > 0 ? "COOLDOWN"
			: input.Charges > 0 ? "READY"
			: "CHARGING";
	}

	private void UpdateDamage(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		int level = input.DamageEnabled ? Math.Clamp(input.DamageLevel, 0, DamageModel.MaxLevel) : 0;
		DamageLevel = level;
		DamageEnabled = input.DamageEnabled;
		CockpitColor color = level == 0 ? CockpitColor.Green : level == 1 ? CockpitColor.Yellow : CockpitColor.Red;

		for (int i = 0; i < SlotCount; i++)
		{
			bool on = i < level;
			Vector4 fill = WithAlpha(_palette(color), on ? 1f : 0f);
			Vector4 stroke = !input.DamageEnabled
				? WithAlpha(_palette(_look.DisabledSegmentColor), _look.DisabledSegmentStroke)
				: on ? _palette(color)
				: WithAlpha(_palette(_look.SegmentOffColor ?? color), _look.SegmentOffStroke);
			_segmentFill[i].Update(fill, dt);
			_segmentStroke[i].Update(stroke, dt);
			_segmentGlow[i].Update(new Vector4(on ? 1f : 0f, 0f, 0f, 0f), dt);
		}

		SegmentsBlinking = level == DamageModel.MaxLevel;
		DamageText = !input.DamageEnabled ? "" : new[] { "NOMINAL", "LIGHT", "HEAVY", "CRITICAL" }[level];
		DamageTextColor = _palette(color);

		float tint = level >= DamageModel.MaxLevel
			? DamagePulseLow + (DamagePulseHigh - DamagePulseLow) * (0.5f + 0.5f * MathF.Cos(MathF.Tau * _time / DamagePulsePeriod))
			: level == 2 ? DamageTintLevel2 : 0f;
		DamageTint = tint * light.DamageTint;
	}

	private static CockpitFade[] NewFades()
	{
		var fades = new CockpitFade[SlotCount];
		for (int i = 0; i < SlotCount; i++)
			fades[i] = new CockpitFade(HudTransitionTime);
		return fades;
	}

	private static Vector4 WithAlpha(Vector4 color, float factor) => new(color.X, color.Y, color.Z, color.W * factor);
}

// Qué se ve en cada cámara (spec docs/cabinas-3d-1-vector.md, 4.7, docs/cabinas-3d-2-blade.md, 4.7, y
// docs/cabinas-3d-3-hammer.md, 4.7). Posición, vuelta y tiempos se ven siempre. La cabina 3D lleva en sus pantallas
// velocidad, boost, daño, radar y minimapa; mientras sus pantallas son de prueba (screensShowHud en false), siguen en
// el HUD de pantalla para poder conducir.
public readonly record struct CockpitViewRules(bool Cockpit3D, bool ScreenDriveHud, bool ScreenRadar)
{
	public static CockpitViewRules For(CameraView view, bool screensShowHud = true)
	{
		if (view != CameraView.Cockpit)
			return new CockpitViewRules(false, true, true);
		return screensShowHud
			? new CockpitViewRules(true, false, false)
			: new CockpitViewRules(true, true, true);
	}
}

// Datos de navegación de las pantallas de la cabina 3D de BLADE (spec docs/cabinas-3d-2-blade.md, 6.1). Lógica pura.
public static class CockpitInstruments
{
	// Rumbo en grados (0–360) del frente en planta (X, Z del mundo): 0 hacia −Z y creciendo hacia la derecha, como el
	// prototipo (atan2(frente.x, −frente.z))
	public static float HeadingDegrees(Vector2 planForward)
	{
		float degrees = MathF.Atan2(planForward.X, -planForward.Y) * 180f / MathF.PI;
		return degrees < 0f ? degrees + 360f : degrees;
	}

	// Lo recorrido de la vuelta (0–1): la distancia sobre el eje desde la meta entre la longitud de la vuelta
	public static float LapFraction(float distance, float lapLength) =>
		lapLength > 0f ? Math.Clamp(distance / lapLength, 0f, 1f) : 0f;

	// Porcentaje entero, redondeando las mitades hacia arriba, como Math.round
	public static int LapPercent(float fraction) => (int)MathF.Floor(fraction * 100f + 0.5f);
}
