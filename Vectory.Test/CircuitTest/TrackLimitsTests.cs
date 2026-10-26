
using System.Numerics;
using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class TrackLimitsTests
{
    private static readonly Vector2 Center = Vector2.Zero;
    private static readonly Vector2 Right = new(1f, 0f);

    [Fact]
    public void Apply_WhenInsideLimits_DoesNotChangeState()
    {
        var position = new Vector2(5f, 3f);
        var velocity = new Vector2(4f, 2f);

        var result = TrackLimits.Apply(
            position, velocity, Center, Right, 10f, 2f);

        Assert.False(result.Hit);
        Assert.Equal(position, result.Position);
        Assert.Equal(velocity, result.Velocity);
        Assert.Equal(0f, result.ImpactSpeed);
    }

    [Fact]
    public void Apply_WhenExactlyAtLimit_DoesNotCountAsHit()
    {
        var position = new Vector2(8f, 3f);

        var result = TrackLimits.Apply(
            position, Vector2.Zero, Center, Right, 10f, 2f);

        Assert.False(result.Hit);
        Assert.Equal(position, result.Position);
    }

    [Fact]
    public void Apply_WhenOutsideRightLimit_CorrectsPosition()
    {
        var result = TrackLimits.Apply(
            new Vector2(10f, 3f),
            Vector2.Zero,
            Center, Right, 10f, 2f);

        Assert.True(result.Hit);
        Assert.Equal(new Vector2(8f, 3f), result.Position);
        Assert.Equal(0f, result.ImpactSpeed);
    }

    [Fact]
    public void Apply_WhenOutsideLeftLimit_CorrectsPosition()
    {
        var result = TrackLimits.Apply(
            new Vector2(-10f, 3f),
            Vector2.Zero,
            Center, Right, 10f, 2f);

        Assert.True(result.Hit);
        Assert.Equal(new Vector2(-8f, 3f), result.Position);
    }

    [Fact]
    public void Apply_WhenMovingOutward_RemovesOutwardVelocity()
    {
        var result = TrackLimits.Apply(
            new Vector2(10f, 3f),
            new Vector2(5f, 4f),
            Center, Right, 10f, 2f);

        Assert.True(result.Hit);
        Assert.Equal(new Vector2(0f, 4f), result.Velocity);
        Assert.Equal(5f, result.ImpactSpeed);
    }

    [Fact]
    public void Apply_WhenMovingInward_PreservesVelocity()
    {
        var velocity = new Vector2(-5f, 4f);

        var result = TrackLimits.Apply(
            new Vector2(10f, 3f),
            velocity,
            Center, Right, 10f, 2f);

        Assert.True(result.Hit);
        Assert.Equal(velocity, result.Velocity);
        Assert.Equal(0f, result.ImpactSpeed);
    }

    [Fact]
    public void Apply_WhenMovingParallelToLimit_PreservesVelocity()
    {
        var velocity = new Vector2(0f, 4f);

        var result = TrackLimits.Apply(
            new Vector2(10f, 3f),
            velocity,
            Center, Right, 10f, 2f);

        Assert.True(result.Hit);
        Assert.Equal(velocity, result.Velocity);
        Assert.Equal(0f, result.ImpactSpeed);
    }

    [Fact]
    public void Apply_WhenMarginExceedsHalfWidth_UsesCenterlineAsLimit()
    {
        var result = TrackLimits.Apply(
            new Vector2(2f, 3f),
            Vector2.Zero,
            Center, Right, 5f, 6f);

        Assert.True(result.Hit);
        Assert.Equal(new Vector2(0f, 3f), result.Position);
    }

    [Fact]
    public void Apply_WhenOutsideLimitButNotMovingOutward_StillCorrectsPosition()
    {
        var position = new Vector2(10f, 3f);
        var velocity = new Vector2(-2f, 4f);

        var result = TrackLimits.Apply(
            position, velocity, Center, Right, 10f, 2f);

        Assert.True(result.Hit);
        Assert.Equal(new Vector2(8f, 3f), result.Position);
        Assert.Equal(velocity, result.Velocity);
        Assert.Equal(0f, result.ImpactSpeed);
    }
}
