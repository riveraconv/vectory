using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class LineCrossingDetectorTests
{
    // Circuito de 100 muestras; con el paso máximo por defecto (25) el límite real es 25
    private const int Samples = 100;

    private static LineCrossingDetector NewDetector(int samples = Samples, int maxStep = 25)
    {
        return new LineCrossingDetector(samples, maxStep);
    }

    // ---------------------------------------------------------------
    // Constructor y argumentos
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_WithTooFewSamples_Throws(int samples)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineCrossingDetector(samples));
    }

    [Fact]
    public void Constructor_WithInvalidMaxStep_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineCrossingDetector(Samples, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Samples)]
    public void Update_WithIndexOutOfRange_Throws(int index)
    {
        var detector = NewDetector();

        Assert.Throws<ArgumentOutOfRangeException>(() => detector.Update(index));
    }

    // ---------------------------------------------------------------
    // Sin movimiento
    // ---------------------------------------------------------------

    [Fact]
    public void Crossed_BeforeAnyUpdate_IsFalse()
    {
        var detector = NewDetector();

        Assert.False(detector.Crossed(0));
        Assert.False(detector.Crossed(10));
    }

    [Fact]
    public void FirstUpdate_OnlySetsStartingPoint()
    {
        var detector = NewDetector();

        detector.Update(10);

        Assert.False(detector.Crossed(9));
        Assert.False(detector.Crossed(10));
        Assert.False(detector.Crossed(11));
    }

    [Fact]
    public void Update_WithSameIndex_CrossesNothing()
    {
        var detector = NewDetector();
        detector.Update(10);

        detector.Update(10);

        Assert.False(detector.Crossed(10));
        Assert.False(detector.Crossed(11));
    }

    // ---------------------------------------------------------------
    // Cruces hacia delante
    // ---------------------------------------------------------------

    [Fact]
    public void Crossed_WhenMovingForwardThroughTarget_IsTrue()
    {
        var detector = NewDetector();
        detector.Update(8);

        detector.Update(12);

        Assert.True(detector.Crossed(10));
    }

    [Fact]
    public void Crossed_IncludesArrivalPointButNotStartingPoint()
    {
        var detector = NewDetector();
        detector.Update(8);

        detector.Update(12);

        Assert.False(detector.Crossed(8));
        Assert.True(detector.Crossed(9));
        Assert.True(detector.Crossed(12));
    }

    [Fact]
    public void Crossed_ForPointsOutsideTheStep_IsFalse()
    {
        var detector = NewDetector();
        detector.Update(8);

        detector.Update(12);

        Assert.False(detector.Crossed(7));
        Assert.False(detector.Crossed(13));
        Assert.False(detector.Crossed(50));
    }

    [Fact]
    public void Crossed_OnlyRefersToTheLastUpdate()
    {
        var detector = NewDetector();
        detector.Update(8);
        detector.Update(12);

        detector.Update(13);

        Assert.False(detector.Crossed(10));
        Assert.True(detector.Crossed(13));
    }

    [Fact]
    public void Crossed_WhenLoopWrapsAround_DetectsPointsAcrossTheSeam()
    {
        var detector = NewDetector();
        detector.Update(98);

        detector.Update(2);   // 98 -> 99 -> 0 -> 1 -> 2

        Assert.True(detector.Crossed(99));
        Assert.True(detector.Crossed(0));
        Assert.True(detector.Crossed(1));
        Assert.True(detector.Crossed(2));
        Assert.False(detector.Crossed(98));
        Assert.False(detector.Crossed(3));
    }

    // ---------------------------------------------------------------
    // Movimientos que no cuentan
    // ---------------------------------------------------------------

    [Fact]
    public void Crossed_WhenMovingBackwards_IsFalse()
    {
        var detector = NewDetector();
        detector.Update(12);

        detector.Update(8);

        Assert.False(detector.Crossed(10));
        Assert.False(detector.Crossed(9));
        Assert.False(detector.Crossed(8));
    }

    [Fact]
    public void Crossed_WhenMovingBackwardsAcrossTheSeam_IsFalse()
    {
        var detector = NewDetector();
        detector.Update(2);

        detector.Update(98);

        Assert.False(detector.Crossed(0));
        Assert.False(detector.Crossed(99));
    }

    [Fact]
    public void Crossed_WhenJumpIsTooLarge_IsIgnored()
    {
        var detector = NewDetector();
        detector.Update(10);

        detector.Update(60);   // salto de 50 > 25: por ejemplo, una recolocación

        Assert.False(detector.Crossed(30));
        Assert.False(detector.Crossed(60));
    }

    [Fact]
    public void AfterIgnoredJump_NormalMovementWorksFromTheNewPoint()
    {
        var detector = NewDetector();
        detector.Update(10);
        detector.Update(60);

        detector.Update(62);

        Assert.True(detector.Crossed(61));
        Assert.False(detector.Crossed(11));
    }

    [Fact]
    public void Crossed_WhenStepEqualsMaxStep_StillCounts()
    {
        var detector = NewDetector(Samples, 25);
        detector.Update(0);

        detector.Update(25);

        Assert.True(detector.Crossed(25));
    }

    [Fact]
    public void Crossed_WhenStepExceedsCustomMaxStep_IsIgnored()
    {
        var detector = NewDetector(Samples, 5);
        detector.Update(0);

        detector.Update(6);

        Assert.False(detector.Crossed(3));
    }

    [Fact]
    public void MaxStep_IsLimitedToHalfTheLoop()
    {
        // Con 10 muestras, más de 4 pasos no se distingue de ir hacia atrás
        var detector = new LineCrossingDetector(10);
        detector.Update(0);

        detector.Update(5);

        Assert.False(detector.Crossed(3));

        detector.Reset();
        detector.Update(0);
        detector.Update(4);

        Assert.True(detector.Crossed(2));
    }

    // ---------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------

    [Fact]
    public void Reset_ForgetsPreviousPosition()
    {
        var detector = NewDetector();
        detector.Update(10);
        detector.Update(12);

        detector.Reset();
        detector.Update(20);   // sin referencia previa: no hay cruce desde 12

        Assert.False(detector.Crossed(15));
        Assert.False(detector.Crossed(20));
    }

    [Fact]
    public void Reset_ClearsLastCrossing()
    {
        var detector = NewDetector();
        detector.Update(8);
        detector.Update(12);

        detector.Reset();

        Assert.False(detector.Crossed(10));
    }

    [Fact]
    public void AfterReset_DetectsCrossingsFromNewStartingPoint()
    {
        var detector = NewDetector();
        detector.Update(10);
        detector.Reset();
        detector.Update(40);

        detector.Update(42);

        Assert.True(detector.Crossed(41));
    }
}
