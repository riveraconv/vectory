using System;
using System.Numerics;

namespace Vectory.Circuit;

// Resultado de aplicar el límite lateral de la pista
public struct LimitResult
{
	public bool Hit;           // true si la nave sobrepasaba el límite
	public Vector2 Position;   // posición ya corregida (igual a la original si no hubo contacto)
	public Vector2 Velocity;   // velocidad sin la parte que empujaba hacia fuera
	public float ImpactSpeed;  // velocidad hacia fuera que se ha anulado (m/s); 0 si no hubo golpe
}

// Límites laterales simples de la pista: no es una colisión física, solo coloca la nave
// de nuevo en el límite y anula la velocidad que la empujaba hacia fuera.
// Lógica pura: no depende de Godot. Trabaja en el plano horizontal
// (Vector2.X = eje X de Godot, Vector2.Y = eje Z de Godot).
public static class TrackLimits
{
	// position/velocity: estado de la nave. center/right: punto de la línea central más cercano
	// y su vector unitario hacia la derecha. halfWidth: mitad del ancho de la pista.
	// margin: distancia que se respeta hasta el borde.
	public static LimitResult Apply(
		Vector2 position, Vector2 velocity,
		Vector2 center, Vector2 right,
		float halfWidth, float margin)
	{
		var result = new LimitResult
		{
			Hit = false,
			Position = position,
			Velocity = velocity,
			ImpactSpeed = 0f
		};

		float limit = MathF.Max(0f, halfWidth - margin);
		float lateral = Vector2.Dot(position - center, right);
		if (MathF.Abs(lateral) <= limit)
			return result;

		Vector2 outward = right * MathF.Sign(lateral);

		result.Hit = true;
		result.Position = center + outward * limit;

		float outwardSpeed = Vector2.Dot(velocity, outward);
		if (outwardSpeed > 0f)
		{
			result.Velocity = velocity - outward * outwardSpeed;
			result.ImpactSpeed = outwardSpeed;
		}

		return result;
	}
}
