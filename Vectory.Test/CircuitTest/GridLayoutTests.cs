using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class GridLayoutTests
{
    private const int Precision = 4;
    private const float TrackWidth = 40f;

    // Valores de la spec: marca 1 a 12, filas cada 16, pares 8 más atrás, columnas al 25 % del ancho
    private static GridSettings CreateSettings() => new()
    {
        FirstDistance = 12f,
        RowSpacing = 16f,
        EvenOffset = 8f,
        LateralFraction = 0.25f
    };

    // ---------------------------------------------------------------
    // Marcas
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(1, 12f, true)]
    [InlineData(2, 20f, false)]
    [InlineData(3, 28f, true)]
    [InlineData(4, 36f, false)]
    [InlineData(5, 44f, true)]
    [InlineData(6, 52f, false)]
    [InlineData(7, 60f, true)]
    [InlineData(8, 68f, false)]
    public void Slot_MatchesSpecTable(int position, float distance, bool left)
    {
        GridSlot slot = GridLayout.Slot(position, TrackWidth, CreateSettings());

        Assert.Equal(distance, slot.Distance, Precision);
        Assert.Equal(left, slot.IsLeft);
    }

    [Fact]
    public void OddPositionsAreLeft_EvenPositionsAreRight()
    {
        var settings = CreateSettings();

        for (int position = 1; position <= 8; position++)
        {
            GridSlot slot = GridLayout.Slot(position, TrackWidth, settings);
            Assert.Equal(position % 2 == 1, slot.IsLeft);
        }
    }

    [Fact]
    public void Lateral_IsQuarterOfTrackWidthToEachSide()
    {
        var settings = CreateSettings();

        Assert.Equal(-10f, GridLayout.Slot(1, TrackWidth, settings).Lateral, Precision);
        Assert.Equal(10f, GridLayout.Slot(2, TrackWidth, settings).Lateral, Precision);
    }

    [Fact]
    public void EachEvenSlot_IsBehindTheOddSlotInFront()
    {
        var settings = CreateSettings();

        for (int odd = 1; odd < 8; odd += 2)
        {
            float oddDistance = GridLayout.Slot(odd, TrackWidth, settings).Distance;
            float evenDistance = GridLayout.Slot(odd + 1, TrackWidth, settings).Distance;
            Assert.True(evenDistance > oddDistance);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Slot_WithPositionBelowOne_Throws(int position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLayout.Slot(position, TrackWidth, CreateSettings()));
    }

    // ---------------------------------------------------------------
    // Reparto aleatorio
    // ---------------------------------------------------------------

    [Fact]
    public void RandomOrder_GivesEachPositionOnce()
    {
        int[] order = GridLayout.RandomOrder(8, new Random(1234));

        Assert.Equal(8, order.Length);
        Assert.Equal(Enumerable.Range(1, 8), order.OrderBy(p => p));
    }

    [Fact]
    public void RandomOrder_WithSameSeed_IsTheSame()
    {
        int[] a = GridLayout.RandomOrder(8, new Random(42));
        int[] b = GridLayout.RandomOrder(8, new Random(42));

        Assert.Equal(a, b);
    }

    [Fact]
    public void RandomOrder_WithManySeeds_PutsThePlayerInEveryPosition()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 200; seed++)
            seen.Add(GridLayout.RandomOrder(8, new Random(seed))[0]);

        Assert.Equal(Enumerable.Range(1, 8), seen.OrderBy(p => p));
    }

    [Fact]
    public void RandomOrder_WithOneParticipant_IsPositionOne()
    {
        Assert.Equal(new[] { 1 }, GridLayout.RandomOrder(1, new Random(7)));
    }

    [Fact]
    public void RandomOrder_WithInvalidArguments_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLayout.RandomOrder(0, new Random()));
        Assert.Throws<ArgumentNullException>(() => GridLayout.RandomOrder(8, null!));
    }
}
