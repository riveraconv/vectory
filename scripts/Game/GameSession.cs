namespace Vectory.Game;

// Lo que el menú decide antes de cargar la escena de carrera y la escena lee al arrancar:
// el modo y el circuito. Es el único sitio por el que pasan de una escena a otra; sin lógica de juego.
// Si la escena de carrera se ejecuta sola desde el editor, el modo es Race y no hay circuito
// (Track usa el de su Inspector o el de por defecto).
public static class GameSession
{
	public static GameMode Mode { get; set; } = GameMode.Race;

	// Ruta del TrackDefinition elegido (p. ej. "res://circuits/circuit_01.tres"); null = ninguno
	public static string CircuitPath { get; set; }
}
