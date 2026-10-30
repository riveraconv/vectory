using System.Numerics;
using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.GhostTests;

public class GhostRecorderTests
{
    private static Vector3 At(double t) => new((float)t * 10f, 0f, 0f);

    [Fact]
    public void Sample_AtSixtyFps_TwentyPerSecond_GivesTwentyOneSamplesInOneSecond()
    {
        var recorder = new GhostRecorder(20);
        recorder.Begin();

        for (int frame = 0; frame <= 60; frame++)
        {
            double t = frame / 60.0;
            recorder.Sample(t, At(t), Quaternion.Identity);
        }

        Assert.Equal(21, recorder.Samples.Count);
        for (int i = 0; i < 21; i++)
            Assert.Equal(i * 0.05, recorder.Samples[i].Time, 6);
    }

    [Fact]
    public void Finish_AddsLastSampleWithExactLapTime()
    {
        var recorder = new GhostRecorder(20);
        recorder.Begin();
        for (int frame = 0; frame <= 60; frame++)
            recorder.Sample(frame / 60.0, At(frame / 60.0), Quaternion.Identity);

        GhostRecording recording = recorder.Finish(1.0123, At(1.0123), Quaternion.Identity);

        Assert.Equal(22, recording.Samples.Count);
        Assert.Equal(1.0123, recording.Samples[^1].Time);
        Assert.Equal(1.0123, recording.LapTime);
        Assert.Equal(20, recording.SampleRate);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void Finish_OnTheTimeOfTheLastSample_ReplacesItInsteadOfDuplicating()
    {
        var recorder = new GhostRecorder(20);
        recorder.Begin();
        recorder.Sample(0.0, At(0), Quaternion.Identity);
        recorder.Sample(0.05, At(0.05), Quaternion.Identity);

        GhostRecording recording = recorder.Finish(0.05, new Vector3(9f, 9f, 9f), Quaternion.Identity);

        Assert.Equal(2, recording.Samples.Count);
        Assert.Equal(new Vector3(9f, 9f, 9f), recording.Samples[^1].Position);
    }

    [Fact]
    public void Discard_EmptiesRecorder_AndBeginStartsFromZero()
    {
        var recorder = new GhostRecorder(20);
        recorder.Begin();
        for (int frame = 0; frame <= 30; frame++)
            recorder.Sample(frame / 60.0, At(frame / 60.0), Quaternion.Identity);

        recorder.Discard();

        Assert.Empty(recorder.Samples);
        Assert.False(recorder.IsRecording);
        Assert.Null(recorder.Finish(1.0, At(1.0), Quaternion.Identity));
        Assert.False(recorder.Sample(0.0, At(0), Quaternion.Identity));

        recorder.Begin();
        Assert.True(recorder.Sample(0.0, At(0), Quaternion.Identity));
        Assert.Single(recorder.Samples);
        Assert.Equal(0.0, recorder.Samples[0].Time);
    }

    [Fact]
    public void Begin_AfterFinish_StartsANewRecordingFromZero()
    {
        var recorder = new GhostRecorder(20);
        recorder.Begin();
        recorder.Sample(0.0, At(0), Quaternion.Identity);
        recorder.Sample(0.5, At(0.5), Quaternion.Identity);
        recorder.Finish(0.6, At(0.6), Quaternion.Identity);

        recorder.Begin();

        Assert.Empty(recorder.Samples);
        Assert.True(recorder.Sample(0.0, At(0), Quaternion.Identity));
        Assert.False(recorder.Sample(0.02, At(0.02), Quaternion.Identity));
    }
}
