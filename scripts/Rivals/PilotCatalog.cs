using System;
using System.Collections.Generic;

namespace Vectory.Rivals;

// Un piloto de la IA (spec docs/rivales-2-clasificacion.md, 3.1): identificador fijo y apodo en mayúsculas.
// Aquí irá su personalidad en la spec 4.
public sealed record RivalPilot(string Id, string Nickname);

// Catálogo fijo de los 20 pilotos de la IA. Lógica pura: sin nodos ni archivos.
public static class PilotCatalog
{
	// Nombre del jugador en la clasificación, de momento fijo
	public const string PlayerName = "PLAYER";

	public static IReadOnlyList<RivalPilot> All { get; } = Array.AsReadOnly(new[]
	{
		new RivalPilot("pilot_01", "RAZOR"),
		new RivalPilot("pilot_02", "EDGE"),
		new RivalPilot("pilot_03", "BUTCHER"),
		new RivalPilot("pilot_04", "VIPER"),
		new RivalPilot("pilot_05", "NOMAD"),
		new RivalPilot("pilot_06", "HEX"),
		new RivalPilot("pilot_07", "BLITZ"),
		new RivalPilot("pilot_08", "CINDER"),
		new RivalPilot("pilot_09", "JACKAL"),
		new RivalPilot("pilot_10", "ONYX"),
		new RivalPilot("pilot_11", "RIPTIDE"),
		new RivalPilot("pilot_12", "WRAITH"),
		new RivalPilot("pilot_13", "KESTREL"),
		new RivalPilot("pilot_14", "SPARK"),
		new RivalPilot("pilot_15", "TITAN"),
		new RivalPilot("pilot_16", "VANDAL"),
		new RivalPilot("pilot_17", "ORBIT"),
		new RivalPilot("pilot_18", "FURY"),
		new RivalPilot("pilot_19", "SHADE"),
		new RivalPilot("pilot_20", "ROOK")
	});

	// count pilotos distintos al azar (como mucho, todos), con el generador que se le pasa
	public static IReadOnlyList<RivalPilot> Draw(Random rng, int count)
	{
		ArgumentNullException.ThrowIfNull(rng);
		var pool = new List<RivalPilot>(All);
		int total = Math.Clamp(count, 0, pool.Count);
		// Fisher–Yates parcial: los primeros total quedan sorteados
		for (int i = 0; i < total; i++)
		{
			int j = i + rng.Next(pool.Count - i);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}
		return pool.GetRange(0, total);
	}
}
