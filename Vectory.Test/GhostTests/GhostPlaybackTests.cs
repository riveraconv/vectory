using System;
using System.Numerics;
using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.GhostTests;

public class GhostPlaybackTests
{
    private static readonly Quaternion Yaw0 = Quaternion.Identity;
    private static readonly Quaternion Yaw90 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

    private static GhostRecording Recording() => new()
    {
        LapTime = 1.0,
        SampleRate = 20,
        Samples = new[]
        {
            new GhostSample(0.0, new Vector3(0f, 0f, 0f), Yaw0),
            new GhostSample(0.5, new Vector3(10f, 0f, 0f), Yaw90),
            new GhostSample(1.0, new Vector3(10f, 0f, 20f), Yaw90)
        }
    };

    private static void AssertQuat(Quaternion expected, Quaternion actual)
    {
        // q y -q son la misma rotación
        float dot = MathF.Abs(Quaternion.Dot(expected, actual));
        Assert.True(dot > 0.99999f, $"expected {expected}, got {actual}");
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 1)]
    [InlineData(1.0, 2)]
    public void ExactSampleTime_ReturnsThatSample(double time, int index)
    {
        GhostRecording recording = Recording();

        Assert.True(GhostPlayback.TryGetPose(recording, time, out Vector3 position, out Quaternion rotation));

        Assert.Equal(recording.Samples[index].Position, position);
        AssertQuat(recording.Samples[index].Rotation, rotation);
    }

    [Fact]
    public void HalfwayBetweenSamples_MidpointAndHalfSlerp()
    {
        Assert.True(GhostPlayback.TryGetPose(Recording(), 0.25, out Vector3 position, out Quaternion rotation));

        Assert.Equal(5f, position.X, 4);
        Assert.Equal(0f, position.Z, 4);
        AssertQuat(Quaternion.Slerp(Yaw0, Yaw90, 0.5f), rotation);
        AssertQuat(Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f), rotation);
    }

    [Fact]
    public void PastTheEnd_IsFinished()
    {
        Assert.False(GhostPlayback.TryGetPose(Recording(), 1.001, out _, out _));
    }

    [Fact]
    public void BeforeZero_ReturnsFirstSample()
    {
        Assert.True(GhostPlayback.TryGetPose(Recording(), -0.3, out Vector3 position, out _));
        Assert.Equal(Vector3.Zero, position);
    }

    [Fact]
    public void NoRecordingOrNoSamples_IsFinished()
    {
        Assert.False(GhostPlayback.TryGetPose(null, 0.0, out _, out _));
        Assert.False(GhostPlayback.TryGetPose(new GhostRecording(), 0.0, out _, out _));
    }
}
