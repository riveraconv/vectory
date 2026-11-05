using System;

namespace Vectory.Ship;

// Cuánto empuja el motor con W según lo cerca que va de la punta (spec docs/naves-5-curva-aceleracion.md, 3.1):
// factor = max(empuje mínimo, 1 − (velocidad / punta)^exponente). La velocidad y la punta son las de ese momento, así
// que la curva se adapta sola al daño y a la pendiente. Lógica pura.
public static class AccelerationCurve
{
	// Exponente que significa "sin curva": la aceleración de antes de esta spec, factor siempre 1
	public const float Linear = 0f;

	public const float DefaultExponent = 2f;

	// El mínimo hace que la nave llegue a la punta de verdad en un tiempo razonable. Es común a todas las naves y es una
	// constante, no un valor del Inspector, porque el selector de naves también lo necesita y no tiene acceso al Player.
	public const float DefaultMinThrust = 0.10f;

	public static float Factor(float speed, float topSpeed, float exponent, float minThrust)
	{
		if (exponent <= Linear || topSpeed <= 0f)
			return 1f;

		float x = Math.Clamp(speed / topSpeed, 0f, 1f);
		return MathF.Max(minThrust, 1f - MathF.Pow(x, exponent));
	}
}
