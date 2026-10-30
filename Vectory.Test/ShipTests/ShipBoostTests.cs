using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class ShipBoostTests
{
    private const int Precision = 4;

    // Valores de la spec (v1.1). SpeedGain va en las mismas unidades que se comprueban (aquí, km/h).
    private static BoostTuning CreateTuning() => new()
    {
        MaxCharges = 3,
        InitialCharges = 0,
        RechargeTime = 35f,
        SpeedGain = 100f,
        RampDuration = 0.5f,
        HoldDuration = 2f,
        Cooldown = 10f
    };

    private static ShipBoost NewBoost(BoostTuning tuning)
    {
        var boost = new ShipBoost();
        boost.Reset(tuning);
        return boost;
    }

    // Avanza el tiempo en pasos de 1 s (exactos en float) y devuelve la velocidad sumada
    private static float Advance(ShipBoost boost, BoostTuning tuning, int seconds)
    {
        float gained = 0f;
        for (int i = 0; i < seconds; i++)
            gained += boost.Update(tuning, 1f);
        return gained;
    }

    // Boost recién activado con una carga ganada por tiempo
    private static ShipBoost Activated(BoostTuning tuning)
    {
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 35);
        boost.TryActivate(tuning);
        return boost;
    }

    // ---------------------------------------------------------------
    // Cargas y recarga
    // ---------------------------------------------------------------

    [Fact]
    public void StartsWithZeroCharges()
    {
        var boost = NewBoost(CreateTuning());

        Assert.Equal(0, boost.Charges);
        Assert.False(boost.CanActivate);
        Assert.Equal(BoostPhase.None, boost.Phase);
    }

    [Fact]
    public void Reset_UsesInitialCharges()
    {
        var tuning = CreateTuning();
        tuning.InitialCharges = 2;

        var boost = NewBoost(tuning);

        Assert.Equal(2, boost.Charges);
    }

    [Fact]
    public void GainsOneChargeEvery35Seconds_UpToThree()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);

        Advance(boost, tuning, 34);
        Assert.Equal(0, boost.Charges);

        Advance(boost, tuning, 1);    // 35 s
        Assert.Equal(1, boost.Charges);

        Advance(boost, tuning, 70);   // 105 s
        Assert.Equal(3, boost.Charges);

        Advance(boost, tuning, 35);   // 140 s
        Assert.Equal(3, boost.Charges);
    }

    [Fact]
    public void AtMaxCharges_RechargeCounterStaysAtZero()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);

        Advance(boost, tuning, 120);

        Assert.Equal(3, boost.Charges);
        Assert.Equal(0f, boost.RechargeProgress);
    }

    [Fact]
    public void SpendingAtMaxCharges_StartsRechargeFromZero()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 120);

        boost.TryActivate(tuning);

        Assert.Equal(2, boost.Charges);
        Assert.Equal(0f, boost.RechargeProgress);

        Advance(boost, tuning, 34);
        Assert.Equal(2, boost.Charges);

        Advance(boost, tuning, 1);
        Assert.Equal(3, boost.Charges);
    }

    [Fact]
    public void SpendingBelowMaxCharges_KeepsRechargeProgress()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 40);   // 1 carga y 5 s de progreso

        boost.TryActivate(tuning);

        Assert.Equal(0, boost.Charges);
        Assert.Equal(5f, boost.RechargeProgress, Precision);

        Advance(boost, tuning, 30);
        Assert.Equal(1, boost.Charges);
    }

    [Fact]
    public void RechargeKeepsRunningDuringCooldownAndImpulse()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);

        Advance(boost, tuning, 35);   // el cooldown dura 10 s; la recarga no se para

        Assert.Equal(1, boost.Charges);
    }

    // ---------------------------------------------------------------
    // Activación y cooldown
    // ---------------------------------------------------------------

    [Fact]
    public void TryActivate_WithoutCharges_DoesNothing()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);

        bool activated = boost.TryActivate(tuning);

        Assert.False(activated);
        Assert.Equal(BoostPhase.None, boost.Phase);
        Assert.False(boost.IsCooldownActive);
        Assert.Equal(0f, boost.Update(tuning, 0.25f));
    }

    [Fact]
    public void TryActivate_WithCharge_SpendsOneAndStartsCooldown()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 70);

        bool activated = boost.TryActivate(tuning);

        Assert.True(activated);
        Assert.Equal(1, boost.Charges);
        Assert.Equal(10f, boost.CooldownRemaining);
        Assert.Equal(BoostPhase.Ramp, boost.Phase);
    }

    [Fact]
    public void TryActivate_DuringCooldown_DoesNothing_UntilCooldownEnds()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 105);
        boost.TryActivate(tuning);

        Advance(boost, tuning, 9);
        Assert.False(boost.TryActivate(tuning));
        Assert.Equal(2, boost.Charges);

        Advance(boost, tuning, 1);
        Assert.True(boost.TryActivate(tuning));
        Assert.Equal(1, boost.Charges);
    }

    [Fact]
    public void Cooldown_IsActiveRightAfterActivating_AndEndsAfter10Seconds()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);

        Assert.True(boost.IsCooldownActive);

        Advance(boost, tuning, 9);
        Assert.True(boost.IsCooldownActive);

        Advance(boost, tuning, 1);
        Assert.False(boost.IsCooldownActive);
    }

    // ---------------------------------------------------------------
    // Impulso: subida y mantenimiento
    // ---------------------------------------------------------------

    [Fact]
    public void Ramp_AddsFullGainOverRampDuration()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);

        float half = boost.Update(tuning, 0.25f);

        Assert.Equal(50f, half, Precision);
        Assert.Equal(BoostPhase.Ramp, boost.Phase);

        float rest = boost.Update(tuning, 0.25f);

        Assert.Equal(100f, half + rest, Precision);
    }

    [Fact]
    public void Ramp_NeverAddsMoreThanTheGain()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);

        float total = 0f;
        for (int i = 0; i < 180; i++)
            total += boost.Update(tuning, 1f / 60f);

        Assert.Equal(100f, total, Precision);
    }

    [Fact]
    public void AfterRamp_HoldLasts2Seconds_ThenImpulseEnds()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);
        boost.Update(tuning, 0.25f);
        boost.Update(tuning, 0.25f);

        Assert.Equal(BoostPhase.Hold, boost.Phase);
        Assert.True(boost.IsImpulseActive);

        boost.Update(tuning, 1.5f);
        Assert.Equal(BoostPhase.Hold, boost.Phase);

        boost.Update(tuning, 0.5f);
        Assert.Equal(BoostPhase.None, boost.Phase);
        Assert.False(boost.IsImpulseActive);
    }

    [Fact]
    public void Hold_AddsNoSpeed()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);
        boost.Update(tuning, 0.5f);

        Assert.Equal(BoostPhase.Hold, boost.Phase);
        Assert.Equal(0f, boost.Update(tuning, 1f));
    }

    [Fact]
    public void ZeroHoldDuration_EndsImpulseRightAfterRamp()
    {
        var tuning = CreateTuning();
        tuning.HoldDuration = 0f;
        var boost = Activated(tuning);

        float gained = boost.Update(tuning, 0.5f);

        Assert.Equal(100f, gained, Precision);
        Assert.Equal(BoostPhase.None, boost.Phase);
    }

    [Fact]
    public void ZeroRampDuration_AddsWholeGainAtOnce()
    {
        var tuning = CreateTuning();
        tuning.RampDuration = 0f;
        var boost = Activated(tuning);

        Assert.Equal(0f, boost.Update(tuning, 0f));
        Assert.Equal(100f, boost.Update(tuning, 1f / 60f), Precision);
        Assert.Equal(BoostPhase.Hold, boost.Phase);
    }

    // ---------------------------------------------------------------
    // Cancelación por frenada
    // ---------------------------------------------------------------

    [Fact]
    public void CancelDuringRamp_KeepsGainSoFar_AndDoesNotRefundCharge()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);
        float gained = boost.Update(tuning, 0.25f);

        boost.CancelImpulse();

        Assert.Equal(50f, gained, Precision);
        Assert.Equal(BoostPhase.None, boost.Phase);
        Assert.Equal(0f, boost.Update(tuning, 0.25f));
        Assert.Equal(0, boost.Charges);
    }

    [Fact]
    public void CancelDuringHold_EndsImpulseAtOnce_AndDoesNotRefundCharge()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);
        boost.Update(tuning, 0.5f);
        boost.Update(tuning, 1f);
        Assert.Equal(BoostPhase.Hold, boost.Phase);

        boost.CancelImpulse();

        Assert.Equal(BoostPhase.None, boost.Phase);
        Assert.Equal(0f, boost.HoldRemaining);
        Assert.Equal(0, boost.Charges);
    }

    [Fact]
    public void CancelImpulse_DoesNotStopCooldown()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 70);
        boost.TryActivate(tuning);

        boost.CancelImpulse();

        Assert.True(boost.IsCooldownActive);
        Assert.False(boost.TryActivate(tuning));
        Assert.Equal(1, boost.Charges);
    }

    // ---------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------

    [Fact]
    public void Reset_ClearsEverything()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 80);
        boost.TryActivate(tuning);
        boost.Update(tuning, 0.5f);

        boost.Reset(tuning);

        Assert.Equal(0, boost.Charges);
        Assert.Equal(0f, boost.RechargeProgress);
        Assert.False(boost.IsCooldownActive);
        Assert.Equal(BoostPhase.None, boost.Phase);
        Assert.Equal(0f, boost.HoldRemaining);
    }

    // ---------------------------------------------------------------
    // Boost bloqueado (daños de nivel 3)
    // ---------------------------------------------------------------

    [Fact]
    public void NotAllowed_DoesNotActivate_ButKeepsRecharging()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 35);
        boost.ActivationAllowed = false;

        Assert.False(boost.CanActivate);
        Assert.False(boost.TryActivate(tuning));
        Assert.Equal(1, boost.Charges);
        Assert.Equal(BoostPhase.None, boost.Phase);

        Advance(boost, tuning, 35);
        Assert.Equal(2, boost.Charges);
    }

    [Fact]
    public void NotAllowed_DoesNotCancelImpulseInProgress()
    {
        var tuning = CreateTuning();
        var boost = Activated(tuning);
        float gained = boost.Update(tuning, 0.25f);

        boost.ActivationAllowed = false;
        gained += boost.Update(tuning, 0.25f);

        Assert.Equal(100f, gained, Precision);
        Assert.Equal(BoostPhase.Hold, boost.Phase);
        boost.Update(tuning, 2f);
        Assert.Equal(BoostPhase.None, boost.Phase);
    }

    [Fact]
    public void AllowedAgain_CanActivate()
    {
        var tuning = CreateTuning();
        var boost = NewBoost(tuning);
        Advance(boost, tuning, 35);
        boost.ActivationAllowed = false;
        boost.ActivationAllowed = true;

        Assert.True(boost.TryActivate(tuning));
    }
}
