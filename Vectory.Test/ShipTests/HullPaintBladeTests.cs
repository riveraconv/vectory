using System;
using System.Linq;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Pinturas, marcas, anticolisión y daño visible del casco 3D de BLADE (spec docs/cascos-3d-2-blade.md, 9, pruebas 5,
// 6, 8, 9 y 10)
public class HullPaintBladeTests
{
    private static MilitaryPaint Paint(string id) => MilitaryPaintCatalog.Find(id)!;

    // ---------- 5. Catálogo ----------

    [Fact]
    public void Catalog_Has20PaintsWithUniqueIds_10CamoAnd10Plain()
    {
        Assert.Equal(20, MilitaryPaintCatalog.Paints.Count);
        Assert.Equal(20, MilitaryPaintCatalog.Paints.Select(p => p.Id).Distinct().Count());
        Assert.Equal(10, MilitaryPaintCatalog.Paints.Count(p => p.IsCamo));
        Assert.True(MilitaryPaintCatalog.Paints.Take(10).All(p => p.IsCamo));
        Assert.True(MilitaryPaintCatalog.Paints.Skip(10).All(p => !p.IsCamo));
    }

    [Fact]
    public void Catalog_OnlyOpsBlackAndNavyGreyAreSatin()
    {
        Assert.Equal(new[] { "ops_black", "navy_grey" }, MilitaryPaintCatalog.Paints.Where(p => p.Satin).Select(p => p.Id));
        Assert.True(MilitaryPaintCatalog.Paints.Where(p => p.IsCamo).All(p => !p.Satin));
    }

    [Fact]
    public void Combinations_The20WithoutMarkingsThenThe20With_AndWrapBothWays()
    {
        Assert.Equal(40, MilitaryPaintCatalog.Combinations.Count);
        Assert.True(MilitaryPaintCatalog.Combinations.Take(20).All(c => !c.WithMarkings));
        Assert.True(MilitaryPaintCatalog.Combinations.Skip(20).All(c => c.WithMarkings));
        Assert.Equal(MilitaryPaintCatalog.Paints, MilitaryPaintCatalog.Combinations.Skip(20).Select(c => c.Paint));
        Assert.Equal(0, HullPaints.Step(HullStyle.Military, 39, 1));
        Assert.Equal(39, HullPaints.Step(HullStyle.Military, 0, -1));
        Assert.Equal(40, HullPaints.Count(HullStyle.Military));
    }

    [Fact]
    public void Default_IsWoodlandWithMarkings_AndVectorKeepsGraphitePlain()
    {
        Assert.Equal(20, HullPaints.DefaultIndex(HullStyle.Military));
        MilitaryPaintChoice choice = MilitaryPaintCatalog.Default;
        Assert.Equal("woodland", choice.Paint.Id);
        Assert.True(choice.WithMarkings);
        Assert.Equal("BOSQUE · MARCAS", HullPaints.DisplayName(HullStyle.Military, 20));
        Assert.Equal("BOSQUE", HullPaints.DisplayName(HullStyle.Military, 0));
        Assert.Equal(0, HullPaints.DefaultIndex(HullStyle.Racing));
        Assert.Equal(HullPaintCatalog.Default.DisplayName, HullPaints.DisplayName(HullStyle.Racing, 0));
    }

    [Fact]
    public void Woodland_HasThePrototypeTones()
    {
        MilitaryPaint w = Paint("woodland");
        Assert.Equal(new uint[] { 0x566040, 0x767A60, 0x2C3326 }, MilitaryHullPaint.Tones(w));
        // Las lisas llevan los tres tonos iguales
        Assert.Equal(new uint[] { 0x565E3E, 0x565E3E, 0x565E3E }, MilitaryHullPaint.Tones(Paint("olive")));
    }

    // ---------- 6. Aplicación de pintura ----------

    [Theory]
    [InlineData("woodland", 107, 112, 93)]
    [InlineData("sand", 179, 163, 131)]
    public void Bottom_IsBotOf(string id, int r, int g, int b)
    {
        Assert.Equal((uint)((r << 16) | (g << 8) | b), MilitaryHullPaint.Bottom(Paint(id)));
    }

    [Fact]
    public void Armor_IsTheTonesTimes085()
    {
        MilitaryPaint p = Paint("desert");
        uint[] tones = MilitaryHullPaint.Tones(p), armor = MilitaryHullPaint.ArmorTones(p);
        for (int i = 0; i < 3; i++)
        {
            for (int shift = 0; shift <= 16; shift += 8)
            {
                int c = (int)((tones[i] >> shift) & 0xFF);
                Assert.Equal((int)Math.Floor(c * 0.85 + 0.5), (int)((armor[i] >> shift) & 0xFF));
            }
        }
    }

    [Fact]
    public void Markings_ShowDecalsAndHazardTips_WithoutThemNeither()
    {
        MilitaryHullLook with = MilitaryHullLook.For(new MilitaryPaintChoice(Paint("woodland"), true));
        MilitaryHullLook without = MilitaryHullLook.For(new MilitaryPaintChoice(Paint("woodland"), false));
        Assert.True(with.MarkingsVisible);
        Assert.True(with.TipsHazard);
        Assert.False(without.MarkingsVisible);
        Assert.False(without.TipsHazard);
    }

    [Theory]
    [InlineData("woodland", 0xDEDECEu)]
    [InlineData("arctic", 0x141612u)]
    [InlineData("khaki", 0x141612u)]
    [InlineData("night", 0xDEDECEu)]
    public void MarkColor_DarkOnLightPaints_LightOnDarkOnes(string id, uint expected)
    {
        Assert.Equal(expected, MilitaryHullLook.MarkColorFor(Paint(id)));
    }

    [Fact]
    public void MarkColor_IsDarkExactlyOnTheLightPaintsOfTheSpec()
    {
        string[] dark = { "desert", "arctic", "tundra", "sand", "navy_grey", "winter_white", "khaki" };
        Assert.Equal(dark.OrderBy(s => s),
            MilitaryPaintCatalog.Paints.Where(p => MilitaryHullLook.MarkColorFor(p) == MilitaryHullLook.MarkDark).Select(p => p.Id).OrderBy(s => s));
    }

    [Fact]
    public void Finish_MatteAndSatin_WithTheArmorPlus005Metal()
    {
        MilitaryHullLook matte = MilitaryHullLook.For(new MilitaryPaintChoice(Paint("woodland"), false));
        MilitaryHullLook satin = MilitaryHullLook.For(new MilitaryPaintChoice(Paint("ops_black"), false));
        Assert.Equal(MilitaryFinish.Matte, matte.Finish);
        Assert.Equal(0.10f, matte.Finish.Metal);
        Assert.Equal(0.15f, matte.ArmorMetal, 5);
        Assert.Equal(1f, matte.Finish.RoughnessFactor);
        Assert.Equal(0f, matte.Finish.Coat);
        Assert.Equal(MilitaryFinish.Satin, satin.Finish);
        Assert.Equal(0.25f, satin.ArmorMetal, 5);
        Assert.Equal(0.75f, satin.Finish.RoughnessFactor);
        Assert.Equal(0.25f, satin.Finish.Coat);
        Assert.Equal(0.35f, satin.Finish.CoatRoughness);
    }

    [Fact]
    public void Floor_IsTheBaseTone()
    {
        Assert.Equal(Paint("urban").Base, MilitaryHullLook.For(new MilitaryPaintChoice(Paint("urban"), true)).Floor);
    }

    // ---------- 8. Calcomanías ----------

    [Fact]
    public void Number_IsB02_InTheMarkColor_Centered_InBlackOpsOne()
    {
        DecalCanvas canvas = MilitaryDecalDrawing.NumberTex(MilitaryHullLook.MarkLight);
        Assert.Equal((1024, 320), (canvas.Width, canvas.Height));
        DecalPrimitive text = Assert.Single(canvas.Primitives);
        Assert.Equal(DecalShape.Text, text.Shape);
        Assert.Equal("B-02", text.Text);
        Assert.Equal(MilitaryHullLook.MarkLight, text.Color);
        Assert.Equal(512f, text.X);
        Assert.Equal(168f, text.Y);
        Assert.Equal(230f, text.FontSize);
        Assert.Equal(DecalBaseline.Middle, text.Baseline);
        Assert.Equal(DecalFont.BlackOps, text.Font);
        Assert.False(text.Italic);
    }

    [Fact]
    public void Insignia_IsCenteredInTheCanvas()
    {
        DecalCanvas canvas = MilitaryDecalDrawing.InsigniaTex(MilitaryHullLook.MarkDark);
        Assert.Equal((512, 512), (canvas.Width, canvas.Height));
        Assert.All(canvas.Primitives, p => Assert.Equal(MilitaryHullLook.MarkDark, p.Color));
        DecalPrimitive ring = canvas.Primitives.Single(p => p.Shape == DecalShape.Ring);
        Assert.Equal((256f, 256f, 228f, 26f), (ring.X, ring.Y, ring.Radius, ring.W));
        DecalPrimitive leaf = canvas.Primitives.Single(p => p.Shape == DecalShape.Polygon);
        Assert.Equal(256f, (leaf.Points.Min(q => q.X) + leaf.Points.Max(q => q.X)) / 2f);
        DecalPrimitive bar = canvas.Primitives.Single(p => p.Shape == DecalShape.Rect);
        Assert.Equal(256f, bar.X + bar.W / 2f);
    }

    [Fact]
    public void Decals_EachPieceGetsItsDrawing_BothSidesShareIt()
    {
        var decals = MilitaryDecalDrawing.For(MilitaryHullLook.For(MilitaryPaintCatalog.Default));
        Assert.Equal(MilitaryHullPieces.Decals.OrderBy(n => n), decals.Keys.OrderBy(n => n));
        Assert.Same(decals["Decal_Number_L"], decals["Decal_Number_R"]);
        Assert.Same(decals["Decal_Insignia_L"], decals["Decal_Insignia_R"]);
    }

    // ---------- 9. Anticolisión ----------

    [Theory]
    [InlineData(0f, 9f, 0f)]
    [InlineData(0.059f, 9f, 0f)]
    [InlineData(0.06f, 0f, 0f)]
    [InlineData(0.6f, 0f, 9f)]
    [InlineData(0.65f, 0f, 9f)]
    [InlineData(0.7f, 0f, 0f)]
    [InlineData(1.2f, 9f, 0f)]
    [InlineData(2.43f, 9f, 0f)]
    public void Strobes_FlashFor006sEvery12s_TheBottomOne06sLater(float time, float top, float bottom)
    {
        var settings = new StrobeSettings();
        Assert.Equal(top, StrobeLights.Intensity(time, 0, settings));
        Assert.Equal(bottom, StrobeLights.Intensity(time, 1, settings));
    }

    [Fact]
    public void Strobes_AreNotAmongTheFlickeringLights()
    {
        Assert.Equal(MilitaryHullPieces.Strobes, HullContract.Military.Strobes);
        Assert.Empty(HullContract.Military.FlickerLights.Intersect(MilitaryHullPieces.Strobes));
        Assert.Empty(HullContract.Military.FlickerLights.Intersect(MilitaryHullPieces.NozzleLights));
        Assert.Empty(HullContract.Racing.Strobes);
    }

    // ---------- 10. Daño visible ----------

    [Fact]
    public void Damage_FlickerOnlyPicksTheNineLights_SmokeAndScorchAsVector()
    {
        Assert.Equal(9, HullContract.Military.FlickerLights.Count);
        Assert.Equal(MilitaryHullPieces.Accents.Concat(MilitaryHullPieces.NavLights).Concat(MilitaryHullPieces.TailLights),
            HullContract.Military.FlickerLights);
        Assert.Equal(new[] { 0f, 0f, 7f, 20f }, Enumerable.Range(0, 4).Select(level => HullDamage.SmokePerSecond(level, false)));
        Assert.All(Enumerable.Range(0, 4), level => Assert.Equal(0f, HullDamage.SmokePerSecond(level, true)));
        Assert.All(Enumerable.Range(0, 4), level => Assert.Equal(0, HullDamage.ScorchMask(level, true)));
    }

    [Fact]
    public void Damage_ScorchReachesPaintArmorBottomTipsMetalAndTitanium_NotLightsGlassOrDecals()
    {
        var scorch = Hull3DBladeTests.Manifest.ScorchMaterials;
        foreach (string m in new[] { "M_Paint", "M_PaintArmor", "M_PaintBottom", "M_AntiSlip", "M_Hazard", "M_Gunmetal", "M_Titanium" })
            Assert.Contains(m, scorch);
        Assert.DoesNotContain("M_CanopyArmor", scorch);
        Assert.DoesNotContain("M_SensorGlass", scorch);
        Assert.DoesNotContain(scorch, m => m.StartsWith("M_Decal") || m.StartsWith("M_Emissive"));
    }

    [Fact]
    public void Tips_SwitchBetweenTheRepeatsOfTheManifest()
    {
        Assert.Equal(0.35f, Hull3DBladeTests.Manifest.RepeatOf("M_Paint"));
        Assert.Equal(3f, Hull3DBladeTests.Manifest.RepeatOf("M_Hazard"));
        Assert.Equal(1f, Hull3DBladeTests.Manifest.RepeatOf("M_Unknown"));
    }
}
