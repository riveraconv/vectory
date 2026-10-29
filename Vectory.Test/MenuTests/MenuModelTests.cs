using Vectory.Menu;
using Xunit;

namespace Vectory.Test.MenuTests;

public class MenuModelTests
{
    // Las opciones del menú principal (spec docs/menu-principal.md): solo RACE y QUIT activas
    private static MenuOption[] MainMenuOptions() => new[]
    {
        new MenuOption("RACE", "RACE", true),
        new MenuOption("CHAMPIONSHIP", "CHAMPIONSHIP", false),
        new MenuOption("TIME_TRIAL", "TIME TRIAL", false),
        new MenuOption("HANGAR", "HANGAR", false),
        new MenuOption("RECORDS", "RECORDS", false),
        new MenuOption("SETTINGS", "SETTINGS", false),
        new MenuOption("QUIT", "QUIT", true)
    };

    private const int Race = 0;
    private const int Hangar = 3;
    private const int Quit = 6;

    [Fact]
    public void Created_SelectsRace()
    {
        var menu = new MenuModel(MainMenuOptions());

        Assert.Equal(Race, menu.SelectedIndex);
    }

    [Fact]
    public void MoveNext_FromRace_SkipsDisabledToQuit()
    {
        var menu = new MenuModel(MainMenuOptions());

        menu.MoveNext();

        Assert.Equal(Quit, menu.SelectedIndex);
    }

    [Fact]
    public void MoveNext_FromQuit_WrapsToRace()
    {
        var menu = new MenuModel(MainMenuOptions());
        menu.MoveNext();

        menu.MoveNext();

        Assert.Equal(Race, menu.SelectedIndex);
    }

    [Fact]
    public void MovePrevious_FromRace_WrapsToQuit()
    {
        var menu = new MenuModel(MainMenuOptions());

        menu.MovePrevious();

        Assert.Equal(Quit, menu.SelectedIndex);
    }

    [Fact]
    public void Select_DisabledOption_KeepsSelection()
    {
        var menu = new MenuModel(MainMenuOptions());

        menu.Select(Hangar);

        Assert.Equal(Race, menu.SelectedIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Select_OutOfRange_KeepsSelection(int index)
    {
        var menu = new MenuModel(MainMenuOptions());

        menu.Select(index);

        Assert.Equal(Race, menu.SelectedIndex);
    }

    [Fact]
    public void Select_EnabledOption_SelectsIt()
    {
        var menu = new MenuModel(MainMenuOptions());

        menu.Select(Quit);

        Assert.Equal(Quit, menu.SelectedIndex);
    }

    [Fact]
    public void Confirm_ReturnsSelectedId()
    {
        var menu = new MenuModel(MainMenuOptions());
        Assert.Equal("RACE", menu.Confirm());

        menu.MoveNext();
        Assert.Equal("QUIT", menu.Confirm());
    }

    [Fact]
    public void Created_FirstOptionDisabled_SelectsFirstEnabled()
    {
        var menu = new MenuModel(new[]
        {
            new MenuOption("A", "A", false),
            new MenuOption("B", "B", false),
            new MenuOption("C", "C", true),
            new MenuOption("D", "D", true)
        });

        Assert.Equal(2, menu.SelectedIndex);
    }

    [Fact]
    public void MoveNext_AllEnabled_VisitsEveryOptionInOrder()
    {
        var menu = new MenuModel(new[]
        {
            new MenuOption("A", "A", true),
            new MenuOption("B", "B", true),
            new MenuOption("C", "C", true)
        });

        Assert.Equal(0, menu.SelectedIndex);
        menu.MoveNext();
        Assert.Equal(1, menu.SelectedIndex);
        menu.MoveNext();
        Assert.Equal(2, menu.SelectedIndex);
        menu.MoveNext();
        Assert.Equal(0, menu.SelectedIndex);
    }

    [Fact]
    public void NoEnabledOptions_NothingSelectedAndNothingHappens()
    {
        var menu = new MenuModel(new[]
        {
            new MenuOption("A", "A", false),
            new MenuOption("B", "B", false)
        });

        menu.MoveNext();
        menu.MovePrevious();

        Assert.Equal(-1, menu.SelectedIndex);
        Assert.Null(menu.Confirm());
    }
}
