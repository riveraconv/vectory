using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class Cockpit3DManifestTests
{
    private static readonly CockpitManifest Manifest = Load();

    private static CockpitManifest Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        string path = Path.Combine(dir!.FullName, "assets", "cockpits", "cockpit_vector.manifest.json");
        return CockpitManifest.Parse(File.ReadAllText(path))!;
    }

    [Fact]
    public void Manifest_HasEveryPieceOfTheContract()
    {
        Assert.Empty(CockpitManifest.Missing(Manifest.Pieces.Select(p => p.Name)));
        Assert.Equal(Cockpit3DPieces.Contract.Count, Manifest.Pieces.Count);
    }

    [Fact]
    public void Manifest_HasTheExactCounts()
    {
        Assert.Equal(15, Manifest.Pieces.Count(p => p.Name.StartsWith("ShiftLight_") && !p.Name.Contains("Base")));
        Assert.Equal(15, Manifest.Pieces.Count(p => p.Name.StartsWith("ShiftLight_Base_")));
        Assert.Equal(4, Manifest.Pieces.Count(p => p.Name.StartsWith("Led_Console_")));
        Assert.Equal(2, Manifest.Pieces.Count(p => p.Name.StartsWith("Led_Nose_")));
        Assert.Equal(5, Manifest.Pieces.Count(p => p.Name.StartsWith("Accent_")));
    }

    [Theory]
    [InlineData("Screen_L", 0.27f, 0.085f)]
    [InlineData("Screen_R", 0.27f, 0.085f)]
    [InlineData("Screen_Radar", 0.09f, 0.09f)]
    [InlineData("Screen_Map", 0.09f, 0.09f)]
    public void Manifest_ScreensMeasureWhatTheSpecSays(string name, float width, float height)
    {
        CockpitManifestPiece piece = Manifest.Piece(name);
        Assert.InRange(piece.Width!.Value, width - 0.001f, width + 0.001f);
        Assert.InRange(piece.Height!.Value, height - 0.001f, height + 0.001f);
    }

    [Fact]
    public void Manifest_LiveLightsCarryColorAndIntensity()
    {
        Assert.Equal("#FF2D2D", Manifest.Piece("Accent_Visor").Emission);
        Assert.Equal("#3DFF8A", Manifest.Piece("ShiftLight_00").Emission);
        Assert.Equal("#FF2D2D", Manifest.Piece("ShiftLight_05").Emission);
        Assert.Equal("#3D7BFF", Manifest.Piece("ShiftLight_14").Emission);
        Assert.Equal("#FF8A00", Manifest.Piece("Led_Console_0").Emission);
        Assert.True(Manifest.Piece("Led_Nose_L").Intensity > 0f);
    }

    [Fact]
    public void Manifest_SaysWhatGlTFCannotCarry()
    {
        Assert.Contains("Glass_Smudge", Manifest.NoShadow);
        Assert.Contains("Screen_L", Manifest.NoShadow);
        Assert.Contains("Frame_Roof", Manifest.NoShadow);
        Assert.DoesNotContain("ShiftLight_Base_00", Manifest.NoShadow);
        Assert.Equal(new[] { "M_CarbonNose", "M_CarbonNoseSide" }, Manifest.LowReflectionMaterials);
        Assert.Equal(0.35f, Manifest.LowReflectionFactor);
        Assert.Equal("M_GlassSmudge", Assert.Single(Manifest.AdditiveMaterials).Material);
    }

    [Fact]
    public void Manifest_BoundsMatchTheAxesOfThePrototype()
    {
        Assert.Equal(-3.11f, Manifest.Bounds.Min[2], 2);
        Assert.Equal(0.47f, Manifest.Bounds.Max[1], 2);
        Assert.True(Manifest.Triangles > 0);
    }

    [Fact]
    public void Parse_BrokenText_GivesNull()
    {
        Assert.Null(CockpitManifest.Parse("not json"));
    }

    [Fact]
    public void Missing_NamesWhatIsNotThere()
    {
        string[] missing = CockpitManifest.Missing(Cockpit3DPieces.Contract.Where(n => n != "Screen_L")).ToArray();
        Assert.Equal(new[] { "Screen_L" }, missing);
    }
}

public class Cockpit3DProfileTests
{
    [Fact]
    public void Vector_UsesTheRacing3DCockpit()
    {
        Cockpit3DProfile cockpit = ShipDefinitions.Find(ShipCatalog.Ship01)!.Visual.Cockpit3D;
        Assert.NotNull(cockpit);
        Assert.Equal(Cockpit3DStyle.Racing, cockpit.Style);
        Assert.EndsWith("cockpit_vector.glb", cockpit.ModelPath);
        Assert.Null(ShipDefinitions.Find(ShipCatalog.Ship01)!.Visual.BuildCockpit);
    }

    // BLADE lleva la cabina 3D militar: Cockpit3DBladeTests

    [Fact]
    public void Hammer_KeepsTheBunker2D()
    {
        ShipVisualProfile visual = ShipDefinitions.Find(ShipCatalog.Ship03)!.Visual;
        Assert.Null(visual.Cockpit3D);
        Assert.Equal<Func<CockpitLayout>>(HammerCockpit.Build, visual.BuildCockpit);
    }
}

public class Cockpit3DViewRulesTests
{
    [Fact]
    public void Vector_InCockpit_Shows3D_AndHidesTheScreenHud()
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Cockpit, true);
        Assert.True(rules.Cockpit3D);
        Assert.False(rules.CockpitLayer);
        Assert.False(rules.ScreenDriveHud);
        Assert.False(rules.ScreenRadar);
    }

    [Fact]
    public void BladeOrHammer_InCockpit_ShowLayer_DriveHudInTheCockpit_RadarOnScreen()
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Cockpit, false);
        Assert.False(rules.Cockpit3D);
        Assert.True(rules.CockpitLayer);
        Assert.False(rules.ScreenDriveHud);
        Assert.True(rules.ScreenRadar);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnyShip_Outside_ShowsOnlyTheScreenHud(bool has3D)
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Chase, has3D);
        Assert.False(rules.Cockpit3D);
        Assert.False(rules.CockpitLayer);
        Assert.True(rules.ScreenDriveHud);
        Assert.True(rules.ScreenRadar);
    }
}

public class CockpitCameraMotionTests
{
    private static readonly CockpitCameraSettings Defaults = new(0.03f, 1.6f, 0.35f, 0.25f, 0.05f);

    [Fact]
    public void Sway_FollowsTheFormula_RightIsPositive()
    {
        // Girando a la derecha el Steer del juego es -1 y la cámara se va a la derecha, como en el prototipo
        var input = new CockpitCameraInput(-1f, 0.5f, 0f, 0f, 0f, 0f);
        Assert.Equal(0.03f * (0.3f + 0.7f * 0.5f), CockpitCameraMotion.Compute(input, Defaults).Position.X, 5);
    }

    [Fact]
    public void Vibration_UsesTheConstantsOfThePrototype_AndNotTheMetersOfTheWorldCamera()
    {
        var input = new CockpitCameraInput(0f, 0f, 0f, 1f, 1f, 0.7f);
        CockpitCameraPose pose = CockpitCameraMotion.Compute(input, Defaults);
        float vib = 0.0025f + 0.003f;
        Assert.Equal(CockpitCameraMotion.Noise(0.7f, 7f) * vib * 1.6f, pose.Position.X, 5);
        Assert.Equal(CockpitCameraMotion.Noise(0.7f, 9f) * vib * 1.6f, pose.Position.Y, 5);
        Assert.Equal(CockpitCameraMotion.Noise(0.7f, 4f) * vib * 0.35f, pose.Rotation.X, 5);
        Assert.Equal(CockpitCameraMotion.Noise(0.7f, 1f) * vib * 0.35f, pose.Rotation.Y, 5);
    }

    [Fact]
    public void Vibration_FlatOutWithBoost_StaysUnderAbout11Millimeters()
    {
        for (float t = 0f; t < 5f; t += 0.01f)
        {
            var input = new CockpitCameraInput(0f, 1f, 0f, 1.5f, 1f, t);
            CockpitCameraPose pose = CockpitCameraMotion.Compute(input, Defaults);
            Assert.InRange(Math.Abs(pose.Position.Y), 0f, 0.0111f);
        }
    }

    [Fact]
    public void CounterRoll_TurnsAgainstTheWorld()
    {
        var input = new CockpitCameraInput(0f, 0f, 0.1f, 0f, 0f, 0f);
        Assert.Equal(-0.025f, CockpitCameraMotion.Compute(input, Defaults).Rotation.Z, 5);
    }

    [Fact]
    public void Push_GoesBackWithTheBoost()
    {
        var input = new CockpitCameraInput(0f, 0f, 0f, 0f, 1f, 0f);
        Assert.Equal(0.05f, CockpitCameraMotion.Compute(input, Defaults).Position.Z, 5);
    }

    [Fact]
    public void AllParametersAtZero_StaysAtTheOriginWithoutTurning()
    {
        var input = new CockpitCameraInput(1f, 1f, 0.1f, 1.5f, 1f, 1.3f);
        CockpitCameraPose pose = CockpitCameraMotion.Compute(input, default);
        Assert.Equal(Vector3.Zero, pose.Position);
        Assert.Equal(0f, pose.Rotation.X);
        Assert.Equal(0f, pose.Rotation.Y);
        Assert.Equal(0f, Math.Abs(pose.Rotation.Z));
    }

    [Fact]
    public void Noise_StaysBetweenMinusOneAndOne()
    {
        for (float t = 0f; t < 10f; t += 0.003f)
            Assert.InRange(CockpitCameraMotion.Noise(t, 7f), -1f, 1f);
    }
}
