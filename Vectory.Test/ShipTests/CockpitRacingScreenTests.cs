using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vectory.Radar;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class ShiftLightsTests
{
    private const float Top = 396f;

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.55f * Top, 0)]
    [InlineData(Top, 15)]
    [InlineData(0.775f * Top, 8)]   // n = 7,5: las mitades, hacia arriba
    public void LitCount_FollowsTheFormula(float speed, int lit)
    {
        Assert.Equal(lit, ShiftLights.LitCount(speed, Top));
    }

    [Fact]
    public void Colors_FiveGreen_FiveRed_FiveBlue_LeftToRight()
    {
        Assert.All(Enumerable.Range(0, 5), i => Assert.Equal(RacingPalette.ShiftGreen, ShiftLights.BaseColor(i)));
        Assert.All(Enumerable.Range(5, 5), i => Assert.Equal(RacingPalette.ShiftRed, ShiftLights.BaseColor(i)));
        Assert.All(Enumerable.Range(10, 5), i => Assert.Equal(RacingPalette.ShiftBlue, ShiftLights.BaseColor(i)));
    }

    [Fact]
    public void Normal_TheFirstNAreLit_TheRestOff()
    {
        float speed = 0.775f * Top;
        Assert.Equal(new ShiftLight(RacingPalette.ShiftRed, 3.5f), ShiftLights.Light(7, speed, Top, false, 0f));
        Assert.Equal(new ShiftLight(RacingPalette.ShiftRed, 0f), ShiftLights.Light(8, speed, Top, false, 0f));
    }

    [Theory]
    [InlineData(0.05f, 4f)]
    [InlineData(0.15f, 0f)]
    [InlineData(0.25f, 4f)]
    [InlineData(0.35f, 0f)]
    public void OverTheTop_All15Blue_OnInTheFirstHalfOfEvery02s(float time, float intensity)
    {
        for (int i = 0; i < ShiftLights.Count; i++)
            Assert.Equal(new ShiftLight(RacingPalette.ShiftBlue, intensity), ShiftLights.Light(i, Top, Top, true, time));
    }
}

public class CockpitRacingPaintTests
{
    private static CockpitHudInput Input(float speed = 100f, int charges = 3, bool cooldown = false, int damage = 0, bool damageEnabled = true, bool boost = false) => new()
    {
        SpeedKmh = speed,
        TopKmh = 396f,
        MaxKmh = 496f,
        Charges = charges,
        Cooldown = cooldown,
        DamageLevel = damage,
        DamageEnabled = damageEnabled,
        BoostActive = boost
    };

    private static CockpitHudState State(in CockpitHudInput input)
    {
        var state = new CockpitHudState(RacingPalette.Role, RacingPalette.Look);
        state.Update(input, CockpitLightSettings.Default, 0.016f);
        return state;
    }

    private static Vector4 C(uint rgb, float a = 1f) => RacingPalette.Rgba(rgb, a);

    [Fact]
    public void SpeedColor_NormalWhite_OverTopOrBoostOrange()
    {
        Assert.Equal(C(RacingPalette.Main), State(Input(100f)).SpeedColor);
        Assert.Equal(C(RacingPalette.Over), State(Input(398f)).SpeedColor);
        Assert.Equal(C(RacingPalette.Over), State(Input(100f, boost: true)).SpeedColor);
    }

    [Fact]
    public void Orb_Full_FullFillStrokeAndHalo()
    {
        CockpitHudPaint orb = State(Input()).Orb(0);
        Assert.Equal(C(RacingPalette.Orb), orb.Fill);
        Assert.Equal(C(RacingPalette.Orb), orb.Stroke);
        Assert.Equal(1f, orb.Glow);
    }

    [Fact]
    public void Orb_Cooldown_DimmedWithoutHalo()
    {
        CockpitHudPaint orb = State(Input(cooldown: true)).Orb(0);
        Assert.Equal(C(RacingPalette.Orb, 0.3f), orb.Fill);
        Assert.Equal(C(RacingPalette.Orb, 0.55f), orb.Stroke);
        Assert.Equal(0f, orb.Glow);
    }

    [Fact]
    public void Orb_Locked_GoesBeforeCooldown()
    {
        CockpitHudPaint orb = State(Input(cooldown: true, damage: 3)).Orb(0);
        Assert.Equal(C(RacingPalette.Crit, 0.3f), orb.Fill);
        Assert.Equal(C(RacingPalette.Crit, 0.55f), orb.Stroke);
        Assert.Equal(0f, orb.Glow);
    }

    [Fact]
    public void Orb_Empty_UsesTheColorOfThisStyle()
    {
        CockpitHudPaint orb = State(Input(charges: 0)).Orb(0);
        Assert.Equal(C(RacingPalette.OrbOff, 0.5f), orb.Fill);
        Assert.Equal(C(RacingPalette.OrbOff, 0.9f), orb.Stroke);
    }

    [Theory]
    [InlineData(0, RacingPalette.Ok)]
    [InlineData(1, RacingPalette.Warn)]
    [InlineData(2, RacingPalette.Crit)]
    [InlineData(3, RacingPalette.Crit)]
    public void DamageTextColor_ByLevel(int level, uint rgb)
    {
        Assert.Equal(C(rgb), State(Input(damage: level)).DamageTextColor);
    }

    [Fact]
    public void Segment_Off_LabelBorder_NoFill()
    {
        CockpitHudPaint segment = State(Input()).Segment(0);
        Assert.Equal(C(RacingPalette.Label, 0.55f), segment.Stroke);
        Assert.Equal(0f, segment.Fill.W);
    }

    [Fact]
    public void Segment_On_FilledWithTheColorOfTheLevel()
    {
        CockpitHudPaint segment = State(Input(damage: 1)).Segment(0);
        Assert.Equal(C(RacingPalette.Warn), segment.Fill);
        Assert.Equal(C(RacingPalette.Warn), segment.Stroke);
    }

    [Fact]
    public void TimeTrial_SegmentsOff_AndNoDamageText()
    {
        CockpitHudState state = State(Input(damage: 3, damageEnabled: false));
        Assert.Equal("", state.DamageText);
        Assert.Equal(0f, state.Segment(0).Fill.W);
        Assert.False(state.SegmentsBlinking);
    }

    [Fact]
    public void Bar_WidthTimesFraction_MarkAtTopOverMax()
    {
        var output = new List<ScreenPrimitive>();
        RacingScreenDrawing.Right(output, State(Input(248f)));

        ScreenPrimitive[] bars = output.Where(p => p.Kind == ScreenPrimitiveKind.FillRect && p.A.Y == RacingScreenDrawing.BarY).ToArray();
        Assert.Equal(RacingScreenDrawing.BarWidth, bars[0].B.X, 3);
        Assert.Equal(RacingPalette.Rgba(RacingPalette.Bar), bars[0].Color);
        Assert.Equal(RacingScreenDrawing.BarWidth * 0.5f, bars[1].B.X, 3);

        ScreenPrimitive mark = output.Last();
        Assert.Equal(ScreenPrimitiveKind.Line, mark.Kind);
        Assert.Equal(RacingScreenDrawing.BarX + RacingScreenDrawing.BarWidth * 396f / 496f, mark.A.X, 3);
        Assert.Equal(C(RacingPalette.Over, 0.9f), mark.Color);
    }

    [Fact]
    public void Right_ElevenTicksAndTheTextsOfThePrototype()
    {
        var output = new List<ScreenPrimitive>();
        RacingScreenDrawing.Right(output, State(Input(248f)));

        Assert.Equal(12, output.Count(p => p.Kind == ScreenPrimitiveKind.Line));
        string[] texts = output.Where(p => p.Kind == ScreenPrimitiveKind.Text).Select(p => p.Text).ToArray();
        Assert.Equal(new[] { "BOOST", "READY", "248", "KM/H" }, texts);
        Assert.Equal(101f, output.Single(p => p.Text == "248").Radius);   // 88 × 1,15
    }

    [Fact]
    public void Left_ThreeSegmentsOf150By44_AndTheTexts()
    {
        var output = new List<ScreenPrimitive>();
        RacingScreenDrawing.Left(output, State(Input(damage: 2)));

        ScreenPrimitive[] borders = output.Where(p => p.Kind == ScreenPrimitiveKind.StrokeRect).ToArray();
        Assert.Equal(3, borders.Length);
        Assert.All(borders, b => Assert.Equal(new Vector2(150f, 44f), b.B));
        Assert.Equal(2, output.Count(p => p.Kind == ScreenPrimitiveKind.FillRect));
        Assert.Contains(output, p => p.Text == "HEAVY");
        Assert.Contains(output, p => p.Text == "INTEGRITY");
    }

    [Fact]
    public void Base_ScanLinesEvery3Px_AndFourCorners()
    {
        var output = new List<ScreenPrimitive>();
        RacingScreenDrawing.Base(output, 256, 256);

        Assert.Equal(1 + 86, output.Count(p => p.Kind == ScreenPrimitiveKind.FillRect));
        Assert.Equal(8, output.Count(p => p.Kind == ScreenPrimitiveKind.Line));
        Assert.Equal(C(RacingPalette.ScreenBackground), output[0].Color);
    }

    [Theory]
    [InlineData(24f, 28f)]
    [InlineData(26f, 30f)]
    [InlineData(22f, 25f)]
    [InlineData(88f, 101f)]
    public void FontSize_Prototype_Times115(float size, float expected)
    {
        Assert.Equal(expected, RacingScreenDrawing.FontSize(size));
    }
}

public class CockpitLightMixTests
{
    private static readonly CockpitLightIntensities One = new(1f, 1f, 1f);

    private static CockpitLightMix Settled(bool boost, bool overTop)
    {
        var mix = new CockpitLightMix();
        for (int i = 0; i < 200; i++)
            mix.Update(boost, overTop, 0.016f);
        return mix;
    }

    [Fact]
    public void Tint_GoesTo055WithBoost_025OverTop_0Otherwise()
    {
        Assert.Equal(0.55f, Settled(true, true).BoostTint, 3);
        Assert.Equal(0.25f, Settled(false, true).BoostTint, 3);
        Assert.Equal(0f, Settled(false, false).BoostTint, 3);
    }

    [Fact]
    public void Tint_ArrivesInAbout01s()
    {
        var mix = new CockpitLightMix();
        for (int i = 0; i < 18; i++)
            mix.Update(true, true, 1f / 60f);
        Assert.InRange(mix.BoostTint, 0.5f, 0.55f);
    }

    [Fact]
    public void Values_FollowTheDesign_TimesTheirIntensity()
    {
        CockpitLightValues values = Settled(true, true).Values(0.45f, new CockpitLightIntensities(0.5f, 2f, 3f));
        Assert.Equal(0.55f * 3.2f * 0.5f, values.BoostFront, 2);
        Assert.Equal(0.55f * 2f * 2f, values.BoostRear, 2);
        Assert.Equal(0.45f * 3f * 3f, values.Damage, 3);
    }

    [Fact]
    public void AtZero_TheyGoOut()
    {
        CockpitLightValues values = Settled(true, true).Values(0.45f, new CockpitLightIntensities(0f, 0f, 0f));
        Assert.Equal(default, values);
    }

    [Fact]
    public void DamageTint_Level2Fixed_Level3Pulsing_TimeTrialNever()
    {
        CockpitHudInput input = new() { SpeedKmh = 0f, TopKmh = 396f, MaxKmh = 496f, DamageLevel = 2, DamageEnabled = true };
        var state = new CockpitHudState(RacingPalette.Role, RacingPalette.Look);
        state.Update(input, CockpitLightSettings.Default, 0.016f);
        Assert.Equal(0.15f, state.DamageTint);

        input.DamageLevel = 3;
        var seen = new List<float>();
        for (int i = 0; i < 100; i++)
        {
            state.Update(input, CockpitLightSettings.Default, 0.02f);
            seen.Add(state.DamageTint);
        }
        Assert.InRange(seen.Max(), 0.44f, 0.451f);
        Assert.InRange(seen.Min(), 0.199f, 0.21f);

        input.DamageEnabled = false;
        state.Update(input, CockpitLightSettings.Default, 0.02f);
        Assert.Equal(0f, new CockpitLightMix().Values(state.DamageTint, One).Damage);
    }
}

public class Cockpit3DFlickerTests
{
    [Fact]
    public void Flicker_OnlyChoosesBetweenAccentsAndLeds()
    {
        Assert.Equal(11, Cockpit3DPieces.FlickerLights.Count);
        Assert.All(Cockpit3DPieces.FlickerLights, n => Assert.True(n.StartsWith("Accent_") || n.StartsWith("Led_"), n));

        var flicker = new EdgeFlicker(Cockpit3DPieces.FlickerLights.Count, EdgeFlickerSettings.Default, new Random(3));
        flicker.SetLevel(3);
        Assert.NotEmpty(flicker.AffectedEdges);
        Assert.All(flicker.AffectedEdges, e => Assert.InRange(e, 0, Cockpit3DPieces.FlickerLights.Count - 1));
    }
}

public class CockpitRadarScreenTests
{
    private const float Step = 0.01f;
    private static readonly Vector2 North = new(0f, -1f);

    private static RadarPrimitive[] Live(RadarStyle style, RadarSweep sweep)
    {
        var output = new List<RadarPrimitive>();
        RadarDrawing.Live(output, style, sweep);
        return output.ToArray();
    }

    private static RadarSweep SweepWithEcho()
    {
        var sweep = new RadarSweep();
        var ship = new[] { new RaceShip(1, new Vector3(30f, 0f, -40f), -Vector3.UnitZ, Vector3.UnitY) };
        for (int i = 0; i < 200; i++)
            sweep.Update(Step, Vector3.Zero, North, ship);
        Assert.NotEmpty(sweep.Echoes);
        return sweep;
    }

    [Fact]
    public void Radar_256Px_SamePositionsScaled_AsTheScreenHud()
    {
        RadarSweep sweep = SweepWithEcho();
        RadarPalette colors = RacingPalette.Radar();
        RadarStyle cockpit = RadarStyle.CockpitScreen(colors);
        RadarStyle hud = RadarStyle.Hud(colors, 50f, 1f);

        RadarPrimitive echo = Live(cockpit, sweep).First(p => p.Kind == RadarPrimitiveKind.Disc);
        RadarPrimitive other = Live(hud, sweep).First(p => p.Kind == RadarPrimitiveKind.Disc);

        Vector2 scaled = other.A / hud.Radius * cockpit.Radius + cockpit.Center;
        Assert.Equal(scaled.X, echo.A.X, 2);
        Assert.Equal(scaled.Y, echo.A.Y, 2);
    }

    [Fact]
    public void Radar_MeasuresAndColorsOfTheSpec()
    {
        RadarSweep sweep = SweepWithEcho();
        RadarStyle style = RadarStyle.CockpitScreen(RacingPalette.Radar());

        Assert.Equal(new Vector2(128f, 122f), style.Center);
        Assert.Equal(94f, style.Radius);

        RadarPrimitive[] live = Live(style, sweep);
        RadarPrimitive echo = live.First(p => p.Kind == RadarPrimitiveKind.Disc);
        Assert.Equal(5.5f, echo.Radius);
        Assert.Equal(RacingPalette.Rgba(RacingPalette.Rival).X, echo.Color.X);
        Assert.Equal(11f, live.Single(p => p.Kind == RadarPrimitiveKind.Arrow).Radius);
        Assert.Equal(28, live.Count(p => p.Kind == RadarPrimitiveKind.Line));
    }

    [Fact]
    public void Radar_Close_RedArcOutsideTheBorder()
    {
        var sweep = new RadarSweep();
        sweep.Update(Step, Vector3.Zero, North, new[] { new RaceShip(1, new Vector3(6f, 0f, 0f), -Vector3.UnitZ, Vector3.UnitY) });
        RadarStyle style = RadarStyle.CockpitScreen(RacingPalette.Radar());

        RadarPrimitive arc = Live(style, sweep).Single(p => p.Kind == RadarPrimitiveKind.Arc);
        Assert.Equal(102f, arc.Radius);
        Assert.Equal(7f, arc.Width);
        Assert.Equal(RacingPalette.Rgba(RacingPalette.Crit).X, arc.Color.X);
    }

    [Fact]
    public void Radar_LabelCenteredBelow()
    {
        var output = new List<RadarPrimitive>();
        RadarDrawing.Base(output, RadarStyle.CockpitScreen(RacingPalette.Radar()), 120f);

        RadarPrimitive label = output.Single(p => p.Kind == RadarPrimitiveKind.CenteredText);
        Assert.Equal("120 M", label.Text);
        Assert.Equal(new Vector2(128f, 244f), label.A);
        Assert.Equal(21f, label.Radius);
        Assert.DoesNotContain(output, p => p.Kind == RadarPrimitiveKind.Disc);
    }

    [Fact]
    public void Minimap_256Px_SamePositionsScaled_AsTheScreenHud()
    {
        RadarPalette colors = RacingPalette.Radar();
        MinimapStyle cockpit = MinimapStyle.CockpitScreen(colors);
        MinimapStyle hud = MinimapStyle.Hud(colors, 50f, 1f);
        var self = new Vector2(10f, 20f);

        var a = new MinimapProjection(self, North, cockpit.Center, cockpit.Radius / 450f);
        var b = new MinimapProjection(self, North, hud.Center, hud.Radius / 450f);
        var world = new Vector2(150f, -200f);

        Vector2 scaled = b.ToScreen(world) / hud.Radius * cockpit.Radius + cockpit.Center;
        Assert.Equal(scaled.X, a.ToScreen(world).X, 2);
        Assert.Equal(scaled.Y, a.ToScreen(world).Y, 2);
    }

    [Fact]
    public void Minimap_MeasuresOfTheSpec()
    {
        MinimapStyle style = MinimapStyle.CockpitScreen(RacingPalette.Radar());
        Assert.Equal(new Vector2(128f, 128f), style.Center);
        Assert.Equal(106f, style.Radius);
        Assert.Equal(14f, style.BandWidth);
        Assert.Equal(0.35f, style.BandOpacity);
        Assert.Equal(3f, style.LineWidth);
        Assert.Equal(0.9f, style.LineOpacity);
        Assert.Equal(4f, style.FinishWidth);
        Assert.Equal(6f, style.ShipRadius);
        Assert.Equal(11f, style.SelfSize);
        Assert.Equal(0f, style.UpMarkWidth);
    }
}
