using System.Collections.Generic;
using System.Linq;
using Vectory.Circuit;
using Vectory.Menu;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class ShipTechnicalProfileTests
{
    private static readonly ShipTuning Vector = VectorTuning.Values;

    [Fact]
    public void Vector_WithMultipliersOne_IsItsTuningOfAlways()
    {
        Assert.Equal(Vector, ShipMultipliers.One.Apply(Vector));
        Assert.Equal(Vector, VectorShip.Definition.Technical);
        Assert.Same(ShipMultipliers.One, VectorShip.Definition.Multipliers);
    }

    [Fact]
    public void Blade_MultipliersOverVector_RestUnchanged()
    {
        ShipTuning t = BladeShip.Definition.Technical;

        Assert.Equal(Vector.MaxSpeed * 1.08f, t.MaxSpeed);
        Assert.Equal(Vector.Acceleration * 0.94f, t.Acceleration);
        Assert.Equal(Vector.TurnRate, t.TurnRate);
        Assert.Equal(Vector.LateralGrip * 0.92f, t.LateralGrip);
        Assert.Equal(Vector.BrakeDeceleration * 1.02f, t.BrakeDeceleration);
        AssertRestLikeVector(t);
    }

    [Fact]
    public void Hammer_MultipliersOverVector_RestUnchanged()
    {
        ShipTuning t = HammerShip.Definition.Technical;

        Assert.Equal(Vector.MaxSpeed * 0.94f, t.MaxSpeed);
        Assert.Equal(Vector.Acceleration * 1.10f, t.Acceleration);
        Assert.Equal(Vector.TurnRate * 0.96f, t.TurnRate);
        Assert.Equal(Vector.LateralGrip * 1.04f, t.LateralGrip);
        Assert.Equal(Vector.BrakeDeceleration * 0.96f, t.BrakeDeceleration);
        AssertRestLikeVector(t);
    }

    [Fact]
    public void Definitions_StayWithinTheBalanceLimit()
    {
        foreach (ShipDefinition ship in ShipDefinitions.All)
        {
            foreach (ShipParameter parameter in ShipMultipliers.Parameters)
                Assert.True(ShipMultipliers.IsWithinLimit(ship.Multipliers[parameter]), $"{ship.Id} {parameter}");
        }
    }

    [Theory]
    [InlineData(1.10f, true)]
    [InlineData(0.90f, true)]
    [InlineData(1.11f, false)]
    [InlineData(0.89f, false)]
    public void IsWithinLimit_BoundsIncluded(float multiplier, bool within)
    {
        Assert.Equal(within, ShipMultipliers.IsWithinLimit(multiplier));
    }

    private static void AssertRestLikeVector(ShipTuning t)
    {
        Assert.Equal(Vector.Drag, t.Drag);
        Assert.Equal(Vector.OverspeedDecay, t.OverspeedDecay);
        Assert.Equal(Vector.SteerResponse, t.SteerResponse);
    }
}

public class ShipTechnicalStatsTests
{
    private static ShipStat Stat(ShipDefinition ship, string label) =>
        ShipStats.Compute(ship.Technical, VectorTuning.Values).Single(s => s.Label == label);

    [Theory]
    [InlineData(ShipStats.TopSpeed, "428 KM/H", 9)]
    [InlineData(ShipStats.Acceleration, "0–200 · 2.4 S", 2)]
    [InlineData(ShipStats.Handling, "74 °/S", 5)]
    [InlineData(ShipStats.Grip, "", 1)]
    [InlineData(ShipStats.Braking, "200–0 · 1.2 S", 6)]
    public void Blade(string label, string value, int segments)
    {
        ShipStat stat = Stat(BladeShip.Definition, label);

        Assert.Equal(value, stat.Value);
        Assert.Equal(segments, stat.Segments);
    }

    [Theory]
    [InlineData(ShipStats.TopSpeed, "372 KM/H", 2)]
    [InlineData(ShipStats.Acceleration, "0–200 · 2.0 S", 10)]
    [InlineData(ShipStats.Handling, "72 °/S", 3)]
    [InlineData(ShipStats.Grip, "", 7)]
    [InlineData(ShipStats.Braking, "200–0 · 1.3 S", 3)]
    public void Hammer(string label, string value, int segments)
    {
        ShipStat stat = Stat(HammerShip.Definition, label);

        Assert.Equal(value, stat.Value);
        Assert.Equal(segments, stat.Segments);
    }

    // El panel de ajuste recorre a la vez los parámetros y los datos de ShipStats
    [Fact]
    public void Parameters_InTheOrderOfTheStats()
    {
        ShipTuning ship = VectorTuning.Values;
        foreach ((ShipParameter parameter, int index) in ShipMultipliers.Parameters.Select((p, i) => (p, i)))
        {
            ShipTuning changed = ShipMultipliers.One.With(parameter, 1.10f).Apply(ship);
            IReadOnlyList<ShipStat> stats = ShipStats.Compute(changed, ship);

            Assert.Equal(10, stats[index].Segments);
            Assert.Equal(1, stats.Count(s => s.Segments != ShipStats.ReferenceSegments));
        }
    }
}

public class ShipCollisionWidthTests
{
    [Fact]
    public void CollisionHalfWidth_OfEachShip()
    {
        Assert.Equal(3.4f, VectorShip.Definition.Visual.CollisionHalfWidth);
        Assert.Equal(1.6f, BladeShip.Definition.Visual.CollisionHalfWidth);
        Assert.Equal(2.5f, HammerShip.Definition.Visual.CollisionHalfWidth);
    }

    [Fact]
    public void WiderShip_TouchesTheBarrierEarlier()
    {
        const float Lateral = 17.5f;

        Assert.True(Touches(VectorShip.Definition, Lateral));
        Assert.False(Touches(HammerShip.Definition, Lateral));
        Assert.False(Touches(BladeShip.Definition, Lateral));

        Assert.True(Touches(HammerShip.Definition, 18.2f));
        Assert.False(Touches(BladeShip.Definition, 18.2f));
    }

    private static bool Touches(ShipDefinition ship, float lateral)
    {
        BarrierContactResult result = new BarrierCollision().Resolve(new BarrierContactInput
        {
            Lateral = lateral,
            HalfWidth = ship.Visual.CollisionHalfWidth,
            BarrierOffset = 20.5f,
            VelocityAlong = 50f,
            HeadingAlong = 1f
        }, BarrierCollisionSettings.Default, 1f / 60f);
        return result.Contact;
    }
}

public class ShipDefinitionResolutionTests
{
    [Fact]
    public void Resolve_EachShip_ItsOwnDefinition()
    {
        Assert.Same(BladeShip.Definition, ShipDefinitions.Resolve("ship_02"));
        Assert.Same(HammerShip.Definition, ShipDefinitions.Resolve("ship_03"));
        Assert.Same(VectorShip.Definition, ShipDefinitions.Resolve("nope"));
    }

    [Fact]
    public void AllShips_HaveDefinition_AndAreSelectable()
    {
        foreach (string id in new[] { "ship_01", "ship_02", "ship_03" })
        {
            Assert.True(ShipDefinitions.Has(id));
            Assert.True(ShipCatalog.Find(id).Selectable);
        }
    }
}

public class TuningLabTests
{
    private static TuningLab Open(string currentId = ShipCatalog.Ship01) => new(ShipDefinitions.All, currentId);

    [Fact]
    public void MoveTo_BecomesCurrent_AndCycleGoesOnFromIt()
    {
        TuningLab lab = Open(ShipCatalog.Ship01);

        lab.MoveTo(ShipCatalog.Ship03);
        Assert.Equal(ShipCatalog.Ship03, lab.Current.Id);

        lab.Cycle();
        Assert.Equal(ShipCatalog.Ship01, lab.Current.Id);
    }

    [Fact]
    public void MoveTo_DoesNotTouchTheLab()
    {
        TuningLab lab = Open(ShipCatalog.Ship01);

        lab.MoveTo(ShipCatalog.Ship02);

        Assert.False(lab.Touched);
    }

    [Fact]
    public void MoveTo_KeepsTheTunedValuesOfAllShips_AndTouchedStays()
    {
        TuningLab lab = Open(ShipCatalog.Ship02);
        lab.SetPercent(ShipCatalog.Ship02, ShipParameter.MaxSpeed, 15);
        lab.SetPercent(ShipCatalog.Ship03, ShipParameter.TurnRate, -12);

        lab.MoveTo(ShipCatalog.Ship03);

        Assert.True(lab.Touched);
        Assert.Equal(15, lab.Percent(ShipCatalog.Ship02, ShipParameter.MaxSpeed));
        Assert.Equal(-12, lab.Percent(ShipCatalog.Ship03, ShipParameter.TurnRate));
    }

    [Fact]
    public void MoveTo_UnknownId_ChangesNothing()
    {
        TuningLab lab = Open(ShipCatalog.Ship02);

        lab.MoveTo("nope");

        Assert.Equal(ShipCatalog.Ship02, lab.Current.Id);
    }

    [Fact]
    public void Starts_Untouched_WithTheValuesOfTheDefinitions()
    {
        TuningLab lab = Open();

        Assert.False(lab.Touched);
        Assert.Equal(ShipCatalog.Ship01, lab.Current.Id);
        Assert.Equal(BladeShip.Definition.Technical, lab.Tuning(ShipCatalog.Ship02));
        Assert.Equal(8, lab.Percent(ShipCatalog.Ship02, ShipParameter.MaxSpeed));
        Assert.Equal(-6, lab.Percent(ShipCatalog.Ship03, ShipParameter.MaxSpeed));
    }

    [Fact]
    public void UnknownShip_StartsOnTheFirst()
    {
        Assert.Equal(ShipCatalog.Ship01, Open("nope").Current.Id);
    }

    [Fact]
    public void Cycle_VectorBladeHammerVector()
    {
        TuningLab lab = Open();

        lab.Cycle();
        Assert.Equal(ShipCatalog.Ship02, lab.Current.Id);
        lab.Cycle();
        Assert.Equal(ShipCatalog.Ship03, lab.Current.Id);
        lab.Cycle();
        Assert.Equal(ShipCatalog.Ship01, lab.Current.Id);
    }

    [Fact]
    public void SetPercent_ChangesOnlyThatShip()
    {
        TuningLab lab = Open();

        Assert.True(lab.SetPercent(ShipCatalog.Ship02, ShipParameter.LateralGrip, 5));

        Assert.Equal(VectorTuning.LateralGrip * 1.05f, lab.Tuning(ShipCatalog.Ship02).LateralGrip);
        Assert.Equal(HammerShip.Definition.Technical, lab.Tuning(ShipCatalog.Ship03));
        Assert.Equal(VectorShip.Definition.Technical, lab.Tuning(ShipCatalog.Ship01));
    }

    [Fact]
    public void SetPercent_ClampedToTheRangeOfThePanel()
    {
        TuningLab lab = Open();

        lab.SetPercent(ShipCatalog.Ship02, ShipParameter.MaxSpeed, 50);

        Assert.Equal(TuningLab.MaxPercent, lab.Percent(ShipCatalog.Ship02, ShipParameter.MaxSpeed));
    }

    [Fact]
    public void Vector_IsTheReference_CannotBeChanged()
    {
        TuningLab lab = Open();

        Assert.False(TuningLab.IsEditable(ShipCatalog.Ship01));
        Assert.True(TuningLab.IsEditable(ShipCatalog.Ship02));
        Assert.False(lab.SetPercent(ShipCatalog.Ship01, ShipParameter.MaxSpeed, 5));
        Assert.Equal(VectorShip.Definition.Technical, lab.Tuning(ShipCatalog.Ship01));
        Assert.False(lab.Touched);
    }

    [Fact]
    public void Reset_BackToTheMultipliersOfTheDefinition()
    {
        TuningLab lab = Open();
        lab.SetPercent(ShipCatalog.Ship02, ShipParameter.MaxSpeed, 15);

        lab.Reset(ShipCatalog.Ship02);

        Assert.Same(BladeShip.Definition.Multipliers, lab.Multipliers(ShipCatalog.Ship02));
        Assert.Equal(BladeShip.Definition.Technical, lab.Tuning(ShipCatalog.Ship02));
    }

    [Fact]
    public void Touched_AfterAnyChange_AndResetDoesNotClearIt()
    {
        TuningLab changed = Open();
        changed.SetPercent(ShipCatalog.Ship03, ShipParameter.TurnRate, 1);
        Assert.True(changed.Touched);

        changed.Reset(ShipCatalog.Ship03);
        Assert.True(changed.Touched);

        TuningLab cycled = Open();
        cycled.Cycle();
        Assert.True(cycled.Touched);
    }

    [Fact]
    public void SameValue_OrResetWithoutChanges_DoNotTouch()
    {
        TuningLab lab = Open();

        Assert.False(lab.SetPercent(ShipCatalog.Ship02, ShipParameter.MaxSpeed, 8));
        lab.Reset(ShipCatalog.Ship02);

        Assert.False(lab.Touched);
    }

    [Fact]
    public void DumpText_AllShips_ReadyToCopy_WithOverLimitMarked()
    {
        TuningLab lab = Open();

        string clean = lab.DumpText();
        Assert.Contains("ship_01: new ShipMultipliers { MaxSpeed = 1.00f", clean);
        Assert.Contains("ship_02: new ShipMultipliers { MaxSpeed = 1.08f, Acceleration = 0.94f, TurnRate = 1.00f, LateralGrip = 0.92f, BrakeDeceleration = 1.02f }", clean);
        Assert.Contains("ship_03: new ShipMultipliers { MaxSpeed = 0.94f, Acceleration = 1.10f", clean);
        Assert.DoesNotContain("OVER LIMIT", clean);

        lab.SetPercent(ShipCatalog.Ship03, ShipParameter.LateralGrip, 15);
        Assert.Contains("LateralGrip = 1.15f", lab.DumpText());
        Assert.Contains("OVER LIMIT: LateralGrip", lab.DumpText());
    }

    [Theory]
    [InlineData(8, "+8 %")]
    [InlineData(-6, "-6 %")]
    [InlineData(0, "0 %")]
    public void PercentText(int percent, string text)
    {
        Assert.Equal(text, TuningLab.PercentText(percent));
    }
}
