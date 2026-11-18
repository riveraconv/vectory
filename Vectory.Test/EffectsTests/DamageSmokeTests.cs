using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Humo de daños: focos, emisor y penacho (spec docs/efectos-4-humo-danos.md, pruebas 1–9)
public class DamageSmokeTests
{
    private const float Kmh = 1f / 3.6f;
    private static readonly DamageSmokeFoci Foci = ShipDefinitions.Find(ShipCatalog.Ship01).Visual.SmokeFoci;
    private static readonly ShipPose Pose = ShipPose.Flat(new Vector3(0f, 2f, 0f));

    private static DamageSmoke Smoke(int seed = 1, DamageSmokeSettings? settings = null) =>
        new(settings ?? DamageSmokeSettings.Default, new Random(seed));

    // Velocidad hacia −Z (el morro con rumbo 0)
    private static Vector3 Forward(float kmh) => new(0f, 0f, -kmh * Kmh);

    private static int EmitFor(DamageSmoke smoke, int level, float seconds, float dt, float quality = 1f)
    {
        int total = 0;
        int steps = (int)MathF.Round(seconds / dt);
        for (int i = 0; i < steps; i++)
            total += smoke.Emit(0, level, Foci, Pose, Forward(300f), quality, dt);
        return total;
    }

    [Fact]
    public void Foci_by_level()
    {
        var s = DamageSmokeSettings.Default;
        Assert.Empty(DamageSmokeFocusRules.Active(0, Foci, s));
        Assert.Empty(DamageSmokeFocusRules.Active(1, Foci, s));
        var two = DamageSmokeFocusRules.Active(2, Foci, s);
        Assert.Equal(new[] { new SmokeFocus(0, Foci.Smoke, 1f) }, two);
        var three = DamageSmokeFocusRules.Active(3, Foci, s);
        Assert.Equal(new[] { Foci.Smoke, Foci.Smoke2, Foci.Smoke3 }, three.Select(f => f.Local));
        Assert.Equal(new[] { 1f, 0.75f, 0.75f }, three.Select(f => f.Size));
        Assert.Equal(0, Smoke().Emit(0, 1, Foci, Pose, Forward(300f), 1f, 1f));
    }

    [Fact]
    public void Count_and_At_give_the_same_foci_as_Active()
    {
        var s = DamageSmokeSettings.Default;
        for (int level = 0; level <= 3; level++)
            Assert.Equal(DamageSmokeFocusRules.Active(level, Foci, s),
                Enumerable.Range(0, DamageSmokeFocusRules.Count(level)).Select(i => DamageSmokeFocusRules.At(i, Foci, s)));
    }

    [Theory]
    [InlineData(1f / 60f)]
    [InlineData(1f / 30f)]
    public void Puffs_per_second_do_not_depend_on_fps(float dt)
    {
        Assert.InRange(EmitFor(Smoke(), 2, 1f, dt), 5, 7);
        Assert.InRange(EmitFor(Smoke(), 3, 1f, dt), 23, 25);
        // LOW: 6 por segundo de media (cada foco tiene su acumulador, así que en el primer segundo salen 2 + 1 + 1)
        Assert.InRange(EmitFor(Smoke(), 3, 4f, dt, 0.25f), 23, 25);
    }

    [Fact]
    public void Lag_is_6_percent_between_1_5_and_7()
    {
        var smoke = Smoke();
        Assert.Equal(6.667f, smoke.Lag(400f * Kmh), 2);
        Assert.Equal(3.333f, smoke.Lag(200f * Kmh), 2);
        Assert.Equal(1.5f, smoke.Lag(0f));
        Assert.Equal(7f, smoke.Lag(500f * Kmh));
    }

    [Fact]
    public void A_new_puff_carries_the_ship_velocity_minus_the_lag()
    {
        var smoke = Smoke();
        Vector3 v = Forward(400f);
        for (int i = 0; i < 60; i++)
            smoke.Emit(0, 3, Foci, Pose, v, 1f, 1f / 60f);
        float lag = smoke.Lag(v.Length());
        Assert.True(smoke.Count > 10);
        for (int i = 0; i < smoke.Count; i++)
        {
            DamagePuff p = smoke[i];
            Assert.InRange(p.Carry.Z, v.Z + lag * 0.8f - 1e-3f, v.Z + lag * 1.2f + 1e-3f);
            Assert.Equal(0f, p.Carry.X, 4);
            Assert.Equal(0f, Vector3.Dot(p.SideAxis, Vector3.UnitZ), 4);
        }
    }

    [Fact]
    public void A_puff_is_born_at_its_focus_and_stays_where_it_goes()
    {
        var smoke = Smoke();
        while (smoke.Count == 0)
            smoke.Emit(0, 2, Foci, Pose, Forward(300f), 1f, 1f / 60f);
        DamagePuff p = smoke[0];
        Vector3 focus = Pose.Point(Foci.Smoke);
        Assert.InRange(p.Position.X, focus.X - 0.08f, focus.X + 0.08f);
        Assert.Equal(focus.Y, p.Position.Y, 4);
        Assert.Equal(focus.Z, p.Position.Z, 4);
        // Sin seguir a la nave: avanza con su propia velocidad
        smoke.Update(0.5f);
        Assert.True(smoke[0].Position.Z < focus.Z - 0.5f * 70f);
        Assert.True(smoke[0].Position.Y > focus.Y);
    }

    [Fact]
    public void Opacity_fades_in_in_0_08_and_out_at_the_end()
    {
        var s = DamageSmokeSettings.Default;
        var smoke = Smoke();
        var p = new DamagePuff { Level = 3, Life = 1.2f };
        Assert.Equal(0f, smoke.Opacity(p));
        p.Age = 0.08f;
        Assert.Equal(0.62f * MathF.Pow(1f - 0.08f / 1.2f, 1.4f), smoke.Opacity(p), 4);
        p.Level = 2;
        Assert.Equal(0.42f * MathF.Pow(1f - 0.08f / 1.2f, 1.4f), smoke.Opacity(p), 4);
        p.Age = 1.2f;
        Assert.Equal(0f, smoke.Opacity(p));
        Assert.Equal(s.Medium.Color, smoke.Color(new DamagePuff { Level = 2, Shade = 1f }));
        Assert.Equal(s.Strong.Color * 0.9f, smoke.Color(new DamagePuff { Level = 3, Shade = 0.9f }));
    }

    [Fact]
    public void Radius_at_half_life_is_nearer_the_end()
    {
        var smoke = Smoke();
        var p = new DamagePuff { Level = 2, Life = 1f, Age = 0.5f, StartRadius = 0.35f, EndRadius = 1.5f };
        float r = smoke.Radius(p);
        Assert.True(r - 0.35f > 1.5f - r);
    }

    [Fact]
    public void No_more_than_the_maximum()
    {
        var settings = DamageSmokeSettings.Default;
        Assert.Equal(420, settings.MaxPuffs);
        settings.MaxPuffs = 30;
        var smoke = Smoke(settings: settings);
        for (int ship = 0; ship < 8; ship++)
            for (int i = 0; i < 60; i++)
                smoke.Emit(ship, 3, Foci, Pose, Forward(300f), 1f, 1f / 60f);
        Assert.Equal(30, smoke.Count);
    }

    [Fact]
    public void Nothing_changes_with_zero_delta()
    {
        var smoke = Smoke();
        EmitFor(smoke, 3, 0.5f, 1f / 60f);
        smoke.Update(0.2f);
        DamagePuff before = smoke[0];
        int count = smoke.Count;
        smoke.Update(0f);
        Assert.Equal(0, smoke.Emit(0, 3, Foci, Pose, Forward(300f), 1f, 0f));
        Assert.Equal(count, smoke.Count);
        Assert.Equal(before.Position, smoke[0].Position);
        Assert.Equal(before.Age, smoke[0].Age);
    }

    [Fact]
    public void Same_seed_same_puffs()
    {
        var a = Smoke(9);
        var b = Smoke(9);
        EmitFor(a, 3, 1f, 1f / 60f);
        EmitFor(b, 3, 1f, 1f / 60f);
        a.Update(0.3f);
        b.Update(0.3f);
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Position, b[i].Position);
            Assert.Equal(a[i].Texture, b[i].Texture);
            Assert.Equal(DamageSmoke.RotationOf(a[i]), DamageSmoke.RotationOf(b[i]));
        }
    }

    [Fact]
    public void Respawn_clears_only_that_ship()
    {
        var smoke = Smoke();
        for (int i = 0; i < 60; i++)
        {
            smoke.Emit(0, 3, Foci, Pose, Forward(300f), 1f, 1f / 60f);
            smoke.Emit(5, 3, Foci, Pose, Forward(300f), 1f, 1f / 60f);
        }
        smoke.Clear(0);
        Assert.True(smoke.Count > 0);
        for (int i = 0; i < smoke.Count; i++)
            Assert.Equal(5, smoke[i].ShipId);
    }

    [Fact]
    public void Smoke_focus_is_the_manifest_empty_of_each_hull()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        foreach ((string id, string hull) in new[] { (ShipCatalog.Ship01, "hull_vector"), (ShipCatalog.Ship02, "hull_blade"), (ShipCatalog.Ship03, "hull_hammer") })
        {
            HullManifest manifest = HullManifest.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "assets", "ships", hull + ".manifest.json")))!;
            var empty = manifest.Empties.Single(e => e.Name == "Smoke");
            DamageSmokeFoci foci = ShipDefinitions.Find(id).Visual.SmokeFoci;
            Assert.Equal(empty.Position[0], foci.Smoke.X, 2);
            Assert.Equal(empty.Position[1], foci.Smoke.Y, 2);
            Assert.Equal(empty.Position[2], foci.Smoke.Z, 2);
        }
    }

    [Fact]
    public void Cloud_texture_is_round_and_lighter_on_top()
    {
        var pixels = CloudTexture.Pixels(3);
        int n = CloudTexture.Size;
        Assert.Equal(n * n, pixels.Length);
        Assert.Equal(0f, pixels[0].Alpha);
        Assert.True(pixels[n / 2 * n + n / 2].Alpha > 0.3f);
        float top = Enumerable.Range(0, n).Average(i => pixels[10 * n + i].Light);
        float bottom = Enumerable.Range(0, n).Average(i => pixels[(n - 10) * n + i].Light);
        Assert.True(top > bottom);
        Assert.NotEqual(pixels[n / 2 * n + n / 2], CloudTexture.Pixels(17)[n / 2 * n + n / 2]);
    }
}
