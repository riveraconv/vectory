using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Autopilot;

// Ajustes del piloto (spec docs/naves-6-equilibrio.md, 7)
public struct AutopilotSettings
{
	public const float DefaultSpeedMargin = 0.97f;

	public float SpeedMargin;    // fracción de la velocidad de curva que usa
	public float BarrierMargin;  // m entre la trazada y la barrera
	public float StepTime;       // s: el paso de física
	public bool UseBoost;

	public static AutopilotSettings Default => new()
	{
		SpeedMargin = DefaultSpeedMargin,
		BarrierMargin = 1f,
		StepTime = 1f / 60f,
		UseBoost = false
	};
}

// Lo que el piloto mira de la nave en cada paso
public readonly record struct PilotState(Vector2 Position, Vector2 Velocity, float Yaw, float Steer, float MotorSpeed, float Distance, float Bank);

// Conducción del piloto (spec 4.4): sigue su trazada y su perfil de velocidad con la misma entrada que da el teclado,
// todo o nada, sin derrapar. Sirve para cualquier nave: todo lo propio de cada una está en la trazada y el perfil.
//  - Dirección: apunta a un punto de la trazada por delante (pure pursuit), a una distancia que crece con la velocidad,
//    y pulsa y suelta las teclas para que el giro suavizado de la dinámica siga al que hace falta.
//  - Velocidad: mira la del motor, porque la real la sigue con LateralGrip de retraso; por eso también mira el
//    objetivo un poco por delante. W si va por debajo, S si no llegaría frenando a un punto próximo, y si no, nada.
// Lógica pura: no depende de Godot.
public sealed class AutopilotDriver
{
	private const float LookaheadBase = 8f;      // m
	private const float LookaheadPerSpeed = 0.3f;   // m por m/s
	private const float BrakeTolerance = 1f;     // m/s por encima del objetivo antes de frenar

	private readonly RacingLine _line;
	private readonly SpeedProfile _profile;
	private readonly ShipTuning _tuning;
	private readonly BankGripSettings _bankGrip;
	private readonly float _lag;       // s que la velocidad real va por detrás de la del motor
	private readonly float _stepTime;
	private readonly float _boostAt;
	private float _lastDistance = -1f;
	private float _steerError;

	private static readonly int[] Keys = { 0, 1, -1 };

	public RacingLine Line => _line;
	public SpeedProfile Profile => _profile;

	// boostAt: m desde la meta en los que gasta una carga (negativo = nunca)
	public AutopilotDriver(RacingLine line, SpeedProfile profile, in ShipTuning tuning, in BankGripSettings bankGrip, float stepTime, float boostAt = -1f)
	{
		_line = line;
		_profile = profile;
		_tuning = tuning;
		_bankGrip = bankGrip;
		_lag = tuning.LateralGrip > 0f ? 1f / tuning.LateralGrip : 0f;
		_stepTime = stepTime;
		_boostAt = boostAt;
	}

	// El piloto de una nave en un circuito: trazada, calibración y perfil
	public static AutopilotDriver Create(TrackFrame frame, float trackWidth, float halfWidth, in ShipTuning tuning,
		in ShipPhysicsSettings physics, in AutopilotSettings settings, float boostAt = -1f)
	{
		RacingLine line = RacingLine.Build(frame, trackWidth, halfWidth, settings.BarrierMargin);
		CurveTable table = CurveCalibration.Measure(tuning, physics, settings.StepTime);
		SpeedProfile profile = SpeedProfile.Build(line, frame, tuning, table, physics, settings.SpeedMargin);
		return new AutopilotDriver(line, profile, tuning, physics.BankGrip, settings.StepTime, settings.UseBoost ? boostAt : -1f);
	}

	public ShipInput Drive(in PilotState state)
	{
		float steer = RequiredSteer(state);
		int steerInput = SteerInput(steer, state.Steer);
		bool brake = MustBrake(state);
		return new ShipInput
		{
			SteerLeft = steerInput > 0,
			SteerRight = steerInput < 0,
			Brake = brake,
			Accelerate = !brake && state.MotorSpeed < TargetAhead(state)
		};
	}

	// Carga de boost: al pasar por su punto, una vez por vuelta
	public bool WantsBoost(float distance)
	{
		if (_boostAt < 0f)
			return false;

		float last = _lastDistance;
		_lastDistance = distance;
		if (last < 0f)
			return false;
		return last <= distance ? last < _boostAt && _boostAt <= distance : _boostAt > last || _boostAt <= distance;
	}

	private float TargetAhead(in PilotState state) =>
		_profile.Target[_line.IndexAt(state.Distance + state.MotorSpeed * _lag)];

	// Giro de -1 a 1 (izquierda positivo) que hace falta para llegar al punto de delante
	private float RequiredSteer(in PilotState state)
	{
		float speed = state.Velocity.Length();
		float lookahead = LookaheadBase + LookaheadPerSpeed * speed;
		int index = _line.Wrap(_line.IndexAt(state.Distance) + (int)MathF.Ceiling(lookahead / _line.PointSpacing));
		Vector2 toTarget = _line.Points[index] - state.Position;
		float distance = toTarget.Length();
		if (distance < 0.01f)
			return 0f;

		// Se apunta con el morro, no con la velocidad: esta va retrasada y, mirándola a ella, la dirección oscila.
		// En una curva el morro va hacia dentro respecto a la velocidad: eso se suma para no quedarse corto.
		var heading = new Vector2(-MathF.Sin(state.Yaw), -MathF.Cos(state.Yaw));
		var right = new Vector2(-heading.Y, heading.X);
		float toLeft = -Vector2.Dot(toTarget, right);
		float lineCurvature = _line.Curvature[_line.Wrap(_line.IndexAt(state.Distance) + (int)(lookahead * 0.5f / _line.PointSpacing))];
		float grip = _tuning.LateralGrip * BankGrip.GripFactor(state.Bank, state.Steer, true, _bankGrip);
		float alpha = MathF.Atan2(toLeft, Vector2.Dot(toTarget, heading)) + lineCurvature * speed * SlipLag(grip);

		float curvature = 2f * MathF.Sin(alpha) / distance;
		float turn = _tuning.TurnRate * BankGrip.TurnFactor(state.Bank, state.Steer, true, _bankGrip) * Math.Clamp(state.MotorSpeed / 10f, 0.1f, 1f);
		return Math.Clamp(curvature * MathF.Max(speed, 1f) / turn, -1f, 1f);
	}

	// Lo que el morro se adelanta a la velocidad en una curva (s): el retraso de esta, por pasos de física
	private float SlipLag(float grip) => grip > 0f ? (1f / (1f - MathF.Exp(-grip * _stepTime)) - 1f) * _stepTime : 0f;

	// La tecla (+1 izquierda, -1 derecha, 0 ninguna) del paso: la que deja más cerca de cero el error de giro acumulado.
	// Solo con pulsar y soltar, el giro va a saltos; así, en media, da justo el que hace falta.
	private int SteerInput(float wanted, float steer)
	{
		float step = _tuning.SteerResponse * _stepTime;
		int best = 0;
		float bestError = float.MaxValue;
		foreach (int key in Keys)
		{
			float next = key > steer ? MathF.Min(steer + step, key) : MathF.Max(steer - step, key);
			float error = MathF.Abs(_steerError + next - wanted);
			if (error < bestError)
			{
				bestError = error;
				best = key;
			}
		}

		float result = best > steer ? MathF.Min(steer + step, best) : MathF.Max(steer - step, best);
		_steerError += result - wanted;
		return best;
	}

	// Si, frenando desde ahora, se pasaría de algún punto próximo
	private bool MustBrake(in PilotState state)
	{
		float speed = state.MotorSpeed;
		float brake = _profile.Braking;
		float lead = speed * _lag;
		float reach = lead + speed * speed / (2f * brake);
		int points = Math.Min(_line.Count - 1, (int)MathF.Ceiling(reach / _line.PointSpacing));
		int start = _line.IndexAt(state.Distance);

		for (int k = 0; k <= points; k++)
		{
			float target = _profile.Target[_line.Wrap(start + k)] + BrakeTolerance;
			float braking = MathF.Max(0f, k * _line.PointSpacing - lead);
			if (speed * speed - 2f * brake * braking > target * target)
				return true;
		}
		return false;
	}
}
