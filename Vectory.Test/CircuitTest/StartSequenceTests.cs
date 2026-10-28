using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class StartSequenceTests
{
    // Valores de la spec: 1 s antes de la primera luz, 1 s entre luces, 5 luces, GO visible 1 s
    private static StartSettings CreateSettings() => new()
    {
        FirstLightDelay = 1.0,
        LightInterval = 1.0,
        LightCount = 5,
        GoDuration = 1.0
    };

    private static StartSequence NewSequence() => new(CreateSettings());

    // Avanza la secuencia en pasos de 1 s (exactos en double)
    private static void Advance(StartSequence sequence, int seconds)
    {
        for (int i = 0; i < seconds; i++)
            sequence.Update(1.0);
    }

    // ---------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_WithoutLights_Throws()
    {
        var settings = CreateSettings();
        settings.LightCount = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => new StartSequence(settings));
    }

    [Fact]
    public void Constructor_WithNonPositiveInterval_Throws()
    {
        var settings = CreateSettings();
        settings.LightInterval = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => new StartSequence(settings));
    }

    // ---------------------------------------------------------------
    // Secuencia
    // ---------------------------------------------------------------

    [Fact]
    public void AtStart_NoLightsNoNumberAndNotStarted()
    {
        var sequence = NewSequence();

        sequence.Update(0.0);

        Assert.Equal(0, sequence.LightsOn);
        Assert.Null(sequence.CountdownNumber);
        Assert.False(sequence.ShowGo);
        Assert.False(sequence.HasStarted);
    }

    [Fact]
    public void BeforeFirstLight_NothingIsShown()
    {
        var sequence = NewSequence();

        sequence.Update(0.5);

        Assert.Equal(0, sequence.LightsOn);
        Assert.Null(sequence.CountdownNumber);
    }

    [Theory]
    [InlineData(1, 1, 5)]
    [InlineData(2, 2, 4)]
    [InlineData(3, 3, 3)]
    [InlineData(4, 4, 2)]
    [InlineData(5, 5, 1)]
    public void EachSecond_TurnsOnOneMoreLightAndCountsDown(int seconds, int lights, int number)
    {
        var sequence = NewSequence();

        Advance(sequence, seconds);

        Assert.Equal(lights, sequence.LightsOn);
        Assert.Equal(number, sequence.CountdownNumber);
        Assert.False(sequence.HasStarted);
        Assert.False(sequence.ShowGo);
    }

    [Fact]
    public void BetweenLights_KeepsThePreviousState()
    {
        var sequence = NewSequence();

        sequence.Update(2.5);

        Assert.Equal(2, sequence.LightsOn);
        Assert.Equal(4, sequence.CountdownNumber);
    }

    [Fact]
    public void AtSixSeconds_LightsGoOutAndGoIsShown()
    {
        var sequence = NewSequence();

        Advance(sequence, 6);

        Assert.Equal(0, sequence.LightsOn);
        Assert.Null(sequence.CountdownNumber);
        Assert.True(sequence.ShowGo);
        Assert.True(sequence.HasStarted);
    }

    [Fact]
    public void GoTime_IsFirstDelayPlusAllLights()
    {
        Assert.Equal(6.0, NewSequence().GoTime);
    }

    [Fact]
    public void Started_IsRaisedOnlyOnce()
    {
        var sequence = NewSequence();
        int starts = 0;
        sequence.Started += () => starts++;

        Advance(sequence, 5);
        Assert.Equal(0, starts);

        Advance(sequence, 1);
        Assert.Equal(1, starts);

        Advance(sequence, 10);
        Assert.Equal(1, starts);
    }

    [Fact]
    public void Started_IsRaisedEvenWithOneBigStep()
    {
        var sequence = NewSequence();
        int starts = 0;
        sequence.Started += () => starts++;

        sequence.Update(20.0);

        Assert.Equal(1, starts);
        Assert.True(sequence.HasStarted);
    }

    [Fact]
    public void GoIsVisibleForOneSecond()
    {
        var sequence = NewSequence();
        Advance(sequence, 6);

        sequence.Update(0.5);
        Assert.True(sequence.ShowGo);

        sequence.Update(0.5);   // 7 s
        Assert.False(sequence.ShowGo);
        Assert.True(sequence.HasStarted);
    }

    [Fact]
    public void Update_WithNegativeDelta_DoesNotGoBack()
    {
        var sequence = NewSequence();
        Advance(sequence, 3);

        sequence.Update(-2.0);

        Assert.Equal(3.0, sequence.Elapsed);
        Assert.Equal(3, sequence.LightsOn);
    }

    [Fact]
    public void FramesOf60Hz_TurnOnLightsAtTheRightSecond()
    {
        var sequence = NewSequence();

        // 0,5 s después de la primera luz, con pasos de un frame
        for (int i = 0; i < 90; i++)
            sequence.Update(1.0 / 60.0);

        Assert.Equal(1, sequence.LightsOn);
        Assert.Equal(5, sequence.CountdownNumber);
    }
}
