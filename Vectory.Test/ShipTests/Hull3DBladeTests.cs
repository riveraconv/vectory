using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Casco 3D de BLADE, variante ASALTO (spec docs/cascos-3d-2-blade.md, 9, pruebas 1 a 4 y 7), con el manifiesto real
// del script de Blender
public class Hull3DBladeTests
{
    internal static readonly HullManifest Manifest = Load();

    private static HullManifest Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        string path = Path.Combine(dir!.FullName, "assets", "ships", "hull_blade.manifest.json");
        return HullManifest.Parse(File.ReadAllText(path))!;
    }

    private static Vector3 Min(CockpitManifestBounds b) => new(b.Min[0], b.Min[1], b.Min[2]);
    private static Vector3 Max(CockpitManifestBounds b) => new(b.Max[0], b.Max[1], b.Max[2]);

    // ---------- 1. Manifiesto ----------

    [Fact]
    public void Manifest_HasEveryPieceAndEmptyOfTheContract_OnceEach()
    {
        HullContract contract = HullContract.Military;
        Assert.Equal(MilitaryHullPieces.Root, Manifest.Root);
        Assert.Empty(HullManifest.Missing(Manifest.Pieces.Select(p => p.Name), contract));
        Assert.Equal(contract.Pieces.Count, Manifest.Pieces.Count);
        Assert.Equal(Manifest.Pieces.Count, Manifest.Pieces.Select(p => p.Name).Distinct().Count());
        Assert.Equal(HullContract.Empties.OrderBy(n => n), Manifest.Empties.Select(e => e.Name).OrderBy(n => n));
    }

    [Fact]
    public void Contract_HasTheCountsOfTheSpec()
    {
        Assert.Equal(6, MilitaryHullPieces.Painted.Length);
        Assert.Equal(2, MilitaryHullPieces.Tips.Length);
        Assert.Equal(8, MilitaryHullPieces.Armor.Length);
        Assert.Single(MilitaryHullPieces.Bottom);
        Assert.Equal(4, MilitaryHullPieces.Accents.Length);
        Assert.Equal(4, MilitaryHullPieces.NavLights.Length);
        Assert.Single(MilitaryHullPieces.TailLights);
        Assert.Equal(2, MilitaryHullPieces.Strobes.Length);
        Assert.Equal(4, MilitaryHullPieces.NozzleLights.Length);
        Assert.Equal(4, MilitaryHullPieces.Decals.Length);
        Assert.Equal(9, MilitaryHullPieces.FlickerLights.Count);
        Assert.Single(MilitaryHullPieces.Floor);
        Assert.Equal(40, MilitaryHullPieces.Contract.Count);
    }

    [Fact]
    public void Manifest_GroupsMatchTheContract()
    {
        Assert.Equal(MilitaryHullPieces.Painted, Manifest.Painted);
        Assert.Equal(MilitaryHullPieces.Tips, Manifest.Tips);
        Assert.Equal(MilitaryHullPieces.Armor, Manifest.Armor);
        Assert.Equal(MilitaryHullPieces.Bottom, Manifest.Bottom);
        Assert.Equal(MilitaryHullPieces.Accents, Manifest.Accents);
        Assert.Equal(MilitaryHullPieces.NavLights, Manifest.NavLights);
        Assert.Equal(MilitaryHullPieces.TailLights, Manifest.TailLights);
        Assert.Equal(MilitaryHullPieces.Strobes, Manifest.Strobes);
        Assert.Equal(MilitaryHullPieces.NozzleLights, Manifest.NozzleLights);
        Assert.Equal(MilitaryHullPieces.Decals, Manifest.Decals);
        Assert.Equal(MilitaryHullPieces.FlickerLights, Manifest.FlickerLights);
        Assert.Equal(MilitaryHullPieces.Probe, Manifest.Probe);
        Assert.Equal(MilitaryHullPieces.Floor, Manifest.Floor);
        // Las piezas que solo se ven con marcas: las calcomanías
        Assert.Equal(MilitaryHullPieces.Decals.OrderBy(n => n), Manifest.Livery.OrderBy(n => n));
    }

    [Theory]
    [InlineData("Exhaust_L", -0.60f, 0.08f, 3.30f)]
    [InlineData("Exhaust_R", 0.60f, 0.08f, 3.30f)]
    [InlineData("Trail_L", -1.60f, 0f, 3.60f)]
    [InlineData("Trail_R", 1.60f, 0f, 3.60f)]
    [InlineData("Smoke", 0.60f, 0.35f, 2.40f)]
    public void Empties_AreWhereTheContractSays(string name, float x, float y, float z)
    {
        Vector3 p = Manifest.Empty(name)!.Point;
        Assert.InRange(Vector3.Distance(p, new Vector3(x, y, z)), 0f, 0.001f);
    }

    [Fact]
    public void Lights_StartWithThePrototypeColorsAndIntensities()
    {
        Assert.Equal("#FF2A1E", Manifest.Piece("Light_Nav_L")!.Emission);
        Assert.Equal("#3DFF6A", Manifest.Piece("Light_Tip_R")!.Emission);
        Assert.Equal(2.4f, Manifest.Piece("Light_Nav_R")!.Intensity);
        Assert.Equal("#F4F7FF", Manifest.Piece("Light_Tail")!.Emission);
        Assert.Equal(2.88f, Manifest.Piece("Light_Tail")!.Intensity!.Value, 4);
        Assert.Equal("#B6FF6A", Manifest.Piece("Accent_FormProng_L")!.Emission);
        Assert.Equal(0.8f, Manifest.Piece("Accent_FormNose_R")!.Intensity);
        Assert.Equal("#FF2A1E", Manifest.Piece("Light_Strobe_Top")!.Emission);
        Assert.Equal(0f, Manifest.Piece("Light_Strobe_Bottom")!.Intensity);
        Assert.Equal("#245BFF", Manifest.Piece("Nozzle_Glow_L")!.Emission);
        Assert.Equal(3.5f, Manifest.Piece("Nozzle_Core_R")!.Intensity);
    }

    // ---------- 2. Medidas ----------

    [Fact]
    public void Bounds_AreTheOnesOfTheSpec()
    {
        const float Tolerance = 0.01f;
        Vector3 min = Min(Manifest.BoundsWithoutProbe), max = Max(Manifest.BoundsWithoutProbe);
        Vector3 minProbe = Min(Manifest.Bounds), maxProbe = Max(Manifest.Bounds);
        Assert.InRange(MathF.Abs(max.Z - min.Z - 8.81f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(maxProbe.Z - minProbe.Z - 9.36f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(Manifest.HalfWidth - 1.61f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Y - min.Y - 0.75f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(min.Z + 5.20f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Z - 3.61f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(min.Y + 0.234f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Y - 0.519f), 0f, Tolerance);
        Assert.Equal(Manifest.HalfWidth, MathF.Max(-min.X, max.X), 4);
    }

    // ---------- 3. Perfil visual ----------

    [Fact]
    public void Blade_HasTheMilitary3DHull_VectorTheRacingOne_HammerTheWireframe()
    {
        ShipHull3DProfile hull = BladeShip.Definition.Visual.Hull3D!;
        Assert.EndsWith("hull_blade.glb", hull.ModelPath);
        Assert.EndsWith("hull_blade.manifest.json", hull.ManifestPath);
        Assert.EndsWith(Manifest.Environment, hull.EnvironmentPath);
        Assert.Equal(HullStyle.Military, hull.Style);
        Assert.Same(HullContract.Military, hull.Contract);
        Assert.Equal(new HoverGlowShape(3.0f, 9.0f, -0.7f), hull.Glow);

        Assert.Equal(HullStyle.Racing, VectorShip.Definition.Visual.Hull3D!.Style);
        Assert.Equal(new HoverGlowShape(HoverGlow.Width, HoverGlow.Length, HoverGlow.CenterZ), VectorShip.Definition.Visual.Hull3D!.Glow);
        Assert.Null(HammerShip.Definition.Visual.Hull3D);
    }

    [Fact]
    public void Blade_KeepsItsWireframe_ForTheGhostAndTheMenu()
    {
        float wireHalfWidth = BladeShip.Definition.Visual.BuildHull().Max(l => MathF.Max(MathF.Abs(l.A.X), MathF.Abs(l.B.X)));
        Assert.Equal(1.6f, wireHalfWidth, 5);
    }

    [Fact]
    public void Blade_ExhaustsAndTrails_AreTheEmptiesOfItsManifest()
    {
        // Las bocas y las estelas se leen del modelo: deben caer donde el wireframe tenía las toberas y las puntas
        Assert.Equal(0.6f, MathF.Abs(Manifest.Empty("Exhaust_L")!.Point.X), 3);
        Assert.Equal(1.6f, Manifest.Empty("Trail_R")!.Point.X, 3);
        Assert.Equal(3.6f, Manifest.Empty("Trail_R")!.Point.Z, 3);
    }

    // ---------- 4. Colisión ----------

    [Fact]
    public void Blade_CollisionHalfWidth_IsTheHalfWidthOfThe3DHull()
    {
        Assert.Equal(1.61f, BladeShip.Definition.Visual.CollisionHalfWidth);
        Assert.InRange(MathF.Abs(Manifest.HalfWidth - BladeShip.Definition.Visual.CollisionHalfWidth), 0f, 0.01f);
    }

    // ---------- Pintura de por defecto: BOSQUE (spec 6.2; el resto del catálogo, en la etapa 3) ----------

    [Fact]
    public void Woodland_Bottom_ArmorTones_AreTheOnesOfThePrototype()
    {
        Assert.Equal(0x6B705Du, MilitaryHullPaint.Bottom(MilitaryHullPaint.Woodland));   // (107, 112, 93)
        // (86, 96, 64) (118, 122, 96) (44, 51, 38) × 0,85
        Assert.Equal(new uint[] { 0x495236, 0x646852, 0x252B20 }, MilitaryHullPaint.ArmorTones(MilitaryHullPaint.Woodland));
    }

    [Fact]
    public void Manifest_PointsToBothCamoMasks()
    {
        Assert.Equal("hull_blade_camo_mask.png", Manifest.CamoMasks.Single(m => m.Material == "M_Paint").Mask);
        Assert.Equal("hull_blade_armor_mask.png", Manifest.CamoMasks.Single(m => m.Material == "M_PaintArmor").Mask);
    }

    // ---------- 7. Camuflaje: el color reconstruido desde la máscara es el del prototipo (±1) ----------

    // Píxeles de hull_blade_camo_mask.png (R tono, G desconchón, B grano) y el color de camoSet(55, BOSQUE) del
    // prototipo en ese mismo píxel, sacado ejecutando su código con Node: base, claro, oscuro y desconchones
    [Theory]
    [InlineData(0, 0, 102, 84, 94, 63)]
    [InlineData(0, 0, 163, 88, 99, 66)]
    [InlineData(0, 0, 128, 86, 96, 64)]
    [InlineData(128, 0, 50, 111, 115, 90)]
    [InlineData(128, 0, 90, 114, 118, 93)]
    [InlineData(128, 0, 71, 113, 117, 92)]
    [InlineData(255, 0, 131, 44, 51, 38)]
    [InlineData(255, 0, 135, 44, 51, 38)]
    [InlineData(255, 0, 65, 42, 48, 36)]
    [InlineData(128, 255, 73, 113, 115, 111)]
    [InlineData(255, 255, 131, 118, 120, 116)]
    [InlineData(255, 255, 116, 117, 119, 115)]
    public void Camo_RebuiltFromTheMask_IsThePrototypeColor(byte tone, byte chip, byte grain, int r, int g, int b)
    {
        byte[] color = MilitaryHullPaint.BakeCamo(new[] { tone, chip, grain }, MilitaryHullPaint.Tones(MilitaryHullPaint.Woodland));
        Assert.InRange(Math.Abs(color[0] - r), 0, 1);
        Assert.InRange(Math.Abs(color[1] - g), 0, 1);
        Assert.InRange(Math.Abs(color[2] - b), 0, 1);
    }
}
