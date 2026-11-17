using System;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Rivals;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Rebufo, duelo y boost de los rivales (spec docs/rivales-4-personalidades.md, 10, pruebas 7–16)
public class DuelTests
{
	private const float Step = 1f / 60f;
	private const float Speed = 80f;   // m/s, por encima de los 150 km/h del rebufo
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
	private static float Length => Front + Back;

	// En medio de una recta del óvalo (la trazada va por el centro)
	private const float At = 200f;

	private static TrafficShip Ship(int id, float distance, float lateral, float speed, float lateralSpeed = 0f) =>
		new(id, distance, lateral, speed, HalfWidth, Front, Back, false, lateralSpeed);

	private static ClearLine Duel(float attack = 0.5f, float defense = 0.5f, float contact = 0.3f)
	{
		(TrackFrame frame, float barrier, PilotData pilot) = Oval.Value;
		var line = new ClearLine(pilot.Line, pilot.Table, frame.LapLength, barrier, Vector.Technical.BrakeDeceleration * 0.9f)
		{
			Duel = new DuelTraits(attack, defense, contact)
		};
		ClearLineSettings settings = line.Settings;
		settings.PassGap = RivalDriver.PassGapFor(contact);
		line.Settings = settings;
		return line;
	}

	private static TrackAhead Straight(float toBraking, int inside = 1, int brakingIndex = 10) =>
		new(true, true, toBraking, inside, brakingIndex);

	// --- Rebufo ---

	private static float Target(float gap, float lateral = 0f, float speed = Speed, float otherSpeed = Speed, bool airborne = false)
	{
		var self = Ship(0, 0f, lateral, speed);
		var other = Ship(1, gap + Front + Back, 0f, otherSpeed);
		return Slipstream.Target(self, airborne, new[] { self, other }, new[] { airborne, false }, SlipstreamSettings.Default, 0f, out _);
	}

	// 7
	[Fact]
	public void Slipstream_strength_goes_from_1_at_5_m_to_0_at_25_m_and_needs_to_be_behind_the_other()
	{
		Assert.Equal(1f, Target(5f), 4);
		Assert.Equal(1f, Target(2f), 4);
		Assert.Equal(0.5f, Target(15f), 4);
		Assert.Equal(0f, Target(30f), 4);
		// Desviado más que el semiancho de la de delante
		Assert.Equal(1f, Target(5f, HalfWidth - 0.01f), 4);
		Assert.Equal(0f, Target(5f, HalfWidth + 0.01f), 4);

		// Cuenta la más cercana, y la de delante no lo tiene
		var self = Ship(0, 0f, 0f, Speed);
		var near = Ship(1, 10f + Length, 0f, Speed);
		var far = Ship(2, 22f + Length, 0f, Speed);
		var all = new[] { self, near, far };
		var air = new[] { false, false, false };
		float r = Slipstream.Target(self, false, all, air, SlipstreamSettings.Default, 0f, out int source);
		Assert.Equal(1, source);
		Assert.Equal(0.75f, r, 4);
		Assert.Equal(0f, Slipstream.Target(far, false, all, air, SlipstreamSettings.Default, 0f, out _));
	}

	// 8
	[Fact]
	public void No_slipstream_below_150_kmh_in_the_air_with_boost_or_behind_a_passable_ship()
	{
		float slow = 150f / 3.6f - 0.5f;
		Assert.Equal(0f, Target(5f, speed: slow));
		Assert.Equal(0f, Target(5f, otherSpeed: slow));
		Assert.Equal(0f, Target(5f, airborne: true));

		var self = Ship(0, 0f, 0f, Speed);
		var passable = Ship(1, 5f + Length, 0f, Speed) with { Passable = true };
		Assert.Equal(0f, Slipstream.Target(self, false, new[] { self, passable }, null, SlipstreamSettings.Default, 0f, out _));

		var slipstream = new Slipstream();
		slipstream.Update(1f, 1, false, 1f);
		Assert.Equal(1f, slipstream.Strength);
		slipstream.Update(1f, 1, true, Step);
		Assert.Equal(0f, slipstream.Strength);
	}

	// 9
	[Fact]
	public void Slipstream_rises_in_half_a_second_and_falls_in_0_3_s()
	{
		var slipstream = new Slipstream();
		for (int i = 0; i < 15; i++)
			slipstream.Update(1f, 1, false, Step);
		Assert.Equal(0.5f, slipstream.Strength, 3);
		for (int i = 0; i < 15; i++)
			slipstream.Update(1f, 1, false, Step);
		Assert.Equal(1f, slipstream.Strength, 3);

		for (int i = 0; i < 9; i++)
			slipstream.Update(0f, -1, false, Step);
		Assert.Equal(0.5f, slipstream.Strength, 3);
		for (int i = 0; i < 9; i++)
			slipstream.Update(0f, -1, false, Step);
		Assert.Equal(0f, slipstream.Strength, 3);
		Assert.Equal(-1, slipstream.Source);
	}

	// 10
	[Fact]
	public void Full_slipstream_gives_4_percent_more_top_speed_and_30_percent_less_drag_only_to_the_ship_behind()
	{
		var slipstream = new Slipstream();
		slipstream.Update(1f, 1, false, 1f);
		Assert.Equal(1.04f, slipstream.TopFactor, 5);
		Assert.Equal(0.7f, slipstream.DragFactor, 5);

		// La de delante: nadie delante, nada
		var leader = Ship(1, 5f + Length, 0f, Speed);
		var follower = Ship(0, 0f, 0f, Speed);
		Assert.Equal(0f, Slipstream.Target(leader, false, new[] { follower, leader }, null, SlipstreamSettings.Default, 0f, out _));
		var none = new Slipstream();
		Assert.Equal(1f, none.TopFactor);
		Assert.Equal(1f, none.DragFactor);
	}

	// --- Duelo ---

	// 11
	[Fact]
	public void Catching_a_ship_on_a_straight_with_200_m_left_it_slips_with_100_m_it_does_not()
	{
		ClearLine line = Duel(attack: 0.5f);
		var ahead = new[] { Ship(1, At + 15f, 0f, Speed) };
		line.Update(Ship(0, At, 0f, Speed), ahead, Step, Straight(200f));
		Assert.Equal(LineState.Slip, line.State);
		// Pegada detrás: su velocidad máxima la deja a SlipGap de la de delante
		Assert.True(line.SpeedCap < float.PositiveInfinity);

		ClearLine late = Duel(attack: 0.5f);
		late.Update(Ship(0, At, 0f, Speed), ahead, Step, Straight(100f));
		Assert.NotEqual(LineState.Slip, late.State);
	}

	// 12
	[Fact]
	public void It_leaves_the_slipstream_at_120_m_plus_120_m_times_attack_before_the_braking_point()
	{
		var ahead = new[] { Ship(1, At + 15f, 0f, Speed) };
		foreach ((float attack, float exit) in new[] { (0.5f, 180f), (1f, 240f) })
		{
			ClearLine line = Duel(attack);
			line.Update(Ship(0, At, 0f, Speed), ahead, Step, Straight(exit + 2f));
			Assert.Equal(LineState.Slip, line.State);
			line.Update(Ship(0, At, 0f, Speed), ahead, Step, Straight(exit - 2f));
			Assert.True(line.IsAttacking, $"attack {attack}");
			Assert.Equal(1, line.AttackTarget);
		}

		// Con poco ataque y sin hueco a ningún lado, se queda detrás
		ClearLine shy = Duel(attack: 0.2f);
		var boxed = new[] { Ship(1, At + 15f, 0f, Speed), Ship(2, At + 15f, -9f, Speed), Ship(3, At + 15f, 9f, Speed) };
		shy.Update(Ship(0, At, 0f, Speed), boxed, Step, Straight(200f));
		shy.Update(Ship(0, At, 0f, Speed), boxed, Step, Straight(100f));
		Assert.Equal(LineState.Follow, shy.State);
	}

	// 13
	[Fact]
	public void It_aborts_without_the_nose_at_half_of_the_other_at_the_braking_point_but_with_contact_0_8_a_quarter_is_enough()
	{
		const float ToBraking = 230f;
		foreach ((float contact, float alongside, bool aborts) in new[] { (0.3f, 0.3f, true), (0.3f, 0.6f, false), (0.8f, 0.3f, false), (0.8f, 0.2f, true) })
		{
			ClearLine line = Duel(attack: 1f, contact: contact);
			line.Update(Ship(0, At, 0f, Speed), new[] { Ship(1, At + 15f, 0f, Speed) }, Step, Straight(ToBraking));
			Assert.True(line.IsAttacking);
			int side = line.State == LineState.AttackLeft ? -1 : 1;

			// En el punto de frenada, a su lado con el morro en esa fracción de su largo
			float self = At + ToBraking;
			float other = self + Front + Back - alongside * Length;
			var beside = new[] { Ship(1, other, 0f, Speed) };
			line.Update(Ship(0, self, side * (2f * HalfWidth + 1f), Speed), beside, Step, Straight(0f, brakingIndex: 11));
			if (aborts)
			{
				Assert.Equal(LineState.Follow, line.State);
				Assert.Equal(1, line.Aborts);
				Assert.Equal(-1, line.AttackTarget);
				// Durante la espera no lo vuelve a intentar con la misma
				line.Update(Ship(0, self + 100f, 0f, Speed), new[] { Ship(1, self + 115f, 0f, Speed) }, Step, Straight(250f, brakingIndex: 12));
				Assert.Equal(LineState.Follow, line.State);
			}
			else
			{
				Assert.True(line.IsAttacking, $"contact {contact}, alongside {alongside}");
			}
		}
	}

	// 14
	[Fact]
	public void Defending_covers_the_inside_once_per_straight_and_not_toward_a_ship_a_quarter_alongside()
	{
		var chaser = Ship(1, At - Length - 8f, 0f, Speed + 3f);
		ClearLine line = Duel(defense: 1f);
		line.Update(Ship(0, At, 0f, Speed), new[] { chaser }, Step, Straight(300f, inside: 1));
		Assert.Equal(LineState.DefendRight, line.State);
		float first = line.Offset;
		// Sigue cubriendo el mismo lado, sin cambiar aunque el que le sigue se vaya al otro
		for (int i = 0; i < 60; i++)
			line.Update(Ship(0, At, 0f, Speed), new[] { chaser with { Lateral = -6f } }, Step, Straight(300f, inside: 1));
		Assert.Equal(LineState.DefendRight, line.State);
		Assert.True(line.Offset > first);

		// Con defensa 0 decide no cubrir, y no lo vuelve a decidir en esa recta
		ClearLine calm = Duel(defense: 0f);
		for (int i = 0; i < 10; i++)
			calm.Update(Ship(0, At, 0f, Speed), new[] { chaser }, Step, Straight(300f - i, inside: 1));
		Assert.Equal(LineState.Clear, calm.State);

		// Una nave por el interior con el morro a un 30 % de su largo: no se mueve hacia ella…
		float self = At;
		var inside = Ship(2, self - Front - Back + 0.3f * Length, 5f, Speed);
		ClearLine blocked = Duel(defense: 1f);
		blocked.Update(Ship(0, self, 0f, Speed), new[] { chaser, inside }, Step, Straight(300f, inside: 1));
		Assert.Equal(LineState.DefendRight, blocked.State);
		float stay = blocked.Offset;
		for (int i = 0; i < 30; i++)
			blocked.Update(Ship(0, self, 0f, Speed), new[] { chaser, inside }, Step, Straight(300f, inside: 1));
		Assert.Equal(stay, blocked.Offset, 3);

		// …salvo con contacto ≥ 0,7 y menos de la mitad: cierra la puerta tarde
		ClearLine late = Duel(defense: 1f, contact: 0.8f);
		late.Update(Ship(0, self, 0f, Speed), new[] { chaser, inside }, Step, Straight(300f, inside: 1));
		float start = late.Offset;
		for (int i = 0; i < 30; i++)
			late.Update(Ship(0, self, 0f, Speed), new[] { chaser, inside }, Step, Straight(300f, inside: 1));
		Assert.True(late.Offset > start + 0.5f);
	}

	// 15
	[Fact]
	public void It_never_moves_sideways_if_the_closing_speed_would_reach_30_kmh()
	{
		var chaser = Ship(1, At - Length - 8f, 0f, Speed + 3f);
		// Una nave por el interior con un 10 % en paralelo, viniendo hacia él
		var inside = Ship(2, At - Front - Back + 0.1f * Length, 5f, Speed, lateralSpeed: -6f);
		ClearLine line = Duel(defense: 1f, contact: 0.8f);
		line.Update(Ship(0, At, 0f, Speed), new[] { chaser, inside }, Step, Straight(300f, inside: 1));
		float stay = line.Offset;
		for (int i = 0; i < 30; i++)
			line.Update(Ship(0, At, 0f, Speed), new[] { chaser, inside }, Step, Straight(300f, inside: 1));
		Assert.Equal(stay, line.Offset, 3);

		// Si no viene hacia él, el cierre es el suyo (4 m/s) y sí se mueve
		var still = inside with { LateralSpeed = 0f };
		ClearLine free = Duel(defense: 1f, contact: 0.8f);
		free.Update(Ship(0, At, 0f, Speed), new[] { chaser, still }, Step, Straight(300f, inside: 1));
		float start = free.Offset;
		for (int i = 0; i < 30; i++)
			free.Update(Ship(0, At, 0f, Speed), new[] { chaser, still }, Step, Straight(300f, inside: 1));
		Assert.True(free.Offset > start + 0.5f);
	}

	// 16
	[Fact]
	public void Each_boost_strategy_spends_in_its_case_and_with_3_charges_it_always_spends_one_with_a_clear_line()
	{
		var none = new BoostSituation(true, 1, 3, true, true, false, float.PositiveInfinity, float.PositiveInfinity, false, false);

		// ADELANTAR: atacando, o con la trazada clara y alguien a menos de 40 m delante
		Assert.True(RivalBoost.Wants(BoostStrategy.Overtake, none with { Clear = false, Attacking = true }));
		Assert.True(RivalBoost.Wants(BoostStrategy.Overtake, none with { AheadGap = 30f }));
		Assert.False(RivalBoost.Wants(BoostStrategy.Overtake, none with { AheadGap = 50f }));
		// DEFENDER: alguien detrás a menos de 15 m
		Assert.True(RivalBoost.Wants(BoostStrategy.Defend, none with { BehindGap = 10f }));
		Assert.False(RivalBoost.Wants(BoostStrategy.Defend, none with { BehindGap = 20f }));
		// ÚLTIMA VUELTA: solo en la última, una por recta
		Assert.False(RivalBoost.Wants(BoostStrategy.LastLap, none));
		Assert.True(RivalBoost.Wants(BoostStrategy.LastLap, none with { LastLap = true }));
		Assert.False(RivalBoost.Wants(BoostStrategy.LastLap, none with { LastLap = true, UsedThisStraight = true }));

		// El mejor momento: a la salida de una curva hacia una recta larga, con la trazada clara. ADELANTAR y DEFENDER con más
		// de una carga; ÚLTIMA VUELTA, solo en la última
		BoostSituation exit = none with { CurveExit = true, Charges = 2 };
		Assert.True(RivalBoost.Wants(BoostStrategy.Overtake, exit));
		Assert.True(RivalBoost.Wants(BoostStrategy.Defend, exit));
		Assert.False(RivalBoost.Wants(BoostStrategy.Overtake, exit with { Charges = 1 }));
		Assert.False(RivalBoost.Wants(BoostStrategy.LastLap, exit));
		Assert.False(RivalBoost.Wants(BoostStrategy.Overtake, exit with { UsedThisStraight = true }));

		// Con todas las cargas y la trazada clara, sea cual sea la estrategia
		foreach (BoostStrategy strategy in new[] { BoostStrategy.Overtake, BoostStrategy.Defend, BoostStrategy.LastLap })
			Assert.True(RivalBoost.Wants(strategy, none with { Charges = 3 }));

		// Nunca sin recta, sin poder activarla ni con la trazada tapada sin atacar
		Assert.False(RivalBoost.Wants(BoostStrategy.Overtake, none with { Charges = 3, StraightAhead = false }));
		Assert.False(RivalBoost.Wants(BoostStrategy.Overtake, none with { Charges = 3, Available = false }));
		Assert.False(RivalBoost.Wants(BoostStrategy.Defend, none with { Charges = 3, Clear = false, BehindGap = 5f }));
	}

	// El piloto sigue pisando W con el impulso del boost aunque pase de la punta
	[Fact]
	public void With_the_boost_impulse_the_pilot_keeps_the_throttle_above_top_speed()
	{
		(TrackFrame frame, float barrier, PilotData pilot) = Oval.Value;
		var driver = new RivalDriver(pilot, frame, Vector.Technical, Physics, Step, PilotTraits.Autopilot, 0f, barrier);
		float d = 0f;
		for (; d < frame.LapLength; d += 5f)
		{
			TrackAhead a = driver.AheadAt(d);
			if (a.OnStraight && a.ToBraking > 300f)
				break;
		}
		FramePoint p = frame.At(d);
		float top = Vector.Technical.MaxSpeed;
		var forward = new System.Numerics.Vector2(p.Tangent.X, p.Tangent.Z);
		var state = new PilotState(new System.Numerics.Vector2(p.Position.X, p.Position.Z), forward * (top + 5f),
			MathF.Atan2(-p.Tangent.X, -p.Tangent.Z), 0f, top + 5f, d, 0f);
		var self = new TrafficShip(0, d, 0f, top + 5f, HalfWidth, Front, Back, false);

		RivalCommand without = driver.Decide(RivalPhase.Running, state, false, self, Array.Empty<TrafficShip>(), new RivalStatus(0, 3, false, false));
		Assert.False(without.Input.Accelerate);
		RivalCommand with = driver.Decide(RivalPhase.Running, state, false, self, Array.Empty<TrafficShip>(), new RivalStatus(0, 3, true, false));
		Assert.True(with.Input.Accelerate);
		Assert.False(with.Input.Brake);
	}
}
