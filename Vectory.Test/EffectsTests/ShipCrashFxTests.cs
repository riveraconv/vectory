using System.Numerics;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Effects;
using Xunit;

namespace Vectory.Test.EffectsTests;

// Efectos de choque entre naves (spec docs/efectos-1-choques.md, pruebas 15–18) y punto de contacto
public class ShipCrashFxTests
{
    private const float Dt = 1f / 60f;
    private static readonly Vector3 ShipVelocity = new(0f, 0f, -80f);

    // Pista recta hacia −Z: derecha +X, arriba +Y, popa +Z
    private static CrashEvent Hit(ImpactLevel level, bool start, GrazeKind graze) =>
        new(CrashKind.Ships, level, graze, start, CrashEvent.ShipSource(0, 3), new Vector3(1f, 0.7f, 0f),
            Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, ShipVelocity, 0f);

    private static CollisionFx Create(CollisionFxSettings? settings = null) =>
        new(settings ?? CollisionFxSettings.Default, new Random(5));

    private static int Embers(SparkField field)
    {
        int n = 0;
        for (int i = 0; i < field.Count; i++)
            if (field[i].Ember)
                n++;
        return n;
    }

    // 15
    [Fact]
    public void A_side_graze_gives_100_sparks_per_second_and_a_rear_graze_none()
    {
        var fx = Create();
        for (int i = 0; i < 60; i++)
            fx.Crash(Hit(ImpactLevel.Graze, false, GrazeKind.Side), Dt);
        Assert.InRange(fx.Sparks.Count, 99, 101);

        var rear = Create();
        for (int i = 0; i < 60; i++)
            rear.Crash(Hit(ImpactLevel.Graze, false, GrazeKind.Rear), Dt);
        Assert.Equal(0, rear.Sparks.Count);
        Assert.Empty(rear.Flashes.All);
        Assert.Equal(0, rear.Smoke.Count);
    }

    // 16
    [Theory]
    [InlineData(ImpactLevel.Medium, 45, 6, 2)]
    [InlineData(ImpactLevel.Strong, 110, 24, 10)]
    public void Ship_hits_give_the_table_sparks_embers_and_puffs(ImpactLevel level, int sparks, int embers, int puffs)
    {
        var fx = Create();
        fx.Crash(Hit(level, true, GrazeKind.Side), Dt);
        Assert.Equal(sparks + embers, fx.Sparks.Count);
        Assert.Equal(embers, Embers(fx.Sparks));
        Assert.Equal(puffs, fx.Smoke.Count);
        Assert.Single(fx.Flashes.All);
        Assert.Equal(level == ImpactLevel.Strong, fx.Flashes.All[0].Ring);
    }

    // 17
    [Fact]
    public void Ship_sparks_inherit_92_percent_and_go_further_back()
    {
        var settings = CollisionFxSettings.Default;
        settings.ShipMedium.SpeedMin = settings.ShipMedium.SpeedMax = 0f;
        settings.EmberSpeedMin = settings.EmberSpeedMax = 0f;
        var fx = Create(settings);
        fx.Crash(Hit(ImpactLevel.Medium, true, GrazeKind.Side), Dt);
        for (int i = 0; i < fx.Sparks.Count; i++)
        {
            Vector3 extra = fx.Sparks[i].Velocity - ShipVelocity * 0.92f;
            Assert.Equal(0f, extra.X, 4);
            Assert.Equal(0f, extra.Y, 4);
            Assert.InRange(extra.Z, 4f - 1e-4f, 12f + 1e-4f);
        }
        // El destello entre naves se mueve con ellas
        Assert.Equal(ShipVelocity * 0.92f, fx.Flashes.All[0].Velocity);
    }

    // 18: un golpe que no cuenta por la espera de la pareja llega como roce: ni ráfaga, ni humo, ni destello grande
    [Fact]
    public void A_hit_inside_the_pair_cooldown_gives_no_burst()
    {
        var frame = new TrackFrame(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -500f), new Vector3(100f, 0f, -500f), new Vector3(100f, 0f, 0f) });
        CrashEvent? e = CrashEvent.Ships(frame, 0, 3, ImpactLevel.Graze, false, GrazeKind.Rear, GrazeKind.Rear,
            new Vector2(0f, -100f), new Vector2(0f, -80f), CollisionFxSettings.Default);
        Assert.False(e!.Value.IsStart);
        var fx = Create();
        fx.Crash(e.Value, Dt);
        Assert.Equal(0, fx.Sparks.Count);
        Assert.Equal(0, fx.Smoke.Count);
        Assert.Empty(fx.Flashes.All);

        CrashEvent? side = CrashEvent.Ships(frame, 0, 3, ImpactLevel.Graze, false, GrazeKind.Side, GrazeKind.Side,
            new Vector2(0f, -100f), new Vector2(0f, -80f), CollisionFxSettings.Default);
        var graze = Create();
        graze.Crash(side!.Value, Dt);
        Assert.InRange(graze.Sparks.Count, 0, 2);
        Assert.Equal(0, graze.Smoke.Count);
    }

    // El evento entre naves: 0,7 m sobre la superficie en el punto del solape, con la pista como ejes
    [Fact]
    public void Ship_event_sits_07_m_over_the_contact_point()
    {
        // Muestras seguidas en la recta: entre muestras de esquina el marco interpola la tangente y se tuerce un poco
        var points = new List<Vector3>();
        for (int z = 0; z >= -500; z -= 10)
            points.Add(new Vector3(0f, 0f, z));
        points.Add(new Vector3(100f, 0f, -500f));
        points.Add(new Vector3(100f, 0f, 0f));
        var frame = new TrackFrame(points);
        CrashEvent? e = CrashEvent.Ships(frame, 0, 3, ImpactLevel.Strong, true, GrazeKind.None, GrazeKind.Side,
            new Vector2(2f, -100f), new Vector2(0f, -80f), CollisionFxSettings.Default);
        Assert.True(e!.Value.IsStart);
        Assert.Equal(GrazeKind.Side, e.Value.Graze);
        Assert.Equal(2f, e.Value.Point.X, 3);
        Assert.Equal(0.7f, e.Value.Point.Y, 3);
        Assert.Equal(-100f, e.Value.Point.Z, 3);
        Assert.Equal(new Vector3(0f, 0f, -80f), e.Value.Velocity);
    }

    // Punto de contacto: puerta con puerta, entre los dos costados; por detrás, en el morro del de detrás
    [Fact]
    public void Contact_point_is_between_the_flanks_or_at_the_rear_ship_nose()
    {
        var shape = new ShipShape(2f, 3.6f, 3.5f);
        Vector2 side = ContactPoint.Between(Vector2.Zero, 0f, shape, new Vector2(3.9f, 0f), 0f, shape);
        Assert.Equal(1.95f, side.X, 3);
        Assert.InRange(side.Y, -3.6f, 3.5f);

        // B detrás (+Z): su morro en 7 − 3,6 = 3,4 y la cola de A en 3,5
        Vector2 rear = ContactPoint.Between(Vector2.Zero, 0f, shape, new Vector2(0.5f, 7f), 0f, shape);
        Assert.Equal(3.45f, rear.Y, 3);
        Assert.InRange(rear.X, -1.5f, 2f);

        // Girada, el mismo resultado al girar todo
        float yaw = 0.7f;
        var rotation = Matrix3x2.CreateRotation(-yaw);
        Vector2 b = Vector2.Transform(new Vector2(3.9f, 0f), rotation);
        Vector2 turned = ContactPoint.Between(Vector2.Zero, yaw, shape, b, yaw, shape);
        Vector2 expected = Vector2.Transform(side, rotation);
        Assert.Equal(expected.X, turned.X, 2);
        Assert.Equal(expected.Y, turned.Y, 2);
    }
}
