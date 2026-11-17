using System;
using System.Collections.Generic;
using System.Linq;
using Vectory.Ship;

namespace Vectory.Rivals;

// Un rival de la carrera (spec docs/rivales-1-parrilla.md, 3.1): identificador fijo dentro de la carrera (rival_1 a
// rival_7), su número para la lista de naves en carrera (1 a 7), su marca de parrilla (1..N), su nave y su pintura
// (una de las 40 combinaciones de esa nave, como índice y como elección) y su piloto (spec docs/rivales-2-clasificacion.md,
// 3.1), que no decide ni la nave ni la pintura, y lo que su reacción en el GO se aparta esta vez de la de su piloto
// (spec docs/rivales-4-personalidades.md, 3.6) y la semilla de sus decisiones al azar en esta carrera (defensa). El ritmo
// es el de su piloto (perfil y variación fija).
public sealed record RivalEntry(string Id, int Number, int GridPosition, string ShipDesignId, int PaintIndex, PaintChoice Paint,
	RivalPilot Pilot, float ReactionJitter = 0f, int Seed = 0)
{
	// s desde el GO hasta que pisa W: la de su piloto más lo de esta salida, nunca negativa
	public float Reaction => System.MathF.Max(0f, Pilot.Traits.Reaction + ReactionJitter);
}

// Alineación de una carrera de RACE: los rivales que ocupan las marcas que deja libres el jugador, cada uno con una nave
// al azar (misma probabilidad, sin límite de repeticiones) y una pintura al azar de las 40 de esa nave. Todo sale del
// mismo generador que el reparto de la parrilla, después de él: con la misma semilla sale la misma alineación. Los pilotos
// (distintos entre sí) se sortean después de naves y pinturas, así estas salen igual que antes de tener pilotos.
// Lógica pura: sin nodos ni archivos.
public static class RivalLineup
{
	public const string IdPrefix = "rival_";

	// Lo que la reacción de cada salida se aparta de la del piloto, como mucho, a cada lado (s)
	public const float ReactionSpread = 0.05f;

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
		if (total > PilotCatalog.All.Count)
			throw new ArgumentException($"Hay {PilotCatalog.All.Count} pilotos: no caben {total} rivales.", nameof(count));
		var picks = new (string Ship, int Paint)[Math.Max(0, total)];
		for (int i = 0; i < picks.Length; i++)
		{
			string ship = ships[rng.Next(ships.Count)];
			HullStyle style = HullPaints.StyleOf(ship) ?? throw new ArgumentException($"La nave '{ship}' no tiene casco 3D.", nameof(ships));
			picks[i] = (ship, rng.Next(HullPaints.Count(style)));
		}
		IReadOnlyList<RivalPilot> pilots = PilotCatalog.Draw(rng, picks.Length);

		// La reacción de esta salida, después de los pilotos: así naves, pinturas y pilotos salen igual que antes
		var reactions = new float[picks.Length];
		for (int i = 0; i < reactions.Length; i++)
			reactions[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * ReactionSpread;

		// Las semillas de sus decisiones, al final: lo de antes sale igual
		var seeds = new int[picks.Length];
		for (int i = 0; i < seeds.Length; i++)
			seeds[i] = rng.Next();

		var rivals = new List<RivalEntry>(picks.Length);
		for (int i = 0; i < picks.Length; i++)
		{
			(string ship, int paint) = picks[i];
			HullStyle style = HullPaints.StyleOf(ship).Value;
			rivals.Add(new RivalEntry(IdPrefix + (i + 1), i + 1, gridOrder[i + 1], ship, paint, HullPaints.ChoiceAt(style, paint), pilots[i], reactions[i], seeds[i]));
		}
		return rivals;
	}
}
