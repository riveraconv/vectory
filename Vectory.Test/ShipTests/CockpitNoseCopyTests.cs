using System;
using System.IO;
using System.Linq;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cabinas 3D · 4 · Morros y cristales (spec docs/cabinas-3d-4-morros.md, 6: pruebas 2 y 3): la copia del morro del casco
// dentro de cada cabina, los cambios del cristal y el casco de HAMMER sin la rejilla del parabrisas
public class CockpitNoseCopyTests
{
    private static readonly CockpitManifest Vector = LoadCockpit("cockpit_vector.manifest.json");
    private static readonly CockpitManifest Blade = LoadCockpit("cockpit_blade.manifest.json");
    private static readonly CockpitManifest Hammer = LoadCockpit("cockpit_hammer.manifest.json");
    private static readonly HullManifest HammerHull = LoadHull("hull_hammer.manifest.json");

    private const float Cut = 0.30f;

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static CockpitManifest LoadCockpit(string file) =>
        CockpitManifest.Parse(File.ReadAllText(Path.Combine(Root(), "assets", "cockpits", file)))!;

    private static HullManifest LoadHull(string file) =>
        HullManifest.Parse(File.ReadAllText(Path.Combine(Root(), "assets", "ships", file)))!;

    private static CockpitManifest Cockpit(string ship) => ship switch
    {
        "vector" => Vector,
        "blade" => Blade,
        _ => Hammer
    };

    // ---------- La copia del morro ----------

    [Theory]
    [InlineData("vector", "hull_vector", 0f, 0.80f, -0.95f)]
    [InlineData("blade", "hull_blade", 0f, 0.37f, -0.45f)]
    [InlineData("hammer", "hull_hammer", 0f, 0.805f, -1.03f)]
    public void Nose_IsACopyOfItsHull_FromTheEyeOfTheSpec(string ship, string hull, float x, float y, float z)
    {
        CockpitManifestNose nose = Cockpit(ship).Nose;
        Assert.Equal(hull, nose.Hull);
        Assert.Equal(new[] { x, y, z }, nose.Eye);
        Assert.Equal(Cut, nose.Cut);
        Assert.Equal("Hull_", nose.MaterialPrefix);
        Assert.NotEmpty(nose.Pieces);
        Assert.All(nose.Pieces, p => Assert.Equal("Nose_" + p.Hull, p.Name));
        Assert.All(nose.Pieces, p => Assert.StartsWith("M_", p.Material));
    }

    [Theory]
    [InlineData("vector", "hull_vector.manifest.json")]
    [InlineData("blade", "hull_blade.manifest.json")]
    [InlineData("hammer", "hull_hammer.manifest.json")]
    public void Nose_LivePieces_AreTheLightsOfTheHull(string ship, string hullFile)
    {
        HullManifest hull = LoadHull(hullFile);
        CockpitManifestHullPiece[] live = Cockpit(ship).Nose.Pieces.Where(p => p.Live).ToArray();
        Assert.Equal(4, live.Length);
        Assert.All(live, p => Assert.True(hull.Piece(p.Hull)?.Intensity > 0f, p.Hull));
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void Nose_HasNothingBehindTheCut(string ship)
    {
        Assert.True(Cockpit(ship).Nose.Bounds.Max[2] <= Cut + 0.0001f);
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void Cockpit_LostItsOwnNoseLights(string ship)
    {
        Assert.DoesNotContain(Cockpit(ship).Pieces, p => p.Name.StartsWith("Led_Nose_") || p.Name.StartsWith("Led_Canard_"));
    }

    [Theory]
    [InlineData("vector", "Light_Nose_L")]
    [InlineData("blade", "Light_Nav_L")]
    [InlineData("hammer", "Light_Canard_L")]
    public void Nose_CarriesTheHullLightsInsteadOfTheOldLeds(string ship, string hullLight)
    {
        Assert.Contains(Cockpit(ship).Nose.Pieces, p => p.Hull == hullLight && p.Live);
    }

    [Theory]
    [InlineData("vector", -3.05f)]   // punta del morro
    [InlineData("blade", -5.30f)]    // punta de la sonda
    [InlineData("hammer", -2.80f)]   // defensa: el eje de sus postes en −2,77 y el radio del tubo
    public void Nose_ReachesWhereTheSpecSays(string ship, float front)
    {
        Assert.InRange(Cockpit(ship).Nose.Bounds.Min[2], front - 0.005f, front + 0.005f);
    }

    [Fact]
    public void Nose_LeavesOutWhatWouldBlockTheView()
    {
        Assert.DoesNotContain(Vector.Nose.Pieces, p => p.Hull == "Canopy_Glass");
        Assert.DoesNotContain(Blade.Nose.Pieces, p => p.Hull == "Canopy_Glass");
        Assert.DoesNotContain(Hammer.Nose.Pieces, p => p.Hull.StartsWith("Cab") || p.Hull.StartsWith("Beacon_")
            || p.Hull.StartsWith("Work_") || p.Hull.StartsWith("Light_Work_"));
    }

    // ---------- Cambios del cristal ----------

    [Fact]
    public void Vector_HasNoCentralBeam()
    {
        Assert.Contains("Frame", Vector.Zones);
        Assert.DoesNotContain("Halo", Vector.Zones);
    }

    [Fact]
    public void Blade_HasTheHigherCombiner_AndTheCanopyBarsInItsFrame()
    {
        Assert.Equal(-0.026f, Blade.CombinerCenterY!.Value, 4);
        Assert.Equal(new[] { "Canopy_Bar_0", "Canopy_Bar_1", "Canopy_Bar_2" }, Blade.Nose.Frame.Select(p => p.Name));
        Assert.All(Blade.Nose.Frame, p => Assert.Equal(p.Name, p.Hull));
        Assert.DoesNotContain(Blade.Nose.Pieces, p => p.Hull.StartsWith("Canopy_Bar_"));
    }

    // ---------- Casco de HAMMER (prueba 3) ----------

    [Fact]
    public void HammerHull_HasNoWindshieldGuard_ButKeepsTheSideGuards()
    {
        Assert.NotEmpty(HammerHull.BuiltPieces);
        Assert.DoesNotContain(HammerHull.BuiltPieces, n => n.StartsWith("Guard_"));
        Assert.Contains(HammerHull.BuiltPieces, n => n.StartsWith("SideGuard_"));
    }
}
