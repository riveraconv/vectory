using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Luces de la nave, etapa 2 (spec docs/efectos-8-luces.md, 14, pruebas 1 a 5): realce, freno y boost, giro y halos,
// con las luces reales de los manifiestos de los tres cascos
public class ShipLightsTests
{
    private static readonly ShipLightSettings S = ShipLightSettings.Default;
    private const float Dt = 1f / 60f;

    private static List<ShipLightDef> Lights(string ship) => ShipLightCatalog.From(ship switch
    {
        "vector" => Hull3DTests.Manifest,
        "blade" => Hull3DBladeTests.Manifest,
        _ => Hull3DHammerTests.Manifest
    });

    private static int Index(ShipLightState state, string name)
    {
        for (int i = 0; i < state.Count; i++)
            if (state.Def(i).Name == name)
                return i;
        throw new ArgumentException(name);
    }

    private static void Run(ShipLightState state, ShipLightInput input, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
            state.Update(Dt, input);
    }

    private static readonly ShipLightInput Rest = new() { Speed = 50f, TopSpeed = 100f };
    private static readonly ShipLightInput Braking = new() { Braking = true, Speed = 50f, TopSpeed = 100f };

    // ---------- Catálogo ----------

    [Theory]
    [InlineData("vector", 17)]
    [InlineData("blade", 18)]
    [InlineData("hammer", 21)]
    public void Catalog_ReadsEveryLightOfTheManifest(string ship, int count)
    {
        List<ShipLightDef> defs = Lights(ship);
        Assert.Equal(count, defs.Count);
        Assert.DoesNotContain(defs, d => d.Name.StartsWith("Nozzle_"));
    }

    [Fact]
    public void Catalog_GivesTheSideByX_AndTheNewBrakeOnlyToLightBrake()
    {
        List<ShipLightDef> hammer = Lights("hammer");
        Assert.Equal(-1, hammer.Single(d => d.Name == "Light_Nose_L").Side);
        Assert.Equal(1, hammer.Single(d => d.Name == "Light_TailBar_R").Side);
        Assert.Equal(0, hammer.Single(d => d.Name == "Light_Brake").Side);
        Assert.Equal(0, Lights("vector").Single(d => d.Name == "Light_Head").Side);
        Assert.Single(hammer, d => d.NewBrake);
        Assert.Single(Lights("blade"), d => d.NewBrake);
        Assert.DoesNotContain(Lights("vector"), d => d.NewBrake);
    }

    // ---------- 1. Realce ----------

    [Theory]
    [InlineData("vector", "Accent_Chine_L", 2.4f)]
    [InlineData("vector", "Light_Nose_R", 1.7f)]
    [InlineData("vector", "Light_Fin_L", 1.7f)]
    [InlineData("vector", "Light_Tail_L", 1.7f)]
    [InlineData("vector", "Light_Head", 1f)]
    [InlineData("blade", "Light_Nav_L", 1.7f)]
    [InlineData("blade", "Accent_FormNose_R", 2.8f)]
    [InlineData("blade", "Light_Tail", 1.5f)]
    [InlineData("hammer", "Light_Canard_R", 1.7f)]
    [InlineData("hammer", "Light_Work_2", 1.8f * 1.6f)]   // con los faros, × 1,6 sobre su realce (7.4)
    [InlineData("hammer", "Light_TailBar_L", 1.7f)]
    public void Enhance_MultipliesTheBaseOfEachType(string ship, string name, float boost)
    {
        var state = new ShipLightState(Lights(ship));
        state.Update(Dt, Rest);
        int i = Index(state, name);
        Assert.Equal(state.Def(i).BaseIntensity * boost, state.Intensity(i), 4);
    }

    [Fact]
    public void Enhance_LeavesStrobesAndBeaconsWithTheirOwnIntensity()
    {
        var blade = new ShipLightState(Lights("blade"));
        int strobe = Index(blade, "Light_Strobe_Top");
        blade.SetExternal(strobe, 6f);
        blade.Update(Dt, Braking);
        Assert.Equal(6f, blade.Intensity(strobe), 4);

        var hammer = new ShipLightState(Lights("hammer"));
        int beacon = Index(hammer, "Beacon_Boost_Lens");
        hammer.SetExternal(beacon, 0.05f);
        hammer.Update(Dt, Braking with { Boost = 1f, Steer = 1f });
        Assert.Equal(0.05f, hammer.Intensity(beacon), 4);
    }

    // ---------- 2. Freno y boost ----------

    [Fact]
    public void Brake_AtFull_MultipliesTheBrakeLightsBy3Point2_AndNothingElse()
    {
        var rest = new ShipLightState(Lights("vector"));
        rest.Update(Dt, Rest);
        var state = new ShipLightState(Lights("vector"));
        Run(state, Braking, 2f);
        Assert.Equal(1f, state.Brake, 3);
        for (int i = 0; i < state.Count; i++)
        {
            float expected = rest.Intensity(i) * (state.Def(i).Type == ShipLightType.Brake ? 3.2f : 1f);
            Assert.Equal(expected, state.Intensity(i), 0.002f * expected + 1e-4f);
        }
    }

    [Fact]
    public void Boost_TurnsTheBrakeLightsOrange_AndTimes2Point4()
    {
        var state = new ShipLightState(Lights("hammer"));
        state.Update(Dt, Rest with { Boost = 1f });
        int i = Index(state, "Light_Tail_L");
        Assert.Equal(state.Def(i).BaseIntensity * 1.7f * 2.4f, state.Intensity(i), 3);
        Assert.Equal(new Vector3(1f, 0x8A / 255f, 0x1E / 255f), state.Color(i));
        int nose = Index(state, "Light_Nose_L");
        Assert.Equal(state.Def(nose).BaseColor, state.Color(nose));
    }

    [Fact]
    public void NewBrakeLights_RestAt35Percent_TheOldOnesDoNot()
    {
        var blade = new ShipLightState(Lights("blade"));
        blade.Update(Dt, Rest);
        int brake = Index(blade, "Light_Brake");
        Assert.Equal(blade.Def(brake).BaseIntensity * 1.7f * 0.35f, blade.Intensity(brake), 4);
        int tail = Index(blade, "Light_Tail");
        Assert.Equal(blade.Def(tail).BaseIntensity * 1.5f, blade.Intensity(tail), 4);
        Run(blade, Braking, 2f);
        Assert.Equal(blade.Def(tail).BaseIntensity * 1.5f, blade.Intensity(tail), 4);   // la cola blanca no frena
        Assert.Equal(blade.Def(brake).BaseIntensity * 1.7f * 3.2f, blade.Intensity(brake), 2);

        var hammer = new ShipLightState(Lights("hammer"));
        hammer.Update(Dt, Rest);
        int center = Index(hammer, "Light_Brake");
        int bar = Index(hammer, "Light_TailBar_R");
        Assert.Equal(hammer.Def(center).BaseIntensity * 1.7f * 0.35f, hammer.Intensity(center), 4);
        Assert.Equal(hammer.Def(bar).BaseIntensity * 1.7f, hammer.Intensity(bar), 4);
    }

    [Fact]
    public void Brake_NeedsSomeSpeed()
    {
        var state = new ShipLightState(Lights("vector"));
        Run(state, Braking with { Speed = 0.5f }, 1f);
        Assert.Equal(0f, state.Brake);
    }

    // ---------- 3. Tiempos del freno ----------

    [Fact]
    public void Brake_RisesAbove90PercentIn0Point1s_AndFallsBelow10PercentIn0Point4s()
    {
        var state = new ShipLightState(Lights("vector"));
        Run(state, Braking, 0.1f);
        Assert.True(state.Brake > 0.9f, $"{state.Brake}");
        Run(state, Braking, 1f);
        Run(state, Rest, 0.4f);
        Assert.True(state.Brake < 0.1f, $"{state.Brake}");
    }

    // ---------- 4. Giro ----------

    [Fact]
    public void Turn_AtFullLeftAtTopSpeed_RaisesTheLeftLightsTimes1Point7()
    {
        var rest = new ShipLightState(Lights("hammer"));
        rest.Update(Dt, Rest);
        var state = new ShipLightState(Lights("hammer"));
        Run(state, new ShipLightInput { Speed = 100f, TopSpeed = 100f, Steer = 1f }, 3f);
        Assert.Equal(1f, state.Turn, 3);
        Assert.Equal(-1, state.InnerSide);
        for (int i = 0; i < state.Count; i++)
        {
            ShipLightDef d = state.Def(i);
            if (ShipLightTypes.IsExternal(d.Type) || d.Type == ShipLightType.Retro)
                continue;
            float expected = rest.Intensity(i) * (d.Side < 0 ? 1.7f : 1f);
            Assert.Equal(expected, state.Intensity(i), 0.002f * expected + 1e-4f);
        }
    }

    [Fact]
    public void Turn_LoadsWithSpeedAndDrift()
    {
        var slow = new ShipLightState(Lights("vector"));
        Run(slow, new ShipLightInput { Speed = 20f, TopSpeed = 100f, Steer = -1f }, 3f);
        Assert.Equal(0.26f, slow.Turn, 2);   // |giro| × mín(1; 1,3 × 0,2)
        Assert.Equal(1, slow.InnerSide);
        var drift = new ShipLightState(Lights("vector"));
        Run(drift, new ShipLightInput { Speed = 20f, TopSpeed = 100f, Steer = -0.5f, Drifting = true }, 3f);
        Assert.Equal(0.63f, drift.Turn, 2);  // 0,5 × 0,26 + 0,5
    }

    // ---------- 5. Halos ----------

    [Fact]
    public void Halo_NotBelowMinimumIntensity()
    {
        var blade = new ShipLightState(Lights("blade"));
        int strobe = Index(blade, "Light_Strobe_Top");
        blade.SetExternal(strobe, 0.04f);
        blade.Update(Dt, Rest);
        Assert.Equal(0f, blade.HaloOpacity(strobe));
        Assert.Equal(0f, blade.HaloSize(strobe));
        blade.SetExternal(strobe, 6f);
        blade.Update(Dt, Rest);
        Assert.Equal(1.3f, blade.HaloSize(strobe), 3);
        Assert.Equal(Math.Min(1.4f, 0.32f * 0.42f * 6f), blade.HaloOpacity(strobe), 3);
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void Halo_NeverOnStrips(string ship)
    {
        var state = new ShipLightState(Lights(ship));
        Run(state, Braking with { Boost = 1f }, 1f);
        for (int i = 0; i < state.Count; i++)
        {
            ShipLightDef d = state.Def(i);
            bool strip = d.Type is ShipLightType.Profile or ShipLightType.Form or ShipLightType.Retro
                || (d.Type == ShipLightType.Head && ship != "hammer") || d.Name.EndsWith("_Reflector");
            if (strip)
                Assert.Equal(0f, state.HaloOpacity(i));
        }
        if (ship != "hammer")
            Assert.DoesNotContain(Enumerable.Range(0, state.Count), i => state.Def(i).Type == ShipLightType.Head && state.Def(i).HasHalo);
        else
            Assert.All(new[] { "Light_Head_L", "Light_Head_R" }, n => Assert.True(state.Def(Index(state, n)).HasHalo));
    }

    [Fact]
    public void Halo_GrowsTo125PercentWithTheBrakeAtFull()
    {
        var state = new ShipLightState(Lights("vector"));
        state.Update(Dt, Rest);
        int tail = Index(state, "Light_Tail_L");
        Assert.Equal(0.85f, state.HaloSize(tail), 3);
        Run(state, Braking, 2f);
        Assert.Equal(0.85f * 1.25f, state.HaloSize(tail), 3);
    }

    [Fact]
    public void Halo_OpacityFollowsTheIntensity_AndTheDamage()
    {
        var state = new ShipLightState(Lights("vector"));
        int nose = Index(state, "Light_Nose_L");
        state.Update(Dt, Rest);
        float i0 = state.Intensity(nose);
        Assert.Equal(Math.Clamp(0.32f * 0.42f * i0, 0f, 1.4f), state.HaloOpacity(nose), 4);
        state.SetDamage(nose, 0.2f);
        state.Update(Dt, Rest);
        Assert.Equal(i0 * 0.2f, state.Intensity(nose), 4);
    }

    [Fact]
    public void Reset_ClearsBrakeAndTurnAtOnce()
    {
        var state = new ShipLightState(Lights("blade"));
        Run(state, new ShipLightInput { Braking = true, Speed = 80f, TopSpeed = 100f, Steer = 1f }, 1f);
        state.Reset();
        Assert.Equal(0f, state.Brake);
        Assert.Equal(0f, state.Turn);
    }

    [Fact]
    public void WithoutDelta_NothingMoves()
    {
        var state = new ShipLightState(Lights("vector"));
        state.Update(0f, Braking with { Steer = 1f });
        Assert.Equal(0f, state.Brake);
        Assert.Equal(0f, state.Turn);
    }
}
