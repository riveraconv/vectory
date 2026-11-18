using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Daño visible del casco: rozaduras por zonas, hollín y grietas (spec docs/efectos-5-danos-casco.md)
public class HullWearTests
{
    private static readonly HullWearSettings S = HullWearSettings.Default;
    private static readonly DamageSmokeFoci Foci = ShipDefinitions.Find(ShipCatalog.Ship01).Visual.SmokeFoci;

    [Fact]
    public void Barrier_scrapes_the_side_it_touched()
    {
        var wear = new HullWear(S);
        wear.Barrier(BarrierSide.Right, ImpactLevel.Medium, true, 1f / 60f);
        Assert.Equal(0.25f, wear[WearZone.Right], 4);
        Assert.Equal(0f, wear[WearZone.Left]);
        wear.Barrier(BarrierSide.Left, ImpactLevel.Strong, true, 1f / 60f);
        Assert.Equal(0.5f, wear[WearZone.Left], 4);
        Assert.Equal(new Vector4(0.5f, 0.25f, 0f, 0f), wear.Zones);
    }

    [Fact]
    public void A_graze_scrapes_while_it_lasts_and_a_held_hit_only_once()
    {
        var wear = new HullWear(S);
        for (int i = 0; i < 60; i++)
            wear.Barrier(BarrierSide.Left, ImpactLevel.Graze, false, 1f / 60f);
        Assert.Equal(0.15f, wear[WearZone.Left], 3);
        // Un medio que sigue en contacto (sin ser nuevo) no suma otra vez
        wear.Barrier(BarrierSide.Right, ImpactLevel.Medium, true, 1f / 60f);
        wear.Barrier(BarrierSide.Right, ImpactLevel.Medium, false, 1f / 60f);
        Assert.Equal(0.25f, wear[WearZone.Right], 4);
    }

    [Fact]
    public void Wear_stops_at_one_and_only_changes_its_version_when_it_changes()
    {
        var wear = new HullWear(S);
        for (int i = 0; i < 5; i++)
            wear.Barrier(BarrierSide.Left, ImpactLevel.Strong, true, 0f);
        Assert.Equal(1f, wear[WearZone.Left]);
        int version = wear.Version;
        wear.Barrier(BarrierSide.Left, ImpactLevel.Strong, true, 0f);
        wear.Barrier(BarrierSide.Right, ImpactLevel.Graze, false, 0f);
        Assert.Equal(version, wear.Version);
        wear.Reset();
        Assert.Equal(Vector4.Zero, wear.Zones);
    }

    [Fact]
    public void Zone_of_a_contact_point()
    {
        // VECTOR: 4 m hasta el morro y 3,31 hasta la popa
        Assert.Equal(WearZone.Front, HullWear.ZoneOf(0.5f, -3f, 4f, 3.31f, S));
        Assert.Equal(WearZone.Rear, HullWear.ZoneOf(-0.5f, 2.5f, 4f, 3.31f, S));
        Assert.Equal(WearZone.Left, HullWear.ZoneOf(-2f, 0.5f, 4f, 3.31f, S));
        Assert.Equal(WearZone.Right, HullWear.ZoneOf(2f, -1f, 4f, 3.31f, S));
    }

    [Fact]
    public void Contact_point_in_the_ship_axes_with_its_heading()
    {
        // Rumbo 0 (adelante −Z): un punto en +X es la derecha; uno en −Z lejos, el morro
        var wear = new HullWear(S);
        wear.ContactAt(new Vector2(2.5f, 0f), Vector2.Zero, 0f, 4f, 3.31f, ImpactLevel.Medium, true, 0f);
        Assert.Equal(0.25f, wear[WearZone.Right], 4);
        wear.ContactAt(new Vector2(0f, -3.5f), Vector2.Zero, 0f, 4f, 3.31f, ImpactLevel.Medium, true, 0f);
        Assert.Equal(0.25f, wear[WearZone.Front], 4);
        // Girada 90° a la izquierda (adelante −X): un punto en −Z queda a su derecha
        var turned = new HullWear(S);
        turned.ContactAt(new Vector2(10f, -2.5f), new Vector2(10f, 0f), MathF.PI / 2f, 4f, 3.31f, ImpactLevel.Strong, true, 0f);
        Assert.Equal(0.5f, turned[WearZone.Right], 4);
    }

    [Fact]
    public void Soot_by_level_on_the_skin_under_each_focus()
    {
        Assert.Equal(0f, HullWear.Soot(1, 0, Foci, S).Radius);
        SootSpot two = HullWear.Soot(2, 0, Foci, S);
        Assert.Equal(0.9f, two.Radius, 4);
        Assert.Equal(0.8f, two.Strength, 4);
        Assert.Equal(Foci.Smoke - Vector3.UnitY * Foci.SkinGaps.X, two.Center);
        Assert.Equal(0f, HullWear.Soot(2, 1, Foci, S).Radius);
        Assert.Equal(1.4f, HullWear.Soot(3, 0, Foci, S).Radius, 4);
        Assert.Equal(1.4f * 0.75f, HullWear.Soot(3, 1, Foci, S).Radius, 4);
        Assert.Equal(1.4f * 0.55f, HullWear.Soot(3, 2, Foci, S).Radius, 4);
        Assert.False(HullWear.Cracks(2));
        Assert.True(HullWear.Cracks(3));
    }

    [Fact]
    public void Time_trial_has_no_soot()
    {
        Assert.Equal(0f, HullWear.Soot(HullDamage.ScorchMask(3, true), 0, Foci, S).Radius);
    }
}
