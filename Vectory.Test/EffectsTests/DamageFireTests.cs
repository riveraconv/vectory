using System;
using System.Linq;
using System.Numerics;
using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Fuego, tinte y brasas del humo de daños (spec docs/efectos-4-humo-danos.md, pruebas 10–13)
public class DamageFireTests
{
    private const float Kmh = 1f / 3.6f;
    private static readonly DamageSmokeSettings S = DamageSmokeSettings.Default;
    private static readonly DamageSmokeFoci Foci = ShipDefinitions.Find(ShipCatalog.Ship01).Visual.SmokeFoci;
    private static readonly ShipPose Pose = ShipPose.Flat(new Vector3(0f, 2f, 0f));
    private static readonly Vector3 Velocity = new(0f, 0f, -300f * Kmh);

    private static DamageEmbers Embers(int seed = 1) => new(DamageSmokeSettings.Default, new Random(seed));

    private static int EmitFor(DamageEmbers embers, int level, float seconds, float dt, float quality = 1f, int ship = 0)
    {
        int total = 0;
        for (int i = 0; i < (int)MathF.Round(seconds / dt); i++)
            total += embers.Emit(ship, level, Foci, Pose, Velocity, quality, dt);
        return total;
    }

    // Sin llamarada en ese momento (para comprobar el resto sin ella)
    private static float CalmTime(int focus)
    {
        float t = 0f;
        while (DamageFire.Flare(focus, t, S.Fire) > 0f)
            t += 0.05f;
        return t;
    }

    [Fact]
    public void Fire_only_at_level_3_with_one_flame_and_a_halo_per_focus()
    {
        Assert.False(DamageFire.Burns(0));
        Assert.False(DamageFire.Burns(2));
        Assert.True(DamageFire.Burns(3));
        Assert.Equal(3, DamageSmokeFocusRules.Count(3));

        // Parada y sin llamarada: largo entre 0,85 y 1,15 veces el suyo, ancho y tamaño del foco
        float t = CalmTime(0);
        FireSprite flame = DamageFire.Flame(0, 1f, t, 0f, S.Fire);
        Assert.Equal(0f, flame.Lean);
        Assert.InRange(flame.Length, 0.40f * 0.85f - 1e-4f, 0.40f * 1.15f + 1e-4f);
        Assert.Equal(0.22f, flame.Width, 4);
        Assert.Equal(t * S.Fire.NoiseSpeed, flame.NoiseTime, 4);
        FireSprite small = DamageFire.Flame(1, 0.75f, CalmTime(1), 0f, S.Fire);
        Assert.Equal(0.22f * 0.75f, small.Width, 4);
        Assert.NotEqual(flame.Seed, small.Seed);

        // El halo, pequeño y tenue
        FireSprite halo = DamageFire.Halo(2, 0.75f, CalmTime(2), S.Fire);
        Assert.Equal(0.8f * 0.75f, halo.Width, 4);
        Assert.InRange(halo.Opacity, 0.12f * 0.5f, 0.12f);
        Assert.Equal(0.12f * 0.75f, halo.Rise, 4);
        Assert.NotEqual(DamageFire.Phase(1, S.Fire), DamageFire.Phase(2, S.Fire));
    }

    [Fact]
    public void Wind_leans_the_flame_back_and_stretches_it_with_speed()
    {
        Assert.Equal(0f, DamageFire.Lean(0f, S.Fire));
        Assert.Equal(1f, DamageFire.Lean(55f, S.Fire), 4);
        Assert.Equal(1f, DamageFire.Lean(110f, S.Fire), 4);
        Assert.InRange(DamageFire.Lean(27.5f, S.Fire), 0.49f, 0.51f);

        var back = new Vector3(0f, 0f, 1f);
        Assert.Equal(Vector3.UnitY, DamageFire.Axis(back, 0f, S.Fire));
        Vector3 full = DamageFire.Axis(back, 1f, S.Fire);
        Assert.Equal(1f, full.Length(), 4);
        Assert.True(full.Z > 0.9f && full.Y > 0.2f);   // casi hacia atrás, subiendo un poco

        float t = CalmTime(0);
        FireSprite still = DamageFire.Flame(0, 1f, t, 0f, S.Fire);
        FireSprite fast = DamageFire.Flame(0, 1f, t, 300f / 3.6f, S.Fire);
        Assert.Equal(1f, fast.Lean, 4);
        Assert.Equal(still.Length * 2f, fast.Length, 3);
        Assert.Equal(still.Width * 0.8f, fast.Width, 4);
        Assert.True(DamageFire.Flame(0, 1f, 1f, 300f / 3.6f, S.Fire).NoiseTime > DamageFire.Flame(0, 1f, 1f, 0f, S.Fire).NoiseTime);
    }

    [Fact]
    public void Flares_come_now_and_then_and_make_the_flame_bigger()
    {
        // En 60 s, cada foco tiene llamaradas (más o menos 55 % de los tramos de 3 s), cortas y de 0 a 1
        int flares = 0;
        bool inFlare = false;
        float peak = 0f;
        for (float t = 0f; t < 60f; t += 0.01f)
        {
            float f = DamageFire.Flare(1, t, S.Fire);
            Assert.InRange(f, 0f, 1f);
            peak = MathF.Max(peak, f);
            if (f > 0f && !inFlare)
                flares++;
            inFlare = f > 0f;
        }
        Assert.InRange(flares, 5, 17);
        Assert.True(peak > 0.95f);

        // Misma hora, misma llamarada (determinista); los focos no van a la vez
        Assert.Equal(DamageFire.Flare(0, 12.34f, S.Fire), DamageFire.Flare(0, 12.34f, S.Fire));
        bool differ = false;
        for (float t = 0f; t < 60f && !differ; t += 0.05f)
            differ = (DamageFire.Flare(0, t, S.Fire) > 0f) != (DamageFire.Flare(2, t, S.Fire) > 0f);
        Assert.True(differ);

        // En el máximo: más larga, más ancha y más brillante
        float tPeak = 0f;
        float best = 0f;
        for (float t = 0f; t < 60f; t += 0.005f)
        {
            float f = DamageFire.Flare(0, t, S.Fire);
            if (f > best)
            {
                best = f;
                tPeak = t;
            }
        }
        FireSprite flaring = DamageFire.Flame(0, 1f, tPeak, 0f, S.Fire);
        Assert.True(flaring.Length > 0.40f * 0.85f * 1.7f);
        Assert.True(flaring.Width > 0.22f * 1.3f);
        Assert.True(flaring.Opacity > 0.9f);
    }

    [Fact]
    public void Flares_alternate_short_and_long()
    {
        for (int slot = 0; slot < 20; slot++)
        {
            float a = DamageFire.FlareDuration(slot, 0, S.Fire);
            float b = DamageFire.FlareDuration(slot + 1, 0, S.Fire);
            float shortOne = MathF.Min(a, b), longOne = MathF.Max(a, b);
            Assert.InRange(shortOne, 0.25f * 0.8f - 1e-4f, 0.25f * 1.2f + 1e-4f);
            Assert.InRange(longOne, 0.6f * 0.8f - 1e-4f, 0.6f * 1.2f + 1e-4f);
            // Focos vecinos, al revés en el mismo tramo
            Assert.True((DamageFire.FlareDuration(slot, 0, S.Fire) < 0.4f) != (DamageFire.FlareDuration(slot, 1, S.Fire) < 0.4f));
        }
    }

    [Fact]
    public void Tint_is_80_percent_orange_at_birth_and_gone_from_30_percent()
    {
        var gray = new Vector3(0.17f, 0.175f, 0.2f);
        Vector3 born = DamageFire.Tint(gray, 0f, S.Fire);
        Assert.Equal(Vector3.Lerp(gray, new Vector3(0.75f, 0.3f, 0.1f), 0.8f), born);
        Assert.Equal(gray, DamageFire.Tint(gray, 0.3f, S.Fire));
        Assert.Equal(gray, DamageFire.Tint(gray, 0.8f, S.Fire));

        // En el humo: nivel 3 con fuego, nivel 2 sin él
        var smoke = new DamageSmoke(S, new Random(1));
        var strong = new DamagePuff { Level = 3, Shade = 1f, Life = 1f, Age = 0f };
        Assert.Equal(Vector3.Lerp(S.Strong.Color, S.Fire.TintColor, 0.8f), smoke.Color(strong));
        strong.Age = 0.5f;
        Assert.Equal(S.Strong.Color, smoke.Color(strong));
        Assert.Equal(S.Medium.Color, smoke.Color(new DamagePuff { Level = 2, Shade = 1f, Life = 1f, Age = 0f }));
    }

    [Theory]
    [InlineData(1f / 60f)]
    [InlineData(1f / 30f)]
    public void Embers_per_second_by_level_and_quality(float dt)
    {
        Assert.Equal(0, EmitFor(Embers(), 1, 1f, dt));
        Assert.InRange(EmitFor(Embers(), 2, 1f, dt), 2, 3);
        Assert.InRange(EmitFor(Embers(), 3, 1f, dt), 14, 15);
        Assert.InRange(EmitFor(Embers(), 3, 1f, dt, 0.25f), 3, 4);
        Assert.Equal(3f, Embers().Rate(2));
        Assert.Equal(15f, Embers().Rate(3));
    }

    [Fact]
    public void Embers_lag_behind_rise_and_fall_slowly()
    {
        var embers = Embers();
        EmitFor(embers, 3, 1f, 1f / 60f);
        float lag = MathF.Min(9f, 0.1f * Velocity.Length());
        for (int i = 0; i < embers.Count; i++)
        {
            DamageEmber e = embers[i];
            Assert.InRange(e.Velocity.Z, Velocity.Z + lag * 0.6f - 1e-3f, Velocity.Z + lag * 1.4f + 1e-3f);
            Assert.InRange(e.Velocity.Y, 1f, 3f);
            Assert.InRange(e.Life, 0.5f, 1f);
        }
        DamageEmber before = embers[0];
        embers.Update(0.1f);
        Assert.True(embers[0].Velocity.Y < before.Velocity.Y);
        Assert.Equal(before.Velocity.Y - 2f * 0.1f, embers[0].Velocity.Y, 3);
        // Con la pausa, nada cambia
        DamageEmber now = embers[0];
        embers.Update(0f);
        Assert.Equal(now.Position, embers[0].Position);
        Assert.Equal(0, embers.Emit(0, 3, Foci, Pose, Velocity, 1f, 0f));
    }

    [Fact]
    public void No_more_than_400_embers()
    {
        var embers = Embers();
        for (int ship = 0; ship < 40; ship++)
            EmitFor(embers, 3, 1f, 1f / 60f, 1f, ship);
        Assert.Equal(400, embers.Count);
    }

    [Fact]
    public void Respawn_clears_puffs_and_embers_of_that_ship_only()
    {
        var smoke = new DamageSmoke(S, new Random(1));
        var embers = Embers();
        for (int i = 0; i < 60; i++)
            foreach (int ship in new[] { 0, 5 })
            {
                smoke.Emit(ship, 3, Foci, Pose, Velocity, 1f, 1f / 60f);
                embers.Emit(ship, 3, Foci, Pose, Velocity, 1f, 1f / 60f);
            }
        smoke.Clear(0);
        embers.Clear(0);
        Assert.True(smoke.Count > 0);
        Assert.True(embers.Count > 0);
        Assert.All(Enumerable.Range(0, smoke.Count), i => Assert.Equal(5, smoke[i].ShipId));
        Assert.All(Enumerable.Range(0, embers.Count), i => Assert.Equal(5, embers[i].ShipId));
    }

    [Fact]
    public void Ember_color_fades_and_its_tail_is_relative_to_your_ship()
    {
        var embers = Embers();
        var e = new DamageEmber { Age = 0f, Life = 1f, Phase = 0f, Velocity = new Vector3(1f, 2f, -80f) };
        Assert.Equal(new Vector3(1f, 0.55f, 0.14f) * 0.7f, embers.ColorOf(e));
        e.Age = 1f;
        Assert.Equal(Vector3.Zero, embers.ColorOf(e));
        Assert.Equal(new Vector3(-1f, -2f, -3f) * 0.04f, embers.Tail(e, new Vector3(0f, 0f, -83f)));
    }
}
