# VECTORY — Spec: Selector de naves

Versión 1.1 · 2026-11-04 · Estado: **definido, listo para implementar**

Cambios en v1.1: en SELECT CIRCUIT la opción **START** pasa a llamarse **SHIP SELECTION**, porque ya no empieza la carrera sino que abre este selector (sección 6). En SELECT SHIP la opción sigue siendo **START**.

Añade una pantalla **SELECT SHIP** entre **SELECT CIRCUIT** y la carrera, en **RACE** y en **TIME TRIAL**. Muestra la nave como un **holograma 3D** que gira solo y se puede girar con el ratón, sus **datos** (barras y números) y los **sistemas comunes** a todas las naves.

Hoy solo hay una nave elegible, **VECTOR** (la nave actual, `ship_01`). **INTERCEPTOR** y **VANGUARD** aparecen en la lista como **COMING SOON**: se ven, pero no se pueden elegir y no tienen geometría ni datos. Su perfil técnico, su geometría y el equilibrio entre naves van en una spec aparte (**naves**), después de esta.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Selector de naves**: *Selector de naves · briefing · barras y números*. Es interactivo: el holograma de la maqueta usa la geometría real de `Spaceship`. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: `CircuitSelectScreen` / `CircuitSelectLogic` / `CircuitSelectFormat`, `MainMenu`, `MenuModel` / `MenuOption`, `OptionList`, `GameSession` (`Mode`, `CircuitPath`, `ShipDesignId`), `ShipCatalog`, `Spaceship` (y su `SetMaterialOverride`), `Player` y sus parámetros, `BoostTuning`, `CircuitMiniature` (como modelo de objeto 3D con cámara propia en el mundo del menú), `WorldStyle`, `Palette`, `MenuFonts`, `UiScale` y `SceneChanger`.

Rama: `feature/ship-selector`, que sale de `develop` con `feature/circuit-elements-design` ya mergeada.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- En SELECT CIRCUIT, la opción **START** pasa a ser **SHIP SELECTION** y ya no carga la carrera: abre **SELECT SHIP** con el modo y el circuito elegidos.
- Composición tipo *briefing*, igual que SELECT CIRCUIT:
  - A la izquierda, el **nombre de la nave** en grande, su descripción y la navegación `‹ 02 / 03 ›`; debajo, el **holograma** de la nave sobre una rejilla con un anillo de proyección.
  - A la derecha, el panel con **SHIP DATA** (5 datos con barra y número), **SHARED SYSTEMS** (boost y daño) y las opciones **START** / **BACK**.
- Naves, en este orden: **INTERCEPTOR**, **VECTOR**, **VANGUARD**. Se abre con **VECTOR** (o con la última elegida).
- **El jugador no ve en ningún sitio que Vector es la referencia.** Las barras se calculan respecto a Vector, pero ni los textos ni la pantalla lo dicen.
- **START** deja la nave en `GameSession.ShipDesignId` y carga la carrera. **BACK** / ESC vuelve a SELECT CIRCUIT con el mismo circuito.
- Todos los textos en inglés.

## 2. Flujo

| Desde | Acción | Resultado |
|---|---|---|
| SELECT CIRCUIT | **SHIP SELECTION** | SELECT SHIP en el modo y con el circuito elegidos |
| SELECT SHIP | A/D o ←/→, o clic en `‹ ›` | Nave anterior / siguiente (da la vuelta) |
| SELECT SHIP | **START** (solo con una nave elegible) | `GameSession.ShipDesignId` = la nave; se carga la carrera |
| SELECT SHIP | **BACK**, o ESC | Vuelve a SELECT CIRCUIT con el mismo circuito y SHIP SELECTION seleccionada |
| Pausa | **RESTART** | Repite modo, circuito y nave, sin pasar por los selectores (como ahora) |
| Pausa / resultados | **BACK TO MAIN MENU** | Menú principal (como ahora) |

- SELECT SHIP va **dentro de la escena del menú principal**, como SELECT CIRCUIT y RECORDS, sin cambiar de escena. Mientras está abierta se ocultan el selector de circuito y su miniatura, igual que el selector oculta hoy el menú. La versión de abajo a la izquierda sigue visible.
- **Nave recordada**: al abrir aparece la de `GameSession.ShipDesignId` si es elegible; si no, la de por defecto del catálogo (Vector). No se guarda en disco.
- Al abrirse, la opción seleccionada es **START**.
- El modo de la pantalla es el del selector de circuito (no se puede cambiar aquí).

## 3. Aspecto

Medidas sobre la pantalla de referencia de 1280×720, escaladas con `UiScale` como el resto de menús. Colores siempre desde `Palette`. Fuentes con `MenuFonts`.

### 3.1 Fondo, rejilla y anillo

- Fondo #05070D (`Palette.Background`), con el glow de `WorldStyle`.
- **Rejilla** de suelo en azul en la zona izquierda (hasta el panel), con el horizonte en y ≈ 500 y un desvanecido hacia el fondo, como la del menú principal. Se reutiliza `HorizonGrid` si encaja.
- **Anillo de proyección** sobre la rejilla, bajo la nave, centrado en x ≈ 410: una elipse azul (radio ~250 × 34 px, al ~45 %) y otra interior cian discontinua (~170 × 22 px, al ~30 %).

### 3.2 Holograma de la nave

- Es una **instancia de `Spaceship`** (la misma geometría que en la carrera, sin copiar ni redibujar líneas), con su propia cámara en el mundo del menú, como `CircuitMiniature`. No se rehace la malla cada frame.
- Encuadre: centrado en x ≈ 410, y ≈ 400; la nave ocupa unos **480 px de ancho** de punta a punta de las alas. Se centra por el centro de su caja.
- **Aristas lejanas más tenues** (efecto holograma): el color de cada línea se multiplica por un factor según su profundidad respecto a la cámara, de ~35 % en la parte de atrás a 100 % en la de delante. Se hace con un material propio puesto con `SetMaterialOverride` y **multiplicando el color, no con `ALPHA`** (con `ALPHA` la nave pierde el glow, como ya pasó con los daños). Si con `SetMaterialOverride` no se pueden conservar los colores de cada línea (cian y azul de las toberas), **se avisa antes**.
- Sin efectos de velocidad, sin tobera encendida, sin inclinación y sin parpadeo de daños.

### 3.3 Giro del holograma

- **Orientación inicial**: vista de tres cuartos, con el morro hacia delante a la izquierda y algo inclinada para que se vea por arriba (en la maqueta, giro de 143° sobre el eje vertical y 22° de inclinación).
- **Giro automático**: sobre el eje **vertical de la pantalla**, una vuelta cada **20 s**.
- **Arrastre con el ratón** (clic izquierdo mantenido en la zona de la nave, de x = 0 a 836 y de y = 250 a 670): giro **libre en esfera**. Mover en horizontal gira sobre el eje vertical de la pantalla y mover en vertical sobre el horizontal, siempre respecto a la pantalla (tipo *trackball*), así se puede ver la nave por cualquier lado, también por debajo. ~0,01 rad por píxel.
- Al **soltar**, la nave conserva la velocidad de giro que llevaba y la va perdiendo con un decaimiento exponencial de **0,4 s**. Si se suelta con el ratón quieto (más de 80 ms sin moverlo), no hay inercia.
- Tras **3 s** sin arrastrar, el giro automático **vuelve poco a poco**, de 0 a su velocidad en **1 s**, desde la orientación en la que esté. Sin saltos ni vuelta a la orientación inicial.
- Mientras se arrastra no hay giro automático.
- La orientación se mantiene al cambiar de nave y al volver (también al pasar por una nave COMING SOON).
- Cursor de mano abierta sobre la zona y cerrada al arrastrar.
- Sin giro con teclado y sin zoom.

### 3.4 Bloque del nombre (arriba a la izquierda, x = 72, y = 56)

- `RACE · CIRCUIT 01 · SELECT SHIP` (o `TIME TRIAL · …`), con el `DisplayName` del circuito en mayúsculas: Share Tech Mono ~15 px, cian, espaciado ~0.22 em.
- Nombre de la nave en mayúsculas: Orbitron Black ~54 px, cian, espaciado ~0.08 em, con halo. En una nave COMING SOON, azul al ~75 % y sin halo, y a su derecha, a 16 px, la etiqueta `COMING SOON` en Share Tech Mono ~13 px, azul grisáceo, con borde azul grisáceo de 1 px al ~50 %.
- Descripción: Share Tech Mono ~14 px, azul grisáceo. Textos de la sección 5.1.
- Fila de navegación: botón `‹` (44×44, borde azul de 1 px), contador `02 / 03` en Share Tech Mono ~14 px y botón `›`, como en SELECT CIRCUIT. Con más de una nave, flechas en cian.

### 3.5 Panel (derecha)

Mismo panel que SELECT CIRCUIT: desde **x = 836** hasta el borde, fondo #05070D al ~92 %, línea cian de 1 px a la izquierda con halo hacia la izquierda, márgenes 52 / 40 / 48 / 40 px. Cabeceras (`SHIP DATA`, `SHARED SYSTEMS`) en Share Tech Mono ~13 px, cian, espaciado ~0.22 em.

**SHIP DATA**: 5 filas, separadas ~14 px. Cada fila lleva una línea azul vertical de 2 px a su izquierda y:
- arriba, la etiqueta a la izquierda (Share Tech Mono ~11 px, azul grisáceo) y el valor a la derecha (Orbitron Medium ~15 px, cian);
- debajo, una **barra de 10 segmentos** de 30 × 7 px separados 3 px. Encendido: relleno cian con halo. Apagado: solo borde azul al ~70 %.
- **Sin marca de centro ni ningún texto sobre la referencia.**

Nave COMING SOON: valores `—` en azul grisáceo al ~60 % y los 10 segmentos apagados con el borde al ~35 %.

**SHARED SYSTEMS**: dos filas, etiqueta (Share Tech Mono ~12 px, azul grisáceo, columna de ~120 px) y valor (Orbitron Medium ~15 px):

| Etiqueta | Valor | Color |
|---|---|---|
| `BOOST` | `3 × +100 KM/H` (cargas máximas y impulso por carga del boost) | naranja (`Palette.Orange`) |
| `DAMAGE` | `3 LEVELS` en RACE, `OFF` en TIME TRIAL | cian; `OFF` en azul grisáceo |

La maqueta solo muestra `3 LEVELS`: en TIME TRIAL no hay daños y se muestra `OFF`.

**Opciones**: abajo del panel, **START** y **BACK**, con la `OptionList` y el estilo de SELECT CIRCUIT. Con una nave COMING SOON, START se ve **desactivada** (azul al ~55 %, sin el texto `COMING SOON`, con `HideDisabledHint`) y BACK queda seleccionada.

### 3.6 Placeholder de una nave COMING SOON

En lugar del holograma (el anillo y la rejilla siguen):
- Un marco de cuatro esquinas en azul (líneas de 30 px) alrededor de un rectángulo discontinuo de ~460 × 120 px centrado en x ≈ 410, y ≈ 390.
- Dentro, `SHIP IN DEVELOPMENT` (Share Tech Mono ~15 px, azul grisáceo al ~75 %) y debajo `COMING SOON` (~12 px, al ~50 %).

### 3.7 Pie

- Versión abajo a la izquierda, como en el menú.
- A su derecha, en x ≈ 300: `← → SHIP · ↑ ↓ OPTION · ENTER SELECT · DRAG ROTATE`, en Share Tech Mono ~13 px, azul grisáceo al ~70 %.

## 4. Navegación

- **A / D** y **← / →** (`menu_left` / `menu_right`): nave anterior / siguiente, con vuelta. Pasan también por las COMING SOON (se pueden ver, no elegir).
- **W / S** y **↑ / ↓**: mueven la selección entre las opciones activas (con una COMING SOON, solo queda BACK).
- **Enter / Espacio** (`menu_accept`): eligen la opción.
- **ESC** (`menu_back`): equivale a BACK.
- **Ratón**: hover y clic en START / BACK, clic en `‹ ›` y arrastre sobre la nave (3.3).
- Al pasar de una COMING SOON a una nave elegible, la opción seleccionada vuelve a ser START.
- Teclado y ratón comparten una sola selección.
- El Enter que abre la pantalla (el SHIP SELECTION de SELECT CIRCUIT) no elige nada en ella: se ignora ese frame, como en RECORDS.

## 5. Naves y datos

### 5.1 Catálogo

`ShipCatalog` deja de ser una lista de ids y pasa a ser la lista de **entradas de nave**, en el orden del selector:

| Id | Nombre | Descripción | Elegible |
|---|---|---|---|
| `ship_02` | `INTERCEPTOR` | `HIGH TOP SPEED · LOW TOLERANCE` | No |
| `ship_01` | `VECTOR` | `BALANCED · ALL-ROUND` | **Sí** |
| `ship_03` | `VANGUARD` | `HIGH ACCELERATION · STRONG LAUNCH` | No |

- `ship_01` **no cambia de id**: los fantasmas guardados lo usan.
- `Default` sigue siendo `ship_01` (no el primero de la lista).
- `Contains` y lo que ya usa el catálogo (fantasma, `GameSession`) siguen funcionando igual.
- Una nave no elegible no tiene geometría ni datos técnicos todavía: solo nombre y descripción para el selector.
- Las descripciones de Interceptor y Vanguard son provisionales; la spec de naves las ajustará a su carácter real.

### 5.2 De dónde salen los datos de Vector

Los datos se **calculan** a partir de los parámetros de conducción reales de la nave, no se escriben a mano en el selector.

Hoy los valores buenos de cuatro parámetros están en `Main.tscn` y no en el código (`MaxSpeed = 110`, `Drag = 5`, `TurnRate = 1.3`, `SteerResponse = 8`), así que el menú no tiene de dónde leerlos sin montar la escena de carrera. Como en las otras specs:

1. Los valores de conducción de Vector pasan a un **único sitio puro** (por ejemplo, unos valores base de `ShipTuning` para `ship_01`), con los valores **actuales de la escena** para esos cuatro y los del código para el resto. Leerlos de `Main.tscn`, no inventarlos.
2. Los `[Export]` del `Player` toman de ahí sus valores por defecto, y en `Main.tscn` se quitan esos cuatro overrides para que no tapen nada.
3. **La conducción no puede cambiar ni un pelo.** Las pruebas de `ShipDynamics` siguen pasando sin tocarlas y una prueba nueva comprueba que los valores base son 110 / 5 / 1,3 / 8.
4. Si esto obliga a tocar cómo el `Player` monta `ShipTuning` más allá de sus valores por defecto, **se avisa antes**.

El perfil técnico completo de cada nave (y qué varía entre ellas) es de la spec de naves. Esta spec solo saca los valores base de Vector a un sitio que el menú pueda leer.

### 5.3 SHIP DATA: valores

| Etiqueta | Valor de Vector hoy | Cómo se calcula |
|---|---|---|
| `TOP SPEED` | `396 KM/H` | `MaxSpeed` × 3,6, redondeado a km/h |
| `ACCELERATION` | `0–200 · 2.2 S` | (200 / 3,6) / `Acceleration`, 1 decimal. La aceleración es lineal en `ShipDynamics` |
| `HANDLING` | `74 °/S` | `TurnRate` en grados por segundo, redondeado |
| `GRIP` | solo barra | `LateralGrip` no tiene una unidad que entienda el jugador: se muestra sin número (sitio del valor vacío) |
| `BRAKING` | `200–0 · 1.2 S` | (200 / 3,6) / `BrakeDeceleration`, 1 decimal |

- Son los valores base de la nave: **sin daño, sin pendiente, sin peralte**.
- La maqueta muestra `GRIP 5.0`: es el valor interno y no se enseña.

### 5.4 SHIP DATA: barras

- Cada barra compara el parámetro de la nave con el de **Vector** (`ship_01`), siempre con un parámetro en el que "más es mejor": `MaxSpeed`, `Acceleration`, `TurnRate`, `LateralGrip` y `BrakeDeceleration` (no los tiempos).
- Segmentos encendidos = 5 + redondeo(50 × (valor / valor de Vector − 1)), limitado a **0–10**. Así Vector llena **5** en todo, y cada ±10 % respecto a Vector es ±5 segmentos (±2 % por segmento).
- Las naves se separarán de Vector con tope, así que las barras no se saldrán del rango en la práctica. Si alguna lo hiciera, se queda en 0 o en 10.
- Hoy, con solo Vector elegible, todas las barras salen con **5 segmentos**.

## 6. Cambios en otras pantallas

- **SELECT CIRCUIT**:
  - La opción **START** pasa a llamarse **SHIP SELECTION** (en mayúsculas, como el resto de opciones). Solo cambia el texto: el identificador de la opción y `CircuitSelectAction.Start` se pueden mantener.
  - Si `SHIP SELECTION` no cabe en el panel al tamaño de las opciones (22 px), se reduce sola con `FitFontSize`, como en los demás menús. Si queda demasiado pequeña, **se avisa**.
  - SHIP SELECTION abre SELECT SHIP en lugar de cargar la carrera, y deja el circuito en `GameSession.CircuitPath` antes. Lo que cambia es lo que hace `CircuitSelectScreen` con la acción.
  - Al volver de SELECT SHIP con BACK, SELECT CIRCUIT se muestra con el mismo circuito y SHIP SELECTION seleccionada.
  - El lienzo del selector de circuito (Selector C) sigue mostrando START: manda esta spec.
- **Carrera**: lee `GameSession.ShipDesignId` como ya hace. Hoy siempre es `ship_01`, así que la carrera no cambia.
- **Fantasma**: no cambia. Ya guarda el diseño de nave; con una sola nave, siempre `ship_01`.
- **Récords**: no cambian. Son por circuito y modo, **sea cual sea la nave**. No se guarda la nave en el récord en esta spec.
- **Menú principal**: no cambia. HANGAR sigue COMING SOON.

## 7. Parámetros en el Inspector

| Parámetro | Valor |
|---|---|
| Tiempo de una vuelta del giro automático | 20 s |
| Inclinación inicial del holograma | 22° |
| Giro inicial sobre el eje vertical | 143° |
| Sensibilidad del arrastre | 0,01 rad por píxel |
| Decaimiento de la inercia | 0,4 s |
| Espera antes de volver al giro automático | 3 s |
| Rampa de vuelta al giro automático | 1 s |
| Ancho de la nave en pantalla | ~480 px |
| Atenuación de las aristas más lejanas | 35 % |
| Tamaño del nombre de la nave | 54 px |
| Tamaño de las opciones | 22 px |

## 8. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **`ShipCatalog`** (puro): entradas con `Id`, nombre, descripción y si es elegible; `Ids`, `Default`, `Contains` y `Find(id)`.
- **Valores base de Vector** (puro): los de la sección 5.2.
- **`ShipStats`** (puro): de un `ShipTuning` y el de referencia, los 5 datos (valor ya formateado y número de segmentos) de las secciones 5.3 y 5.4.
- **`ShipSelectLogic`** (pura): modo, circuito (para el texto), lista de naves, nave actual (recordada o `Default`), `NextShip()` / `PreviousShip()` con vuelta, opciones START / BACK con `MenuModel` (START desactivada en una no elegible), y qué hacer: `None`, `Start` (con el id de la nave) o `BackToCircuitSelect`. Sin archivos ni escenas.
- **`ShipSelectFormat`** (puro): `RACE · CIRCUIT 01 · SELECT SHIP`, `02 / 03`, `396 KM/H`, `0–200 · 2.2 S`, `74 °/S`, `200–0 · 1.2 S`, `—`, `3 × +100 KM/H`, `3 LEVELS` / `OFF`.
- **`HologramSpin`** (puro, `System.Numerics`): orientación (cuaternión o matriz), `Drag(dx, dy)`, `Release(…)`, `Step(delta)` con inercia, espera y rampa del giro automático. Con `delta` = 0 no cambia nada.
- **`ShipHologram`** (nodo Godot): la instancia de `Spaceship` con su cámara en el mundo del menú y el material del holograma; aplica la orientación de `HologramSpin`. Sin reglas.
- **`ShipSelectScreen`** (nodo, en `scripts/Menu`): dibuja el bloque del nombre y el panel reutilizando `OptionList` y los componentes del selector de circuito, traduce teclado y ratón, llama a la lógica y rellena `GameSession` al empezar.

### Pruebas unitarias mínimas

Catálogo y valores:
1. El catálogo tiene INTERCEPTOR, VECTOR y VANGUARD en ese orden; solo `ship_01` es elegible; `Default` es `ship_01`; `Contains("ship_01")` sigue siendo verdadero.
2. Los valores base de Vector son `MaxSpeed` 110, `Drag` 5, `TurnRate` 1,3 y `SteerResponse` 8, y el resto los de siempre.

Datos:
3. Con los valores de Vector: `396 KM/H`, `0–200 · 2.2 S`, `74 °/S`, `200–0 · 1.2 S` y 5 segmentos en las 5 barras.
4. Una nave con un 10 % más de punta → 10 segmentos; con un 10 % menos → 0; con un 2 % más → 6. Un 30 % más se queda en 10.

Lógica:
5. Al abrir con `ShipDesignId` = `ship_01`: VECTOR y START seleccionada. Con un id no elegible o desconocido: VECTOR.
6. Next / Previous dan la vuelta y pasan por las no elegibles.
7. En una no elegible, START está desactivada y BACK seleccionada; Choose(START) no hace nada.
8. START en VECTOR devuelve `Start` con `ship_01`; BACK y ESC devuelven `BackToCircuitSelect`.

Formato:
9. Cabecera con el modo y el circuito; contador `02 / 03`; `DAMAGE` = `3 LEVELS` en RACE y `OFF` en TIME TRIAL.

SELECT CIRCUIT:
10. Las opciones de `CircuitSelectLogic` son `SHIP SELECTION` y `BACK`; elegir `SHIP SELECTION` devuelve la misma acción que antes devolvía START. Se adaptan las pruebas existentes que comprueben el texto `START`.

Holograma:
11. Sin arrastrar, tras un `Step` de 20 s a velocidad completa la orientación ha dado una vuelta.
12. Tras soltar con velocidad, la velocidad baja con el decaimiento; durante los primeros 3 s no hay giro automático; a los 4 s está al 100 %.
13. Arrastrar en horizontal gira alrededor del eje vertical de la pantalla, sea cual sea la orientación.
14. `Step(0)` no cambia nada.

Las pruebas existentes siguen pasando.

## 9. Prueba manual

1. Menú → RACE: en SELECT CIRCUIT la opción de abajo dice **SHIP SELECTION** (no START) y cabe bien en el panel. Elegirla: se abre SELECT SHIP con `RACE · CIRCUIT 01 · SELECT SHIP`, VECTOR, `02 / 03` y START seleccionada. No se ve el selector de circuito ni su miniatura.
2. Comparar con el lienzo: composición, colores y tamaños.
3. La nave gira sola, despacio. Arrastrar con el ratón: se puede girar en cualquier dirección, también verla por debajo. Al soltar sigue un poco y se frena. A los 3 s vuelve a girar sola, sin saltos.
4. Las aristas del fondo de la nave se ven más tenues que las de delante, y la nave conserva su glow y sus colores.
5. Los datos de VECTOR: `396 KM/H`, `0–200 · 2.2 S`, `74 °/S`, GRIP solo barra, `200–0 · 1.2 S`, y todas las barras a la mitad. No hay ninguna marca ni texto de referencia.
6. D / → / `›`: VANGUARD con COMING SOON, placeholder, datos en `—`, START desactivada y BACK seleccionada. Enter: vuelve a SELECT CIRCUIT. Otra vez D: INTERCEPTOR igual. Otra vez: VECTOR, con la nave en la misma orientación y START seleccionada.
7. ESC: vuelve a SELECT CIRCUIT con el mismo circuito y SHIP SELECTION seleccionada. Elegirla de nuevo: SELECT SHIP con VECTOR.
8. START en VECTOR: la carrera empieza como siempre (parrilla, semáforo, daño, HUD). Conducir: **se conduce exactamente igual que antes** (punta, aceleración, giro, derrape).
9. Pausa → RESTART: repite la carrera sin pasar por los selectores.
10. TIME TRIAL → circuito → SELECT SHIP: `TIME TRIAL · …` y `DAMAGE OFF`. START: salida lanzada, fantasma y récords como antes.
11. Probar todo también con el ratón, y en ventana y a pantalla completa.
12. Ejecutar la escena de carrera sola desde el editor: sigue funcionando como antes y con la misma conducción.

## 10. Fuera de alcance

- Perfil técnico, geometría y equilibrio de Interceptor y Vanguard (spec de naves), y el piloto automático de equilibrio.
- Separar del todo la parte técnica y la visual de la nave (spec de naves). Aquí solo se sacan los valores base de Vector.
- Que la carrera o el fantasma usen una geometría o unos valores distintos según la nave.
- Guardar la nave en los récords o mostrarla en RECORDS y en los resultados.
- HANGAR.
- Giro del holograma con teclado, zoom, y efectos (tobera, estela) en el holograma.
- Guardar en disco la última nave elegida.
- Animaciones de transición al cambiar de nave o de pantalla.
- Sonido y mando.
