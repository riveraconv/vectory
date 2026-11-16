using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Circuit;
using Vectory.Rivals;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Alineación de los rivales (spec docs/rivales-1-parrilla.md, 7, pruebas 1–4)
public class RivalLineupTests
{
	private const int Participants = 8;

	private static (int[] Grid, IReadOnlyList<RivalEntry> Rivals) Lineup(int seed)
	{
		var rng = new Random(seed);
		int[] grid = GridLayout.RandomOrder(Participants, rng);
		return (grid, RivalLineup.Build(grid, rng));
	}

	[Fact]
	public void With_8_participants_there_are_7_rivals_on_the_other_marks()
	{
		(int[] grid, IReadOnlyList<RivalEntry> rivals) = Lineup(42);

		Assert.Equal(7, rivals.Count);
		int[] all = rivals.Select(r => r.GridPosition).Append(grid[0]).OrderBy(p => p).ToArray();
		Assert.Equal(Enumerable.Range(1, Participants), all);
		Assert.DoesNotContain(grid[0], rivals.Select(r => r.GridPosition));
		Assert.Equal(Enumerable.Range(1, 7).Select(i => $"rival_{i}"), rivals.Select(r => r.Id));
		Assert.Equal(Enumerable.Range(1, 7), rivals.Select(r => r.Number));
	}

	[Fact]
	public void The_same_seed_gives_the_same_lineup()
	{
		(int[] gridA, IReadOnlyList<RivalEntry> a) = Lineup(1234);
		(int[] gridB, IReadOnlyList<RivalEntry> b) = Lineup(1234);

		Assert.Equal(gridA, gridB);
		Assert.Equal(a, b);
	}

	[Fact]
	public void Ships_are_the_three_and_paints_belong_to_their_ship()
	{
		for (int seed = 0; seed < 200; seed++)
		{
			foreach (RivalEntry rival in Lineup(seed).Rivals)
			{
				Assert.Contains(rival.ShipDesignId, new[] { ShipCatalog.Ship01, ShipCatalog.Ship02, ShipCatalog.Ship03 });
				HullStyle style = Assert.NotNull(HullPaints.StyleOf(rival.ShipDesignId));
				Assert.InRange(rival.PaintIndex, 0, HullPaints.Count(style) - 1);
				Assert.Equal(rival.PaintIndex, HullPaints.IndexOf(style, rival.Paint));
			}
		}
	}

	[Fact]
	public void Over_many_seeds_all_ships_and_both_finishes_appear()
	{
		var rivals = Enumerable.Range(0, 100).SelectMany(seed => Lineup(seed).Rivals).ToList();

		Assert.Equal(3, rivals.Select(r => r.ShipDesignId).Distinct().Count());
		Assert.Contains(rivals, r => r.Paint.Marks);
		Assert.Contains(rivals, r => !r.Paint.Marks);
		Assert.Equal(3, RivalLineup.Ships.Count);
	}
}
