# VECTORY

Juego de carreras futuristas con estética vectorial/wireframe de los años 80. Godot .NET (C#), renderizador Forward+, para Windows/PC. Vista principal desde cabina, con cámara alternativa en tercera persona (tecla C).

El diseño completo está en `scripts/VECTORY_Documento_Maestro.txt`. Léelo cuando necesites decidir algo de diseño o de alcance; este archivo es solo el resumen para trabajar.

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

- La escena se monta por código desde `Game/Main.cs`. Si `Track` y `Player` ya existen en la escena, se usan tal cual (así se afinan sus parámetros en el Inspector); si no, se crean.
- Los nodos de Godot (`Player`, `Track`, `Hud`...) solo **conectan piezas**. Las reglas viven en clases puras sin dependencia de Godot (usan `System.Numerics`), para poder probarlas con xunit.
- SOLID sin sobreingeniería. Pruebas unitarias cuando haya lógica que lo justifique.
- Los parámetros de ajuste van como `[Export]` en el Inspector y luego se pasan al código por defecto.

## Estructura (`scripts/`)

- `Circuit/`: `Track` (genera la geometría del circuito y la línea central), `TrackLimits` (límites laterales), `RaceTracker` (vueltas, sectores, cronómetro, mejores tiempos), `RaceSession` (conecta `Track` con `RaceTracker`), `LineCrossingDetector` (cruces de meta y checkpoints).
- `Core/`: `Palette` (colores).
- `Game/`: `Main`.
- `Ship/`: `Player`, `ShipDynamics`, `ShipInput`, `ShipTuning`, `Spaceship`, `Cockpit`, `ChaseCamera`.
- `UI/`: `Hud`.
- Tests en `Vectory.Test/` (`CircuitTest`, `CoreTests`, `ShipTests`): xunit, `net10.0`, referencia a `..\VECTORY.csproj`.

Espacios de nombres: `Vectory.Circuit`, `Vectory.Core`, `Vectory.Ship`, `Vectory.UI`, `Vectory.Game`.

## Estilo visual

- Solo líneas y aristas luminosas, sin superficies sólidas ni texturas. Fondo oscuro, bloom moderado.
- **`Palette.cs` es la única fuente de colores.** No escribas colores a mano en otros scripts; usa `Palette.X` (por ejemplo `Palette.TrackEdge`). Cian `#36E7FF`, azul `#245BFF`, magenta `#FF43D0` (rivales), naranja `#FF9D32` (impulso, energía, alertas), fondo `#05070D`, azul grisáceo `#A6B8D8` (info secundaria del HUD).
- Respeta el estilo de indentación de cada archivo (no todos usan lo mismo).

## Comandos

- Compilar: `dotnet build` en la raíz del proyecto.
- Tests: `dotnet test` dentro de `Vectory.Test`.
- 75 tests (`ShipDynamics`, `TrackLimits`, `RaceTracker`, `LineCrossingDetector`), todos pasando. Si alguno falla, averigua si el fallo es del test o de la lógica.
- Los informes de `dotnet test` (`TestResults/`) están en `.gitignore`: no se suben.
- Godot 4.7.2 mono: `F:\Programas\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`. Con `--path` y un script `extends SceneTree` (`--script`) se puede arrancar el juego, leer la consola y sacar capturas sin tocar el proyecto.

## Estado actual

- Hecho: nave wireframe, control arcade con boost (Shift), cámara de cabina y de tercera persona, circuito cerrado generado por código, límites laterales, cabina minimalista, meta y checkpoints (cantidad configurable con `CheckpointCount`, 5 por defecto), vueltas, cronómetro y tiempos por sector.
- HUD hecho (velocidad, boost, vuelta, tiempo, mejor vuelta, posición), con textos en inglés (`POSITION`, `LAP`, `BEST`). Los mensajes de carrera en consola también van en inglés (`[Race] ...`). Los comentarios del código siguen en español.
- `MaxSpeed` del Player está a 80 m/s en `Main.tscn` (el valor por defecto del código es 60).
- Decisiones del usuario que se salen del documento maestro y se mantienen: circuito cerrado, dos cámaras, boost con Shift, vueltas y cronómetro dentro de la demo.
- Pendiente: cuenta atrás y salida desde parrilla (hoy el cronómetro arranca al moverse), rivales e IA, posición real en el HUD (ahora fija en 1/1), ajustar el contorno interior de la cabina, récords entre partidas.
- Pendientes menores del HUD: la etiqueta "BOOST" queda entre las dos barras y no se ve claro a cuál pertenece; `FormatTime` está duplicado en `Hud` y `RaceSession`.

## Importante

- Cada vez que cerremos una etapa, junto antes de que llegue el momento de hacer el commit, actualicemos este documento con la información relevante. Solicitamelo cada vez que vayamos a hacerlo.
