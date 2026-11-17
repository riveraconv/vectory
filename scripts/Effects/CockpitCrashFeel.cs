using System;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Effects;

// Ajustes de cómo se nota un choque desde la cabina (decisión del usuario sobre la spec docs/efectos-1-choques.md:
// desde la cabina no se veía si se estaba rozando). Luz: intensidad relativa (1 = el roce más fuerte), que quien la
// pinta multiplica por la suya. Temblor: m de desplazamiento del ojo.
public sealed class CockpitCrashFeelSettings
{
	public float GrazeLightMin = 0.5f;        // el roce parpadea entre estas dos
	public float GrazeLightMax = 1f;
	public float GrazeFlickerRate = 30f;      // cambios por segundo, como el destello del roce
	public float GrazeLightFade = 0.1f;       // s en apagarse al dejar de rozar
	public float MediumLight = 1.8f;
	public float MediumLightDuration = 0.3f;
	public float StrongLight = 2.6f;
	public float StrongLightDuration = 0.45f;

	public float RumbleAmplitude = 0.012f;    // m mientras se roza
	public float RumbleSmoothing = 0.05f;     // s: entra y sale casi al momento
	public float RumbleFrequency = 31f;       // Hz base: más fino que la vibración por velocidad

	public static CockpitCrashFeelSettings Default => new();
}

// Luz de las chispas en la cabina por cada lado y temblor fino mientras se roza (solo la nave del jugador). Un paso de
// física por llamada a Step; con dt 0 (pausa) no cambia nada. Random inyectado para el parpadeo. Lógica pura.
public sealed class CockpitCrashFeel
{
	private readonly CockpitCrashFeelSettings _s;
	private readonly Random _random;
	private readonly Side[] _sides = { new(), new() };
	private float _rumbleTarget;

	private sealed class Side
	{
		public bool Grazing;
		public float Graze;          // valor del parpadeo ahora
		public float FlickerClock;   // s hasta el siguiente cambio
		public float Fade;           // 1 → 0 al dejar de rozar
		public float ImpactPeak;
		public float ImpactDuration;
		public float ImpactAge = float.MaxValue;
	}

	public CockpitCrashFeel(CockpitCrashFeelSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	public float Rumble { get; private set; }   // 0..1, suavizado
	public float Time { get; private set; }

	// Contacto de este paso en un lado: roce continuo o golpe medio / fuerte que cuenta
	public void Contact(BarrierSide side, ImpactLevel level, bool isNewImpact)
	{
		Side s = _sides[(int)side];
		if (isNewImpact && level != ImpactLevel.Graze)
		{
			bool strong = level == ImpactLevel.Strong;
			s.ImpactPeak = strong ? _s.StrongLight : _s.MediumLight;
			s.ImpactDuration = strong ? _s.StrongLightDuration : _s.MediumLightDuration;
			s.ImpactAge = 0f;
			return;
		}
		if (!s.Grazing && s.Fade <= 0f)
			s.FlickerClock = 0f;   // al empezar, ya con luz
		s.Grazing = true;
		_rumbleTarget = 1f;
	}

	// Avanza el paso. Lo que no se haya marcado con Contact en este paso se apaga.
	public void Step(float dt)
	{
		if (dt <= 0f)
			return;
		Time += dt;
		foreach (Side s in _sides)
		{
			if (s.Grazing)
			{
				s.Fade = 1f;
				s.FlickerClock -= dt;
				if (s.FlickerClock <= 0f)
				{
					s.Graze = _s.GrazeLightMin + (_s.GrazeLightMax - _s.GrazeLightMin) * _random.NextSingle();
					s.FlickerClock += _s.GrazeFlickerRate > 0f ? 1f / _s.GrazeFlickerRate : 0f;
					if (s.FlickerClock < 0f)
						s.FlickerClock = 0f;
				}
			}
			else if (s.Fade > 0f)
			{
				s.Fade = _s.GrazeLightFade > 0f ? MathF.Max(0f, s.Fade - dt / _s.GrazeLightFade) : 0f;
			}
			s.Grazing = false;
			if (s.ImpactAge < float.MaxValue)
				s.ImpactAge += dt;
		}

		Rumble = _s.RumbleSmoothing > 0f
			? Rumble + (_rumbleTarget - Rumble) * (1f - MathF.Exp(-dt / _s.RumbleSmoothing))
			: _rumbleTarget;
		_rumbleTarget = 0f;
	}

	// Luz de las chispas de un lado (0 = nada): el mayor del roce y del golpe
	public float Light(BarrierSide side)
	{
		Side s = _sides[(int)side];
		float graze = s.Graze * s.Fade;
		float impact = 0f;
		if (s.ImpactDuration > 0f && s.ImpactAge < s.ImpactDuration)
		{
			float u = s.ImpactAge / s.ImpactDuration;
			impact = s.ImpactPeak * (1f - u) * (1f - u);
		}
		return MathF.Max(graze, impact);
	}

	// Desplazamiento del ojo por el temblor (m), en los ejes de la pantalla: suma de senos, determinista con el tiempo
	public Vector2 RumbleOffset(float amplitudeScale = 1f)
	{
		float a = _s.RumbleAmplitude * Rumble * amplitudeScale;
		if (a <= 0f)
			return Vector2.Zero;
		float w = MathF.Tau * _s.RumbleFrequency * Time;
		float x = 0.6f * MathF.Sin(w) + 0.4f * MathF.Sin(w * 1.73f + 1.1f);
		float y = 0.6f * MathF.Sin(w * 1.31f + 0.4f) + 0.4f * MathF.Sin(w * 2.11f + 2.3f);
		return new Vector2(x, y) * a;
	}

	public void Reset()
	{
		foreach (Side s in _sides)
		{
			s.Grazing = false;
			s.Graze = 0f;
			s.Fade = 0f;
			s.ImpactAge = float.MaxValue;
			s.ImpactDuration = 0f;
		}
		Rumble = 0f;
		_rumbleTarget = 0f;
	}
}
