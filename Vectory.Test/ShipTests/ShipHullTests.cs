using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cascos de Hoja y Martillo (spec docs/naves-3-cascos.md). El casco de Vector ya se compara con su captura en
// VectorDefinitionTests.
public class ShipHullTests
{
    private static IReadOnlyList<ShipLine> Hull(string id) => ShipDefinitions.Find(id)!.Visual.BuildHull();

    private static IEnumerable<Vector3> Points(IReadOnlyList<ShipLine> hull) => hull.SelectMany(l => new[] { l.A, l.B });

    [Theory]
    [InlineData(ShipCatalog.Ship02, "hoja")]
    [InlineData(ShipCatalog.Ship03, "martillo")]
    public void Hull_MatchesTheCaptureOfTheDesign_SegmentBySegment(string id, string captureName)
    {
        const float Tolerance = 1e-5f;
        JsonElement capture = HullReference.Root.GetProperty(captureName);
        IReadOnlyList<ShipLine> hull = Hull(id);

        Assert.Equal(capture.GetArrayLength(), hull.Count);
        int i = 0;
        foreach (JsonElement expected in capture.EnumerateArray())
        {
            ShipLine line = hull[i];
            AssertClose(HullReference.ToVector(expected.GetProperty("a")), line.A, Tolerance);
            AssertClose(HullReference.ToVector(expected.GetProperty("b")), line.B, Tolerance);
            Assert.Equal(expected.GetProperty("color").GetString(), line.Color == ShipLineColor.Blue ? "245bff" : "36e7ff");
            Assert.Equal(i, line.Edge);
            i++;
        }
    }

    [Theory]
    [InlineData(ShipCatalog.Ship01, 7.2f, 3.4f, 1.0f, 118)]
    [InlineData(ShipCatalog.Ship02, 8.8f, 1.6f, 0.62f, 138)]
    [InlineData(ShipCatalog.Ship03, 6.6f, 2.5f, 1.4f, 126)]
    public void Measures_AreTheOnesOfTheTable(string id, float length, float halfWidth, float height, int segments)
    {
        IReadOnlyList<ShipLine> hull = Hull(id);
        Vector3[] points = Points(hull).ToArray();

        Assert.Equal(segments, hull.Count);
        Assert.Equal(length, points.Max(p => p.Z) - points.Min(p => p.Z), 3);
        Assert.Equal(halfWidth, points.Max(p => MathF.Abs(p.X)), 3);
        Assert.Equal(height, points.Max(p => p.Y) - points.Min(p => p.Y), 3);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void TwoNozzles_TwelveBlueSegmentsEach(string id)
    {
        Assert.Equal(2 * 12, Hull(id).Count(l => l.Color == ShipLineColor.Blue));
    }

    [Fact]
    public void Builder_NozzleHasTwelveBlueSegments_AndEdgesInOrder()
    {
        var builder = new ShipHullBuilder();

        builder.Nozzle(0f, 0f, 0f, 1f, 0.5f);

        Assert.Equal(36, builder.Lines.Count);
        Assert.Equal(12, builder.Lines.Count(l => l.Color == ShipLineColor.Blue));
        Assert.Equal(Enumerable.Range(0, 36), builder.Lines.Select(l => l.Edge));
    }

    [Theory]
    [InlineData(ShipCatalog.Ship02, 5.2f, 3.2f, 0.62f, 8.8f, 0.17f, -0.8f)]
    [InlineData(ShipCatalog.Ship03, 3.6f, 5.0f, 1.4f, 6.6f, 0.2f, -0.3f)]
    public void NoseAndPhysicsBox_AreTheOnesOfTheTable_AndWrapTheHull(
        string id, float nose, float width, float height, float length, float centerY, float centerZ)
    {
        ShipVisualProfile visual = ShipDefinitions.Find(id)!.Visual;
        Vector3[] points = Points(visual.BuildHull()).ToArray();

        Assert.Equal(nose, visual.NoseDistance);
        Assert.Equal(new Vector3(width, height, length), visual.PhysicsBoxSize);
        Assert.Equal(new Vector3(0f, centerY, centerZ), visual.PhysicsBoxCenter);

        Assert.Equal(-visual.NoseDistance, points.Min(p => p.Z), 3);
        Assert.Equal(visual.PhysicsBoxSize.X / 2f, points.Max(p => MathF.Abs(p.X)), 3);
        Assert.Equal(visual.PhysicsBoxCenter.Y + visual.PhysicsBoxSize.Y / 2f, points.Max(p => p.Y), 3);
        Assert.Equal(visual.PhysicsBoxCenter.Y - visual.PhysicsBoxSize.Y / 2f, points.Min(p => p.Y), 3);
        Assert.Equal(visual.PhysicsBoxCenter.Z + visual.PhysicsBoxSize.Z / 2f, points.Max(p => p.Z), 3);
        Assert.Equal(visual.PhysicsBoxCenter.Z - visual.PhysicsBoxSize.Z / 2f, points.Min(p => p.Z), 3);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship01)]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void CollisionHalfWidth_IsTheHalfWidthOfTheHull(string id)
    {
        ShipVisualProfile visual = ShipDefinitions.Find(id)!.Visual;

        float hull = Points(visual.BuildHull()).Max(p => MathF.Abs(p.X));

        Assert.InRange(MathF.Abs(hull - visual.CollisionHalfWidth), 0f, 0.01f);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship01, 1f)]
    [InlineData(ShipCatalog.Ship02, 1.22f)]
    [InlineData(ShipCatalog.Ship03, 0.92f)]
    public void ChaseFraming_IsTheOneOfVector_TimesTheFactor(string id, float factor)
    {
        ShipVisualProfile visual = ShipDefinitions.Find(id)!.Visual;
        ChaseFraming vector = VectorShip.Definition.Visual.Chase;

        Assert.Equal(factor, visual.ChaseFactor);
        Assert.Equal(vector.Height * factor, visual.Chase.Height);
        Assert.Equal(vector.Distance * factor, visual.Chase.Distance);
        Assert.Equal(vector.ExtraDistance * factor, visual.Chase.ExtraDistance);
        Assert.Equal(vector.LookTarget * factor, visual.Chase.LookTarget);
    }

    [Fact]
    public void ChaseFraming_Scaled_KeepsTheAngle()
    {
        ChaseFraming vector = VectorShip.Definition.Visual.Chase;
        ChaseFraming scaled = vector.Scaled(1.22f);

        Assert.Equal(vector.Height / vector.Distance, scaled.Height / scaled.Distance, 5);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void Flicker_PicksValidEdgesOfTheHull(string id)
    {
        IReadOnlyList<ShipLine> hull = Hull(id);
        int edgeCount = hull.Max(l => l.Edge) + 1;
        Assert.Equal(hull.Count, edgeCount);
        var flicker = new EdgeFlicker(edgeCount, EdgeFlickerSettings.Default, new Random(1234));

        flicker.SetLevel(3);

        Assert.Equal((int)MathF.Round(0.35f * edgeCount), flicker.AffectedEdges.Count);
        Assert.All(flicker.AffectedEdges, edge => Assert.InRange(edge, 0, edgeCount - 1));
        Assert.Equal(flicker.AffectedEdges.Count, flicker.AffectedEdges.Distinct().Count());
    }

    // El fantasma dibuja el casco de la definición de su archivo
    [Theory]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void Definition_BringsItsOwnHull_NotTheOneOfVector(string id)
    {
        ShipVisualProfile visual = ShipDefinitions.Resolve(id).Visual;
        ShipVisualProfile vector = VectorShip.Definition.Visual;

        Assert.NotEqual(vector.BuildHull, visual.BuildHull);
        Assert.NotEqual(vector.BuildHull().Count, visual.BuildHull().Count);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, tolerance);
    }

    // Captura de designs/naves-geometria.js, volcada con Node antes de traducirlo
    private static class HullReference
    {
        public static readonly JsonElement Root = Load();

        public static Vector3 ToVector(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

        private static JsonElement Load()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
                dir = dir.Parent;
            if (dir == null)
                throw new DirectoryNotFoundException("No se encuentra project.godot");

            string path = Path.Combine(dir.FullName, "Vectory.Test", "ShipTests", "Reference", "hull_reference.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.Clone();
        }
    }
}
