using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Vectory.Rivals;

// Adelantamientos de una carrera (spec docs/rivales-4-personalidades.md, 8): con el orden de la clasificación en directo
// de cada paso, cada pareja que se cruza cuenta uno hecho para la que pasa delante y uno sufrido para la otra, si las dos
// están corriendo (no en la previa ni terminadas). Lógica pura.
public sealed class OvertakeCounter
{
	private readonly Dictionary<int, int> _made = new();
	private readonly Dictionary<int, int> _suffered = new();
	private readonly Dictionary<int, int> _previous = new();
	private readonly Dictionary<int, int> _current = new();

	public int Made(int id) => _made.GetValueOrDefault(id);
	public int Suffered(int id) => _suffered.GetValueOrDefault(id);

	// order: ids de delante a detrás; racing: si una nave cuenta ahora
	public void Update(IReadOnlyList<int> order, Func<int, bool> racing)
	{
		_current.Clear();
		for (int i = 0; i < order.Count; i++)
			_current[order[i]] = i;

		if (_previous.Count > 0)
		{
			foreach ((int a, int now) in _current)
			{
				if (!_previous.TryGetValue(a, out int before) || !racing(a))
					continue;
				foreach ((int b, int bNow) in _current)
				{
					if (b == a || bNow <= now || !_previous.TryGetValue(b, out int bBefore) || !racing(b))
						continue;
					// a va ahora delante de b y antes iba detrás
					if (bBefore < before)
					{
						_made[a] = Made(a) + 1;
						_suffered[b] = Suffered(b) + 1;
					}
				}
			}
		}

		_previous.Clear();
		foreach ((int id, int position) in _current)
			_previous[id] = position;
	}
}

// Una fila del informe de carrera. Para el jugador, lo que no se lleva queda en null y sale como —.
public readonly record struct RaceReportRow(int Position, string Name, string Profile, string Ship, int Made, int Suffered,
	int? Rams, int? Received, int? Mutual, int DamagePoints, int DamageLevel, int? Boosts, int? LateBrakings, int? WideExits,
	int? BadStarts);

// Informe de carrera en la consola (spec 8): por piloto, perfil, nave, puesto, adelantamientos hechos y sufridos, golpes
// por papel, daño final, boosts y errores. Sirve para ver si un perfil gana demasiado. Lógica pura.
public static class RaceReport
{
	public static string Text(IEnumerable<RaceReportRow> rows)
	{
		var text = new StringBuilder();
		text.AppendLine("[Race] Report: POS PILOT PROFILE SHIP · OVERTAKES MADE/SUFFERED · HITS RAMS/RECEIVED/MUTUAL · DAMAGE · BOOSTS · ERRORS LATE/WIDE/START");
		foreach (RaceReportRow r in rows.OrderBy(r => r.Position))
		{
			text.AppendLine(string.Create(CultureInfo.InvariantCulture,
				$"[Race]   {r.Position,2} {r.Name,-8} {r.Profile,-12} {r.Ship,-7} OVT {r.Made}/{r.Suffered}  HITS {N(r.Rams)}/{N(r.Received)}/{N(r.Mutual)}  "
				+ $"DMG {r.DamagePoints}P L{r.DamageLevel}  BST {N(r.Boosts)}  ERR {N(r.LateBrakings)}/{N(r.WideExits)}/{N(r.BadStarts)}"));
		}
		return text.ToString().TrimEnd();
	}

	private static string N(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "—";
}
