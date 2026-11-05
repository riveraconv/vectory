using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cabinas contra la captura de designs/cabina-vector.js, designs/cabinas-hoja-martillo-def.js y designs/lib.js (spec docs/naves-3b-cabinas.md, 3.4)
public class CockpitCaptureTests
{
    private const float PointTolerance = 0.06f;
    private const float ValueTolerance = 0.001f;

    private static CockpitLayout Build(string name) => name switch
    {
        "hoja" => HojaCockpit.Build(),
        "martillo" => MartilloCockpit.Build(),
        _ => VectorCockpit.Build()
    };

    [Theory]
    [InlineData("vector")]
    [InlineData("hoja")]
    [InlineData("martillo")]
    public void Cockpit_MatchesTheCapture_ElementByElement(string name)
    {
        CockpitLayout layout = Build(name);
        JsonElement expected = CockpitReference.Root.GetProperty(name).GetProperty("elements");

        Assert.Equal(expected.GetArrayLength(), layout.Elements.Count);
        int i = 0;
        foreach (JsonElement e in expected.EnumerateArray())
            AssertElement(i, e, layout.Elements[i++]);
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("hoja")]
    [InlineData("martillo")]
    public void Gradients_MatchTheDesign(string name)
    {
        foreach (JsonProperty property in CockpitReference.Root.GetProperty(name).GetProperty("gradients").EnumerateObject())
        {
            CockpitGradient gradient = CockpitKit.Gradients[CockpitReference.GradientOf(property.Name)];
            JsonElement g = property.Value;
            if (g.GetProperty("kind").GetString() == "linear")
            {
                Assert.False(gradient.Radial, property.Name);
                AssertVector(g.GetProperty("from"), gradient.From, ValueTolerance, property.Name);
                AssertVector(g.GetProperty("to"), gradient.To, ValueTolerance, property.Name);
            }
            else
            {
                Assert.True(gradient.Radial, property.Name);
                AssertVector(g.GetProperty("center"), gradient.From, ValueTolerance, property.Name);
                Assert.Equal(g.GetProperty("radius").GetSingle(), gradient.To.X - gradient.From.X, 3);
            }

            JsonElement stops = g.GetProperty("stops");
            Assert.Equal(stops.GetArrayLength(), gradient.Stops.Count);
            int i = 0;
            foreach (JsonElement stop in stops.EnumerateArray())
            {
                CockpitGradientStop actual = gradient.Stops[i++];
                Assert.Equal(stop.GetProperty("offset").GetSingle(), actual.Offset, 3);
                Assert.Equal(stop.GetProperty("opacity").GetSingle(), actual.Opacity, 3);
                Assert.Equal(CockpitReference.Hex(stop.GetProperty("color").GetString()!), CockpitReference.Hex(actual.Color));
            }
        }
    }

    private static void AssertElement(int index, JsonElement e, CockpitElement actual)
    {
        string where = $"#{index} {e}";
        string[] live = e.GetProperty("live").EnumerateArray().Select(v => v.GetString()!).ToArray();

        Assert.True(string.Equals(e.GetProperty("type").GetString(), KindName(actual.Kind), StringComparison.Ordinal), where);
        Assert.True(live.SequenceEqual(ExpectedLive(actual)), where);

        JsonElement matrix = e.GetProperty("matrix");
        float[] m = { actual.Transform.M11, actual.Transform.M12, actual.Transform.M21, actual.Transform.M22, actual.Transform.M31, actual.Transform.M32 };
        for (int i = 0; i < 6; i++)
            AssertClose(matrix[i].GetSingle(), m[i], ValueTolerance, where);

        AssertPaint(e, actual, where);
        AssertNumber(e, "strokeWidth", actual.StrokeWidth, where);
        AssertNumber(e, "opacity", actual.Opacity, where);
        AssertNumber(e, "fillOpacity", actual.FillOpacity, where);
        AssertNumber(e, "strokeOpacity", actual.StrokeOpacity, where);
        Assert.True(e.GetProperty("dash").EnumerateArray().Select(v => v.GetSingle()).SequenceEqual(actual.Dash), where);

        switch (actual.Kind)
        {
            case CockpitElementKind.Polygon:
            case CockpitElementKind.Polyline:
                AssertPoints(e.GetProperty("points"), actual.Points, where);
                break;
            case CockpitElementKind.Arc:
                AssertArc(e, actual, live, where);
                break;
            case CockpitElementKind.Curve:
                AssertPoints(e.GetProperty("quad"), actual.Points, where);
                break;
            case CockpitElementKind.Segments when live.Length > 0:
                break;
            case CockpitElementKind.Segments:
                JsonElement segments = e.GetProperty("segments");
                Assert.True(segments.GetArrayLength() * 2 == actual.Points.Count, where);
                for (int i = 0; i < segments.GetArrayLength(); i++)
                {
                    JsonElement s = segments[i];
                    AssertClose(s[0].GetSingle(), actual.Points[i * 2].X, PointTolerance, where);
                    AssertClose(s[1].GetSingle(), actual.Points[i * 2].Y, PointTolerance, where);
                    AssertClose(s[2].GetSingle(), actual.Points[i * 2 + 1].X, PointTolerance, where);
                    AssertClose(s[3].GetSingle(), actual.Points[i * 2 + 1].Y, PointTolerance, where);
                }
                break;
            case CockpitElementKind.Circle:
                AssertVector(e.GetProperty("center"), actual.Center, PointTolerance, where);
                AssertClose(e.GetProperty("radius").GetSingle(), actual.Radii.X, PointTolerance, where);
                break;
            case CockpitElementKind.Ellipse:
                AssertVector(e.GetProperty("center"), actual.Center, PointTolerance, where);
                AssertVector(e.GetProperty("radii"), actual.Radii, PointTolerance, where);
                break;
            case CockpitElementKind.Rect:
                AssertVector(e.GetProperty("position"), actual.Position, PointTolerance, where);
                JsonElement size = e.GetProperty("size");
                if (size[0].ValueKind == JsonValueKind.Number)
                    AssertClose(size[0].GetSingle(), actual.Size.X, PointTolerance, where);
                AssertClose(size[1].GetSingle(), actual.Size.Y, PointTolerance, where);
                AssertClose(e.TryGetProperty("corner", out JsonElement corner) ? corner.GetSingle() : 0f, actual.CornerRadius, ValueTolerance, where);
                break;
            case CockpitElementKind.Text:
                AssertVector(e.GetProperty("position"), actual.Position, PointTolerance, where);
                AssertClose(e.GetProperty("size").GetSingle(), actual.FontSize, ValueTolerance, where);
                AssertClose(e.GetProperty("spacing").GetSingle(), actual.LetterSpacing, ValueTolerance, where);
                Assert.True(string.Equals(e.GetProperty("anchor").GetString(), actual.Anchor.ToString().ToLowerInvariant(), StringComparison.Ordinal), where);
                if (e.GetProperty("text").ValueKind == JsonValueKind.String)
                    Assert.True(e.GetProperty("text").GetString() == actual.Text, where);
                break;
        }
    }

    private static void AssertPaint(JsonElement e, CockpitElement actual, string where)
    {
        JsonElement fill = e.GetProperty("fill");
        if (fill.ValueKind == JsonValueKind.String)
        {
            string value = fill.GetString()!;
            if (value == "none")
                Assert.True(actual.Fill == null, where);
            else if (value.StartsWith("url(#"))
                Assert.True(actual.Fill?.Gradient == CockpitReference.GradientOf(value[5..^1]), where);
            else
                Assert.True(actual.Fill?.Color is { } color && CockpitReference.Hex(value) == CockpitReference.Hex(color), where);
        }

        JsonElement stroke = e.GetProperty("stroke");
        if (stroke.ValueKind == JsonValueKind.String)
        {
            string value = stroke.GetString()!;
            if (value == "none")
                Assert.True(actual.Stroke == null, where);
            else
                Assert.True(actual.Stroke is { } color && CockpitReference.Hex(value) == CockpitReference.Hex(color), where);
        }
    }

    // Los huecos ({{...}}) del diseño que corresponden a cada parte viva
    private static IEnumerable<string> ExpectedLive(CockpitElement e) => e.Slot switch
    {
        CockpitSlot.ScreenGlow => new[] { "scrGlowOp" },
        CockpitSlot.BoostOrb => new[] { $"o{e.SlotIndex}" },
        CockpitSlot.DamageSegment => new[] { $"d{e.SlotIndex}" },
        CockpitSlot.DamageText => new[] { "dmgCol", "dmgTxt" },
        CockpitSlot.BoostText => new[] { "boostTxt" },
        CockpitSlot.SpeedNumber => new[] { "spd", "spdColor" },
        CockpitSlot.SpeedBar => new[] { $"bar{e.Size.X}", "spdColor" },
        CockpitSlot.BoostTint => new[] { "boostTint" },
        CockpitSlot.DamageTint => new[] { "dmgTint", "dmgTintCls" },
        CockpitSlot.GaugeBack => new[] { "arcBg" },
        CockpitSlot.GaugeTop => new[] { "arcRed" },
        CockpitSlot.GaugeSpeed => new[] { "arcFg", "spdColor" },
        CockpitSlot.GaugeTicks => new[] { "arcTicks" },
        CockpitSlot.GaugeNeedle => new[] { "needle" },
        _ => Array.Empty<string>()   // la marca de la barra es fija en el diseño (396 / 496, los de Vector)
    };

    private static string KindName(CockpitElementKind kind) => kind is CockpitElementKind.Segments or CockpitElementKind.Arc or CockpitElementKind.Curve
        ? "path"
        : kind.ToString().ToLowerInvariant();

    // El diseño da el arco por sus extremos: lo vivo (los arcos del dial) no tiene geometría en la captura
    private static void AssertArc(JsonElement e, CockpitElement actual, string[] live, string where)
    {
        if (live.Length > 0)
            return;

        JsonElement arc = e.GetProperty("arc");
        Vector2 Point(float degrees) => actual.Center + new Vector2(MathF.Cos(degrees * MathF.PI / 180f), MathF.Sin(degrees * MathF.PI / 180f)) * actual.Radii.X;

        AssertVector(arc.GetProperty("from"), Point(actual.StartAngle), PointTolerance, where);
        AssertVector(arc.GetProperty("to"), Point(actual.EndAngle), PointTolerance, where);
        AssertClose(arc.GetProperty("radius").GetSingle(), actual.Radii.X, PointTolerance, where);
        Assert.True(arc.GetProperty("sweep").GetInt32() == 1 && actual.EndAngle > actual.StartAngle, where);
        Assert.True(arc.GetProperty("large").GetInt32() == 0 && actual.EndAngle - actual.StartAngle <= 180f, where);
    }

    private static void AssertNumber(JsonElement e, string name, float actual, string where)
    {
        JsonElement value = e.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Number)
            AssertClose(value.GetSingle(), actual, ValueTolerance, where);
    }

    private static void AssertPoints(JsonElement expected, IReadOnlyList<Vector2> actual, string where)
    {
        Assert.True(expected.GetArrayLength() == actual.Count, where);
        for (int i = 0; i < actual.Count; i++)
            AssertVector(expected[i], actual[i], PointTolerance, where);
    }

    private static void AssertVector(JsonElement expected, Vector2 actual, float tolerance, string where)
    {
        AssertClose(expected[0].GetSingle(), actual.X, tolerance, where);
        AssertClose(expected[1].GetSingle(), actual.Y, tolerance, where);
    }

    private static void AssertClose(float expected, float actual, float tolerance, string where)
    {
        Assert.True(MathF.Abs(expected - actual) <= tolerance, $"{expected} != {actual}: {where}");
    }

    private static class CockpitReference
    {
        public static readonly JsonElement Root = Load();

        public static CockpitGradientId GradientOf(string svgId) => svgId switch
        {
            "pTop" => CockpitGradientId.Top,
            "pConsole" => CockpitGradientId.Console,
            "pFront" => CockpitGradientId.Front,
            "pPillar" => CockpitGradientId.Pillar,
            "pSide" => CockpitGradientId.Side,
            "noseL" => CockpitGradientId.NoseLeft,
            "noseR" => CockpitGradientId.NoseRight,
            "scrGlow" => CockpitGradientId.ScreenGlow,
            "tintOrange" => CockpitGradientId.TintOrange,
            "tintRed" => CockpitGradientId.TintRed,
            "shadowDown" => CockpitGradientId.ShadowDown,
            "aoL" => CockpitGradientId.OcclusionLeft,
            "aoR" => CockpitGradientId.OcclusionRight,
            "cowl" => CockpitGradientId.Cowl,
            "bezelIn" => CockpitGradientId.BezelIn,
            "deckFar" => CockpitGradientId.DeckFar,
            "footShade" => CockpitGradientId.FootShade,
            "scan" => CockpitGradientId.Scan,
            _ => throw new ArgumentOutOfRangeException(svgId)
        };

        public static string Hex(CockpitColor color) => CockpitPalette.Get(color).ToHtml(false).ToLowerInvariant();

        public static string Hex(string svg)
        {
            string hex = svg.TrimStart('#').ToLowerInvariant();
            return hex.Length == 3 ? string.Concat(hex.Select(c => $"{c}{c}")) : hex;
        }

        private static JsonElement Load()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
                dir = dir.Parent;
            if (dir == null)
                throw new DirectoryNotFoundException("No se encuentra project.godot");

            string path = Path.Combine(dir.FullName, "Vectory.Test", "ShipTests", "Reference", "cockpit_reference.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.Clone();
        }
    }
}

public class CockpitProfileTests
{
    [Theory]
    [InlineData(ShipCatalog.Ship01, "vector")]
    [InlineData(ShipCatalog.Ship02, "hoja")]
    [InlineData(ShipCatalog.Ship03, "martillo")]
    public void CockpitOfEachShip_PitchedDown(string id, string cockpit)
    {
        ShipVisualProfile visual = ShipDefinitions.Find(id)!.Visual;
        Func<CockpitLayout> expected = cockpit switch
        {
            "hoja" => HojaCockpit.Build,
            "martillo" => MartilloCockpit.Build,
            _ => VectorCockpit.Build
        };

        Assert.Equal(expected, visual.BuildCockpit);
        Assert.Equal(-8f, visual.Cockpit.PitchDegrees);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship02, 0.62f)]
    [InlineData(ShipCatalog.Ship03, 1.4f)]
    public void Eye_ScaledByHeight_SameFovAndDepth(string id, float heightFactor)
    {
        CockpitFraming vector = VectorShip.Definition.Visual.Cockpit;
        CockpitFraming framing = ShipDefinitions.Find(id)!.Visual.Cockpit;

        Assert.Equal(vector.Eye.Y * heightFactor, framing.Eye.Y, 4);
        Assert.Equal(vector.Eye.X, framing.Eye.X);
        Assert.Equal(vector.Eye.Z, framing.Eye.Z);
        Assert.Equal(vector.Fov, framing.Fov);
    }

    // El punto de fuga queda donde lo espera el diseño: y ≈ 300 de 720, con la tangente de la mitad del FOV en 360 px
    [Fact]
    public void Pitch_PutsTheVanishingPointNearY300()
    {
        CockpitFraming framing = VectorShip.Definition.Visual.Cockpit;
        float rise = 360f * MathF.Tan(-framing.PitchDegrees * MathF.PI / 180f) / MathF.Tan(framing.Fov * 0.5f * MathF.PI / 180f);

        Assert.InRange(360f - rise, 298f, 302f);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship01)]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void Flicker_PicksValidEdges_OnlyStrokesOfTheFixedPart(string id)
    {
        CockpitLayout layout = ShipDefinitions.Find(id)!.Visual.BuildCockpit();
        int[] edges = layout.Elements.Where(e => e.Edge >= 0).Select(e => e.Edge).ToArray();

        Assert.Equal(Enumerable.Range(0, layout.EdgeCount), edges);
        Assert.All(layout.Elements.Where(e => e.Edge >= 0), e =>
        {
            Assert.NotNull(e.Stroke);
            Assert.False(e.IsLive);
        });
        Assert.DoesNotContain(layout.Elements, e => e.Kind == CockpitElementKind.Text && e.Edge >= 0);

        var flicker = new EdgeFlicker(layout.EdgeCount, EdgeFlickerSettings.Default, new Random(1234));
        flicker.SetLevel(3);

        Assert.Equal((int)MathF.Round(0.35f * layout.EdgeCount), flicker.AffectedEdges.Count);
        Assert.All(flicker.AffectedEdges, edge => Assert.InRange(edge, 0, layout.EdgeCount - 1));
    }

    // Lo menudo no parpadea: cada arista que cambia obliga a redibujar su trozo de cabina
    [Theory]
    [InlineData(ShipCatalog.Ship01)]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void Flicker_NotOnScrewsRivetsOrScratches(string id)
    {
        CockpitLayout layout = ShipDefinitions.Find(id)!.Visual.BuildCockpit();

        Assert.DoesNotContain(layout.Elements, e => e.Edge >= 0 && e.Kind == CockpitElementKind.Circle && e.Radii.X <= 3f);
        Assert.DoesNotContain(layout.Elements, e => e.Edge >= 0 && e.Kind == CockpitElementKind.Curve);
        Assert.DoesNotContain(layout.Elements, e => e.Edge >= 0 && e.Kind == CockpitElementKind.Polyline
            && e.Points.Count == 2 && Vector2.Distance(e.Points[0], e.Points[1]) < 5f);
    }

    [Theory]
    [InlineData(ShipCatalog.Ship01)]
    [InlineData(ShipCatalog.Ship02)]
    [InlineData(ShipCatalog.Ship03)]
    public void RunPlan_KeepsTheOrder_SmallFixedRuns_OnlyLiveOrFixed(string id)
    {
        CockpitLayout layout = ShipDefinitions.Find(id)!.Visual.BuildCockpit();

        List<CockpitRun> runs = CockpitRunPlan.Split(layout.Elements);

        Assert.Equal(layout.Elements, runs.SelectMany(r => r.Elements));
        Assert.All(runs, r =>
        {
            Assert.All(r.Elements, e => Assert.Equal(r.IsLive, e.IsLive));
            if (!r.IsLive)
                Assert.InRange(r.Elements.Count, 1, CockpitRunPlan.MaxFixedElements);
        });
    }

    [Fact]
    public void SpriteKey_OnlySmallDetailThatNeverChanges()
    {
        var canvas = new CockpitCanvas();
        canvas.Screw(100f, 100f);
        canvas.Circle(50f, 50f, 2f, CockpitColor.Cyan, opacity: 0.8f);
        canvas.Circle(50f, 50f, 20f, CockpitColor.Cyan);
        canvas.Circle(50f, 50f, 2f, CockpitGradientId.BezelIn);
        canvas.Circle(50f, 50f, 2f, CockpitColor.Cyan, CockpitColor.Blue);
        canvas.Line(new Vector2(0f, 0f), new Vector2(300f, 0f), CockpitColor.Blue, 1f, 1f);
        canvas.BoostOrbs(0f, 0f, 3f, 1f);
        IReadOnlyList<CockpitElement> e = canvas.Finish().Elements;

        Assert.True(CockpitSpriteKey.TryFrom(e[0], out CockpitSpriteKey circle));
        Assert.False(circle.IsLine);
        Assert.Equal(3f, circle.Radius);
        Assert.Equal(CockpitColor.Blue, circle.Stroke);
        Assert.True(CockpitSpriteKey.TryFrom(e[1], out CockpitSpriteKey slot));
        Assert.True(slot.IsLine);
        Assert.Equal(4f, slot.Length);
        Assert.True(CockpitSpriteKey.TryFrom(e[2], out _));
        Assert.False(CockpitSpriteKey.TryFrom(e[3], out _));   // grande
        Assert.False(CockpitSpriteKey.TryFrom(e[4], out _));   // con degradado
        Assert.False(CockpitSpriteKey.TryFrom(e[5], out _));   // arista que parpadea
        Assert.False(CockpitSpriteKey.TryFrom(e[6], out _));   // línea larga
        Assert.DoesNotContain(e.Where(x => x.IsLive), x => CockpitSpriteKey.TryFrom(x, out _));
    }

    [Fact]
    public void ViewRules_LayerInCockpit_ScreenHudOutside()
    {
        Assert.Equal(new CockpitViewRules(true, false), CockpitViewRules.For(CameraView.Cockpit));
        Assert.Equal(new CockpitViewRules(false, true), CockpitViewRules.For(CameraView.Chase));
    }
}

public class CockpitHudStateTests
{
    private const float Settled = 10f;   // más que cualquier transición

    private static readonly Vector4 Cyan = CockpitPalette.Rgba(CockpitColor.Cyan);
    private static readonly Vector4 Orange = CockpitPalette.Rgba(CockpitColor.Orange);
    private static readonly Vector4 Blue = CockpitPalette.Rgba(CockpitColor.Blue);
    private static readonly Vector4 Red = CockpitPalette.Rgba(CockpitColor.Red);
    private static readonly Vector4 Green = CockpitPalette.Rgba(CockpitColor.Green);
    private static readonly Vector4 Yellow = CockpitPalette.Rgba(CockpitColor.Yellow);

    private static CockpitHudInput Input(float speed = 300f, bool boost = false, int charges = 0, bool cooldown = false, int damage = 0, bool damageEnabled = true) => new()
    {
        SpeedKmh = speed,
        TopKmh = 396f,
        MaxKmh = 496f,
        BoostActive = boost,
        Charges = charges,
        Cooldown = cooldown,
        DamageLevel = damage,
        DamageEnabled = damageEnabled
    };

    private static CockpitHudState State(CockpitHudInput input, CockpitLightSettings? light = null)
    {
        var state = new CockpitHudState(CockpitPalette.Rgba);
        state.Update(input, light ?? CockpitLightSettings.Default, Settled);
        return state;
    }

    private static Vector4 Alpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, color.W * alpha);

    [Theory]
    [InlineData(300f, false, false)]
    [InlineData(396f, false, false)]
    [InlineData(420f, false, true)]
    [InlineData(300f, true, true)]
    public void SpeedColor_CyanBelowTop_OrangeAboveAndWithBoost(float speed, bool boost, bool orange)
    {
        Assert.Equal(orange ? Orange : Cyan, State(Input(speed, boost)).SpeedColor);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(248f, 0.5f)]
    [InlineData(600f, 1f)]
    public void SpeedFraction_OverTopPlusBoost_Clamped(float speed, float fraction)
    {
        Assert.Equal(fraction, State(Input(speed)).SpeedFraction, 4);
    }

    [Fact]
    public void Orbs_FourForms_BlockedBeforeCooldown()
    {
        Assert.Equal(new CockpitHudPaint(Alpha(Red, 0.3f), Alpha(Red, 0.55f), 0f), State(Input(charges: 3, cooldown: true, damage: 3)).Orb(0));
        Assert.Equal(new CockpitHudPaint(Alpha(Orange, 0.3f), Alpha(Orange, 0.55f), 0f), State(Input(charges: 3, cooldown: true, damage: 2)).Orb(0));
        Assert.Equal(new CockpitHudPaint(Orange, Orange, 1f), State(Input(charges: 3)).Orb(2));
        Assert.Equal(new CockpitHudPaint(Alpha(Blue, 0.25f), Alpha(Blue, 0.7f), 0f), State(Input(charges: 1)).Orb(1));
    }

    [Theory]
    [InlineData(2, false, false, 3, "LOCKED")]
    [InlineData(2, true, false, 0, "ACTIVE")]
    [InlineData(2, false, true, 0, "COOLDOWN")]
    [InlineData(1, false, false, 0, "READY")]
    [InlineData(0, false, false, 0, "CHARGING")]
    [InlineData(0, false, true, 3, "CHARGING")]
    public void BoostText(int charges, bool boost, bool cooldown, int damage, string text)
    {
        Assert.Equal(text, State(Input(charges: charges, boost: boost, cooldown: cooldown, damage: damage)).BoostText);
    }

    [Theory]
    [InlineData(0, "NOMINAL", 0)]
    [InlineData(1, "LIGHT", 1)]
    [InlineData(2, "HEAVY", 2)]
    [InlineData(3, "CRITICAL", 3)]
    public void Damage_SegmentsOnByLevel_AndText(int level, string text, int on)
    {
        CockpitHudState state = State(Input(damage: level));
        Vector4 color = level == 0 ? Green : level == 1 ? Yellow : Red;

        Assert.Equal(text, state.DamageText);
        Assert.Equal(color, state.DamageTextColor);
        Assert.Equal(level == 3, state.SegmentsBlinking);
        for (int i = 0; i < 3; i++)
        {
            bool lit = i < on;
            Assert.Equal(lit ? 1f : 0f, state.Segment(i).Fill.W, 3);
            if (!lit)
                Assert.Equal(Alpha(color, 0.6f), state.Segment(i).Stroke);
        }
    }

    [Fact]
    public void Blink_OnlyAtLevelThree_AtTwoHertz()
    {
        var state = new CockpitHudState(CockpitPalette.Rgba);
        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0f);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.1f);
        Assert.Equal(1f, state.Segment(0).Fill.W, 3);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.2f);
        Assert.Equal(CockpitHudState.BlinkDimOpacity, state.Segment(0).Fill.W, 3);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.25f);
        Assert.Equal(1f, state.Segment(0).Fill.W, 3);
    }

    [Fact]
    public void TimeTrial_DamageOff_NoTextNoRedTint_OrbsNotBlocked()
    {
        CockpitHudState state = State(Input(charges: 3, damage: 3, damageEnabled: false));

        Assert.Equal("", state.DamageText);
        Assert.Equal(0f, state.DamageTint);
        Assert.False(state.SegmentsBlinking);
        Assert.Equal(0f, state.Segment(0).Fill.W);
        Assert.Equal(Alpha(Blue, CockpitHudState.DisabledDamageBorderOpacity), state.Segment(0).Stroke);
        Assert.Equal("READY", state.BoostText);
        Assert.Equal(Orange, state.Orb(0).Fill);
    }

    [Fact]
    public void Fade_ChangesColorInATransitionTime()
    {
        var state = new CockpitHudState(CockpitPalette.Rgba);
        state.Update(Input(300f), CockpitLightSettings.Default, 0.016f);
        Assert.Equal(Cyan, state.SpeedColor);

        state.Update(Input(450f), CockpitLightSettings.Default, CockpitHudState.HudTransitionTime / 2f);
        Assert.Equal(Vector4.Lerp(Cyan, Orange, 0.5f).X, state.SpeedColor.X, 3);

        state.Update(Input(450f), CockpitLightSettings.Default, CockpitHudState.HudTransitionTime);
        Assert.Equal(Orange, state.SpeedColor);
    }

    [Theory]
    [InlineData(0f, 0.35f)]
    [InlineData(248f, 0.525f)]
    [InlineData(496f, 0.7f)]
    public void ScreenGlow_GrowsWithSpeed(float speed, float glow)
    {
        Assert.Equal(glow, State(Input(speed)).ScreenGlow, 3);
    }

    [Fact]
    public void Light_TableValues_TimesIntensity_AndOffAtZero()
    {
        Assert.Equal(0f, State(Input(300f)).BoostTint, 3);
        Assert.Equal(0.25f, State(Input(420f)).BoostTint, 3);
        Assert.Equal(0.55f, State(Input(300f, true)).BoostTint, 3);
        Assert.Equal(0f, State(Input(damage: 1)).DamageTint, 3);
        Assert.Equal(0.15f, State(Input(damage: 2)).DamageTint, 3);
        Assert.InRange(State(Input(damage: 3)).DamageTint, 0.2f, 0.45f);

        var half = new CockpitLightSettings { BoostTint = 0.5f, DamageTint = 0.5f, ScreenGlow = 0.5f };
        Assert.Equal(0.275f, State(Input(300f, true), half).BoostTint, 3);
        Assert.Equal(0.075f, State(Input(damage: 2), half).DamageTint, 3);
        Assert.Equal(0.175f, State(Input(0f), half).ScreenGlow, 3);

        CockpitHudState off = State(Input(300f, true, damage: 3), new CockpitLightSettings());
        Assert.Equal(0f, off.BoostTint);
        Assert.Equal(0f, off.DamageTint);
        Assert.Equal(0f, off.ScreenGlow);
    }

    [Fact]
    public void DamagePulse_BetweenHighAndLow_EverySecond()
    {
        var state = new CockpitHudState(CockpitPalette.Rgba);
        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0f);
        Assert.Equal(0.45f, state.DamageTint, 3);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.5f);
        Assert.Equal(0.2f, state.DamageTint, 3);

        state.Update(Input(damage: 3), CockpitLightSettings.Default, 0.5f);
        Assert.Equal(0.45f, state.DamageTint, 3);
    }

    [Fact]
    public void SpeedBar_WidthTimesFraction_MarkOnTheTopOfTheShip()
    {
        CockpitElement bar = VectorCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.SpeedBar);
        CockpitElement mark = VectorCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.SpeedBarMark);
        CockpitHudInput other = Input(264f);
        other.TopKmh = 428f;
        other.MaxKmh = 528f;
        CockpitHudState state = State(other);

        Assert.Equal(88f, CockpitHudPieces.Resolve(bar, state).Size.X, 3);
        Assert.Equal(926f + 176f * 428f / 528f, CockpitHudPieces.Resolve(mark, state).Points[0].X, 3);
    }

    // En el lienzo no se rellena por un fallo del archivo: en el juego, con la misma regla que la de Vector
    [Fact]
    public void SpeedBar_OfHoja_WidthTimesFraction()
    {
        CockpitElement bar = HojaCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.SpeedBar);

        Assert.Equal(130f, CockpitHudPieces.Resolve(bar, State(Input(248f))).Size.X, 3);
    }

    [Theory]
    [InlineData(0f, 150f)]
    [InlineData(372f, 339.1525f)]
    [InlineData(472f, 390f)]
    public void GaugeNeedle_PointsToTheAngleOfTheSpeed(float speed, float degrees)
    {
        CockpitHudInput input = Input(speed);
        input.TopKmh = 372f;
        input.MaxKmh = 472f;
        CockpitHudState state = State(input);
        CockpitElement needle = MartilloCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.GaugeNeedle);

        IReadOnlyList<Vector2> points = CockpitHudPieces.Resolve(needle, state).Points;

        float a = degrees * MathF.PI / 180f;
        var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
        Assert.Equal(640f + dir.X * 34f, points[0].X, 2);
        Assert.Equal(672f + dir.Y * 34f, points[0].Y, 2);
        Assert.Equal(640f + dir.X * 54f, points[1].X, 2);
        Assert.Equal(672f + dir.Y * 54f, points[1].Y, 2);
    }

    [Fact]
    public void GaugeArcs_FollowTheScaleOfTheShip()
    {
        CockpitHudInput input = Input(236f);
        input.TopKmh = 372f;
        input.MaxKmh = 472f;
        CockpitHudState state = State(input);
        CockpitLayout layout = MartilloCockpit.Build();

        CockpitElement top = CockpitHudPieces.Resolve(layout.Elements.Single(e => e.Slot == CockpitSlot.GaugeTop), state);
        CockpitElement speed = CockpitHudPieces.Resolve(layout.Elements.Single(e => e.Slot == CockpitSlot.GaugeSpeed), state);
        CockpitElement back = CockpitHudPieces.Resolve(layout.Elements.Single(e => e.Slot == CockpitSlot.GaugeBack), state);

        Assert.Equal(150f + 240f * 372f / 472f, top.StartAngle, 2);
        Assert.Equal(390f, top.EndAngle);
        Assert.Equal(150f, speed.StartAngle);
        Assert.Equal(150f + 120f, speed.EndAngle, 2);
        Assert.Equal(state.SpeedColor, speed.StrokeRgba);
        Assert.Equal(150f, back.StartAngle);
        Assert.Equal(390f, back.EndAngle);
    }

    [Fact]
    public void GaugeNumbers_PlacedByTheScaleOfTheShip()
    {
        CockpitElement number = MartilloCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.GaugeNumber && e.Text == "400");
        CockpitHudInput input = Input();
        input.TopKmh = 372f;
        input.MaxKmh = 472f;

        Vector2 position = CockpitHudPieces.Resolve(number, State(input)).Position;

        float a = (150f + 240f * 400f / 472f) * MathF.PI / 180f;
        Assert.Equal(640f + MathF.Cos(a) * 78f, position.X, 2);
        Assert.Equal(672f + MathF.Sin(a) * 78f + 3f, position.Y, 2);
    }

    // Tan fina que no se ve y Godot no la podría triangular: al frenar hasta parar salía un error en cada frame
    [Theory]
    [InlineData(0.01f, 0f)]
    [InlineData(0.0001f, 0f)]
    public void SpeedBar_AlmostEmpty_IsNotDrawn(float speed, float width)
    {
        CockpitElement bar = VectorCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.SpeedBar);

        Assert.Equal(width, CockpitHudPieces.Resolve(bar, State(Input(speed))).Size.X);
    }

    [Fact]
    public void SameDrawing_OnlyDifferentWhenSomethingChanges()
    {
        CockpitElement needle = MartilloCockpit.Build().Elements.Single(e => e.Slot == CockpitSlot.GaugeNeedle);

        CockpitElement a = CockpitHudPieces.Resolve(needle, State(Input(100f)));
        CockpitElement same = CockpitHudPieces.Resolve(needle, State(Input(100f)));
        CockpitElement other = CockpitHudPieces.Resolve(needle, State(Input(200f)));

        Assert.True(CockpitHudPieces.SameDrawing(a, same));
        Assert.False(CockpitHudPieces.SameDrawing(a, other));
        Assert.False(CockpitHudPieces.SameDrawing(a, a with { Opacity = 0.5f }));
    }

    [Fact]
    public void Resolve_FixedElementsDoNotChange()
    {
        CockpitElement element = VectorCockpit.Build().Elements.First(e => !e.IsLive);

        Assert.Same(element, CockpitHudPieces.Resolve(element, State(Input())));
    }
}

public class CockpitGeometryTests
{
    [Fact]
    public void ScanLines_OneEveryThreePixels_InsideThePolygon()
    {
        Vector2[] square = { new(10f, 10f), new(20f, 10f), new(20f, 22f), new(10f, 22f) };

        List<Vector2> lines = CockpitGeometry.ScanLines(square);

        Assert.Equal(8, lines.Count);
        Assert.Equal(new Vector2(10f, 12.5f), lines[0]);
        Assert.Equal(new Vector2(20f, 12.5f), lines[1]);
        Assert.Equal(15.5f, lines[2].Y);
    }

    [Fact]
    public void DashPairs_OnAndOffAlongTheLine()
    {
        List<Vector2> pairs = CockpitGeometry.DashPairs(new[] { Vector2.Zero, new Vector2(20f, 0f) }, 6f, 5f);

        Assert.Equal(new[] { Vector2.Zero, new Vector2(6f, 0f), new Vector2(11f, 0f), new Vector2(17f, 0f) }, pairs);
    }
}
