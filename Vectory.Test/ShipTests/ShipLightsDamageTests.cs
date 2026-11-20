using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Luces de la nave, etapa 4 (spec docs/efectos-8-luces.md, 14, pruebas 12 a 17): parpadeo con daño nuevo, pausa,
// recolocación y nave atravesable, con las luces reales de los manifiestos
public class ShipLightsDamageTests
{
    private static readonly ShipLightSettings S = ShipLightSettings.Default;
    private const float Dt = 1f / 60f;

    private static List<ShipLightDef> Lights(string ship) => ShipLightCatalog.From(ship switch
    {
        "vector" => Hull3DTests.Manifest,
        "blade" => Hull3DBladeTests.Manifest,
        _ => Hull3DHammerTests.Manifest
    });

    private static ShipLightDamage Damage(string ship, int seed = 1, int random = 5) =>
        new(Lights(ship), S, seed, new Random(random));

    private static void Run(ShipLightDamage damage, int level, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
            damage.Update(Dt, level);
    }

    // ---------- 12. Daño 0 ----------

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void Level0_KeepsEveryLightAt1(string ship)
    {
        ShipLightDamage damage = Damage(ship);
        for (float t = 0f; t < 10f; t += Dt)
        {
            damage.Update(Dt, 0);
            for (int i = 0; i < Lights(ship).Count; i++)
                Assert.Equal(1f, damage.Factor(i));
            Assert.Equal(0, damage.SparkCount);
        }
    }

    // ---------- 13. Muertas ----------

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void Level3_Kills30PercentOfTheFlickeringLights_AlwaysTheSame(string ship)
    {
        List<ShipLightDef> defs = Lights(ship);
        int flickering = defs.Count(d => ShipLightTypes.Flickers(d.Type));
        ShipLightDamage a = Damage(ship, seed: 4, random: 1), b = Damage(ship, seed: 4, random: 99);
        int[] deadA = Enumerable.Range(0, defs.Count).Where(a.IsDead).ToArray();
        int[] deadB = Enumerable.Range(0, defs.Count).Where(b.IsDead).ToArray();
        Assert.Equal((int)MathF.Round(flickering * 0.3f, MidpointRounding.AwayFromZero), deadA.Length);
        Assert.Equal(deadA, deadB);   // dos carreras: las mismas
        Assert.All(deadA, i => Assert.True(ShipLightTypes.Flickers(defs[i].Type)));
    }

    [Fact]
    public void DeadLights_AreOffAndReviveForAMomentWithASpark()
    {
        List<ShipLightDef> defs = Lights("hammer");
        ShipLightDamage damage = Damage("hammer");
        int dead = Enumerable.Range(0, defs.Count).First(damage.IsDead);
        bool revived = false, spark = false, off = false;
        for (float t = 0f; t < 8f; t += Dt)
        {
            damage.Update(Dt, 3);
            float f = damage.Factor(dead);
            off |= f == 0f;
            revived |= MathF.Abs(f - 1.3f) < 1e-4f;
            for (int k = 0; k < damage.SparkCount; k++)
                spark |= damage.Spark(k) == dead;
            Assert.True(f == 0f || MathF.Abs(f - 1.3f) < 1e-4f, $"{f}");
        }
        Assert.True(off && revived && spark);
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void NeverFlicker_StrobesBeaconsNozzlesHeadlightsAndRetroMouths(string ship)
    {
        List<ShipLightDef> defs = Lights(ship);
        ShipLightDamage damage = Damage(ship);
        for (float t = 0f; t < 10f; t += Dt)
        {
            damage.Update(Dt, 3);
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].Type is ShipLightType.Strobe or ShipLightType.Beacon or ShipLightType.Head or ShipLightType.Retro)
                {
                    Assert.Equal(1f, damage.Factor(i));
                    Assert.False(damage.IsDead(i));
                }
        }
        Assert.DoesNotContain(defs, d => d.Name.StartsWith("Nozzle_"));
    }

    // ---------- 14. Apagones ----------

    [Theory]
    [InlineData(1, 0.07f, 0.16f)]
    [InlineData(2, 0.10f, 0.32f)]
    [InlineData(3, 0.12f, 0.50f)]
    public void Blackouts_LastWithinTheRangeOfTheirLevel_ThenStutter(int level, float min, float max)
    {
        List<ShipLightDef> defs = Lights("vector");
        ShipLightDamage damage = Damage("vector", seed: 2, random: level);
        int light = Enumerable.Range(0, defs.Count).First(i => ShipLightTypes.Flickers(defs[i].Type) && !damage.IsDead(i));
        float off = 0f;
        int blackouts = 0, sparks = 0;
        var stutter = new List<float>();
        bool inStutter = false;
        for (float t = 0f; t < 60f; t += Dt)
        {
            damage.Update(Dt, level);
            float f = damage.Factor(light);
            for (int k = 0; k < damage.SparkCount; k++)
                if (damage.Spark(k) == light)
                    sparks++;
            if (f == 0f)
            {
                off += Dt;
                continue;
            }
            if (off > 0f)
            {
                // acaba un apagón: duró entre lo sorteado (± un paso) y vuelve tartamudeando
                Assert.InRange(off, min - Dt, max + Dt);
                blackouts++;
                off = 0f;
                inStutter = true;
                stutter.Clear();
            }
            if (inStutter)
            {
                if (f == S.StutterLow || f == S.StutterHigh)
                {
                    stutter.Add(f);
                    continue;
                }
                Assert.InRange(stutter.Count * Dt, S.StutterTime - 2 * Dt, S.StutterTime + 2 * Dt);
                Assert.Contains(S.StutterLow, stutter);
                Assert.Contains(S.StutterHigh, stutter);
                inStutter = false;
            }
        }
        Assert.True(blackouts > 3, $"{blackouts}");
        if (level >= 2)
            Assert.Equal(blackouts, sparks, 1f);
        else
            Assert.Equal(0, sparks);
    }

    [Fact]
    public void Level2_HumsOutsideTheBlackouts()
    {
        List<ShipLightDef> defs = Lights("blade");
        ShipLightDamage damage = Damage("blade");
        damage.Update(Dt, 2);
        int light = Enumerable.Range(0, defs.Count).First(i => ShipLightTypes.Flickers(defs[i].Type) && damage.Factor(i) > 0.5f);
        Assert.InRange(damage.Factor(light), 0.76f, 1.0f);
    }

    [Fact]
    public void Averages_FollowTheSideOfTheLightsThatGoOut()
    {
        ShipLightDamage damage = Damage("hammer");
        Run(damage, 3, 1f);
        Assert.InRange(damage.Average, 0f, 1f);
        Assert.True(damage.Average < 1f);
        Assert.InRange(damage.SideAverage(-1), 0f, 1f);
        Assert.InRange(damage.BrakeAverage, 0f, 1f);
    }

    // ---------- 15. TIME TRIAL ----------

    [Fact]
    public void TimeTrial_NoFlickerAndNoSparks_AtAnyLevel()
    {
        var state = new ShipLightState(Lights("vector"), S, 3, new Random(3));
        for (float t = 0f; t < 10f; t += Dt)
        {
            state.Update(Dt, new ShipLightInput { Speed = 50f, TopSpeed = 100f, DamageLevel = 3, TimeTrial = true });
            for (int i = 0; i < state.Count; i++)
                Assert.Equal(1f, state.Damage.Factor(i));
            Assert.Equal(0, state.Damage.SparkCount);
        }
    }

    // ---------- 16. Pausa ----------

    [Fact]
    public void Pause_WithoutDelta_NothingMoves()
    {
        var state = new ShipLightState(Lights("hammer"), S, 2, new Random(2));
        var input = new ShipLightInput { Braking = true, Speed = 80f, TopSpeed = 100f, Steer = 1f, DamageLevel = 3 };
        state.Update(Dt, input);
        float[] before = Enumerable.Range(0, state.Count).Select(state.Damage.Factor).ToArray();
        float brake = state.Brake, turn = state.Turn;
        for (int k = 0; k < 100; k++)
            state.Update(0f, input with { Steer = -1f });
        Assert.Equal(before, Enumerable.Range(0, state.Count).Select(state.Damage.Factor).ToArray());
        Assert.Equal(brake, state.Brake);
        Assert.Equal(turn, state.Turn);
        Assert.Equal(0, state.Damage.SparkCount);
    }

    // ---------- 17. Recolocación ----------

    [Fact]
    public void Reset_ClearsBlackoutsAndSparks_ButTheDeadStayDead()
    {
        List<ShipLightDef> defs = Lights("vector");
        var state = new ShipLightState(defs, S, 5, new Random(5));
        var input = new ShipLightInput { Braking = true, Speed = 80f, TopSpeed = 100f, Steer = 1f, DamageLevel = 3 };
        for (float t = 0f; t < 3f; t += Dt)
            state.Update(Dt, input);
        state.Reset();
        Assert.Equal(0f, state.Brake);
        Assert.Equal(0f, state.Turn);
        Assert.Equal(0, state.Damage.SparkCount);
        for (int i = 0; i < defs.Count; i++)
            if (!state.Damage.IsDead(i))
                Assert.Equal(1f, state.Damage.Factor(i));
    }

    // ---------- Atravesable (11) ----------

    [Fact]
    public void Passable_TurnsOffTrackLightsHullBrakeAndHeadlights()
    {
        var state = new ShipLightState(Lights("vector"), S, 1, new Random(1));
        state.Update(Dt, new ShipLightInput { Speed = 50f, TopSpeed = 100f, Passable = true });
        Assert.Equal(0f, state.TrackLeft);
        Assert.Equal(0f, state.TrackRear);
        Assert.Equal(0f, state.HullBrakeEnergy);
        Assert.Equal(0f, state.HeadlightFactor);
        Assert.True(state.Intensity(0) > 0f);   // las piezas siguen (las oculta el parpadeo del casco)
    }
}
