using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Game;
using Vectory.Rivals;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Trazada clara de los rivales (spec docs/rivales-3-contacto.md, 12, pruebas 18–24) y ritmo de cada rival
public class ClearLineTests
{
	private const float Step = 1f / 60f;
	private static readonly ShipPhysicsSettings Physics = ShipPhysicsSettings.Default;

	private static readonly Lazy<(TrackFrame Frame, float BarrierOffset, PilotData Pilot)> Oval = new(() =>
	{
		TrackFile file = TrackFile.Load("test_oval");
		TrackFrame frame = file.Frame();
		var cache = new PilotCache(frame, file.TrackWidth, Physics, AutopilotSettings.Default);
		return (frame, BarrierGeometry.Offset(file.TrackWidth, BarrierGeometry.DefaultGap), cache.Get(Vector));
	});

	private static ShipDefinition Vector => ShipDefinitions.Find(ShipCatalog.Ship01);
	private static readonly float HalfWidth = Vector.Visual.CollisionHalfWidth;
	private static readonly float Front = Vector.Visual.CollisionFront;
	private static readonly float Back = Vector.Visual.CollisionBack;

	// En medio de una recta del óvalo (la trazada va por el centro)
	private const float At = 200f;

	private static ClearLine NewLine()
	{
		(TrackFrame frame, float barrier, PilotData pilot) = Oval.Value;
		return new ClearLine(pilot.Line, pilot.Table, frame.LapLength, barrier, Vector.Technical.BrakeDeceleration * 0.9f);
	}

	private static float LineLateral(float distance) => Oval.Value.Pilot.Line.LateralAt(distance);

	private static TrafficShip Ship(int id, float distance, float lateralFromLine, float speed) =>
		new(id, distance, LineLateral(distance) + lateralFromLine, speed, HalfWidth, Front, Back, false);

	// Lateral al que llega con su desplazamiento: el de la trazada en el punto al que apunta el piloto, más el desplazamiento
	private static float AimedLateral(ClearLine line, float distance, float speed) =>
		LineLateral(distance + AutopilotDriver.Lookahead(speed)) + line.Offset;

	// Ella misma, donde la ha llevado su desplazamiento
	private static TrafficShip Self(ClearLine line, float distance, float speed) =>
		new(0, distance, AimedLateral(line, distance, speed), speed, HalfWidth, Front, Back, false);

	// 18
	[Fact]
	public void With_nobody_ahead_the_line_is_clear()
	{
		ClearLine line = NewLine();
		line.Update(Self(line, At, 60f), Array.Empty<TrafficShip>(), Step);
		Assert.Equal(LineState.Clear, line.State);
		Assert.Equal(0f, line.Offset);
		Assert.True(float.IsPositiveInfinity(line.SpeedCap));

		// Una detrás o a más de la distancia de mirada (máx(30 m, 1,5 s)), tampoco
		line.Update(Self(line, At, 60f), new[] { Ship(1, At - 20f, 0f, 30f), Ship(2, At + 110f, 0f, 30f) }, Step);
		Assert.Equal(LineState.Clear, line.State);
	}

	// 19
	[Fact]
	public void A_slower_ship_in_the_corridor_with_room_on_the_left_makes_it_pass_left_within_the_barrier()
	{
		ClearLine line = NewLine();
		// La de delante en la trazada y otra a la derecha que cierra ese lado
		var others = new[] { Ship(1, At + 30f, 0f, 40f), Ship(2, At + 30f, 9f, 40f) };
		for (int i = 0; i < 600; i++)
			line.Update(Self(line, At, 60f), others, Step);

		Assert.Equal(LineState.PassLeft, line.State);
		// A su izquierda con 1 m de hueco, sin pasar del límite de la barrera
		float lateral = AimedLateral(line, At, 60f);
		Assert.Equal(others[0].Lateral - (2f * HalfWidth + 1f), lateral, 2);
		float limit = Oval.Value.BarrierOffset - 1f - HalfWidth;
		Assert.True(lateral >= -limit - 1e-3f);
	}

	// 20
	[Fact]
	public void Without_room_on_either_side_it_follows_at_half_a_second()
	{
		ClearLine line = NewLine();
		float speed = 60f;
		float wanted = MathF.Max(8f, 0.5f * speed);
		float ahead = At + Front + Back + wanted;
		var others = new[] { Ship(1, ahead, 0f, 50f), Ship(2, ahead, -10f, 50f), Ship(3, ahead, 10f, 50f) };
		line.Update(Self(line, At, speed), others, Step);

		Assert.Equal(LineState.Follow, line.State);
		Assert.Equal(1, line.Blocker);
		Assert.Equal(50f, line.SpeedCap, 3);

		// Más cerca, frena más; nunca por debajo de 0
		line.Update(Self(line, At + 10f, speed), others, Step);
		Assert.True(line.SpeedCap < 50f);

		// Con poca velocidad, el hueco mínimo es de 8 m
		line = NewLine();
		var slow = new[] { Ship(1, At + Front + Back + 8f, 0f, 5f), Ship(2, At + Front + Back + 8f, -10f, 5f), Ship(3, At + Front + Back + 8f, 10f, 5f) };
		line.Update(Self(line, At, 6f), slow, Step);
		Assert.Equal(5f, line.SpeedCap, 3);
	}

	// 21
	[Fact]
	public void A_faster_ship_ahead_does_not_block()
	{
		ClearLine line = NewLine();
		line.Update(Self(line, At, 60f), new[] { Ship(1, At + 25f, 0f, 70f) }, Step);
		Assert.Equal(LineState.Clear, line.State);
		Assert.Equal(-1, line.Blocker);

		// A la misma velocidad sí cuenta
		line.Update(Self(line, At, 60f), new[] { Ship(1, At + 25f, 0f, 60f) }, Step);
		Assert.NotEqual(LineState.Clear, line.State);

		// Atravesable, no
		line = NewLine();
		line.Update(Self(line, At, 60f), new[] { Ship(1, At + 25f, 0f, 30f) with { Passable = true } }, Step);
		Assert.Equal(LineState.Clear, line.State);
	}

	// 22
	[Fact]
	public void The_offset_changes_at_most_four_meters_per_second()
	{
		ClearLine line = NewLine();
		var others = new[] { Ship(1, At + 30f, 0f, 40f) };
		float previous = 0f;
		for (int i = 0; i < 120; i++)
		{
			line.Update(Self(line, At, 60f), others, Step);
			Assert.True(MathF.Abs(line.Offset - previous) <= 4f * Step + 1e-5f);
			previous = line.Offset;
		}
		Assert.NotEqual(0f, line.Offset);
	}

	// 23
	[Fact]
	public void It_returns_to_the_line_only_after_passing_by_five_meters_and_with_that_side_free()
	{
		ClearLine line = NewLine();
		var others = new[] { Ship(1, At + 30f, 0f, 40f) };
		for (int i = 0; i < 300; i++)
			line.Update(Self(line, At, 60f), others, Step);
		float passing = line.Offset;
		Assert.NotEqual(0f, passing);

		// Ya delante, pero su cola solo 3 m por delante del morro del otro: sigue apartada
		float me = At + 100f;
		var justPassed = new[] { Ship(1, me - Back - Front - 3f, 0f, 40f) };
		line.Update(Self(line, me, 60f), justPassed, Step);
		line.Update(Self(line, me, 60f), justPassed, Step);
		Assert.Equal(passing, line.Offset);

		// Con una nave a su lado, entre ella y la trazada, tampoco
		var alongside = new[] { Ship(1, me - Back - Front - 6f, 0f, 40f), Ship(2, me, 0f, 60f) };
		line.Update(Self(line, me, 60f), alongside, Step);
		Assert.Equal(passing, line.Offset);

		// A 6 m y con el lado libre, vuelve poco a poco
		var passed = new[] { Ship(1, me - Back - Front - 6f, 0f, 40f) };
		line.Update(Self(line, me, 60f), passed, Step);
		Assert.Equal(LineState.Clear, line.State);
		Assert.True(MathF.Abs(line.Offset) < MathF.Abs(passing));
		for (int i = 0; i < 600; i++)
			line.Update(Self(line, me, 60f), passed, Step);
		Assert.Equal(0f, line.Offset);
	}

	// 24
	[Fact]
	public void The_boost_waits_for_a_clear_line_while_still_on_the_straight()
	{
		(TrackFrame frame, float barrier, PilotData pilot) = Oval.Value;
		const float BoostAt = 150f;
		var driver = new RivalDriver(pilot, frame, Vector.Technical, Physics, Step, BoostAt, RivalDriver.DefaultPace, 200f, barrier);
		var blocked = new[] { Ship(1, BoostAt + 30f, 0f, 40f) };

		driver.Decide(RivalPhase.Running, State(BoostAt - 1f), true, Ship(0, BoostAt - 1f, 0f, 60f), blocked);
		RivalCommand entering = driver.Decide(RivalPhase.Running, State(BoostAt + 1f), true, Ship(0, BoostAt + 1f, 0f, 60f), blocked);
		Assert.False(entering.Boost);

		// Todavía en la recta y ya clara: lo gasta, una vez
		RivalCommand clear = driver.Decide(RivalPhase.Running, State(BoostAt + 50f), true, Ship(0, BoostAt + 50f, 0f, 60f), Array.Empty<TrafficShip>());
		Assert.True(clear.Boost);
		RivalCommand again = driver.Decide(RivalPhase.Running, State(BoostAt + 51f), true, Ship(0, BoostAt + 51f, 0f, 60f), Array.Empty<TrafficShip>());
		Assert.False(again.Boost);

		// Si la recta se acaba tapada, esa vuelta no lo gasta
		driver = new RivalDriver(pilot, frame, Vector.Technical, Physics, Step, BoostAt, RivalDriver.DefaultPace, 200f, barrier);
		driver.Decide(RivalPhase.Running, State(BoostAt - 1f), true, Ship(0, BoostAt - 1f, 0f, 60f), blocked);
		driver.Decide(RivalPhase.Running, State(BoostAt + 1f), true, Ship(0, BoostAt + 1f, 0f, 60f), blocked);
		RivalCommand after = driver.Decide(RivalPhase.Running, State(BoostAt + 210f), true, Ship(0, BoostAt + 210f, 0f, 60f), Array.Empty<TrafficShip>());
		Assert.False(after.Boost);
	}

	private static PilotState State(float distance, float speed = 60f)
	{
		FramePoint p = Oval.Value.Frame.At(distance);
		float yaw = MathF.Atan2(-p.Tangent.X, -p.Tangent.Z);
		var forward = new Vector2(p.Tangent.X, p.Tangent.Z);
		return new PilotState(new Vector2(p.Position.X, p.Position.Z), forward * speed, yaw, 0f, speed, distance, 0f);
	}

	[Fact]
	public void Each_rival_gets_its_own_pace_after_the_rest_of_the_lineup()
	{
		var order = GridLayout.RandomOrder(8, new Random(5));
		IReadOnlyList<RivalEntry> rivals = RivalLineup.Build(order, new Random(5));
		var again = RivalLineup.Build(GridLayout.RandomOrder(8, new Random(5)), new Random(5));
		Assert.Equal(rivals, again);

		bool different = false;
		foreach (RivalEntry rival in rivals)
		{
			Assert.InRange(rival.PaceOffset, -RivalLineup.PaceSpread, RivalLineup.PaceSpread);
			different |= rival.PaceOffset != rivals[0].PaceOffset;
		}
		Assert.True(different);
		Assert.Equal(0.99f, RivalDriver.PaceFor(0.97f, 0.02f), 5);
		Assert.Equal(1f, RivalDriver.PaceFor(1f, 0.02f), 5);
	}

	[Fact]
	public void Without_offset_or_cap_the_pilot_drives_as_before()
	{
		(TrackFrame frame, _, PilotData pilot) = Oval.Value;
		SpeedProfile profile = SpeedProfile.Build(pilot.Line, frame, Vector.Technical, pilot.Table, Physics, 0.97f);
		var a = new AutopilotDriver(pilot.Line, profile, Vector.Technical, Physics.BankGrip, Step);
		var b = new AutopilotDriver(pilot.Line, profile, Vector.Technical, Physics.BankGrip, Step);
		for (float d = 0f; d < 600f; d += 7f)
			Assert.Equal(a.Drive(State(d)), b.Drive(State(d), 0f, float.PositiveInfinity));
	}
}
