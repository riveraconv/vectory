using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class BarrierFlashTests
{
    private const float Lap = 1000f;

    private static BarrierFlash Create() => new(Lap, BarrierFlashSettings.Default);

    [Fact]
    public void Medium_FullAtImpactPoint_ZeroAtRadius_ZeroAfterDuration()
    {
        var flash = Create();
        flash.Impact(BarrierSide.Right, 100f, ImpactLevel.Medium);

        Assert.Equal(1f, flash.IntensityAt(BarrierSide.Right, 100f), 4);
        Assert.Equal(0.5f, flash.IntensityAt(BarrierSide.Right, 103.5f), 4);
        Assert.Equal(0f, flash.IntensityAt(BarrierSide.Right, 107f), 4);
        Assert.Equal(0f, flash.IntensityAt(BarrierSide.Right, 93f), 4);

        flash.Update(0.2f);
        Assert.Equal(0.5f, flash.IntensityAt(BarrierSide.Right, 100f), 4);

        flash.Update(0.2f);
        Assert.Equal(0f, flash.IntensityAt(BarrierSide.Right, 100f), 4);
        Assert.False(flash.IsActive(BarrierSide.Right));
    }

    [Fact]
    public void Strong_UsesItsRadiusAndDuration()
    {
        var flash = Create();
        flash.Impact(BarrierSide.Left, 100f, ImpactLevel.Strong);

        Assert.Equal(0.5f, flash.IntensityAt(BarrierSide.Left, 105f), 4);
        flash.Update(0.5f);
        Assert.True(flash.IsActive(BarrierSide.Left));
        flash.Update(0.1f);
        Assert.False(flash.IsActive(BarrierSide.Left));
    }

    [Fact]
    public void NewImpactSameSide_ReplacesPrevious_OtherSideCoexists()
    {
        var flash = Create();
        flash.Impact(BarrierSide.Right, 100f, ImpactLevel.Medium);
        flash.Impact(BarrierSide.Left, 300f, ImpactLevel.Medium);
        flash.Update(0.1f);

        flash.Impact(BarrierSide.Right, 500f, ImpactLevel.Strong);

        Assert.Equal(0f, flash.IntensityAt(BarrierSide.Right, 100f), 4);
        Assert.Equal(1f, flash.IntensityAt(BarrierSide.Right, 500f), 4);
        Assert.Equal(0.75f, flash.IntensityAt(BarrierSide.Left, 300f), 4);
    }

    [Fact]
    public void Distance_WrapsAroundFinishLine()
    {
        var flash = Create();
        flash.Impact(BarrierSide.Right, 998f, ImpactLevel.Medium);

        Assert.True(flash.IntensityAt(BarrierSide.Right, 2f) > 0f);
        Assert.Equal(flash.IntensityAt(BarrierSide.Right, 994f), flash.IntensityAt(BarrierSide.Right, 2f), 3);
    }

    [Fact]
    public void Graze_StaysWhileTouching_FollowsShip_FadesIn0_2sAfter()
    {
        var flash = Create();

        flash.Update(0.016f);
        flash.Graze(BarrierSide.Right, 100f);
        Assert.Equal(0.4f, flash.IntensityAt(BarrierSide.Right, 100f), 4);

        flash.Update(1f);
        flash.Graze(BarrierSide.Right, 150f);
        Assert.Equal(0.4f, flash.IntensityAt(BarrierSide.Right, 150f), 4);
        Assert.Equal(0f, flash.IntensityAt(BarrierSide.Right, 100f), 4);

        // Ha dejado de rozar
        flash.Update(0.016f);
        flash.Update(0.1f);
        Assert.True(flash.IntensityAt(BarrierSide.Right, 150f) > 0f);
        flash.Update(0.1f);
        Assert.Equal(0f, flash.IntensityAt(BarrierSide.Right, 150f), 4);
    }

    [Fact]
    public void Graze_DoesNotHideStrongerFlash_UntilItFadesBelowGraze()
    {
        var flash = Create();
        flash.Impact(BarrierSide.Right, 100f, ImpactLevel.Medium);

        flash.Update(0.1f);
        flash.Graze(BarrierSide.Right, 101f);
        Assert.Equal(0.75f, flash.IntensityAt(BarrierSide.Right, 100f), 4);

        flash.Update(0.25f);   // medio a 0,125: por debajo del roce
        flash.Graze(BarrierSide.Right, 110f);
        Assert.Equal(0.4f, flash.IntensityAt(BarrierSide.Right, 110f), 4);
    }

    // Ahead adelanta el centro del destello respecto al de la nave, en el roce y en el golpe
    [Fact]
    public void Ahead_moves_the_flash_centre_forward()
    {
        BarrierFlashSettings settings = BarrierFlashSettings.Default;
        settings.Ahead = 6f;
        var flash = new BarrierFlash(Lap, settings);
        flash.Impact(BarrierSide.Left, 100f, ImpactLevel.Medium);
        Assert.Equal(1f, flash.IntensityAt(BarrierSide.Left, 106f), 4);

        var graze = new BarrierFlash(Lap, settings);
        graze.Graze(BarrierSide.Right, 100f);
        Assert.Equal(settings.Graze.Intensity, graze.IntensityAt(BarrierSide.Right, 106f), 4);
    }
}
