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

// Lo que el conductor sabe de su nave para el boost: cargas, máximo, impulso en marcha y si va en la última vuelta
public readonly record struct RivalStatus(int Charges, int MaxCharges, bool ImpulseActive, bool LastLap);

// Conductor de un rival (spec docs/rivales-1-parrilla.md, 4; docs/rivales-4-personalidades.md, 3 y 4): envuelve el piloto
// automático de su nave y su trazada clara con el duelo, y decide la entrada según el momento de la carrera. Junta lo
// demás; sin reglas propias.
//  - Previa: nada. En el GO espera su reacción (ReactionTime). En carrera: el piloto, sin derrapar.
//  - Ritmo: márgenes de recta y curva y frenada de los rasgos de su piloto, más el ajuste global de depuración (SetPace).
//  - Duelo: la trazada clara con sus rasgos de ataque, defensa y contacto; atacando por dentro frena más tarde, y con el
//    impulso del boost sigue pisando W (perfiles alternativos del piloto).
//  - Boost: la estrategia de su piloto (RivalBoost), en rectas con recta por delante.
//  - Terminado: el piloto al CooldownPace de su objetivo y sin boost.
// Lógica pura.
public sealed class RivalDriver
{
	public const float DefaultCooldownPace = 0.6f;

	private const float StraightTolerance = 0.999f;
	private const float InsideSearch = 400f;           // m tras la frenada en los que se busca el vértice de la curva
	private const float MinInsideCurvature = 1f / 2000f;

	private readonly PilotData _data;
	private readonly TrackFrame _frame;
	private readonly ShipTuning _tuning;
	private readonly ShipPhysicsSettings _physics;
	private readonly float _stepTime;
	private readonly float _barrierOffset;
	private readonly Random _random;
	private AutopilotDriver _pilot;
	private float _runningTime;
	private int _boostedAt = -1;      // la frenada de la recta en la que gastó la última carga
	private bool[] _onStraight;
	private int[] _nextBrake;
	private int[] _insideAt;
	private static readonly TrafficShip[] NoTraffic = Array.Empty<TrafficShip>();

	public PilotTraits Traits { get; }
	public float PaceOffset { get; private set; }      // ajuste global de depuración sobre los márgenes (fracción)
	public float StraightMargin { get; private set; }  // los márgenes con los que conduce ahora
	public float CurveMargin { get; private set; }
	public float ReactionTime { get; set; }            // s desde el GO hasta que pisa W
	public float CooldownPace { get; set; } = DefaultCooldownPace;
	public AutopilotDriver Pilot => _pilot;
	public ClearLine ClearLine { get; private set; }

	// Cargas de boost gastadas en esta carrera (depuración)
	public int Boosts { get; private set; }

	// La pista por delante en el último paso
	public TrackAhead Ahead { get; private set; }

	// traits: los rasgos de su piloto; la reacción empieza siendo la suya. paceOffset: el ajuste global de depuración.
	// barrierOffset: desplazamiento de la barrera, para el hueco al apartarse. random: para decidir si defiende.
	public RivalDriver(PilotData data, TrackFrame frame, in ShipTuning tuning, in ShipPhysicsSettings physics, float stepTime,
		in PilotTraits traits, float paceOffset = 0f, float barrierOffset = float.PositiveInfinity, Random random = null)
	{
		Traits = traits;
		ReactionTime = traits.Reaction;
		_data = data;
		_frame = frame;
		_tuning = tuning;
		_physics = physics;
		_stepTime = stepTime;
		_barrierOffset = barrierOffset;
		_random = random ?? new Random(0);
		SetPace(paceOffset);
	}

	// El ajuste global de depuración (fracción, se suma a los dos márgenes de sus rasgos; ninguno pasa de su tope)
	public void SetPace(float paceOffset)
	{
		PaceOffset = paceOffset;
		StraightMargin = Traits.StraightWith(paceOffset);
		CurveMargin = Traits.CurveWith(paceOffset);
		SpeedProfile profile = SpeedProfile.Build(_data.Line, _frame, _tuning, _data.Table, _physics, CurveMargin, StraightMargin,
			Traits.Braking);

		// Atacando por dentro frena un poco más tarde; con el impulso, la punta más lo que da el boost
		DuelSettings duel = ClearLine?.DuelSettings ?? DuelSettings.Default;
		float lateBraking = MathF.Min(1f, Traits.Braking + duel.LateBrakingPerAttack * Traits.Attack);
		SpeedProfile late = SpeedProfile.Build(_data.Line, _frame, _tuning, _data.Table, _physics, CurveMargin, StraightMargin, lateBraking);
		SpeedProfile boost = SpeedProfile.Build(_data.Line, _frame, _tuning, _data.Table, _physics, CurveMargin, 1f, Traits.Braking,
			false, _physics.Boost.SpeedGain);
		_pilot = new AutopilotDriver(_data.Line, profile, _tuning, _physics.BankGrip, _stepTime)
		{
			LateProfile = late,
			BoostProfile = boost
		};

		var clearLine = new ClearLine(_data.Line, _data.Table, _frame.LapLength, _barrierOffset, profile.Braking) { Pace = CurveMargin };
		if (ClearLine != null)
		{
			clearLine.Settings = ClearLine.Settings;
			clearLine.DuelSettings = ClearLine.DuelSettings;
		}
		ClearLineSettings settings = clearLine.Settings;
		settings.PassGap = PassGapFor(Traits.Contact);
		clearLine.Settings = settings;
		clearLine.Duel = new DuelTraits(Traits.Attack, Traits.Defense, Traits.Contact);
		clearLine.DuelRandom = _random;
		ClearLine = clearLine;

		BuildTrackAhead(profile);
	}

	// Hueco mínimo entre cascos al adelantar (spec 4.3): 1 m − 0,7 × contacto
	public static float PassGapFor(float contact) => 1f - 0.7f * Math.Clamp(contact, 0f, 1f);

	// boostAvailable: si la nave puede gastar una carga ahora (ShipBoost.CanActivate: carga, sin cooldown, sin bloqueo)
	public RivalCommand Decide(RivalPhase phase, in PilotState state, bool boostAvailable) =>
		Decide(phase, state, boostAvailable, default, NoTraffic, default);

	// Con las demás naves de la carrera (self: él mismo visto desde la pista; traffic puede incluirle) y su boost
	public RivalCommand Decide(RivalPhase phase, in PilotState state, bool boostAvailable, in TrafficShip self,
		IReadOnlyList<TrafficShip> traffic, in RivalStatus status)
	{
		if (phase == RivalPhase.PreStart)
			return default;

		bool finished = phase == RivalPhase.Finished;
		_pilot.TargetScale = finished ? CooldownPace : 1f;
		_pilot.Boosting = !finished && status.ImpulseActive;

		// En el GO, su reacción sin tocar nada (el boost se recarga igual: lo hace la física desde el GO)
		if (!finished && _runningTime < ReactionTime)
		{
			_runningTime += _stepTime;
			return default;
		}

		if (finished)
		{
			_pilot.LateBraking = false;
			ClearLine.Reset();
			return new RivalCommand(_pilot.Drive(state), false);
		}

		TrackAhead ahead = AheadAt(state.Distance);
		Ahead = ahead;
		ClearLine.Update(self, traffic, _stepTime, ahead);
		_pilot.LateBraking = ClearLine.LateBraking;

		LineState line = ClearLine.State;
		bool clear = ClearLine.Blocker < 0 && line is LineState.Clear or LineState.DefendLeft or LineState.DefendRight;
		var situation = new BoostSituation(
			boostAvailable,
			status.Charges,
			status.MaxCharges,
			ahead.OnStraight && ahead.ToBraking >= RivalBoost.MinStraight,
			clear,
			ClearLine.IsAttacking && !ClearLine.LateBraking,
			ClearLine.AheadGap,
			ClearLine.BehindGap,
			status.LastLap,
			_boostedAt == ahead.BrakingIndex);
		bool boost = RivalBoost.Wants(Traits.Boost, situation);
		if (boost)
		{
			_boostedAt = ahead.BrakingIndex;
			Boosts++;
		}

		return new RivalCommand(_pilot.Drive(state, ClearLine.Offset, ClearLine.SpeedCap, ClearLine.LateralLimit), boost);
	}

	// Roce de lado con otra nave (away: hacia dónde apartarse, −1 izquierda, +1 derecha)
	public void OnSideGraze(int away) => ClearLine.Yield(away);

	// Recolocado: a la trazada
	public void Reset()
	{
		ClearLine.Reset();
		_pilot.LateBraking = false;
	}

	// La pista por delante en un punto (m desde la meta)
	public TrackAhead AheadAt(float distance)
	{
		RacingLine line = _data.Line;
		int index = line.IndexAt(distance);
		int brake = _nextBrake[index];
		int points = brake >= index ? brake - index : brake + line.Count - index;
		return new TrackAhead(true, _onStraight[index], points * line.PointSpacing, _insideAt[brake], brake);
	}

	// En cada punto de la trazada: si va en recta (manda la punta, no la curva), dónde empieza la siguiente frenada (donde
	// el objetivo empieza a quedar por debajo de su límite, o donde empieza a mandar la curva) y el interior de la curva
	// que viene detrás de cada frenada (el lado contrario al giro en su punto más lento)
	private void BuildTrackAhead(SpeedProfile profile)
	{
		RacingLine line = _data.Line;
		int n = line.Count;
		_onStraight = new bool[n];
		var starts = new bool[n];
		for (int i = 0; i < n; i++)
		{
			float radius = 1f / MathF.Max(MathF.Abs(line.Curvature[i]), 1e-4f);
			float inCurve = _data.Table.SpeedFor(radius);
			_onStraight[i] = inCurve >= _tuning.MaxSpeed || inCurve * CurveMargin >= _tuning.MaxSpeed * StraightMargin * StraightTolerance;
		}
		// Lo que va frenando ya no es recta: el objetivo por debajo de su límite y bajando (si sube, solo va acelerando)
		for (int i = 0; i < n; i++)
		{
			bool braking = profile.Target[i] < profile.CurveLimit[i] - 0.05f && profile.Target[line.Wrap(i + 1)] < profile.Target[i] - 1e-4f;
			_onStraight[i] = _onStraight[i] && !braking;
		}
		for (int i = 0; i < n; i++)
		{
			int previous = line.Wrap(i - 1);
			starts[i] = _onStraight[previous] && !_onStraight[i];
		}

		_nextBrake = new int[n];
		int next = -1;
		for (int k = 2 * n - 1; k >= 0; k--)
		{
			int i = k % n;
			if (starts[i])
				next = i;
			if (k < n)
				_nextBrake[i] = next >= 0 ? next : i;
		}

		_insideAt = new int[n];
		int search = (int)MathF.Ceiling(InsideSearch / line.PointSpacing);
		for (int i = 0; i < n; i++)
		{
			if (!starts[i])
				continue;
			int slowest = i;
			for (int k = 1; k <= search; k++)
			{
				int j = line.Wrap(i + k);
				if (profile.CurveLimit[j] < profile.CurveLimit[slowest])
					slowest = j;
			}
			float curvature = line.Curvature[slowest];
			// Curvatura positiva: hacia la derecha la curva se abre, así que el interior queda a la izquierda
			_insideAt[i] = MathF.Abs(curvature) < MinInsideCurvature ? 0 : -MathF.Sign(curvature);
		}
	}
}

// Lo que mira la estrategia de boost en un paso (spec docs/rivales-4-personalidades.md, 4.6)
public readonly record struct BoostSituation(bool Available, int Charges, int MaxCharges, bool StraightAhead, bool Clear,
	bool Attacking, float AheadGap, float BehindGap, bool LastLap, bool UsedThisStraight);

// Cuándo gasta un rival una carga de boost (spec 4.6). Solo en una recta con al menos MinStraight por delante, una cada vez
// (Available: carga, sin cooldown ni daño de nivel 3) y nunca con la trazada tapada sin estar atacando. Con todas las
// cargas, gasta una con la trazada clara sea cual sea su estrategia, para no perder recargas. Lógica pura.
public static class RivalBoost
{
	public const float MinStraight = 150f;       // m de recta por delante
	public const float ChasingDistance = 40f;    // ADELANTAR, con la trazada clara: alguien delante a menos de esto
	public const float ChasedDistance = 15f;     // DEFENDER: alguien detrás a menos de esto

	public static bool Wants(BoostStrategy strategy, in BoostSituation s)
	{
		if (!s.Available || !s.StraightAhead || (!s.Clear && !s.Attacking))
			return false;
		if (s.MaxCharges > 0 && s.Charges >= s.MaxCharges && s.Clear)
			return true;
		return strategy switch
		{
			BoostStrategy.Overtake => s.Attacking || (s.Clear && s.AheadGap <= ChasingDistance),
			BoostStrategy.Defend => s.BehindGap <= ChasedDistance,
			BoostStrategy.LastLap => s.LastLap && !s.UsedThisStraight,
			_ => false
		};
	}
}
