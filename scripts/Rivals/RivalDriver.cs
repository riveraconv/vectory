using System;
using System.Collections.Generic;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Rivals;

// En qué punto de su carrera está un rival
public enum RivalPhase
{
	PreStart,   // en la parrilla, antes del GO: sin entrada
	Running,    // en carrera
	Finished    // ha completado la última vuelta: vuelta de enfriamiento
}

// Lo que hace un rival en un paso: las teclas y si gasta una carga de boost
public readonly record struct RivalCommand(ShipInput Input, bool Boost);

// Conductor de un rival (spec docs/rivales-1-parrilla.md, 4.1, 4.2 y 4.4): envuelve el piloto automático de su nave y
// decide la entrada según el momento de la carrera, el boost y el ritmo. Sin reglas de física propias.
//  - Previa: nada. En carrera: el piloto, con la misma entrada que da el teclado, sin derrapar.
//  - Boost: una carga al entrar en la recta más larga, si se puede activar (carga, sin cooldown y sin daño de nivel 3).
//  - Terminado: el piloto al CooldownPace de su objetivo y sin boost.
//  - Ritmo: el margen de velocidad del piloto en las curvas (el 97 % de la spec 6), común, más lo que se aparta el de
//    cada rival (PaceFor); cambiarlo rehace el perfil, que es barato (trazada y calibración vienen de la caché).
//  - Trazada clara (spec docs/rivales-3-contacto.md, 8): con las demás naves, se aparta o se queda detrás (ClearLine), y
//    el boost de la recta solo se gasta con la trazada clara; si al entrar alguien la tapa, espera mientras siga en ella.
// Lógica pura.
public sealed class RivalDriver
{
	public const float DefaultPace = AutopilotSettings.DefaultSpeedMargin;
	public const float DefaultCooldownPace = 0.6f;

	private readonly PilotData _data;
	private readonly TrackFrame _frame;
	private readonly ShipTuning _tuning;
	private readonly ShipPhysicsSettings _physics;
	private readonly float _stepTime;
	private readonly float _boostAt;
	private readonly float _boostLength;
	private readonly float _barrierOffset;
	private AutopilotDriver _pilot;
	private bool _boostPending;
	private static readonly TrafficShip[] NoTraffic = Array.Empty<TrafficShip>();

	public float Pace { get; private set; }
	public float CooldownPace { get; set; } = DefaultCooldownPace;
	public AutopilotDriver Pilot => _pilot;
	public ClearLine ClearLine { get; private set; }

	// boostAt: m desde la meta donde empieza la recta más larga (negativo = nunca usa boost) y boostLength, lo que mide.
	// barrierOffset: desplazamiento de la barrera, para el hueco al apartarse.
	public RivalDriver(PilotData data, TrackFrame frame, in ShipTuning tuning, in ShipPhysicsSettings physics, float stepTime,
		float boostAt, float pace = DefaultPace, float boostLength = 0f, float barrierOffset = float.PositiveInfinity)
	{
		_data = data;
		_frame = frame;
		_tuning = tuning;
		_physics = physics;
		_stepTime = stepTime;
		_boostAt = boostAt;
		_boostLength = boostLength;
		_barrierOffset = barrierOffset;
		SetPace(pace);
	}

	// El ritmo de un rival: el común más lo suyo, sin pasar del 100 % de la velocidad de curva
	public static float PaceFor(float pace, float offset) => MathF.Min(1f, pace + offset);

	public void SetPace(float pace)
	{
		Pace = pace;
		SpeedProfile profile = SpeedProfile.Build(_data.Line, _frame, _tuning, _data.Table, _physics, pace);
		_pilot = new AutopilotDriver(_data.Line, profile, _tuning, _physics.BankGrip, _stepTime, _boostAt);
		var clearLine = new ClearLine(_data.Line, _data.Table, _frame.LapLength, _barrierOffset, profile.Braking) { Pace = pace };
		if (ClearLine != null)
			clearLine.Settings = ClearLine.Settings;
		ClearLine = clearLine;
	}

	// boostAvailable: si la nave puede gastar una carga ahora (ShipBoost.CanActivate: carga, sin cooldown, sin bloqueo)
	public RivalCommand Decide(RivalPhase phase, in PilotState state, bool boostAvailable) =>
		Decide(phase, state, boostAvailable, default, NoTraffic);

	// Con las demás naves de la carrera (self: él mismo visto desde la pista; traffic puede incluirle)
	public RivalCommand Decide(RivalPhase phase, in PilotState state, bool boostAvailable, in TrafficShip self,
		IReadOnlyList<TrafficShip> traffic)
	{
		if (phase == RivalPhase.PreStart)
			return default;

		// El paso por el inicio de la recta se sigue siempre, así no se dispara tarde al volver a poder
		bool atStraight = _pilot.WantsBoost(state.Distance);
		bool finished = phase == RivalPhase.Finished;
		_pilot.TargetScale = finished ? CooldownPace : 1f;

		if (finished)
		{
			_boostPending = false;
			ClearLine.Reset();
			return new RivalCommand(_pilot.Drive(state), false);
		}

		ClearLine.Update(self, traffic, _stepTime);
		bool clear = ClearLine.State == LineState.Clear && ClearLine.Blocker < 0;

		// Una carga al entrar en la recta más larga (sin carga entonces, esa vuelta no), solo con la trazada clara:
		// si alguien la tapa, espera mientras siga en la recta
		if (atStraight)
			_boostPending = boostAvailable;
		else if (_boostPending && !InStraight(state.Distance))
			_boostPending = false;
		bool boost = false;
		if (_boostPending && clear && boostAvailable)
		{
			boost = true;
			_boostPending = false;
		}

		return new RivalCommand(_pilot.Drive(state, ClearLine.Offset, ClearLine.SpeedCap, ClearLine.LateralLimit), boost);
	}

	// Recolocado: a la trazada, sin esperar el boost
	public void Reset()
	{
		ClearLine.Reset();
		_boostPending = false;
	}

	private bool InStraight(float distance)
	{
		float lap = _frame.LapLength;
		float rel = (distance - _boostAt) % lap;
		if (rel < 0f)
			rel += lap;
		return rel < _boostLength;
	}

	// El inicio de la recta más larga de un circuito (m desde la meta); negativo si no hay rectas
	public static float LongestStraightStart(TrackFrame frame) => LongestStraight(frame).Start;

	// La recta más larga de un circuito: dónde empieza (m desde la meta; negativo si no hay rectas) y lo que mide
	public static (float Start, float Length) LongestStraight(TrackFrame frame)
	{
		float start = -1f;
		float length = 0f;
		foreach (TrackSegment segment in TrackAnalysis.Segments(frame.Plan))
		{
			if (!segment.IsCurve && segment.Length > length)
			{
				length = segment.Length;
				start = segment.Start;
			}
		}
		return (start, length);
	}
}
