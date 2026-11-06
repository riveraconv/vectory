using System.Numerics;
using Vectory.Circuit;
using Vectory.Radar;
using Xunit;

namespace Vectory.Test.RadarTests;

public class RadarProjectionTests
{
	// Frente al norte del mundo (−Z), como una nave con yaw 0
	private static readonly Vector2 North = new(0f, -1f);
	private static readonly Vector2 East = new(1f, 0f);

	[Fact]
	public void A_ship_ahead_comes_out_on_positive_y()
	{
		RadarContact c = RadarProjection.Project(Vector3.Zero, North, new Vector3(0f, 0f, -50f));
		Assert.Equal(0f, c.X, 3);
		Assert.Equal(50f, c.Y, 3);
		Assert.Equal(50f, c.Distance, 3);
	}

	[Fact]
	public void A_ship_on_the_right_comes_out_on_positive_x()
	{
		RadarContact c = RadarProjection.Project(Vector3.Zero, North, new Vector3(20f, 0f, 0f));
		Assert.Equal(20f, c.X, 3);
		Assert.Equal(0f, c.Y, 3);
	}

	[Fact]
	public void A_ship_behind_comes_out_on_negative_y()
	{
		RadarContact c = RadarProjection.Project(Vector3.Zero, North, new Vector3(0f, 0f, 30f));
		Assert.Equal(-30f, c.Y, 3);
	}

	[Fact]
	public void The_axes_turn_with_the_ship()
	{
		// Girando 90° a la derecha, lo que estaba al norte queda a la izquierda
		RadarContact c = RadarProjection.Project(Vector3.Zero, East, new Vector3(0f, 0f, -50f));
		Assert.Equal(-50f, c.X, 3);
		Assert.Equal(0f, c.Y, 3);

		// y lo que está al este, delante
		c = RadarProjection.Project(Vector3.Zero, East, new Vector3(50f, 0f, 0f));
		Assert.Equal(0f, c.X, 3);
		Assert.Equal(50f, c.Y, 3);
	}

	[Theory]
	[InlineData(119f, 0f, true)]
	[InlineData(121f, 0f, false)]
	[InlineData(50f, 14f, true)]
	[InlineData(50f, 16f, false)]
	[InlineData(50f, -16f, false)]
	public void Range_and_height_decide_who_shows(float distance, float height, bool expected)
	{
		RadarContact c = RadarProjection.Project(Vector3.Zero, North, new Vector3(0f, height, -distance));
		Assert.Equal(expected, RadarProjection.IsVisible(in c, height, new RadarSettings()));
	}

	[Fact]
	public void The_height_does_not_change_the_position()
	{
		RadarContact flat = RadarProjection.Project(Vector3.Zero, North, new Vector3(10f, 0f, -40f));
		RadarContact high = RadarProjection.Project(Vector3.Zero, North, new Vector3(10f, 12f, -40f));
		Assert.Equal(flat, high);
	}
}

public class RadarSweepTests
{
	private static readonly Vector2 North = new(0f, -1f);

	private static RaceShip ShipAt(float x, float z, float y = 0f) => new(1, new Vector3(x, y, z), -Vector3.UnitZ, Vector3.UnitY);

	private static void Advance(RadarSweep sweep, float seconds, params RaceShip[] ships)
	{
		const float Step = 1f / 60f;
		int steps = (int)MathF.Round(seconds / Step);
		for (int i = 0; i < steps; i++)
			sweep.Update(Step, Vector3.Zero, North, ships);
	}

	[Fact]
	public void After_0_4_s_the_beam_points_right()
	{
		var sweep = new RadarSweep();
		sweep.Update(0.4f, Vector3.Zero, North, Array.Empty<RaceShip>());
		Assert.Equal(MathF.PI / 2f, sweep.Angle, 3);
	}

	[Fact]
	public void A_ship_ahead_is_only_marked_when_the_beam_comes_back_to_zero()
	{
		var sweep = new RadarSweep();
		RaceShip ahead = ShipAt(0f, -50f);

		Advance(sweep, 1.55f, ahead);
		Assert.Empty(sweep.Echoes);

		Advance(sweep, 0.1f, ahead);
		Assert.Single(sweep.Echoes);
	}

	[Fact]
	public void A_ship_on_the_right_is_marked_at_a_quarter_turn()
	{
		var sweep = new RadarSweep();
		RaceShip right = ShipAt(50f, 0f);

		Advance(sweep, 0.38f, right);
		Assert.Empty(sweep.Echoes);

		Advance(sweep, 0.05f, right);
		Assert.Single(sweep.Echoes);
		Assert.Equal(50f, sweep.Echoes[0].X, 3);
	}

	[Fact]
	public void An_echo_fades_linearly_and_goes_away()
	{
		var sweep = new RadarSweep();
		RaceShip[] ship = { ShipAt(50f, 0f) };
		sweep.Update(0.5f, Vector3.Zero, North, ship);   // el haz pasa por π/2
		Assert.Single(sweep.Echoes);
		Assert.Equal(1f, sweep.EchoOpacity(sweep.Echoes[0]), 3);

		RaceShip[] none = Array.Empty<RaceShip>();
		sweep.Update(0.8f, Vector3.Zero, North, none);
		Assert.Equal(0.5f, sweep.EchoOpacity(sweep.Echoes[0]), 3);

		sweep.Update(0.8f, Vector3.Zero, North, none);
		Assert.Empty(sweep.Echoes);
	}

	[Fact]
	public void An_echo_does_not_move_with_the_ship()
	{
		var sweep = new RadarSweep();
		sweep.Update(0.5f, Vector3.Zero, North, new[] { ShipAt(50f, 0f) });
		RadarEcho first = sweep.Echoes[0];

		// La nave se mueve a otro sitio por el que el haz todavía no pasa
		sweep.Update(0.1f, Vector3.Zero, North, new[] { ShipAt(-20f, 40f) });
		Assert.Equal(first.X, sweep.Echoes[0].X);
		Assert.Equal(first.Y, sweep.Echoes[0].Y);
	}

	[Fact]
	public void Out_of_range_or_on_another_level_there_is_no_echo()
	{
		var sweep = new RadarSweep();
		Advance(sweep, 2f, ShipAt(121f, 0f));
		Assert.Empty(sweep.Echoes);

		sweep = new RadarSweep();
		Advance(sweep, 2f, ShipAt(50f, 0f, 16f));
		Assert.Empty(sweep.Echoes);
	}

	[Theory]
	[InlineData(11f, true)]
	[InlineData(13f, false)]
	public void The_close_warning_does_not_wait_for_the_beam(float distance, bool expected)
	{
		var sweep = new RadarSweep();
		// Detrás: el haz tarda en llegar, pero el aviso sale en el primer paso
		sweep.Update(0.01f, Vector3.Zero, North, new[] { ShipAt(0f, distance) });
		Assert.Equal(expected, sweep.Warnings.Count == 1);
		Assert.Empty(sweep.Echoes);
	}

	[Fact]
	public void The_warning_points_at_the_ship_and_follows_the_formula()
	{
		var sweep = new RadarSweep();
		sweep.Update(0.01f, Vector3.Zero, North, new[] { ShipAt(6f, 0f) });   // a la derecha, a 6 m

		RadarWarning warning = sweep.Warnings[0];
		Assert.Equal(0f, warning.Angle, 3);                      // atan2(−y, x): a la derecha en pantalla
		Assert.Equal(0.4f + 0.6f * 0.5f, warning.Opacity, 3);

		sweep.Update(0.01f, Vector3.Zero, North, new[] { ShipAt(0f, -6f) });  // delante: arriba en pantalla
		Assert.Equal(-MathF.PI / 2f, sweep.Warnings[0].Angle, 3);
	}

	[Fact]
	public void An_echo_remembers_if_the_ship_was_close()
	{
		var sweep = new RadarSweep();
		sweep.Update(0.5f, Vector3.Zero, North, new[] { ShipAt(8f, 0f) });
		Assert.True(sweep.Echoes[0].IsClose);

		sweep = new RadarSweep();
		sweep.Update(0.5f, Vector3.Zero, North, new[] { ShipAt(30f, 0f) });
		Assert.False(sweep.Echoes[0].IsClose);
	}
}

public class RaceShipListTests
{
	[Fact]
	public void Set_adds_or_updates_by_id_and_Remove_takes_it_out()
	{
		var list = new RaceShipList();
		list.Set(new RaceShip(1, Vector3.One, -Vector3.UnitZ, Vector3.UnitY));
		list.Set(new RaceShip(2, Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY));
		list.Set(new RaceShip(1, Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY));

		Assert.Equal(2, list.Ships.Count);
		Assert.Equal(Vector3.UnitX, list.Ships[0].Position);

		list.Remove(1);
		Assert.Single(list.Ships);
		Assert.Equal(2, list.Ships[0].Id);
	}

	[Fact]
	public void Only_the_test_ships_are_on_the_list()
	{
		// La lista solo tiene lo que le escriben: ni la nave del jugador ni el fantasma, que nadie añade
		TrackFrame frame = TestTrack.Circle();
		var list = new RaceShipList();
		var fleet = new TestShipFleet(list, 4);
		fleet.Update(0.1f, frame, 0f, 0f, 2f, 15f);

		Assert.Equal(4, list.Ships.Count);
		foreach (RaceShip ship in list.Ships)
			Assert.True(ship.Id >= 1000);
	}
}

public class TestShipPatternTests
{
	private const float MaxLateral = 16f;

	[Theory]
	[InlineData(0, 0f, 35f, 5.0488f)]                // 6·sen(1)
	[InlineData(0, 10f, 68.5567f, -4.9097f)]         // 35 + 45·sen(2,3); 6·sen(4,1)
	[InlineData(1, 0f, 6.8256f, -5.2976f)]           // −25 + 35·sen(2); 7·sen(4)
	[InlineData(3, 0f, 168.9023f, 0f)]               // 110 + 70·sen(1); 5·sen(0)
	[InlineData(4, 0f, 920f, 3.3659f)]               // lejana: 4·sen(1)
	[InlineData(4, 10f, 910f, 0.5645f)]              // −1 m/s; 4·sen(3)
	[InlineData(5, 10f, -1535f, 2.8222f)]            // +1,5 m/s; −4·sen(5,5)
	public void The_table_gives_the_expected_values(int ship, float t, float distance, float lateral)
	{
		TestShipSpec spec = TestShipPattern.Specs[ship];
		TestShipPattern.Compute(in spec, t, 0f, 100f, MaxLateral, out float s, out float d);   // lateral lejano: sin apartarse
		Assert.Equal(distance, s, 2);
		Assert.Equal(lateral, d, 2);
	}

	[Fact]
	public void A_ship_that_would_be_on_top_moves_aside_to_3_8_m()
	{
		// Nave 3 en t tal que queda casi a la altura del jugador y cerca de lado
		var spec = new TestShipSpec(2f, 0f, 0f, 0f, 1f, 0f, MathF.PI / 2f);
		TestShipPattern.Compute(in spec, 0f, 500f, 0f, MaxLateral, out _, out float right);
		Assert.Equal(3.8f, right, 3);

		spec = new TestShipSpec(2f, 0f, 0f, 0f, -1f, 0f, MathF.PI / 2f);
		TestShipPattern.Compute(in spec, 0f, 500f, 0f, MaxLateral, out _, out float left);
		Assert.Equal(-3.8f, left, 3);

		// Ya a más de 7 m por delante no se aparta
		spec = new TestShipSpec(8f, 0f, 0f, 0f, 1f, 0f, MathF.PI / 2f);
		TestShipPattern.Compute(in spec, 0f, 500f, 0f, MaxLateral, out _, out float away);
		Assert.Equal(1f, away, 3);
	}

	[Fact]
	public void The_lateral_stays_on_the_track()
	{
		// Se aparta hacia fuera desde el lateral 15 y se queda en el límite
		var spec = new TestShipSpec(0f, 0f, 0f, 0f, 15.5f, 0f, MathF.PI / 2f);
		TestShipPattern.Compute(in spec, 0f, 100f, 15f, MaxLateral, out _, out float lateral);
		Assert.Equal(MaxLateral, lateral, 3);
	}

	[Theory]
	[InlineData(0, 2)]
	[InlineData(2, 4)]
	[InlineData(4, 6)]
	[InlineData(6, 0)]
	public void The_key_cycles_0_2_4_6(int count, int next)
	{
		Assert.Equal(next, TestShipPattern.NextCount(count));
	}

	[Fact]
	public void Lowering_the_count_takes_the_extra_ships_off_the_list()
	{
		TrackFrame frame = TestTrack.Circle();
		var list = new RaceShipList();
		var fleet = new TestShipFleet(list, 6);
		fleet.Update(0.1f, frame, 0f, 0f, 2f, 15f);
		Assert.Equal(6, list.Ships.Count);

		fleet.SetCount(2);
		Assert.Equal(2, list.Ships.Count);

		fleet.SetCount(0);
		Assert.Empty(list.Ships);
	}

	[Fact]
	public void The_ships_are_placed_with_the_track_frame()
	{
		TrackFrame frame = TestTrack.Circle();
		var list = new RaceShipList();
		var fleet = new TestShipFleet(list, 2);
		fleet.Update(0f, frame, 100f, 0f, 2f, 15f);

		TestShipSpec spec = TestShipPattern.Specs[0];
		TestShipPattern.Compute(in spec, 0f, 100f, 0f, 15f, out float s, out float d);
		FramePoint point = frame.At(s);
		RaceShip ship = fleet.Ship(0);
		Assert.Equal(point.PointAt(d, 2f), ship.Position);
		Assert.Equal(point.Tangent, ship.Forward);
		Assert.Equal(point.Normal, ship.Up);
	}
}

public class RadarDrawingTests
{
	private static readonly RadarPalette PaletteA = new(new(0f), new(0.1f), new(0.2f), new(0.3f), new(0.4f), new(0.5f), new(0.6f), new(0.7f));
	private static readonly RadarPalette PaletteB = new(new(1f), new(0.9f), new(0.8f), new(0.75f), new(0.65f), new(0.55f), new(0.45f), new(0.35f));

	private static RadarSweep BusySweep()
	{
		var sweep = new RadarSweep();
		var ships = new[]
		{
			new RaceShip(1, new Vector3(50f, 0f, 0f), -Vector3.UnitZ, Vector3.UnitY),
			new RaceShip(2, new Vector3(6f, 0f, 0f), -Vector3.UnitZ, Vector3.UnitY)
		};
		sweep.Update(0.5f, Vector3.Zero, new Vector2(0f, -1f), ships);
		return sweep;
	}

	private static List<RadarPrimitive> Draw(RadarStyle style, RadarSweep sweep)
	{
		var primitives = new List<RadarPrimitive>();
		RadarDrawing.Base(primitives, style, sweep.Settings.Range);
		RadarDrawing.Live(primitives, style, sweep);
		return primitives;
	}

	[Fact]
	public void Two_palettes_give_the_same_primitives_with_other_colors()
	{
		RadarSweep sweep = BusySweep();
		List<RadarPrimitive> a = Draw(RadarStyle.Hud(PaletteA, 60f, 1f), sweep);
		List<RadarPrimitive> b = Draw(RadarStyle.Hud(PaletteB, 60f, 1f), sweep);

		Assert.Equal(a.Count, b.Count);
		for (int i = 0; i < a.Count; i++)
		{
			Assert.Equal(a[i] with { Color = default }, b[i] with { Color = default });
			Assert.Equal(a[i].Color.W, b[i].Color.W);
			Assert.NotEqual(a[i].Color, b[i].Color);
		}
	}

	[Fact]
	public void The_base_has_background_border_two_rings_cross_and_label()
	{
		var primitives = new List<RadarPrimitive>();
		RadarDrawing.Base(primitives, RadarStyle.Hud(PaletteA, 60f, 1f), 120f);

		Assert.Equal(RadarPrimitiveKind.Disc, primitives[0].Kind);
		Assert.Equal(0.72f, primitives[0].Color.W, 3);
		Assert.Equal(60f, primitives[1].Radius);
		Assert.Equal(2f, primitives[1].Width);
		Assert.Equal(20f, primitives[2].Radius, 3);
		Assert.Equal(40f, primitives[3].Radius, 3);
		Assert.Equal("120 M", primitives[^1].Text);
		Assert.Equal(new Vector2(60f * 0.72f, 60f * 0.98f), primitives[^1].A);
	}

	[Fact]
	public void The_beam_has_28_lines_fading_from_0_4_to_0()
	{
		var primitives = new List<RadarPrimitive>();
		RadarDrawing.Live(primitives, RadarStyle.Hud(PaletteA, 60f, 1f), new RadarSweep());
		List<RadarPrimitive> lines = primitives.FindAll(p => p.Kind == RadarPrimitiveKind.Line);

		Assert.Equal(28, lines.Count);
		Assert.Equal(0.4f, lines[0].Color.W, 3);
		Assert.Equal(0.4f * (1f / 28f), lines[27].Color.W, 3);
		// El haz empieza mirando arriba (delante), del centro al borde
		Assert.Equal(Vector2.Zero, lines[0].A);
		Assert.Equal(0f, lines[0].B.X, 3);
		Assert.Equal(-60f, lines[0].B.Y, 3);
	}

	[Fact]
	public void Echoes_are_placed_scaled_and_close_ones_are_bigger()
	{
		List<RadarPrimitive> primitives = new();
		RadarDrawing.Live(primitives, RadarStyle.Hud(PaletteA, 60f, 1f), BusySweep());
		List<RadarPrimitive> discs = primitives.FindAll(p => p.Kind == RadarPrimitiveKind.Disc);

		Assert.Equal(2, discs.Count);
		Assert.Equal(25f, discs[0].A.X, 3);    // 50 / 120 · 60
		Assert.Equal(3.4f, discs[0].Radius, 3);
		Assert.Equal(new Vector4(PaletteA.Rival, 1f), discs[0].Color);
		Assert.Equal(3f, discs[1].A.X, 3);     // 6 / 120 · 60
		Assert.Equal(4.2f, discs[1].Radius, 3);
		Assert.Equal(new Vector4(PaletteA.Close, 1f), discs[1].Color);
	}

	[Fact]
	public void The_warning_arc_goes_outside_the_border()
	{
		List<RadarPrimitive> primitives = new();
		RadarDrawing.Live(primitives, RadarStyle.Hud(PaletteA, 60f, 1f), BusySweep());
		RadarPrimitive arc = primitives.Find(p => p.Kind == RadarPrimitiveKind.Arc);

		Assert.Equal(64f, arc.Radius);
		Assert.Equal(4f, arc.Width);
		Assert.Equal(-0.42f, arc.StartAngle, 3);
		Assert.Equal(0.42f, arc.EndAngle, 3);
	}

	[Fact]
	public void The_arrow_size_has_a_minimum_and_grows_with_the_radius()
	{
		Assert.Equal(6f, RadarStyle.Hud(PaletteA, 60f, 1f).SelfSize);
		Assert.Equal(7.5f, RadarStyle.Hud(PaletteA, 100f, 1f).SelfSize, 3);
	}

	[Fact]
	public void The_scale_only_changes_the_size()
	{
		RadarStyle one = RadarStyle.Hud(PaletteA, 60f, 1f);
		RadarStyle big = RadarStyle.Hud(PaletteA, 60f, 1.5f);
		Assert.Equal(one.Radius * 1.5f, big.Radius, 3);
		Assert.Equal(one.EchoRadius * 1.5f, big.EchoRadius, 3);
		Assert.Equal(one.BeamOpacity, big.BeamOpacity);
	}
}

// Una pista circular para colocar naves, sin Godot
internal static class TestTrack
{
	public static TrackFrame Circle()
	{
		const int Points = 256;
		const float Radius = 500f;
		var plan = new List<Vector3>();
		for (int i = 0; i < Points; i++)
		{
			float a = MathF.Tau * i / Points;
			plan.Add(new Vector3(MathF.Cos(a) * Radius, 0f, MathF.Sin(a) * Radius));
		}
		return new TrackFrame(plan);
	}
}
