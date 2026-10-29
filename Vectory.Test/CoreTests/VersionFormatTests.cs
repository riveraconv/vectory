using Vectory.Core;
using Xunit;

namespace Vectory.Test.CoreTests;

public class VersionFormatTests
{
    [Theory]
    [InlineData("0.1.0", "v0.1.0")]
    [InlineData("1.2.3", "v1.2.3")]
    [InlineData(" 0.1.0 ", "v0.1.0")]
    public void Format_AddsPrefix(string version, string expected)
    {
        Assert.Equal(expected, VersionFormat.Format(version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_MissingVersion_ShowsZero(string version)
    {
        Assert.Equal("v0.0.0", VersionFormat.Format(version));
    }
}
