using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Cono de vapor (spec docs/efectos-9-vision-tunel.md, 10: pruebas 7, 8 y 9 del cono)
public class SonicConeFxTests
{
    private static readonly SonicConeSettings S = SonicConeSettings.Default;
    private const float Top = 100f;   // m/s de punta base

    // 7
    [Fact]
    public void The_cone_shows_from_75_percent_of_top_speed_and_is_full_at_the_top()
    {
        Assert.Equal(0f, SonicConeCurves.Target(0f, Top, 0f, S));
        Assert.Equal(0f, SonicConeCurves.Target(75f, Top, 0f, S));
        Assert.Equal(0.5f, SonicConeCurves.Target(87.5f, Top, 0f, S), 5);
        Assert.Equal(1f, SonicConeCurves.Target(100f, Top, 0f, S), 5);
    }

    // 7
    [Fact]
    public void Boost_adds_only_when_the_cone_is_already_visible()
    {
        Assert.Equal(0f, SonicConeCurves.Target(60f, Top, 1f, S));
        Assert.Equal(1.3f, SonicConeCurves.Target(100f, Top, 1f, S), 5);
        Assert.Equal(1.3f, SonicConeCurves.Target(130f, Top, 1f, S), 5);
    }

    // 8
    [Fact]
    public void Each_camera_shows_only_its_cone_and_speed_lines_off_removes_both()
    {
        SonicConeFrame cockpit = SonicConeCurves.Frame(1f, 0f, 0f, CameraView.Cockpit, true, S);
        Assert.Equal(0.03f, cockpit.CockpitOpacity, 5);
        Assert.Equal(0f, cockpit.ChaseOpacity);
        SonicConeFrame chase = SonicConeCurves.Frame(1f, 1f, 0f, CameraView.Chase, true, S);
        Assert.Equal(0f, chase.CockpitOpacity);
        Assert.Equal(0.24f, chase.ChaseOpacity, 5);
        Assert.Equal(0.6f, chase.BoostMix, 5);
        Assert.Equal(0f, SonicConeCurves.Frame(1f, 1f, 0f, CameraView.Cockpit, false, S).CockpitOpacity);
        Assert.Equal(0f, SonicConeCurves.Frame(1f, 1f, 0f, CameraView.Chase, false, S).ChaseOpacity);
    }

    // 9
    [Fact]
    public void Paused_it_does_not_change_and_respawned_it_is_gone_at_once()
    {
        var state = new SonicConeState();
        state.Update(Top, Top, 0f, S, 0.18f);
        Assert.Equal(0.632f, state.Amount, 3);
        float amount = state.Amount, flow = state.Flow;
        state.Update(Top, Top, 1f, S, 0f);
        Assert.Equal(amount, state.Amount);
        Assert.Equal(flow, state.Flow);
        state.Reset();
        Assert.Equal(0f, state.Amount);
    }

    [Fact]
    public void It_fades_to_exactly_zero_and_the_flow_wraps()
    {
        var state = new SonicConeState();
        state.Update(Top, Top, 0f, S, 1f);
        for (int i = 0; i < 180; i++)
            state.Update(0f, Top, 0f, S, 1f / 60f);
        Assert.Equal(0f, state.Amount);
        for (int i = 0; i < 2000; i++)
            state.Update(Top, Top, 1f, S, 1f);
        Assert.InRange(state.Flow, 0f, SonicConeCurves.FlowWrap);
    }
}
