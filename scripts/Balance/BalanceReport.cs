using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Vectory.Core;
using Vectory.Records;
using Vectory.Ship;

namespace Vectory.Balance;

// Informe del equilibrio (spec docs/naves-6-equilibrio.md, 4.5): tabla para la consola y CSV, a partir de los resultados.
// Lógica pura: no depende de Godot.
public static class BalanceReport
{
	private const float KmhPerMs = 3.6f;
	private const string Yes = "✓";
	private const string No = "✗";

	private static string ShipName(string id) => ShipCatalog.Find(id)?.Name ?? id;

	private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

	private static string Kmh(float metersPerSecond) => Number(metersPerSecond * KmhPerMs, "0");

	private static string Time(StintResult stint, double value) => stint.IsValid || value > 0.0 ? TimeFormat.Format(value) : "--:--.---";

	// Tabla legible: un bloque por circuito, con sus naves y la comprobación de las reglas
	public static string Text(IReadOnlyList<StintResult> results)
	{
		var text = new StringBuilder();
		List<CircuitCheck> checks = BalanceRules.Check(results);
		foreach (CircuitCheck check in checks)
		{
			var stints = results.Where(r => r.CircuitId == check.CircuitId).ToList();
			text.AppendLine();
			text.AppendLine($"=== {check.CircuitName} ({check.CircuitId}){(check.IsMandatory ? "" : " · test circuit: informative only")} ===");
			int sectors = stints.Max(s => s.Sectors.Count);
			string sectorHeader = string.Concat(Enumerable.Range(1, sectors).Select(i => $"{"S" + i,-11}"));
			text.AppendLine($"{"SHIP",-8}{"FLYING",-11}{"GAP",-9}{sectorHeader}{"FIRST",-11}{"TOTAL",-11}{"TOP KM/H",-16}{"W %",-6}{"G/M/S",-8}{"DMG",-5}{"RESP",-6}{"BOOST",-6}");
			foreach (StintResult stint in stints)
			{
				check.GapToReference.TryGetValue(stint.ShipId, out double gap);
				string sectorTimes = string.Concat(stint.Sectors.Select(s => $"{TimeFormat.Format(s),-11}"));
				sectorTimes = sectorTimes.PadRight(sectors * 11);
				string top = $"{Kmh(stint.TopSpeed)} @ {Number(stint.TopSpeedAt, "0")}";
				string gapText = double.IsNaN(gap) ? "—" : DeltaFormat.Format(gap);
				string name = stint.IsValid ? ShipName(stint.ShipId) : ShipName(stint.ShipId) + "!";
				text.AppendLine($"{name,-8}{Time(stint, stint.FlyingLap),-11}{gapText,-9}{sectorTimes}{Time(stint, stint.FirstLap),-11}{Time(stint, stint.TotalTime),-11}{top,-16}{Number(stint.FullThrottleShare * 100f, "0"),-6}{$"{stint.Grazes}/{stint.MediumImpacts}/{stint.StrongImpacts}",-8}{stint.DamageLevel,-5}{stint.Respawns,-6}{stint.BoostsUsed,-6}");
				text.AppendLine($"        curves min km/h: {string.Join(" ", stint.CurveMinSpeeds.Select(Kmh))}");
			}

			string wins = string.Join(" · ", check.SectorsWon.Select(w => $"{ShipName(w.Key)} {w.Value}"));
			string gaps = string.Join(" · ", check.GapToReference.Where(g => g.Key != BalanceRules.ReferenceShipId)
				.Select(g => $"{ShipName(g.Key)} {(double.IsNaN(g.Value) ? "—" : DeltaFormat.Format(g.Value))}"));
			text.AppendLine($"RULE 1 {(check.Rule1 ? Yes : No)} within ±{Number(BalanceRules.Rule1Margin, "0.0")} s: {gaps}");
			text.AppendLine($"RULE 2 {(check.Rule2 ? Yes : No)} sectors won: {wins}");
			if (!check.AllValid)
				text.AppendLine($"{No} Invalid stint: not finished or repositioned (ships marked with !)");
		}

		text.AppendLine();
		text.AppendLine($"MANDATORY RULES: {(BalanceRules.MandatoryPassed(checks) ? Yes : No)}");
		return text.ToString();
	}

	// Una fila por nave y circuito. Sectores y curvas varían por circuito: van en una columna, separados por "|".
	public static string Csv(IReadOnlyList<StintResult> results)
	{
		var csv = new StringBuilder();
		csv.AppendLine("circuit,ship,valid,flying_lap,first_lap,total_time,sectors,top_speed_kmh,top_speed_at_m,curve_min_kmh,full_throttle_pct,grazes,medium,strong,respawns,damage_level,boosts");

		foreach (StintResult r in results)
		{
			string sectors = string.Join("|", r.Sectors.Select(s => Number(s, "0.000")));
			string curves = string.Join("|", r.CurveMinSpeeds.Select(Kmh));
			csv.AppendLine(string.Join(",",
				r.CircuitId,
				r.ShipId,
				r.IsValid ? "true" : "false",
				Number(r.FlyingLap, "0.000"),
				Number(r.FirstLap, "0.000"),
				Number(r.TotalTime, "0.000"),
				sectors,
				Kmh(r.TopSpeed),
				Number(r.TopSpeedAt, "0"),
				curves,
				Number(r.FullThrottleShare * 100f, "0.0"),
				r.Grazes.ToString(CultureInfo.InvariantCulture),
				r.MediumImpacts.ToString(CultureInfo.InvariantCulture),
				r.StrongImpacts.ToString(CultureInfo.InvariantCulture),
				r.Respawns.ToString(CultureInfo.InvariantCulture),
				r.DamageLevel.ToString(CultureInfo.InvariantCulture),
				r.BoostsUsed.ToString(CultureInfo.InvariantCulture)));
		}
		return csv.ToString();
	}

	// Nombre del archivo, con la fecha y la hora
	public static string FileName(DateTime when) =>
		$"balance_{when.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture)}.csv";
}
