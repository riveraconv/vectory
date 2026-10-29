using System;
using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

// Preparación de la salida lanzada de TIME TRIAL: READY? 5 s, GO! 1 s
public class ReadySequenceTests
{
    private static ReadySequence Create() => new(new ReadySettings { ReadyDuration = 5.0, GoDuration = 1.0 });

    [Fact]
    public void Start_ShowsReady_NotStarted()
    {
        var ready = Create();
        ready.Update(0);

        Assert.True(ready.ShowReady);
        Assert.False(ready.ShowGo);
        Assert.False(ready.HasStarted);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.99)]
    public void BeforeFiveSeconds_StillReady(double elapsed)
    {
        var ready = Create();
        ready.Update(elapsed);

        Assert.True(ready.ShowReady);
        Assert.False(ready.ShowGo);
        Assert.False(ready.HasStarted);
    }

    [Fact]
    public void AtFiveSeconds_GoVisibleAndStarted()
    {
        var ready = Create();
        ready.Update(5.0);

        Assert.False(ready.ShowReady);
        Assert.True(ready.ShowGo);
        Assert.True(ready.HasStarted);
    }

    [Fact]
    public void StartedEvent_FiresOnce()
    {
        var ready = Create();
        int count = 0;
        ready.Started += () => count++;

        for (int i = 0; i < 80; i++)
            ready.Update(0.1);   // 8 s en pasos de 0,1 s

        Assert.Equal(1, count);
    }

    [Fact]
    public void AtSixSeconds_GoHidden()
    {
        var ready = Create();
        ready.Update(5.0);
        ready.Update(1.0);

        Assert.False(ready.ShowReady);
        Assert.False(ready.ShowGo);
        Assert.True(ready.HasStarted);
    }

    [Fact]
    public void ZeroDelta_DoesNotAdvance()
    {
        var ready = Create();
        ready.Update(3.0);
        ready.Update(0);
        ready.Update(-1.0);

        Assert.Equal(3.0, ready.Elapsed);
        Assert.False(ready.HasStarted);
    }

    [Fact]
    public void NegativeDurations_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadySequence(new ReadySettings { ReadyDuration = -1, GoDuration = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadySequence(new ReadySettings { ReadyDuration = 5, GoDuration = -1 }));
    }
}
