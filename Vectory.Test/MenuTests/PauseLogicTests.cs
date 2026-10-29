using Vectory.Menu;
using Xunit;

namespace Vectory.Test.MenuTests;

public class PauseLogicTests
{
    private static PauseLogic OpenPause(bool raceFinished = false)
    {
        var pause = new PauseLogic { RaceFinished = raceFinished };
        pause.TogglePause();
        return pause;
    }

    private static string Selected(PauseLogic pause) => pause.Menu.Confirm();

    [Fact]
    public void StartsClosed_ToggleOpensWithContinueSelected()
    {
        var pause = new PauseLogic();
        Assert.Equal(PauseState.Closed, pause.State);
        Assert.Null(pause.Menu);

        PauseAction action = pause.TogglePause();

        Assert.Equal(PauseAction.None, action);
        Assert.Equal(PauseState.Open, pause.State);
        Assert.Equal(PauseLogic.ContinueId, Selected(pause));
    }

    [Fact]
    public void Open_Toggle_ClosesLikeContinue()
    {
        var pause = OpenPause();

        PauseAction action = pause.TogglePause();

        Assert.Equal(PauseAction.Close, action);
        Assert.Equal(PauseState.Closed, pause.State);
    }

    [Fact]
    public void Open_ChooseContinue_Closes()
    {
        var pause = OpenPause();

        PauseAction action = pause.Choose(PauseLogic.ContinueId);

        Assert.Equal(PauseAction.Close, action);
        Assert.Equal(PauseState.Closed, pause.State);
    }

    [Theory]
    [InlineData(PauseLogic.RestartId, PauseTarget.Restart)]
    [InlineData(PauseLogic.BackToMenuId, PauseTarget.BackToMenu)]
    public void RaceNotFinished_RestartOrBack_AsksConfirmationWithNoSelected(string option, PauseTarget target)
    {
        var pause = OpenPause();

        PauseAction action = pause.Choose(option);

        Assert.Equal(PauseAction.None, action);
        Assert.Equal(PauseState.Confirm, pause.State);
        Assert.Equal(target, pause.Confirming);
        Assert.Equal(PauseLogic.NoId, Selected(pause));
    }

    [Theory]
    [InlineData(PauseLogic.RestartId)]
    [InlineData(PauseLogic.BackToMenuId)]
    public void Confirm_No_BackToPauseWithOriginSelected(string option)
    {
        var pause = OpenPause();
        pause.Choose(option);

        PauseAction action = pause.Choose(PauseLogic.NoId);

        Assert.Equal(PauseAction.None, action);
        Assert.Equal(PauseState.Open, pause.State);
        Assert.Equal(PauseTarget.None, pause.Confirming);
        Assert.Equal(option, Selected(pause));
    }

    [Theory]
    [InlineData(PauseLogic.RestartId)]
    [InlineData(PauseLogic.BackToMenuId)]
    public void Confirm_Toggle_SameAsNo(string option)
    {
        var pause = OpenPause();
        pause.Choose(option);

        PauseAction action = pause.TogglePause();

        Assert.Equal(PauseAction.None, action);
        Assert.Equal(PauseState.Open, pause.State);
        Assert.Equal(option, Selected(pause));
    }

    [Theory]
    [InlineData(PauseLogic.RestartId, PauseAction.Restart)]
    [InlineData(PauseLogic.BackToMenuId, PauseAction.GoToMenu)]
    public void Confirm_Yes_ReturnsAction(string option, PauseAction expected)
    {
        var pause = OpenPause();
        pause.Choose(option);

        PauseAction action = pause.Choose(PauseLogic.YesId);

        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData(PauseLogic.RestartId, PauseAction.Restart)]
    [InlineData(PauseLogic.BackToMenuId, PauseAction.GoToMenu)]
    public void RaceFinished_RestartOrBack_NoConfirmation(string option, PauseAction expected)
    {
        var pause = OpenPause(raceFinished: true);

        PauseAction action = pause.Choose(option);

        Assert.Equal(expected, action);
        Assert.NotEqual(PauseState.Confirm, pause.State);
    }

    [Fact]
    public void Reopen_SelectsContinueAgain()
    {
        var pause = OpenPause();
        pause.Choose(PauseLogic.RestartId);
        pause.Choose(PauseLogic.NoId);     // de vuelta en la pausa con RESTART seleccionada
        pause.TogglePause();               // cerrar

        pause.TogglePause();               // reabrir

        Assert.Equal(PauseState.Open, pause.State);
        Assert.Equal(PauseLogic.ContinueId, Selected(pause));
    }

    [Fact]
    public void Open_NavigationUsesMenuModel()
    {
        var pause = OpenPause();

        pause.Menu.MoveNext();
        Assert.Equal(PauseLogic.RestartId, Selected(pause));
        pause.Menu.MoveNext();
        Assert.Equal(PauseLogic.BackToMenuId, Selected(pause));
        pause.Menu.MoveNext();
        Assert.Equal(PauseLogic.ContinueId, Selected(pause));
    }

    [Fact]
    public void Closed_ChooseDoesNothing()
    {
        var pause = new PauseLogic();

        Assert.Equal(PauseAction.None, pause.Choose(PauseLogic.RestartId));
        Assert.Equal(PauseState.Closed, pause.State);
    }

    [Fact]
    public void Open_ConfirmIdsDoNothing()
    {
        var pause = OpenPause();

        Assert.Equal(PauseAction.None, pause.Choose(PauseLogic.YesId));
        Assert.Equal(PauseState.Open, pause.State);
    }
}
