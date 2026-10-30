using System;
using System.Numerics;
using Vectory.Ghost;
using Vectory.Records;
using Xunit;

namespace Vectory.Test.GhostTests;

public class GhostStoreTests
{
    private static GhostRecording Ghost(string circuitId = "circuit_01") => new()
    {
        CircuitId = circuitId,
        TrackSignature = "0123456789abcdef",
        CheckpointCount = 3,
        ShipDesignId = "ship_01",
        LapTime = 72.123456,
        SampleRate = 20,
        Samples = new[]
        {
            new GhostSample(0.0, new Vector3(1.23456f, -2f, 3.5f), Quaternion.Identity),
            new GhostSample(0.05, new Vector3(2.5f, -2f, 4.75f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f)),
            new GhostSample(72.123456, new Vector3(1f, -2f, 3f), Quaternion.Identity)
        }
    };

    [Fact]
    public void Json_RoundTrip_KeepsSamplesShipAndTime()
    {
        GhostRecording original = Ghost();

        Assert.True(GhostJson.TryFromJson(GhostJson.ToJson(original), out GhostRecording loaded, out string error), error);

        Assert.Equal("circuit_01", loaded.CircuitId);
        Assert.Equal("0123456789abcdef", loaded.TrackSignature);
        Assert.Equal(3, loaded.CheckpointCount);
        Assert.Equal("ship_01", loaded.ShipDesignId);
        Assert.Equal(72.123456, loaded.LapTime);
        Assert.Equal(20, loaded.SampleRate);
        Assert.Equal(original.Samples.Count, loaded.Samples.Count);
        for (int i = 0; i < original.Samples.Count; i++)
        {
            GhostSample a = original.Samples[i];
            GhostSample b = loaded.Samples[i];
            Assert.Equal(a.Time, b.Time, 4);
            Assert.Equal(a.Position.X, b.Position.X, 3);
            Assert.Equal(a.Position.Y, b.Position.Y, 3);
            Assert.Equal(a.Position.Z, b.Position.Z, 3);
            Assert.True(MathF.Abs(Quaternion.Dot(a.Rotation, b.Rotation)) > 0.9999f);
        }
    }

    [Fact]
    public void Json_SamplesAreCompactArrays()
    {
        string json = GhostJson.ToJson(Ghost());

        Assert.Contains("\"samples\":[[0,1.235,-2,3.5,0,0,0,1]", json);
    }

    [Fact]
    public void Json_UnknownField_Loads()
    {
        string json = GhostJson.ToJson(Ghost()).Replace("\"version\":1", "\"version\":1,\"futureField\":{\"a\":[1,2]}");

        Assert.True(GhostJson.TryFromJson(json, out GhostRecording loaded, out _));
        Assert.Equal(3, loaded.Samples.Count);
    }

    [Theory]
    [InlineData("{ \"version\": 1, \"circ")]
    [InlineData("not json")]
    [InlineData("{\"version\":2,\"circuitId\":\"circuit_01\",\"lapTime\":70,\"samples\":[]}")]
    [InlineData("{\"circuitId\":\"circuit_01\",\"lapTime\":70,\"samples\":[]}")]
    [InlineData("{\"version\":1,\"lapTime\":70,\"samples\":[]}")]
    [InlineData("{\"version\":1,\"circuitId\":\"circuit_01\",\"samples\":[]}")]
    [InlineData("{\"version\":1,\"circuitId\":\"circuit_01\",\"lapTime\":70}")]
    [InlineData("{\"version\":1,\"circuitId\":\"circuit_01\",\"lapTime\":70,\"samples\":[[0,1,2]]}")]
    public void Json_CorruptNewerOrIncomplete_NoGhostWithoutException(string json)
    {
        Assert.False(GhostJson.TryFromJson(json, out GhostRecording ghost, out string error));
        Assert.Null(ghost);
        Assert.NotNull(error);
    }

    [Fact]
    public void Store_SaveThenLoad_KeepsGhost()
    {
        var store = new GhostStore(new MemoryGhostStorage());

        Assert.True(store.Save(Ghost()));
        GhostRecording loaded = store.Load("circuit_01");

        Assert.NotNull(loaded);
        Assert.Equal(72.123456, loaded.LapTime);
        Assert.Null(store.Load("circuit_02"));
    }

    [Fact]
    public void Store_CorruptFile_MovedToBackup_NoGhost_WithWarning()
    {
        var storage = new MemoryGhostStorage();
        var file = (MemoryRecordStorage)storage.For("circuit_01");
        file.Text = "{ broken";
        var store = new GhostStore(storage);

        GhostRecording loaded = store.Load("circuit_01");

        Assert.Null(loaded);
        Assert.NotNull(store.Warning);
        Assert.Equal("{ broken", file.Backup);
        Assert.Null(file.Text);
    }

    [Fact]
    public void Store_CircuitWithoutId_NoGhost()
    {
        var store = new GhostStore(new MemoryGhostStorage());

        Assert.False(store.Save(Ghost(circuitId: "")));
        Assert.Null(store.Load(""));
    }

    [Fact]
    public void Store_DeleteAll_LeavesNoGhosts()
    {
        var storage = new MemoryGhostStorage();
        var store = new GhostStore(storage);
        store.Save(Ghost("circuit_01"));
        store.Save(Ghost("circuit_02"));

        Assert.True(store.DeleteAll());

        Assert.Null(store.Load("circuit_01"));
        Assert.Null(store.Load("circuit_02"));
    }
}
