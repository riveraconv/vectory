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

	// --- Ampliación de la paleta del documento maestro, SOLO para el delta de TIME TRIAL (spec docs/time-trial.md)
	// y el indicador de daño (spec docs/danos.md) ---
	public static readonly Color Green = new Color("3DFF8A");       // delta mejor que el récord; nave sin daño
	public static readonly Color Red = new Color("FF4D5E");         // delta peor que el récord; daño grave y boost bloqueado
	public static readonly Color Yellow = new Color("FFE04A");      // SOLO para el indicador de daño: daño leve

	// Blanco: solo para mezclar en los destellos y para la marca de parrilla del jugador (spec docs/elementos-circuito.md)
	public static readonly Color White = new Color(1f, 1f, 1f);

	// --- Derivados (siempre calculados a partir de los de arriba) ---
	public static readonly Color BlueDim = Blue.Darkened(0.4f);     // líneas tenues (rejilla, transversales); por debajo del umbral de glow

	// --- Roles: qué color usa cada elemento. Para cambiar el aspecto de un elemento, se cambia aquí ---
	public static readonly Color TrackEdge = Cyan;                  // bordes de la pista (spec docs/elementos-circuito.md, 2)
	public static readonly Color BoostCharge = Orange;              // esfera de carga de boost disponible
	public static readonly Color BoostChargeGlow = new Color(Orange, 0.25f); // halo de la carga disponible
	public static readonly Color BoostCooldownFill = new Color(Orange, 0.3f);    // relleno atenuado de la carga en cooldown
	public static readonly Color BoostCooldownBorder = new Color(Orange, 0.55f); // borde atenuado de la carga en cooldown
	public static readonly Color BoostEmpty = Blue;                 // borde de la esfera de carga vacía
	public static readonly Color BoostEmptyFill = new Color(Blue, 0.25f);    // relleno apagado de la carga vacía
	public static readonly Color BoostBlockedFill = new Color(Red, 0.3f);    // relleno de la carga bloqueada por daños (nivel 3)
	public static readonly Color BoostBlockedBorder = new Color(Red, 0.55f); // borde de la carga bloqueada por daños
	public static readonly Color BarrierBand = Cyan;                // bordes exteriores (gruesos) de las franjas de la barrera: base y remate
	public static readonly Color BarrierBandInner = new Color(Cyan, 0.85f); // bordes interiores de las franjas
	public static readonly Color BarrierBandTie = new Color(Cyan, 0.6f);    // travesaños de las franjas
	public static readonly Color BarrierFill = new Color(Blue, 0.5f);   // línea media del relleno de la barrera
	public static readonly Color BarrierPost = new Color(Blue, 0.4f);   // verticales del relleno de la barrera
	public static readonly Color BarrierFlash = Orange;             // destello de impacto en la barrera
	public static readonly Color StartLightOn = Magenta;            // luz del semáforo 2D encendida (StartLightsView; el HUD ya no la usa)
	public static readonly Color StartLightGlow = new Color(Magenta, 0.25f); // halo de la luz encendida
	public static readonly Color StartLightOff = BoostEmpty;        // borde de la luz apagada (como la carga de boost vacía)
	public static readonly Color StartLightOffFill = BoostEmptyFill; // relleno de la luz apagada
	public static readonly Color Countdown = Cyan;                  // número de la cuenta atrás y READY?
	public static readonly Color CountdownGlow = new Color(Cyan, 0.55f);   // halo de la cuenta atrás y READY?
	public static readonly Color Go = Orange;                       // texto GO! de la salida
	public static readonly Color GoGlow = new Color(Orange, 0.55f); // halo del GO!
	public static readonly Color ResultsBorder = Cyan;              // borde de la pantalla de resultados
	public static readonly Color ResultsBackground = new Color(Background, 0.85f); // fondo semitransparente de los resultados

	// --- Elementos del circuito (spec docs/elementos-circuito.md). Brillo y opacidad se aplican multiplicando el color ---
	public static readonly Color TrackRung = Blue;                  // transversales del suelo (opacidad en el Inspector del Track)
	public static readonly Color TrackLane = Blue;                  // carriles discontinuos y línea central del suelo (ídem)
	public static readonly Color FinishGate = Orange;               // pórtico de meta
	public static readonly Color CheckpointGate = Cyan;             // pórticos de checkpoint (brillo en reposo en el Inspector)
	public static readonly Color GateFlashWhite = White;            // hacia dónde va el color en los destellos de meta y checkpoints
	public static readonly Color CheckpointText = Cyan;             // S1 y tiempo del panel del checkpoint
	public static readonly Color SectorBetter = Cyan;               // diferencia de sector que mejora (panel y HUD)
	public static readonly Color SectorWorse = Orange;              // diferencia de sector que empeora (panel y HUD)
	public static readonly Color GridMark = Cyan;                   // marco y número de cada puesto de parrilla
	public static readonly Color GridMarkPlayer = White;            // marco y número del puesto del jugador
	public static readonly Color StartPanel = Cyan;                 // marco del semáforo de la meta (al 55 %)
	public static readonly Color StartLamp = Orange;                // luces del semáforo de la meta (apagada al 50 %)
	public static readonly Color StartLampLit = Orange.Lerp(White, 0.15f);   // luz encendida: naranja con un toque de blanco
	public static readonly Color StartLampGo = Cyan;                // destello de salida
	public static readonly Color TunnelRing = Blue;                 // anillos y celosía del túnel
	public static readonly Color TunnelRail = Cyan;                 // largueros del túnel
	public static readonly Color TunnelPulse = Cyan.Lerp(White, 0.6f);      // casi blanco cian del pulso
	public static readonly Color CurveChevron = Orange;             // chevrons de los avisos de curva
	public static readonly Color CrestBeacon = Orange;              // balizas ^ de cresta
	public static readonly Color CrestBarrier = Orange;             // tramo de barrera iluminada de la cresta

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

	// --- SELECT CIRCUIT ---
	public static readonly Color MiniatureEdge = Cyan;              // bordes de pista de la miniatura
	public static readonly Color MiniatureCenter = Blue;            // línea central discontinua de la miniatura
	public static readonly Color MiniatureFinish = Orange;          // línea de meta de la miniatura
	public static readonly Color MiniatureGrid = Blue.Darkened(0.72f);      // rejilla bajo la miniatura
	public static readonly Color SelectName = Cyan;                 // nombre del circuito
	public static readonly Color SelectNameGlow = new Color(Cyan, 0.5f);    // halo del nombre
	public static readonly Color SelectHeader = Cyan;               // RACE · SELECT CIRCUIT y cabeceras del panel
	public static readonly Color SelectLabel = BlueGray;            // descripción, contador y etiquetas de datos
	public static readonly Color SelectValue = Cyan;                // valores de datos y récords
	public static readonly Color SelectDataBar = Blue;              // línea vertical a la izquierda de cada dato
	public static readonly Color SelectTestTag = Orange;            // etiqueta TEST
	public static readonly Color SelectTestTagBorder = new Color(Orange, 0.7f);
	public static readonly Color SelectNavBorder = Blue;            // borde de los botones ‹ ›
	public static readonly Color SelectArrow = Cyan;                // flechas ‹ › con más de un circuito
	public static readonly Color SelectArrowDisabled = new Color(Blue, 0.4f);   // flechas con un solo circuito
	public static readonly Color SelectPanel = new Color(Background, 0.92f);    // fondo del panel de telemetría
	public static readonly Color SelectPanelLine = Cyan;            // línea izquierda del panel
	public static readonly Color SelectPanelGlow = new Color(Cyan, 0.18f);      // halo de esa línea, hacia la izquierda
	public static readonly Color SelectFooter = new Color(BlueGray, 0.7f);      // ← → CIRCUIT · ↑ ↓ OPTION · ENTER SELECT
	public static readonly Color ProfileLine = Cyan;                // perfil de elevación
	public static readonly Color ProfileGuide = new Color(Blue, 0.45f);         // líneas guía del perfil (+E / −E)
	public static readonly Color ProfileSector = new Color(Blue, 0.7f);         // líneas de sector del perfil

	// --- SELECT SHIP ---
	public static readonly Color ShipNameDisabled = new Color(Blue, 0.75f);     // nombre de una nave COMING SOON
	public static readonly Color ShipTag = BlueGray;                // etiqueta COMING SOON
	public static readonly Color ShipTagBorder = new Color(BlueGray, 0.5f);
	public static readonly Color ShipValueEmpty = new Color(BlueGray, 0.6f);    // — de una nave COMING SOON
	public static readonly Color ShipDamageOff = BlueGray;          // DAMAGE OFF en TIME TRIAL
	public static readonly Color ShipBoost = Orange;                // valor de BOOST
	public static readonly Color ShipBarOn = Cyan;                  // segmento encendido
	public static readonly Color ShipBarGlow = new Color(Cyan, 0.3f);           // halo del segmento encendido
	public static readonly Color ShipBarOff = new Color(Blue, 0.7f);            // borde del segmento apagado
	public static readonly Color ShipBarEmpty = new Color(Blue, 0.35f);         // borde del segmento de una nave COMING SOON
	public static readonly Color HologramRing = new Color(Blue, 0.45f);         // anillo de proyección
	public static readonly Color HologramRingInner = new Color(Cyan, 0.3f);     // anillo interior discontinuo
	public static readonly Color ShipPlaceholderFrame = Blue;       // marco del placeholder
	public static readonly Color ShipPlaceholderTitle = new Color(BlueGray, 0.75f);  // SHIP IN DEVELOPMENT
	public static readonly Color ShipPlaceholderHint = new Color(BlueGray, 0.5f);    // COMING SOON del placeholder
	public static readonly Color DragCursorFill = Cyan;             // cursor de mano del arrastre
	public static readonly Color DragCursorOutline = Background;

	// --- TIME TRIAL y récords ---
	public static readonly Color DeltaBetter = Green;               // delta: mejor (o igual) que el récord
	public static readonly Color DeltaBetterGlow = new Color(Green, 0.5f);
	public static readonly Color DeltaWorse = Red;                  // delta: peor que el récord
	public static readonly Color DeltaWorseGlow = new Color(Red, 0.5f);
	public static readonly Color NewRecord = Orange;                // NEW RECORD (franja de fin de vuelta y resultados)
	public static readonly Color NewRecordGlow = new Color(Orange, 0.5f);
	public static readonly Color GhostLine = new Color(BlueGray, 0.35f);    // nave fantasma: azul grisáceo translúcido, sin brillo

	// --- Daños (indicador de tres segmentos del HUD) ---
	public static readonly Color DamageNone = Green;                // nivel 0
	public static readonly Color DamageLight = Yellow;              // nivel 1
	public static readonly Color DamageHeavy = Red;                 // niveles 2 y 3
	public static readonly Color DamageLightGlow = new Color(Yellow, 0.5f); // halo de los segmentos encendidos
	public static readonly Color DamageHeavyGlow = new Color(Red, 0.5f);
	public static readonly Color DamageNoneOff = new Color(Green, 0.6f);    // borde de un segmento apagado, según el nivel
	public static readonly Color DamageLightOff = new Color(Yellow, 0.6f);
	public static readonly Color DamageHeavyOff = new Color(Red, 0.6f);

	// --- Cabinas (spec docs/naves-3b-cabinas.md): tonos de sus paneles sólidos, tal como están en los diseños ---
	public static readonly Color CockpitBlack = new Color("000000");            // sombras y oclusión
	public static readonly Color CockpitHighlight = new Color("BFF6FF");        // brillo en cantos
	public static readonly Color CockpitGlassReflection = new Color("DFFBFF");  // reflejos del cristal
	public static readonly Color CockpitPanelLight = new Color("16213A");
	public static readonly Color CockpitPanelMid = new Color("0C1324");
	public static readonly Color CockpitPanelDark = new Color("060A14");
	public static readonly Color CockpitConsoleLight = new Color("1A2744");
	public static readonly Color CockpitConsoleDark = new Color("0A1020");
	public static readonly Color CockpitFrontLight = new Color("070B16");
	public static readonly Color CockpitFrontDark = new Color("03050A");
	public static readonly Color CockpitPillarLight = new Color("121B30");
	public static readonly Color CockpitNoseLight = new Color("101A30");
	public static readonly Color CockpitCowlLight = new Color("1C2A48");
	public static readonly Color CockpitStep = new Color("05080F");             // canto de los escalones del morro
	public static readonly Color CockpitSink = new Color("02030A");             // huecos del frontal
	public static readonly Color CockpitSinkWall = new Color("0D1424");
	public static readonly Color CockpitScreen = new Color("02040A");           // fondo de las pantallas
	public static readonly Color CockpitVent = new Color("04070F");             // rejillas
	public static readonly Color CockpitScrew = new Color("0B1222");            // tornillos e interruptores
}
