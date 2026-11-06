using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vectory.Autopilot;
using Vectory.Balance;
using Vectory.Circuit;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.BalanceTests;

// Piloto automático y simulador (spec docs/naves-6-equilibrio.md, 8). Todo corre sin nodos de Godot.
public class BalanceTests
{
    private const float Step = 1f / 60f;
    private static readonly ShipPhysicsSettings Physics = ShipPhysicsSettings.Default;
    private static readonly string[] Ships = { "ship_02", "ship_01", "ship_03" };

    private static ShipDefinition Ship(string id) => ShipDefinitions.Find(id);

    private static BalanceCircuit Circuit(string id)
    {
        TrackFile file = TrackFile.Load(id);
        return new BalanceCircuit(id, id, file.IsTest, file.Frame(), file.TrackWidth, file.CheckpointCount);
    }

    // Dos rectas largas y dos curvas de 180°, en sentido horario visto desde arriba
    private static TrackFrame Stadium(float straight, float radius)
    {
        var points = new List<Vector3>();
        const float spacing = 2f;
        for (float x = -straight / 2f; x < straight / 2f; x += spacing)
            points.Add(new Vector3(x, 0f, -radius));
        int arc = (int)(MathF.PI * radius / spacing);
        for (int i = 0; i < arc; i++)
        {
            float a = MathF.PI * i / arc;
            points.Add(new Vector3(straight / 2f + radius * MathF.Sin(a), 0f, -radius * MathF.Cos(a)));
        }
        for (float x = straight / 2f; x > -straight / 2f; x -= spacing)
            points.Add(new Vector3(x, 0f, radius));
        for (int i = 0; i < arc; i++)
        {
            float a = MathF.PI * i / arc;
            points.Add(new Vector3(-straight / 2f - radius * MathF.Sin(a), 0f, radius * MathF.Cos(a)));
        }
        return new TrackFrame(points);
    }

    // --- Trazada ---

    [Fact]
    public void On_a_long_straight_the_line_goes_through_the_center()
    {
        const float straight = 2000f;
        TrackFrame frame = Stadium(straight, 120f);
        RacingLine line = RacingLine.Build(frame, 40f, 3.4f, 1f);

        Assert.InRange(line.Lateral[line.IndexAt(straight / 2f)], -0.5f, 0.5f);
    }

    [Fact]
    public void In_a_curve_the_line_moves_inside_and_never_passes_the_maximum()
    {
        const float straight = 600f;
        const float radius = 120f;
        TrackFrame frame = Stadium(straight, radius);
        float max = RacingLine.MaxOffset(40f, 3.4f, 1f);
        RacingLine line = RacingLine.Build(frame, 40f, 3.4f, 1f);

        // A mitad de la primera curva: gira a la derecha, así que el interior es la derecha
        float apex = line.Lateral[line.IndexAt(straight + MathF.PI * radius / 2f)];
        Assert.True(line.Curvature[line.IndexAt(straight + MathF.PI * radius / 2f)] < 0f);
        Assert.True(apex > 5f, $"apex lateral {apex}");
        Assert.All(line.Lateral, lateral => Assert.InRange(lateral, -max, max));
    }

    [Fact]
    public void A_wider_ship_has_less_room_and_a_tighter_line()
    {
        Assert.Equal(40f / 2f - 3.4f - 1f, RacingLine.MaxOffset(40f, 3.4f, 1f));
        Assert.True(RacingLine.MaxOffset(40f, 3.4f, 1f) < RacingLine.MaxOffset(40f, 1.6f, 1f));

        TrackFrame frame = TrackFile.Load("circuit_01").Frame();
        RacingLine narrow = RacingLine.Build(frame, 40f, 1.6f, 1f);
        RacingLine wide = RacingLine.Build(frame, 40f, 3.4f, 1f);
        float wideLateral = wide.Lateral.Max(MathF.Abs);
        float narrowLateral = narrow.Lateral.Max(MathF.Abs);
        Assert.True(wideLateral < narrowLateral, $"wide {wideLateral} narrow {narrowLateral}");
        float wideSum = wide.Curvature.Sum(k => k * k);
        float narrowSum = narrow.Curvature.Sum(k => k * k);
        Assert.True(wideSum >= narrowSum, $"wide {wideSum} narrow {narrowSum}");
    }

    [Fact]
    public void The_line_lowers_the_curvature_of_going_through_the_center()
    {
        TrackFrame frame = TrackFile.Load("circuit_01").Frame();
        RacingLine center = RacingLine.Center(frame);
        RacingLine line = RacingLine.Build(frame, 40f, 1.6f, 1f);

        float lineSum = line.Curvature.Sum(k => k * k);
        float centerSum = center.Curvature.Sum(k => k * k);
        Assert.True(lineSum < centerSum, $"line {lineSum} center {centerSum}");
    }

    // --- Velocidad ---

    [Fact]
    public void Calibration_gives_more_speed_with_more_radius_and_top_speed_with_a_huge_one()
    {
        ShipTuning tuning = Ship("ship_01").Technical;
        CurveTable table = CurveCalibration.Measure(tuning, Physics, Step);

        for (int i = 1; i < table.Speeds.Count; i++)
            Assert.True(table.Speeds[i] >= table.Speeds[i - 1]);
        Assert.True(table.Speeds[0] < table.Speeds[table.Speeds.Count - 1]);
        Assert.Equal(tuning.MaxSpeed, table.SpeedFor(10000f));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void More_turn_rate_or_more_grip_do_not_lower_the_speed_for_a_radius(bool turn)
    {
        ShipTuning normal = Ship("ship_01").Technical;
        ShipTuning better = normal;
        if (turn)
            better.TurnRate *= 1.2f;
        else
            better.LateralGrip *= 1.2f;

        CurveTable a = CurveCalibration.Measure(normal, Physics, Step);
        CurveTable b = CurveCalibration.Measure(better, Physics, Step);

        const float bisectionNoise = 1f;   // m/s: lo que da de sí medir con la física real
        for (int i = 0; i < a.Speeds.Count; i++)
            Assert.True(b.Speeds[i] >= a.Speeds[i] - bisectionNoise, $"radius {a.Radii[i]}: {a.Speeds[i]} → {b.Speeds[i]}");
    }

    private static (RacingLine Line, SpeedProfile Profile, CurveTable Table, ShipTuning Tuning) OvalProfile()
    {
        TrackFile file = TrackFile.Load("test_oval");
        TrackFrame frame = file.Frame();
        ShipDefinition ship = Ship("ship_01");
        RacingLine line = RacingLine.Build(frame, file.TrackWidth, ship.Visual.CollisionHalfWidth, 1f);
        CurveTable table = CurveCalibration.Measure(ship.Technical, Physics, Step);
        return (line, SpeedProfile.Build(line, frame, ship.Technical, table, Physics, 0.97f), table, ship.Technical);
    }

    [Fact]
    public void The_profile_never_passes_the_table_or_the_top_speed()
    {
        (RacingLine line, SpeedProfile profile, CurveTable table, ShipTuning tuning) = OvalProfile();

        for (int i = 0; i < line.Count; i++)
        {
            Assert.True(profile.Target[i] <= tuning.MaxSpeed + 1e-3f);
            float radius = 1f / MathF.Max(MathF.Abs(line.Curvature[i]), 1e-4f);
            Assert.True(profile.Target[i] <= table.SpeedFor(radius) + 1e-3f);
        }
    }

    [Fact]
    public void The_profile_respects_the_real_brakes()
    {
        (RacingLine line, SpeedProfile profile, _, ShipTuning tuning) = OvalProfile();

        for (int i = 0; i < line.Count; i++)
        {
            float v = profile.Target[i];
            float next = profile.Target[line.Wrap(i + 1)];
            float maxDrop = 2f * tuning.BrakeDeceleration * line.PointSpacing;
            Assert.True(v * v - next * next <= maxDrop + 1e-2f);
        }
    }

    // --- Simulador y piloto ---

    [Fact]
    public void Two_equal_stints_give_exactly_the_same_times()
    {
        BalanceCircuit circuit = Circuit("test_hills");

        StintResult a = StintRunner.Run(circuit, Ship("ship_02"), StintOptions.Default);
        StintResult b = StintRunner.Run(circuit, Ship("ship_02"), StintOptions.Default);

        Assert.Equal(a.FlyingLap, b.FlyingLap);
        Assert.Equal(a.FirstLap, b.FirstLap);
        Assert.Equal(a.TotalTime, b.TotalTime);
        Assert.Equal(a.Sectors, b.Sectors);
        Assert.Equal(a.TopSpeed, b.TopSpeed);
        Assert.Equal(a.CurveMinSpeeds, b.CurveMinSpeeds);
    }

    [Theory]
    [InlineData("test_oval")]
    [InlineData("circuit_01")]
    public void All_ships_complete_the_stint_without_respawns(string circuitId)
    {
        BalanceCircuit circuit = Circuit(circuitId);
        foreach (string ship in Ships)
        {
            StintResult result = StintRunner.Run(circuit, Ship(ship), StintOptions.Default);
            Assert.True(result.Finished, ship);
            Assert.Equal(0, result.Respawns);
            Assert.True(result.IsValid);
            Assert.True(result.FlyingLap > 0.0);
            Assert.Equal(circuit.CheckpointIndices.Count + 1, result.Sectors.Count);
            Assert.Equal(circuit.Curves.Count, result.CurveMinSpeeds.Count);
        }
    }

    [Fact]
    public void In_circuit_01_the_ships_do_not_touch_the_barrier()
    {
        BalanceCircuit circuit = Circuit("circuit_01");
        foreach (string ship in Ships)
        {
            StintResult result = StintRunner.Run(circuit, Ship(ship), StintOptions.Default);
            Assert.Equal(0, result.Grazes + result.MediumImpacts + result.StrongImpacts);
        }
    }

    [Fact]
    public void The_flying_lap_is_faster_than_the_standing_start_lap()
    {
        StintResult result = StintRunner.Run(Circuit("circuit_01"), Ship("ship_01"), StintOptions.Default);
        Assert.True(result.FlyingLap < result.FirstLap);
    }

    [Fact]
    public void With_boost_it_is_spent_on_the_longest_straight_at_the_same_point_for_all_ships()
    {
        BalanceCircuit circuit = Circuit("test_oval");
        StintOptions options = StintOptions.Default;
        options.Pilot.UseBoost = true;
        const float oneStep = 3f;   // m: a estas velocidades, lo que avanza la nave en un paso

        foreach (string ship in Ships)
        {
            StintResult result = StintRunner.Run(circuit, Ship(ship), options);
            Assert.True(result.BoostsUsed >= 1, ship);
            Assert.InRange(result.FirstBoostAt, circuit.LongestStraightStart, circuit.LongestStraightStart + oneStep);
        }
    }

    [Fact]
    public void Without_the_option_no_boost_is_used()
    {
        StintResult result = StintRunner.Run(Circuit("test_oval"), Ship("ship_01"), StintOptions.Default);
        Assert.Equal(0, result.BoostsUsed);
    }

    [Fact]
    public void Segments_are_the_same_curves_the_circuit_selector_counts()
    {
        TrackFile file = TrackFile.Load("circuit_01");
        List<Vector3> centerline = file.Centerline();
        TrackData data = TrackAnalysis.Analyze(centerline, file.TrackWidth, Array.Empty<float>(), 8);

        List<TrackSegment> segments = TrackAnalysis.Segments(centerline);

        Assert.Equal(data.Turns, segments.Count(s => s.IsCurve));
        Assert.Equal(data.LongestStraight, segments.Where(s => !s.IsCurve).Max(s => s.Length));
    }

    // --- Reglas ---

    private static StintResult Stint(string ship, double lap, double[] sectors, string circuit = "c", bool test = false, int respawns = 0) => new()
    {
        CircuitId = circuit,
        CircuitName = circuit,
        CircuitIsTest = test,
        ShipId = ship,
        Finished = true,
        Respawns = respawns,
        FlyingLap = lap,
        Sectors = sectors
    };

    private static List<StintResult> Fair(string circuit = "c", bool test = false, double bladeGap = 0.1) => new()
    {
        Stint("ship_02", 50.0 + bladeGap, new[] { 10.0, 11.0, 12.0, 13.1 }, circuit, test),
        Stint("ship_01", 50.0, new[] { 11.0, 10.0, 12.5, 13.0 }, circuit, test),
        Stint("ship_03", 50.2, new[] { 11.5, 11.0, 11.5, 13.5 }, circuit, test)
    };

    [Fact]
    public void Rule_1_passes_at_029_and_fails_at_031()
    {
        Assert.True(BalanceRules.CheckCircuit(Fair(bladeGap: 0.29)).Rule1);
        Assert.False(BalanceRules.CheckCircuit(Fair(bladeGap: 0.31)).Rule1);
        Assert.False(BalanceRules.CheckCircuit(Fair(bladeGap: -0.31)).Rule1);
    }

    [Fact]
    public void Rule_2_passes_when_each_ship_wins_a_sector_and_none_wins_all()
    {
        CircuitCheck check = BalanceRules.CheckCircuit(Fair());

        Assert.True(check.Rule2);
        Assert.Equal(1, check.SectorsWon["ship_02"]);
    }

    [Fact]
    public void Rule_2_fails_if_a_ship_wins_all_sectors()
    {
        var stints = new List<StintResult>
        {
            Stint("ship_02", 50.0, new[] { 1.0, 1.0, 1.0, 1.0 }),
            Stint("ship_01", 50.1, new[] { 2.0, 2.0, 2.0, 2.0 }),
            Stint("ship_03", 50.2, new[] { 3.0, 3.0, 3.0, 3.0 })
        };
        Assert.False(BalanceRules.CheckCircuit(stints).Rule2);
    }

    [Fact]
    public void Rule_2_fails_if_a_ship_wins_no_sector()
    {
        var stints = new List<StintResult>
        {
            Stint("ship_02", 50.0, new[] { 1.0, 2.0, 1.0, 2.0 }),
            Stint("ship_01", 50.1, new[] { 2.0, 1.0, 2.0, 1.0 }),
            Stint("ship_03", 50.2, new[] { 3.0, 3.0, 3.0, 3.0 })
        };
        CircuitCheck check = BalanceRules.CheckCircuit(stints);
        Assert.False(check.Rule2);
        Assert.Equal(0, check.SectorsWon["ship_03"]);
    }

    [Fact]
    public void A_stint_with_a_respawn_is_not_valid_and_fails_the_rules()
    {
        List<StintResult> stints = Fair();
        stints[0] = Stint("ship_02", 50.1, new[] { 10.0, 11.0, 12.0, 13.0 }, respawns: 1);

        CircuitCheck check = BalanceRules.CheckCircuit(stints);

        Assert.False(stints[0].IsValid);
        Assert.False(check.AllValid);
        Assert.False(check.Rule1);
        Assert.False(check.Rule2);
    }

    [Fact]
    public void Test_circuits_are_reported_but_do_not_fail_the_rules()
    {
        var results = new List<StintResult>();
        results.AddRange(Fair("real"));
        results.AddRange(Fair("oval", test: true, bladeGap: 5.0));

        List<CircuitCheck> checks = BalanceRules.Check(results);

        Assert.False(checks.Single(c => c.CircuitId == "oval").Passed);
        Assert.True(BalanceRules.MandatoryPassed(checks));
        Assert.Contains("oval", BalanceReport.Text(results));
    }

    [Fact]
    public void A_real_circuit_that_fails_fails_the_mandatory_rules()
    {
        Assert.False(BalanceRules.MandatoryPassed(BalanceRules.Check(Fair(bladeGap: 5.0))));
    }

    // --- Barrido ---

    [Fact]
    public void Sweep_gives_one_result_per_value_of_the_range()
    {
        Assert.Equal(new[] { 0.94f, 0.95f, 0.96f, 0.97f, 0.98f, 0.99f, 1f }, BalanceSweep.Values(0.94f, 1f, 0.01f));
        Assert.Empty(BalanceSweep.Values(1f, 0.9f, 0.01f));
        Assert.Empty(BalanceSweep.Values(0.9f, 1f, 0f));

        BalanceCircuit circuit = Circuit("test_oval");
        ShipDefinition[] ships = Ships.Select(Ship).ToArray();
        var sweep = new BalanceSweep(new[] { circuit }, ships, Ship("ship_03"), BalanceParameter.MaxSpeed, StintOptions.Default);

        var rows = BalanceSweep.Values(0.94f, 0.98f, 0.02f).Select(sweep.Evaluate).ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(3, row.Checks[0].GapToReference.Count));
        // Más punta en un óvalo: HAMMER se acerca a VECTOR
        Assert.True(rows[2].Checks[0].GapToReference["ship_03"] < rows[0].Checks[0].GapToReference["ship_03"]);
    }

    [Fact]
    public void A_sweep_does_not_change_the_definitions()
    {
        ShipTuning before = Ship("ship_03").Technical;
        var sweep = new BalanceSweep(new[] { Circuit("test_oval") }, Ships.Select(Ship).ToArray(), Ship("ship_03"), BalanceParameter.MaxSpeed, StintOptions.Default);

        sweep.Evaluate(1.05f);

        Assert.Equal(before, Ship("ship_03").Technical);
    }

    [Fact]
    public void A_variant_only_changes_its_parameter()
    {
        ShipDefinition hammer = Ship("ship_03");

        ShipDefinition faster = ShipVariant.With(hammer, BalanceParameter.MaxSpeed, 1f);
        Assert.Equal(VectorTuning.MaxSpeed, faster.Technical.MaxSpeed);
        Assert.Equal(hammer.Technical.Acceleration, faster.Technical.Acceleration);
        Assert.Equal(hammer.Technical.AccelCurve, faster.Technical.AccelCurve);

        ShipDefinition curve = ShipVariant.With(hammer, BalanceParameter.AccelCurve, 2.5f);
        Assert.Equal(2.5f, curve.Technical.AccelCurve);
        Assert.Equal(hammer.Technical.MaxSpeed, curve.Technical.MaxSpeed);

        // Con su valor actual, la nave no cambia
        foreach (BalanceParameter parameter in Enum.GetValues<BalanceParameter>())
            Assert.Equal(hammer.Technical, ShipVariant.With(hammer, parameter, ShipVariant.Current(hammer, parameter)).Technical);
    }

    [Theory]
    [InlineData(BalanceParameter.MaxSpeed, 0.90f, true)]
    [InlineData(BalanceParameter.MaxSpeed, 1.10f, true)]
    [InlineData(BalanceParameter.MaxSpeed, 1.12f, false)]
    [InlineData(BalanceParameter.AccelCurve, 1.5f, true)]
    [InlineData(BalanceParameter.AccelCurve, 3.0f, true)]
    [InlineData(BalanceParameter.AccelCurve, 1.4f, false)]
    public void The_range_is_the_usual_one(BalanceParameter parameter, float value, bool within)
    {
        Assert.Equal(within, ShipVariant.IsWithinRange(parameter, value));
    }

    [Fact]
    public void Match_finds_the_value_of_a_known_function()
    {
        MatchResult match = BalanceSweep.Bisect(value => (value - 0.973f) * 30.0, 0.9f, 1.1f, 0.05);

        Assert.True(match.Found);
        Assert.True(Math.Abs(match.Gap) < 0.05);
        Assert.InRange(match.Value, 0.97f, 0.976f);
    }

    [Fact]
    public void Match_works_when_the_function_goes_down()
    {
        MatchResult match = BalanceSweep.Bisect(value => (1.05f - value) * 30.0, 0.9f, 1.1f, 0.05);

        Assert.True(match.Found);
        Assert.InRange(match.Value, 1.04f, 1.06f);
    }

    [Fact]
    public void Match_says_when_there_is_no_value_in_the_range()
    {
        MatchResult match = BalanceSweep.Bisect(value => (value - 1.5f) * 30.0, 0.9f, 1.1f, 0.05);

        Assert.False(match.Found);
        Assert.Equal(1.1f, match.Value);   // el más cercano
        Assert.Contains("NONE", SweepReport.MatchText("ship_03", BalanceParameter.MaxSpeed, "c", match));
    }

    [Fact]
    public void Match_does_not_trust_an_invalid_stint()
    {
        MatchResult match = BalanceSweep.Bisect(_ => double.NaN, 0.9f, 1.1f, 0.05);

        Assert.False(match.Found);
        Assert.True(double.IsNaN(match.Gap));
    }

    [Fact]
    public void Match_on_a_real_circuit_leaves_the_ship_close_to_the_reference()
    {
        BalanceCircuit circuit = Circuit("circuit_01");
        ShipDefinition hammer = Ship("ship_03");
        var sweep = new BalanceSweep(new[] { circuit }, Ships.Select(Ship).ToArray(), hammer, BalanceParameter.MaxSpeed, StintOptions.Default);

        MatchResult match = sweep.Match(circuit);

        Assert.True(match.Found);
        double reference = StintRunner.Run(circuit, Ship("ship_01"), StintOptions.Default).FlyingLap;
        double lap = StintRunner.Run(circuit, ShipVariant.With(hammer, BalanceParameter.MaxSpeed, match.Value), StintOptions.Default).FlyingLap;
        Assert.True(Math.Abs(lap - reference) < BalanceSweep.MatchTolerance);
    }

    [Fact]
    public void The_sweep_text_marks_what_passes_and_what_is_out_of_range()
    {
        var pass = new SweepRow { Value = 0.97f, Checks = new[] { BalanceRules.CheckCircuit(Fair()) } };
        var fail = new SweepRow { Value = 1.15f, OutOfRange = true, Checks = new[] { BalanceRules.CheckCircuit(Fair(bladeGap: 5.0)) } };

        string text = SweepReport.Text("ship_03", BalanceParameter.MaxSpeed, new[] { pass, fail });

        Assert.True(pass.Passed);
        Assert.False(fail.Passed);
        Assert.Contains("0.9700  ✓", text);
        Assert.Contains(SweepReport.OutOfRangeMark, text);
        Assert.Contains("PASS: 0.9700", text);
    }

    // --- Informe ---

    [Fact]
    public void The_csv_has_a_row_per_ship_and_circuit_with_all_the_columns()
    {
        var results = new List<StintResult>();
        results.AddRange(StintsWithData("real"));
        results.AddRange(StintsWithData("oval"));

        string[] lines = BalanceReport.Csv(results).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(1 + results.Count, lines.Length);
        string[] header = lines[0].Split(',');
        foreach (string column in new[]
        {
            "circuit", "ship", "valid", "flying_lap", "first_lap", "total_time", "sectors", "top_speed_kmh", "top_speed_at_m",
            "curve_min_kmh", "full_throttle_pct", "grazes", "medium", "strong", "respawns"
        })
            Assert.Contains(column, header);

        string[] row = lines[1].Split(',');
        Assert.Equal(header.Length, row.Length);
        Assert.Equal("real", row[Array.IndexOf(header, "circuit")]);
        Assert.Equal("50.000", row[Array.IndexOf(header, "flying_lap")]);
        Assert.Equal("10.000|11.000", row[Array.IndexOf(header, "sectors")]);
        Assert.Equal("180", row[Array.IndexOf(header, "top_speed_kmh")]);
        Assert.Equal("72|108", row[Array.IndexOf(header, "curve_min_kmh")]);
        Assert.Equal("75.0", row[Array.IndexOf(header, "full_throttle_pct")]);
    }

    private static IEnumerable<StintResult> StintsWithData(string circuit)
    {
        foreach (string ship in Ships)
        {
            yield return new StintResult
            {
                CircuitId = circuit,
                CircuitName = circuit,
                ShipId = ship,
                Finished = true,
                FlyingLap = 50.0,
                Sectors = new[] { 10.0, 11.0 },
                TopSpeed = 50f,
                CurveMinSpeeds = new[] { 20f, 30f },
                FullThrottleShare = 0.75f
            };
        }
    }

    [Fact]
    public void The_file_name_has_the_date_and_time()
    {
        Assert.Equal("balance_2026-11-05_19-30.csv", BalanceReport.FileName(new DateTime(2026, 11, 5, 19, 30, 12)));
    }
}
