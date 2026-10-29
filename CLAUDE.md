# VECTORY

Juego de carreras futuristas con estética vectorial/wireframe de los años 80. Godot .NET (C#), renderizador Forward+, para Windows/PC. Vista principal desde cabina, con cámara alternativa en tercera persona (tecla C).

El diseño completo está en `docs/VECTORY_Documento_Maestro.txt`. Léelo cuando necesites decidir algo de diseño o de alcance; este archivo es solo el resumen para trabajar. Las specs de cada feature van en `docs/` (por ejemplo `docs/boost.md`) y, donde lo indiquen, mandan sobre el documento maestro.

## Cómo trabajamos

- El usuario no escribe código a mano: tú escribes el código. Entrega **archivos completos y listos para usar**, nunca fragmentos sueltos.
- No inventes código ni APIs. Si no estás seguro de algo de Godot o de C#, dilo y compruébalo.
- Entregas pequeñas y fáciles de validar. Primero base funcional, luego mejorar, luego ampliar.
- Decide tú lo técnico secundario. Pregunta solo ante un bloqueo real o una decisión de producto que no se pueda deducir.
- No amplíes el alcance por iniciativa propia ni añadas funciones "por dejarlas preparadas".
- No declares algo terminado solo porque compile. Di qué has probado, qué queda pendiente y qué limitaciones conoces.
- No refactorices ni reformatees archivos ajenos a la tarea. Conserva lo que ya funciona.
- **El usuario prueba el juego a mano** (conducción, cámara, HUD). Los tests automáticos no sustituyen eso.
- **Commit solo cuando el usuario valide**, un commit claro por etapa validada, con mensaje descriptivo.
- El usuario habla en español, de forma informal y directa. Responde en español.

## Arquitectura

- El juego arranca en el menú principal (`scenes/MainMenu.tscn`, escena principal del proyecto). RACE carga la carrera (`Main.tscn`, en la raíz), que se sigue pudiendo ejecutar sola desde el editor. Todo cambio de escena o salida del juego pasa por `Game/SceneChanger` (`ChangeTo`, `Reload`, `Quit`).
- La escena de carrera se monta por código desde `Game/Main.cs`. Si `Track` y `Player` ya existen en la escena, se usan tal cual (así se afinan sus parámetros en el Inspector); si no, se crean.
- Cada circuito es un recurso `TrackDefinition` en un `.tres` de `circuits/` (puntos de control en píxeles, `MetersPerPixel`, `ImageCenter`, sentido, ancho de pista y número de checkpoints). `Track` lo recibe en su export `Definition`; si no hay ninguno asignado carga `res://circuits/circuit_01.tres`. Lo común a todos los circuitos (altura, arcos, parrilla...) sigue como export del `Track`.
- Si el circuito no se puede generar (sin `TrackDefinition` o no válido), `Track.IsBuilt` es `false`: `Track` y `Main` lo avisan con `PushError` y `Main` no monta parrilla, límites, carrera ni HUD (así no se cierra el juego).
- `Main` crea la carrera (`RaceSession`) y se la pasa al `Player` (`SetRaceSession`). El `Player` le pasa su posición cada paso de física y solo acepta input si `RaceSession.CanDrive` (estado `Running`); no conoce la secuencia de salida. Repetir la carrera = recargar la escena.
- Los nodos de Godot (`Player`, `Track`, `Hud`...) solo **conectan piezas**. Las reglas viven en clases puras sin dependencia de Godot (usan `System.Numerics`), para poder probarlas con xunit.
- SOLID sin sobreingeniería. Pruebas unitarias cuando haya lógica que lo justifique.
- Los parámetros de ajuste van como `[Export]` en el Inspector y luego se pasan al código por defecto.

## Estructura (`scripts/`)

- `Circuit/`: `Track` (genera la geometría del circuito, la línea central y las marcas de parrilla a partir de un `TrackDefinition`; calcula dónde colocar la nave en su marca), `TrackDefinition` (recurso con los datos de un circuito), `TrackLimits` (límites laterales), `RaceTracker` (estados de carrera, vueltas, sectores, cronómetro, mejores tiempos con su vuelta, posición final y `RaceResult`), `RaceSession` (ciclo de carrera: conecta `Track`, `StartSequence` y `RaceTracker`), `StartSequence` (semáforo y cuenta atrás), `GridLayout` (posiciones de parrilla y reparto aleatorio), `RaceResult` (datos de la pantalla de resultados), `LineCrossingDetector` (cruces de meta y checkpoints).
- `Core/`: `Palette` (colores), `TimeFormat` (formato `m:ss.mmm` de los tiempos, redondeado al milisegundo; úsalo siempre para mostrar tiempos), `VersionFormat` (`v0.1.0`; `v0.0.0` si falta).
- `Game/`: `Main` (escena de carrera), `SceneChanger` (cambio de escena y salida), `WorldStyle` (WorldEnvironment común: fondo y glow; lo usan carrera y menú).
- `Menu/`: `MenuModel` + `MenuOption` (navegación pura: salta desactivadas, da la vuelta, `Select`, `Confirm`), `MainMenu` (capa 2D: título, opciones y versión; teclado y ratón), `MenuBackground` (fondo 3D: cámara con encuadre desplazado `ShipScreenX`, `Spaceship`, rejilla, horizonte y sombra izquierda), `HorizonGrid` (mesh de la rejilla de horizonte y de la línea del horizonte).
- `Ship/`: `Player`, `ShipDynamics`, `ShipInput`, `ShipTuning`, `ShipBoost` (cargas, recarga, cooldown e impulso: subida + mantenimiento), `BoostTuning`, `Spaceship`, `Cockpit`, `ChaseCamera`.
- `UI/`: `Hud`, `BoostChargesView` (esferas de cargas de boost), `StartLightsView` (semáforo y cuenta atrás), `ResultsView` (pantalla de resultados).
- Tests en `Vectory.Test/` (`CircuitTest`, `CoreTests`, `MenuTests`, `ShipTests`): xunit, `net10.0`, referencia a `..\VECTORY.csproj`.
- Escenas: `Main.tscn` (carrera, en la raíz) y `scenes/MainMenu.tscn` (menú). Fuentes en `assets/fonts/` (Orbitron variable y Share Tech Mono, con su licencia OFL); de momento solo las usa el menú.
- Circuitos en `circuits/` (fuera de `scripts/`): `circuit_01.tres` es el actual (ancho 40 m, 5 checkpoints). Para uno nuevo: primer punto = salida/meta, en sentido horario visto desde arriba, con unos 70 m de recta detrás de la meta para la parrilla.

Espacios de nombres: `Vectory.Circuit`, `Vectory.Core`, `Vectory.Menu`, `Vectory.Ship`, `Vectory.UI`, `Vectory.Game`.

## Estilo visual

- Solo líneas y aristas luminosas, sin superficies sólidas ni texturas. Fondo oscuro, bloom moderado.
- **`Palette.cs` es la única fuente de colores.** No escribas colores a mano en otros scripts; usa `Palette.X` (por ejemplo `Palette.TrackEdge`). Cian `#36E7FF`, azul `#245BFF`, magenta `#FF43D0` (rivales), naranja `#FF9D32` (impulso, energía, alertas), fondo `#05070D`, azul grisáceo `#A6B8D8` (info secundaria del HUD).
- Respeta el estilo de indentación de cada archivo (no todos usan lo mismo).

## Comandos

- Compilar: `dotnet build` en la raíz del proyecto.
- Tests: `dotnet test` dentro de `Vectory.Test`.
- 179 tests (`ShipDynamics`, `ShipBoost`, `TrackLimits`, `RaceTracker`, `LineCrossingDetector`, `StartSequence`, `GridLayout`, `TimeFormat`, `VersionFormat`, `MenuModel`), todos pasando. Si alguno falla, averigua si el fallo es del test o de la lógica.
- Solo se testea la lógica pura. Lo que depende de nodos o recursos de Godot (`RaceSession`, `Track`, `TrackDefinition`, `Player`, `Main`, vistas del HUD, `MainMenu`, `MenuBackground`) lo valida el usuario jugando: xunit no puede crear objetos de Godot sin el motor.
- Con features nuevas, el usuario puede pedir implementar primero y decidir los tests después de validar.
- La salida de `dotnet` puede salir en español ("Compilación correcta", "Correctas!", "Con error"): tenlo en cuenta al filtrarla.
- Los informes de `dotnet test` (`TestResults/`) están en `.gitignore`: no se suben.
- Godot 4.7.2 mono: `F:\Programas\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`. Con `--path` y un script `extends SceneTree` (`--script`) se puede arrancar el juego, leer la consola y sacar capturas sin tocar el proyecto (también simular teclas con `Input.action_press`). Tras añadir recursos nuevos (fuentes, scripts), `--headless --path . --import` genera sus `.import` y `.uid`.

## Estado actual

- Hecho: nave wireframe, control arcade, cámara de cabina y de tercera persona, circuito cerrado generado por código a partir de un `TrackDefinition`, límites laterales, cabina minimalista, meta y checkpoints (cantidad en el `.tres` del circuito, 5 en `circuit_01`), vueltas, cronómetro y tiempos por sector.
- HUD hecho (posición, vuelta, tiempo, mejor vuelta, velocidad con su barra y cargas de boost), con textos en inglés (`POSITION`, `LAP`, `BEST`). Los mensajes de carrera en consola también van en inglés (`[Race] ...`). Los comentarios del código siguen en español.
- **Boost por cargas** (spec `docs/boost.md` v1.1): hecho y con commit. Espacio (acción de input `boost`, definida en `project.godot`) gasta una carga: 0 al empezar, 1 cada 35 s, máximo 3. Cada carga suma +100 km/h en 0,5 s (subida), los mantiene 2 s (mantenimiento) y después pierde el exceso sobre la punta a 20 km/h por segundo. Cooldown de 10 s. Frenar corta el impulso y la carga no vuelve. En el HUD, 3 esferas encima de la velocidad: naranja con halo (disponible), naranja atenuado (en cooldown), azul (vacía). Parámetros en el grupo **Boost** del Inspector del Player (velocidades en km/h).
- El boost provisional con Shift (`BoostMaxSpeed`, `BoostAcceleration`) y la barra naranja de "BOOST" del HUD se quitaron. La barra de velocidad llega a su tope en punta + impulso (`Player.BoostedTopSpeed`).
- **Ciclo de carrera** (spec `docs/carrera.md` v1.0): hecho, con tests de la lógica pura y validado por el usuario.
  - Estados `PreStart` → `Running` → `Finished` (`RaceState`, en `RaceTracker`).
  - Parrilla de 8 marcas detrás de la meta (corchetes azules en la malla del circuito, impares a la izquierda y pares a la derecha y más atrás). El jugador sale de una posición aleatoria (`GridLayout.RandomOrder` con `Random` inyectado). Parámetros en el grupo **Parrilla** del Inspector del Track.
  - Semáforo de 5 luces magenta y cuenta atrás 5 → 1; a los 6 s, GO (visible 1 s). En la previa no hay input ni recarga del boost; el cronómetro y la recarga arrancan en el GO. Parámetros en el grupo **Salida** del Inspector de Main.
  - El primer cruce de meta tras el GO no cierra vuelta; la vuelta 1 cuenta desde el GO.
  - Al completar la última vuelta: cronómetro parado, sin control, boost cancelado, la nave se para sola. A los 1,5 s (grupo **Resultados** de Main), pantalla de resultados: posición (`1st / 8`), mejor vuelta con su número y mejor tiempo de cada sector con su vuelta. Enter (acción `restart_race`) recarga la escena.
  - Textos de resultados en inglés (`RACE FINISHED`, `BEST LAP`, `BEST SECTORS`, `ENTER · Restart race`), como el resto del HUD, aunque la spec los da en español. El HUD muestra `POSITION 1/8`.
  - Las marcas miden 4 m de ancho (valor de la spec) y la nave unos 6,8 m: si no encajan, se ajustan con `GridMarkWidth` / `GridMarkLegLength`.
- **Menú principal** (spec `docs/menu-principal.md` v1.2, rama `feature/menus`): hecho, con tests de la lógica pura y validado por el usuario.
  - El juego arranca en el menú. Opciones RACE, CHAMPIONSHIP, TIME TRIAL, HANGAR, RECORDS, SETTINGS y QUIT: solo RACE (carga la carrera) y QUIT (cierra el juego) están activas; el resto, atenuadas con `COMING SOON`. Las opciones son una lista de `MenuOption` en `MainMenu`: activar una es cambiar `Enabled` y añadir su caso en `MainMenu.Execute`.
  - Teclado: W/S o flechas (`menu_up` / `menu_down`), Enter o Espacio (`menu_accept`); Esc no hace nada. Ratón: hover selecciona y clic elige, solo en opciones activas.
  - Medidas de la spec sobre 1280×720, multiplicadas por la altura base del proyecto / 720 (1080 → ×1,5). Parámetros en el Inspector de `MainMenu` (tamaños) y `MenuBackground` (cámara, rejilla, sombra).
  - Fondo: la cámara mira de frente a la nave y el encuadre se desplaza con proyección frustum (`ShipScreenX` = 0,734), así la nave queda a la derecha sin girarse y el horizonte sale recto. El suelo está a la altura de la punta del morro (`FloorY` = 0, `HorizonZ` = -4).
  - Versión leída de `application/config/version` (0.1.0) y mostrada como `v0.1.0`.
  - Los halos del texto son sombras sin desplazamiento (`LabelSettings.Shadow*`): más suaves que el brillo del lienzo de diseño.
  - Godot puede reescribir `run/main_scene` de `project.godot` como `uid://` al abrir el editor: es normal.
- `MaxSpeed` del Player está a 80 m/s en `Main.tscn` (el valor por defecto del código es 60).
- `Main.tscn` aún tiene `TrackWidth = 40.0` en el nodo Track: ya no hace nada (el ancho sale del `.tres`) y se puede quitar.
- Decisiones del usuario que se salen del documento maestro y se mantienen: circuito cerrado, dos cámaras, vueltas y cronómetro dentro de la demo, boost por cargas que se recargan solo por tiempo (sin recarga por conducción limpia ni derrapes), 8 participantes por carrera, sin ventaja por buena reacción en la salida.
- Pendiente: pausa y volver al menú desde la carrera o los resultados (siguiente spec de menús), el resto de opciones del menú, rivales e IA (la parrilla y `RandomOrder` ya reparten las 8 posiciones; `FinalPosition` es siempre 1 hasta que haya rivales), posición en vivo en el HUD (ahora fija en 1/8), ajustar el contorno interior de la cabina, récords entre partidas.

## Importante

- Cada vez que cerremos una etapa, justo antes de que llegue el momento de hacer el commit, actualicemos este documento con la información relevante. Solicitamelo cada vez que vayamos a hacerlo.
