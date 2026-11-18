using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Pinturas, marcas, balizas y daño visible del casco 3D de HAMMER (spec docs/cascos-3d-3-hammer.md, 9, pruebas 5, 6, 8,
// 9 y 10; la 7, el color reconstruido desde las máscaras, está en Hull3DHammerTests)
public class HullPaintHammerTests
{
    private static IndustrialPaint Paint(string id) => IndustrialPaintCatalog.Find(id)!;

    // ---------- 5. Catálogo ----------

    [Fact]
    public void Catalog_Has20PaintsWithUniqueIds_15PlainAnd5TwoTone()
    {
        Assert.Equal(20, IndustrialPaintCatalog.Paints.Count);
        Assert.Equal(20, IndustrialPaintCatalog.Paints.Select(p => p.Id).Distinct().Count());
        Assert.True(IndustrialPaintCatalog.Paints.Take(15).All(p => !p.IsTwoTone));
        Assert.True(IndustrialPaintCatalog.Paints.Skip(15).All(p => p.IsTwoTone));
    }

    [Theory]
    [InlineData("site_yellow", 170, 132, 40)]
    [InlineData("bright_yellow", 196, 156, 30)]
    [InlineData("crane_red", 150, 36, 30)]
    [InlineData("fleet_white", 206, 204, 196)]
    [InlineData("coal_black", 36, 36, 38)]
    [InlineData("petrol_blue", 30, 78, 86)]
    public void Catalog_PlainColorsAreTheOnesOfThePrototype(string id, int r, int g, int b)
    {
        uint rgb = (uint)((r << 16) | (g << 8) | b);
        Assert.Equal(rgb, Paint(id).Body);
        Assert.Equal(rgb, Paint(id).CabColor);
    }

    [Fact]
    public void Catalog_40CombinationsInOrder_WithWrapBothWays()
    {
        IReadOnlyList<IndustrialPaintChoice> all = IndustrialPaintCatalog.Combinations;
        Assert.Equal(40, all.Count);
        Assert.True(all.Take(20).All(c => !c.WithMarkings));
        Assert.True(all.Skip(20).All(c => c.WithMarkings));
        Assert.Equal(IndustrialPaintCatalog.Paints, all.Take(20).Select(c => c.Paint));
        Assert.Equal(IndustrialPaintCatalog.Paints, all.Skip(20).Select(c => c.Paint));

        Assert.Equal(40, HullPaints.Count(HullStyle.Industrial));
        Assert.Equal(0, HullPaints.Step(HullStyle.Industrial, 39, 1));
        Assert.Equal(39, HullPaints.Step(HullStyle.Industrial, 0, -1));
        Assert.Equal("ORANGE · WHITE CAB", HullPaints.DisplayName(HullStyle.Industrial, 16));
        Assert.Equal("SITE YELLOW · MARKINGS", HullPaints.DisplayName(HullStyle.Industrial, 20));
    }

    [Fact]
    public void Default_IsSiteYellowWithMarkings_The21st()
    {
        Assert.Equal(20, IndustrialPaintCatalog.DefaultIndex);
        Assert.Equal(20, HullPaints.DefaultIndex(HullStyle.Industrial));
        Assert.Equal("site_yellow", IndustrialPaintCatalog.Default.Paint.Id);
        Assert.True(IndustrialPaintCatalog.Default.WithMarkings);
        Assert.Equal(IndustrialHullPaint.SiteYellow, IndustrialPaintCatalog.Default.Paint.Body);
    }

    [Fact]
    public void OtherStyles_KeepTheirCatalogs()
    {
        Assert.Equal(MilitaryPaintCatalog.DefaultIndex, HullPaints.DefaultIndex(HullStyle.Military));
        Assert.Equal(HullPaintCatalog.Combinations.Count, HullPaints.Count(HullStyle.Racing));
    }

    // ---------- 6. Aplicación de pintura ----------

    [Fact]
    public void TwoTone_CabIsItsOwnColor_PlainCabIsTheBody()
    {
        IndustrialHullLook orangeWhite = IndustrialHullLook.For(new IndustrialPaintChoice(Paint("orange_white"), false));
        Assert.Equal(0xC4621Eu, orangeWhite.Body);   // (196, 98, 30)
        Assert.Equal(0xCECCC4u, orangeWhite.Cab);    // (206, 204, 196)

        IndustrialHullLook orange = IndustrialHullLook.For(new IndustrialPaintChoice(Paint("orange"), true));
        Assert.Equal(orange.Body, orange.Cab);
    }

    [Fact]
    public void Markings_ShowDecalsAndHazards_AndTheHazardLeadingEdge()
    {
        IndustrialHullLook with = IndustrialHullLook.For(new IndustrialPaintChoice(Paint("site_yellow"), true));
        IndustrialHullLook without = IndustrialHullLook.For(new IndustrialPaintChoice(Paint("site_yellow"), false));
        Assert.True(with.MarkingsVisible);
        Assert.True(with.LeadingEdgeHazard);
        Assert.False(without.MarkingsVisible);
        Assert.False(without.LeadingEdgeHazard);

        // Las piezas que se ocultan sin marcas: todas las calcomanías y las tres franjas
        Assert.Equal(13, IndustrialHullPieces.LiveryPieces.Count());
        Assert.Contains("EngineBay_Hazard", IndustrialHullPieces.LiveryPieces);
        Assert.Contains("Decal_Cab_L", IndustrialHullPieces.LiveryPieces);
        Assert.DoesNotContain("Canard_L_LeadingEdge", IndustrialHullPieces.LiveryPieces);
    }

    [Theory]
    [InlineData("site_yellow", 0xE8E2D0u)]     // 342: claras
    [InlineData("bright_yellow", 0x141412u)]   // 382: oscuras
    [InlineData("fleet_white", 0x141412u)]
    [InlineData("coal_black", 0xE8E2D0u)]
    [InlineData("cream", 0x141412u)]
    [InlineData("white_red", 0x141412u)]       // cuenta el casco, no la cabina
    [InlineData("yellow_black", 0xE8E2D0u)]
    public void MarkColor_DarkOnLightPaints_LightOnDarkOnes(string id, uint expected)
    {
        Assert.Equal(expected, IndustrialHullLook.MarkColorFor(Paint(id)));
        Assert.Equal(expected, IndustrialHullLook.For(new IndustrialPaintChoice(Paint(id), true)).MarkColor);
    }

    [Fact]
    public void MarkColor_DarkOnlyOnTheFourLightPaints()
    {
        string[] dark = IndustrialPaintCatalog.Paints.Where(p => IndustrialHullLook.MarkColorFor(p) == IndustrialHullLook.MarkDark)
            .Select(p => p.Id).ToArray();
        Assert.Equal(new[] { "bright_yellow", "fleet_white", "cream", "white_red" }, dark);
    }

    // ---------- 8. Calcomanías ----------

    private static DecalPrimitive TextOf(DecalCanvas canvas, int index = 0) =>
        canvas.Primitives.Where(p => p.Shape == DecalShape.Text).ElementAt(index);

    [Fact]
    public void Unit_Is03_InBlackOpsOf300px_CenteredAt256_206()
    {
        DecalCanvas c = IndustrialDecalDrawing.UnitTex(0x141412);
        Assert.Equal((512, 384), (c.Width, c.Height));
        DecalPrimitive t = TextOf(c);
        Assert.Equal("03", t.Text);
        Assert.Equal(300f, t.FontSize);
        Assert.Equal(DecalFont.BlackOps, t.Font);
        Assert.Equal((256f, 206f), (t.X, t.Y));
        Assert.Equal(DecalBaseline.Middle, t.Baseline);
        Assert.Equal(DecalAlign.Center, t.Align);
    }

    [Fact]
    public void Logo_IsAHexagonThreePeaksAndM7()
    {
        DecalCanvas c = IndustrialDecalDrawing.LogoTex(0xE8E2D0);
        Assert.Equal((512, 512), (c.Width, c.Height));
        DecalPrimitive hexagon = c.Primitives.Single(p => p.Shape == DecalShape.Outline);
        Assert.Equal(6, hexagon.Points.Length);
        Assert.Equal(24f, hexagon.W);
        Assert.True(hexagon.Radius > 0f);   // esquinas redondeadas
        // vértices a π/6 + i·π/3 con radio 228 desde el centro
        Assert.All(hexagon.Points, v => Assert.Equal(228f, (v - new System.Numerics.Vector2(256, 256)).Length(), 3));
        Assert.Equal(256f + 228f * MathF.Cos(MathF.PI / 6), hexagon.Points[0].X, 3);
        Assert.Equal(5, c.Primitives.Single(p => p.Shape == DecalShape.Polygon).Points.Length);
        DecalPrimitive t = TextOf(c);
        Assert.Equal("M·7", t.Text);
        Assert.Equal(120f, t.FontSize);
        Assert.Equal((256f, 392f), (t.X, t.Y));
    }

    [Theory]
    [InlineData("NO STEP")]
    [InlineData("HOT")]
    public void Warning_TriangleWithAnErasedBang_AndTheTextFromX160(string text)
    {
        DecalCanvas c = IndustrialDecalDrawing.WarnTex(text, 0x141412);
        Assert.Equal((512, 160), (c.Width, c.Height));
        Assert.Equal(new System.Numerics.Vector2[] { new(34, 128), new(84, 32), new(134, 128) },
            c.Primitives.Single(p => p.Shape == DecalShape.Polygon).Points);
        DecalPrimitive bang = TextOf(c, 0);
        Assert.Equal("!", bang.Text);
        Assert.True(bang.Erase);
        Assert.True(bang.Bold);
        Assert.Equal(DecalFont.ShareTech, bang.Font);
        Assert.Equal((84f, 96f, 70f), (bang.X, bang.Y, bang.FontSize));
        DecalPrimitive label = TextOf(c, 1);
        Assert.Equal(text, label.Text);
        Assert.False(label.Erase);
        Assert.Equal(DecalAlign.Left, label.Align);
        Assert.Equal((160f, 78f, 332f), (label.X, label.FontSize, label.MaxWidth));
        Assert.Equal(DecalFont.BlackOps, label.Font);
    }

    [Fact]
    public void Stencil_IsHMR03_InAFrame10pxFromTheEdge()
    {
        DecalCanvas c = IndustrialDecalDrawing.StencilTex(0xE8E2D0);
        Assert.Equal((512, 128), (c.Width, c.Height));
        DecalPrimitive frame = c.Primitives.Single(p => p.Shape == DecalShape.Outline);
        Assert.Equal(6f, frame.W);
        Assert.Equal(new System.Numerics.Vector2(10, 10), frame.Points[0]);
        Assert.Equal(new System.Numerics.Vector2(502, 118), frame.Points[2]);
        DecalPrimitive t = TextOf(c);
        Assert.Equal("HMR-03", t.Text);
        Assert.Equal((256f, 68f, 74f, 462f), (t.X, t.Y, t.FontSize, t.MaxWidth));
    }

    [Theory]
    [InlineData("site_yellow")]
    [InlineData("fleet_white")]
    public void Decals_AllInTheMarkColor_BothSidesShareTheirCanvas(string id)
    {
        IndustrialHullLook look = IndustrialHullLook.For(new IndustrialPaintChoice(Paint(id), true));
        IReadOnlyDictionary<string, DecalCanvas> decals = IndustrialDecalDrawing.For(look);
        Assert.Equal(IndustrialHullPieces.Decals.OrderBy(n => n), decals.Keys.OrderBy(n => n));
        Assert.All(decals.Values.SelectMany(c => c.Primitives), p => Assert.Equal(look.MarkColor, p.Color));
        foreach (string kind in new[] { "Unit", "Logo", "NoStep", "Hot", "Cab" })
            Assert.Same(decals[$"Decal_{kind}_L"], decals[$"Decal_{kind}_R"]);
        Assert.Equal("NO STEP", TextOf(decals["Decal_NoStep_L"], 1).Text);
        Assert.Equal("HOT", TextOf(decals["Decal_Hot_R"], 1).Text);
    }

    [Fact]
    public void Decals_OfVectorAndBlade_KeepTheirAlignmentAndPaint()
    {
        // Lo nuevo de las primitivas tiene valores por defecto que no cambian lo que pintaban
        Assert.All(MilitaryDecalDrawing.NumberTex(0x141612).Primitives, p =>
        {
            Assert.Equal(DecalAlign.Center, p.Align);
            Assert.False(p.Erase);
            Assert.False(p.Bold);
        });
    }

    // ---------- 9. Balizas del techo ----------

    private static readonly IndustrialLampSettings Lamps = IndustrialLampSettings.Default;

    [Fact]
    public void BoostBeacon_PulsesOnlyWithTheBoost()
    {
        for (float t = 0f; t < 1f; t += 0.037f)
        {
            (float boost, _) = IndustrialHullBeacons.Intensities(true, 0, true, t, Lamps);
            Assert.Equal(4f + 1.5f * MathF.Sin(20f * t), boost, 4);
            (float off, _) = IndustrialHullBeacons.Intensities(false, 3, true, t, Lamps);
            Assert.Equal(0.05f, off);
        }
    }

    [Fact]
    public void DamageBeacon_FixedAt2_BlinkingAt3_OffBelowAndInTimeTrial()
    {
        Assert.Equal(0.05f, IndustrialHullBeacons.Intensities(false, 0, true, 0.1f, Lamps).Damage);
        Assert.Equal(0.05f, IndustrialHullBeacons.Intensities(false, 1, true, 0.1f, Lamps).Damage);
        Assert.Equal(5f, IndustrialHullBeacons.Intensities(false, 2, true, 0.1f, Lamps).Damage);
        Assert.Equal(5f, IndustrialHullBeacons.Intensities(false, 2, true, 0.4f, Lamps).Damage);
        // daño 3: encendida la primera mitad de cada medio segundo (2 Hz)
        Assert.Equal(5f, IndustrialHullBeacons.Intensities(false, 3, true, 0.1f, Lamps).Damage);
        Assert.Equal(0.05f, IndustrialHullBeacons.Intensities(false, 3, true, 0.3f, Lamps).Damage);
        Assert.Equal(5f, IndustrialHullBeacons.Intensities(false, 3, true, 0.6f, Lamps).Damage);
        // TIME TRIAL: siempre apagada
        foreach (int level in new[] { 0, 1, 2, 3 })
            Assert.Equal(0.05f, IndustrialHullBeacons.Intensities(true, level, false, 0.1f, Lamps).Damage);
    }

    [Fact]
    public void Beacons_DoNotFlickerWithDamage()
    {
        Assert.Empty(HullContract.Industrial.FlickerLights.Intersect(IndustrialHullPieces.Beacons));
    }

    // ---------- 10. Daño visible ----------

    [Fact]
    public void Flicker_OnlyPicksAmongThe12Lights()
    {
        var expected = new[]
        {
            "Light_Nose_L", "Light_Nose_R", "Light_Canard_L", "Light_Canard_R", "Light_Tail_L", "Light_Tail_R",
            "Light_TailBar_L", "Light_TailBar_R", "Light_Work_0", "Light_Work_1", "Light_Work_2", "Light_Work_3"
        };
        Assert.Equal(expected, HullContract.Industrial.FlickerLights);
        Assert.Equal(expected, Hull3DHammerTests.Manifest.FlickerLights);
        Assert.Empty(HullContract.Industrial.FlickerLights.Intersect(IndustrialHullPieces.NozzleLights));
    }

    [Fact]
    public void No_scorch_in_time_trial()
    {
        Assert.All(Enumerable.Range(0, 4), level => Assert.Equal(0, HullDamage.ScorchMask(level, true)));
    }

    [Fact]
    public void Scorch_ReachesPaintPlateTreadHazardSteelTitaniumAndCopper_NotLightsGlassOrDecals()
    {
        var scorch = Hull3DHammerTests.Manifest.ScorchMaterials;
        foreach (string m in new[] { "M_Paint", "M_Paint_Canard", "M_Paint_Fin", "M_Paint_Bay", "M_PaintCab", "M_PlateDark",
                     "M_Tread", "M_Hazard", "M_Steel", "M_Titanium", "M_Copper" })
            Assert.Contains(m, scorch);
        Assert.DoesNotContain("M_CabGlass", scorch);
        Assert.Empty(scorch.Intersect(Hull3DHammerTests.Manifest.DecalMaterials));
        Assert.DoesNotContain(scorch, m => m.StartsWith("M_Emissive") || m.StartsWith("M_Beacon"));
    }
}
