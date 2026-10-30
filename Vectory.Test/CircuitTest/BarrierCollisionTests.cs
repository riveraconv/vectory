using System;
using Vectory.Circuit;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class BarrierCollisionTests
{
    private const float Kmh = 1f / 3.6f;
    private const float HalfWidth = 3.4f;
    private const float Offset = 20.5f;
    private const float Limit = Offset - HalfWidth;   // 17,1 m

    private static readonly BarrierCollisionSettings Settings = BarrierCollisionSettings.Default;

    // Nave en "lateral", con velocidad "speed" (m/s) a "angleDeg" grados contra la barrera derecha
    private static BarrierContactInput Input(float lateral, float speed, float angleDeg)
    {
        float a = angleDeg * MathF.PI / 180f;
        return new BarrierContactInput
        {
            Lateral = lateral,
            HalfWidth = HalfWidth,
            BarrierOffset = Offset,
            VelocityAlong = speed * MathF.Cos(a),
            VelocityLateral = speed * MathF.Sin(a),
            HeadingAlong = MathF.Cos(a),
            HeadingLateral = MathF.Sin(a)
        };
    }

    // Entrada con una velocidad perpendicular concreta (km/h) contra la barrera derecha
    private static BarrierContactInput PerpendicularInput(float lateral, float perpendicularKmh)
    {
        return new BarrierContactInput
        {
            Lateral = lateral,
            HalfWidth = HalfWidth,
            BarrierOffset = Offset,
            VelocityAlong = 50f,
            VelocityLateral = perpendicularKmh * Kmh,
            HeadingAlong = 1f,
            HeadingLateral = 0f
        };
    }

    [Fact]
    public void Resolve_OneMeterFromLimit_NoContact()
    {
        var collision = new BarrierCollision();

        var result = collision.Resolve(Input(Limit - 1f, 50f, 10f), Settings, 0.016f);

        Assert.False(result.Contact);
        Assert.Equal(Limit - 1f, result.Lateral);
    }

    [Fact]
    public void Resolve_PastLimit_ContactAndCorrectedExactlyToLimit()
    {
        var collision = new BarrierCollision();

        var result = collision.Resolve(Input(Limit + 0.3f, 50f, 2f), Settings, 0.016f);

        Assert.True(result.Contact);
        Assert.Equal(BarrierSide.Right, result.Side);
        Assert.Equal(Limit, result.Lateral, 4);
    }

    [Fact]
    public void Resolve_LeftSide_CorrectedToNegativeLimit()
    {
        var collision = new BarrierCollision();

        var result = collision.Resolve(Input(-(Limit + 0.5f), 50f, -2f), Settings, 0.016f);

        Assert.True(result.Contact);
        Assert.Equal(BarrierSide.Left, result.Side);
        Assert.Equal(-Limit, result.Lateral, 4);
    }

    [Fact]
    public void Resolve_StepFromInsideToBeyondBarrier_StillContact()
    {
        var collision = new BarrierCollision();
        collision.Resolve(Input(Limit - 0.5f, 110f, 30f), Settings, 0.016f);

        // En un solo paso ha pasado más allá de la propia barrera
        var result = collision.Resolve(Input(Offset + 1f, 110f, 30f), Settings, 0.016f);

        Assert.True(result.Contact);
        Assert.Equal(Limit, result.Lateral, 4);
    }

    [Theory]
    [InlineData(20f, ImpactLevel.Graze)]
    [InlineData(60f, ImpactLevel.Medium)]
    [InlineData(120f, ImpactLevel.Strong)]
    [InlineData(30f, ImpactLevel.Medium)]
    [InlineData(90f, ImpactLevel.Strong)]
    public void Classify_ByPerpendicularSpeed(float kmh, ImpactLevel expected)
    {
        Assert.Equal(expected, ImpactClassifier.Classify(kmh, 30f, 90f));
    }

    [Theory]
    [InlineData(20f, ImpactLevel.Graze)]
    [InlineData(60f, ImpactLevel.Medium)]
    [InlineData(120f, ImpactLevel.Strong)]
    public void Resolve_UsesPerpendicularSpeedForLevel(float kmh, ImpactLevel expected)
    {
        var collision = new BarrierCollision();

        var result = collision.Resolve(PerpendicularInput(Limit + 0.1f, kmh), Settings, 0.016f);

        Assert.Equal(expected, result.Level);
    }

    [Fact]
    public void Resolve_Graze_RemovesPerpendicularSpeed_Loses4Percent_Straightens30Percent()
    {
        var collision = new BarrierCollision();
        // 80 m/s a 3°: unos 15 km/h contra la barrera, roce
        var input = Input(Limit + 0.1f, 80f, 3f);

        var result = collision.Resolve(input, Settings, 0.016f);

        Assert.Equal(ImpactLevel.Graze, result.Level);
        Assert.True(result.IsGrazeStart);
        Assert.False(result.IsNewImpact);
        Assert.Equal(0f, result.VelocityLateral);
        Assert.Equal(input.VelocityAlong * 0.96f, result.VelocityAlong, 3);
        Assert.Equal(0.96f, result.SpeedScale, 4);

        float angle = MathF.Asin(result.HeadingLateral) * 180f / MathF.PI;
        Assert.Equal(3f * 0.7f, angle, 3);
        Assert.True(result.HeadingAlong > 0f);
    }

    [Fact]
    public void Resolve_ContinuedGraze_OnlyFrictionNoMoreEntryLoss()
    {
        var collision = new BarrierCollision();
        collision.Resolve(Input(Limit + 0.1f, 80f, 3f), Settings, 0.016f);

        var input = Input(Limit + 0.05f, 80f, 2f);
        var result = collision.Resolve(input, Settings, 0.5f);

        Assert.False(result.IsGrazeStart);
        Assert.Equal(1f, result.SpeedScale);
        Assert.Equal(20f * Kmh * 0.5f, result.SpeedLoss, 4);
        Assert.Equal(input.VelocityAlong - 20f * Kmh * 0.5f, result.VelocityAlong, 3);
        // Sin más enderezado mientras sigue el roce
        Assert.Equal(input.HeadingLateral, result.HeadingLateral, 5);
    }

    [Fact]
    public void Resolve_Medium_BouncesAt40Percent_Loses20Percent_TurnsHalfAngleInward()
    {
        var collision = new BarrierCollision();
        // 80 m/s a 12°: unos 60 km/h contra la barrera, medio
        var input = Input(Limit + 0.2f, 80f, 12f);

        var result = collision.Resolve(input, Settings, 0.016f);

        Assert.Equal(ImpactLevel.Medium, result.Level);
        Assert.True(result.IsNewImpact);
        Assert.Equal(-input.VelocityLateral * 0.4f, result.VelocityLateral, 3);
        Assert.Equal(input.VelocityAlong * 0.8f, result.VelocityAlong, 3);
        Assert.Equal(0.8f, result.SpeedScale, 4);

        float angle = MathF.Asin(result.HeadingLateral) * 180f / MathF.PI;
        Assert.Equal(-6f, angle, 3);
    }

    [Fact]
    public void Resolve_Strong_BouncesAt50Percent_Loses45Percent()
    {
        var collision = new BarrierCollision();
        // 80 m/s a 25°: unos 120 km/h contra la barrera, fuerte
        var input = Input(Limit + 0.2f, 80f, 25f);

        var result = collision.Resolve(input, Settings, 0.016f);

        Assert.Equal(ImpactLevel.Strong, result.Level);
        Assert.True(result.IsNewImpact);
        Assert.Equal(-input.VelocityLateral * 0.5f, result.VelocityLateral, 3);
        Assert.Equal(input.VelocityAlong * 0.55f, result.VelocityAlong, 3);
        Assert.Equal(0.55f, result.SpeedScale, 4);
    }

    [Fact]
    public void Resolve_TwoMediumImpactsSameSide_0_1sApart_CountAsOne()
    {
        var collision = new BarrierCollision();
        var hit = PerpendicularInput(Limit + 0.1f, 60f);
        var away = PerpendicularInput(Limit - 1f, -20f);

        var first = collision.Resolve(hit, Settings, 0.016f);
        collision.Resolve(away, Settings, 0.05f);
        var second = collision.Resolve(hit, Settings, 0.05f);

        Assert.True(first.IsNewImpact);
        Assert.True(second.Contact);
        Assert.False(second.IsNewImpact);
        Assert.Equal(ImpactLevel.Graze, second.Level);
    }

    [Fact]
    public void Resolve_TwoMediumImpactsSameSide_0_5sApart_CountAsTwo()
    {
        var collision = new BarrierCollision();
        var hit = PerpendicularInput(Limit + 0.1f, 60f);
        var away = PerpendicularInput(Limit - 1f, -20f);

        var first = collision.Resolve(hit, Settings, 0.016f);
        collision.Resolve(away, Settings, 0.25f);
        var second = collision.Resolve(hit, Settings, 0.25f);

        Assert.True(first.IsNewImpact);
        Assert.True(second.IsNewImpact);
        Assert.Equal(ImpactLevel.Medium, second.Level);
    }

    [Fact]
    public void Resolve_CooldownIsPerSide()
    {
        var collision = new BarrierCollision();
        var right = PerpendicularInput(Limit + 0.1f, 60f);
        var left = PerpendicularInput(-(Limit + 0.1f), -60f);

        collision.Resolve(right, Settings, 0.016f);
        var other = collision.Resolve(left, Settings, 0.05f);

        Assert.True(other.IsNewImpact);
        Assert.Equal(BarrierSide.Left, other.Side);
        Assert.Equal(60f * Kmh * 0.4f, other.VelocityLateral, 3);
    }
}
