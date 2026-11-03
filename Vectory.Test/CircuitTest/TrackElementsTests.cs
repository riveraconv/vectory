using System.Numerics;
using Vectory.Circuit;
using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.CircuitTest;

// Elementos del circuito (spec docs/elementos-circuito.md, 11): perfiles, disposición, validación y animaciones
public class TrackElementsTests
{
    // Circunferencia cerrada que sale en -Z y gira a la derecha (hacia +X); al revés gira a la izquierda
    private static List<Vector3> RightCircle(float radius = 100f, int samples = 360)
    {
        var points = new List<Vector3>();
        for (int i = 0; i < samples; i++)
        {
            float t = i * 2f * MathF.PI / samples;
            points.Add(new Vector3(radius - radius * MathF.Cos(t), 0f, -radius * MathF.Sin(t)));
        }
        return points;
    }

    private static StartSequence DefaultStart() => new(new StartSettings
    {
        FirstLightDelay = 1.0,
        LightInterval = 1.0,
        LightCount = 5,
        GoDuration = 1.0
    });

    // ---------------------------------------------------------------
    // Perfiles
    // ---------------------------------------------------------------

    [Fact]
    public void FinishProfile_SixPointsFromBaseToBase()
    {
        Vector2[] p = TrackElementShapes.FinishProfile(10f, 2f, 12f);

        Assert.Equal(6, p.Length);
        Assert.Equal(new Vector2(-12f, 0f), p[0]);
        Assert.Equal(new Vector2(12f, 0f), p[5]);
        Assert.Equal(12f, p.Max(v => v.Y));
    }

    [Fact]
    public void CheckpointProfile_FourPointsRectangle()
    {
        Vector2[] p = TrackElementShapes.CheckpointProfile(10f, 2f, 9f);

        Assert.Equal(new[] { new Vector2(-12f, 0f), new Vector2(-12f, 9f), new Vector2(12f, 9f), new Vector2(12f, 0f) }, p);
    }

    [Fact]
    public void TunnelProfile_EightPointsSymmetricAndOpenAtTheBottom()
    {
        Vector2[] p = TrackElementShapes.TunnelProfile(10f, 2f, 11f);

        Assert.Equal(8, p.Length);
        Assert.Equal(0f, p[0].Y);
        Assert.Equal(0f, p[7].Y);
        for (int i = 0; i < p.Length; i++)
        {
            Assert.Equal(-p[i].X, p[p.Length - 1 - i].X, 4);
            Assert.Equal(p[i].Y, p[p.Length - 1 - i].Y, 4);
        }
        Assert.Equal(11f, p.Max(v => v.Y), 4);
    }

    // ---------------------------------------------------------------
    // Disposición
    // ---------------------------------------------------------------

    [Fact]
    public void TunnelRings_EveryEightMetresAndOneAtTheEnd()
    {
        float[] rings = TrackElementShapes.TunnelRings(new TrackSpan(0f, 100f), 8f);

        float[] expected = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 72, 80, 88, 96, 100 };
        Assert.Equal(expected, rings);
    }

    [Fact]
    public void TunnelRings_ExactMultipleDoesNotRepeatTheEnd()
    {
        float[] rings = TrackElementShapes.TunnelRings(new TrackSpan(10f, 50f), 8f);

        Assert.Equal(new float[] { 10, 18, 26, 34, 42, 50 }, rings);
    }

    [Fact]
    public void ChevronPositions_EveryNineMetresWithinTheSpan()
    {
        float[] positions = TrackElementShapes.ChevronPositions(new TrackSpan(100f, 130f), 9f);

        Assert.Equal(new float[] { 100, 109, 118, 127 }, positions);
    }

    [Fact]
    public void Chevrons_RightTurnGoOnTheLeftBarrier()
    {
        var frame = new TrackFrame(RightCircle());
        float turn = TrackElementShapes.SignedTurn(frame, new TrackSpan(20f, 120f));

        Assert.True(turn > 0f);
        Assert.Equal(BarrierSide.Left, TrackElementShapes.ChevronSide(turn));
    }

    [Fact]
    public void Chevrons_LeftTurnGoOnTheRightBarrier()
    {
        List<Vector3> circle = RightCircle();
        circle.Reverse();
        var frame = new TrackFrame(circle);
        float turn = TrackElementShapes.SignedTurn(frame, new TrackSpan(20f, 120f));

        Assert.True(turn < 0f);
        Assert.Equal(BarrierSide.Right, TrackElementShapes.ChevronSide(turn));
    }

    [Fact]
    public void SignedTurn_QuarterOfACircleIsAboutNinetyDegrees()
    {
        var frame = new TrackFrame(RightCircle(100f, 720));
        float quarter = MathF.PI * 100f / 2f;

        float turn = TrackElementShapes.SignedTurn(frame, new TrackSpan(0f, quarter));

        Assert.Equal(MathF.PI / 2f, turn, 2);
    }

    [Fact]
    public void CrestBeacons_OneTwoAndThreeArrowsGettingCloser()
    {
        CrestBeacon[] beacons = TrackElementShapes.CrestBeacons(500f, new[] { 50f, 150f, 100f });

        Assert.Equal(3, beacons.Length);
        Assert.Equal((350f, 1), (beacons[0].Distance, beacons[0].Arrows));
        Assert.Equal((400f, 2), (beacons[1].Distance, beacons[1].Arrows));
        Assert.Equal((450f, 3), (beacons[2].Distance, beacons[2].Arrows));
    }

    [Fact]
    public void CrestBarrier_FortyFiveMetresEachSide()
    {
        TrackSpan span = TrackElementShapes.CrestBarrier(500f, 45f);

        Assert.Equal(455f, span.Start);
        Assert.Equal(545f, span.End);
        Assert.True(TrackElementShapes.InCrestBarrier(455f, new[] { 500f }, 45f, 2000f));
        Assert.False(TrackElementShapes.InCrestBarrier(546f, new[] { 500f }, 45f, 2000f));
    }

    [Fact]
    public void CrestBarrier_NearTheFinishWrapsAroundTheLap()
    {
        float[] crests = { 10f };

        Assert.True(TrackElementShapes.InCrestBarrier(1980f, crests, 45f, 2000f));
        Assert.False(TrackElementShapes.InCrestBarrier(1960f, crests, 45f, 2000f));
    }

    // ---------------------------------------------------------------
    // Validación
    // ---------------------------------------------------------------

    [Fact]
    public void ValidateSpans_IgnoresReversedOverlappingAndCrossingTheFinish()
    {
        var warnings = new List<string>();
        var raw = new[]
        {
            new Vector2(100f, 300f),   // vale
            new Vector2(500f, 400f),   // inicio > fin
            new Vector2(250f, 350f),   // se solapa con el primero
            new Vector2(1900f, 2100f), // cruza la meta (vuelta de 2000 m)
            new Vector2(-20f, 50f),    // también
            new Vector2(600f, 800f)    // vale
        };

        List<TrackSpan> spans = TrackElementValidation.ValidateSpans(raw, 2000f, "Tunnels", 40f, warnings);

        Assert.Equal(new[] { (100f, 300f), (600f, 800f) }, spans.Select(s => (s.Start, s.End)));
        Assert.Equal(4, warnings.Count);
    }

    [Fact]
    public void ValidateSpans_ShortTunnelIsKeptWithAWarning()
    {
        var warnings = new List<string>();

        List<TrackSpan> spans = TrackElementValidation.ValidateSpans(new[] { new Vector2(100f, 130f) }, 2000f, "Tunnels", 40f, warnings);

        Assert.Single(spans);
        Assert.Single(warnings);
    }

    [Fact]
    public void ValidateCrests_IgnoresOutsideTheLap()
    {
        var warnings = new List<string>();

        List<float> crests = TrackElementValidation.ValidateCrests(new[] { 500f, -5f, 2500f }, 2000f, warnings);

        Assert.Equal(new[] { 500f }, crests);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void CrestInsideOrJustBeforeATunnel_Warns()
    {
        var tunnels = new[] { new TrackSpan(1000f, 1200f) };

        var inside = new List<string>();
        TrackElementValidation.CheckCrestsNearTunnels(new[] { 1100f }, tunnels, 50f, inside);
        var before = new List<string>();
        TrackElementValidation.CheckCrestsNearTunnels(new[] { 970f }, tunnels, 50f, before);
        var far = new List<string>();
        TrackElementValidation.CheckCrestsNearTunnels(new[] { 900f, 1300f }, tunnels, 50f, far);

        Assert.Single(inside);
        Assert.Single(before);
        Assert.Empty(far);
    }

    // ---------------------------------------------------------------
    // Animaciones
    // ---------------------------------------------------------------

    [Fact]
    public void FinishFlash_PeakAtTriggerAndNormalAfterDuration()
    {
        var flash = new GateFlash(0.6f, 1f, 3.5f, 0.7f);
        Assert.Equal(1f, flash.Brightness);

        flash.Trigger();
        Assert.Equal(3.5f, flash.Brightness);
        Assert.Equal(0.7f, flash.WhiteMix);

        flash.Update(0.6f);
        Assert.Equal(1f, flash.Brightness);
        Assert.Equal(0f, flash.WhiteMix);
    }

    [Fact]
    public void FinishFlash_QuadraticFallAndRetriggerRestarts()
    {
        var flash = new GateFlash(0.6f, 1f, 3.5f, 0.7f);
        flash.Trigger();
        flash.Update(0.3f);

        Assert.Equal(0.25f, flash.Level, 4);

        flash.Trigger();
        Assert.Equal(3.5f, flash.Brightness);
    }

    [Fact]
    public void CheckpointFlash_ReturnsToIdleBrightness()
    {
        var flash = new GateFlash(0.45f, 0.5f, 1.6f, 0.4f);
        flash.Trigger();
        Assert.Equal(1.6f, flash.Brightness);

        flash.Update(0.5f);
        Assert.Equal(0.5f, flash.Brightness);
    }

    [Fact]
    public void TunnelPulse_GlowByDistanceToTheNearestPulse()
    {
        float[] pulses = { 50f };

        Assert.Equal(1f, TunnelPulse.Glow(50f, pulses), 5);
        Assert.Equal(MathF.Exp(-4f), TunnelPulse.Glow(64f, pulses), 5);
    }

    [Fact]
    public void TunnelPulse_PulsesEvenlySpacedAndLooping()
    {
        float[] positions = TunnelPulse.Positions(1f, 200f, 120f, 2);

        Assert.Equal(new[] { 120f, 20f }, positions);
    }

    [Fact]
    public void ChevronWave_PeakTravelsForward()
    {
        // Pico en la fase 0,1: en t = 0,1/2,2 s está en el inicio; un poco después, más adelante
        Assert.Equal(1f, ChevronWave.Glow(0.1f / 2.2f, 0f, 2.2f, 45f), 4);
        float later = 0.1f / 2.2f + 0.1f;
        Assert.True(ChevronWave.Glow(later, 9.9f, 2.2f, 45f) > 0.99f);
        Assert.True(ChevronWave.Glow(later, 0f, 2.2f, 45f) < 0.1f);
    }

    [Fact]
    public void Heartbeat_StaysBetweenMinAndMax()
    {
        for (float t = 0f; t < 3f; t += 0.05f)
        {
            float b = Heartbeat.Brightness(t, 0.6f, 1f, 1.5f);
            Assert.InRange(b, 1f, 1.5f);
        }
    }

    [Fact]
    public void StartLights_ThreeLitThenCyanFlashThenOff()
    {
        StartSequence start = DefaultStart();

        start.Update(3.5);
        StartLightsState at35 = StartLightsState.From(start, 0.7f);
        Assert.Equal(3, at35.LightsOn);
        Assert.Equal(0f, at35.GoFlash);

        start.Update(2.7);   // 6,2 s
        StartLightsState at62 = StartLightsState.From(start, 0.7f);
        Assert.Equal(0, at62.LightsOn);
        Assert.True(at62.GoFlash > 0f);

        start.Update(0.8);   // 7 s
        StartLightsState at7 = StartLightsState.From(start, 0.7f);
        Assert.Equal(0, at7.LightsOn);
        Assert.Equal(0f, at7.GoFlash);
    }

    [Fact]
    public void StartLights_WithoutSequenceAreOff()
    {
        StartLightsState state = StartLightsState.From(null, 0.7f);

        Assert.Equal(0, state.LightsOn);
        Assert.Equal(0f, state.GoFlash);
    }

    [Fact]
    public void GridMarks_VisibleBeforeGoGoneHalfASecondAfterAndStayGone()
    {
        StartSequence start = DefaultStart();

        start.Update(5.9);
        Assert.Equal(1f, GridMarksFade.Opacity(GridMarksFade.TimeSinceGo(start), 0.5f));

        start.Update(0.35);   // 0,25 s después del GO
        Assert.Equal(0.5f, GridMarksFade.Opacity(GridMarksFade.TimeSinceGo(start), 0.5f), 3);

        start.Update(0.25);   // 0,5 s
        Assert.Equal(0f, GridMarksFade.Opacity(GridMarksFade.TimeSinceGo(start), 0.5f), 3);

        start.Update(120.0);  // vueltas después
        Assert.Equal(0f, GridMarksFade.Opacity(GridMarksFade.TimeSinceGo(start), 0.5f));
    }

    [Fact]
    public void TimedDisplay_FadesInAndOut()
    {
        Assert.Equal(0f, TimedDisplay.Opacity(0f, 2f, 0.1f, 0.3f));
        Assert.Equal(1f, TimedDisplay.Opacity(1f, 2f, 0.1f, 0.3f));
        Assert.Equal(0.5f, TimedDisplay.Opacity(1.85f, 2f, 0.1f, 0.3f), 4);
        Assert.Equal(0f, TimedDisplay.Opacity(2f, 2f, 0.1f, 0.3f));
    }

    // ---------------------------------------------------------------
    // Tiempo del sector (panel del checkpoint y HUD)
    // ---------------------------------------------------------------

    [Fact]
    public void SectorTime_NegativeDeltaIsBetter()
    {
        SectorTimeText text = SectorTimeText.Build(0, 23.456, 23.670);

        Assert.Equal("S1", text.Label);
        Assert.Equal("0:23.456", text.Time);
        Assert.Equal("-0.214", text.Delta);
        Assert.True(text.IsBetter);
    }

    [Fact]
    public void SectorTime_PositiveDeltaIsWorse()
    {
        SectorTimeText text = SectorTimeText.Build(2, 31.000, 30.836);

        Assert.Equal("S3", text.Label);
        Assert.Equal("+0.164", text.Delta);
        Assert.False(text.IsBetter);
    }

    [Fact]
    public void SectorTime_WithoutPreviousBestHasNoDelta()
    {
        SectorTimeText text = SectorTimeText.Build(1, 20.0, null);

        Assert.False(text.HasDelta);
        Assert.Null(text.Delta);
    }

    [Fact]
    public void RaceTracker_LastSectorPreviousBestIsTheBestBeforeThisOne()
    {
        var tracker = new RaceTracker(1, 3);
        tracker.Start();

        tracker.Tick(10);
        tracker.CheckpointCrossed(0);
        Assert.Null(tracker.LastSectorPreviousBest);

        tracker.Tick(5);
        tracker.FinishLineCrossed();
        tracker.Tick(9);
        tracker.CheckpointCrossed(0);
        Assert.Equal(10.0, tracker.LastSectorPreviousBest);

        tracker.Tick(6);
        tracker.FinishLineCrossed();
        tracker.Tick(12);
        tracker.CheckpointCrossed(0);
        Assert.Equal(9.0, tracker.LastSectorPreviousBest);
    }

    // ---------------------------------------------------------------
    // Firmas: los elementos no tocan el trazado
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("circuit_01")]
    [InlineData("test_oval")]
    public void Signature_FlatCircuitsUnchanged(string name)
    {
        TrackFile circuit = TrackFile.Load(name);
        TrackFrame frame = circuit.Frame();

        string signature = TrackSignature.Compute(frame.Points, circuit.TrackWidth, frame.SampleBanks);

        Assert.Equal(TrackSignature.Compute(circuit.Centerline(), circuit.TrackWidth), signature);
        if (name == "circuit_01")
            Assert.Equal("57dfd23466f3c04b", signature);
    }
}
