using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Luces de la nave, etapa 3 (spec docs/efectos-8-luces.md, 14, pruebas 6 a 9): luz en pista, luz de freno sobre el
// casco, faros y retro. La sombra (prueba 10) va con la spec del suelo (v1.2).
public class ShipLightsWorldTests
{
    private static readonly ShipLightSettings S = ShipLightSettings.Default;
    private const float Dt = 1f / 60f;

    private static HullManifest Manifest(string ship) => ship switch
    {
        "vector" => Hull3DTests.Manifest,
        "blade" => Hull3DBladeTests.Manifest,
        _ => Hull3DHammerTests.Manifest
    };

    private static ShipLightState State(string ship) => new(ShipLightCatalog.From(Manifest(ship)));

    private static void Run(ShipLightState state, ShipLightInput input, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
            state.Update(Dt, input);
    }

    private static readonly ShipLightInput Rest = new() { Speed = 50f, TopSpeed = 100f };

    // ---------- 6. Luz en pista ----------

    [Fact]
    public void RearTrackLight_Is1Point2AtRest_14Point2Braking_10Point2WithBoost()
    {
        var state = State("vector");
        state.Update(Dt, Rest);
        Assert.Equal(1.2f, state.TrackRear, 3);
        Run(state, Rest with { Braking = true }, 2f);
        Assert.Equal(14.2f, state.TrackRear, 2);
        var boost = State("vector");
        boost.Update(Dt, Rest with { Boost = 1f });
        Assert.Equal(10.2f, boost.TrackRear, 3);
    }

    [Fact]
    public void TrackLights_FadeOutWithHeight_NothingAt3Point5m()
    {
        var state = State("hammer");
        state.Update(Dt, Rest with { Power = 1f, Boost = 1f, Altitude = 3.5f });
        Assert.Equal(0f, state.TrackLeft);
        Assert.Equal(0f, state.TrackRight);
        Assert.Equal(0f, state.TrackRear);
        Assert.Equal(0f, state.TrackNozzle);
        state.Update(Dt, Rest with { Altitude = 1.75f });
        Assert.Equal(8f * 1.4f * 0.5f, state.TrackLeft, 3);
    }

    [Fact]
    public void InnerSideTrackLight_Times1Plus1Point5G()
    {
        var state = State("blade");
        Run(state, new ShipLightInput { Speed = 100f, TopSpeed = 100f, Steer = -1f }, 3f);
        Assert.Equal(1, state.InnerSide);
        Assert.Equal(8f * 1.4f * (1f + 1.5f * state.Turn), state.TrackRight, 3);
        Assert.Equal(8f * 1.4f, state.TrackLeft, 3);
    }

    [Fact]
    public void NozzleTrackLight_FollowsPowerAndBoost_AndIsWeakOnTheGrid()
    {
        var state = State("vector");
        state.Update(Dt, Rest with { Power = 1f, Boost = 1f });
        Assert.Equal(1f + 3.5f + 8f, state.TrackNozzle, 3);
        state.Update(Dt, Rest with { Power = 0.2f, OnGrid = true });
        Assert.Equal((1f + 3.5f * 0.2f) * 0.4f, state.TrackNozzle, 3);
    }

    [Theory]
    [InlineData("vector", "#FF2D2D", "#FF2D2D")]   // el acento de su pintura de partida
    [InlineData("blade", "#FF2A1E", "#3DFF6A")]
    [InlineData("hammer", "#FF9A1A", "#FF9A1A")]
    public void SideTrackLights_TakeTheColorOfTheShip(string ship, string left, string right)
    {
        var state = State(ship);
        state.Update(Dt, Rest);
        Assert.Equal(ShipLightCatalog.Rgb(left), state.TrackLeftColor);
        Assert.Equal(ShipLightCatalog.Rgb(right), state.TrackRightColor);
    }

    [Fact]
    public void TrackLightPositions_AreTheTableOfTheSpec()
    {
        var hammer = ShipTrackLights.Positions(HullStyle.Industrial);
        Assert.Equal(new Vector3(-2.4f, -0.40f, -2.2f), hammer.Left);
        Assert.Equal(new Vector3(0f, -0.45f, 6.4f), hammer.Nozzle);
        Assert.Equal(new Vector3(1.6f, -0.30f, 2.6f), ShipTrackLights.Positions(HullStyle.Military).Right);
        Assert.Equal(new Vector3(0f, -0.30f, 3.4f), ShipTrackLights.Positions(HullStyle.Racing).Rear);
    }

    // ---------- 7. Luz de freno sobre el casco ----------

    [Fact]
    public void HullBrakeLight_IsEB015AtRest_115Braking_075WithBoost_AndOrangeWithBoost()
    {
        var state = State("blade");
        state.Update(Dt, Rest);
        Assert.Equal(0.15f, state.HullBrakeEnergy, 3);
        Run(state, Rest with { Braking = true }, 2f);
        Assert.Equal(1.15f, state.HullBrakeEnergy, 2);
        var boost = State("blade");
        boost.Update(Dt, Rest with { Boost = 1f });
        Assert.Equal(0.75f, boost.HullBrakeEnergy, 3);
        Assert.Equal(S.BoostColor, boost.HullBrakeColor);
    }

    [Theory]
    [InlineData("blade", "Light_Brake")]
    [InlineData("hammer", "Light_Brake")]
    public void HullBrakeLight_Sits035mBehindTheNewBrakeLight(string ship, string piece)
    {
        float[] c = Manifest(ship).Piece(piece)!.Center;
        Assert.Equal(new Vector3(c[0], c[1], c[2] + 0.35f), State(ship).HullBrakePosition);
    }

    [Fact]
    public void Vector_HullBrakeLight_Sits035mBehindTheMiddleOfItsTailLights()
    {
        float[] l = Hull3DTests.Manifest.Piece("Light_Tail_L")!.Center, r = Hull3DTests.Manifest.Piece("Light_Tail_R")!.Center;
        Vector3 p = State("vector").HullBrakePosition;
        Assert.Equal(0f, p.X, 3);
        Assert.Equal((l[2] + r[2]) / 2f + 0.35f, p.Z, 3);
    }

    // ---------- 8. Faros ----------

    [Theory]
    [InlineData("vector", HullStyle.Racing, 2, 0.20f)]
    [InlineData("blade", HullStyle.Military, 2, 0.19f)]
    [InlineData("hammer", HullStyle.Industrial, 4, 0.18f)]
    public void Spots_CountAndHalfAngles(string ship, HullStyle style, int count, float half)
    {
        List<ShipSpotDef> spots = ShipSpots.From(Manifest(ship), style);
        Assert.Equal(count, spots.Count);
        Assert.All(spots.Where(s => !s.Work), s => Assert.Equal(half, s.HalfAngle));
        if (style == HullStyle.Industrial)
        {
            ShipSpotDef[] work = spots.Where(s => s.Work).ToArray();
            Assert.Equal(2, work.Length);
            Assert.All(work, s => Assert.Equal(0.32f, s.HalfAngle));
            Assert.All(work, s => Assert.Equal(0.16f, MathF.Abs(s.Lens.X), 2));
        }
    }

    [Fact]
    public void Spot_Is09UpAnd08BackFromItsLens_AimingAtTheTrack15mAhead()
    {
        ShipSpotDef spot = ShipSpots.From(Hull3DTests.Manifest, HullStyle.Racing).First(s => s.Lens.X > 0f);
        Assert.Equal(spot.Lens + new Vector3(0f, 0.9f, 0.8f), ShipSpots.LightPosition(spot, S));
        Vector3 target = ShipSpots.Target(spot, S, -2f);
        Assert.Equal(-2f, target.Y);
        var plan = new Vector2(target.X - spot.Lens.X, target.Z - spot.Lens.Z);
        Assert.Equal(15f, plan.Length(), 3);
        Assert.Equal(0.07f, plan.X / -plan.Y, 3);   // su dirección en planta (0,07; −1)
    }

    [Fact]
    public void Beam_FadesWithTheCameraInsideTheCone_InFrontOfTheLamp()
    {
        var lens = Vector3.Zero;
        var axis = new Vector3(0f, 0f, -1f);
        Assert.Equal(0f, ShipSpots.BeamCameraFactor(new Vector3(0f, 0f, -8f), lens, axis, 0.2f, S));   // en el eje, delante
        Assert.Equal(1f, ShipSpots.BeamCameraFactor(new Vector3(0f, 1f, 5f), lens, axis, 0.2f, S));     // detrás del faro
        Assert.Equal(1f, ShipSpots.BeamCameraFactor(new Vector3(20f, 0f, -8f), lens, axis, 0.2f, S));   // lejos del eje
    }

    [Fact]
    public void Beam_EndRadius_AndOpacities()
    {
        var head = new ShipSpotDef(Vector3.Zero, new Vector2(0f, -1f), 0.2f, 1320f, false);
        var work = head with { Work = true };
        Assert.Equal(MathF.Tan(0.2f) * 0.75f * 10f, ShipSpots.BeamEndRadius(head, S), 4);
        Assert.Equal(0.07f, ShipSpots.BeamOpacity(head, S));
        Assert.Equal(0.04f, ShipSpots.BeamOpacity(work, S));
    }

    // ---------- 9. Retro ----------

    [Theory]
    [InlineData("vector")]
    [InlineData("blade")]
    [InlineData("hammer")]
    public void RetroMouth_Is015AtRest_And415AtFull(string ship)
    {
        var state = State(ship);
        int mouth = Enumerable.Range(0, state.Count).First(i => state.Def(i).Type == ShipLightType.Retro);
        state.Update(Dt, Rest);
        Assert.Equal(0.15f, state.Intensity(mouth), 4);
        Assert.Equal(0f, state.RetroLight);
        state.Update(Dt, Rest with { Retro = 1f, Steer = 1f });
        Assert.Equal(4.15f, state.Intensity(mouth), 4);
        Assert.Equal(2f, state.RetroLight, 4);
    }

    // La spec pide la trampilla abierta del todo con R = 0,5, pero su fórmula y la de la maqueta (mín(1; 1,4·R)) la
    // abren del todo a partir de R ≈ 0,71: manda la maqueta
    [Fact]
    public void Flap_ClosedAtRest_OpenAt075Rad_FullyOpenBeforeTheJetPeaks()
    {
        var state = State("blade");
        state.Update(Dt, Rest);
        Assert.Equal(0f, state.FlapAngle);
        state.Update(Dt, Rest with { Retro = 1f });
        Assert.Equal(0.75f, state.FlapAngle, 4);
        state.Update(Dt, Rest with { Retro = 0.5f });
        Assert.Equal(0.75f * 0.7f, state.FlapAngle, 4);
        state.Update(Dt, Rest with { Retro = 0.72f });
        Assert.Equal(0.75f, state.FlapAngle, 4);
    }
}
