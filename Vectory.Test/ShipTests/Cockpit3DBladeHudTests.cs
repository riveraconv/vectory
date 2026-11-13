using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vectory.Radar;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cabina 3D de BLADE, etapa 3: HUD y luces vivas (spec docs/cabinas-3d-2-blade.md, 8: pruebas 5 a 12)
public class Cockpit3DBladeHudTests
{
    private const float Top = 412f;     // punta de BLADE
    private const float Max = Top + 100f;

    private static CockpitHudInput Input(float speed = 200f, int charges = 3, bool cooldown = false, int damage = 0,
        bool damageEnabled = true, bool boost = false) => new()
    {
        SpeedKmh = speed,
        TopKmh = Top,
        MaxKmh = Max,
        BoostActive = boost,
        Charges = charges,
        Cooldown = cooldown,
        DamageLevel = damage,
        DamageEnabled = damageEnabled,
        LapText = "LAP 1/3"
    };

    // El primer Update deja cada color en su valor, sin transición
    private static CockpitHudState State(in CockpitHudInput input, float time = 0f)
    {
        var state = new CockpitHudState(MilitaryPalette.Role, MilitaryPalette.Look);
        state.Update(input, CockpitLightSettings.Default, time);
        return state;
    }

    private static Vector4 Rgba(uint rgb, float alpha = 1f) => MilitaryPalette.Rgba(rgb, alpha);

    // ---------- 5 · Pintado en militar (tabla 6.1) ----------

    [Fact]
    public void Orbs_Full_AreMainWithHalo()
    {
        CockpitHudPaint orb = State(Input(charges: 3)).Orb(0);
        Assert.Equal(Rgba(MilitaryPalette.Main), orb.Fill);
        Assert.Equal(Rgba(MilitaryPalette.Main), orb.Stroke);
        Assert.Equal(1f, orb.Glow);
    }

    [Fact]
    public void Orbs_Cooldown_AreOverAt30And55_WithoutHalo()
    {
        CockpitHudPaint orb = State(Input(charges: 3, cooldown: true)).Orb(0);
        Assert.Equal(Rgba(MilitaryPalette.Over, 0.3f), orb.Fill);
        Assert.Equal(Rgba(MilitaryPalette.Over, 0.55f), orb.Stroke);
        Assert.Equal(0f, orb.Glow);
    }

    // Bloqueada va antes que cooldown: con daño 3 manda el rojo
    [Fact]
    public void Orbs_Locked_AreCritAt30And55_EvenInCooldown()
    {
        CockpitHudPaint orb = State(Input(charges: 3, cooldown: true, damage: 3)).Orb(0);
        Assert.Equal(Rgba(MilitaryPalette.Crit, 0.3f), orb.Fill);
        Assert.Equal(Rgba(MilitaryPalette.Crit, 0.55f), orb.Stroke);
    }

    [Fact]
    public void Orbs_Empty_AreOrbOffAt50And90()
    {
        CockpitHudPaint orb = State(Input(charges: 1)).Orb(2);
        Assert.Equal(Rgba(MilitaryPalette.OrbOff, 0.5f), orb.Fill);
        Assert.Equal(Rgba(MilitaryPalette.OrbOff, 0.9f), orb.Stroke);
    }

    [Theory]
    [InlineData(0, MilitaryPalette.Ok)]
    [InlineData(1, MilitaryPalette.Warn)]
    [InlineData(2, MilitaryPalette.Crit)]
    [InlineData(3, MilitaryPalette.Crit)]
    public void Damage_ColorOfEachLevel(int level, uint color)
    {
        Assert.Equal(Rgba(color), State(Input(damage: level)).DamageTextColor);
    }

    [Fact]
    public void Speed_IsMain_AndOverAboveTopOrWithBoost()
    {
        Assert.Equal(Rgba(MilitaryPalette.Main), State(Input(speed: 300f)).SpeedColor);
        Assert.Equal(Rgba(MilitaryPalette.Over), State(Input(speed: 420f)).SpeedColor);
        Assert.Equal(Rgba(MilitaryPalette.Over), State(Input(speed: 300f, boost: true)).SpeedColor);
    }

    [Fact]
    public void BoostText_CritWhenLocked_OverWithBoost_MainOtherwise()
    {
        Assert.Equal(Rgba(MilitaryPalette.Crit), MilitaryScreenDrawing.BoostTextColor(State(Input(damage: 3))));
        Assert.Equal(Rgba(MilitaryPalette.Over), MilitaryScreenDrawing.BoostTextColor(State(Input(boost: true))));
        Assert.Equal(Rgba(MilitaryPalette.Main), MilitaryScreenDrawing.BoostTextColor(State(Input())));
    }

    // VECTOR no cambia: sus cargas llenas y en cooldown siguen en naranja
    [Fact]
    public void Racing_KeepsItsOrangeOrbs()
    {
        var state = new CockpitHudState(RacingPalette.Role, RacingPalette.Look);
        state.Update(Input(charges: 3), CockpitLightSettings.Default, 0f);
        Assert.Equal(RacingPalette.Rgba(RacingPalette.Orb), state.Orb(0).Fill);
    }

    // ---------- 6 · Escala de velocidad ----------

    [Theory]
    [InlineData(0f, 336f)]
    [InlineData(0.5f, 233f)]
    [InlineData(1f, 130f)]
    public void Scale_FillsHeightTimesF(float fraction, float top)
    {
        Assert.Equal(top, MilitaryScreenDrawing.ScaleFillTop(fraction), 3);
    }

    [Fact]
    public void Scale_TopLineAtTopOverMax()
    {
        CockpitHudState state = State(Input());
        float expected = 336f - 206f * Top / Max;
        Assert.Equal(expected, MilitaryScreenDrawing.ScaleTopLine(state.TopFraction), 3);

        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Speed(output, state);
        ScreenPrimitive line = output.Single(p => p.Kind == ScreenPrimitiveKind.Line && p.Color == Rgba(MilitaryPalette.Over));
        Assert.Equal(expected, line.A.Y, 3);
        Assert.Equal(3f, line.Width);
    }

    // ---------- 7 · Cinta del cristal ----------

    [Fact]
    public void Tape_At300_Mark300InTheCenter_And350_85pxAbove()
    {
        Assert.Equal(157f, MilitaryScreenDrawing.SpeedTapeY(300f, 300f));
        Assert.Equal(72f, MilitaryScreenDrawing.SpeedTapeY(350f, 300f), 3);

        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Combiner(output, State(Input(speed: 300f)), new CombinerInput(300f, Top, 0f, 0f));
        Assert.Contains(output, p => p.Kind == ScreenPrimitiveKind.Line && p.A == new Vector2(96f, 72f) && p.B == new Vector2(82f, 72f));
        Assert.Contains(output, p => p.Kind == ScreenPrimitiveKind.Text && p.Text == "350" && p.A == new Vector2(76f, 78f));
        // La marca de 300 queda debajo de la caja de la velocidad: no se pinta
        Assert.DoesNotContain(output, p => p.Kind == ScreenPrimitiveKind.Line && p.A.Y == 157f && p.A.X == 96f && p.B.X == 82f);
    }

    [Fact]
    public void Tape_TopTriangle_AtTheTopOfTheShip()
    {
        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Combiner(output, State(Input(speed: 380f)), new CombinerInput(380f, Top, 0f, 0f));

        float y = MilitaryScreenDrawing.SpeedTapeY(Top, 380f);
        ScreenPrimitive triangle = output.Single(p => p.Kind == ScreenPrimitiveKind.Triangle && p.Color == Rgba(MilitaryPalette.Over));
        Assert.Equal(new Vector2(98f, y), triangle.A);
    }

    [Theory]
    [InlineData(0f, 256f)]
    [InlineData(1f, 290f)]
    [InlineData(-1f, 222f)]
    public void FlightPathMarker_MovesWithTheTurn(float turn, float x)
    {
        Assert.Equal(new Vector2(x, 123f), MilitaryScreenDrawing.FlightPathMarker(turn));
    }

    [Theory]
    [InlineData(0f, "00")]
    [InlineData(90f, "09")]
    [InlineData(350f, "35")]
    [InlineData(-10f, "35")]
    [InlineData(370f, "01")]
    public void HeadingLabel_InTensWithTwoDigits(float degrees, string label)
    {
        Assert.Equal(label, MilitaryScreenDrawing.HeadingLabel(degrees));
    }

    [Theory]
    [InlineData(0f, -1f, 0f)]
    [InlineData(1f, 0f, 90f)]
    [InlineData(0f, 1f, 180f)]
    [InlineData(-1f, 0f, 270f)]
    public void Heading_ZeroTowardsMinusZ_Clockwise(float x, float z, float degrees)
    {
        Assert.Equal(degrees, CockpitInstruments.HeadingDegrees(new Vector2(x, z)), 3);
    }

    // ---------- 8 · Columna de daño ----------

    private static ScreenPrimitive[] DamageBlocks(List<ScreenPrimitive> output) =>
        output.Where(p => p.Kind == ScreenPrimitiveKind.FillRect && p.A.X == 204f).ToArray();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Column_LitBlocksAreTheLevel(int level)
    {
        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Speed(output, State(Input(damage: level)));
        Assert.Equal(level, DamageBlocks(output).Length);
    }

    [Fact]
    public void Column_BlinksOnlyAtLevel3()
    {
        // A los 0,3 s el parpadeo de 2 Hz está en su mitad apagada
        var two = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Speed(two, State(Input(damage: 2), 0.3f));
        Assert.All(DamageBlocks(two), b => Assert.Equal(1f, b.Color.W));

        var three = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.Speed(three, State(Input(damage: 3), 0.3f));
        Assert.All(DamageBlocks(three), b => Assert.Equal(CockpitHudState.BlinkDimOpacity, b.Color.W, 3));
    }

    [Fact]
    public void Column_InTimeTrial_OffWithDimBorder_AndNoText()
    {
        var output = new List<ScreenPrimitive>();
        CockpitHudState state = State(Input(damage: 3, damageEnabled: false));
        MilitaryScreenDrawing.Speed(output, state);

        Assert.Empty(DamageBlocks(output));
        ScreenPrimitive border = output.First(p => p.Kind == ScreenPrimitiveKind.StrokeRect && p.A.X == 204f);
        Assert.Equal(Rgba(MilitaryPalette.Dim, 0.6f), border.Color);
        Assert.DoesNotContain(output, p => p.Kind == ScreenPrimitiveKind.Text && p.A == new Vector2(230f, 364f));
    }

    // ---------- 9 · Pilotos MASTER ----------

    [Theory]
    [InlineData(0, true, MasterLamps.Off, MasterLamps.Off)]
    [InlineData(1, true, MasterLamps.CautionOn, MasterLamps.Off)]
    [InlineData(2, true, MasterLamps.CautionOn, MasterLamps.Off)]
    [InlineData(3, true, MasterLamps.CautionOn, MasterLamps.WarningOn)]
    [InlineData(3, false, MasterLamps.Off, MasterLamps.Off)]
    public void MasterLamps_FollowTheTableOfTheSpec(int level, bool damageEnabled, float caution, float warning)
    {
        Assert.Equal(caution, MasterLamps.Caution(level, damageEnabled));
        Assert.Equal(warning, MasterLamps.Warning(level, damageEnabled, blinkOn: true));
    }

    [Fact]
    public void MasterWarn_BlinksWithTheDamageBlink()
    {
        Assert.Equal(MasterLamps.Off, MasterLamps.Warning(3, true, blinkOn: false));
        Assert.True(State(Input(damage: 3), 0.1f).BlinkOn);
        Assert.False(State(Input(damage: 3), 0.3f).BlinkOn);
    }

    [Fact]
    public void MasterLamps_OffValueIsThatOfThePrototype()
    {
        Assert.Equal(0.12f, MasterLamps.Off);
        Assert.Equal(2.2f, MasterLamps.CautionOn);
        Assert.Equal(2.6f, MasterLamps.WarningOn);
    }

    // ---------- 11 · Radar y minimapa en militar ----------

    [Fact]
    public void Radar_MilitaryStyle_SizesOfTheSpec()
    {
        RadarStyle style = RadarStyle.MilitaryScreen(MilitaryPalette.Radar());
        Assert.Equal(new Vector2(256f, 236f), style.Center);
        Assert.Equal(160f, style.Radius);
        Assert.Equal(6.6f, style.EchoRadius, 3);
        Assert.Equal(8.4f, style.EchoCloseRadius, 3);
        Assert.Equal(13.2f, style.SelfSize, 3);
        Assert.Equal(7f, style.WarningWidth);
        Assert.Equal(8f, style.WarningOffset);
    }

    [Fact]
    public void Radar_MilitaryBase_HasDiagonals_AndNoRangeLabel()
    {
        var military = new List<RadarPrimitive>();
        RadarDrawing.Base(military, RadarStyle.MilitaryScreen(MilitaryPalette.Radar()), 120f);
        var racing = new List<RadarPrimitive>();
        RadarDrawing.Base(racing, RadarStyle.CockpitScreen(RacingPalette.Radar()), 120f);

        Assert.Equal(4, military.Count(p => p.Kind == RadarPrimitiveKind.Line));
        Assert.Equal(2, racing.Count(p => p.Kind == RadarPrimitiveKind.Line));
        Assert.DoesNotContain(military, p => p.Kind is RadarPrimitiveKind.Text or RadarPrimitiveKind.CenteredText);
        Assert.Contains(racing, p => p.Kind is RadarPrimitiveKind.Text or RadarPrimitiveKind.CenteredText);
    }

    [Fact]
    public void Radar_MilitaryPalette_ColorsOfTheSpec()
    {
        RadarPalette palette = MilitaryPalette.Radar();
        Assert.Equal(Rgb(MilitaryPalette.Dim), palette.Rings);
        Assert.Equal(Rgb(MilitaryPalette.Main), palette.Beam);
        Assert.Equal(Rgb(MilitaryPalette.Rival), palette.Rival);
        Assert.Equal(Rgb(MilitaryPalette.Crit), palette.Close);
        Assert.Equal(Rgb(MilitaryPalette.Crit), palette.Finish);
    }

    private static Vector3 Rgb(uint rgb)
    {
        Vector4 c = MilitaryPalette.Rgba(rgb);
        return new Vector3(c.X, c.Y, c.Z);
    }

    [Fact]
    public void Minimap_MilitaryStyle_WindowOfTheSpec()
    {
        MinimapStyle style = MinimapStyle.MilitaryScreen(MilitaryPalette.Radar());
        Assert.Equal(new Vector2(150f, 182f), style.Center);
        Assert.Equal(128f, style.Radius);
        Assert.Equal(14f, style.BandWidth);
        Assert.Equal(6f, style.ShipRadius);
    }

    private static RaceShip Ship(int id, float x, float z) => new(id, new Vector3(x, 0f, z), -Vector3.UnitZ, Vector3.UnitY);

    [Fact]
    public void Targets_CountOnlyShipsWithin120m_AndNearestIsTheClosest()
    {
        var sweep = new RadarSweep();
        sweep.Update(0.016f, Vector3.Zero, new Vector2(0f, -1f), new[] { Ship(1, 0f, -50f), Ship(2, 30f, -90f), Ship(3, 0f, -130f) });

        Assert.Equal(2, sweep.TargetCount);
        Assert.Equal(50f, sweep.NearestDistance!.Value, 3);
        Assert.False(sweep.NearestIsClose);
    }

    [Fact]
    public void Targets_None_GivesNoNearest()
    {
        var sweep = new RadarSweep();
        sweep.Update(0.016f, Vector3.Zero, new Vector2(0f, -1f), new[] { Ship(1, 0f, -500f) });

        Assert.Equal(0, sweep.TargetCount);
        Assert.Null(sweep.NearestDistance);
        Assert.Equal("---", MilitaryScreenDrawing.NearestText(sweep.NearestDistance));
    }

    [Fact]
    public void Nearest_UnderTheCloseDistance_IsCrit()
    {
        var sweep = new RadarSweep();
        sweep.Update(0.016f, Vector3.Zero, new Vector2(0f, -1f), new[] { Ship(1, 5f, 0f) });
        Assert.True(sweep.NearestIsClose);

        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.RadarLive(output, 0f, sweep.TargetCount, sweep.NearestDistance, sweep.NearestIsClose);
        ScreenPrimitive nearest = output.Single(p => p.Kind == ScreenPrimitiveKind.Text && p.Text == "005 M");
        Assert.Equal(Rgba(MilitaryPalette.Crit), nearest.Color);
        Assert.Contains(output, p => p.Kind == ScreenPrimitiveKind.Text && p.Text == "TGT 01");
    }

    // ---------- 12 · Progreso de la vuelta ----------

    [Theory]
    [InlineData(0f, 0f, "0 %")]
    [InlineData(0.5f, 130f, "50 %")]
    [InlineData(1f, 260f, "100 %")]
    public void Progress_BarAndPercent(float fraction, float width, string percent)
    {
        var output = new List<ScreenPrimitive>();
        MilitaryScreenDrawing.MapLive(output, "LAP 2/3", fraction);

        float filled = output.Where(p => p.Kind == ScreenPrimitiveKind.FillRect && p.Color == Rgba(MilitaryPalette.Main))
            .Select(p => p.B.X).DefaultIfEmpty(0f).Single();
        Assert.Equal(width, filled, 3);
        Assert.Contains(output, p => p.Kind == ScreenPrimitiveKind.Text && p.Text == percent);
        Assert.Contains(output, p => p.Kind == ScreenPrimitiveKind.Text && p.Text == "LAP 2/3");
    }

    [Theory]
    [InlineData(0f, 4200f, 0f)]
    [InlineData(2100f, 4200f, 0.5f)]
    [InlineData(4300f, 4200f, 1f)]
    [InlineData(-5f, 4200f, 0f)]
    public void LapFraction_DistanceOverLapLength_Clamped(float distance, float length, float fraction)
    {
        Assert.Equal(fraction, CockpitInstruments.LapFraction(distance, length), 4);
    }
}
