
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
        BoostMaxSpeed = 100f,
        Acceleration = 20f,
        BoostAcceleration = 30f,
        BrakeDeceleration = 40f,
        Drag = 10f,
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

    [Fact]
    public void Boost_UsesBoostSpeedAndAcceleration()
    {
        var dynamics = new ShipDynamics();
        var tuning = CreateTuning();
        var boost = new ShipInput { Boost = true };

        dynamics.Step(boost, tuning, 1f);

        Assert.Equal(30f, dynamics.Speed);

        for (int i = 0; i < 10; i++)
            dynamics.Step(boost, tuning, 1f);

        Assert.Equal(tuning.BoostMaxSpeed, dynamics.Speed);
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
