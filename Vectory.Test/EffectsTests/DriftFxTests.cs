using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Effects;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Efectos del derrape: intensidad y marcas (spec docs/efectos-2-derrape.md, pruebas 1–8)
public class DriftFxTests
{
    private static readonly DriftFxSettings S = DriftFxSettings.Default;

    private static float Rad(float degrees) => degrees * MathF.PI / 180f;

    private static DriftState Drift(float degrees, bool drifting = true, bool airborne = false) =>
        DriftIntensity.Evaluate(drifting, airborne, Rad(degrees), S);

    // Muestra con el centro entre las puntas en z (avanzando hacia −Z)
    private static void Step(DriftMarkTrail trail, bool active, float z, float time, float d = 1f) =>
        trail.Update(active, d, new Vector3(-2f, 0.03f, z), new Vector3(2f, 0.03f, z), time);

    // ---------- Intensidad ----------

    [Fact]
    public void Intensity_StartsAt4Degrees_AndIsFullFrom22()
    {
        Assert.False(Drift(3f).Active);
        Assert.Equal(0f, Drift(4f).D);
        Assert.Equal(0.5f, Drift(13f).D, 4);
        Assert.Equal(1f, Drift(22f).D, 4);
        Assert.Equal(1f, Drift(30f).D);
        Assert.True(Drift(13f).Active);
    }

    [Fact]
    public void NoEffects_WithoutDriftState_OrInTheAir()
    {
        Assert.False(Drift(30f, drifting: false).Active);
        Assert.False(Drift(30f, airborne: true).Active);
        Assert.Equal(0f, Drift(30f, airborne: true).D);
    }

    [Fact]
    public void RubSide_IsTheInnerOne_OppositeToTheSlip()
    {
        Assert.Equal(BarrierSide.Left, Drift(15f).Side);    // desliza hacia la derecha
        Assert.Equal(BarrierSide.Right, Drift(-15f).Side);  // hacia la izquierda
    }

    // ---------- Marcas ----------

    [Fact]
    public void Sample_EverySpacing_NotEveryStep()
    {
        var trail = new DriftMarkTrail(S);
        Step(trail, true, 0f, 0f);
        Step(trail, true, -0.5f, 0.01f);
        Assert.Equal(1, trail.Count);
        Step(trail, true, -0.8f, 0.02f);
        Assert.Equal(2, trail.Count);
    }

    [Fact]
    public void CuttingTheEffects_StartsANewStroke()
    {
        var trail = new DriftMarkTrail(S);
        Step(trail, true, 0f, 0f);
        Step(trail, true, -1f, 0.1f);
        Step(trail, false, -2f, 0.2f);
        Step(trail, true, -3f, 0.3f);
        Step(trail, true, -4f, 0.4f);
        Assert.Equal(4, trail.Count);
        Assert.Equal(trail[0].Stroke, trail[1].Stroke);
        Assert.NotEqual(trail[1].Stroke, trail[2].Stroke);
        Assert.Equal(trail[2].Stroke, trail[3].Stroke);
    }

    [Fact]
    public void Brightness_Holds6Seconds_ThenFadesTo0At8()
    {
        Assert.Equal(0.32f, DriftMarks.Brightness(1f, 0f, S), 5);
        Assert.Equal(0.32f, DriftMarks.Brightness(1f, 6f, S), 5);
        Assert.Equal(0.16f, DriftMarks.Brightness(1f, 7f, S), 5);
        Assert.Equal(0f, DriftMarks.Brightness(1f, 8f, S));
        Assert.Equal(0.112f, DriftMarks.Brightness(0f, 0f, S), 5);
    }

    [Fact]
    public void FullTrail_DropsTheOldest()
    {
        var trail = new DriftMarkTrail(S);
        for (int i = 0; i <= S.MaxMarkSamples; i++)
            Step(trail, true, -i, i * 0.001f);
        Assert.Equal(S.MaxMarkSamples, trail.Count);
        Assert.Equal(-1f, trail[0].Left.Z);
        Assert.Equal(-S.MaxMarkSamples, trail[trail.Count - 1].Left.Z);
    }

    [Fact]
    public void Pause_DoesNotAge_AndOldMarksGo()
    {
        var marks = new DriftMarks(S);
        var on = new DriftState(true, 1f, BarrierSide.Left);
        marks.Update(1, on, new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f));
        marks.Advance(7f);
        marks.Advance(0f);
        Assert.Equal(7f, marks.Now);
        DriftMarkTrail trail = Assert.Single(marks.Trails);
        Assert.Equal(0.16f, trail.BrightnessOf(trail[0], marks.Now), 5);

        marks.Advance(1f);
        trail.Prune(marks.Now);
        Assert.Equal(0, trail.Count);
    }

    [Fact]
    public void Recolocation_CutsTheStroke_AndKeepsTheMarks()
    {
        var marks = new DriftMarks(S);
        var on = new DriftState(true, 1f, BarrierSide.Left);
        marks.Update(1, on, new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f));
        marks.Cut(1);
        marks.Update(1, on, new Vector3(-2f, 0f, -0.1f), new Vector3(2f, 0f, -0.1f));
        DriftMarkTrail trail = Assert.Single(marks.Trails);
        Assert.Equal(2, trail.Count);
        Assert.NotEqual(trail[0].Stroke, trail[1].Stroke);
    }

    // ---------- Dónde roza ----------

    [Fact]
    public void MarkPoints_AreUnderTheWingTips_OnTheSurface()
    {
        // Morro hacia −Z: la izquierda es −X y la popa +Z
        (Vector3 left, Vector3 right) = DriftContact.MarkPoints(null, new Vector3(10f, 2f, 5f), new Vector2(0f, -1f), 2f, 2.7f, S);
        Assert.Equal(new Vector3(10f - 1.9f, 0.03f, 7.7f), left);
        Assert.Equal(new Vector3(10f + 1.9f, 0.03f, 7.7f), right);
    }

    [Fact]
    public void EveryShip_RubsAtItsWingTips()
    {
        Assert.Equal(2.7f, VectorShip.Definition.Visual.DriftContact.TipZ);
        Assert.Equal(3.6f, BladeShip.Definition.Visual.DriftContact.TipZ);
        Assert.Equal(-2.2f, HammerShip.Definition.Visual.DriftContact.TipZ);   // patines de los canards, delante
    }
}
