using System;
using Vectory.Ship;

namespace Vectory.Effects;

// Ajustes del cono de vapor (spec docs/efectos-9-vision-tunel.md, 5 y 9). Fracciones de la punta base; tiempos en s.
public sealed class SonicConeSettings
{
	// Cuándo entra: desde VisibleFrom de la punta hasta VisibleFull, con arranque y llegada suaves
	public float VisibleFrom = 0.75f;
	public float VisibleFull = 1f;
	public float BoostWeight = 0.3f;     // lo que suma el boost, solo si ya se ve
	public float Max = 1.3f;
	public float SmoothTime = 0.18f;

	// Opacidad con cantidad 1
	public float CockpitOpacity = 0.03f;
	public float ChaseOpacity = 0.24f;

	// Velocidad del vapor: FlowBase + v (m/s) × FlowSpeed + FlowBoost × b
	public float FlowBase = 0.5f;
	public float FlowSpeed = 0.022f;
	public float FlowBoost = 1.2f;

	// Color: blanco-cian que pasa a ámbar claro con boost, hasta esta mezcla
	public float BoostMix = 0.6f;

	// Rivales (5.1): opacidad con cantidad 1, fundido cerca de la cámara (m: invisible / completo) y boost suavizado (s)
	public float RivalOpacity = 0.2f;
	public float RivalFadeFrom = 6f;
	public float RivalFadeFull = 18f;
	public float RivalBoostRise = 0.25f;
	public float RivalBoostFall = 0.5f;

	public static SonicConeSettings Default => new();
}

// Lo que recibe el cono en un frame. Solo una de las dos opacidades es distinta de 0: la de la cámara activa.
public readonly record struct SonicConeFrame(float CockpitOpacity, float ChaseOpacity, float BoostMix, float Flow);

// Curvas del cono de vapor (spec docs/efectos-9-vision-tunel.md, 5). Lógica pura.
public static class SonicConeCurves
{
	// Cantidad objetivo, sin suavizar: 0 hasta VisibleFrom, 1 desde VisibleFull, más el boost
	public static float Target(float speed, float baseTopSpeed, float boost, SonicConeSettings s)
	{
		if (baseTopSpeed <= 0f)
			return 0f;
		float ratio = speed / baseTopSpeed;
		float span = s.VisibleFull - s.VisibleFrom;
		float k = span <= 0f
			? (ratio >= s.VisibleFull ? 1f : 0f)
			: Math.Clamp((ratio - s.VisibleFrom) / span, 0f, 1f);
		if (k <= 0f)
			return 0f;
		k = k * k * (3f - 2f * k);
		return MathF.Min(s.Max, k + s.BoostWeight * Math.Clamp(boost, 0f, 1f));
	}

	// Opacidad del cono de un rival: la misma en las dos cámaras. SPEED LINES en OFF lo quita.
	public static float RivalOpacity(float amount, bool enabled, SonicConeSettings s) =>
		enabled ? MathF.Max(0f, amount) * s.RivalOpacity : 0f;

	// Boost de un rival (0–1), lineal, con los tiempos de la cámara del jugador
	public static float StepBoost(float boost, bool boosting, SonicConeSettings s, float dt)
	{
		float time = boosting ? s.RivalBoostRise : s.RivalBoostFall;
		if (time <= 0f)
			return boosting ? 1f : 0f;
		return Math.Clamp(boost + (boosting ? dt : -dt) / time, 0f, 1f);
	}

	// El ruido del shader se repite cada 256 celdas y usa el recorrido × 1 y × 1,7: aquí da la vuelta sin salto
	public const float FlowWrap = 2560f;

	// Lo de un frame. SPEED LINES en OFF lo quita; repeticiones y otras cámaras, la de persecución.
	public static SonicConeFrame Frame(float amount, float boost, float flow, CameraView view, bool enabled, SonicConeSettings s)
	{
		float a = enabled ? MathF.Max(0f, amount) : 0f;
		bool cockpit = view == CameraView.Cockpit;
		return new SonicConeFrame(cockpit ? a * s.CockpitOpacity : 0f, cockpit ? 0f : a * s.ChaseOpacity,
			s.BoostMix * Math.Clamp(boost, 0f, 1f), flow);
	}
}

// Cantidad suavizada y recorrido del vapor. Con dt 0 (pausa) no cambian.
public sealed class SonicConeState
{
	public float Amount { get; private set; }
	public float Flow { get; private set; }

	public void Reset()
	{
		Amount = 0f;
	}

	public void Update(float speed, float baseTopSpeed, float boost, SonicConeSettings s, float dt)
	{
		float target = SonicConeCurves.Target(speed, baseTopSpeed, boost, s);
		Amount = s.SmoothTime <= 0f ? target : Amount + (target - Amount) * (1f - MathF.Exp(-dt / s.SmoothTime));
		// Por debajo de esto no se ve: a 0 exacto, para que el cono deje de pintarse
		if (target <= 0f && Amount < 0.002f)
			Amount = 0f;
		float step = dt * (s.FlowBase + MathF.Max(0f, speed) * s.FlowSpeed + s.FlowBoost * Math.Clamp(boost, 0f, 1f));
		Flow = (Flow + step) % SonicConeCurves.FlowWrap;
	}
}
