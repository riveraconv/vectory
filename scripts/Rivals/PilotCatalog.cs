using System;
using System.Collections.Generic;

namespace Vectory.Rivals;

// Un piloto de la IA (spec docs/rivales-2-clasificacion.md, 3.1): identificador fijo y apodo en mayúsculas, con su
// perfil y su variación fija (spec docs/rivales-4-personalidades.md, 3.3 y 3.4)
public sealed record RivalPilot(string Id, string Nickname, PilotProfile Profile = PilotProfile.Balanced, PilotVariation Variation = default)
{
	// Los rasgos con los que conduce: su perfil más su variación, dentro de sus rangos
	public PilotTraits Traits => PilotTraits.Effective(PilotProfiles.Traits(Profile), Variation);
}

// Catálogo fijo de los 20 pilotos de la IA. Las variaciones se sortearon una vez con una semilla sacada de su id
// (FNV-1a) y se guardan aquí ya calculadas: no cambian de carrera a carrera y se pueden retocar a mano.
// Lógica pura: sin nodos ni archivos.
public static class PilotCatalog
{
	// Nombre del jugador en la clasificación, de momento fijo
	public const string PlayerName = "PLAYER";

	public static IReadOnlyList<RivalPilot> All { get; } = Array.AsReadOnly(new[]
	{
		new RivalPilot("pilot_01", "RAZOR", PilotProfile.Sprinter, new PilotVariation(-0.007f, 0.008f, -0.006f, -0.022f, 0.05f, 0.09f, -0.06f, 0.08f)),
		new RivalPilot("pilot_02", "EDGE", PilotProfile.CornerSpecialist, new PilotVariation(0.008f, 0.009f, 0.012f, 0f, 0.06f, -0.07f, 0.07f, 0.1f)),
		new RivalPilot("pilot_03", "BUTCHER", PilotProfile.Aggressive, new PilotVariation(0f, 0.005f, 0.006f, 0.003f, 0.08f, -0.07f, -0.08f, 0.04f)),
		new RivalPilot("pilot_04", "VIPER", PilotProfile.Sprinter, new PilotVariation(-0.007f, -0.008f, 0.014f, 0.002f, 0.1f, -0.08f, 0.05f, -0.04f)),
		new RivalPilot("pilot_05", "NOMAD", PilotProfile.Conservative, new PilotVariation(0.002f, 0.008f, 0.014f, 0.004f, 0.07f, 0.08f, 0.08f, -0.08f)),
		new RivalPilot("pilot_06", "HEX", PilotProfile.Technical, new PilotVariation(0.009f, 0.009f, -0.015f, 0.014f, 0.06f, 0.05f, -0.06f, -0.09f)),
		new RivalPilot("pilot_07", "BLITZ", PilotProfile.Sprinter, new PilotVariation(0.007f, 0f, -0.01f, 0.02f, -0.06f, 0.01f, 0.1f, 0.07f)),
		new RivalPilot("pilot_08", "CINDER", PilotProfile.Balanced, new PilotVariation(0.003f, 0.008f, -0.01f, -0.008f, 0.08f, -0.09f, 0.04f, 0.08f)),
		new RivalPilot("pilot_09", "JACKAL", PilotProfile.Balanced, new PilotVariation(0.001f, 0.006f, 0.012f, -0.001f, -0.08f, -0.02f, -0.08f, -0.01f)),
		new RivalPilot("pilot_10", "ONYX", PilotProfile.Balanced, new PilotVariation(0.009f, 0.001f, 0.011f, 0.005f, -0.04f, -0.09f, 0.04f, -0.01f)),
		new RivalPilot("pilot_11", "RIPTIDE", PilotProfile.CornerSpecialist, new PilotVariation(-0.001f, -0.01f, -0.001f, 0.009f, 0.04f, 0.09f, -0.05f, -0.02f)),
		new RivalPilot("pilot_12", "WRAITH", PilotProfile.Technical, new PilotVariation(0f, -0.004f, -0.014f, -0.028f, 0.04f, 0f, 0.1f, -0.08f)),
		new RivalPilot("pilot_13", "KESTREL", PilotProfile.CornerSpecialist, new PilotVariation(0.001f, 0.007f, -0.009f, -0.02f, -0.01f, 0.01f, 0.04f, 0.05f)),
		new RivalPilot("pilot_14", "SPARK", PilotProfile.Sprinter, new PilotVariation(-0.002f, -0.007f, -0.009f, 0.002f, 0.07f, -0.05f, -0.08f, -0.09f)),
		new RivalPilot("pilot_15", "TITAN", PilotProfile.Balanced, new PilotVariation(-0.005f, -0.002f, 0.009f, 0.003f, -0.09f, 0f, 0.08f, -0.08f)),
		new RivalPilot("pilot_16", "VANDAL", PilotProfile.Aggressive, new PilotVariation(0.001f, 0.002f, 0.011f, 0.006f, 0.08f, 0.09f, -0.01f, 0.05f)),
		new RivalPilot("pilot_17", "ORBIT", PilotProfile.Conservative, new PilotVariation(0.002f, 0.003f, -0.007f, -0.029f, -0.04f, 0.02f, 0.06f, -0.04f)),
		new RivalPilot("pilot_18", "FURY", PilotProfile.Aggressive, new PilotVariation(0.007f, -0.006f, -0.016f, 0.023f, -0.08f, -0.08f, -0.06f, -0.07f)),
		new RivalPilot("pilot_19", "SHADE", PilotProfile.Technical, new PilotVariation(0.005f, 0.007f, -0.005f, -0.019f, 0.02f, 0.05f, 0f, 0.05f)),
		new RivalPilot("pilot_20", "ROOK", PilotProfile.Conservative, new PilotVariation(0.01f, 0.003f, -0.003f, -0.009f, 0.06f, -0.07f, -0.05f, 0.06f))
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
