using System;

namespace Vectory.Ship;

public struct SlopeSettings
{
	public float ReferenceAngle;   // radianes: a esta pendiente el efecto es completo (con tope a partir de ahí)
	public float Effect;           // fracción que se quita subiendo y se suma bajando (0,3 = ×0,7 / ×1,3)
}

// Pendientes (spec docs/pista-3d.md, 4): subiendo, la velocidad punta y la aceleración bajan; bajando, suben.
// Proporcional a la pendiente hasta la de referencia. Lógica pura.
public static class SlopeEffect
{
	// slope: ángulo en radianes en el sentido de la marcha (positivo subiendo)
	public static float Factor(float slope, in SlopeSettings s)
	{
		if (s.ReferenceAngle <= 0f)
			return 1f;
		return 1f - s.Effect * Math.Clamp(slope / s.ReferenceAngle, -1f, 1f);
	}
}

public struct BankGripSettings
{
	public float GripBonus;        // agarre extra al peralte de referencia (0,4 = +40 %)
	public float TurnBonus;        // giro extra al peralte de referencia
	public float ReferenceBank;    // radianes (25°)
	public float MaxBank;          // radianes: los multiplicadores no pasan de lo que sale aquí (35°)
}

// Peralte (spec docs/pista-3d.md, 5.2): en curva, a favor del giro (la pista sube hacia el exterior)
// da agarre lateral y algo más de giro; en contra los quita. Sin girar o en el aire no cambia nada.
// Lógica pura.
public static class BankGrip
{
	// bank: radianes, positivo = el borde derecho más alto. steer: giro (izquierda positivo).
	// Girando a la izquierda el exterior es la derecha: a favor si el borde derecho está más alto.
	public static float EffectiveBank(float bank, float steer)
	{
		if (steer > 0f)
			return bank;
		if (steer < 0f)
			return -bank;
		return 0f;
	}

	public static float GripFactor(float bank, float steer, bool grounded, in BankGripSettings s) =>
		Factor(bank, steer, grounded, s.GripBonus, s);

	public static float TurnFactor(float bank, float steer, bool grounded, in BankGripSettings s) =>
		Factor(bank, steer, grounded, s.TurnBonus, s);

	private static float Factor(float bank, float steer, bool grounded, float bonus, in BankGripSettings s)
	{
		if (!grounded || s.ReferenceBank <= 0f)
			return 1f;
		float max = MathF.Max(0f, s.MaxBank);
		float effective = Math.Clamp(EffectiveBank(bank, steer), -max, max);
		return 1f + bonus * effective / s.ReferenceBank;
	}
}
