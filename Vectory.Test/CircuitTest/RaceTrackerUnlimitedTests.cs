using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

// Vueltas libres y salida lanzada (TIME TRIAL)
public class RaceTrackerUnlimitedTests
{
    private const int Checkpoints = 3;

    private static RaceTracker NewUnlimited() => new(Checkpoints, RaceTracker.UnlimitedLaps);

    // Una vuelta completa: un tiempo por sector, pasando por todos los checkpoints y la meta
    private static void CompleteLap(RaceTracker tracker, double sectorTime = 10.0)
    {
        for (int i = 0; i < Checkpoints; i++)
        {
            tracker.Tick(sectorTime);
            tracker.CheckpointCrossed(i);
        }
        tracker.Tick(sectorTime);
        tracker.FinishLineCrossed();
    }

    [Fact]
    public void UnlimitedLaps_TenLaps_NeverFinishes()
    {
        var tracker = NewUnlimited();
        tracker.Start();

        for (int lap = 0; lap < 10; lap++)
            CompleteLap(tracker);

        Assert.False(tracker.HasLapLimit);
        Assert.Equal(RaceState.Running, tracker.State);
        Assert.False(tracker.IsFinished);
        Assert.Null(tracker.Result);
        Assert.Equal(11, tracker.CurrentLap);
    }

    [Fact]
    public void BeforeStart_CheckpointsAndTimeChangeNothing()
    {
        var tracker = NewUnlimited();

        tracker.Tick(30.0);
        tracker.CheckpointCrossed(0);
        tracker.FinishLineCrossed();

        Assert.Equal(RaceState.PreStart, tracker.State);
        Assert.Equal(0, tracker.CurrentLapTime);
        Assert.Equal(0, tracker.NextCheckpointIndex);
        Assert.Empty(tracker.CurrentLapSplits);
        Assert.Null(tracker.LastLapTime);
    }

    [Fact]
    public void StartAtFinishLine_LapOneCountsFromZero()
    {
        var tracker = NewUnlimited();
        tracker.Tick(12.0);     // vuelta de salida: no cuenta

        tracker.Start();

        Assert.Equal(1, tracker.CurrentLap);
        Assert.Equal(0, tracker.CurrentLapTime);

        CompleteLap(tracker, 10.0);
        Assert.Equal(40.0, tracker.LastLapTime);
    }

    [Fact]
    public void Splits_RecordedAtEachCheckpoint_AndResetOnNewLap()
    {
        var tracker = NewUnlimited();
        tracker.Start();

        tracker.Tick(10.0);
        tracker.CheckpointCrossed(0);
        tracker.Tick(12.0);
        tracker.CheckpointCrossed(1);

        Assert.Equal(new[] { 10.0, 22.0 }, tracker.CurrentLapSplits);

        tracker.Tick(8.0);
        tracker.CheckpointCrossed(2);
        tracker.Tick(5.0);
        tracker.FinishLineCrossed();

        Assert.Equal(new[] { 10.0, 22.0, 30.0 }, tracker.LastLapSplits);
        Assert.Empty(tracker.CurrentLapSplits);
        Assert.Equal(2, tracker.CurrentLap);
    }

    [Fact]
    public void LimitedRace_ResultIncludesTotalTime()
    {
        var tracker = new RaceTracker(Checkpoints, totalLaps: 2);
        tracker.Start();

        CompleteLap(tracker, 10.0);
        CompleteLap(tracker, 11.0);

        Assert.True(tracker.IsFinished);
        Assert.Equal(84.0, tracker.Result.TotalTime, 6);
    }
}
