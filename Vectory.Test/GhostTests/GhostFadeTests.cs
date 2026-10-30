using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.GhostTests;

public class GhostFadeTests
{
    [Theory]
    [InlineData(25f, 0.35f)]
    [InlineData(20f, 0.35f)]
    [InlineData(13f, 0.175f)]
    [InlineData(6f, 0f)]
    [InlineData(2f, 0f)]
    public void Opacity_ByDistance(float distance, float expected)
    {
        Assert.Equal(expected, GhostFade.Opacity(distance, 0.35f, 20f, 6f), 5);
    }
}
