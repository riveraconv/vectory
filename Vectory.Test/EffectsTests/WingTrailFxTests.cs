using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Estelas de las alas (spec docs/efectos-7-estelas.md, pruebas 1–9)
public class WingTrailFxTests
{
    private static readonly WingTrailSettings S = WingTrailSettings.Default;
    // Los de la spec, sin los ajustes del usuario (la mitad de larga, el 80 % de ancha)
    private static readonly WingTrailSettings Spec = new() { LifeScale = 1f, WidthScale = 1f };
    private const float Dt = 1f / 60f;

    // Una nave que avanza hacia −Z a speed m/s girando a omega rad/s, durante seconds; las puntas van con ella
    private static void Run(WingTrailState state, float seconds, float speed, float omega = 0f, bool drift = false,
        float slip = 0f, float throttle = 1f, float boost = 0f)
    {
        float heading = 0f;
        Vector3 position = Vector3.Zero;
        if (state.Tip(0).Count > 0)
            position = (state.Tip(0)[0].Position + state.Tip(1)[0].Position) * 0.5f;
        int steps = (int)MathF.Round(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            heading += omega * Dt;
            var velocity = new Vector2(MathF.Sin(heading), MathF.Cos(heading)) * speed;
            state.Move(Dt, new WingTrailMotion { Velocity = velocity, Drifting = drift, SlipAngle = slip }, S);
            position += new Vector3(velocity.X, 0f, velocity.Y) * Dt;
            state.Update(Dt, Frame(position, throttle, boost), S);
        }
    }

    private static WingTrailFrame Frame(Vector3 position, float throttle = 1f, float boost = 0f) => new()
    {
        TipLeft = position + new Vector3(-2.6f, 0f, 2.72f),
        TipRight = position + new Vector3(2.6f, 0f, 2.72f),
        Right = Vector3.UnitX,
        Up = Vector3.UnitY,
        Throttle = throttle,
        Boost = boost
    };

    // 1
    [Fact]
    public void Load_is_zero_on_a_straight_full_on_lateral_acceleration_and_085_drifting_at_max_slip()
    {
        var straight = new WingTrailState(S);
        Run(straight, 1f, 100f);
        Assert.Equal(0f, straight.Load, 3);

        var turning = new WingTrailState(S);
        Run(turning, 1.5f, 50f, omega: 25f / 50f);
        Assert.Equal(1f, turning.Load, 2);

        var drifting = new WingTrailState(S);
        Run(drifting, 1.5f, 50f, drift: true, slip: -S.MaxSlipDegrees * MathF.PI / 180f);
        Assert.Equal(0.85f, drifting.Load, 2);
    }

    [Fact]
    public void Load_uses_the_direction_of_travel_and_wraps_around_pi()
    {
        var state = new WingTrailState(S);
        // Justo al cruzar ±π: sin envolver, ω sería enorme
        state.Move(Dt, new WingTrailMotion { Velocity = new Vector2(0.001f, -50f) }, S);
        state.Move(Dt, new WingTrailMotion { Velocity = new Vector2(-0.001f, -50f) }, S);
        Assert.True(state.LoadTarget < 0.01f);
        Assert.Equal(0.6f, WingTrailState.TurnLoad(30f, -0.5f, S), 4);
    }

    // 2
    [Fact]
    public void Life_is_025_stopped_115_at_top_speed_and_16_at_top_speed_with_boost()
    {
        Assert.Equal(0.25f, WingTrailState.Life(0f, 0f, 0f, Spec), 4);
        Assert.Equal(1.15f, WingTrailState.Life(1f, 0f, 0f, Spec), 4);
        Assert.Equal(1.6f, WingTrailState.Life(1f, 1f, 0f, Spec), 4);
        // Decisión del usuario: la mitad de larga
        Assert.Equal(0.575f, WingTrailState.Life(1f, 0f, 0f, S), 4);
        Assert.Equal(0.8f, WingTrailState.Life(1f, 1f, 0f, S), 4);
    }

    // 3
    [Fact]
    public void Visibility_starts_at_37_percent_is_full_at_82_and_load_shows_it_slow()
    {
        Assert.Equal(0f, WingTrailState.Visibility(0.3f, 0f, 0f, S), 4);
        Assert.Equal(0f, WingTrailState.Visibility(0.37f, 0f, 0f, S), 4);
        Assert.Equal(1f, WingTrailState.Visibility(0.82f, 0f, 0f, S), 4);
        Assert.Equal(0.8f, WingTrailState.Visibility(0.3f, 0f, 1f, S), 4);
        Assert.Equal(1f, WingTrailState.Visibility(0f, 1f, 0f, S), 4);
    }

    // 4
    [Fact]
    public void Width_at_the_wing_tip_and_half_at_the_end()
    {
        Assert.Equal(0.24f, WingTrailState.Width(1f, 0f, 0f, 0f, Spec), 4);
        Assert.Equal(0.64f, WingTrailState.Width(1f, 1f, 1f, 0f, Spec), 4);
        Assert.Equal(0.32f, WingTrailState.Width(1f, 1f, 1f, 1f, Spec), 4);
        // Decisión del usuario: el 80 %
        Assert.Equal(0.192f, WingTrailState.Width(1f, 0f, 0f, 0f, S), 4);
    }

    // 5
    [Fact]
    public void Color_is_the_normal_one_without_boost_and_60_percent_to_boost_with_it()
    {
        WingTrailColors c = WingTrailColors.CyanOrange;
        Assert.Equal(c.Normal, WingTrailState.Color(0f, c, S));
        Vector3 boosted = WingTrailState.Color(1f, c, S);
        Vector3 expected = c.Normal + (c.Boost - c.Normal) * 0.6f;
        Assert.Equal(expected.X, boosted.X, 4);
        Assert.Equal(expected.Y, boosted.Y, 4);
        Assert.Equal(expected.Z, boosted.Z, 4);
    }

    // 6
    [Fact]
    public void Samples_never_exceed_400_nor_22_seconds_and_only_live_ones_are_drawn()
    {
        var state = new WingTrailState(S);
        // 3 s a 240 frames/s: 720 muestras, más de 400
        float dt = 1f / 240f;
        Vector3 p = Vector3.Zero;
        for (int i = 0; i < 720; i++)
        {
            p.Z -= 0.5f;
            state.Update(dt, Frame(p), S);
        }
        Assert.Equal(400, state.Tip(0).Count);

        var slow = new WingTrailState(S);
        p = Vector3.Zero;
        for (int i = 0; i < 300; i++)
        {
            p.Z -= 1f;
            slow.Update(Dt, Frame(p, throttle: 0.5f), S);
        }
        WingTrailTip tip = slow.Tip(1);
        Assert.True(slow.Time - tip[tip.Count - 1].Time <= S.History + 1e-4f);

        float life = WingTrailState.Life(0.5f, 0f, 0f, S);
        int live = WingTrailRibbon.LiveCount(tip, slow.Time, S);
        int expected = Enumerable.Range(0, tip.Count).Count(i => slow.Time - tip[i].Time <= life);
        Assert.Equal(expected, live);
        Assert.InRange(live, 20, 23);
        Assert.True(live < tip.Count);
        var output = new WingTrailVertex[800];
        int vertices = WingTrailRibbon.Build(tip, slow.Time, new Vector3(0f, 5f, 20f), WingTrailColors.CyanOrange, S, output);
        Assert.Equal(2 * live, vertices);
        Assert.All(output.Take(vertices), v => Assert.InRange(v.U, 0f, 1f));
    }

    [Fact]
    public void Ribbon_faces_the_camera_with_the_width_of_each_sample()
    {
        var state = new WingTrailState(S);
        Run(state, 1f, 100f);
        var output = new WingTrailVertex[800];
        int n = WingTrailRibbon.Build(state.Tip(1), state.Time, new Vector3(0f, 50f, 0f), WingTrailColors.CyanOrange, S, output);
        Assert.True(n >= 4);
        // Cámara encima de una estela que va a lo largo de Z: los dos lados, en X
        Vector3 across = output[1].Position - output[0].Position;
        Assert.Equal(0.24f * S.WidthScale, across.Length(), 3);
        Assert.True(MathF.Abs(across.X) > 0.99f * across.Length());
        Assert.Equal(-1f, output[0].V);
        Assert.Equal(1f, output[1].V);
        Assert.Equal(0f, output[0].Distance);
        Assert.True(output[n - 1].Distance > 0f);
    }

    [Fact]
    public void No_sample_while_the_tip_has_not_moved()
    {
        var state = new WingTrailState(S);
        state.Update(Dt, Frame(Vector3.Zero), S);
        state.Update(Dt, Frame(Vector3.Zero), S);
        Assert.Equal(1, state.Tip(0).Count);
    }

    // 7
    [Fact]
    public void Pause_adds_no_samples_and_does_not_age_them()
    {
        var state = new WingTrailState(S);
        Run(state, 0.5f, 80f, omega: 0.3f);
        int count = state.Tip(0).Count;
        float time = state.Time;
        float load = state.Load;
        state.Move(0f, new WingTrailMotion { Velocity = new Vector2(0f, -80f) }, S);
        state.Update(0f, Frame(new Vector3(0f, 0f, -500f)), S);
        Assert.Equal(count, state.Tip(0).Count);
        Assert.Equal(time, state.Time);
        Assert.Equal(load, state.Load);
    }

    // 8
    [Fact]
    public void Reset_clears_the_samples_and_the_load()
    {
        var state = new WingTrailState(S);
        Run(state, 1f, 50f, omega: 0.5f);
        Assert.True(state.Load > 0.5f);
        state.Reset();
        Assert.Equal(0, state.Tip(0).Count);
        Assert.Equal(0, state.Tip(1).Count);
        Assert.Equal(0f, state.Load);
        Assert.Equal(0f, state.LoadTarget);
    }

    // 9
    [Fact]
    public void Color_provider_gives_cyan_and_orange_for_every_ship_and_paint()
    {
        foreach (string id in new[] { ShipCatalog.Ship01, ShipCatalog.Ship02, ShipCatalog.Ship03 })
        {
            for (int paint = -1; paint < 6; paint++)
            {
                WingTrailColors c = WingTrailPalette.For(id, paint);
                Assert.Equal(new Vector3(0.21f, 0.90f, 1f), c.Normal);
                Assert.Equal(new Vector3(1f, 0.62f, 0.20f), c.Boost);
            }
        }
    }

    [Fact]
    public void Trail_tips_match_the_hull_manifests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        foreach ((string id, string hull) in new[] { (ShipCatalog.Ship01, "hull_vector"), (ShipCatalog.Ship02, "hull_blade"), (ShipCatalog.Ship03, "hull_hammer") })
        {
            HullManifest manifest = HullManifest.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "assets", "ships", hull + ".manifest.json")))!;
            Vector3 tip = ShipDefinitions.Find(id).Visual.TrailTip;
            var right = manifest.Empties.Single(e => e.Name == "Trail_R");
            var left = manifest.Empties.Single(e => e.Name == "Trail_L");
            Assert.Equal(right.Position[0], tip.X, 2);
            Assert.Equal(right.Position[1], tip.Y, 2);
            Assert.Equal(right.Position[2], tip.Z, 2);
            Assert.Equal(left.Position[0], -tip.X, 2);
            Assert.Equal(left.Position[1], tip.Y, 2);
            Assert.Equal(left.Position[2], tip.Z, 2);
        }
    }
}
