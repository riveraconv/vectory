using System;

namespace Vectory.Ship;

// Parámetros de la inclinación visual (radianes y segundos)
public struct BankSettings
{
	public float MaxNormal;    // inclinación a giro máximo y velocidad punta, sin derrapar
	public float MaxDrift;     // lo mismo en derrape
	public float SmoothTime;   // constante de tiempo del suavizado
}

// Inclinación (alabeo) de la nave al girar (spec docs/conduccion-avanzada.md, 7). Solo visual.
// Objetivo = máximo × giro (-1..1, izquierda positivo) × (velocidad / punta, con tope en 1),
// alcanzado con un suavizado exponencial. Lógica pura.
public class ShipBank
{
	// Ángulo actual en radianes (positivo al girar a la izquierda)
	public float Angle { get; private set; }

	public void Reset()
	{
		Angle = 0f;
	}

	public static float Target(float steer, float speedRatio, bool drifting, in BankSettings s)
	{
		float max = drifting ? s.MaxDrift : s.MaxNormal;
		return max * Math.Clamp(steer, -1f, 1f) * Math.Clamp(speedRatio, 0f, 1f);
	}

	public void Update(float steer, float speedRatio, bool drifting, in BankSettings s, float dt)
	{
		float target = Target(steer, speedRatio, drifting, s);
		if (s.SmoothTime <= 0f)
		{
			Angle = target;
			return;
		}
		Angle += (target - Angle) * (1f - MathF.Exp(-dt / s.SmoothTime));
	}
}
