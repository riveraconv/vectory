using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Balance;

// Un circuito para simular, sin nodos: su marco y lo que se deduce de él
public sealed class BalanceCircuit
{
	public string Id { get; }
	public string Name { get; }
	public bool IsTest { get; }
	public TrackFrame Frame { get; }
	public float TrackWidth { get; }
	public IReadOnlyList<int> CheckpointIndices { get; }
	public IReadOnlyList<TrackSegment> Curves { get; }

	// m desde la meta: el inicio de la recta más larga (negativo si no hay ninguna)
	public float LongestStraightStart { get; }

	public BalanceCircuit(string id, string name, bool isTest, TrackFrame frame, float trackWidth, int checkpointCount)
	{
		Id = id;
		Name = name;
		IsTest = isTest;
		Frame = frame;
		TrackWidth = trackWidth;
		CheckpointIndices = CenterlineBuilder.CheckpointIndices(frame.Plan, checkpointCount);

		var curves = new List<TrackSegment>();
		TrackSegment? longest = null;
		foreach (TrackSegment segment in TrackAnalysis.Segments(frame.Plan))
		{
			if (segment.IsCurve)
				curves.Add(segment);
			else if (longest == null || segment.Length > longest.Value.Length)
				longest = segment;
		}
		Curves = curves;
		LongestStraightStart = longest?.Start ?? -1f;
	}
}

// Una carrera sin escena (spec docs/naves-6-equilibrio.md, 4.1): un circuito, una nave y la física completa, con las
// mismas clases que el juego y en su mismo orden: ShipPhysics, cruces y vueltas, daño y recolocación. No conoce al
// piloto: la entrada le llega de fuera. Lógica pura: no depende de Godot, y dos carreras iguales dan exactamente lo mismo.
public sealed class ShipSimulator
{
	public const float StepTime = 1f / 60f;   // el paso de física del juego
	public const int GridPosition = 1;

	private readonly BalanceCircuit _circuit;
	private readonly ShipTuning _tuning;
	private readonly float _halfWidth;
	private readonly ShipPhysicsSettings _settings = ShipPhysicsSettings.Default;
	private readonly LineCrossingDetector _detector;
	private readonly RespawnDetector _respawnDetector;
	private readonly DamageModel _damage = new(DamageSettings.Default);
	private readonly ShipPhysics _physics = new();
	private Vector3 _position;

	public RaceTracker Tracker { get; }
	public int Respawns { get; private set; }
	public double Time { get; private set; }

	public ShipDynamics Dynamics => _physics.Dynamics;
	public ShipBoost Boost => _physics.Boost;
	public ShipVertical Vertical => _physics.Vertical;
	public BarrierTouch LastTouch => _physics.LastBarrierTouch;
	public Vector3 Position => _position;
	public float Distance => _physics.Surface.Distance;
	public float Lateral => _physics.Surface.Lateral;
	public float Bank => _physics.Surface.Frame.Bank;
	public int DamageLevel => _damage.Level;
	public int Laps { get; }

	public ShipSimulator(BalanceCircuit circuit, ShipDefinition ship, int laps)
	{
		_circuit = circuit;
		_tuning = ship.Technical;
		_halfWidth = ship.Visual.CollisionHalfWidth;
		Laps = laps;

		float barrierOffset = BarrierGeometry.Offset(circuit.TrackWidth, BarrierGeometry.DefaultGap);
		_physics.SetTrack(circuit.Frame, barrierOffset);
		_respawnDetector = new RespawnDetector(new RespawnSettings
		{
			BarrierOffset = barrierOffset,
			StuckSpeed = RespawnSettings.DefaultStuckSpeedKmh / 3.6f,
			StuckTime = RespawnSettings.DefaultStuckTime,
			OutsideMargin = RespawnSettings.DefaultOutsideMargin
		});

		TrackPlacement.GridStart(circuit.Frame, GridPosition, ship.Visual.NoseDistance, circuit.TrackWidth, GridSettings.Default,
			_settings.Vertical.Hover, out _position, out float yaw);
		_physics.Place(_position, yaw, 0f, _settings.Boost);

		Tracker = new RaceTracker(circuit.CheckpointIndices.Count, laps);
		_detector = new LineCrossingDetector(circuit.Frame.Count);
		_detector.Update(circuit.Frame.NearestIndex(_position.X, _position.Z));
		Tracker.Start();
	}

	public void Step(in ShipInput input, bool boostPressed)
	{
		bool canDrive = Tracker.State == RaceState.Running;

		ShipTuning tuning = _tuning;
		tuning.MaxSpeed *= _damage.TopSpeedFactor;
		tuning.TurnRate *= _damage.TurnFactor;
		_physics.Boost.ActivationAllowed = _damage.BoostAllowed;

		_physics.Move(ref _position, input, boostPressed, canDrive, tuning, _halfWidth, _settings, StepTime);
		Time += StepTime;

		_detector.Update(_circuit.Frame.NearestIndex(_position.X, _position.Z));
		if (Tracker.State == RaceState.Running)
			Tracker.Advance(_detector, _circuit.CheckpointIndices, Track.FinishIndex, StepTime);

		BarrierTouch touch = _physics.LastBarrierTouch;
		if (touch.IsNewImpact && Tracker.State == RaceState.Running)
			_damage.AddImpact(touch.Level);

		if (Tracker.State != RaceState.Running)
		{
			_respawnDetector.Reset();
			return;
		}

		if (_respawnDetector.Update(StepTime, touch.Contact, _physics.Dynamics.RealSpeed, touch.Lateral, _physics.Vertical.Airborne))
		{
			TrackPlacement.Respawn(_circuit.Frame, _position, _settings.Vertical.Hover, out _position, out float yaw);
			_physics.Respawn(_position, yaw);
			Respawns++;
		}
	}
}
