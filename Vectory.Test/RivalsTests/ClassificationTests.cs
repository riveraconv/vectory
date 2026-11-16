using System.Collections.Generic;
using System.Linq;
using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Clasificación final (spec docs/rivales-2-clasificacion.md, 8, pruebas 10–13)
public class ClassificationTests
{
	private const float Lap = 1000f;
	private static readonly float[] Checkpoints = { 250f, 500f, 750f };

	private static RaceStandings Standings() => new(Checkpoints, Lap, 0, TimeMarks.DefaultStep, 3);

	private static StandingsShip Racing(int id, int grid, int lap, int next, float s, double time, double? lastLap = null,
		double? bestLap = null) =>
		new(id, grid, lap, next, s, true, false, time, lastLap, bestLap, id == 0 ? "PLAYER" : $"R{id}", "VECTOR");

	private static StandingsShip Done(int id, int grid, double time, double? bestLap = 40) =>
		new(id, grid, 3, 3, 2f, false, true, time, bestLap, bestLap, id == 0 ? "PLAYER" : $"R{id}", "VECTOR");

	// --- 10 ---

	[Fact]
	public void The_estimate_uses_the_last_lap_pace_and_keeps_the_track_order()
	{
		Assert.Equal(100 + 500 / (1000 / 40.0), RaceClassification.Estimate(100, 500, Lap, 40, 0, 0), 9);
		// Sin vuelta completa: media desde el GO
		Assert.Equal(30 + 700 / (300 / 30.0), RaceClassification.Estimate(30, 700, Lap, null, 300, 30), 9);
		// Parada del todo: sigue siendo un número
		Assert.True(double.IsFinite(RaceClassification.Estimate(30, 700, Lap, null, 0, 30)));

		RaceStandings standings = Standings();
		standings.Update(new[] { Racing(0, 1, 3, 3, 990f, 119), Racing(1, 2, 3, 2, 700f, 119, 40), Racing(2, 3, 3, 3, 800f, 119, 50) });
		standings.Update(new[] { Done(0, 1, 120), Racing(1, 2, 3, 2, 710f, 120, 40), Racing(2, 3, 3, 3, 810f, 120, 50) });

		RaceClassification final = standings.Final;
		Assert.NotNull(final);
		// Orden de pista en tu llegada: tú, el 2 (810 m) y el 1 (710 m), aunque la estimación del 1 sea menor
		Assert.Equal(new[] { 0, 2, 1 }, final.Rows.Select(r => r.Id));
		Assert.Equal(new[] { 0, 2, 1 }, standings.Order);
		Assert.False(final.Rows[0].IsEstimated);
		Assert.Equal(120, final.Rows[0].TotalTime, 9);
		ClassificationRow second = final.Rows[1];
		Assert.True(second.IsEstimated);
		Assert.Equal(120 + 190.0 / (1000 / 50.0), second.TotalTime, 6);   // le faltan 3000 − 2810 = 190 m, a 20 m/s
		Assert.All(final.Rows.Skip(1), r => Assert.True(r.TotalTime > final.Rows[0].TotalTime));
		Assert.Equal(1, final.PositionOf(0));
	}

	// --- 11 ---

	[Fact]
	public void An_estimate_lower_than_the_one_ahead_becomes_that_plus_one_millisecond()
	{
		var entries = new[]
		{
			new ClassificationEntry(0, "PLAYER", "VECTOR", true, 120, 39),
			new ClassificationEntry(1, "R1", "BLADE", false, 0, 40),
			new ClassificationEntry(2, "R2", "HAMMER", false, 0, 41)
		};
		var final = new RaceClassification(entries, new Dictionary<int, double> { [1] = 125, [2] = 124 });

		Assert.Equal(125, final.Rows[1].TotalTime, 9);
		Assert.Equal(125.001, final.Rows[2].TotalTime, 9);

		// Tampoco por delante de ti
		var below = new RaceClassification(entries, new Dictionary<int, double> { [1] = 119, [2] = 130 });
		Assert.Equal(120.001, below.Rows[1].TotalTime, 9);
	}

	// --- 12 ---

	[Fact]
	public void A_real_arrival_turns_the_row_real_and_rivals_behind_you_are_reordered_by_their_real_times()
	{
		RaceStandings standings = Standings();
		var moved = new List<int>();
		standings.FinalChanged += (_, id) => moved.Add(id);
		standings.Update(new[] { Done(0, 1, 120), Racing(1, 2, 3, 3, 900f, 120, 40), Racing(2, 3, 3, 3, 800f, 120, 40),
			Racing(3, 4, 3, 3, 700f, 120, 40) });
		Assert.Equal(new[] { 0, 1, 2, 3 }, standings.Final.Rows.Select(r => r.Id));
		Assert.Equal(3, standings.Final.EstimatedCount);

		// Llega el 2 antes que el 1 (que se ha quedado atascado): pasa delante de él, detrás de ti, y se avisa
		standings.Update(new[] { Done(0, 1, 120), Racing(1, 2, 3, 3, 905f, 124, 40), Done(2, 3, 124), Racing(3, 4, 3, 3, 960f, 124, 40) });
		Assert.Equal(new[] { 0, 2, 1, 3 }, standings.Final.Rows.Select(r => r.Id));
		Assert.Equal(new[] { 1, 2, 3, 4 }, standings.Final.Rows.Select(r => r.Position));
		Assert.False(standings.Final.Rows[1].IsEstimated);
		Assert.Equal(124, standings.Final.Rows[1].TotalTime, 9);
		Assert.True(standings.Final.Rows[2].IsEstimated);
		Assert.True(standings.Final.Rows[2].TotalTime > 124);   // las estimadas, detrás de las llegadas
		Assert.Equal(new[] { 0, 2, 1, 3 }, standings.Order);   // el panel del HUD, igual

		// Llegan el 3 y luego el 1: cada uno en su sitio por tiempo
		standings.Update(new[] { Done(0, 1, 120), Racing(1, 2, 3, 3, 910f, 125, 40), Done(2, 3, 124), Done(3, 4, 125) });
		standings.Update(new[] { Done(0, 1, 120), Done(1, 2, 126), Done(2, 3, 124), Done(3, 4, 125) });
		Assert.Equal(new[] { 0, 2, 3, 1 }, standings.Order);
		Assert.Equal(0, standings.Final.EstimatedCount);
		Assert.Equal(new double[] { 120, 124, 125, 126 }, standings.Final.Rows.Select(r => r.TotalTime));
		// Al fijarla nadie; el 2 sube del 3º al 2º; el 3 sube del 4º al 3º; el 1 llega 4º, donde ya estaba tras ellos
		Assert.Equal(new[] { -1, 2, 3, -1 }, moved);

		// Tu puesto y el de quien llegó antes que tú no se mueven nunca
		var before = new RaceClassification(new[]
		{
			new ClassificationEntry(1, "R1", "BLADE", true, 119, 39),
			new ClassificationEntry(0, "PLAYER", "VECTOR", true, 120, 39),
			new ClassificationEntry(2, "R2", "BLADE", false, 0, 40)
		}, new Dictionary<int, double> { [2] = 125 });
		Assert.Equal(3, before.Arrive(2, 118, 40));   // imposible en pista, pero ni así pasa delante
		Assert.Equal(new[] { 1, 0, 2 }, before.Rows.Select(r => r.Id));
		Assert.Equal(1, before.GapToLeader(2), 9);
		Assert.Equal(0, before.Arrive(2, 130, 40));   // ya llegada: nada
	}

	[Fact]
	public void Rivals_who_finished_before_you_are_real_and_ahead()
	{
		RaceStandings standings = Standings();
		standings.Update(new[] { Racing(0, 2, 3, 3, 980f, 118), Done(1, 1, 117), Racing(2, 3, 3, 3, 700f, 118, 40) });
		standings.Update(new[] { Done(0, 2, 120), Done(1, 1, 117), Racing(2, 3, 3, 3, 760f, 120, 40) });

		RaceClassification final = standings.Final;
		Assert.Equal(new[] { 1, 0, 2 }, final.Rows.Select(r => r.Id));
		Assert.False(final.Rows[0].IsEstimated);
		Assert.False(final.Rows[1].IsEstimated);
		Assert.True(final.Rows[2].IsEstimated);
		Assert.Equal(2, final.PositionOf(0));
	}

	// --- 13 ---

	[Fact]
	public void The_race_result_carries_the_whole_classification()
	{
		RaceStandings standings = Standings();
		var ships = new List<StandingsShip> { Racing(0, 4, 3, 3, 990f, 119, 40, 39.5) };
		for (int id = 1; id <= 7; id++)
			ships.Add(Racing(id, id < 4 ? id : id + 1, 3, 3, 1000f - id * 30f, 119, 40, 40 + id));
		standings.Update(ships);
		ships[0] = Done(0, 4, 120, 39.5);
		standings.Update(ships);

		var tracker = new RaceResult(1, 8, 120, 39.5, 2, new[] { new SectorBest(10, 1) }, "VECTOR");
		RaceResult result = tracker.WithClassification(standings.Final, 0);

		Assert.Equal(8, result.Classification.Count);
		Assert.Equal(8, result.ParticipantCount);
		Assert.Equal(standings.Final.PositionOf(0), result.Position);
		Assert.Equal(standings.PositionOf(0), result.Position);
		Assert.Equal(120, result.TotalTime, 9);
		Assert.Equal("VECTOR", result.ShipName);
		Assert.Equal(Enumerable.Range(1, 8), result.Classification.Rows.Select(r => r.Position));
		ClassificationRow player = result.Classification.Rows.Single(r => r.Id == 0);
		Assert.Equal("PLAYER", player.Name);
		Assert.Equal("VECTOR", player.ShipName);
		Assert.Equal(39.5, player.BestLapTime);
		Assert.False(player.IsEstimated);
		Assert.All(result.Classification.Rows.Where(r => r.Id != 0), r =>
		{
			Assert.True(r.IsEstimated);
			Assert.Equal($"R{r.Id}", r.Name);
			Assert.Equal(40.0 + r.Id, r.BestLapTime);
		});
		Assert.Equal(0, result.Classification.GapToLeader(1), 9);
		Assert.True(result.Classification.GapToLeader(8) > 0);

		// Sin clasificación, el resultado del tracker tal cual
		Assert.Same(tracker, tracker.WithClassification(null, 0));
	}
}
