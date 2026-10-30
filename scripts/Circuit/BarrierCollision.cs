using System;

namespace Vectory.Circuit;

// Parámetros de la respuesta al impacto (velocidades en m/s; fracciones de 0 a 1)
public struct BarrierCollisionSettings
{
	public float MediumSpeed;        // velocidad perpendicular desde la que el impacto es medio
	public float StrongSpeed;        // velocidad perpendicular desde la que el impacto es fuerte
	public float GrazeStraighten;    // fracción del ángulo contra la barrera que se quita al empezar un roce
	public float GrazeEntryLoss;     // fracción de velocidad que se pierde al empezar un roce
	public float GrazeFriction;      // m/s que se pierden cada segundo mientras sigue el roce
	public float MediumRestitution;  // fracción de la velocidad perpendicular que vuelve hacia dentro (medio)
	public float StrongRestitution;  // ídem en el fuerte
	public float MediumSpeedLoss;    // fracción de velocidad que se pierde en un medio
	public float StrongSpeedLoss;    // ídem en un fuerte
	public float ImpactCooldown;     // segundos sin contar otro impacto medio o fuerte en el mismo lado

	// Valores de la spec, con las velocidades ya en m/s
	public static BarrierCollisionSettings Default => new()
	{
		MediumSpeed = 30f / 3.6f,
		StrongSpeed = 90f / 3.6f,
		GrazeStraighten = 0.3f,
		GrazeEntryLoss = 0.04f,
		GrazeFriction = 20f / 3.6f,
		MediumRestitution = 0.4f,
		StrongRestitution = 0.5f,
		MediumSpeedLoss = 0.2f,
		StrongSpeedLoss = 0.45f,
		ImpactCooldown = 0.3f
	};
}

// Estado de la nave respecto al eje del circuito en el punto más cercano.
// "Along" = en el sentido de la tangente del eje; "Lateral" = hacia la derecha (positivo) o la izquierda.
public struct BarrierContactInput
{
	public float Lateral;          // desplazamiento lateral del origen de la nave (m)
	public float HalfWidth;        // semiancho de colisión de la nave (m)
	public float BarrierOffset;    // desplazamiento lateral de la barrera (m)
	public float VelocityAlong;    // velocidad real (m/s)
	public float VelocityLateral;
	public float HeadingAlong;     // dirección hacia la que apunta la nave (vector unitario)
	public float HeadingLateral;
}

// Resultado de un paso de colisión. Sin contacto, todo vale lo mismo que la entrada.
public struct BarrierContactResult
{
	public bool Contact;
	public BarrierSide Side;
	public ImpactLevel Level;          // Graze también si un medio/fuerte cae dentro de la espera entre impactos
	public bool IsNewImpact;           // impacto medio o fuerte que cuenta (destello, sacudida, daños)
	public bool IsGrazeStart;          // primer paso de un roce (sin contacto en ese lado en el paso anterior)
	public float PerpendicularSpeed;   // velocidad contra la barrera al tocarla (m/s, >= 0)
	public float Lateral;              // desplazamiento lateral corregido (justo en el límite si hay contacto)
	public float VelocityAlong;
	public float VelocityLateral;
	public float HeadingAlong;
	public float HeadingLateral;
	public float SpeedScale;           // factor para la velocidad del motor (1 = sin cambios)
	public float SpeedLoss;            // m/s que se restan además a la velocidad del motor (fricción del roce)
}

// Colisión de la nave con las barreras (spec docs/barreras-y-colisiones.md, 3 y 4).
// No usa físicas: la barrera es un límite lateral respecto al eje. Hay contacto cuando
// |desplazamiento lateral| + semiancho >= desplazamiento de la barrera; entonces la nave se coloca
// justo en el límite (solo cambia la parte lateral de la posición) y se aplica el roce o el rebote.
// Como se mira la posición después de mover, una nave que en un paso pasaría de dentro a fuera
// también hace contacto: no se puede saltar la barrera.
//
// Guarda por lado la espera entre impactos y si había contacto en el paso anterior.
// Lógica pura: no depende de Godot.
public sealed class BarrierCollision
{
	private readonly float[] _cooldown = new float[2];
	private readonly bool[] _wasInContact = new bool[2];

	// Olvida contactos y esperas (al recolocar la nave)
	public void Reset()
	{
		Array.Clear(_cooldown);
		Array.Clear(_wasInContact);
	}

	public BarrierContactResult Resolve(in BarrierContactInput input, in BarrierCollisionSettings s, float dt)
	{
		for (int i = 0; i < 2; i++)
			_cooldown[i] = MathF.Max(0f, _cooldown[i] - dt);

		var result = new BarrierContactResult
		{
			Contact = false,
			Side = input.Lateral >= 0f ? BarrierSide.Right : BarrierSide.Left,
			Level = ImpactLevel.Graze,
			Lateral = input.Lateral,
			VelocityAlong = input.VelocityAlong,
			VelocityLateral = input.VelocityLateral,
			HeadingAlong = input.HeadingAlong,
			HeadingLateral = input.HeadingLateral,
			SpeedScale = 1f,
			SpeedLoss = 0f
		};

		float limit = MathF.Max(0f, input.BarrierOffset - input.HalfWidth);
		if (MathF.Abs(input.Lateral) < limit)
		{
			_wasInContact[0] = false;
			_wasInContact[1] = false;
			return result;
		}

		int side = (int)result.Side;
		float sign = result.Side == BarrierSide.Right ? 1f : -1f;
		bool continuing = _wasInContact[side];
		_wasInContact[side] = true;
		_wasInContact[1 - side] = false;

		result.Contact = true;
		result.Lateral = sign * limit;

		// Velocidad y dirección "hacia fuera" (positivas = contra la barrera)
		float outwardSpeed = input.VelocityLateral * sign;
		float perpendicular = MathF.Max(0f, outwardSpeed);
		result.PerpendicularSpeed = perpendicular;

		ImpactLevel level = ImpactClassifier.Classify(perpendicular, s.MediumSpeed, s.StrongSpeed);
		if (level != ImpactLevel.Graze && _cooldown[side] > 0f)
			level = ImpactLevel.Graze;
		result.Level = level;

		float headingOut = Math.Clamp(input.HeadingLateral * sign, -1f, 1f);
		float angle = MathF.Asin(headingOut);   // ángulo con la barrera; positivo = apunta contra ella

		if (level == ImpactLevel.Graze)
		{
			// Sin rebote: se quita la velocidad que va contra la barrera y la nave se desliza
			if (outwardSpeed > 0f)
				result.VelocityLateral = 0f;

			if (!continuing)
			{
				result.IsGrazeStart = true;
				result.SpeedScale = 1f - s.GrazeEntryLoss;
				if (angle > 0f)
					SetHeading(ref result, input.HeadingAlong, angle * (1f - s.GrazeStraighten), sign);
			}
			else
			{
				result.SpeedLoss = s.GrazeFriction * dt;
			}

			result.VelocityAlong *= result.SpeedScale;
			result.VelocityAlong = MoveTowardZero(result.VelocityAlong, result.SpeedLoss);
			return result;
		}

		// Medio o fuerte: rebote hacia dentro, dirección girada hacia fuera de la barrera y pérdida de velocidad
		bool strong = level == ImpactLevel.Strong;
		float restitution = strong ? s.StrongRestitution : s.MediumRestitution;
		float loss = strong ? s.StrongSpeedLoss : s.MediumSpeedLoss;

		result.IsNewImpact = true;
		_cooldown[side] = s.ImpactCooldown;

		result.VelocityLateral = -sign * perpendicular * restitution;
		result.SpeedScale = 1f - loss;
		result.VelocityAlong *= result.SpeedScale;

		if (angle > 0f)
			SetHeading(ref result, input.HeadingAlong, -angle * 0.5f, sign);

		return result;
	}

	// Nueva dirección con un ángulo dado respecto a la barrera (positivo = contra ella),
	// conservando el sentido de avance a lo largo del eje
	private static void SetHeading(ref BarrierContactResult result, float headingAlong, float angle, float sign)
	{
		float alongSign = headingAlong < 0f ? -1f : 1f;
		result.HeadingAlong = alongSign * MathF.Cos(angle);
		result.HeadingLateral = sign * MathF.Sin(angle);
	}

	private static float MoveTowardZero(float value, float amount)
	{
		if (MathF.Abs(value) <= amount)
			return 0f;
		return value - MathF.Sign(value) * amount;
	}
}
