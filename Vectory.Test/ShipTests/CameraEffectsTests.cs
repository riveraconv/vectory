using System;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Efectos de cámara: inclinación de la cabina, vibración y reacción al boost (spec docs/camara.md v1.1, sección 8)
public class CameraEffectsTests
{
    private const float Dt = 1f / 60f;
    private const float Deg = MathF.PI / 180f;
    private const float Top = 110f;

    private static CameraEffectSettings Settings() => new();

    private static float RunFor(float seconds, Action step)
    {
        int frames = (int)MathF.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
            step();
        return frames * Dt;
    }

    // --- Inclinación de la cabina ---

    [Fact]
    public void CockpitBank_ShipNotTurning_TargetIsZero()
    {
        Assert.Equal(0f, CockpitBank.Target(0f, Settings()));
    }

    [Fact]
    public void CockpitBank_ShipAtMax_TargetIsShipTimesFactor_WithinCap()
    {
        var s = Settings();
        float shipTarget = 10f * Deg;   // × 0,4 = 4°, por debajo del tope de 8°
        Assert.Equal(shipTarget * 0.4f, CockpitBank.Target(shipTarget, s), 5);
        Assert.Equal(-shipTarget * 0.4f, CockpitBank.Target(-shipTarget, s), 5);
    }

    [Fact]
    public void CockpitBank_NeverExceedsCap()
    {
        var s = Settings();
        Assert.Equal(8f * Deg, CockpitBank.Target(25f * Deg, s), 5);   // 25° × 0,4 = 10° → 8°
        Assert.Equal(-8f * Deg, CockpitBank.Target(-25f * Deg, s), 5);
    }

    [Fact]
    public void CockpitBank_FactorZero_AlwaysZero()
    {
        var s = Settings();
        s.CockpitBankFactor = 0f;
        var bank = new CockpitBank();
        RunFor(2f, () => bank.Update(25f * Deg, s, Dt));
        Assert.Equal(0f, CockpitBank.Target(25f * Deg, s));
        Assert.Equal(0f, bank.Angle);
    }

    [Fact]
    public void CockpitBank_ApproachesTargetSmoothly_AndReachesIt()
    {
        var s = Settings();
        var bank = new CockpitBank();
        float target = CockpitBank.Target(20f * Deg, s);

        bank.Update(20f * Deg, s, Dt);
        Assert.True(bank.Angle > 0f);
        Assert.True(bank.Angle < target * 0.1f);   // en un frame no salta

        float previous = bank.Angle;
        for (int i = 0; i < 300; i++)
        {
            bank.Update(20f * Deg, s, Dt);
            Assert.True(bank.Angle >= previous);
            Assert.True(bank.Angle <= target + 1e-6f);
            previous = bank.Angle;
        }
        Assert.Equal(target, bank.Angle, 4);   // 5 s ≈ 14 constantes de tiempo
    }

    [Fact]
    public void CockpitBank_IsSlowerThanShipBank()
    {
        var s = Settings();
        var cockpit = new CockpitBank();
        var ship = new ShipBank();
        var shipSettings = new BankSettings { MaxNormal = 25f * Deg, MaxDrift = 20f * Deg, SmoothTime = 0.15f };
        float shipTarget = ShipBank.Target(1f, 1f, false, shipSettings);
        float cockpitTarget = CockpitBank.Target(shipTarget, s);

        RunFor(0.2f, () =>
        {
            ship.Update(1f, 1f, false, shipSettings, Dt);
            cockpit.Update(shipTarget, s, Dt);
        });

        Assert.True(cockpit.Angle / cockpitTarget < ship.Angle / shipTarget);
    }

    // --- Vibración ---

    [Fact]
    public void Vibration_BelowThreshold_IsZero()
    {
        var s = Settings();
        Assert.Equal(0f, CameraVibration.TargetIntensity(0.84f * Top, Top, false, s));
        Assert.Equal(0f, CameraVibration.TargetIntensity(0.5f * Top, Top, true, s));

        var vibration = new CameraVibration();
        RunFor(2f, () => vibration.Update(0.8f * Top, Top, false, s, Dt));
        Assert.Equal(0f, vibration.Intensity);
        Assert.Equal(Vector2.Zero, vibration.Offset(CameraView.Chase, s));
        Assert.Equal(Vector2.Zero, vibration.Offset(CameraView.Cockpit, s));
    }

    [Fact]
    public void Vibration_AtTopSpeed_ReachesMaxAmplitudeOfEachCamera()
    {
        var s = Settings();
        var vibration = new CameraVibration();
        RunFor(3f, () => vibration.Update(Top, Top, false, s, Dt));

        Assert.Equal(1f, vibration.Intensity, 4);
        Assert.Equal(0.03f, vibration.Amplitude(CameraView.Cockpit, s), 5);
        Assert.Equal(0.018f, vibration.Amplitude(CameraView.Chase, s), 5);

        Vector2 offset = vibration.Offset(CameraView.Chase, s);
        Assert.True(MathF.Abs(offset.X) <= 0.018f + 1e-6f && MathF.Abs(offset.Y) <= 0.018f + 1e-6f);
    }

    [Fact]
    public void Vibration_GrowsBetweenThresholdAndTop()
    {
        var s = Settings();
        float half = CameraVibration.TargetIntensity((0.85f + 0.15f / 2f) * Top, Top, false, s);
        Assert.Equal(0.5f, half, 3);
        Assert.Equal(1f, CameraVibration.TargetIntensity(1.3f * Top, Top, false, s));   // por encima de la punta, tope 1
    }

    [Fact]
    public void Vibration_WithBoost_IsMultiplied()
    {
        var s = Settings();
        Assert.Equal(1.5f, CameraVibration.TargetIntensity(Top, Top, true, s), 5);

        var vibration = new CameraVibration();
        RunFor(3f, () => vibration.Update(Top, Top, true, s, Dt));
        Assert.Equal(0.018f * 1.5f, vibration.Amplitude(CameraView.Chase, s), 4);
    }

    [Fact]
    public void Vibration_AmplitudeZero_AlwaysZero()
    {
        var s = Settings();
        s.VibrationAmplitudeChase = 0f;
        s.VibrationAmplitudeCockpit = 0f;
        var vibration = new CameraVibration();
        RunFor(3f, () => vibration.Update(Top, Top, true, s, Dt));
        Assert.Equal(Vector2.Zero, vibration.Offset(CameraView.Chase, s));
        Assert.Equal(Vector2.Zero, vibration.Offset(CameraView.Cockpit, s));
    }

    [Fact]
    public void Vibration_IsDeterministic()
    {
        var s = Settings();
        var a = new CameraVibration();
        var b = new CameraVibration();
        for (int i = 0; i < 200; i++)
        {
            float speed = Top * (0.8f + 0.2f * i / 200f);
            a.Update(speed, Top, i > 100, s, Dt);
            b.Update(speed, Top, i > 100, s, Dt);
            Assert.Equal(a.Offset(CameraView.Chase, s), b.Offset(CameraView.Chase, s));
        }
        Assert.Equal(CameraVibration.Wave(1.234f, 8f), CameraVibration.Wave(1.234f, 8f));
        Assert.NotEqual(CameraVibration.Wave(1.234f, 8f), CameraVibration.Wave(1.25f, 8f));
    }

    [Fact]
    public void Vibration_Wave_StaysWithinUnitRange()
    {
        for (int i = 0; i < 2000; i++)
        {
            Vector2 w = CameraVibration.Wave(i * 0.001f, 8f);
            Assert.InRange(w.X, -1f, 1f);
            Assert.InRange(w.Y, -1f, 1f);
        }
    }

    [Fact]
    public void Vibration_UsesCurrentTopSpeed()
    {
        var s = Settings();
        float damagedTop = Top * 0.85f;
        // A la punta reducida por daño vibra al máximo; con la punta normal no llegaría al umbral
        Assert.Equal(1f, CameraVibration.TargetIntensity(damagedTop, damagedTop, false, s), 5);
        Assert.Equal(0f, CameraVibration.TargetIntensity(damagedTop * 0.99f, Top, false, s));
    }

    [Fact]
    public void Vibration_FadesSmoothly_WhenSlowingDown()
    {
        var s = Settings();
        var vibration = new CameraVibration();
        RunFor(3f, () => vibration.Update(Top, Top, true, s, Dt));
        float before = vibration.Intensity;

        vibration.Update(0f, Top, false, s, Dt);
        Assert.True(vibration.Intensity > before * 0.9f);   // no se corta de golpe

        RunFor(2f, () => vibration.Update(0f, Top, false, s, Dt));
        Assert.True(vibration.Intensity < 0.001f);
    }

    // --- Reacción al boost ---

    [Fact]
    public void Boost_RisesToMaxInEnterTime_AndFallsInExitTime()
    {
        var s = Settings();
        var effect = new BoostCameraEffect();

        effect.Update(true, s, 0.125f);
        Assert.InRange(effect.FovExtra(s), 0.1f, 7.9f);

        effect.Update(true, s, 0.125f);   // 0,25 s
        Assert.Equal(8f, effect.FovExtra(s), 4);
        Assert.Equal(0.6f, effect.Push(CameraView.Chase, s), 4);
        Assert.Equal(0.05f, effect.Push(CameraView.Cockpit, s), 4);

        effect.Update(false, s, 0.25f);
        Assert.InRange(effect.FovExtra(s), 0.1f, 7.9f);   // a mitad de la salida

        effect.Update(false, s, 0.25f);   // 0,5 s
        Assert.Equal(0f, effect.FovExtra(s));
        Assert.Equal(0f, effect.Push(CameraView.Chase, s));
    }

    [Fact]
    public void Boost_NoJumpInOneFrame()
    {
        var s = Settings();
        var effect = new BoostCameraEffect();
        float previous = 0f;
        RunFor(0.25f, () =>
        {
            effect.Update(true, s, Dt);
            Assert.True(effect.FovExtra(s) - previous < 1.5f);
            previous = effect.FovExtra(s);
        });
        RunFor(0.5f, () =>
        {
            effect.Update(false, s, Dt);
            Assert.True(previous - effect.FovExtra(s) < 1f);
            previous = effect.FovExtra(s);
        });
        Assert.Equal(0f, effect.FovExtra(s), 4);
    }

    [Fact]
    public void Boost_CancelledHalfway_ReturnsWithExitTime()
    {
        var s = Settings();
        var effect = new BoostCameraEffect();
        effect.Update(true, s, 0.125f);   // a medias
        float amount = effect.Amount;

        effect.Update(false, s, Dt);
        Assert.True(effect.Amount < amount && effect.Amount > amount - 0.05f);
        effect.Update(false, s, 0.25f);   // la mitad del camino se hace en la mitad del tiempo de salida
        Assert.Equal(0f, effect.Amount, 3);
    }

    // --- Recolocación y pausa ---

    [Fact]
    public void Reset_PutsAllEffectsBackToBase_Instantly()
    {
        var s = Settings();
        var bank = new CockpitBank();
        var vibration = new CameraVibration();
        var effect = new BoostCameraEffect();
        RunFor(2f, () =>
        {
            bank.Update(25f * Deg, s, Dt);
            vibration.Update(Top, Top, true, s, Dt);
            effect.Update(true, s, Dt);
        });
        Assert.True(bank.Angle > 0f && vibration.Intensity > 0f && effect.Amount > 0f);

        bank.Reset();
        vibration.Reset();
        effect.Reset();

        Assert.Equal(0f, bank.Angle);
        Assert.Equal(0f, vibration.Intensity);
        Assert.Equal(Vector2.Zero, vibration.Offset(CameraView.Chase, s));
        Assert.Equal(0f, effect.FovExtra(s));
        Assert.Equal(0f, effect.Push(CameraView.Chase, s));
    }

    [Fact]
    public void ZeroDelta_ChangesNothing()
    {
        var s = Settings();
        var bank = new CockpitBank();
        var vibration = new CameraVibration();
        var effect = new BoostCameraEffect();
        RunFor(0.1f, () =>
        {
            bank.Update(25f * Deg, s, Dt);
            vibration.Update(Top, Top, true, s, Dt);
            effect.Update(true, s, Dt);
        });
        float angle = bank.Angle;
        float intensity = vibration.Intensity;
        Vector2 offset = vibration.Offset(CameraView.Chase, s);
        float amount = effect.Amount;

        bank.Update(-25f * Deg, s, 0f);
        vibration.Update(0f, Top, false, s, 0f);
        effect.Update(false, s, 0f);

        Assert.Equal(angle, bank.Angle);
        Assert.Equal(intensity, vibration.Intensity);
        Assert.Equal(offset, vibration.Offset(CameraView.Chase, s));
        Assert.Equal(amount, effect.Amount);
    }
}
