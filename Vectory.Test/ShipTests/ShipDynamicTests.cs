
using System;
using System.Numerics;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class ShipDynamicsTests
{
    private static ShipTuning CreateTuning() => new()
    {
        MaxSpeed = 70f,
        Acceleration = 20f,
        BrakeDeceleration = 40f,
        Drag = 10f,
        OverspeedDecay = 5f,
        TurnRate = 1.5f,
        SteerResponse = 5f,
        LateralGrip = 5f
    };

    [Fact]
    public void Reset_ClearsMotionAndSetsYaw()
    {
        var dynamics = new ShipDynamics();
        dynamics.SetVelocity(new Vector2(10f, 5f));

        dynamics.Reset(0.75f);

        Assert.Equal(0f, dynamics.Speed);
        Assert.Equal(0f, dynamics.Steer);
        Assert.Equal(0.75f, dynamics.Yaw);
        Assert.Equal(0f, dynamics.TurnFactor);
        Assert.Equal(Vector2.Zero, dynamics.Velocity);
        Assert.Equal(0f, dynamics.RealSpeed);
    }

    [Fact]
    public void Accelerate_IncreasesSpeedWithoutExceedingMaxSpeed()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        var input = new ShipInput { Accelerate = true };

        dynamics.Step(input, tuning, 1f);

        Assert.Equal(20f, dynamics.Speed);

        for (int i = 0; i < 10; i++)
            dynamics.Step(input, tuning, 1f);

        Assert.Equal(tuning.MaxSpeed, dynamics.Speed);
    }

    [Fact]
    public void Brake_ReducesSpeedWithoutGoingBelowZero()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        var accelerate = new ShipInput { Accelerate = true };

        dynamics.Step(accelerate, tuning, 2f);
        Assert.Equal(40f, dynamics.Speed);

        var brake = new ShipInput { Brake = true };
        dynamics.Step(brake, tuning, 0.5f);

        Assert.Equal(20f, dynamics.Speed);

        dynamics.Step(brake, tuning, 1f);

        Assert.Equal(0f, dynamics.Speed);
    }

    [Fact]
    public void NoInput_AppliesDragGradually()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        var accelerate = new ShipInput { Accelerate = true };

        dynamics.Step(accelerate, tuning, 2f);
        Assert.Equal(40f, dynamics.Speed);

        dynamics.Step(default, tuning, 1f);

        Assert.Equal(30f, dynamics.Speed);
    }

    [Fact]
    public void ReduceSpeed_DoesNotAllowNegativeSpeed()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 1f);
        Assert.Equal(20f, dynamics.Speed);

        dynamics.ReduceSpeed(5f);
        Assert.Equal(15f, dynamics.Speed);

        dynamics.ReduceSpeed(100f);
        Assert.Equal(0f, dynamics.Speed);
    }

    // Deja la nave a la velocidad punta y le suma un impulso de boost
    private static ShipDynamics AboveMaxSpeed(ShipTuning tuning, float extra)
    {
        var dynamics = new ShipDynamics();
        for (int i = 0; i < 10; i++)
            dynamics.Step(new ShipInput { Accelerate = true }, tuning, 1f);

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 0.1f, extra);
        return dynamics;
    }

    [Fact]
    public void BoostGain_IsAddedOnTopOfMaxSpeed()
    {
        var tuning = CreateTuning();

        var dynamics = AboveMaxSpeed(tuning, 20f);

        Assert.Equal(90f, dynamics.Speed);
    }

    [Fact]
    public void BoostGain_WorksFromStandstill()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();

        dynamics.Step(default, tuning, 0.1f, 15f);

        Assert.Equal(15f, dynamics.Speed);
    }

    [Fact]
    public void AboveMaxSpeed_DuringRamp_KeepsTheExcessAndAddsTheGain()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 1f, 5f);

        Assert.Equal(95f, dynamics.Speed);
    }

    [Fact]
    public void Hold_AboveMaxSpeed_WithThrottle_KeepsSpeedWithoutAdding()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 1f, 0f, boosting: true);

        Assert.Equal(90f, dynamics.Speed);
    }

    [Fact]
    public void Hold_AboveMaxSpeed_WithoutThrottle_KeepsSpeed()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(default, tuning, 1f, 0f, boosting: true);

        Assert.Equal(90f, dynamics.Speed);
    }

    [Fact]
    public void Hold_BelowMaxSpeed_WithoutThrottle_DoesNotApplyDrag()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        dynamics.Step(default, tuning, 0.1f, 30f);

        dynamics.Step(default, tuning, 1f, 0f, boosting: true);

        Assert.Equal(30f, dynamics.Speed);
    }

    [Fact]
    public void Hold_BelowMaxSpeed_WithThrottle_AcceleratesUpToMaxSpeed()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        dynamics.Step(default, tuning, 0.1f, 30f);
        var accelerate = new ShipInput { Accelerate = true };

        dynamics.Step(accelerate, tuning, 1f, 0f, boosting: true);
        Assert.Equal(50f, dynamics.Speed);

        dynamics.Step(accelerate, tuning, 2f, 0f, boosting: true);
        Assert.Equal(tuning.MaxSpeed, dynamics.Speed);
    }

    [Fact]
    public void Hold_Braking_BrakesNormally()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(new ShipInput { Brake = true }, tuning, 0.5f, 0f, boosting: true);

        Assert.Equal(70f, dynamics.Speed);
    }

    [Fact]
    public void AboveMaxSpeed_Accelerating_LosesExcessGradually()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 1f);

        // El motor no añade velocidad; pierde OverspeedDecay (5) en 1 s
        Assert.Equal(85f, dynamics.Speed);
    }

    [Fact]
    public void AboveMaxSpeed_WithoutThrottle_UsesDragIfLarger()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(default, tuning, 1f);

        // Drag (10) es mayor que OverspeedDecay (5)
        Assert.Equal(80f, dynamics.Speed);
    }

    [Fact]
    public void AboveMaxSpeed_Braking_BrakesNormally()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        dynamics.Step(new ShipInput { Brake = true }, tuning, 0.5f);

        Assert.Equal(70f, dynamics.Speed);
    }

    [Fact]
    public void AboveMaxSpeed_DecayStopsAtMaxSpeed()
    {
        var tuning = CreateTuning();
        var dynamics = AboveMaxSpeed(tuning, 20f);

        for (int i = 0; i < 10; i++)
            dynamics.Step(new ShipInput { Accelerate = true }, tuning, 1f);

        Assert.Equal(tuning.MaxSpeed, dynamics.Speed);
    }

    [Fact]
    public void TurnFactor_IsAlwaysBetweenZeroAndOne()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();

        dynamics.Step(default, tuning, 0.1f);
        Assert.InRange(dynamics.TurnFactor, 0f, 1f);

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 0.5f);
        Assert.Equal(1f, dynamics.TurnFactor);
    }

    [Fact]
    public void SteerLeft_ChangesYawInPositiveDirection()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        var input = new ShipInput
        {
            Accelerate = true,
            SteerLeft = true
        };

        dynamics.Step(input, tuning, 1f);

        Assert.True(dynamics.Yaw > 0f);
        Assert.InRange(dynamics.Steer, 0f, 1f);
    }

    [Fact]
    public void AbsorbCollision_DoesNotIncreaseEngineSpeed()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 2f);
        Assert.Equal(40f, dynamics.Speed);

        // The ship is moving backwards relative to its forward direction.
        dynamics.SetVelocity(-dynamics.Forward * 10f);

        dynamics.AbsorbCollision();

        Assert.Equal(0f, dynamics.Speed);
    }

    [Fact]
    public void Velocity_GraduallyApproachesForwardMotion()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();

        dynamics.SetVelocity(new Vector2(20f, 0f));

        dynamics.Step(new ShipInput { Accelerate = true }, tuning, 0.1f);

        Assert.NotEqual(Vector2.Zero, dynamics.Velocity);
        Assert.True(dynamics.Velocity.X < 20f);
        Assert.True(dynamics.RealSpeed > 0f);
    }
    [Fact]
    public void SteerLeftAndRightTogether_CancelEachOther()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();

        dynamics.Step(
            new ShipInput { Accelerate = true },
            tuning,
            1f);

        dynamics.Step(
            new ShipInput
            {
                SteerLeft = true,
                SteerRight = true
            },
            tuning,
            0.1f);

        Assert.Equal(0f, dynamics.Steer);
    }
}
