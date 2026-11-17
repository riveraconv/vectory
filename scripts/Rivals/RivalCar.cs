using System.Collections.Generic;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Contact;
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
public sealed class RivalCar : IContactShip
{
	private readonly RivalRaceContext _context;
	private readonly ShipTuning _tuning;
	private readonly float _halfWidth;
	private readonly LineCrossingDetector _detector;
	private readonly RespawnDetector _respawn;
	private readonly ShipPhysics _physics = new();
	private Vector3 _position;
	private Vector3 _previousPosition;
	private bool _wasInContact;
	private bool _canDrive;

	public RivalEntry Entry { get; }
	public ShipDefinition Ship { get; }
	public RivalDriver Driver { get; }
	public RaceTracker Tracker { get; }
	public DamageModel Damage { get; }
	public BarrierFlash Flash { get; }

	public int Contacts { get; private set; }   // veces que ha empezado a tocar la barrera
	public int Respawns { get; private set; }

	// Golpes medios y fuertes con otras naves (spec docs/rivales-3-contacto.md, 9): embestidas, recibidos y mutuos
	public int Rams { get; private set; }
	public int Received { get; private set; }
	public int Mutual { get; private set; }

	// Choque entre naves (IContactShip)
	public int Id => Entry.Number;
	public ShipShape Shape { get; }
	public Vector3 PreviousPosition => _previousPosition;
	public PassableState Passable { get; } = new();
	public bool CanCollide => _canDrive;

	public Vector3 Position => _position;
	public TrackFrame Frame => _context.Frame;
	public ShipPhysics Physics => _physics;
	public ShipDynamics Dynamics => _physics.Dynamics;
	public ShipBoost Boost => _physics.Boost;
	public BarrierTouch LastTouch => _physics.LastBarrierTouch;

	// Distancia sobre el eje desde la meta (0..largo de la vuelta), para la clasificación
	public float TrackDistance => _physics.HasSurface ? _physics.Surface.Frame.Distance : 0f;

	public RivalPhase Phase => Tracker.State switch
	{
		RaceState.Running => RivalPhase.Running,
		RaceState.Finished => RivalPhase.Finished,
		_ => RivalPhase.PreStart
	};

	// paceOffset: el ajuste global de depuración sobre los márgenes de su piloto (fracción)
	public RivalCar(RivalEntry entry, ShipDefinition ship, PilotCache pilots, RivalRaceContext context, float paceOffset = 0f)
	{
		Entry = entry;
		Ship = ship;
		_context = context;
		_tuning = ship.Technical;
		_halfWidth = ship.Visual.CollisionHalfWidth;
		Shape = ShipShape.From(ship.Visual);

		(float straightStart, float straightLength) = RivalDriver.LongestStraight(context.Frame);
		Driver = new RivalDriver(pilots.Get(ship), context.Frame, _tuning, context.Physics, context.StepTime,
			straightStart, entry.Pilot.Traits, paceOffset, straightLength, context.BarrierOffset)
		{
			ReactionTime = entry.Reaction
		};
		Tracker = new RaceTracker(context.CheckpointIndices.Count, context.Laps);
		Damage = new DamageModel(context.Damage);
		Flash = new BarrierFlash(context.LapLength, context.Flash);
		_respawn = new RespawnDetector(context.Respawn);

		_physics.SetTrack(context.Frame, context.BarrierOffset);
		TrackPlacement.GridStart(context.Frame, entry.GridPosition, ship.Visual.NoseDistance, context.TrackWidth, context.Grid,
			context.Hover, out _position, out float yaw);
		_physics.Place(_position, yaw, 0f, context.Physics.Boost);
		_previousPosition = _position;

		_detector = new LineCrossingDetector(context.Frame.Count);
		_detector.Update(context.Frame.NearestIndex(_position.X, _position.Z));
	}

	// Un paso de física entero, sin otras naves. raceStarted: la carrera ya ha dado el GO (el rival arranca su
	// cronómetro en ese paso).
	public void Step(bool raceStarted)
	{
		Move(raceStarted);
		AfterContacts(false);
	}

	// Él mismo visto desde la pista, para la trazada clara de los demás (y la suya)
	public TrafficShip Traffic => TrafficOf(this);

	// Una nave vista desde la pista: distancia y lateral sobre ella, velocidad a lo largo del eje y su forma. Las que no
	// chocan (atravesables o sin conducir) no tapan a nadie.
	public static TrafficShip TrafficOf(IContactShip ship)
	{
		ShipPhysics physics = ship.Physics;
		if (!physics.HasSurface)
			return new TrafficShip(ship.Id, 0f, 0f, 0f, ship.Shape.HalfWidth, ship.Shape.Front, ship.Shape.Back, true);
		FramePoint f = physics.Surface.Frame;
		Vector2 velocity = physics.Dynamics.Velocity;
		float speed = Vector2.Dot(velocity, new Vector2(f.PlanTangent.X, f.PlanTangent.Z));
		float lateralSpeed = Vector2.Dot(velocity, new Vector2(f.PlanRight.X, f.PlanRight.Z));
		return new TrafficShip(ship.Id, f.Distance, physics.Surface.Lateral, speed, ship.Shape.HalfWidth, ship.Shape.Front,
			ship.Shape.Back, ship.Passable.IsPassable || !ship.CanCollide, lateralSpeed);
	}

	// Primera parte del paso: conducir, mover (barrera incluida) y la carrera. Después van los contactos entre naves.
	// traffic: todas las naves de la carrera al empezar el paso (null = conduce sin mirar)
	public void Move(bool raceStarted, IReadOnlyList<TrafficShip> traffic = null)
	{
		float dt = _context.StepTime;
		if (raceStarted && Tracker.State == RaceState.PreStart)
			Tracker.Start();

		RivalPhase phase = Phase;
		bool canDrive = phase != RivalPhase.PreStart;
		_canDrive = canDrive;
		_previousPosition = _position;

		ShipTuning tuning = _tuning;
		tuning.MaxSpeed *= Damage.TopSpeedFactor;
		tuning.TurnRate *= Damage.TurnFactor;
		_physics.Boost.ActivationAllowed = Damage.BoostAllowed;

		var state = new PilotState(new Vector2(_position.X, _position.Z), Dynamics.Velocity, Dynamics.Yaw, Dynamics.Steer,
			Dynamics.Speed, _physics.Surface.Distance, _physics.Surface.Frame.Bank);
		RivalCommand command = traffic == null
			? Driver.Decide(phase, state, Boost.CanActivate)
			: Driver.Decide(phase, state, Boost.CanActivate, Traffic, traffic);
		_physics.Move(ref _position, command.Input, command.Boost, canDrive, tuning, _halfWidth, _context.Physics, dt);

		_detector.Update(_context.Frame.NearestIndex(_position.X, _position.Z));
		if (Tracker.State == RaceState.Running)
		{
			Tracker.Advance(_detector, _context.CheckpointIndices, Track.FinishIndex, dt);
			// Al terminar, el impulso en curso se corta como en la llegada del jugador
			if (Tracker.State == RaceState.Finished)
			{
				Boost.CancelImpulse();
				Passable.Finished = true;
			}
		}
	}

	// Segunda parte, con los contactos ya resueltos: barrera (destello, daño) y recolocación. touchedShip: ha tocado
	// a otra nave en este paso (cuenta para el atasco).
	public void AfterContacts(bool touchedShip)
	{
		UpdateBarrier(_context.StepTime, _canDrive, touchedShip);
	}

	// Separación de otra nave: la barrera otra vez, sin que pase tiempo
	public void MoveBy(Vector2 delta)
	{
		_position.X += delta.X;
		_position.Z += delta.Y;
		_physics.AfterContactMove(ref _position);
	}

	// Golpe con otra nave que cuenta: estadística y daño (solo en carrera)
	public void OnShipImpact(ContactRole role, int points)
	{
		switch (role)
		{
			case ContactRole.Rammer: Rams++; break;
			case ContactRole.Receiver: Received++; break;
			default: Mutual++; break;
		}
		if (Tracker.State == RaceState.Running)
			Damage.AddPoints(points);
	}

	// Destello, daño (solo en carrera) y recolocación (siempre que conduce, también en la vuelta de enfriamiento)
	private void UpdateBarrier(float dt, bool canDrive, bool touchedShip)
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
		// El atasco cuenta contra la barrera o contra otra nave
		if (_respawn.Update(dt, touch.Contact || touchedShip, Dynamics.RealSpeed, touch.Lateral, _physics.Vertical.Airborne))
		{
			TrackPlacement.Respawn(_context.Frame, _position, _context.Hover, out _position, out float yaw);
			_physics.Respawn(_position, yaw);
			Driver.Reset();
			_previousPosition = _position;
			Passable.Respawned();
			Respawns++;
		}
	}
}
