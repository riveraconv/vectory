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

	// --- Ampliación de la paleta del documento maestro, SOLO para el delta de TIME TRIAL (spec docs/time-trial.md) ---
	public static readonly Color Green = new Color("3DFF8A");       // delta mejor que el récord
	public static readonly Color Red = new Color("FF4D5E");         // delta peor que el récord

	// --- Derivados (siempre calculados a partir de los de arriba) ---
	public static readonly Color BlueDim = Blue.Darkened(0.4f);     // líneas tenues (rejilla, transversales); por debajo del umbral de glow

	// --- Roles: qué color usa cada elemento. Para cambiar el aspecto de un elemento, se cambia aquí ---
	public static readonly Color TrackEdge = Blue.Lightened(0.25f); // bordes de la pista: azul algo más claro, distinto del cian de la nave
	public static readonly Color Checkpoint = Cyan.Darkened(0.5f);  // checkpoints: cian al 50 % de intensidad, más tenue que la meta
	public static readonly Color BoostCharge = Orange;              // esfera de carga de boost disponible
	public static readonly Color BoostChargeGlow = new Color(Orange, 0.25f); // halo de la carga disponible
	public static readonly Color BoostCooldownFill = new Color(Orange, 0.3f);    // relleno atenuado de la carga en cooldown
	public static readonly Color BoostCooldownBorder = new Color(Orange, 0.55f); // borde atenuado de la carga en cooldown
	public static readonly Color BoostEmpty = Blue;                 // borde de la esfera de carga vacía
	public static readonly Color BoostEmptyFill = new Color(Blue, 0.25f);    // relleno apagado de la carga vacía
	public static readonly Color GridMark = Blue;                   // marcas de la parrilla de salida
	public static readonly Color StartLightOn = Magenta;            // luz del semáforo encendida (competición)
	public static readonly Color StartLightGlow = new Color(Magenta, 0.25f); // halo de la luz encendida
	public static readonly Color StartLightOff = BoostEmpty;        // borde de la luz apagada (como la carga de boost vacía)
	public static readonly Color StartLightOffFill = BoostEmptyFill; // relleno de la luz apagada
	public static readonly Color Countdown = Cyan;                  // número de la cuenta atrás y READY?
	public static readonly Color CountdownGlow = new Color(Cyan, 0.55f);   // halo de la cuenta atrás y READY?
	public static readonly Color Go = Orange;                       // texto GO! de la salida
	public static readonly Color GoGlow = new Color(Orange, 0.55f); // halo del GO!
	public static readonly Color ResultsBorder = Cyan;              // borde de la pantalla de resultados
	public static readonly Color ResultsBackground = new Color(Background, 0.85f); // fondo semitransparente de los resultados

	// --- Menú principal ---
	public static readonly Color MenuTitle = Cyan;                  // VECTORY
	public static readonly Color MenuTitleGlow = new Color(Cyan, 0.55f);    // halo de VECTORY
	public static readonly Color MenuRacing = Orange;               // RACING
	public static readonly Color MenuRacingGlow = new Color(Orange, 0.45f); // halo de RACING
	public static readonly Color MenuSpeedLine = Cyan;              // líneas de velocidad del título (la central usa Blue)
	public static readonly Color MenuSpeedLineDim = new Color(Cyan, 0.6f);  // línea de velocidad tenue
	public static readonly Color MenuSelected = Cyan;               // opción seleccionada y su barra
	public static readonly Color MenuSelectedGlow = new Color(Cyan, 0.55f); // halo de la opción seleccionada
	public static readonly Color MenuIdle = BlueGray;               // opción activa sin seleccionar
	public static readonly Color MenuDisabled = new Color(Blue, 0.55f);     // opción desactivada
	public static readonly Color MenuHint = new Color(BlueGray, 0.6f);      // COMING SOON
	public static readonly Color Version = new Color(BlueGray, 0.7f);       // número de versión
	public static readonly Color MenuGrid = Blue;                   // rejilla de horizonte del fondo
	public static readonly Color MenuHorizon = Cyan;                // línea del horizonte
	public static readonly Color MenuShade = Background;            // sombra izquierda, detrás del texto

	// --- Pausa ---
	public static readonly Color PauseDim = Background;             // atenuado de toda la pantalla (opacidad en el Inspector)
	public static readonly Color PauseBand = Background;            // fondo de la franja (opacidad en el Inspector)
	public static readonly Color PauseLine = Cyan;                  // líneas de arriba y abajo de la franja
	public static readonly Color PauseLineGlow = new Color(Cyan, 0.25f);    // halo de esas líneas
	public static readonly Color PauseTitle = Cyan;                 // PAUSED
	public static readonly Color PauseTitleGlow = new Color(Cyan, 0.5f);    // halo de PAUSED
	public static readonly Color ConfirmTitle = Orange;             // RESTART RACE? / BACK TO MAIN MENU? (alerta)
	public static readonly Color ConfirmTitleGlow = new Color(Orange, 0.45f); // halo del título de confirmación
	public static readonly Color PauseHint = BlueGray;              // ESC · CONTINUE / CURRENT RACE PROGRESS WILL BE LOST
	public static readonly Color MenuSeparator = new Color(Blue, 0.7f);     // separadores (pausa y resultados)

	// --- Pantalla RECORDS ---
	public static readonly Color RecordsHeader = Cyan;              // cabeceras TIME TRIAL / RACE
	public static readonly Color RecordsEmptyTime = new Color(BlueGray, 0.6f);  // --:--.--- sin récord
	public static readonly Color RecordsArrow = Cyan;               // flechas del selector de circuito (activas)

	// --- TIME TRIAL y récords ---
	public static readonly Color DeltaBetter = Green;               // delta: mejor (o igual) que el récord
	public static readonly Color DeltaBetterGlow = new Color(Green, 0.5f);
	public static readonly Color DeltaWorse = Red;                  // delta: peor que el récord
	public static readonly Color DeltaWorseGlow = new Color(Red, 0.5f);
	public static readonly Color NewRecord = Orange;                // NEW RECORD (franja de fin de vuelta y resultados)
	public static readonly Color NewRecordGlow = new Color(Orange, 0.5f);
	public static readonly Color GhostLine = new Color(BlueGray, 0.35f);    // nave fantasma: azul grisáceo translúcido, sin brillo
}
