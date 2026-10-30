using System;
using Vectory.Records;
using Xunit;

namespace Vectory.Test.RecordsTests;

// Borrado y lectura tolerante del archivo de récords (spec RECORDS, 5.2 y 5.4)
public class RecordFileCompatibilityTests
{
    private static readonly DateTime When = new(2026, 10, 29, 12, 0, 0);

    [Fact]
    public void Clear_SavesEmptyFileWithVersion_ReloadHasNoRecords()
    {
        var storage = new MemoryRecordStorage();
        var store = new RecordStore(storage);
        store.Load();
        store.Book.SubmitTimeTrialLap("circuit_01", 72.0, new[] { 20.0, 45.0, 60.0 }, When);
        store.Book.SubmitRace("circuit_01", 230.0, 75.0, When);
        store.Save();
        Assert.True(store.Book.HasAnyRecord);

        store.Book.Clear();
        store.Save();

        Assert.False(store.Book.HasAnyRecord);
        Assert.Contains("\"version\": 1", storage.Text);

        var reloaded = new RecordStore(storage);
        reloaded.Load();
        Assert.Null(reloaded.Warning);
        Assert.False(reloaded.Book.HasAnyRecord);
    }

    [Fact]
    public void UnknownField_LoadsWithSameRecords()
    {
        const string json = """
        {
          "version": 1,
          "player": "ARI",
          "circuits": {
            "circuit_01": {
              "timeTrial": { "lapTime": 72.384, "splits": [20.0, 45.0, 60.0], "date": "2026-10-29", "player": "ARI" },
              "race": { "totalTime": { "time": 230.5, "date": "2026-10-29" }, "bestLap": { "time": 75.25, "date": "2026-10-29" } }
            }
          }
        }
        """;

        Assert.True(RecordBook.TryFromJson(json, out RecordBook book, out string error), error);
        Assert.Equal(72.384, book.GetTimeTrial("circuit_01").LapTime);
        Assert.Equal(230.5, book.GetRace("circuit_01").TotalTime.Time);
        Assert.Equal(75.25, book.GetRace("circuit_01").BestLap.Time);
    }

    [Fact]
    public void RecordWithoutDateOrSplits_Loads()
    {
        const string json = """
        { "version": 1, "circuits": { "circuit_01": {
            "timeTrial": { "lapTime": 72.384 },
            "race": { "bestLap": { "time": 75.25 } } } } }
        """;

        Assert.True(RecordBook.TryFromJson(json, out RecordBook book, out string error), error);
        TimeTrialRecord timeTrial = book.GetTimeTrial("circuit_01");
        Assert.Equal(72.384, timeTrial.LapTime);
        Assert.False(timeTrial.SplitsMatch(3));
        Assert.Null(book.GetRace("circuit_01").TotalTime);
        Assert.Equal("—", RecordFormat.Date(true, book.GetRace("circuit_01").BestLap.Date));
    }

    [Theory]
    [InlineData("""{ "version": 1, "circuits": { "circuit_01": { "timeTrial": { "splits": [1.0], "date": "2026-10-29" } } } }""")]
    [InlineData("""{ "version": 1, "circuits": { "circuit_01": { "race": { "totalTime": { "date": "2026-10-29" } } } } }""")]
    [InlineData("""{ "circuits": {} }""")]
    public void RecordWithoutTime_OrFileWithoutVersion_IsCorrupt(string json)
    {
        Assert.False(RecordBook.TryFromJson(json, out RecordBook book, out _));
        Assert.False(book.HasAnyRecord);
    }

    [Fact]
    public void NewerFormatVersion_IsCorrupt()
    {
        const string json = """{ "version": 2, "circuits": { "circuit_01": { "timeTrial": { "lapTime": 72.0 } } } }""";

        Assert.False(RecordBook.TryFromJson(json, out _, out string error));
        Assert.NotNull(error);
    }

    [Fact]
    public void NewRecords_StoreDateAsLocalDay()
    {
        var book = new RecordBook();
        book.SubmitTimeTrialLap("circuit_01", 72.0, Array.Empty<double>(), When);

        Assert.Equal("2026-10-29", book.GetTimeTrial("circuit_01").Date);
    }
}
