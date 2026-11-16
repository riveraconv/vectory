using System.Linq;
using System.Numerics;
using Vectory.Game;
using Vectory.Menu;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.MenuTests;

// Pantalla PAINT (spec docs/selector-pinturas.md, pruebas 7–17)
public class PaintSelectTests
{
    private static PaintSelectLogic Open(HullStyle style, PaintChoice saved, string ship = "ship_01") =>
        new(GameMode.Race, "Circuit 01", ship, style, saved);

    private static PaintSelectLogic OpenVector(int savedIndex = 0) =>
        Open(HullStyle.Racing, HullPaints.ChoiceAt(HullStyle.Racing, savedIndex));

    // ---------- 7. Cabecera ----------
    [Fact]
    public void Header_AddsPaintToTheSelectShipOne()
    {
        Assert.Equal("RACE · CIRCUIT 01 · SELECT SHIP · PAINT", PaintSelectFormat.Header(GameMode.Race, "Circuit 01"));
        Assert.Equal("TIME TRIAL · CIRCUIT 01 · SELECT SHIP · PAINT", PaintSelectFormat.Header(GameMode.TimeTrial, "Circuit 01"));
    }

    // ---------- 8. Línea de datos ----------
    [Fact]
    public void DataLine_AsInTheSpec()
    {
        Assert.Equal("SATIN · 01 / 40", PaintSelectFormat.DataLine(HullStyle.Racing, 0));
        Assert.Equal("SATIN · NO. 07 · 21 / 40", PaintSelectFormat.DataLine(HullStyle.Racing, 20));
        Assert.Equal("CAMO · MATTE · MARKINGS · 22 / 40", PaintSelectFormat.DataLine(HullStyle.Military, 21));
        Assert.Equal("TWO-TONE · 17 / 40", PaintSelectFormat.DataLine(HullStyle.Industrial, 16));
    }

    [Fact]
    public void DataLine_OtherCases()
    {
        Assert.Equal("MATTE · 11 / 40", PaintSelectFormat.DataLine(HullStyle.Military, 10));
        Assert.Equal("SATIN · 14 / 40", PaintSelectFormat.DataLine(HullStyle.Military, 13));
        Assert.Equal("INDUSTRIAL · 01 / 40", PaintSelectFormat.DataLine(HullStyle.Industrial, 0));
        Assert.Equal("INDUSTRIAL · FLEET MARKINGS · 21 / 40", PaintSelectFormat.DataLine(HullStyle.Industrial, 20));
        Assert.Equal("CHAMELEON · NO. 13 · 40 / 40", PaintSelectFormat.DataLine(HullStyle.Racing, 39));
        Assert.Equal("07 / 40", PaintSelectFormat.Counter(6));
    }

    [Fact]
    public void Names_GroupsAndFooter()
    {
        Assert.Equal("GRAPHITE", PaintSelectFormat.PaintName(HullStyle.Racing, 20));
        Assert.Equal("ORANGE · WHITE CAB", PaintSelectFormat.PaintName(HullStyle.Industrial, 36));
        Assert.Equal("RACING LIVERY", PaintSelectFormat.MarksGroup(HullStyle.Racing));
        Assert.Equal("MARKINGS", PaintSelectFormat.MarksGroup(HullStyle.Military));
        Assert.Equal("FLEET MARKINGS", PaintSelectFormat.MarksGroup(HullStyle.Industrial));
        Assert.Equal("← → ↑ ↓ PAINT · M FLEET MARKINGS · ENTER APPLY · ESC BACK · DRAG ROTATE",
            PaintSelectFormat.Footer(HullStyle.Industrial));
    }

    // ---------- 9. Etiqueta ----------
    [Fact]
    public void Status_EquippedOnlyForTheSavedOne()
    {
        PaintSelectLogic logic = OpenVector(3);
        Assert.True(logic.SelectionIsEquipped);
        Assert.Equal("EQUIPPED", PaintSelectFormat.Status(logic.SelectionIsEquipped));

        logic.Right();
        Assert.False(logic.SelectionIsEquipped);
        Assert.Equal("PREVIEW", PaintSelectFormat.Status(logic.SelectionIsEquipped));
        Assert.True(logic.IsEquipped(3));
    }

    // ---------- 10. Al abrir, la guardada ----------
    [Fact]
    public void Open_SelectsTheSavedPaint_InTheSwatches()
    {
        PaintSelectLogic logic = Open(HullStyle.Military, new PaintChoice("desert", true), "ship_02");

        Assert.Equal(21, logic.Index);
        Assert.Equal(21, logic.SavedIndex);
        Assert.Equal(new PaintChoice("desert", true), logic.Selection);
        Assert.Equal(PaintSelectFocus.Swatches, logic.Focus);
    }

    [Fact]
    public void Open_WithAnUnknownPaint_UsesTheDefault()
    {
        PaintSelectLogic logic = Open(HullStyle.Industrial, new PaintChoice("nope", false), "ship_03");

        Assert.Equal(HullPaints.DefaultIndex(HullStyle.Industrial), logic.Index);
        Assert.True(logic.SelectionIsEquipped);
    }

    // ---------- 11. ← / → con vuelta dentro de su cuadrícula ----------
    [Fact]
    public void LeftRight_WrapInsideTheirGrid()
    {
        PaintSelectLogic logic = OpenVector(19);
        logic.Right();
        Assert.Equal(0, logic.Index);
        logic.Left();
        Assert.Equal(19, logic.Index);

        logic.Select(20);
        logic.Left();
        Assert.Equal(39, logic.Index);
        logic.Right();
        Assert.Equal(20, logic.Index);
    }

    // ---------- 12. ↑ / ↓ entre filas, cuadrículas y opciones ----------
    [Fact]
    public void UpDown_CrossGridsAndReachTheOptions()
    {
        PaintSelectLogic logic = OpenVector(17);   // fila 4 de lisas, columna 3
        logic.Down();
        Assert.Equal(22, logic.Index);             // fila 1 de marcas, columna 3
        logic.Up();
        Assert.Equal(17, logic.Index);

        logic.Select(37);                          // fila 4 de marcas
        logic.Down();
        Assert.Equal(PaintSelectFocus.Options, logic.Focus);
        Assert.Equal(PaintSelectLogic.ApplyId, logic.Menu.Confirm());
        Assert.Equal(37, logic.Index);

        logic.Down();
        Assert.Equal(PaintSelectLogic.BackId, logic.Menu.Confirm());
        logic.Down();
        Assert.Equal(PaintSelectLogic.BackId, logic.Menu.Confirm());
        logic.Up();
        Assert.Equal(PaintSelectLogic.ApplyId, logic.Menu.Confirm());
        logic.Up();
        Assert.Equal(PaintSelectFocus.Swatches, logic.Focus);
        Assert.Equal(37, logic.Index);
    }

    [Fact]
    public void Up_FromTheFirstRowOfPlain_StaysThere_AndInsideAGridMovesOneRow()
    {
        PaintSelectLogic logic = OpenVector(2);
        logic.Up();
        Assert.Equal(2, logic.Index);
        logic.Down();
        Assert.Equal(7, logic.Index);
    }

    [Fact]
    public void InTheOptions_LeftAndRightDoNothing()
    {
        PaintSelectLogic logic = OpenVector(5);
        logic.FocusOptions(1);
        logic.Left();
        logic.Right();
        Assert.Equal(5, logic.Index);
        Assert.Equal(PaintSelectLogic.BackId, logic.Menu.Confirm());
    }

    // ---------- 13. M ----------
    [Fact]
    public void M_TogglesMarks_OfTheSamePaint()
    {
        PaintSelectLogic logic = OpenVector(4);
        logic.ToggleMarks();
        Assert.Equal(new PaintChoice("pearl_white", true), logic.Selection);
        logic.ToggleMarks();
        Assert.Equal(new PaintChoice("pearl_white", false), logic.Selection);
    }

    // ---------- 14. Enter y ESC ----------
    [Fact]
    public void Enter_InTheSwatches_Applies_AndEscGoesBack()
    {
        PaintSelectLogic logic = OpenVector();
        logic.Right();

        Assert.Equal(PaintSelectAction.Apply, logic.Accept());
        Assert.Equal(new PaintChoice("piano_cyan", false), logic.Selection);
        Assert.Equal(PaintSelectAction.Back, logic.Back());
    }

    [Fact]
    public void Enter_InTheOptions_ChoosesTheSelectedOne_AndAClickOnASwatchReturnsToThem()
    {
        PaintSelectLogic logic = OpenVector();
        logic.FocusOptions(1);
        Assert.Equal(PaintSelectAction.Back, logic.Accept());
        logic.FocusOptions(0);
        Assert.Equal(PaintSelectAction.Apply, logic.Accept());
        Assert.Equal(PaintSelectAction.None, logic.Choose(null));

        logic.Select(30);
        Assert.Equal(PaintSelectFocus.Swatches, logic.Focus);
        Assert.Equal(30, logic.Index);
        logic.Select(40);
        Assert.Equal(30, logic.Index);
    }

    // ---------- 15. SELECT SHIP: START, PAINT y BACK ----------
    [Fact]
    public void ShipSelect_HasStartPaintAndBack_WithStartSelected()
    {
        var logic = new ShipSelectLogic(GameMode.Race, "Circuit 01", ShipCatalog.Entries, "ship_02", ShipCatalog.Default);

        Assert.Equal(new[] { "START", "PAINT", "BACK" }, logic.Menu.Options.Select(o => o.Label));
        Assert.Equal(ShipSelectLogic.StartId, logic.Menu.Confirm());
        Assert.Equal(ShipSelectAction.OpenPaint, logic.Choose(ShipSelectLogic.PaintId));

        logic.Menu.MoveNext();
        logic.Menu.MoveNext();
        logic.ReturnFromPaint();
        Assert.Equal(ShipSelectLogic.PaintId, logic.Menu.Confirm());
    }

    [Fact]
    public void ShipSelect_NotSelectable_PaintAndStartDisabled()
    {
        var logic = new ShipSelectLogic(GameMode.Race, "Circuit 01", ShipSelectLogicTests.CatalogWithComingSoon, "ship_01", ShipCatalog.Default);
        logic.NextShip();

        Assert.False(logic.Menu.Options[0].Enabled);
        Assert.False(logic.Menu.Options[1].Enabled);
        Assert.Equal(ShipSelectAction.None, logic.Choose(ShipSelectLogic.PaintId));
        Assert.Equal(ShipSelectLogic.BackId, logic.Menu.Confirm());
    }

    // ---------- 16. Muestras de VECTOR ----------
    [Fact]
    public void VectorSwatch_WithLivery_HasStripesAndNumber_PlainDoesNot()
    {
        PaintSwatch plain = PaintSwatch.For(HullStyle.Racing, 0);
        PaintSwatch livery = PaintSwatch.For(HullStyle.Racing, 20);

        Assert.Equal(SwatchBackground.Gradient, plain.Background);
        Assert.Equal(0xFF2D2Du, plain.AccentBar);
        Assert.Null(plain.Stripes);
        Assert.Null(plain.Number);

        Assert.Equal(0xFF2D2Du, livery.Stripes);
        Assert.Equal("07", livery.Number);
        Assert.Equal(0xF2F2F2u, livery.NumberColor);
    }

    // ---------- 17. Muestras de HAMMER ----------
    [Fact]
    public void HammerSwatch_TwoTone_HasBodyAndCab_AndMarksAddTheHazardBand()
    {
        PaintSwatch twoTone = PaintSwatch.For(HullStyle.Industrial, 16);   // orange_white
        PaintSwatch marks = PaintSwatch.For(HullStyle.Industrial, 36);

        Assert.Equal(SwatchBackground.Split, twoTone.Background);
        Assert.Equal(IndustrialPaintCatalog.Find("orange_white").Body, twoTone.Color);
        Assert.Equal(IndustrialPaintCatalog.Find("orange_white").CabColor, twoTone.Light);
        Assert.False(twoTone.Hazard);
        Assert.True(marks.Hazard);

        // Un lado de casco y otro de cabina; con marcas, la banda abajo
        Assert.Equal(PaintSwatchDrawing.Rgb(twoTone.Color), PaintSwatchDrawing.Sample(twoTone, 5f, 5f, 66f, 30f));
        Assert.Equal(PaintSwatchDrawing.Rgb(twoTone.Light), PaintSwatchDrawing.Sample(twoTone, 60f, 25f, 66f, 30f));
        Vector3 band = PaintSwatchDrawing.Sample(marks, 33f, 28f, 66f, 30f);
        Assert.True(band == PaintSwatchDrawing.Rgb(PaintSwatch.HazardYellow) || band == PaintSwatchDrawing.Rgb(PaintSwatch.HazardBlack));
    }

    [Fact]
    public void BladeSwatches_CamoHasBlobs_MarksHaveTheBadge()
    {
        PaintSwatch camo = PaintSwatch.For(HullStyle.Military, 0);
        PaintSwatch plain = PaintSwatch.For(HullStyle.Military, 10);
        PaintSwatch marks = PaintSwatch.For(HullStyle.Military, 20);

        Assert.Equal(SwatchBackground.Camo, camo.Background);
        Assert.Equal(SwatchBackground.Gradient, plain.Background);
        Assert.False(camo.Badge);
        Assert.True(marks.Badge);

        // Centro de la mancha clara grande (28 %, 30 %) y del distintivo (a 4 + 5,5 px de arriba y de la derecha)
        Assert.Equal(PaintSwatchDrawing.Rgb(camo.Light), PaintSwatchDrawing.Sample(camo, 0.28f * 66f, 0.30f * 30f, 66f, 30f));
        Assert.Equal(PaintSwatchDrawing.Rgb(PaintSwatch.BadgeFill), PaintSwatchDrawing.Sample(marks, 66f - 9.5f, 9.5f, 66f, 30f));
    }

    [Fact]
    public void Gradient_IsFlatUntilItsStop_AndDarkerAtTheEnd()
    {
        PaintSwatch swatch = PaintSwatch.For(HullStyle.Racing, 7);   // racing_red
        Vector3 color = PaintSwatchDrawing.Rgb(swatch.Color);

        Assert.Equal(color, PaintSwatchDrawing.Sample(swatch, 0f, 0f, 66f, 30f));
        Vector3 corner = PaintSwatchDrawing.Sample(swatch, 66f, 30f, 66f, 30f);
        // En la esquina, (1 − 0,55) / (1,6 − 0,55) del camino hacia negro
        Assert.Equal(color.X * (1f - 0.45f / 1.05f), corner.X, 3);
    }
}

// Clave de un lienzo de calcomanía: la usa el pintor para no repetir los ya pintados al cambiar de pintura
public class DecalCanvasKeyTests
{
    [Fact]
    public void SameContent_SameKey_AndAnyChange_ChangesIt()
    {
        HullLivery livery = HullPaintCatalog.Find("graphite_red").Livery;
        Assert.Equal(HullDecalDrawing.NumberPlate(livery).Key(), HullDecalDrawing.NumberPlate(livery).Key());
        Assert.NotEqual(HullDecalDrawing.NumberPlate(livery).Key(),
            HullDecalDrawing.NumberPlate(HullPaintCatalog.Find("piano_cyan").Livery).Key());

        var a = new DecalCanvas(8, 8, new[] { new DecalPrimitive(DecalShape.Polygon, 0, 0, 0, 0, 0, 0xFFFFFF, Points: new[] { new Vector2(0, 0), new Vector2(1, 2) }) });
        var b = new DecalCanvas(8, 8, new[] { new DecalPrimitive(DecalShape.Polygon, 0, 0, 0, 0, 0, 0xFFFFFF, Points: new[] { new Vector2(0, 0), new Vector2(1, 3) }) });
        Assert.NotEqual(a.Key(), b.Key());
    }
}
