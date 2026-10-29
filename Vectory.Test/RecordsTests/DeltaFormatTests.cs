using Vectory.Records;
using Xunit;

namespace Vectory.Test.RecordsTests;

public class DeltaFormatTests
{
    [Theory]
    [InlineData(30.000, 30.145, "-0.145", true)]
    [InlineData(30.312, 30.000, "+0.312", false)]
    [InlineData(30.000, 30.000, "±0.000", true)]
    public void Delta_SignFormatAndBetter(double current, double reference, string expected, bool better)
    {
        double delta = DeltaFormat.Delta(current, reference);

        Assert.Equal(expected, DeltaFormat.Format(delta));
        Assert.Equal(better, DeltaFormat.IsBetter(delta));
    }

    [Theory]
    [InlineData(62.315, "+1:02.315")]
    [InlineData(-62.315, "-1:02.315")]
    [InlineData(60.0, "+1:00.000")]
    [InlineData(59.9994, "+59.999")]
    public void Delta_OverSixtySeconds_UsesMinutes(double delta, string expected)
    {
        Assert.Equal(expected, DeltaFormat.Format(delta));
    }

    [Fact]
    public void Delta_BelowHalfMillisecond_IsZeroAndBetter()
    {
        Assert.Equal("±0.000", DeltaFormat.Format(0.0004));
        Assert.True(DeltaFormat.IsBetter(0.0004));
    }
}
