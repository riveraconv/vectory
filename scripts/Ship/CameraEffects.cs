using System;
using System.Numerics;

namespace Vectory.Ship;

// Cámara a la que se aplica un efecto (cada una tiene su amplitud y su empuje)
public enum CameraView
{
	Cockpit,
	Chase
}

// Ajustes de los efectos de cámara (spec docs/camara.md, 6 y 7). Clase de datos pura con los valores
// ajustados al probar por defecto: ahora la rellena el Inspector del Player; más adelante la leerá SETTINGS.
// Ángulos en radianes, FOV en grados, distancias en m y tiempos en s.
public sealed class CameraEffectSettings
{
	// Inclinación de la cabina
	public float CockpitBankFactor = 0.4f;                // respecto a la inclinación objetivo de la nave (0 = no se inclina)
	public float CockpitBankMax = 8f * MathF.PI / 180f;
	public float CockpitBankSmoothTime = 0.35f;

	// Vibración a alta velocidad
	public float VibrationThreshold = 0.85f;              // fracción de la punta desde la que empieza
	public float VibrationAmplitudeCockpit = 0.03f;
	public float VibrationAmplitudeChase = 0.018f;
	public float VibrationBoostMultiplier = 1.5f;
	public float VibrationFrequency = 8f;                 // Hz base
	public float VibrationSmoothTime = 0.2f;

	// Reacción al boost
	public float BoostFovExtra = 8f;                      // grados sobre el FOV base de cada cámara
	public float BoostPushCockpit = 0.05f;
	public float BoostPushChase = 0.6f;
	public float BoostEnterTime = 0.25f;
	public float BoostExitTime = 0.5f;

	// Peralte de la pista en la cabina (spec docs/pista-3d.md, 5.1): 1 = la vista se ladea el peralte completo
	public float CockpitTrackBankFactor = 1f;

	public float VibrationAmplitude(CameraView view) =>
		view == CameraView.Cockpit ? VibrationAmplitudeCockpit : VibrationAmplitudeChase;

	public float BoostPush(CameraView view) =>
		view == CameraView.Cockpit ? BoostPushCockpit : BoostPushChase;
}

// Inclinación de la cabina en curva (spec docs/camara.md, 2). Solo visual.
// Objetivo = inclinación objetivo de la nave × factor de cabina, con su propio tope, alcanzado con un
// suavizado exponencial más lento que el de la nave. Lógica pura.
public sealed class CockpitBank
{
	// Ángulo actual en radianes (mismo signo que la inclinación de la nave)
	public float Angle { get; private set; }

	public void Reset()
	{
		Angle = 0f;
	}

	public static float Target(float shipTarget, CameraEffectSettings s)
	{
		float max = MathF.Max(0f, s.CockpitBankMax);
		return Math.Clamp(shipTarget * s.CockpitBankFactor, -max, max);
	}

	public void Update(float shipTarget, CameraEffectSettings s, float dt)
	{
		float target = Target(shipTarget, s);
		if (s.CockpitBankSmoothTime <= 0f)
		{
			Angle = target;
			return;
		}
		Angle += (target - Angle) * (1f - MathF.Exp(-dt / s.CockpitBankSmoothTime));
	}
}

// Vibración de posición de la cámara a alta velocidad (spec docs/camara.md, 3). Solo desplaza, no rota.
// Intensidad objetivo: 0 hasta el umbral, sube lineal hasta 1 en la punta actual y se multiplica con boost;
// la intensidad real la sigue con un suavizado exponencial. El temblor es una suma de senos del tiempo
// (determinista). Lógica pura.
public sealed class CameraVibration
{
	// Intensidad suavizada (1 = amplitud máxima de la cámara; con boost puede pasar de 1)
	public float Intensity { get; private set; }

	// Tiempo del temblor (avanza con Update; la pausa lo congela)
	public float Time { get; private set; }

	public void Reset()
	{
		Intensity = 0f;
		Time = 0f;
	}

	public static float TargetIntensity(float speed, float topSpeed, bool boosting, CameraEffectSettings s)
	{
		if (topSpeed <= 0f)
			return 0f;

		float ratio = speed / topSpeed;
		float threshold = Math.Clamp(s.VibrationThreshold, 0f, 1f);
		float k = threshold >= 1f
			? (ratio >= 1f ? 1f : 0f)
			: Math.Clamp((ratio - threshold) / (1f - threshold), 0f, 1f);
		return boosting ? k * s.VibrationBoostMultiplier : k;
	}

	public void Update(float speed, float topSpeed, bool boosting, CameraEffectSettings s, float dt)
	{
		Time += dt;
		float target = TargetIntensity(speed, topSpeed, boosting, s);
		if (s.VibrationSmoothTime <= 0f)
		{
			Intensity = target;
			return;
		}
		Intensity += (target - Intensity) * (1f - MathF.Exp(-dt / s.VibrationSmoothTime));
	}

	// Desplazamiento máximo actual de una cámara, en m
	public float Amplitude(CameraView view, CameraEffectSettings s) => Intensity * s.VibrationAmplitude(view);

	// Desplazamiento actual de una cámara en su plano (X derecha, Y arriba), en m
	public Vector2 Offset(CameraView view, CameraEffectSettings s) =>
		Wave(Time, s.VibrationFrequency) * Amplitude(view, s);

	// Forma del temblor en un instante: cada eje con dos senos de frecuencias y fases distintas,
	// normalizada para que cada componente quede en -1..1
	public static Vector2 Wave(float time, float frequency)
	{
		float w = 2f * MathF.PI * frequency * time;
		float x = (MathF.Sin(w + 0.3f) + 0.5f * MathF.Sin(2.3f * w + 1.1f)) / 1.5f;
		float y = (MathF.Sin(1.7f * w + 0.6f) + 0.5f * MathF.Sin(3.1f * w + 2.0f)) / 1.5f;
		return new Vector2(x, y);
	}
}

// Reacción de la cámara al boost (spec docs/camara.md, 4): FOV extra y empuje hacia atrás.
// Una cantidad de 0 a 1 sube en el tiempo de entrada mientras el boost está activo y baja en el
// tiempo de salida cuando no; los efectos usan esa cantidad con una curva suave (smoothstep). Lógica pura.
public sealed class BoostCameraEffect
{
	// Progreso lineal de 0 (sin efecto) a 1 (efecto completo)
	public float Amount { get; private set; }

	// Progreso con la curva suave aplicada
	public float Eased => Amount * Amount * (3f - 2f * Amount);

	public void Reset()
	{
		Amount = 0f;
	}

	public void Update(bool boosting, CameraEffectSettings s, float dt)
	{
		if (boosting)
			Amount = s.BoostEnterTime > 0f ? MathF.Min(1f, Amount + dt / s.BoostEnterTime) : 1f;
		else
			Amount = s.BoostExitTime > 0f ? MathF.Max(0f, Amount - dt / s.BoostExitTime) : 0f;
	}

	// Grados que se suman al FOV base
	public float FovExtra(CameraEffectSettings s) => s.BoostFovExtra * Eased;

	// Metros que la cámara se echa hacia atrás
	public float Push(CameraView view, CameraEffectSettings s) => s.BoostPush(view) * Eased;
}
