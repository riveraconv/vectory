using System;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Derrape e inclinación (spec docs/conduccion-avanzada.md v1.1, sección 9)
public class DriftTests
{
    private const float Kmh = 1f / 3.6f;
    private const float Dt = 1f / 60f;

    private static ShipTuning CreateTuning() => new()
    {
        MaxSpeed = 110f,
        Acceleration = 25f,
        BrakeDeceleration = 45f,
        Drag = 5f,
        OverspeedDecay = 20f * Kmh,
        TurnRate = 1.6f,
        SteerResponse = 6f,
        LateralGrip = 5f
    };

    // Valores de la spec v1.1 (los del Player por defecto), en m/s y radianes
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

    private static readonly ShipInput DriftRight = new() { Drift = true, SteerRight = true };
    private static readonly ShipInput DriftLeft = new() { Drift = true, SteerLeft = true };

    // Nave mirando a -Z (yaw 0) a "kmh" km/h, ya derrapando hacia la derecha
    private static ShipDynamics StartDrifting(float kmh, in ShipTuning tuning, in DriftTuning drift)
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, kmh * Kmh);
        dynamics.Step(DriftRight, tuning, drift, Dt);
        Assert.True(dynamics.Drifting);
        return dynamics;
    }

    // Velocidad de "speed" m/s con un ángulo de deslizamiento "angle" (positivo = hacia la derecha) respecto al morro
    private static Vector2 SlidingVelocity(ShipDynamics dynamics, float speed, float angle)
    {
        return (dynamics.Forward * MathF.Cos(angle) + dynamics.Right * MathF.Sin(angle)) * speed;
    }

    // --- Conducción normal ---

    [Fact]
    public void WithoutDriftKey_DrivingIsIdenticalToTheModelWithoutDrift()
    {
        var withDrift = new ShipDynamics();
        var withoutDrift = new ShipDynamics();
        withDrift.Reset(0.3f, 150f * Kmh);
        withoutDrift.Reset(0.3f, 150f * Kmh);
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();

        var inputs = new[]
        {
            new ShipInput { Accelerate = true, SteerLeft = true },
            new ShipInput { Brake = true, SteerRight = true },
            new ShipInput { SteerLeft = true },
            new ShipInput()
        };

        for (int i = 0; i < 240; i++)
        {
            ShipInput input = inputs[i / 60];
            withDrift.Step(input, tuning, drift, Dt);
            withoutDrift.Step(input, tuning, Dt);
        }

        Assert.False(withDrift.Drifting);
        Assert.Equal(1f, withDrift.GripFactor);
        Assert.Equal(withoutDrift.Velocity, withDrift.Velocity);
        Assert.Equal(withoutDrift.Yaw, withDrift.Yaw);
        Assert.Equal(withoutDrift.Speed, withDrift.Speed);
    }

    [Fact]
    public void SlipAngle_IsZeroBelow20Kmh()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f);

        dynamics.SetVelocity(SlidingVelocity(dynamics, 19f * Kmh, 0.5f));
        Assert.Equal(0f, dynamics.SlipAngle);

        dynamics.SetVelocity(SlidingVelocity(dynamics, 30f * Kmh, 0.5f));
        Assert.Equal(0.5f, dynamics.SlipAngle, 4);
    }

    [Fact]
    public void SlipAngle_IsPositiveWhenSlidingRight()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(1.2f);

        dynamics.SetVelocity(SlidingVelocity(dynamics, 100f * Kmh, -0.3f));

        Assert.Equal(-0.3f, dynamics.SlipAngle, 4);
    }

    // --- Entrada y salida ---

    [Fact]
    public void DriftKeyAndSteerAt150Kmh_StartsDrift()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);

        dynamics.Step(DriftLeft, CreateTuning(), CreateDrift(), Dt);

        Assert.True(dynamics.Drifting);
        Assert.Equal(0f, dynamics.GripFactor);
    }

    [Fact]
    public void DriftKeyAndSteerAt100Kmh_DoesNotDrift()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 100f * Kmh);

        dynamics.Step(DriftRight, CreateTuning(), CreateDrift(), Dt);

        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void DriftKeyWithoutSteer_DoesNotDrift()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);

        dynamics.Step(new ShipInput { Drift = true }, CreateTuning(), CreateDrift(), Dt);

        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void SteerAndBrakeWithoutDriftKey_DoesNotDrift()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);

        dynamics.Step(new ShipInput { Brake = true, SteerRight = true }, CreateTuning(), CreateDrift(), Dt);

        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void BothSteerKeys_DoNotCountAsSteering()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);

        dynamics.Step(new ShipInput { Drift = true, SteerLeft = true, SteerRight = true }, CreateTuning(), CreateDrift(), Dt);

        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void DisabledDrift_NeverDrifts()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 200f * Kmh);

        dynamics.Step(DriftRight, CreateTuning(), default, Dt);

        Assert.False(dynamics.Drifting);
        Assert.Equal(1f, dynamics.GripFactor);
    }

    [Fact]
    public void DriftRule_UsesEnterSpeedToStartAndExitSpeedToContinue()
    {
        DriftTuning drift = CreateDrift();

        Assert.True(DriftRule.ShouldDrift(false, true, drift.EnterSpeed, drift));
        Assert.False(DriftRule.ShouldDrift(false, true, drift.EnterSpeed - 0.01f, drift));
        Assert.True(DriftRule.ShouldDrift(true, true, drift.ExitSpeed, drift));
        Assert.False(DriftRule.ShouldDrift(true, true, drift.ExitSpeed - 0.01f, drift));
        Assert.False(DriftRule.ShouldDrift(true, false, drift.EnterSpeed * 2f, drift));
        Assert.False(DriftRule.ShouldDrift(false, true, drift.EnterSpeed * 2f, default));
    }

    [Fact]
    public void BrakeWhileDrifting_BrakesAtFullStrength()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        float before = dynamics.Speed;

        dynamics.Step(new ShipInput { Drift = true, SteerRight = true, Brake = true }, tuning, drift, 0.1f);

        // Frenada completa + pérdida fija (con la velocidad aún alineada, sin pérdida por ángulo)
        Assert.True(dynamics.Drifting);
        Assert.Equal(before - tuning.BrakeDeceleration * 0.1f - drift.FixedLoss * 0.1f, dynamics.Speed, 3);
    }

    [Fact]
    public void ReleasingSteer_ExitsAndGripRecoversLinearly()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(200f, tuning, drift);
        var driftKeyOnly = new ShipInput { Drift = true };

        dynamics.Step(driftKeyOnly, tuning, drift, 0.1f);
        Assert.False(dynamics.Drifting);
        Assert.Equal(0.1f / 3f, dynamics.GripFactor, 4);

        for (int i = 0; i < 14; i++)
            dynamics.Step(driftKeyOnly, tuning, drift, 0.1f);
        Assert.Equal(0.5f, dynamics.GripFactor, 3);

        for (int i = 0; i < 15; i++)
            dynamics.Step(driftKeyOnly, tuning, drift, 0.1f);
        Assert.Equal(1f, dynamics.GripFactor);
    }

    [Fact]
    public void ReleasingDriftKey_ExitsDrift()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(200f, tuning, drift);

        dynamics.Step(new ShipInput { SteerRight = true }, tuning, drift, Dt);

        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void DroppingBelowExitSpeed_ExitsDriftEvenWithKeysHeld()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(150f, tuning, drift);
        var brakingDrift = new ShipInput { Drift = true, SteerRight = true, Brake = true };

        float speedBeforeExit = 0f;
        for (int i = 0; i < 600 && dynamics.Drifting; i++)
        {
            speedBeforeExit = dynamics.RealSpeed;
            dynamics.Step(brakingDrift, tuning, drift, Dt);
        }

        Assert.False(dynamics.Drifting);
        Assert.True(speedBeforeExit < drift.ExitSpeed);

        // Por debajo de la velocidad de entrada no vuelve a entrar
        dynamics.Step(DriftRight, tuning, drift, Dt);
        Assert.False(dynamics.Drifting);
    }

    // --- Durante el derrape ---

    [Fact]
    public void SlipAngle_NeverExceedsMaximum()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(250f, tuning, drift);
        var input = new ShipInput { Drift = true, SteerRight = true, Accelerate = true };

        float maxSeen = 0f;
        for (int i = 0; i < 180 && dynamics.Drifting; i++)
        {
            dynamics.Step(input, tuning, drift, Dt);
            maxSeen = MathF.Max(maxSeen, MathF.Abs(dynamics.SlipAngle));
            Assert.True(MathF.Abs(dynamics.SlipAngle) <= drift.MaxSlipAngle + 1e-4f);
        }

        // El tope se llega a usar
        Assert.True(maxSeen > drift.MaxSlipAngle * 0.99f);
    }

    [Fact]
    public void SpeedLoss_IsFixedAtZeroAngle()
    {
        ShipTuning tuning = CreateTuning();
        tuning.Drag = 0f;
        DriftTuning drift = CreateDrift();
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        float before = dynamics.Speed;

        dynamics.Step(DriftLeft, tuning, drift, 0.1f);

        Assert.True(dynamics.Drifting);
        Assert.Equal(5f, (before - dynamics.Speed) / 0.1f / Kmh, 3);
    }

    [Theory]
    [InlineData(0.5f, 10f)]   // a mitad del ángulo máximo: 5 + 10 × 0,5
    [InlineData(1f, 15f)]     // al máximo: 5 + 10
    public void SpeedLoss_GrowsWithSlipAngle(float slipFraction, float expectedKmhPerSecond)
    {
        // Sin giro del rumbo ni arrastre, para medir solo la pérdida del motor
        ShipTuning tuning = CreateTuning();
        tuning.Drag = 0f;
        tuning.TurnRate = 0f;
        DriftTuning drift = CreateDrift();
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        dynamics.SetVelocity(SlidingVelocity(dynamics, 150f * Kmh, drift.MaxSlipAngle * slipFraction * 0.999f));
        float before = dynamics.Speed;

        // Desliza a la derecha y gira a la izquierda (sin contravolante)
        dynamics.Step(DriftLeft, tuning, drift, Dt);

        Assert.True(dynamics.Drifting);
        float loss = before - dynamics.Speed;
        if (slipFraction < 1f)
            Assert.Equal(expectedKmhPerSecond, loss / Dt / Kmh, 1);
        else
            // Al máximo, frenar la parte hacia delante pasa del tope y recorta también la lateral: pierde al menos eso
            Assert.True(loss / Dt / Kmh >= expectedKmhPerSecond * 0.99f);
    }

    [Fact]
    public void DriftSpeedLoss_SlowsTheRealSpeed()
    {
        ShipTuning tuning = CreateTuning();
        tuning.Drag = 0f;
        DriftTuning drift = CreateDrift();
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        float before = dynamics.RealSpeed;

        dynamics.Step(DriftLeft, tuning, drift, 0.1f);

        Assert.Equal(before - drift.FixedLoss * 0.1f, dynamics.RealSpeed, 3);
        Assert.Equal(dynamics.RealSpeed, dynamics.Speed, 3);
    }

    [Fact]
    public void EngineSpeed_NeverExceedsRealSpeedWhileDrifting()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(250f, tuning, drift);
        var input = new ShipInput { Drift = true, SteerRight = true, Accelerate = true };

        for (int i = 0; i < 180 && dynamics.Drifting; i++)
        {
            dynamics.Step(input, tuning, drift, Dt);
            if (dynamics.Drifting)
                Assert.True(dynamics.Speed <= dynamics.RealSpeed + 1e-4f);
        }
    }

    [Fact]
    public void AfterDrift_ShipDoesNotSpeedUpOnItsOwn()
    {
        ShipTuning tuning = CreateTuning();
        tuning.Drag = 0f;
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(250f, tuning, drift);
        for (int i = 0; i < 30; i++)
            dynamics.Step(DriftRight, tuning, drift, Dt);
        Assert.True(dynamics.Drifting);

        // Sale y, sin acelerar, la velocidad real no sube mientras vuelve el agarre
        float atExit = dynamics.RealSpeed;
        for (int i = 0; i < 240; i++)
        {
            dynamics.Step(default, tuning, drift, Dt);
            Assert.True(dynamics.RealSpeed <= atExit + 1e-3f);
        }
    }

    [Fact]
    public void Countersteer_RaisesGripAndReducesSlipFaster()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        DriftTuning noCountersteerGrip = drift;
        noCountersteerGrip.CountersteerGrip = drift.Grip;

        var withGrip = new ShipDynamics();
        var withoutGrip = new ShipDynamics();
        foreach (ShipDynamics d in new[] { withGrip, withoutGrip })
        {
            d.Reset(0f, 150f * Kmh);
            d.SetVelocity(SlidingVelocity(d, 150f * Kmh, 0.6f));
        }
        float startSlip = withGrip.SlipAngle;

        // Desliza a la derecha y gira a la derecha: contravolante
        for (int i = 0; i < 10; i++)
        {
            withGrip.Step(DriftRight, tuning, drift, Dt);
            withoutGrip.Step(DriftRight, tuning, noCountersteerGrip, Dt);
        }

        Assert.True(withGrip.Drifting);
        Assert.Equal(drift.CountersteerGrip, withGrip.GripFactor);
        Assert.True(withGrip.SlipAngle < withoutGrip.SlipAngle);
        Assert.True(withGrip.SlipAngle < startSlip);
    }

    [Fact]
    public void SteeringIntoTheSlide_KeepsDriftGrip()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        dynamics.SetVelocity(SlidingVelocity(dynamics, 150f * Kmh, 0.4f));

        // Desliza a la derecha y gira a la izquierda: derrape normal, sin contravolante
        dynamics.Step(DriftLeft, tuning, drift, Dt);

        Assert.True(dynamics.Drifting);
        Assert.Equal(drift.Grip, dynamics.GripFactor);
    }

    [Fact]
    public void DriftTurnRate_Is1Point4TimesNormal_AndScalesWithDamage()
    {
        ShipTuning tuning = CreateTuning();
        ShipTuning damaged = tuning;
        damaged.TurnRate *= 0.85f;
        DriftTuning drift = CreateDrift();

        var normal = new ShipDynamics();
        var drifting = new ShipDynamics();
        var damagedDrifting = new ShipDynamics();
        normal.Reset(0f, 150f * Kmh);
        drifting.Reset(0f, 150f * Kmh);
        damagedDrifting.Reset(0f, 150f * Kmh);

        normal.Step(new ShipInput { SteerLeft = true }, tuning, drift, Dt);
        drifting.Step(DriftLeft, tuning, drift, Dt);
        damagedDrifting.Step(DriftLeft, damaged, drift, Dt);

        Assert.True(drifting.Drifting);
        Assert.Equal(1.4f, drifting.Yaw / normal.Yaw, 4);
        Assert.Equal(1.4f * 0.85f, damagedDrifting.Yaw / normal.Yaw, 4);
    }

    [Fact]
    public void DriftTurnRate_AlsoScalesWithImpactControl()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        var full = new ShipDynamics();
        var reduced = new ShipDynamics();
        full.Reset(0f, 150f * Kmh);
        reduced.Reset(0f, 150f * Kmh);

        full.Step(DriftLeft, tuning, drift, Dt);
        reduced.Step(DriftLeft, tuning, drift, Dt, control: 0.5f);

        Assert.Equal(0.5f, reduced.Yaw / full.Yaw, 4);
    }

    [Fact]
    public void AccelerationWhileDrifting_IsReduced()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        float before = dynamics.Speed;

        dynamics.Step(new ShipInput { Drift = true, SteerLeft = true, Accelerate = true }, tuning, drift, 0.1f);

        float expected = before + tuning.Acceleration * drift.AccelerationFactor * 0.1f - drift.FixedLoss * 0.1f;
        Assert.Equal(expected, dynamics.Speed, 3);
    }

    // --- Otros sistemas ---

    [Fact]
    public void BoostWhileDrifting_AddsToSpeedAlongTheNose()
    {
        ShipTuning tuning = CreateTuning();
        tuning.Drag = 0f;
        DriftTuning drift = CreateDrift();
        ShipDynamics boosted = StartDrifting(200f, tuning, drift);
        ShipDynamics plain = StartDrifting(200f, tuning, drift);
        foreach (ShipDynamics d in new[] { boosted, plain })
            d.SetVelocity(SlidingVelocity(d, d.RealSpeed, -0.5f));

        // Desliza a la izquierda y gira a la derecha (sin contravolante), con y sin boost.
        // El impulso va hacia donde apunta el morro al empezar el paso.
        Vector2 forward = boosted.Forward;
        Vector2 right = boosted.Right;
        boosted.Step(DriftRight, tuning, drift, Dt, boostGain: 5f);
        plain.Step(DriftRight, tuning, drift, Dt);

        Assert.True(boosted.Drifting);
        Vector2 gain = boosted.Velocity - plain.Velocity;
        float forwardGain = Vector2.Dot(gain, forward);
        float lateralGain = Vector2.Dot(gain, right);
        Assert.Equal(5f, forwardGain, 3);
        Assert.Equal(0f, lateralGain, 3);
    }

    [Fact]
    public void EndDrift_StopsDriftAndNeedsReleaseToReenter()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(250f, tuning, drift);

        dynamics.EndDrift();
        Assert.False(dynamics.Drifting);

        // Con las teclas aún pulsadas y velocidad de sobra, no vuelve a entrar
        dynamics.Step(DriftRight, tuning, drift, Dt);
        Assert.False(dynamics.Drifting);
        Assert.True(dynamics.GripFactor > drift.Grip && dynamics.GripFactor < 1f);

        // Al soltar el giro y volver a pulsarlo, sí
        dynamics.Step(new ShipInput { Drift = true }, tuning, drift, Dt);
        dynamics.Step(DriftRight, tuning, drift, Dt);
        Assert.True(dynamics.Drifting);
    }

    [Fact]
    public void BarrierContact_DoesNotEndDriftByItself()
    {
        // El roce llega como ApplyImpact sin EndDrift: el derrape sigue
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(200f, tuning, drift);

        dynamics.ApplyImpact(dynamics.Velocity * 0.96f, dynamics.Yaw, 0.96f, 0f);
        dynamics.Step(DriftRight, tuning, drift, Dt);

        Assert.True(dynamics.Drifting);
    }

    [Fact]
    public void Reset_ClearsDriftSlipAndGrip()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(200f, tuning, drift);
        for (int i = 0; i < 30; i++)
            dynamics.Step(DriftRight, tuning, drift, Dt);
        Assert.NotEqual(0f, dynamics.SlipAngle);

        dynamics.Reset(0.5f);

        Assert.False(dynamics.Drifting);
        Assert.Equal(1f, dynamics.GripFactor);
        Assert.Equal(0f, dynamics.SlipAngle);
    }

    [Fact]
    public void Reset_AfterEndDrift_AllowsDriftingAgainRightAway()
    {
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(200f, tuning, drift);
        dynamics.EndDrift();

        dynamics.Reset(0f, 200f * Kmh);
        dynamics.Step(DriftRight, tuning, drift, Dt);

        Assert.True(dynamics.Drifting);
    }

    [Fact]
    public void WithoutControl_CannotDriftAndGripRecoversNormally()
    {
        // Sin control (previa o carrera terminada) el Player pasa una entrada vacía
        ShipTuning tuning = CreateTuning();
        DriftTuning drift = CreateDrift();
        ShipDynamics dynamics = StartDrifting(200f, tuning, drift);

        dynamics.Step(default, tuning, drift, 0.1f);

        Assert.False(dynamics.Drifting);
        Assert.Equal(0.1f / 3f, dynamics.GripFactor, 4);

        var idle = new ShipDynamics();
        idle.Reset(0f, 200f * Kmh);
        idle.Step(default, tuning, drift, Dt);
        Assert.False(idle.Drifting);
    }

    // --- Inclinación ---

    private static BankSettings CreateBank() => new()
    {
        MaxNormal = 25f * MathF.PI / 180f,
        MaxDrift = 20f * MathF.PI / 180f,
        SmoothTime = 0.15f
    };

    [Fact]
    public void BankTarget_IsZeroWithoutSteering()
    {
        Assert.Equal(0f, ShipBank.Target(0f, 1f, false, CreateBank()));
        Assert.Equal(0f, ShipBank.Target(0f, 1f, true, CreateBank()));
    }

    [Fact]
    public void BankTarget_ReachesMaximumAtFullSteerAndTopSpeed()
    {
        BankSettings bank = CreateBank();

        Assert.Equal(bank.MaxNormal, ShipBank.Target(1f, 1f, false, bank), 5);
        Assert.Equal(-bank.MaxNormal, ShipBank.Target(-1f, 1f, false, bank), 5);
        Assert.Equal(bank.MaxDrift, ShipBank.Target(1f, 1f, true, bank), 5);
    }

    [Fact]
    public void BankTarget_ScalesWithSpeedUpToTopSpeed()
    {
        BankSettings bank = CreateBank();

        Assert.Equal(bank.MaxNormal * 0.5f, ShipBank.Target(1f, 0.5f, false, bank), 5);
        Assert.Equal(bank.MaxNormal * 0.25f, ShipBank.Target(0.5f, 0.5f, false, bank), 5);
        Assert.Equal(bank.MaxNormal, ShipBank.Target(1f, 1.8f, false, bank), 5);
    }

    [Fact]
    public void Bank_ApproachesTargetSmoothly()
    {
        BankSettings bank = CreateBank();
        var shipBank = new ShipBank();
        float target = ShipBank.Target(1f, 1f, false, bank);

        shipBank.Update(1f, 1f, false, bank, Dt);
        Assert.True(shipBank.Angle > 0f && shipBank.Angle < target * 0.2f);

        // Tras una constante de tiempo, ~63 %
        var oneTimeConstant = new ShipBank();
        for (int i = 0; i < 9; i++)
            oneTimeConstant.Update(1f, 1f, false, bank, bank.SmoothTime / 9f);
        Assert.Equal(target * (1f - MathF.Exp(-1f)), oneTimeConstant.Angle, 4);

        for (int i = 0; i < 120; i++)
            shipBank.Update(1f, 1f, false, bank, Dt);
        Assert.Equal(target, shipBank.Angle, 3);

        shipBank.Reset();
        Assert.Equal(0f, shipBank.Angle);
    }
}
