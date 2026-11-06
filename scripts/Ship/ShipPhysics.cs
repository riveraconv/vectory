using System;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Ship;

// Valores por defecto de la física común a todas las naves, en las unidades del Inspector del Player (km/h y grados).
// Son los únicos: de aquí salen los [Export] del Player y ShipPhysicsSettings.Default.
public static class ShipPhysicsDefaults
{
	// Boost (MaxCharges y SpeedGain están en BoostTuning)
	public const int BoostInitialCharges = 0;
	public const float BoostRechargeTime = 35f;
	public const float BoostRampTime = 0.5f;
	public const float BoostHoldTime = 2f;
	public const float BoostCooldown = 10f;

	// Derrape
	public const float DriftEnterSpeed = 120f;
	public const float DriftExitSpeed = 60f;
	public const float DriftGrip = 0f;
	public const float DriftCountersteerGrip = 0.5f;
	public const float DriftTurnMultiplier = 1.4f;
	public const float DriftFixedLoss = 5f;
	public const float DriftSlipLoss = 10f;
	public const float DriftAcceleration = 0.5f;
	public const float DriftMaxSlipAngle = 60f;
	public const float DriftGripRecoveryTime = 3f;

	// Vuelo
	public const float Gravity = 14f;
	public const float AirTurn = 0.3f;
	public const float LandingSpeedLoss = 0.1f;

	// Pendiente y peralte
	public const float SlopeReference = 10f;
	public const float SlopeEffectAmount = 0.3f;
	public const float BankGripBonus = 0.4f;
	public const float BankTurnBonus = 0.15f;
	public const float BankReference = 25f;
	public const float BankMax = 35f;

	// Colisiones
	public const float ImpactMediumSpeed = 30f;
	public const float ImpactStrongSpeed = 90f;
	public const float GrazeStraighten = 0.3f;
	public const float GrazeEntryLoss = 0.04f;
	public const float GrazeFriction = 20f;
	public const float MediumRestitution = 0.4f;
	public const float StrongRestitution = 0.5f;
	public const float MediumSpeedLoss = 0.2f;
	public const float StrongSpeedLoss = 0.45f;
	public const float RecoveryDuration = 1f;
	public const float RecoveryControl = 0.5f;
	public const float ImpactCooldown = 0.3f;
}

// Parámetros de un paso de ShipPhysics, con las velocidades en m/s y los ángulos en radianes.
// El Player los rellena cada paso desde el Inspector; sin escena se usa Default.
public struct ShipPhysicsSettings
{
	private const float KmhPerMs = 3.6f;
	private const float DegToRad = MathF.PI / 180f;

	public BoostTuning Boost;
	public DriftTuning Drift;
	public SlopeSettings Slope;
	public BankGripSettings BankGrip;
	public BarrierCollisionSettings Collision;
	public VerticalSettings Vertical;
	public float AirTurn;            // giro en el aire respecto al normal
	public float LandingSpeedLoss;   // fracción de velocidad que se pierde al aterrizar
	public float RecoveryDuration;   // segundos de control reducido tras un impacto fuerte
	public float RecoveryControl;    // giro y aceleración al empezar la corrección

	public static ShipPhysicsSettings Default => new()
	{
		Boost = new BoostTuning
		{
			MaxCharges = BoostTuning.DefaultMaxCharges,
			InitialCharges = ShipPhysicsDefaults.BoostInitialCharges,
			RechargeTime = ShipPhysicsDefaults.BoostRechargeTime,
			SpeedGain = BoostTuning.DefaultSpeedGainKmh / KmhPerMs,
			RampDuration = ShipPhysicsDefaults.BoostRampTime,
			HoldDuration = ShipPhysicsDefaults.BoostHoldTime,
			Cooldown = ShipPhysicsDefaults.BoostCooldown
		},
		Drift = new DriftTuning
		{
			Enabled = true,
			EnterSpeed = ShipPhysicsDefaults.DriftEnterSpeed / KmhPerMs,
			ExitSpeed = ShipPhysicsDefaults.DriftExitSpeed / KmhPerMs,
			Grip = ShipPhysicsDefaults.DriftGrip,
			CountersteerGrip = ShipPhysicsDefaults.DriftCountersteerGrip,
			TurnMultiplier = ShipPhysicsDefaults.DriftTurnMultiplier,
			FixedLoss = ShipPhysicsDefaults.DriftFixedLoss / KmhPerMs,
			SlipLoss = ShipPhysicsDefaults.DriftSlipLoss / KmhPerMs,
			AccelerationFactor = ShipPhysicsDefaults.DriftAcceleration,
			MaxSlipAngle = ShipPhysicsDefaults.DriftMaxSlipAngle * DegToRad,
			GripRecoveryTime = ShipPhysicsDefaults.DriftGripRecoveryTime
		},
		Slope = new SlopeSettings
		{
			ReferenceAngle = ShipPhysicsDefaults.SlopeReference * DegToRad,
			Effect = ShipPhysicsDefaults.SlopeEffectAmount
		},
		BankGrip = new BankGripSettings
		{
			GripBonus = ShipPhysicsDefaults.BankGripBonus,
			TurnBonus = ShipPhysicsDefaults.BankTurnBonus,
			ReferenceBank = ShipPhysicsDefaults.BankReference * DegToRad,
			MaxBank = ShipPhysicsDefaults.BankMax * DegToRad
		},
		Collision = BarrierCollisionSettings.Default,
		Vertical = new VerticalSettings
		{
			Gravity = ShipPhysicsDefaults.Gravity,
			Hover = Track.ShipHover
		},
		AirTurn = ShipPhysicsDefaults.AirTurn,
		LandingSpeedLoss = ShipPhysicsDefaults.LandingSpeedLoss,
		RecoveryDuration = ShipPhysicsDefaults.RecoveryDuration,
		RecoveryControl = ShipPhysicsDefaults.RecoveryControl
	};
}

// Un paso de física completo de una nave sobre la pista, sin nodos: boost, corrección tras un impacto, pendiente y
// peralte, dinámica, barreras y capa vertical, en ese orden. Lo usan el Player y lo que simule una nave sin escena.
//
// Un paso son dos llamadas, porque en medio alguien mueve la nave: BeginStep calcula la velocidad, quien la usa
// la aplica a la posición (el Player con MoveAndSlide) y AfterMove resuelve barreras y altura con la posición ya movida.
// Move hace las dos, con el movimiento en línea recta que da MoveAndSlide sin nada contra lo que chocar.
// Lógica pura: no depende de Godot.
public sealed class ShipPhysics
{
	public ShipDynamics Dynamics { get; } = new();
	public ShipBoost Boost { get; } = new();
	public ImpactRecovery Recovery { get; } = new();
	public ShipVertical Vertical { get; } = new();

	private readonly BarrierCollision _collision = new();
	private TrackFrame _frame;
	private float _barrierOffset;

	// Superficie de la pista bajo la nave en el último paso (sin circuito: ninguna, la nave no sube ni baja)
	public bool HasSurface { get; private set; }
	public SurfacePoint Surface { get; private set; }

	// Contacto con la barrera en el último paso
	public BarrierTouch LastBarrierTouch { get; private set; }

	// Circuito sobre el que conduce (null = ninguno: sin barreras ni superficie)
	public void SetTrack(TrackFrame frame, float barrierOffset)
	{
		_frame = frame;
		_barrierOffset = barrierOffset;
	}

	// Colocación: parada o avanzando a speed m/s, con el boost como al empezar, en el suelo
	public void Place(Vector3 position, float yaw, float speed, in BoostTuning boost)
	{
		Dynamics.Reset(yaw, speed);
		Boost.Reset(boost);
		_collision.Reset();
		Recovery.Cancel();
		Settle(position);
	}

	// Recolocación: parada, sin tocar el boost, en el suelo (si estaba en el aire, se cancela el vuelo)
	public void Respawn(Vector3 position, float yaw)
	{
		Dynamics.Reset(yaw);
		_collision.Reset();
		Recovery.Cancel();
		LastBarrierTouch = default;
		Settle(position);
	}

	private void Settle(Vector3 position)
	{
		Vertical.Reset(position.Y);
		UpdateSurface(position);
	}

	// Un paso entero, moviendo en línea recta
	public void Move(ref Vector3 position, in ShipInput input, bool boostPressed, bool canDrive, in ShipTuning baseTuning,
		float halfWidth, in ShipPhysicsSettings s, float dt)
	{
		BeginStep(input, boostPressed, canDrive, baseTuning, s, dt);
		Vector2 velocity = Dynamics.Velocity;
		position += new Vector3(velocity.X, 0f, velocity.Y) * dt;
		AfterMove(ref position, velocity, false, halfWidth, s, dt);
	}

	// Primera parte del paso: boost y dinámica. Deja en Dynamics.Velocity la velocidad con la que mover la nave.
	// Sin carrera en marcha (canDrive) no hay input ni boost (ni recarga).
	public void BeginStep(ShipInput input, bool boostPressed, bool canDrive, in ShipTuning baseTuning, in ShipPhysicsSettings s, float dt)
	{
		if (!canDrive)
			input = default;

		// En el aire, W y S no hacen nada (tampoco cortan el boost)
		bool airborne = Vertical.Airborne;
		if (airborne)
		{
			input.Accelerate = false;
			input.Brake = false;
		}

		float boostGain = 0f;
		if (canDrive)
		{
			if (boostPressed)
				Boost.TryActivate(s.Boost);
			if (input.Brake)
				Boost.CancelImpulse();
			boostGain = Boost.Update(s.Boost, dt);
		}
		else
		{
			Boost.CancelImpulse();
		}

		// Tras un impacto fuerte, el giro y la aceleración van reducidos un momento.
		// En el suelo, la pendiente cambia la punta y la aceleración, y el peralte el agarre y el giro
		// (en llano los factores son 1); en el aire, sin W/S ni agarre y con el giro reducido.
		Recovery.Update(dt);
		if (airborne)
			Dynamics.StepAirborne(input, baseTuning, dt, boostGain, s.AirTurn, Recovery.Factor);
		else
			Dynamics.Step(input, ApplyTrackSurface(baseTuning, s), s.Drift, dt, boostGain, Boost.IsImpulseActive, Recovery.Factor);
	}

	// Segunda parte, con la nave ya movida: velocity es la que resulta de moverla y collided, si chocó contra algo.
	// Barreras (también en el aire: la barrera es infinitamente alta) y capa vertical.
	public void AfterMove(ref Vector3 position, Vector2 velocity, bool collided, float halfWidth, in ShipPhysicsSettings s, float dt)
	{
		Dynamics.SetVelocity(velocity);

		UpdateSurface(position);
		ApplyBarriers(ref position, halfWidth, s, dt);

		// Si choca contra algo, el motor también pierde velocidad
		if (collided)
			Dynamics.AbsorbCollision();

		UpdateVertical(ref position, s, dt);
	}

	// Superficie bajo la nave en su posición actual (en planta)
	private void UpdateSurface(Vector3 position)
	{
		HasSurface = _frame != null;
		if (HasSurface)
			Surface = _frame.SurfaceUnder(position.X, position.Z, position.Y);
	}

	// Pendiente (punta y aceleración) y peralte (agarre y giro) de la superficie bajo la nave, sobre los
	// parámetros que ya llevan el daño. La pendiente es la del eje en el sentido de la marcha (según hacia
	// dónde apunta la nave respecto al eje); el peralte cuenta según el giro actual.
	private ShipTuning ApplyTrackSurface(ShipTuning t, in ShipPhysicsSettings s)
	{
		if (!HasSurface)
			return t;

		FramePoint f = Surface.Frame;
		var tangent = new Vector2(f.PlanTangent.X, f.PlanTangent.Z);
		float direction = Vector2.Dot(Dynamics.Forward, tangent) >= 0f ? 1f : -1f;
		float slope = direction * MathF.Atan(f.Slope);
		float slopeFactor = SlopeEffect.Factor(slope, s.Slope);
		t.MaxSpeed *= slopeFactor;
		t.Acceleration *= slopeFactor;

		t.LateralGrip *= BankGrip.GripFactor(f.Bank, Dynamics.Steer, true, s.BankGrip);
		t.TurnRate *= BankGrip.TurnFactor(f.Bank, Dynamics.Steer, true, s.BankGrip);
		return t;
	}

	// Altura de la nave: en el suelo, a la altura de vuelo sobre la superficie; en el aire, con la gravedad.
	// Al aterrizar: velocidad contra la superficie fuera y la pérdida, si la hay. Sin daño ni impacto contra la barrera.
	private void UpdateVertical(ref Vector3 position, in ShipPhysicsSettings s, float dt)
	{
		if (!HasSurface)
			return;

		FramePoint f = Surface.Frame;
		var sample = new SurfaceSample(Surface.SurfaceY, f.Normal, new Vector2(f.PlanTangent.X, f.PlanTangent.Z), f.Curvature);
		Vector2 velocity = Vertical.Update(dt, sample, Dynamics.Velocity, s.Vertical);

		if (Vertical.Landed)
		{
			float keep = 1f - Math.Clamp(s.LandingSpeedLoss, 0f, 1f);
			Dynamics.SetVelocity(velocity * keep);
			if (keep < 1f)
				Dynamics.ReduceSpeed(Dynamics.Speed * (1f - keep));
		}

		position.Y = Vertical.Height;
	}

	// Barreras (BarrierCollision): con la posición ya movida, se mira el desplazamiento lateral sobre la
	// superficie respecto al punto del eje bajo la nave. Si toca, la nave vuelve justo al límite (solo la
	// parte lateral de la posición, a lo largo del lado de la superficie) y se aplican el roce o el rebote
	// a la velocidad, la dirección y la velocidad del motor. Igual en el aire.
	// El boost no se toca: las pérdidas se aplican sobre la velocidad que lleve, también con impulso.
	private void ApplyBarriers(ref Vector3 position, float halfWidth, in ShipPhysicsSettings s, float dt)
	{
		if (!HasSurface)
		{
			LastBarrierTouch = default;
			return;
		}

		SurfacePoint surface = Surface;
		FramePoint f = surface.Frame;
		var tangent = new Vector2(f.PlanTangent.X, f.PlanTangent.Z);
		var right = new Vector2(f.PlanRight.X, f.PlanRight.Z);
		Vector2 velocity = Dynamics.Velocity;
		Vector2 forward = Dynamics.Forward;

		var input = new BarrierContactInput
		{
			Lateral = surface.Lateral,
			HalfWidth = halfWidth,
			BarrierOffset = _barrierOffset,
			VelocityAlong = Vector2.Dot(velocity, tangent),
			VelocityLateral = Vector2.Dot(velocity, right),
			HeadingAlong = Vector2.Dot(forward, tangent),
			HeadingLateral = Vector2.Dot(forward, right)
		};

		BarrierContactResult r = _collision.Resolve(input, s.Collision, dt);
		LastBarrierTouch = new BarrierTouch(r.Contact, r.Side, r.Level, r.IsNewImpact, f.Distance, surface.Lateral);

		if (!r.Contact)
			return;

		// En planta, el lateral sobre la superficie avanza a lo largo de la parte horizontal de su lado (en llano, Right)
		float correction = r.Lateral - surface.Lateral;
		position.X += f.Right.X * correction;
		position.Z += f.Right.Z * correction;
		UpdateSurface(position);

		Vector2 newVelocity = tangent * r.VelocityAlong + right * r.VelocityLateral;
		Vector2 newForward = tangent * r.HeadingAlong + right * r.HeadingLateral;
		float yaw = MathF.Atan2(-newForward.X, -newForward.Y);
		Dynamics.ApplyImpact(newVelocity, yaw, r.SpeedScale, r.SpeedLoss);

		// Un impacto medio o fuerte corta el derrape (el roce no)
		if (r.IsNewImpact && r.Level != ImpactLevel.Graze)
			Dynamics.EndDrift();

		if (r.IsNewImpact && r.Level == ImpactLevel.Strong)
		{
			Recovery.Duration = s.RecoveryDuration;
			Recovery.MinFactor = s.RecoveryControl;
			Recovery.Start();
		}
	}
}

// Contacto de la nave con la barrera en un paso de física: lado, nivel, si es un impacto medio o fuerte
// que cuenta, punto del eje (m desde la meta) y desplazamiento lateral antes de corregirlo
public readonly record struct BarrierTouch(
	bool Contact, BarrierSide Side, ImpactLevel Level, bool IsNewImpact, float AxisDistance, float Lateral);
