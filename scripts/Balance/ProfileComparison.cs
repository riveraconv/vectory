using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vectory.Ship;

namespace Vectory.Balance;

// Qué es cada fila de la comparación de perfiles
public enum PaceKind
{
	Reference,   // el perfil con el que se compara (equilibrado)
	Profile,     // un perfil, sin variación: regla de ±0,3 s
	Pilot,       // un piloto, con su variación: regla de ±0,4 s
	Info         // solo para ver (el piloto automático de F7): no cuenta
}

// El ritmo de un piloto o de un perfil, sin saber de pilotos: nombre, grupo (su perfil) y lo que cambia en el perfil de
// velocidad
public readonly record struct PaceEntry(string Name, string Group, PaceKind Kind, float StraightMargin, float CurveMargin, float Braking);

// Una fila: la vuelta lanzada de una entrada con una nave y lo que la separa de la referencia con esa nave
public sealed class PaceRow
{
	public string ShipId { get; init; } = "";
	public PaceEntry Entry { get; init; }
	public double FlyingLap { get; init; }
	public bool Valid { get; init; }
	public double Gap { get; init; } = double.NaN;    // s respecto a la referencia (positivo = más lenta)
	public bool Counts => Entry.Kind is PaceKind.Profile or PaceKind.Pilot;
	public double Limit => Entry.Kind == PaceKind.Pilot ? ProfileComparison.PilotMargin : ProfileComparison.ProfileMargin;
	public bool Passed => !Counts || (Valid && Math.Abs(Gap) <= Limit + 1e-9);
	public MatchResult? Proposal { get; init; }       // puntos que mover los dos márgenes para igualar a la referencia
}

// Modo PERFILES (spec docs/rivales-4-personalidades.md, 3.5): la tanda de siempre (vuelta lanzada, sin rivales ni errores)
// con cada perfil y cada piloto y una nave, comparada con la del perfil de referencia con la misma nave. Si un perfil se
// sale, propone cuánto subir o bajar sus dos márgenes a la vez (en puntos) para igualarla: no cambia nada. Lógica pura.
public static class ProfileComparison
{
	public const double ProfileMargin = 0.3;   // s
	public const double PilotMargin = 0.4;     // s
	public const float MaxShift = 0.05f;       // ±5 puntos al buscar la propuesta

	// Las opciones de la tanda con los márgenes y la frenada de una entrada, más un desplazamiento de los dos márgenes
	public static StintOptions OptionsFor(in StintOptions baseOptions, in PaceEntry entry, float shift = 0f)
	{
		StintOptions options = baseOptions;
		options.Pilot.StraightMargin = Math.Clamp(entry.StraightMargin + shift, 0.5f, 1f);
		options.Pilot.SpeedMargin = Math.Clamp(entry.CurveMargin + shift, 0.5f, Vectory.Autopilot.AutopilotSettings.MaxCurveMargin);
		options.Pilot.BrakingShare = entry.Braking;
		return options;
	}

	// Todas las entradas con una nave; la primera de tipo Reference es la referencia
	public static List<PaceRow> Run(BalanceCircuit circuit, ShipDefinition ship, IReadOnlyList<PaceEntry> entries, in StintOptions baseOptions)
	{
		int referenceIndex = -1;
		for (int i = 0; i < entries.Count && referenceIndex < 0; i++)
		{
			if (entries[i].Kind == PaceKind.Reference)
				referenceIndex = i;
		}
		if (referenceIndex < 0)
			throw new ArgumentException("Hace falta una entrada de referencia.", nameof(entries));

		StintOptions options = baseOptions;
		StintResult reference = StintRunner.Run(circuit, ship, OptionsFor(options, entries[referenceIndex]));
		var rows = new List<PaceRow>(entries.Count);
		for (int i = 0; i < entries.Count; i++)
		{
			PaceEntry entry = entries[i];
			StintResult stint = i == referenceIndex ? reference : StintRunner.Run(circuit, ship, OptionsFor(options, entry));
			bool valid = stint.IsValid && reference.IsValid;
			double gap = valid ? stint.FlyingLap - reference.FlyingLap : double.NaN;
			MatchResult? proposal = null;
			if (entry.Kind == PaceKind.Profile && (!valid || Math.Abs(gap) > ProfileMargin))
			{
				PaceEntry e = entry;
				proposal = BalanceSweep.Bisect(shift =>
				{
					StintResult shifted = StintRunner.Run(circuit, ship, OptionsFor(options, e, shift));
					return shifted.IsValid && reference.IsValid ? shifted.FlyingLap - reference.FlyingLap : double.NaN;
				}, -MaxShift, MaxShift, BalanceSweep.MatchTolerance);
			}
			rows.Add(new PaceRow
			{
				ShipId = ship.Id,
				Entry = entry,
				FlyingLap = stint.FlyingLap,
				Valid = stint.IsValid,
				Gap = gap,
				Proposal = proposal
			});
		}
		return rows;
	}

	public static bool AllPassed(IEnumerable<PaceRow> rows) => rows.All(r => r.Passed);

	// Vuelta media de perfiles y pilotos (sin el piloto de F7), de las tandas que valen; NaN si ninguna
	public static double MeanLap(IEnumerable<PaceRow> rows)
	{
		var laps = rows.Where(r => r.Entry.Kind != PaceKind.Info && r.Valid).Select(r => r.FlyingLap).ToList();
		return laps.Count > 0 ? laps.Average() : double.NaN;
	}

	// Tabla de la consola: una por nave y, al final, la propuesta media de cada perfil que se sale
	public static string Text(string circuitName, IReadOnlyList<PaceRow> rows)
	{
		var text = new System.Text.StringBuilder();
		text.AppendLine();
		text.AppendLine($"=== PROFILES · {circuitName} · flying lap, no rivals, no errors ===");
		text.AppendLine($"Profiles within ±{Seconds1(ProfileMargin)} s of the reference, pilots within ±{Seconds1(PilotMargin)} s, with the same ship");
		foreach (IGrouping<string, PaceRow> ship in rows.GroupBy(r => r.ShipId))
		{
			text.AppendLine();
			text.AppendLine($"--- {ShipCatalog.Find(ship.Key)?.Name ?? ship.Key} ---");
			text.AppendLine($"{"",-2}{"NAME",-14}{"PROFILE",-14}{"STR",6}{"CRV",6}{"BRK",6}{"LAP",9}{"GAP",9}");
			foreach (PaceRow row in ship)
			{
				PaceEntry e = row.Entry;
				string mark = !row.Counts ? " " : row.Passed ? "✓" : "✗";
				string proposal = row.Proposal is MatchResult m
					? $"  PROPOSE {Points(m.Value)} BOTH MARGINS{(m.Found ? "" : $" (closest, gap {Gap(m.Gap)})")}"
					: "";
				text.AppendLine($"{mark,-2}{e.Name,-14}{e.Group,-14}{Percent(e.StraightMargin),6}{Percent(e.CurveMargin),6}"
					+ $"{e.Braking.ToString("0.00", CultureInfo.InvariantCulture),6}{(row.Valid ? Seconds(row.FlyingLap) : "INVALID"),9}"
					+ $"{(e.Kind == PaceKind.Reference ? "REF" : Gap(row.Gap)),9}{proposal}");
			}
		}

		text.AppendLine();
		var proposals = rows.Where(r => r.Proposal != null).GroupBy(r => r.Entry.Name).ToList();
		if (proposals.Count == 0)
		{
			text.AppendLine(AllPassed(rows) ? "ALL PROFILES AND PILOTS ✓" : "PILOTS OUT OF RANGE: adjust their variation by hand");
		}
		else
		{
			foreach (IGrouping<string, PaceRow> group in proposals)
			{
				double mean = group.Average(r => r.Proposal.Value.Value);
				text.AppendLine($"PROPOSAL {group.Key}: {Points((float)mean)} on both margins (mean of {group.Count()} ships)");
			}
		}
		int pilotsOut = rows.Count(r => r.Entry.Kind == PaceKind.Pilot && !r.Passed);
		if (pilotsOut > 0)
			text.AppendLine($"{pilotsOut} pilot rows out of ±{Seconds1(PilotMargin)} s");
		return text.ToString();
	}

	private static string Percent(float value) => (value * 100f).ToString("0.0", CultureInfo.InvariantCulture);
	private static string Seconds1(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
	private static string Seconds(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
	private static string Gap(double gap) => double.IsNaN(gap) ? "—" : Vectory.Records.DeltaFormat.Format(gap);

	private static string Points(float shift)
	{
		float points = shift * 100f;
		string value = MathF.Abs(points).ToString("0.0", CultureInfo.InvariantCulture);
		return points >= 0f ? "+" + value : "−" + value;
	}
}
