using System.Numerics;
using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class TrackAnalysisTests
{
    // Trazado de prueba en planta (X, Z) con rectas y arcos, con un punto cada metro
    private sealed class PathBuilder
    {
        private readonly List<Vector3> _points = new();
        private Vector2 _position;
        private float _heading;   // radianes, en el plano X-Z

        public PathBuilder(float startX = 0f, float startZ = 0f, float headingDegrees = 0f)
        {
            _position = new Vector2(startX, startZ);
            _heading = headingDegrees * MathF.PI / 180f;
        }

        public PathBuilder Straight(float length)
        {
            int steps = Math.Max(1, (int)MathF.Round(length));
            for (int i = 0; i < steps; i++)
            {
                Add();
                _position += new Vector2(MathF.Cos(_heading), MathF.Sin(_heading)) * (length / steps);
            }
            return this;
        }

        // Arco de un radio y un ángulo (grados; positivo y negativo son los dos sentidos de giro)
        public PathBuilder Arc(float radius, float degrees)
        {
            float angle = degrees * MathF.PI / 180f;
            int steps = Math.Max(2, (int)MathF.Round(radius * MathF.Abs(angle)));
            float step = angle / steps;
            for (int i = 0; i < steps; i++)
            {
                Add();
                // Cuerda del tramo con el rumbo a mitad del giro: los puntos caen sobre el arco
                float chord = 2f * radius * MathF.Sin(MathF.Abs(step) / 2f);
                _position += new Vector2(MathF.Cos(_heading + step / 2f), MathF.Sin(_heading + step / 2f)) * chord;
                _heading += step;
            }
            return this;
        }

        // Cierra el trazado: tiene que volver al punto de partida
        public List<Vector3> Closed()
        {
            Assert.True(Vector2.Distance(_position, new Vector2(_points[0].X, _points[0].Z)) < 0.5f,
                $"El trazado de prueba no cierra: termina en {_position}");
            return _points;
        }

        private void Add() => _points.Add(new Vector3(_position.X, 0f, _position.Y));
    }

    private static TrackData Analyze(List<Vector3> centerline, IReadOnlyList<float>? checkpoints = null) =>
        TrackAnalysis.Analyze(centerline, 40f, checkpoints ?? Array.Empty<float>(), 8);

    // Rectángulo con esquinas redondeadas (sides a y b), con simetría de 180° para que cierre;
    // corner() añade cada esquina (90° en total) y extra() algo a mitad de las rectas largas
    private static List<Vector3> Rounded(float a, float b, Action<PathBuilder> corner, Action<PathBuilder>? extra = null)
    {
        var path = new PathBuilder();
        for (int half = 0; half < 2; half++)
        {
            path.Straight(a / 2f);
            extra?.Invoke(path);
            path.Straight(a / 2f);
            corner(path);
            path.Straight(b);
            corner(path);
        }
        return path.Closed();
    }

    // ---- 1. Círculo ----

    [Fact]
    public void Circle_LengthOneTurnNoStraight()
    {
        var circle = new PathBuilder().Arc(100f, 360f).Closed();

        TrackData data = Analyze(circle);

        Assert.InRange(data.Length, 625f, 630f);
        Assert.Equal(1, data.Turns);
        Assert.Equal(0f, data.LongestStraight);
    }

    [Fact]
    public void Circle_DirectionFollowsPointOrder()
    {
        // Con Z hacia abajo en la vista cenital, girar hacia +Z desde +X es sentido horario
        var clockwise = new PathBuilder().Arc(100f, 360f).Closed();
        var counter = new PathBuilder().Arc(100f, -360f).Closed();

        Assert.True(Analyze(clockwise).Clockwise);
        Assert.False(Analyze(counter).Clockwise);
    }

    // ---- 2. Óvalo ----

    [Fact]
    public void Oval_TwoTurnsAndLongStraight()
    {
        var oval = new PathBuilder().Straight(210f).Arc(120f, 180f).Straight(420f).Arc(120f, 180f).Straight(210f).Closed();

        TrackData data = Analyze(oval);

        Assert.InRange(data.Length, 1590f, 1598f);
        Assert.Equal(2, data.Turns);
        Assert.InRange(data.LongestStraight, 410f, 430f);
    }

    [Fact]
    public void TestOvalFile_TwoTurnsAndLongStraight()
    {
        TrackFile oval = TrackFile.Load("test_oval");

        TrackData data = Analyze(oval.Centerline());

        Assert.True(oval.IsTest);
        Assert.Equal("test_oval", oval.Id);
        Assert.InRange(data.Length, 1585f, 1600f);
        Assert.Equal(2, data.Turns);
        Assert.InRange(data.LongestStraight, 400f, 430f);
        Assert.True(data.Clockwise);
    }

    [Fact]
    public void Circuit01File_IsClockwise()
    {
        Assert.True(Analyze(TrackFile.Load("circuit_01").Centerline()).Clockwise);
    }

    // ---- 3. Rectángulo redondeado ----

    [Fact]
    public void RoundedRectangle_FourTurns()
    {
        var rectangle = Rounded(300f, 150f, p => p.Arc(30f, 90f));

        Assert.Equal(4, Analyze(rectangle).Turns);
    }

    // ---- 4. Chicane ----

    [Fact]
    public void Chicane_CountsAsTwoTurns()
    {
        // Una chicane (izquierda y derecha seguidas) en cada recta larga: 4 esquinas + 2 × 2
        var track = Rounded(400f, 150f, p => p.Arc(30f, 90f), p => p.Arc(50f, -30f).Arc(50f, 30f));

        Assert.Equal(8, Analyze(track).Turns);
    }

    // ---- 5. Curvas suaves y curvas cercanas ----

    [Fact]
    public void GentleBend_UnderFifteenDegrees_DoesNotCount()
    {
        // Un desvío de 10° y su vuelta (40 m entre medias) en cada recta larga: solo cuentan las 4 esquinas
        var track = Rounded(400f, 150f, p => p.Arc(30f, 90f), p => p.Arc(100f, -10f).Straight(40f).Arc(100f, 10f));

        Assert.Equal(4, Analyze(track).Turns);
    }

    [Fact]
    public void SameDirectionCurves_TwentyMetersApart_AreOneTurn()
    {
        var track = Rounded(300f, 150f, p => p.Arc(50f, 45f).Straight(20f).Arc(50f, 45f));

        Assert.Equal(4, Analyze(track).Turns);
    }

    [Fact]
    public void SameDirectionCurves_FiftyMetersApart_AreTwoTurns()
    {
        var track = Rounded(300f, 150f, p => p.Arc(50f, 45f).Straight(50f).Arc(50f, 45f));

        Assert.Equal(8, Analyze(track).Turns);
    }

    // ---- 6. Recta que cruza la meta ----

    [Fact]
    public void StraightAcrossFinish_JoinsWithFirst()
    {
        // La meta está en mitad de una recta de 500 m: 250 m al principio y 250 m al final
        var track = Rounded(500f, 150f, p => p.Arc(30f, 90f));

        TrackData data = Analyze(track);

        Assert.InRange(data.LongestStraight, 480f, 505f);
    }

    // ---- 7. Perfil de elevación ----

    [Fact]
    public void FlatAxis_ProfileAtZeroAndScaleTwenty()
    {
        var oval = new PathBuilder().Straight(210f).Arc(120f, 180f).Straight(420f).Arc(120f, 180f).Straight(210f).Closed();

        TrackData data = Analyze(oval);

        Assert.Equal(TrackAnalysis.DefaultProfileSamples, data.Profile.Length);
        Assert.All(data.Profile, y => Assert.Equal(0f, y));
        Assert.Equal(20f, data.ProfileScale);
    }

    [Fact]
    public void PointAt27Meters_ScaleThirty()
    {
        var oval = new PathBuilder().Straight(210f).Arc(120f, 180f).Straight(420f).Arc(120f, 180f).Straight(210f).Closed();
        oval[500] = new Vector3(oval[500].X, 27f, oval[500].Z);

        TrackData data = Analyze(oval);

        Assert.Equal(30f, data.ProfileScale);
        Assert.Contains(data.Profile, y => y > 0f);
    }

    [Fact]
    public void ProfileScale_RoundsUpToTens()
    {
        var points = new List<Vector3> { new(0, 0, 0), new(100, 40, 0), new(100, 0, 100), new(0, -12, 100) };

        Assert.Equal(40f, TrackAnalysis.ProfileScale(points, 20f));
    }

    [Fact]
    public void Profile_RelativeToFinishHeight()
    {
        // La pista de la carrera va a -2 m: el perfil plano sigue en 0
        var points = new PathBuilder().Arc(100f, 360f).Closed().ConvertAll(p => new Vector3(p.X, -2f, p.Z));

        TrackData data = Analyze(points);

        Assert.All(data.Profile, y => Assert.Equal(0f, y));
        Assert.Equal(20f, data.ProfileScale);
    }

    // ---- 8. Sectores ----

    [Fact]
    public void Sectors_CheckpointsPlusOne_AtCheckpointDistances()
    {
        var oval = new PathBuilder().Straight(210f).Arc(120f, 180f).Straight(420f).Arc(120f, 180f).Straight(210f).Closed();
        float[] cumulative = CenterlineBuilder.CumulativeDistances(oval);
        float lap = cumulative[oval.Count];
        var indices = CenterlineBuilder.CheckpointIndices(oval, 3);
        float[] distances = indices.Select(i => cumulative[i]).ToArray();

        TrackData data = Analyze(oval, distances);

        Assert.Equal(4, data.Sectors);
        Assert.Equal(3, data.CheckpointFractions.Length);
        for (int k = 0; k < 3; k++)
            Assert.Equal(distances[k] / lap, data.CheckpointFractions[k], 4);
    }

    [Fact]
    public void NoCheckpoints_OneSector()
    {
        var circle = new PathBuilder().Arc(100f, 360f).Closed();

        TrackData data = Analyze(circle);

        Assert.Equal(1, data.Sectors);
        Assert.Empty(data.CheckpointFractions);
    }

    [Fact]
    public void WidthGridAndBanking_PassedThrough()
    {
        var circle = new PathBuilder().Arc(100f, 360f).Closed();

        TrackData data = TrackAnalysis.Analyze(circle, 24f, Array.Empty<float>(), 8);

        Assert.Equal(24f, data.TrackWidth);
        Assert.Equal(8, data.GridSlots);
        Assert.Equal(0f, data.MaxBanking);
    }
}
