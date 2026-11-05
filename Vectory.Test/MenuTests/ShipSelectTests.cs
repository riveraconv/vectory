using System;
using System.Linq;
using System.Numerics;
using Vectory.Game;
using Vectory.Menu;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.MenuTests;

public class ShipCatalogTests
{
    [Fact]
    public void Entries_InSelectorOrder_OnlyVectorSelectable()
    {
        Assert.Equal(new[] { "BLADE", "VECTOR", "HAMMER" }, ShipCatalog.Entries.Select(e => e.Name));
        Assert.Equal(new[] { "ship_02", "ship_01", "ship_03" }, ShipCatalog.Entries.Select(e => e.Id));
        Assert.Equal(new[] { ShipCatalog.Ship01 }, ShipCatalog.Entries.Where(e => e.Selectable).Select(e => e.Id));
    }

    [Fact]
    public void Default_IsShip01_NotTheFirstOfTheList()
    {
        Assert.Equal("ship_01", ShipCatalog.Default);
        Assert.NotEqual(ShipCatalog.Default, ShipCatalog.Entries[0].Id);
    }

    [Fact]
    public void Contains_And_Find()
    {
        Assert.True(ShipCatalog.Contains("ship_01"));
        Assert.False(ShipCatalog.Contains("nope"));
        Assert.False(ShipCatalog.Contains(null));
        Assert.Equal("VECTOR", ShipCatalog.Find("ship_01").Name);
        Assert.Null(ShipCatalog.Find("nope"));
    }

    [Fact]
    public void VectorBaseValues_AreTheOnesOfTheRaceScene()
    {
        ShipTuning t = VectorTuning.Values;

        Assert.Equal(110f, t.MaxSpeed);
        Assert.Equal(5f, t.Drag);
        Assert.Equal(1.3f, t.TurnRate);
        Assert.Equal(8f, t.SteerResponse);
        Assert.Equal(25f, t.Acceleration);
        Assert.Equal(45f, t.BrakeDeceleration);
        Assert.Equal(5f, t.LateralGrip);
        Assert.Equal(20f / 3.6f, t.OverspeedDecay);
    }
}

public class ShipStatsTests
{
    private static ShipStat Stat(ShipTuning ship, string label) =>
        ShipStats.Compute(ship, VectorTuning.Values).Single(s => s.Label == label);

    [Fact]
    public void Vector_ValuesAndHalfBars()
    {
        ShipTuning vector = VectorTuning.Values;

        Assert.Equal("396 KM/H", Stat(vector, ShipStats.TopSpeed).Value);
        Assert.Equal("0–200 · 2.2 S", Stat(vector, ShipStats.Acceleration).Value);
        Assert.Equal("74 °/S", Stat(vector, ShipStats.Handling).Value);
        Assert.Equal("", Stat(vector, ShipStats.Grip).Value);
        Assert.Equal("200–0 · 1.2 S", Stat(vector, ShipStats.Braking).Value);
        Assert.All(ShipStats.Compute(vector, vector), s => Assert.Equal(5, s.Segments));
    }

    [Theory]
    [InlineData(1.10f, 10)]
    [InlineData(0.90f, 0)]
    [InlineData(1.02f, 6)]
    [InlineData(0.98f, 4)]
    [InlineData(1.30f, 10)]
    [InlineData(0.50f, 0)]
    [InlineData(1.00f, 5)]
    public void Bars_ComparedToReference(float factor, int segments)
    {
        ShipTuning ship = VectorTuning.Values;
        ship.MaxSpeed *= factor;

        Assert.Equal(segments, Stat(ship, ShipStats.TopSpeed).Segments);
    }

    [Fact]
    public void Bars_UseTheParameterWhereMoreIsBetter()
    {
        ShipTuning ship = VectorTuning.Values;
        ship.Acceleration *= 1.1f;
        ship.BrakeDeceleration *= 0.9f;
        ship.LateralGrip *= 1.02f;

        Assert.Equal(10, Stat(ship, ShipStats.Acceleration).Segments);
        Assert.Equal(0, Stat(ship, ShipStats.Braking).Segments);
        Assert.Equal(6, Stat(ship, ShipStats.Grip).Segments);
    }

    [Fact]
    public void Empty_DashesAndNoSegments()
    {
        Assert.Equal(5, ShipStats.Empty().Count);
        Assert.All(ShipStats.Empty(), s =>
        {
            Assert.Equal(ShipSelectFormat.None, s.Value);
            Assert.Equal(0, s.Segments);
        });
    }
}

public class ShipSelectLogicTests
{
    private static ShipSelectLogic Open(string? currentId, GameMode mode = GameMode.Race) =>
        new(mode, "Circuit 01", ShipCatalog.Entries, currentId, ShipCatalog.Default);

    [Fact]
    public void Open_RememberedShip_StartSelected()
    {
        ShipSelectLogic logic = Open("ship_01");

        Assert.Equal("ship_01", logic.ShipId);
        Assert.Equal(1, logic.ShipIndex);
        Assert.Equal(ShipSelectLogic.StartId, logic.Menu.Confirm());
    }

    [Theory]
    [InlineData("ship_02")]
    [InlineData("ship_03")]
    [InlineData("unknown")]
    [InlineData(null)]
    public void Open_NotSelectableOrUnknown_Vector(string? id)
    {
        ShipSelectLogic logic = Open(id);

        Assert.Equal("ship_01", logic.ShipId);
        Assert.Equal(ShipSelectLogic.StartId, logic.Menu.Confirm());
    }

    [Fact]
    public void NextAndPrevious_WrapAndPassThroughNotSelectable()
    {
        ShipSelectLogic logic = Open("ship_01");

        logic.NextShip();
        Assert.Equal("ship_03", logic.ShipId);
        logic.NextShip();
        Assert.Equal("ship_02", logic.ShipId);
        logic.NextShip();
        Assert.Equal("ship_01", logic.ShipId);

        logic.PreviousShip();
        Assert.Equal("ship_02", logic.ShipId);
        logic.PreviousShip();
        Assert.Equal("ship_03", logic.ShipId);
    }

    [Fact]
    public void NotSelectable_StartDisabled_BackSelected_ChooseStartDoesNothing()
    {
        ShipSelectLogic logic = Open("ship_01");
        logic.NextShip();

        Assert.False(logic.Menu.Options[0].Enabled);
        Assert.Equal(ShipSelectLogic.BackId, logic.Menu.Confirm());
        Assert.Equal(ShipSelectAction.None, logic.Choose(ShipSelectLogic.StartId));

        logic.Menu.MoveNext();
        Assert.Equal(ShipSelectLogic.BackId, logic.Menu.Confirm());
    }

    [Fact]
    public void BackToSelectable_StartSelectedAgain()
    {
        ShipSelectLogic logic = Open("ship_01");
        logic.NextShip();
        logic.PreviousShip();

        Assert.Equal(ShipSelectLogic.StartId, logic.Menu.Confirm());
    }

    [Fact]
    public void Actions()
    {
        ShipSelectLogic logic = Open("ship_01");

        Assert.Equal(ShipSelectAction.Start, logic.Choose(ShipSelectLogic.StartId));
        Assert.Equal("ship_01", logic.ShipId);
        Assert.Equal(ShipSelectAction.BackToCircuitSelect, logic.Choose(ShipSelectLogic.BackId));
        Assert.Equal(ShipSelectAction.BackToCircuitSelect, logic.Back());
        Assert.Equal(ShipSelectAction.None, logic.Choose(null));
    }
}

public class ShipSelectFormatTests
{
    [Fact]
    public void Header_ModeAndUpperCaseCircuit()
    {
        Assert.Equal("RACE · CIRCUIT 01 · SELECT SHIP", ShipSelectFormat.Header(GameMode.Race, "Circuit 01"));
        Assert.Equal("TIME TRIAL · TEST OVAL · SELECT SHIP", ShipSelectFormat.Header(GameMode.TimeTrial, "Test Oval"));
    }

    [Fact]
    public void Counter() => Assert.Equal("02 / 03", ShipSelectFormat.Counter(1, 3));

    [Fact]
    public void Damage_ByMode()
    {
        Assert.Equal("3 LEVELS", ShipSelectFormat.Damage(GameMode.Race, DamageModel.MaxLevel));
        Assert.Equal("OFF", ShipSelectFormat.Damage(GameMode.TimeTrial, DamageModel.MaxLevel));
    }

    [Fact]
    public void Boost() =>
        Assert.Equal("3 × +100 KM/H", ShipSelectFormat.Boost(BoostTuning.DefaultMaxCharges, BoostTuning.DefaultSpeedGainKmh));
}

public class HologramSpinTests
{
    private const float Tolerance = 1e-3f;

    private static void AssertSameRotation(Quaternion expected, Quaternion actual)
    {
        foreach (Vector3 axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            Vector3 a = Vector3.Transform(axis, expected);
            Vector3 b = Vector3.Transform(axis, actual);
            Assert.True(Vector3.Distance(a, b) < Tolerance, $"{a} != {b}");
        }
    }

    // Arrastre a 60 fps hasta soltar
    private static HologramSpin SpinReleasedWithVelocity()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        spin.BeginDrag();
        for (int i = 0; i < 30; i++)
        {
            spin.Drag(10f, 0f);
            spin.Step(1f / 60f);
        }
        spin.Release();
        return spin;
    }

    [Fact]
    public void Initial_NoseTowardsCameraOnTheLeft_SeenFromAbove()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);

        Vector3 nose = Vector3.Transform(-Vector3.UnitZ, spin.Orientation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, spin.Orientation);

        Assert.True(nose.X < 0f, "morro a la izquierda");
        Assert.True(nose.Z > 0f, "morro hacia la cámara");
        Assert.True(up.Z > 0f, "se ve por arriba");
    }

    [Fact]
    public void Auto_OneTurnEveryPeriod()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        Quaternion start = spin.Orientation;

        spin.Step(20f);

        AssertSameRotation(start, spin.Orientation);
    }

    [Fact]
    public void Auto_QuarterPeriod_IsAQuarterTurnAroundScreenVertical()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        Quaternion start = spin.Orientation;

        spin.Step(5f);

        AssertSameRotation(Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f) * start, spin.Orientation);
    }

    [Fact]
    public void Release_VelocityDecays()
    {
        HologramSpin spin = SpinReleasedWithVelocity();
        float start = spin.Velocity.Length();
        Assert.True(start > 0f);

        spin.Step(0.4f);

        Assert.Equal(start * MathF.Exp(-1f), spin.Velocity.Length(), 3);
    }

    [Fact]
    public void Release_MouseStill_NoInertia()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        spin.BeginDrag();
        spin.Drag(10f, 0f);
        spin.Step(1f / 60f);
        for (int i = 0; i < 6; i++)
            spin.Step(1f / 60f);   // 100 ms sin mover

        spin.Release();

        Assert.Equal(Vector2.Zero, spin.Velocity);
    }

    [Fact]
    public void AfterRelease_NoAutoFor3Seconds_FullAt4()
    {
        HologramSpin spin = SpinReleasedWithVelocity();

        spin.Step(2.9f);
        Assert.Equal(0f, spin.AutoFactor);

        spin.Step(0.6f);   // 3,5 s
        Assert.Equal(0.5f, spin.AutoFactor, 3);

        spin.Step(0.5f);   // 4 s
        Assert.Equal(1f, spin.AutoFactor);
    }

    [Fact]
    public void AfterRelease_StillMouse_OrientationHoldsDuringWait()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        spin.BeginDrag();
        spin.Release();
        Quaternion start = spin.Orientation;

        spin.Step(3f);

        Assert.Equal(start, spin.Orientation);
    }

    [Fact]
    public void WhileDragging_NoAutoSpin()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        spin.BeginDrag();
        Quaternion start = spin.Orientation;

        spin.Step(5f);

        Assert.Equal(start, spin.Orientation);
        Assert.Equal(0f, spin.AutoFactor);
    }

    [Fact]
    public void AutoResume_IsContinuous_NoJumpWithManySmallSteps()
    {
        HologramSpin once = SpinReleasedWithVelocity();
        HologramSpin many = SpinReleasedWithVelocity();

        once.Step(4f);
        for (int i = 0; i < 400; i++)
            many.Step(0.01f);

        AssertSameRotation(once.Orientation, many.Orientation);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1.7f, 0.9f)]
    [InlineData(-2.4f, 3f)]
    public void DragHorizontal_AlwaysAroundScreenVertical(float pitch, float yaw)
    {
        HologramSpin spin = Oriented(pitch, yaw);
        Quaternion before = spin.Orientation;

        spin.Drag(50f, 0f);

        Quaternion expected = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f) * before;
        AssertSameRotation(expected, spin.Orientation);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1.7f, 0.9f)]
    public void DragVertical_AlwaysAroundScreenHorizontal(float pitch, float yaw)
    {
        HologramSpin spin = Oriented(pitch, yaw);
        Quaternion before = spin.Orientation;

        spin.Drag(0f, 50f);

        Quaternion expected = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f) * before;
        AssertSameRotation(expected, spin.Orientation);
    }

    [Fact]
    public void DragRight_FrontMovesRight_DragDown_FrontMovesDown()
    {
        var settings = HologramSpinSettings.Default;
        settings.InitialYaw = 0f;
        settings.InitialTilt = 0f;
        var spin = new HologramSpin(settings);
        spin.BeginDrag();

        spin.Drag(50f, 0f);
        Assert.True(Vector3.Transform(Vector3.UnitZ, spin.Orientation).X > 0f);

        var spin2 = new HologramSpin(settings);
        spin2.BeginDrag();
        spin2.Drag(0f, 50f);
        Assert.True(Vector3.Transform(Vector3.UnitZ, spin2.Orientation).Y < 0f);
    }

    [Fact]
    public void Drag_WithoutBeginDrag_DoesNothing()
    {
        var spin = new HologramSpin(HologramSpinSettings.Default);
        Quaternion start = spin.Orientation;

        spin.Drag(50f, 50f);

        Assert.Equal(start, spin.Orientation);
    }

    [Fact]
    public void StepZero_ChangesNothing()
    {
        HologramSpin spin = SpinReleasedWithVelocity();
        Quaternion orientation = spin.Orientation;
        Vector2 velocity = spin.Velocity;

        spin.Step(0f);

        Assert.Equal(orientation, spin.Orientation);
        Assert.Equal(velocity, spin.Velocity);
    }

    // Una orientación cualquiera, ya con el arrastre empezado
    private static HologramSpin Oriented(float pitch, float yaw)
    {
        var settings = HologramSpinSettings.Default;
        settings.InitialTilt = pitch;
        settings.InitialYaw = yaw;
        var spin = new HologramSpin(settings);
        spin.BeginDrag();
        return spin;
    }
}
