using System;
using Vectory.Records;
using Xunit;

namespace Vectory.Test.RecordsTests;

public class RecordStoreTests
{
    private static readonly DateTime When = new(2026, 10, 29, 12, 0, 0);

    [Fact]
    public void NoFile_StartsEmpty_WithoutWarning()
    {
        var store = new RecordStore(new MemoryRecordStorage());

        store.Load();

        Assert.Null(store.Warning);
        Assert.Null(store.Book.GetTimeTrial("circuit_01"));
    }

    [Fact]
    public void Save_ThenLoad_KeepsRecords()
    {
        var storage = new MemoryRecordStorage();
        var store = new RecordStore(storage);
        store.Load();
        store.Book.SubmitTimeTrialLap("circuit_01", 72.0, new[] { 20.0, 45.0, 60.0 }, When);

        Assert.True(store.Save());

        var reloaded = new RecordStore(storage);
        reloaded.Load();
        Assert.Equal(72.0, reloaded.Book.GetTimeTrial("circuit_01").LapTime);
    }

    [Fact]
    public void CorruptFile_MovedToBackup_StartsEmpty_WithWarning()
    {
        var storage = new MemoryRecordStorage { Text = "{ \"version\": 1, \"circ" };
        var store = new RecordStore(storage);

        store.Load();

        Assert.NotNull(store.Warning);
        Assert.Equal("{ \"version\": 1, \"circ", storage.Backup);
        Assert.Null(storage.Text);
        Assert.Null(store.Book.GetTimeTrial("circuit_01"));
    }

    [Fact]
    public void FailingStorage_NeverThrows()
    {
        var store = new RecordStore(new BrokenStorage());

        store.Load();
        Assert.NotNull(store.Warning);

        Assert.False(store.Save());
        Assert.NotNull(store.Warning);
    }

    private sealed class BrokenStorage : IRecordStorage
    {
        public string Read() => throw new System.IO.IOException("disk error");
        public void Write(string text) => throw new System.IO.IOException("disk full");
        public void MoveToBackup() => throw new System.IO.IOException("disk error");
    }
}
