using System;
using System.Numerics;
using Vectory.Ship;

namespace Vectory.Effects;

// Llama de las toberas (spec docs/efectos-6-toberas.md, secciones 3, 4 y 10). Valores de la maqueta
// designs/efectos-toberas.html; los tiempos (τ) en s, los largos y radios en m, los radios los de VECTOR (cada nave los
// escala con su ThrusterShape.Scale). Los colores, tal cual los da la maqueta (en el shader no se convierten).
public sealed class ThrusterFxSettings
{
	// --- Potencia P (3.1): objetivo con gas = base + velocidad × th; sin gas, ídem; frenando, fijo; parada, como mínimo el ralentí
	public float ThrottleBase = 0.35f, ThrottleSpeed = 0.65f;
	public float BrakePower = 0.06f;
	public float CoastBase = 0.12f, CoastSpeed = 0.22f;
	public float IdlePower = 0.14f;
	public float IdleSpeed = 1f;                                  // m/s por debajo de los que la nave está parada
	public float PowerRise = 0.12f, PowerFall = 0.3f, PowerBrakeFall = 0.06f;

	// --- Boost de la llama bt, encendido y frenada (3.2)
	public float BoostRise = 0.18f, BoostFall = 0.45f;
	public float KickGain = 0.45f, KickTau = 0.35f;               // el largo × (1 + KickGain × kick) al empezar un boost
	public float BrakeFlickTau = 0.2f;

	// --- Giro y derrape (3.3)
	public float SteerSmoothing = 0.12f;
	public float OuterPush = 0.28f;                               // la de fuera, hasta un 28 % más larga (y la de dentro, más corta)
	public float OuterPushSpeed = 1.4f;                           // × th, con tope en 1
	public float MaxSlipDegrees = 22f;                            // βmáx: la punta, doblada del todo desde aquí

	// --- Parpadeo q (3.4)
	public float FlickerBase = 0.07f, FlickerDrift = 0.18f, FlickerBrake = 0.35f;

	// --- Forma (4.2)
	public float LengthBase = 0.45f, LengthPower = 3.1f, LengthBoost = 4.2f;
	public float WidthBase = 0.85f, WidthPower = 0.25f, WidthBoost = 0.35f;
	public float BendDrift = 0.22f, BendSteer = 0.06f;            // × largo
	public float PlumeMouthRadius = 0.27f, PlumeTipRadius = 0.10f;
	public float CoreMouthRadius = 0.15f, CoreTipRadius = 0.02f;
	public float CoreLength = 0.55f, CoreBend = 0.5f;

	// --- Brillo (4.4); Brightness: el ajuste a ojo con el glow del juego
	public float BrightBase = 0.9f, BrightPower = 1.6f, BrightBoost = 1.6f;
	public float BrightTurnBase = 0.85f, BrightTurn = 0.15f;
	public float CoreBrightness = 1.25f;
	public float Brightness = 1f;

	// --- Velocidad del ruido (4.3): lo que corre hacia atrás por segundo
	public float NoiseBase = 4f, NoisePower = 10f, NoiseBoost = 8f;

	// --- Colores con boost (4.4): los mismos en todas las naves
	public Vector3 BoostCore = new(1f, 0.95f, 0.85f);
	public Vector3 BoostMid = new(1f, 0.62f, 0.20f);
	public Vector3 BoostOut = new(1f, 0.30f, 0.12f);

	// --- Anillo de encendido del boost (4.5): de RingStart a RingEnd m en RingDuration s; la segunda boca, RingDelay s después
	public float RingStart = 0.3f, RingEnd = 2.7f, RingDuration = 0.35f;
	public float RingAspect = 0.75f;                              // alto / ancho
	public float RingIntensity = 1.4f;
	public float RingDelay = 0.04f;
	public float RingBehind = 0.15f;                              // m por detrás de la boca
	public Vector3 RingColor = new(1f, 0.434f, 0.117f);          // #FFB060 (en la maqueta, un color hexadecimal: pasa a lineal)

	// --- Boca (4.6): emisión × (base + P × … + bt × …) × q / norma; con P = 0,3 y sin boost, la de siempre
	public float MouthBase = 0.6f, MouthPower = 1.6f, MouthBoost = 1.5f, MouthNorm = 1.08f;

	// --- Diamantes de choque (5.1): fuerza D = máx(0, (P − desde) / rango) × ganancia + bt; en el penacho, × DiamondGlow
	public float DiamondFrom = 0.6f, DiamondRange = 0.4f, DiamondGain = 0.55f, DiamondBoost = 1f;
	public float DiamondGlow = 2.2f;

	// --- Calor (5.2): plano detrás de la boca a base + largo × … m, de ancho y alto base + largo × …; fuerza k = base +
	// P × … + bt × …; desplazamiento en coordenadas de pantalla × HeatDisplacement
	public float HeatBehind = 0.8f, HeatBehindLength = 0.55f;
	public float HeatWidth = 1.3f, HeatWidthLength = 0.25f;
	public float HeatHeight = 1.1f, HeatHeightLength = 0.5f;
	public float HeatBase = 0.25f, HeatPower = 0.75f, HeatBoost = 1f;
	public float HeatDisplacement = 0.045f;
	// El desplazamiento es en coordenadas de pantalla: sin esto, una nave lejana (plano pequeño) tomaría píxeles de muy
	// lejos y se vería doble. Hasta HeatReferenceDistance m de la cámara, el de la maqueta (su cámara exterior); más
	// lejos, × referencia / distancia; y de HeatFadeStart a HeatFadeEnd m se apaga del todo
	public float HeatReferenceDistance = 8f;
	public float HeatFadeStart = 30f, HeatFadeEnd = 60f;

	// --- Luz en el casco (5.3): OmniLight3D LightBehind m detrás de la boca; energía E0 × (base + P × … + bt × …) × q
	public float LightBehind = 0.7f, LightRange = 7f, LightAttenuation = 1.6f;
	public float LightEnergy = 4f;                               // E0, ajustado a ojo (con 1 la popa apenas se ilumina)
	public float LightBase = 0.3f, LightPower = 0.9f, LightBoost = 1f;

	// --- Retroempuje (6): fuerza R con S por encima de RetroMinSpeed m/s; largo (base + R × …) × (1 + vaivén × sen)
	public float RetroMinSpeed = 20f / 3.6f;
	public float RetroRise = 0.05f, RetroFall = 0.15f;
	public float RetroLengthBase = 0.35f, RetroLengthForce = 0.9f, RetroWobble = 0.25f;
	public float RetroCoreLength = 0.6f;
	public float RetroPlumeMouthRadius = 0.13f, RetroPlumeTipRadius = 0.03f;
	public float RetroCoreMouthRadius = 0.07f, RetroCoreTipRadius = 0.03f;
	public float RetroWidth = 0.55f;
	public float RetroPlumeBrightness = 1.8f, RetroCoreBrightness = 2.6f;   // × R (en el shader, el núcleo × 1,6)
	public float RetroNoiseSpeed = 12f;
	public float RetroVisible = 0.01f;                                      // con menos fuerza no se ve
	public Vector3 RetroCore = new(0.9f, 0.98f, 1f);
	public Vector3 RetroMid = new(0.45f, 0.8f, 1f);
	public Vector3 RetroOut = new(0.14f, 0.36f, 1f);

	// --- Daño 3 (7): chasquidos cada PopMin–PopMax s, que saltan a 1 y caen con PopTau; cortes: al acabar su espera, con
	// CutChance la llama baja a CutFactor durante CutMin–CutMax s; si no, otra espera de WaitMin–WaitMax s
	public float PopMin = 0.4f, PopMax = 1.2f, PopTau = 0.08f;
	public float PopIntensity = 1.6f;
	public float PopSize = 0.6f, PopGrow = 1.2f;                            // m: tamaño + crecimiento × (1 − fuerza)
	public float PopBehind = 0.6f;                                          // m por detrás de la boca
	public Vector3 PopColor = new(1f, 0.195f, 0.024f);                     // #FF7A2A (hexadecimal de la maqueta: pasa a lineal)
	public float CutChance = 0.18f, CutFactor = 0.15f;
	public float CutMin = 0.05f, CutMax = 0.12f;
	public float WaitMin = 0.12f, WaitMax = 0.5f;

	public static ThrusterFxSettings Default => new();
}

// Lo que hace una nave en este instante, para sus toberas: entradas (todo o nada; el giro, +1 izquierda), derrape y su
// ángulo (rad, positivo = desliza a la derecha, como ShipDynamics.SlipAngle), boost, velocidad real y punta base (m/s)
public struct ThrusterInput
{
	public bool Accelerate;
	public bool Brake;
	public float Steer;
	public bool Drifting;
	public float SlipAngle;
	public bool Boosting;
	public float Speed;
	public float TopSpeed;
	public int DamageLevel;   // 0–3; solo RACE (en TIME TRIAL, 0)

	public static float SteerOf(in ShipInput input) => (input.SteerLeft ? 1f : 0f) - (input.SteerRight ? 1f : 0f);
}

// Cómo se ve una tobera en este instante: lo que el shader necesita. Length, Bend y los radios en m (del penacho; el
// núcleo sale de estos con los factores de los ajustes); Brightness es I sin el parpadeo; Flicker, q. Mouth: el factor de
// la emisión de la boca. Ring: tamaño (m, ancho) e intensidad del anillo de encendido (0 = no se ve).
public struct ThrusterFlame
{
	public float Length;
	public float Width;
	public float Bend;
	public float Brightness;
	public float Flicker;
	public float Turn;
	public Vector3 Core;
	public Vector3 Mid;
	public Vector3 Out;
	public float Mouth;
	public float RingSize;
	public float RingIntensity;
	public float Diamonds;     // D (5.1)
	public float Heat;         // k (5.2); 0 = no se dibuja
	public float LightEnergy;  // energía de su luz (5.3)
	public float Cut;          // corte del daño 3 (7.2): 1, o CutFactor
	public float PopIntensity; // chasquido del daño 3 (7.1): 0 = no se ve
	public float PopSize;
}

// Estado de las dos toberas de una nave (spec docs/efectos-6-toberas.md, 3): potencia, boost de la llama, encendido,
// frenada, giro suavizado, anillos y tiempo propio (que la pausa para: sin delta no cambia nada). Puro.
public sealed class ThrusterState
{
	public const int Nozzles = 2;   // 0 = izquierda, 1 = derecha (vistas desde detrás)

	private readonly float[] _ringAge = new float[Nozzles];
	private bool _wasBoosting;
	private readonly Random _random;
	private readonly float[] _cut = new float[Nozzles];
	private readonly float[] _cutTimer = new float[Nozzles];
	private readonly float[] _pop = new float[Nozzles];
	private readonly float[] _popTimer = new float[Nozzles];

	public float Power { get; private set; }
	public float Boost { get; private set; }
	public float Kick { get; private set; }
	public float BrakeFlick { get; private set; }
	public float Steer { get; private set; }
	public float Throttle { get; private set; }   // th de este instante
	public float Slip { get; private set; }
	public bool Drifting { get; private set; }

	// Tiempo de la nave (s, con su desfase: dos naves no parpadean igual) y lo que ha corrido el ruido
	public float Time { get; private set; }
	public float Scroll { get; private set; }

	// Retroempuje (6) y lo que ha corrido su ruido
	public float Retro { get; private set; }
	public float RetroScroll { get; private set; }

	// seed: el azar de los cortes y chasquidos del daño 3 (misma semilla, mismos momentos)
	public ThrusterState(float phase = 0f, ThrusterFxSettings settings = null, int seed = 0)
	{
		Time = phase;
		_random = new Random(seed);
		for (int k = 0; k < Nozzles; k++)
			_cut[k] = 1f;
		Reset(settings ?? ThrusterFxSettings.Default);
	}

	public float Cut(int k) => _cut[k];
	public float Pop(int k) => _pop[k];

	// Recolocación (8): al ralentí y sin encendido ni frenada, al momento
	public void Reset(ThrusterFxSettings s)
	{
		Power = s.IdlePower;
		Kick = 0f;
		BrakeFlick = 0f;
		Steer = 0f;
		Retro = 0f;
		for (int k = 0; k < Nozzles; k++)
		{
			_ringAge[k] = float.NaN;
			_pop[k] = 0f;
			_cut[k] = 1f;
		}
	}

	public void Update(float dt, in ThrusterInput input, ThrusterFxSettings s)
	{
		if (dt <= 0f)
			return;

		Throttle = input.TopSpeed > 0f ? Math.Clamp(input.Speed / input.TopSpeed, 0f, 1f) : 0f;
		float target = input.Accelerate || input.Boosting ? s.ThrottleBase + s.ThrottleSpeed * Throttle
			: input.Brake ? s.BrakePower
			: s.CoastBase + s.CoastSpeed * Throttle;
		if (input.Speed < s.IdleSpeed)
			target = MathF.Max(target, s.IdlePower);
		float tau = target > Power ? s.PowerRise : input.Brake ? s.PowerBrakeFall : s.PowerFall;
		Power = Approach(Power, target, tau, dt);

		Boost = Approach(Boost, input.Boosting ? 1f : 0f, input.Boosting ? s.BoostRise : s.BoostFall, dt);
		Kick = Approach(Kick, 0f, s.KickTau, dt);
		for (int k = 0; k < Nozzles; k++)
		{
			if (float.IsNaN(_ringAge[k]))
				continue;
			_ringAge[k] += dt;
			if (_ringAge[k] > s.RingDuration)
				_ringAge[k] = float.NaN;
		}
		if (input.Boosting && !_wasBoosting)
		{
			Kick = 1f;
			for (int k = 0; k < Nozzles; k++)
				_ringAge[k] = -k * s.RingDelay;
		}
		_wasBoosting = input.Boosting;

		BrakeFlick = input.Brake ? 1f : Approach(BrakeFlick, 0f, s.BrakeFlickTau, dt);
		Steer = Approach(Steer, input.Steer, s.SteerSmoothing, dt);
		Slip = input.SlipAngle;
		Drifting = input.Drifting;

		bool retro = input.Brake && input.Speed > s.RetroMinSpeed;
		Retro = Approach(Retro, retro ? 1f : 0f, input.Brake ? s.RetroRise : s.RetroFall, dt);
		RetroScroll += s.RetroNoiseSpeed * dt;

		UpdateDamage(dt, input.DamageLevel >= 3, s);

		Time += dt;
		Scroll += (s.NoiseBase + s.NoisePower * Power + s.NoiseBoost * Boost) * dt;
	}

	// Daño 3 (7): cada tobera, por separado, con sus cortes y sus chasquidos; sin daño 3, ni cortes ni chasquidos nuevos
	private void UpdateDamage(float dt, bool damaged, ThrusterFxSettings s)
	{
		for (int k = 0; k < Nozzles; k++)
		{
			_pop[k] = Approach(_pop[k], 0f, s.PopTau, dt);
			if (!damaged)
			{
				_cut[k] = 1f;
				_cutTimer[k] = 0f;
				_popTimer[k] = 0f;
				continue;
			}
			_cutTimer[k] -= dt;
			if (_cutTimer[k] <= 0f)
			{
				_cut[k] = _random.NextDouble() < s.CutChance ? s.CutFactor : 1f;
				_cutTimer[k] = _cut[k] < 1f ? Between(s.CutMin, s.CutMax) : Between(s.WaitMin, s.WaitMax);
			}
			_popTimer[k] -= dt;
			if (_popTimer[k] <= 0f)
			{
				_pop[k] = 1f;
				_popTimer[k] = Between(s.PopMin, s.PopMax);
			}
		}
	}

	private float Between(float a, float b) => a + (b - a) * (float)_random.NextDouble();

	// Largo del chorro k del retroempuje (6), en m
	public float RetroLength(int k, ThrusterFxSettings s) =>
		(s.RetroLengthBase + s.RetroLengthForce * Retro) * (1f + s.RetroWobble * MathF.Sin(53f * Time + 2f * k));

	// Cómo se ve la tobera k con la forma y los colores de su nave
	// heat: false con PARTICLES en LOW (5.2), y el calor no se dibuja
	public ThrusterFlame Flame(int k, in ThrusterShape shape, ThrusterFxSettings s, bool heat = true)
	{
		float side = k == 0 ? -1f : 1f;
		float turn = TurnFactor(Steer, side, Throttle, s);
		float length = Length(Power, Boost, turn, Kick, s) * shape.LengthScale;
		float age = _ringAge[k];
		bool ring = !float.IsNaN(age) && age >= 0f;
		float ringT = ring ? age / s.RingDuration : 0f;
		float flicker = Flicker(k, s);
		return new ThrusterFlame
		{
			Length = length,
			Width = Width(Power, Boost, s),
			Bend = Bend(Slip, Steer, length, s),
			Brightness = Brightness(Power, Boost, turn, s),
			Flicker = flicker,
			Turn = turn,
			Core = Mix(shape.Colors.Core, s.BoostCore, Boost),
			Mid = Mix(shape.Colors.Mid, s.BoostMid, Boost),
			Out = Mix(shape.Colors.Out, s.BoostOut, Boost),
			Mouth = (s.MouthBase + s.MouthPower * Power + s.MouthBoost * Boost) * flicker / s.MouthNorm,
			RingSize = ring ? s.RingStart + (s.RingEnd - s.RingStart) * ringT : 0f,
			RingIntensity = ring ? s.RingIntensity * (1f - ringT) : 0f,
			Diamonds = Diamonds(Power, Boost, s),
			Heat = heat ? HeatStrength(Power, Boost, s) * _cut[k] : 0f,
			LightEnergy = LightEnergyOf(Power, Boost, s) * flicker,
			Cut = _cut[k],
			PopIntensity = s.PopIntensity * _pop[k],
			PopSize = s.PopSize + s.PopGrow * (1f - _pop[k])
		};
	}

	// Parpadeo q de la tobera k (3.4), con el corte del daño 3 (7.2)
	public float Flicker(int k, ThrusterFxSettings s)
	{
		float t = Time;
		float q = 1f + s.FlickerBase * MathF.Sin(37f * t + 2f * k) * MathF.Sin(23f * t + k);
		if (Drifting)
			q *= 1f + s.FlickerDrift * MathF.Sin(61f * t + 3f * k);
		if (BrakeFlick > 0.01f)
			q *= 1f - s.FlickerBrake * BrakeFlick * (0.5f + 0.5f * MathF.Sin(47f * t + k));
		q *= _cut[k];
		return q;
	}

	// --- Fórmulas de la spec, sueltas para poder probarlas ---

	// tm (3.3): la tobera de fuera empuja más; side −1 izquierda, +1 derecha; steer +1 izquierda
	public static float TurnFactor(float steer, float side, float throttle, ThrusterFxSettings s) =>
		1f + s.OuterPush * steer * side * MathF.Min(1f, s.OuterPushSpeed * throttle);

	public static float Length(float power, float boost, float turn, float kick, ThrusterFxSettings s) =>
		(s.LengthBase + s.LengthPower * power + s.LengthBoost * boost) * turn * (1f + s.KickGain * kick);

	public static float Width(float power, float boost, ThrusterFxSettings s) =>
		(s.WidthBase + s.WidthPower * power) * (1f + s.WidthBoost * boost);

	// Derrapando, la punta hacia dentro de la curva (deslizando a la derecha, que es girar a la izquierda: −x)
	public static float Bend(float slip, float steer, float length, ThrusterFxSettings s)
	{
		float max = s.MaxSlipDegrees * MathF.PI / 180f;
		float drift = max > 0f ? MathF.Min(1f, MathF.Abs(slip) / max) : 0f;
		return -MathF.Sign(slip) * drift * length * s.BendDrift - steer * s.BendSteer * length;
	}

	public static float Brightness(float power, float boost, float turn, ThrusterFxSettings s) =>
		(s.BrightBase + s.BrightPower * power + s.BrightBoost * boost) * (s.BrightTurnBase + s.BrightTurn * turn) * s.Brightness;

	public static float Diamonds(float power, float boost, ThrusterFxSettings s) =>
		MathF.Max(0f, (power - s.DiamondFrom) / s.DiamondRange) * s.DiamondGain + s.DiamondBoost * boost;

	public static float HeatStrength(float power, float boost, ThrusterFxSettings s) =>
		s.HeatBase + s.HeatPower * power + s.HeatBoost * boost;

	// Lo que queda del calor a esa distancia de la cámara (m): 1 cerca, referencia / distancia más lejos y 0 desde el final
	public static float HeatDistanceFactor(float distance, ThrusterFxSettings s)
	{
		float near = distance > s.HeatReferenceDistance && distance > 0f ? s.HeatReferenceDistance / distance : 1f;
		float span = s.HeatFadeEnd - s.HeatFadeStart;
		float t = span > 0f ? Math.Clamp((distance - s.HeatFadeStart) / span, 0f, 1f) : (distance >= s.HeatFadeEnd ? 1f : 0f);
		return near * (1f - t * t * (3f - 2f * t));
	}

	// Sin el parpadeo
	public static float LightEnergyOf(float power, float boost, ThrusterFxSettings s) =>
		s.LightEnergy * (s.LightBase + s.LightPower * power + s.LightBoost * boost);

	public static float Approach(float x, float target, float tau, float dt) =>
		tau <= 0f ? target : x + (target - x) * (1f - MathF.Exp(-dt / tau));

	private static Vector3 Mix(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
}
