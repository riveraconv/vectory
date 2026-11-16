using System;
using System.Linq;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Game;
using Vectory.Rivals;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Conductor de rival, caché de trazadas y carrera de cada rival (spec docs/rivales-1-parrilla.md, 7, pruebas 5–11)
public class RivalDriverTests
{
	private const float Step = 1f / 60f;
	private static readonly ShipPhysicsSettings Physics = ShipPhysicsSettings.Default;

	// El óvalo de pruebas, leído de su .tres; la caché se comparte entre pruebas para no calibrar de más
	private static readonly Lazy<(TrackFrame Frame, TrackFile File)> Oval = new(() =>
	{
		TrackFile file = TrackFile.Load("test_oval");
		return (file.Frame(), file);
	});
	private static readonly Lazy<PilotCache> Cache = new(() =>
		new PilotCache(Oval.Value.Frame, Oval.Value.File.TrackWidth, Physics, AutopilotSettings.Default));

	private static ShipDefinition Vector => ShipDefinitions.Find(ShipCatalog.Ship01);

	private static RivalDriver Driver(float pace = RivalDriver.DefaultPace, float boostAt = 100f) =>
		new(Cache.Value.Get(Vector), Oval.Value.Frame, Vector.Technical, Physics, Step, boostAt, pace);

	private static PilotState At(float distance, float speed = 0f)
	{
		TrackFrame frame = Oval.Value.Frame;
		FramePoint p = frame.At(distance);
		float yaw = MathF.Atan2(-p.Tangent.X, -p.Tangent.Z);
		var forward = new Vector2(p.Tangent.X, p.Tangent.Z);
		return new PilotState(new Vector2(p.Position.X, p.Position.Z), forward * speed, yaw, 0f, speed, distance, 0f);
	}

	private static RivalRaceContext Context()
	{
		(TrackFrame frame, TrackFile file) = Oval.Value;
		return new RivalRaceContext
		{
			Frame = frame,
			TrackWidth = file.TrackWidth,
			BarrierOffset = BarrierGeometry.Offset(file.TrackWidth, BarrierGeometry.DefaultGap),
			LapLength = frame.LapLength,
			CheckpointIndices = CenterlineBuilder.CheckpointIndices(frame.Plan, file.CheckpointCount),
			Respawn = new RespawnSettings
			{
				BarrierOffset = BarrierGeometry.Offset(file.TrackWidth, BarrierGeometry.DefaultGap),
				StuckSpeed = RespawnSettings.DefaultStuckSpeedKmh / 3.6f,
				StuckTime = RespawnSettings.DefaultStuckTime,
				OutsideMargin = RespawnSettings.DefaultOutsideMargin
			}
		};
	}

	private static RivalEntry Entry(int number, int grid) =>
		new($"rival_{number}", number, grid, ShipCatalog.Ship01, 0, HullPaints.ChoiceAt(HullStyle.Racing, 0),
			PilotCatalog.All[number - 1]);

	// 5
	[Fact]
	public void Before_the_start_there_is_no_input_and_in_the_race_there_is()
	{
		RivalDriver driver = Driver();

		Assert.Equal(default, driver.Decide(RivalPhase.PreStart, At(10f), true));
		RivalCommand running = driver.Decide(RivalPhase.Running, At(10f), true);
		Assert.True(running.Input.Accelerate);
		Assert.False(running.Input.Drift);
	}

	// 6
	[Fact]
	public void Boost_only_when_entering_the_longest_straight_with_a_charge_one_at_a_time()
	{
		RivalDriver driver = Driver(boostAt: 100f);

		Assert.False(driver.Decide(RivalPhase.Running, At(90f, 50f), true).Boost);
		Assert.True(driver.Decide(RivalPhase.Running, At(101f, 50f), true).Boost);
		Assert.False(driver.Decide(RivalPhase.Running, At(102f, 50f), true).Boost);

		// Sin carga o con cooldown, pasa de largo y no lo gasta más tarde
		RivalDriver empty = Driver(boostAt: 100f);
		empty.Decide(RivalPhase.Running, At(90f, 50f), false);
		Assert.False(empty.Decide(RivalPhase.Running, At(101f, 50f), false).Boost);
		Assert.False(empty.Decide(RivalPhase.Running, At(102f, 50f), true).Boost);
	}

	[Fact]
	public void With_damage_level_3_it_never_boosts()
	{
		var damage = new DamageModel(DamageSettings.Default);
		for (int i = 0; i < 5; i++)
			damage.AddImpact(ImpactLevel.Strong);
		Assert.Equal(3, damage.Level);

		var boost = new ShipBoost();
		boost.Reset(Physics.Boost with { InitialCharges = 3 });
		boost.ActivationAllowed = damage.BoostAllowed;

		RivalDriver driver = Driver(boostAt: 100f);
		driver.Decide(RivalPhase.Running, At(90f, 50f), boost.CanActivate);
		Assert.False(driver.Decide(RivalPhase.Running, At(101f, 50f), boost.CanActivate).Boost);
	}

	// 7
	[Fact]
	public void Finished_goes_at_60_percent_without_boost()
	{
		RivalDriver driver = Driver(boostAt: 100f);
		driver.Decide(RivalPhase.Finished, At(90f, 50f), true);
		RivalCommand command = driver.Decide(RivalPhase.Finished, At(101f, 50f), true);

		Assert.False(command.Boost);
		Assert.Equal(0.6f, driver.Pilot.TargetScale);

		driver.Decide(RivalPhase.Running, At(150f, 50f), true);
		Assert.Equal(1f, driver.Pilot.TargetScale);
	}

	// 8
	[Fact]
	public void The_pace_scales_the_curve_speeds_of_the_table()
	{
		float[] full = Driver(1f).Pilot.Profile.CurveLimit;
		RivalDriver slower = Driver(0.9f);
		float[] paced = slower.Pilot.Profile.CurveLimit;
		float top = Vector.Technical.MaxSpeed;

		int curvePoints = 0;
		for (int i = 0; i < full.Length; i++)
		{
			if (full[i] >= top * 0.9f)
				continue;
			curvePoints++;
			Assert.Equal(full[i] * 0.9f, paced[i], 3);
		}
		Assert.True(curvePoints > 10);
		Assert.Equal(0.9f, slower.Pace);

		slower.SetPace(1f);
		Assert.Equal(full, slower.Pilot.Profile.CurveLimit);
	}

	// 9
	[Fact]
	public void Rivals_with_the_same_ship_share_line_and_calibration_and_absent_ships_are_not_computed()
	{
		var cache = new PilotCache(Oval.Value.Frame, Oval.Value.File.TrackWidth, Physics, AutopilotSettings.Default);
		PilotData a = cache.Get(Vector);
		PilotData b = cache.Get(Vector);

		Assert.Same(a, b);
		Assert.Equal(new[] { ShipCatalog.Ship01 }, cache.ComputedShips.ToArray());
	}

	[Fact]
	public void Preparing_computes_each_ship_once_and_gives_the_same_as_computing_alone()
	{
		ShipDefinition blade = ShipDefinitions.Find(ShipCatalog.Ship02);
		var prepared = new PilotCache(Oval.Value.Frame, Oval.Value.File.TrackWidth, Physics, AutopilotSettings.Default);
		prepared.Prepare(new[] { Vector, blade, Vector });

		Assert.Equal(2, prepared.ComputedShips.Count);
		PilotData alone = Cache.Value.Get(blade);
		PilotData parallel = prepared.Get(blade);
		Assert.Equal(alone.Line.Points, parallel.Line.Points);
		Assert.Equal(alone.Table.SpeedFor(80f), parallel.Table.SpeedFor(80f));
	}

	// 10
	[Fact]
	public void Each_rival_counts_its_own_laps()
	{
		RivalRaceContext context = Context();
		var leader = new RivalCar(Entry(1, 1), Vector, Cache.Value, context, RivalDriver.DefaultPace);
		var waiting = new RivalCar(Entry(2, 2), Vector, Cache.Value, context, RivalDriver.DefaultPace);
		var player = new RaceTracker(context.CheckpointIndices.Count, context.Laps);

		for (int i = 0; i < 60 * 90 && leader.Tracker.CurrentLap < 3; i++)
		{
			leader.Step(true);
			waiting.Step(false);
		}

		Assert.Equal(3, leader.Tracker.CurrentLap);
		Assert.Equal(0, leader.Respawns);
		Assert.Equal(RaceState.PreStart, waiting.Tracker.State);
		Assert.Equal(0, waiting.Dynamics.Speed);
		Assert.Equal(RaceState.PreStart, player.State);
	}

	[Fact]
	public void A_rival_finishes_its_race_and_keeps_driving_slowly()
	{
		RivalRaceContext context = Context();
		var car = new RivalCar(Entry(1, 3), Vector, Cache.Value, context, RivalDriver.DefaultPace);

		int steps = 0;
		while (car.Tracker.State != RaceState.Finished && steps++ < 60 * 180)
			car.Step(true);
		Assert.Equal(RaceState.Finished, car.Tracker.State);
		Assert.False(car.Boost.IsImpulseActive);

		for (int i = 0; i < 60 * 20; i++)
			car.Step(true);
		Assert.Equal(RivalPhase.Finished, car.Phase);
		Assert.InRange(car.Dynamics.Speed, 5f, Vector.Technical.MaxSpeed * 0.65f);
	}

	// 11
	[Fact]
	public void There_are_no_rivals_in_time_trial()
	{
		Assert.Equal(0, RivalLineup.CountFor(GameMode.TimeTrial, 8, 7));
		Assert.Equal(7, RivalLineup.CountFor(GameMode.Race, 8, 7));
		Assert.Equal(7, RivalLineup.CountFor(GameMode.Race, 8, 20));
		Assert.Equal(0, RivalLineup.CountFor(GameMode.Race, 8, -1));
	}
}
