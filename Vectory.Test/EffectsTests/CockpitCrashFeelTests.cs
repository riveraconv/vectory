using System.Numerics;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Effects;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Cómo se nota un choque desde la cabina (spec docs/efectos-1-choques.md, 14): luz por lado, temblor y chispas altas
public class CockpitCrashFeelTests
{
    private const float Dt = 1f / 60f;

    private static CockpitCrashFeel Create() => new(CockpitCrashFeelSettings.Default, new Random(1));

    [Fact]
    public void Grazing_lights_only_its_side_flickering_between_its_limits()
    {
        var feel = Create();
        var seen = new HashSet<float>();
        for (int i = 0; i < 30; i++)
        {
            feel.Contact(BarrierSide.Right, ImpactLevel.Graze, false);
            feel.Step(Dt);
            float light = feel.Light(BarrierSide.Right);
            Assert.InRange(light, 0.5f, 1f);
            Assert.Equal(0f, feel.Light(BarrierSide.Left));
            seen.Add(light);
        }
        Assert.True(seen.Count > 5);
    }

    [Fact]
    public void The_graze_light_fades_out_after_leaving_the_barrier()
    {
        var feel = Create();
        feel.Contact(BarrierSide.Left, ImpactLevel.Graze, false);
        feel.Step(Dt);
        Assert.True(feel.Light(BarrierSide.Left) > 0f);
        for (int i = 0; i < 8; i++)
            feel.Step(Dt);
        Assert.Equal(0f, feel.Light(BarrierSide.Left));
    }

    [Theory]
    [InlineData(ImpactLevel.Medium, 1.8f, 0.3f)]
    [InlineData(ImpactLevel.Strong, 2.6f, 0.45f)]
    public void A_hit_flashes_and_dies_out_in_its_duration(ImpactLevel level, float peak, float duration)
    {
        var feel = Create();
        feel.Contact(BarrierSide.Right, level, true);
        feel.Step(0.001f);
        Assert.InRange(feel.Light(BarrierSide.Right), peak * 0.98f, peak);
        feel.Step(duration);
        Assert.Equal(0f, feel.Light(BarrierSide.Right));
    }

    [Fact]
    public void Rumble_only_while_grazing_and_scaled_by_cockpit_shake()
    {
        var feel = Create();
        Assert.Equal(Vector2.Zero, feel.RumbleOffset());
        for (int i = 0; i < 20; i++)
        {
            feel.Contact(BarrierSide.Right, ImpactLevel.Graze, false);
            feel.Step(Dt);
        }
        Assert.True(feel.Rumble > 0.95f);
        Vector2 offset = feel.RumbleOffset();
        Assert.True(offset.Length() > 0f);
        Assert.True(MathF.Abs(offset.X) <= 0.012f + 1e-6f && MathF.Abs(offset.Y) <= 0.012f + 1e-6f);
        Assert.Equal(Vector2.Zero, feel.RumbleOffset(0f));

        for (int i = 0; i < 30; i++)
            feel.Step(Dt);
        Assert.True(feel.Rumble < 0.01f);
    }

    [Fact]
    public void A_hit_alone_does_not_rumble_and_a_paused_step_changes_nothing()
    {
        var feel = Create();
        feel.Contact(BarrierSide.Left, ImpactLevel.Strong, true);
        feel.Step(Dt);
        Assert.Equal(0f, feel.Rumble);
        float light = feel.Light(BarrierSide.Left);
        feel.Step(0f);
        Assert.Equal(light, feel.Light(BarrierSide.Left));
    }

    [Fact]
    public void Some_barrier_sparks_jump_higher()
    {
        var settings = CollisionFxSettings.Default;
        settings.HighSparkFraction = 1f;
        settings.BarrierDirection.UpMin = settings.BarrierDirection.UpMax = 0f;
        settings.BarrierDirection.RandomUp = 0f;
        var fx = new CollisionFx(settings, new Random(1));
        var hit = new CrashEvent(CrashKind.Barrier, ImpactLevel.Medium, GrazeKind.None, true, 1, new Vector3(19.95f, 0.75f, 0f),
            -Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, 0f);
        fx.Crash(hit, Dt);
        for (int i = 0; i < fx.Sparks.Count; i++)
            if (!fx.Sparks[i].Ember)
                Assert.True(fx.Sparks[i].Velocity.Y >= settings.HighSparkUpMin - 1e-4f);

        settings.HighSparkFraction = 0f;
        var low = new CollisionFx(settings, new Random(1));
        low.Crash(hit, Dt);
        for (int i = 0; i < low.Sparks.Count; i++)
            Assert.True(low.Sparks[i].Velocity.Y < settings.HighSparkUpMin);
    }

    // Roce por detrás: temblor a la mitad, sin luz en ningún sitio
    [Fact]
    public void A_rear_graze_only_rumbles_at_half()
    {
        var feel = Create();
        for (int i = 0; i < 30; i++)
        {
            feel.RearGraze();
            feel.Step(Dt);
        }
        Assert.InRange(feel.Rumble, 0.48f, 0.5f);
        foreach (CrashLightSide side in Enum.GetValues<CrashLightSide>())
            Assert.Equal(0f, feel.Light(side));
    }

    // Golpe por detrás: el fogonazo va detrás (o delante), no a los lados, y no tiembla
    [Theory]
    [InlineData(CrashLightSide.Rear)]
    [InlineData(CrashLightSide.Front)]
    public void A_rear_hit_lights_only_its_end(CrashLightSide end)
    {
        var feel = Create();
        feel.Contact(end, ImpactLevel.Strong, true);
        feel.Step(0.001f);
        Assert.True(feel.Light(end) > 2.5f);
        foreach (CrashLightSide side in Enum.GetValues<CrashLightSide>())
            if (side != end)
                Assert.Equal(0f, feel.Light(side));
        Assert.Equal(0f, feel.Rumble);
    }
}
