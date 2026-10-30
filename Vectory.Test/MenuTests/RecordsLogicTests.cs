using Vectory.Menu;
using Xunit;

namespace Vectory.Test.MenuTests;

public class RecordsLogicTests
{
    private static string Selected(RecordsLogic logic) => logic.Menu.Confirm();

    [Fact]
    public void Open_ViewingFirstCircuitBackSelected()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);

        Assert.Equal(RecordsState.Viewing, logic.State);
        Assert.Equal(0, logic.CircuitIndex);
        Assert.Equal(RecordsLogic.BackId, Selected(logic));
    }

    [Fact]
    public void NoRecords_ResetDisabled_MoveNextStaysOnBack()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: false);

        Assert.False(logic.Menu.Options[1].Enabled);
        logic.Menu.MoveNext();
        Assert.Equal(RecordsLogic.BackId, Selected(logic));
        Assert.Equal(RecordsAction.None, logic.Choose(RecordsLogic.ResetId));
        Assert.Equal(RecordsState.Viewing, logic.State);
    }

    [Fact]
    public void WithRecords_ResetEnabledAndSelectable()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);

        Assert.True(logic.Menu.Options[1].Enabled);
        logic.Menu.MoveNext();
        Assert.Equal(RecordsLogic.ResetId, Selected(logic));
    }

    [Fact]
    public void Reset_AsksConfirmationWithNo_NoReturnsWithResetSelected()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);

        Assert.Equal(RecordsAction.None, logic.Choose(RecordsLogic.ResetId));
        Assert.Equal(RecordsState.ConfirmReset, logic.State);
        Assert.Equal(RecordsLogic.NoId, Selected(logic));

        Assert.Equal(RecordsAction.None, logic.Choose(RecordsLogic.NoId));
        Assert.Equal(RecordsState.Viewing, logic.State);
        Assert.Equal(RecordsLogic.ResetId, Selected(logic));
    }

    [Fact]
    public void Confirm_Escape_SameAsNo()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);
        logic.Choose(RecordsLogic.ResetId);

        Assert.Equal(RecordsAction.None, logic.Back());
        Assert.Equal(RecordsState.Viewing, logic.State);
        Assert.Equal(RecordsLogic.ResetId, Selected(logic));
    }

    [Fact]
    public void Yes_ResetsAndReturnsWithoutRecords()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);
        logic.Choose(RecordsLogic.ResetId);

        Assert.Equal(RecordsAction.ResetRecords, logic.Choose(RecordsLogic.YesId));
        Assert.Equal(RecordsState.Viewing, logic.State);
        Assert.Equal(RecordsLogic.BackId, Selected(logic));
        Assert.False(logic.HasAnyRecord);
        Assert.False(logic.Menu.Options[1].Enabled);
    }

    [Fact]
    public void Viewing_BackOrEscape_GoesToMenu()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);

        Assert.Equal(RecordsAction.BackToMenu, logic.Choose(RecordsLogic.BackId));
        Assert.Equal(RecordsAction.BackToMenu, logic.Back());
    }

    [Fact]
    public void OneCircuit_ChangingCircuitDoesNothing()
    {
        var logic = new RecordsLogic(1, hasAnyRecord: true);

        logic.NextCircuit();
        Assert.Equal(0, logic.CircuitIndex);
        logic.PreviousCircuit();
        Assert.Equal(0, logic.CircuitIndex);
        Assert.False(logic.CanChangeCircuit);
    }

    [Fact]
    public void ThreeCircuits_WrapBothWays()
    {
        var logic = new RecordsLogic(3, hasAnyRecord: true);

        logic.PreviousCircuit();
        Assert.Equal(2, logic.CircuitIndex);
        logic.NextCircuit();
        Assert.Equal(0, logic.CircuitIndex);
        logic.NextCircuit();
        logic.NextCircuit();
        Assert.Equal(2, logic.CircuitIndex);
        logic.NextCircuit();
        Assert.Equal(0, logic.CircuitIndex);
    }

    [Fact]
    public void ConfirmReset_ChangingCircuitHasNoEffect()
    {
        var logic = new RecordsLogic(3, hasAnyRecord: true);
        logic.NextCircuit();
        logic.Choose(RecordsLogic.ResetId);

        logic.NextCircuit();
        logic.PreviousCircuit();

        Assert.Equal(1, logic.CircuitIndex);
        Assert.False(logic.CanChangeCircuit);
    }
}
