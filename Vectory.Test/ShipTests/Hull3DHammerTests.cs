using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Casco 3D de HAMMER, variante MINERO (spec docs/cascos-3d-3-hammer.md, 9, pruebas 1 a 4 y la pintura de por defecto),
// con el manifiesto real del script de Blender
public class Hull3DHammerTests
{
    internal static readonly HullManifest Manifest = Load();

    private static HullManifest Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        string path = Path.Combine(dir!.FullName, "assets", "ships", "hull_hammer.manifest.json");
        return HullManifest.Parse(File.ReadAllText(path))!;
    }

    private static Vector3 Min(CockpitManifestBounds b) => new(b.Min[0], b.Min[1], b.Min[2]);
    private static Vector3 Max(CockpitManifestBounds b) => new(b.Max[0], b.Max[1], b.Max[2]);

    // ---------- 1. Manifiesto ----------

    [Fact]
    public void Manifest_HasEveryPieceAndEmptyOfTheContract_OnceEach()
    {
        HullContract contract = HullContract.Industrial;
        Assert.Equal(IndustrialHullPieces.Root, Manifest.Root);
        Assert.Empty(HullManifest.Missing(Manifest.Pieces.Select(p => p.Name), contract));
        Assert.Equal(contract.Pieces.Count, Manifest.Pieces.Count);
        Assert.Equal(Manifest.Pieces.Count, Manifest.Pieces.Select(p => p.Name).Distinct().Count());
        Assert.Equal(HullContract.Empties.OrderBy(n => n), Manifest.Empties.Select(e => e.Name).OrderBy(n => n));
    }

    [Fact]
    public void Contract_HasTheCountsOfTheSpec()
    {
        Assert.Equal(8, IndustrialHullPieces.PaintedBody.Length);
        Assert.Single(IndustrialHullPieces.PaintedCab);
        Assert.Equal(2, IndustrialHullPieces.LeadingEdges.Length);
        Assert.Equal(3, IndustrialHullPieces.LiveryHazards.Length);
        Assert.Equal(4, IndustrialHullPieces.AmberLights.Length);
        Assert.Equal(4, IndustrialHullPieces.TailLights.Length);
        Assert.Equal(4, IndustrialHullPieces.WorkLights.Length);
        Assert.Equal(2, IndustrialHullPieces.Beacons.Length);
        Assert.Equal(4, IndustrialHullPieces.NozzleLights.Length);
        Assert.Equal(3, IndustrialHullPieces.Glass.Length);
        Assert.Equal(10, IndustrialHullPieces.Decals.Length);
        Assert.Equal(12, IndustrialHullPieces.FlickerLights.Count);
        Assert.Equal(45, IndustrialHullPieces.Contract.Count);
        Assert.Empty(HullContract.Industrial.Strobes);
        // Ni las balizas del techo ni las toberas titilan con el daño
        Assert.Empty(IndustrialHullPieces.FlickerLights.Intersect(IndustrialHullPieces.Beacons.Concat(IndustrialHullPieces.NozzleLights)));
    }

    [Fact]
    public void Manifest_GroupsMatchTheContract()
    {
        Assert.Equal(IndustrialHullPieces.PaintedBody, Manifest.PaintedBody);
        Assert.Equal(IndustrialHullPieces.PaintedCab, Manifest.PaintedCab);
        Assert.Equal(IndustrialHullPieces.LeadingEdges, Manifest.LeadingEdges.Pieces);
        Assert.Equal(IndustrialHullPieces.AmberLights, Manifest.AmberLights);
        Assert.Equal(IndustrialHullPieces.TailLights, Manifest.TailLights);
        Assert.Equal(IndustrialHullPieces.WorkLights, Manifest.WorkLights);
        Assert.Equal(IndustrialHullPieces.Beacons, Manifest.Beacons.Select(b => b.Name));
        Assert.Equal(IndustrialHullPieces.NozzleLights, Manifest.NozzleLights);
        Assert.Equal(IndustrialHullPieces.Glass, Manifest.Glass);
        Assert.Equal(IndustrialHullPieces.Decals, Manifest.Decals);
        Assert.Equal(IndustrialHullPieces.FlickerLights, Manifest.FlickerLights);
        // Las piezas que solo se ven con marcas: calcomanías y franjas de las aletas y del compartimento
        Assert.Equal(IndustrialHullPieces.LiveryPieces.OrderBy(n => n), Manifest.Livery.OrderBy(n => n));
    }

    [Fact]
    public void LeadingEdges_SwapToDarkPlate_WithTheirUvScale()
    {
        Assert.Equal("M_Hazard", Manifest.LeadingEdges.WithLivery);
        Assert.Equal("M_PlateDark", Manifest.LeadingEdges.WithoutLivery);
        Assert.Equal(Manifest.RepeatOf("M_PlateDark") / Manifest.RepeatOf("M_Hazard"), Manifest.LeadingEdges.UvScaleWithoutLivery, 5);
    }

    [Theory]
    [InlineData("Exhaust_L", -0.45f, 0.20f, 3.00f)]
    [InlineData("Exhaust_R", 0.45f, 0.20f, 3.00f)]
    [InlineData("Trail_L", -2.50f, -0.50f, -2.20f)]
    [InlineData("Trail_R", 2.50f, -0.50f, -2.20f)]
    [InlineData("Smoke", 0.40f, 1.33f, -0.06f)]
    public void Empties_AreWhereTheContractSays(string name, float x, float y, float z)
    {
        Vector3 p = Manifest.Empty(name)!.Point;
        Assert.InRange(Vector3.Distance(p, new Vector3(x, y, z)), 0f, 0.001f);
    }

    [Fact]
    public void Lights_StartWithThePrototypeColorsAndIntensities()
    {
        Assert.Equal("#FF9A1A", Manifest.Piece("Light_Nose_L")!.Emission);
        Assert.Equal(2.4f, Manifest.Piece("Light_Canard_R")!.Intensity);
        Assert.Equal("#FF2A1E", Manifest.Piece("Light_Tail_L")!.Emission);
        Assert.Equal(2.4f, Manifest.Piece("Light_Tail_R")!.Intensity);
        Assert.Equal(2.2f, Manifest.Piece("Light_TailBar_L")!.Intensity);
        Assert.Equal("#FFF2DA", Manifest.Piece("Light_Work_2")!.Emission);
        Assert.Equal(2.0f, Manifest.Piece("Light_Work_0")!.Intensity);
        // Toberas naranjas (las de VECTOR y BLADE son azules)
        Assert.Equal("#FF5A14", Manifest.Piece("Nozzle_Glow_L")!.Emission);
        Assert.Equal(2.2f, Manifest.Piece("Nozzle_Glow_R")!.Intensity);
        Assert.Equal("#FFB46A", Manifest.Piece("Nozzle_Core_L")!.Emission);
        Assert.Equal(3.5f, Manifest.Piece("Nozzle_Core_R")!.Intensity);
        // Balizas del techo apagadas en reposo
        Assert.Equal("#FF9A1A", Manifest.Piece("Beacon_Boost_Lens")!.Emission);
        Assert.Equal("#FF2A10", Manifest.Piece("Beacon_Damage_Lens")!.Emission);
        Assert.Equal(0.05f, Manifest.Piece("Beacon_Damage_Lens")!.Intensity);
        Assert.Equal(new[] { "boost", "damage" }, Manifest.Beacons.Select(b => b.Role));
    }

    // ---------- 2. Medidas ----------

    [Fact]
    public void Bounds_AreTheOnesOfTheSpec()
    {
        const float Tolerance = 0.01f;
        Vector3 min = Min(Manifest.BoundsWithLivery), max = Max(Manifest.BoundsWithLivery);
        Vector3 roofMin = Min(Manifest.BoundsWithoutRoofExtras), roofMax = Max(Manifest.BoundsWithoutRoofExtras);
        Assert.InRange(MathF.Abs(max.Z - min.Z - 6.83f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(Manifest.HalfWidth - 2.50f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Y - min.Y - 1.87f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(roofMax.Y - roofMin.Y - 1.60f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(min.Z + 3.83f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Z - 3.00f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(min.Y + 0.52f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Y - 1.35f), 0f, Tolerance);
        Assert.Equal(Manifest.HalfWidth, MathF.Max(-min.X, max.X), 4);
    }

    // ---------- 3. Perfil visual ----------

    [Fact]
    public void Hammer_HasTheIndustrial3DHull()
    {
        ShipHull3DProfile hull = HammerShip.Definition.Visual.Hull3D!;
        Assert.EndsWith("hull_hammer.glb", hull.ModelPath);
        Assert.EndsWith("hull_hammer.manifest.json", hull.ManifestPath);
        Assert.EndsWith(Manifest.Environment, hull.EnvironmentPath);
        Assert.Equal(HullStyle.Industrial, hull.Style);
        Assert.Same(HullContract.Industrial, hull.Contract);
        Assert.Equal(new HoverGlowShape(3.4f, 7.4f, -0.5f), hull.Glow);
    }

    [Fact]
    public void Hammer_KeepsItsWireframe_ForTheGhostAndTheMenu()
    {
        float wireHalfWidth = HammerShip.Definition.Visual.BuildHull().Max(l => MathF.Max(MathF.Abs(l.A.X), MathF.Abs(l.B.X)));
        Assert.Equal(2.5f, wireHalfWidth, 5);
    }

    [Fact]
    public void Hammer_ExhaustsAndTrails_AreTheEmptiesOfItsManifest()
    {
        // Las bocas y las estelas se leen del modelo: caen donde el wireframe tenía las toberas y las puntas caídas
        Assert.Equal(0.45f, MathF.Abs(Manifest.Empty("Exhaust_L")!.Point.X), 3);
        Assert.Equal(0.2f, Manifest.Empty("Exhaust_R")!.Point.Y, 3);
        Assert.Equal(2.5f, Manifest.Empty("Trail_R")!.Point.X, 3);
        Assert.Equal(-0.5f, Manifest.Empty("Trail_R")!.Point.Y, 3);
        Assert.Equal(-2.2f, Manifest.Empty("Trail_R")!.Point.Z, 3);
    }

    // ---------- 4. Colisión ----------

    [Fact]
    public void Hammer_CollisionHalfWidth_IsTheHalfWidthOfThe3DHull()
    {
        Assert.Equal(2.5f, HammerShip.Definition.Visual.CollisionHalfWidth);
        Assert.InRange(MathF.Abs(Manifest.HalfWidth - HammerShip.Definition.Visual.CollisionHalfWidth), 0f, 0.01f);
    }

    // ---------- Pintura de por defecto: AMARILLO OBRA (spec 6.2; el resto del catálogo, en la etapa 3) ----------

    [Fact]
    public void Manifest_PointsToTheMasksOfBodyAndCab()
    {
        HullManifestPaintMask body = Manifest.PaintMasks.Single(m => m.Group == "body");
        HullManifestPaintMask cab = Manifest.PaintMasks.Single(m => m.Group == "cab");
        Assert.Equal("hull_hammer_body_mul.png", body.Mul);
        Assert.Equal("hull_hammer_body_add.png", body.Add);
        Assert.Equal(new[] { "M_Paint", "M_Paint_Canard", "M_Paint_Fin", "M_Paint_Bay" }, body.Materials);
        Assert.Equal("hull_hammer_cab_mul.png", cab.Mul);
        Assert.Equal(new[] { "M_PaintCab" }, cab.Materials);
        Assert.Equal((float)IndustrialHullPaint.MulScale, body.MulScale);
        Assert.Equal(0xAA8428u, IndustrialHullPaint.SiteYellow);   // (170, 132, 40)
    }

    // Píxeles de hull_hammer_body_mul.png (MUL / 1,25) y hull_hammer_body_add.png (ADD) y el color de
    // plateSet(87, pintura) del prototipo en ese mismo píxel, sacado ejecutando su código con Node: chapa lisa,
    // desconchón, arañazo, junta y remache, con AMARILLO OBRA y con BLANCO FLOTA
    [Theory]
    [InlineData(179, 0, 0, 0, 149, 116, 35, 180, 179, 172)]          // lisa (3, 3)
    [InlineData(211, 0, 0, 0, 176, 136, 41, 213, 211, 203)]          // lisa (108, 259)
    [InlineData(138, 75, 74, 70, 190, 163, 97, 215, 212, 202)]       // desconchón (111, 267)
    [InlineData(139, 83, 81, 77, 199, 171, 105, 223, 220, 211)]      // desconchón (109, 269)
    [InlineData(127, 47, 46, 42, 152, 128, 67, 175, 172, 164)]       // arañazo (429, 3)
    [InlineData(168, 39, 38, 36, 179, 147, 69, 209, 206, 197)]       // arañazo (11, 208)
    [InlineData(48, 0, 0, 0, 40, 31, 9, 48, 48, 46)]                 // junta (0, 0)
    [InlineData(58, 0, 0, 0, 49, 38, 11, 59, 58, 56)]                // junta (7, 256)
    [InlineData(186, 22, 22, 22, 177, 142, 58, 210, 208, 200)]       // remache (11, 10)
    [InlineData(206, 24, 24, 24, 196, 158, 65, 233, 231, 223)]       // remache (427, 245)
    public void Bake_RebuildsThePrototypeColor_WithinOne(int mul, int ar, int ag, int ab,
        int yr, int yg, int yb, int wr, int wg, int wb)
    {
        byte[] m = { (byte)mul };
        byte[] add = { (byte)ar, (byte)ag, (byte)ab };
        byte[] yellow = IndustrialHullPaint.Bake(m, add, IndustrialHullPaint.SiteYellow);
        byte[] white = IndustrialHullPaint.Bake(m, add, 0xCECCC4);   // BLANCO FLOTA (206, 204, 196)
        int[] wantYellow = { yr, yg, yb }, wantWhite = { wr, wg, wb };
        for (int c = 0; c < 3; c++)
        {
            Assert.InRange(Math.Abs(yellow[c] - wantYellow[c]), 0, 1);
            Assert.InRange(Math.Abs(white[c] - wantWhite[c]), 0, 1);
        }
    }

    [Fact]
    public void HullPaints_Industrial_HasOnlyTheDefault_ForNow()
    {
        Assert.Equal(1, HullPaints.Count(HullStyle.Industrial));
        Assert.Equal(0, HullPaints.DefaultIndex(HullStyle.Industrial));
        Assert.Equal(0, HullPaints.Step(HullStyle.Industrial, 0, 1));
        Assert.Equal("AMARILLO OBRA · MARCAS", HullPaints.DisplayName(HullStyle.Industrial, 0));
    }
}
