using System;
using Vectory.Records;
using Xunit;

namespace Vectory.Test.RecordsTests;

public class RecordBookTests
{
    private const string Circuit = "circuit_01";
    private static readonly DateTime When = new(2026, 10, 29, 12, 0, 0);
    private static readonly double[] Splits = { 20.0, 45.0, 60.0 };

    [Fact]
    public void TimeTrial_NoPreviousRecord_FirstLapIsRecord()
    {
        var book = new RecordBook();

        TimeTrialLapResult result = book.SubmitTimeTrialLap(Circuit, 72.384, Splits, When);

        Assert.True(result.IsNewRecord);
        Assert.Null(result.PreviousLapTime);
        Assert.Equal(72.384, book.GetTimeTrial(Circuit).LapTime);
        Assert.Equal(Splits, book.GetTimeTrial(Circuit).Splits);
    }

    [Fact]
    public void TimeTrial_SlowerDoesNotChange_FasterDoes_TieDoesNot()
    {
        var book = new RecordBook();
        book.SubmitTimeTrialLap(Circuit, 72.0, Splits, When);

        TimeTrialLapResult slower = book.SubmitTimeTrialLap(Circuit, 73.0, Splits, When);
        Assert.False(slower.IsNewRecord);
        Assert.Equal(72.0, slower.PreviousLapTime);
        Assert.Equal(72.0, book.GetTimeTrial(Circuit).LapTime);

        TimeTrialLapResult tie = book.SubmitTimeTrialLap(Circuit, 72.0, new[] { 1.0, 2.0, 3.0 }, When);
        Assert.False(tie.IsNewRecord);
        Assert.Equal(Splits, book.GetTimeTrial(Circuit).Splits);

        TimeTrialLapResult faster = book.SubmitTimeTrialLap(Circuit, 71.5, Splits, When);
        Assert.True(faster.IsNewRecord);
        Assert.Equal(72.0, faster.PreviousLapTime);
        Assert.Equal(71.5, book.GetTimeTrial(Circuit).LapTime);
    }

    [Fact]
    public void RaceAndTimeTrial_AreIndependent()
    {
        var book = new RecordBook();

        book.SubmitTimeTrialLap(Circuit, 70.0, Splits, When);
        RaceRecordResult race = book.SubmitRace(Circuit, 230.0, 75.0, When);

        // La vuelta de RACE (75) no toca el récord de TIME TRIAL (70), y al revés
        Assert.True(race.NewBestLap);
        Assert.Equal(70.0, book.GetTimeTrial(Circuit).LapTime);
        Assert.Equal(75.0, book.GetRace(Circuit).BestLap.Time);
    }

    [Fact]
    public void Race_TotalAndBestLap_ComparedSeparately()
    {
        var book = new RecordBook();
        RaceRecordResult first = book.SubmitRace(Circuit, 230.0, 75.0, When);
        Assert.True(first.NewTotalTime);
        Assert.True(first.NewBestLap);

        // Total peor pero vuelta mejor
        RaceRecordResult second = book.SubmitRace(Circuit, 235.0, 74.0, When);
        Assert.False(second.NewTotalTime);
        Assert.True(second.NewBestLap);

        // Total mejor pero vuelta peor
        RaceRecordResult third = book.SubmitRace(Circuit, 228.0, 76.0, When);
        Assert.True(third.NewTotalTime);
        Assert.False(third.NewBestLap);

        // Empates: nada nuevo
        RaceRecordResult tie = book.SubmitRace(Circuit, 228.0, 74.0, When);
        Assert.False(tie.NewTotalTime);
        Assert.False(tie.NewBestLap);

        Assert.Equal(228.0, book.GetRace(Circuit).TotalTime.Time);
        Assert.Equal(74.0, book.GetRace(Circuit).BestLap.Time);
    }

    [Fact]
    public void Splits_OtherCheckpointCount_NotValidForDelta_ButRecordTimeKept()
    {
        var book = new RecordBook();
        book.SubmitTimeTrialLap(Circuit, 72.0, new[] { 10.0, 20.0, 30.0, 40.0, 50.0 }, When);

        TimeTrialRecord record = book.GetTimeTrial(Circuit);

        Assert.False(record.SplitsMatch(3));
        Assert.True(record.SplitsMatch(5));
        Assert.Equal(72.0, record.LapTime);
    }

    [Fact]
    public void Json_RoundTrip_KeepsRecords()
    {
        var book = new RecordBook();
        book.SubmitTimeTrialLap(Circuit, 72.384, Splits, When);
        book.SubmitRace(Circuit, 230.5, 75.25, When);
        book.SubmitTimeTrialLap("circuit_02", 90.0, new[] { 30.0 }, When);

        Assert.True(RecordBook.TryFromJson(book.ToJson(), out RecordBook loaded, out string error), error);

        Assert.Equal(72.384, loaded.GetTimeTrial(Circuit).LapTime);
        Assert.Equal(Splits, loaded.GetTimeTrial(Circuit).Splits);
        Assert.Equal(book.GetTimeTrial(Circuit).Date, loaded.GetTimeTrial(Circuit).Date);
        Assert.Equal(230.5, loaded.GetRace(Circuit).TotalTime.Time);
        Assert.Equal(75.25, loaded.GetRace(Circuit).BestLap.Time);
        Assert.Equal(90.0, loaded.GetTimeTrial("circuit_02").LapTime);
        Assert.Null(loaded.GetRace("circuit_02"));
    }

    [Fact]
    public void Json_ContainsFormatVersion()
    {
        Assert.Contains("\"version\": 1", new RecordBook().ToJson());
    }

    [Theory]
    [InlineData("{ \"version\": 1, \"circuits\": { \"circuit_01\": { \"timeTrial\": { \"lapTime\": ")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("{ \"version\": 99, \"circuits\": {} }")]
    [InlineData("null")]
    public void Json_CorruptOrUnknownVersion_LoadsEmptyWithoutException(string json)
    {
        bool ok = RecordBook.TryFromJson(json, out RecordBook book, out string error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.NotNull(book);
        Assert.Null(book.GetTimeTrial(Circuit));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CircuitWithoutId_SavesNothing(string? id)
    {
        var book = new RecordBook();

        TimeTrialLapResult lap = book.SubmitTimeTrialLap(id, 70.0, Splits, When);
        RaceRecordResult race = book.SubmitRace(id, 230.0, 75.0, When);

        Assert.False(lap.IsNewRecord);
        Assert.False(race.NewTotalTime);
        Assert.False(race.NewBestLap);
        Assert.Null(book.GetTimeTrial(id));
        Assert.Null(book.GetRace(id));
        Assert.True(RecordBook.TryFromJson(book.ToJson(), out RecordBook loaded, out _));
        Assert.Null(loaded.GetTimeTrial(Circuit));
    }
}
