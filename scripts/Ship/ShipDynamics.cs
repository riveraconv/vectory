using System;
using System.Numerics;

namespace Vectory.Ship;

// Modelo de conducción arcade de la nave: velocidad del motor, giro suavizado e inercia lateral.
// Lógica pura: no depende de Godot, así que se puede probar sin ejecutar el juego.
// Trabaja en el plano horizontal: Vector2.X = eje X de Godot, Vector2.Y = eje Z de Godot.
// Convenciones: yaw positivo = girar a la izquierda; "adelante" para yaw = 0 es -Z.
public class ShipDynamics
{
	// Velocidad que "pide" el motor (m/s), siempre >= 0
	public float Speed { get; private set; }

	// Giro suavizado entre -1 y 1 (izquierda positivo)
	public float Steer { get; private set; }

	// Orientación en radianes
	public float Yaw { get; private set; }

	// Cuánto gira la nave según su velocidad (0 parada, 1 a partir de 10 m/s)
	public float TurnFactor { get; private set; }

	// Velocidad real en el plano horizontal (m/s); se retrasa respecto a Forward * Speed
	public Vector2 Velocity { get; private set; }

	public float RealSpeed => Velocity.Length();

	// Dirección hacia la que apunta la nave
	public Vector2 Forward => new Vector2(-MathF.Sin(Yaw), -MathF.Cos(Yaw));

	// Deja la nave parada y orientada según yaw
	public void Reset(float yaw)
	{
		Speed = 0f;
		Steer = 0f;
		Yaw = yaw;
		TurnFactor = 0f;
		Velocity = Vector2.Zero;
	}

	// Avanza el modelo un paso de dt segundos
	public void Step(in ShipInput input, in ShipTuning t, float dt)
	{
		// Velocidad del motor
		if (input.Boost)
			Speed = MoveToward(Speed, t.BoostMaxSpeed, t.BoostAcceleration * dt);
		else if (input.Accelerate)
			Speed = MoveToward(Speed, t.MaxSpeed, t.Acceleration * dt);
		else if (input.Brake)
			Speed = MoveToward(Speed, 0f, t.BrakeDeceleration * dt);
		else
			Speed = MoveToward(Speed, 0f, t.Drag * dt);

		// Giro: izquierda = positivo. Si está casi parada, apenas gira.
		float steerTarget = (input.SteerLeft ? 1f : 0f) - (input.SteerRight ? 1f : 0f);
		Steer = MoveToward(Steer, steerTarget, t.SteerResponse * dt);
		TurnFactor = Math.Clamp(Speed / 10f, 0f, 1f);
		Yaw += Steer * t.TurnRate * TurnFactor * dt;

		// Inercia lateral: la velocidad real se acerca poco a poco a "hacia donde apunta la nave"
		float k = 1f - MathF.Exp(-t.LateralGrip * dt);
		Velocity = Vector2.Lerp(Velocity, Forward * Speed, k);
	}

	// El motor mueve la nave y devuelve la velocidad real resultante (por ejemplo, tras deslizar contra algo)
	public void SetVelocity(Vector2 velocity)
	{
		Velocity = velocity;
	}

	// Tras un choque, el motor no puede pedir más velocidad de la que la nave lleva hacia delante
	public void AbsorbCollision()
	{
		float forwardSpeed = MathF.Max(0f, Vector2.Dot(Velocity, Forward));
		Speed = MathF.Min(Speed, forwardSpeed);
	}

	// Resta velocidad al motor (sin bajar de 0)
	public void ReduceSpeed(float amount)
	{
		Speed = MathF.Max(0f, Speed - amount);
	}

	private static float MoveToward(float from, float to, float delta)
	{
		if (MathF.Abs(to - from) <= delta)
			return to;
		return from + MathF.Sign(to - from) * delta;
	}
}
