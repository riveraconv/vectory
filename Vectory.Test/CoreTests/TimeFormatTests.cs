using Vectory.Core;
using Xunit;

namespace Vectory.Test.CoreTests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0.0, "0:00.000")]
    [InlineData(11.902, "0:11.902")]
    [InlineData(72.384, "1:12.384")]
    [InlineData(600.5, "10:00.500")]
    public void Format_UsesMinutesSecondsAndMilliseconds(double seconds, string expected)
    {
        Assert.Equal(expected, TimeFormat.Format(seconds));
    }
}
