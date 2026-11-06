using System;
using System.Collections.Generic;
using System.Linq;

namespace Vectory.Balance;

// Comprobación de las reglas 1 y 2 en un circuito
public sealed class CircuitCheck
{
	public string CircuitId { get; init; } = "";
	public string CircuitName { get; init; } = "";
	public bool IsMandatory { get; init; }         // los circuitos de pruebas se informan pero no obligan

	public bool Rule1 { get; init; }               // nadie imbatible
	public bool Rule2 { get; init; }               // personalidad
	public bool AllValid { get; init; }            // todas las tandas terminadas y sin recolocación

	// Por nave: diferencia con la de referencia (s; positiva = más lenta) y sectores ganados
	public IReadOnlyDictionary<string, double> GapToReference { get; init; } = new Dictionary<string, double>();
	public IReadOnlyDictionary<string, int> SectorsWon { get; init; } = new Dictionary<string, int>();

	public bool Passed => Rule1 && Rule2;
}

// Las reglas del equilibrio (spec docs/naves-6-equilibrio.md, 2), con los resultados de las naves en cada circuito.
// Una tanda que no vale incumple las dos. Un empate en un sector (a menos de un microsegundo) lo ganan las empatadas.
// Lógica pura: no depende de Godot.
public static class BalanceRules
{
	public const double Rule1Margin = 0.3;   // s
	public const string ReferenceShipId = "ship_01";

	private const double TieTolerance = 1e-6;

	// Una comprobación por circuito, en el orden en que aparecen
	public static List<CircuitCheck> Check(IEnumerable<StintResult> results)
	{
		var checks = new List<CircuitCheck>();
		foreach (IGrouping<string, StintResult> circuit in results.GroupBy(r => r.CircuitId))
			checks.Add(CheckCircuit(circuit.ToList()));
		return checks;
	}

	// Las reglas obligatorias (las de los circuitos que no son de pruebas) se cumplen
	public static bool MandatoryPassed(IEnumerable<CircuitCheck> checks) =>
		checks.Where(c => c.IsMandatory).All(c => c.Passed);

	public static CircuitCheck CheckCircuit(IReadOnlyList<StintResult> stints)
	{
		StintResult first = stints[0];
		StintResult reference = stints.FirstOrDefault(s => s.ShipId == ReferenceShipId);
		bool allValid = stints.All(s => s.IsValid);

		var gaps = new Dictionary<string, double>();
		bool rule1 = allValid && reference != null;
		foreach (StintResult stint in stints)
		{
			double gap = reference != null && stint.IsValid && reference.IsValid ? stint.FlyingLap - reference.FlyingLap : double.NaN;
			gaps[stint.ShipId] = gap;
			if (stint != reference && !(Math.Abs(gap) <= Rule1Margin))
				rule1 = false;
		}

		Dictionary<string, int> won = SectorWins(stints);
		int sectorCount = stints.Max(s => s.Sectors.Count);
		bool rule2 = allValid && stints.Count > 1 && sectorCount > 0
			&& won.Values.All(w => w >= 1 && w < sectorCount);

		return new CircuitCheck
		{
			CircuitId = first.CircuitId,
			CircuitName = first.CircuitName,
			IsMandatory = !first.CircuitIsTest,
			Rule1 = rule1,
			Rule2 = rule2,
			AllValid = allValid,
			GapToReference = gaps,
			SectorsWon = won
		};
	}

	private static Dictionary<string, int> SectorWins(IReadOnlyList<StintResult> stints)
	{
		var won = stints.ToDictionary(s => s.ShipId, _ => 0);
		int sectorCount = stints.Max(s => s.Sectors.Count);
		for (int sector = 0; sector < sectorCount; sector++)
		{
			var inSector = stints.Where(s => s.IsValid && sector < s.Sectors.Count).ToList();
			if (inSector.Count == 0)
				continue;

			int index = sector;
			double best = inSector.Min(s => s.Sectors[index]);
			foreach (StintResult stint in inSector)
			{
				if (stint.Sectors[index] <= best + TieTolerance)
					won[stint.ShipId]++;
			}
		}
		return won;
	}
}
