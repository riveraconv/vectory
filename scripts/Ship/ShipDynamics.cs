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

	// Deja la nave orientada según yaw, parada o (salida lanzada) avanzando a "speed" m/s hacia delante
	public void Reset(float yaw, float speed = 0f)
	{
		Speed = MathF.Max(0f, speed);
		Steer = 0f;
		Yaw = yaw;
		TurnFactor = Math.Clamp(Speed / 10f, 0f, 1f);
		Velocity = Forward * Speed;
	}

	// Avanza el modelo un paso de dt segundos.
	// boostGain: m/s que el boost suma en este paso (0 si no hay subida en curso).
	// boosting: hay un impulso en curso (subida o mantenimiento); la nave no pierde velocidad
	// ni por estar por encima de la punta ni por soltar el acelerador.
	public void Step(in ShipInput input, in ShipTuning t, float dt, float boostGain = 0f, bool boosting = false)
	{
		boosting |= boostGain > 0f;

		// Velocidad del motor
		if (Speed > t.MaxSpeed)
			StepAboveMaxSpeed(input, t, dt, boosting);
		else if (input.Accelerate)
			Speed = MoveToward(Speed, t.MaxSpeed, t.Acceleration * dt);
		else if (input.Brake)
			Speed = MoveToward(Speed, 0f, t.BrakeDeceleration * dt);
		else if (!boosting)
			Speed = MoveToward(Speed, 0f, t.Drag * dt);

		// El impulso del boost no lo recorta la velocidad punta
		Speed += MathF.Max(0f, boostGain);

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

	// Por encima de la punta (tras un boost): el motor no acelera más y el exceso se pierde
	// poco a poco. Frenar frena como siempre; soltar el acelerador aplica el Drag si es mayor.
	// Mientras el impulso sigue en curso (subida o mantenimiento), el exceso no se pierde.
	private void StepAboveMaxSpeed(in ShipInput input, in ShipTuning t, float dt, bool boosting)
	{
		if (input.Brake)
		{
			Speed = MoveToward(Speed, 0f, t.BrakeDeceleration * dt);
			return;
		}

		if (boosting)
			return;

		float loss = input.Accelerate ? t.OverspeedDecay : MathF.Max(t.OverspeedDecay, t.Drag);
		Speed = MoveToward(Speed, t.MaxSpeed, loss * dt);
	}

	private static float MoveToward(float from, float to, float delta)
	{
		if (MathF.Abs(to - from) <= delta)
			return to;
		return from + MathF.Sign(to - from) * delta;
	}
}
