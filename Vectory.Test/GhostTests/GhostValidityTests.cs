using System.Collections.Generic;
using System.Numerics;
using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.GhostTests;

public class GhostValidityTests
{
    private static readonly Vector3[] Centerline =
    {
        new(0f, -2f, 0f), new(100f, -2f, 0f), new(100f, -2f, 100f), new(0f, -2f, 100f)
    };

    private static readonly string Signature = TrackSignature.Compute(Centerline, 40f);

    private static GhostRecording Ghost(int samples = 2) => new()
    {
        CircuitId = "circuit_01",
        TrackSignature = Signature,
        CheckpointCount = 3,
        ShipDesignId = "ship_01",
        LapTime = 72.345,
        SampleRate = 20,
        Samples = MakeSamples(samples)
    };

    private static GhostSample[] MakeSamples(int count)
    {
        var samples = new GhostSample[count];
        for (int i = 0; i < count; i++)
            samples[i] = new GhostSample(i * 0.05, Vector3.Zero, Quaternion.Identity);
        return samples;
    }

    [Fact]
    public void Matching_IsValid()
    {
        Assert.True(GhostValidity.IsValid(Ghost(), "circuit_01", 3, Signature, 72.345));
    }

    [Fact]
    public void RecordWithinOneMillisecond_IsValid()
    {
        Assert.True(GhostValidity.IsValid(Ghost(), "circuit_01", 3, Signature, 72.3455));
        Assert.True(GhostValidity.IsValid(Ghost(), "circuit_01", 3, Signature, 72.346));
    }

    [Fact]
    public void OtherCircuitId_NotValid()
    {
        Assert.False(GhostValidity.IsValid(Ghost(), "circuit_02", 3, Signature, 72.345));
    }

    [Fact]
    public void OtherCheckpointCount_NotValid()
    {
        Assert.False(GhostValidity.IsValid(Ghost(), "circuit_01", 4, Signature, 72.345));
    }

    [Fact]
    public void OtherSignature_NotValid()
    {
        Assert.False(GhostValidity.IsValid(Ghost(), "circuit_01", 3, "0000000000000000", 72.345));
    }

    [Fact]
    public void OtherRecordTime_NotValid()
    {
        Assert.False(GhostValidity.IsValid(Ghost(), "circuit_01", 3, Signature, 72.347));
        Assert.False(GhostValidity.IsValid(Ghost(), "circuit_01", 3, Signature, 70.0));
    }

    [Fact]
    public void NoRecord_NotValid()
    {
        Assert.False(GhostValidity.IsValid(Ghost(), "circuit_01", 3, Signature, null));
    }

    [Fact]
    public void LessThanTwoSamples_NotValid()
    {
        Assert.False(GhostValidity.IsValid(Ghost(samples: 1), "circuit_01", 3, Signature, 72.345));
        Assert.False(GhostValidity.IsValid(Ghost(samples: 0), "circuit_01", 3, Signature, 72.345));
    }

    [Fact]
    public void NoGhostOrNoCircuitId_NotValid()
    {
        Assert.False(GhostValidity.IsValid(null, "circuit_01", 3, Signature, 72.345));
        Assert.False(GhostValidity.IsValid(Ghost() with { CircuitId = "" }, "", 3, Signature, 72.345));
    }

    [Fact]
    public void Signature_SameTrack_SameValue()
    {
        var copy = new List<Vector3>(Centerline);

        Assert.Equal(Signature, TrackSignature.Compute(copy, 40f));
        Assert.Equal(16, Signature.Length);
    }

    [Fact]
    public void Signature_MovedPoint_Changes()
    {
        var moved = new List<Vector3>(Centerline);
        moved[2] = new Vector3(100f, -2f, 100.5f);

        Assert.NotEqual(Signature, TrackSignature.Compute(moved, 40f));
    }

    [Fact]
    public void Signature_OtherWidth_Changes()
    {
        Assert.NotEqual(Signature, TrackSignature.Compute(Centerline, 36f));
    }

    [Fact]
    public void Signature_IgnoresDifferencesBelowRounding()
    {
        var nudged = new List<Vector3>(Centerline);
        nudged[1] = new Vector3(100.0001f, -2f, -0.0001f);

        Assert.Equal(Signature, TrackSignature.Compute(nudged, 40f));
    }
}
