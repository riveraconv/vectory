using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class RespawnDetectorTests
{
    private const float Kmh = 1f / 3.6f;
    private const float Dt = 0.1f;

    private static RespawnDetector Create() => new(new RespawnSettings
    {
        BarrierOffset = 20.5f,
        StuckSpeed = 10f * Kmh,
        StuckTime = 3f,
        OutsideMargin = 2f
    });

    // Llama a Update "steps" veces y cuenta cuántas veces pidió recolocar
    private static int Run(RespawnDetector detector, int steps, bool contact, float speed, float lateral = 17f)
    {
        int count = 0;
        for (int i = 0; i < steps; i++)
        {
            if (detector.Update(Dt, contact, speed, lateral))
                count++;
        }
        return count;
    }

    [Fact]
    public void StuckAgainstBarrier_2_9s_NoRespawn_3s_RespawnOnce()
    {
        var detector = Create();

        Assert.Equal(0, Run(detector, 29, true, 5f * Kmh));
        Assert.True(detector.Update(Dt, true, 5f * Kmh, 17f));
        Assert.False(detector.Update(Dt, true, 5f * Kmh, 17f));
    }

    [Fact]
    public void SlowWithoutContact_10s_NoRespawn()
    {
        var detector = Create();

        Assert.Equal(0, Run(detector, 100, false, 5f * Kmh, 0f));
    }

    [Fact]
    public void OutsideTrack_RespawnImmediately()
    {
        var detector = Create();

        Assert.True(detector.Update(Dt, false, 50f, 23f));
        Assert.True(Create().Update(Dt, false, 50f, -23f));
    }

    [Fact]
    public void JustBeyondBarrierButWithinMargin_NoRespawn()
    {
        var detector = Create();

        Assert.False(detector.Update(Dt, false, 50f, 22.4f));
    }

    [Fact]
    public void LosingContact_ResetsCounter()
    {
        var detector = Create();

        Run(detector, 20, true, 5f * Kmh);
        detector.Update(Dt, false, 5f * Kmh, 17f);
        Assert.Equal(0f, detector.StuckTimer);
        Assert.Equal(0, Run(detector, 29, true, 5f * Kmh));
    }

    [Fact]
    public void SpeedingUp_ResetsCounter()
    {
        var detector = Create();

        Run(detector, 20, true, 5f * Kmh);
        detector.Update(Dt, true, 15f * Kmh, 17f);
        Assert.Equal(0f, detector.StuckTimer);
        Assert.Equal(0, Run(detector, 29, true, 5f * Kmh));
    }
}
