using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Ship;

namespace Vectory.Balance;

// Lo que se puede barrer: los multiplicadores de la spec 2 y la curva de la spec 5
public enum BalanceParameter
{
	MaxSpeed,
	Acceleration,
	TurnRate,
	LateralGrip,
	BrakeDeceleration,
	AccelCurve
}

// Una nave con otro valor en un parámetro, sin tocar su definición. Lógica pura.
public static class ShipVariant
{
	public static float Min(BalanceParameter parameter) =>
		parameter == BalanceParameter.AccelCurve ? AccelerationCurve.MinAllowed : 1f - ShipMultipliers.BalanceLimit;

	public static float Max(BalanceParameter parameter) =>
		parameter == BalanceParameter.AccelCurve ? AccelerationCurve.MaxAllowed : 1f + ShipMultipliers.BalanceLimit;

	public static bool IsWithinRange(BalanceParameter parameter, float value) =>
		parameter == BalanceParameter.AccelCurve ? AccelerationCurve.IsWithinRange(value) : ShipMultipliers.IsWithinLimit(value);

	public static float Current(ShipDefinition ship, BalanceParameter parameter) =>
		parameter == BalanceParameter.AccelCurve ? ship.Technical.AccelCurve : ship.Multipliers[ToShipParameter(parameter)];

	public static ShipDefinition With(ShipDefinition ship, BalanceParameter parameter, float value)
	{
		if (parameter == BalanceParameter.AccelCurve)
			return ship with { Technical = ship.Technical with { AccelCurve = value } };

		ShipMultipliers multipliers = ship.Multipliers.With(ToShipParameter(parameter), value);
		ShipTuning technical = multipliers.Apply(ShipDefinitions.Default.Technical);
		technical.AccelCurve = ship.Technical.AccelCurve;
		technical.MinThrust = ship.Technical.MinThrust;
		return ship with { Technical = technical, Multipliers = multipliers };
	}

	private static ShipParameter ToShipParameter(BalanceParameter parameter) => parameter switch
	{
		BalanceParameter.MaxSpeed => ShipParameter.MaxSpeed,
		BalanceParameter.Acceleration => ShipParameter.Acceleration,
		BalanceParameter.TurnRate => ShipParameter.TurnRate,
		BalanceParameter.LateralGrip => ShipParameter.LateralGrip,
		BalanceParameter.BrakeDeceleration => ShipParameter.BrakeDeceleration,
		_ => throw new ArgumentOutOfRangeException(nameof(parameter))
	};
}

// Un valor del barrido: las tandas de la nave con ese valor y las reglas en cada circuito
public sealed class SweepRow
{
	public float Value { get; init; }
	public bool OutOfRange { get; init; }
	public IReadOnlyList<CircuitCheck> Checks { get; init; } = Array.Empty<CircuitCheck>();

	// Reglas 1 y 2 en los circuitos que obligan
	public bool Passed => BalanceRules.MandatoryPassed(Checks);
}

// Resultado de MATCH. Sin un valor válido, Value es el mejor que se ha probado (NaN en Gap si la tanda no valía).
public readonly record struct MatchResult(bool Found, float Value, double Gap);

// Barrido de un parámetro de una nave (spec docs/naves-6-equilibrio.md, 5). SWEEP corre la tanda con cada valor;
// MATCH busca por bisección el valor con el que su vuelta lanzada queda a menos de 0,05 s de la de la nave de referencia.
// Las otras naves se corren una vez por circuito. Solo propone números: no cambia las definiciones.
// Lógica pura: no depende de Godot.
public sealed class BalanceSweep
{
	public const double MatchTolerance = 0.05;   // s

	private const int MaxBisectionSteps = 16;

	private readonly IReadOnlyList<BalanceCircuit> _circuits;
	private readonly IReadOnlyList<ShipDefinition> _ships;
	private readonly ShipDefinition _ship;
	private readonly BalanceParameter _parameter;
	private readonly StintOptions _options;
	private readonly Dictionary<(string Circuit, string Ship), StintResult> _baseline = new();

	public BalanceSweep(IReadOnlyList<BalanceCircuit> circuits, IReadOnlyList<ShipDefinition> ships, ShipDefinition ship,
		BalanceParameter parameter, in StintOptions options)
	{
		_circuits = circuits;
		_ships = ships;
		_ship = ship;
		_parameter = parameter;
		_options = options;
	}

	// Los valores del rango, ambos extremos incluidos. Sin paso positivo o con el rango al revés, ninguno.
	public static List<float> Values(float from, float to, float step)
	{
		var values = new List<float>();
		if (step <= 0f || to < from)
			return values;

		int count = (int)MathF.Round((to - from) / step + 0.0001f, MidpointRounding.ToZero) + 1;
		for (int i = 0; i < count; i++)
			values.Add((float)Math.Round(from + (double)step * i, 4));
		return values;
	}

	public SweepRow Evaluate(float value)
	{
		ShipDefinition variant = ShipVariant.With(_ship, _parameter, value);
		var stints = new List<StintResult>();
		foreach (BalanceCircuit circuit in _circuits)
		{
			foreach (ShipDefinition ship in _ships)
				stints.Add(ship.Id == _ship.Id ? StintRunner.Run(circuit, variant, _options) : Baseline(circuit, ship));
		}
		return new SweepRow
		{
			Value = value,
			OutOfRange = !ShipVariant.IsWithinRange(_parameter, value),
			Checks = BalanceRules.Check(stints)
		};
	}

	// El valor con el que la vuelta lanzada de la nave iguala a la de referencia en un circuito, dentro del rango permitido
	public MatchResult Match(BalanceCircuit circuit)
	{
		StintResult reference = Baseline(circuit, ShipDefinitions.Find(BalanceRules.ReferenceShipId));
		return Bisect(value =>
		{
			StintResult stint = StintRunner.Run(circuit, ShipVariant.With(_ship, _parameter, value), _options);
			return stint.IsValid && reference.IsValid ? stint.FlyingLap - reference.FlyingLap : double.NaN;
		}, ShipVariant.Min(_parameter), ShipVariant.Max(_parameter), MatchTolerance);
	}

	// Bisección de gap(valor) = 0 entre low y high. Si no cambia de signo o una tanda no vale, no hay valor.
	public static MatchResult Bisect(Func<float, double> gap, float low, float high, double tolerance)
	{
		double atLow = gap(low);
		double atHigh = gap(high);
		if (double.IsNaN(atLow) || double.IsNaN(atHigh))
			return new MatchResult(false, double.IsNaN(atLow) ? low : high, double.NaN);

		MatchResult best = Math.Abs(atLow) <= Math.Abs(atHigh) ? new MatchResult(false, low, atLow) : new MatchResult(false, high, atHigh);
		if (Math.Abs(best.Gap) < tolerance)
			return best with { Found = true };
		if (Math.Sign(atLow) == Math.Sign(atHigh))
			return best;

		for (int i = 0; i < MaxBisectionSteps; i++)
		{
			float middle = (low + high) * 0.5f;
			double atMiddle = gap(middle);
			if (double.IsNaN(atMiddle))
				return new MatchResult(false, middle, double.NaN);
			if (Math.Abs(atMiddle) < Math.Abs(best.Gap))
				best = new MatchResult(false, middle, atMiddle);
			if (Math.Abs(atMiddle) < tolerance)
				return best with { Found = true };

			if (Math.Sign(atMiddle) == Math.Sign(atLow))
				low = middle;
			else
				high = middle;
		}
		return best;
	}

	private StintResult Baseline(BalanceCircuit circuit, ShipDefinition ship)
	{
		if (!_baseline.TryGetValue((circuit.Id, ship.Id), out StintResult result))
		{
			result = StintRunner.Run(circuit, ship, _options);
			_baseline[(circuit.Id, ship.Id)] = result;
		}
		return result;
	}
}

// Texto de SWEEP y MATCH para la consola. Lógica pura.
public static class SweepReport
{
	public const string OutOfRangeMark = "OUT OF RANGE";

	private const string Yes = "✓";
	private const string No = "✗";

	private static string Name(string id) => ShipCatalog.Find(id)?.Name ?? id;

	private static string Number(double value) => value.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);

	private static string Gap(double gap) => double.IsNaN(gap) ? "—" : Vectory.Records.DeltaFormat.Format(gap);

	public static string Text(string shipId, BalanceParameter parameter, IReadOnlyList<SweepRow> rows)
	{
		var text = new System.Text.StringBuilder();
		text.AppendLine();
		text.AppendLine($"=== SWEEP · {Name(shipId)} · {parameter} ===");
		if (shipId == BalanceRules.ReferenceShipId && parameter != BalanceParameter.AccelCurve)
			text.AppendLine("VECTOR is the reference: its percentages do not change (spec 6)");

		foreach (SweepRow row in rows)
		{
			text.AppendLine($"{Number(row.Value)}  {(row.Passed ? Yes + " RULES 1 AND 2" : No)}{(row.OutOfRange ? "  " + OutOfRangeMark : "")}");
			foreach (CircuitCheck check in row.Checks)
			{
				string gaps = string.Join(" ", check.GapToReference.Where(g => g.Key != BalanceRules.ReferenceShipId).Select(g => $"{Name(g.Key)} {Gap(g.Value)}"));
				string won = string.Join(" ", check.SectorsWon.Select(w => $"{Name(w.Key)} {w.Value}"));
				string test = check.IsMandatory ? "" : " (test)";
				text.AppendLine($"        {check.CircuitName}{test}: {gaps} · won {won} · R1 {(check.Rule1 ? Yes : No)} R2 {(check.Rule2 ? Yes : No)}{(check.AllValid ? "" : " · INVALID STINT")}");
			}
		}

		var passing = rows.Where(r => r.Passed).Select(r => Number(r.Value)).ToList();
		text.AppendLine($"PASS: {(passing.Count > 0 ? string.Join(", ", passing) : "none")}");
		return text.ToString();
	}

	public static string MatchText(string shipId, BalanceParameter parameter, string circuitName, in MatchResult match)
	{
		string range = $"{Number(ShipVariant.Min(parameter))}–{Number(ShipVariant.Max(parameter))}";
		string head = $"=== MATCH · {Name(shipId)} · {parameter} · {circuitName} · range {range} ===";
		if (match.Found)
			return $"\n{head}\nFOUND {Number(match.Value)}: gap to VECTOR {Gap(match.Gap)} (within ±{BalanceSweep.MatchTolerance:0.00} s)\n";
		return $"\n{head}\nNONE within the range. Closest: {Number(match.Value)}, gap to VECTOR {Gap(match.Gap)}\n";
	}
}
