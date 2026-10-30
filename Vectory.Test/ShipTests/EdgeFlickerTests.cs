using System;
using System.Linq;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class EdgeFlickerTests
{
    private static EdgeFlicker NewFlicker(int edges = 100, int seed = 1234) =>
        new(edges, EdgeFlickerSettings.Default, new Random(seed));

    [Fact]
    public void LevelZero_NoEdges()
    {
        var flicker = NewFlicker();

        flicker.SetLevel(0);
        bool changed = flicker.Update(10f);

        Assert.Empty(flicker.AffectedEdges);
        Assert.False(changed);
        Assert.DoesNotContain(Enumerable.Range(0, 100), flicker.IsDimmed);
    }

    [Fact]
    public void Levels_AddEdges_KeepingPreviousOnes()
    {
        var flicker = NewFlicker();

        flicker.SetLevel(1);
        int[] level1 = flicker.AffectedEdges.ToArray();
        Assert.Equal(10, level1.Length);

        flicker.SetLevel(2);
        int[] level2 = flicker.AffectedEdges.ToArray();
        Assert.Equal(20, level2.Length);
        Assert.Subset(level2.ToHashSet(), level1.ToHashSet());

        flicker.SetLevel(3);
        int[] level3 = flicker.AffectedEdges.ToArray();
        Assert.Equal(35, level3.Length);
        Assert.Subset(level3.ToHashSet(), level2.ToHashSet());

        Assert.Equal(35, level3.Distinct().Count());
        Assert.All(level3, e => Assert.InRange(e, 0, 99));
    }

    [Fact]
    public void JumpingLevels_GetsTheFinalProportion()
    {
        var flicker = NewFlicker();

        flicker.SetLevel(3);

        Assert.Equal(35, flicker.AffectedEdges.Count);
    }

    [Fact]
    public void LowerLevel_DoesNotRemoveEdges()
    {
        var flicker = NewFlicker();
        flicker.SetLevel(2);

        flicker.SetLevel(1);

        Assert.Equal(2, flicker.Level);
        Assert.Equal(20, flicker.AffectedEdges.Count);
    }

    [Fact]
    public void SameSeed_SameEdges()
    {
        var a = NewFlicker(seed: 42);
        var b = NewFlicker(seed: 42);

        a.SetLevel(1);
        b.SetLevel(1);
        a.SetLevel(3);
        b.SetLevel(3);

        Assert.Equal(a.AffectedEdges, b.AffectedEdges);
    }

    [Fact]
    public void OnlyAffectedEdges_Dim_ForAShortTime()
    {
        var flicker = NewFlicker();
        flicker.SetLevel(3);
        var affected = flicker.AffectedEdges.ToHashSet();

        const float dt = 0.005f;
        var dimTime = new float[100];
        float maxDim = 0f;
        for (int step = 0; step < 2000; step++)   // 10 s
        {
            flicker.Update(dt);
            for (int e = 0; e < 100; e++)
            {
                if (!flicker.IsDimmed(e))
                {
                    dimTime[e] = 0f;
                    continue;
                }
                Assert.Contains(e, affected);
                dimTime[e] += dt;
                maxDim = MathF.Max(maxDim, dimTime[e]);
            }
        }

        // Cada parpadeo dura de 60 a 120 ms (con el margen de un paso)
        Assert.InRange(maxDim, 0.06f - dt, 0.12f + dt);
    }

    [Fact]
    public void Update_ReportsChanges()
    {
        var flicker = NewFlicker();
        flicker.SetLevel(3);

        bool changed = false;
        for (int i = 0; i < 100 && !changed; i++)
            changed = flicker.Update(0.01f);

        Assert.True(changed);
    }
}
