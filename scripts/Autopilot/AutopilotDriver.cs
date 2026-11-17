using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Autopilot;

// Ajustes del piloto (spec docs/naves-6-equilibrio.md, 7)
public struct AutopilotSettings
{
	public const float DefaultSpeedMargin = 0.97f;
	// Lo más que puede usar de la velocidad de curva: la calibración es prudente y al 106 % aún no roza
	// (spec docs/rivales-4-personalidades.md, 3.1)
	public const float MaxCurveMargin = 1.05f;

	public float SpeedMargin;    // fracción de la velocidad de curva que usa
	public float StraightMargin; // fracción de la punta que usa (spec docs/rivales-4-personalidades.md, 3.1)
	public float BrakingShare;   // fracción de la frenada con la que calcula dónde frenar (1 = la de la spec 6)
	public float BarrierMargin;  // m entre la trazada y la barrera
	public float StepTime;       // s: el paso de física
	public bool UseBoost;

	public static AutopilotSettings Default => new()
	{
		SpeedMargin = DefaultSpeedMargin,
		StraightMargin = 1f,
		BrakingShare = 1f,
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

	// Fracción del perfil de velocidad que persigue (1 = el perfil entero). Los rivales la bajan en la vuelta de
	// enfriamiento (spec docs/rivales-1-parrilla.md, 4.4).
	public float TargetScale { get; set; } = 1f;

	// Perfiles alternativos de los rivales (spec docs/rivales-4-personalidades.md, 4.3 y 4.6): con LateBraking, el de
	// frenar más tarde (atacando por dentro); con Boosting, el de la punta más el impulso, para seguir pisando W con el
	// boost en vez de soltarlo al pasar de la punta. Sin ellos (el piloto de F7), el perfil de siempre.
	public SpeedProfile LateProfile { get; set; }
	public SpeedProfile BoostProfile { get; set; }
	public bool LateBraking { get; set; }
	public bool Boosting { get; set; }

	private SpeedProfile Active => Boosting && BoostProfile != null ? BoostProfile
		: LateBraking && LateProfile != null ? LateProfile : _profile;

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
		SpeedProfile profile = SpeedProfile.Build(line, frame, tuning, table, physics, settings.SpeedMargin, settings.StraightMargin,
			settings.BrakingShare);
		return new AutopilotDriver(line, profile, tuning, physics.BankGrip, settings.StepTime, settings.UseBoost ? boostAt : -1f);
	}

	// Lo que mira por delante, sobre la trazada, para apuntar (m)
	public static float Lookahead(float speed) => LookaheadBase + LookaheadPerSpeed * speed;

	public ShipInput Drive(in PilotState state) => Drive(state, 0f, float.PositiveInfinity);

	// Con la trazada desplazada lateralOffset m (positivo = derecha), sin que el punto al que apunta pase de lateralLimit
	// a ningún lado (la barrera), y sin pasar de speedCap m/s (spec docs/rivales-3-contacto.md, 8): lo usa el conductor
	// de los rivales; sin desplazamiento ni límite, es lo de siempre
	public ShipInput Drive(in PilotState state, float lateralOffset, float speedCap, float lateralLimit = float.PositiveInfinity)
	{
		float steer = RequiredSteer(state, lateralOffset, lateralLimit);
		int steerInput = SteerInput(steer, state.Steer);
		bool brake = MustBrake(state) || state.MotorSpeed > speedCap + BrakeTolerance;
		return new ShipInput
		{
			SteerLeft = steerInput > 0,
			SteerRight = steerInput < 0,
			Brake = brake,
			// Con el impulso del boost (y su perfil) sigue pisando: la pasada hacia delante del perfil no sabe del boost
			Accelerate = !brake && state.MotorSpeed < (Boosting && BoostProfile != null ? speedCap : MathF.Min(TargetAhead(state), speedCap))
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
		Active.Target[_line.IndexAt(state.Distance + state.MotorSpeed * _lag)] * TargetScale;

	// Giro de -1 a 1 (izquierda positivo) que hace falta para llegar al punto de delante
	private float RequiredSteer(in PilotState state, float lateralOffset, float lateralLimit)
	{
		float speed = state.Velocity.Length();
		float lookahead = LookaheadBase + LookaheadPerSpeed * speed;
		int index = _line.Wrap(_line.IndexAt(state.Distance) + (int)MathF.Ceiling(lookahead / _line.PointSpacing));
		Vector2 target = _line.Points[index];
		if (lateralOffset != 0f)
		{
			float lateral = _line.Lateral[index];
			float displaced = Math.Clamp(lateral + lateralOffset, -lateralLimit, lateralLimit);
			target += _line.Rights[index] * (displaced - lateral);
		}
		Vector2 toTarget = target - state.Position;
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
		SpeedProfile profile = Active;
		float speed = state.MotorSpeed;
		float brake = profile.Braking;
		float lead = speed * _lag;
		float reach = lead + speed * speed / (2f * brake);
		int points = Math.Min(_line.Count - 1, (int)MathF.Ceiling(reach / _line.PointSpacing));
		int start = _line.IndexAt(state.Distance);

		for (int k = 0; k <= points; k++)
		{
			float target = profile.Target[_line.Wrap(start + k)] * TargetScale + BrakeTolerance;
			float braking = MathF.Max(0f, k * _line.PointSpacing - lead);
			if (speed * speed - 2f * brake * braking > target * target)
				return true;
		}
		return false;
	}
}
