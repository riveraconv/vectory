# VECTORY · Historial de implementación

Lo hecho etapa por etapa, con las decisiones que se apartan de las specs, lo medido y las trampas ya resueltas. Antes de tocar un sistema, lee su apartado (busca el nombre de su spec, por ejemplo `efectos-1-choques`). Si algo de aquí contradice una spec, manda lo de aquí: son decisiones validadas por el usuario. Dentro de un apartado, lo más nuevo sustituye a lo anterior cuando lo dice ("Sustituye a lo que las secciones anteriores dicen…").

Al cerrar una etapa, se añade su apartado al final, corto: qué se hizo, en qué se aparta de la spec, lo medido y las trampas nuevas. No repitas lo que ya dice la spec.

## Base

- Hecho: nave wireframe, control arcade con derrape e inclinación, cámara de cabina y de tercera persona, circuito cerrado generado por código a partir de un `TrackDefinition`, barreras con colisiones, cabina minimalista, meta y checkpoints (cantidad en el `.tres` del circuito, 3 en `circuit_01`), vueltas, cronómetro y tiempos por sector.
- HUD hecho (posición, vuelta, tiempo, mejor vuelta, velocidad con su barra y cargas de boost), con textos en inglés (`POSITION`, `LAP`, `BEST`). Los mensajes de carrera en consola también van en inglés (`[Race] ...`). Los comentarios del código siguen en español.

## Boost por cargas

(spec `docs/boost.md` v1.1): hecho y con commit. Shift (acción de input `boost`, definida en `project.godot`; era Espacio hasta la conducción avanzada) gasta una carga: 0 al empezar, 1 cada 35 s, máximo 3. Cada carga suma +100 km/h en 0,5 s (subida), los mantiene 2 s (mantenimiento) y después pierde el exceso sobre la punta a 20 km/h por segundo. Cooldown de 10 s. Frenar corta el impulso y la carga no vuelve. En el HUD, 3 esferas encima de la velocidad: naranja con halo (disponible), naranja atenuado (en cooldown), azul (vacía). Parámetros en el grupo **Boost** del Inspector del Player (velocidades en km/h).

- El boost provisional antiguo (`BoostMaxSpeed`, `BoostAcceleration`) y la barra naranja de "BOOST" del HUD se quitaron. La barra de velocidad llega a su tope en punta + impulso (`Player.BoostedTopSpeed`).

## Ciclo de carrera

(spec `docs/carrera.md` v1.0): hecho, con tests de la lógica pura y validado por el usuario.

- Estados `PreStart` → `Running` → `Finished` (`RaceState`, en `RaceTracker`).
- Parrilla de 8 puestos detrás de la meta (impares a la izquierda y pares a la derecha y más atrás); desde los elementos del circuito, cada puesto se marca con un marco de luz y su número (ver abajo). El jugador sale de una posición aleatoria (`GridLayout.RandomOrder` con `Random` inyectado). Parámetros en el grupo **Parrilla** del Inspector del Track.
- Semáforo de 5 luces (desde los elementos del circuito, en la meta y en naranja) y cuenta atrás 5 → 1; a los 6 s, GO (visible 1 s). En la previa no hay input ni recarga del boost; el cronómetro y la recarga arrancan en el GO. Parámetros en el grupo **Salida** del Inspector de Main.
- El primer cruce de meta tras el GO no cierra vuelta; la vuelta 1 cuenta desde el GO.
- Al completar la última vuelta: cronómetro parado, sin control, boost cancelado, la nave se para sola. A los 1,5 s (grupo **Resultados** de Main), pantalla de resultados: posición (`1st / 8`), mejor vuelta con su número y mejor tiempo de cada sector con su vuelta (ver **Pausa y resultados** abajo).
- Textos de resultados en inglés (`RACE FINISHED`, `BEST LAP`, `BEST SECTORS`), como el resto del HUD, aunque la spec los da en español. El HUD muestra `POSITION 1/8`.

## Menú principal

(spec `docs/menu-principal.md` v1.2, rama `feature/menus`): hecho, con tests de la lógica pura y validado por el usuario.

- El juego arranca en el menú. Opciones RACE, CHAMPIONSHIP, TIME TRIAL, HANGAR, RECORDS, SETTINGS y QUIT: solo RACE (carga la carrera) y QUIT (cierra el juego) están activas; el resto, atenuadas con `COMING SOON`. Las opciones son una lista de `MenuOption` en `MainMenu`: activar una es cambiar `Enabled` y añadir su caso en `MainMenu.Execute`.
- Teclado: W/S o flechas (`menu_up` / `menu_down`), Enter o Espacio (`menu_accept`); Esc no hace nada. Ratón: hover selecciona y clic elige, solo en opciones activas.
- Medidas de la spec sobre 1280×720, multiplicadas por la altura base del proyecto / 720 (1080 → ×1,5). Parámetros en el Inspector de `MainMenu` (tamaños) y `MenuBackground` (cámara, rejilla, sombra).
- Fondo: la cámara mira de frente a la nave y el encuadre se desplaza con proyección frustum (`ShipScreenX` = 0,734), así la nave queda a la derecha sin girarse y el horizonte sale recto. El suelo está a la altura de la punta del morro (`FloorY` = 0, `HorizonZ` = -4).
- Versión leída de `application/config/version` (0.1.0) y mostrada como `v0.1.0`.
- Los halos del texto son sombras sin desplazamiento (`LabelSettings.Shadow*`): más suaves que el brillo del lienzo de diseño.
- Godot puede reescribir `run/main_scene` de `project.godot` como `uid://` al abrir el editor: es normal.

## Pausa y resultados

(spec `docs/pausa-y-resultados.md` v1.1, rama `feature/pause`): hecho, con tests de `PauseLogic` y validado por el usuario.

- ESC (acción `pause`) abre la pausa en la previa, en carrera y con los resultados. Congela nave, cronómetro, semáforo, recarga del boost y el retardo de los resultados. Opciones CONTINUE / RESTART / BACK TO MAIN MENU; RESTART y BACK piden confirmación (NO / YES, NO por defecto) salvo con la carrera terminada. ESC en la pausa = CONTINUE; en la confirmación = NO (vuelve con la opción de origen seleccionada).
- Al continuar, `Player.BlockInputUntilReleased()`: la nave ignora conducción, derrape y boost hasta que se sueltan todas sus teclas (lo pulsado en la pausa no llega a la nave).
- Aspecto: `MenuBand` de 260 px con PAUSED / `ESC · CONTINUE` a la izquierda, separador y opciones como el menú principal. El título de confirmación se reduce solo si no cabe antes del separador (`ConfirmTitleFontSize` es el máximo). Parámetros en el nodo `PauseMenu` de `Main.tscn`.
- Resultados: `MenuBand` de 340 px (`ResultsBandHeight` en Main) con tres columnas: RACE FINISHED + posición; BEST LAP + sectores (columnas alineadas); RESTART RACE / BACK TO MAIN MENU (RESTART seleccionada al aparecer). Las opciones se reducen solas si no caben antes del borde derecho (`ResultsOptionFontSize` = 24 es el máximo; con Orbitron quedan en 23). Si los sectores no caben, la franja crece centrada.
- Teclado de resultados en `Main` (con `menu_*`); el clic llega por `Hud.ResultsOptionChosen`. Con la pausa abierta, Main está congelado y la lista de resultados no responde. La acción `restart_race` ya no existe.
- Los valores de conducción de VECTOR viven en `Ship/VectorTuning` (`MaxSpeed = 110` m/s, `Drag = 5`, `TurnRate = 1.3`, `SteerResponse = 8`, ajustados por el usuario). Ya no están en el Inspector ni en `Main.tscn`: para tocar uno se cambia ahí, y Blade y Hammer lo arrastran por sus multiplicadores. Para afinar por nave está el panel de ajuste, no el Inspector.
- Decisiones del usuario que se salen del documento maestro y se mantienen: circuito cerrado, dos cámaras, vueltas y cronómetro dentro de la demo, boost por cargas que se recargan solo por tiempo (sin recarga por conducción limpia ni derrapes), 8 participantes por carrera, sin ventaja por buena reacción en la salida, derrape con tecla dedicada (Espacio) y boost en Shift.

## TIME TRIAL y récords

(spec `docs/time-trial.md` v1.3, rama `feature/timetrial`): hecho, con tests de la lógica pura y validado por el usuario.

- TIME TRIAL activa en el menú. Salida lanzada: la nave aparece quieta y centrada a 150 m antes de la meta con `READY?` (5 s, sin control; ESC y C sí funcionan; la pausa congela la espera). En el `GO!` (1 s visible) `Player.Launch` la pone de golpe al 60 % de `MaxSpeed` y se toma el control; la recarga del boost empieza ahí. Sin parrilla ni semáforo (`RaceSession.ForTimeTrial` + `ReadySequence`). RESTART vuelve a `READY?`. Parámetros en el grupo **Time Trial** de `Main` (`ReadyDuration`, `ReadyGoDuration`, distancia y velocidad de salida…).
- Texto de salida (RACE y TIME TRIAL): Orbitron Black con halo, 56 px sobre 1280×720 (`StartTextFontSize` en el grupo **Salida** de `Main`). Números y `READY?` en cian, `GO!` en naranja; en RACE el GO también es `GO!`. Las luces del semáforo no cambian. El cronómetro arranca al cruzar la meta hacia delante (antes, `OUT LAP` a 0). Vueltas libres: `RaceTracker.UnlimitedLaps` (= -1; 0 sigue siendo inválido), nunca `Finished` ni resultados; se sale con la pausa (RESTART y BACK siempre con confirmación).
- HUD en TIME TRIAL: `TIME TRIAL` + nombre del circuito arriba a la izquierda; `OUT LAP` / `LAP N`; `BEST` = récord de TIME TRIAL del circuito. `TimeTrialView`: delta por sector (`SECTOR N` + delta verde/rojo, 2,5 s) contra los tiempos de paso del récord, y franja de fin de vuelta (`MenuBand` de 120 px arriba, sin atenuar ni pausar) con `LAP N` + tiempo y `NEW RECORD` (detalle `-0.482 · PREVIOUS …` o `FIRST RECORD`) o el delta de la vuelta + `BEST …`. Se congelan con la pausa. Parámetros en el grupo **Time Trial** de `Main` (se pasan al HUD).
- Récords en `user://records.json` (en Windows, `%APPDATA%\Godot\app_userdata\VECTORY\`), por `Id` de circuito y modo, independientes: TIME TRIAL = mejor vuelta + tiempos de paso (se guarda al batirla, en ese momento); RACE = mejor tiempo total y mejor vuelta (al terminar la carrera; abandonar no guarda). Con otro número de checkpoints se conserva el tiempo pero no hay delta hasta un récord nuevo. Archivo corrupto o de otra versión → aviso en consola, `records.json.bak` y se empieza sin récords.
- Resultados de RACE: `TOTAL TIME` encima de `BEST LAP` (`RaceResult.TotalTime`). `NEW RECORD` va en la línea de la etiqueta (`TOTAL TIME  NEW RECORD`), no a la derecha del tiempo: junto a la mejor vuelta no cabía y pisaba las opciones.
- Para probar sin tocar los récords del usuario: apartar `records.json` (y la carpeta `ghosts` y `settings.json`, si existen) antes y restaurarlos (o borrar los creados) después. **Hazlo siempre antes de una prueba automática que pueda completar una vuelta, una carrera o cambiar GHOST**: el usuario ya tiene récords reales.
- NEW RECORD en resultados va en la línea de la etiqueta aunque la spec (7.2) diga "a la derecha del tiempo": decisión validada por el usuario.

## Pantalla RECORDS

(spec `docs/records.md` v1.1, rama `feature/records`): hecho, con tests de la lógica pura y validado por el usuario.

- RECORDS activa en el menú. Se abre dentro de la escena del menú (sin cambiar de escena): `MainMenu` oculta su título y opciones (`_content`) y deja de procesar input mientras está abierta; la versión sigue visible. Al volver, RECORDS queda seleccionada.
- Franja de 340 px (`MenuBand`) sobre el fondo del menú atenuado al 50 %, separadores en x = 450 y 915: `RECORDS` + selector `‹ CIRCUIT 01 ›` + `1 / 1` (flechas desactivadas con un circuito); TIME TRIAL (BEST LAP) y RACE (TOTAL TIME, BEST LAP) con tiempo y fecha `29 OCT 2026`, o `--:--.---` / `NO RECORD`; BACK / RESET RECORDS (opciones reducidas solas si no caben).
- RESET RECORDS → `RESET` / `RECORDS?` en naranja + `ALL CIRCUITS AND MODES` / `CANNOT BE UNDONE`, NO / YES (NO por defecto). YES guarda un archivo vacío con su versión (no se borra a mano).
- Input: W/S y flechas arriba/abajo, Enter/Espacio, `menu_left` / `menu_right` (A/D y flechas) para el circuito, `menu_back` (Escape) = BACK o NO. Ratón: hover, clic y flechas. El Enter que abre la pantalla no elige nada en ella (se ignora ese frame).
- Los récords se leen al abrir; los de circuitos que ya no están en `CircuitCatalog` no se muestran pero siguen en el archivo (y RESET los borra).

## Nave fantasma en TIME TRIAL

(spec `docs/ghost.md` v1.0, rama `feature/ghost`): hecha, con tests de la lógica pura y validada por el usuario.

- El fantasma es la vuelta récord de TIME TRIAL del circuito (la del `BEST` y el delta), con la nave que la hizo (`ship_01`). Aparece en la meta al empezar cada vuelta y sigue el tiempo de vuelta actual (así la pausa no lo desincroniza); en la `OUT LAP` no se ve y, si su vuelta termina antes, desaparece hasta la siguiente. Sin colisión ni efectos; se desvanece de 20 m a 6 m de la nave. Parámetros: opacidad y distancias en el nodo `GhostShip` de `Main.tscn`; `GhostSampleRate` (20) en el grupo **Fantasma** de `Main`.
- Cada vuelta se graba siempre (también con GHOST en OFF), con `RaceSession.LapStarted` (se lanza en la salida y tras cada `LapCompleted`). Si es récord, la grabación se guarda en ese momento como fantasma, a la vez que el récord. `Main` procesa la física con `ProcessPhysicsPriority = 1` para muestrear la nave después de que se mueva.
- Archivos: `user://ghosts/<Id>.json` (uno por circuito, separado de `records.json`) y `user://settings.json` (`ghostEnabled`, ON por defecto). El fantasma solo se usa si coinciden Id, checkpoints, firma del trazado y el tiempo del récord (±1 ms) y tiene al menos 2 muestras; si no, no se borra, se sobrescribe con el siguiente récord. Los récords anteriores a esta etapa no tienen fantasma hasta batirlos.
- Pausa de TIME TRIAL: CONTINUE, `GHOST: ON / OFF`, SETTINGS (desde SETTINGS etapa 4), RESTART, BACK TO MAIN MENU (franja de 320 px y separador de 252 px). Elegir GHOST cambia el ajuste, lo guarda y la pausa sigue abierta; sin fantasma válido sale desactivada (sin `COMING SOON`), evaluada cada vez que se abre. La pausa de RACE no cambia.
- RESET RECORDS (pantalla RECORDS) borra también todos los fantasmas; el ajuste GHOST no se toca.

## Barreras y colisiones

(spec `docs/barreras-y-colisiones.md` v1.1, rama `feature/barrier-colision`): hechas, con tests de la lógica pura y validadas por el usuario. Sustituyen a los límites laterales (`TrackLimits` ya no existe).

- Barrera a cada lado, 0,5 m por fuera del borde, 3 m de alto: franja cian abajo (0–0,4 m) y arriba (2,4–3 m) con base y remate gruesos (dos líneas a 4 cm), línea media azul a 1,4 m, travesaños y verticales cada ~4 m (ajustadas a la vuelta). Malla de líneas aparte (`BarrierMesh`) con un shader: las líneas llevan en UV la distancia sobre el eje y el lado, y el destello se pinta con uniforms, sin rehacer la malla. Colores `Palette.Barrier*`. Parámetros en los grupos **Barrera** y **Destello de la barrera** del Track.
- Colisión sin físicas: contacto si |lateral| + semiancho ≥ desplazamiento de la barrera; la nave vuelve justo al límite. Semiancho **3,4 m** (puntas de las alas), aunque la spec dice 1,9: decisión del usuario. Niveles por velocidad perpendicular: roce (< 30 km/h: se desliza, −4 % y 30 % de enderezado solo al empezar el contacto, luego 20 km/h/s), medio (rebote 0,4, −20 %, dirección girada hacia dentro la mitad del ángulo) y fuerte (≥ 90 km/h: rebote 0,5, −45 % y 1 s de giro y aceleración del 50 % al 100 %). En medio y fuerte la pérdida se aplica a la velocidad a lo largo de la barrera; la perpendicular rebota con su restitución. 0,3 s de espera por lado entre impactos (dentro de la espera cuenta como roce). No cancela el boost. Parámetros en los grupos **Colisiones** y **Sacudida de cámara** del Player (velocidades en km/h).
- Destello naranja por lado (roce 3 m / 40 % mientras roza y 0,2 s de apagado, sigue a la nave; medio 7 m / 0,4 s; fuerte 10 m / 0,6 s). Un roce no tapa un destello más fuerte hasta que este baja de su intensidad. Sacudida solo en medio y fuerte, al 50 % en cabina; uno más fuerte sustituye al que haya.
- Recolocación: tras 3 s contra la barrera a menos de 10 km/h, o al momento si la nave pasa 2 m más allá de la barrera. Solo con control (`CanDrive` y no `Finished`). En el punto del eje más cercano, centrada, mirando hacia delante y parada; no toca vueltas, boost ni la grabación del fantasma. Parámetros en el grupo **Recolocación** de `Main`.
- `Main` (en `_PhysicsProcess`, después del Player) conecta destello, sacudida y recolocación con `Player.LastBarrierTouch`. El fantasma no colisiona.

## Daños

(spec `docs/danos.md` v1.1, rama `feature/damage-system`): hechos, con tests de la lógica pura y validados por el usuario.

- Solo en RACE (en TIME TRIAL ni puntos, ni indicador, ni parpadeo). Impacto medio +1, fuerte +3, roce 0; nivel 1/2/3 desde 3/6/9 puntos. Solo suman con la carrera en `Running` (no en la previa ni terminada); la recolocación no suma ni repara. No baja en la carrera; RESTART empieza en 0.
- Nivel 2: punta ×0,92. Nivel 3: punta ×0,85, giro ×0,85 y boost bloqueado (las cargas se siguen ganando; un impulso en curso termina normal). `Player` recibe `TopSpeedFactor`, `TurnRateFactor` y `BoostAllowed` (no conoce el daño) y los aplica al montar `ShipTuning`; el exceso sobre la nueva punta se pierde con la regla del boost.
- Parpadeo: ~10/20/35 % de las aristas de la nave y la cabina, cada 0,5–1 s en los tres niveles, 6–15 s al 10 % (valores ajustados por el usuario); se mantienen al subir de nivel; la pausa lo congela. El material de parpadeo solo se pone con daño (menú y fantasma no cambian). La "opacidad" se aplica **multiplicando el color**, no con `ALPHA`: con `ALPHA` el material pasa a transparentes y toda la nave pierde glow.
- HUD: tres segmentos (22×8 px y separación 4 sobre 1280×720, ×1,5) a 24 px a la izquierda de las esferas: borde verde (0), uno amarillo (1), dos rojos (2), tres rojos parpadeando a 2 Hz (3). Esferas con carga en rojo atenuado con nivel 3.
- Parámetros: grupo **Daños** de `Main` (puntos, niveles, factores y medidas del indicador) y **Daños (parpadeo)** del Player. `Main` conecta todo con `LastBarrierTouch` y `LevelChanged`; consola `[Race] Damage level N (P points)`.

## Conducción avanzada: derrape e inclinación

(spec `docs/conduccion-avanzada.md` v1.2, rama `feature/advanced-driving`): hecha, con tests de la lógica pura (`DriftTests`) y validada por el usuario (también el arreglo de la v1.2). Solo física e inclinación de la nave; la cámara va en otra spec.

- Se probaron dos modos de entrada y se quedó el de tecla dedicada: **Espacio** (acción `drift`) + A/D a 120 km/h o más entra en derrape; se sale al soltar Espacio o A/D o bajo 60 km/h. Espacio solo no hace nada y no frena; S frena siempre al 100 % (y corta el boost, como siempre). El boost pasó a **Shift**. El modo A (S + giro) se quitó.
- Modelo: el mismo de antes (la velocidad real se acerca a morro × velocidad del motor con `LateralGrip`) multiplicando el agarre por `GripFactor`: 0 % en derrape, 50 % contravolanteando (girar hacia el lado al que desliza), y al salir vuelve al 100 % de forma lineal en 3 s. En derrape: giro ×1,4 (se multiplica con daño y corrección de impacto), aceleración 50 %, pérdida 5 km/h/s + hasta 10 km/h/s según el ángulo, ángulo máximo 60°. El tope del ángulo solo se aplica en derrape y en la recuperación del agarre (así la conducción normal y los rebotes no cambian). Sin tiempo máximo ni ayudas.
- Como con poco agarre la velocidad real apenas sigue al motor, en derrape y en la recuperación del agarre el cambio de la velocidad del motor de cada paso (W, S, boost, pérdidas) se suma también a la velocidad real hacia el morro (`PushAlongNose`), y en derrape el motor no supera la velocidad real (lo perdido, también con el tope del ángulo, no se recupera solo al salir). Antes de este arreglo, con agarre 0, S y el boost no hacían nada hasta salir del derrape. Mantener el derrape girando hasta el tope de 60° frena mucho la nave.
- Un impacto medio o fuerte termina el derrape y no se vuelve a entrar hasta soltar Espacio o A/D; el roce no lo corta. Recolocación, `PlaceAt` y `Launch` lo dejan todo a cero (agarre 100 %). Sin control (previa, terminada) no se derrapa. El boost suma en la dirección del morro.
- Inclinación (`ShipBank`): objetivo = máximo × giro suavizado × (velocidad real / punta, con tope en 1); 25° normal, 20° en derrape; suavizado de 0,15 s. Solo la geometría exterior (`Spaceship`); la cabina no hereda esta inclinación (la suya, más sutil, es de la spec de cámara; `MaxBank` ya no existe). El fantasma graba la inclinación sin cambiar el formato.
- Parámetros en los grupos **Derrape** e **Inclinación** del Player (velocidades en km/h, ángulos en grados).

## Cámara: efectos de la conducción avanzada

(spec `docs/camara.md` v1.1, rama `feature/camera`): hechos, con tests de la lógica pura (`CameraEffectsTests`) y ajustados por el usuario en pista. Solo visuales: no cambian giro, física ni control.

- Inclinación de la cabina: solo en cabina, ladea juntos cámara y marco (se aplica a la cámara de `Cockpit`, que tiene el marco de hijo). Objetivo = inclinación objetivo de la nave (`ShipBank.Target`) × 0,4, tope 8°, suavizado de 0,35 s (más lento que el de la nave). Factor 0 = no se inclina. La cámara exterior no se inclina.
- Vibración: solo posición, sin rotar. Desde el 85 % de la punta actual (con daño, la reducida) hasta el máximo en la punta; ×1,5 con boost; 0,03 m en cabina y 0,018 m en exterior; 8 Hz base (suma de senos del tiempo, determinista); intensidad suavizada 0,2 s.
- Boost (`ShipBoost.IsImpulseActive`: subida + mantenimiento): +8° de FOV en las dos cámaras y empuje hacia atrás de 0,05 m en cabina y 0,6 m en exterior; entra en 0,25 s y sale en 0,5 s (curva smoothstep), también al cancelarlo frenando. El marco de la cabina se escala con el FOV, así en pantalla no cambia.
- Se calculan siempre para las dos cámaras (cambiar con C no da saltos). La pausa los congela (no corre el `_PhysicsProcess`); `Respawn` y `PlaceAt` los ponen a cero al momento. Se suman a la sacudida de impacto, que no cambia. Menú y fantasma no tienen efectos.
- Parámetros en el grupo **Efectos de cámara** del Player (ángulos en grados); `Player` los copia a un `CameraEffectSettings` cada paso. Sin menú SETTINGS ni guardado en disco todavía (la spec, sección 6, propone inclinación 0–100 %, vibración 0–100 % y efecto del boost ON/OFF).
- Main.tscn no guarda valores de cámara: todos son los valores por defecto del código.

## Selector de circuito

(spec `docs/selector-circuito.md` v1.0, rama `feature/circuit-selector`): hecho, con tests de la lógica pura y validado por el usuario.

- RACE y TIME TRIAL abren SELECT CIRCUIT en su modo, dentro de la escena del menú (como RECORDS): se ocultan título, opciones, nave, rejilla y sombra del menú; la versión sigue visible. SHIP SELECTION (antes START, seleccionada al abrir) abre SELECT SHIP; BACK / ESC vuelve al menú con la opción del modo seleccionada. Se recuerda el circuito visto (también al salir con BACK) en esta ejecución; no se guarda en disco. RESTART no pasa por los selectores.
- Izquierda: `RACE · SELECT CIRCUIT`, nombre en Orbitron Black 54 px (+ etiqueta naranja `TEST` en los de pruebas), descripción, botones `‹ ›` con `01 / 02`, y la miniatura (bordes cian, centro azul discontinuo, meta naranja, rejilla `MiniatureGrid`), escalada a ~700 px, 62° y una vuelta cada 50 s. Derecha (desde x = 836): TRACK DATA (8 datos calculados, `MAX BANKING` siempre `0°`), ELEVATION PROFILE (120 muestras, escala mínima ±20 m), RECORDS del modo (RACE: TOTAL TIME / BEST LAP; TIME TRIAL: BEST LAP / GHOST) y SHIP SELECTION / BACK. Pie `← → CIRCUIT · ↑ ↓ OPTION · ENTER SELECT`.
- Datos calculados al abrir (una vez por circuito) con el mismo eje que la carrera: `Track.DefaultTrackY`, `Track.DefaultGridSlots` y `RaceSession.RaceLaps` (constantes sacadas para no duplicar valores). En CIRCUIT 01 sale 4.2 KM, 6 curvas y una recta de 1310 M (la de meta es real).
- El perfil mide la altura respecto a la meta (la pista va a −2 m y así sale plana en el centro).
- Los checkpoints de TEST OVAL van a distancias iguales (1/4, 2/4 y 3/4 de la vuelta), no a la salida de cada curva como dice la spec: el formato de `TrackDefinition` no permite colocarlos y el usuario decidió no cambiarlo.
- Parámetros en el Inspector de `CircuitSelectScreen` (tamaños, perfil, miniatura) y de `CircuitMiniature`; `IncludeTestCircuits` en `CircuitCatalog`.

## Pista 3D: desniveles, peraltes y saltos

(spec `docs/pista-3d.md` v1.1, rama `feature/track-3d`): hecha, con tests de la lógica pura y validada por el usuario.

- `TrackDefinition` gana `ElevationKeys` y `BankKeys` (distancia sobre el eje en planta, altura en m / peralte en grados; positivo = borde derecho más alto). Sin claves la pista es plana: `circuit_01` y `test_oval` no cambian y conservan su firma, récords y fantasmas. Avisos en consola (sin colgar) con peralte > 35°, pendiente > 15° o claves desordenadas / fuera de la vuelta (se ignoran). Límites en el grupo **Pista 3D** del Track.
- Toda la geometría (bordes, líneas, transversales, barreras con verticales a lo largo de N, meta, checkpoints, arcos y parrilla) sale del marco de la pista. La colisión con la barrera mide el lateral sobre la superficie; la corrección se aplica en planta a lo largo de la parte horizontal de B. La rejilla de fondo baja a 2 m por debajo del punto más bajo de la pista.
- **Decisión técnica**: la dinámica sigue en planta (no se cambió cómo `Player` guarda posición, velocidad ni rumbo) y la nave flota 2 m en vertical sobre la superficie; con pendientes de 8° la diferencia es < 1 %. En llano todo da exactamente lo mismo que antes.
- Despegue: cuando v² × curvatura del perfil (solo la de la altura, no el movimiento lateral sobre el peralte) supera la gravedad. Gravedad **14 m/s²** (ajustada; la spec v1.0 decía 6): en TEST HILLS despega desde ~325 km/h. En el aire: sin W/S ni agarre, giro al 30 %, sin entrar en derrape (el estado se resuelve al aterrizar), boost sí, barrera igual; morro hacia el movimiento (tope 20°, 0,3 s) y ladeo a 0. Tocando una cresta que pide más del 70 % de la gravedad no aterriza (evita despegar y aterrizar en cada paso). Aterrizaje: fuera la velocidad contra la superficie, pérdida del **10 %** (ajustada; la spec decía 0), sacudida media (≥ 3 m/s) o fuerte (≥ 8 m/s), compresión de 0,3 m en 0,2 s (nave y cabina), alineación en 0,15 s; sin daño.
- Pendiente: punta y aceleración ×0,7 subiendo / ×1,3 bajando a 10° (proporcional, con tope), según hacia dónde apunta la nave respecto al eje; el exceso se pierde con la regla de siempre. Peralte a favor del giro: agarre ×1,4 y giro ×1,15 a 25° (en contra, ×0,6 y ×0,85; tope a 35°; sin girar o en el aire, ×1).
- Cámaras: la cabina va con la nave (peralte completo; `CockpitTrackBankFactor` en **Efectos de cámara** para reducirlo); la exterior sigue el "arriba" de la superficie con 0,3 s (`CameraUpSmoothing`), y el del mundo en el aire. Recolocación y colocación dejan la nave en el suelo y alineada. `MAX BANKING` del selector muestra el peralte real.
- Parámetros en los grupos **Vuelo** y **Pendiente y peralte** del Player.

## Elementos del circuito

(spec `docs/elementos-circuito.md` v1.0, rama `feature/circuit-elements-design`): hechos, con tests de la lógica pura y validados por el usuario. Nada se pinta en la pista salvo las marcas de parrilla.

- Suelo (diseño 0): bordes cian, dos carriles azules discontinuos a ±1/3 del medio ancho (5 m cada 10 m) y línea central al 85 %, transversales cada 10 m al 65 %. Solo visual: firmas, récords y fantasmas no cambian.
- Meta: pórtico doble naranja de medio hexágono (12 m, 4 m entre arcos, pilares 2 m por fuera del borde) con uniones y X; destella (×3,5, 70 % blanco, 0,6 s, caída cuadrática) en cada cruce hacia delante, también el primero tras la salida.
- Checkpoints: pórtico doble rectangular cian al 50 % (9 m, 3 m entre arcos); destello suave (×1,6, 0,45 s). Panel en el arco de entrada con `S1   0:23.456` y la diferencia con el mejor sector anterior de la nave (cian mejora, naranja empeora), 2 s; mira a las naves que llegan, así que el jugador lo ve con la cámara exterior. El HUD muestra lo mismo 2 s bajo `BEST` (también el sector de la meta).
- Semáforo en el arco de entrada de la meta (centro a 9,9 m): 5 octógonos naranjas, encendidos con 4 anillos interiores, destello cian en el GO (0,7 s). Se quitó el semáforo 2D del HUD; la cuenta atrás y el GO! siguen. En TIME TRIAL se ve apagado.
- Parrilla (solo RACE): marco de luz de 3,6 × 6,4 m por puesto, centrado en la nave, con su número 1 m detrás (como en la maqueta, aunque la spec diga "delante"); el del jugador en blanco. Se apagan en 0,5 s desde el GO y no vuelven.
- Túneles (`Tunnels`): sección octogonal abierta por abajo, anillos azules cada 8 m, largueros cian, celosía alterna y bocas dobles; dos pulsos a 120 m/s por shader. Solo geometría: sin colisión.
- Chevrons (`CurveWarnings`): `>>` naranjas cada 9 m sobre la barrera exterior (lado calculado por el giro del eje), de frente a la nave y apuntando al giro, como en la maqueta; ola a 2,2 Hz con un ciclo cada 45 m.
- Cresta (`CrestWarnings`): balizas en las dos barreras a 150 / 100 / 50 m con 1 / 2 / 3 flechas (latido 0,6 Hz) y barrera naranja de −45 a +45 m (sustituye a la cian, con un segundo remate 0,35 m más arriba; latido 0,8 Hz).
- Los fantasmas no hacen destellar nada. Parámetros en los grupos **Suelo**, **Meta**, **Checkpoints**, **Parrilla**, **Semáforo**, **Túneles**, **Avisos de curva** y **Cresta** del Track.

## Selector de naves

(spec `docs/selector-naves.md` v1.1, rama `feature/ship-selector`): hecho, con tests de la lógica pura y validado por el usuario.

- SELECT SHIP va entre SELECT CIRCUIT y la carrera, dentro de la escena del menú. Naves en orden BLADE, VECTOR y HAMMER; solo VECTOR es elegible y las otras dos salen como COMING SOON (placeholder, datos en `—`, START desactivada y BACK seleccionada). Se recuerda la nave elegida en esta ejecución, no en disco. BACK / ESC vuelve a SELECT CIRCUIT con el mismo circuito y SHIP SELECTION seleccionada. La carrera, el fantasma y los récords no cambian según la nave.
- Holograma: instancia de `Spaceship` con material propio que multiplica el color según la profundidad (aristas lejanas al 35 %, sin `ALPHA`), en el mundo del menú con cámara propia que no gira, así los giros van respecto a la pantalla. Una vuelta cada 20 s; arrastre libre en esfera con 0,01 rad por píxel de 1280×720, inercia de 0,4 s (sin inercia si el ratón llevaba quieto más de 80 ms) y giro automático de vuelta a los 3 s con rampa de 1 s. Mientras se ve una COMING SOON se congela, para que VECTOR vuelva en la misma orientación.
- Rejilla y anillos están en 3D sobre un suelo calculado desde las medidas en píxeles de la spec: el anillo es un círculo real (centro en y ≈ 550; la spec no daba esa altura) y el horizonte queda en y ≈ 500.
- SHIP DATA se calcula con `VectorTuning`: `396 KM/H`, `0–200 · 2.2 S`, `74 °/S`, GRIP solo barra, `200–0 · 1.2 S`. Barras: 5 + redondeo(50 × (valor / Vector − 1)), entre 0 y 10. El jugador no ve en ningún sitio que Vector es la referencia. SHARED SYSTEMS: `3 × +100 KM/H` y `3 LEVELS` (`OFF` en TIME TRIAL).
- La opción de SELECT CIRCUIT se llama SHIP SELECTION aunque su lienzo diga START: manda la spec. Cabe a sus 22 px (`FitFontSize` la reduciría si no).
- Parámetros en el Inspector de `ShipSelectScreen` (grupos **Aspecto** y **Holograma**) y de `ShipHologram`.

## Definición de nave

(spec `docs/naves-1-definicion.md` v1.2, rama `refactor/ship-definition`, versión 0.13.1): hecha, con tests de la lógica pura y validada por el usuario. Refactor sin cambios para el jugador.

- Cada nave tiene `ShipDefinition`: perfil técnico (`ShipTuning`) y perfil visual (casco, cabina, encuadre de las cámaras, semiancho de colisión, morro y caja física).
- Semiancho de VECTOR: **3,4 m** (el ancho de colisión es el ancho de la nave: la punta del ala si tiene alas). Se define como valor en el perfil, no se calcula del casco. La caja física de 6,8 × 1,2 × 7,2 hoy no choca con nada; se conserva.
- Siguen comunes y en el Inspector: boost, Derrape, Vuelo, Pendiente y peralte, Inclinación, el resto de Colisiones, `CameraFollow`, `BaseFov`, `MaxFov`, `CameraUpSmoothing`. El FOV 80 de cabina va en el perfil porque el marco está dibujado para él. La spec 2 los ha dejado comunes a todas las naves.
- El fantasma usa el casco de la nave de su archivo (`ShipDesignId`). El encuadre de `MenuBackground` se queda en esa pantalla (siempre muestra `Default`). El del holograma se calcula una vez en `_Ready`: la spec 4 tendrá que recalcularlo con naves de otro tamaño.
- Toberas y estela no entran: las añadirá `efectos-velocidad` al perfil visual. `docs/efectos-velocidad.md` y `docs/escenografia.md` están guardadas sin implementar y **no se tocan hasta que el usuario lo diga**.
- Commits de refactor sin cambios para el jugador: prefijo `refactor:`.

## Perfil técnico, colisión y ajuste en pruebas

(spec `docs/naves-2-tecnico.md` v1.1, rama `feature/ship-tuning`, versión 0.13.2): hecho, con tests de la lógica pura y validado por el usuario. Para el jugador no cambia nada.

- `ship_02` (Blade) y `ship_03` (Hammer) tienen definición: multiplicadores sobre Vector, tope ±10 % comprobado en un test. Blade: punta ×1,08, aceleración ×0,94, agarre ×0,92, frenada ×1,02. Hammer: punta ×0,94, aceleración ×1,10, giro ×0,96, agarre ×1,04, frenada ×0,96. Son los valores de arranque de la spec, sin cambios tras la prueba.
- Semiancho de colisión: Vector 3,4 m (ya lo era), Hammer 2,5 m y Blade 1,6 m. **Vector es la más ancha y no tiene compensación**: si la ventaja de ancho pesa demasiado, se compensa en los multiplicadores, no en el ancho. Con la pista de 40 m quedan 17,1 / 18,0 / 18,9 m libres por lado.
- Siguen no elegibles: el selector las muestra como COMING SOON con datos `—` aunque tengan definición. Solo se conducen con el panel.
- Panel de ajuste (solo con `OS.IsDebugBuild()`, en carrera): F9 panel, F10 cambia de nave, F11 vuelca los multiplicadores en la consola (panel Salida del editor y `godot.log`), F12 vuelve a los valores de la definición. Acciones `debug_tuning_*`. Deslizadores de ±20 %, con ratón y sin foco; lo que pasa del tope sale en naranja. Funciona también con la pausa, por encima de ella. La nave y los valores duran hasta cerrar el juego (también con RESTART y al volver al menú, sin que se entere el selector) y no se guardan en disco.
- Una vez usado F10 o un deslizador, no se guarda ningún récord ni fantasma hasta cerrar el juego, aunque se pulse F12: sin `NEW RECORD`, con aviso en consola si se habría guardado. `BEST`, delta y fantasma existentes siguen visibles.

## Cascos de Blade y Hammer

(spec `docs/naves-3-cascos.md` v1.1, rama `feature/ship-geometry`, versión 0.13.3): hechos, con tests de la lógica pura y validados por el usuario. Para el jugador no cambia nada: solo se ven con el panel de ajuste.

- Blade = `Interceptor_E` y Hammer = `Vanguard_C` de `designs/naves-geometria.js`, con los mismos puntos, orden y colores. Largo / semiancho / alto / segmentos: Blade 8,8 / 1,6 / 0,62 / 138; Hammer 6,6 / 2,5 / 1,40 / 126; Vector 7,2 / 3,4 / 1,0 / 118. Las toberas son parte del casco.
- Cámara exterior: el encuadre de Vector × 1,22 (Blade) y × 0,92 (Hammer), también el punto al que mira, para que el ángulo no cambie. Son los valores de arranque, sin cambios tras la prueba.
- F10 cambia también el casco y el encuadre exterior, también con la pausa y tras RESTART. Con daño, el parpadeo del casco se rehace con el mismo nivel y vuelve a sortear las aristas. Caja física y posición no cambian (cabina y ojo, ver **Cabinas**).
- Morro y caja física de Blade y Hammer están en su perfil, pero no tienen efecto hasta la spec 4.
- La llama y la estela no entran: los datos de cada nave están en la sección "Datos de cada nave" de `docs/efectos-velocidad.md`, que sigue sin implementarse.

## Cabinas: etapa 1, base y Vector

(spec `docs/naves-3b-cabinas.md` v1.1, rama `feature/ship-cockpits`; la versión sube a 0.13.4 en la etapa 3): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado. Sustituye a lo que las secciones anteriores dicen del marco de líneas 3D de la cabina, que ya no existe.

- La cabina es una capa 2D (`CanvasLayer` en la capa 0, debajo del HUD) a ×1,5 sobre 1920×1080. Va fija en pantalla: lo que se ladea, vibra o se abre con el boost es el mundo. Fuera de 16:9 Godot pone bandas negras (`canvas_items` + `keep`), también sobre el 3D, así que la capa no calcula franjas. Sin bloom: el glow solo afecta al 3D; el halo del HUD se aproxima con una copia mayor y tenue.
- Los elementos van en el orden del diseño y se dibujan por tramos seguidos: los fijos solo se redibujan cuando parpadea una arista y los vivos, cada frame. Parpadean los trazos de la parte fija, con los valores de siempre del Inspector (ver **Rendimiento de las cabinas**).
- En cabina, velocidad, boost y daño salen en el cuadro de mando y se ocultan del HUD de pantalla; posición, vuelta y tiempos no cambian, aunque quedan cruzados por los pilares. En TIME TRIAL (`Player.CockpitDamageEnabled` en false) el daño queda apagado.
- **Decisión del usuario**: cámara de cabina inclinada −8° (`CockpitFraming.PitchDegrees`), para dejar el punto de fuga en y ≈ 300 de 720 como en el diseño; la altura del ojo no lo mueve. Con el boost sube a unos 307 por el FOV.
- Luz: tinte naranja, tinte rojo y brillo de las pantallas, con intensidades en el grupo **Cabina** del Player.
- Se respeta una rareza del diseño: algunos tornillos llevan la ranura por la mitad porque el archivo concatena `x + 2` sobre un texto (`CockpitKit.ScrewRoundedX`).

## Cabinas: etapa 2, Blade

(spec `docs/naves-3b-cabinas.md` v1.1, rama `feature/ship-cockpits`): hecha, con tests de la lógica pura y validada por el usuario. Solo se ve con F10: Blade sigue sin ser elegible.

- BURBUJA: arco fino, morro largo y pantalla larga con todo el HUD. La barra de velocidad se rellena en el juego aunque en el lienzo no, por un fallo del archivo; la marca naranja va en la punta de la nave (428 km/h en Blade).
- Ojo: el de Vector con la altura × 0,62 (0,279 m), misma profundidad, FOV 80 e inclinación −8°. Es el valor de arranque, sin cambios tras la prueba.
- F10 cambia también cabina y ojo (corrige la spec 3), con la pausa y tras RESTART. Con daño, el parpadeo de casco y cabina se rehace con el mismo nivel y vuelve a sortear las aristas.
- El fantasma dibuja la nave de su archivo, no la del jugador: con F10 se sigue viendo el de Vector hasta que la spec 4 permita grabar con otra nave.

## Cabinas: etapa 3, Hammer

(spec `docs/naves-3b-cabinas.md` v1.1, rama `feature/ship-cockpits`, versión 0.13.4): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec 3b. Solo se ve con F10.

- BÚNKER: canard delante, cristal dividido por pilares de una pieza, dial central, boost con tres luces bajo tapas de seguridad y daño en tres placas atornilladas. El desgaste de los pilares usa el generador del diseño con las semillas 11, 23, 37 y 51.
- Dial (centro (640, 672), radio 48, de 150° a 390°): arco de fondo, tramo final desde la punta, arco de velocidad, 11 marcas, números y aguja. Números y tramo final siguen la escala de cada nave (472 km/h en Hammer). El tramo final es naranja, como en el archivo, aunque la spec diga rojo. Que el dial sobresalga de la pantalla es del diseño y el usuario lo da por bueno.
- Ojo: el de Vector con la altura × 1,40 (0,63 m), misma profundidad, FOV 80 e inclinación −8°. Es el valor de arranque, sin cambios tras la prueba.

## Rendimiento de las cabinas

(pedido por el usuario tras probar la etapa 3): redibujar es lo que cuesta, no enseñar lo ya dibujado; un redibujado completo del Búnker tardaba ~30 ms y daba tirones en cabina con daño. **Que no vuelva a pasar es muy importante para el usuario.** Medido conduciendo a 144 Hz, Hammer con daño 3 pasó de 108 FPS y 148 frames de más de 16,7 ms a 144 FPS y ninguno. No se ha probado en un PC flojo.

- La parte fija va en tramos de hasta 30 elementos y solo se redibuja el tramo donde cambió una arista; la parte viva, solo si cambia algo (`SameDrawing`).
- Tornillos, remaches, arañazos, lamas de las rejillas de Hammer y marcas finas del dial no parpadean (`AddDetail`): 131 aristas en Vector, 97 en Blade y 89 en Hammer. Decisión del usuario.
- En la cámara exterior la capa no se oculta: se aparta de la pantalla con `Offset`, porque al volver a enseñar algo oculto Godot lo redibuja entero. Lo que parpadea mientras tanto se redibuja al volver.
- Lo menudo que no cambia (círculos de hasta 4 px de radio y ranuras cortas) se dibuja de antemano en una textura común por cabina (`CockpitSpriteAtlas`), porque Godot agrupa en una llamada los rectángulos seguidos de una misma textura: unas 550 / 420 / 880 llamadas de dibujo por frame en Vector / Blade / Hammer.
- Una barra de menos de 0,25 px no se dibuja: tan fina no se puede triangular y salía un error en cada frame al parar.
- Para medir: sin vsync (`Engine.max_fps = 0`) el coste se ve mejor, pero los tirones se cuentan con vsync; mirar el peor frame y los frames lentos, no los FPS medios.

## Nombres definitivos: BLADE y HAMMER

(spec `docs/naves-4-integracion.md` v1.1, etapa 0, rama `feature/ship-integration`): hecho, con tests y validado por el usuario. Refactor: conducción y aspecto iguales; cambian los textos y el rótulo de la cabina de Blade, `BLADE · BLD-02`. Las naves siguen sin poder elegirse.

- `ship_01` = VECTOR, `ship_02` = BLADE (antes Interceptor / Hoja), `ship_03` = HAMMER (antes Vanguard / Martillo). Los ids no cambian: los fantasmas y los récords los usan.
- **Los nombres viejos se conservan a propósito** en `designs/` (en el lienzo siguen siendo Hoja y Martillo, y el archivo guarda las opciones descartadas), en las citas a `designs/` de los comentarios (`Interceptor_E`, `Cab_Hoja_1`…), en las specs ya hechas y en el documento maestro. `docs/efectos-velocidad.md` no se toca hasta implementarla.

## Selector con las tres naves

(spec `docs/naves-4-integracion.md` v1.1, etapa 1, rama `feature/ship-integration`): hecho, con tests de la lógica pura y validado por el usuario. Sustituye a lo que las secciones anteriores dicen de que BLADE y HAMMER no son elegibles y de que el encuadre del holograma se calcula una vez.

- BLADE, VECTOR y HAMMER se eligen en SELECT SHIP con holograma, datos y START. El COMING SOON sigue en el código para naves futuras.
- Holograma: la escala es común (Vector a 480 px de ancho; `ShipScreenWidth` en el Inspector de `ShipSelectScreen`) y la cámara no se mueve. Cada nave se centra por el centro de su caja. Esferas de 9,95 / 9,38 / 8,40 m (Vector / Blade / Hammer); una prueba avisa si una nave pasa de la de Vector, y entonces habría que calcular la escala con la más grande.
- Última nave: START la guarda en `lastShipDesignId` de `settings.json` (sin subir la versión de formato: el campo es opcional). `MainMenu` la lee solo la primera vez que se abre; `Main.tscn` ejecutada sola corre con VECTOR. Se guarda el id tal cual y `ShipCatalog.StartShip` decide al arrancar. RESET RECORDS no lo borra. La nave del fondo del menú sigue siendo VECTOR.

## Carrera, TIME TRIAL, fantasma y resultados con cada nave

(spec `docs/naves-4-integracion.md` v1.1, etapa 2, rama `feature/ship-integration`): hecho, con tests de la lógica pura y validado por el usuario. Sustituye a lo que las secciones anteriores dicen de que el panel se queda con su nave al volver al menú y de que el fantasma siempre es de Vector.

- Entrar con la nave elegida no necesitaba arreglos: nada dependía de pasar por F10. Medido entrando desde el selector, cada nave sale con su casco, su cabina, su ojo, su punta y su cámara exterior.
- Fantasma: el casco es siempre el de la nave de su archivo y se actualiza al momento al batir el récord. Antes solo se ponía al cargar uno guardado, así que un primer récord con otra nave salía con casco de Vector.
- Panel F9–F12: al entrar con una nave distinta de la elegida la última vez pasa a ella, con lo retocado, y F10 sigue desde ella. RESTART no la cambia. **Caso conocido**, solo de depuración: tras F10, elegir otra vez la misma nave que la última vez no devuelve el panel a ella, porque la sección 7 de la spec solo lo pide con otra nave distinta (la 4.1 parece decir lo contrario).
- Resultados de RACE: nombre de la nave bajo la posición, en Share Tech Mono de 16 px (`ResultsShipNameFontSize`, grupo **Resultados** de `Main`). Es la nave de la sesión (`Player.Definition`, que F10 no cambia).
- Para llegar a los resultados en una prueba automática, a 144 Hz los frames no sirven de medida: hay que esperar por tiempo los 1,5 s de `ResultsDelay`.

## Récords con nave

(spec `docs/naves-4-integracion.md` v1.1, etapa 3, rama `feature/ship-integration`, versión 0.14.0): hecho, con tests de la lógica pura y validado por el usuario. Cierra la spec 4: las tres naves son jugables de verdad.

- Cada récord de `records.json` (TIME TRIAL; RACE tiempo total y mejor vuelta) gana el campo opcional `shipDesignId`, sin subir la versión de formato. Sigue habiendo uno por circuito y modo: batirlo con otra nave lo sustituye. Tiempo total y mejor vuelta guardan cada uno su nave, así que pueden ser de naves distintas.
- Un récord sin el campo cuenta como VECTOR, también los hechos con BLADE o HAMMER antes de esta etapa. Un id que no está en el catálogo se carga igual y no enseña nave.
- RECORDS: `29 OCT 2026 · BLADE` detrás del tiempo; SELECT CIRCUIT sigue sin nave. **Fecha y nave van a 11 px** en vez de 14, validado por el usuario: es el mayor tamaño con el que el texto más largo deja 12 px hasta el separador de x = 915. Se calcula una vez con el texto y el tiempo más anchos posibles y vale para las tres filas, también para `NO RECORD`, así no salta al cambiar de circuito. Se avisa en la consola por debajo de 10 px.

## Curva de aceleración: etapa 1, curva común

(spec `docs/naves-5-curva-aceleracion.md` v1.0, rama `feature/ship-acceleration-curve`; la versión sube a 0.14.1 en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. Cambia la sensación de las tres naves. Sustituye a lo que las secciones anteriores dicen de que el panel de ajuste bloquea los récords.

- Con W por debajo de la punta, aceleración × max(empuje mínimo, 1 − (velocidad / punta)^exponente), con la velocidad del motor y la punta y la aceleración efectivas de ese momento (nave, daño, pendiente, derrape). Exponente 2 y empuje mínimo 10 %, iguales en las tres naves. Frenada, `Drag`, exceso sobre la punta, boost y pérdidas del derrape no cambian.
- `ShipTuning` lleva `AccelCurve` y `MinThrust`. Un `ShipTuning` sin curva (`AccelCurve` = 0, `AccelerationCurve.Linear`) acelera como antes: las pruebas de dinámica que montan el suyo no cambian.
- **Decisión**: el empuje mínimo es una constante, sin Inspector, aunque la spec lo pedía: el selector de naves también lo necesitará y no tiene acceso al `Player`.
- Medido en el juego con VECTOR, con el velocímetro: 0–200 en 2,63 s, 0–300 en 4,53 s y 396 km/h en 10,8 s (lineal: 2,40 / 3,51 / 5,4). Salen unos 0,2 s por encima del cálculo, y hasta 1 s en la punta, porque el velocímetro muestra la velocidad real, que va por detrás de la del motor por la inercia lateral (ya pasaba antes). Desde parado necesita unos 800 m para llegar a la punta.
- Derrape largo a fondo: como la aceleración cae arriba, VECTOR se estabiliza entre ~323 km/h (ángulo máximo) y ~373 km/h (poco ángulo), por cálculo. El usuario da el derrape por bueno.
- Panel (F9): casilla `LINEAR ACCEL (OLD)`, común a las tres naves; F12 no la quita.
- **Decisión del usuario, cambia la spec 2**: el panel ya no bloquea récords ni fantasma, porque guardarlos sirve para comparar naves; se guardan con la nave que se conduce, también con F10 (`TuningLab.DrivenShipId`). Sigue habiendo uno por circuito y modo: batirlo con otra nave lo sustituye, y el usuario no quiere uno por nave. El nombre de la nave en los resultados de RACE sigue siendo el de la sesión.

## Curva de aceleración: etapa 2, curva por nave y selector

(spec `docs/naves-5-curva-aceleracion.md` v1.0, rama `feature/ship-acceleration-curve`, versión 0.14.1): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec 5. Sustituye a lo que las secciones anteriores dicen del `0–200` lineal del selector.

- El exponente es un dato de cada nave (`VectorTuning.AccelCurve` y una constante en `BladeShip` y `HammerShip`): 2 en las tres, como valor de arranque. No es un multiplicador: su rango es 1,5–3,0 (`AccelerationCurve.IsWithinRange`), comprobado en un test. El empuje mínimo sigue siendo común.
- SHIP DATA: `ShipStats.SecondsTo` integra la dinámica real a 240 pasos por segundo, interpolando el cruce (sin eso, los 2,447 s de VECTOR salían `2.5`). La barra de `ACCELERATION` compara por tiempo: 5 + redondeo(50 × (tiempo de Vector / tiempo de la nave − 1)). VECTOR `2.4 S` · 5, BLADE `2.6 S` · 3, HAMMER `2.3 S` · 9. Como sale de integrar, la punta también mueve esta barra.
- Panel: `ACCEL CURVE` por nave (1,0–4,0, paso 0,1, naranja fuera de rango, con 0–200 y 0–300 al lado; también en VECTOR, cuyos porcentajes siguen bloqueados) y `MIN THRUST` común (0–30 %; `EMPUJE MIN` en la primera versión de la spec). Compara con VECTOR tal como esté en el panel. F11 vuelca también curvas y empuje mínimo; F12 devuelve la curva de esa nave y el empuje mínimo, sin quitar la casilla lineal.

## Equilibrio con piloto automático: etapa 0 y refactor previo

(spec `docs/naves-6-equilibrio.md` v1.0, rama `feature/ship-balance`): hechos, con tests de la lógica pura y validados por el usuario. Refactor sin cambios para el jugador: una nave ya se puede simular sin escena.

- La física de un paso pasó de `Player` a `ShipPhysics`, la colocación de `Track` a `TrackPlacement` y el bucle de cruces de `RaceSession` a `RaceTracker.Advance`. Daño y recolocación siguen en `Main`, porque ya son piezas puras con muy poca conexión: el simulador las usará directamente.
- `MoveAndSlide` se queda en `Player`: en la escena no hay nada contra lo que chocar, así que equivale a posición + velocidad × paso, que es lo que hace `ShipPhysics.Move`. `Track.ProjectOnAxis` y `AxisPoint` ya no existen.
- Valores por defecto en un solo sitio: `ShipPhysicsDefaults` y constantes en `DamageSettings`, `RespawnSettings`, `GridSettings` y `BarrierGeometry`. `Main.tscn` no sobrescribe ningún valor del `Player`.
- Comprobado con una sonda temporal, 6.000 pasos en cuatro escenarios (TIME TRIAL y RACE, `circuit_01` y `test_hills`, las tres naves): idéntico al bit antes y después.
- Inventario: física a 60 Hz; entrada todo o nada (`ShipInput`), con el giro suavizado dentro de la dinámica; `circuit_01` tiene 4 sectores.

## Equilibrio con piloto automático: etapa 1, simulador y piloto

(spec `docs/naves-6-equilibrio.md` v1.0, rama `feature/ship-balance`): hecha, con tests de la lógica pura y validada por el usuario. Para el jugador no cambia nada: solo depuración.

- Sin boost, en `circuit_01`: BLADE 38,750 (−1,800), VECTOR 40,550, HAMMER 42,383 (+1,833), sin contactos; BLADE gana los cuatro sectores. Reglas 1 y 2 en ✗: es lo que corrigen las etapas 2 y 3. **Riesgo abierto**: VECTOR no gana ningún sector y sus porcentajes no se tocan; es intermedia en cuatro de los cinco parámetros, empata en giro con BLADE y es la más ancha. Si la regla 2 no se puede cumplir, decide el usuario.
- Medido en el juego real con el piloto, sin ventana: 38,750 / 40,533 / 42,383, y BLADE en `test_hills` 18,750 igual que el simulador. Diferencia máxima 0,017 s.
- **F7, no F8** (acción `debug_autopilot`): F8 parece ser el atajo con el que Godot detiene el juego lanzado desde el editor (sin confirmar). Con el piloto usado, esa carrera no guarda récord ni fantasma aunque se quite, aunque el panel ya no lo bloquee desde la spec 5. El piloto de F7 no usa boost.
- Se apartan de la spec o la concretan: el margen del 97 % va solo sobre la velocidad de curva, porque si no la nave no llegaría a su punta; a más de 160 m de una curva la línea va forzada por el centro, porque a la curvatura le da igual el lateral en una recta larga. El ±0,1 s con el simulador se compara en la vuelta lanzada, porque en RACE se sale de un puesto aleatorio.
- Trampas ya resueltas, por si se toca el piloto: mirar la velocidad y no el morro hace oscilar la dirección; pulsar y soltar sin acumular el error deja el giro a saltos y la nave ±2,5 m fuera de la línea; suavizar con vecinos a índice fijo (o hacia el punto medio) pega la línea al interior, cierra el radio y **penaliza a la nave estrecha**. La anticipación (8 m + 0,3 s × velocidad) tiene una zona plana alrededor.
- El piloto no comete errores y pulsa con una precisión que una persona no tiene: en `test_hills` hace 18,75 s con BLADE sin boost y el récord del usuario es 19,25 s.

## Equilibrio con piloto automático: etapa 2, barrido

(spec `docs/naves-6-equilibrio.md` v1.0, rama `feature/ship-balance`): hecho, con tests de la lógica pura. Lanzado en Godot sin ventana; el usuario no lo ha probado desde el Inspector y lo da por bueno, porque los barridos los lanza el agente. Tampoco se ha visto el naranja de lo que se sale de rango. Para el jugador no cambia nada: no toca las definiciones.

- `MATCH` busca siempre en el rango permitido (multiplicadores 0,90–1,10 y curva 1,5–3,0), sin usar `From` y `To`, y compara en el primer circuito que no sea de pruebas. `SWEEP` y `MATCH` solo escriben en la consola. Barrer los porcentajes de VECTOR se permite, con un aviso, para experimentar.
- **Datos para la etapa 3**, en `circuit_01` y moviendo un solo parámetro: la punta lo decide casi todo (cada 1 % ≈ 0,33 s). Es el único parámetro con el que se iguala a VECTOR: BLADE ×1,006 (ahora ×1,08) y HAMMER ×0,997 (ahora ×0,94), lo que dejaría las tres casi con la misma punta. La frenada apenas cuenta. Con BLADE en ×1,006 los sectores quedan BLADE 2, VECTOR 2 y HAMMER 0: VECTOR sí puede ganar sectores. Ningún valor de un solo parámetro cumple las reglas 1 y 2: hay que mover varios, uno cada vez.

## Equilibrio con piloto automático: etapa 3, valores definitivos

(spec `docs/naves-6-equilibrio.md` v1.0, rama `feature/ship-balance`, versión 0.14.2): hecha y validada por el usuario conduciendo. Cierra la spec 6. Cambia cómo van BLADE y HAMMER. Sustituye a lo que las secciones anteriores dicen de sus multiplicadores, de su curva y de los datos que salen de ellos: 428 km/h en BLADE (ahora 412), 472 en la escala de HAMMER (ahora 488, su punta más el boost) y las barras de SHIP DATA.

- BLADE: punta ×1,04, aceleración ×0,94, giro ×0,90, agarre ×0,90, frenada ×1,02, curva 2,0. HAMMER: punta ×0,98, aceleración ×1,10, giro ×1,04, agarre ×1,10, frenada ×0,96, curva 1,5. VECTOR no cambia.
- En `circuit_01`, sin boost: BLADE 40,333 (−0,217), VECTOR 40,550, HAMMER 40,783 (+0,233), sin contactos. Sectores: BLADE S1 y S2 (las rectas), HAMMER S3 (el técnico), VECTOR S4. Con el piloto en el juego real: 40,333 / 40,533 / 40,767. Reglas 1 y 2 en ✓. Fuera de lo obligatorio, en `test_oval` HAMMER gana los cuatro sectores (−0,233) y en `test_hills` BLADE (−0,700).
- Elegidos con una búsqueda temporal de combinaciones (648 tandas, 901 parejas válidas): entre las de más diferencia de punta, sin depender de un empate en S4. El barrido de varios parámetros sigue fuera de `BalanceRunner`.
- **Lo que hay que saber antes de tocarlos**: VECTOR gana S4 por solo 0,033 s, así que un cambio pequeño en cualquier nave se lo puede quitar; lo vigila el test `The_definitions_pass_the_mandatory_rules_in_circuit_01`. La curva 1,5 de HAMMER está en el límite y le quita aceleración arriba (`0–200` en `2.4 S`, igual que VECTOR): sin ella ganaría también S4 y VECTOR se quedaría sin sectores. La punta se ha acercado (de 372–428 a 388–412 km/h), porque cada 1 % vale 0,33 s.
- Selector: `412 KM/H`, `2.6 S`, `67 °/S` en BLADE, con `HANDLING` y `GRIP` a 0 segmentos; `388 KM/H`, `2.4 S`, `77 °/S` en HAMMER, con `GRIP` a 10. VECTOR sigue igual.
- Los récords y fantasmas hechos con BLADE o HAMMER antes de esta versión se conservan aunque esas naves ya no vayan igual.

## Radar: etapa 1, naves de prueba y radar

(spec `docs/radar-minimapa.md` v1.0, rama `feature/radar-minimap`; la versión sube en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. Para el jugador solo aparece el radar, vacío hasta que haya rivales. Se apartan de la spec, por decisión del usuario:

- **F6, no F7**, para las naves de prueba (acción `debug_test_ships`): F7 es el piloto. `docs/cabinas-3d-1-vector.md` también dice F7 en su prueba 18b.
- **El radar va fijo en pantalla también en cabina**: la spec habla de inclinarlo "como el resto del HUD de cabina", pero nada del HUD se inclina.
- **Borde izquierdo alineado con `POSITION`** (x ≈ 27 sobre 1280×720, los 40 px de `Hud.Margin` en 1920×1080), no a 40 px sobre 1280×720. La altura es la de la spec. El minimapa irá igual.
- Lo concreta el agente: proyección en planta con el rumbo del morro y altura en vertical (con el plano de la nave, un peralte haría desaparecer naves de la misma pista). Solo el haz va recortado al círculo: un eco a más de ~113 m sobresale hasta 3,4 px, porque `clip_children` obligaría a redibujar por un búfer aparte.
- Las naves lejanas (+920 y −1550 m) solo son lejanas en `circuit_01`: en `test_oval` la vuelta cerrada deja una a ~50 m. Se solapan con tu casco al apartarse a 3,8 m, porque VECTOR mide 3,4 m de semiancho; no hay colisión.
- Medido en cabina de HAMMER sin daño, a 144 Hz: ningún frame lento, peor frame 15,5 ms con radar y 15,2 ms sin él. Para medir, usa `Time.get_ticks_usec`: el `delta` de `_process` sale suavizado. La carrera arranca en persecución: C pasa a cabina.
- Parámetros en el grupo **Radar** de `Main`.

## Radar: etapa 2, minimapa

(spec `docs/radar-minimapa.md` v1.0, rama `feature/radar-minimap`, versión 0.14.3): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Para el jugador aparece el minimapa bajo la posición, en todas las vistas y modos; no sale en resultados.

- Centro a x ≈ 105 (alineado con `POSITION`, como el radar), borde de arriba a 78 px sobre 1280×720. En TIME TRIAL la cabecera queda justo encima, sin tocarlo.
- **Escala radio / 450 m**, no (radio − 4) / 450 como en la página: lo dicen el texto de la spec, su prueba 5 y el prototipo de la cabina 3D, cuya prueba 10 pide las mismas posiciones.
- **Raya de meta con ±3 semianchos reales**: ±60 m, unos 21 px en total. En la página del radar son ±33 m y en el prototipo de la cabina ±30 m, porque sus pistas son más estrechas.
- Sin `clip_children`: el trazado se recorta por cálculo. Las puntas de la banda y los puntos de las naves pueden sobresalir unos píxeles al cruzar el borde en diagonal. Una nave cuyo centro queda fuera no se dibuja.
- `AUTOPILOT` pasa debajo del minimapa. El panel F9 lo tapa mientras está abierto (solo depuración).
- Medido en cabina de HAMMER sin daño, 15 s por caso, a 144 Hz: con radar y minimapa 2 frames lentos y 23,4 ms el peor; sin ellos 1 y 34,3 ms. Sin vsync, 1,74 ms de media frente a 1,51 ms: unos 0,24 ms por frame. No se ha probado en un PC flojo. Para medir, ninguna captura dentro de la ventana medida: guardar el PNG cuesta ~75 ms.
- `RadarSweep` es uno solo, así que el radar sigue igual al cambiar de cámara o de pantalla (la cabina 3D lo dibuja en la suya).

## Cabina 3D de VECTOR: etapa 1, Blender

(spec `docs/cabinas-3d-1-vector.md` v1.2, rama `feature/cockpit-3d-vector`; la versión sube a 0.14.4 en la etapa 3): hecha y validada por el usuario mirando las capturas y el `.blend`. Para el jugador no cambia nada: el juego aún no usa el modelo.

- `art/blender/cockpit_vector.py` traduce `buildCockpit()` de `designs/cabina-3d-vector-radar.html` (colocación 1, sin intercambiar) y `art/blender/lib/` tiene lo común para BLADE y HAMMER: `geometry` (mismas mallas que three.js, sin Blender), `textures`, `builder` y `blender_io` (BLADE añade los suyos, ver su etapa 1). **El script es la fuente**: el `.blend` se regenera y lo que se cambie a mano en él se pierde. `art/.gdignore` evita que Godot importe lo de esa carpeta.
- Salida en `assets/cockpits/`: `cockpit_vector.glb` (38.515 triángulos, 3,4 MB), `cockpit_vector.manifest.json` y `cockpit_env.png`. Godot, al importar el `.glb`, saca sus 13 texturas ahí como `cockpit_vector_T_*.png`: son copias, unos 1,2 MB repetidos, y se dejan así porque Godot les genera mipmaps y las comprime.
- Comprobado: las 13 texturas son idénticas byte a byte a las del prototipo, comparadas ejecutando su código en Node; dos ejecuciones dan el mismo `.glb` y el mismo panorama; el script falla si falta una de las 50 piezas del contrato o si los ejes no cuadran.
- Se aparta de la spec o la concreta: las piezas no vivas van juntas por zona y material (76 mallas, para reducir llamadas de dibujo), sin un objeto vacío por grupo; las cajas redondeadas salen de la fórmula de three.js, sin modificador; cada pieza viva tiene su material (`M_Emissive_ShiftGreen_ShiftLight_00`, `M_Screen_Screen_L`).
- Capturas con Eevee y vista Filmic (esta instalación no ofrece ACES), solo para mirar: el morro sale más claro porque los reflejos ×0,35 los aplica Godot. El panorama va con Cycles, porque Eevee no tiene cámara panorámica; su centro mira a −Z, y en Godot hay que comprobar si el cielo necesita girarse.
- Trampas ya resueltas: una matriz pasada a Blender como lista se lee por columnas (hay que usar `mathutils.Matrix`), y Cycles guarda su tiempo de render en el PNG, así que el script quita esos textos para que el archivo no cambie.

## Cabina 3D de VECTOR: etapa 2, Godot

(spec `docs/cabinas-3d-1-vector.md` v1.2, rama `feature/cockpit-3d-vector`; la versión sube a 0.14.4 en la etapa 3): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado: VECTOR en cabina lleva la cabina 3D, con pantallas de prueba y el HUD de pantalla todavía visible. BLADE y HAMMER siguen con su capa 2D. La capa 2D de VECTOR ya no se crea, y su código y su captura se quitan en la etapa 3. Sustituye a lo que las secciones anteriores dicen del encuadre de VECTOR.

- **Decisión del usuario, encuadre B**: la cámara del mundo de VECTOR es la del prototipo, con el ojo a 1,45 m de la pista (`Eye.Y` = −0,55), FOV 70 y sin inclinar. BLADE y HAMMER salen de `CockpitFraming.For2DCockpit`: **no pueden copiar el encuadre de VECTOR**, y tampoco su `Cockpit3D` (van con `Cockpit3D = null`, porque su perfil es una copia del de VECTOR). Límite conocido: desde los puestos de delante, el semáforo puede salirse por arriba.
- El grupo **Cabina 3D** del `Player` tiene luces, FOV, balanceo, vibración, contragiro, empuje del boost, glow y brillo de las pantallas (valores finales, en la etapa 3).
- Lo que glTF no trae, en `Cockpit3DView`: barniz (Godot no lee `KHR_materials_clearcoat`), metal por material (la textura de rugosidad es gris y se leería como metal), reflejos ×0,35 (bajando `MetallicSpecular` y `Clearcoat`, porque Godot no tiene intensidad de reflejos por material), manchas en suma y sombras.
- Exposición **1,75**: el ACES de three.js divide entre 0,6 y el de Godot no, así que el 1,05 del prototipo se quedaba oscuro. Imagen del `SubViewport` con mezcla premultiplicada: así no salen bordes raros y el halo se ve sobre el mundo. Con fondo transparente, Godot no pinta el cielo, pero lo sigue usando para los reflejos.
- Medido en VECTOR en cabina, daño 3, 15 s, 144 Hz: 144 FPS, ningún frame lento, peor frame 12,5 ms (con la 2D, 14,9 ms). Sin vsync, 1,87 ms de media frente a 1,39 ms. C no da tirones, ni la primera vez (9,6 ms); F10 de VECTOR a BLADE, 56 ms, solo depuración. Sin medir aún con radar y minimapa vivos, y sin probar en un PC flojo. Para medir, ninguna captura dentro de la ventana y `Select-Object -First` no sirve para filtrar la consola: corta el proceso.
- Pendiente: sin antialiasing, como el resto del juego, se notan los dientes de sierra en los bordes de la cabina. Sin ajustes de exportación todavía: cuando los haya, hay que incluir `*.json` para el manifiesto. El giro del panorama (180°) sale de la fórmula de Godot y no se ha comprobado aparte: se nota poco, porque casi es simétrico.

## Cabina 3D de VECTOR: etapa 3, HUD y luces

(spec `docs/cabinas-3d-1-vector.md` v1.3, rama `feature/cockpit-3d-vector`, versión 0.14.4): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que las secciones anteriores dicen de la cabina 2D de VECTOR (`VectorCockpit` ya no existe; su diseño sigue en `designs/`), de sus 131 aristas y de las pantallas de prueba. En la cabina de VECTOR, velocidad, boost, daño, radar y minimapa salen en sus pantallas y no en el HUD; en la cámara exterior, como siempre. BLADE y HAMMER no cambian.

- Pantallas grandes: `drawLeft` y `drawRight` del prototipo, en Rajdhani Bold, con lo fijo aparte; solo se pintan si cambia lo que enseñan (`RacingScreenCanvas.Commit` + `SubViewport` en `Once`). Pequeñas: `RadarView` y `MinimapView` reutilizados, cada frame y solo en la vista de cabina. El estado es `CockpitHudState`, con `CockpitHudLook` para lo apagado de cada estilo.
- Parpadeo con daño: `EdgeFlicker` con una "arista" por luz (11, `Accent_*` y `Led_*`), bajando al 20 %. Shift lights con intensidades constantes, sin Inspector: casi salen blancas, con el color en el halo.
- **Luces ajustadas midiendo 16 zonas contra el prototipo**, a petición del usuario: principal 0,65, pantallas 0,2 y 0,13, glow 0,8, boost 0,85, daño 0,7. Se añaden `Cockpit3DClearcoat`, `Cockpit3DGlowWide`, `Cockpit3DReflections` y `Cockpit3DCarbonSpecular`. **`Cockpit3DClearcoat` no puede pasar de 1**: la consola se queda negra. El limitador de rugosidad está apagado en `project.godot`: apagaba el carbono, y a cambio puede centellear algo más con la vibración. Diferencias que quedan en la spec, sección 13.
- Para comparar con el prototipo: en el navegador hay que tener activos `CÁMARA PROPIA` y `AUTO`, y copiar el lienzo en un `requestAnimationFrame` doble con `drawImage` para leer sus píxeles. Con la pestaña en segundo plano va a saltos.
- Medido en VECTOR en cabina, daño 3, 15 s, 144 Hz: 144 FPS, ningún frame lento, peor frame 13 ms. Sin vsync, 1,90 ms de media (2D: 1,39 ms). El primer C de la carrera da un frame de 17 ms; los siguientes no pasan de 9 ms. No se ha probado en un PC flojo.
- El usuario no ha pedido cambiar el empuje del boost (`Cockpit3DBoostPush`, 5 cm), aunque se nota bastante con la cabina tan cerca del ojo. Sin comprobar en el juego: el arco rojo de nave pegada en la pantalla del radar (sí en tests).

## Cabina 3D de BLADE: etapa 1, Blender

(spec `docs/cabinas-3d-2-blade.md` v1.0, rama `feature/cockpit-3d-blade`; la versión sube a 0.14.5 en la etapa 3): hecha y validada por el usuario mirando las capturas, el `.blend` y el prototipo. Para el jugador no cambia nada: el juego aún no usa el modelo (BLADE sigue con la Burbuja 2D).

- `art/blender/cockpit_blade.py` traduce `buildCockpit(4)` de `designs/cabina-3d-blade.html` (disposición 4 · DEFINITIVA; las 1–3 se quedan como diseño). Estilo MILITAR: pintura gris verdosa con desconchones, pintura rugosa negra, antideslizante, pavonado y franjas de peligro.
- **El prototipo de BLADE no usa las piezas ni el ruido del de VECTOR**: su `B` pasa todo por `boxUV`, `beam` lleva la caja a lo largo de su Z, la tira parte los cuadriláteros por la otra diagonal, las piezas cuelgan de grupos con `lookAt` y `mkRng` / `valueNoise` / `fbm` son otros. Por eso van aparte, sin tocar lo de VECTOR: `lib/blade_builder.py` y `lib/textures_military.py`; `lib/geometry.py` solo gana `sphere_section` (cúpula) y `strip_ab`. Para una cabina nueva, mirar antes si su prototipo usa las piezas de uno u otro.
- Salida en `assets/cockpits/`: `cockpit_blade.glb` (66.614 triángulos, 7,6 MB: pesan los mapas de relieve finos del antideslizante y de la pintura rugosa), `cockpit_blade.manifest.json` y `cockpit_blade_env.png` (panorama propio: su `buildEnv` no es el de VECTOR). Godot saca 20 texturas como `cockpit_blade_T_*.png`. Las texturas sueltas de Blender van en `art/blender/out/textures/blade/`, porque algunas se llaman igual que las de VECTOR.
- El manifiesto tiene el formato del de VECTOR (`lowReflectionMaterials` con `M_PaintNose` × 0,5) y tres datos más para la etapa 2: `reflections` (morro 0,5, cristal 1,3, cristal HUD 1,5), `transparentMaterials` y `M_ScreenHUD` entre los materiales en suma. `M_PaintDark` lleva el tinte del prototipo metido en su propia textura.
- Como en VECTOR, las piezas no vivas van juntas por zona, material y sombra (las sin sombra en objetos `*_NoShadow`, apuntados en `noShadow`), sin un vacío por grupo: `Plate` cuelga de `Dash`, no de `Dash_Plate`. Cada pieza viva lleva su material (`M_Screen_Radar`, `M_Lamp_Caution`, `M_Emissive_Accent_Accent_Radar`…).
- **Añadido sobre el prototipo**, pedido por el usuario porque el frontal quedaba vacío (spec 3.4): a cada lado de las cajas, placa de interruptores con 2 × 3 palancas, guardas compartidas y selector giratorio; tapa de seguridad a franjas en la izquierda y segundo selector en la derecha; juntas y remaches en el frontal. Las piezas pequeñas del añadido llevan caja de 1 segmento y esferas bajas: +8.000 triángulos (eran ~30.000). También está en el prototipo, solo en la disposición 4, y publicado en su artefacto (`https://claude.ai/artifact/GCa5iSJfY4h5rEShCquCPp`).
- Comprobado: dos ejecuciones dan el mismo `.glb`; el script falla con mensaje claro si falta una pieza del contrato (probado con una copia que pedía una inventada) o si los ejes no cuadran (morro −4,750, sonda −5,480, arco 0,456); pantallas, pilotos y placa miden lo de la tabla; el `.glb` y el panorama de VECTOR no cambian.
- En las capturas, pantallas, pilotos y placa salen negros (los pinta Godot) y el cristal HUD se oculta, porque sin el modo suma es un rectángulo negro. Salen más oscuras que el prototipo, que tiene la ciudad iluminada.
- Inventario para la etapa 2: el ojo de BLADE irá a 0,90 m de la pista (`Eye.Y` = −1,10; VECTOR −0,55, con la nave a 2 m); `CockpitFraming.PitchDegrees` ya existe; `Cockpit3DView`, `Cockpit3DPieces` y `CockpitManifest.Missing` están escritos para VECTOR (paleta de carreras, dos tipos de pantalla, shift lights) y hay que generalizarlos; la Burbuja 2D es `BladeCockpit.cs` más su parte en `CockpitTests`. La spec corregida: F6 (no F7) para las naves de prueba y HAMMER con su −8° de siempre.

## Cabina 3D de BLADE: etapa 2, Godot

(spec `docs/cabinas-3d-2-blade.md` v1.0, rama `feature/cockpit-3d-blade`; la versión sube a 0.14.5 en la etapa 3): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado: BLADE en cabina lleva la cabina 3D militar, con pantallas de prueba (`SPD`, `RADAR`, `MAP` y `HUD` en el cristal) y el HUD de pantalla todavía visible. La Burbuja 2D ya no se usa (`BuildCockpit` sigue en el perfil hasta la etapa 3). VECTOR y HAMMER no cambian. Sustituye a lo que las secciones anteriores dicen del ojo de BLADE (0,279 m, FOV 80, −8°) y de que `Cockpit3DView` sea solo de VECTOR.

- **`Cockpit3DView` ya no sabe de ninguna nave**: piezas, pantallas, luces, colores de escena, factor de la luz de daño (3 en VECTOR, 1,2 en BLADE) y fuente salen de `Cockpit3DLayout`. Las pantallas tienen contenido (`RacingLeft`, `RacingRight`, `Radar`, `Minimap`, `Test`); las de prueba y los textos fijos se pintan una vez (`UpdateMode.Once` al enseñarse). El manifiesto aplica reflejos por material (sin poder pasar de 1 en `Metallic` ni `MetallicSpecular`) y transparencias sin escribir profundidad.
- Ojo de BLADE a 0,90 m (`Eye.Y` = −1,10), FOV 70 e inclinación −0,10 rad (−5,73°), en las dos cámaras: la de la cabina 3D la suma a su movimiento con `CockpitCameraMotion.Pitched`. `CockpitFraming.PitchRadians` es la única conversión que usan las dos.
- Luces: **las del Inspector de VECTOR**, porque los dos prototipos montan las mismas; cambian los colores de escena (ambiente `#1C221C`, pantallas `#CFFFC0`, boost `#FFA040`, daño `#FF4D3A`) y la luz de cada pantalla (`LargeScreenLight` × 0,1 × área / 0,017: 0,14 el radar, 0,086 las laterales). La spec pedía ajustarlas a ojo con su propio grupo del Inspector; el usuario lo dio por bueno sin tocarlas. No hay grupo de BLADE.
- Pilotos MASTER y placa: `SubViewport` de 256 × 112 / 256 × 32 con su texto en Share Tech Mono (negrita = `VariationEmbolden` 0,6). Pilotos sin luz y apagados (color × 0,12); la placa usa el material del modelo con el texto como color.
- **Trampa ya resuelta**: el cristal HUD va en suma, pero su lienzo con fondo negro opaco tapaba el mundo, porque la cabina se junta con él por alfa. Su `SubViewport` lleva fondo transparente y la imagen de prueba no pinta fondo. Godot no permite quitar el tonemap a un solo material, así que el cristal HUD sí lo lleva (en el prototipo no).
- Comprobado: VECTOR y HAMMER idénticos al píxel antes y después (cámara exterior y cabina, TIME TRIAL con `--fixed-fps 60`). F10 pasa VECTOR → BLADE 3D → HAMMER 2D → VECTOR en plena carrera. BLADE en cabina a 144 Hz: ningún frame lento en 6 s y peor frame 11,5 ms (otra pasada, uno de 19 ms; VECTOR también da uno suelto de ~20 ms). No se ha probado en un PC flojo.
- Límites conocidos: las luces de posición del morro salen casi blancas con el glow.

## Cabina 3D de BLADE: etapa 3, HUD y luces

(spec `docs/cabinas-3d-2-blade.md` v1.0, rama `feature/cockpit-3d-blade`, versión 0.14.5): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que la etapa 2 dice de las pantallas de prueba, del HUD de pantalla visible en la cabina de BLADE y de que `BuildCockpit` siga en su perfil: la Burbuja 2D (`BladeCockpit`) ya no existe y su parte de la captura `cockpit_reference.json` tampoco; su diseño sigue en `designs/`. En la cabina de BLADE, velocidad, boost, daño, radar y minimapa salen en sus pantallas y no en el HUD; en la cámara exterior, como siempre.

- Pantallas, de `drawCombiner`, `drawSpeed3`, `drawRadar3` y `drawMap3` del prototipo, en Share Tech Mono: **cristal HUD** (cinta de rumbo, cinta de velocidad a 1,7 px por km/h con su caja, marcador de trayectoria × 34 px, galones de boost y `TOP`), **velocidad** (número, escala con la línea de la punta, celdas de boost y columna de daño), **radar** (el de siempre con `RadarStyle.MilitaryScreen`: centro (256, 236), radio 160, diagonales; encima, cinta de rumbo, rótulos 40 / 80 / 120, `TGT`, `NRST` y los botones) y **mapa** (`MinimapStyle.MilitaryScreen`, ventana en (150, 182) con radio 128, `450 M` y la barra de progreso con `LAP n/m` y el porcentaje). Cristal HUD y velocidad solo se pintan si cambian; lo vivo del radar y del mapa, cada frame en la vista de cabina.
- Datos nuevos de `CockpitHudInput`: rumbo (0 hacia −Z, en sentido horario), giro (−`Dynamics.Steer`: la pantalla gira positivo a la derecha), texto de la vuelta (el mismo del HUD, que `Main` pasa con `Player.SetCockpitLap`) y lo recorrido de la vuelta (`Player.TrackDistance` / `Track.LapLength`).
- Colores militares con el estado de siempre (`CockpitHudState` con `MilitaryPalette.Role` y `Look`): carga llena en fósforo y cooldown en ámbar gracias a `FullOrbColor` y `CooldownOrbColor`, que en VECTOR (`RacingPalette.Look`) están puestos en naranja a propósito (sin ponerlos, el enum valdría cian). El apagado tenue (dim) es el rol `PanelDark`.
- Pilotos MASTER (`MasterLamps`): CAUTION × 2,2 con daño ≥ 1, WARN × 2,6 con daño 3 y `BlinkOn`, si no × 0,12; en TIME TRIAL apagados. El brillo de las pantallas de BLADE es `ScreenBrightness` × (0,8 + 0,4 × brillo) (`BrightnessFollowsGlow`); el de VECTOR sigue fijo.
- Diferencias con el prototipo: galones y triángulos sin halo (los rellenos no lo llevan); del triángulo de la punta en la cinta solo asoma la punta, porque el prototipo lo recorta; con daño 3 parpadea también el borde de los bloques, no solo el relleno (el estado es común a todas las cabinas).
- Comprobado: VECTOR y HAMMER idénticos al píxel al commit de la etapa 2, también conduciendo con el piloto, naves de prueba y radar vivo (TIME TRIAL, `--fixed-fps 60`, seis capturas). BLADE conduciendo en cabina, 8 s a 144 Hz: ningún frame lento y peor frame 7,8 ms, en RACE y en TIME TRIAL. Con daño solo lo ha visto el usuario: el piloto no choca.

## Cabina 3D de HAMMER: etapa 1, Blender

(spec `docs/cabinas-3d-3-hammer.md` v1.0, rama `feature/cockpit-3d-hammer`; la versión sube a 0.14.6 en la etapa 3): hecha y validada por el usuario mirando las capturas y el `.blend`. Para el jugador no cambia nada: el juego aún no usa el modelo (HAMMER sigue con el Búnker 2D).

- `art/blender/cockpit_hammer.py` traduce `buildCockpit()` con `VARIANT = 2` (COLGANTE) de `designs/cabina-3d-hammer.html`; las disposiciones 1 MANÓMETROS y 3 OPERARIO se quedan como diseño. Estilo INDUSTRIAL: chapa pintada atornillada, acero gastado, chapa lagrimada, franjas, cables y tubería.
- **El prototipo de HAMMER usa las mismas piezas `B`, el mismo ruido y el mismo `buildEnv` que el de BLADE** (comparado con `diff`): reutiliza `lib/blade_builder.py` y el ruido de `lib/textures_military.py` sin tocarlos, y **el panorama de BLADE** (`cockpit_blade_env.png`, apuntado en el campo `environment` del manifiesto). Lo nuevo: `lib/textures_industrial.py` (`scratch_map`, `plate_set` ×3, `steel_set`, `tread_set`, `hazard_set`; su franja no es la de BLADE) y, en `lib/geometry.py`, `torus` (con arco) y `circle`.
- Salida en `assets/cockpits/`: `cockpit_hammer.glb` (41.030 triángulos, 5,1 MB) y `cockpit_hammer.manifest.json` (39 piezas del contrato, formato de BLADE: reflejos `M_PaintNose` 0,6, `M_CrtGlass` 1,4, `M_Glass` 1,3; suma `M_GlassSmudge` 0,06; transparencias). Godot saca 19 texturas como `cockpit_hammer_T_*.png`; las sueltas de Blender van en `art/blender/out/textures/hammer/`.
- Se exporta en reposo: setas con la carga llena (`#FFB000` × 1,7), balizas apagadas (× 0,05), acentos 1,4, balizas del morro y del canard 3, LEDs del panel 1,5. Cada luz viva, pantalla, rótulo y esfera lleva su material (`M_Lamp_Lamp_Boost_1`, `M_Beacon_Beacon_Boost_Lens`, `M_Label_Plate`, `M_Label_Gauge_Hyd_Face`…): `M_Plate` es la chapa oliva, no la placa. Los rótulos se renombran (`Label_Boost_1…3`, `Label_Aux`). `Plate` cuelga de `Frame`, no de `Header_Plate`, como en BLADE.
- Concreta la spec: jaulas de las balizas y abrazaderas de la tubería sin sombra, porque en el prototipo son mallas sueltas (sin `castShadow`) y sin `boxUV`.
- Comprobado: las siete texturas idénticas byte a byte a las del prototipo (todos los píxeles, ejecutando sus generadores con Node); ejes (morro z −2,550, canard ±2,500 / −1,120, bloque colgante (0, 0,205, −0,535), larguero y 0,370); dos ejecuciones dan el mismo `.glb`; falla con mensaje claro si falta una pieza del contrato; `.glb` de VECTOR y BLADE y panorama sin cambios. Comparado con el prototipo desde el ojo: todo en su sitio.
- En las capturas, pantallas negras (las pinta Godot), setas sin halo y un punto de luz de cada pantalla en el cristal de su CRT: es lo que avisa la spec (4.3) y se resuelve en Godot.
- Inventario para la etapa 2: el ojo de HAMMER pasa de 2,63 m, FOV 80 y −8° (Búnker 2D) a 2,03 m (`Eye.Y` = 0,03), FOV 70 y −4,9°; `Cockpit3DView` ya es genérico, pero faltan luces de estado con color cambiante (setas y balizas; `MasterLamps` solo cambia la intensidad) y ecos y naves cuadrados en `RadarDrawing` y `MinimapDrawing` (hoy son discos); la fuente VT323 no está en el proyecto; los tintes de los bordes ya los pinta `Cockpit3DView`, pero su textura sale de `CockpitPainter.TextureOf`. La spec dice `design/` (es `designs/`) y F7 para las naves de prueba (es F6).

## Cabina 3D de HAMMER: etapa 2, Godot

(spec `docs/cabinas-3d-3-hammer.md` v1.0, rama `feature/cockpit-3d-hammer`; la versión sube a 0.14.6 en la etapa 3): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado: HAMMER en cabina lleva la cabina 3D industrial, con pantallas de prueba (`RADAR`, `HULL`, `MAP` en el bloque colgante y `SPD` en la tira) y el HUD de pantalla todavía visible (`ScreensShowHud` en false). El Búnker 2D ya no lo usa ninguna nave (`BuildCockpit` es null en las tres; `HammerCockpit` y sus pruebas siguen hasta la etapa 3, que las construyen directamente). VECTOR y BLADE no cambian. Sustituye a lo que las secciones anteriores dicen del ojo de HAMMER (0,63 m, FOV 80, −8°).

- Ojo de HAMMER a 2,03 m (`Eye.Y` = 0,03, `Eye.Z` = −1,2), FOV 70 e inclinación −0,085 rad (−4,9°), en las dos cámaras de cabina. Panorama: `cockpit_blade_env.png`.
- `Cockpit3DView` sigue sin saber de ninguna nave. Gana el contenido `Test` (imagen de prueba pintada una vez, con su rótulo en `Cockpit3DScreen.TestLabel`), las esferas de los relojes (`Cockpit3DLayout.Gauges`: `HYD` al 62 % y `TEMP` al 38 %, sobre el material del modelo como la placa) y, en `Cockpit3DScene`, color de la lámpara, fuerzas de hemisferio y ambiente y factores de la luz principal y de la lámpara sobre el Inspector. Por defecto valen lo de VECTOR (0,6 / 0,45, factor 1, lámpara del color de la principal): **VECTOR y BLADE salen idénticos al píxel** al commit de la etapa 1 (cabina parada, conduciendo y exterior, TIME TRIAL, `--fixed-fps 60`).
- Textos fijos como `labelPlane`: lienzo de 256 px de ancho y alto max(16, redondeo(256 × alto / ancho)), letra del 62 % (placa 256 × 32 a 20 px, `BST n` 256 × 75 a 47, `Label_Aux` 256 × 17 a 11), Share Tech Mono, línea base en el centro + 1 + 0,3 × la letra (la de la placa de BLADE).
- **Luces ajustadas midiendo zonas contra el prototipo**: luz principal × 0,5 y lámpara × 3 sobre las del Inspector (`IndustrialKeyFactor`, `IndustrialLampFactor`). Con las proporciones de los prototipos (2,2 / 2,6 y 0,9 / 0,08) la cabina salía mucho más clara, sobre todo el salpicadero por la lámpara. Seis de ocho zonas quedan a ±2 niveles; el pilar derecho sale algo más claro y la franja bajo el bloque colgante, más oscura. Sin grupo propio en el Inspector, como BLADE: el usuario lo dio por bueno. Hemisferio 0,55 y ambiente 0,5, los del prototipo. Para medir: las zonas no pueden pisar el HUD de pantalla ni las letras de prueba.
- Las setas se ven con la carga llena (× 1,7 del manifiesto): algo más de halo que en el prototipo, que también las quema; se ajustan en la etapa 3. El punto de luz de cada pantalla en el cristal de su CRT no se ve: no hizo falta tocar la máscara de luces.
- Medido en HAMMER en cabina conduciendo, 10 s a 144 Hz: 144 FPS, ningún frame lento y peor frame 12,7 ms en dos pasadas (en otra, uno suelto de 17,6 ms). Sin vsync, 2,11 ms de media (Búnker 2D: 1,39 ms). No se ha probado en un PC flojo.
- Con el prototipo en el panel del navegador oculto, `requestAnimationFrame` no corre y no se puede leer su lienzo: se comparó con una captura del panel.

## Cabina 3D de HAMMER: etapa 3, HUD y luces

(spec `docs/cabinas-3d-3-hammer.md` v1.0, rama `feature/cockpit-3d-hammer`, versión 0.14.6): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec y las cabinas 3D: **ya no hay capa de cabina 2D**. Sustituye a lo que las secciones anteriores dicen de las pantallas de prueba de HAMMER, de su HUD de pantalla en cabina, del Búnker 2D y de toda la capa 2D (sus primitivas, tramos, textura común, `AddDetail`, aristas por cabina, `Offset` en la cámara exterior, `For2DCockpit` y los tonos `Palette.Cockpit*`): su diseño sigue en `designs/`. En la cabina de HAMMER, velocidad, boost, daño, radar y minimapa salen en sus pantallas y no en el HUD; en la cámara exterior, como siempre.

- Pantallas CRT, de `drawSpeed2`, `drawDamage2`, `drawRadar2` y `drawMap2` del prototipo, en **VT323** (`assets/fonts/VT323-Regular.ttf`, bajada del repositorio de Google Fonts con su `VT323-OFL.txt`): **tira** (48 segmentos encendidos = redondeo(48 × f), marca de TOP, número y texto del boost en crit / over / main), **casco** (tres sectores de abajo arriba, parpadeo 100 % / 25 % con daño 3, en TIME TRIAL apagados con borde dim y sin texto), **radar** (`RadarStyle.IndustrialScreen`: centro (192, 158), radio 112, ecos cuadrados de 6,05 / 7,7 px; `TGT` y la más cercana) y **mapa** (`MinimapStyle.IndustrialScreen`: ventana en (192, 146), radio 104, naves cuadradas; barra de la vuelta). Encima de todo, el efecto CRT (viñeta radial —textura hecha una vez por tamaño en `RacingScreenCanvas`, con `IndustrialScreenDrawing.VignetteAlpha`— y banda de 6 px a 60 px/s con el tiempo de la cabina). Las cuatro se repintan cada frame en la vista de cabina, porque la banda baja siempre. Rótulos y relojes siguen en Share Tech Mono (`Cockpit3DLayout.LabelFont`).
- Setas y balizas (`IndustrialLamps`, `Cockpit3DStatusLamp`): color e intensidad de su emisión según el estado; no titilan con el daño. **Setas ajustadas midiendo el halo contra el prototipo**: llena **1,25** y bloqueada **1,5** (la spec dice 1,7 y 2,0: con el glow de Godot daban el doble de halo); cooldown 0,6 y vacía 0,25. Balizas: ámbar 4 ± 1,5 × sen(20 t) con el boost; roja 5 fija con daño 2 y con el parpadeo con daño 3; apagadas 0,05. Grupo **Cabina 3D: HAMMER** del Inspector del `Player`.
- Capa 2D fuera, con el permiso del usuario: `CockpitLayer`, `CockpitDrawRun`, `CockpitPainter`, `CockpitSpriteAtlas`, `CockpitKit`, `HammerCockpit`, `CockpitPalette`, `CockpitElements`, `CockpitHudPieces`, `CockpitHudLook.Default`, `BuildCockpit`, `For2DCockpit`, `Palette.Cockpit*`, `CockpitTests` y `cockpit_reference.json`. Se quedan `CockpitHudState` (ahora siempre con paleta y `Look` del estilo; sus pruebas, en `CockpitHudStateTests` con los colores de VECTOR) y `CockpitColor`, recortado a los ocho roles que se usan (`PanelDark` pasa a ser `Dim`). Los tintes de boost y daño, con el mismo degradado de antes, en `CockpitTints`. `Cockpit3D` es obligatorio en el perfil y `CockpitViewRules` ya no tiene campo de la capa 2D (`For(view, screensShowHud)`).
- Comprobado: VECTOR y BLADE idénticos al píxel al commit de la etapa 2 en diez capturas (TIME TRIAL con `--fixed-fps 60`: cabina parada, tres cargas, boost con su tinte, piloto con naves de prueba y exterior). HAMMER en el juego: pantallas vivas, carga llena, boost (`ACTIVE`, seta gastada vacía, otras en cooldown, baliza ámbar) y, en RACE, daño 2 y 3 forzados con una sonda temporal. En cabina con naves de prueba, a 144 Hz: 144 FPS; sin vsync, 2,09 ms de media, igual que en la etapa 2; un frame suelto de ~20 ms cada dos pasadas, igual antes que ahora. No se ha probado en un PC flojo.
- Para pruebas automáticas: el piloto ignora el teclado, así que el boost se prueba sin él; para tener cargas o daño al momento, una sonda C# temporal con reflexión (`Player._physics.Boost.Reset` con `BoostInitialCharges` y `Main._damage.AddImpact`). Lo que se pone con `set()` desde GDScript en las exportaciones no sobrevive a la salida lanzada.
- Diferencias con el prototipo: la nave propia del radar y del minimapa es la flecha del juego (la de BLADE). Sin comprobar en el juego: el arco rojo de nave pegada (sí en tests).

## Casco 3D de VECTOR: etapa 1, Blender

(spec `docs/cascos-3d-1-vector.md` v1.0, rama `feature/hull-3d-vector`; la versión sube a 0.14.7 en la etapa 4): hecha y validada por el usuario mirando las capturas y el `.blend`. Para el jugador no cambia nada: el juego aún no usa el modelo (VECTOR sigue con su casco wireframe).

- `art/blender/hull_vector.py` traduce `buildHull()` con `VAR = 3` (NOCTURNA) y `SPAN = 2.6` de `designs/casco-3d-vector.html`, más la geometría de `addLivery()` (los dibujos de las calcomanías los pondrá Godot). La spec dice `design/` (es `designs/`). **El script es la fuente**: el `.blend` se regenera.
- Lo nuevo de `lib/`: `hull_geometry.py` (perfil Hermite del fuselaje, superelipse n = 1,7, piel, cúpula, tapa, ala NACA, deriva extruida con bisel, splitter, torno, octógonos de góndola y tobera, rectángulo redondeado, caja y tubo cerrado de la junta) y `textures_hull.py` (carbono ×2, pintura neutra, aluminio y titanio cepillados, ruido y máscaras de quemaduras, textura de prueba de las calcomanías). **Sus texturas usan el mismo ruido que BLADE y HAMMER** (`textures_military`, sin tocarlo). `blender_io.mesh_object` gana `uv2_scale` opcional (segunda capa `UV2`): sin él, las cabinas no cambian.
- Salida en `assets/ships/`: `hull_vector.glb` (2,9 MB, 23.588 triángulos sin librea y 27.392 con ella; 57 mallas), `hull_vector.manifest.json` y `hull_scorch_1/2/3.png` (gris). Medidas sin librea: z −4,00 → 3,31 (7,310 m), semiancho **2,634** (lo da el filo luminoso de la deriva), y −0,31 → 0,705.
- **Se aparta de la spec**: el panorama de reflejos es `cockpit_blade_env.png`, no `cockpit_env.png` (el `buildEnv` del prototipo del casco es el de BLADE, semilla 77; manda el prototipo). Como en las cabinas, lo que no está en el contrato va junto por zona, material y sombra (`Pod_L_M_Titanium`…). Las calcomanías comparten material por tipo (`M_Decal_Number`, `_Sponsor`, `_Checker`) y salen negras y opacas en el modelo.
- glTF lleva el barniz (`KHR_materials_clearcoat`, que Godot no lee), el color de la pintura como factor y la normal × 0,4; no lleva la iridiscencia, el factor 1,7 de rugosidad de `M_Paint` ni los reflejos × 1,8 de la cúpula: van en el manifiesto, con barniz, transparencias, materiales con quemaduras, piezas sin sombra, vacíos y caja con y sin librea.
- Comprobado: las 15 texturas y el ruido de las quemaduras, idénticos byte a byte al prototipo (todos los píxeles, ejecutando su código con Node); medidas de la sección 3.3 dentro de su margen (el script falla si no, o si falta una pieza del contrato); dos ejecuciones dan el mismo `.glb`; los `.glb` de las tres cabinas no cambian. Todas las calcomanías, derechas y sin espejo con la textura de prueba. No se ha comparado con el prototipo en el navegador del panel (no se pudo arrancar el servidor local); las capturas van con Eevee y Filmic, sin bloom.
- **Decisión del usuario**: llamas y estelas no van en las specs de los cascos (se harán aparte, con `efectos-velocidad`); en la etapa 2 no se colocan ni se achata la boca. Los vacíos `Exhaust_*`, `Trail_*` y `Smoke` siguen en el modelo y en el manifiesto.
- Orbitron ya está en el proyecto (variable, peso 900), aunque la spec dice que falta.

## Casco 3D de VECTOR: etapa 2, el casco en carrera

(spec `docs/cascos-3d-1-vector.md` v1.0, rama `feature/hull-3d-vector`): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado: VECTOR lleva el casco 3D (GRAFITO lisa) en la cámara exterior; en cabina no se dibuja, ni él ni sus luces. BLADE y HAMMER, el fantasma, las naves de prueba, el menú y el holograma siguen con su wireframe. Sustituye a lo que las secciones anteriores dicen del semiancho de VECTOR (3,4 m): el de colisión es **2,63 m**; el wireframe, que sigue en el código y en su captura, llega a 3,4.

- Llamas, estelas y achatado de la boca no entran (decisión del usuario, etapa 1).
- Luces del prototipo **entre π** (`Hull3DSettings`, grupo **Casco 3D** del Inspector del `Player`): Godot no divide la luz difusa entre π y three.js sí; con los mismos números los titanios salían quemados. El mundo de carrera va sin tonemap (lineal) y con el glow de siempre: el casco no tiene el ACES del prototipo.
- Reflejos: el entorno de la carrera gana el panorama de BLADE como cielo solo para reflejos (el fondo sigue de color) y la luz ambiente del hemisferio. No cambia la pista porque todo el mundo de carrera es unshaded. Se pone en diferido desde el casco, porque la `WorldEnvironment` la crea `Main` después del `Player`.
- Importación del `.glb` sin LOD ni luz horneada (`meshes/generate_lods=false`, `meshes/light_baking=0`).
- Comprobado con sondas: casco en la parrilla y conduciendo, nada en cabina, F10 VECTOR → BLADE → HAMMER → VECTOR. Rendimiento en cámara exterior, 10 s conduciendo, antes y después: 144 FPS, ningún frame lento, peor frame 7,8 ms; sin vsync, 1,34 ms de media en los dos. No se ha probado en un PC flojo.
- Equilibrio (spec 4.6): en `circuit_01` sin boost VECTOR pasa de 40,550 a 40,450; BLADE −0,117 y HAMMER +0,333. La regla 2 sigue en ✓ (BLADE 2, VECTOR 1, HAMMER 1). Resuelto en la etapa 3 subiendo el margen de la regla 1, sin tocar ninguna nave.

## Casco 3D de VECTOR: etapa 3, pinturas, libreas y daño

(spec `docs/cascos-3d-1-vector.md` v1.0, rama `feature/hull-3d-vector`; la versión sube a 0.14.7 en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario con la cámara orbital. Sustituye a lo que las secciones anteriores dicen del margen de la regla 1 del equilibrio (0,3 s).

- **Decisión del usuario, cambia la spec 6 de naves**: la regla 1 del equilibrio pasa de ±0,3 a **±0,5 s** (`BalanceRules.Rule1Margin`), porque con las alas más cortas de VECTOR, HAMMER quedaba a +0,333. Ninguna nave cambia.
- Catálogo (`HullPaintCatalog`): las 20 pinturas y 7 acabados del prototipo, lisas y con librea; por defecto GRAFITO lisa. No se elige en ningún menú ni se guarda. **F5** / **Mayúsculas + F5** (decisión del usuario: F6 es de las naves de prueba; acciones `debug_hull_paint_next` / `_previous`, con las teclas exactas) recorren las 40 en el panel F9, que enseña `PAINT GRAFITO #07 · 21/40 · F5`. Se mantiene con RESTART y al cambiar de nave con F10.
- Rugosidad final del acabado, sin textura (Godot no admite el factor 1,7 del prototipo). Con librea, derivas, franjas y filete en BRILLO con su color; las calcomanías se pintan una vez por pintura en un `SubViewport` y comparten textura los dos lados; color del texto oscuro si R + G + B > 360.
- **Se aparta de la spec**: la iridiscencia (perla, camaleón y cúpula) es una pasada aditiva que cambia el color con el ángulo de vista, más fuerte en los bordes, ajustada a ojo (`IridescenceOfPaint` 0,16, `IridescenceOfCanopy` 0,18; `HullIridescence` en el grupo **Casco 3D**). Las quemaduras no vuelven mate lo quemado (el detalle de Godot solo cambia el color): la máscara también apaga la iridiscencia.
- Daño (solo RACE): quemaduras con `hull_scorch_N.png` en el alfa de una textura casi negra, por la UV2, preparadas al cargar para no dar tirones; humo 7 / 20 por segundo con niveles 2 / 3; **parpadeo con las reglas de `danos`** (decisión del usuario: los valores del grupo **Daños (parpadeo)** del `Player`, no los del prototipo) sobre las 14 luces, bajando al 20 %. `Spaceship.EdgeCount` vale 14 con casco 3D, así F10 rehace bien el parpadeo.
- Brillo de levitación: plano de 3,2 × 7,5 m sobre la pista bajo la nave, con la inclinación de la pista, 0,32 ± 0,06 × `HullHoverGlow` (grupo **Casco 3D**), apagándose hasta 3 m por encima de la altura de vuelo.
- Comprobado con sondas: las 40 pinturas, librea legible y sin espejo en los dos lados, daño 3 con quemaduras y humo, 5 de 14 luces apagadas como mucho con daño 3, pausa que lo congela todo. En la cresta de TEST HILLS VECTOR apenas sube unos centímetros, así que el apagado del brillo en el aire solo lo cubre la prueba. El humo tras la recolocación no se ha comprobado: al usuario le da igual, porque quiere un sistema de daños visuales más profundo en el futuro.
- **Para después** (decisión del usuario): luz de frenado en los pilotos traseros, con la spec de efectos (`efectos-velocidad`).
- Cámara orbital de depuración (`DebugOrbitCamera`, **F4**, acción `debug_orbit_camera`): añadida para poder validar el casco, porque la cámara de persecución solo lo enseña por detrás.

## Casco 3D de VECTOR: etapa 4, holograma

(spec `docs/cascos-3d-1-vector.md` v1.0, rama `feature/hull-3d-vector`, versión **0.14.7**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que las secciones anteriores dicen de que el holograma es siempre wireframe, de su escala calculada con el casco de Vector y de las esferas (9,95 m de Vector como límite).

- SELECT SHIP enseña el casco 3D de VECTOR con GRAFITO lisa: el mismo `ShipHull3D` que en carrera, sin daño, humo ni brillo de levitación, y sin aristas lejanas tenues (eso sigue solo en el wireframe). Giro (`HologramSpin`) sin cambios. BLADE y HAMMER siguen con su holograma wireframe hasta sus specs de casco.
- Luces del casco fijas en el mundo del menú (respecto a la pantalla), el juego del prototipo girado media vuelta para que la principal llegue del lado de la cámara, que mira hacia −Z. Reflejos del mismo panorama. Sin ajustes propios en el Inspector: el usuario lo dio por bueno así.
- Escala fija: 480 px para los 6,8 m del VECTOR wireframe (`HologramFraming.ReferenceWidth`). VECTOR se ve más estrecho (unos 372 px) y se centra con la caja de su manifiesto. Esferas: VECTOR 9,07, BLADE 9,38, HAMMER 8,40; la prueba exige ≤ 10 m (`MaxSphereDiameter`).
- Comprobado con una sonda, del menú a SELECT SHIP sin pulsar START (para no tocar `settings.json`): VECTOR 3D girando, BLADE y HAMMER wireframe, vuelta a VECTOR 3D y `v0.14.7` en el menú.

## Casco 3D de BLADE: etapa 1, Blender

(spec `docs/cascos-3d-2-blade.md` v1.0, rama `feature/hull-3d-blade`; la versión sube a 0.14.8 en la etapa 4): hecha y validada por el usuario mirando las capturas y el `.blend`. Para el jugador no cambia nada: el juego aún no usa el modelo (BLADE sigue con su casco wireframe).

- `art/blender/hull_blade.py` traduce `buildHull()` con `VAR = 3` (ASALTO) y `LIV = true` de `designs/casco-3d-blade.html`, con la geometría de las marcas (los dibujos los pondrá Godot). Las variantes 1 FURTIVO y 2 NAVAL se quedan como diseño. **El script es la fuente**: el `.blend` se regenera.
- Lo nuevo de `lib/`: `hull_blade_geometry.py` (fuselaje de BLADE con sección de lente p = 1,2, `skin_grid`, `skin_ribbon`, cúpula burbuja, carenado, puntas traseras y base de las placas) y `textures_hull_blade.py` (`camo_set`, `camo_mask`, `hull_hazard_set`, `hull_antislip_set`; el prefijo `hull_` los separa de los de la cabina de BLADE, que son otro algoritmo). Panza y metales con `paint_set` y `brushed_set` de VECTOR. `hull_geometry.extrude` gana `capped_bevel` (tope de inglete √2 de three.js, sin él la punta trasera salía 1,6 cm más larga); VECTOR no lo usa, así que su deriva no cambia aunque en una esquina se aparta unos milímetros de su prototipo.
- Salida en `assets/ships/`: `hull_blade.glb` (6,1 MB, 28.872 triángulos sin marcas y 29.416 con ellas; el prototipo da ~30.300 / 30.900 porque cuenta los triángulos de tamaño cero que aquí se quitan), `hull_blade.manifest.json` y las máscaras `hull_blade_camo_mask.png` / `hull_blade_armor_mask.png`. Quemaduras: las de VECTOR. Godot saca 21 texturas como `hull_blade_T_*.png`. Medidas: z −5,20 → 3,608 (8,808 m), sonda a −5,75, semiancho **1,612**, y −0,234 → 0,519; esfera con sonda **9,927 m**.
- **El camuflaje es una máscara**: R = tono (0 base, 128 claro, 255 oscuro), G = desconchón, B = grano ((k − 0,9) / 0,2). En el `.glb`, `M_Paint` y `M_PaintArmor` llevan la máscara como color; Godot pondrá el de la pintura (en la etapa 3, con las máscaras sueltas y filtro más cercano, porque la compresión del `.glb` rompería los bordes duros). En las capturas y el `.blend`, BOSQUE ya coloreado.
- Manifiesto con el formato del de VECTOR (`bounds` = sin marcas y con sonda, la que usa el holograma; `boundsWithLivery` con marcas; `livery` = el grupo `Markings`) y además `tips`, `armor`, `bottom`, `navLights`, `strobes`, `flickerLights` (las 9 que titilan), `probe`, `camoMasks` y `repeats` (la punta pasa de `M_Hazard` ×3 a `M_Paint` ×0,35 sin marcas: Godot tendrá que cambiar la escala de las UV). La anticolisión se exporta apagada (intensidad 0).
- **Decisión del usuario, cambia el prototipo y la spec**: la placa de blindaje 0 pasa de z −3,4 a **−2,95**, porque tapaba la mitad trasera del número del morro (también en el prototipo).
- Comprobado: las 8 texturas idénticas byte a byte al prototipo (ejecutando su código con Node) y la máscara reconstruye el color a ±1; medidas de la sección 3.3 y los cinco vacíos dentro de su margen (el script falla si no, o si falta una pieza del contrato); dos ejecuciones dan el mismo `.glb`; el `.glb` de VECTOR no cambia. Calcomanías con la textura de prueba derechas y sin espejo. Comparado con el prototipo en el navegador del panel (abre como `file://`).
- Se aparta de la spec o la concreta, para las etapas siguientes: pinturas con **F5** (la spec dice F6, que son las naves de prueba); **sin llamas ni achatado de la boca** (no hay efectos de velocidad; decisión de VECTOR), solo los vacíos; el número `B-02` **opaco**, como el prototipo (la spec dice 90 %); Black Ops One no está en el proyecto y hay que bajarla en la etapa 3 (OFL). La bola de sensores no toca la pista: la nave flota a 2 m.

## Casco 3D de BLADE: etapa 2, el casco en carrera

(spec `docs/cascos-3d-2-blade.md` v1.0, rama `feature/hull-3d-blade`; la versión sube a 0.14.8 en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado: BLADE lleva el casco 3D ASALTO (BOSQUE con marcas) en la cámara exterior; en cabina no se dibuja. HAMMER, el fantasma, las naves de prueba y el menú siguen con su wireframe. Sustituye a lo que las secciones anteriores dicen del semiancho de BLADE (1,6 m): el de colisión es **1,61 m**; el wireframe sigue llegando a 1,6.

- **`ShipHull3D` ya no sabe de VECTOR**: las piezas, los vacíos y las luces que titilan salen del contrato del estilo de su perfil (`HullContract.For`); la iridiscencia (cúpula de VECTOR, bola de sensores de BLADE, 0,18 × el factor del manifiesto) y el color de cada luz, del manifiesto. La pintura de VECTOR sigue igual; la de BLADE va aparte. VECTOR sale **idéntico al píxel** al commit de la etapa 1 (cinco capturas con `--fixed-fps 60`: parrilla, exterior, cabina y orbital).
- **Se aparta de la spec**: el camuflaje no es un shader. El color se hornea una vez en la CPU desde la máscara sin pérdidas (`hull_blade_camo_mask.png`, `MilitaryHullPaint.BakeCamo`) en una textura con mipmaps, compartida por las instancias con la misma pintura: mismo píxel que el prototipo (±1) y sin centelleo de lejos. Así el material sigue siendo `StandardMaterial3D` y las quemaduras irán con el detalle, como en VECTOR. Panza del color de `botOf` con rugosidad fija 0,65 (la textura × 2,6 del prototipo no cabe en Godot).
- **Decisión del usuario, cambia el prototipo y la spec**: suelo de la cabina (`Canopy_Floor`, `M_CanopyFloor`), con la planta de la cúpula, 1,5 mm sobre la piel y sobre el arranque del carenado, sin pasar de 1 mm bajo el cristal, siempre del **tono base de la pintura, liso**: el camuflaje no se ve a través del cristal. El contrato pasa a 40 piezas y el casco a 32.148 / 32.692 triángulos.
- Brillo de levitación de 3 × 9 m centrado 0,7 m por delante (`HoverGlowShape` en el perfil). Importación del `.glb` sin LOD ni luz horneada, como VECTOR. Sin llamas ni estelas (no hay efectos de velocidad).
- **El holograma de SELECT SHIP ya enseña el casco 3D de BLADE**, porque usa el mismo código: sus pruebas pasan a 227,6 px de ancho y esfera de 9,93 m con la sonda. No se ha mirado en el menú. La etapa 4 queda casi en comprobarlo y subir la versión.
- Todavía no: F5 recorre las pinturas de VECTOR y en BLADE no cambia nada (el panel enseña esos nombres); las calcomanías no se ven (falta Black Ops One); la anticolisión está apagada. El daño visible usa el código común y debería funcionar en RACE, sin probar. Todo eso, en la etapa 3.
- Equilibrio en `circuit_01` sin boost: BLADE 40,333 por vuelta antes y después; total de 3 vueltas +0,017 s. Reglas 1 y 2 en ✓. Rendimiento sin medir.
- Para pruebas automáticas: una ventana del juego puede recibir teclas de fuera mientras se abre (salió una captura con la pausa abierta); la sonda imprime `GetTree().Paused` para detectarlo.

## Casco 3D de BLADE: etapa 3, pinturas, marcas, luces y daño

(spec `docs/cascos-3d-2-blade.md` v1.0, rama `feature/hull-3d-blade`; la versión sube a 0.14.8 en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que la etapa 2 dice de que F5 no cambia BLADE, de las calcomanías invisibles y de la anticolisión apagada.

- Catálogo (`MilitaryPaintCatalog`): las 20 pinturas del prototipo (10 camuflajes y 10 lisas; NEGRO OPERACIONES y GRIS NAVAL en SATINADO, el resto MATE), primero las 20 sin marcas y luego las 20 con marcas; por defecto BOSQUE con marcas (la 21). No se elige en ningún menú ni se guarda.
- **F5 / Mayúsculas + F5 recorren las 40 de la nave que se conduce**: cada estilo de casco guarda su combinación (`HullPaints`, índices; `Player.SetHullPaint(estilo, índice)`), que dura hasta cerrar el juego, también con F10 y RESTART. El panel enseña `PAINT BOSQUE · MARCAS · 21/40 · F5`, y `PAINT —` en HAMMER.
- Con marcas se ven `B-02` (Black Ops One de 230 px, **opaco** como el prototipo) y el distintivo inventado, en `#141612` sobre las pinturas claras y `#DEDECE` sobre las oscuras, y las puntas llevan las franjas de aviso; sin marcas, ni calcomanías y las puntas con la pintura. Cada punta tiene una copia de la pintura con `Uv1Scale` = repetición de la pintura / la de las franjas (0,35 / 3), porque sus UV vienen horneadas para las franjas.
- **Black Ops One** (`assets/fonts/BlackOpsOne-Regular.ttf`, 163 KB, con `BlackOpsOne-OFL.txt`), bajada con el permiso del usuario del repositorio de Google Fonts.
- Anticolisión: 9 de intensidad durante 0,06 s cada 1,2 s, la de abajo 0,6 s después; con el tiempo de la nave, así la pausa la congela; no titila con el daño; el holograma no la mueve. Periodo, duración, desfase e intensidad en el grupo **Casco 3D** del `Player` (`HullStrobe*`).
- Daño: el de VECTOR, con las 9 luces del contrato de BLADE, el humo desde `Smoke` y las quemaduras con el detalle de Godot por la UV2, también en el camuflaje y en las puntas pintadas. **Se aparta de la spec**: lo quemado solo se oscurece, no se vuelve mate.
- Comprobado con sondas: seis pinturas (BOSQUE, DESIERTO, ÁRTICO y NEGRO OPERACIONES con marcas; NEGRO OPERACIONES y BOSQUE sin ellas), puntas con y sin franjas, y RACE con daño 3 (quemaduras y humo). VECTOR idéntico al píxel al commit de la etapa 2, también con GRAFITO con librea. La anticolisión solo en tests: en las capturas no se ve destellar.
- No entra: intensidades de las luces de posición, cola, formación y toberas en el Inspector (salen del manifiesto), y el × 1,6 de las toberas con boost (VECTOR tampoco lo tiene).

## Casco 3D de BLADE: etapa 4, holograma

(spec `docs/cascos-3d-2-blade.md` v1.0, rama `feature/hull-3d-blade`, versión **0.14.8**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que las secciones anteriores dicen de que BLADE sigue con su holograma wireframe y de su esfera de 9,38 m.

- SELECT SHIP enseña el casco 3D de BLADE con BOSQUE con marcas (`B-02` y distintivos legibles): el mismo `ShipHull3D` que en carrera, con las luces fijas en el mundo del menú y los reflejos del holograma de VECTOR, sin daño, humo, brillo de levitación ni anticolisión (el menú no llama a `UpdateEffects`, así que las luces se quedan con la intensidad 0 del modelo). No hizo falta código nuevo: ya lo enseñaba desde la etapa 2.
- Escala fija de siempre: BLADE a 227,6 px de ancho y esfera de **9,93 m con la sonda** (sin ella, 9,41), por debajo de los 10 m de `MaxSphereDiameter`. HAMMER sigue con su holograma wireframe hasta su spec.
- Comprobado con una sonda, del menú a SELECT SHIP sin pulsar START (`settings.json` sin cambios): BLADE 3D girando, VECTOR 3D, HAMMER wireframe, vuelta a BLADE 3D y `v0.14.8` en el menú. Arrastre e inercia, sin cambios y sin probar en esta etapa.

## Casco 3D de HAMMER: etapa 1, Blender

(spec `docs/cascos-3d-3-hammer.md` v1.0, rama `feature/hull-3d-hammer`; la versión sube a 0.14.9 en la etapa 4): hecha y validada por el usuario mirando las capturas y el `.blend`. Para el jugador no cambia nada: el juego aún no usa el modelo (HAMMER sigue con su casco wireframe).

- `art/blender/hull_hammer.py` traduce `buildHull()` con `VAR = 1` (MINERO) y `LIV = true` de `designs/casco-3d-hammer.html`, con la geometría de las marcas (los dibujos los pondrá Godot). Las variantes 2 REMOLCADOR, 3 DEMOLICIÓN y 4 FUNDICIÓN se quedan como diseño. **El script es la fuente**: el `.blend` se regenera. `docs/casco-3d-hammer.html` es una copia exacta del prototipo.
- Lo nuevo de `lib/`, sin tocar nada de lo que ya había: `hull_hammer_geometry.py` (fuselaje en superelipse n = 3,2 con cara roma, piel, canard facetado, `slab`, `profile_extrude`, `poly_tube`, `beam`, `rbox`, `cyl`) y `textures_hull_hammer.py` (la pintura como máscara). Chapa oscura, acero, chapa lagrimada y franjas son los de la cabina de HAMMER (`textures_industrial`, semillas 82–85); titanio y cobre, el `brushed_set` de VECTOR (32 y 33). Quemaduras: las de VECTOR.
- **La pintura es una máscara**: `color = pintura × MUL × 1,25 + ADD`, con `hull_hammer_body_mul/add.png` (semilla 87: casco, canard, puntas caídas, aletas y compartimento) y `hull_hammer_cab_mul/add.png` (semilla 89: cabina). Los ruidos de `plate_layers` repiten las llamadas de `plate_set` sin tocarlo. En el `.glb`, los `M_Paint*` y `M_PaintCab` llevan MUL como color; en las capturas y el `.blend`, AMARILLO OBRA ya coloreado.
- Salida en `assets/ships/`: `hull_hammer.glb` (6,5 MB, 83 mallas; 66.538 triángulos sin marcas y 66.648 con ellas: la spec da ~1.360 más porque el prototipo cuenta sus llamas y estelas), `hull_hammer.manifest.json` y las cuatro máscaras. Medidas: z −3,829 → 3,00 (6,83 m), semiancho **2,502**, y −0,52 → 1,353 (1,604 sin los extras del techo), esfera **8,67 m**, iguales a las del prototipo.
- Manifiesto con el formato del de BLADE y además `paintedBody`, `paintedCab`, `paintMasks`, `leadingEdges` (con marcas `M_Hazard`; sin ellas `M_PlateDark` con escala de UV 0,4167, porque sus UV vienen horneadas para las franjas), `amberLights`, `tailLights`, `workLights`, `beacons` (lentes de las balizas del techo, apagadas a 0,05), `flickerLights` (las 12 que titilan), `livery` (13 piezas que solo se ven con marcas: las calcomanías, `Fin_*_Hazard` y `EngineBay_Hazard`) y `boundsWithoutRoofExtras`. Balizas y focos son grupos (`Beacon_*`, `Work_*`) dentro de `Cab`.
- **Se aparta de la spec**: las piezas extruidas llevan las UV del prototipo (las de `ExtrudeGeometry`), no `boxUV`, para que la chapa quede igual. Jaulas de las balizas y abrazaderas de las tuberías sin sombra, como en el prototipo. **Sin llamas ni achatado de la boca** (decisión de VECTOR), solo los vacíos. Para las etapas siguientes: pinturas con **F5** (la spec dice F6, que son las naves de prueba).
- Comprobado: las 8 texturas idénticas byte a byte al prototipo (todos los píxeles, ejecutando su código con Node); MUL y ADD iguales a ±7·10⁻⁶ y el color reconstruido de las máscaras de 8 bits a ±1 con AMARILLO OBRA y BLANCO FLOTA; medidas de la sección 3.3 y los cinco vacíos dentro de su margen (el script falla si no, o si falta una pieza del contrato); dos ejecuciones dan el mismo `.glb`. Calcomanías con la textura de prueba derechas y sin espejo. Comparado con el prototipo en el navegador del panel (abre como `file://`).
- Inventario para la etapa 2: `ShipHull3D` tiene ramas por estilo (`Racing`, `Military`); HAMMER necesita un tercer estilo con su aplicador, y se propone hornear el color de las máscaras en la CPU, como el camuflaje de BLADE. Los efectos de velocidad no existen, así que el color de la llama por nave no se puede aplicar. Los patines (y −0,52) no tocan la pista: la nave flota a 2 m. Ancho de colisión: sigue en 2,50 m.

## Casco 3D de HAMMER: etapa 2, el casco en carrera

(spec `docs/cascos-3d-3-hammer.md` v1.0, rama `feature/hull-3d-hammer`; la versión sube a 0.14.9 en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Cambio visual buscado: HAMMER lleva el casco 3D MINERO (AMARILLO OBRA con marcas) en la cámara exterior; en cabina no se dibuja. Ya las tres naves llevan casco 3D en carrera; el fantasma, las naves de prueba y el menú siguen con su wireframe. Sustituye a lo que las secciones anteriores dicen de que HAMMER no tiene casco 3D.

- Estilo nuevo `HullStyle.Industrial` y su contrato en `IndustrialHull.cs` (`IndustrialHullPieces`: 45 piezas, 12 luces que titilan; ni las balizas del techo ni las toberas). `HullContract.For` y `HullPaints` ya distinguen los tres estilos. `HullManifest` lee lo nuevo de HAMMER (`PaintedBody`, `PaintedCab`, `LeadingEdges`, `Beacons`, `PaintMasks`, `BoundsWithoutRoofExtras`…).
- **Pintura**: `ShipHull3D` hornea una vez en la CPU el color del casco y el de la cabina desde sus máscaras (`IndustrialHullPaint.Bake`: pintura × MUL × 1,25 + ADD, redondeo a par) en una textura con mipmaps, compartida por las instancias con la misma pintura, como el camuflaje de BLADE. Rugosidad y normal, las del modelo. Por ahora, AMARILLO OBRA en casco y cabina; F5 tiene una sola pintura en HAMMER (`PAINT AMARILLO OBRA · MARCAS · 1/1`) hasta la etapa 3. Las calcomanías quedan invisibles hasta que se dibujen (etapa 3); las franjas de aletas, compartimento y borde del canard sí se ven.
- Perfil: panorama de BLADE, brillo de levitación de 3,4 × 7,4 m centrado en z −0,5, ancho de colisión **2,50 m** (el manifiesto da 2,502, al centímetro igual que el wireframe) y factor de cámara 0,92 sin cambios. Importación del `.glb` sin LOD ni luz horneada; Godot saca 24 texturas como `hull_hammer_T_*.png`. Sin llamas ni estelas (no hay efectos de velocidad), y por tanto sin color de llama por nave.
- **El holograma de SELECT SHIP ya enseña el casco 3D de HAMMER**, porque usa el mismo código: sus pruebas pasan a 353,2 px de ancho y esfera de 8,67 m. No se ha mirado en el menú: la etapa 4 queda casi en comprobarlo y subir la versión.
- Comprobado con sondas: parrilla, órbita F4, conduciendo con el piloto y en cabina (oculto con el código común). Rendimiento en cámara exterior conduciendo, 10 s por nave en la misma ejecución, a 144 Hz: 144 FPS y ningún frame lento en las tres (peor frame ~10 ms); sin vsync, 1,39 ms de media HAMMER, 1,35 BLADE y 1,27 VECTOR. No se ha probado en un PC flojo. VECTOR y BLADE no se han comparado al píxel: sus ramas del código no cambian.
- Equilibrio en `circuit_01` sin boost, igual que antes: BLADE −0,117, VECTOR 40,450, HAMMER +0,333. Reglas 1 y 2 en ✓.

## Casco 3D de HAMMER: etapa 3, pinturas, marcas, balizas y daño

(spec `docs/cascos-3d-3-hammer.md` v1.0, rama `feature/hull-3d-hammer`; la versión sube a 0.14.9 en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que la etapa 2 dice de que F5 tiene una sola pintura en HAMMER y de las calcomanías invisibles.

- Catálogo (`IndustrialPaintCatalog`): las 20 pinturas del prototipo (15 lisas y 5 bicolor, con la cabina de otro color; todas con el mismo acabado), primero las 20 sin marcas y luego las 20 con marcas; por defecto AMARILLO OBRA con marcas (la 21). F5 / Mayúsculas + F5 las recorren (`PAINT NARANJA · CABINA BLANCA · MARCAS · 37/40 · F5`). No se elige en ningún menú ni se guarda.
- `IndustrialHullLook`: casco, cabina (`cabOf`), marcas y su color (`#141412` si R + G + B del casco > 360, si no `#E8E2D0`: oscuras en AMARILLO VIVO, BLANCO FLOTA, CREMA y BLANCO · CABINA ROJA). Con marcas se ven las 10 calcomanías y las franjas de aletas y compartimento, y el borde del canard lleva franjas; sin ellas, nada de eso y el borde con una copia de `M_PlateDark` con `Uv1Scale` 0,4167 (sus UV van horneadas para las franjas), como las puntas de BLADE.
- Calcomanías (`IndustrialDecalDrawing`: `03`, logotipo M·7 inventado, NO STEP, HOT y `HMR-03`), con el dibujo y las medidas del prototipo. **El pintor de calcomanías gana tres cosas**: `DecalShape.Outline` (polilínea cerrada con grosor; con `Radius` > 0, esquinas redondeadas con un círculo en cada vértice), `DecalAlign.Left` y `Erase` (el `!` vaciado del triángulo: va en un nodo aparte con `CanvasItemMaterial` en resta, que quita también el alfa), más `DecalFont.ShareTech` con `Bold` (`VariationEmbolden` 0,6). Los lienzos de VECTOR y BLADE no usan nada de eso y se pintan en un solo nodo como antes.
- Balizas del techo (`IndustrialHullBeacons`, `ShipHull3D.SetBeacons`): **la lógica de la cabina 3D de HAMMER** (`IndustrialLamps`) con el parpadeo a 2 Hz sacado del tiempo de la nave, así la pausa las congela; ámbar 4 ± 1,5 × sen(20 t) con el boost, roja 5 con daño 2 y parpadeando con daño 3, 0,05 apagadas y en TIME TRIAL; no titilan con el daño. **Se aparta de la spec**: sin grupo propio en **Casco 3D**; comparten los ajustes del grupo **Cabina 3D: HAMMER** (`Player.BuildIndustrialLampSettings`).
- Daño: el común (quemaduras con el detalle por la UV2, también en el borde pintado del canard; humo desde `Smoke`, en la boca del escape vertical; parpadeo de las 12 luces). Como en BLADE, lo quemado se oscurece pero no se vuelve mate. Brillo de levitación, el común con el tamaño del perfil.
- Comprobado con sondas (una C# temporal para el boost y el daño): cuatro pinturas, las cinco calcomanías leídas de su textura (con el `!` vaciado), balizas con boost (4,4–5,5), daño 2 (5) y daño 3 (5 / 0,05) y daño 3 con quemaduras y humo. Sin comprobar en imagen: el parpadeo de las luces, el brillo al saltar una cresta, la pausa con humo y balizas y que el humo salga justo de la boca del escape (la nave se movía).

## Casco 3D de HAMMER: etapa 4, holograma

(spec `docs/cascos-3d-3-hammer.md` v1.0, rama `feature/hull-3d-hammer`, versión **0.14.9**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec y los cascos 3D: **las tres naves llevan casco 3D en carrera y en el selector**. Sustituye a lo que las secciones anteriores dicen de que HAMMER sigue con su holograma wireframe y de su esfera de 8,40 m.

- SELECT SHIP enseña el casco 3D de HAMMER con AMARILLO OBRA con marcas: el mismo `ShipHull3D` que en carrera, con las luces fijas en el mundo del menú y los reflejos de los otros dos, sin daño, humo ni brillo de levitación; las balizas del techo se quedan apagadas (0,05 del modelo), porque el menú no llama a `SetBeacons`. No hizo falta código nuevo: ya lo enseñaba desde la etapa 2.
- Escala fija de siempre: HAMMER a 353,2 px de ancho y esfera de **8,67 m** (BLADE 9,93 con la sonda, VECTOR 9,07), por debajo de los 10 m de `MaxSphereDiameter`. Ninguna nave enseña ya el wireframe en el selector; el wireframe sigue para el fantasma, las naves de prueba y el fondo del menú.
- Comprobado con una sonda, del menú a SELECT SHIP sin pulsar START (`settings.json` sin cambios): HAMMER, BLADE y VECTOR en 3D, vuelta a HAMMER y `v0.14.9`. Arrastre e inercia, sin cambios y sin probar en esta etapa.
- Para una sonda del menú con `--script`: la escena principal no se carga sola; hay que abrir `res://scenes/MainMenu.tscn` con `change_scene_to_file`.

## Selector de pinturas: etapa 1, pintura guardada

(spec `docs/selector-pinturas.md` v1.0, rama `feature/paint-select`; la versión sube a **0.15.0**, minor por decisión del usuario, en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. Para el jugador no cambia nada hasta que haya una pintura guardada: aún no hay pantalla PAINT.

- Las 60 pinturas y los acabados ganan `ScreenName` en inglés (tablas de la spec, 3.4); `Name` sigue siendo el interno. `DisplayName` (panel F9) ya va en inglés: `GRAPHITE #07`, `WOODLAND · MARKINGS`, `ORANGE · WHITE CAB · MARKINGS`.
- `settings.json` gana `shipPaints` (`{ "ship_01": { "paint": "racing_red", "marks": true } }`), sin subir la versión de formato. Nave desconocida, pintura de otro catálogo o valores raros se ignoran sin dar el archivo por corrupto; `marks` ausente o no booleano = el de la pintura por defecto. Sin entradas, el campo no se escribe. RESET RECORDS no lo toca.
- `Main` lee los ajustes en los dos modos (antes solo en TIME TRIAL, en `SetupGhost`) y monta la nave con la pintura guardada de las tres naves, así F10 enseña la de la nave nueva. **F5 manda sobre la guardada** mientras dure el juego (decisión del usuario) y nunca guarda; el panel parte de la que lleva la nave. SELECT SHIP lee las pinturas cada vez que se abre.
- Las specs de los cascos dicen que las pinturas por defecto están en el Inspector: no lo están, salen de los catálogos. La spec dice `design/` (es `designs/`).
- Comprobado con sondas (archivos del usuario apartados y devueltos): VECTOR con RACING RED y librea en el holograma y en la parrilla, HAMMER con una pintura inexistente sale con la de por defecto sin avisos, BLADE con la suya.

## Selector de pinturas: etapa 2, pantalla PAINT

(spec `docs/selector-pinturas.md` v1.0, rama `feature/paint-select`, versión **0.15.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. El jugador elige la pintura de cada nave entre sus 40 combinaciones.

- SELECT SHIP: START, PAINT y BACK (PAINT desactivada con una COMING SOON). PAINT abre la pantalla encima con el mismo holograma y el mismo giro, sin rehacerlo; cada muestra pinta la nave al momento. APPLY guarda y BACK / ESC vuelve sin cambios; las dos vuelven con PAINT seleccionada y la pintura guardada. M (acción `menu_marks`) cambia lisa ↔ con marcas.
- Composición TALLER. **Se apartan de la spec, validado**: el pie empieza en x = 196 (la spec dice 110 porque en la maqueta la versión va en x = 28; en el juego va en x = 100) con el espaciado de la maqueta (0,08 em) y se reduce si no cabe; APPLY / BACK no se marcan mientras la selección está en las muestras (`OptionList.ShowSelection`; la maqueta marca APPLY siempre); el nombre de la pintura en Orbitron Medium (la spec), aunque la maqueta lo pone en 700. Nuevo color `Palette.PaintName` (`#E9F6FF`, de la spec) y colores `Paint*` de las muestras y la etiqueta; los de dentro de las muestras son de los catálogos.
- El nombre de la pintura toma un tamaño por nave (el más largo con la etiqueta más ancha cabe en el ancho de las cuadrículas, que sobresalen 6 px del contenido del panel, como en la maqueta); la cabecera se reduce si no cabe antes del panel.
- **Tirones**: repintar costaba hasta 30 ms la primera vez. Ahora las máscaras de BLADE y HAMMER se leen una vez, sus colores se calculan en otro hilo al abrir PAINT (y ya en RGBA8) y las calcomanías se guardan por contenido y se empiezan como mucho 2 por frame. Medido a 144 Hz con 40 cambios en 80 frames: ningún frame de más de 16,7 ms en las tres naves; el peor, ~16 ms (un refresco perdido) al ver una librea nueva. **Se probó leer las calcomanías con `TextureGetDataAsync`**: menos espera, pero HAMMER salía con 62 píxeles distintos sin causa clara; se descartó. Una `GetImage` síncrona dentro de su llamada perdió el dispositivo de D3D12 (`0x887a0005`). No se ha probado en un PC flojo.
- Comprobado: carrera en TIME TRIAL con las tres naves idéntica al píxel al commit de la etapa 1 (con `--fixed-fps 60`).
- **Para medir**: un `GD.Print` en cada repintado costaba más que el repintado (la consola de Windows es lenta) y falseaba los tirones; no medir con prints dentro de lo que se mide.

## Rivales: etapa 1, parrilla llena

(spec `docs/rivales-1-parrilla.md` v1.0, rama `feature/rivals-grid`; la versión sube a 0.16.0 en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. En RACE, 7 rivales ocupan las marcas libres, cada uno con una nave y una pintura al azar (cambian con RESTART); se ven desde la cabina y desde fuera, salen en el radar y el minimapa y **se quedan quietos** también tras el GO (conducir es la etapa 2). En TIME TRIAL no hay rivales. Sustituye a lo que las secciones anteriores dicen del ojo de las cabinas.

- Se aparta de la spec: el piloto es **F7** (la spec dice F8) y las naves de prueba **F6** (dice F7). Sin efectos de velocidad, los rivales no llevan toberas ni estela: las únicas luces de boost por fuera son las balizas de HAMMER.
- Inventario: no hizo falta sacar nada de `Player`; la nave de la IA se monta con las piezas puras (`ShipPhysics`, `ShipAttitude`, `RaceTracker.Advance`), como `ShipSimulator`. Piloto de una nave en `circuit_01`: ~90 ms (trazada ~28, calibración ~60), sin caché en disco. La carga de la carrera tarda ~200 ms más con los 7 rivales (primer frame a ~650 ms frente a ~465). Los cascos no se pisan en la parrilla (20 m entre columnas, 16 m entre puestos del mismo lado).
- **Luces**: cada casco llevaba tres direccionales en la misma capa; con 8 naves serían 24 (Godot tiene un tope) y todas se iluminarían entre sí. Los rivales van en `ShipHull3D.RivalLayer` (capa 12) sin luces propias, con un solo `HullLightRig` colgado del cuerpo del `Player`: gira con tu nave y no se apaga en cabina. Comprobado: TIME TRIAL idéntico al píxel al commit anterior.
- **Decisión del usuario, ojo de las cabinas dentro del casco 3D**: los rivales se veían muy por encima desde la cabina, porque el ojo iba por debajo de la propia nave. Ahora, desde la pista: VECTOR **2,55 m** (`Eye.Y` 0,55; antes 1,45), BLADE **2,42 m** (0,42; antes 0,90) y HAMMER **2,87 m** (0,87; antes 2,03), dentro de su cúpula o cabina (VECTOR 2,38–2,71, BLADE 2,20–2,50, ventanas de HAMMER 2,76–2,98). FOV, inclinación y profundidad no cambian. Se ve más pista bajo el horizonte; desde las primeras filas el semáforo puede salirse por arriba (sobre todo HAMMER).
- Para medir dónde está una pieza de un casco 3D: un `--script` que instancia el `.glb` y suma las `AABB` globales; ojo, en esas cajas la Y del juego sale como −Z.
- Para pruebas automáticas con otro modo o nave: una clase C# temporal en `scripts/` que cambia `GameSession` en su **constructor** (en `_Ready`, instanciada desde `_initialize`, no llega a correr), creada con `load(...).new()`.

## Rivales: etapa 2, rivales en carrera

(spec `docs/rivales-1-parrilla.md` v1.0, rama `feature/rivals-grid`, versión **0.16.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que la etapa 1 dice de que los rivales se quedan quietos.

- En el GO, los 7 rivales conducen con el piloto de su nave (trazada y calibración de `PilotCache`, sin derrapar), dan las 3 vueltas con su propio `RaceTracker` y, al terminar, cortan el boost y siguen al 60 % de su objetivo. Cuando terminas tú, los demás siguen corriendo detrás de los resultados. No guardan récords.
- Mismas reglas que el jugador: física (`Player.BuildPhysicsSettings`), daño (grupo **Daños** de `Main`), recolocación, destello en la barrera (ya uno por nave) y boost (cargas desde el GO, una por vuelta al entrar en la recta más larga; en `circuit_01` empieza a 3533 m, casi en la meta). La sacudida es solo tuya. La pausa los congela; F7 sigue siendo solo para tu nave.
- Ritmo: un solo margen para todos (`RivalPace` 0,97 y `RivalCooldownPace` 0,6 en el grupo **Rivales** de `Main`; PACE en F9). Con 0,97, BLADE da vueltas de 40,32 s y HAMMER de 40,75 s en `circuit_01`, como el simulador de equilibrio, sin tocar la barrera.
- Medido a 144 Hz, 10 s desde el GO con todos juntos: ningún frame lento; peor frame 8,3 ms en exterior y 14,3 ms en cabina. La carga sube de ~0,43 s a ~0,83 s (los pilotos de las tres naves, ~0,36 s, ya en paralelo). TIME TRIAL idéntico al píxel al commit de la etapa 1.
- Sin comprobar en el juego: un rival chocando (destello, daño y humo) y el deslizador PACE en movimiento.
- **Pendiente, decisión del usuario**: los rivales con la misma nave van en fila, solapados (puestos a 8 m y naves de 7–9 m, misma línea y mismo ritmo). Se propuso ritmo por rival (± 2 % sorteado) y carril propio en las rectas (−4…+4 m lejos de las curvas); el usuario prefiere compararlo con lo que proponga la spec 3 de rivales antes de tocar nada.

## Rivales 2: etapa 1, pilotos y clasificación en directo

(spec `docs/rivales-2-clasificacion.md` v1.0, rama `feature/rivals-2-grid-classification` —la spec dice `feature/rivals-standings`—; la versión sube a 0.17.0 en la última etapa): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que las secciones anteriores dicen de `POSITION 1/8` en el HUD y del minimapa arriba a la izquierda.

- Cada rival lleva un piloto de los 20 (`PilotCatalog`), 7 distintos por carrera, sorteados después de naves y pinturas (con la misma semilla, la misma alineación de antes más los pilotos). El jugador es `PLAYER`. F9 y la consola enseñan el apodo.
- `Main` monta `RaceStandings` en RACE y cada frame le pasa el jugador (id 0, su marca) y los rivales (su número), desde sus `RaceTracker` y su distancia sobre el eje; los intervalos se refrescan cada 0,25 s. Las naves de prueba y el fantasma no entran. La posición de los resultados sigue siendo 1 hasta la etapa 2.
- Panel (diseño `designs/clasificacion-hud.html`, B · PANEL): cinco huecos con el jugador en el del medio, en Share Tech Mono con las medidas de la página; también en cabina, **fijo en pantalla** (decisión del usuario: en cabina se ven panel, vuelta y cronómetro; nada del HUD se inclina). Colores nuevos `Palette.StandingsName` y `StandingsPlayerStrip`. La línea de la izquierda llega al 80 % en el medio, como la página.
- Minimapa: radio 56 (`MinimapDiameter` 112), arriba a la derecha, centrado con la línea más ancha posible de LAP / tiempo / BEST (calculada una vez) y `MinimapGap` 32 px bajo la línea de base de BEST; igual en TIME TRIAL. El tiempo de sector queda encima sin tocarlo (unos 13 px por cálculo).
- Parámetros en los grupos **Clasificación** y **Minimapa** de `Main`. Los 5 huecos son una constante.
- Comprobado con capturas: parrilla, en marcha (8º con los huecos de abajo vacíos), cabina de VECTOR y TIME TRIAL. Sin comprobar por el agente: ir 1º, adelantar y las cabinas de BLADE y HAMMER (las validó el usuario jugando).

## Rivales 2: etapa 2, clasificación final

(spec `docs/rivales-2-clasificacion.md` v1.0, rama `feature/rivals-2-grid-classification`; la versión sube a 0.17.0 en la etapa 3): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que las secciones anteriores dicen de que `FinalPosition` es siempre 1 en los resultados.

- Al llegar el jugador, `RaceStandings` fija el orden de pista de ese momento (también el del panel) y da a cada rival sin terminar un tiempo estimado: tiempo ahora + lo que le falta / su velocidad media en la última vuelta (sin ninguna, desde el GO; con ritmo nulo, 1 m/s). Si una estimación queda igual o por debajo de la de delante, pasa a esa + 0,001 s. Las llegadas siguientes pasan la fila a real sin moverla; si contradicen el orden, manda el orden y la consola avisa.
- `RaceResult.Classification`: los participantes con puesto, piloto, nave, tiempo, estimado o real y mejor vuelta, y la diferencia con el primero (`GapToLeader`). La clasificación sigue cambiando con los resultados ya en pantalla. `Main` la añade al enseñar los resultados (`WithClassification`): la franja enseña el puesto real. `RaceTracker.FinalPosition` sigue valiendo 1 (no conoce a los demás). Los récords no cambian.
- Consola: la tabla `[Race] Classification (N estimated)` al llegar el jugador y con cada llegada de un rival. Sin nada en F9.
- Arreglo que ya venía de antes: elegir una opción de los resultados con el teclado cambiaba de escena en medio del `_Process` de `Main`, y la rejilla daba dos errores fuera del árbol; ahora ese frame se corta ahí.
- Comprobado con el piloto, carreras completas con `--fixed-fps 60`: estimados 2:05.676 / 2:05.806 y reales 2:05.667 / 2:05.783; franja `7th / 8` igual que el panel; BACK TO MAIN MENU nada más salir, sin errores. Sin comprobar por el agente: la pausa en los resultados y una llegada fuera de orden en el juego (sí en tests).

## Rivales 2: etapa 3, clasificación en resultados

(spec `docs/rivales-2-clasificacion.md` v1.0, sección 7.1, rama `feature/rivals-2-grid-classification`, versión **0.17.0**): hecha y validada por el usuario. Cierra la spec. Sustituye a lo que las secciones anteriores dicen de la franja de resultados de 340 px con las opciones en la tercera columna y de que las llegadas tras el jugador no cambian de puesto.

- Maqueta `designs/clasificacion-resultados.html`, versión **B · TABLA A LA DERECHA**, con tiempos totales y la etiqueta `EST` (elegidos por el usuario). Franja de 440 px (`ResultsBandHeight`); resultado y tiempos como antes; la tabla en x = 890; separadores en x = 470 y 860, centrados en la parte de arriba; opciones en fila (`OptionList.Horizontal`), a 22 px (`ResultsOptionFontSize`), con ↑ ↓ y ← →.
- **Decisión del usuario, cambia la 4.3 de la spec**: tras tu llegada, los rivales que llegan después se reordenan por su tiempo real (las llegadas delante de los que siguen en pista); tu puesto y el de los llegados antes que tú no se mueven. Con los rivales en grupo, 4 de 7 llegadas contradecían el orden fijado y la tabla salía con tiempos desordenados. La consola avisa cuando una llegada cambia el puesto de alguien.
- Colores nuevos `Palette.ClassificationProvisional`, `ClassificationFinal`, `ClassificationEstimated`, `ClassificationArrival` y `ResultsDivider`. `RaceResult.ClassificationPlayerId`: la fila resaltada.
- Comprobado con el piloto: llegando 6º, tabla `FINAL`; con los rivales al 85 % (`RivalField.SetPace` desde la sonda), 1º con 7 `EST` que van pasando a reales. La reordenación solo está cubierta por las pruebas: en la carrera de prueba nadie se adelantó antes de la meta.

## Rivales 3: etapa 1, choques entre naves

(spec `docs/rivales-3-contacto.md` v1.0, rama `feature/rivals-3-contact`; la versión sube a 0.18.0 en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. En RACE las naves ya no se atraviesan: chocan entre ellas y contigo con roce, medio y fuerte. Los rivales todavía no miran (etapa 2), así que en la salida se encadenan alcances: es lo esperado.

- Inventario (etapa 0, sin cambios): la dinámica guarda la velocidad aparte del rumbo, así que el empujón de lado no necesitó refactor; no hubo etapa de refactor aparte.
- Se apartan de la spec o la concretan: semiancho de VECTOR **2,63 m** (la spec decía 3,4); el rectángulo usa la caja real del casco, sin centrar en el origen; la altura se compara **sobre la superficie** (con 25° de peralte, dos naves pegadas difieren ~2,2 m en el mundo); el descarte de las 20 m es en planta (con la distancia de carrera, una nave doblada parecería lejos); el rectángulo va en planta, así que en un peralte de 25° cuenta un 10 % menos de ancho.
- Respuesta: la parte del empujón hacia el morro va también al motor (`ShipDynamics.ApplyContact`), porque si no el agarre la deshacía en unas décimas y el de detrás se volvía a meter; la de lado la corrige el agarre y el morro no gira. Tras separar, `ShipPhysics.AfterContactMove` vuelve a pasar la barrera sin que pase tiempo y no pierde un impacto de la primera pasada; la altura la ajusta el paso siguiente.
- Orden del paso: `Player` (prioridad 0) se mueve; `Main` (prioridad 1) llama a `RivalField.StepPhysics` (rivales, contactos y fin del paso de cada rival), luego aplica a tu nave la sacudida y el daño de los golpes, y después su barrera y su recolocación. Tu cámara va un paso por detrás de la separación (centímetros).
- Daño (grupo **Daños** de `Main`, `ContactMediumRammer`…): +1 / 0 / +1 en un medio y +3 / +1 / +3 en un fuerte (embiste / recibe / mutuo), con `DamageModel.AddPoints`; solo en carrera. Sacudida solo si el golpe es contigo.
- Parámetros: grupo **Colisiones** del `Player` (`ShipContactMaxHeight`, `ShipContactCullDistance`, `ShipContactMutualMargin`, `ShipContactCooldown`; umbrales, restituciones, roce, pérdidas y corrección son los de la barrera), grupo **Recolocación** del `Player` (parpadeo) y `PassableAfterRespawn` en **Recolocación** de `Main`.
- Parpadeo: `GeometryInstance3D.Transparency` en las mallas del casco (`ShipHull3D.SetOpacity`), sin tocar materiales. Medido a 144 Hz durante el parpadeo: ningún frame lento (peor 8,6 ms). No se ha probado en un PC flojo.
- F9: casilla `CONTACT` (dura hasta cerrar el juego, no se guarda) y, por rival, puntos y `SHIP` embestidas / recibidos / mutuos, más `RIVAL VS RIVAL`.
- Comprobado con el piloto y la misma parrilla (semilla temporal), `--fixed-fps 60`: con `CONTACT` apagado la carrera es idéntica al commit anterior (vueltas de las 8 naves y posiciones finales); con choques, 27 golpes que cuentan en una carrera, 23 entre rivales, y el daño cuadra con el reparto.
- **Ya pasaba antes, sin tocar**: al recolocar la nave aparece una bola gris en el cielo (parece el humo del casco al reiniciarse con `ClearSmoke`); sale igual en el commit anterior. Arreglado al quitar ese humo (humo de daños, etapa 1).
- Para la etapa 2: recordar el ritmo por rival (± 2 %) y el carril propio en las rectas, que el usuario dejó para comparar con esta spec.

## Rivales 3: etapa 2, trazada clara

(spec `docs/rivales-3-contacto.md` v1.0, sección 8, rama `feature/rivals-3-contact`, versión **0.18.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Los rivales ya no se comen por detrás: se apartan para pasar, siguen detrás o se quedan en su carril.

- **Ritmo por rival** (decisión del usuario, propuesta en la spec 1 de rivales): el común ± hasta un 2 % (`RivalEntry.PaceOffset`, `RivalLineup.PaceSpread`), sorteado después de los pilotos, así naves, pinturas y pilotos salen igual; `RivalDriver.PaceFor` no pasa del 100 %. El PACE de F9 mueve la base y el panel enseña el de cada rival. Sin él, dos rivales con la misma nave no se alcanzaban nunca. El carril propio en las rectas queda para la spec 4.
- `ClearLine` (pura, en `Autopilot/`): `CLEAR`, `PASS L/R` o `FOLLOW` con el tráfico de todas las naves al empezar el paso (`TrafficShip`, que `RivalCar.TrafficOf` saca de cada `IContactShip`, también del jugador). El piloto (`AutopilotDriver.Drive` con desplazamiento, velocidad máxima y límite lateral; sin ellos conduce igual que antes, así el F7 y el equilibrio no cambian) apunta a la trazada desplazada.
- **Se aparta de la spec o la concreta**, porque la versión literal seguía chocando y llevaba a los rivales contra la barrera: el desplazamiento se mide en el punto al que apunta el piloto; empieza donde está la nave (en la salida, su columna de la parrilla; recolocada, el centro) y llega a la trazada a 4 m/s y con el lado libre, así las columnas no se cierran en diagonal; el pasillo va de su carril a la trazada; "nunca hacia donde hay una nave al lado" vale también al seguir su propia trazada (cada nave tiene la suya y en las curvas se cruzan), y mira dónde estará la otra en 1 s; si no puede volver, mantiene su **lateral real** (un desplazamiento fijo respecto a la trazada la llevaba contra la barrera en las curvas: 10–23 impactos medios por carrera); al pasar, el objetivo es un lateral real al lado de la otra, recalculado en cada paso; el desplazamiento nunca va más de 3 m por delante de donde va de verdad; el punto al que apunta nunca pasa de la barrera menos 1 m.
- Boost: la carga de la recta más larga solo con la trazada clara; si al entrar alguien la tapa, espera mientras siga en ella (`RivalDriver.LongestStraight`). Sin carga al entrar, esa vuelta no, como antes.
- F9: casilla `LINE STATES` (`RivalShip.ShowLineStates`, solo depuración, dura hasta cerrar el juego): estado encima de cada rival, a tamaño fijo en pantalla, y su pasillo hasta la distancia de mirada, en `Palette.RivalStateClear` / `Pass` / `Follow`.
- Medido con el piloto en 8 carreras de `circuit_01` (parrilla sorteada con una semilla temporal, `--fixed-fps 60`): **0 golpes medios o fuertes entre rivales** (antes ~23 por carrera), 0–1 impactos y 0–5 roces de los rivales con la barrera; apartados un 20–25 % del tiempo y siguiendo un 5 %; totales de 2:06 a 2:13. Peor frame en la salida a 144 Hz, 15–18 ms frente a 14 ms del commit anterior (un frame lento en una de tres pasadas). No se ha probado en un PC flojo.
- Sin comprobar por el agente: el jugador lento o parado en la trazada y el boost esperando en la recta en el juego (sí en tests), y otros circuitos. El piloto F7 no esquiva: los golpes que quedan suelen ser suyos.

## Rivales 3: parche 1.1, roce por detrás, y rival bloqueado tras una nave parada

(spec `docs/rivales-3-contacto.md` v1.1, rama `fix/rear-contact`, versión **0.18.1**): hechos, con tests de la lógica pura y validados por el usuario. Sustituye a lo que la etapa 1 dice de que todo roce frena a las dos naves.

- Causa medida antes de cambiar nada: dos VECTOR pegadas en línea recta acababan las dos a 345,8 km/h y bajando (solas, 392 y 396); el contacto era continuo y lo único que les quitaba velocidad era la fricción del roce (20 km/h/s a las dos). Ahora van a 393,9.
- Tipo de roce (`GrazeKind`, `ContactResponse.Kind`): el ángulo entre la normal y la dirección media de las dos naves; por encima de 45° es **de lado** y frena como antes (4 % al entrar y 20 km/h/s); 45° o menos, **por detrás**, solo el empujón con restitución 0. Se recalcula en cada paso. `ShipContactSideGrazeAngle` en el grupo **Colisiones** del `Player`. Golpes medios y fuertes, sin cambios.
- F9: `YOUR GRAZE SIDE/REAR/— LOST x KM/H IN 5 S` (`RivalField.PlayerGraze`, `GrazeLossMeter`).
- **Rival bloqueado** (fallo de la etapa 2, encontrado al probar con el jugador parado en la parrilla): un rival justo detrás de una nave parada decidía pasarla, pero mientras su pasillo la pisaba iba a la velocidad de seguirla, 0; parado no puede girar, y sin tocarla el atasco no lo recolocaba. Ahora, al apartarse, nunca baja de 3 m/s (`ClearLineSettings.PassMinSpeed`); en carrera normal nunca se va tan despacio, así que no cambia nada.
- **Descartado, por si se vuelve a intentar**: limitar la velocidad con la distancia de frenada, alargar la mirada hasta ella, no cruzar por delante de la otra y una red de seguridad para cualquier nave del camino. Con el jugador parado mejoraba, pero en carrera normal los golpes entre rivales subían de 0 a 1–5 por carrera y la red de seguridad creaba atascos nuevos.
- Medido en 8 carreras de `circuit_01` por caso (parrilla con semilla temporal, `--fixed-fps 60`): con el jugador parado, terminan los 7 rivales en todas (antes 5–7), 0 golpes contra él y 1 entre rivales en total; con el jugador en marcha (piloto F7), terminan todos y 0 golpes entre rivales en 7 de 8 (1 en la otra, carambola de un golpe del piloto F7).
- **Límite conocido**: la mirada de la spec (máx(30 m, 1,5 s)) se queda corta para frenar ante una nave parada a toda velocidad (~154 m frente a ~164 m de frenada a 370 km/h). En las 8 carreras finales no salió ningún golpe fuerte contra el jugador parado; con los arreglos descartados sí salieron (270–370 km/h).
- Para pruebas automáticas: en un `--script` de `SceneTree`, `_physics_process` tiene que devolver `bool` (si no, el script no carga).

## Rivales 4: etapa 1, perfiles y ritmo

(spec `docs/rivales-4-personalidades.md` v1.1, rama `feature/rivals-4-personality` —la spec dice `feature/rivals-personality`—; la versión sube en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que las secciones anteriores dicen del ritmo de los rivales (`RivalPace` 0,97 común ± `PaceOffset` del 2 % por rival).

- Cada piloto tiene uno de los seis perfiles (tabla 3.3) y su variación fija, guardada en el catálogo. Por el inventario, la reacción del jugador no existe (decisión de siempre) y no se crea; queda como feature para más adelante.
- **Márgenes**: el de recta frena la punta donde manda la punta y el de curva la velocidad de la calibración donde manda la curva; sin radio de 600 m ni transición de 30 m (el límite es el menor de los dos). La frenada multiplica el 90 % de la spec 6. Con recta 1, curva 0,97 y frenada 1 sale el perfil de antes: el F7 y el equilibrio de naves no cambian.
- **Decisión del usuario, cambia la tabla 3.2**: NORMAL al ritmo de los rivales de antes (el usuario ya les ganaba: 2:03.700 con VECTOR frente a 2:06–2:13). Recta / curva: equilibrado 99 / 99, velocista 100 / 96, especialista 98 / 102, agresivo 99 / 99, conservador 99 / 99, técnico 98 / 100. La curva puede llegar al 105 % (al 106 % el piloto aún no roza). WRAITH, retocado a mano a −0,4 puntos en los dos márgenes. Cada punto de recta vale ~0,2 s por vuelta, uno de curva ~0,1 s; la frenada casi no cuenta.
- `BalanceRunner` en modo PERFILES, en `circuit_01`: perfiles a ±0,18 s del equilibrado y pilotos a ±0,35 s en las tres naves; el equilibrado a 0,03–0,12 s del piloto de F7.
- Reacción en el GO: la del piloto ± 0,05 s sorteados en cada salida (sustituye al `PaceOffset`). PACE de F9 y `RivalPaceOffset` del grupo **Rivales** de `Main`: ajuste en puntos (−15…+5, 0 por defecto) sobre los dos márgenes. F9 enseña por rival perfil, márgenes, frenada y reacción; la consola, el perfil en la alineación.
- Medido en carreras sin ventana con el jugador parado: 0 golpes entre rivales en 12 y uno contra él en una; ganan con 2:05–2:08.
- **Para la etapa 2**: los rivales casi no aprovechan el boost (0,02–0,05 s por vuelta): el perfil tiene la punta como techo y sueltan W con el impulso. La spec v1.1 (4.6) ya pide que sigan pisando W. El usuario nota duras las penalizaciones de los golpes; de momento sin cambios.

## Rivales 4: etapa 2, duelo

(spec `docs/rivales-4-personalidades.md` v1.1, rama `feature/rivals-4-personality`; la versión sube en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que las secciones anteriores dicen del boost de los rivales (una carga al entrar en la recta más larga).

- **Rebufo para todas las naves**, el jugador incluido (`Slipstream`, grupo **Rebufo** del `Player`; los rivales usan esos valores). Lo calcula `RivalField` al empezar cada paso; el jugador lo usa en el siguiente (un paso de retraso). En TIME TRIAL no hay nadie: 0. Con W pisado manda el +4 % de punta: el `Drag` solo actúa sin acelerar.
- HUD: chevrons sobre las esferas (10 px sobre 1280×720, ×1,5), también en cabina. Cabina: 10 líneas de aire (`AirLineCount`, `AirLineIntensity` en **Cabina**), propuesta inicial validada por el usuario; no hay líneas de velocidad (`efectos-velocidad` sigue sin hacerse), así que su largo lo calculan ellas.
- Duelo y boost con los rasgos de cada piloto; concreciones en la sección 4.6b de la spec (SLIP solo por encima de 150 km/h y con la de delante como mucho 15 km/h más lenta, hueco de 6 m o 0,15 s; ataque por dentro a menos de 60 m de la frenada y aborto 30 m después; defensa al 60 % del límite lateral; roce de lado con contacto < 0,5: 1 m durante 1 s).
- **Arreglos encontrados al medir**: con el impulso del boost el rival soltaba W (el perfil tiene la punta de techo): ahora usa un perfil con la punta + el impulso y sigue pisando; apartado en recta, la trazada clara le ponía de tope el margen de curva de la punta (venía de la spec 3); la zona de frenada contaba como recta y algunos se pegaban en SLIP al que ya frenaba.
- Medido sin ventana con el jugador en F7 en `circuit_01`: tiempos de los rivales como en la etapa 1 (2:05–2:14) y unos 0,6 golpes medios o fuertes entre rivales por carrera (sin duelo, 0 en 10), sobre todo al llegar a la frenada tras un ataque. Muchos adelantamientos en recta se abortan: el rebufo solo no basta para ponerse a la par; con boost, sí. El usuario lo da por bueno.
- Con el jugador parado en la parrilla (`Main.tscn` sola), los rivales del fondo pueden perder segundos detrás de él en la salida: es el caso artificial del parche 1.1.
- Si hace falta probar el duelo en frenada, el usuario propone un circuito de pruebas técnico (horquillas tras rectas medias, chicane), aparte.

## Rivales 4: etapa 3, errores

(spec `docs/rivales-4-personalidades.md` v1.1, rama `feature/rivals-4-personality`; la versión sube en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario.

- Frenada tardía, salida abierta y mala salida con las frecuencias de la spec, del generador de cada rival (`RivalEntry.Seed` con otra máscara que el de la defensa). Concreciones en la sección 5.3b de la spec.
- **Se aparta de la spec**: medido con un rival solo en `circuit_01`, frenar 5–15 m tarde y acelerar antes en la salida no le costaba tiempo (frena al 90 % de lo que puede y a la salida la trazada ya va por fuera). Cada error sortea lo que pierde (0,3–0,8 s) y baja su objetivo en el tramo del error lo que hace falta: ~0,45–0,6 s por error, con algún roce o golpe medio contra la barrera.
- F9: casilla **ERRORS** (dura hasta cerrar el juego, no se guarda) y `ERR LB / WX / BS` por rival. El panel la vuelve a aplicar al arrancar la carrera: para pruebas automáticas sin errores, hay que apagarla desde ahí, no antes.
- Informe de carrera en la consola (`[Race] Report`) cuando terminan todos los rivales, con el puesto de ese momento; del jugador, puesto, adelantamientos y daño.
- Medido en 6 carreras sin ventana con el jugador en F7: 2–7 errores por carrera, tiempos de 2:04 a 2:15 y menos de un golpe entre rivales por carrera.

## Rivales 4: etapa 4, dificultad

(spec `docs/rivales-4-personalidades.md` v1.1, rama `feature/rivals-4-personality`, versión **0.19.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec 4 de rivales. Sustituye a lo que la etapa 1 dice de los márgenes de los perfiles y del techo de curva (105 %).

- FÁCIL / NORMAL / DIFÍCIL en `GameSession.Difficulty`, leída al montar la carrera (`RivalRaceContext.Difficulty`); SETTINGS solo tendrá que leerla y guardarla. Solo cambia rasgos de los pilotos, nunca la física; sin rubber banding.
- **Decisión del usuario tras probarla**: con la primera tabla ganaba por ~5 s en las tres. La calibración de curva es muy prudente (al 130 % aún no rozan en `circuit_01`; al 140 %, sí). Curva de los seis perfiles +13 puntos (equilibrado 99 / 112, velocista 100 / 109, especialista 98 / 115, agresivo 99 / 112, conservador 99 / 112, técnico 98 / 113), techo de curva 130 %, WRAITH sin variación de recta, y la dificultad mueve recta y curva por separado: FÁCIL −3 / −12, DIFÍCIL +1 / +13 (sección 6.3c de la spec).
- Boost en el mejor momento, como el usuario: salida de curva (a menos de 150 m) hacia una recta de 500 m, con la trazada clara; ADELANTAR y DEFENDER con más de una carga, ÚLTIMA VUELTA solo en la última.
- Objetivos acordados, ganador de la carrera: FÁCIL ~2:08, NORMAL 2:03–2:04, DIFÍCIL 2:00–2:01. Medido sin ventana con solo los rivales en pista: 2:06.5–2:10.4, 2:02.9–2:05.7 y 2:00.2–2:01.9. PERFILES: NORMAL ✓ (vuelta media 39,77 s), FÁCIL 41,33 s, DIFÍCIL 38,94 s. Golpes entre rivales: ~1,2 por carrera en NORMAL y ~1,8 en DIFÍCIL.
- Para medir carreras de rivales sin que estorbe el jugador: con F7 va ahora más lento que ellos y los frena, y parado en la parrilla atasca a los del fondo; hace falta quitarlo de sus naves (temporalmente, en `RivalField`).
- Solo afinado en `circuit_01`: el usuario quiere reajustarlo cuando haya más circuitos.

## SETTINGS: etapa 1, pantalla, modelo y guardado

(spec `docs/settings.md` v1.1, rama `feature/settings`; la versión sube en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. SETTINGS sale del COMING SOON del menú principal. Sustituye a lo que las secciones anteriores dicen de que la dificultad no se guarda.

- Composición C · BRIEFING de `designs/settings.html`: a la izquierda, miga, nombre, grupo · valor, explicación y vista previa 2D; a la derecha, el panel con la lista (filas de 32 px, zona de 540 px que se desplaza sola con 40 px de margen) y RESET DEFAULTS / BACK en fila. Fondo: la rejilla del menú sin la nave. Ratón: pasar elige, clic en flechas, segmentos y OFF / ON, rueda.
- Se aplican al momento: BLOOM (glow de las escenas, también las abiertas, y de la cabina 3D), SPEED LINES (líneas de aire del rebufo), SPEED FOV (FOV por velocidad de la cámara exterior y FOV del boost), COCKPIT SHAKE (vibración de las dos cámaras y de la cabina 3D; no la sacudida por golpes) y DIFFICULTY (siguiente carrera). Se guardan al salir y al confirmar RESET DEFAULTS, en `settings.json`, sin tocar pintura, fantasma ni última nave.
- **Decisiones del usuario**: TUNNEL VISION y PARTICLES en COMING SOON, porque `efectos-velocidad` no está hecha (PARTICLES se activa con los efectos de choque); modo de pantalla por defecto **BORDERLESS** (lo de hoy). Modo, resolución y teclas se ven pero se aplican en las etapas 2 y 3.
- Concreta la spec: la barra lateral de los selectores del juego en vez del rombo de la página; resolución guardada como `1920x1080`, leída sin distinguir mayúsculas.
- Comprobado con capturas por sonda (sin salir de la pantalla, así no guarda): lista, desplazamiento, vistas previas de dificultad, pista y teclas, RESET DEFAULTS / BACK y la pregunta. El resto, el usuario jugando.

## SETTINGS: etapa 2, VIDEO

(spec `docs/settings.md` v1.1, secciones 4.1–4.3 y 4.5, rama `feature/settings`; la versión sube en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Modo de pantalla y resolución ya se aplican. Sustituye a lo que la etapa 1 dice de que modo y resolución se ven sin aplicarse.

- WINDOWED: ventana con marco del tamaño de la resolución, centrada en su pantalla. FULLSCREEN: pantalla completa exclusiva de Godot. BORDERLESS (por defecto): su pantalla completa, lo de siempre. En FULLSCREEN y BORDERLESS la resolución es la del 3D (`Scaling3DScale` = ancho / ancho de la pantalla, 0,1–1); HUD y menús, a la de la pantalla.
- Solo salen las resoluciones que caben en la pantalla donde arranca la ventana; por defecto, la mayor. Una guardada que ya no cabe pasa a la mayor, con aviso.
- Al cambiar modo o resolución se aplica al momento y, 0,5 s después del último cambio, sale KEEP THIS DISPLAY? con `REVERTING IN 10`; REVERT, ESC o el fin de la cuenta vuelven a lo de antes del primer cambio. Con un cambio esperando, ESC saca la pregunta en vez de salir. `DisplayDelay` y `DisplayCountdown` en el Inspector de `SettingsScreen`.
- **Trampa de Godot en Windows**: una ventana del tamaño exacto de la pantalla se da por pantalla completa exclusiva, y al salir de pantalla completa vuelve al tamaño de la primera ventana (1920 × 1080): WINDOWED no salía nunca. Arreglo: `window_width_override` / `window_height_override` = 1600 × 900 en `project.godot` (solo la primera ventana; el juego sigue arrancando en pantalla completa y el lienzo es de 1920 × 1080).
- Límite conocido: WINDOWED a la resolución de la pantalla deja la barra del título fuera.
- Comprobado con sondas (sin guardar): WINDOWED a 1920 / 1600 / 1280, FULLSCREEN, BORDERLESS y vuelta, con modo, tamaño y escala del 3D esperados. Para que una sonda espere la pregunta, por tiempo, no por frames (a 144 Hz 0,5 s son 72 frames).

## SETTINGS: etapa 3, CONTROLS

(spec `docs/settings.md` v1.1, secciones 6.1–6.3, rama `feature/settings`; la versión sube en la etapa 4): hecha, con tests de la lógica pura y validada por el usuario. Las teclas de conducción se cambian en SETTINGS. Sustituye a lo que la etapa 1 dice de que las teclas se ven sin aplicarse.

- Inventario: acelerar, frenar, girar y la cámara iban con teclas fijas en `Player`. Ahora son acciones de `project.godot`: `accelerate`, `brake`, `steer_left`, `steer_right` (W/S/A/D primero y la flecha detrás) y `camera` (C), todas con teclas **físicas**; `boost`, `drift` y `pause` ya existían. Los menús siguen con sus `menu_*`, que SETTINGS no toca.
- ENTER o clic en la tecla: `PRESS A KEY` en naranja, parpadeando (0,6 s). La tecla nueva funciona al momento; si la tenía otra acción, se intercambian (`SWAPPED WITH BRAKE`). ESC cancela sin salir de SETTINGS; F1–F12 se ignoran. PAUSE, fija en ESC. RESET DEFAULTS devuelve también las teclas.
- **Decisión técnica**: la segunda tecla de otra acción (las flechas) tampoco vale, así ninguna tecla hace dos cosas y las flechas no cambian nunca.
- Archivo: sección `controls` de `settings.json`, por nombre (`"keyBoost": "E"`, `"SHIFT"`, `"#código"` si no tiene nombre), sin PAUSE. Una tecla que falta, no se conoce o no vale, por defecto con aviso; si dos acciones comparten tecla, todas por defecto.
- Comprobado con sondas (sin guardar): cambiar e intercambiar, F9 ignorada, ESC cancela sin salir, menús intactos; en carrera, W acelera y C cambia de cámara con las acciones nuevas. Para simular una tecla física en una sonda: `InputEventKey` con `physical_keycode`.

## SETTINGS: etapa 4, desde la pausa

(spec `docs/settings.md` v1.1, sección 7.2, rama `feature/settings`, versión **0.20.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que las secciones anteriores dicen de las opciones de la pausa.

- Pausa de RACE: CONTINUE · SETTINGS · RESTART · BACK TO MAIN MENU (separador de 204 px). TIME TRIAL conserva GHOST (decisión del usuario): CONTINUE · GHOST · SETTINGS · RESTART · BACK, con la franja a 320 px (`GhostBandHeight`) para que quepan las cinco.
- SETTINGS es la misma pantalla del menú, hija de `PauseMenu` en su capa (10), con la carrera congelada detrás y atenuada al 80 % (`SettingsDimOpacity`) y `PAUSE · SETTINGS` en la miga. Mientras está abierta, la pausa no lee el teclado (ESC es de SETTINGS); BACK / ESC guardan y vuelven a la pausa con SETTINGS elegida. Al continuar, la nave ignora lo pulsado hasta soltarlo, como siempre.
- Todo se aplica al momento también desde la pausa: efectos, teclas, modo y resolución (con su confirmación); la dificultad, en la siguiente carrera (RESTART ya la usa).
- Comprobado con sondas en RACE (pausa → SETTINGS → ESC → pausa con SETTINGS → continuar) y la pausa de TIME TRIAL. Ojo: salir de SETTINGS en una sonda guarda `settings.json`.

## Efectos de choque: etapa 1, barrera

(spec `docs/efectos-1-choques.md` v1.0, rama `feature/fx-collisions`; la versión sube a 0.21.0 en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. Los choques contra la barrera de todas las naves (RACE y TIME TRIAL) sacan chispas en trazos, destello con rayos (y onda en el fuerte) y humo en medios y fuertes; solo visual. Entre naves, en la etapa 2.

- PARTICLES sale del COMING SOON (decisión del usuario): LOW / MEDIUM / HIGH al 25 / 50 / 100 % de chispas, humo y sus máximos, guardado en `effects`; texto *Amount of sparks and smoke from crashes*. Un `settings.json` de antes avisa una vez de que falta y usa HIGH.
- `Main` pasa a `CollisionEffects` el `LastBarrierTouch` del jugador y el de cada rival en cada paso de física, después de la barrera del jugador; `Player.PlanVelocity`, nuevo. Un golpe dentro de la espera llega como roce: saca chispas de roce, no la ráfaga.
- **Decisión del usuario**: el destello de la barrera (`FlashAhead` del `Track`, para todas las naves) y el punto de las chispas (`BarrierContactAhead` de `CollisionEffects`) van **2 m por delante** del centro de la nave, no 1,3 m hacia la popa. Adelantarlos más para verlos por el cristal dejaba de parecer real; en su lugar, desde la cabina se nota el choque: luz naranja de las chispas por el lado tocado (omni en la cabina 3D, `Cockpit3DView.SetCrashLight`, y tinte en ese borde, `CockpitTints.SparkLeft` / `SparkRight`), temblor fino mientras se roza (con COCKPIT SHAKE, por `Cockpit.SetShake` y la mitad en la cabina 3D) y un 25 % de chispas que suben más al rebotar en la barrera. Grupo **Cabina: choques** del `Player`; `Main` llama a `Player.FeelBarrier` en cada paso. Comprobado en cabina de VECTOR con un roce forzado: borde y pilar derechos en naranja. Sin probar en BLADE ni HAMMER.
- Se aparta de la spec por la maqueta (sección 14 de la spec): alguna chispa sale algo hacia la barrera y rebota; redondeo de las mitades hacia arriba (3 bocanadas en LOW con un fuerte, no 2).
- Comprobado con sondas en RACE (`Main.tscn` sola, archivos del usuario apartados y devueltos): golpe fuerte con onda, medio con humo y roce a 289 km/h con trazos detrás, sin errores. A 144 Hz, 4 s de choques en la salida: 0–4 frames lentos y peor frame 16–25 ms, igual que sin efectos (1–3 y 22–23 ms). No medido con 3000 chispas vivas ni en un PC flojo. Cabina, pausa, PARTICLES en LOW y TIME TRIAL los validó el usuario jugando.

## Efectos de choque: etapa 2, entre naves

(spec `docs/efectos-1-choques.md` v1.0, rama `feature/fx-collisions`, versión **0.21.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Los contactos entre naves de RACE (también entre dos rivales) sacan chispas naranjas, destello que se mueve con ellas y humo en medios y fuertes; el roce por detrás no saca nada.

- `ShipContactEvent` gana punto, normal y `Facing` (solo para los efectos; el choque no cambia). `Main` pasa los `Events` del paso a `CollisionEffects.ShipContact` con la velocidad del que embiste o la media.
- La luz y el temblor de la cabina también se encienden con un roce de lado o un golpe con tu nave, en el lado donde queda el punto (`Player.FeelShip`; `FeelBarrier` + `StepCrashFeel`).
- **Decisión del usuario, toques por detrás con tu nave** (fuera no cambia nada): un roce por detrás solo tiembla en cabina, a la mitad (`CrashRearGrazeRumble`); un golpe por detrás enciende un fogonazo suave detrás si te embisten (dos omnis tras el asiento, arriba y a la altura de los mandos, la de abajo a la mitad) o delante si embistes tú (`CrashEndLightEnergy` 0,5, sin tinte). Grupo **Cabina: choques** del `Player`. Comprobado en cabina de VECTOR con un golpe forzado: techo, pilares y rebordes de los mandos en naranja, sin quemar. Sin probar en BLADE ni HAMMER.
- Comprobado con sondas en RACE: golpes medios y fuertes con rivales, con onda y humo; rendimiento como en la etapa 1. Roce de lado sostenido, CONTACT apagado en F9, naves terminadas atravesables y golpes entre rivales los validó el usuario jugando.

## Brillo de levitación fuera

(decisión del usuario, al empezar `docs/efectos-2-derrape.md`): la mancha azulada bajo los cascos 3D ya no existe, ni en tu nave ni en los rivales (`HoverGlow`, `HoverGlowShape`, `GlowTransform` y `HullHoverGlow` del grupo **Casco 3D**). Sustituye a lo que las secciones anteriores dicen de él. Puede volver más adelante: está en el historial.

## Efectos de derrape: etapa 1, marcas

(spec `docs/efectos-2-derrape.md` v1.1, rama `feature/fx-drift`; la versión sube a 0.22.0 en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. Al derrapar en el suelo con más de 4° de deslizamiento, la nave deja dos líneas naranjas finas bajo las puntas de las alas (95 % del semiancho de colisión), una muestra cada 0,8 m, que duran 6 s y se apagan en 2 s; brillo 0,32 × (0,35 + 0,65·d), con d de 0 a 1 entre 4° y 22°. Solo visual.

- Inventario: el derrape se lee de `ShipDynamics` (`Drifting`, `SlipAngle`; el `Player` los expone, con `CollisionHalfWidth` y `DriftContact` de la nave que se ve). **Los rivales no derrapan nunca** (su piloto no pulsa la tecla): `Main` ya les pasa su derrape, pero no dejan nada. El ángulo máximo del juego es 60° (la maqueta, 24°): los 22° se alcanzan sin problema. Contravolanteando, la nave se inclina al otro lado, pero el lado del roce sigue saliendo del deslizamiento, como dice la spec.
- Puntas (`DriftContactShape.TipZ`, sacadas de los scripts de Blender: los manifiestos no guardan los bordes de las alas): VECTOR z 2,7 (popa de la punta del ala), BLADE 3,6 (final de las puntas traseras), **HAMMER −2,2, los patines de las puntas caídas de los canards, delante** (decisión del usuario: no tiene alas detrás).
- `CollisionEffects` lo dibuja: `DriftShip` en cada paso de física (lo llama `Main` en `UpdateCollisionEffects`, para tu nave y la de cada rival), `CutDrift` al recolocar y una malla de líneas en suma para todas las naves (`DriftMarks`). Grupo **Derrape** de su Inspector; el color sale de `Palette.Orange`.
- Comprobado con una sonda en TIME TRIAL con VECTOR (derrape a cada lado): marcas detrás de las puntas. El resto (BLADE, HAMMER, peralte, cresta, pausa, RESTART), el usuario jugando.

## Efectos de derrape: etapa 2, chispas y foco

(spec `docs/efectos-2-derrape.md` v1.3, rama `feature/fx-drift`, versión **0.22.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Al derrapar salen chispas del borde interior del ala y un foco de chispas en el punto que más roza. Solo visual.

- **Decisión del usuario, cambia la spec**: la mancha naranja en la pista y el halo de 1,4 m no gustaban; los sustituye un **foco de chispas**: núcleo blanco caliente de 0,2 m (`DriftCoreSize`) y halo naranja de 0,9 m al 35 %, de cara a la cámara, chisporroteando (0,7 + 0,3 · sen(97t) · sen(61t), con desfase por nave). El 60 % de las chispas sale de él en chorro estrecho (±0,3 m/s), el resto a lo largo del borde (±1 m/s a lo largo y en vertical, no a los lados: así todas salen hacia fuera con 2 m/s o más), y 8 brasas por segundo × d.
- **Decisión del usuario, cambia la spec**: chispas y foco nacen **en el ala, a su altura real** (con la inclinación y el cabeceo de la nave que se ve: `Player.HullTransform`, `RivalShip.HullTransform`), 0,2 m por debajo del borde (`DriftFocusDrop`, ajustado por el usuario), no a 8 cm sobre la pista: la nave va a 2 m y se veían separadas de ella. Las marcas siguen en la pista.
- Borde y foco por nave (`DriftContactShape`, de los scripts de Blender): VECTOR borde de ataque del ala de z 0,2 (55 %) a la punta (z 2,7), y −0,08, foco en la punta (z 2,45); BLADE el filo del fuselaje de z 0,2 (70 %) al final de las puntas (z 3,6), y 0,01, foco en z 3,4; HAMMER el patín del canard (z −2,35 a −2,05, 98,6 %, y −0,52), foco en z −2,2.
- Parámetros en el grupo **Derrape** de `CollisionEffects` (chispas, chorro, brasas, bajada, núcleo y halo). Para cambiar un valor por defecto desde fuera del editor, el editor abierto sigue enseñando el viejo hasta reiniciarlo (su Build no recarga un DLL ya compilado).
- Comprobado con una sonda en TIME TRIAL con las tres naves en cámara exterior. Cabina, PARTICLES en LOW, rozar la barrera derrapando y los fps, el usuario jugando; los rivales no derrapan.

## Efectos de aterrizaje

(spec `docs/efectos-3-aterrizaje.md` v1.1, rama `feature/fx-landing`, versión **0.23.0**): hechos, con tests de la lógica pura y validados por el usuario. Al aterrizar con caída media (3–8 m/s) o fuerte (≥ 8 m/s), con los umbrales de la sacudida de `pista-3d`, salen chispas en trazos desde la panza (20 / 70 + 12 brasas en HIGH) y polvo gris bajo a los lados (4 / 12 bocanadas). Todas las naves, RACE y TIME TRIAL; el fantasma no. Solo visual: sacudida y compresión no cambian.

- Inventario: `ShipVertical.Landed` / `LandingSpeed` en cada paso (el `Player` los expone con `JustLanded`, `LandingSpeed`, `LandingThresholds` y `ExteriorProfile`); `Main` los pasa a `CollisionEffects.Landing` en `UpdateCollisionEffects`, para tu nave y para cada rival (`car.Physics.Vertical`).
- **Decisiones del usuario, cambian la spec**: las chispas nacen 6 cm por debajo de la **panza real** de la nave (`ShipVisualProfile.BellyY`, la y mínima del manifiesto, con su inclinación), no a 6 cm de la pista, porque la nave flota a 2 m; las distancias de lado del polvo (0,5–1,8 m, de la nave de la maqueta, de 1,87 m de semiancho) se **escalan con el semiancho** de cada nave. El polvo va a 0,3 m de la pista. Lo del brillo de levitación de la spec no aplica (ya no existe).
- El humo de los choques no cambia: cada bocanada de polvo lleva su `SmokeStyle` (frenado 2,2 / 0,6, opacidad 0,38, entrada 0,08 s, alto 0,7 × el ancho y tope a 0,5 m de la barrera). Comparten los máximos de 3000 chispas y 120 bocanadas y PARTICLES.
- Comprobado con una sonda en TIME TRIAL en TEST HILLS con VECTOR y el piloto: aterriza a ~0,07 m/s (sin efectos, correcto), porque llega a la cresta a ~298 km/h; con un aterrizaje medio y uno fuerte forzados en esos puntos, chispas y polvo en la bajada en cámara exterior. Aterrizajes reales, cabina, BLADE, HAMMER, rivales, barrera, LOW, pausa y RESTART, el usuario jugando. El polvo hereda el 50 % de la velocidad, como la maqueta: a 300 km/h queda pronto detrás de la cámara exterior.

## Humo de daños: etapa 1, humo nuevo

(spec `docs/efectos-4-humo-danos.md` v1.1, rama `feature/fx-damage-smoke`; la versión sube a 0.24.0 en la etapa 2): hecha, con tests de la lógica pura y validada por el usuario. Sustituye a lo que las secciones anteriores dicen del humo de daño de los cascos 3D (`CpuParticles3D`, 7 / 20 por segundo): ya no existe, ni `ClearSmoke` / `ClearHullSmoke`, y con él desaparece la bola gris que salía en el cielo al recolocar. El parpadeo de luces no cambia (las quemaduras, ver **Daño visible del casco**).

- En RACE, con daño 2, penacho gris (6,5 bocanadas/s) desde `Smoke`; con daño 3, casi negro y denso (10 + 7 + 7) desde los tres focos. Hereda la velocidad de la nave menos un retraso (6 %, entre 1,5 y 7 m/s): estela corta pegada a la nave a cualquier velocidad; en las curvas sigue la trayectoria que llevaba. Todas las naves; al recolocar se borra el de esa nave.
- Focos (perfil visual, 5 cm sobre la piel, medidos en los `.glb`): VECTOR (±0,75, 0,50, 2,20) y (1,84, 0,18, 1,60); BLADE (±0,60, 0,35, 2,40) y (1,10, 0,19, 2,00); HAMMER (0,40, 1,33, −0,06) y (±0,45, 0,72, 2,50).
- **Decisiones del usuario, cambian la spec**: máximo **420** bocanadas (la maqueta; la spec decía 300, y 8 naves en nivel 3 llenan casi 300); las brasas de la etapa 2 en un sistema aparte (las chispas de los choques no admiten gravedad ni vaivén por chispa); el humo se ordena de lejos a cerca cada frame. En LOW con tres focos salen 2 + 1 + 1 en el primer segundo (un acumulador por foco), 6/s de media.
- **Rendimiento** (el usuario notaba la carrera más lenta con el humo en la cara; medido a fondo, era efecto visual): con el humo pegado a la cámara (hasta ~220 bocanadas en pantalla, persecución y cabina, con y sin vsync, con un depurador conectado como F5) no baja de 144 fps y la GPU no cambia (~1,2–2 ms). Lo que sí se arregló, porque ya pasaba antes: pausas del recolector de .NET de 12–27 ms cada pocos segundos y un pico de ~20 ms al subir el daño.
  - Basura: rehacer una malla con arrays de C# dentro de un `Godot.Collections.Array` crea objetos con finalizador; el humo va en un `MultiMesh` con `RenderingServer.MultimeshSetBuffer` y un `float[]` fijo, y las demás mallas de `CollisionEffects` reutilizan su array y los opcionales de `AddSurfaceFromArrays`. Los textos en `SetShaderParameter("…")` e `IsAction…("…")` crean un `StringName` con finalizador en cada llamada: en `TrackElements`, `Track`, `Player`, `PauseMenu`, `Main`, `ShipHull3D`, `EdgeFlickerMaterial` y los paneles de depuración van como `static readonly StringName`. En `Main`, bucles `for` sobre `_rivals.Ships` y los contactos (un `foreach` sobre una `IReadOnlyList` crea un enumerador). De ~2,2 MB/s a ~0,7–0,9 MB/s y, en 18 s con daño, como mucho una recolección pequeña.
  - Subir el daño: encender el detalle de las quemaduras cambiaba de shader y Godot compilaba ~14 pipelines por nave en pleno golpe. Ahora va siempre encendido con un píxel transparente y solo cambia la textura: sin pico. Las tres naves sin daño salen idénticas al píxel al commit anterior.
  - **Para no volver a meter basura**: en lo que corre cada frame o cada paso, nada de textos en llamadas a Godot que piden `StringName`, nada de `foreach` sobre interfaces, LINQ ni lambdas, y nada de arrays de Godot nuevos. Para medir: `GC.GetAllocatedBytesForCurrentThread` alrededor del sospechoso y `GC.CollectionCount`; un `GD.Print` con la salida por una tubería cuesta hasta 15 ms; una ventana tapada mientras se usa el PC va a ~64 Hz y falsea la medida.
- Sin probar por el agente: chocar de verdad hasta los niveles 2 y 3, frenar hasta pararse, recolocarse con daño y TIME TRIAL (los validó el usuario jugando).

## Humo de daños: etapa 2, fuego y brasas

(spec `docs/efectos-4-humo-danos.md` v1.6, rama `feature/fx-damage-smoke`, versión **0.24.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec y las de efectos de nave. Sustituye a lo que la etapa 1 dice del penacho (A · PENACHO) como humo de daños y de los focos a 5 cm de la piel.

- **Decisión del usuario, B · ENVUELTO** (la otra variante de la maqueta): el penacho le parecía una nube demasiado grande. El humo va en los ejes de la nave y se mueve con ella: nace junto al foco, sale hacia la popa a 1,5–3,2 m/s y hacia fuera por el lado de su foco, vida y radio final × 0,85. `DamageSmokeWrap` en el Inspector de `CollisionEffects` (en false, A · PENACHO, que sigue en el código).
- Con daño 3, el humo sale naranja el primer 30 % de su vida (peso 0,8) y se apaga a gris.
- **Decisión del usuario, fuego distinto del de la maqueta** (las 4 gotas apiladas parecían largas y de hoguera): **una llama por foco** dibujada por un shader con ruido fbm que corre por su eje, con base ancha, cola que ondula y se rompe en lenguas, núcleo amarillo claro, cuerpo naranja y bordes rojo oscuro, en suma; 0,40 m de largo y 0,22 m de ancho a tamaño 1, con el largo entre 0,85 y 1,15. **Barrida por el viento**: su eje va de la vertical (parada) a casi hacia atrás contra la velocidad (del todo desde 55 m/s, sigue subiendo un 40 %), se alarga ×2 y se estrecha un 20 %. **Llamaradas**: en cada tramo de 3 s, con un 55 %, una de 0,25 o 0,6 s (alternas entre tramos y entre focos vecinos, ± 20 %): largo × 1,8, ancho × 1,35 y brillo × 1,3, también el halo. Halo pequeño y tenue (0,8 m, 0,12). **Tamaños distintos**: 1 en `Smoke`, 0,7 en `Smoke2`, 0,5 en `Smoke3`. **Pegado al casco**: nace en la piel bajo cada foco (`SkinGaps`, medidos en los `.glb`: VECTOR 0,22 / 0,22 / 0,05 m, BLADE 0,13 / 0,13 / 0,05, HAMMER 0 / 0,05 / 0,05); parada, la base se mete 6 cm en la piel; inclinada, nada (si no, se quedaba dentro de la góndola redonda de VECTOR).
- Brasas: 3/s con daño 2 y 5 por foco con daño 3 (× PARTICLES), máximo 400, gravedad 2 m/s² y vaivén lateral, trazo con la velocidad relativa a tu nave; sistema aparte de las chispas de los choques.
- Fuego y brasas se dibujan **después del humo** (`RenderPriority` 1): con el humo oscuro encima, el fuego se veía apagado. Llama y halo en `MultiMesh` con búfer fijo y brasas en un `ImmediateMesh`: sin basura por frame. Tu fuego no se dibuja en cabina; tu humo y tus brasas sí. Recolocar borra humo y brasas de esa nave; el fuego sigue. La pausa lo congela todo (el ruido y las llamaradas van con el tiempo de la carrera).
- Comprobado con sondas en RACE (daño forzado, piloto, persecución y cámara orbital, las tres naves, parada y a fondo): la pausa congela el fuego al píxel. Con las 8 naves ardiendo, frame medio +0,07 ms, peor frame 6–11 ms y ninguna recolección del GC en 6 s. Cabina con daño 3, PARTICLES en LOW, ir detrás de un rival ardiendo y los fps con varios rivales dañados, el usuario jugando.

## Daño visible del casco

(spec `docs/efectos-5-danos-casco.md` v1.0, rama `feature/fx-damage-smoke`, versión **0.25.0**): hecho, con tests de la lógica pura y validado por el usuario. Propuesta del agente, aceptada sin maqueta. Sustituye a lo que las secciones anteriores dicen de las **quemaduras** de los cascos 3D (máscaras `hull_scorch_1/2/3.png` por la UV2, que oscurecían la pintura en manchas por todo el casco): ya no existen, ni sus archivos, ni su escritura en `art/blender/hull_vector.py`, ni la entrada `scorchMasks` de los manifiestos. El parpadeo de las luces y las reglas del daño no cambian. Solo RACE, todas las naves.

- **Rozaduras** donde se choca: cuatro zonas que solo suben en la carrera (también tras recolocar): roce 0,15/s, medio +0,25, fuerte +0,5, tope 1. Barrera → su costado; otra nave → la zona del punto del contacto (más allá del 60 % de la caja hacia el morro o la popa, esa zona). Se ven como pintura desgastada y **arañazos largos a lo largo de la nave**, de metal claro poco metálico (muy metálico solo reflejaba el entorno oscuro y salía negro).
- **Hollín** alrededor de los focos del humo, en la piel: nivel 2, `Smoke` (0,9 m, 0,8); nivel 3, los tres (1,4 m × 1 / 0,75 / 0,55, 0,95). **Grietas** incandescentes con daño 3, finas, hasta el 45 % del radio del hollín, latiendo; la pausa las congela.
- Conexión: `Main.UpdateCollisionEffects` pasa los roces y golpes de cada paso a `Player.WearBarrier` / `WearShip` y a `RivalShip.WearBarrier` / `WearShip`; cada nave lo lleva a `Spaceship.SetHullWear` solo cuando cambia.
- **Trampas ya resueltas**: las piezas de los `.glb` traen cada una su transformación (por eso la pasada recibe la inversa de la raíz del modelo y no usa `VERTEX` tal cual); algunas, como el fuselaje de VECTOR, traen las caras y las normales al revés, así que la pasada va por las dos caras y decide el costado por la posición, con la normal sin signo.
- Comprobado con una sonda en RACE, nave parada en la parrilla y cámara orbital, desgaste y daño forzados, en las tres naves. Rozaduras de verdad conduciendo, desgaste parcial, rivales dañados, pinturas perladas de VECTOR, la pausa con grietas y los fps, el usuario jugando.
- **Cristal de la cabina** (spec v1.1, sección 6, pedido por el usuario): con daño 1 / 2 / 3, 3 / 6 / 10 picadas, 1 / 2 / 3 grietas largas quebradas que salen de ellas, se ramifican y se alargan por el mismo camino al subir de nivel (gris humo al 45 %, elegido por el usuario, ~1,5 px) y 0 / 1 / 3 manchas con salpicaduras (al 30 %), por casi todo el cristal y empujadas hacia los bordes, en sitios sorteados una vez por carrera; para que no parezcan pegatinas, se apagan a trozos a lo largo y cogen la luz de la cabina (boost, daños, chispas) con una cara de fractura por tramo, sin brillo propio (validado así por el usuario: un brillo base las hacía demasiado visibles; el mundo no llega al `SubViewport` de la cabina, así que no hay refracción real); el usuario descartó una primera versión de grietas en estrella; pegadas al cristal (pasada detrás de `M_GlassSmudge`, que tienen las tres cabinas, en los ejes de la pieza), tapadas por pilares y arco. Solo RACE y tu cabina. Comprobado con una sonda en las tres cabinas con daño 1, 2 y 3 forzado (con daño 3, ~6,9 ms de media a 144 Hz, peor frame < 10 ms); conduciendo y con F10, el usuario jugando.
- **Cristal de la cabina por fuera** (spec v1.2, sección 7, pedido por el usuario): las mismas marcas en las piezas de cristal de los cascos 3D (`Canopy_Glass` de VECTOR y BLADE, las tres `glass` de HAMMER), para todas las naves en RACE, proyectadas sobre las dos medidas más largas de cada pieza; más claras y opacas que dentro (gris claro al 85 %), porque el cristal brillante se comía el gris humo. Comprobado con una sonda en cámara de persecución y orbital en las tres naves; rivales dañados, el usuario jugando.

## Toberas: etapa 1, llama nueva

(spec `docs/efectos-6-toberas.md` v1.1, rama `feature/fx-thrusters`; la versión sube a 0.26.0 en la etapa 3, ya hecha): hecha, con tests de la lógica pura y validada por el usuario. Primera spec de la segunda tanda de efectos de nave (6 toberas, 7 estelas, 8 luces de la nave, 9 visión túnel; el 5 es `efectos-5-danos-casco`). Todas las naves llevan llama en las toberas: tú y los rivales en carrera, el fantasma y el fondo del menú; el holograma y las naves de prueba, no.

- **`efectos-velocidad` sigue sin hacer**: su sección 5 (la tobera de líneas) queda sustituida por esta spec antes de hacerse; el resto (visión túnel, líneas de velocidad, FOV, partículas y chispas de las toberas, estelas) sigue fuera. No había tobera vieja que quitar. Esta spec define su propio `th` = velocidad / punta base.
- Llama de dos capas por boca (penacho y núcleo), dos conos de una malla compartida de 28 × 28 deformados por un shader sin luz, en suma, sin escribir profundidad, con ruido fbm de la maqueta (`designs/efectos-toberas.html`). Reacciona a acelerar, soltar, frenar (se encoge y titila), girar (la tobera de fuera hasta un 28 % más larga), derrapar (la punta hacia dentro de la curva) y boost (blanco cálido, naranja y rojo, con el anillo de encendido en cada boca, la segunda 0,04 s después). Se actualiza en `_Process` con lo que la nave le pasa en cada paso de física: la pausa la congela.
- **Inventario** (spec 2.1): bocas `Exhaust_L/R` de los cascos 3D, en el mismo sitio que las del wireframe. **Escala y achatado de la boca, los de las specs de los cascos**: VECTOR 1 / 0,46, BLADE 0,67 / 0,35 y llama × 0,85, HAMMER 0,79 redonda. `βmáx` = 22° (la maqueta y `DriftIntensity`), no los 60° del juego. Puntos del retroempuje ya en el perfil, para la etapa 3.
- **Decisión del usuario**: la llama de HAMMER sin boost es **naranja** (núcleo `#FFB46A`, medio `#FF5A14`, fuera rojo oscuro), como decidió su spec de casco; con boost, la de todas. La boca del casco 3D (`ShipHull3D.SetNozzleGlow`) toma el color medio y su emisión sigue la potencia (con P = 0,3 sin boost, la de siempre): **VECTOR y BLADE pasan de azul a cian**; HAMMER sigue naranja.
- Se apartan de la spec o la concretan: los colores no salen de `Palette` (como los demás efectos: los de boost en `ThrusterFxSettings` y los normales en el perfil de cada nave), pasados tal cual da la maqueta; el ruido corre integrado (`∫ vr dt`), porque `t × vr` daba saltos con la carrera avanzada; los ajustes, en `ThrusterFxSettings` y los principales en el grupo **Toberas** del Inspector del `Player` (`ThrusterBrightness` para ajustar con el glow, tiempos, encendido, largo, empuje de fuera, doblado, `βmáx` y anillo), que también usan los rivales; fantasma y menú, los de por defecto.
- En la cuenta atrás, la llama ruge con W (se lee el teclado aparte, solo visual); terminada, sin gas. Los rivales guardan su entrada (`RivalCar.LastInput`). El fantasma va siempre con gas, con la velocidad de su pose, sin boost, y su intensidad × su opacidad (`GhostShip.SetShip`). Recolocación, `PlaceAt` y recolocación de un rival: al ralentí al momento. En cabina tu llama no se ve; las de los rivales, sí.
- Comprobado con capturas en RACE con VECTOR (ralentí, a fondo, girando, frenando) y el menú sin errores; `hint_screen_texture` probada en la carrera para la etapa 2. Boost, derrape, BLADE, HAMMER, rivales, fantasma y el brillo, el usuario jugando. Rendimiento sin medir (4 conos por nave): prueba 13 de la etapa 2.

## Toberas: etapa 2, diamantes, calor y luz

(spec `docs/efectos-6-toberas.md` v1.1, sección 5, rama `feature/fx-thrusters`; la versión sube a 0.26.0 en la etapa 3): hecha, con tests de la lógica pura y validada por el usuario. Solo en carrera (tú y los rivales); el fantasma y el menú siguen solo con la llama (`EnableThrusters(…, full: false)`).

- **Diamantes de choque**: bandas brillantes quietas dentro del penacho, desde el 60 % de potencia y sobre todo con boost (`ThrusterDiamonds`).
- **Calor**: un plano de cara a la cámara detrás de cada boca que desplaza la imagen con dos ruidos, dentro de una elipse, con `hint_screen_texture`; con PARTICLES en LOW no se dibuja. Se dibuja **antes que las llamas** (prioridad −1): la textura de pantalla no lleva lo transparente y, después, borraba la llama de detrás. **Decisión del usuario tras probarlo**: como el desplazamiento es de pantalla, una nave lejana se veía doble (espectros a 2–3 m de ella); ahora va con toda su fuerza hasta 8 m de la cámara, × 8 / distancia más lejos y se apaga de 30 a 60 m (`ThrusterHeat`, `ThrusterHeatReference`, `ThrusterHeatFadeStart`, `ThrusterHeatFadeEnd`).
- **Luz en el casco**: una `OmniLight3D` por boca, 0,7 m detrás, rango 7 m, sin sombras, en la capa de su casco (`Spaceship.Hull3DLayer`), del color medio de su llama: **naranja en HAMMER**. Solo con casco 3D. `E0` = **4** (`ThrusterLightEnergy`; con el 1 de la spec la popa apenas se teñía). Límite conocido: los rivales comparten capa, así que la luz de uno toca a los que tenga a menos de 7 m.
- Medido en RACE con VECTOR y las 8 naves, 8 s a fondo desde el GO, a 144 Hz: frame medio igual (6,95 ms); sin vsync, 2,52 ms con las toberas y 1,85 sin ellas (unos 0,67 ms). Los frames lentos que salen (GO y hacia los 7 s) salen igual con las toberas apagadas. No se ha probado en un PC flojo.

## Toberas: etapa 3, retroempuje y daño 3

(spec `docs/efectos-6-toberas.md` v1.1, secciones 6 y 7, rama `feature/fx-thrusters`, versión **0.26.0**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec de las toberas.

- **Retroempuje**: al frenar por encima de 20 km/h, dos chorros azules cortos en el morro (los puntos de `ThrusterShape.Retro`), con la misma malla y shader que la llama, redondos, sin diamantes ni doblado ni escala de la boca, con su ruido (velocidad 12). Solo en carrera; en tu cabina no se ve.
- **Daño 3** (solo RACE: el nivel llega por `ThrusterInput.DamageLevel`, el tuyo con `CockpitDamageEnabled` y el de cada rival el suyo): por tobera, cortes al 15 % (18 % de probabilidad, 0,05–0,12 s, esperas de 0,12–0,5 s) que bajan llama, boca, luz y calor, y chasquidos naranjas cada 0,4–1,2 s, escalados con la boca. Semilla por nave (de su desfase). Convive con el fuego y el humo de daños.
- Comprobado con capturas en RACE con VECTOR y daño 3 forzado con una sonda temporal (cortes y chasquidos junto al humo; los chorros del morro, con la cámara orbital F4: desde la de persecución los tapa el casco). Pausa, recolocación, fantasma, BLADE y HAMMER, el usuario jugando.

## Estelas: etapa 1, cinta

(spec `docs/efectos-7-estelas.md` v1.0, rama `feature/fx-trails`): hecho, con tests de la lógica pura y validado por el usuario. Cinta de luz por punta de ala, tú y los rivales; ni el fantasma ni el menú.

- `efectos-velocidad` 6.2 nunca se hizo: no había líneas que quitar ni tests que adaptar.
- **Decisión del usuario**: la carga por giro sale de la **dirección de la marcha** (la velocidad), no del morro (`Yaw`), como la maqueta; con el morro, entrar en un derrape la disparaba.
- **Decisión del usuario, tras probarla**: la mitad de larga (`LifeScale` 0,5: 0,575 s a la punta, 0,8 s de máximo), el 80 % de ancha (`WidthScale`) y brillo 0,7. Los valores de la spec siguen en el código y estos los multiplican.
- Colores en el proveedor (`WingTrailColors`), no en `Palette`, como los de las toberas: la lógica es pura.
- Solo se guarda una muestra si la punta se ha movido: sin interpolación de la física, la nave se mueve a 60 Hz aunque se dibuje a 144. La carga se calcula en cada paso de física (`Move`) y se suaviza en cada frame.
- Medido en RACE con 8 naves a 144 Hz: ~30 µs por nave y frame (peor ~0,2 ms). 6 pasadas de 20 s con estelas y 6 sin ellas: frames lentos sueltos en las dos, en carreras con choques.

## Estelas: etapa 2, pulsos

(spec `docs/efectos-7-estelas.md` v1.0, versión **0.27.0**): hecho, con tests de la lógica pura y validado por el usuario. Cierra la spec.

- **Decisión del usuario, tras probarlo**: **sin vórtice** (sección 5.1 y prueba 10): no le gustó. La estela es la de la etapa 1.
- Pulsos como en la spec, en el shader de la cinta (D y el boost de cada muestra en `UV2`). La fase (25·t) va módulo una vuelta (`WingTrailRibbon.PulsePhase`): da lo mismo y el shader no pierde precisión en carreras largas. `WingTrailRibbon.Pulse` repite la fórmula del shader para los tests.
- Con el piloto automático no hay boost: los pulsos solo se vieron jugando, el usuario. Rendimiento sin volver a medir (solo cambia el shader).

## Cabinas 3D · morros: etapa 1, Blender

(spec `docs/cabinas-3d-4-morros.md` v1.0, rama `feature/cockpit-noses`; la versión sube en la etapa 2): hecha y validada por el usuario mirando las capturas y el `.blend`. Para el jugador, en cabina desaparece el morro propio de cada cabina (la copia del casco está en el modelo, pero oculta hasta la etapa 2), con sus luces; VECTOR sin viga central, BLADE con el cristal HUD más alto y HAMMER sin la rejilla del parabrisas por fuera.

- Los tres `hull_<nave>.py` tienen `build_hull(out)` y solo ejecutan `main()` lanzados directamente: las cabinas los importan. Los `.glb` de VECTOR y BLADE salen idénticos byte a byte. **Cambiar un casco obliga a volver a ejecutar su cabina.**
- `lib/nose_copy.py`: corte, quitar piezas y mover al ojo sobre las sopas del `Builder`, antes de Blender (sin bisección de Blender); las piezas cortadas no se tapan, porque el corte queda 30 cm detrás del ojo. Objetos `Nose_` + su nombre en el `.glb` del casco (también los juntados por zona y material, p. ej. `Nose_Fuselage_M_Carbon`; la sonda de BLADE es `Nose_Nose_Pitot`). Entrada `nose` en el manifiesto de cada cabina (piezas, nombre en el casco, material, si es viva, caja); los barrotes de BLADE, enteros, en `nose.frame`.
- **Trampa ya resuelta**: los materiales del casco se llaman a veces igual que los de la cabina (`M_Paint`, `M_Dark`…) y Blender los renombraría a `.001`; además `Cockpit3DView` aplica reglas por nombre. Dentro de la cabina llevan el prefijo `Hull_` (también las imágenes). Godot saca sus texturas con un número cuando el archivo se llama igual (`cockpit_vector_T_Carbon_color_14.png`).
- **Decisión del usuario, cambia la spec 3 y 4**: como la cúpula y la cabina de grúa del casco son más bajas que las cabinas, en el mismo espacio el casco tapa el salpicadero de BLADE y HAMMER (suelo de la cúpula, barrotes, lomo) a centímetros del ojo. La maqueta no lo tiene porque pinta la cabina encima del casco. En la etapa 2 la copia se pinta **en una pasada propia detrás de la cabina**, con la misma cámara y luces. Las capturas `<nave>_eye_new.png` van así (dos capas juntadas); `_eye_new_depth.png`, en el mismo espacio.
- HAMMER sin `Guard_*`: su techo sin extras baja de 1,084 a **1,055** (alto 1,575), en el script y en su test. El manifiesto del casco gana `builtPieces` (todas las piezas con nombre, también las juntadas) para poder comprobarlo. VECTOR gana `zones` (sin `Halo`) y BLADE `combinerCenterY` (−0,026).
- Medido: la copia suma 9.196 triángulos en VECTOR (cabina 46.720), 16.647 en BLADE (79.371) y 30.438 en HAMMER (66.848). Puntas: VECTOR (0; −0,80; −3,05), BLADE −4,75 y sonda −5,30, HAMMER cara −2,57 y defensa −2,77. Dos ejecuciones, mismo `.glb`.
- Godot, lo mínimo: `Led_Nose_*` y `Led_Canard_*` fuera de los contratos y del parpadeo (VECTOR 9 luces, BLADE 8, HAMMER 9); `CockpitManifest.Nose` y `Cockpit3DView.HideHullCopy`, que se quita en la etapa 2. Sin comprobar a propósito que el script falla con una pieza detrás del corte.

## Cabinas 3D · morros: etapa 2, Godot

(spec `docs/cabinas-3d-4-morros.md` v1.0, rama `feature/cockpit-noses`, versión **0.27.1**): hecha, con tests de la lógica pura y validada por el usuario. Cierra la spec. Sustituye a lo que las secciones anteriores dicen del ojo de las tres cabinas (VECTOR (0; 0,55; −1,2), BLADE 0,42 y HAMMER 0,87, los dos a z −1,2), de sus morros propios y de sus `Led_Nose_*` / `Led_Canard_*`, y de `HideHullCopy` de la etapa 1.

- Ojo de la tabla 3.3 en `CockpitFraming.Eye`, que ya iba en coordenadas del casco: VECTOR (0; 0,80; −0,95), BLADE (0; 0,37; −0,45), HAMMER (0; 0,805; −1,03); con la nave a 2 m de la pista, 2,80 / 2,37 / 2,805 m. Las alturas de la spec son las de la maqueta (levitación 0,65 / 0,75).
- **Pasada de la copia** (decisión del usuario, etapa 1): `Cockpit3DView` tiene un segundo `SubViewport` con el mismo mundo (`World3D = FindWorld3D()` del primero) y una cámara que copia la de la cabina y solo ve la capa 20 (`NoseLayer`); su imagen va debajo de la de la cabina. La copia no da sombra y la luz principal de la cabina no la toca.
- **Pintura, daño y luces**: `HullNoseCopy` (la crea la cabina) y `ShipHull3D.SetNoseCopy` (se lo pasa `Player` tras crear la cabina y tras F10; solo si la copia es de ese casco). Las luces y las calcomanías comparten el material del casco (`HullNoseCopyRules.SharesMaterial`); el resto lleva una copia, rehecha al cargar y al cambiar de pintura, con su propia pasada del daño (sus ejes: el ojo + la inversa de la raíz del modelo) que recibe los mismos parámetros. Comprobado con una sonda en las tres: mismos materiales pieza a pieza tras cambiar de pintura y con daño 3 y roces delante.
- **Se aparta de la spec 4.3** (validado por el usuario): con las luces de la cabina la copia salía entre 4 y 8 veces más oscura que el casco en persecución, y bajar reflejos no lo arregla. Se ilumina como el casco y como en la maqueta: `ShipHull3D` pone un `HullLightRig` en la capa de la copia, colgado del modelo de la cabina, y la cámara de la pasada lleva una copia del entorno de la carrera (sin tonemap). Reflejos × 1 (`Cockpit3DScene.NoseReflections`). Las omni de la cabina (boost, daño, chispas) sí la tocan.
- **Decisión del usuario, cambia la spec 3.2 y el casco**: los barrotes de BLADE, solo por detrás del arco de la cúpula (z −1,15), dentro y fuera (`nose_copy.clip_back` en `hull_blade.py`); la parte de delante, donde convergen, tapaba la vista al pilotar. Casco de 32.148 a 31.836 triángulos. En el `.glb` del casco van juntos en `Canopy_M_Gunmetal`: el manifiesto de la cabina los apunta ahí.
- El retro (`efectos-6`) no se ve en cabina porque `Spaceship` oculta las toberas en esa vista: la copia no tapa nada nuevo. Los puntos blancos sobre el morro de BLADE salen igual sin la copia (y en la maqueta).
- Medido conduciendo en cabina 10 s con y sin la pasada: a 144 Hz, 6,94 ms en las tres con y sin ella; sin vsync, de 2,46 a 2,63 ms (VECTOR), de 3,04 a 3,17 (BLADE) y de 2,52 a 2,80 (HAMMER). Frames lentos sueltos (20–36 ms) igual con y sin ella. No se ha probado en un PC flojo.
- Para sondas: `Player` cambia de cámara en `_UnhandledKeyInput`, así que hay que mandar la tecla C (`InputEventKey`), no la acción; `FindChildren` busca por la clase de Godot, no por la de C#.

## Luces de la nave: etapa 0, inventario

(spec `docs/efectos-8-luces.md` v1.2, rama `feature/fx-lights`): informe hecho y validado por el usuario, sin cambios en el código. La spec pasa a la v1.2 antes de empezar (es la 8: toberas 6, estelas 7; las maquetas en `designs/`).

- **Decisiones del usuario**: suelo de pista y túnel fuera (7.2): la luz en pista y los faros se dejan preparados, en una **capa propia libre (la 2)**, hasta la spec del suelo. **Sombra de contacto aplazada** a esa spec (no hay suelo ni brillo de levitación sobre los que se vea); las siluetas sí salen en la etapa 1. **Retro a 45° hacia arriba** en el casco y en la cabina (`Retro_*_Point` a (0; 1; −1)), más discreto en cabina; si la boca no cuadra con el chorro, se propone el ajuste con capturas. Trampilla de BLADE también en la copia del morro. **TIME TRIAL**: una «luz» del encendido por segundo durante el `READY?` y el golpe en el `GO!`.
- **Lo encontrado**: la pista y el túnel son **solo líneas** *unshaded*, en la capa 1 con todo lo demás. Capas usadas: 1, 11 (tu casco), 12 (rivales, compartida) y 20 (copia del morro). Cada pieza viva ya tiene material propio por nave (`ShipHull3D` duplica al cargar y guarda la base del manifiesto); la copia del morro comparte el de las luces. Los tipos irán en el **manifiesto** (tipo, centro y dirección hacia fuera, que hoy no lleva). Parpadeo de hoy: `Spaceship.ShowEdgeFlicker` → `ShipHull3D.ShowLightFlicker` (× 0,2).
- **Entradas**: `ThrusterInput` ya lleva freno, derrape, boost, velocidad, punta y daño (0 en TIME TRIAL), para ti y los rivales; el giro suavizado τ 0,12 es `ThrusterState.Steer`; `P`, `bt`, `R` en `ThrusterState`. Salida: `StartSequence.LightsOn` / `Started` y `ReadySequence`. Altura: `ShipVertical.Height` − superficie − levitación.
- **Para la etapa 3**: los colores del retro son comunes (`ThrusterFxSettings`): HAMMER naranja pide pasarlos al perfil. Los chorros cuelgan de `ThrusterFlames`, oculto entero en cabina: hay que separar su visibilidad. En cabina la copia del morro se pinta encima del mundo y tapa la parte del chorro sobre el morro. Chispas: se reutiliza `SparkField` con una entrada nueva y color fijo.
- Rendimiento previsto: Forward+ sin límite por objeto y 512 elementos de luz por vista; unas 70 omni y 20 focos con 8 naves. Se mide en la etapa 3.

## Luces de la nave: etapa 1, Blender

(spec `docs/efectos-8-luces.md` v1.2, rama `feature/fx-lights`; la versión sube en la etapa 4): hecha, con tests de los manifiestos y validada por el usuario mirando las capturas. Faros (VECTOR visera, BLADE rendijas, HAMMER faros redondos sobre la defensa), freno en «=» de BLADE, freno central de HAMMER, toberas de retro en el morro y siluetas, en `art/blender/lib/lamps.py`; las tres cabinas, regeneradas (su copia del morro lleva los faros y el retro).

- **Se aparta de la maqueta, validado**: las ranuras del retro de BLADE van a (±0,30; −2,60), entre las placas de blindaje 0 y 1: en (±0,35; −2,30) quedaban debajo de la placa 1, también en la maqueta. La trampilla lleva `M_RetroFlap` (el de la maqueta), no el del blindaje: Godot solo pinta las placas de su contrato. Cañón del retro de HAMMER como la maqueta (0,09 en la boca, 0,085 detrás; la spec lo dice al revés), abierto por delante y con tapa detrás; se mete 2–5 cm en el costado del morro, como en la maqueta.
- Contratos: VECTOR 50 piezas, BLADE 51, HAMMER 78; vacíos `Spot_Head_*`, `Retro_*`, `Retro_*_Point` (con `forward` (0; 1; −1) normalizado, el retro a 45°) y, en BLADE, `Retro_*_FlapHinge` (`HullContract.LampEmpties`, `LampPieces`). La trampilla lleva el origen en la bisagra. El manifiesto gana el centro de cada luz (`center`, para los halos de la etapa 2) y `shadow` (imagen, píxeles, tamaño, centro, fila 0 en el morro). Las tomas dorsales de VECTOR llevan la pintura y el perlado del casco (`ShipHull3D`).
- Silueta rasterizada con numpy desde las sopas (sin render), ×4 y reducida, con desenfoque gaussiano σ 7 px; BLADE sin la sonda.
- Hasta la etapa 2, en el juego los faros van a su intensidad de partida (6–7) y el freno nuevo de BLADE y HAMMER, rojo fijo a tope.
- Comprobado: dos ejecuciones dan los mismos `.glb`, manifiestos y siluetas; Godot importa y una carrera sin ventana carga las tres naves sin errores. Copia del morro: VECTOR 10.184 triángulos, BLADE 22.331, HAMMER 34.326. No se ha visto en el juego con glow.

## Luces de la nave: etapa 2, pilotos, halos, freno y giro

(spec `docs/efectos-8-luces.md` v1.2, rama `feature/fx-lights`): hecha, con tests de la lógica pura y validada por el usuario jugando, con los valores de la spec. Tú y los rivales; el fantasma, el menú y el holograma, como antes.

- Tipos de las luces en el manifiesto (`lights`: tipo y dirección del halo, desde `lamps.LIGHT_TYPES`); el lado sale de la x del centro de la pieza. Las toberas no están (son de `efectos-6`); las bocas del retro y los faros, con su intensidad de partida hasta la etapa 3.
- El parpadeo con daño de siempre (× 0,2) pasa a ser un factor más del estado de las luces (`ShipHull3D.ShowLightFlicker` → `HullLights.SetFlicker`) hasta que la etapa 4 lo sustituya. La anticolisión y las balizas siguen con su lógica: el estado lee su intensidad del material y solo les pone el halo. La pintura de VECTOR pasa su color de partida (`SetBaseColor`).
- Halo: cuadrado de cara a la cámara en un `MultiMesh` por nave, en suma, que lee la profundidad; se acerca 8 cm a la cámara para que la pieza no lo corte. Opacidad × BLOOM × la opacidad del casco atravesable.
- **Trampa ya resuelta**: escribir los materiales y el búfer de los halos en cada frame costaba ~0,8 ms por frame con 8 naves. Solo se escribe lo que cambia más de un 1 %. Medido sin vsync, 4 carreras con y 4 sin luces: media de 2,15–2,68 ms frente a 2,26–2,48, dentro del ruido; sin frames lentos.
- Comprobado con capturas en RACE con VECTOR (parrilla, frenando, girando). Boost, BLADE, HAMMER y rivales, el usuario jugando.

## Luces de la nave: etapa 3, faros, retro y luz en pista

(spec `docs/efectos-8-luces.md` v1.2, rama `feature/fx-lights`): hecha, con tests de la lógica pura y validada por el usuario jugando, con los valores de la spec. Sin sombra ni suelo que reciba luz (v1.2: van con la spec del suelo).

- **Retro** (cambia `efectos-6`): los chorros salen de `Retro_*_Point` a 45° hacia arriba, escalados por nave (`ThrusterShape.Retro`: `RetroJetShape` con ancho, alto y colores; HAMMER naranja); se quitan los puntos viejos del perfil y los colores comunes de `ThrusterFxSettings`. **En cabina se ven** tus chorros a la mitad (`RetroCockpit` 0,5): las toberas siguen visibles con `ThrusterFlames.CockpitView`, que oculta la llama, el calor y sus luces. Boca 0,15 + 4·R, luz ER × 2·R en la capa del casco, trampilla de BLADE en el casco y en la copia del morro.
- **Se aparta de la spec**: la prueba 9 pide la trampilla abierta del todo con R = 0,5, pero la fórmula (mín(1; 1,4·R), también la de la maqueta) la abre del todo desde R ≈ 0,71; se sigue la fórmula.
- Faros: focos por nave (HAMMER, 4 con los de trabajo, cuyas lentes van × 1,6) y haz visible, que se apaga con la cámara dentro del cono, a la mitad en cabina y con BLOOM a 0. Luz de freno sobre el casco detrás del freno nuevo (VECTOR, del medio de sus pilotos). Luz en pista y focos, preparados en la capa 2 (`ShipWorldLights.TrackLightLayer`, vacía): sus intensidades de la maqueta pasan a Godot con `TrackEnergyScale` y `SpotEnergyScale`, puestas a ojo sin poder verlas.
- **Trampas ya resueltas**: los nodos de los vacíos del `.glb` van en los ejes de Blender (la rotación de la raíz está encima): `ShipHull3D.EmptyPosition` lee ahora las posiciones del manifiesto, que van en los ejes de la nave (nadie más lo usaba). La bisagra de la trampilla en la copia del morro sale de lo que se ha movido su caja respecto a la del casco, cuyo origen es la bisagra.
- Medido sin vsync, 4 carreras con y 4 sin luces: media de 2,15–2,94 ms frente a 2,02–2,28 (unos 0,4 ms), sin frames lentos y con peores frames parecidos; la diferencia no se pudo atribuir a la luz en pista ni al haz (el ruido es del mismo tamaño). Sin medir en la salida con los 8 ni en un túnel.
- Comprobado con capturas en RACE con las tres naves frenando (persecución, cabina y orbital), con los récords, fantasmas y ajustes del usuario apartados y devueltos.

## Pendiente

- `docs/naves-disenos.md` no recoge todavía las cabinas elegidas. El resto de opciones del menú (CHAMPIONSHIP, HANGAR), ajustar el contorno interior de la cabina. Reajustar la dificultad de los rivales con circuitos nuevos (decisión del usuario).
