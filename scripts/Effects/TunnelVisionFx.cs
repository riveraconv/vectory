using System;
using Vectory.Ship;

namespace Vectory.Effects;

// Ajustes de la visión túnel (spec docs/efectos-9-vision-tunel.md, 9). Valores de la spec, que son los de
// docs/efectos-velocidad.md, 9, salvo el desdoble (3) y el factor de cámara (4). Fracciones de la distancia del centro
// a la esquina para la máscara y la viñeta; tiempos en s.
public sealed class TunnelVisionSettings
{
	// Intensidad (2): f empieza en esta fracción de la punta base
	public float SpeedStart = 0.25f;
	public float SpeedWeight = 0.85f;
	public float BoostWeight = 0.45f;
	public float Max = 1.2f;
	public float SmoothTime = 0.12f;

	// Post-proceso (2)
	public float Blur = 0.11f;
	public float Darken = 0.62f;
	public float Halo = 0.10f;
	public float HaloRamp = 0.45f;        // el halo llega entero con esta intensidad (maqueta: mín(1, I / 0,45))
	public float MaskStart = 0.22f;
	public float MaskFull = 0.95f;
	public float VignetteStart = 0.42f;
	public int Samples = 16;
	public int SamplesLow = 6;            // con PARTICLES en LOW

	// Desdoble (3)
	public float AberrationMax = 0.010f;
	public float AberrationBase = 0.4f;   // parte que hay sin boost

	// Factor de cámara (4)
	public float CockpitFactor = 1f;
	public float ChaseFactor = 0.5f;

	public static TunnelVisionSettings Default => new();
}

// Lo que recibe el post-proceso en un frame: todo ya multiplicado por la cámara y por TUNNEL VISION
public readonly record struct TunnelVisionFrame(float Intensity, float Aberration, float Halo, int Samples);

// Curvas de la visión túnel (spec docs/efectos-9-vision-tunel.md, 1–4). Lógica pura.
public static class TunnelVisionCurves
{
	// Factor de velocidad f: 0 por debajo de SpeedStart × la punta base, 1 a la punta
	public static float SpeedFactor(float speed, float baseTopSpeed, TunnelVisionSettings s)
	{
		if (baseTopSpeed <= 0f)
			return 0f;
		float start = s.SpeedStart * baseTopSpeed;
		float span = baseTopSpeed - start;
		if (span <= 0f)
			return speed >= baseTopSpeed ? 1f : 0f;
		return Math.Clamp((speed - start) / span, 0f, 1f);
	}

	// Intensidad objetivo, sin suavizar ni cámara: mín(Max; 0,85·f + 0,45·b)
	public static float Target(float speedFactor, float boost, TunnelVisionSettings s) =>
		MathF.Min(s.Max, s.SpeedWeight * speedFactor + s.BoostWeight * boost);

	// Cabina 1, persecución 0,5. Otras cámaras (repeticiones…), la de persecución.
	public static float CameraFactor(CameraView view, TunnelVisionSettings s) =>
		view == CameraView.Cockpit ? s.CockpitFactor : s.ChaseFactor;

	// Intensidad final: la suavizada × el factor de cámara × TUNNEL VISION (0–1)
	public static float Final(float intensity, CameraView view, float tunnelVision, TunnelVisionSettings s) =>
		MathF.Max(0f, intensity * CameraFactor(view, s) * Math.Clamp(tunnelVision, 0f, 1f));

	// Desdoble por unidad de máscara: máximo × (0,4 + 0,6·b). El shader lo multiplica por la máscara de bordes.
	public static float Aberration(float boost, TunnelVisionSettings s)
	{
		float b = Math.Clamp(boost, 0f, 1f);
		return s.AberrationMax * (s.AberrationBase + (1f - s.AberrationBase) * b);
	}

	// Halo ámbar de las esquinas: halo × b × mín(1, intensidad final / HaloRamp)
	public static float Halo(float finalIntensity, float boost, TunnelVisionSettings s)
	{
		float ramp = s.HaloRamp > 0f ? MathF.Min(1f, finalIntensity / s.HaloRamp) : 1f;
		return s.Halo * Math.Clamp(boost, 0f, 1f) * MathF.Max(0f, ramp);
	}

	// Muestras del desenfoque según PARTICLES (0 LOW, 1 MEDIUM, 2 HIGH)
	public static int Samples(int particlesLevel, TunnelVisionSettings s) =>
		particlesLevel <= 0 ? s.SamplesLow : s.Samples;

	// Todo lo de un frame. Sin intensidad, nada (tampoco desdoble ni halo).
	public static TunnelVisionFrame Frame(float intensity, float boost, CameraView view, float tunnelVision, int particlesLevel,
		TunnelVisionSettings s)
	{
		float final = Final(intensity, view, tunnelVision, s);
		int samples = Samples(particlesLevel, s);
		if (final <= 0f)
			return new TunnelVisionFrame(0f, 0f, 0f, samples);
		return new TunnelVisionFrame(final, Aberration(boost, s), Halo(final, boost, s), samples);
	}
}

// Intensidad suavizada de la visión túnel (2): sigue al objetivo con una constante de SmoothTime, para que no salte con
// un choque. Con dt 0 (pausa) no cambia. La cámara y TUNNEL VISION van después, sin suavizar (un corte de cámara).
public sealed class TunnelVisionState
{
	public float Intensity { get; private set; }

	public void Reset()
	{
		Intensity = 0f;
	}

	public void Update(float speed, float baseTopSpeed, float boost, TunnelVisionSettings s, float dt)
	{
		float target = TunnelVisionCurves.Target(TunnelVisionCurves.SpeedFactor(speed, baseTopSpeed, s), boost, s);
		if (s.SmoothTime <= 0f)
		{
			Intensity = target;
			return;
		}
		Intensity += (target - Intensity) * (1f - MathF.Exp(-dt / s.SmoothTime));
	}
}
