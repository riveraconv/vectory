using System;
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
//  - Ritmo: el margen de velocidad del piloto en las curvas (el 97 % de la spec 6), para todos los rivales; cambiarlo
//    rehace el perfil, que es barato (trazada y calibración vienen de la caché).
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
	private AutopilotDriver _pilot;

	public float Pace { get; private set; }
	public float CooldownPace { get; set; } = DefaultCooldownPace;
	public AutopilotDriver Pilot => _pilot;

	// boostAt: m desde la meta donde empieza la recta más larga (negativo = nunca usa boost)
	public RivalDriver(PilotData data, TrackFrame frame, in ShipTuning tuning, in ShipPhysicsSettings physics, float stepTime,
		float boostAt, float pace = DefaultPace)
	{
		_data = data;
		_frame = frame;
		_tuning = tuning;
		_physics = physics;
		_stepTime = stepTime;
		_boostAt = boostAt;
		SetPace(pace);
	}

	public void SetPace(float pace)
	{
		Pace = pace;
		SpeedProfile profile = SpeedProfile.Build(_data.Line, _frame, _tuning, _data.Table, _physics, pace);
		_pilot = new AutopilotDriver(_data.Line, profile, _tuning, _physics.BankGrip, _stepTime, _boostAt);
	}

	// boostAvailable: si la nave puede gastar una carga ahora (ShipBoost.CanActivate: carga, sin cooldown, sin bloqueo)
	public RivalCommand Decide(RivalPhase phase, in PilotState state, bool boostAvailable)
	{
		if (phase == RivalPhase.PreStart)
			return default;

		// El paso por el inicio de la recta se sigue siempre, así no se dispara tarde al volver a poder
		bool atStraight = _pilot.WantsBoost(state.Distance);
		bool finished = phase == RivalPhase.Finished;
		_pilot.TargetScale = finished ? CooldownPace : 1f;
		return new RivalCommand(_pilot.Drive(state), !finished && atStraight && boostAvailable);
	}

	// El inicio de la recta más larga de un circuito (m desde la meta); negativo si no hay rectas
	public static float LongestStraightStart(TrackFrame frame)
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
		return start;
	}
}
