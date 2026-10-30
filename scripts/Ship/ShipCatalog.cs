using System.Collections.Generic;
using System.Linq;

namespace Vectory.Ship;

// Lista única de diseños de nave del juego, por su identificador. Hoy solo hay uno: la nave actual
// (Spaceship). El fantasma de TIME TRIAL guarda el diseño con el que se hizo la vuelta; HANGAR lo cambiará.
public static class ShipCatalog
{
	public const string Ship01 = "ship_01";

	public static readonly IReadOnlyList<string> Ids = new[] { Ship01 };

	public static string Default => Ids[0];

	public static bool Contains(string id) => id != null && Ids.Contains(id);
}
