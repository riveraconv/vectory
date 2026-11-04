using System.Collections.Generic;
using System.Linq;

namespace Vectory.Ship;

// Una nave del catálogo. Sin parámetros de conducción no se puede elegir todavía (COMING SOON):
// solo tiene nombre y descripción para el selector.
public sealed record ShipEntry(string Id, string Name, string Description, ShipTuning? Tuning = null)
{
	public bool Selectable => Tuning.HasValue;
}

// Lista única de naves del juego, en el orden de SELECT SHIP. El fantasma de TIME TRIAL guarda el diseño
// con el que se hizo la vuelta. ship_01 no puede cambiar de id: los fantasmas guardados lo usan.
public static class ShipCatalog
{
	public const string Ship01 = "ship_01";
	public const string Ship02 = "ship_02";
	public const string Ship03 = "ship_03";

	public static readonly IReadOnlyList<ShipEntry> Entries = new[]
	{
		new ShipEntry(Ship02, "INTERCEPTOR", "HIGH TOP SPEED · LOW TOLERANCE"),
		new ShipEntry(Ship01, "VECTOR", "BALANCED · ALL-ROUND", VectorTuning.Values),
		new ShipEntry(Ship03, "VANGUARD", "HIGH ACCELERATION · STRONG LAUNCH")
	};

	public static readonly IReadOnlyList<string> Ids = Entries.Select(e => e.Id).ToArray();

	public static string Default => Ship01;

	public static bool Contains(string id) => id != null && Ids.Contains(id);

	// La entrada de una nave, o null si no existe
	public static ShipEntry Find(string id) => Entries.FirstOrDefault(e => e.Id == id);
}
