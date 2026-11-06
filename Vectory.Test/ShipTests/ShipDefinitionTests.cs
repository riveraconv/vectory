using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Vectory.Menu;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

public class ShipDefinitionsTests
{
    [Fact]
    public void AllThreeShips_HaveDefinition()
    {
        Assert.True(ShipDefinitions.Has(ShipCatalog.Ship01));
        Assert.True(ShipDefinitions.Has(ShipCatalog.Ship02));
        Assert.True(ShipDefinitions.Has(ShipCatalog.Ship03));
        Assert.False(ShipDefinitions.Has("nope"));
        Assert.False(ShipDefinitions.Has(null));
    }

    [Fact]
    public void EverySelectableEntry_HasDefinition()
    {
        foreach (ShipEntry entry in ShipCatalog.Entries.Where(e => e.Selectable))
            Assert.NotNull(ShipDefinitions.Find(entry.Id));
    }

    [Fact]
    public void Resolve_Ship01_IsVector_WithoutWarning()
    {
        int warnings = 0;

        Assert.Same(VectorShip.Definition, ShipDefinitions.Resolve("ship_01", _ => warnings++));
        Assert.Equal(0, warnings);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData(null)]
    public void Resolve_Unknown_IsVector(string? id)
    {
        Assert.Same(VectorShip.Definition, ShipDefinitions.Resolve(id));
    }

    [Fact]
    public void Resolve_WarnsOncePerId()
    {
        const string Id = "only_used_by_the_warning_test";
        var warnings = new List<string>();

        ShipDefinitions.Resolve(Id, warnings.Add);
        ShipDefinitions.Resolve(Id, warnings.Add);

        string warning = Assert.Single(warnings);
        Assert.Contains(Id, warning);
    }

    [Fact]
    public void Find_DoesNotFallBack()
    {
        Assert.Null(ShipDefinitions.Find("nope"));
        Assert.Same(VectorShip.Definition, ShipDefinitions.Find("ship_01"));
    }
}

public class VectorDefinitionTests
{
    private static ShipVisualProfile Visual => VectorShip.Definition.Visual;

    [Fact]
    public void Technical_IsTheBaseValues()
    {
        ShipTuning t = VectorShip.Definition.Technical;

        Assert.Equal(VectorTuning.Values, t);
        Assert.Equal(110f, t.MaxSpeed);
        Assert.Equal(5f, t.Drag);
        Assert.Equal(1.3f, t.TurnRate);
        Assert.Equal(8f, t.SteerResponse);
        Assert.Equal(25f, t.Acceleration);
        Assert.Equal(45f, t.BrakeDeceleration);
        Assert.Equal(5f, t.LateralGrip);
        Assert.Equal(20f / 3.6f, t.OverspeedDecay);
    }

    [Fact]
    public void Stats_AreTheSameAsBefore()
    {
        ShipTuning vector = VectorShip.Definition.Technical;
        IReadOnlyList<ShipStat> stats = ShipStats.Compute(vector, ShipDefinitions.Default.Technical);

        Assert.Equal("396 KM/H", stats.Single(s => s.Label == ShipStats.TopSpeed).Value);
        Assert.Equal("0–200 · 2.4 S", stats.Single(s => s.Label == ShipStats.Acceleration).Value);
        Assert.Equal("74 °/S", stats.Single(s => s.Label == ShipStats.Handling).Value);
        Assert.Equal("200–0 · 1.2 S", stats.Single(s => s.Label == ShipStats.Braking).Value);
        Assert.All(stats, s => Assert.Equal(5, s.Segments));
    }

    [Fact]
    public void Hull_MatchesTheReference()
    {
        AssertLines(Reference.Root.GetProperty("hull"), Visual.BuildHull());
    }

    [Fact]
    public void Cameras_MatchTheReference()
    {
        // La cámara del mundo en cabina ya no sale de la captura: era la de la cabina 2D de Vector
        AssertClose(Reference.Vector("cockpitEye"), CockpitFraming.For2DCockpit.Eye);
        Assert.Equal(Reference.Number("cockpitFov"), CockpitFraming.For2DCockpit.Fov, 5);
        Assert.Equal(Reference.Number("chaseHeight"), Visual.Chase.Height, 5);
        Assert.Equal(Reference.Number("chaseDistance"), Visual.Chase.Distance, 5);
        Assert.Equal(Reference.Number("chaseExtraDistance"), Visual.Chase.ExtraDistance, 5);
        // No se puede leer de un nodo: era un valor fijo de ChaseCamera
        AssertClose(new Vector3(0f, 0.3f, -10f), Visual.Chase.LookTarget);
    }

    [Fact]
    public void CollisionHalfWidth_IsTheWingTip()
    {
        Assert.Equal(3.4f, Visual.CollisionHalfWidth);
        Assert.Equal(Reference.Number("collisionHalfWidth"), Visual.CollisionHalfWidth, 5);
        Assert.Equal(Visual.CollisionHalfWidth, Visual.BuildHull().Max(l => MathF.Max(MathF.Abs(l.A.X), MathF.Abs(l.B.X))), 5);
    }

    [Fact]
    public void NoseAndPhysicsBox_AreTheSameAsBefore()
    {
        Assert.Equal(4f, Visual.NoseDistance);
        AssertClose(Reference.Vector("boxSize"), Visual.PhysicsBoxSize);
        AssertClose(Reference.Vector("boxCenter"), Visual.PhysicsBoxCenter);
    }

    private static void AssertLines(JsonElement expected, IReadOnlyList<ShipLine> lines)
    {
        Assert.Equal(expected.GetArrayLength(), lines.Count);
        int i = 0;
        foreach (JsonElement line in expected.EnumerateArray())
        {
            ShipLine actual = lines[i++];
            AssertClose(Reference.ToVector(line.GetProperty("a")), actual.A);
            AssertClose(Reference.ToVector(line.GetProperty("b")), actual.B);
            Assert.Equal(line.GetProperty("edge").GetInt32(), actual.Edge);
            Assert.Equal(line.GetProperty("color").GetString(), actual.Color == ShipLineColor.Blue ? "245bff" : "36e7ff");
        }
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        const float Tolerance = 1e-5f;
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, Tolerance);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, Tolerance);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, Tolerance);
    }

    // Geometría y medidas de Vector volcadas de los nodos del juego antes de sacarlas a la definición
    private static class Reference
    {
        public static readonly JsonElement Root = Load();

        public static float Number(string name) => Root.GetProperty(name).GetSingle();

        public static Vector3 Vector(string name) => ToVector(Root.GetProperty(name));

        public static Vector3 ToVector(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

        private static JsonElement Load()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
                dir = dir.Parent;
            if (dir == null)
                throw new DirectoryNotFoundException("No se encuentra project.godot");

            string path = Path.Combine(dir.FullName, "Vectory.Test", "ShipTests", "Reference", "vector_reference.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.Clone();
        }
    }
}
