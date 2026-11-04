using System;
using System.Collections.Generic;

namespace Vectory.Ship;

// Una nave: su perfil técnico (cómo conduce) y su perfil visual (cómo es y cómo se ve).
// Lo común a todas las naves (boost, daños, ajustes de cámara, paleta) no entra aquí. Lógica pura.
public sealed record ShipDefinition(string Id, ShipTuning Technical, ShipVisualProfile Visual);

// Las definiciones por id. Hoy solo tiene definición Vector; las naves sin ella no se pueden elegir (ShipCatalog).
public static class ShipDefinitions
{
	private static readonly Dictionary<string, ShipDefinition> ById = new()
	{
		[VectorShip.Definition.Id] = VectorShip.Definition
	};

	// Ids de los que ya se ha avisado: el aviso de Resolve sale una sola vez por id
	private static readonly HashSet<string> Warned = new();

	public static ShipDefinition Default => ById[ShipCatalog.Default];

	// La definición de una nave, o null si no tiene (sin caer en la de por defecto)
	public static ShipDefinition Find(string id) =>
		id != null && ById.TryGetValue(id, out ShipDefinition definition) ? definition : null;

	public static bool Has(string id) => Find(id) != null;

	// La definición de la nave. Con un id desconocido o sin definición devuelve la de por defecto y lo avisa una vez
	// por id (warn escribe en la consola; aquí no se conoce Godot).
	public static ShipDefinition Resolve(string id, Action<string> warn = null)
	{
		ShipDefinition definition = Find(id);
		if (definition != null)
			return definition;

		if (Warned.Add(id ?? ""))
			warn?.Invoke($"Ship: no definition for '{id}'; using '{ShipCatalog.Default}'.");
		return Default;
	}
}
