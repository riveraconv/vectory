using System.Numerics;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Effects;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Efectos de choque contra la barrera (spec docs/efectos-1-choques.md, pruebas 1–14)
public class CollisionFxTests
{
    private const float Barrier = 20f;
    private static readonly Vector3 ShipVelocity = new(0f, 0f, -80f);

    // Barrera derecha en planta (eje hacia −Z): normal hacia −X, arriba +Y, popa +Z
    private static CrashEvent BarrierHit(ImpactLevel level, bool start, float y = 0.75f) =>
        new(CrashKind.Barrier, level, GrazeKind.None, start, 1, new Vector3(Barrier - 0.05f, y, 0f),
            -Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, ShipVelocity, 0f);

    private static CollisionFx Create(int seed = 1, float quality = 1f)
    {
        var fx = new CollisionFx(CollisionFxSettings.Default, new Random(seed));
        fx.SetQuality(quality);
        return fx;
    }

    private static int Embers(SparkField field)
    {
        int n = 0;
        for (int i = 0; i < field.Count; i++)
            if (field[i].Ember)
                n++;
        return n;
    }

    // 1
    [Theory]
    [InlineData(ImpactLevel.Medium, 55, 8)]
    [InlineData(ImpactLevel.Strong, 130, 30)]
    public void Barrier_hits_in_high_give_the_table_sparks_and_embers(ImpactLevel level, int sparks, int embers)
    {
        var fx = Create();
        fx.Crash(BarrierHit(level, true), 1f / 60f);
        Assert.Equal(sparks + embers, fx.Sparks.Count);
        Assert.Equal(embers, Embers(fx.Sparks));
    }

    // 2
    [Theory]
    [InlineData(60)]
    [InlineData(30)]
    public void Barrier_graze_gives_140_sparks_per_second_whatever_the_frame_rate(int fps)
    {
        var fx = Create();
        float dt = 1f / fps;
        for (int i = 0; i < fps; i++)
            fx.Crash(BarrierHit(ImpactLevel.Graze, false), dt);
        Assert.InRange(fx.Sparks.Count, 139, 141);
    }

    // 3
    [Fact]
    public void Low_quarters_the_sparks_and_never_gives_zero_on_a_hit()
    {
        var fx = Create(quality: 0.25f);
        fx.Crash(BarrierHit(ImpactLevel.Medium, true), 1f / 60f);
        Assert.Equal(14 + 2, fx.Sparks.Count);
        Assert.Equal(2, Embers(fx.Sparks));

        var settings = CollisionFxSettings.Default;
        settings.BarrierMedium.Count = 1;
        settings.BarrierMedium.Embers = 0;
        var tiny = new CollisionFx(settings, new Random(1));
        tiny.SetQuality(0.25f);
        tiny.Crash(BarrierHit(ImpactLevel.Medium, true), 1f / 60f);
        Assert.Equal(1, tiny.Sparks.Count);
    }

    // 4
    [Fact]
    public void A_full_pool_takes_no_new_sparks_and_keeps_the_live_ones()
    {
        var settings = CollisionFxSettings.Default;
        settings.MaxSparks = 100;
        var fx = new CollisionFx(settings, new Random(1));
        fx.Crash(BarrierHit(ImpactLevel.Strong, true), 1f / 60f);
        Assert.Equal(100, fx.Sparks.Count);
        Spark first = fx.Sparks[0];
        fx.Crash(BarrierHit(ImpactLevel.Medium, true), 1f / 60f);
        Assert.Equal(100, fx.Sparks.Count);
        Assert.Equal(first.Position, fx.Sparks[0].Position);
        Assert.Equal(first.Velocity, fx.Sparks[0].Velocity);
    }

    // 5 (la maqueta añade ±0,45 de desorden a la componente hacia la normal, así que una chispa puede salir algo
    // hacia la barrera; lo que se comprueba es que la media va hacia dentro y que ninguna queda fuera tras un paso)
    [Fact]
    public void Barrier_sparks_go_inwards_and_inherit_half_the_ship_speed()
    {
        var settings = CollisionFxSettings.Default;
        settings.BarrierStrong.SpeedMin = settings.BarrierStrong.SpeedMax = 0f;
        settings.EmberSpeedMin = settings.EmberSpeedMax = 0f;
        settings.HighSparkFraction = 0f;
        var still = new CollisionFx(settings, new Random(3));
        still.Crash(BarrierHit(ImpactLevel.Strong, true), 1f / 60f);
        for (int i = 0; i < still.Sparks.Count; i++)
            Assert.Equal(ShipVelocity * 0.5f, still.Sparks[i].Velocity);

        var fx = Create(3);
        fx.Crash(BarrierHit(ImpactLevel.Strong, true), 1f / 60f);
        float inward = 0f;
        for (int i = 0; i < fx.Sparks.Count; i++)
            inward += Vector3.Dot(fx.Sparks[i].Velocity - ShipVelocity * 0.5f, -Vector3.UnitX);
        Assert.True(inward / fx.Sparks.Count > 0f);

        fx.Update(1f / 60f, new FlatCrashGround(Barrier));
        for (int i = 0; i < fx.Sparks.Count; i++)
            Assert.True(fx.Sparks[i].Position.X <= Barrier + 1e-4f);
    }

    // 6
    [Fact]
    public void A_spark_dies_at_its_life_and_a_zero_step_changes_nothing()
    {
        var field = new SparkField(CollisionFxSettings.Default);
        field.Add(new Spark { Position = new Vector3(0f, 5f, 0f), Velocity = new Vector3(1f, 2f, 3f), Life = 0.5f });
        field.Update(0f, new FlatCrashGround(Barrier));
        Assert.Equal(1, field.Count);
        Assert.Equal(new Vector3(0f, 5f, 0f), field[0].Position);
        Assert.Equal(new Vector3(1f, 2f, 3f), field[0].Velocity);
        Assert.Equal(0f, field[0].Age);

        field.Update(0.25f, null);
        Assert.Equal(1, field.Count);
        field.Update(0.25f, null);
        Assert.Equal(0, field.Count);
    }

    // 7
    [Fact]
    public void Sparks_bounce_on_the_ground_and_on_the_barrier()
    {
        var s = CollisionFxSettings.Default;
        s.AirDamping = 0f;
        s.Gravity = 0f;
        var ground = new FlatCrashGround(Barrier);

        var field = new SparkField(s);
        field.Add(new Spark { Position = new Vector3(0f, 0.05f, 0f), Velocity = new Vector3(0f, -10f, -4f), Life = 1f });
        field.Update(0.01f, ground);
        Assert.Equal(3f, field[0].Velocity.Y, 4);
        Assert.Equal(-4f * 0.6f, field[0].Velocity.Z, 4);
        Assert.True(field[0].Position.Y >= s.GroundClearance - 1e-5f);

        var wall = new SparkField(s);
        wall.Add(new Spark { Position = new Vector3(Barrier - 0.05f, 1f, 0f), Velocity = new Vector3(10f, 0f, 0f), Life = 1f });
        wall.Update(0.01f, ground);
        Assert.Equal(Barrier, wall[0].Position.X, 4);
        Assert.Equal(-3f, wall[0].Velocity.X, 4);
    }

    // 8
    [Fact]
    public void Spark_colour_goes_from_warm_white_to_orange_and_fades_out()
    {
        var s = CollisionFxSettings.Default;
        Assert.Equal(new Vector3(1f, 0.95f, 0.82f), SparkField.ColorAt(0f, false, s));
        Vector3 orange = new Vector3(1f, 0.6f, 0.18f) * MathF.Pow(0.7f, 1.1f);
        Vector3 at = SparkField.ColorAt(0.3f, false, s);
        Assert.Equal(orange.X, at.X, 4);
        Assert.Equal(orange.Y, at.Y, 4);
        Assert.Equal(orange.Z, at.Z, 4);
        Assert.Equal(Vector3.Zero, SparkField.ColorAt(1f, false, s));
        Assert.Equal(Vector3.Zero, SparkField.ColorAt(1f, true, s));
    }

    // 9
    [Fact]
    public void Streak_length_follows_relative_speed_up_to_its_limit()
    {
        var s = CollisionFxSettings.Default;
        Vector3 v = new(3f, 1f, -60f);
        Assert.Equal(Vector3.Zero, SparkField.Tail(v, v, s));
        Assert.Equal(30f * 0.03f, SparkField.Tail(new Vector3(0f, 0f, -30f), Vector3.Zero, s).Length(), 4);
        Vector3 tail = SparkField.Tail(new Vector3(0f, 0f, -500f), Vector3.Zero, s);
        Assert.Equal(2.2f, tail.Length(), 4);
        Assert.True(tail.Z > 0f);
    }

    // 10
    [Fact]
    public void Same_seed_same_sparks()
    {
        var a = Create(42);
        var b = Create(42);
        a.Crash(BarrierHit(ImpactLevel.Strong, true), 1f / 60f);
        b.Crash(BarrierHit(ImpactLevel.Strong, true), 1f / 60f);
        Assert.Equal(a.Sparks.Count, b.Sparks.Count);
        for (int i = 0; i < a.Sparks.Count; i++)
        {
            Assert.Equal(a.Sparks[i].Position, b.Sparks[i].Position);
            Assert.Equal(a.Sparks[i].Velocity, b.Sparks[i].Velocity);
            Assert.Equal(a.Sparks[i].Life, b.Sparks[i].Life);
        }
    }

    // 11
    [Fact]
    public void Flash_intensity_and_strong_ring()
    {
        var fx = Create();
        CrashFlash medium = fx.Flashes.Add(BarrierHit(ImpactLevel.Medium, true), ImpactLevel.Medium, 1f);
        Assert.Equal(1f, CrashFlashes.Intensity(medium), 4);
        medium.Age = 0.16f;
        Assert.Equal(0f, CrashFlashes.Intensity(medium), 4);

        CrashFlash strong = fx.Flashes.Add(BarrierHit(ImpactLevel.Strong, true), ImpactLevel.Strong, 1f);
        Assert.True(strong.Ring);
        Assert.Equal(0.5f, fx.Flashes.RingRadius(strong), 4);
        strong.Age = 0.4f;
        Assert.Equal(7f, fx.Flashes.RingRadius(strong), 4);
        Assert.Equal(0f, fx.Flashes.RingStrength(strong), 4);
        Assert.False(medium.Ring);
    }

    // 12
    [Fact]
    public void A_ninth_flash_drops_the_oldest()
    {
        var fx = Create();
        CrashFlash first = fx.Flashes.Add(BarrierHit(ImpactLevel.Medium, true), ImpactLevel.Medium, 1f);
        for (int i = 0; i < 8; i++)
            fx.Flashes.Add(BarrierHit(ImpactLevel.Medium, true), ImpactLevel.Medium, 1f);
        Assert.Equal(8, fx.Flashes.All.Count);
        Assert.DoesNotContain(first, fx.Flashes.All);
    }

    // 13 (la spec da 2 bocanadas en LOW con un fuerte; con el redondeo de la maqueta, 10 × 0,25 = 2,5 → 3)
    [Fact]
    public void Smoke_puffs_by_level_and_quality()
    {
        var fx = Create();
        Assert.Equal(0, fx.Smoke.Emit(BarrierHit(ImpactLevel.Graze, false), 1f));
        Assert.Equal(2, fx.Smoke.Emit(BarrierHit(ImpactLevel.Medium, true), 1f));
        Assert.Equal(10, fx.Smoke.Emit(BarrierHit(ImpactLevel.Strong, true), 1f));
        Assert.Equal(1, fx.Smoke.CountFor(ImpactLevel.Medium, 0.25f));
        Assert.Equal(3, fx.Smoke.CountFor(ImpactLevel.Strong, 0.25f));

        var settings = CollisionFxSettings.Default;
        settings.SmokeStrong.Count = 4;
        var low = new CollisionFx(settings, new Random(1));
        Assert.Equal(2, low.Smoke.CountFor(ImpactLevel.Strong, 0.25f));
    }

    // 14
    [Fact]
    public void A_delayed_puff_waits_and_its_radius_grows_fast_first()
    {
        var fx = Create();
        var puff = new SmokePuff { Position = Vector3.Zero, Age = -0.2f, Life = 1f, StartRadius = 0.4f, EndRadius = 1.6f };
        Assert.False(CrashSmoke.IsVisible(puff));
        Assert.Equal(0f, fx.Smoke.Opacity(puff));
        puff.Age = 0.5f;
        float r = CrashSmoke.Radius(puff);
        Assert.True(r - 0.4f > 1.6f - r);
    }

    // Lo de 14 dentro de la simulación: una bocanada con retraso no se mueve hasta que pasa
    [Fact]
    public void Delayed_puffs_do_not_move_while_waiting()
    {
        var fx = Create();
        fx.Smoke.Emit(BarrierHit(ImpactLevel.Strong, true), 1f);
        int waiting = -1;
        for (int i = 0; i < fx.Smoke.Count; i++)
            if (fx.Smoke[i].Age < -0.05f)
                waiting = i;
        Assert.True(waiting >= 0);
        Vector3 before = fx.Smoke[waiting].Position;
        fx.Smoke.Update(0.01f);
        Assert.Equal(before, fx.Smoke[waiting].Position);
    }

    // PARTICLES en plena carrera: las vivas siguen; las que sobran del máximo nuevo, fuera
    [Fact]
    public void Lowering_quality_trims_the_extra_sparks()
    {
        var fx = Create();
        for (int i = 0; i < 10; i++)
            fx.Crash(BarrierHit(ImpactLevel.Strong, true), 1f / 60f);
        Assert.Equal(1600, fx.Sparks.Count);
        fx.SetQuality(0.25f);
        Assert.Equal(750, fx.Sparks.Count);
    }

    // El punto de contacto con la barrera: en el lado golpeado, a 0,75 m sobre la pista y, con el valor de la spec, 1,3 m
    // hacia la popa (por defecto va adelantado, para que se vea desde la cabina)
    [Fact]
    public void Barrier_contact_point_sits_on_the_hit_barrier_behind_the_ship_centre()
    {
        var frame = new TrackFrame(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -500f), new Vector3(100f, 0f, -500f), new Vector3(100f, 0f, 0f) });
        var s = CollisionFxSettings.Default;
        s.BarrierContactAhead = -1.3f;
        CrashEvent? e = CrashEvent.Barrier(frame, 20.5f, 3, BarrierSide.Left, ImpactLevel.Medium, true, 100f,
            frame.At(100f).PlanTangent is var t ? new Vector2(t.X, t.Z) : default, new Vector2(0f, -50f), s);
        Assert.NotNull(e);
        FramePoint expected = frame.At(100f - 1.3f);
        Vector3 point = expected.PointAt(-(20.5f - 0.05f), 0.75f);
        Assert.Equal(point.X, e.Value.Point.X, 3);
        Assert.Equal(point.Y, e.Value.Point.Y, 3);
        Assert.Equal(point.Z, e.Value.Point.Z, 3);
        Assert.True(Vector3.Dot(e.Value.Normal, expected.Right) > 0.99f);
        Assert.True(e.Value.IsStart);

        CrashEvent? graze = CrashEvent.Barrier(frame, 20.5f, 3, BarrierSide.Left, ImpactLevel.Medium, false, 100f,
            Vector2.UnitY, Vector2.Zero, s);
        Assert.False(graze!.Value.IsStart);
        Assert.Equal(ImpactLevel.Graze, graze.Value.Level);
    }

    // Adelantado hacia el morro: con la nave yendo hacia delante por la pista, el punto va por delante de su centro
    [Fact]
    public void Barrier_contact_point_moves_ahead_with_a_positive_offset()
    {
        var frame = new TrackFrame(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -500f), new Vector3(100f, 0f, -500f), new Vector3(100f, 0f, 0f) });
        var s = CollisionFxSettings.Default;
        s.BarrierContactAhead = 6f;
        Vector3 t = frame.At(100f).PlanTangent;
        CrashEvent? e = CrashEvent.Barrier(frame, 20.5f, 0, BarrierSide.Right, ImpactLevel.Graze, false, 100f,
            new Vector2(t.X, t.Z), Vector2.Zero, s);
        Assert.Equal(106f, e!.Value.AxisDistance, 2);
    }
}
