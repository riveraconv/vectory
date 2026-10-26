using Godot;

namespace Vectory.Core;

// Paleta de colores de VECTORY (documento maestro). ÚNICA fuente de colores del proyecto:
// ningún otro script debe escribir un color a mano; se usa siempre Palette.X.
public static class Palette
{
	// --- Colores base del documento maestro ---
	public static readonly Color Cyan = new Color("36E7FF");        // líneas principales de pista, nave del jugador, HUD general
	public static readonly Color Blue = new Color("245BFF");        // profundidad, elementos secundarios
	public static readonly Color Magenta = new Color("FF43D0");     // rivales y competición
	public static readonly Color Orange = new Color("FF9D32");      // impulso, energía, alertas, meta
	public static readonly Color Background = new Color("05070D");  // fondo
	public static readonly Color BlueGray = new Color("A6B8D8");    // información secundaria del HUD

	// --- Derivados (siempre calculados a partir de los de arriba) ---
	public static readonly Color BlueDim = Blue.Darkened(0.4f);     // líneas tenues (rejilla, transversales); por debajo del umbral de glow

	// --- Roles: qué color usa cada elemento. Para cambiar el aspecto de un elemento, se cambia aquí ---
	public static readonly Color TrackEdge = Blue.Lightened(0.25f); // bordes de la pista: azul algo más claro, distinto del cian de la nave
}
