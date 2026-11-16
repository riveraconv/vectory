using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Ship;

namespace Vectory.Rivals;

// Un rival de la carrera (spec docs/rivales-1-parrilla.md, 3.1): identificador fijo dentro de la carrera (rival_1 a
// rival_7), su número para la lista de naves en carrera (1 a 7), su marca de parrilla (1..N), su nave y su pintura
// (una de las 40 combinaciones de esa nave, como índice y como elección)
public sealed record RivalEntry(string Id, int Number, int GridPosition, string ShipDesignId, int PaintIndex, PaintChoice Paint);

// Alineación de una carrera de RACE: los rivales que ocupan las marcas que deja libres el jugador, cada uno con una nave
// al azar (misma probabilidad, sin límite de repeticiones) y una pintura al azar de las 40 de esa nave. Todo sale del
// mismo generador que el reparto de la parrilla, después de él: con la misma semilla sale la misma alineación.
// Lógica pura: sin nodos ni archivos.
public static class RivalLineup
{
	public const string IdPrefix = "rival_";

	// Cuántos rivales hay en una carrera: los pedidos, como mucho uno por marca libre; en TIME TRIAL, ninguno
	public static int CountFor(Vectory.Game.GameMode mode, int participants, int requested) =>
		mode == Vectory.Game.GameMode.Race ? Math.Clamp(requested, 0, Math.Max(0, participants - 1)) : 0;

	// Las naves que pueden llevar los rivales: las elegibles del catálogo, en su orden
	public static IReadOnlyList<string> Ships => ShipCatalog.Entries.Where(e => e.Selectable && HullPaints.StyleOf(e.Id) != null).Select(e => e.Id).ToArray();

	// gridOrder: el reparto de GridLayout.RandomOrder; el elemento 0 es la marca del jugador y el i, la del rival i.
	// count: cuántos rivales (como mucho, uno por marca libre; negativo = todas las marcas libres).
	public static IReadOnlyList<RivalEntry> Build(IReadOnlyList<int> gridOrder, Random rng, int count = -1) => Build(gridOrder, rng, Ships, count);

	public static IReadOnlyList<RivalEntry> Build(IReadOnlyList<int> gridOrder, Random rng, IReadOnlyList<string> ships, int count = -1)
	{
		ArgumentNullException.ThrowIfNull(gridOrder);
		ArgumentNullException.ThrowIfNull(rng);
		if (ships == null || ships.Count == 0)
			throw new ArgumentException("Hace falta al menos una nave.", nameof(ships));

		int total = count < 0 ? gridOrder.Count - 1 : Math.Min(count, gridOrder.Count - 1);
		var rivals = new List<RivalEntry>(Math.Max(0, total));
		for (int i = 1; i <= total; i++)
		{
			string ship = ships[rng.Next(ships.Count)];
			HullStyle style = HullPaints.StyleOf(ship) ?? throw new ArgumentException($"La nave '{ship}' no tiene casco 3D.", nameof(ships));
			int paint = rng.Next(HullPaints.Count(style));
			rivals.Add(new RivalEntry(IdPrefix + i, i, gridOrder[i], ship, paint, HullPaints.ChoiceAt(style, paint)));
		}
		return rivals;
	}
}
