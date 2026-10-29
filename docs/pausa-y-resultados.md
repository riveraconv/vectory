# VECTORY — Spec: Pausa y salida de la carrera

Versión 1.1 · 2026-10-29 · Estado: **definido, listo para implementar**

Segunda spec de menús. Añade la **pausa con ESC** durante la carrera (CONTINUE / RESTART / BACK TO MAIN MENU, con confirmación) y la opción **BACK TO MAIN MENU** en la pantalla de resultados. Con esto se cierra el ciclo menú → carrera → menú.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, artboards *Pausa (ESC)*, *Pausa · confirmación* y *Resultados A · una franja, tres columnas* (el artboard *Resultados (actual)* queda solo como referencia de lo anterior). Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: `MenuModel` / `MenuOption` (navegación), `MainMenu` (aspecto de las opciones), `SceneChanger` (cambio de escena), `ResultsView` y `Main.cs` (ciclo de carrera).

**Cambios en v1.1** (sobre lo ya implementado de la v1.0): solo cambia el aspecto de la **pantalla de resultados** (sección 4, más los puntos que la mencionan en 7, 8 y 9). La pausa, la confirmación, la navegación y el cambio de escena quedan igual.

Rama: `feature/pause` (sale de `develop`, con `feature/menus` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- **ESC** abre la pausa **en cualquier momento de la carrera**: previa (cuenta atrás), en carrera y con los resultados en pantalla.
- En pausa **se congela todo**: nave, cronómetro, semáforo / cuenta atrás, recarga del boost y el retardo de la pantalla de resultados.
- Pausa: **CONTINUE, RESTART, BACK TO MAIN MENU**.
- RESTART y BACK TO MAIN MENU piden **confirmación** (NO / YES, con NO por defecto), salvo si la carrera ya ha terminado.
- Pantalla de resultados: el panel cuadrado pasa a ser una **franja horizontal** como la de la pausa, en tres columnas (resultado, tiempos, opciones), con la lista **RESTART RACE / BACK TO MAIN MENU** en lugar del pie `ENTER · Restart race`.
- Todos los textos en inglés. Mismo estilo de opciones que el menú principal (barra lateral).

---

## 2. Flujo

| Desde | Acción | Resultado |
|---|---|---|
| Carrera (previa, en carrera o resultados) | ESC | Se abre la pausa con **CONTINUE** seleccionada; todo se congela |
| Pausa | CONTINUE, o ESC | Se cierra la pausa y todo sigue donde estaba |
| Pausa (carrera sin terminar) | RESTART | Confirmación `RESTART RACE?` |
| Pausa (carrera sin terminar) | BACK TO MAIN MENU | Confirmación `BACK TO MAIN MENU?` |
| Pausa (carrera terminada) | RESTART | Repite la carrera directamente, sin confirmación |
| Pausa (carrera terminada) | BACK TO MAIN MENU | Vuelve al menú directamente, sin confirmación |
| Confirmación | NO, o ESC | Vuelve a la pausa, con la opción que se había elegido (RESTART o BACK) seleccionada |
| Confirmación de RESTART | YES | Repite la carrera (recarga la escena: nueva posición de parrilla aleatoria) |
| Confirmación de BACK | YES | Carga el menú principal |
| Resultados | RESTART RACE | Repite la carrera |
| Resultados | BACK TO MAIN MENU | Carga el menú principal |

- "Carrera terminada" = estado `Finished` de la carrera (tanto antes como después de que aparezca el panel de resultados).
- Al volver al menú principal, este aparece en su estado inicial (RACE seleccionada).
- ESC en el **menú principal** sigue sin hacer nada (sin cambios).

## 3. Pausa

### 3.1 Qué se congela

- Se usa la pausa del árbol de Godot (`GetTree().Paused`). Todo lo de la carrera (nave, carrera, HUD, cámara, resultados) se para; solo el menú de pausa sigue procesando.
- Mientras está en pausa, **W, S, A, D, Shift y Espacio no afectan a la nave**. Al continuar, la nave **no** debe recibir pulsaciones hechas durante la pausa (p. ej. la W usada para navegar no se convierte en aceleración ni en un boost).
- Al continuar, todo sigue **exactamente** donde estaba: velocidad, posición, cronómetro, luces del semáforo, recarga del boost, tiempo que faltaba para mostrar los resultados.
- La pausa **no** cambia el tiempo de la carrera: el tiempo en pausa no cuenta en vueltas ni sectores.
- Cambiar de cámara (C) no funciona en pausa.

### 3.2 Aspecto (franja horizontal)

Medidas sobre la pantalla de referencia de 1280×720, escaladas igual que en `MainMenu`.

- **Atenuado de la carrera**: toda la pantalla, #05070D al ~45 % por encima de la carrera y el HUD.
- **Franja**: de lado a lado, 260 px de alto, centrada en vertical (y = 230). Fondo #05070D al ~90 %. Línea fina cian (1 px, `Palette.Cyan`) arriba y abajo, con halo suave.
- **Bloque izquierdo** (x = 96, centrado en vertical dentro de la franja):
  - `PAUSED`: Orbitron Black ~56 px, cian, espaciado ~0.08 em, con halo.
  - Debajo: `ESC  ·  CONTINUE` en Share Tech Mono ~16 px, azul grisáceo #A6B8D8, espaciado ~0.18 em.
- **Separador**: línea vertical de 1 px, azul #245BFF al ~70 %, en x = 500, de 156 px de alto, centrada en la franja.
- **Opciones** (x = 560, centradas en vertical dentro de la franja): CONTINUE, RESTART, BACK TO MAIN MENU, con **el mismo aspecto que el menú principal** (filas de 48 px, 6 px de separación, Orbitron 28 px, barra lateral cian 4×36 px, texto seleccionado desplazado 20 px, sin seleccionar en azul grisáceo).

### 3.3 Confirmación

Se muestra en la **misma franja**, sustituyendo su contenido (sin ventana aparte):

- Bloque izquierdo:
  - Título en **naranja** #FF9D32 (alerta), Orbitron Black ~40 px, con halo naranja: `RESTART RACE?` o `BACK TO MAIN MENU?`.
  - Debajo: `CURRENT RACE PROGRESS WILL BE LOST` en Share Tech Mono ~16 px, azul grisáceo.
- Opciones: **NO, YES** (en ese orden), mismo aspecto que las de la pausa, centradas en vertical en la franja.
- Al abrirse, **NO** está seleccionado.
- El título de `BACK TO MAIN MENU?` cabe en el bloque izquierdo sin pisar el separador; si con la fuente real no cabe, se reduce el tamaño del título (parámetro en el Inspector).

## 4. Pantalla de resultados

El panel cuadrado con borde cian se **sustituye** por una **franja horizontal** con el mismo lenguaje que la pausa (variante **A** del lienzo: *Resultados A · una franja, tres columnas*). Mismos datos que ahora; cambia solo cómo se presentan.

### 4.1 Fondo y franja

- **Atenuado de la carrera**: toda la pantalla, #05070D al ~45 % (igual que la pausa).
- **Franja**: de lado a lado, **340 px** de alto, centrada en vertical (y = 190 sobre 720). Fondo #05070D al ~90 %, línea fina cian (1 px) arriba y abajo con halo suave. Mismo componente/estilo que la franja de la pausa, con otro alto.
- Dentro, **tres columnas** separadas por líneas verticales de 1 px, azul #245BFF al ~70 %, de 244 px de alto y centradas en la franja (en x = 470 y x = 860).
- Cada columna se centra en vertical dentro de la franja.

### 4.2 Columna izquierda: resultado (x = 96)

- `RACE FINISHED`: Orbitron Black ~34 px, cian, espaciado ~0.08 em, con halo.
- Debajo (18 px), la posición: `1st` en Orbitron Bold ~80 px, cian, con halo; seguido de ` / 8` en Orbitron Medium ~36 px (≈ 45 % del tamaño de la posición), azul grisáceo.
- La posición y el total salen del `RaceResult`, como ahora (ordinal en inglés: 1st, 2nd, 3rd, 4th…).

### 4.3 Columna central: tiempos (x = 520)

- `BEST LAP`: etiqueta en Share Tech Mono ~14 px, azul grisáceo, espaciado ~0.22 em.
- Debajo: el tiempo de la mejor vuelta en Orbitron Medium ~30 px, cian, y a su derecha (14 px) `lap N` en Share Tech Mono ~16 px, azul grisáceo.
- Hueco de ~14 px.
- `BEST SECTORS`: misma etiqueta que BEST LAP.
- Debajo, **una fila por sector**, con 3 columnas alineadas entre filas:
  - `S1`, `S2`…: Share Tech Mono ~15 px, azul grisáceo (columna de ~44 px).
  - Tiempo: Orbitron Medium ~20 px, cian (columna de ~130 px).
  - `lap N`: Share Tech Mono ~15 px, azul grisáceo.
  - 4 px entre filas.
- Formato de tiempo: el de `TimeFormat`, como ahora.
- El número de sectores depende del circuito. Con más sectores la columna crece y se mantiene centrada en la franja; si no cupiera en los 340 px, la franja crece en alto (centrada) en vez de recortar.

### 4.4 Columna derecha: opciones (x = 900)

- **RESTART RACE / BACK TO MAIN MENU**, centradas en vertical en la franja.
- Mismo estilo que las opciones del menú, algo más pequeñas: Orbitron ~24 px, filas de 46 px, 4 px de separación, barra lateral 4×32 px, texto seleccionado desplazado 16 px; seleccionada en cian con halo, sin seleccionar en azul grisáceo.
- Al aparecer la franja, **RESTART RACE** está seleccionada.
- `BACK TO MAIN MENU` debe caber sin salirse de la pantalla por la derecha; si con la fuente real no cabe, se reduce el tamaño de las opciones (parámetro en el Inspector).

### 4.5 Otros

- Se **quita** el pie `ENTER · Restart race`.
- Aparece igual que ahora: **1,5 s** después de cruzar la meta, sin animación.
- La acción `restart_race` deja de usarse en la pantalla de resultados (se elige con `menu_accept`, como el resto de menús). Si nada más la usa, se elimina del Input Map.
- Solo cambia la pantalla de resultados; el HUD **no** cambia de fuente.

## 5. Navegación (pausa, confirmación y resultados)

Igual que en el menú principal:

- **W / flecha arriba** y **S / flecha abajo**: mueven la selección, dando la vuelta.
- **Enter / Espacio**: eligen (`menu_accept`).
- **Ratón**: pasar por encima selecciona, clic izquierdo elige.
- Teclado y ratón comparten una sola selección.
- **ESC**: acción nueva `pause` (tecla Escape).
  - En carrera / previa / resultados: abre la pausa.
  - En la pausa: equivale a CONTINUE.
  - En la confirmación: equivale a NO.
- Con la pausa abierta, la lista de resultados (si está visible) **no** responde ni a teclado ni a ratón.
- Al abrir la pausa sobre los resultados y continuar, los resultados conservan la opción que tenían seleccionada.
- Una misma pulsación no puede hacer dos cosas: el ESC que abre la pausa no la cierra en el mismo frame, y el Enter que confirma en la pausa no llega a la lista de resultados.

## 6. Cambio de escena

- Todo cambio de escena sigue pasando por `SceneChanger`.
- Antes de recargar la carrera o ir al menú, **se quita la pausa** (`GetTree().Paused = false`); si no, la escena nueva arrancaría congelada. Esto se hace dentro de `SceneChanger` (en `ChangeTo` y `Reload`), para que nadie tenga que acordarse.
- La ruta del menú principal sale de `SceneChanger.MainMenuScene` (o de un parámetro en el Inspector que use ese valor por defecto, como hace `MainMenu` con la carrera).

## 7. Parámetros en el Inspector

| Parámetro | Valor por defecto |
|---|---|
| Ruta de la escena del menú principal | `SceneChanger.MainMenuScene` |
| Opacidad del atenuado de pantalla en pausa | 0,45 |
| Opacidad del fondo de la franja | 0,9 |
| Alto de la franja | 260 px |
| Tamaño de PAUSED | 56 px |
| Tamaño del título de confirmación | 40 px |
| Tamaño del texto secundario de la franja | 16 px |
| Tamaño de las opciones de la pausa | 28 px |
| Alto de la franja de resultados | 340 px |
| Tamaño de las opciones de resultados | 24 px |

Las medidas de filas, barra y desplazamiento de la pausa son las del menú principal.

## 8. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual (`scripts/Menu`, `scripts/UI`, `scripts/Game`, `scripts/Core`).

- **Lista de opciones reutilizable**: sacar de `MainMenu` la parte que dibuja una lista vertical de opciones a partir de un `MenuModel` (filas, hueco y barra de selección, desplazamiento, estilos, hover y clic) a un componente propio, configurable en tamaños (fuente, alto de fila, barra, desplazamiento). La usan `MainMenu`, la pausa (opciones y confirmación) y los resultados. `MainMenu` debe quedar **exactamente igual** que ahora a la vista y en comportamiento.
- **Fuentes del menú**: la carga de Orbitron / Share Tech Mono y la creación de fuentes con peso y espaciado (`LoadFont`, `MakeFont`, `MakeSettings`) se mueven a un sitio compartido para no duplicarlas.
- **Lógica de la pausa** (clase propia, sin Godot):
  - Estados: `Closed`, `Open`, `Confirm` (con qué se está confirmando: Restart o BackToMenu).
  - `TogglePause()` (ESC), `Choose(opción)`, y la respuesta a la confirmación.
  - Sabe si la carrera ha terminado (se le pasa al abrir o al elegir) para saltarse la confirmación.
  - Devuelve qué hay que hacer: nada, cerrar la pausa, reiniciar o ir al menú. **No** cambia de escena ella misma.
  - Usa `MenuModel` para la navegación de cada lista; no duplica la lógica de selección.
- **Nodo de la pausa** (`PauseMenu` o similar, en `scripts/Menu` o `scripts/UI`): `ProcessMode = Always`, dibuja la franja, traduce input a la lógica, pone y quita `GetTree().Paused` y llama a `SceneChanger` según el resultado.
- **Franja**: el dibujo de la franja (atenuado, fondo, líneas cian, separadores verticales) en un componente propio que usan la pausa y los resultados, con el alto como parámetro.
- **Resultados**: `ResultsView` se rehace con la franja y las tres columnas; sigue solo mostrando el `RaceResult`. La lista de opciones va en el componente reutilizable. La acción elegida (reiniciar / menú) la ejecuta quien ya gestiona el ciclo de carrera (`Main.cs`), no `ResultsView`.
- `Main.cs` añade el nodo de la pausa y conecta las acciones; no contiene lógica de la pausa.

### Pruebas unitarias mínimas

Lógica de la pausa:
1. Empieza `Closed`. `TogglePause()` → `Open` con CONTINUE seleccionada.
2. En `Open`, `TogglePause()` → `Closed` (ESC = continuar).
3. En `Open`, elegir CONTINUE → `Closed`, acción "cerrar".
4. Carrera sin terminar: elegir RESTART → `Confirm(Restart)` con NO seleccionado; elegir BACK TO MAIN MENU → `Confirm(BackToMenu)` con NO seleccionado.
5. En `Confirm`, NO → `Open` con la opción de origen seleccionada (RESTART o BACK).
6. En `Confirm`, `TogglePause()` (ESC) → igual que NO.
7. `Confirm(Restart)` + YES → acción "reiniciar". `Confirm(BackToMenu)` + YES → acción "ir al menú".
8. Carrera terminada: RESTART → acción "reiniciar" directa; BACK TO MAIN MENU → acción "ir al menú" directa, sin pasar por `Confirm`.
9. Reabrir la pausa tras cerrarla vuelve a seleccionar CONTINUE.

Componente compartido / modelo:
10. Las pruebas actuales de `MenuModel` y `VersionFormat` siguen pasando sin cambios.

## 9. Prueba manual

1. Desde el menú, RACE. Durante la cuenta atrás, ESC: aparece la franja con PAUSED y el semáforo se queda congelado. CONTINUE: la cuenta atrás sigue por donde iba.
2. En carrera, a buena velocidad, ESC: todo quieto (nave, cronómetro). Esperar 10 s y continuar: la nave sigue a la misma velocidad y el cronómetro no ha sumado esos 10 s.
3. Pausar con W pulsada, navegar con W/S y continuar soltando todo: la nave no acelera sola ni lanza boost.
4. Con una carga de boost recargándose, pausar 10 s: al continuar, la recarga no ha avanzado.
5. ESC dentro de la pausa: se cierra. ESC otra vez: se abre.
6. RESTART: confirmación naranja `RESTART RACE?` con NO seleccionado. ESC: vuelve a la pausa con RESTART seleccionada. RESTART → YES: nueva carrera, nueva posición de parrilla, sin quedarse congelada.
7. BACK TO MAIN MENU → NO: vuelve a la pausa. BACK TO MAIN MENU → YES: menú principal con RACE seleccionada; RACE de nuevo funciona.
8. Probar todo lo anterior también con el ratón (hover + clic).
9. Terminar la carrera. Antes de que salgan los resultados, pausar 5 s: al continuar, los resultados tardan lo que les faltaba, no aparecen de golpe.
10. Con los resultados: aparece la franja con tres columnas (RACE FINISHED y posición; mejor vuelta y sectores; opciones), sin el panel cuadrado ni el pie ENTER. Los tiempos cuadran con los del HUD. RESTART RACE seleccionada; navegar con W/S y ratón y probar las dos opciones.
11. Con los resultados en pantalla, ESC: pausa encima. RESTART y BACK TO MAIN MENU van **sin** confirmación. CONTINUE: vuelven los resultados con la misma opción seleccionada.
12. Comprobar que la franja y los resultados se ven bien en ventana y a pantalla completa, y que el menú principal sigue igual que antes.

## 10. Fuera de alcance

- Opciones dentro de la pausa (ajustes, controles, volumen).
- Pausa automática al perder el foco de la ventana.
- Animaciones de apertura/cierre de la pausa o de la franja.
- Sonido.
- Mando.
- Cambiar la fuente del HUD.
- Guardar récords o resultados al salir.
