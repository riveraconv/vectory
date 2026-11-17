using System;
using System.Linq;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Rivals;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Perfiles de piloto y ritmo (spec docs/rivales-4-personalidades.md, 10, pruebas 1–6)
public class PilotProfileTests
{
	private const float Step = 1f / 60f;
	private static readonly ShipPhysicsSettings Physics = ShipPhysicsSettings.Default;

	private static readonly Lazy<(TrackFrame Frame, PilotData Data)> Oval = new(() =>
	{
		TrackFile file = TrackFile.Load("test_oval");
		TrackFrame frame = file.Frame();
		var cache = new PilotCache(frame, file.TrackWidth, Physics, AutopilotSettings.Default);
		return (frame, cache.Get(Vector));
	});

	private static ShipDefinition Vector => ShipDefinitions.Find(ShipCatalog.Ship01);

	private static SpeedProfile Profile(float curve, float straight, float braking)
	{
		(TrackFrame frame, PilotData data) = Oval.Value;
		return SpeedProfile.Build(data.Line, frame, Vector.Technical, data.Table, Physics, curve, straight, braking);
	}

	// 1
	[Fact]
	public void The_catalog_has_20_pilots_each_with_the_profile_of_the_table()
	{
		Assert.Equal(20, PilotCatalog.All.Count);
		var expected = new (PilotProfile Profile, string[] Names)[]
		{
			(PilotProfile.Balanced, new[] { "CINDER", "JACKAL", "ONYX", "TITAN" }),
			(PilotProfile.Sprinter, new[] { "RAZOR", "BLITZ", "SPARK", "VIPER" }),
			(PilotProfile.CornerSpecialist, new[] { "EDGE", "KESTREL", "RIPTIDE" }),
			(PilotProfile.Aggressive, new[] { "BUTCHER", "VANDAL", "FURY" }),
			(PilotProfile.Conservative, new[] { "ROOK", "NOMAD", "ORBIT" }),
			(PilotProfile.Technical, new[] { "HEX", "SHADE", "WRAITH" })
		};
		foreach ((PilotProfile profile, string[] names) in expected)
		{
			string[] actual = PilotCatalog.All.Where(p => p.Profile == profile).Select(p => p.Nickname).OrderBy(n => n).ToArray();
			Assert.Equal(names.OrderBy(n => n).ToArray(), actual);
		}
		Assert.Equal(6, PilotProfiles.All.Count);
	}

	// 2
	[Fact]
	public void Each_pilot_has_a_fixed_variation_within_the_limits()
	{
		foreach (RivalPilot pilot in PilotCatalog.All)
			Assert.True(pilot.Variation.IsWithinLimits, pilot.Nickname);

		// Siempre la misma: está guardada en el catálogo, no se sortea
		RivalPilot razor = PilotCatalog.All.Single(p => p.Nickname == "RAZOR");
		Assert.Equal(new PilotVariation(-0.007f, 0.008f, -0.006f, -0.022f, 0.05f, 0.09f, -0.06f, 0.08f), razor.Variation);
		Assert.Equal(razor.Traits, PilotCatalog.All.Single(p => p.Id == "pilot_01").Traits);
		Assert.False(new PilotVariation(0.011f, 0f, 0f, 0f, 0f, 0f, 0f, 0f).IsWithinLimits);
	}

	// 3
	[Fact]
	public void Effective_traits_are_profile_plus_variation_within_their_ranges()
	{
		PilotTraits sprinter = PilotProfiles.Traits(PilotProfile.Sprinter);
		var variation = new PilotVariation(0.01f, -0.01f, 0.02f, -0.03f, 0.1f, -0.1f, 0.1f, -0.1f);
		PilotTraits t = PilotTraits.Effective(sprinter, variation);
		Assert.Equal(1.00f, t.StraightMargin, 5);   // 1,00 + 0,01, con tope en la punta
		Assert.Equal(0.95f, t.CurveMargin, 5);
		Assert.Equal(0.94f, t.Braking, 5);
		Assert.Equal(0.17f, t.Reaction, 5);
		Assert.Equal(0.7f, t.Attack, 5);
		Assert.Equal(0.2f, t.Defense, 5);
		Assert.Equal(0.4f, t.Contact, 5);
		Assert.Equal(0.9f, t.Errors, 5);
		Assert.Equal(BoostStrategy.Overtake, t.Boost);

		// Limitados: recta ≤ 100 %, curva ≤ 105 %, frenada ≤ 1, reacción ≥ 0,15 s, defensa ≥ 0
		var extreme = new PilotTraits(0.99f, 1.05f, 0.99f, 0.15f, 0.95f, 0.05f, 0.05f, BoostStrategy.Defend, 0.35f);
		PilotTraits clamped = PilotTraits.Effective(extreme, variation with { CurveMargin = 0.01f });
		Assert.Equal(1f, clamped.StraightMargin, 5);
		Assert.Equal(1.05f, clamped.CurveMargin, 5);
		Assert.Equal(1f, clamped.Braking, 5);
		Assert.Equal(PilotTraits.MinReaction, clamped.Reaction, 5);
		Assert.Equal(1f, clamped.Attack, 5);
		Assert.Equal(0f, clamped.Defense, 5);
		Assert.Equal(PilotTraits.MinErrors, clamped.Errors, 5);

		// Todos los pilotos, dentro de los rangos
		foreach (RivalPilot pilot in PilotCatalog.All)
			Assert.Equal(pilot.Traits, pilot.Traits.Clamped());

		// El ajuste de depuración se suma a los márgenes sin pasar de sus topes
		Assert.Equal(1f, t.StraightWith(0.02f), 5);
		Assert.Equal(0.93f, t.CurveWith(-0.02f), 5);
		Assert.Equal(1.05f, t.CurveWith(0.2f), 5);
	}

	// 4
	[Fact]
	public void The_straight_margin_caps_the_top_speed_and_the_curve_margin_the_curve_speed_without_jumps()
	{
		(TrackFrame frame, PilotData data) = Oval.Value;
		SpeedProfile profile = Profile(0.99f, 0.95f, 1f);
		float top = Vector.Technical.MaxSpeed;

		int straight = 0;
		int curve = 0;
		for (int i = 0; i < profile.CurveLimit.Length; i++)
		{
			float radius = 1f / MathF.Max(MathF.Abs(data.Line.Curvature[i]), 1e-4f);
			float inCurve = data.Table.SpeedFor(radius);
			float expected = inCurve >= top ? 0.95f * top : MathF.Min(0.95f * top, 0.99f * inCurve);
			Assert.Equal(expected, profile.CurveLimit[i], 3);
			if (radius >= 600f)
			{
				straight++;
				Assert.Equal(0.95f * top, profile.CurveLimit[i], 3);
			}
			else if (0.99f * inCurve < 0.95f * top)
			{
				curve++;
			}
		}
		Assert.True(straight > 100 && curve > 100);

		// Sin saltos: de un punto al siguiente, como mucho lo que da la aceleración o la frenada de la nave
		float ds = data.Line.PointSpacing;
		float maxJump = MathF.Sqrt(2f * MathF.Max(Vector.Technical.Acceleration, Vector.Technical.BrakeDeceleration) * ds) + 0.01f;
		for (int i = 0; i < profile.Target.Length; i++)
		{
			float next = profile.Target[data.Line.Wrap(i + 1)];
			Assert.True(MathF.Abs(next - profile.Target[i]) <= maxJump, $"jump at {i}");
		}

		// Con recta 1 y frenada 1 es el perfil de la spec 6
		SpeedProfile old = SpeedProfile.Build(data.Line, frame, Vector.Technical, data.Table, Physics, 0.97f);
		Assert.Equal(old.Target, Profile(0.97f, 1f, 1f).Target);
	}

	// 5
	[Fact]
	public void With_less_braking_it_starts_braking_earlier()
	{
		// En circuit_01, con frenadas fuertes (en el óvalo apenas frena)
		TrackFile file = TrackFile.Load("circuit_01");
		TrackFrame frame = file.Frame();
		PilotData data = new PilotCache(frame, file.TrackWidth, Physics, AutopilotSettings.Default).Get(Vector);
		SpeedProfile full = SpeedProfile.Build(data.Line, frame, Vector.Technical, data.Table, Physics, 0.97f, 1f, 1f);
		SpeedProfile early = SpeedProfile.Build(data.Line, frame, Vector.Technical, data.Table, Physics, 0.97f, 1f, 0.9f);
		Assert.Equal(full.Braking * 0.9f, early.Braking, 4);

		// Desde el punto más rápido, hacia delante, el primero en el que el objetivo ya baja de la punta
		int fastest = Array.IndexOf(full.Target, full.Target.Max());
		float top = full.Target[fastest];
		int BrakingStart(SpeedProfile p)
		{
			for (int k = 0; k < p.Target.Length; k++)
			{
				if (p.Target[data.Line.Wrap(fastest + k)] < top - 0.01f)
					return k;
			}
			return -1;
		}
		int fullStart = BrakingStart(full);
		int earlyStart = BrakingStart(early);
		Assert.True(fullStart > 0 && earlyStart > 0);
		Assert.True(earlyStart < fullStart, $"{earlyStart} vs {fullStart}");
		for (int i = 0; i < full.Target.Length; i++)
			Assert.True(early.Target[i] <= full.Target[i] + 1e-4f);
	}

	// 6
	[Fact]
	public void No_input_until_the_reaction_time_after_the_go()
	{
		(TrackFrame frame, PilotData data) = Oval.Value;
		PilotTraits traits = PilotTraits.Autopilot with { Reaction = 0.25f };
		var driver = new RivalDriver(data, frame, Vector.Technical, Physics, Step, traits);
		Assert.Equal(0.25f, driver.ReactionTime);

		FramePoint p = frame.At(10f);
		var state = new PilotState(new Vector2(p.Position.X, p.Position.Z), Vector2.Zero, MathF.Atan2(-p.Tangent.X, -p.Tangent.Z), 0f, 0f, 10f, 0f);
		int steps = 0;
		while (!driver.Decide(RivalPhase.Running, state, true).Input.Accelerate)
		{
			steps++;
			Assert.True(steps < 100);
		}
		Assert.Equal(15, steps);   // 0,25 s a 60 Hz

		// Con la reacción de su piloto y lo de esta salida
		var entry = new RivalEntry("rival_1", 1, 2, ShipCatalog.Ship01, 0, HullPaints.ChoiceAt(HullStyle.Racing, 0),
			PilotCatalog.All[0], 0.04f);
		Assert.Equal(PilotCatalog.All[0].Traits.Reaction + 0.04f, entry.Reaction, 5);
		Assert.Equal(0f, (entry with { ReactionJitter = -1f }).Reaction);
	}
}
