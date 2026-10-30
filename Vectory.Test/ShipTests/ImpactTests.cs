using Vectory.Circuit;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class ImpactRecoveryTests
{
    [Fact]
    public void AfterStrongImpact_FactorGoesFromHalfToOneInOneSecond()
    {
        var recovery = new ImpactRecovery();
        recovery.Start();

        Assert.Equal(0.5f, recovery.Factor, 4);
        recovery.Update(0.5f);
        Assert.Equal(0.75f, recovery.Factor, 4);
        recovery.Update(0.5f);
        Assert.Equal(1f, recovery.Factor, 4);
        Assert.False(recovery.IsActive);
    }

    [Fact]
    public void AnotherStrongImpact_RestartsAtHalf()
    {
        var recovery = new ImpactRecovery();
        recovery.Start();
        recovery.Update(0.5f);

        recovery.Start();

        Assert.Equal(0.5f, recovery.Factor, 4);
    }

    [Fact]
    public void Cancel_BackToFullControl()
    {
        var recovery = new ImpactRecovery();
        recovery.Start();

        recovery.Cancel();

        Assert.Equal(1f, recovery.Factor);
    }
}

public class CameraShakeTests
{
    private static CameraShake Create() => new(CameraShakeSettings.Default);

    [Fact]
    public void Medium_HasAmplitude_AndStopsAt0_25s()
    {
        var shake = Create();
        shake.Trigger(ImpactLevel.Medium);

        Assert.True(shake.Amplitude > 0f);
        shake.Update(0.1f);
        Assert.True(shake.Amplitude > 0f);
        Assert.True(shake.Amplitude < 0.06f);

        shake.Update(0.15f);
        Assert.Equal(0f, shake.Amplitude);
        Assert.False(shake.IsActive);
        Assert.Equal(0f, shake.Offset.Length());
        Assert.Equal(0f, shake.Roll);
    }

    [Fact]
    public void Graze_DoesNotShake()
    {
        var shake = Create();
        shake.Trigger(ImpactLevel.Graze);

        Assert.False(shake.IsActive);
    }

    [Fact]
    public void MediumDuringStrong_IsIgnored()
    {
        var shake = Create();
        shake.Trigger(ImpactLevel.Strong);
        shake.Update(0.1f);
        float before = shake.Amplitude;

        shake.Trigger(ImpactLevel.Medium);

        Assert.Equal(before, shake.Amplitude);
        shake.Update(0.2f);
        Assert.True(shake.IsActive);   // sigue el fuerte (0,4 s), no el medio (0,25 s)
    }

    [Fact]
    public void StrongDuringMedium_Replaces()
    {
        var shake = Create();
        shake.Trigger(ImpactLevel.Medium);
        shake.Update(0.1f);

        shake.Trigger(ImpactLevel.Strong);

        Assert.Equal(0.15f, shake.Amplitude, 4);
    }

    [Fact]
    public void SameLevelDuringShake_IsIgnored()
    {
        var shake = Create();
        shake.Trigger(ImpactLevel.Medium);
        shake.Update(0.2f);

        shake.Trigger(ImpactLevel.Medium);
        shake.Update(0.05f);

        Assert.False(shake.IsActive);
    }
}
