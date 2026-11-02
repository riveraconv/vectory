namespace Vectory.Ship;

// Cómo se entra en derrape (spec docs/conduccion-avanzada.md, 3). Se elige en el Inspector del Player.
public enum DriftMode
{
	BrakeTurn,     // A: frenar (S) + girar a partir de cierta velocidad
	DedicatedKey   // B: tecla de derrape (acción "drift") + girar; el boost pasa a otra tecla
}

// Sitio único que decide qué acción del Input Map es el boost y cuál el derrape según el modo.
// Las teclas de cada acción se cambian en Project Settings > Input Map sin tocar código.
// Lógica pura: solo devuelve nombres de acciones.
public static class DriftKeyMap
{
	public const string Boost = "boost";                    // Espacio
	public const string Drift = "drift";                    // Espacio
	public const string BoostDedicatedKey = "boost_mode_b"; // Shift

	// Acción que gasta una carga de boost
	public static string BoostAction(DriftMode mode)
	{
		return mode == DriftMode.DedicatedKey ? BoostDedicatedKey : Boost;
	}

	// Acción que hay que mantener para derrapar (null en el modo A: ahí se derrapa con el freno)
	public static string DriftAction(DriftMode mode)
	{
		return mode == DriftMode.DedicatedKey ? Drift : null;
	}
}
