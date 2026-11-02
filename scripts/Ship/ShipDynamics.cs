using System;
using System.Numerics;

namespace Vectory.Ship;

// Modelo de conducción arcade de la nave: velocidad del motor, giro suavizado, inercia lateral
// y derrape (spec docs/conduccion-avanzada.md). Sin derrape, la conducción es la de siempre.
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

	// Perpendicular a la derecha del morro
	public Vector2 Right => new Vector2(MathF.Cos(Yaw), -MathF.Sin(Yaw));

	// Por debajo de esta velocidad real (20 km/h) el ángulo de deslizamiento se considera 0
	public const float MinSlipSpeed = 20f / 3.6f;

	// Si la nave está derrapando
	public bool Drifting { get; private set; }

	// Fracción del agarre lateral normal que se aplica (1 sin derrape; baja al derrapar y se recupera al salir)
	public float GripFactor { get; private set; } = 1f;

	// Ángulo entre el morro y la dirección de movimiento (radianes; positivo = desliza hacia la derecha)
	public float SlipAngle
	{
		get
		{
			if (RealSpeed < MinSlipSpeed)
				return 0f;
			return MathF.Atan2(Vector2.Dot(Velocity, Right), Vector2.Dot(Velocity, Forward));
		}
	}

	// Tras un impacto que corta el derrape, no se vuelve a entrar hasta soltar la tecla de derrape o el giro
	private bool _driftNeedsRelease;

	// Deja la nave orientada según yaw, parada o (salida lanzada) avanzando a "speed" m/s hacia delante
	public void Reset(float yaw, float speed = 0f)
	{
		Speed = MathF.Max(0f, speed);
		Steer = 0f;
		Yaw = yaw;
		TurnFactor = Math.Clamp(Speed / 10f, 0f, 1f);
		Velocity = Forward * Speed;
		Drifting = false;
		GripFactor = 1f;
		_driftNeedsRelease = false;
	}

	// Termina el derrape al momento (impacto medio o fuerte): el agarre empieza a recuperarse
	// y no se vuelve a entrar hasta soltar la tecla de derrape o el giro
	public void EndDrift()
	{
		if (!Drifting)
			return;
		Drifting = false;
		_driftNeedsRelease = true;
	}

	// Avanza el modelo un paso de dt segundos.
	// boostGain: m/s que el boost suma en este paso (0 si no hay subida en curso).
	// boosting: hay un impulso en curso (subida o mantenimiento); la nave no pierde velocidad
	// ni por estar por encima de la punta ni por soltar el acelerador.
	// control: factor de 0 a 1 para la aceleración y el giro (corrección tras un impacto fuerte); el freno no cambia.
	public void Step(in ShipInput input, in ShipTuning t, float dt, float boostGain = 0f, bool boosting = false, float control = 1f)
	{
		Step(input, t, default, dt, boostGain, boosting, control);
	}

	// Igual, con derrape (drift.Enabled). En derrape cambian la aceleración, la frenada (modo A),
	// el giro del rumbo y el agarre lateral, y la nave pierde velocidad según el ángulo de deslizamiento.
	public void Step(in ShipInput input, in ShipTuning baseTuning, in DriftTuning drift, float dt, float boostGain = 0f, bool boosting = false, float control = 1f)
	{
		boosting |= boostGain > 0f;

		// Derrape: entrar, seguir o salir, y agarre de este paso
		float steerInput = (input.SteerLeft ? 1f : 0f) - (input.SteerRight ? 1f : 0f);
		UpdateDrift(input, drift, steerInput, dt);

		ShipTuning t = baseTuning;
		if (Drifting)
		{
			t.Acceleration *= drift.AccelerationFactor;
			t.TurnRate *= drift.TurnMultiplier;
			if (drift.Mode == DriftMode.BrakeTurn)
				t.BrakeDeceleration *= drift.BrakeFactor;
		}

		// Velocidad del motor
		if (Speed > t.MaxSpeed)
			StepAboveMaxSpeed(input, t, dt, boosting);
		else if (input.Accelerate)
			Speed = MoveToward(Speed, t.MaxSpeed, t.Acceleration * control * dt);
		else if (input.Brake)
			Speed = MoveToward(Speed, 0f, t.BrakeDeceleration * dt);
		else if (!boosting)
			Speed = MoveToward(Speed, 0f, t.Drag * dt);

		// El impulso del boost no lo recorta la velocidad punta
		Speed += MathF.Max(0f, boostGain);

		// Pérdida por derrapar: fija más una parte proporcional al ángulo de deslizamiento
		if (Drifting)
		{
			float slip = drift.MaxSlipAngle > 0f ? Math.Clamp(MathF.Abs(SlipAngle) / drift.MaxSlipAngle, 0f, 1f) : 0f;
			Speed = MathF.Max(0f, Speed - (drift.FixedLoss + drift.SlipLoss * slip) * dt);
		}

		// Giro: izquierda = positivo. Si está casi parada, apenas gira.
		Steer = MoveToward(Steer, steerInput, t.SteerResponse * dt);
		TurnFactor = Math.Clamp(Speed / 10f, 0f, 1f);
		Yaw += Steer * t.TurnRate * TurnFactor * control * dt;

		// Inercia lateral: la velocidad real se acerca poco a poco a "hacia donde apunta la nave".
		// En derrape (y mientras se recupera el agarre) lo hace más despacio y el ángulo tiene tope.
		float k = 1f - MathF.Exp(-t.LateralGrip * GripFactor * dt);
		Velocity = Vector2.Lerp(Velocity, Forward * Speed, k);
		if (Drifting || GripFactor < 1f)
			LimitSlip(drift.MaxSlipAngle);
	}

	// Decide si la nave derrapa en este paso y con qué agarre
	private void UpdateDrift(in ShipInput input, in DriftTuning drift, float steerInput, float dt)
	{
		bool held = DriftRule.TriggerHeld(input, drift.Mode) && steerInput != 0f;
		if (!held)
			_driftNeedsRelease = false;

		Drifting = DriftRule.ShouldDrift(Drifting, held && !_driftNeedsRelease, RealSpeed, drift);

		if (Drifting)
		{
			// Contravolante: girar hacia el lado al que desliza la nave
			bool countersteer = steerInput * Vector2.Dot(Velocity, Right) < 0f;
			GripFactor = countersteer ? drift.CountersteerGrip : drift.Grip;
		}
		else if (GripFactor < 1f)
		{
			// Al salir, el agarre vuelve al 100 % de forma lineal en GripRecoveryTime
			if (drift.GripRecoveryTime > 0f && drift.Grip < 1f)
				GripFactor = MoveToward(GripFactor, 1f, (1f - drift.Grip) / drift.GripRecoveryTime * dt);
			else
				GripFactor = 1f;
		}
	}

	// Recorta la velocidad lateral para que el ángulo de deslizamiento no pase de maxAngle
	private void LimitSlip(float maxAngle)
	{
		if (maxAngle <= 0f || RealSpeed < MinSlipSpeed)
			return;

		float along = Vector2.Dot(Velocity, Forward);
		if (along <= 0f)
			return;

		float lateral = Vector2.Dot(Velocity, Right);
		float maxLateral = along * MathF.Tan(maxAngle);
		if (MathF.Abs(lateral) > maxLateral)
			Velocity = Forward * along + Right * (MathF.Sign(lateral) * maxLateral);
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

	// Resultado de un contacto con la barrera: velocidad real y orientación nuevas, y la velocidad
	// del motor multiplicada por speedScale y reducida en speedLoss (sin bajar de 0).
	// El yaw se aplica como el giro más corto desde el actual, para que no salte de vuelta.
	public void ApplyImpact(Vector2 velocity, float yaw, float speedScale, float speedLoss)
	{
		Velocity = velocity;
		float turn = (yaw - Yaw) % (2f * MathF.PI);
		if (turn > MathF.PI)
			turn -= 2f * MathF.PI;
		else if (turn < -MathF.PI)
			turn += 2f * MathF.PI;
		Yaw += turn;
		Speed = MathF.Max(0f, Speed * speedScale - speedLoss);
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
