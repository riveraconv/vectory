using System;
using System.Linq;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Game;
using Vectory.Rivals;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Dificultad de los rivales (spec docs/rivales-4-personalidades.md, 10, pruebas 20–22)
public class DifficultyTests
{
	private static readonly ShipPhysicsSettings Physics = ShipPhysicsSettings.Default;

	private static readonly Lazy<(TrackFrame Frame, TrackFile File, PilotCache Cache)> Oval = new(() =>
	{
		TrackFile file = TrackFile.Load("test_oval");
		TrackFrame frame = file.Frame();
		return (frame, file, new PilotCache(frame, file.TrackWidth, Physics, AutopilotSettings.Default));
	});

	private static RivalRaceContext Context(Difficulty difficulty)
	{
		(TrackFrame frame, TrackFile file, _) = Oval.Value;
		return new RivalRaceContext
		{
			Frame = frame,
			TrackWidth = file.TrackWidth,
			BarrierOffset = BarrierGeometry.Offset(file.TrackWidth, BarrierGeometry.DefaultGap),
			LapLength = frame.LapLength,
			CheckpointIndices = CenterlineBuilder.CheckpointIndices(frame.Plan, file.CheckpointCount),
			Difficulty = difficulty
		};
	}

	private static RivalCar Car(Difficulty difficulty, RivalPilot pilot, string shipId = ShipCatalog.Ship01)
	{
		ShipDefinition ship = ShipDefinitions.Find(shipId);
		var entry = new RivalEntry("rival_1", 1, 2, shipId, 0, HullPaints.ChoiceAt(HullPaints.StyleOf(shipId).Value, 0), pilot, 0.02f, 7);
		return new RivalCar(entry, ship, Oval.Value.Cache, Context(difficulty));
	}

	// 20
	[Fact]
	public void Easy_normal_and_hard_apply_the_table_with_its_limits()
	{
		var t = new PilotTraits(0.97f, 0.98f, 0.95f, 0.25f, 0.5f, 0.6f, 0.4f, BoostStrategy.Defend, 1f);
		Assert.Equal(t, DifficultyRules.Apply(t, Difficulty.Normal));

		// Recta −3 y curva −12 puntos en FÁCIL; +1 y +13 en DIFÍCIL (v1.1)
		PilotTraits easy = DifficultyRules.Apply(t, Difficulty.Easy);
		Assert.Equal(0.94f, easy.StraightMargin, 5);
		Assert.Equal(0.86f, easy.CurveMargin, 5);
		Assert.Equal(0.91f, easy.Braking, 5);
		Assert.Equal(0.40f, easy.Reaction, 5);
		Assert.Equal(0.30f, easy.Attack, 5);
		Assert.Equal(0.36f, easy.Defense, 5);
		Assert.Equal(0.24f, easy.Contact, 5);
		Assert.Equal(2f, easy.Errors, 5);
		Assert.Equal(BoostStrategy.Defend, easy.Boost);

		PilotTraits hard = DifficultyRules.Apply(t, Difficulty.Hard);
		Assert.Equal(0.98f, hard.StraightMargin, 5);
		Assert.Equal(1.11f, hard.CurveMargin, 5);
		Assert.Equal(0.97f, hard.Braking, 5);
		Assert.Equal(0.20f, hard.Reaction, 5);
		Assert.Equal(0.60f, hard.Attack, 5);
		Assert.Equal(0.72f, hard.Defense, 5);
		Assert.Equal(0.40f, hard.Contact, 5);
		Assert.Equal(0.4f, hard.Errors, 5);

		// Límites: recta 100 %, curva 130 %, frenada 1, ataque y defensa 1
		var top = new PilotTraits(0.995f, 1.20f, 0.99f, 0.02f, 0.9f, 0.95f, 1f, BoostStrategy.Overtake, 1f);
		PilotTraits capped = DifficultyRules.Apply(top, Difficulty.Hard);
		Assert.Equal(1f, capped.StraightMargin, 5);
		Assert.Equal(1.30f, capped.CurveMargin, 5);
		Assert.Equal(1f, capped.Braking, 5);
		Assert.Equal(0f, capped.Reaction, 5);
		Assert.Equal(1f, capped.Attack, 5);
		Assert.Equal(1f, capped.Defense, 5);
		Assert.Equal(1f, capped.Contact, 5);

		// Un conservador sigue siendo el que menos ataca, también en DIFÍCIL
		PilotTraits conservative = DifficultyRules.Apply(PilotProfiles.Traits(PilotProfile.Conservative), Difficulty.Hard);
		Assert.True(PilotProfiles.All.Where(p => p != PilotProfile.Conservative)
			.All(p => DifficultyRules.Apply(PilotProfiles.Traits(p), Difficulty.Hard).Attack > conservative.Attack));
	}

	// 21
	[Fact]
	public void The_ship_physics_is_the_same_in_the_three()
	{
		RivalPilot pilot = PilotCatalog.All[0];
		foreach (string shipId in new[] { ShipCatalog.Ship01, "ship_02", "ship_03" })
		{
			ShipTuning tuning = ShipDefinitions.Find(shipId).Technical;
			foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
			{
				RivalCar car = Car(difficulty, pilot, shipId);
				Assert.Equal(tuning, car.Ship.Technical);
			}
		}

		// Solo cambian los rasgos con los que conduce
		Assert.True(Car(Difficulty.Hard, pilot).Driver.CurveMargin > Car(Difficulty.Easy, pilot).Driver.CurveMargin);
		Assert.True(Car(Difficulty.Easy, pilot).Driver.Errors.Multiplier > Car(Difficulty.Hard, pilot).Driver.Errors.Multiplier);
		Assert.Equal(DifficultyRules.Apply(pilot.Traits, Difficulty.Easy).Reaction + 0.02f, Car(Difficulty.Easy, pilot).Driver.ReactionTime, 5);
	}

	// 22
	[Fact]
	public void The_difficulty_is_read_when_the_race_is_set_up()
	{
		Difficulty saved = GameSession.Difficulty;
		try
		{
			GameSession.Difficulty = Difficulty.Hard;
			RivalRaceContext context = Context(GameSession.Difficulty);
			var entry = new RivalEntry("rival_1", 1, 2, ShipCatalog.Ship01, 0, HullPaints.ChoiceAt(HullStyle.Racing, 0), PilotCatalog.All[4]);
			var car = new RivalCar(entry, ShipDefinitions.Find(ShipCatalog.Ship01), Oval.Value.Cache, context);
			PilotTraits before = car.Driver.Traits;

			// Cambiarla a mitad no cambia los rasgos en curso: vale para la carrera siguiente
			GameSession.Difficulty = Difficulty.Easy;
			Assert.Equal(before, car.Driver.Traits);
			Assert.Equal(DifficultyRules.Apply(PilotCatalog.All[4].Traits, Difficulty.Hard), car.Driver.Traits);
			Assert.Equal(Difficulty.Normal, DifficultyRules.Default);
		}
		finally
		{
			GameSession.Difficulty = saved;
		}
	}
}
