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

    // ---------- Chispas ----------

    // VECTOR, 2 m sobre el suelo plano sin girar, morro hacia −Z a 60 m/s, deslizando hacia la derecha
    private static readonly DriftEdge Edge = new(2.63f, 0.2f, 0.55f, 2.7f, 1f, -0.08f, 2.45f, 1f);
    private static readonly ShipPose Pose = ShipPose.Flat(new Vector3(0f, 2f, 0f));
    private static readonly Vector2 Velocity = new(10f, -60f);

    private static int EmitFor(float seconds, float dt, float d, float quality, SparkField? field = null, int seed = 1,
        BarrierSide side = BarrierSide.Left, DriftFxSettings? settings = null)
    {
        field ??= new SparkField(CollisionFxSettings.Default);
        var emitter = new DriftSparkEmitter(settings ?? S, new Random(seed));
        var state = new DriftState(true, d, side);
        int total = 0;
        int steps = (int)MathF.Round(seconds / dt);
        for (int i = 0; i < steps; i++)
            total += emitter.Emit(1, state, null, Pose, Velocity, Edge, quality, dt, field);
        return total;
    }

    private static DriftFxSettings NoEmbers()
    {
        var s = DriftFxSettings.Default;
        s.EmberRate = 0f;
        return s;
    }

    [Fact]
    public void SparkRate_Is160PerSecondTimesD_ScaledByParticles()
    {
        DriftFxSettings s = NoEmbers();
        Assert.InRange(EmitFor(1f, 1f / 60f, 1f, 1f, settings: s), 159, 161);
        Assert.InRange(EmitFor(1f, 1f / 30f, 1f, 1f, settings: s), 159, 161);
        Assert.InRange(EmitFor(1f, 1f / 60f, 0.5f, 1f, settings: s), 79, 81);
        Assert.InRange(EmitFor(1f, 1f / 60f, 1f, 0.25f, settings: s), 39, 41);
        // Más 8 brasas por segundo × d
        Assert.InRange(EmitFor(1f, 1f / 60f, 1f, 1f), 167, 169);
    }

    [Theory]
    [InlineData(BarrierSide.Left)]
    [InlineData(BarrierSide.Right)]
    public void Sparks_LeaveFromTheWingOnTheRubSide_AndOutwards(BarrierSide side)
    {
        var field = new SparkField(CollisionFxSettings.Default);
        EmitFor(1f, 1f / 60f, 1f, 1f, field, side: side);
        float sign = side == BarrierSide.Left ? -1f : 1f;
        var inherited = new Vector3(Velocity.X, 0f, Velocity.Y) * S.SparkInherit;
        Assert.True(field.Count > 100);
        int jet = 0;
        for (int i = 0; i < field.Count; i++)
        {
            Spark p = field[i];
            Assert.True(p.Position.X * sign >= Edge.HalfWidth * Edge.StartFraction - 1e-3f);
            Assert.Equal(2f + Edge.Y, p.Position.Y, 4);   // a la altura del ala, no en la pista
            Assert.True((p.Velocity - inherited).X * sign >= MathF.Min(S.SparkOutMin, S.EmberOutMin) - 1e-3f);
            if (p.Position == Pose.Point(DriftContact.FocusPoint(Edge, side)))
                jet++;
        }
        // La mayor parte sale del foco, en chorro
        Assert.InRange(jet / (float)field.Count, 0.5f, 0.75f);
    }

    [Fact]
    public void NoEffects_NoSparks_AndNoFocus()
    {
        var field = new SparkField(CollisionFxSettings.Default);
        var emitter = new DriftSparkEmitter(S, new Random(1));
        for (int i = 0; i < 60; i++)
            emitter.Emit(1, DriftState.None, null, Pose, Velocity, Edge, 1f, 1f / 60f, field);
        Assert.Equal(0, field.Count);
        Assert.Equal(0f, DriftFocus.Core(DriftState.None, 1f, 1, S));
        Assert.Equal(0f, DriftFocus.Halo(DriftState.None, 1f, 1, S));
    }

    [Fact]
    public void Focus_Crackles_Between40And100Percent_WithFullD()
    {
        var full = new DriftState(true, 1f, BarrierSide.Left);
        var zero = new DriftState(true, 0f, BarrierSide.Left);
        for (float t = 0f; t < 3f; t += 0.013f)
        {
            Assert.InRange(DriftFocus.Core(full, t, 2, S), 0.4f, 1f);
            Assert.InRange(DriftFocus.Halo(full, t, 2, S), 0.14f, 0.35f);
            Assert.Equal(0f, DriftFocus.Core(zero, t, 2, S));
        }
        // Cada nave con su desfase
        Assert.NotEqual(DriftFocus.Flicker(0.3f, 1, S), DriftFocus.Flicker(0.3f, 2, S));
    }

    [Fact]
    public void SameSeed_SameSparks()
    {
        var a = new SparkField(CollisionFxSettings.Default);
        var b = new SparkField(CollisionFxSettings.Default);
        EmitFor(0.5f, 1f / 60f, 0.8f, 1f, a, seed: 7);
        EmitFor(0.5f, 1f / 60f, 0.8f, 1f, b, seed: 7);
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Position, b[i].Position);
            Assert.Equal(a[i].Velocity, b[i].Velocity);
        }
    }

    [Fact]
    public void Sparks_FollowTheShipBank()
    {
        // Nave ladeada 20° a la izquierda: el borde izquierdo baja
        float a = 20f * MathF.PI / 180f;
        var pose = new ShipPose(new Vector3(0f, 2f, 0f), new Vector3(MathF.Cos(a), MathF.Sin(a), 0f),
            new Vector3(-MathF.Sin(a), MathF.Cos(a), 0f), Vector3.UnitZ);
        Vector3 focus = pose.Point(DriftContact.FocusPoint(Edge, BarrierSide.Left));
        Assert.True(focus.Y < 2f + Edge.Y - 0.8f);
    }

    [Fact]
    public void EveryShip_RubsAlongItsEdge_WithTheFocusOnIt()
    {
        foreach (DriftContactShape c in new[] { VectorShip.Definition.Visual.DriftContact,
            BladeShip.Definition.Visual.DriftContact, HammerShip.Definition.Visual.DriftContact })
        {
            Assert.InRange(c.EdgeEndFraction, 0f, 1f);
            Assert.InRange(c.EdgeStartFraction, 0f, c.EdgeEndFraction);
            Assert.InRange(c.FocusZ, MathF.Min(c.EdgeStartZ, c.EdgeEndZ), MathF.Max(c.EdgeStartZ, c.EdgeEndZ));
            Assert.InRange(c.FocusFraction, c.EdgeStartFraction, 1f);
        }
        Assert.Equal(-2.2f, HammerShip.Definition.Visual.DriftContact.FocusZ);   // el patín del canard
        Assert.Equal(-0.52f, HammerShip.Definition.Visual.DriftContact.EdgeY);
    }
}
