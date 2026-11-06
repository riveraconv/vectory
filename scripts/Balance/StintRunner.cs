using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Balance;

// Resultado de una tanda (spec docs/naves-6-equilibrio.md, 4.5). Lo de los tiempos de sector, las velocidades y el
// porcentaje de W es de la vuelta lanzada.
public sealed class StintResult
{
	public string CircuitId { get; init; } = "";
	public string CircuitName { get; init; } = "";
	public bool CircuitIsTest { get; init; }
	public string ShipId { get; init; } = "";
	public bool UsedBoost { get; init; }

	public bool Finished { get; init; }
	public int Respawns { get; init; }
	public bool IsValid => Finished && Respawns == 0;

	public double FlyingLap { get; init; }
	public double FirstLap { get; init; }
	public double TotalTime { get; init; }
	public IReadOnlyList<double> Sectors { get; init; } = Array.Empty<double>();

	public float TopSpeed { get; init; }          // m/s
	public float TopSpeedAt { get; init; }        // m desde la meta
	public IReadOnlyList<float> CurveMinSpeeds { get; init; } = Array.Empty<float>();   // m/s, una por curva
	public float FullThrottleShare { get; init; } // de 0 a 1

	public int Grazes { get; init; }
	public int MediumImpacts { get; init; }
	public int StrongImpacts { get; init; }
	public int DamageLevel { get; init; }
	public int BoostsUsed { get; init; }
	public float FirstBoostAt { get; init; } = -1f;   // m desde la meta
}

// Opciones de una tanda
public struct StintOptions
{
	public const int DefaultLaps = 3;
	public const int FlyingLapNumber = 2;   // la segunda vuelta, ya en marcha desde la meta

	public int Laps;
	public AutopilotSettings Pilot;

	public static StintOptions Default => new() { Laps = DefaultLaps, Pilot = AutopilotSettings.Default };
}

// Una tanda: el piloto conduce una nave en un circuito hasta terminar sus vueltas, desde parado, sin rivales.
// El daño por contacto se aplica y se apunta; si hay recolocación, la tanda no vale. Lógica pura: no depende de Godot.
public static class StintRunner
{
	private const double MaxTime = 900.0;   // s: una tanda que no acaba en este tiempo no vale

	public static StintResult Run(BalanceCircuit circuit, ShipDefinition ship, in StintOptions options)
	{
		ShipPhysicsSettings physics = ShipPhysicsSettings.Default;
		AutopilotDriver driver = AutopilotDriver.Create(circuit.Frame, circuit.TrackWidth, ship.Visual.CollisionHalfWidth,
			ship.Technical, physics, options.Pilot, circuit.LongestStraightStart);
		var simulator = new ShipSimulator(circuit, ship, options.Laps);

		var laps = new List<double>();
		var sectors = new List<double>();
		simulator.Tracker.LapCompleted += (_, time, _) => laps.Add(time);
		simulator.Tracker.SectorCompleted += (lap, _, time, _) =>
		{
			if (lap == StintOptions.FlyingLapNumber)
				sectors.Add(time);
		};
		double total = 0.0;
		simulator.Tracker.RaceFinished += time => total = time;

		var curveMin = new float[circuit.Curves.Count];
		Array.Fill(curveMin, float.MaxValue);
		float top = 0f;
		float topAt = 0f;
		int flyingSteps = 0;
		int throttleSteps = 0;
		int grazes = 0;
		int medium = 0;
		int strong = 0;
		int boosts = 0;
		float firstBoostAt = -1f;
		bool wasInContact = false;

		while (simulator.Tracker.State == RaceState.Running && simulator.Time < MaxTime)
		{
			ShipDynamics d = simulator.Dynamics;
			Vector3 p = simulator.Position;
			var state = new PilotState(new Vector2(p.X, p.Z), d.Velocity, d.Yaw, d.Steer, d.Speed, simulator.Distance, simulator.Bank);
			ShipInput input = driver.Drive(state);
			bool boost = driver.WantsBoost(simulator.Distance);
			int chargesBefore = simulator.Boost.Charges;
			float distanceBefore = simulator.Distance;

			bool flying = simulator.Tracker.CurrentLap == StintOptions.FlyingLapNumber;
			simulator.Step(input, boost);

			if (simulator.Boost.Charges < chargesBefore)
			{
				if (boosts == 0)
					firstBoostAt = distanceBefore;
				boosts++;
			}

			BarrierTouch touch = simulator.LastTouch;
			if (touch.IsNewImpact)
			{
				if (touch.Level == ImpactLevel.Strong)
					strong++;
				else
					medium++;
			}
			else if (touch.Contact && !wasInContact)
			{
				grazes++;
			}
			wasInContact = touch.Contact;

			if (!flying)
				continue;

			flyingSteps++;
			if (input.Accelerate)
				throttleSteps++;

			float speed = d.RealSpeed;
			if (speed > top)
			{
				top = speed;
				topAt = simulator.Distance;
			}
			for (int i = 0; i < curveMin.Length; i++)
			{
				if (Contains(circuit.Curves[i], simulator.Distance, circuit.Frame.LapLength))
					curveMin[i] = MathF.Min(curveMin[i], speed);
			}
		}

		bool finished = simulator.Tracker.State == RaceState.Finished && laps.Count >= StintOptions.FlyingLapNumber;
		return new StintResult
		{
			CircuitId = circuit.Id,
			CircuitName = circuit.Name,
			CircuitIsTest = circuit.IsTest,
			ShipId = ship.Id,
			UsedBoost = options.Pilot.UseBoost,
			Finished = finished,
			Respawns = simulator.Respawns,
			FlyingLap = laps.Count >= StintOptions.FlyingLapNumber ? laps[StintOptions.FlyingLapNumber - 1] : 0.0,
			FirstLap = laps.Count >= 1 ? laps[0] : 0.0,
			TotalTime = total,
			Sectors = sectors,
			TopSpeed = top,
			TopSpeedAt = topAt,
			CurveMinSpeeds = curveMin,
			FullThrottleShare = flyingSteps > 0 ? (float)throttleSteps / flyingSteps : 0f,
			Grazes = grazes,
			MediumImpacts = medium,
			StrongImpacts = strong,
			DamageLevel = simulator.DamageLevel,
			BoostsUsed = boosts,
			FirstBoostAt = firstBoostAt
		};
	}

	private static bool Contains(in TrackSegment segment, float distance, float lapLength)
	{
		float offset = (distance - segment.Start) % lapLength;
		if (offset < 0f)
			offset += lapLength;
		return offset <= segment.Length;
	}
}
