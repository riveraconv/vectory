using System.Collections.Generic;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Rivals;

// Lo que comparten todos los rivales de una carrera: el circuito y las reglas, con los mismos valores que el jugador
public sealed class RivalRaceContext
{
	public TrackFrame Frame { get; init; }
	public float TrackWidth { get; init; }
	public float BarrierOffset { get; init; }
	public float LapLength { get; init; }
	public IReadOnlyList<int> CheckpointIndices { get; init; }
	public int Laps { get; init; } = 3;
	public GridSettings Grid { get; init; } = GridSettings.Default;
	public float Hover { get; init; } = 2f;
	public ShipPhysicsSettings Physics { get; init; } = ShipPhysicsSettings.Default;
	public DamageSettings Damage { get; init; } = DamageSettings.Default;
	public RespawnSettings Respawn { get; init; }
	public BarrierFlashSettings Flash { get; init; } = BarrierFlashSettings.Default;
	public float StepTime { get; init; } = 1f / 60f;
}

// La carrera de un rival, sin nodos (spec docs/rivales-1-parrilla.md, 4): su nave en su marca, su conductor, la física
// de siempre (ShipPhysics), su propio RaceTracker y las mismas reglas que el jugador para barreras, destello,
// recolocación y daño. Arranca con el GO de la carrera; al completar la última vuelta cancela el impulso del boost y
// sigue en su vuelta de enfriamiento. Lógica pura: el nodo RivalShip solo la pinta.
public sealed class RivalCar
{
	private readonly RivalRaceContext _context;
	private readonly ShipTuning _tuning;
	private readonly float _halfWidth;
	private readonly LineCrossingDetector _detector;
	private readonly RespawnDetector _respawn;
	private readonly ShipPhysics _physics = new();
	private Vector3 _position;
	private bool _wasInContact;

	public RivalEntry Entry { get; }
	public ShipDefinition Ship { get; }
	public RivalDriver Driver { get; }
	public RaceTracker Tracker { get; }
	public DamageModel Damage { get; }
	public BarrierFlash Flash { get; }

	public int Contacts { get; private set; }   // veces que ha empezado a tocar la barrera
	public int Respawns { get; private set; }

	public Vector3 Position => _position;
	public ShipPhysics Physics => _physics;
	public ShipDynamics Dynamics => _physics.Dynamics;
	public ShipBoost Boost => _physics.Boost;
	public BarrierTouch LastTouch => _physics.LastBarrierTouch;

	public RivalPhase Phase => Tracker.State switch
	{
		RaceState.Running => RivalPhase.Running,
		RaceState.Finished => RivalPhase.Finished,
		_ => RivalPhase.PreStart
	};

	public RivalCar(RivalEntry entry, ShipDefinition ship, PilotCache pilots, RivalRaceContext context, float pace)
	{
		Entry = entry;
		Ship = ship;
		_context = context;
		_tuning = ship.Technical;
		_halfWidth = ship.Visual.CollisionHalfWidth;

		Driver = new RivalDriver(pilots.Get(ship), context.Frame, _tuning, context.Physics, context.StepTime,
			RivalDriver.LongestStraightStart(context.Frame), pace);
		Tracker = new RaceTracker(context.CheckpointIndices.Count, context.Laps);
		Damage = new DamageModel(context.Damage);
		Flash = new BarrierFlash(context.LapLength, context.Flash);
		_respawn = new RespawnDetector(context.Respawn);

		_physics.SetTrack(context.Frame, context.BarrierOffset);
		TrackPlacement.GridStart(context.Frame, entry.GridPosition, ship.Visual.NoseDistance, context.TrackWidth, context.Grid,
			context.Hover, out _position, out float yaw);
		_physics.Place(_position, yaw, 0f, context.Physics.Boost);

		_detector = new LineCrossingDetector(context.Frame.Count);
		_detector.Update(context.Frame.NearestIndex(_position.X, _position.Z));
	}

	// Un paso de física. raceStarted: la carrera ya ha dado el GO (el rival arranca su cronómetro en ese paso).
	public void Step(bool raceStarted)
	{
		float dt = _context.StepTime;
		if (raceStarted && Tracker.State == RaceState.PreStart)
			Tracker.Start();

		RivalPhase phase = Phase;
		bool canDrive = phase != RivalPhase.PreStart;

		ShipTuning tuning = _tuning;
		tuning.MaxSpeed *= Damage.TopSpeedFactor;
		tuning.TurnRate *= Damage.TurnFactor;
		_physics.Boost.ActivationAllowed = Damage.BoostAllowed;

		var state = new PilotState(new Vector2(_position.X, _position.Z), Dynamics.Velocity, Dynamics.Yaw, Dynamics.Steer,
			Dynamics.Speed, _physics.Surface.Distance, _physics.Surface.Frame.Bank);
		RivalCommand command = Driver.Decide(phase, state, Boost.CanActivate);
		_physics.Move(ref _position, command.Input, command.Boost, canDrive, tuning, _halfWidth, _context.Physics, dt);

		_detector.Update(_context.Frame.NearestIndex(_position.X, _position.Z));
		if (Tracker.State == RaceState.Running)
		{
			Tracker.Advance(_detector, _context.CheckpointIndices, Track.FinishIndex, dt);
			// Al terminar, el impulso en curso se corta como en la llegada del jugador
			if (Tracker.State == RaceState.Finished)
				Boost.CancelImpulse();
		}

		UpdateBarrier(dt, canDrive);
	}

	// Destello, daño (solo en carrera) y recolocación (siempre que conduce, también en la vuelta de enfriamiento)
	private void UpdateBarrier(float dt, bool canDrive)
	{
		BarrierTouch touch = _physics.LastBarrierTouch;
		Flash.Update(dt);
		if (touch.Contact)
		{
			if (!_wasInContact)
				Contacts++;
			if (touch.IsNewImpact)
			{
				Flash.Impact(touch.Side, touch.AxisDistance, touch.Level);
				if (Tracker.State == RaceState.Running)
					Damage.AddImpact(touch.Level);
			}
			else
			{
				Flash.Graze(touch.Side, touch.AxisDistance);
			}
		}
		_wasInContact = touch.Contact;

		if (!canDrive)
		{
			_respawn.Reset();
			return;
		}
		if (_respawn.Update(dt, touch.Contact, Dynamics.RealSpeed, touch.Lateral, _physics.Vertical.Airborne))
		{
			TrackPlacement.Respawn(_context.Frame, _position, _context.Hover, out _position, out float yaw);
			_physics.Respawn(_position, yaw);
			Respawns++;
		}
	}
}
