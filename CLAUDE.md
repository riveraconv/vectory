# VECTORY

Juego de carreras futuristas con estética vectorial/wireframe de los años 80. Godot .NET (C#), renderizador Forward+, para Windows/PC. Vista principal desde cabina, con cámara alternativa en tercera persona (tecla C).

## Dónde está cada cosa

Este archivo es solo lo que vale para cualquier tarea. Lo demás está aparte y **se lee cuando hace falta**, no siempre:

- **Specs**: en `docs/` (por ejemplo `docs/boost.md`). Donde lo indiquen, mandan sobre el documento maestro.
- **Historial**: `docs/historial.md`. Lo hecho etapa por etapa, con las **decisiones que se apartan de las specs**, lo medido y las trampas ya resueltas, un apartado por spec. **Antes de tocar un sistema, lee su spec y su apartado del historial** (busca el nombre de la spec). Si el historial contradice una spec, manda el historial: son decisiones validadas por el usuario.
- **Arquitectura en detalle**: `docs/arquitectura.md`. Qué hace cada clase de cada carpeta, escenas, circuitos, qué cubren los tests y cómo probar sin el usuario (sondas, capturas, comparaciones al píxel). Léelo cuando necesites saber qué existe y dónde, o antes de una prueba automática.
- **Diseño completo**: `docs/VECTORY_Documento_Maestro.txt`. Para decidir algo de diseño o de alcance.
- **Maquetas y prototipos**: `designs/` (no `design/`, aunque alguna spec lo diga).

## Cómo trabajamos

- El usuario no escribe código a mano: tú escribes el código. Entrega **archivos completos y listos para usar**, nunca fragmentos sueltos.
- No inventes código ni APIs. Si no estás seguro de algo de Godot o de C#, dilo y compruébalo.
- Entregas pequeñas y fáciles de validar. Primero base funcional, luego mejorar, luego ampliar. Las specs van por etapas: no se empieza una sin validar la anterior.
- Decide tú lo técnico secundario. Pregunta solo ante un bloqueo real o una decisión de producto que no se pueda deducir. Si algo de una spec es dudoso, pregunta antes de seguir.
- No amplíes el alcance por iniciativa propia ni añadas funciones "por dejarlas preparadas".
- No declares algo terminado solo porque compile. Di qué has probado, qué queda pendiente y qué limitaciones conoces.
- No refactorices ni reformatees archivos ajenos a la tarea. Conserva lo que ya funciona. Los commits de refactor sin cambios para el jugador llevan el prefijo `refactor:`.
- **El usuario prueba el juego a mano** (conducción, cámara, HUD). Los tests automáticos no sustituyen eso.
- **Los commits los hace el usuario, siempre.** Cuando una etapa está validada, avisa de que toca commit, di qué entra y sugiere el mensaje (en inglés, descriptivo, un commit por etapa). No hagas `git commit` tú.
- El usuario habla en español, de forma informal y directa. Responde en español. Textos del juego y mensajes de consola (`[Race] ...`) en inglés; comentarios del código en español.

## Arquitectura

- El juego arranca en el menú principal (`scenes/MainMenu.tscn`). Los selectores (circuito, nave, pintura) y SETTINGS van dentro de la escena del menú; la carrera es `Main.tscn` (en la raíz), montada por código desde `Game/Main.cs`. Modo, circuito, nave y dificultad pasan por `Game/GameSession`. Todo cambio de escena pasa por `Game/SceneChanger`.
- Cada circuito es un `TrackDefinition` en `circuits/`. Se añaden en `CircuitCatalog`.
- Los nodos de Godot (`Player`, `Track`, `Hud`, `CollisionEffects`...) solo **conectan piezas** y dibujan. Las reglas viven en clases puras sin dependencia de Godot (`System.Numerics`), para probarlas con xunit.
- SOLID sin sobreingeniería. Pruebas unitarias cuando haya lógica que lo justifique.
- Los parámetros de ajuste van como `[Export]` en el Inspector, agrupados, y sus valores definitivos pasan a ser los valores por defecto del código. Excepción: lo propio de cada nave (`ShipTuning`, encuadre de las cámaras, semiancho de colisión, perfil visual) sale de su definición (`ShipDefinition`), no del Inspector.
- Física sin el motor de Godot: la nave, las barreras y los contactos entre naves son lógica propia respecto al eje de la pista (`ShipPhysics`, `BarrierCollision`, `ShipContactSolver`).

Carpetas de `scripts/` (detalle en `docs/arquitectura.md`): `Circuit/` (pista, barreras, carrera, clasificación), `Autopilot/` (piloto y trazada, base de los rivales), `Balance/` (simulador de equilibrio, puro), `tools/` (`BalanceRunner`), `Rivals/` (pilotos, perfiles, dificultad, rivales en carrera), `Contact/` (choques entre naves, rebufo), `Radar/` (radar y minimapa), `Effects/` (efectos de choque, derrape, aterrizaje, humo y fuego de daños), `Settings/`, `Core/` (`Palette`, `TimeFormat`, `VersionFormat`), `Game/`, `Ghost/`, `Records/`, `Menu/`, `Ship/` (naves, física, cascos y cabinas 3D, daño), `UI/`. Tests en `Vectory.Test/`.

## Estilo visual

- Circuitos, entorno, efectos, menús y HUD de las esquinas: solo líneas y aristas luminosas, fondo oscuro, bloom moderado. Naves y cabinas: 3D realista con PBR, cada nave con su estilo (VECTOR carreras, BLADE militar, HAMMER industrial) y su paleta propia.
- **`Palette.cs` es la única fuente de colores.** No escribas colores a mano en otros scripts. Verde, rojo y amarillo solo para el delta de TIME TRIAL y los daños; blanco solo para mezclar en destellos y la marca de parrilla del jugador.
- Respeta el estilo de indentación de cada archivo.

## Comandos

- Compilar: `dotnet build` en la raíz. Tests: `dotnet test` dentro de `Vectory.Test` (todos tienen que pasar; si alguno falla, averigua si el fallo es del test o de la lógica). La salida de `dotnet` puede salir en español.
- Solo se testea la lógica pura. Lo que depende de nodos o recursos de Godot lo valida el usuario jugando.
- Godot 4.7.2 mono: `F:\Programas\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`.
- Blender 5.2.3 LTS: `F:\Programas\Blender\blender.exe`. Los scripts de `art/blender/` son la fuente de cabinas y cascos 3D; el `.blend` se regenera.
- Cómo arrancar el juego sin ventana, sondas, capturas y comparaciones al píxel: `docs/arquitectura.md`, «Herramientas y pruebas automáticas».

## Reglas que no se pueden romper

- **Archivos del usuario**: antes de cualquier prueba automática que pueda completar una vuelta, una carrera, cambiar GHOST o salir de SETTINGS, aparta `records.json`, la carpeta `ghosts` y `settings.json` (en `%APPDATA%\Godot\app_userdata\VECTORY\`) y devuélvelos después. El usuario tiene récords reales.
- **Rendimiento**: que no haya tirones es muy importante para el usuario. Mide el peor frame y los frames lentos, no los FPS medios. Nada de capturas ni `GD.Print` dentro de lo que se mide.
- **Sin basura por frame**: en lo que corre cada frame o cada paso, nada de textos en llamadas a Godot que piden `StringName` (van como `static readonly StringName`), nada de `foreach` sobre interfaces, LINQ ni lambdas, y nada de arrays de Godot nuevos (búferes fijos, `MultiMesh` con `RenderingServer.MultimeshSetBuffer`).
- **Nada de cambiar de shader en plena carrera** (compila pipelines y da un pico): deja el efecto siempre puesto y cambia solo su valor o su textura.
- **La firma de `circuit_01` no se puede cambiar** (se perderían los fantasmas): su trazado, su ancho y sus checkpoints son fijos.
- **Equilibrio**: VECTOR gana S4 por 0,033 s; un cambio pequeño en cualquier nave se lo puede quitar. Lo vigila el test `The_definitions_pass_the_mandatory_rules_in_circuit_01`.
- **Cambio sin efecto visible**: si un cambio no debe tocar lo que se ve, compruébalo al píxel contra el commit anterior (cómo, en `docs/arquitectura.md`).
- **Teclas de depuración** (solo con `OS.IsDebugBuild()`): F4 cámara orbital, F5 / Mayúsculas + F5 pintura, F6 naves de prueba, F7 piloto, F9 panel de ajuste, F10 cambiar de nave, F11 volcar valores, F12 restablecer. F8 no se usa (Godot detiene el juego con ella).

## Estado

- Versión **0.29.0**. Última spec cerrada: `docs/efectos-9-vision-tunel.md` (visión túnel solo sobre el mundo, a la mitad en persecución; cono de vapor cerca de la punta, en vez de líneas de velocidad). El detalle de todo lo hecho, en `docs/historial.md`.
- Capas de pantalla en carrera: visión túnel 1, cabina 3D 2, HUD 3, pausa 10, depuración 11.
- `docs/escenografia.md` y el resto de `docs/efectos-velocidad.md` (FOV +14°, partículas de las toberas) siguen **sin implementar**. No se tocan hasta que el usuario lo diga.
- Pendiente: un suelo de pista y de túnel que reciba luz (la luz en pista y los focos de `efectos-8-luces` esperan en la capa 2, y su sombra de contacto, aplazada); CHAMPIONSHIP y HANGAR del menú, `docs/naves-disenos.md` sin las cabinas elegidas, ajustar el contorno interior de la cabina, reajustar la dificultad de los rivales cuando haya circuitos nuevos.

## Al cerrar una etapa

Justo antes de que toque el commit, pídeme permiso para apuntarla y:

1. Añade su apartado al final de `docs/historial.md`, **corto**: qué se hizo, en qué se aparta de la spec (decisiones del usuario), lo medido y las trampas nuevas. No repitas lo que ya dice la spec.
2. Si entra una clase o una carpeta nueva, una línea en `docs/arquitectura.md`.
3. En este archivo, solo si cambia algo general: la versión y la última spec en **Estado**, una regla nueva en **Reglas que no se pueden romper** o un comando. Este archivo tiene que seguir siendo corto.
