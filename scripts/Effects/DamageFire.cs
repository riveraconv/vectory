using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Ship;

namespace Vectory.Effects;

// Ajustes del fuego del nivel 3 (spec docs/efectos-4-humo-danos.md, 5). Distancias en m, velocidades en m/s.
// Colores propios del efecto (no salen de Palette, como los demás efectos). Lógica pura: datos.
// Decisión del usuario (v1.3): en lugar de las 4 gotas de la maqueta, una llama por foco dibujada con ruido animado, y un
// halo pequeño y tenue. (v1.4): barrida por el viento —con la velocidad se inclina hacia atrás y se alarga— y, de vez en
// cuando, una llamarada en algún foco.
public sealed class DamageFireSettings
{
	public float FlameWidth = 0.22f, FlameHeight = 0.40f;   // m, a tamaño 1 y parada (la base, en el foco)
	// m que la base se mete en la piel con la nave parada; inclinada, nada: tumbada a lo largo del casco se quedaría dentro
	public float FlameSink = 0.06f;
	public float FlameOpacity = 0.9f;
	public float FlickerMin = 0.85f, FlickerMax = 1.15f;    // el largo oscila entre estos factores
	public float NoiseSpeed = 2.2f;                         // lo que corre el ruido por segundo, parada (en largos de la llama)
	public float NoiseSpeedAtFull = 5f;                     // ídem, inclinada del todo
	public Vector3 CoreColor = new(1f, 0.86f, 0.55f);       // núcleo, amarillo claro
	public Vector3 MidColor = new(1f, 0.45f, 0.10f);        // cuerpo, naranja
	public Vector3 EdgeColor = new(0.55f, 0.08f, 0.02f);    // bordes, rojo oscuro

	// --- Viento: con la velocidad la llama se inclina hacia atrás, se alarga y se estrecha
	public float LeanSpeed = 55f;                           // m/s (~200 km/h) desde los que va inclinada del todo
	public float LeanUpKeep = 0.4f;                         // lo que sigue subiendo inclinada del todo (respecto a hacia atrás)
	public float LeanStretch = 1f;                          // largo × (1 + esto) inclinada del todo
	public float LeanNarrow = 0.2f;                         // ancho × (1 − esto) inclinada del todo

	// --- Llamaradas: en cada tramo de FlarePeriod segundos, con FlareChance, una llamarada; cortas y largas se alternan
	// de un tramo al siguiente (y entre focos), cada una ± FlareDurationJitter (decisión del usuario: antes, todas de 0,4 s)
	public float FlarePeriod = 3f;
	public float FlareChance = 0.55f;
	public float FlareShort = 0.25f, FlareLong = 0.6f;      // s
	public float FlareDurationJitter = 0.2f;                // fracción
	public float FlareRise = 0.2f;                          // fracción de la duración en llegar al máximo
	public float FlareLength = 0.8f;                        // largo × (1 + esto) en el máximo
	public float FlareWidth = 0.35f;                        // ancho × (1 + esto)
	public float FlareBrightness = 0.3f;                    // opacidad × (1 + esto); también el halo

	public float HaloSize = 0.8f;
	public float HaloOpacity = 0.12f;
	public float HaloRise = 0.12f;
	public Vector3 HaloColor = new(1f, 0x6A / 255f, 0x1E / 255f);     // #FF6A1E
	public float ShipPhase = 1.37f;                         // s de desfase por id de nave

	// Tinte del humo recién salido (4.3)
	public Vector3 TintColor = new(0.75f, 0.30f, 0.10f);
	public float TintUntil = 0.3f;                          // fracción de la vida
	public float TintWeight = 0.8f;
}

// Ajustes de las brasas (6). Velocidades en m/s, tiempos en s. Lógica pura: datos.
public sealed class DamageEmberSettings
{
	public float MediumRate = 3f;        // por segundo, en Smoke
	public float StrongRatePerFocus = 5f;
	public float Lift = 0.1f;            // m sobre el foco al nacer
	public float LagFraction = 0.1f, LagMax = 9f;           // retraso: mín(9; 10 % de la velocidad)
	public float LagScaleMin = 0.6f, LagScaleMax = 1.4f;
	public float SideSpeed = 1.5f;       // ± a los lados
	public float UpMin = 1f, UpMax = 3f;
	public float Gravity = 2f;
	public float Sway = 2f, SwayFrequency = 5f;             // m/s² de vaivén lateral
	public float LifeMin = 0.5f, LifeMax = 1.0f;
	public float StreakTime = 0.04f;     // s de trazo
	public Vector3 Color = new(1f, 0.55f, 0.14f);
	public float FlickerFrequency = 40f;
	public int MaxEmbers = 400;
}

// La llama o el halo de un foco: su inclinación (0 recta, 1 barrida del todo), su largo a lo largo de su eje, su ancho, su
// opacidad, el tiempo de su ruido y su semilla, y lo que sube su centro sobre el foco (solo el halo)
public readonly record struct FireSprite(float Lean, float Length, float Width, float Opacity, float NoiseTime, float Seed, float Rise);

// Fuego del nivel 3 (5): una llama y un halo en cada foco, con su parpadeo, su inclinación por el viento y sus
// llamaradas. time: el de la carrera más el desfase de la nave (la pausa lo congela). Lógica pura: la forma y el color de
// la llama los pone su shader con ruido.
public static class DamageFire
{
	public static bool Burns(int level) => level >= 3;

	public static float Phase(int shipId, DamageFireSettings s) => shipId * s.ShipPhase;

	// Parpadeo del foco f, de 0 a 1
	public static float Flicker(int focus, float time) =>
		0.5f + 0.5f * MathF.Sin(23f * time + 3f * focus) * MathF.Sin(13f * time + 1.7f * focus);

	// Inclinación por la velocidad de la nave: 0 parada, 1 desde LeanSpeed (con una curva suave)
	public static float Lean(float speed, DamageFireSettings s)
	{
		float x = s.LeanSpeed > 0f ? Math.Clamp(speed / s.LeanSpeed, 0f, 1f) : 1f;
		return x * x * (3f - 2f * x);
	}

	// Eje de la llama: de la vertical del mundo (parada) a casi hacia atrás (back: hacia atrás en horizontal)
	public static Vector3 Axis(Vector3 back, float lean, DamageFireSettings s)
	{
		Vector3 axis = Vector3.UnitY * MathF.Max(s.LeanUpKeep, 1f - lean) + back * lean;
		return axis.LengthSquared() > 1e-8f ? Vector3.Normalize(axis) : Vector3.UnitY;
	}

	// Llamarada del foco f en time, de 0 a 1: en cada tramo, sale o no (FlareChance) en un momento al azar del tramo, sube
	// en FlareRise de su duración y se apaga en el resto. Determinista: no necesita generador.
	public static float Flare(int focus, float time, DamageFireSettings s)
	{
		if (s.FlarePeriod <= 0f)
			return 0f;
		int slot = (int)MathF.Floor(time / s.FlarePeriod);
		if (Hash(slot, focus, 1) >= s.FlareChance)
			return 0f;
		float duration = FlareDuration(slot, focus, s);
		if (duration <= 0f)
			return 0f;
		float start = Hash(slot, focus, 2) * MathF.Max(0f, s.FlarePeriod - duration);
		float x = (time - slot * s.FlarePeriod - start) / duration;
		if (x < 0f || x > 1f)
			return 0f;
		float rise = Math.Clamp(s.FlareRise, 0.01f, 0.99f);
		float env = x < rise ? x / rise : 1f - (x - rise) / (1f - rise);
		return env * env * (3f - 2f * env);
	}

	// Lo que la base de la llama se mete en la piel con esa inclinación
	public static float Sink(float lean, float size, DamageFireSettings s) => s.FlameSink * size * (1f - lean);

	public static FireSprite Flame(int focus, float size, float time, float speed, DamageFireSettings s)
	{
		float lean = Lean(speed, s);
		float flare = Flare(focus, time, s);
		float flicker = s.FlickerMin + (s.FlickerMax - s.FlickerMin) * Flicker(focus, time);
		float length = s.FlameHeight * size * flicker * (1f + s.LeanStretch * lean) * (1f + s.FlareLength * flare);
		float width = s.FlameWidth * size * (1f - s.LeanNarrow * lean) * (1f + s.FlareWidth * flare);
		float opacity = MathF.Min(1f, s.FlameOpacity * (1f + s.FlareBrightness * flare));
		float noiseSpeed = s.NoiseSpeed + (s.NoiseSpeedAtFull - s.NoiseSpeed) * lean;
		return new FireSprite(lean, length, width, opacity, time * noiseSpeed, focus * 7.31f, 0f);
	}

	public static FireSprite Halo(int focus, float size, float time, DamageFireSettings s)
	{
		float fk = 0.75f + 0.25f * MathF.Sin(31f * time + focus) * MathF.Sin(17f * time);
		float flare = Flare(focus, time, s);
		return new FireSprite(0f, s.HaloSize * size, s.HaloSize * size, s.HaloOpacity * fk * (1f + s.FlareBrightness * flare),
			0f, 0f, s.HaloRise * size);
	}

	// Duración de la llamarada de un tramo: corta y larga alternas, de un tramo al siguiente y entre focos vecinos
	public static float FlareDuration(int slot, int focus, DamageFireSettings s)
	{
		float baseDuration = ((slot + focus) & 1) == 0 ? s.FlareShort : s.FlareLong;
		float jitter = 1f + s.FlareDurationJitter * (2f * Hash(slot, focus, 3) - 1f);
		return MathF.Min(baseDuration * jitter, s.FlarePeriod);
	}

	// 0–1 a partir de enteros (para las llamaradas)
	private static float Hash(int a, int b, int c)
	{
		unchecked
		{
			uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2246822519u;
			h = (h ^ (h >> 13)) * 1274126177u;
			h ^= h >> 16;
			return (h & 0xFFFFFF) / 16777216f;
		}
	}

	// Color del humo con el fuego (4.3): durante el primer 30 % de su vida, mezclado con naranja
	public static Vector3 Tint(Vector3 color, float u, DamageFireSettings s)
	{
		if (u >= s.TintUntil || s.TintUntil <= 0f)
			return color;
		float k = s.TintWeight * (1f - u / s.TintUntil);
		return Vector3.Lerp(color, s.TintColor, k);
	}
}

// Una brasa del humo de daños: posición, velocidad, edad, vida, fase del vaivén y del parpadeo, y su nave
public struct DamageEmber
{
	public int ShipId;
	public Vector3 Position;
	public Vector3 Velocity;
	public Vector3 SideAxis;
	public float Age;
	public float Life;
	public float Phase;
}

// Brasas del humo de daños de todas las naves (6): un acumulador por nave, nacen en un foco al azar de los activos,
// gravedad suave y vaivén lateral; no rebotan ni chocan. Con el máximo lleno, las nuevas no salen. Lógica pura.
public sealed class DamageEmbers
{
	private readonly DamageEmberSettings _s;
	private readonly DamageSmokeSettings _smoke;
	private readonly Random _random;
	private readonly List<DamageEmber> _embers = new();
	private readonly Dictionary<int, float> _accumulators = new();

	public DamageEmbers(DamageSmokeSettings settings, Random random)
	{
		_smoke = settings ?? throw new ArgumentNullException(nameof(settings));
		_s = settings.Embers;
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	public int Count => _embers.Count;
	public DamageEmber this[int index] => _embers[index];

	// Brasas por segundo de un nivel en HIGH: 3 en el 2, 5 por foco en el 3
	public float Rate(int level) => level < 2 ? 0f : level == 2 ? _s.MediumRate : _s.StrongRatePerFocus * DamageSmokeFocusRules.Count(level);

	public int Emit(int shipId, int level, in DamageSmokeFoci foci, in ShipPose pose, Vector3 velocity, float quality, float dt)
	{
		int focusCount = DamageSmokeFocusRules.Count(level);
		if (focusCount == 0 || dt <= 0f)
		{
			if (focusCount == 0)
				_accumulators.Remove(shipId);
			return 0;
		}
		_accumulators.TryGetValue(shipId, out float acc);
		acc += Rate(level) * quality * dt;
		var plan = new Vector3(velocity.X, 0f, velocity.Z);
		Vector3 back = plan.LengthSquared() > 0.01f ? -Vector3.Normalize(plan) : Vector3.UnitZ;
		var side = new Vector3(-back.Z, 0f, back.X);
		float lag = MathF.Min(_s.LagMax, _s.LagFraction * velocity.Length());
		int added = 0;
		while (acc >= 1f)
		{
			acc -= 1f;
			if (_embers.Count >= _s.MaxEmbers)
				continue;
			int f = Math.Min(focusCount - 1, (int)(_random.NextSingle() * focusCount));
			SmokeFocus focus = DamageSmokeFocusRules.At(f, foci, _smoke);
			_embers.Add(new DamageEmber
			{
				ShipId = shipId,
				Position = pose.Point(focus.Local) + Vector3.UnitY * _s.Lift,
				Velocity = velocity + back * (lag * Range(_s.LagScaleMin, _s.LagScaleMax))
					+ side * Range(-_s.SideSpeed, _s.SideSpeed) + Vector3.UnitY * Range(_s.UpMin, _s.UpMax),
				SideAxis = side,
				Age = 0f,
				Life = Range(_s.LifeMin, _s.LifeMax),
				Phase = Range(0f, MathF.Tau)
			});
			added++;
		}
		_accumulators[shipId] = acc;
		return added;
	}

	// Con dt 0 (pausa) no cambia nada
	public void Update(float dt)
	{
		if (dt <= 0f)
			return;
		for (int i = _embers.Count - 1; i >= 0; i--)
		{
			DamageEmber e = _embers[i];
			e.Age += dt;
			if (e.Age >= e.Life)
			{
				int last = _embers.Count - 1;
				_embers[i] = _embers[last];
				_embers.RemoveAt(last);
				continue;
			}
			e.Velocity += (-Vector3.UnitY * _s.Gravity + e.SideAxis * (_s.Sway * MathF.Sin(_s.SwayFrequency * e.Age + e.Phase))) * dt;
			e.Position += e.Velocity * dt;
			_embers[i] = e;
		}
	}

	public void Clear()
	{
		_embers.Clear();
		_accumulators.Clear();
	}

	public void Clear(int shipId)
	{
		for (int i = _embers.Count - 1; i >= 0; i--)
			if (_embers[i].ShipId == shipId)
			{
				int last = _embers.Count - 1;
				_embers[i] = _embers[last];
				_embers.RemoveAt(last);
			}
		_accumulators.Remove(shipId);
	}

	// Brillo de la cabeza del trazo: naranja × (1 − u) × parpadeo
	public Vector3 ColorOf(in DamageEmber e)
	{
		float u = Math.Clamp(e.Age / e.Life, 0f, 1f);
		return _s.Color * ((1f - u) * (0.7f + 0.3f * MathF.Sin(_s.FlickerFrequency * e.Age + e.Phase)));
	}

	// Cola del trazo: hacia atrás de la velocidad relativa a reference (tu nave), StreakTime segundos
	public Vector3 Tail(in DamageEmber e, Vector3 reference) => -(e.Velocity - reference) * _s.StreakTime;

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();
}
