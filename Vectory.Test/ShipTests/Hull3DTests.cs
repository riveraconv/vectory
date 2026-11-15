using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Casco 3D de VECTOR (spec docs/cascos-3d-1-vector.md, 9, pruebas 1 a 4), con el manifiesto real del script de Blender
public class Hull3DTests
{
    internal static readonly HullManifest Manifest = Load();

    private static HullManifest Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        string path = Path.Combine(dir!.FullName, "assets", "ships", "hull_vector.manifest.json");
        return HullManifest.Parse(File.ReadAllText(path))!;
    }

    private static Vector3 Min(CockpitManifestBounds b) => new(b.Min[0], b.Min[1], b.Min[2]);
    private static Vector3 Max(CockpitManifestBounds b) => new(b.Max[0], b.Max[1], b.Max[2]);

    // ---------- 1. Manifiesto ----------

    [Fact]
    public void Manifest_HasEveryPieceAndEmptyOfTheContract_OnceEach()
    {
        Assert.Equal(HullPieces.Root, Manifest.Root);
        Assert.Empty(HullManifest.Missing(Manifest.Pieces.Select(p => p.Name)));
        Assert.Equal(HullPieces.Contract.Count, Manifest.Pieces.Count);
        Assert.Equal(Manifest.Pieces.Count, Manifest.Pieces.Select(p => p.Name).Distinct().Count());
        Assert.Equal(HullPieces.Empties.OrderBy(n => n), Manifest.Empties.Select(e => e.Name).OrderBy(n => n));
    }

    [Fact]
    public void Contract_HasTheCountsOfTheSpec()
    {
        Assert.Equal(5, HullPieces.Painted.Length);
        Assert.Equal(8, HullPieces.AccentEdges.Length);
        Assert.Equal(4, HullPieces.AccentLights.Length);
        Assert.Equal(2, HullPieces.TailLights.Length);
        Assert.Equal(4, HullPieces.NozzleLights.Length);
        Assert.Equal(6, HullPieces.Stripes.Length);
        Assert.Equal(2, HullPieces.Pins.Length);
        Assert.Equal(9, HullPieces.Decals.Length);
        Assert.Equal(14, HullPieces.FlickerLights.Count());
        Assert.Equal(41, HullPieces.Contract.Count);
    }

    [Fact]
    public void Manifest_GroupsMatchTheContract()
    {
        Assert.Equal(HullPieces.Painted, Manifest.Painted);
        Assert.Equal(HullPieces.AccentEdges.Concat(HullPieces.AccentLights), Manifest.Accents);
        Assert.Equal(HullPieces.TailLights, Manifest.TailLights);
        Assert.Equal(HullPieces.NozzleLights, Manifest.NozzleLights);
        Assert.Equal(HullPieces.Stripes, Manifest.Stripes);
        Assert.Equal(HullPieces.Pins, Manifest.Pins);
        Assert.Equal(HullPieces.Decals, Manifest.Decals);
        Assert.Equal(HullPieces.LiveryPieces.OrderBy(n => n), Manifest.Livery.OrderBy(n => n));
    }

    [Theory]
    [InlineData("Decal_Number_Pod_L", 0.34f, 0.204f)]
    [InlineData("Decal_Number_Pod_R", 0.34f, 0.204f)]
    [InlineData("Decal_Sponsor_Pod_L", 0.42f, 0.105f)]
    [InlineData("Decal_Sponsor_Pod_R", 0.42f, 0.105f)]
    [InlineData("Decal_Checker_L", 0.56f, 0.14f)]
    [InlineData("Decal_Checker_R", 0.56f, 0.14f)]
    public void PodDecals_MeasureWhatTheContractSays(string name, float width, float height)
    {
        CockpitManifestPiece piece = Manifest.Piece(name)!;
        Assert.InRange(MathF.Abs(piece.Width!.Value - width), 0f, 0.001f);
        Assert.InRange(MathF.Abs(piece.Height!.Value - height), 0f, 0.001f);
    }

    [Theory]
    [InlineData("Exhaust_L", -0.75f, 0.1f, 3.2f)]
    [InlineData("Exhaust_R", 0.75f, 0.1f, 3.2f)]
    [InlineData("Trail_L", -2.6f, -0.05f, 2.72f)]
    [InlineData("Trail_R", 2.6f, -0.05f, 2.72f)]
    [InlineData("Smoke", 0.75f, 0.5f, 2.2f)]
    public void Empties_AreWhereTheContractSays(string name, float x, float y, float z)
    {
        Vector3 p = Manifest.Empty(name)!.Point;
        Assert.InRange(Vector3.Distance(p, new Vector3(x, y, z)), 0f, 0.001f);
    }

    [Fact]
    public void Lights_StartWithThePrototypeColorsAndIntensities()
    {
        Assert.Equal("#FF2D2D", Manifest.Piece("Accent_Chine_R")!.Emission);
        Assert.Equal(0.9f, Manifest.Piece("Accent_Chine_R")!.Intensity);
        Assert.Equal(1f, Manifest.Piece("Accent_WingLE_L")!.Intensity);
        Assert.Equal(3f, Manifest.Piece("Light_Nose_L")!.Intensity);
        Assert.Equal(3.5f, Manifest.Piece("Light_Fin_R")!.Intensity);
        Assert.Equal("#FF1A1A", Manifest.Piece("Light_Tail_L")!.Emission);
        Assert.Equal(3.5f, Manifest.Piece("Light_Tail_L")!.Intensity);
        Assert.Equal("#245BFF", Manifest.Piece("Nozzle_Glow_L")!.Emission);
        Assert.Equal(2.2f, Manifest.Piece("Nozzle_Glow_L")!.Intensity);
        Assert.Equal(3.5f, Manifest.Piece("Nozzle_Core_R")!.Intensity);
    }

    // ---------- 2. Medidas, sin librea ----------

    [Fact]
    public void Bounds_WithoutLivery_AreTheOnesOfTheSpec()
    {
        const float Tolerance = 0.01f;
        Vector3 min = Min(Manifest.Bounds), max = Max(Manifest.Bounds);
        Assert.InRange(MathF.Abs(max.Z - min.Z - 7.31f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(Manifest.HalfWidth - 2.634f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Y - min.Y - 1.016f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(min.Z + 4.00f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Z - 3.31f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(min.Y + 0.31f), 0f, Tolerance);
        Assert.InRange(MathF.Abs(max.Y - 0.706f), 0f, Tolerance);
        Assert.Equal(Manifest.HalfWidth, MathF.Max(-min.X, max.X), 4);
    }

    // ---------- 3. Perfil visual ----------

    [Fact]
    public void Vector_HasThe3DHull_BladeAndHammerKeepTheWireframe()
    {
        ShipHull3DProfile hull = VectorShip.Definition.Visual.Hull3D!;
        Assert.EndsWith("hull_vector.glb", hull.ModelPath);
        Assert.EndsWith("hull_vector.manifest.json", hull.ManifestPath);
        Assert.EndsWith(Manifest.Environment, hull.EnvironmentPath);
        Assert.Null(BladeShip.Definition.Visual.Hull3D);
        Assert.Null(HammerShip.Definition.Visual.Hull3D);
    }

    [Fact]
    public void Vector_KeepsItsWireframe_ForTheGhostTheMenuAndTheTestShips()
    {
        // El fantasma, el menú y las naves de prueba dibujan BuildHull: sigue siendo el wireframe de antes
        float wireHalfWidth = VectorShip.Definition.Visual.BuildHull().Max(l => MathF.Max(MathF.Abs(l.A.X), MathF.Abs(l.B.X)));
        Assert.Equal(3.4f, wireHalfWidth, 5);
    }

    // ---------- 4. Colisión ----------

    [Fact]
    public void Vector_CollisionHalfWidth_IsTheHalfWidthOfThe3DHull()
    {
        Assert.Equal(2.63f, VectorShip.Definition.Visual.CollisionHalfWidth);
        Assert.InRange(MathF.Abs(Manifest.HalfWidth - VectorShip.Definition.Visual.CollisionHalfWidth), 0f, 0.01f);
    }
}
