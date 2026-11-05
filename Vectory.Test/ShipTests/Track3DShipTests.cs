using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Pista 3D en la nave: capa vertical, aterrizaje, vuelo, pendiente, peralte y orientación
// (spec docs/pista-3d.md v1.1, sección 10)
public class Track3DShipTests
{
    private const float Kmh = 1f / 3.6f;
    private const float Dt = 1f / 60f;
    private const float Deg = MathF.PI / 180f;
    private const float Hover = 2f;

    // Las pruebas 8 y 9 de la spec usan 6 m/s²
    private static readonly VerticalSettings Vertical = new() { Gravity = 6f, Hover = Hover };

    private static readonly LandingSettings Landing = new()
    {
        MediumSpeed = 3f,
        StrongSpeed = 8f,
        CompressionDepth = 0.3f,
        CompressionTime = 0.2f
    };

    private static readonly SlopeSettings Slope = new() { ReferenceAngle = 10f * Deg, Effect = 0.3f };

    private static readonly BankGripSettings Bank = new()
    {
        GripBonus = 0.4f,
        TurnBonus = 0.15f,
        ReferenceBank = 25f * Deg,
        MaxBank = 35f * Deg
    };

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
        MaxSlipAngle = 60f * Deg,
        GripRecoveryTime = 3f
    };

    // Parábola h(x) = x² / (2·r) a lo largo de X (r negativo = cresta de radio |r|; positivo = hondonada)
    private static SurfaceSample Parabola(float r, float x)
    {
        Vector3 normal = Vector3.Normalize(new Vector3(-x / r, 1f, 0f));
        return new SurfaceSample(x * x / (2f * r), normal, Vector2.UnitX, 1f / r);
    }

    // Recorre el perfil a velocidad horizontal constante y dice si despegó en algún momento
    private static bool CrossProfile(float r, float fromX, float toX, float speed, Action<ShipVertical, SurfaceSample>? check = null)
    {
        var vertical = new ShipVertical();
        vertical.Reset(Parabola(r, fromX).Height + Hover);
        var velocity = new Vector2(speed, 0f);
        bool tookOff = false;
        for (float x = fromX; x < toX; x += speed * Dt)
        {
            SurfaceSample surface = Parabola(r, x);
            vertical.Update(Dt, surface, velocity, Vertical);
            tookOff |= vertical.TookOff;
            check?.Invoke(vertical, surface);
        }
        return tookOff;
    }

    // Nave ya en el aire a "height", con velocidad vertical vy (despega en una cresta muy cerrada)
    private static ShipVertical Airborne(float height, Vector2 velocity, float vy = 0f)
    {
        var vertical = new ShipVertical();
        vertical.Reset(height);
        var sharpCrest = new SurfaceSample(height - Hover, Vector3.Normalize(new Vector3(-vy / velocity.X, 1f, 0f)), Vector2.UnitX, -1f);
        vertical.Update(Dt, sharpCrest, velocity, Vertical);
        Assert.True(vertical.Airborne);
        return vertical;
    }

    // ---- 7. En llano ----

    [Fact]
    public void Flat_AlwaysGroundedAtHoverHeight()
    {
        var vertical = new ShipVertical();
        vertical.Reset(0f);

        for (int i = 0; i < 600; i++)
        {
            vertical.Update(Dt, SurfaceSample.Flat(-2f), new Vector2(110f, 3f), Vertical);
            Assert.True(vertical.Grounded);
            Assert.Equal(0f, vertical.Height);
            Assert.Equal(0f, vertical.VerticalSpeed);
            Assert.False(vertical.TookOff);
            Assert.False(vertical.Landed);
        }
    }

    // ---- 8. Cresta de radio 600 ----

    [Fact]
    public void Crest600_NoTakeoffAt200Kmh()
    {
        Assert.False(CrossProfile(-600f, -60f, 60f, 200f * Kmh));
    }

    [Fact]
    public void Crest600_TakeoffAt230Kmh()
    {
        Assert.True(CrossProfile(-600f, -60f, 60f, 230f * Kmh));
    }

    [Fact]
    public void TakeoffRule_VSquaredOverRadius()
    {
        // 6 m/s² y radio 600: despega por encima de 60 m/s (~216 km/h)
        Assert.False(ShipVertical.TakesOff(-1f / 600f, 59.5f, 6f));
        Assert.True(ShipVertical.TakesOff(-1f / 600f, 60.5f, 6f));
        Assert.False(ShipVertical.TakesOff(1f / 100f, 200f, 6f));   // hondonada
    }

    [Fact]
    public void Takeoff_KeepsVerticalSpeedOfRamp()
    {
        // Rampa subiendo al 10 %: sale hacia arriba con la velocidad vertical de la rampa
        var vertical = new ShipVertical();
        vertical.Reset(Hover);
        var ramp = new SurfaceSample(0f, Vector3.Normalize(new Vector3(-0.1f, 1f, 0f)), Vector2.UnitX, -1f);

        vertical.Update(Dt, ramp, new Vector2(50f, 0f), Vertical);

        Assert.True(vertical.TookOff);
        Assert.Equal(5f, vertical.VerticalSpeed, 3);
    }

    // ---- 9. En el aire ----

    [Fact]
    public void Air_ParabolaWithGravityAndHorizontalSpeedKept()
    {
        var velocity = new Vector2(80f, -4f);
        ShipVertical vertical = Airborne(50f, velocity);
        float h0 = vertical.Height;
        var lowSurface = SurfaceSample.Flat(-100f);

        float t = 0f;
        for (int i = 0; i < 120; i++)
        {
            Vector2 returned = vertical.Update(Dt, lowSurface, velocity, Vertical);
            t += Dt;
            Assert.Equal(velocity, returned);
        }

        Assert.Equal(h0 - 0.5f * 6f * t * t, vertical.Height, 3);
        Assert.Equal(-6f * t, vertical.VerticalSpeed, 3);
        Assert.True(vertical.Airborne);
    }

    // ---- 10. Hondonada ----

    [Fact]
    public void Dip_NeverTakesOffNorGoesBelowSurface()
    {
        bool tookOff = CrossProfile(100f, -40f, 40f, 140f, (vertical, surface) =>
        {
            Assert.True(vertical.Grounded);
            Assert.True(vertical.Height >= surface.Height + Hover - 1e-4f);
        });

        Assert.False(tookOff);
    }

    [Fact]
    public void Air_NeverBelowSurface()
    {
        ShipVertical vertical = Airborne(10f, new Vector2(60f, 0f));

        // La superficie sube por encima de la nave: aterriza a la altura de vuelo
        vertical.Update(Dt, SurfaceSample.Flat(12f), new Vector2(60f, 0f), Vertical);

        Assert.True(vertical.Grounded);
        Assert.Equal(12f + Hover, vertical.Height, 4);
    }

    // ---- 11. Aterrizaje ----

    [Theory]
    [InlineData(2f, LandingLevel.None)]
    [InlineData(5f, LandingLevel.Medium)]
    [InlineData(10f, LandingLevel.Strong)]
    public void Landing_LevelBySpeed(float fallSpeed, LandingLevel expected)
    {
        Assert.Equal(expected, LandingImpact.Level(fallSpeed, Landing));
    }

    [Fact]
    public void Landing_IntensityZeroBelowMediumAndOneFromStrong()
    {
        Assert.Equal(0f, LandingImpact.Intensity(2f, Landing));
        Assert.InRange(LandingImpact.Intensity(5f, Landing), 0.1f, 0.9f);
        Assert.Equal(1f, LandingImpact.Intensity(12f, Landing));
    }

    [Fact]
    public void Landing_RemovesSpeedAgainstSurfaceAndKeepsParallel()
    {
        var velocity = new Vector2(60f, 0f);
        ShipVertical vertical = Airborne(Hover + 0.05f, velocity);
        // Cae sobre una subida del 10 % (la normal se inclina hacia atrás): parte de la velocidad va contra ella
        Vector3 normal = Vector3.Normalize(new Vector3(-0.1f, 1f, 0f));
        var slope = new SurfaceSample(0f, normal, Vector2.UnitX, 0f);

        Vector2 after = vertical.Update(0.5f, slope, velocity, Vertical);

        Assert.True(vertical.Landed);
        var before = new Vector3(velocity.X, -6f * 0.5f, 0f);   // vy al tocar: 0,5 s cayendo desde 0
        var landed = new Vector3(after.X, vertical.VerticalSpeed, after.Y);
        Assert.Equal(0f, Vector3.Dot(landed, normal), 3);
        Vector3 removed = before - landed;
        Assert.Equal(0f, Vector3.Cross(removed, normal).Length(), 2);
        Assert.Equal(-Vector3.Dot(before, normal), vertical.LandingSpeed, 2);
    }

    [Fact]
    public void Compression_GoesDownAndBack()
    {
        var compression = new LandingCompression();
        compression.Trigger(1f);

        compression.Update(0.1f);
        Assert.Equal(0.3f, compression.Offset(Landing), 3);   // en la mitad, lo más abajo

        compression.Update(0.1f);
        Assert.Equal(0f, compression.Offset(Landing), 3);
    }

    // ---- 12. Dinámica en el aire ----

    [Fact]
    public void Airborne_AccelerateAndBrakeDoNothing()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 50f);
        Vector2 velocity = dynamics.Velocity;

        dynamics.StepAirborne(new ShipInput { Accelerate = true }, CreateTuning(), Dt);
        dynamics.StepAirborne(new ShipInput { Brake = true }, CreateTuning(), Dt);

        Assert.Equal(50f, dynamics.Speed);
        Assert.Equal(velocity, dynamics.Velocity);
    }

    [Fact]
    public void Airborne_KeepsSlidingVelocity()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 50f);
        dynamics.SetVelocity(new Vector2(8f, -50f));   // deslizando hacia la derecha

        for (int i = 0; i < 30; i++)
            dynamics.StepAirborne(default, CreateTuning(), Dt);

        Assert.Equal(new Vector2(8f, -50f), dynamics.Velocity);
    }

    [Fact]
    public void Airborne_Turn30Percent()
    {
        var ground = new ShipDynamics();
        var air = new ShipDynamics();
        ground.Reset(0f, 50f);
        air.Reset(0f, 50f);
        var input = new ShipInput { SteerLeft = true };

        ground.Step(input, CreateTuning(), Dt);
        air.StepAirborne(input, CreateTuning(), Dt, turnFactor: 0.3f);

        Assert.Equal(ground.Yaw * 0.3f, air.Yaw, 6);
    }

    [Fact]
    public void Airborne_CannotEnterDrift()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);

        for (int i = 0; i < 30; i++)
            dynamics.StepAirborne(new ShipInput { Drift = true, SteerRight = true }, CreateTuning(), Dt);

        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void Airborne_DriftStateKeptUntilLanding()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 150f * Kmh);
        dynamics.Step(new ShipInput { Drift = true, SteerRight = true }, CreateTuning(), CreateDrift(), Dt);
        Assert.True(dynamics.Drifting);

        dynamics.StepAirborne(default, CreateTuning(), Dt);
        Assert.True(dynamics.Drifting);

        // Al aterrizar sin mantener el derrape, sale con las reglas de siempre
        dynamics.Step(default, CreateTuning(), CreateDrift(), Dt);
        Assert.False(dynamics.Drifting);
    }

    [Fact]
    public void Airborne_BoostAddsToLongitudinalSpeed()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 50f);

        dynamics.StepAirborne(default, CreateTuning(), Dt, boostGain: 2f);

        Assert.Equal(52f, dynamics.Speed, 4);
        Assert.Equal(52f, Vector2.Dot(dynamics.Velocity, dynamics.Forward), 4);
    }

    // ---- 13. Pendiente ----

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(10f, 0.7f)]
    [InlineData(-10f, 1.3f)]
    [InlineData(5f, 0.85f)]
    [InlineData(-5f, 1.15f)]
    [InlineData(20f, 0.7f)]
    [InlineData(-20f, 1.3f)]
    public void Slope_Factor(float degrees, float expected)
    {
        Assert.Equal(expected, SlopeEffect.Factor(degrees * Deg, Slope), 4);
    }

    // ---- 14. Exceso sobre la punta al empezar una subida ----

    [Fact]
    public void Uphill_ExcessLostAt20KmhPerSecond()
    {
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 110f);
        ShipTuning uphill = CreateTuning();
        float factor = SlopeEffect.Factor(10f * Deg, Slope);
        uphill.MaxSpeed *= factor;
        uphill.Acceleration *= factor;

        for (int i = 0; i < 60; i++)
            dynamics.Step(new ShipInput { Accelerate = true }, uphill, Dt);

        Assert.Equal(110f - 20f * Kmh, dynamics.Speed, 2);
    }

    // ---- 15. Peralte ----

    [Fact]
    public void Bank25InFavour_GripAndTurnUp()
    {
        // Girando a la izquierda, a favor = el borde derecho más alto (peralte positivo)
        Assert.Equal(1.4f, BankGrip.GripFactor(25f * Deg, 1f, true, Bank), 4);
        Assert.Equal(1.15f, BankGrip.TurnFactor(25f * Deg, 1f, true, Bank), 4);
        // Girando a la derecha, a favor = el izquierdo más alto (peralte negativo)
        Assert.Equal(1.4f, BankGrip.GripFactor(-25f * Deg, -0.5f, true, Bank), 4);
    }

    [Fact]
    public void Bank25Against_GripAndTurnDown()
    {
        Assert.Equal(0.6f, BankGrip.GripFactor(25f * Deg, -1f, true, Bank), 4);
        Assert.Equal(0.85f, BankGrip.TurnFactor(25f * Deg, -1f, true, Bank), 4);
    }

    [Fact]
    public void Bank_NoEffectWithoutTurningOrInTheAir()
    {
        Assert.Equal(1f, BankGrip.GripFactor(25f * Deg, 0f, true, Bank));
        Assert.Equal(1f, BankGrip.TurnFactor(25f * Deg, 0f, true, Bank));
        Assert.Equal(1f, BankGrip.GripFactor(25f * Deg, 1f, false, Bank));
        Assert.Equal(1f, BankGrip.TurnFactor(25f * Deg, 1f, false, Bank));
    }

    [Fact]
    public void Bank_CappedAt35Degrees()
    {
        Assert.Equal(BankGrip.GripFactor(35f * Deg, 1f, true, Bank), BankGrip.GripFactor(45f * Deg, 1f, true, Bank), 5);
        Assert.Equal(1f + 0.4f * 35f / 25f, BankGrip.GripFactor(45f * Deg, 1f, true, Bank), 4);
    }

    [Fact]
    public void Bank_FlatGivesExactlyOne()
    {
        Assert.Equal(1f, BankGrip.GripFactor(0f, 1f, true, Bank));
        Assert.Equal(1f, SlopeEffect.Factor(0f, Slope));
    }

    // ---- 16. Combinación con el daño de nivel 3 ----

    [Fact]
    public void Bank_CombinesWithDamageTurnFactor()
    {
        var dynamics = new ShipDynamics();
        var reference = new ShipDynamics();
        dynamics.Reset(0f, 50f);
        reference.Reset(0f, 50f);
        ShipTuning tuning = CreateTuning();
        tuning.TurnRate *= 0.85f * BankGrip.TurnFactor(25f * Deg, 1f, true, Bank);

        var input = new ShipInput { SteerLeft = true };
        dynamics.Step(input, tuning, Dt);
        reference.Step(input, CreateTuning(), Dt);

        Assert.Equal(reference.Yaw * 0.85f * 1.15f, dynamics.Yaw, 5);
    }

    // ---- Orientación ----

    [Fact]
    public void Attitude_FlatIsLevel()
    {
        ShipAttitude.SurfaceAngles(Vector3.UnitY, 1.2f, out float pitch, out float roll);

        Assert.Equal(0f, pitch, 6);
        Assert.Equal(0f, roll, 6);
    }

    [Fact]
    public void Attitude_FollowsSlopeAndBank()
    {
        // Rumbo 0 = mirando a -Z. Subida de 8° hacia delante: la normal se inclina hacia +Z
        float a = 8f * Deg;
        ShipAttitude.SurfaceAngles(new Vector3(0f, MathF.Cos(a), MathF.Sin(a)), 0f, out float pitch, out float roll);
        Assert.Equal(a, pitch, 4);
        Assert.Equal(0f, roll, 4);

        // Borde derecho (+X) 25° más alto: la normal se inclina hacia -X; lado derecho arriba = ladeo positivo
        float b = 25f * Deg;
        ShipAttitude.SurfaceAngles(new Vector3(-MathF.Sin(b), MathF.Cos(b), 0f), 0f, out pitch, out roll);
        Assert.Equal(0f, pitch, 4);
        Assert.Equal(b, roll, 4);
    }

    [Fact]
    public void Attitude_AirPitchFollowsMotionWithCap()
    {
        Assert.Equal(MathF.Atan2(5f, 60f), ShipAttitude.AirPitch(5f, 60f, 20f * Deg), 5);
        Assert.Equal(20f * Deg, ShipAttitude.AirPitch(40f, 60f, 20f * Deg), 5);
        Assert.Equal(-20f * Deg, ShipAttitude.AirPitch(-40f, 60f, 20f * Deg), 5);
    }

    [Fact]
    public void Attitude_InTheAirRollGoesToZero()
    {
        var attitude = new ShipAttitude();
        attitude.Reset(0f, 25f * Deg);
        var settings = new AttitudeSettings { GroundSmoothTime = 0.1f, AirSmoothTime = 0.3f, LandingSmoothTime = 0.15f, AirMaxPitch = 20f * Deg };

        for (int i = 0; i < 180; i++)
            attitude.Update(false, false, Vector3.UnitY, 0f, 0f, 60f, settings, Dt);

        Assert.Equal(0f, attitude.Roll, 3);
    }

    // ---- 17 y 18. Recolocación y atasco ----

    [Fact]
    public void Respawn_FromTheAirLeavesShipGroundedAndStopped()
    {
        ShipVertical vertical = Airborne(30f, new Vector2(80f, 0f), 5f);
        var dynamics = new ShipDynamics();
        dynamics.Reset(0f, 80f);

        vertical.Reset(2f);
        dynamics.Reset(0.5f, 0f);

        Assert.True(vertical.Grounded);
        Assert.Equal(2f, vertical.Height);
        Assert.Equal(0f, vertical.VerticalSpeed);
        Assert.Equal(0f, dynamics.RealSpeed);
        vertical.Update(Dt, SurfaceSample.Flat(0f), dynamics.Velocity, Vertical);
        Assert.True(vertical.Grounded);
    }

    [Fact]
    public void StuckDetector_DoesNotCountInTheAir()
    {
        var detector = new RespawnDetector(new RespawnSettings
        {
            BarrierOffset = 20.5f,
            StuckSpeed = 10f * Kmh,
            StuckTime = 3f,
            OutsideMargin = 2f
        });

        for (int i = 0; i < 400; i++)
            Assert.False(detector.Update(Dt, true, 1f, 17f, airborne: true));
        Assert.Equal(0f, detector.StuckTimer);
    }
}
