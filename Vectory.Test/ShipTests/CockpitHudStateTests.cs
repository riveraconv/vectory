using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Estado del HUD de cabina, común a las tres cabinas 3D (spec docs/naves-3b-cabinas.md, 4.2 y 5): aquí con los colores y
// lo apagado del estilo carreras
public class CockpitHudStateTests
{
    private const float Settled = 10f;   // más que cualquier transición

    private static readonly Vector4 Cyan = RacingPalette.Role(CockpitColor.Cyan);
    private static readonly Vector4 Orange = RacingPalette.Role(CockpitColor.Orange);
    private static readonly Vector4 Blue = RacingPalette.Role(CockpitColor.Blue);
    private static readonly Vector4 BlueGray = RacingPalette.Role(CockpitColor.BlueGray);
    private static readonly Vector4 Red = RacingPalette.Role(CockpitColor.Red);
    private static readonly Vector4 Green = RacingPalette.Role(CockpitColor.Green);
    private static readonly Vector4 Yellow = RacingPalette.Role(CockpitColor.Yellow);

    private static CockpitHudInput Input(float speed = 300f, bool boost = false, int charges = 0, bool cooldown = false, int damage = 0, bool damageEnabled = true) => new()
    {
        SpeedKmh = speed,
        TopKmh = 396f,
        MaxKmh = 496f,
        BoostActive = boost,
        Charges = charges,
        Cooldown = cooldown,
        DamageLevel = damage,
        DamageEnabled = damageEnabled
    };

    private static CockpitHudState NewState() => new(RacingPalette.Role, RacingPalette.Look);

    private static CockpitHudState State(CockpitHudInput input, CockpitLightSettings? light = null)
    {
        CockpitHudState state = NewState();
        state.Update(input, light ?? CockpitLightSettings.Default, Settled);
        return state;
    }

    private static Vector4 Alpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, color.W * alpha);

    [Theory]
    [InlineData(300f, false, false)]
    [InlineData(396f, false, false)]
    [InlineData(420f, false, true)]
    [InlineData(300f, true, true)]
    public void SpeedColor_CyanBelowTop_OrangeAboveAndWithBoost(float speed, bool boost, bool orange)
    {
        Assert.Equal(orange ? Orange : Cyan, State(Input(speed, boost)).SpeedColor);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(248f, 0.5f)]
    [InlineData(600f, 1f)]
    public void SpeedFraction_OverTopPlusBoost_Clamped(float speed, float fraction)
    {
        Assert.Equal(fraction, State(Input(speed)).SpeedFraction, 4);
    }

    [Fact]
    public void Orbs_FourForms_BlockedBeforeCooldown()
    {
        Assert.Equal(new CockpitHudPaint(Alpha(Red, 0.3f), Alpha(Red, 0.55f), 0f), State(Input(charges: 3, cooldown: true, damage: 3)).Orb(0));
        Assert.Equal(new CockpitHudPaint(Alpha(Orange, 0.3f), Alpha(Orange, 0.55f), 0f), State(Input(charges: 3, cooldown: true, damage: 2)).Orb(0));
        Assert.Equal(new CockpitHudPaint(Orange, Orange, 1f), State(Input(charges: 3)).Orb(2));
        Assert.Equal(new CockpitHudPaint(Alpha(Blue, 0.5f), Alpha(Blue, 0.9f), 0f), State(Input(charges: 1)).Orb(1));
    }

    [Theory]
    [InlineData(2, false, false, 3, "LOCKED")]
    [InlineData(2, true, false, 0, "ACTIVE")]
    [InlineData(2, false, true, 0, "COOLDOWN")]
    [InlineData(1, false, false, 0, "READY")]
    [InlineData(0, false, false, 0, "CHARGING")]
    [InlineData(0, false, true, 3, "CHARGING")]
    public void BoostText(int charges, bool boost, bool cooldown, int damage, string text)
    {
        Assert.Equal(text, State(Input(charges: charges, boost: boost, cooldown: cooldown, damage: damage)).BoostText);
    }

    [Theory]
    [InlineData(0, "NOMINAL", 0)]
    [InlineData(1, "LIGHT", 1)]
    [InlineData(2, "HEAVY", 2)]
    [InlineData(3, "CRITICAL", 3)]
    public void Damage_SegmentsOnByLevel_AndText(int level, string text, int on)
    {
        CockpitHudState state = State(Input(damage: level));
        Vector4 color = level == 0 ? Green : level == 1 ? Yellow : Red;

        Assert.Equal(text, state.DamageText);
        Assert.Equal(color, state.DamageTextColor);
        Assert.Equal(level == 3, state.SegmentsBlinking);
        Assert.Equal(level, state.DamageLevel);
        for (int i = 0; i < 3; i++)
        {
            bool lit = i < on;
            Assert.Equal(lit ? 1f : 0f, state.Segment(i).Fill.W, 3);
            if (!lit)
                Assert.Equal(Alpha(BlueGray, 0.55f), state.Segment(i).Stroke);
        }
    }

    [Fact]
    public void Blink_OnlyAtLevelThree_AtTwoHertz()
    {
        CockpitHudState state = NewState();
        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0f);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.1f);
        Assert.Equal(1f, state.Segment(0).Fill.W, 3);
        Assert.True(state.BlinkOn);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.2f);
        Assert.Equal(CockpitHudState.BlinkDimOpacity, state.Segment(0).Fill.W, 3);
        Assert.False(state.BlinkOn);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.25f);
        Assert.Equal(1f, state.Segment(0).Fill.W, 3);
    }

    [Fact]
    public void TimeTrial_DamageOff_NoTextNoRedTint_OrbsNotBlocked()
    {
        CockpitHudState state = State(Input(charges: 3, damage: 3, damageEnabled: false));

        Assert.Equal("", state.DamageText);
        Assert.Equal(0f, state.DamageTint);
        Assert.False(state.SegmentsBlinking);
        Assert.False(state.DamageEnabled);
        Assert.Equal(0f, state.Segment(0).Fill.W);
        Assert.Equal(Alpha(BlueGray, 0.55f), state.Segment(0).Stroke);
        Assert.Equal("READY", state.BoostText);
        Assert.Equal(Orange, state.Orb(0).Fill);
    }

    [Fact]
    public void Fade_ChangesColorInATransitionTime()
    {
        CockpitHudState state = NewState();
        state.Update(Input(300f), CockpitLightSettings.Default, 0.016f);
        Assert.Equal(Cyan, state.SpeedColor);

        state.Update(Input(450f), CockpitLightSettings.Default, CockpitHudState.HudTransitionTime / 2f);
        Assert.Equal(Vector4.Lerp(Cyan, Orange, 0.5f).X, state.SpeedColor.X, 3);

        state.Update(Input(450f), CockpitLightSettings.Default, CockpitHudState.HudTransitionTime);
        Assert.Equal(Orange, state.SpeedColor);
    }

    [Theory]
    [InlineData(0f, 0.35f)]
    [InlineData(248f, 0.525f)]
    [InlineData(496f, 0.7f)]
    public void ScreenGlow_GrowsWithSpeed(float speed, float glow)
    {
        Assert.Equal(glow, State(Input(speed)).ScreenGlow, 3);
    }

    [Fact]
    public void Light_TableValues_TimesIntensity_AndOffAtZero()
    {
        Assert.Equal(0f, State(Input(300f)).BoostTint, 3);
        Assert.Equal(0.25f, State(Input(420f)).BoostTint, 3);
        Assert.Equal(0.55f, State(Input(300f, true)).BoostTint, 3);
        Assert.Equal(0f, State(Input(damage: 1)).DamageTint, 3);
        Assert.Equal(0.15f, State(Input(damage: 2)).DamageTint, 3);
        Assert.InRange(State(Input(damage: 3)).DamageTint, 0.2f, 0.45f);

        var half = new CockpitLightSettings { BoostTint = 0.5f, DamageTint = 0.5f, ScreenGlow = 0.5f };
        Assert.Equal(0.275f, State(Input(300f, true), half).BoostTint, 3);
        Assert.Equal(0.075f, State(Input(damage: 2), half).DamageTint, 3);
        Assert.Equal(0.175f, State(Input(0f), half).ScreenGlow, 3);

        CockpitHudState off = State(Input(300f, true, damage: 3), new CockpitLightSettings());
        Assert.Equal(0f, off.BoostTint);
        Assert.Equal(0f, off.DamageTint);
        Assert.Equal(0f, off.ScreenGlow);
    }

    [Fact]
    public void DamagePulse_BetweenHighAndLow_EverySecond()
    {
        CockpitHudState state = NewState();
        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0f);
        Assert.Equal(0.45f, state.DamageTint, 3);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.5f);
        Assert.Equal(0.2f, state.DamageTint, 3);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.5f);
        Assert.Equal(0.45f, state.DamageTint, 3);
    }

    [Fact]
    public void ViewRules_3DInCockpit_ScreenHudOutside()
    {
        Assert.Equal(new CockpitViewRules(true, false, false), CockpitViewRules.For(CameraView.Cockpit));
        Assert.Equal(new CockpitViewRules(true, true, true), CockpitViewRules.For(CameraView.Cockpit, false));
        Assert.Equal(new CockpitViewRules(false, true, true), CockpitViewRules.For(CameraView.Chase));
    }
}
