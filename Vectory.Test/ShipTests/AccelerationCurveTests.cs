using System;
using Vectory.Menu;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Curva de aceleración (spec docs/naves-5-curva-aceleracion.md, etapa 1)
public class AccelerationCurveTests
{
    private const float Kmh = 1f / 3.6f;
    private const float Dt = 1f / 60f;

    private static readonly ShipInput Throttle = new() { Accelerate = true };

    // Segundos con W a fondo desde parada hasta "target" m/s, integrando la dinámica real
    private static float TimeTo(float target, in ShipTuning tuning, float dt = Dt)
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f);
        float time = 0f;
        while (dynamics.Speed < target && time < 120f)
        {
            dynamics.Step(Throttle, tuning, dt);
            time += dt;
        }
        return time;
    }

    private static DriftTuning CreateDrift() => new()
    {
        Enabled = true,
        EnterSpeed = 120f * Kmh,
        ExitSpeed = 60f * Kmh,
        Grip = 0f,
        CountersteerGrip = 0.5f,
        TurnMultiplier = 1.4f,
        FixedLoss = 5f * Kmh,
        SlipLoss = 10f * Kmh,
        AccelerationFactor = 0.5f,
        MaxSlipAngle = 60f * MathF.PI / 180f,
        GripRecoveryTime = 3f
    };

    // --- 1 y 2. El factor ---

    [Fact]
    public void Factor_AtZeroSpeed_IsOne()
    {
        Assert.Equal(1f, AccelerationCurve.Factor(0f, 110f, 2f, 0.1f));
    }

    [Fact]
    public void Factor_AtHalfTheTopSpeed_Is075()
    {
        Assert.Equal(0.75f, AccelerationCurve.Factor(55f, 110f, 2f, 0.1f), 4);
    }

    [Fact]
    public void Factor_At97Percent_IsTheMinimum()
    {
        Assert.Equal(0.10f, AccelerationCurve.Factor(110f * 0.97f, 110f, 2f, 0.1f), 4);
    }

    [Fact]
    public void Factor_FallsContinuously_WithoutSteps()
    {
        float previous = 1f;
        for (int i = 1; i <= 1000; i++)
        {
            float factor = AccelerationCurve.Factor(110f * i / 1000f, 110f, 2f, 0.1f);
            Assert.True(factor <= previous);
            Assert.True(previous - factor < 0.003f, $"Step at {i}: {previous} -> {factor}");
            previous = factor;
        }
    }

    [Fact]
    public void Factor_WithLinearExponent_IsAlwaysOne()
    {
        Assert.Equal(1f, AccelerationCurve.Factor(100f, 110f, AccelerationCurve.Linear, 0.1f));
    }

    // --- 3. En la punta o por encima, W no añade ---

    [Fact]
    public void AtTopSpeed_ThrottleAddsNothing()
    {
        ShipTuning tuning = VectorTuning.Values;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, tuning.MaxSpeed);

        dynamics.Step(Throttle, tuning, Dt);

        Assert.Equal(tuning.MaxSpeed, dynamics.Speed);
    }

    [Fact]
    public void AboveTopSpeed_ThrottleAddsNothing()
    {
        ShipTuning tuning = VectorTuning.Values;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, tuning.MaxSpeed + 10f);

        dynamics.Step(Throttle, tuning, Dt);

        Assert.True(dynamics.Speed < tuning.MaxSpeed + 10f);
    }

    // --- 4. Con un exponente enorme es casi lineal ---

    [Fact]
    public void HugeExponent_WithinTwoPercentOfLinear()
    {
        ShipTuning curved = VectorTuning.Values;
        curved.AccelCurve = 1000f;
        ShipTuning linear = VectorTuning.Values;
        linear.AccelCurve = AccelerationCurve.Linear;

        float curvedTime = TimeTo(curved.MaxSpeed, curved, 1f / 240f);
        float linearTime = TimeTo(linear.MaxSpeed, linear, 1f / 240f);

        Assert.True(MathF.Abs(curvedTime / linearTime - 1f) < 0.02f, $"{curvedTime} vs {linearTime}");
    }

    // --- 5. VECTOR con W a fondo (valores del cálculo analítico, con Acceleration = 25) ---

    [Theory]
    [InlineData(100f, 1.14f)]
    [InlineData(200f, 2.45f)]
    [InlineData(300f, 4.36f)]
    [InlineData(350f, 6.13f)]
    public void Vector_TimeToSpeed(float kmh, float seconds)
    {
        Assert.Equal(seconds, TimeTo(kmh * Kmh, VectorTuning.Values), 0.05f);
    }

    [Fact]
    public void Vector_ReachesExactlyItsTopSpeed_InAboutTenSeconds()
    {
        ShipTuning tuning = VectorTuning.Values;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f);
        float time = 0f;
        while (dynamics.Speed < tuning.MaxSpeed && time < 60f)
        {
            dynamics.Step(Throttle, tuning, Dt);
            time += Dt;
        }

        Assert.Equal(tuning.MaxSpeed, dynamics.Speed);
        Assert.Equal(10.26f, time, 0.05f);
    }

    // --- 11. Curva por nave: dentro del rango en las definiciones, y empuje mínimo común ---

    [Fact]
    public void AllShips_CurveWithinRange_AndCommonMinThrust()
    {
        foreach (ShipDefinition ship in ShipDefinitions.All)
        {
            Assert.True(AccelerationCurve.IsWithinRange(ship.Technical.AccelCurve), ship.Id);
            Assert.Equal(AccelerationCurve.DefaultMinThrust, ship.Technical.MinThrust);
        }
    }

    [Theory]
    [InlineData(1.5f, true)]
    [InlineData(3.0f, true)]
    [InlineData(1.4f, false)]
    [InlineData(3.1f, false)]
    public void CurveRange(float curve, bool within)
    {
        Assert.Equal(within, AccelerationCurve.IsWithinRange(curve));
    }

    // --- 6. Con daño, la curva va contra la punta reducida ---

    [Fact]
    public void WithDamage_ReachesTheReducedTopSpeed()
    {
        ShipTuning damaged = VectorTuning.Values;
        damaged.MaxSpeed *= 0.92f;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f);

        for (int i = 0; i < 60 * 20; i++)
            dynamics.Step(Throttle, damaged, Dt);

        Assert.Equal(damaged.MaxSpeed, dynamics.Speed);
    }

    [Fact]
    public void WithDamage_FactorUsesTheReducedTopSpeed()
    {
        ShipTuning damaged = VectorTuning.Values;
        damaged.MaxSpeed *= 0.92f;
        float speed = damaged.MaxSpeed / 2f;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, speed);

        dynamics.Step(Throttle, damaged, Dt);

        Assert.Equal(speed + damaged.Acceleration * 0.75f * Dt, dynamics.Speed, 3);
    }

    // --- 7. En derrape, la curva × el factor de aceleración del derrape ---

    [Fact]
    public void WhileDrifting_CurveTimesDriftFactor()
    {
        ShipTuning tuning = VectorTuning.Values;
        DriftTuning drift = CreateDrift();
        float speed = tuning.MaxSpeed / 2f;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, speed);

        dynamics.Step(new ShipInput { Drift = true, SteerLeft = true, Accelerate = true }, tuning, drift, 0.1f);

        float expected = speed + tuning.Acceleration * 0.75f * drift.AccelerationFactor * 0.1f - drift.FixedLoss * 0.1f;
        Assert.Equal(expected, dynamics.Speed, 3);
    }

    // --- 8. Boost ---

    [Fact]
    public void BoostGain_DoesNotGoThroughTheCurve()
    {
        ShipTuning tuning = VectorTuning.Values;
        float speed = tuning.MaxSpeed * 0.9f;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, speed);

        dynamics.Step(new ShipInput(), tuning, Dt, boostGain: 1f, boosting: true);

        Assert.Equal(speed + 1f, dynamics.Speed, 4);
    }

    [Fact]
    public void AfterBoost_AboveTop_ThrottleAddsNothing_AndBelowItAcceleratesWithTheCurve()
    {
        ShipTuning tuning = VectorTuning.Values;
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, tuning.MaxSpeed + 5f);

        // El exceso se pierde con su regla, hasta la punta y sin pasar de largo
        float previous = dynamics.Speed;
        for (int i = 0; i < 60 * 3; i++)
        {
            dynamics.Step(Throttle, tuning, Dt);
            Assert.True(dynamics.Speed <= previous);
            previous = dynamics.Speed;
        }
        Assert.Equal(tuning.MaxSpeed, dynamics.Speed);

        // Por debajo de la punta vuelve a acelerar con la curva
        float speed = tuning.MaxSpeed / 2f;
        dynamics.Reset(0f, speed);
        dynamics.Step(Throttle, tuning, Dt);
        Assert.Equal(speed + tuning.Acceleration * 0.75f * Dt, dynamics.Speed, 3);
    }

    // --- 9. Independiente del framerate ---

    [Fact]
    public void TimeTo300_SameAt30And144Fps()
    {
        float at30 = TimeTo(300f * Kmh, VectorTuning.Values, 1f / 30f);
        float at144 = TimeTo(300f * Kmh, VectorTuning.Values, 1f / 144f);

        Assert.True(MathF.Abs(at30 / at144 - 1f) < 0.02f, $"{at30} vs {at144}");
    }

    // --- 10. Casilla lineal ---

    [Fact]
    public void Linear_GivesExactlyTheOldDynamics()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);
        lab.SetLinearAcceleration(true);
        ShipTuning tuning = lab.Tuning(ShipCatalog.Ship01);
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f);

        // La regla de la 0.14.0, paso a paso: Acceleration × dt hasta la punta
        float old = 0f;
        for (int i = 0; i < 60 * 6; i++)
        {
            dynamics.Step(Throttle, tuning, Dt);
            old = MathF.Abs(tuning.MaxSpeed - old) <= tuning.Acceleration * Dt
                ? tuning.MaxSpeed
                : old + tuning.Acceleration * Dt;
            Assert.Equal(old, dynamics.Speed);
        }
    }

    [Fact]
    public void Linear_ReachesTopSpeedIn44Seconds()
    {
        ShipTuning tuning = VectorTuning.Values;
        tuning.AccelCurve = AccelerationCurve.Linear;

        Assert.Equal(4.4f, TimeTo(tuning.MaxSpeed, tuning), 0.05f);
    }

    [Fact]
    public void Lab_LinearOff_ByDefault_AndShipsKeepTheCurve()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        Assert.False(lab.LinearAcceleration);
        Assert.False(lab.Touched);
        foreach (ShipDefinition ship in ShipDefinitions.All)
            Assert.Equal(ship.Technical, lab.Tuning(ship.Id));
    }

    [Fact]
    public void Lab_Linear_AffectsTheThreeShips_AndTouches()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        Assert.True(lab.SetLinearAcceleration(true));

        Assert.True(lab.Touched);
        foreach (ShipDefinition ship in ShipDefinitions.All)
            Assert.Equal(AccelerationCurve.Linear, lab.Tuning(ship.Id).AccelCurve);
    }

    [Fact]
    public void Lab_LinearOffAgain_RestoresTheCurve_ButStaysTouched()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);
        lab.SetLinearAcceleration(true);

        Assert.True(lab.SetLinearAcceleration(false));

        Assert.True(lab.Touched);
        Assert.Equal(BladeShip.Definition.Technical.AccelCurve, lab.Tuning(ShipCatalog.Ship02).AccelCurve);
    }

    [Fact]
    public void Lab_LinearSameValue_DoesNotTouch()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        Assert.False(lab.SetLinearAcceleration(false));

        Assert.False(lab.Touched);
    }

    // --- 12. Curva por nave en el laboratorio ---

    [Fact]
    public void Lab_CurveChangesOnlyThatShip_AndResetRestoresIt()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        Assert.True(lab.SetAccelCurve(ShipCatalog.Ship02, 2.7f));

        Assert.True(lab.Touched);
        Assert.Equal(2.7f, lab.Tuning(ShipCatalog.Ship02).AccelCurve, 3);
        Assert.Equal(HammerShip.Definition.Technical.AccelCurve, lab.Tuning(ShipCatalog.Ship03).AccelCurve);
        Assert.Equal(VectorShip.Definition.Technical.AccelCurve, lab.Tuning(ShipCatalog.Ship01).AccelCurve);

        lab.Reset(ShipCatalog.Ship02);

        Assert.Equal(BladeShip.Definition.Technical, lab.Tuning(ShipCatalog.Ship02));
    }

    [Fact]
    public void Lab_VectorCurve_CanBeMoved_ButNotItsPercents()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        Assert.True(lab.SetAccelCurve(ShipCatalog.Ship01, 2.5f));

        Assert.Equal(2.5f, lab.Tuning(ShipCatalog.Ship01).AccelCurve, 3);
        Assert.False(lab.SetPercent(ShipCatalog.Ship01, ShipParameter.MaxSpeed, 5));
    }

    [Theory]
    [InlineData(0.2f, 1f)]
    [InlineData(9f, 4f)]
    [InlineData(2.34f, 2.3f)]
    public void Lab_Curve_ClampedAndSnappedToTheStep(float set, float expected)
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        lab.SetAccelCurve(ShipCatalog.Ship02, set);

        Assert.Equal(expected, lab.AccelCurve(ShipCatalog.Ship02), 3);
    }

    [Fact]
    public void Lab_SameCurve_DoesNotTouch()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        Assert.False(lab.SetAccelCurve(ShipCatalog.Ship02, BladeShip.Definition.Technical.AccelCurve));

        Assert.False(lab.Touched);
    }

    [Fact]
    public void Lab_CurveOutOfRange_IsMarked()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);
        Assert.False(lab.IsCurveOutOfRange(ShipCatalog.Ship02));

        lab.SetAccelCurve(ShipCatalog.Ship02, 3.5f);

        Assert.True(lab.IsCurveOutOfRange(ShipCatalog.Ship02));
        Assert.False(lab.IsCurveOutOfRange(ShipCatalog.Ship03));
    }

    [Fact]
    public void Lab_MinThrust_IsCommon_Clamped_AndResetRestoresIt()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);
        Assert.Equal(10, lab.MinThrustPercent);

        Assert.True(lab.SetMinThrustPercent(20));
        Assert.True(lab.Touched);
        foreach (ShipDefinition ship in ShipDefinitions.All)
            Assert.Equal(0.20f, lab.Tuning(ship.Id).MinThrust, 3);

        lab.SetMinThrustPercent(90);
        Assert.Equal(TuningLab.MinThrustMaxPercent, lab.MinThrustPercent);

        lab.Reset(ShipCatalog.Ship01);
        Assert.Equal(10, lab.MinThrustPercent);
    }

    [Fact]
    public void Lab_LinearWinsOverTheCurve_WhichComesBackAfterwards()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);
        lab.SetAccelCurve(ShipCatalog.Ship02, 2.5f);

        lab.SetLinearAcceleration(true);
        Assert.Equal(AccelerationCurve.Linear, lab.Tuning(ShipCatalog.Ship02).AccelCurve);

        lab.SetLinearAcceleration(false);
        Assert.Equal(2.5f, lab.Tuning(ShipCatalog.Ship02).AccelCurve, 3);
    }

    // --- 13. F11 ---

    [Fact]
    public void DumpText_IncludesTheCurves_AndMinThrust_AndMarksOutOfRange()
    {
        var lab = new TuningLab(ShipDefinitions.All, ShipCatalog.Ship01);

        string clean = lab.DumpText();
        Assert.Contains("ship_01: AccelCurve = 2.0f", clean);
        Assert.Contains("ship_02: AccelCurve = 2.0f", clean);
        Assert.Contains("ship_03: AccelCurve = 1.5f", clean);
        Assert.Contains("MinThrust = 0.10f", clean);
        Assert.DoesNotContain("OUT OF RANGE", clean);

        lab.SetAccelCurve(ShipCatalog.Ship03, 3.5f);
        lab.SetMinThrustPercent(15);

        string changed = lab.DumpText();
        Assert.Contains("ship_03: AccelCurve = 3.5f  // OUT OF RANGE", changed);
        Assert.Contains("MinThrust = 0.15f", changed);
    }

    // --- 14 y 15. ShipStats ---

    [Fact]
    public void Stats_ZeroTo200_ComesFromIntegratingTheCurve()
    {
        ShipTuning vector = VectorTuning.Values;

        // El cálculo analítico con la curva da 2,447 s; el lineal, 2,222 s
        Assert.Equal(2.447f, ShipStats.SecondsTo(vector, 200f), 0.01f);
        Assert.Equal("0–200 · 2.4 S", StatValue(vector, vector));
        Assert.Equal(5, StatSegments(vector, vector));
    }

    [Fact]
    public void Stats_LinearTuning_GivesTheLinearTime()
    {
        ShipTuning linear = VectorTuning.Values;
        linear.AccelCurve = AccelerationCurve.Linear;

        Assert.Equal(2.222f, ShipStats.SecondsTo(linear, 200f), 0.01f);
    }

    [Fact]
    public void Stats_Bar_ComparedByTime()
    {
        ShipTuning vector = VectorTuning.Values;
        ShipTuning quicker = vector;
        quicker.AccelCurve = 3f;
        ShipTuning slower = vector;
        slower.AccelCurve = 1.5f;

        Assert.True(ShipStats.SecondsTo(quicker, 200f) < ShipStats.SecondsTo(vector, 200f));
        Assert.True(StatSegments(quicker, vector) > 5);
        Assert.True(ShipStats.SecondsTo(slower, 200f) > ShipStats.SecondsTo(vector, 200f));
        Assert.True(StatSegments(slower, vector) < 5);
    }

    [Fact]
    public void Stats_ShipThatNeverGetsThere_DoesNotHang()
    {
        ShipTuning slow = VectorTuning.Values;
        slow.MaxSpeed = 30f;

        Assert.Equal(60f, ShipStats.SecondsTo(slow, 200f), 0.1f);
    }

    private static string StatValue(in ShipTuning ship, in ShipTuning reference) =>
        Array.Find(System.Linq.Enumerable.ToArray(ShipStats.Compute(ship, reference)), s => s.Label == ShipStats.Acceleration).Value;

    private static int StatSegments(in ShipTuning ship, in ShipTuning reference) =>
        Array.Find(System.Linq.Enumerable.ToArray(ShipStats.Compute(ship, reference)), s => s.Label == ShipStats.Acceleration).Segments;
}
