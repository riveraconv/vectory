using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cabina 3D de BLADE, etapa 2 (spec docs/cabinas-3d-2-blade.md, 8: pruebas 1 a 4) y la disposición viva de las dos
// cabinas 3D
public class Cockpit3DBladeManifestTests
{
    private static readonly CockpitManifest Manifest = Load("cockpit_blade.manifest.json");
    private static readonly CockpitManifest VectorManifest = Load("cockpit_vector.manifest.json");

    private static CockpitManifest Load(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        return CockpitManifest.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "assets", "cockpits", file)))!;
    }

    [Fact]
    public void Manifest_HasEveryPieceOfTheContract_AndNothingElse()
    {
        Assert.Equal("CockpitBlade", Manifest.Root);
        Assert.Empty(CockpitManifest.Missing(Manifest.Pieces.Select(p => p.Name), MilitaryPieces.Contract));
        Assert.Equal(MilitaryPieces.Contract.Count, Manifest.Pieces.Count);
        Assert.Equal(MilitaryPieces.Contract.Count, Manifest.Pieces.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void Contract_HasTheCountsOfTheSpec()
    {
        IReadOnlyList<string> c = MilitaryPieces.Contract;
        Assert.Equal(4, c.Count(n => n.StartsWith("Screen_") && !n.EndsWith("_Shade")));
        Assert.Equal(3, c.Count(n => n.EndsWith("_Shade")));
        Assert.Equal(4, c.Count(n => n.StartsWith("Accent_")));
        Assert.Equal(3, c.Count(n => n.StartsWith("Led_Visor_")));
        Assert.Equal(2, c.Count(n => n.StartsWith("Lamp_")));
        Assert.Equal(23, c.Count);   // tabla 3.7: 1 + 3 + 3 + 4 + 3 + 2 + 1 + 2 + 1 + 1 + 1 + 1
    }

    [Theory]
    [InlineData("Screen_HUD", 0.22f, 0.13f)]
    [InlineData("Screen_Speed", 0.105f, 0.14f)]
    [InlineData("Screen_Radar", 0.17f, 0.14f)]
    [InlineData("Screen_Map", 0.105f, 0.14f)]
    [InlineData("Lamp_Caution", 0.048f, 0.019f)]
    [InlineData("Lamp_Warning", 0.048f, 0.019f)]
    [InlineData("Plate", 0.10f, 0.0125f)]
    public void Manifest_SurfacesMeasureWhatTheSpecSays(string name, float width, float height)
    {
        CockpitManifestPiece piece = Manifest.Piece(name)!;
        Assert.InRange(piece.Width!.Value, width - 0.001f, width + 0.001f);
        Assert.InRange(piece.Height!.Value, height - 0.001f, height + 0.001f);
    }

    [Fact]
    public void Manifest_EveryFlickerLightCarriesColorAndIntensity()
    {
        foreach (string name in MilitaryPieces.FlickerLights)
        {
            CockpitManifestPiece piece = Manifest.Piece(name)!;
            Assert.NotNull(piece.Emission);
            Assert.True(piece.Intensity > 0f, name);
        }
        Assert.Equal(1.6f, Manifest.Piece("Accent_Radar")!.Intensity);
        Assert.Equal(0.45f, Manifest.Piece("Accent_Glare")!.Intensity);
        Assert.Equal(3f, Manifest.Piece("Led_Nose_L")!.Intensity);
    }

    [Fact]
    public void Manifest_SaysWhatGlTFCannotCarry()
    {
        Assert.Contains(Manifest.AdditiveMaterials, a => a.Material == "M_GlassSmudge" && a.Opacity == 0.05f);
        Assert.Contains(Manifest.AdditiveMaterials, a => a.Material == "M_ScreenHUD");
        Assert.Contains(Manifest.TransparentMaterials, t => t.Material == "M_Glass" && t.Opacity == 0.05f);
        Assert.Contains(Manifest.TransparentMaterials, t => t.Material == "M_CombinerGlass" && t.Opacity == 0.10f);
        Assert.Contains("Screen_Radar", Manifest.NoShadow);
        Assert.Contains("Glass_Canopy", Manifest.NoShadow);
    }

    [Fact]
    public void ReflectionFactor_UsesTheFullListWhenThereIsOne()
    {
        Assert.Equal(0.5f, Manifest.ReflectionFactor("M_PaintNose"));
        Assert.Equal(1.3f, Manifest.ReflectionFactor("M_Glass"));
        Assert.Equal(1.5f, Manifest.ReflectionFactor("M_CombinerGlass"));
        Assert.Equal(1f, Manifest.ReflectionFactor("M_Paint"));
    }

    [Fact]
    public void ReflectionFactor_OfVector_IsTheOneItHadBefore()
    {
        Assert.Empty(VectorManifest.Reflections);
        Assert.Equal(VectorManifest.LowReflectionFactor, VectorManifest.ReflectionFactor("M_CarbonNose"));
        Assert.Equal(1f, VectorManifest.ReflectionFactor("M_Carbon"));
    }

    [Fact]
    public void Bounds_MatchTheAxesOfThePrototype()
    {
        Assert.InRange(Manifest.Bounds.Min[2], -5.52f, -5.50f);   // punta de la sonda
        Assert.InRange(Manifest.Bounds.Max[1], 0.66f, 0.67f);      // cúpula
    }
}

public class Cockpit3DBladeProfileTests
{
    private const float ShipHover = 2f;   // Track.ShipHover: la nave vuela a 2 m de la pista

    [Fact]
    public void Blade_UsesTheMilitary3DCockpit()
    {
        Cockpit3DProfile cockpit = ShipDefinitions.Find(ShipCatalog.Ship02)!.Visual.Cockpit3D!;
        Assert.Equal(Cockpit3DStyle.Military, cockpit.Style);
        Assert.EndsWith("cockpit_blade.glb", cockpit.ModelPath);
        Assert.EndsWith("cockpit_blade.manifest.json", cockpit.ManifestPath);
        Assert.EndsWith("cockpit_blade_env.png", cockpit.EnvironmentPath);
        Assert.Same(Cockpit3DLayouts.Military, cockpit.Layout);
    }

    [Fact]
    public void Blade_EyeAt090m_Fov70_LookingDown57Degrees()
    {
        CockpitFraming framing = ShipDefinitions.Find(ShipCatalog.Ship02)!.Visual.Cockpit;
        Assert.Equal(0.90f, framing.Eye.Y + ShipHover, 4);
        Assert.Equal(70f, framing.Fov);
        Assert.Equal(-5.7f, framing.PitchDegrees, 1);
        Assert.Equal(-0.10f, framing.PitchRadians, 5);
    }

    [Fact]
    public void Vector_StaysRacing_WithoutPitch()
    {
        ShipVisualProfile visual = ShipDefinitions.Find(ShipCatalog.Ship01)!.Visual;
        Assert.Same(Cockpit3DLayouts.Racing, visual.Cockpit3D!.Layout);
        Assert.Equal(0f, visual.Cockpit.PitchDegrees);
        Assert.Equal(1.45f, visual.Cockpit.Eye.Y + ShipHover, 4);
    }
}

public class Cockpit3DBladeViewRulesTests
{
    // Etapa 3: las pantallas llevan velocidad, boost, daño, radar y minimapa, y el HUD de pantalla ya no sale en cabina
    [Fact]
    public void Blade_InCockpit_Shows3D_AndHidesTheScreenHud()
    {
        bool screensShowHud = Cockpit3DLayouts.Military.ScreensShowHud;
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Cockpit, true, screensShowHud);

        Assert.True(screensShowHud);
        Assert.True(rules.Cockpit3D);
        Assert.False(rules.CockpitLayer);
        Assert.False(rules.ScreenDriveHud);
        Assert.False(rules.ScreenRadar);
    }

    // La regla de las pantallas de prueba sigue en el código para la próxima cabina 3D
    [Fact]
    public void ScreensWithoutHud_InCockpit_KeepTheScreenHud()
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Cockpit, true, false);
        Assert.True(rules.Cockpit3D);
        Assert.True(rules.ScreenDriveHud);
        Assert.True(rules.ScreenRadar);
    }

    [Fact]
    public void Vector_InCockpit_StillHidesTheScreenHud()
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Cockpit, true, Cockpit3DLayouts.Racing.ScreensShowHud);
        Assert.True(rules.Cockpit3D);
        Assert.False(rules.ScreenDriveHud);
        Assert.False(rules.ScreenRadar);
    }

    [Fact]
    public void Blade_Outside_ShowsOnlyTheScreenHud()
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Chase, true, false);
        Assert.False(rules.Cockpit3D);
        Assert.True(rules.ScreenDriveHud);
        Assert.True(rules.ScreenRadar);
    }
}

public class Cockpit3DPitchTests
{
    // El morro tiene que casar con la pista: la cámara de la cabina se inclina lo mismo que la del mundo
    [Fact]
    public void Blade_CockpitCameraPitch_IsTheWorldCameraPitch()
    {
        CockpitFraming framing = ShipDefinitions.Find(ShipCatalog.Ship02)!.Visual.Cockpit;
        CockpitCameraPose still = CockpitCameraMotion.Compute(default, default);

        CockpitCameraPose pitched = CockpitCameraMotion.Pitched(still, framing.PitchRadians);

        Assert.Equal(framing.PitchRadians, pitched.Rotation.X);
        Assert.Equal(0f, pitched.Rotation.Y);
        Assert.Equal(0f, pitched.Rotation.Z);
    }

    [Fact]
    public void Pitch_AddsToTheOwnMotion_WithoutTouchingTheRest()
    {
        var pose = new CockpitCameraPose(new Vector3(0.01f, 0.02f, 0.03f), new Vector3(0.004f, 0.005f, 0.006f));

        CockpitCameraPose pitched = CockpitCameraMotion.Pitched(pose, -0.1f);

        Assert.Equal(pose.Position, pitched.Position);
        Assert.Equal(0.004f - 0.1f, pitched.Rotation.X, 6);
        Assert.Equal(0.005f, pitched.Rotation.Y);
        Assert.Equal(0.006f, pitched.Rotation.Z);
    }

    [Fact]
    public void Vector_HasNoPitch_SoItsCockpitCameraDoesNotChange()
    {
        var pose = new CockpitCameraPose(new Vector3(0.01f, 0f, 0f), new Vector3(0.004f, 0.005f, 0.006f));
        float pitch = ShipDefinitions.Find(ShipCatalog.Ship01)!.Visual.Cockpit.PitchRadians;

        Assert.Equal(pose, CockpitCameraMotion.Pitched(pose, pitch));
    }
}

public class Cockpit3DLayoutTests
{
    [Fact]
    public void Racing_IsVectorAsItWas()
    {
        Cockpit3DLayout racing = Cockpit3DLayouts.Racing;
        Assert.Equal(Cockpit3DPieces.Contract, racing.Contract);
        Assert.Equal(Cockpit3DPieces.FlickerLights, racing.FlickerLights);
        Assert.Equal(ShiftLights.Count, racing.ShiftLightCount);
        Assert.Empty(racing.Labels);
        Assert.True(racing.ScreensShowHud);
        Assert.Equal(CockpitLightMix.DamageFactor, racing.Scene.DamageLightFactor);
        Assert.Equal(RacingPalette.Ambient, racing.Scene.Ambient);

        Assert.Equal(new[] { "Screen_L", "Screen_R", "Screen_Radar", "Screen_Map" }, racing.Screens.Select(s => s.Piece));
        Assert.All(racing.Screens, s => Assert.Equal(1f, s.LightFactor));
        // 6 cm por delante de la boca de la pantalla, que está 4 mm por delante del fondo de su carcasa
        Assert.Equal(0.045f - 0.004f + 0.06f, racing.Screens[0].LightOffset, 6);
        Assert.Equal(0.035f - 0.004f + 0.06f, racing.Screens[2].LightOffset, 6);
    }

    [Fact]
    public void Military_HasTheFourScreensOfTheSpec_WithTheirPixels()
    {
        Cockpit3DScreen[] screens = Cockpit3DLayouts.Military.Screens.ToArray();

        Assert.Equal(new[] { "Screen_HUD", "Screen_Speed", "Screen_Radar", "Screen_Map" }, screens.Select(s => s.Piece));
        Assert.Equal((512, 302), (screens[0].Width, screens[0].Height));
        Assert.Equal((300, 400), (screens[1].Width, screens[1].Height));
        Assert.Equal((512, 420), (screens[2].Width, screens[2].Height));
        Assert.Equal((300, 400), (screens[3].Width, screens[3].Height));
        Assert.Equal(new[]
        {
            Cockpit3DScreenContent.MilitaryCombiner, Cockpit3DScreenContent.MilitarySpeed,
            Cockpit3DScreenContent.MilitaryRadar, Cockpit3DScreenContent.MilitaryMap
        }, screens.Select(s => s.Content));
        Assert.True(Cockpit3DLayouts.Military.BrightnessFollowsGlow);
        Assert.False(Cockpit3DLayouts.Racing.BrightnessFollowsGlow);
    }

    [Fact]
    public void Military_OnlyTheHudGlassIsAdditive_AndHasNoLight()
    {
        Cockpit3DScreen[] screens = Cockpit3DLayouts.Military.Screens.ToArray();

        Assert.True(screens[0].Additive);
        Assert.Equal(Cockpit3DScreenLight.None, screens[0].Light);
        Assert.All(screens.Skip(1), s =>
        {
            Assert.False(s.Additive);
            Assert.Equal(Cockpit3DScreenLight.Large, s.Light);
            Assert.Equal(0.06f - 0.0015f, s.LightOffset, 6);
        });
    }

    // brillo × 0,03 × área / 0,017 en el prototipo de BLADE, frente a brillo × 0,3 de las pantallas grandes de VECTOR
    [Theory]
    [InlineData("Screen_Radar", 0.14f)]
    [InlineData("Screen_Speed", 0.086471f)]
    [InlineData("Screen_Map", 0.086471f)]
    public void Military_ScreenLight_FollowsTheAreaOfEachScreen(string piece, float factor)
    {
        Cockpit3DScreen screen = Cockpit3DLayouts.Military.Screens.Single(s => s.Piece == piece);
        Assert.Equal(factor, screen.LightFactor, 5);
    }

    [Fact]
    public void Military_FlickersAccentsAndLeds_NeverScreensOrLamps()
    {
        IReadOnlyList<string> flicker = Cockpit3DLayouts.Military.FlickerLights;

        Assert.Equal(10, flicker.Count);
        Assert.All(flicker, n => Assert.True(n.StartsWith("Accent_") || n.StartsWith("Led_"), n));
        Assert.All(flicker, n => Assert.Contains(n, MilitaryPieces.Contract));
        Assert.Equal(0, Cockpit3DLayouts.Military.ShiftLightCount);
    }

    [Fact]
    public void Military_LabelsAreTheMasterLampsAndThePlate()
    {
        Cockpit3DLabel[] labels = Cockpit3DLayouts.Military.Labels.ToArray();

        Cockpit3DLabel caution = labels.Single(l => l.Piece == "Lamp_Caution");
        Assert.Equal(new[] { "MASTER", "CAUTION" }, caution.Lines.Select(l => l.Text));
        Assert.Equal(new[] { 50f, 94f }, caution.Lines.Select(l => l.Y));
        Assert.Equal((256, 112, 40f, true), (caution.Width, caution.Height, caution.FontSize, caution.Bold));
        Assert.Equal(MilitaryPalette.Caution, caution.LampColor);

        Cockpit3DLabel warning = labels.Single(l => l.Piece == "Lamp_Warning");
        Assert.Equal(new[] { "MASTER", "WARN" }, warning.Lines.Select(l => l.Text));
        Assert.Equal(MilitaryPalette.Warning, warning.LampColor);

        Cockpit3DLabel plate = labels.Single(l => l.Piece == "Plate");
        Assert.Equal("BLADE · BLD-02", plate.Lines.Single().Text);
        Assert.Equal((256, 32, 20f, 23f), (plate.Width, plate.Height, plate.FontSize, plate.Lines.Single().Y));
        Assert.Null(plate.LampColor);
    }

    [Fact]
    public void Military_SceneColorsAreThoseOfThePrototype()
    {
        Cockpit3DScene scene = Cockpit3DLayouts.Military.Scene;
        Assert.Equal(0x1C221Cu, scene.Ambient);
        Assert.Equal(0xCFFFC0u, scene.ScreenLight);
        Assert.Equal(0xFFA040u, scene.BoostLight);
        Assert.Equal(0xFF4D3Au, scene.DamageLight);
        Assert.Equal(1.2f, scene.DamageLightFactor);
        Assert.Equal(Cockpit3DFont.Mono, Cockpit3DLayouts.Military.Font);
    }

    [Fact]
    public void DamageLight_UsesTheFactorOfEachCockpit()
    {
        var mix = new CockpitLightMix();
        var intensity = new CockpitLightIntensities(1f, 1f, 1f);

        Assert.Equal(0.45f * 3f, mix.Values(0.45f, intensity).Damage, 5);
        Assert.Equal(0.45f * 1.2f, mix.Values(0.45f, intensity, 1.2f).Damage, 5);
    }
}

public class MilitaryScreenDrawingTests
{
    [Fact]
    public void Base_HasBackground_ScanLines_AndCorners()
    {
        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Base(output, 300, 400);

        ScreenPrimitive background = output[0];
        Assert.Equal(MilitaryPalette.Rgba(MilitaryPalette.ScreenBackground), background.Color);
        Assert.Equal(new Vector2(300f, 400f), background.B);
        Assert.Equal(134, output.Count(p => p.Kind == ScreenPrimitiveKind.FillRect && p.B.Y == 1f));   // una cada 3 px
        Assert.Equal(8, output.Count(p => p.Kind == ScreenPrimitiveKind.Line));                        // 4 esquinas
    }

    [Fact]
    public void Corners_AreThoseOfThePrototype()
    {
        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Corners(output, 300, 400);

        ScreenPrimitive first = output[0];
        Assert.Equal(new Vector2(6f, 22f), first.A);
        Assert.Equal(new Vector2(6f, 6f), first.B);
        Assert.Equal(2f, first.Width);
        Assert.Equal(MilitaryPalette.Rgba(MilitaryPalette.Frame, 0.8f), first.Color);
        Assert.Equal(new Vector2(294f, 394f), output[^1].A);
    }

    [Fact]
    public void Label_IsBackgroundAndCenteredLines()
    {
        Cockpit3DLabel caution = Cockpit3DLayouts.Military.Labels.Single(l => l.Piece == "Lamp_Caution");
        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Label(output, caution);

        Assert.Equal(new Vector2(256f, 112f), output[0].B);
        ScreenPrimitive[] lines = output.Where(p => p.Kind == ScreenPrimitiveKind.Text).ToArray();
        Assert.Equal(new[] { new Vector2(128f, 50f), new Vector2(128f, 94f) }, lines.Select(p => p.A));
        Assert.All(lines, p => Assert.Equal(40f, p.Radius));
    }
}
