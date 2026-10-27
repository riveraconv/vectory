using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class RaceTrackerTests
{
    private const int Precision = 6;

    private static RaceTracker NewStarted(int checkpoints = 3, int laps = 3)
    {
        var tracker = new RaceTracker(checkpoints, laps);
        tracker.Start();
        return tracker;
    }

    // Recorre una vuelta completa: sectorTimes[i] es lo que tarda el sector i.
    // Hay que pasar CheckpointCount + 1 tiempos (el último sector acaba en la meta).
    private static void DriveLap(RaceTracker tracker, params double[] sectorTimes)
    {
        for (int i = 0; i < tracker.CheckpointCount; i++)
        {
            tracker.Tick(sectorTimes[i]);
            tracker.CheckpointCrossed(i);
        }

        tracker.Tick(sectorTimes[tracker.CheckpointCount]);
        tracker.FinishLineCrossed();
    }

    private static void AssertTime(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual!.Value, Precision);
    }

    // ---------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_WithoutCheckpoints_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RaceTracker(0));
    }

    [Fact]
    public void Constructor_WithoutLaps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RaceTracker(3, 0));
    }

    [Fact]
    public void SectorCount_IsCheckpointsPlusOne()
    {
        var tracker = new RaceTracker(5);

        Assert.Equal(6, tracker.SectorCount);
        Assert.Equal(6, tracker.BestSectorTimes.Count);
    }

    [Fact]
    public void Constructor_UsesThreeLapsByDefault()
    {
        var tracker = new RaceTracker(5);

        Assert.Equal(3, tracker.TotalLaps);
    }

    // ---------------------------------------------------------------
    // Antes de empezar
    // ---------------------------------------------------------------

    [Fact]
    public void BeforeStart_TickCheckpointAndFinish_AreIgnored()
    {
        var tracker = new RaceTracker(2);
        int events = 0;
        tracker.CheckpointPassed += _ => events++;
        tracker.SectorCompleted += (_, _, _, _) => events++;
        tracker.LapCompleted += (_, _, _) => events++;

        tracker.Tick(5.0);
        tracker.CheckpointCrossed(0);
        tracker.FinishLineCrossed();

        Assert.False(tracker.IsRunning);
        Assert.Equal(0.0, tracker.TotalTime);
        Assert.Equal(0, tracker.NextCheckpointIndex);
        Assert.Equal(0, events);
    }

    [Fact]
    public void Start_BeginsFirstLapWithCleanState()
    {
        var tracker = NewStarted();

        Assert.True(tracker.IsRunning);
        Assert.False(tracker.IsFinished);
        Assert.Equal(1, tracker.CurrentLap);
        Assert.Equal(0, tracker.NextCheckpointIndex);
        Assert.Equal(0, tracker.CurrentSectorIndex);
        Assert.Equal(0.0, tracker.CurrentLapTime);
        Assert.Equal(0.0, tracker.TotalTime);
        Assert.Null(tracker.BestLapTime);
        Assert.Null(tracker.LastLapTime);
        Assert.Null(tracker.LastSectorTime);
    }

    // ---------------------------------------------------------------
    // Cronómetro
    // ---------------------------------------------------------------

    [Fact]
    public void Tick_AccumulatesLapSectorAndTotalTime()
    {
        var tracker = NewStarted();

        tracker.Tick(1.5);
        tracker.Tick(0.5);

        Assert.Equal(2.0, tracker.CurrentLapTime, Precision);
        Assert.Equal(2.0, tracker.CurrentSectorTime, Precision);
        Assert.Equal(2.0, tracker.TotalTime, Precision);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Tick_WithNonPositiveDelta_IsIgnored(double delta)
    {
        var tracker = NewStarted();

        tracker.Tick(delta);

        Assert.Equal(0.0, tracker.TotalTime);
    }

    // ---------------------------------------------------------------
    // Checkpoints
    // ---------------------------------------------------------------

    [Fact]
    public void CheckpointCrossed_InOrder_AdvancesToNextSector()
    {
        var tracker = NewStarted();
        var passed = new List<int>();
        tracker.CheckpointPassed += passed.Add;

        tracker.CheckpointCrossed(0);
        tracker.CheckpointCrossed(1);

        Assert.Equal(2, tracker.NextCheckpointIndex);
        Assert.Equal(2, tracker.CurrentSectorIndex);
        Assert.Equal(new[] { 0, 1 }, passed);
    }

    [Fact]
    public void CheckpointCrossed_OutOfOrder_IsIgnored()
    {
        var tracker = NewStarted();
        int events = 0;
        tracker.CheckpointPassed += _ => events++;

        tracker.CheckpointCrossed(2);

        Assert.Equal(0, tracker.NextCheckpointIndex);
        Assert.Equal(0, events);
    }

    [Fact]
    public void CheckpointCrossed_SameCheckpointTwice_CountsOnlyOnce()
    {
        var tracker = NewStarted();
        int events = 0;
        tracker.CheckpointPassed += _ => events++;

        tracker.CheckpointCrossed(0);
        tracker.CheckpointCrossed(0);

        Assert.Equal(1, tracker.NextCheckpointIndex);
        Assert.Equal(1, events);
    }

    // ---------------------------------------------------------------
    // Meta y vueltas
    // ---------------------------------------------------------------

    [Fact]
    public void FinishLineCrossed_WithoutAllCheckpoints_IsIgnored()
    {
        var tracker = NewStarted();
        int laps = 0;
        tracker.LapCompleted += (_, _, _) => laps++;
        tracker.CheckpointCrossed(0);
        tracker.CheckpointCrossed(1);

        tracker.FinishLineCrossed();

        Assert.Equal(1, tracker.CurrentLap);
        Assert.Equal(2, tracker.NextCheckpointIndex);
        Assert.Null(tracker.LastLapTime);
        Assert.Equal(0, laps);
    }

    [Fact]
    public void FinishLineCrossed_AtStartOfRace_IsIgnored()
    {
        // La nave sale unos metros antes de la meta y la cruza nada más arrancar
        var tracker = NewStarted();
        tracker.Tick(2.0);

        tracker.FinishLineCrossed();

        Assert.Equal(1, tracker.CurrentLap);
        Assert.Null(tracker.LastLapTime);
        Assert.Equal(2.0, tracker.CurrentLapTime, Precision);
    }

    [Fact]
    public void FinishLineCrossed_WithAllCheckpoints_CompletesLapAndStartsNext()
    {
        var tracker = NewStarted();

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);

        Assert.Equal(2, tracker.CurrentLap);
        Assert.Equal(0, tracker.NextCheckpointIndex);
        Assert.Equal(0.0, tracker.CurrentLapTime);
        Assert.Equal(0.0, tracker.CurrentSectorTime);
        AssertTime(10.0, tracker.LastLapTime);
        Assert.True(tracker.IsRunning);
    }

    [Fact]
    public void TotalTime_KeepsAccumulatingAcrossLaps()
    {
        var tracker = NewStarted();

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);
        DriveLap(tracker, 1.0, 1.0, 1.0, 1.0);

        Assert.Equal(14.0, tracker.TotalTime, Precision);
    }

    [Fact]
    public void CheckpointsMustBePassedAgainEachLap()
    {
        var tracker = NewStarted();
        DriveLap(tracker, 1.0, 1.0, 1.0, 1.0);
        int laps = 0;
        tracker.LapCompleted += (_, _, _) => laps++;

        // Segunda vuelta: cruzar la meta sin pasar los checkpoints no cuenta
        tracker.Tick(1.0);
        tracker.FinishLineCrossed();

        Assert.Equal(2, tracker.CurrentLap);
        Assert.Equal(0, laps);
    }

    [Fact]
    public void LapCompleted_ReportsLapNumberTimeAndBestFlag()
    {
        var tracker = NewStarted();
        var laps = new List<(int Lap, double Time, bool IsBest)>();
        tracker.LapCompleted += (lap, time, isBest) => laps.Add((lap, time, isBest));

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);   // 10 s
        DriveLap(tracker, 1.0, 2.0, 3.0, 3.0);   // 9 s, mejora
        DriveLap(tracker, 3.0, 3.0, 3.0, 3.0);   // 12 s, no mejora

        Assert.Equal(3, laps.Count);
        Assert.Equal((1, 10.0, true), laps[0]);
        Assert.Equal((2, 9.0, true), laps[1]);
        Assert.Equal((3, 12.0, false), laps[2]);
    }

    [Fact]
    public void BestLapTime_KeepsFastestLap()
    {
        var tracker = NewStarted();

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);   // 10 s
        DriveLap(tracker, 1.0, 2.0, 3.0, 3.0);   // 9 s
        DriveLap(tracker, 3.0, 3.0, 3.0, 3.0);   // 12 s

        AssertTime(9.0, tracker.BestLapTime);
        AssertTime(12.0, tracker.LastLapTime);
    }

    [Fact]
    public void LapEqualToBest_IsNotMarkedAsBest()
    {
        var tracker = NewStarted();
        var flags = new List<bool>();
        tracker.LapCompleted += (_, _, isBest) => flags.Add(isBest);

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);
        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);

        Assert.Equal(new[] { true, false }, flags);
    }

    // ---------------------------------------------------------------
    // Sectores
    // ---------------------------------------------------------------

    [Fact]
    public void CheckpointCrossed_ClosesSectorAndRestartsSectorTimer()
    {
        var tracker = NewStarted();

        tracker.Tick(1.5);
        tracker.CheckpointCrossed(0);

        AssertTime(1.5, tracker.LastSectorTime);
        Assert.Equal(0.0, tracker.CurrentSectorTime);
        // El tiempo de vuelta sigue corriendo
        Assert.Equal(1.5, tracker.CurrentLapTime, Precision);
    }

    [Fact]
    public void SectorTimes_AddUpToLapTime()
    {
        var tracker = NewStarted();
        var sectors = new List<double>();
        double lapTime = 0;
        tracker.SectorCompleted += (_, _, time, _) => sectors.Add(time);
        tracker.LapCompleted += (_, time, _) => lapTime = time;

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);

        Assert.Equal(4, sectors.Count);
        Assert.Equal(lapTime, sectors.Sum(), Precision);
    }

    [Fact]
    public void SectorCompleted_ReportsLapSectorTimeAndBestFlag()
    {
        var tracker = NewStarted();
        var sectors = new List<(int Lap, int Sector, double Time, bool IsBest)>();
        tracker.SectorCompleted += (lap, sector, time, isBest) => sectors.Add((lap, sector, time, isBest));

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);
        DriveLap(tracker, 2.0, 1.0, 3.0, 5.0);

        Assert.Equal(8, sectors.Count);

        // Vuelta 1: todo es mejor porque no hay con qué comparar
        Assert.Equal((1, 0, 1.0, true), sectors[0]);
        Assert.Equal((1, 1, 2.0, true), sectors[1]);
        Assert.Equal((1, 2, 3.0, true), sectors[2]);
        Assert.Equal((1, 3, 4.0, true), sectors[3]);

        // Vuelta 2: solo mejora el sector 1; el 2 empata y no cuenta
        Assert.Equal((2, 0, 2.0, false), sectors[4]);
        Assert.Equal((2, 1, 1.0, true), sectors[5]);
        Assert.Equal((2, 2, 3.0, false), sectors[6]);
        Assert.Equal((2, 3, 5.0, false), sectors[7]);
    }

    [Fact]
    public void BestSectorTimes_KeepsFastestOfEachSector()
    {
        var tracker = NewStarted();

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);
        DriveLap(tracker, 2.0, 1.0, 3.0, 5.0);

        AssertTime(1.0, tracker.BestSectorTimes[0]);
        AssertTime(1.0, tracker.BestSectorTimes[1]);
        AssertTime(3.0, tracker.BestSectorTimes[2]);
        AssertTime(4.0, tracker.BestSectorTimes[3]);
    }

    [Fact]
    public void BestSectorTimes_AreNullUntilSectorIsCompleted()
    {
        var tracker = NewStarted();

        tracker.Tick(1.0);
        tracker.CheckpointCrossed(0);

        Assert.NotNull(tracker.BestSectorTimes[0]);
        Assert.Null(tracker.BestSectorTimes[1]);
        Assert.Null(tracker.BestSectorTimes[2]);
        Assert.Null(tracker.BestSectorTimes[3]);
    }

    [Fact]
    public void LastSector_IsClosedByFinishLine_BeforeLapCompletedEvent()
    {
        var tracker = new RaceTracker(1, 1);
        tracker.Start();
        var order = new List<string>();
        tracker.CheckpointPassed += i => order.Add($"checkpoint {i}");
        tracker.SectorCompleted += (_, sector, _, _) => order.Add($"sector {sector}");
        tracker.LapCompleted += (lap, _, _) => order.Add($"lap {lap}");
        tracker.RaceFinished += _ => order.Add("race");

        tracker.Tick(1.0);
        tracker.CheckpointCrossed(0);
        tracker.Tick(2.0);
        tracker.FinishLineCrossed();

        Assert.Equal(
            new[] { "checkpoint 0", "sector 0", "sector 1", "lap 1", "race" },
            order);
    }

    // ---------------------------------------------------------------
    // Fin de carrera
    // ---------------------------------------------------------------

    [Fact]
    public void RaceFinished_FiresAfterLastLapWithTotalTime()
    {
        var tracker = NewStarted(3, 2);
        var finished = new List<double>();
        tracker.RaceFinished += finished.Add;

        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);
        Assert.Empty(finished);
        Assert.False(tracker.IsFinished);

        DriveLap(tracker, 1.0, 1.0, 1.0, 1.0);

        Assert.True(tracker.IsFinished);
        Assert.False(tracker.IsRunning);
        Assert.Single(finished);
        Assert.Equal(14.0, finished[0], Precision);
    }

    [Fact]
    public void AfterRaceFinished_LapNumberStaysAtLastLap()
    {
        var tracker = NewStarted(3, 2);

        DriveLap(tracker, 1.0, 1.0, 1.0, 1.0);
        DriveLap(tracker, 1.0, 1.0, 1.0, 1.0);

        Assert.Equal(2, tracker.CurrentLap);
    }

    [Fact]
    public void AfterRaceFinished_EverythingIsIgnored()
    {
        var tracker = NewStarted(3, 1);
        DriveLap(tracker, 1.0, 1.0, 1.0, 1.0);
        double totalTime = tracker.TotalTime;
        int events = 0;
        tracker.CheckpointPassed += _ => events++;
        tracker.SectorCompleted += (_, _, _, _) => events++;
        tracker.LapCompleted += (_, _, _) => events++;
        tracker.RaceFinished += _ => events++;

        tracker.Tick(5.0);
        tracker.CheckpointCrossed(0);
        tracker.FinishLineCrossed();

        Assert.Equal(totalTime, tracker.TotalTime, Precision);
        Assert.Equal(0, events);
    }

    [Fact]
    public void Start_AfterRace_ResetsEverything()
    {
        var tracker = NewStarted(3, 1);
        DriveLap(tracker, 1.0, 2.0, 3.0, 4.0);

        tracker.Start();

        Assert.True(tracker.IsRunning);
        Assert.False(tracker.IsFinished);
        Assert.Equal(1, tracker.CurrentLap);
        Assert.Equal(0, tracker.NextCheckpointIndex);
        Assert.Equal(0.0, tracker.TotalTime);
        Assert.Equal(0.0, tracker.CurrentLapTime);
        Assert.Equal(0.0, tracker.CurrentSectorTime);
        Assert.Null(tracker.BestLapTime);
        Assert.Null(tracker.LastLapTime);
        Assert.Null(tracker.LastSectorTime);
        Assert.All(tracker.BestSectorTimes, time => Assert.Null(time));
    }

    [Fact]
    public void SingleCheckpoint_GivesTwoSectorsPerLap()
    {
        var tracker = new RaceTracker(1, 1);
        tracker.Start();

        DriveLap(tracker, 3.0, 4.0);

        Assert.Equal(2, tracker.SectorCount);
        AssertTime(3.0, tracker.BestSectorTimes[0]);
        AssertTime(4.0, tracker.BestSectorTimes[1]);
        AssertTime(7.0, tracker.BestLapTime);
    }
}
