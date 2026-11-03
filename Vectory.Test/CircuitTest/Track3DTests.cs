using System.Numerics;
using Vectory.Circuit;
using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.CircuitTest;

// Pista 3D: perfil de altura y peralte, marco de la pista, validación y firma (spec docs/pista-3d.md v1.1, sección 10)
public class Track3DTests
{
    private const float Deg = MathF.PI / 180f;

    // Eje de TEST OVAL (plano) con los perfiles que se pasen
    private static TrackFrame OvalWith(Vector2[] elevation, Vector2[] bank, ICollection<string> warnings = null)
    {
        List<Vector3> plan = TrackFile.Load("test_oval").Centerline();
        float lap = CenterlineBuilder.CumulativeDistances(plan)[plan.Count];
        return new TrackFrame(plan, new TrackProfile(elevation, lap, warnings), new TrackProfile(bank, lap, warnings));
    }

    private static float AngleBetween(Vector3 a, Vector3 b) =>
        MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)), -1f, 1f));

    // ---- 1. Sin claves ----

    [Fact]
    public void NoKeys_ZeroEverywhereAndNormalUp()
    {
        TrackFrame frame = OvalWith(Array.Empty<Vector2>(), Array.Empty<Vector2>());

        Assert.True(frame.IsFlat);
        for (float s = 0f; s < frame.LapLength; s += 37f)
        {
            FramePoint f = frame.At(s);
            Assert.Equal(TrackFile.TrackY, f.Position.Y, 4);
            Assert.Equal(0f, f.Bank);
            Assert.Equal(0f, f.Slope);
            Assert.True(AngleBetween(f.Normal, Vector3.UnitY) < 1e-3f);
            Assert.Equal(0f, f.Right.Y, 5);
        }
    }

    [Fact]
    public void NoKeys_ProfileIsZero()
    {
        var profile = new TrackProfile(null, 1000f);

        Assert.True(profile.IsZero);
        Assert.Equal(0f, profile.ValueAt(123f));
        Assert.Equal(0f, profile.SlopeAt(456f));
    }

    // ---- 2. Interpolación ----

    [Fact]
    public void Interpolation_MonotoneWithoutOvershootAndContinuousAtClose()
    {
        var profile = new TrackProfile(new[] { new Vector2(0f, 0f), new Vector2(100f, 10f) }, 1000f);

        float previous = profile.ValueAt(0f);
        for (float s = 0.5f; s <= 100f; s += 0.5f)
        {
            float v = profile.ValueAt(s);
            Assert.True(v >= previous - 1e-5f);
            Assert.InRange(v, -1e-5f, 10f + 1e-5f);
            previous = v;
        }
        // Bajada de vuelta a 0 por el cierre, también sin pasarse
        for (float s = 100f; s < 1000f; s += 5f)
            Assert.InRange(profile.ValueAt(s), -1e-5f, 10f + 1e-5f);

        Assert.Equal(profile.ValueAt(0f), profile.ValueAt(999.99f), 2);
        Assert.Equal(profile.ValueAt(0f), profile.ValueAt(1000f), 4);
    }

    [Fact]
    public void Interpolation_ParabolaKeysGiveConstantCurvature()
    {
        // Cresta de radio 600 con claves de una parábola: la curvatura es -1/600 entre ellas
        var keys = new List<Vector2> { new(0f, 0f) };
        for (float x = -60f; x <= 60f; x += 10f)
            keys.Add(new Vector2(500f + x, 20f - x * x / 1200f));
        var profile = new TrackProfile(keys, 2000f);

        for (float s = 460f; s <= 540f; s += 2f)
            Assert.Equal(-1f / 600f, profile.CurvatureAt(s), 5);
    }

    // ---- 3. Peralte ----

    [Fact]
    public void Bank20_NormalTilted20AndRightEdgeHigher()
    {
        TrackFrame frame = OvalWith(Array.Empty<Vector2>(), new[] { new Vector2(0f, 20f) });

        FramePoint f = frame.At(100f);

        Assert.Equal(20f * Deg, AngleBetween(f.Normal, Vector3.UnitY), 3);
        Assert.True(f.PointAt(10f, 0f).Y > f.PointAt(-10f, 0f).Y);
        Assert.Equal(10f * MathF.Sin(20f * Deg), f.PointAt(10f, 0f).Y - f.Position.Y, 3);
    }

    [Fact]
    public void NegativeBank_LeftEdgeHigher()
    {
        TrackFrame frame = OvalWith(Array.Empty<Vector2>(), new[] { new Vector2(0f, -20f) });

        FramePoint f = frame.At(100f);

        Assert.True(f.PointAt(-10f, 0f).Y > f.PointAt(10f, 0f).Y);
    }

    [Fact]
    public void Frame_IsOrthonormal()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();

        foreach (float s in new[] { 50f, 400f, 900f, 1000f, 1250f, 1600f })
        {
            FramePoint f = frame.At(s);
            Assert.Equal(1f, f.Tangent.Length(), 4);
            Assert.Equal(1f, f.Normal.Length(), 4);
            Assert.Equal(1f, f.Right.Length(), 4);
            Assert.Equal(0f, Vector3.Dot(f.Tangent, f.Normal), 4);
            Assert.Equal(0f, Vector3.Dot(f.Tangent, f.Right), 4);
            Assert.Equal(0f, Vector3.Dot(f.Normal, f.Right), 4);
        }
    }

    // ---- 4. Ida y vuelta ----

    [Theory]
    [InlineData(150f, 0f, 2f)]      // recta plana
    [InlineData(500f, -12f, 3f)]    // curva 1, peralte de 25°
    [InlineData(870f, 8f, 2f)]      // subida
    [InlineData(1060f, -15f, 1.5f)] // bajada de la cresta
    [InlineData(1600f, 14f, 4f)]    // curva 2, peralte de 12°
    public void RoundTrip_ClosestReturnsSameCoordinates(float s, float lateral, float height)
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();
        Vector3 position = frame.PointAt(s, lateral, height);

        SurfacePoint p = frame.Closest(position);

        Assert.Equal(s, p.Distance, 1);
        Assert.Equal(lateral, p.Lateral, 2);
        Assert.Equal(height, p.Height, 2);
    }

    [Theory]
    [InlineData(500f, -12f)]
    [InlineData(1060f, 15f)]
    [InlineData(1600f, -18f)]
    public void RoundTrip_SurfaceUnderReturnsSameCoordinates(float s, float lateral)
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();
        Vector3 surface = frame.PointAt(s, lateral, 0f);

        SurfacePoint p = frame.SurfaceUnder(surface.X, surface.Z, surface.Y + 2f);

        Assert.Equal(s, p.Distance, 1);
        Assert.Equal(lateral, p.Lateral, 2);
        Assert.Equal(surface.Y, p.SurfaceY, 2);
        Assert.Equal(2f, p.Height, 2);
    }

    // ---- 5. Validación ----

    [Fact]
    public void Validation_BankOver35Warns()
    {
        var warnings = new List<string>();
        TrackFrame frame = OvalWith(Array.Empty<Vector2>(), new[] { new Vector2(100f, 40f), new Vector2(300f, 0f) });

        frame.CheckLimits(35f, 15f, warnings);

        Assert.Single(warnings);
        Assert.Contains("banking", warnings[0]);
    }

    [Fact]
    public void Validation_SlopeOver15Warns()
    {
        var warnings = new List<string>();
        // Sube 20 m en 50 m: pendiente media de ~22°
        TrackFrame frame = OvalWith(new[] { new Vector2(100f, 0f), new Vector2(150f, 20f), new Vector2(400f, 20f), new Vector2(600f, 0f) },
            Array.Empty<Vector2>());

        frame.CheckLimits(35f, 15f, warnings);

        Assert.Single(warnings);
        Assert.Contains("slope", warnings[0]);
    }

    [Fact]
    public void Validation_UnorderedAndOutsideKeysAreIgnored()
    {
        var warnings = new List<string>();

        var profile = new TrackProfile(new[] { new Vector2(100f, 5f), new Vector2(50f, 9f), new Vector2(1200f, 9f), new Vector2(200f, 5f) },
            1000f, warnings);

        Assert.Equal(2, profile.KeyCount);
        Assert.Equal(2, warnings.Count);
        Assert.Equal(5f, profile.ValueAt(150f), 4);
    }

    [Fact]
    public void Validation_TestHillsWithinLimits()
    {
        var warnings = new List<string>();
        TrackFrame frame = TrackFile.Load("test_hills").Frame(warnings);

        frame.CheckLimits(35f, 15f, warnings);

        Assert.Empty(warnings);
        Assert.Equal(25f, frame.MaxAbsBank, 3);
    }

    // ---- 6. Firma ----

    [Theory]
    [InlineData("circuit_01")]
    [InlineData("test_oval")]
    public void Signature_FlatCircuitsUnchanged(string name)
    {
        TrackFile circuit = TrackFile.Load(name);
        TrackFrame frame = circuit.Frame();

        // Firma de antes de la pista 3D: solo el eje en planta y el ancho
        string before = TrackSignature.Compute(circuit.Centerline(), circuit.TrackWidth);

        Assert.Equal(before, TrackSignature.Compute(frame.Points, circuit.TrackWidth, frame.SampleBanks));
    }

    [Fact]
    public void Signature_Circuit01StillMatchesSavedGhosts()
    {
        TrackFile circuit = TrackFile.Load("circuit_01");
        TrackFrame frame = circuit.Frame();

        Assert.Equal("57dfd23466f3c04b", TrackSignature.Compute(frame.Points, circuit.TrackWidth, frame.SampleBanks));
    }

    [Fact]
    public void Signature_BankChangesIt()
    {
        TrackFrame flat = OvalWith(Array.Empty<Vector2>(), Array.Empty<Vector2>());
        TrackFrame banked = OvalWith(Array.Empty<Vector2>(), new[] { new Vector2(300f, 0f), new Vector2(400f, 10f), new Vector2(600f, 0f) });

        Assert.NotEqual(TrackSignature.Compute(flat.Points, 40f, flat.SampleBanks),
            TrackSignature.Compute(banked.Points, 40f, banked.SampleBanks));
    }

    [Fact]
    public void Signature_ElevationChangesIt()
    {
        TrackFrame flat = OvalWith(Array.Empty<Vector2>(), Array.Empty<Vector2>());
        TrackFrame hills = OvalWith(new[] { new Vector2(300f, 0f), new Vector2(400f, 5f), new Vector2(600f, 0f) }, Array.Empty<Vector2>());

        Assert.NotEqual(TrackSignature.Compute(flat.Points, 40f, flat.SampleBanks),
            TrackSignature.Compute(hills.Points, 40f, hills.SampleBanks));
    }

    // ---- TEST HILLS (spec 8) ----

    [Fact]
    public void TestHills_ProfileReachesTopAndBottom()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();

        float max = float.MinValue, min = float.MaxValue;
        for (float s = 0f; s < frame.LapLength; s += 1f)
        {
            float h = frame.Elevation.ValueAt(s);
            max = MathF.Max(max, h);
            min = MathF.Min(min, h);
        }

        Assert.Equal(20f, max, 1);
        Assert.Equal(-6f, min, 1);
        Assert.Equal(0f, frame.Elevation.ValueAt(150f));   // recta 1 plana (parrilla y salida)
        Assert.Equal(0f, frame.Elevation.ValueAt(2000f));
    }

    [Fact]
    public void TestHills_CrestRadiusAbout600()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();

        float minRadius = float.MaxValue;
        for (float s = 880f; s < 1120f; s += 0.5f)
        {
            float k = frame.Elevation.CurvatureAt(s);
            if (k < 0f)
                minRadius = MathF.Min(minRadius, -1f / k);
        }

        Assert.InRange(minRadius, 550f, 650f);
    }

    [Fact]
    public void Analysis_MaxBankingIsReported()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();

        TrackData data = TrackAnalysis.Analyze(frame.Points, 40f, Array.Empty<float>(), 8, maxBanking: frame.MaxAbsBank);

        Assert.Equal(25f, data.MaxBanking, 3);
        Assert.True(data.ProfileScale >= 20f);
    }
}
