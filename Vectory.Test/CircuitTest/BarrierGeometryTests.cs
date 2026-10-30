using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class BarrierGeometryTests
{
    [Fact]
    public void PostDistances_With1000mAnd4m_Gives250EvenPosts()
    {
        float[] posts = BarrierGeometry.PostDistances(1000f, 4f);

        Assert.Equal(250, posts.Length);
        Assert.Equal(0f, posts[0]);
        Assert.Equal(4f, posts[1], 4);
        Assert.Equal(996f, posts[^1], 3);
    }

    [Fact]
    public void PostDistances_With1001m_AdjustsSpacingToWholeNumberWithoutOddLastGap()
    {
        float[] posts = BarrierGeometry.PostDistances(1001f, 4f);
        float spacing = BarrierGeometry.AdjustedSpacing(1001f, 4f);

        Assert.Equal(250, posts.Length);
        Assert.Equal(1001f / 250f, spacing, 5);

        // Todos los tramos iguales, también el del cierre de la vuelta
        for (int i = 1; i < posts.Length; i++)
            Assert.Equal(spacing, posts[i] - posts[i - 1], 3);
        Assert.Equal(spacing, 1001f - posts[^1], 3);
    }

    [Fact]
    public void PostCount_ShortLap_AlwaysAtLeastOne()
    {
        Assert.Equal(1, BarrierGeometry.PostCount(1f, 4f));
        Assert.Equal(0, BarrierGeometry.PostCount(0f, 4f));
    }

    [Fact]
    public void LineHeights_Defaults_Are0_0_4_1_4_2_4_3()
    {
        BarrierLineHeights h = BarrierGeometry.LineHeights(3f, 0.4f, 0.6f, 1.4f);

        Assert.Equal(0f, h.Base);
        Assert.Equal(0.4f, h.BottomBandTop, 4);
        Assert.Equal(1.4f, h.Middle, 4);
        Assert.Equal(2.4f, h.TopBandBottom, 4);
        Assert.Equal(3f, h.Top, 4);
    }

    [Fact]
    public void Offset_IsHalfWidthPlusGap()
    {
        Assert.Equal(20.5f, BarrierGeometry.Offset(40f, 0.5f));
    }
}
