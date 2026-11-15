using System;
using System.Linq;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Pinturas, libreas y daño visible del casco 3D de VECTOR (spec docs/cascos-3d-1-vector.md, 9, pruebas 5 a 8)
public class HullPaintTests
{
    private static HullPaintChoice Choice(string id, bool livery) => new(HullPaintCatalog.Find(id)!, livery);

    // ---------- 5. Catálogo ----------

    [Fact]
    public void Catalog_Has20PaintsWithUniqueIdsAndCompleteLiveries()
    {
        Assert.Equal(20, HullPaintCatalog.Paints.Count);
        Assert.Equal(20, HullPaintCatalog.Paints.Select(p => p.Id).Distinct().Count());
        foreach (HullPaint paint in HullPaintCatalog.Paints)
        {
            Assert.False(string.IsNullOrEmpty(paint.Name));
            Assert.NotNull(paint.Finish);
            Assert.NotNull(paint.Livery);
            Assert.False(string.IsNullOrEmpty(paint.Livery.Number));
            Assert.Contains(paint.Finish, HullPaintCatalog.Finishes);
        }
    }

    [Fact]
    public void Catalog_HasTheSevenFinishesOfTheTable()
    {
        Assert.Equal(new[] { "BRILLO", "METALIZADO", "SATINADO", "MATE", "CROMADO", "PERLA", "CAMALEÓN" },
            HullPaintCatalog.Finishes.Select(f => f.Name));
        Assert.Equal(0.42f, HullPaintCatalog.Satin.Roughness);
        Assert.Equal(0.6f, HullPaintCatalog.Satin.Metal);
        Assert.Equal(0f, HullPaintCatalog.Matte.Coat);
        Assert.Equal(0.35f, HullPaintCatalog.Pearl.Iridescence);
        Assert.Equal(1f, HullPaintCatalog.Chameleon.Iridescence);
        Assert.All(HullPaintCatalog.Finishes.Where(f => f != HullPaintCatalog.Pearl && f != HullPaintCatalog.Chameleon),
            f => Assert.Equal(0f, f.Iridescence));
    }

    [Fact]
    public void Combinations_AreThe20PlainAndThenThe20WithLivery()
    {
        Assert.Equal(40, HullPaintCatalog.Combinations.Count);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(new HullPaintChoice(HullPaintCatalog.Paints[i], false), HullPaintCatalog.Combinations[i]);
            Assert.Equal(new HullPaintChoice(HullPaintCatalog.Paints[i], true), HullPaintCatalog.Combinations[20 + i]);
        }
        Assert.Equal(20, HullPaintCatalog.IndexOf(Choice("graphite_red", true)));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(39, 1, 0)]
    [InlineData(0, -1, 39)]
    [InlineData(20, -1, 19)]
    public void Step_GoesForwardAndBack_WithWrap(int index, int delta, int expected)
    {
        Assert.Equal(expected, HullPaintCatalog.Step(index, delta));
    }

    [Fact]
    public void Default_IsGraphitePlain()
    {
        Assert.Equal("graphite_red", HullPaintCatalog.Default.Paint.Id);
        Assert.False(HullPaintCatalog.Default.WithLivery);
        Assert.Equal("GRAFITO", HullPaintCatalog.Default.DisplayName);
        Assert.Equal("GRAFITO #07", Choice("graphite_red", true).DisplayName);
    }

    // ---------- 6. Aplicación de pintura ----------

    [Fact]
    public void Accents_FollowThePaint_TailAndNozzlesStayFixed()
    {
        HullPaintLook look = HullPaintLook.For(Choice("piano_cyan", false));
        foreach (string light in HullPieces.AccentEdges.Concat(HullPieces.AccentLights))
            Assert.Equal(0x36E7FFu, look.LightColor(light));
        Assert.All(HullPieces.TailLights, l => Assert.Equal(0xFF1A1Au, look.LightColor(l)));
        Assert.Equal(0x245BFFu, look.LightColor("Nozzle_Glow_L"));
        Assert.Equal(0x7FA6FFu, look.LightColor("Nozzle_Core_R"));
    }

    [Fact]
    public void WithLivery_LiveryIsVisible_AndEndplatesTakeTheStripeColor()
    {
        HullPaintLook look = HullPaintLook.For(Choice("pearl_white", true));
        Assert.True(look.LiveryVisible);
        Assert.Equal(0x1A42B0u, look.EndplateColor);
        Assert.Equal(HullPaintCatalog.Gloss, look.EndplateFinish);
        Assert.Equal(0x1A42B0u, look.StripeColor);
        Assert.Equal(0xFFFFFFu, look.PinColor);
        Assert.Equal(0xE4E6EAu, look.PaintColor);
        Assert.Equal(HullPaintCatalog.Pearl, look.PaintFinish);
    }

    [Fact]
    public void WithoutLivery_NoLivery_AndEndplatesKeepThePaint()
    {
        HullPaintLook look = HullPaintLook.For(Choice("pearl_white", false));
        Assert.False(look.LiveryVisible);
        Assert.Equal(0xE4E6EAu, look.EndplateColor);
        Assert.Equal(HullPaintCatalog.Pearl, look.EndplateFinish);
    }

    [Theory]
    [InlineData("pearl_white", 0x111111u)]
    [InlineData("graphite_red", 0xF2F2F2u)]
    [InlineData("taxi", 0x111111u)]          // 232 + 180 + 20 = 432
    [InlineData("british_green", 0xF2F2F2u)]
    public void TextColor_DependsOnHowLightThePaintIs(string id, uint expected)
    {
        Assert.Equal(expected, HullPaintLook.For(Choice(id, true)).TextColor);
    }

    // ---------- 7. Calcomanías ----------

    [Fact]
    public void GraphiteLivery_NumberIs07_OnLightBackground()
    {
        HullPaintLook look = HullPaintLook.For(Choice("graphite_red", true));
        foreach (string piece in new[] { "Decal_Number_Pod_L", "Decal_Number_Nose" })
        {
            DecalCanvas canvas = HullDecalDrawing.For(look)[piece];
            DecalPrimitive text = canvas.Primitives.Single(p => p.Shape == DecalShape.Text);
            Assert.Equal("07", text.Text);
            Assert.Equal(0x111111u, text.Color);
            Assert.Contains(canvas.Primitives, p => p.Shape != DecalShape.Text && p.Color == 0xF2F2F2u);
            Assert.Contains(canvas.Primitives, p => p.Shape != DecalShape.Text && p.Color == 0xFF2D2Du);
        }
    }

    [Fact]
    public void Decals_HaveTheCanvasSizesOfThePrototype()
    {
        var decals = HullDecalDrawing.For(HullPaintLook.For(Choice("graphite_red", true)));
        Assert.Equal(HullPieces.Decals.OrderBy(n => n), decals.Keys.OrderBy(n => n));
        Assert.Equal((640, 384), (decals["Decal_Number_Pod_R"].Width, decals["Decal_Number_Pod_R"].Height));
        Assert.Equal((512, 512), (decals["Decal_Number_Nose"].Width, decals["Decal_Number_Nose"].Height));
        Assert.Equal((1024, 256), (decals["Decal_Sponsor_L"].Width, decals["Decal_Sponsor_L"].Height));
        Assert.Equal((512, 128), (decals["Decal_Checker_L"].Width, decals["Decal_Checker_L"].Height));
        Assert.Equal(16 * 4, decals["Decal_Checker_R"].Primitives.Count);
    }

    [Fact]
    public void Stickers_AreCentred_AndTheSameOnBothSides()
    {
        var decals = HullDecalDrawing.For(HullPaintLook.For(Choice("graphite_red", true)));
        Assert.Same(decals["Decal_Sponsor_L"], decals["Decal_Sponsor_R"]);
        foreach (string piece in new[] { "Decal_Sponsor_L", "Decal_Sponsor_Pod_L" })
        {
            DecalCanvas canvas = decals[piece];
            Assert.All(canvas.Primitives.Where(p => p.Shape == DecalShape.Text), p => Assert.Equal(canvas.Width / 2f, p.X));
        }
        Assert.Equal(new[] { "NULLDRIVE", "KAIROS GRID · VX-7" },
            decals["Decal_Sponsor_L"].Primitives.Where(p => p.Shape == DecalShape.Text).Select(p => p.Text));
        Assert.Equal(new[] { "VECTORY" },
            decals["Decal_Sponsor_Pod_R"].Primitives.Where(p => p.Shape == DecalShape.Text).Select(p => p.Text));
    }

    [Fact]
    public void Roundel_UsesASmallerNumber_WithTwoDigits()
    {
        DecalPrimitive two = HullDecalDrawing.NumberRoundel(HullPaintCatalog.Find("graphite_red")!.Livery).Primitives.Single(p => p.Shape == DecalShape.Text);
        DecalPrimitive one = HullDecalDrawing.NumberRoundel(HullPaintCatalog.Find("racing_red")!.Livery).Primitives.Single(p => p.Shape == DecalShape.Text);
        Assert.Equal(200f, two.FontSize);
        Assert.Equal(250f, one.FontSize);
    }

    // ---------- 8. Daño visible ----------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void ScorchMask_IsTheOneOfTheLevel(int level, int mask)
    {
        Assert.Equal(mask, HullDamage.ScorchMask(level, false));
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(1, 0f)]
    [InlineData(2, 7f)]
    [InlineData(3, 20f)]
    public void Smoke_PerSecondByLevel(int level, float rate)
    {
        Assert.Equal(rate, HullDamage.SmokePerSecond(level, false));
    }

    [Fact]
    public void TimeTrial_HasNoDamageAtAll()
    {
        for (int level = 0; level <= 3; level++)
        {
            Assert.Equal(0, HullDamage.ScorchMask(level, true));
            Assert.Equal(0f, HullDamage.SmokePerSecond(level, true));
        }
    }

    [Fact]
    public void Flicker_ChoosesOnlyAmongThe14AccentAndLightPieces()
    {
        string[] lights = HullPieces.FlickerLights.ToArray();
        Assert.Equal(HullDamage.FlickerLightCount, lights.Length);
        Assert.All(lights, n => Assert.True(n.StartsWith("Accent_", StringComparison.Ordinal) || n.StartsWith("Light_", StringComparison.Ordinal)));
        Assert.DoesNotContain(lights, n => n.StartsWith("Nozzle_", StringComparison.Ordinal));

        var flicker = new EdgeFlicker(lights.Length, EdgeFlickerSettings.Default, new Random(7));
        flicker.SetLevel(3);
        Assert.All(flicker.AffectedEdges, e => Assert.InRange(e, 0, lights.Length - 1));
        Assert.Equal((int)MathF.Round(0.35f * 14), flicker.AffectedEdges.Count);
    }

    [Fact]
    public void Smoke_IsDarkerAtLevel3_AndGrowsWithSpeed()
    {
        Assert.Equal(0x4A4A4Au, HullDamage.SmokeColor(2));
        Assert.Equal(0x262626u, HullDamage.SmokeColor(3));
        Assert.Equal(1.95f, HullDamage.SmokeFinalSize(0f));
        Assert.True(HullDamage.SmokeFinalSize(100f) > HullDamage.SmokeFinalSize(0f));
    }

    // ---------- Brillo de levitación ----------

    [Fact]
    public void HoverGlow_PulsesAround032_AndFadesOutInTheAir()
    {
        Assert.Equal(0.32f, HoverGlow.Strength(0f, 1f), 5);
        Assert.Equal(0.38f, HoverGlow.Strength(MathF.PI / 6f, 1f), 4);
        Assert.Equal(0f, HoverGlow.Strength(1f, 0f));
        Assert.Equal(1f, HoverGlow.AirFade(0f));
        Assert.Equal(0.5f, HoverGlow.AirFade(1.5f), 5);
        Assert.Equal(0f, HoverGlow.AirFade(3f));
        Assert.Equal(1f, HoverGlow.Falloff(0f, 0f));
        Assert.Equal(0f, HoverGlow.Falloff(1f, 0f));
    }
}
