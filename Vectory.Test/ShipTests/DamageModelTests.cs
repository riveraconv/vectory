using System.Collections.Generic;
using Vectory.Circuit;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class DamageModelTests
{
    private const int Precision = 4;

    private static DamageModel NewModel() => new(DamageSettings.Default);

    private static void Add(DamageModel model, ImpactLevel level, int times)
    {
        for (int i = 0; i < times; i++)
            model.AddImpact(level);
    }

    [Fact]
    public void StartsAtLevelZero_WithZeroPoints()
    {
        var model = NewModel();

        Assert.Equal(0, model.Points);
        Assert.Equal(0, model.Level);
    }

    [Fact]
    public void Graze_AddsNothing()
    {
        var model = NewModel();

        Add(model, ImpactLevel.Graze, 20);

        Assert.Equal(0, model.Points);
        Assert.Equal(0, model.Level);
    }

    [Fact]
    public void Medium_AddsOne_Strong_AddsThree()
    {
        var model = NewModel();

        model.AddImpact(ImpactLevel.Medium);
        Assert.Equal(1, model.Points);

        model.AddImpact(ImpactLevel.Strong);
        Assert.Equal(4, model.Points);
    }

    [Theory]
    [InlineData(3, 0, 1)]   // 3 medios → nivel 1
    [InlineData(3, 1, 2)]   // 1 fuerte + 3 medios → nivel 2
    [InlineData(0, 3, 3)]   // 3 fuertes → nivel 3
    [InlineData(0, 5, 3)]   // 5 fuertes → sigue en 3
    [InlineData(2, 0, 0)]
    [InlineData(5, 0, 1)]
    [InlineData(6, 0, 2)]
    [InlineData(8, 0, 2)]
    public void Level_FollowsPoints(int mediums, int strongs, int expectedLevel)
    {
        var model = NewModel();

        Add(model, ImpactLevel.Medium, mediums);
        Add(model, ImpactLevel.Strong, strongs);

        Assert.Equal(expectedLevel, model.Level);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void LevelsZeroAndOne_HaveNoEffect(int mediums)
    {
        var model = NewModel();

        Add(model, ImpactLevel.Medium, mediums);

        Assert.InRange(model.Level, 0, 1);
        Assert.Equal(1f, model.TopSpeedFactor, Precision);
        Assert.Equal(1f, model.TurnFactor, Precision);
        Assert.True(model.BoostAllowed);
    }

    [Fact]
    public void LevelTwo_ReducesTopSpeedOnly()
    {
        var model = NewModel();

        Add(model, ImpactLevel.Strong, 2);

        Assert.Equal(2, model.Level);
        Assert.Equal(0.92f, model.TopSpeedFactor, Precision);
        Assert.Equal(1f, model.TurnFactor, Precision);
        Assert.True(model.BoostAllowed);
    }

    [Fact]
    public void LevelThree_ReducesTopSpeedAndTurn_AndBlocksBoost()
    {
        var model = NewModel();

        Add(model, ImpactLevel.Strong, 3);

        Assert.Equal(3, model.Level);
        Assert.Equal(0.85f, model.TopSpeedFactor, Precision);
        Assert.Equal(0.85f, model.TurnFactor, Precision);
        Assert.False(model.BoostAllowed);
    }

    [Fact]
    public void LevelChanged_FiresOncePerLevelUp()
    {
        var model = NewModel();
        var levels = new List<int>();
        model.LevelChanged += levels.Add;

        Add(model, ImpactLevel.Medium, 12);

        Assert.Equal(new[] { 1, 2, 3 }, levels);
    }

    [Fact]
    public void ImpactJumpingTwoLevels_FiresSingleEventWithFinalLevel()
    {
        var model = NewModel();
        var levels = new List<int>();
        model.LevelChanged += levels.Add;

        Add(model, ImpactLevel.Medium, 5);    // 5 puntos: nivel 1
        model.AddImpact(ImpactLevel.Strong);  // 8 puntos: nivel 2
        Assert.Equal(new[] { 1, 2 }, levels);

        var jump = new DamageModel(DamageSettings.Default with { StrongPoints = 7 });
        var jumpLevels = new List<int>();
        jump.LevelChanged += jumpLevels.Add;

        jump.AddImpact(ImpactLevel.Strong);   // 0 → 7 puntos: de nivel 0 a 2 de golpe

        Assert.Equal(new[] { 2 }, jumpLevels);
        Assert.Equal(2, jump.Level);
    }

    [Fact]
    public void AfterStop_ImpactsDoNotAdd()
    {
        var model = NewModel();
        model.AddImpact(ImpactLevel.Medium);
        var levels = new List<int>();
        model.LevelChanged += levels.Add;

        model.Stop();
        Add(model, ImpactLevel.Strong, 5);

        Assert.True(model.Stopped);
        Assert.Equal(1, model.Points);
        Assert.Equal(0, model.Level);
        Assert.Empty(levels);
    }
}
