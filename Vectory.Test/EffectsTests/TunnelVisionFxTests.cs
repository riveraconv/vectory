using System;
using Vectory.Effects;
using Vectory.Settings;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Visión túnel (spec docs/efectos-9-vision-tunel.md, 10: pruebas 1–6, 9 sin las líneas, 10 y 11; las líneas, 7 y 8,
// van en la etapa 2)
public class TunnelVisionFxTests
{
    private static readonly TunnelVisionSettings S = TunnelVisionSettings.Default;
    private const float Top = 100f;   // m/s de punta base
    private const int High = 2;

    private static float Cockpit(float intensity) => TunnelVisionCurves.Final(intensity, CameraView.Cockpit, 1f, S);
    private static float Chase(float intensity) => TunnelVisionCurves.Final(intensity, CameraView.Chase, 1f, S);
    private static float AtTop(float boost) => TunnelVisionCurves.Target(TunnelVisionCurves.SpeedFactor(Top, Top, S), boost, S);

    // 1
    [Fact]
    public void The_speed_factor_starts_at_a_quarter_of_the_base_top_speed()
    {
        Assert.Equal(0f, TunnelVisionCurves.SpeedFactor(0f, Top, S));
        Assert.Equal(0f, TunnelVisionCurves.SpeedFactor(25f, Top, S));
        Assert.Equal(0.5f, TunnelVisionCurves.SpeedFactor(62.5f, Top, S), 5);
        Assert.Equal(1f, TunnelVisionCurves.SpeedFactor(Top, Top, S));
        Assert.Equal(1f, TunnelVisionCurves.SpeedFactor(130f, Top, S));   // con boost, tope en 1
    }

    // 2
    [Fact]
    public void In_the_cockpit_the_tunnel_is_0_stopped_085_at_top_speed_and_12_at_most_with_boost()
    {
        Assert.Equal(0f, Cockpit(TunnelVisionCurves.Target(0f, 0f, S)));
        Assert.Equal(0.85f, Cockpit(AtTop(0f)), 5);
        Assert.Equal(1.2f, Cockpit(AtTop(1f)), 5);
        Assert.Equal(1.2f, Cockpit(TunnelVisionCurves.Target(1f, 1f, S)), 5);
    }

    // 3
    [Fact]
    public void The_chase_camera_gets_half_the_tunnel()
    {
        Assert.Equal(0.85f, Cockpit(AtTop(0f)), 5);
        Assert.Equal(0.425f, Chase(AtTop(0f)), 5);
        Assert.Equal(1.2f, Cockpit(AtTop(1f)), 5);
        Assert.Equal(0.6f, Chase(AtTop(1f)), 5);
    }

    // 4
    [Fact]
    public void Tunnel_vision_setting_multiplies_after_the_camera_factor()
    {
        Assert.Equal(0.2125f, TunnelVisionCurves.Final(AtTop(0f), CameraView.Chase, 0.5f, S), 5);
        Assert.Equal(0f, TunnelVisionCurves.Final(AtTop(1f), CameraView.Cockpit, 0f, S));
        Assert.Equal(0f, TunnelVisionCurves.Final(AtTop(1f), CameraView.Chase, 0f, S));
        TunnelVisionFrame off = TunnelVisionCurves.Frame(AtTop(1f), 1f, CameraView.Cockpit, 0f, High, S);
        Assert.Equal(0f, off.Intensity);
        Assert.Equal(0f, off.Aberration);
        Assert.Equal(0f, off.Halo);
    }

    // 5
    [Fact]
    public void The_intensity_is_smoothed_and_does_not_move_with_the_game_paused()
    {
        var state = new TunnelVisionState();
        state.Update(Top, Top, 0f, S, 0f);
        Assert.Equal(0f, state.Intensity);

        state.Update(Top, Top, 0f, S, 0.12f);
        Assert.Equal(0.85f * (1f - MathF.Exp(-1f)), state.Intensity, 4);   // el 63 % del objetivo
        float paused = state.Intensity;
        state.Update(0f, Top, 0f, S, 0f);
        Assert.Equal(paused, state.Intensity);

        // Un frenazo de golpe no lo quita de golpe
        state.Update(0f, Top, 0f, S, 1f / 60f);
        Assert.True(state.Intensity > paused * 0.8f);
    }

    // 6
    [Fact]
    public void The_chromatic_split_is_always_on_and_grows_with_boost()
    {
        Assert.Equal(0.004f, TunnelVisionCurves.Aberration(0f, S), 6);   // el 40 % de 0,010
        Assert.Equal(0.007f, TunnelVisionCurves.Aberration(0.5f, S), 6);
        Assert.Equal(0.010f, TunnelVisionCurves.Aberration(1f, S), 6);

        Assert.Equal(0.004f, TunnelVisionCurves.Frame(0.5f, 0f, CameraView.Cockpit, 1f, High, S).Aberration, 6);
        TunnelVisionFrame still = TunnelVisionCurves.Frame(0f, 1f, CameraView.Cockpit, 1f, High, S);
        Assert.Equal(0f, still.Aberration);
        Assert.Equal(0f, still.Halo);
    }

    // El halo ámbar: solo con boost, y crece con la intensidad hasta 0,45 (maqueta)
    [Fact]
    public void The_amber_halo_needs_boost_and_grows_with_the_intensity()
    {
        Assert.Equal(0f, TunnelVisionCurves.Halo(1f, 0f, S));
        Assert.Equal(0.10f, TunnelVisionCurves.Halo(0.6f, 1f, S), 5);
        Assert.Equal(0.05f, TunnelVisionCurves.Halo(0.225f, 1f, S), 5);
        Assert.Equal(0.10f, TunnelVisionCurves.Frame(AtTop(1f), 1f, CameraView.Chase, 1f, High, S).Halo, 5);
    }

    // 9 (túnel): recolocar lo pone a 0 al momento, en las dos cámaras
    [Fact]
    public void Respawning_puts_the_tunnel_to_0_at_once_in_both_cameras()
    {
        var state = new TunnelVisionState();
        for (int i = 0; i < 120; i++)
            state.Update(Top, Top, 1f, S, 1f / 60f);
        Assert.True(state.Intensity > 1f);
        state.Reset();
        Assert.Equal(0f, state.Intensity);
        Assert.Equal(0f, TunnelVisionCurves.Frame(state.Intensity, 1f, CameraView.Cockpit, 1f, High, S).Intensity);
        Assert.Equal(0f, TunnelVisionCurves.Frame(state.Intensity, 1f, CameraView.Chase, 1f, High, S).Intensity);
    }

    // 10
    [Fact]
    public void Particles_low_uses_6_samples_and_the_others_16()
    {
        Assert.Equal(6, TunnelVisionCurves.Samples(0, S));
        Assert.Equal(16, TunnelVisionCurves.Samples(1, S));
        Assert.Equal(16, TunnelVisionCurves.Samples(2, S));
    }

    // 11
    [Fact]
    public void Tunnel_vision_is_a_real_setting_and_reaches_the_game()
    {
        Assert.False(SettingsCatalog.Find(SettingsCatalog.TunnelVision).ComingSoon);
        var model = new SettingsModel();
        try
        {
            Assert.Equal(1f, model.TunnelVision);
            model.Set(SettingsCatalog.TunnelVision, 3);
            model.Set(SettingsCatalog.Particles, 0);
            Assert.Equal("30 %", model.ValueText(SettingsCatalog.TunnelVision));
            ActiveSettings.From(model);
            Assert.Equal(0.3f, ActiveSettings.TunnelVision, 5);
            Assert.Equal(0, ActiveSettings.ParticlesLevel);
        }
        finally
        {
            ActiveSettings.From(new SettingsModel());
        }
    }
}
