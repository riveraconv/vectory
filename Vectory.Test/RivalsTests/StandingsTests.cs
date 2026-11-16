using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Circuit;
using Vectory.Rivals;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Pilotos y clasificación en directo (spec docs/rivales-2-clasificacion.md, 8, pruebas 1–9)
public class StandingsTests
{
	private const float Lap = 1000f;
	private static readonly float[] Checkpoints = { 250f, 500f, 750f };

	// --- 1 y 2: pilotos y alineación ---

	[Fact]
	public void The_catalog_has_20_pilots_with_unique_ids_and_nicknames()
	{
		IReadOnlyList<RivalPilot> all = PilotCatalog.All;

		Assert.Equal(20, all.Count);
		Assert.Equal(20, all.Select(p => p.Id).Distinct().Count());
		Assert.Equal(20, all.Select(p => p.Nickname).Distinct().Count());
		Assert.Equal(Enumerable.Range(1, 20).Select(i => $"pilot_{i:00}"), all.Select(p => p.Id));
		Assert.Equal("RAZOR", all[0].Nickname);
		Assert.Equal("ROOK", all[19].Nickname);
		Assert.All(all, p => Assert.Equal(p.Nickname.ToUpperInvariant(), p.Nickname));
	}

	[Fact]
	public void The_lineup_has_7_different_pilots_and_the_same_seed_gives_the_same_ones()
	{
		for (int seed = 0; seed < 100; seed++)
		{
			IReadOnlyList<RivalEntry> a = Lineup(seed);
			IReadOnlyList<RivalEntry> b = Lineup(seed);

			Assert.Equal(7, a.Select(r => r.Pilot).Distinct().Count());
			Assert.All(a, r => Assert.Contains(r.Pilot, PilotCatalog.All));
			Assert.Equal(a.Select(r => r.Pilot), b.Select(r => r.Pilot));
		}
		// Con semillas distintas cambian
		Assert.True(Enumerable.Range(0, 20).Select(s => string.Join(",", Lineup(s).Select(r => r.Pilot.Id))).Distinct().Count() > 1);
	}

	[Fact]
	public void Pilots_do_not_change_ships_or_paints()
	{
		// Los pilotos se sortean después: naves y pinturas salen de los mismos números del generador que sin ellos
		var rng = new Random(77);
		int[] grid = GridLayout.RandomOrder(8, rng);
		IReadOnlyList<RivalEntry> rivals = RivalLineup.Build(grid, rng);

		var check = new Random(77);
		GridLayout.RandomOrder(8, check);
		foreach (RivalEntry rival in rivals)
		{
			string ship = RivalLineup.Ships[check.Next(RivalLineup.Ships.Count)];
			int paint = check.Next(Vectory.Ship.HullPaints.Count(Assert.NotNull(Vectory.Ship.HullPaints.StyleOf(ship))));
			Assert.Equal(ship, rival.ShipDesignId);
			Assert.Equal(paint, rival.PaintIndex);
		}
	}

	private static IReadOnlyList<RivalEntry> Lineup(int seed)
	{
		var rng = new Random(seed);
		int[] grid = GridLayout.RandomOrder(8, rng);
		return RivalLineup.Build(grid, rng);
	}

	// --- 3 a 6: clasificación en directo ---

	private static StandingsShip PreStart(int id, int grid, float s) => new(id, grid, 0, 0, s, false, false, 0);

	private static StandingsShip Racing(int id, int grid, int lap, int next, float s, double time = 10) =>
		new(id, grid, lap, next, s, true, false, time);

	private static RaceStandings Standings() => new(Checkpoints, Lap);

	[Fact]
	public void On_the_grid_the_order_is_the_grid_order()
	{
		RaceStandings standings = Standings();
		// Detrás de la meta: la marca 1 es la más cercana a ella
		standings.Update(new[] { PreStart(0, 3, 970f), PreStart(1, 1, 990f), PreStart(2, 4, 962f), PreStart(3, 2, 984f) });

		Assert.Equal(new[] { 1, 3, 0, 2 }, standings.Order);
		Assert.True(standings.DistanceOf(1) < 0);
		Assert.Equal(-10, standings.DistanceOf(1), 3);
		Assert.Equal(3, standings.PositionOf(0));
	}

	[Fact]
	public void An_exact_tie_goes_to_the_ship_that_started_further_ahead()
	{
		RaceStandings standings = Standings();
		standings.Update(new[] { Racing(0, 5, 1, 1, 300f), Racing(1, 2, 1, 1, 300f) });

		Assert.Equal(new[] { 1, 0 }, standings.Order);
	}

	[Fact]
	public void More_race_distance_goes_ahead_and_more_laps_beat_a_larger_s()
	{
		RaceStandings standings = Standings();
		standings.Update(new[]
		{
			Racing(0, 1, 1, 1, 300f),
			Racing(1, 2, 1, 1, 320f),
			Racing(2, 3, 2, 0, 100f)   // una vuelta más, con s menor
		});

		Assert.Equal(new[] { 2, 1, 0 }, standings.Order);
		Assert.Equal(1100, standings.DistanceOf(2), 3);
	}

	[Fact]
	public void Going_backwards_or_skipping_a_checkpoint_does_not_pass_a_ship_with_more_checkpoints()
	{
		RaceStandings standings = Standings();
		standings.Update(new[]
		{
			Racing(0, 1, 1, 2, 520f),   // ha pasado los checkpoints 0 y 1
			Racing(1, 2, 1, 1, 700f)    // se ha saltado el 1: no cuenta más allá de él
		});
		Assert.Equal(new[] { 0, 1 }, standings.Order);
		Assert.Equal(500, standings.DistanceOf(1), 3);

		// Hacia atrás por detrás del checkpoint 1: se queda en él, no pierde la vuelta
		standings.Update(new[] { Racing(0, 1, 1, 2, 480f), Racing(1, 2, 1, 1, 499f) });
		Assert.Equal(new[] { 0, 1 }, standings.Order);
		Assert.Equal(500, standings.DistanceOf(0), 3);

		// Hacia atrás cruzando la meta al empezar una vuelta: negativo, no una vuelta entera de más
		Assert.Equal(1000 - 20, RaceDistance.Compute(2, 0, 980f, Checkpoints, Lap), 3);
	}

	[Fact]
	public void Finished_ships_go_first_in_arrival_order()
	{
		RaceStandings standings = Standings();
		standings.Update(new[] { Racing(0, 1, 3, 3, 990f, 100), Racing(1, 2, 3, 3, 995f, 100), Racing(2, 3, 3, 3, 900f, 100) });

		// Llega la 2 primero aunque iba detrás (por ejemplo, la otra se ha parado); luego la 0
		standings.Update(new[] { Racing(0, 1, 3, 3, 995f, 101), Racing(1, 2, 3, 3, 996f, 101), Finished(2, 3, 101) });
		standings.Update(new[] { Finished(0, 1, 102), Racing(1, 2, 3, 3, 997f, 102), Finished(2, 3, 101) });

		Assert.Equal(new[] { 2, 0, 1 }, standings.Order);
		Assert.True(standings.IsFinished(2));
		Assert.False(standings.IsFinished(1));
	}

	[Fact]
	public void Ships_arriving_together_are_ordered_by_time_then_grid()
	{
		RaceStandings standings = Standings();
		standings.Update(new[] { Finished(0, 4, 100.5), Finished(1, 2, 100.2), Finished(2, 1, 100.5) });

		Assert.Equal(new[] { 1, 2, 0 }, standings.Order);
	}

	private static StandingsShip Finished(int id, int grid, double time) => new(id, grid, 3, 3, 2f, false, true, time);

	// --- 7 y 8: intervalo y formato ---

	[Fact]
	public void A_rival_ahead_shows_how_long_ago_it_passed_your_point_and_one_behind_how_long_ago_you_passed_its()
	{
		var rival = new TimeMarks();
		rival.Record(0, 0);
		rival.Record(100, 10);   // 10 m/s
		var player = new TimeMarks();
		player.Record(0, 0);
		player.Record(91.6, 10);

		// El rival pasó por 91,6 m a los 9,16 s; tú estás ahí a los 10 s
		RaceInterval ahead = RaceInterval.Between(91.6, 10, player, 100, 10, rival, Lap);
		Assert.Equal(RaceIntervalKind.Time, ahead.Kind);
		Assert.Equal(0.84, ahead.Seconds, 6);
		Assert.Equal("▲ 0.84", StandingsFormat.Interval(ahead));

		// Tú pasaste por 88 m a los 8,8 s... con tus marcas a 10 m/s
		var me = new TimeMarks();
		me.Record(0, 0);
		me.Record(100, 10);
		var other = new TimeMarks();
		other.Record(0, 0);
		other.Record(88, 10);
		RaceInterval behind = RaceInterval.Between(100, 10, me, 88, 10, other, Lap);
		Assert.Equal(-1.2, behind.Seconds, 6);
		Assert.Equal("▼ 1.20", StandingsFormat.Interval(behind));
	}

	[Fact]
	public void Laps_of_difference_and_no_data_have_their_own_text()
	{
		var marks = new TimeMarks();
		marks.Record(0, 0);

		Assert.Equal("+1 LAP", StandingsFormat.Interval(RaceInterval.Between(1500, 0, marks, 400, 0, marks, Lap)));
		Assert.Equal("+2 LAPS", StandingsFormat.Interval(RaceInterval.Between(2500, 0, marks, 400, 0, marks, Lap)));
		Assert.Equal("−1 LAP", StandingsFormat.Interval(RaceInterval.Between(400, 0, marks, 1500, 0, marks, Lap)));
		Assert.Equal("—", StandingsFormat.Interval(RaceInterval.None));

		// Antes de que el rival haya pasado por tu punto (o tú por el suyo): sin dato
		var empty = new TimeMarks();
		Assert.Equal(RaceIntervalKind.None, RaceInterval.Between(-20, 0, empty, -10, 0, empty, Lap).Kind);
		var rival = new TimeMarks();
		rival.Record(-10, 0);
		Assert.Equal(RaceIntervalKind.None, RaceInterval.Between(-20, 0, empty, -10, 0, rival, Lap).Kind);
	}

	[Fact]
	public void From_a_minute_the_interval_shows_minutes()
	{
		Assert.Equal("1:02.31", StandingsFormat.Seconds(62.31));
		Assert.Equal("12.30", StandingsFormat.Seconds(12.3));
		Assert.Equal("0.00", StandingsFormat.Seconds(0));
		Assert.Equal("1:00.00", StandingsFormat.Seconds(59.999));
		Assert.Equal("▼ 1:02.31", StandingsFormat.Interval(RaceInterval.Time(-62.31)));
	}

	[Fact]
	public void Time_marks_every_10_m_interpolate_and_ignore_going_backwards()
	{
		var marks = new TimeMarks();
		marks.Record(-15, 0);
		marks.Record(25, 4);     // marcas en −10, 0, 10 y 20
		Assert.Equal(4, marks.Count);
		Assert.Equal(0.5, Assert.NotNull(marks.TimeAt(-10)), 6);
		Assert.Equal(2.5, Assert.NotNull(marks.TimeAt(10)), 6);
		Assert.Equal(2.75, Assert.NotNull(marks.TimeAt(12.5)), 6);

		marks.Record(5, 6);      // hacia atrás: nada nuevo
		Assert.Equal(4, marks.Count);
		Assert.Equal(2.5, Assert.NotNull(marks.TimeAt(10)), 6);

		marks.Record(35, 7);     // vuelve a avanzar desde 25 m a los 6 s
		Assert.Equal(5, marks.Count);
		Assert.Equal(6.5, Assert.NotNull(marks.TimeAt(30)), 6);
		Assert.Null(marks.TimeAt(36));
		Assert.Null(marks.TimeAt(-16));
	}

	[Fact]
	public void A_rival_keeps_its_interval_from_its_arrival_and_all_freeze_when_you_arrive()
	{
		RaceStandings standings = Standings();
		// El 1 va delante; los dos a 10 m/s, él 10 m por delante
		for (int i = 0; i <= 20; i++)
		{
			double t = i * 0.5;
			standings.Update(new[] { Racing(0, 2, 3, 3, 800f + (float)(t * 10), t), Racing(1, 1, 3, 3, 810f + (float)(t * 10), t) });
		}
		Assert.Equal(1.0, standings.IntervalOf(1).Seconds, 3);

		// Llega el 1 (en 1000 m de su tercera vuelta): su intervalo se queda el de ese momento
		standings.Update(new[] { Racing(0, 2, 3, 3, 990f, 19), Finished(1, 1, 19) });
		double atArrival = standings.IntervalOf(1).Seconds;
		standings.Update(new[] { Racing(0, 2, 3, 3, 991f, 25) });
		Assert.Equal(atArrival, standings.IntervalOf(1).Seconds, 6);

		// Llegas tú: el del rival de detrás se queda fijo
		RaceStandings two = Standings();
		for (int i = 0; i <= 20; i++)
		{
			double t = i * 0.5;
			two.Update(new[] { Racing(0, 1, 3, 3, 900f + (float)(t * 10), t), Racing(2, 2, 3, 3, 880f + (float)(t * 10), t) });
		}
		two.Update(new[] { Finished(0, 1, 10), Racing(2, 2, 3, 3, 980f, 10) });
		double frozen = two.IntervalOf(2).Seconds;
		Assert.Equal(-2.0, frozen, 3);
		two.Update(new[] { Finished(0, 1, 10), Racing(2, 2, 3, 3, 999f, 40) });
		Assert.Equal(frozen, two.IntervalOf(2).Seconds, 6);
	}

	// --- 9: huecos del panel ---

	private static StandingsRow?[] Slots(int playerPosition)
	{
		// Ids 10, 11… salvo el jugador (0) en su puesto
		var order = new List<int>();
		int next = 10;
		for (int p = 1; p <= 8; p++)
			order.Add(p == playerPosition ? 0 : next++);
		return StandingsFormat.Slots(order, 0, id => id == 0 ? PilotCatalog.PlayerName : $"R{id}", id => "▲ 0.84");
	}

	[Fact]
	public void The_panel_shows_two_ahead_and_two_behind_with_you_in_the_middle()
	{
		StandingsRow?[] fourth = Slots(4);
		Assert.Equal(new[] { 2, 3, 4, 5, 6 }, fourth.Select(r => Assert.NotNull(r).Position));
		Assert.Equal(new[] { -2, -1, 0, 1, 2 }, fourth.Select(r => Assert.NotNull(r).Offset));
		Assert.True(Assert.NotNull(fourth[2]).IsPlayer);
		Assert.Equal("PLAYER", Assert.NotNull(fourth[2]).Name);
		Assert.Equal("", Assert.NotNull(fourth[2]).Interval);
		Assert.Equal("▲ 0.84", Assert.NotNull(fourth[1]).Interval);
	}

	[Fact]
	public void First_and_last_leave_their_empty_slots_empty()
	{
		StandingsRow?[] first = Slots(1);
		Assert.Null(first[0]);
		Assert.Null(first[1]);
		Assert.True(Assert.NotNull(first[2]).IsPlayer);
		Assert.Equal(1, Assert.NotNull(first[2]).Position);
		Assert.Equal(new[] { 2, 3 }, new[] { Assert.NotNull(first[3]).Position, Assert.NotNull(first[4]).Position });

		StandingsRow?[] last = Slots(8);
		Assert.Null(last[3]);
		Assert.Null(last[4]);
		Assert.True(Assert.NotNull(last[2]).IsPlayer);
		Assert.Equal(new[] { 6, 7, 8 }, new[] { Assert.NotNull(last[0]).Position, Assert.NotNull(last[1]).Position, Assert.NotNull(last[2]).Position });

		StandingsRow?[] second = Slots(2);
		Assert.Null(second[0]);
		Assert.Equal(1, Assert.NotNull(second[1]).Position);
	}
}
