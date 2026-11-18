using System;
using System.Numerics;
using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Llama de las toberas (spec docs/efectos-6-toberas.md, pruebas 1–8)
public class ThrusterFxTests
{
    private static readonly ThrusterFxSettings S = ThrusterFxSettings.Default;
    private const float Top = 110f;
    private const float Dt = 1f / 60f;

    private static ThrusterInput Drive(bool accelerate = false, bool brake = false, float speed = Top, float steer = 0f,
        bool boost = false, bool drift = false, float slip = 0f) => new()
    {
        Accelerate = accelerate,
        Brake = brake,
        Steer = steer,
        Boosting = boost,
        Drifting = drift,
        SlipAngle = slip,
        Speed = speed,
        TopSpeed = Top
    };

    private static void Run(ThrusterState state, ThrusterInput input, float seconds)
    {
        int steps = (int)MathF.Round(seconds / Dt);
        for (int i = 0; i < steps; i++)
            state.Update(Dt, input, S);
    }

    private static ThrusterShape Shape(float scale = 1f) =>
        new(new Vector3(0.75f, 0.1f, 3.2f), scale, 0.46f, 1f, Vector3.Zero, ThrusterColors.Standard);

    [Fact]
    public void Power_targets_follow_the_pedals()
    {
        var full = new ThrusterState();
        Run(full, Drive(accelerate: true), 3f);
        Assert.Equal(1f, full.Power, 3);

        var coast = new ThrusterState();
        Run(coast, Drive(), 3f);
        Assert.Equal(0.34f, coast.Power, 3);

        var braking = new ThrusterState();
        Run(braking, Drive(brake: true, speed: 50f), 2f);
        Assert.Equal(0.06f, braking.Power, 3);

        var parked = new ThrusterState();
        Run(parked, Drive(brake: true, speed: 0f), 2f);
        Assert.Equal(0.14f, parked.Power, 3);
        Run(parked, Drive(speed: 0.5f), 2f);
        Assert.True(parked.Power >= 0.14f - 1e-4f);
    }

    [Fact]
    public void Power_rises_fast_and_falls_slower_coasting_than_braking()
    {
        var state = new ThrusterState();
        Assert.Equal(0.14f, state.Power, 4);
        Run(state, Drive(accelerate: true), 0.5f);
        Assert.True(state.Power > 0.9f, $"P = {state.Power}");

        var coast = new ThrusterState();
        Run(coast, Drive(accelerate: true), 3f);
        var brake = new ThrusterState();
        Run(brake, Drive(accelerate: true), 3f);
        Run(coast, Drive(), 0.1f);
        Run(brake, Drive(brake: true), 0.1f);
        Assert.True(coast.Power > brake.Power + 0.3f, $"sin gas {coast.Power}, frenando {brake.Power}");
    }

    [Fact]
    public void Length_matches_the_spec()
    {
        Assert.Equal(1.38f, ThrusterState.Length(0.3f, 0f, 1f, 0f, S), 3);
        Assert.Equal(7.75f, ThrusterState.Length(1f, 1f, 1f, 0f, S), 3);
    }

    [Fact]
    public void Boost_start_kicks_the_length_and_fades_in_its_tau()
    {
        var state = new ThrusterState();
        Run(state, Drive(accelerate: true), 3f);
        state.Update(Dt, Drive(accelerate: true, boost: true), S);
        Assert.Equal(1f, state.Kick, 4);
        float withKick = ThrusterState.Length(state.Power, state.Boost, 1f, state.Kick, S);
        float without = ThrusterState.Length(state.Power, state.Boost, 1f, 0f, S);
        Assert.Equal(1.45f, withKick / without, 3);

        Run(state, Drive(accelerate: true, boost: true), 0.35f);
        Assert.Equal(MathF.Exp(-1f), state.Kick, 2);

        // Mientras sigue el boost no vuelve a dispararse
        Run(state, Drive(accelerate: true, boost: true), 1f);
        Assert.True(state.Kick < 0.1f);
    }

    [Fact]
    public void Outer_nozzle_pushes_more_when_turning()
    {
        var state = new ThrusterState();
        Run(state, Drive(accelerate: true, steer: 1f), 2f);
        ThrusterFlame left = state.Flame(0, Shape(), S);
        ThrusterFlame right = state.Flame(1, Shape(), S);
        float straight = ThrusterState.Length(state.Power, 0f, 1f, 0f, S);
        Assert.Equal(1.28f, right.Length / straight, 3);
        Assert.Equal(0.72f, left.Length / straight, 3);

        var parked = new ThrusterState();
        Run(parked, Drive(steer: 1f, speed: 0f), 2f);
        Assert.Equal(parked.Flame(0, Shape(), S).Length, parked.Flame(1, Shape(), S).Length, 5);
    }

    [Fact]
    public void Drift_bends_the_tip_inside_the_curve()
    {
        float max = 22f * MathF.PI / 180f;
        Assert.Equal(-0.22f * 2f, ThrusterState.Bend(max, 0f, 2f, S), 4);
        Assert.Equal(-0.22f * 2f, ThrusterState.Bend(max * 2f, 0f, 2f, S), 4);
        Assert.Equal(-0.11f * 2f, ThrusterState.Bend(max * 0.5f, 0f, 2f, S), 4);
        Assert.Equal(0.22f * 2f, ThrusterState.Bend(-max, 0f, 2f, S), 4);
        // Girando a la izquierda sin derrapar, un poco hacia la izquierda
        Assert.Equal(-0.06f * 2f, ThrusterState.Bend(0f, 1f, 2f, S), 4);
    }

    [Fact]
    public void Mouth_scale_changes_radii_not_length()
    {
        var state = new ThrusterState();
        Run(state, Drive(accelerate: true), 2f);
        ThrusterFlame big = state.Flame(1, Shape(1f), S);
        ThrusterFlame small = state.Flame(1, Shape(0.5f), S);
        Assert.Equal(big.Length, small.Length, 5);
        // Los radios los escala el nodo con Shape.Scale; la longitud solo con LengthScale
        ThrusterShape shorter = Shape() with { LengthScale = 0.85f };
        Assert.Equal(big.Length * 0.85f, state.Flame(1, shorter, S).Length, 4);
    }

    [Fact]
    public void Colors_mix_from_normal_to_boost()
    {
        var normal = new ThrusterState();
        Run(normal, Drive(accelerate: true), 2f);
        ThrusterFlame n = normal.Flame(0, Shape(), S);
        Assert.Equal(ThrusterColors.Standard.Mid, n.Mid);
        Assert.Equal(ThrusterColors.Standard.Core, n.Core);

        var boosted = new ThrusterState();
        Run(boosted, Drive(accelerate: true, boost: true), 5f);
        ThrusterFlame b = boosted.Flame(0, Shape(), S);
        Assert.True(Vector3.Distance(S.BoostMid, b.Mid) < 1e-4f);
        Assert.True(Vector3.Distance(S.BoostOut, b.Out) < 1e-4f);

        // A la mitad, la mezcla (bt sube con τ 0,18: ln 2 × τ)
        var half = new ThrusterState();
        Run(half, Drive(accelerate: true), 2f);
        half.Update(MathF.Log(2f) * S.BoostRise, Drive(accelerate: true, boost: true), S);
        Assert.Equal(0.5f, half.Boost, 3);
        Vector3 mid = (ThrusterColors.Standard.Mid + S.BoostMid) * 0.5f;
        Assert.True(Vector3.Distance(mid, half.Flame(0, Shape(), S).Mid) < 1e-3f);

        // HAMMER: su naranja sin boost
        ThrusterShape hammer = Shape() with { Colors = ThrusterColors.Industrial };
        Assert.Equal(ThrusterColors.Industrial.Mid, normal.Flame(0, hammer, S).Mid);
    }

    [Fact]
    public void Ring_appears_on_both_mouths_with_a_delay()
    {
        var state = new ThrusterState();
        Run(state, Drive(accelerate: true), 1f);
        state.Update(Dt, Drive(accelerate: true, boost: true), S);
        Assert.True(state.Flame(0, Shape(), S).RingIntensity > 0f);
        Assert.Equal(0f, state.Flame(1, Shape(), S).RingIntensity);
        Run(state, Drive(accelerate: true, boost: true), 0.05f);
        ThrusterFlame right = state.Flame(1, Shape(), S);
        Assert.True(right.RingIntensity > 0f);
        Assert.True(right.RingSize > S.RingStart && right.RingSize < S.RingEnd);
        Run(state, Drive(accelerate: true, boost: true), 0.4f);
        Assert.Equal(0f, state.Flame(0, Shape(), S).RingIntensity);
        Assert.Equal(0f, state.Flame(1, Shape(), S).RingIntensity);
    }

    [Fact]
    public void Mouth_gives_todays_emission_at_cruise()
    {
        Assert.Equal(1f, (S.MouthBase + S.MouthPower * 0.3f) / S.MouthNorm, 4);
    }

    [Fact]
    public void Pause_changes_nothing()
    {
        var state = new ThrusterState(0.3f);
        Run(state, Drive(accelerate: true, steer: 1f), 1f);
        float power = state.Power, steer = state.Steer, time = state.Time, scroll = state.Scroll;
        ThrusterFlame before = state.Flame(1, Shape(), S);
        state.Update(0f, Drive(brake: true, steer: -1f, boost: true), S);
        Assert.Equal(power, state.Power);
        Assert.Equal(steer, state.Steer);
        Assert.Equal(time, state.Time);
        Assert.Equal(scroll, state.Scroll);
        Assert.Equal(before, state.Flame(1, Shape(), S));
    }

    [Fact]
    public void Brake_flickers_and_reset_goes_back_to_idle()
    {
        var state = new ThrusterState();
        Run(state, Drive(accelerate: true), 2f);
        Run(state, Drive(brake: true, speed: 60f), 0.5f);
        Assert.Equal(1f, state.BrakeFlick);
        float min = float.MaxValue;
        for (int i = 0; i < 120; i++)
        {
            state.Update(Dt, Drive(brake: true, speed: 60f), S);
            min = MathF.Min(min, state.Flicker(0, S));
        }
        Assert.True(min < 0.8f, $"q mínimo {min}");

        state.Reset(S);
        Assert.Equal(S.IdlePower, state.Power);
        Assert.Equal(0f, state.Kick);
        Assert.Equal(0f, state.BrakeFlick);
    }

    [Fact]
    public void Every_ship_has_its_thrusters()
    {
        Assert.Equal(new Vector3(0.75f, 0.10f, 3.20f), VectorShip.Definition.Visual.Thrusters.Nozzle);
        Assert.Equal(ThrusterColors.Industrial, HammerShip.Definition.Visual.Thrusters.Colors);
        Assert.Equal(ThrusterColors.Standard, BladeShip.Definition.Visual.Thrusters.Colors);
        Assert.Equal(0.85f, BladeShip.Definition.Visual.Thrusters.LengthScale);
        Assert.Equal(1f, HammerShip.Definition.Visual.Thrusters.Squash);
    }
}
