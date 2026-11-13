using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vectory.Radar;
using Vectory.Test.RadarTests;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cabina 3D de HAMMER, etapa 3 (spec docs/cabinas-3d-3-hammer.md, 8: pruebas 5 a 12): pintado industrial, tira de
// velocidad, casco, setas, balizas, radar y minimapa, progreso de la vuelta y efecto CRT
public class Cockpit3DHammerHudTests
{
    private const float Settled = 10f;   // más que cualquier transición

    private static Vector4 C(uint rgb, float alpha = 1f) => IndustrialPalette.Rgba(rgb, alpha);

    private static CockpitHudInput Input(float speed = 300f, bool boost = false, int charges = 0, bool cooldown = false, int damage = 0,
        bool damageEnabled = true) => new()
    {
        SpeedKmh = speed,
        TopKmh = 388f,
        MaxKmh = 488f,
        BoostActive = boost,
        Charges = charges,
        Cooldown = cooldown,
        DamageLevel = damage,
        DamageEnabled = damageEnabled
    };

    private static CockpitHudState State(CockpitHudInput input, float dt = Settled)
    {
        var state = new CockpitHudState(IndustrialPalette.Role, IndustrialPalette.Look);
        state.Update(input, CockpitLightSettings.Default, dt);
        return state;
    }

    // ---------- 5: pintado en industrial ----------

    [Theory]
    [InlineData(300f, false, IndustrialPalette.Main)]
    [InlineData(420f, false, IndustrialPalette.Over)]
    [InlineData(300f, true, IndustrialPalette.Over)]
    public void Speed_MainNormal_OverAboveTopOrWithBoost(float speed, bool boost, uint color)
    {
        Assert.Equal(C(color), State(Input(speed, boost)).SpeedColor);
    }

    [Theory]
    [InlineData(0, IndustrialPalette.Ok)]
    [InlineData(1, IndustrialPalette.Warn)]
    [InlineData(2, IndustrialPalette.Crit)]
    [InlineData(3, IndustrialPalette.Crit)]
    public void Damage_OkWarnCrit(int level, uint color)
    {
        Assert.Equal(C(color), State(Input(damage: level)).DamageTextColor);
    }

    [Fact]
    public void BoostText_CritLocked_OverActive_MainOtherwise()
    {
        Assert.Equal(C(IndustrialPalette.Crit), IndustrialScreenDrawing.BoostTextColor("LOCKED", false));
        Assert.Equal(C(IndustrialPalette.Over), IndustrialScreenDrawing.BoostTextColor("ACTIVE", true));
        Assert.Equal(C(IndustrialPalette.Main), IndustrialScreenDrawing.BoostTextColor("READY", false));
        Assert.Equal(C(IndustrialPalette.Main), IndustrialScreenDrawing.BoostTextColor("COOLDOWN", false));
    }

    // ---------- 6: tira de velocidad ----------

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.5f, 24)]
    [InlineData(1f, 48)]
    public void SpeedStrip_LitSegments(float fraction, int lit)
    {
        Assert.Equal(lit, IndustrialScreenDrawing.LitSegments(fraction));
    }

    [Fact]
    public void SpeedStrip_DrawsTheLitSegmentsInTheSpeedColor_AndTheTopMark()
    {
        CockpitHudState state = State(Input(244f));   // la mitad de 488
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.Speed(output, state, false, 0f);

        ScreenPrimitive[] segments = output.Where(p => p.Kind == ScreenPrimitiveKind.FillRect && p.A.Y == 104f).ToArray();
        Assert.Equal(48, segments.Length);
        Assert.Equal(24, segments.Count(p => p.Color == state.SpeedColor && p.Glow == 8f));
        Assert.Equal(24, segments.Count(p => p.Color == C(IndustrialPalette.Bar)));
        Assert.Equal(new Vector2(25.5f, 104f), segments[0].A);
        Assert.Equal(new Vector2(12f, 24f), segments[0].B);   // 15 px por segmento, menos 3 de separación

        ScreenPrimitive mark = output.Single(p => p.Kind == ScreenPrimitiveKind.Line);
        Assert.Equal(24f + 720f * 388f / 488f, mark.A.X, 3);
        Assert.Equal(3f, mark.Width);
        Assert.Equal(C(IndustrialPalette.Over), mark.Color);

        ScreenPrimitive number = output.Single(p => p.Kind == ScreenPrimitiveKind.Text && p.Radius == 112f);
        Assert.Equal(("244", new Vector2(610f, 92f), ScreenTextAlign.Right), (number.Text, number.A, number.Align));
    }

    // ---------- 7: casco ----------

    private static List<ScreenPrimitive> Damage(CockpitHudState state)
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.Damage(output, state, 0f);
        return output;
    }

    private static ScreenPrimitive[] Fills(List<ScreenPrimitive> output) =>
        output.Where(p => p.Kind == ScreenPrimitiveKind.FillRect && p.B == new Vector2(220f, 46f)).ToArray();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Hull_SectorsLitAsTheLevel_FromTheBottom(int level)
    {
        ScreenPrimitive[] fills = Fills(Damage(State(Input(damage: level))));
        Assert.Equal(level, fills.Length);
        for (int i = 0; i < level; i++)
            Assert.Equal(208f - i * 58f, fills[i].A.Y);
    }

    [Fact]
    public void Hull_BlinksOnlyAtLevelThree()
    {
        // Con 0,3 s de reloj el parpadeo está en su mitad apagada (de 0,25 a 0,5 s)
        CockpitHudState three = State(Input(damage: 3), 0.3f);
        CockpitHudState two = State(Input(damage: 2), 0.3f);
        Assert.False(three.BlinkOn);
        Assert.All(Fills(Damage(three)), p => Assert.Equal(CockpitHudState.BlinkDimOpacity, p.Color.W, 3));
        Assert.All(Fills(Damage(two)), p => Assert.Equal(1f, p.Color.W, 3));
    }

    [Fact]
    public void Hull_InTimeTrial_OffWithDimBorders_AndNoText()
    {
        List<ScreenPrimitive> output = Damage(State(Input(damage: 3, damageEnabled: false)));

        Assert.Empty(Fills(output));
        ScreenPrimitive[] borders = output.Where(p => p.Kind == ScreenPrimitiveKind.StrokeRect).ToArray();
        Assert.Equal(3, borders.Length);
        Assert.All(borders, p => Assert.Equal(C(IndustrialPalette.Dim, 0.6f), p.Color));
        Assert.DoesNotContain(output, p => p.Kind == ScreenPrimitiveKind.Text && p.A.Y == 288f);
    }

    [Fact]
    public void Hull_TextBelow_InTheDamageColor()
    {
        ScreenPrimitive text = Damage(State(Input(damage: 1))).Single(p => p.Kind == ScreenPrimitiveKind.Text && p.A.Y == 288f);
        Assert.Equal(("LIGHT", C(IndustrialPalette.Warn)), (text.Text, text.Color));
    }

    // ---------- 8: setas ----------

    private static readonly IndustrialLampSettings Lamps = IndustrialLampSettings.Default;

    [Fact]
    public void Setas_TableOfTheSpec()
    {
        // Llena y bloqueada, ajustadas contra el prototipo (1,7 y 2,0 en la spec)
        Assert.Equal(new IndustrialLamp(IndustrialPalette.Main, 1.25f), IndustrialLamps.Seta(0, 3, false, 0, true, Lamps));
        Assert.Equal(new IndustrialLamp(IndustrialPalette.Over, 0.6f), IndustrialLamps.Seta(0, 3, true, 0, true, Lamps));
        Assert.Equal(new IndustrialLamp(IndustrialPalette.Crit, 1.5f), IndustrialLamps.Seta(0, 3, false, 3, true, Lamps));
        Assert.Equal(new IndustrialLamp(IndustrialPalette.LampOff, 0.25f), IndustrialLamps.Seta(2, 2, false, 0, true, Lamps));
    }

    [Fact]
    public void Setas_PriorityBlockedCooldownFullEmpty()
    {
        // Bloqueada gana al cooldown; el cooldown, a la llena; sin carga, vacía aunque haya cooldown o daño 3
        Assert.Equal(IndustrialPalette.Crit, IndustrialLamps.Seta(0, 1, true, 3, true, Lamps).Color);
        Assert.Equal(IndustrialPalette.Over, IndustrialLamps.Seta(0, 1, true, 2, true, Lamps).Color);
        Assert.Equal(IndustrialPalette.LampOff, IndustrialLamps.Seta(1, 1, true, 3, true, Lamps).Color);
        // En TIME TRIAL no hay daño: no se bloquea
        Assert.Equal(IndustrialPalette.Main, IndustrialLamps.Seta(0, 1, false, 3, false, Lamps).Color);
    }

    // ---------- 9: balizas ----------

    [Fact]
    public void BoostBeacon_OnlyWithBoost_Beating()
    {
        Assert.Equal(0.05f, IndustrialLamps.BoostBeacon(false, 1f, Lamps));
        Assert.Equal(4f, IndustrialLamps.BoostBeacon(true, 0f, Lamps), 5);
        Assert.Equal(4f + 1.5f, IndustrialLamps.BoostBeacon(true, MathF.PI / 40f, Lamps), 4);   // sen(20 t) = 1
    }

    [Theory]
    [InlineData(0, true, true, 0.05f)]
    [InlineData(1, true, true, 0.05f)]
    [InlineData(2, true, false, 5f)]
    [InlineData(3, true, true, 5f)]
    [InlineData(3, true, false, 0.05f)]
    [InlineData(3, false, true, 0.05f)]
    [InlineData(2, false, true, 0.05f)]
    public void DamageBeacon_FixedAt2_BlinkingAt3_OffOtherwiseAndInTimeTrial(int level, bool enabled, bool blinkOn, float intensity)
    {
        Assert.Equal(intensity, IndustrialLamps.DamageBeacon(level, enabled, blinkOn, Lamps));
    }

    // ---------- 11: radar y minimapa en industrial ----------

    private static RaceShip Ship(int id, float x, float z) => new(id, new Vector3(x, 0f, z), -Vector3.UnitZ, Vector3.UnitY);

    [Fact]
    public void Radar_IndustrialStyle_SizesOfTheSpec()
    {
        RadarStyle style = RadarStyle.IndustrialScreen(IndustrialPalette.Radar());
        Assert.Equal(new Vector2(192f, 158f), style.Center);
        Assert.Equal(112f, style.Radius);
        Assert.Equal(6.05f, style.EchoRadius, 3);
        Assert.Equal(7.7f, style.EchoCloseRadius, 3);
        Assert.Equal(12.1f, style.SelfSize, 3);
        Assert.Equal(7f, style.WarningWidth);
        Assert.Equal(8f, style.WarningOffset);
        Assert.True(style.SquareEchoes);
        Assert.False(RadarStyle.MilitaryScreen(MilitaryPalette.Radar()).SquareEchoes);
    }

    [Fact]
    public void Radar_IndustrialPalette_ColorsOfTheSpec()
    {
        RadarPalette palette = IndustrialPalette.Radar();
        Vector3 Rgb(uint c) => new(C(c).X, C(c).Y, C(c).Z);
        Assert.Equal(Rgb(IndustrialPalette.Dim), palette.Rings);
        Assert.Equal(Rgb(IndustrialPalette.Main), palette.Beam);
        Assert.Equal(Rgb(IndustrialPalette.Rival), palette.Rival);
        Assert.Equal(Rgb(IndustrialPalette.Crit), palette.Close);
        Assert.Equal(Rgb(IndustrialPalette.Label), palette.Border);
        Assert.Equal(Rgb(IndustrialPalette.Label), palette.Band);
        Assert.Equal(Rgb(IndustrialPalette.Main), palette.Track);
        Assert.Equal(Rgb(IndustrialPalette.Crit), palette.Finish);
    }

    [Fact]
    public void Radar_EchoesAreSquares_AtTheScaledPositions()
    {
        var sweep = new RadarSweep();
        sweep.Update(0.5f, Vector3.Zero, new Vector2(0f, -1f), new[] { Ship(1, 60f, 0f) });   // el haz pasa por la derecha
        RadarStyle style = RadarStyle.IndustrialScreen(IndustrialPalette.Radar());
        var output = new List<RadarPrimitive>();
        RadarDrawing.Live(output, style, sweep);

        RadarPrimitive echo = output.Single(p => p.Kind == RadarPrimitiveKind.Square);
        Assert.Equal(192f + 60f / 120f * 112f, echo.A.X, 3);
        Assert.Equal(158f, echo.A.Y, 3);
        Assert.Equal(6.05f, echo.Radius, 3);
        Assert.Equal(new Vector4(new Vector3(C(IndustrialPalette.Rival).X, C(IndustrialPalette.Rival).Y, C(IndustrialPalette.Rival).Z), 1f),
            echo.Color);
        Assert.DoesNotContain(output, p => p.Kind == RadarPrimitiveKind.Disc);
    }

    [Fact]
    public void Radar_Targets_OnlyWithin120m_AndTheNearest()
    {
        var sweep = new RadarSweep();
        sweep.Update(0.016f, Vector3.Zero, new Vector2(0f, -1f), new[] { Ship(1, 0f, -50f), Ship(2, 30f, -90f), Ship(3, 0f, -130f) });
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.RadarLive(output, sweep.TargetCount, sweep.NearestDistance, sweep.NearestIsClose, 0f);

        Assert.Equal(2, sweep.TargetCount);
        ScreenPrimitive[] texts = output.Where(p => p.Kind == ScreenPrimitiveKind.Text).ToArray();
        Assert.Equal("TGT 02", texts[0].Text);
        Assert.Equal(("050 M", C(IndustrialPalette.Rival)), (texts[1].Text, texts[1].Color));

        output.Clear();
        IndustrialScreenDrawing.RadarLive(output, 1, 8f, true, 0f);
        Assert.Equal(C(IndustrialPalette.Crit), output.Where(p => p.Kind == ScreenPrimitiveKind.Text).ElementAt(1).Color);

        output.Clear();
        IndustrialScreenDrawing.RadarLive(output, 0, null, false, 0f);
        Assert.Equal("---", output.Where(p => p.Kind == ScreenPrimitiveKind.Text).ElementAt(1).Text);
    }

    [Fact]
    public void Minimap_IndustrialStyle_WindowOfTheSpec_WithSquareShips()
    {
        MinimapStyle style = MinimapStyle.IndustrialScreen(IndustrialPalette.Radar());
        Assert.Equal(new Vector2(192f, 146f), style.Center);
        Assert.Equal(104f, style.Radius);
        Assert.Equal(12f, style.BandWidth);
        Assert.Equal(5f, style.ShipRadius);
        Assert.True(style.SquareShips);

        var picture = new MinimapPicture();
        MinimapDrawing.Live(picture, style, new MinimapPath(TestTrack.Circle(), 40f), new Vector2(500f, 0f), new Vector2(0f, -1f), 450f,
            new[] { Ship(1, 500f, -100f) });
        RadarPrimitive ship = picture.Primitives.Single(p => p.Kind == RadarPrimitiveKind.Square);
        Assert.Equal(192f, ship.A.X, 3);
        Assert.Equal(146f - 100f / 450f * 104f, ship.A.Y, 3);
        Assert.Equal(5f, ship.Radius);
    }

    // ---------- 12: progreso de la vuelta ----------

    [Theory]
    [InlineData(0f, 0f, "0 %")]
    [InlineData(0.5f, 88f, "50 %")]
    [InlineData(1f, 176f, "100 %")]
    public void LapProgress_BarAndPercent(float fraction, float width, string percent)
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.MapLive(output, "LAP 2/3", fraction, 0f);

        ScreenPrimitive[] bars = output.Where(p => p.Kind == ScreenPrimitiveKind.FillRect && p.A.Y == 262f).ToArray();
        Assert.Equal(new Vector2(176f, 10f), bars[0].B);
        Assert.Equal(width, bars.Length > 1 ? bars[1].B.X : 0f, 3);
        ScreenPrimitive[] texts = output.Where(p => p.Kind == ScreenPrimitiveKind.Text).ToArray();
        Assert.Equal("LAP 2/3", texts[0].Text);
        Assert.Equal(percent, texts[1].Text);
    }

    // ---------- Efecto CRT (6.2) ----------

    [Fact]
    public void Crt_BandFallsAt60PxPerSecond_AndStartsAgainAtTheTop()
    {
        Assert.Equal(0f, IndustrialScreenDrawing.BandY(0f, 294));
        Assert.Equal(60f, IndustrialScreenDrawing.BandY(1f, 294), 3);
        Assert.Equal(6f, IndustrialScreenDrawing.BandY(5f, 294), 3);   // 300 − 294
    }

    [Fact]
    public void Crt_Vignette_ClearInTheMiddle_DarkAtTheOuterRadius()
    {
        Assert.Equal(0f, IndustrialScreenDrawing.VignetteAlpha(192f, 147f, 384f, 294f));
        // De 0,3 × 294 = 88,2 px a 0,62 × 384 = 238,08 px del centro
        Assert.Equal(0.65f * (150f - 88.2f) / (238.08f - 88.2f), IndustrialScreenDrawing.VignetteAlpha(192f + 150f, 147f, 384f, 294f), 3);
        Assert.Equal(0.65f, IndustrialScreenDrawing.VignetteAlpha(-200f, 147f, 384f, 294f), 3);
    }

    [Fact]
    public void Crt_OnEveryScreen_AtTheEnd()
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.MapLive(output, "", 0.3f, 0.5f);

        Assert.Equal(ScreenPrimitiveKind.Vignette, output[^2].Kind);
        Assert.Equal(new Vector2(384f, 294f), output[^2].B);
        Assert.Equal(C(0x000000, 0.65f), output[^2].Color);
        Assert.Equal(new Vector2(0f, 30f), output[^1].A);   // 0,5 s × 60 px/s
        Assert.Equal(C(IndustrialPalette.Main, 0.05f), output[^1].Color);
    }

    [Fact]
    public void Corners_Are18PxAt8_AndOnTheSpeedStrip14At6()
    {
        var output = new List<ScreenPrimitive>();
        IndustrialScreenDrawing.Corners(output, 384, 294);
        Assert.Equal(8, output.Count);
        Assert.Equal(new Vector2(8f, 26f), output[0].A);
        Assert.Equal(2.5f, output[0].Width);
        Assert.Equal(C(IndustrialPalette.Frame, 0.75f), output[0].Color);

        output.Clear();
        IndustrialScreenDrawing.Corners(output, 768, 144);
        Assert.Equal(new Vector2(6f, 20f), output[0].A);
        Assert.Equal(new Vector2(762f, 138f), output[^1].A);
    }
}
