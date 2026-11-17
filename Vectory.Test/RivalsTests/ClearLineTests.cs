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

	// 24 (spec docs/rivales-4-personalidades.md, 4.6: nunca con la trazada tapada sin estar atacando)
	[Fact]
	public void The_boost_is_never_spent_with_a_blocked_line_unless_attacking()
	{
		(TrackFrame frame, float barrier, PilotData pilot) = Oval.Value;
		var driver = new RivalDriver(pilot, frame, Vector.Technical, Physics, Step, PilotTraits.Autopilot, 0f, barrier);
		float d = 0f;
		for (; d < frame.LapLength; d += 5f)
		{
			TrackAhead a = driver.AheadAt(d);
			if (a.OnStraight && a.ToBraking > 200f && a.ToBraking < 230f)
				break;
		}
		var status = new RivalStatus(3, 3, false, false);

		// Una nave mucho más lenta justo delante, en su carril y sin sitio a los lados para atacar: la sigue, sin boost
		var blocked = new[] { Ship(1, d + 15f, 0f, 20f), Ship(2, d + 12f, -9f, 20f), Ship(3, d + 12f, 9f, 20f) };
		RivalCommand command = driver.Decide(RivalPhase.Running, State(d), true, Ship(0, d, 0f, 60f), blocked, status);
		Assert.False(driver.ClearLine.IsAttacking);
		Assert.False(command.Boost);

		// Con la trazada clara, sí
		RivalCommand clear = driver.Decide(RivalPhase.Running, State(d + 1f), true, Ship(0, d + 1f, 0f, 60f), Array.Empty<TrafficShip>(), status);
		Assert.True(clear.Boost);
	}

	private static PilotState State(float distance, float speed = 60f)
	{
		FramePoint p = Oval.Value.Frame.At(distance);
		float yaw = MathF.Atan2(-p.Tangent.X, -p.Tangent.Z);
		var forward = new Vector2(p.Tangent.X, p.Tangent.Z);
		return new PilotState(new Vector2(p.Position.X, p.Position.Z), forward * speed, yaw, 0f, speed, distance, 0f);
	}

	[Fact]
	public void Passing_a_stopped_ship_never_drops_below_the_speed_it_needs_to_steer()
	{
		ClearLine line = NewLine();
		var parked = new[] { Ship(1, At + Front + Back + 8f, 0f, 0f) };
		line.Update(Self(line, At, 0f), parked, Step);

		Assert.True(line.State is LineState.PassLeft or LineState.PassRight);
		Assert.True(line.SpeedCap >= ClearLineSettings.DefaultPassMinSpeed - 1e-4f);

		// Con hueco de sobra, más rápido que el mínimo
		line = NewLine();
		line.Update(Self(line, At, 0f), new[] { Ship(1, At + Front + Back + 40f, 0f, 0f) }, Step);
		Assert.True(line.SpeedCap > ClearLineSettings.DefaultPassMinSpeed + 1f);
	}

	[Fact]
	public void A_rival_behind_a_ship_stopped_on_the_grid_gets_past_it()
	{
		TrackFile file = TrackFile.Load("circuit_01");
		TrackFrame frame = file.Frame();
		float barrier = BarrierGeometry.Offset(file.TrackWidth, BarrierGeometry.DefaultGap);
		var context = new RivalRaceContext
		{
			Frame = frame,
			TrackWidth = file.TrackWidth,
			BarrierOffset = barrier,
			LapLength = frame.LapLength,
			CheckpointIndices = CenterlineBuilder.CheckpointIndices(frame.Plan, file.CheckpointCount),
			Respawn = new RespawnSettings
			{
				BarrierOffset = barrier,
				StuckSpeed = RespawnSettings.DefaultStuckSpeedKmh / 3.6f,
				StuckTime = RespawnSettings.DefaultStuckTime,
				OutsideMargin = RespawnSettings.DefaultOutsideMargin
			}
		};
		var cache = new PilotCache(frame, file.TrackWidth, Physics, AutopilotSettings.Default);
		var entry = new RivalEntry("rival_1", 1, 8, ShipCatalog.Ship01, 0, HullPaints.ChoiceAt(HullStyle.Racing, 0), PilotCatalog.All[0]);
		var car = new RivalCar(entry, Vector, cache, context);

		// Una VECTOR parada en el puesto 6, justo delante en la misma columna
		TrackPlacement.GridStart(frame, 6, Vector.Visual.NoseDistance, file.TrackWidth, context.Grid, Track.ShipHover,
			out System.Numerics.Vector3 position, out _);
		SurfacePoint under = frame.SurfaceUnder(position.X, position.Z, position.Y);
		var parked = new TrafficShip(0, under.Frame.Distance, under.Lateral, 0f, HalfWidth, Front, Back, false);

		float lap = frame.LapLength;
		float ahead = 0f;
		for (int i = 0; i < 60 * 15 && ahead <= 0f; i++)
		{
			car.Move(true, new[] { parked, car.Traffic });
			car.AfterContacts(false);
			float ds = (car.TrackDistance - parked.Distance) % lap;
			ahead = ds > lap / 2f ? ds - lap : ds < -lap / 2f ? ds + lap : ds;
		}
		Assert.True(ahead > 0f, "the rival stayed behind the stopped ship");
	}

	// Spec docs/rivales-4-personalidades.md, 3.6: la reacción de cada salida se sortea después del resto de la alineación
	[Fact]
	public void Each_rival_gets_its_own_reaction_jitter_after_the_rest_of_the_lineup()
	{
		var order = GridLayout.RandomOrder(8, new Random(5));
		IReadOnlyList<RivalEntry> rivals = RivalLineup.Build(order, new Random(5));
		var again = RivalLineup.Build(GridLayout.RandomOrder(8, new Random(5)), new Random(5));
		Assert.Equal(rivals, again);

		bool different = false;
		foreach (RivalEntry rival in rivals)
		{
			Assert.InRange(rival.ReactionJitter, -RivalLineup.ReactionSpread, RivalLineup.ReactionSpread);
			Assert.Equal(MathF.Max(0f, rival.Pilot.Traits.Reaction + rival.ReactionJitter), rival.Reaction, 5);
			different |= rival.ReactionJitter != rivals[0].ReactionJitter;
		}
		Assert.True(different);
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
