using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Luces de la nave, etapa 1 (spec docs/efectos-8-luces.md, 14, prueba 11): las piezas nuevas de los cascos 3D, sus
// vacíos, la silueta para la sombra y dónde quedan las luces de freno, con los manifiestos reales de los scripts
public class HullLampsTests
{
    private static readonly HullManifest Vector = Hull3DTests.Manifest;
    private static readonly HullManifest Blade = Hull3DBladeTests.Manifest;
    private static readonly HullManifest Hammer = Hull3DHammerTests.Manifest;

    private static HullManifest Of(string ship) => ship switch
    {
        "vector" => Vector,
        "blade" => Blade,
        _ => Hammer
    };

    private static string ShipsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "assets", "ships");
    }

    private static Vector3 Center(HullManifest m, string piece)
    {
        float[] c = m.Piece(piece)!.Center;
        Assert.NotNull(c);
        return new Vector3(c[0], c[1], c[2]);
    }

    // ---------- Piezas nuevas ----------

    [Theory]
    [InlineData("vector", "#FFF4E6", "Light_Head")]
    [InlineData("blade", "#F4F7FF", "Light_Head_L_Top", "Light_Head_R_Top", "Light_Head_L_Bottom", "Light_Head_R_Bottom")]
    [InlineData("hammer", "#FFF4E6", "Light_Head_L", "Light_Head_R")]
    public void Headlights_AreLiveWhiteLights(string ship, string color, params string[] lenses)
    {
        foreach (string name in lenses)
        {
            CockpitManifestPiece piece = Of(ship).Piece(name)!;
            Assert.Equal(color, piece.Emission);
            Assert.InRange(piece.Intensity!.Value, 6f, 7f);
        }
    }

    [Theory]
    [InlineData("vector", "#73CCFF")]
    [InlineData("blade", "#73CCFF")]
    [InlineData("hammer", "#FF8A3A")]
    public void RetroMouths_GlowWithTheMidColorOfTheirShip_AtRest(string ship, string color)
    {
        foreach (string name in LampPieces.RetroGlows)
        {
            CockpitManifestPiece piece = Of(ship).Piece(name)!;
            Assert.Equal(color, piece.Emission);
            Assert.Equal(0.15f, piece.Intensity!.Value, 3);
        }
    }

    [Fact]
    public void Hammer_Reflectors_GlowALittle()
    {
        foreach (string name in IndustrialHullPieces.Reflectors)
            Assert.Equal(0.35f, Hammer.Piece(name)!.Intensity!.Value, 3);
    }

    // ---------- Vacíos ----------

    [Theory]
    [InlineData("vector", 0.10f, 0.03f, -3.74f)]
    [InlineData("blade", 0.333f, 0.027f, -3.98f)]
    [InlineData("hammer", 0.40f, 0.255f, -3.72f)]
    public void SpotEmpties_AreWhereTheSpecSays(string ship, float x, float y, float z)
    {
        Vector3 l = Of(ship).Empty("Spot_Head_L")!.Point, r = Of(ship).Empty("Spot_Head_R")!.Point;
        Assert.Equal(-x, l.X, 2);
        Assert.Equal(x, r.X, 2);
        Assert.Equal(y, r.Y, 2);
        Assert.Equal(z, r.Z, 2);
    }

    // Retro a 45° hacia arriba, en el casco y en la cabina (spec v1.2)
    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void RetroPoints_PointUpAt45Degrees_OnEachSide(string ship)
    {
        Vector3 up45 = Vector3.Normalize(new Vector3(0f, 1f, -1f));
        foreach (string name in LampPieces.RetroPoints)
        {
            HullManifestEmpty point = Of(ship).Empty(name)!;
            Assert.True(Vector3.Distance(up45, point.Direction!.Value) < 0.001f, name);
            Assert.Equal(name.Contains("_L_") ? -1 : 1, Math.Sign(point.Point.X));
            Assert.True(point.Point.Z < -1.5f, name);   // en el morro
        }
    }

    [Fact]
    public void Blade_FlapHinges_AreBehindTheirMouths()
    {
        foreach (string side in new[] { "L", "R" })
        {
            Vector3 hinge = Blade.Empty($"Retro_{side}_FlapHinge")!.Point;
            Vector3 mouth = Blade.Empty($"Retro_{side}_Point")!.Point;
            Assert.InRange(hinge.Z - mouth.Z, 0.12f, 0.13f);
            Assert.Equal(mouth.X, hinge.X, 3);
        }
    }

    // ---------- Luces de freno ----------

    [Fact]
    public void Blade_BrakeLight_SitsUnderTheTailLight_AsAnEqualsSign()
    {
        Vector3 tail = Center(Blade, "Light_Tail"), brake = Center(Blade, "Light_Brake");
        Assert.Equal(0f, brake.X, 3);
        Assert.Equal(tail.Z, brake.Z, 3);
        Assert.Equal(0.06f, tail.Y - brake.Y, 3);   // separadas lo mismo que su alto (0,03)
        Assert.Equal(BrakeColor, Blade.Piece("Light_Brake")!.Emission);
    }

    [Fact]
    public void Hammer_BrakeLight_SitsBetweenTheEngines_OnTheRearCap()
    {
        Vector3 brake = Center(Hammer, "Light_Brake");
        float exhaustX = MathF.Abs(Hammer.Empty("Exhaust_L")!.Point.X);
        Assert.Equal(0f, brake.X, 3);
        Assert.True(brake.Y > Hammer.Empty("Exhaust_L")!.Point.Y + 0.3f);
        Assert.InRange(brake.Z, 2.2f, 2.25f);
        Assert.True(exhaustX > 0.4f);
        Assert.Equal(BrakeColor, Hammer.Piece("Light_Brake")!.Emission);
        Assert.Equal(2.4f, Hammer.Piece("Light_Brake")!.Intensity!.Value, 3);
    }

    private const string BrakeColor = "#FF2A1E";

    // ---------- Silueta ----------

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void Shadow_IsAGraySilhouette_256Wide_WithItsMargin(string ship)
    {
        HullManifest m = Of(ship);
        HullManifestShadow shadow = m.Shadow!;
        Assert.Equal($"hull_{ship}_shadow.png", shadow.Image);
        Assert.Equal(256, shadow.Pixels[0]);
        Assert.Equal(shadow.Pixels[1], (int)MathF.Round(256f * shadow.Size[1] / shadow.Size[0]));
        // 1,2 m de margen por cada lado sobre el ancho del casco
        float width = m.BoundsWithLivery.Max[0] - m.BoundsWithLivery.Min[0];
        Assert.Equal(width + 2.4f, shadow.Size[0], 2);
        Assert.Equal(0f, shadow.Center[0], 2);
        Assert.Equal("+z", shadow.Rows);

        string path = Path.Combine(ShipsFolder(), shadow.Image);
        Assert.True(File.Exists(path), path);
        byte[] png = File.ReadAllBytes(path);
        int w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Assert.Equal(shadow.Pixels[0], w);
        Assert.Equal(shadow.Pixels[1], h);
    }

    // BLADE sin la sonda: la silueta empieza 1,2 m antes del morro, no de la punta de la sonda
    [Fact]
    public void Blade_Shadow_LeavesOutTheProbe()
    {
        HullManifestShadow shadow = Blade.Shadow!;
        float front = shadow.Center[1] - shadow.Size[1] / 2;
        Assert.Equal(Blade.BoundsWithoutProbe.Min[2] - 1.2f, front, 2);
    }
}
