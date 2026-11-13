using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cabina 3D de HAMMER (spec docs/cabinas-3d-3-hammer.md, 8: pruebas 1 a 12), su disposición viva y su dibujo
public class Cockpit3DHammerManifestTests
{
    private static readonly CockpitManifest Manifest = Load("cockpit_hammer.manifest.json");

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
        Assert.Equal("CockpitHammer", Manifest.Root);
        Assert.Empty(CockpitManifest.Missing(Manifest.Pieces.Select(p => p.Name), IndustrialPieces.Contract));
        Assert.Equal(IndustrialPieces.Contract.Count, Manifest.Pieces.Count);
        Assert.Equal(IndustrialPieces.Contract.Count, Manifest.Pieces.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void Contract_HasTheCountsOfTheSpec()
    {
        IReadOnlyList<string> c = IndustrialPieces.Contract;
        Assert.Equal(4, c.Count(n => n.StartsWith("Screen_") && !n.EndsWith("_Shade") && !n.EndsWith("_Glass")));
        Assert.Equal(4, c.Count(n => n.EndsWith("_Shade")));
        Assert.Equal(4, c.Count(n => n.StartsWith("Screen_") && n.EndsWith("_Glass")));
        Assert.Equal(3, c.Count(n => n.StartsWith("Accent_")));
        Assert.Equal(4, c.Count(n => n.StartsWith("Led_Nose_") || n.StartsWith("Led_Canard_")));
        Assert.Equal(3, c.Count(n => n.StartsWith("Lamp_Boost_")));
        Assert.Equal(2, c.Count(n => n.StartsWith("Beacon_")));
        Assert.Equal(4, c.Count(n => n.StartsWith("Label_")));
        Assert.Equal(6, c.Count(n => n.StartsWith("Led_Panel_")));
        Assert.Equal(2, c.Count(n => n.StartsWith("Gauge_")));
        Assert.Equal(39, c.Count);   // tabla 3.7: 4 + 4 + 3 + 2 + 2 + 3 + 2 + 1 + 3 + 1 + 6 + 2 + 2 + 4
    }

    [Theory]
    [InlineData("Screen_Radar", 0.17f, 0.13f)]
    [InlineData("Screen_Map", 0.17f, 0.13f)]
    [InlineData("Screen_Damage", 0.12f, 0.12f)]
    [InlineData("Screen_Speed", 0.40f, 0.075f)]
    [InlineData("Plate", 0.13f, 0.016f)]
    [InlineData("Label_Boost_1", 0.034f, 0.010f)]
    [InlineData("Label_Boost_3", 0.034f, 0.010f)]
    [InlineData("Label_Aux", 0.17f, 0.011f)]
    [InlineData("Gauge_Hyd_Face", 0.038f, 0.038f)]
    [InlineData("Gauge_Temp_Face", 0.038f, 0.038f)]
    public void Manifest_SurfacesMeasureWhatTheSpecSays(string name, float width, float height)
    {
        CockpitManifestPiece piece = Manifest.Piece(name)!;
        Assert.InRange(piece.Width!.Value, width - 0.001f, width + 0.001f);
        Assert.InRange(piece.Height!.Value, height - 0.001f, height + 0.001f);
    }

    [Fact]
    public void Manifest_EveryLightCarriesColorAndIntensity()
    {
        foreach (string name in IndustrialPieces.FlickerLights)
        {
            CockpitManifestPiece piece = Manifest.Piece(name)!;
            Assert.NotNull(piece.Emission);
            Assert.True(piece.Intensity > 0f, name);
        }
        Assert.Equal(1.4f, Manifest.Piece("Accent_Radar")!.Intensity);
        Assert.Equal(3f, Manifest.Piece("Led_Canard_R")!.Intensity);
        Assert.Equal(1.5f, Manifest.Piece("Led_Panel_1")!.Intensity);
        Assert.Equal("#7CFF3A", Manifest.Piece("Led_Panel_1")!.Emission);
        Assert.Equal("#FFA020", Manifest.Piece("Led_Panel_2")!.Emission);
        Assert.Equal("#FFB000", Manifest.Piece("Lamp_Boost_1")!.Emission);
        Assert.Equal("#FF2A10", Manifest.Piece("Beacon_Damage_Lens")!.Emission);
    }

    [Fact]
    public void Manifest_SaysWhatGlTFCannotCarry()
    {
        Assert.Contains(Manifest.AdditiveMaterials, a => a.Material == "M_GlassSmudge" && a.Opacity == 0.06f);
        Assert.Contains(Manifest.TransparentMaterials, t => t.Material == "M_Glass" && t.Opacity == 0.06f);
        Assert.Contains(Manifest.TransparentMaterials, t => t.Material == "M_CrtGlass" && t.Opacity == 0.14f);
        Assert.Contains(Manifest.TransparentMaterials, t => t.Material == "M_ScreenShade" && t.Opacity == 0.7f);
        Assert.Equal(0.6f, Manifest.ReflectionFactor("M_PaintNose"));
        Assert.Equal(1.4f, Manifest.ReflectionFactor("M_CrtGlass"));
        Assert.Equal(1.3f, Manifest.ReflectionFactor("M_Glass"));
        Assert.Equal(1f, Manifest.ReflectionFactor("M_Plate"));
        Assert.Contains("Screen_Speed", Manifest.NoShadow);
        Assert.Contains("Beacon_Boost_Lens", Manifest.NoShadow);
        Assert.Contains("Glass_Front", Manifest.NoShadow);
    }

    [Fact]
    public void Bounds_MatchTheAxesOfThePrototype()
    {
        Assert.InRange(Manifest.Bounds.Min[2], -2.98f, -2.96f);   // borde de ataque de la punta del canard
        Assert.InRange(Manifest.Bounds.Max[0], 2.50f, 2.52f);     // puntas del canard
        Assert.InRange(Manifest.Bounds.Min[1], -1.14f, -1.12f);   // puntas caídas
    }
}

public class Cockpit3DHammerProfileTests
{
    private const float ShipHover = 2f;   // Track.ShipHover: la nave vuela a 2 m de la pista

    [Fact]
    public void Hammer_UsesTheIndustrial3DCockpit_WithTheReflectionsOfBlade()
    {
        Cockpit3DProfile cockpit = ShipDefinitions.Find(ShipCatalog.Ship03)!.Visual.Cockpit3D!;
        Assert.Equal(Cockpit3DStyle.Industrial, cockpit.Style);
        Assert.EndsWith("cockpit_hammer.glb", cockpit.ModelPath);
        Assert.EndsWith("cockpit_hammer.manifest.json", cockpit.ManifestPath);
        Assert.EndsWith("cockpit_blade_env.png", cockpit.EnvironmentPath);
        Assert.Same(Cockpit3DLayouts.Industrial, cockpit.Layout);
    }

    [Fact]
    public void Hammer_EyeAt203m_Fov70_LookingDown49Degrees()
    {
        CockpitFraming framing = ShipDefinitions.Find(ShipCatalog.Ship03)!.Visual.Cockpit;
        Assert.Equal(2.03f, framing.Eye.Y + ShipHover, 4);
        Assert.Equal(-1.2f, framing.Eye.Z);
        Assert.Equal(70f, framing.Fov);
        Assert.Equal(-4.9f, framing.PitchDegrees, 1);
        Assert.Equal(-0.085f, framing.PitchRadians, 5);
    }

    [Fact]
    public void EachShipHasItsOwnStyle_AndNoneIs2D()
    {
        ShipVisualProfile vector = ShipDefinitions.Find(ShipCatalog.Ship01)!.Visual;
        ShipVisualProfile blade = ShipDefinitions.Find(ShipCatalog.Ship02)!.Visual;
        ShipVisualProfile hammer = ShipDefinitions.Find(ShipCatalog.Ship03)!.Visual;

        Assert.Equal(Cockpit3DStyle.Racing, vector.Cockpit3D!.Style);
        Assert.Equal(0f, vector.Cockpit.PitchDegrees);
        Assert.Equal(Cockpit3DStyle.Military, blade.Cockpit3D!.Style);
        Assert.Equal(0.90f, blade.Cockpit.Eye.Y + ShipHover, 4);
        Assert.Equal(-5.7f, blade.Cockpit.PitchDegrees, 1);
        Assert.Equal(Cockpit3DStyle.Industrial, hammer.Cockpit3D!.Style);
        Assert.All(new[] { vector, blade, hammer }, v => Assert.NotNull(v.Cockpit3D));
    }
}

public class Cockpit3DHammerViewRulesTests
{
    // Etapa 3: las pantallas llevan velocidad, boost, daño, radar y minimapa, y el HUD de pantalla ya no sale en cabina
    [Fact]
    public void Hammer_InCockpit_Shows3D_AndHidesTheScreenHud()
    {
        bool screensShowHud = Cockpit3DLayouts.Industrial.ScreensShowHud;
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Cockpit, screensShowHud);

        Assert.True(screensShowHud);
        Assert.True(rules.Cockpit3D);
        Assert.False(rules.ScreenDriveHud);
        Assert.False(rules.ScreenRadar);
    }

    [Fact]
    public void Hammer_Outside_ShowsOnlyTheScreenHud()
    {
        CockpitViewRules rules = CockpitViewRules.For(CameraView.Chase, Cockpit3DLayouts.Industrial.ScreensShowHud);
        Assert.False(rules.Cockpit3D);
        Assert.True(rules.ScreenDriveHud);
        Assert.True(rules.ScreenRadar);
    }

    // En cabina, la cámara del mundo y la de la cabina se inclinan lo mismo; la exterior no se inclina (ChaseCamera no
    // lee el encuadre de cabina)
    [Fact]
    public void Hammer_CockpitCameraPitch_IsTheWorldCameraPitch()
    {
        CockpitFraming framing = ShipDefinitions.Find(ShipCatalog.Ship03)!.Visual.Cockpit;
        CockpitCameraPose still = CockpitCameraMotion.Compute(default, default);

        CockpitCameraPose pitched = CockpitCameraMotion.Pitched(still, framing.PitchRadians);

        Assert.Equal(framing.PitchRadians, pitched.Rotation.X);
        Assert.Equal(0f, pitched.Rotation.Y);
        Assert.Equal(0f, pitched.Rotation.Z);
    }
}

public class Cockpit3DIndustrialLayoutTests
{
    private static Cockpit3DLayout Industrial => Cockpit3DLayouts.Industrial;

    [Fact]
    public void Industrial_HasTheFourScreensOfTheSpec_WithTheirPixels()
    {
        Cockpit3DScreen[] screens = Industrial.Screens.ToArray();

        Assert.Equal(new[] { "Screen_Radar", "Screen_Damage", "Screen_Map", "Screen_Speed" }, screens.Select(s => s.Piece));
        Assert.Equal(new[]
        {
            Cockpit3DScreenContent.IndustrialRadar, Cockpit3DScreenContent.IndustrialDamage,
            Cockpit3DScreenContent.IndustrialMap, Cockpit3DScreenContent.IndustrialSpeed
        }, screens.Select(s => s.Content));
        Assert.Equal((384, 294), (screens[0].Width, screens[0].Height));
        Assert.Equal((300, 300), (screens[1].Width, screens[1].Height));
        Assert.Equal((384, 294), (screens[2].Width, screens[2].Height));
        Assert.Equal((768, 144), (screens[3].Width, screens[3].Height));
        Assert.All(screens, s =>
        {
            Assert.False(s.Additive);
            Assert.Equal(Cockpit3DScreenLight.Large, s.Light);
            Assert.Equal(0.06f - 0.0015f, s.LightOffset, 6);
        });
        Assert.True(Industrial.BrightnessFollowsGlow);
        Assert.Equal(4, Industrial.Shades.Count);
        Assert.Equal(Cockpit3DFont.Vt323, Industrial.Font);
        Assert.Equal(Cockpit3DFont.Mono, Industrial.LabelsFont);
        Assert.Equal(Cockpit3DFont.Mono, Cockpit3DLayouts.Military.LabelsFont);
    }

    [Fact]
    public void Industrial_StatusLamps_AreTheThreeSetasAndTheTwoBeacons()
    {
        Assert.Equal(new[]
        {
            ("Lamp_Boost_1", Cockpit3DStatusLampKind.Seta, 0), ("Lamp_Boost_2", Cockpit3DStatusLampKind.Seta, 1),
            ("Lamp_Boost_3", Cockpit3DStatusLampKind.Seta, 2), ("Beacon_Boost_Lens", Cockpit3DStatusLampKind.BoostBeacon, 0),
            ("Beacon_Damage_Lens", Cockpit3DStatusLampKind.DamageBeacon, 0)
        }, Industrial.StatusLampList.Select(l => (l.Piece, l.Kind, l.Index)));
        Assert.All(Industrial.StatusLampList, l => Assert.DoesNotContain(l.Piece, Industrial.FlickerLights));
        Assert.Empty(Cockpit3DLayouts.Racing.StatusLampList);
        Assert.Empty(Cockpit3DLayouts.Military.StatusLampList);
    }

    // brillo × 0,03 × área / 0,017 en el prototipo, como BLADE, frente a brillo × 0,3 de las pantallas grandes de VECTOR
    [Theory]
    [InlineData("Screen_Radar", 0.17f * 0.13f)]
    [InlineData("Screen_Damage", 0.12f * 0.12f)]
    [InlineData("Screen_Speed", 0.40f * 0.075f)]
    public void Industrial_ScreenLight_FollowsTheAreaOfEachScreen(string piece, float area)
    {
        Cockpit3DScreen screen = Industrial.Screens.Single(s => s.Piece == piece);
        Assert.Equal(0.03f * area / 0.017f / 0.3f, screen.LightFactor, 5);
    }

    [Fact]
    public void Industrial_FlickersAccentsAndLeds_NeverScreensLampsOrBeacons()
    {
        IReadOnlyList<string> flicker = Industrial.FlickerLights;

        Assert.Equal(13, flicker.Count);   // 3 acentos, 4 balizas del morro y del canard, 6 LEDs del panel
        Assert.All(flicker, n => Assert.True(n.StartsWith("Accent_") || n.StartsWith("Led_"), n));
        Assert.All(flicker, n => Assert.Contains(n, IndustrialPieces.Contract));
        Assert.Equal(0, Industrial.ShiftLightCount);
    }

    [Fact]
    public void Industrial_LabelsAreThoseOfLabelPlane()
    {
        Cockpit3DLabel plate = Industrial.Labels.Single(l => l.Piece == "Plate");
        Assert.Equal("HAMMER · HMR-03", plate.Lines.Single().Text);
        Assert.Equal((256, 32, 20f, false), (plate.Width, plate.Height, plate.FontSize, plate.Bold));
        Assert.Equal(23f, plate.Lines.Single().Y, 4);   // la línea base de la placa de BLADE, con el mismo lienzo
        Assert.Equal(0x16130Du, plate.Background);
        Assert.Equal(0xCFC3A4u, plate.TextColor);
        Assert.Null(plate.LampColor);

        Cockpit3DLabel boost = Industrial.Labels.Single(l => l.Piece == "Label_Boost_2");
        Assert.Equal("BST 2", boost.Lines.Single().Text);
        Assert.Equal((256, 75, 47f), (boost.Width, boost.Height, boost.FontSize));   // 256 × 0,010 / 0,034 = 75,3; 75 × 0,62 = 46,5

        Cockpit3DLabel aux = Industrial.Labels.Single(l => l.Piece == "Label_Aux");
        Assert.Equal("E-STOP    IGN    MODE", aux.Lines.Single().Text);
        Assert.Equal((256, 17, 11f), (aux.Width, aux.Height, aux.FontSize));       // 256 × 0,011 / 0,17 = 16,6
        Assert.Equal(5, Industrial.Labels.Count);
    }

    [Fact]
    public void Industrial_GaugesAreDecorative_WithTheirFixedNeedles()
    {
        Assert.Equal(new[] { ("Gauge_Hyd_Face", "HYD", 0.62f), ("Gauge_Temp_Face", "TEMP", 0.38f) },
            Industrial.GaugeFaces.Select(g => (g.Piece, g.Label, g.Value)));
        Assert.Empty(Cockpit3DLayouts.Racing.GaugeFaces);
        Assert.Empty(Cockpit3DLayouts.Military.GaugeFaces);
    }

    [Fact]
    public void Industrial_SceneIsThatOfThePrototype()
    {
        Cockpit3DScene scene = Industrial.Scene;
        Assert.Equal(0xFFE2B8u, scene.KeyLight);
        Assert.Equal(0xFFB870u, scene.Lamp);
        Assert.Equal(0x9A8F80u, scene.HemisphereSky);
        Assert.Equal(0x15110Cu, scene.HemisphereGround);
        Assert.Equal(0x2C241Au, scene.Ambient);
        Assert.Equal(0xFFB000u, scene.ScreenLight);
        Assert.Equal(0xFF9D32u, scene.BoostLight);
        Assert.Equal(0xFF4D3Au, scene.DamageLight);
        Assert.Equal(1.2f, scene.DamageLightFactor);
        Assert.Equal(0.55f, scene.HemisphereStrength);
        Assert.Equal(0.5f, scene.AmbientStrength);
        // Ajustadas a ojo contra el prototipo, sobre las luces del Inspector (las de VECTOR)
        Assert.Equal(0.5f, scene.KeyFactor);
        Assert.Equal(3f, scene.LampFactor);
        Assert.Equal(Cockpit3DFont.Vt323, Industrial.Font);
    }

    // VECTOR y BLADE no cambian: sus luces son las del Inspector tal cual
    [Fact]
    public void RacingAndMilitary_KeepTheirLights()
    {
        foreach (Cockpit3DScene scene in new[] { Cockpit3DLayouts.Racing.Scene, Cockpit3DLayouts.Military.Scene })
        {
            Assert.Equal(scene.KeyLight, scene.Lamp);
            Assert.Equal(1f, scene.KeyFactor);
            Assert.Equal(1f, scene.LampFactor);
            Assert.Equal(0.6f, scene.HemisphereStrength);
            Assert.Equal(0.45f, scene.AmbientStrength);
        }
    }
}

public class IndustrialScreenDrawingTests
{
    [Fact]
    public void Base_IsBackgroundAndAScanLineEvery3Px()
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.Base(output, 384, 294);

        Assert.Equal(IndustrialPalette.Rgba(IndustrialPalette.ScreenBackground), output[0].Color);
        Assert.Equal(new Vector2(384f, 294f), output[0].B);
        Assert.Equal(98, output.Count(p => p.Kind == ScreenPrimitiveKind.FillRect && p.B.Y == 1f));
        Assert.All(output.Skip(1), p => Assert.Equal(IndustrialPalette.Rgba(IndustrialPalette.Main, 0.07f), p.Color));
    }

    [Fact]
    public void Corners_Are18PxAt8_AndOnTheSpeedStrip14At6()
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.Corners(output, 384, 294);
        Assert.Equal(8, output.Count);
        Assert.Equal(new Vector2(8f, 26f), output[0].A);
        Assert.Equal(new Vector2(8f, 8f), output[0].B);
        Assert.Equal(2.5f, output[0].Width);
        Assert.Equal(IndustrialPalette.Rgba(IndustrialPalette.Frame, 0.75f), output[0].Color);

        output.Clear();
        IndustrialScreenDrawing.Corners(output, 768, 144);
        Assert.Equal(new Vector2(6f, 20f), output[0].A);
        Assert.Equal(new Vector2(762f, 138f), output[^1].A);
    }

    [Fact]
    public void GaugeFace_IsThatOfMiniGauge()
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.GaugeFace(output, "HYD", 0.62f);

        Assert.Equal(new Vector2(128f, 128f), output[0].B);
        Assert.Equal(IndustrialPalette.Rgba(IndustrialPalette.GaugeFace), output[0].Color);
        Vector4 red = IndustrialPalette.Rgba(IndustrialPalette.GaugeRed);
        Assert.All(output.Where(p => p.Color == red), p => Assert.Equal(6f, p.Width));
        Assert.Equal(9, output.Count(p => p.Kind == ScreenPrimitiveKind.Line && p.Color == IndustrialPalette.Rgba(IndustrialPalette.Cream)));
        ScreenPrimitive text = output.Single(p => p.Kind == ScreenPrimitiveKind.Text);
        Assert.Equal(("HYD", new Vector2(64f, 104f), 20f), (text.Text, text.A, text.Radius));

        // Aguja de 4 px desde el centro hasta R − 10 en su ángulo
        ScreenPrimitive needle = output.Single(p => p.Kind == ScreenPrimitiveKind.Line && p.Width == 4f);
        float a = IndustrialScreenDrawing.GaugeAngle(0.62f);
        Assert.Equal(new Vector2(64f, 70f), needle.A);
        Assert.Equal(64f + MathF.Cos(a) * 42f, needle.B.X, 3);
        Assert.Equal(70f + MathF.Sin(a) * 42f, needle.B.Y, 3);
        Assert.Equal(IndustrialPalette.Rgba(IndustrialPalette.GaugeHub), output[^1].Color);
    }
}
