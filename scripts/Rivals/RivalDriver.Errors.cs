using System;
using System.Collections.Generic;
using Vectory.Autopilot;

namespace Vectory.Rivals;

// Los errores de un rival (spec docs/rivales-4-personalidades.md, 5): una tirada al acercarse a cada curva (a RollDistance
// de su frenada), con la presión y las naves de al lado de ese momento. Si sale:
//  - Frenada tardía: no frena por el perfil hasta LateMeters después de su punto de frenada y, desde ahí, se va
//    WideMeters por fuera de la trazada hasta salir de la curva. Si no le cabe, roza la barrera.
//  - Salida abierta: desde el vértice pisa W aunque vaya por encima del objetivo y se abre hacia la barrera hasta salir.
// Con la frenada al 90 % de la real y la trazada hacia fuera a la salida, eso apenas le costaba tiempo: cada error sortea
// además lo que pierde (0,3–0,8 s), que se aplica bajando su objetivo en el tramo del error (de la frenada, o del vértice,
// a la recta) en la proporción que da ese tiempo.
// Con una nave al lado, el error se corta (nunca saca a nadie de la pista). Luego vuelve a su trazada poco a poco. No
// derrapa nunca. La física es la de siempre: si toca la barrera, sus reglas y su daño.
public sealed partial class RivalDriver
{
	private const float RollDistance = 200f;       // m antes de la frenada a los que se tira el error de la curva
	private const float ErrorLateralSpeed = 3f;    // m/s a los que se abre o vuelve
	private const float BarrierReach = 0.3f;       // m más allá del semiancho junto a la barrera: así la toca

	private bool _startRolled;
	private int _rolledBrake = -1;
	private CurveError _error = CurveError.None;
	private float _errorBrakeAt;     // m sobre el eje del punto de frenada de la curva del error
	private float _errorApexAt;      // m sobre el eje de su vértice
	private float _errorExitAt;      // m sobre el eje de la recta de después
	private float _errorScale = 1f;  // fracción del objetivo en el tramo del error
	private int _errorOutward;       // hacia fuera de la curva: −1 izquierda, +1 derecha
	private bool _errorPastBrake;
	private float _errorOffset;      // lo que se aparta de la trazada por el error ahora (m)

	public PilotErrors Errors { get; }

	// Un error en marcha (frenada tardía o salida abierta), hasta salir de la curva
	public bool ErrorActive => _error.Kind != ErrorKind.None;
	public ErrorKind CurrentError => _error.Kind;

	// El desplazamiento por el error de este paso; tira el error de la curva que viene y aplica el que haya
	private float UpdateError(float distance, in TrackAhead ahead, in TrafficShip self, IReadOnlyList<TrafficShip> traffic)
	{
		bool alongside = Alongside(self, traffic);

		if (ahead.BrakingIndex != _rolledBrake && ahead.ToBraking <= RollDistance && ahead.InsideSide != 0)
		{
			_rolledBrake = ahead.BrakingIndex;
			if (!ErrorActive)
			{
				CurveError error = Errors.ForCurve(_curvesPerLap, UnderPressure(), alongside);
				if (error.Kind != ErrorKind.None)
				{
					_error = error;
					_errorBrakeAt = distance + ahead.ToBraking;
					_errorApexAt = _errorBrakeAt + _apexAfter[ahead.BrakingIndex] * _data.Line.PointSpacing;
					_errorExitAt = _errorBrakeAt + _exitAfter[ahead.BrakingIndex] * _data.Line.PointSpacing;
					float from = error.Kind == ErrorKind.WideExit ? _errorApexAt : _errorBrakeAt;
					float time = TimeBetween(from, _errorExitAt);
					_errorScale = time > 0f ? time / (time + error.Loss) : 1f;
					_errorOutward = -ahead.InsideSide;
					_errorPastBrake = false;
				}
			}
		}

		float target = 0f;
		if (ErrorActive)
		{
			float fromBrake = Relative(distance - _errorBrakeAt);
			float fromApex = Relative(distance - _errorApexAt);
			if (fromBrake >= 0f)
				_errorPastBrake = true;

			// Fuera de la curva: se acabó
			if (alongside || (_errorPastBrake && fromApex > 0f && ahead.OnStraight))
			{
				EndError();
			}
			else if (_error.Kind == ErrorKind.LateBraking)
			{
				_pilot.HoldBrake = fromBrake < _error.LateMeters;
				if (fromBrake >= _error.LateMeters)
				{
					target = _errorOutward * _error.WideMeters;
					_pilot.TargetScale = _errorScale;
				}
			}
			else if (fromApex >= 0f && _errorPastBrake)
			{
				_pilot.ForceThrottle = fromApex < _error.WideMeters * 10f;
				target = _errorOutward * 2f * _barrierOffset;
				_pilot.TargetScale = _errorScale;
			}
		}

		float step = ErrorLateralSpeed * _stepTime;
		_errorOffset = MathF.Abs(target - _errorOffset) <= step ? target : _errorOffset + MathF.Sign(target - _errorOffset) * step;
		return _errorOffset;
	}

	// Segundos del perfil entre dos puntos sobre el eje (from antes que to)
	private float TimeBetween(float from, float to)
	{
		RacingLine line = _data.Line;
		int start = line.IndexAt(from);
		int points = (int)MathF.Ceiling(MathF.Max(0f, Relative(to - from) < 0f ? Relative(to - from) + _frame.LapLength : Relative(to - from)) / line.PointSpacing);
		float time = 0f;
		for (int k = 0; k < points; k++)
			time += line.PointSpacing / MathF.Max(1f, _profile.Target[line.Wrap(start + k)]);
		return time;
	}

	// Con un error, la nave puede llegar a la barrera: el punto al que apunta, hasta tocarla
	private float ErrorLateralLimit(in TrafficShip self) => MathF.Max(0f, _barrierOffset - self.HalfWidth + BarrierReach);

	private void EndError()
	{
		_error = CurveError.None;
		_pilot.HoldBrake = false;
		_pilot.ForceThrottle = false;
	}

	// Bajo presión: atacando, defendiendo o con alguien a menos de PressureDistance
	private bool UnderPressure()
	{
		float distance = Errors.Settings.PressureDistance;
		LineState state = ClearLine.State;
		return ClearLine.IsAttacking || state is LineState.DefendLeft or LineState.DefendRight
			|| ClearLine.AheadGap < distance || ClearLine.BehindGap < distance;
	}

	// Una nave al lado: solapadas a lo largo y a menos de AlongsideGap entre cascos
	private bool Alongside(in TrafficShip self, IReadOnlyList<TrafficShip> traffic)
	{
		float gapLimit = Errors.Settings.AlongsideGap;
		for (int i = 0; i < traffic.Count; i++)
		{
			TrafficShip o = traffic[i];
			if (o.Id == self.Id || o.Passable)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			bool overlap = ds < self.Front + o.Back && -ds < self.Back + o.Front;
			float gap = MathF.Abs(o.Lateral - self.Lateral) - self.HalfWidth - o.HalfWidth;
			if (overlap && gap < gapLimit)
				return true;
		}
		return false;
	}

	private float Relative(float ds)
	{
		float lap = _frame.LapLength;
		float half = lap * 0.5f;
		ds %= lap;
		if (ds > half)
			ds -= lap;
		else if (ds <= -half)
			ds += lap;
		return ds;
	}
}
