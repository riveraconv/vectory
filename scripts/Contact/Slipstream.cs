using System;
using System.Collections.Generic;
using Vectory.Autopilot;

namespace Vectory.Contact;

// Parámetros del rebufo (spec docs/rivales-4-personalidades.md, 4.1), en m, m/s y s
public struct SlipstreamSettings
{
	public const float DefaultMaxDistance = 25f;
	public const float DefaultFullDistance = 5f;
	public const float DefaultMinSpeedKmh = 150f;
	public const float DefaultTopGain = 0.04f;
	public const float DefaultDragCut = 0.30f;
	public const float DefaultRiseTime = 0.5f;
	public const float DefaultFallTime = 0.3f;

	public float MaxDistance;    // de su morro a la cola de la de delante: más lejos, nada
	public float FullDistance;   // más cerca, rebufo entero
	public float MinSpeed;       // m/s, las dos naves
	public float TopGain;        // punta × (1 + esto × r)
	public float DragCut;        // Drag × (1 − esto × r)
	public float RiseTime;       // de 0 a 1
	public float FallTime;       // de 1 a 0

	public static SlipstreamSettings Default => new()
	{
		MaxDistance = DefaultMaxDistance,
		FullDistance = DefaultFullDistance,
		MinSpeed = DefaultMinSpeedKmh / 3.6f,
		TopGain = DefaultTopGain,
		DragCut = DefaultDragCut,
		RiseTime = DefaultRiseTime,
		FallTime = DefaultFallTime
	};
}

// Rebufo de una nave (spec 4.1), para todas, el jugador incluido: detrás de otra, a menos de MaxDistance de su morro a la
// cola de la otra, con su centro desviado del de la otra como mucho el semiancho de esta, las dos en el suelo y por encima
// de MinSpeed. Fuerza r: 1 a FullDistance o menos, 0 a MaxDistance, lineal entre medias; cuenta la más cercana. Sube a su
// valor en RiseTime y baja en FallTime; con el boost en marcha, nada. A la de delante no le hace nada. Las atravesables
// no dan rebufo. Lógica pura: no depende de Godot.
public sealed class Slipstream
{
	public SlipstreamSettings Settings { get; set; } = SlipstreamSettings.Default;

	// Fuerza de 0 a 1, ya suavizada
	public float Strength { get; private set; }

	// Id de la nave que lo da ahora; -1 si ninguna
	public int Source { get; private set; } = -1;

	public float TopFactor => 1f + Settings.TopGain * Strength;
	public float DragFactor => 1f - Settings.DragCut * Strength;

	// Fuerza que le corresponde ahora y de quién, sin suavizar. airborne: él está en el aire; las demás, en Airborne.
	public static float Target(in TrafficShip self, bool airborne, IReadOnlyList<TrafficShip> others, IReadOnlyList<bool> othersAirborne,
		in SlipstreamSettings s, float lapLength, out int source)
	{
		source = -1;
		if (airborne || self.Passable || self.Speed < s.MinSpeed)
			return 0f;

		float best = float.MaxValue;
		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Passable || o.Speed < s.MinSpeed || (othersAirborne != null && othersAirborne[i]))
				continue;
			float ds = Relative(o.Distance - self.Distance, lapLength);
			if (ds <= 0f)
				continue;
			float gap = ds - self.Front - o.Back;
			if (gap > s.MaxDistance || MathF.Abs(self.Lateral - o.Lateral) > o.HalfWidth)
				continue;
			if (gap < best)
			{
				best = gap;
				source = o.Id;
			}
		}
		return source < 0 ? 0f : Strength01(best, s);
	}

	// r para una distancia de su morro a la cola de la de delante
	public static float Strength01(float gap, in SlipstreamSettings s)
	{
		if (gap <= s.FullDistance)
			return 1f;
		if (gap >= s.MaxDistance)
			return 0f;
		return (s.MaxDistance - gap) / (s.MaxDistance - s.FullDistance);
	}

	// Un paso: sube o baja hacia target; con el boost en marcha, a 0 al momento
	public void Update(float target, int source, bool boostActive, float dt)
	{
		SlipstreamSettings s = Settings;
		if (boostActive)
		{
			Strength = 0f;
			Source = -1;
			return;
		}
		target = Math.Clamp(target, 0f, 1f);
		float rate = target > Strength ? 1f / MathF.Max(1e-3f, s.RiseTime) : 1f / MathF.Max(1e-3f, s.FallTime);
		float step = rate * dt;
		Strength = MathF.Abs(target - Strength) <= step ? target : Strength + MathF.Sign(target - Strength) * step;
		Source = target > 0f ? source : Strength > 0f ? Source : -1;
	}

	// A cero (recolocada, colocada en la salida)
	public void Reset()
	{
		Strength = 0f;
		Source = -1;
	}

	private static float Relative(float ds, float lapLength)
	{
		if (lapLength <= 0f)
			return ds;
		float half = lapLength * 0.5f;
		ds %= lapLength;
		if (ds > half)
			ds -= lapLength;
		else if (ds <= -half)
			ds += lapLength;
		return ds;
	}
}
