using System;
using System.Numerics;

namespace Vectory.Effects;

// Estelas de las puntas de las alas (spec docs/efectos-7-estelas.md, secciones 3, 4, 7 y 8). Valores de la maqueta
// designs/efectos-estelas.html (A·CINTA); los tiempos en s, los anchos en m. Lógica pura: el nodo WingTrails solo dibuja.
public sealed class WingTrailSettings
{
	// Carga L (3.1): aceleración lateral completa (m/s²), parte del derrape y suavizado
	public float LoadFullAcceleration = 25f;
	public float LoadDrift = 0.85f;
	public float LoadTau = 0.15f;
	public float MaxSlipDegrees = 22f;           // βmáx (el de las toberas)
	public float MinTurnSpeed = 1f;              // m/s: más despacio no hay dirección de la marcha (ω = 0)

	// Muestras (3.2)
	public int MaxSamples = 400;
	public float History = 2.2f;
	public float MinStep = 0.001f;               // m: si la punta no se ha movido (frame sin paso de física), no hay muestra

	// Vida (3.3)
	public float LifeBase = 0.25f, LifeThrottle = 0.9f, LifeBoost = 0.5f, LifeLoad = 0.25f, LifeMax = 1.6f;
	public float LifeScale = 0.5f;               // la vida de la spec × esto (decisión del usuario: la mitad de larga)

	// Ancho (4.2)
	public float WidthBase = 0.10f, WidthThrottle = 0.14f, WidthLoad = 0.22f, WidthBoost = 0.18f, WidthTaper = 0.5f;
	public float WidthScale = 0.8f;              // el ancho de la spec × esto (decisión del usuario: más discreta)

	// Visibilidad e intensidad (4.2)
	public float VisibleFrom = 0.37f, VisibleFull = 0.82f, VisibleLoad = 0.8f;
	public float IntensityBase = 1.4f, IntensityBoost = 1.2f, IntensityLoad = 0.8f;
	public float BoostMix = 0.6f;

	// Shader (4.3)
	public float AlongPower = 1.5f, AlongFadeIn = 0.04f;
	public float HaloGain = 0.6f, HaloFalloff = 3.5f, CoreGain = 1.2f, CoreFalloff = 45f;
	public float Brightness = 0.7f;              // ajuste a ojo con el glow (decisión del usuario: más discreta)

	// Pulsos (5.2): B × máx(0; sen(0,9·D − 25·t))^16 × 2,5 × e^(−8·v²)
	public float PulseSpacing = 0.9f;            // rad/m
	public float PulseSpeed = 25f;               // rad/s
	public float PulseSharpness = 16f;
	public float PulseGain = 2.5f;
	public float PulseFalloff = 8f;

	public static WingTrailSettings Default => new();
}

// Pareja de colores de la estela de una nave (7): normal y con boost, tal cual los da la maqueta (el shader no los convierte)
public readonly record struct WingTrailColors(Vector3 Normal, Vector3 Boost)
{
	public static readonly WingTrailColors CyanOrange = new(new(0.21f, 0.90f, 1f), new(1f, 0.62f, 0.20f));
}

// Proveedor de los colores de la estela (7): (nave, pintura) → pareja. Hoy, cian y naranja para todas; un día saldrá de
// la pintura.
public static class WingTrailPalette
{
	public static WingTrailColors For(string shipId, int paint) => WingTrailColors.CyanOrange;
}

// Movimiento de la nave en un paso de física (3.1): de aquí sale la carga
public struct WingTrailMotion
{
	public Vector2 Velocity;    // m/s, en el plano (la dirección de la marcha)
	public bool Drifting;
	public float SlipAngle;     // β, radianes
}

// Lo que se guarda en cada frame (3.2): las puntas en el mundo, los ejes de la nave, th y bt
public struct WingTrailFrame
{
	public Vector3 TipLeft;
	public Vector3 TipRight;
	public Vector3 Right;
	public Vector3 Up;
	public float Throttle;      // th: velocidad / punta base, 0–1
	public float Boost;         // bt: el boost de la llama de las toberas
}

public struct WingTrailSample
{
	public Vector3 Position;
	public float Time;
	public float Throttle;
	public float Boost;
	public float Load;
	public Vector3 Right;
	public Vector3 Up;
}

// Recorrido de una punta: anillo de muestras, la 0 la más nueva
public sealed class WingTrailTip
{
	private readonly WingTrailSample[] _samples;
	private int _newest = -1;

	public WingTrailTip(int capacity) => _samples = new WingTrailSample[Math.Max(1, capacity)];

	public int Count { get; private set; }

	public WingTrailSample this[int i] => _samples[(_newest - i + _samples.Length) % _samples.Length];

	public void Add(in WingTrailSample sample)
	{
		_newest = (_newest + 1) % _samples.Length;
		_samples[_newest] = sample;
		if (Count < _samples.Length)
			Count++;
	}

	// Quita las más viejas de history
	public void Prune(float now, float history)
	{
		while (Count > 0 && now - this[Count - 1].Time > history)
			Count--;
	}

	public void Clear()
	{
		Count = 0;
		_newest = -1;
	}
}

// Estado de las estelas de una nave (3): carga y recorrido de las dos puntas
public sealed class WingTrailState
{
	public const int Tips = 2;   // 0 = izquierda, 1 = derecha

	private readonly WingTrailTip[] _tips = new WingTrailTip[Tips];
	private float _heading;
	private bool _hasHeading;
	private float _loadTarget;

	public WingTrailState(WingTrailSettings settings = null)
	{
		WingTrailSettings s = settings ?? WingTrailSettings.Default;
		for (int k = 0; k < Tips; k++)
			_tips[k] = new WingTrailTip(s.MaxSamples);
	}

	public float Load { get; private set; }
	public float Time { get; private set; }
	public float LoadTarget => _loadTarget;

	public WingTrailTip Tip(int k) => _tips[k];

	// Un paso de física: ω sale del cambio de la dirección de la marcha (envuelta a ±π)
	public void Move(float dt, in WingTrailMotion motion, WingTrailSettings s)
	{
		if (dt <= 0f)
			return;
		float speed = motion.Velocity.Length();
		float turnLoad = 0f;
		if (speed >= s.MinTurnSpeed)
		{
			float heading = MathF.Atan2(motion.Velocity.X, motion.Velocity.Y);
			if (_hasHeading)
			{
				float omega = Wrap(heading - _heading) / dt;
				turnLoad = TurnLoad(speed, omega, s);
			}
			_heading = heading;
			_hasHeading = true;
		}
		else
		{
			_hasHeading = false;
		}
		float target = turnLoad;
		if (motion.Drifting)
			target = MathF.Max(target, DriftLoad(motion.SlipAngle, s));
		_loadTarget = target;
	}

	// Un frame: suaviza la carga y guarda una muestra por punta (si se han movido). Con dt 0 (pausa), nada.
	public void Update(float dt, in WingTrailFrame frame, WingTrailSettings s)
	{
		if (dt <= 0f)
			return;
		Time += dt;
		Load += (_loadTarget - Load) * (1f - MathF.Exp(-dt / s.LoadTau));
		for (int k = 0; k < Tips; k++)
		{
			WingTrailTip tip = _tips[k];
			Vector3 p = k == 0 ? frame.TipLeft : frame.TipRight;
			tip.Prune(Time, s.History);
			if (tip.Count > 0 && Vector3.DistanceSquared(tip[0].Position, p) < s.MinStep * s.MinStep)
				continue;
			tip.Add(new WingTrailSample
			{
				Position = p,
				Time = Time,
				Throttle = frame.Throttle,
				Boost = frame.Boost,
				Load = Load,
				Right = frame.Right,
				Up = frame.Up
			});
		}
	}

	// Recolocación: sin muestras y sin carga al momento
	public void Reset()
	{
		for (int k = 0; k < Tips; k++)
			_tips[k].Clear();
		Load = 0f;
		_loadTarget = 0f;
		_hasHeading = false;
	}

	public static float TurnLoad(float speed, float omega, WingTrailSettings s) =>
		MathF.Min(1f, speed * MathF.Abs(omega) / s.LoadFullAcceleration);

	public static float DriftLoad(float slip, WingTrailSettings s)
	{
		float max = s.MaxSlipDegrees * MathF.PI / 180f;
		return max > 0f ? MathF.Min(1f, MathF.Abs(slip) / max) * s.LoadDrift : 0f;
	}

	// 3.3
	public static float Life(float throttle, float boost, float load, WingTrailSettings s) =>
		MathF.Min(s.LifeMax, s.LifeBase + s.LifeThrottle * throttle + s.LifeBoost * boost + s.LifeLoad * load) * s.LifeScale;

	// 4.2
	public static float Visibility(float throttle, float boost, float load, WingTrailSettings s)
	{
		float bySpeed = Math.Clamp((throttle - s.VisibleFrom) / (s.VisibleFull - s.VisibleFrom), 0f, 1f);
		return MathF.Max(bySpeed, MathF.Max(s.VisibleLoad * load, boost));
	}

	public static float Width(float throttle, float boost, float load, float u, WingTrailSettings s) =>
		(s.WidthBase + s.WidthThrottle * throttle + s.WidthLoad * load + s.WidthBoost * boost) * (1f - s.WidthTaper * u)
		* s.WidthScale;

	public static float Intensity(float throttle, float boost, float load, WingTrailSettings s) =>
		(s.IntensityBase + s.IntensityBoost * boost + s.IntensityLoad * load) * Visibility(throttle, boost, load, s);

	public static Vector3 Color(float boost, in WingTrailColors colors, WingTrailSettings s) =>
		Vector3.Lerp(colors.Normal, colors.Boost, s.BoostMix * boost);

	private static float Wrap(float a)
	{
		a %= 2f * MathF.PI;
		if (a > MathF.PI)
			a -= 2f * MathF.PI;
		else if (a < -MathF.PI)
			a += 2f * MathF.PI;
		return a;
	}
}

// Un vértice de la cinta (4.1)
public struct WingTrailVertex
{
	public Vector3 Position;
	public float U;             // 0 en la punta del ala, 1 al final
	public float V;             // −1 a un lado, +1 al otro
	public Vector3 Color;
	public float Intensity;
	public float Distance;      // D: m desde la punta del ala, a lo largo de los centros
	public float Boost;         // bt de la muestra (para los pulsos)
}

// Constructor de la cinta (4.1 y 4.2): de las muestras vivas de una punta, dos vértices por muestra, de cara a la cámara
public static class WingTrailRibbon
{
	// Cuántas muestras se dibujan: las de edad ≤ vida, con la vida de la más nueva
	public static int LiveCount(WingTrailTip tip, float now, WingTrailSettings s)
	{
		if (tip.Count == 0)
			return 0;
		WingTrailSample newest = tip[0];
		float life = WingTrailState.Life(newest.Throttle, newest.Boost, newest.Load, s);
		int n = 0;
		while (n < tip.Count && now - tip[n].Time <= life)
			n++;
		return n;
	}

	// Fase de los pulsos (5.2) en el tiempo t, envuelta a una vuelta (el seno no cambia y el shader no pierde precisión)
	public static float PulsePhase(float time, WingTrailSettings s) => (s.PulseSpeed * time) % (2f * MathF.PI);

	// Pulso en un punto de la cinta (5.2), lo mismo que hace el shader
	public static float Pulse(float boost, float distance, float phase, float v, WingTrailSettings s) =>
		boost * MathF.Pow(MathF.Max(0f, MathF.Sin(s.PulseSpacing * distance - phase)), s.PulseSharpness) * s.PulseGain
		* MathF.Exp(-s.PulseFalloff * v * v);

	// Escribe los vértices en output (2 por muestra viva) y devuelve cuántos. Con menos de dos muestras vivas, 0.
	public static int Build(WingTrailTip tip, float now, Vector3 camera, in WingTrailColors colors, WingTrailSettings s,
		WingTrailVertex[] output)
	{
		int n = Math.Min(LiveCount(tip, now, s), output.Length / 2);
		if (n < 2)
			return 0;
		WingTrailSample newest = tip[0];
		float life = WingTrailState.Life(newest.Throttle, newest.Boost, newest.Load, s);
		float distance = 0f;
		for (int i = 0; i < n; i++)
		{
			WingTrailSample smp = tip[i];
			Vector3 c = smp.Position;
			if (i > 0)
				distance += Vector3.Distance(c, tip[i - 1].Position);
			Vector3 dir = tip[Math.Max(0, i - 1)].Position - tip[Math.Min(n - 1, i + 1)].Position;
			if (dir.LengthSquared() < 1e-8f)
				dir = Vector3.Cross(smp.Up, smp.Right);   // la marcha: −Z de la nave
			Vector3 side = Vector3.Cross(dir, camera - c);
			side = side.LengthSquared() > 1e-10f ? Vector3.Normalize(side) : smp.Right;

			float u = life > 0f ? Math.Clamp((now - smp.Time) / life, 0f, 1f) : 1f;
			float half = 0.5f * WingTrailState.Width(smp.Throttle, smp.Boost, smp.Load, u, s);
			float intensity = WingTrailState.Intensity(smp.Throttle, smp.Boost, smp.Load, s);
			Vector3 color = WingTrailState.Color(smp.Boost, colors, s);
			for (int k = 0; k < 2; k++)
			{
				float v = k == 0 ? -1f : 1f;
				output[2 * i + k] = new WingTrailVertex
				{
					Position = c + side * (half * v),
					U = u,
					V = v,
					Color = color,
					Intensity = intensity,
					Distance = distance,
					Boost = smp.Boost
				};
			}
		}
		return 2 * n;
	}
}
