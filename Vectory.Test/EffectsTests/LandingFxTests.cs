using System;
using System.Numerics;
using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Efectos del aterrizaje: chispas desde la panza y polvo (spec docs/efectos-3-aterrizaje.md, pruebas 1–8)
public class LandingFxTests
{
    private static readonly LandingSettings Thresholds = new() { MediumSpeed = 3f, StrongSpeed = 8f };

    // VECTOR a 2 m de una pista plana, sin inclinar, a 270 km/h hacia −Z
    private const float HalfWidth = 2.63f, Back = 3.31f, Belly = -0.31f, Hover = 2f;
    private static readonly ShipPose Pose = ShipPose.Flat(new Vector3(0f, Hover, 0f));
    private static readonly Vector2 Velocity = new(0f, -75f);

    private static CollisionFx Fx(int seed = 1, float quality = 1f)
    {
        var fx = new CollisionFx(CollisionFxSettings.Default, new Random(seed));
        fx.SetQuality(quality);
        return fx;
    }

    private static LandingBurst Land(CollisionFx fx, LandingLevel level) =>
        fx.Land(level, null, Pose, Velocity, HalfWidth, Back, Belly);

    [Fact]
    public void Level_follows_the_pista3d_thresholds()
    {
        Assert.Equal(LandingLevel.None, LandingImpact.Level(2.9f, Thresholds));
        Assert.Equal(LandingLevel.Medium, LandingImpact.Level(3f, Thresholds));
        Assert.Equal(LandingLevel.Medium, LandingImpact.Level(7.9f, Thresholds));
        Assert.Equal(LandingLevel.Strong, LandingImpact.Level(8f, Thresholds));
        Assert.Equal(default, Land(Fx(), LandingLevel.None));
    }

    [Fact]
    public void High_counts_by_level()
    {
        Assert.Equal(new LandingBurst(20, 0, 4), Land(Fx(), LandingLevel.Medium));
        Assert.Equal(new LandingBurst(70, 12, 12), Land(Fx(), LandingLevel.Strong));
    }

    [Fact]
    public void Low_counts_with_their_minimums()
    {
        var emitter = new LandingEmitter(LandingFxSettings.Default, new Random(1));
        Assert.Equal(new LandingBurst(5, 0, 1), emitter.CountFor(LandingLevel.Medium, 0.25f));
        Assert.Equal(new LandingBurst(18, 3, 3), emitter.CountFor(LandingLevel.Strong, 0.25f));
        Assert.Equal(new LandingBurst(18, 3, 3), Land(Fx(quality: 0.25f), LandingLevel.Strong));

        var few = LandingFxSettings.Default;
        few.Medium.Sparks = 1;
        few.Strong.Puffs = 4;
        var low = new LandingEmitter(few, new Random(1));
        Assert.Equal(1, low.CountFor(LandingLevel.Medium, 0.25f).Sparks);
        Assert.Equal(2, low.CountFor(LandingLevel.Strong, 0.25f).Puffs);
    }

    [Fact]
    public void Sparks_start_under_the_belly_and_go_out_by_their_side()
    {
        var fx = Fx();
        Land(fx, LandingLevel.Strong);
        LandingFxSettings s = LandingFxSettings.Default;
        Assert.Equal(82, fx.Sparks.Count);
        for (int i = 0; i < fx.Sparks.Count; i++)
        {
            Spark p = fx.Sparks[i];
            Assert.InRange(p.Position.X, -s.SparkWidthFraction * HalfWidth, s.SparkWidthFraction * HalfWidth);
            Assert.InRange(p.Position.Z, -s.SparkAhead, Back);
            Assert.Equal(Hover + Belly - s.SparkBellyDrop, p.Position.Y, 4);
            Assert.True(p.Velocity.Y > 0f);
            if (!p.Ember)
            {
                Assert.True(MathF.Sign(p.Position.X) * p.Velocity.X > 0f);
                Assert.InRange(p.Life, s.SparkLifeMin, s.SparkLifeMax);
            }
            else
                Assert.InRange(p.Life, s.EmberLifeMin, s.EmberLifeMax);
            // A lo largo, el 70 % de la nave (−52,5 m/s) de 4 menos a 2 más
            Assert.InRange(p.Velocity.Z, -52.5f - 2f, -52.5f + 4f);
        }
    }

    [Fact]
    public void Sparks_follow_the_ship_bank()
    {
        // Inclinada 20° a la derecha: la panza de la izquierda queda más alta que la de la derecha
        float a = 20f * MathF.PI / 180f;
        var pose = new ShipPose(new Vector3(0f, Hover, 0f), new Vector3(MathF.Cos(a), -MathF.Sin(a), 0f),
            new Vector3(MathF.Sin(a), MathF.Cos(a), 0f), Vector3.UnitZ);
        var fx = Fx();
        fx.Land(LandingLevel.Strong, null, pose, Velocity, HalfWidth, Back, Belly);
        for (int i = 0; i < fx.Sparks.Count; i++)
        {
            Spark p = fx.Sparks[i];
            float lateral = Vector3.Dot(p.Position - pose.Origin, pose.Right);
            Assert.Equal(Belly - LandingFxSettings.Default.SparkBellyDrop, Vector3.Dot(p.Position - pose.Origin, pose.Up), 3);
            if (lateral < -0.5f)
                Assert.True(p.Position.Y > Hover + Belly - 0.06f);
        }
    }

    [Fact]
    public void Puffs_alternate_sides_and_go_out()
    {
        var fx = Fx();
        Land(fx, LandingLevel.Strong);
        LandingFxSettings s = LandingFxSettings.Default;
        float scale = HalfWidth / s.DustReferenceHalfWidth;
        Assert.Equal(12, fx.Smoke.Count);
        for (int i = 0; i < fx.Smoke.Count; i++)
        {
            SmokePuff p = fx.Smoke[i];
            float side = i % 2 == 1 ? 1f : -1f;
            Assert.Equal(side, MathF.Sign(p.Position.X));
            Assert.Equal(side, MathF.Sign(p.Velocity.X));
            Assert.InRange(MathF.Abs(p.Position.X), s.DustSideMin * scale, s.DustSideMax * scale);
            Assert.InRange(p.Position.Z, -s.DustAhead, s.DustBehind);
            Assert.Equal(s.DustLift, p.Position.Y, 4);
            Assert.InRange(p.Age, -s.DustWindow, 0f);
            Assert.Equal(0.7f, CrashSmoke.HeightScale(p));
        }
    }

    [Fact]
    public void Dust_has_its_own_opacity_and_crash_smoke_is_unchanged()
    {
        var fx = Fx();
        Land(fx, LandingLevel.Medium);
        SmokePuff dust = fx.Smoke[0];
        dust.Age = 0.08f;
        float u = dust.Age / dust.Life;
        Assert.Equal(0.38f * MathF.Pow(1f - u, 1.3f), fx.Smoke.Opacity(dust), 4);

        var crash = new SmokePuff { Age = 0.1f, Life = 1f, StartRadius = 0.4f, EndRadius = 1.6f };
        Assert.Equal(0.42f * MathF.Pow(0.9f, 1.3f), fx.Smoke.Opacity(crash), 4);
        Assert.Equal(1f, CrashSmoke.HeightScale(crash));
    }

    [Fact]
    public void A_puff_stops_half_a_metre_from_the_barrier()
    {
        var fx = Fx();
        var ground = new FlatCrashGround(11.5f);
        fx.Smoke.Add(new SmokePuff
        {
            Position = new Vector3(10f, 0.3f, 0f), Velocity = new Vector3(6f, 0f, -30f), Life = 1.5f,
            StartRadius = 0.6f, EndRadius = 2.4f, Style = LandingFxSettings.Default.Dust
        });
        for (int i = 0; i < 30; i++)
            fx.Smoke.Update(1f / 60f, ground);
        SmokePuff p = fx.Smoke[0];
        Assert.Equal(11f, p.Position.X, 4);
        Assert.Equal(0f, p.Velocity.X);
        Assert.True(p.Velocity.Z < 0f);

        // El humo de los choques no tiene tope
        var crash = Fx();
        crash.Smoke.Add(new SmokePuff { Position = new Vector3(10f, 0.3f, 0f), Velocity = new Vector3(6f, 0f, 0f), Life = 1.5f });
        for (int i = 0; i < 30; i++)
            crash.Smoke.Update(1f / 60f, ground);
        Assert.True(crash.Smoke[0].Position.X > 11f);
    }

    [Fact]
    public void Same_seed_same_effects()
    {
        var a = Fx(7);
        var b = Fx(7);
        Land(a, LandingLevel.Strong);
        Land(b, LandingLevel.Strong);
        Assert.Equal(a.Sparks.Count, b.Sparks.Count);
        for (int i = 0; i < a.Sparks.Count; i++)
        {
            Assert.Equal(a.Sparks[i].Position, b.Sparks[i].Position);
            Assert.Equal(a.Sparks[i].Velocity, b.Sparks[i].Velocity);
        }
        for (int i = 0; i < a.Smoke.Count; i++)
            Assert.Equal(a.Smoke[i].Position, b.Smoke[i].Position);
    }

    [Fact]
    public void Nothing_changes_with_zero_delta()
    {
        var fx = Fx();
        Land(fx, LandingLevel.Strong);
        var ground = new FlatCrashGround(11.5f);
        Spark spark = fx.Sparks[0];
        SmokePuff puff = fx.Smoke[0];
        fx.Update(0f, ground);
        Assert.Equal(spark.Position, fx.Sparks[0].Position);
        Assert.Equal(spark.Age, fx.Sparks[0].Age);
        Assert.Equal(puff.Position, fx.Smoke[0].Position);
        Assert.Equal(puff.Age, fx.Smoke[0].Age);
    }

    [Fact]
    public void Belly_heights_match_the_hull_manifests()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        foreach ((string id, string hull) in new[] { (ShipCatalog.Ship01, "hull_vector"), (ShipCatalog.Ship02, "hull_blade"), (ShipCatalog.Ship03, "hull_hammer") })
        {
            string path = System.IO.Path.Combine(dir!.FullName, "assets", "ships", hull + ".manifest.json");
            HullManifest manifest = HullManifest.Parse(System.IO.File.ReadAllText(path))!;
            Assert.Equal(manifest.Bounds.Min[1], ShipDefinitions.Find(id).Visual.BellyY, 2);
        }
    }
}
