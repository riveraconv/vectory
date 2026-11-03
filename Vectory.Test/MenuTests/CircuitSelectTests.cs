using Vectory.Circuit;
using Vectory.Game;
using Vectory.Menu;
using Xunit;

namespace Vectory.Test.MenuTests;

public class CircuitSelectLogicTests
{
    private const string Circuit01 = "res://circuits/circuit_01.tres";
    private const string TestOval = "res://circuits/test_oval.tres";
    private const string TestHills = "res://circuits/test_hills.tres";
    private static readonly string[] TwoCircuits = { Circuit01, TestOval };

    // ---- 9. Lógica de la pantalla ----

    [Fact]
    public void Open_StartSelectedAndSessionCircuit()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, TwoCircuits, TestOval);

        Assert.Equal(CircuitSelectLogic.StartId, logic.Menu.Confirm());
        Assert.Equal(1, logic.CircuitIndex);
        Assert.Equal(TestOval, logic.CircuitPath);
        Assert.Equal(GameMode.Race, logic.Mode);
    }

    [Fact]
    public void Open_WithoutSessionCircuit_First()
    {
        var logic = new CircuitSelectLogic(GameMode.TimeTrial, TwoCircuits, null!);

        Assert.Equal(0, logic.CircuitIndex);
        Assert.Equal(Circuit01, logic.CircuitPath);
    }

    [Fact]
    public void Open_SessionCircuitNotInList_First()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, new[] { Circuit01 }, TestOval);

        Assert.Equal(0, logic.CircuitIndex);
    }

    [Fact]
    public void NextAndPrevious_WrapAround()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, TwoCircuits, Circuit01);

        logic.NextCircuit();
        Assert.Equal(1, logic.CircuitIndex);
        logic.NextCircuit();
        Assert.Equal(0, logic.CircuitIndex);
        logic.PreviousCircuit();
        Assert.Equal(1, logic.CircuitIndex);
    }

    [Fact]
    public void OneCircuit_NextAndPreviousDoNothing()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, new[] { Circuit01 }, null!);

        logic.NextCircuit();
        logic.PreviousCircuit();

        Assert.False(logic.CanChangeCircuit);
        Assert.Equal(0, logic.CircuitIndex);
    }

    [Fact]
    public void Start_ReturnsStartWithCurrentCircuit()
    {
        var logic = new CircuitSelectLogic(GameMode.TimeTrial, TwoCircuits, Circuit01);
        logic.NextCircuit();

        Assert.Equal(CircuitSelectAction.Start, logic.Choose(logic.Menu.Confirm()));
        Assert.Equal(TestOval, logic.CircuitPath);
    }

    [Fact]
    public void BackOption_ReturnsBackToMenu()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, TwoCircuits, Circuit01);
        logic.Menu.MoveNext();

        Assert.Equal(CircuitSelectLogic.BackId, logic.Menu.Confirm());
        Assert.Equal(CircuitSelectAction.BackToMenu, logic.Choose(logic.Menu.Confirm()));
    }

    [Fact]
    public void Escape_ReturnsBackToMenu()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, TwoCircuits, Circuit01);

        Assert.Equal(CircuitSelectAction.BackToMenu, logic.Back());
    }

    [Fact]
    public void Options_WrapBetweenStartAndBack()
    {
        var logic = new CircuitSelectLogic(GameMode.Race, TwoCircuits, Circuit01);

        logic.Menu.MovePrevious();
        Assert.Equal(CircuitSelectLogic.BackId, logic.Menu.Confirm());
        logic.Menu.MoveNext();
        Assert.Equal(CircuitSelectLogic.StartId, logic.Menu.Confirm());
    }

    [Fact]
    public void NoCircuits_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitSelectLogic(GameMode.Race, Array.Empty<string>(), null!));
    }

    // ---- 11. Catálogo ----

    [Fact]
    public void Catalog_OrderCircuit01ThenTestOvalThenTestHills()
    {
        Assert.Equal(new[] { Circuit01, TestOval, TestHills }, CircuitCatalog.AllPaths);
    }

    [Fact]
    public void Catalog_WithoutTestCircuits_HidesTestCircuits()
    {
        var paths = CircuitCatalog.Filter(CircuitCatalog.AllPaths, false, p => p == TestOval || p == TestHills);

        Assert.Equal(new[] { Circuit01 }, paths);
    }

    [Fact]
    public void Catalog_WithTestCircuits_TestCircuitsAfterCircuit01()
    {
        var paths = CircuitCatalog.Filter(CircuitCatalog.AllPaths, true, p => p == TestOval || p == TestHills);

        Assert.Equal(new[] { Circuit01, TestOval, TestHills }, paths);
    }
}

public class CircuitSelectFormatTests
{
    // ---- 12. Formato ----

    [Theory]
    [InlineData(4213f, "4.2 KM")]
    [InlineData(1594f, "1.6 KM")]
    [InlineData(950f, "1.0 KM")]
    public void Length(float meters, string expected) => Assert.Equal(expected, CircuitSelectFormat.Length(meters));

    [Theory]
    [InlineData(816f, "820 M")]
    [InlineData(413.7f, "410 M")]
    [InlineData(0f, "0 M")]
    public void Straight_RoundedToTen(float meters, string expected) => Assert.Equal(expected, CircuitSelectFormat.Straight(meters));

    [Fact]
    public void Width() => Assert.Equal("24 M", CircuitSelectFormat.Width(24f));

    [Fact]
    public void Banking() => Assert.Equal("0°", CircuitSelectFormat.Banking(0f));

    [Fact]
    public void Race_LapsAndGrid()
    {
        Assert.Equal("3", CircuitSelectFormat.Laps(GameMode.Race, 3));
        Assert.Equal("8 SHIPS", CircuitSelectFormat.Grid(GameMode.Race, 8));
    }

    [Fact]
    public void TimeTrial_FreeAndNoGrid()
    {
        Assert.Equal("FREE", CircuitSelectFormat.Laps(GameMode.TimeTrial, 3));
        Assert.Equal("—", CircuitSelectFormat.Grid(GameMode.TimeTrial, 8));
    }

    [Theory]
    [InlineData(0, 2, "01 / 02")]
    [InlineData(1, 2, "02 / 02")]
    [InlineData(0, 1, "01 / 01")]
    public void Counter(int index, int count, string expected) => Assert.Equal(expected, CircuitSelectFormat.Counter(index, count));

    [Fact]
    public void Description()
    {
        Assert.Equal("CLOSED LOOP · CLOCKWISE", CircuitSelectFormat.Description(false, true));
        Assert.Equal("CLOSED LOOP · COUNTERCLOCKWISE", CircuitSelectFormat.Description(false, false));
        Assert.Equal("TEST TRACK · CLOCKWISE", CircuitSelectFormat.Description(true, true));
    }

    [Fact]
    public void Headers()
    {
        Assert.Equal("RACE · SELECT CIRCUIT", CircuitSelectFormat.Header(GameMode.Race));
        Assert.Equal("TIME TRIAL · SELECT CIRCUIT", CircuitSelectFormat.Header(GameMode.TimeTrial));
        Assert.Equal("RECORDS · RACE", CircuitSelectFormat.RecordsHeader(GameMode.Race));
        Assert.Equal("RECORDS · TIME TRIAL", CircuitSelectFormat.RecordsHeader(GameMode.TimeTrial));
    }

    [Fact]
    public void Times()
    {
        Assert.Equal("--:--.---", CircuitSelectFormat.Time(null));
        Assert.Equal("0:37.067", CircuitSelectFormat.Time(37.0666));
    }

    [Fact]
    public void Ghost()
    {
        Assert.Equal("AVAILABLE", CircuitSelectFormat.Ghost(true));
        Assert.Equal("NONE", CircuitSelectFormat.Ghost(false));
    }

    [Fact]
    public void ProfileLabels()
    {
        Assert.Equal("+20 M", CircuitSelectFormat.ScaleTop(20f));
        Assert.Equal("−30 M", CircuitSelectFormat.ScaleBottom(30f));
        Assert.Equal("S1", CircuitSelectFormat.Sector(0));
    }
}
