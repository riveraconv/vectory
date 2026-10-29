using System.Collections.Generic;

namespace Vectory.Circuit;

// Lista única de los circuitos del juego (rutas de sus TrackDefinition), en el orden del selector.
// La usan el menú (circuito de la sesión), Track (circuito por defecto) y la pantalla RECORDS.
// Para añadir un circuito: su .tres en circuits/ (con un Id único) y su ruta aquí.
public static class CircuitCatalog
{
	public static readonly IReadOnlyList<string> Paths = new[]
	{
		"res://circuits/circuit_01.tres"
	};

	// El primero de la lista: el de por defecto mientras no haya selector de circuito
	public static string Default => Paths[0];
}
