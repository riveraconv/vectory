using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.ShipTests;

// Paso de física de una nave sin nodos (ShipPhysics) y colocación sobre la pista (TrackPlacement).
public class ShipPhysicsTests
{
    private const float Dt = 1f / 60f;
    private const float Hover = 2f;

    private static readonly ShipDefinition Vector = ShipDefinitions.Find("ship_01");
    private static readonly ShipPhysicsSettings Settings = ShipPhysicsSettings.Default;
    private static readonly ShipInput Throttle = new() { Accelerate = true };

    private static float YawAlong(FramePoint f) => MathF.Atan2(-f.PlanTangent.X, -f.PlanTangent.Z);

    private static void Run(ShipPhysics physics, ref Vector3 position, in ShipInput input, int steps, bool canDrive = true, bool boost = false)
    {
        for (int i = 0; i < steps; i++)
            physics.Move(ref position, input, boost && i == 0, canDrive, Vector.Technical, Vector.Visual.CollisionHalfWidth, Settings, Dt);
    }

    // Nave colocada en el suelo, en un punto de la pista, mirando en el sentido de la marcha
    private static ShipPhysics PlaceOn(TrackFrame frame, float distance, float lateral, float speed, out Vector3 position)
    {
        var physics = new ShipPhysics();
        physics.SetTrack(frame, BarrierGeometry.Offset(40f, BarrierGeometry.DefaultGap));
        FramePoint f = frame.At(distance);
        position = f.PointAt(lateral, 0f) + Vector3.UnitY * Hover;
        physics.Place(position, YawAlong(f), speed, Settings.Boost);
        return physics;
    }

    [Fact]
    public void Default_settings_use_the_shared_values()
    {
        Assert.Equal(BoostTuning.DefaultSpeedGainKmh / 3.6f, Settings.Boost.SpeedGain);
        Assert.True(Settings.Drift.Enabled);
        Assert.Equal(BarrierCollisionSettings.Default, Settings.Collision);
        Assert.Equal(Track.ShipHover, Settings.Vertical.Hover);
    }

    [Fact]
    public void Without_a_track_a_step_is_the_dynamics_and_a_straight_move()
    {
        var physics = new ShipPhysics();
        Vector3 position = new(1f, 5f, 2f);
        physics.Place(position, 0f, 0f, Settings.Boost);

        var dynamics = new ShipDynamics();
        dynamics.Reset(0f);
        Vector3 expected = position;
        for (int i = 0; i < 300; i++)
        {
            dynamics.Step(Throttle, Vector.Technical, Settings.Drift, Dt);
            expected += new Vector3(dynamics.Velocity.X, 0f, dynamics.Velocity.Y) * Dt;
            physics.Move(ref position, Throttle, false, true, Vector.Technical, Vector.Visual.CollisionHalfWidth, Settings, Dt);
        }

        Assert.False(physics.HasSurface);
        Assert.Equal(dynamics.Speed, physics.Dynamics.Speed);
        Assert.Equal(expected, position);
    }

    [Fact]
    public void Without_control_there_is_no_throttle_and_no_recharge()
    {
        var physics = new ShipPhysics();
        Vector3 position = Vector3.Zero;
        physics.Place(position, 0f, 0f, Settings.Boost);

        Run(physics, ref position, Throttle, 60, canDrive: false);

        Assert.Equal(0f, physics.Dynamics.Speed);
        Assert.Equal(0f, physics.Boost.RechargeProgress);
    }

    [Fact]
    public void Boost_pressed_spends_a_charge()
    {
        BoostTuning boost = Settings.Boost;
        boost.InitialCharges = 2;
        var physics = new ShipPhysics();
        Vector3 position = Vector3.Zero;
        physics.Place(position, 0f, 0f, boost);

        physics.Move(ref position, Throttle, true, true, Vector.Technical, Vector.Visual.CollisionHalfWidth, Settings, Dt);

        Assert.Equal(1, physics.Boost.Charges);
        Assert.True(physics.Boost.IsImpulseActive);
    }

    [Fact]
    public void Respawn_stops_the_ship_and_keeps_the_boost_while_place_resets_it()
    {
        BoostTuning boost = Settings.Boost;
        boost.InitialCharges = 2;
        var physics = new ShipPhysics();
        Vector3 position = Vector3.Zero;
        physics.Place(position, 0f, 50f, boost);
        physics.Move(ref position, Throttle, true, true, Vector.Technical, Vector.Visual.CollisionHalfWidth, Settings, Dt);

        physics.Respawn(position, 1f);
        Assert.Equal(0f, physics.Dynamics.Speed);
        Assert.Equal(1f, physics.Dynamics.Yaw);
        Assert.Equal(1, physics.Boost.Charges);

        physics.Place(position, 0f, 0f, boost);
        Assert.Equal(2, physics.Boost.Charges);
    }

    [Fact]
    public void On_the_ground_the_ship_flies_at_hover_height()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();
        ShipPhysics physics = PlaceOn(frame, 800f, 0f, 30f, out Vector3 position);

        Run(physics, ref position, Throttle, 60);

        Assert.True(physics.Vertical.Grounded);
        Assert.Equal(physics.Surface.SurfaceY + Hover, position.Y, 2f);
        Assert.False(physics.LastBarrierTouch.Contact);
    }

    [Fact]
    public void Touching_the_barrier_leaves_the_ship_at_the_limit()
    {
        TrackFrame frame = TrackFile.Load("test_oval").Frame();
        float halfWidth = Vector.Visual.CollisionHalfWidth;
        float limit = BarrierGeometry.Offset(40f, BarrierGeometry.DefaultGap) - halfWidth;
        ShipPhysics physics = PlaceOn(frame, 100f, limit - 0.1f, 50f, out Vector3 position);

        var input = new ShipInput { Accelerate = true, SteerRight = true };
        bool touched = false;
        for (int i = 0; i < 120 && !touched; i++)
        {
            physics.Move(ref position, input, false, true, Vector.Technical, halfWidth, Settings, Dt);
            touched = physics.LastBarrierTouch.Contact;
        }

        Assert.True(touched);
        Assert.Equal(BarrierSide.Right, physics.LastBarrierTouch.Side);
        Assert.InRange(physics.Surface.Lateral, limit - 0.01f, limit + 0.01f);
    }

    [Fact]
    public void A_fast_crest_takes_off_and_landing_loses_speed()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();
        ShipPhysics physics = PlaceOn(frame, 900f, 0f, 110f, out Vector3 position);

        bool flew = false;
        bool landed = false;
        float beforeLanding = 0f;
        float afterLanding = 0f;
        for (int i = 0; i < 300 && !landed; i++)
        {
            beforeLanding = physics.Dynamics.RealSpeed;
            physics.Move(ref position, default, false, true, Vector.Technical, Vector.Visual.CollisionHalfWidth, Settings, Dt);
            flew |= physics.Vertical.Airborne;
            landed = physics.Vertical.Landed;
            afterLanding = physics.Dynamics.RealSpeed;
        }

        Assert.True(flew);
        Assert.True(landed);
        Assert.True(afterLanding < beforeLanding);
    }

    [Fact]
    public void The_same_run_gives_exactly_the_same_result()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();

        Vector3 Drive()
        {
            ShipPhysics physics = PlaceOn(frame, 700f, 3f, 80f, out Vector3 position);
            var input = new ShipInput { Accelerate = true, SteerLeft = true };
            Run(physics, ref position, input, 600);
            return position;
        }

        Assert.Equal(Drive(), Drive());
    }

    [Fact]
    public void Flying_start_is_centred_before_the_finish_at_hover_height()
    {
        TrackFrame frame = TrackFile.Load("circuit_01").Frame();

        TrackPlacement.FlyingStart(frame, 150f, Hover, out Vector3 position, out float yaw);

        SurfacePoint surface = frame.SurfaceUnder(position.X, position.Z, position.Y);
        Assert.Equal(frame.LapLength - 150f, surface.Distance, 0.5f);
        Assert.Equal(0f, surface.Lateral, 0.01f);
        Assert.Equal(Hover, surface.Height, 0.01f);
        Assert.Equal(YawAlong(surface.Frame), yaw, 0.01f);
    }

    [Fact]
    public void Grid_start_is_behind_the_mark_on_its_side()
    {
        TrackFrame frame = TrackFile.Load("circuit_01").Frame();
        const float nose = 3f;

        TrackPlacement.GridStart(frame, 1, nose, 40f, GridSettings.Default, Hover, out Vector3 first, out _);
        TrackPlacement.GridStart(frame, 2, nose, 40f, GridSettings.Default, Hover, out Vector3 second, out _);

        SurfacePoint a = frame.SurfaceUnder(first.X, first.Z, first.Y);
        SurfacePoint b = frame.SurfaceUnder(second.X, second.Z, second.Y);
        Assert.Equal(-10f, a.Lateral, 0.01f);
        Assert.Equal(10f, b.Lateral, 0.01f);
        Assert.Equal(frame.LapLength - GridSettings.DefaultFirstDistance - nose - 0.5f, a.Distance, 0.5f);
        Assert.Equal(a.Distance - GridSettings.DefaultEvenOffset, b.Distance, 0.5f);
    }

    [Fact]
    public void Respawn_point_is_centred_on_the_axis_below_the_ship()
    {
        TrackFrame frame = TrackFile.Load("test_hills").Frame();
        FramePoint f = frame.At(1000f);
        Vector3 ship = f.PointAt(12f, 5f);

        TrackPlacement.Respawn(frame, ship, Hover, out Vector3 position, out float yaw);

        SurfacePoint surface = frame.SurfaceUnder(position.X, position.Z, position.Y);
        Assert.Equal(1000f, surface.Distance, 1f);
        Assert.Equal(0f, surface.Lateral, 0.01f);
        Assert.Equal(Hover, surface.Height, 0.01f);
        Assert.Equal(YawAlong(surface.Frame), yaw, 0.01f);
    }

    [Fact]
    public void Advance_times_the_race_and_counts_checkpoints_and_finish()
    {
        var tracker = new RaceTracker(2, 2);
        var detector = new LineCrossingDetector(30);
        int[] checkpoints = { 10, 20 };
        tracker.Start();
        detector.Update(0);

        for (int i = 1; i <= 25; i++)
        {
            detector.Update(i);
            tracker.Advance(detector, checkpoints, 0, 0.5);
        }
        Assert.Equal(2, tracker.NextCheckpointIndex);
        Assert.Equal(12.5, tracker.CurrentLapTime, 6);

        for (int i = 26; i <= 30; i++)
        {
            detector.Update(i % 30);
            tracker.Advance(detector, checkpoints, 0, 0.5);
        }
        Assert.Equal(2, tracker.CurrentLap);
        Assert.Equal(0, tracker.NextCheckpointIndex);
    }
}
