using System;
using System.Collections.Generic;
using Godot;

namespace Vectory.Circuit;

// Lista única de los circuitos del juego (rutas de sus TrackDefinition), en el orden del selector.
// La usan el menú (SELECT CIRCUIT), Track (circuito por defecto) y la pantalla RECORDS.
// Para añadir un circuito: su .tres en circuits/ (con un Id único) y su ruta en AllPaths.
public static class CircuitCatalog
{
	// Único interruptor de los circuitos de pruebas (TrackDefinition.IsTest): con false no salen
	// ni en SELECT CIRCUIT ni en RECORDS (sus récords y fantasmas se quedan en los archivos).
	public const bool IncludeTestCircuits = true;

	// Todos los circuitos, también los de pruebas, en orden
	public static readonly IReadOnlyList<string> AllPaths = new[]
	{
		"res://circuits/circuit_01.tres",
		"res://circuits/test_oval.tres"
	};

	private static IReadOnlyList<string> _paths;

	// Circuitos que se ven en el juego (sin los de pruebas si IncludeTestCircuits es false)
	public static IReadOnlyList<string> Paths => _paths ??= Filter(AllPaths, IncludeTestCircuits, IsTestCircuit);

	// El primero de la lista: el de por defecto
	public static string Default => Paths.Count > 0 ? Paths[0] : AllPaths[0];

	// Lógica pura del interruptor: las rutas en su orden, quitando las de pruebas si no se incluyen
	public static IReadOnlyList<string> Filter(IEnumerable<string> paths, bool includeTestCircuits, Func<string, bool> isTest)
	{
		var result = new List<string>();
		foreach (string path in paths)
		{
			if (includeTestCircuits || !isTest(path))
				result.Add(path);
		}
		return result;
	}

	private static bool IsTestCircuit(string path)
	{
		var definition = ResourceLoader.Exists(path) ? GD.Load<TrackDefinition>(path) : null;
		return definition != null && definition.IsTest;
	}
}
