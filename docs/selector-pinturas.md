# VECTORY — Spec: Selector de pinturas

Versión 1.0 · 2026-11-16 · Estado: **propuesta, pendiente de validar**

Añade una pantalla **PAINT** que sale de **SELECT SHIP**. En ella el jugador elige la pintura de la nave entre las **40 combinaciones** de su catálogo: 20 pinturas, cada una lisa y con marcas. La pintura elegida **se guarda por nave** y es la que lleva su nave en carrera y en el holograma del selector.

Diseño de referencia: **`design/selector-pinturas.html`**, composición **A · TALLER**. Las composiciones B y C se quedan en el archivo como diseño guardado. La nave de la maqueta es **simplificada**: aquí va el casco 3D real (`ShipHull3D`), igual que en el holograma de SELECT SHIP. Si algo no cuadra con la maqueta, manda esta spec y se avisa.

Se apoya en lo que ya existe:
- `ShipSelectScreen` / `ShipSelectLogic` / `ShipSelectFormat` y `ShipHologram` / `HologramSpin`.
- `ShipHull3D` y su `SetPaint`, los **catálogos de pinturas** de cada nave y sus **aplicadores** (specs `cascos-3d-1-vector`, `cascos-3d-2-blade` y `cascos-3d-3-hammer`).
- `GameSession` y `GameSettings` (`user://settings.json`, con `lastShipDesignId` de la spec `naves-4-integracion`).
- `OptionList`, `MenuModel`, `Palette`, `MenuFonts`, `UiScale` y `FitFontSize`.

Rama: `feature/paint-select`, que sale de `develop` con los tres cascos 3D ya mergeados. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 0 · Inventario**: mirar y apuntar, sin cambiar nada.
- **Etapa 1 · Pintura guardada**:
  - nombres en inglés en los catálogos;
  - una pintura guardada **por nave** en `settings.json`;
  - la carrera y el holograma de SELECT SHIP usan la pintura guardada.
  - Todavía sin pantalla nueva.
- **Etapa 2 · Pantalla PAINT**:
  - opción **PAINT** en SELECT SHIP;
  - la pantalla con la composición TALLER;
  - navegación, vista previa en el holograma y **APPLY** / **BACK**.
- **No cambian**: la conducción, los catálogos (colores, acabados, marcas), el fantasma (sigue wireframe), la cabina 3D por dentro y el HUD.
- **Rivales**: fuera de esta spec. Lo decidido para cuando lleguen va en la sección 10.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Dónde viven los **catálogos de pinturas** de las tres naves, cómo se identifica una pintura (`id`) y si las marcas son un `bool` aparte.
2. Cómo se le pasa la pintura a `ShipHull3D` (`SetPaint(...)`) y si cambiarla **rehace la malla** o solo cambia materiales y calcomanías. Cambiar de pintura en la pantalla tiene que ser **instantáneo y sin tirones**.
3. Qué pintura lleva hoy la nave en carrera y en el holograma (la **por defecto** de cada catálogo) y desde dónde se decide.
4. Cómo están hoy `GameSettings` y `settings.json`: campos, escritura segura, `.bak` y lectura tolerante.
5. Cómo monta `ShipSelectScreen` sus opciones (START / BACK) y cómo se abre y se cierra (dentro de la escena del menú, sin cambiar de escena).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Pintura guardada

### 3.1 Elección de pintura

Una elección es **(id de pintura, con marcas sí / no)**: `PaintChoice`.

- Cada nave tiene la suya.
- Por defecto, la **pintura por defecto** de su catálogo (la de las specs de los cascos):

| Nave | Por defecto |
|---|---|
| VECTOR | `graphite_red`, lisa |
| BLADE | `woodland`, con marcas |
| HAMMER | `site_yellow`, con marcas |

### 3.2 Guardado

- `user://settings.json` gana un campo **`shipPaints`**: por cada id de nave, `{ "paint": "<id>", "marks": true|false }`.

```json
"shipPaints": {
  "ship_01": { "paint": "graphite_red", "marks": false },
  "ship_02": { "paint": "woodland", "marks": true },
  "ship_03": { "paint": "site_yellow", "marks": true }
}
```

- **Solo se escribe** al pulsar **APPLY** (etapa 2). Una nave sin entrada usa su pintura por defecto.
- Lectura **tolerante**:
  - archivo antiguo sin `shipPaints` → todas por defecto;
  - nave desconocida → se ignora;
  - pintura que no está en el catálogo de esa nave → la por defecto de esa nave;
  - `marks` ausente o no booleano → el de la pintura por defecto.
- Mismas reglas de escritura que hoy: escritura segura y `.bak`. Guardar una pintura **no toca** `lastShipDesignId` ni `ghostEnabled`, y al revés.
- **RESET RECORDS no lo borra**: es un ajuste, no un récord.

### 3.3 Dónde se usa

| Sitio | Pintura |
|---|---|
| Carrera y TIME TRIAL (tu nave, cámara exterior) | La guardada de la nave elegida |
| Pausa → RESTART | La misma |
| Holograma de SELECT SHIP | La guardada de cada nave, al pasar de una a otra |
| Pantalla PAINT | La que se esté viendo (vista previa); al salir, la guardada |
| Fantasma | Sin cambios: wireframe |
| Cabina 3D por dentro | Sin cambios |
| Nave del fondo del menú principal | Sin cambios |

- La carrera la toma al **montar la nave**. No hace falta cambiarla en mitad de una carrera.
- El panel de pruebas (F9–F12) sigue igual. F10 (cambiar de nave) pone la **pintura guardada** de la nave nueva.

### 3.4 Nombres en inglés

Los catálogos ganan un **nombre para pantalla** en inglés. Los nombres internos de las specs de los cascos (GRAFITO, BOSQUE…) dejan de usarse en pantalla. Los `id` **no cambian**.

**VECTOR**

| `id` | Nombre | `id` | Nombre |
|---|---|---|---|
| `graphite_red` | GRAPHITE | `acid_green` | ACID GREEN |
| `piano_cyan` | PIANO BLACK | `british_green` | BRITISH GREEN |
| `black_chrome` | BLACK CHROME | `cobalt` | COBALT |
| `stealth_matte` | STEALTH | `midnight` | MIDNIGHT |
| `pearl_white` | PEARL WHITE | `ice` | ICE |
| `liquid_silver` | LIQUID SILVER | `violet` | VIOLET |
| `titanium` | TITANIUM | `neon_pink` | NEON PINK |
| `racing_red` | RACING RED | `chameleon` | CHAMELEON |
| `lava` | LAVA | `amber` | AMBER |
| `taxi` | TAXI YELLOW | `gold` | GOLD |

Acabados: BRILLO → `GLOSS`, METALIZADO → `METALLIC`, SATINADO → `SATIN`, MATE → `MATTE`, CROMADO → `CHROME`, PERLA → `PEARL`, CAMALEÓN → `CHAMELEON`.

**BLADE**

| `id` | Nombre | `id` | Nombre |
|---|---|---|---|
| `woodland` | WOODLAND | `olive` | OLIVE |
| `desert` | DESERT | `sand` | SAND |
| `arctic` | ARCTIC | `field_grey` | FIELD GREY |
| `urban` | URBAN | `ops_black` | OPS BLACK |
| `night` | NIGHT | `nato_green` | NATO GREEN |
| `jungle` | JUNGLE | `earth` | EARTH BROWN |
| `tundra` | TUNDRA | `navy_grey` | NAVY GREY |
| `naval_split` | NAVAL | `winter_white` | WINTER WHITE |
| `autumn` | AUTUMN | `slate_blue` | SLATE BLUE |
| `ash` | ASH | `khaki` | KHAKI |

**HAMMER**

| `id` | Nombre | `id` | Nombre |
|---|---|---|---|
| `site_yellow` | SITE YELLOW | `khaki` | KHAKI |
| `bright_yellow` | BRIGHT YELLOW | `safety_green` | SAFETY GREEN |
| `orange` | ORANGE | `cream` | CREAM |
| `terracotta` | TERRACOTTA | `maroon` | MAROON |
| `tractor_green` | TRACTOR GREEN | `petrol_blue` | PETROL BLUE |
| `crane_red` | CRANE RED | `yellow_black` | YELLOW · BLACK CAB |
| `fleet_white` | FLEET WHITE | `orange_white` | ORANGE · WHITE CAB |
| `site_blue` | SITE BLUE | `white_red` | WHITE · RED CAB |
| `plant_grey` | PLANT GREY | `green_white` | GREEN · WHITE CAB |
| `coal_black` | COAL BLACK | `grey_orange` | GREY · ORANGE CAB |

### 3.5 Criterio de aceptación

- Sin `shipPaints` en el archivo, todo se ve **exactamente igual** que antes: mismas pinturas por defecto en carrera y en el holograma.
- Escribiendo a mano en `settings.json` otra pintura para una nave, esa nave sale con ella en carrera y en el holograma. Las otras dos, no.

## 4. Etapa 2 · Pantalla PAINT

### 4.1 Flujo

| Desde | Acción | Resultado |
|---|---|---|
| SELECT SHIP | **PAINT** | Pantalla PAINT de la nave que se está viendo, con su pintura guardada seleccionada |
| PAINT | Moverse por las muestras | La nave del holograma se pinta con la muestra seleccionada (**vista previa**). No se guarda nada |
| PAINT | **APPLY** | Se guarda la pintura de esa nave (3.2) y se vuelve a SELECT SHIP con **PAINT** seleccionada |
| PAINT | **BACK**, o ESC | Se vuelve a SELECT SHIP **sin cambios**: la nave vuelve a su pintura guardada, con **PAINT** seleccionada |

- **SELECT SHIP** pasa a tener tres opciones: **START**, **PAINT** y **BACK**, en ese orden.
  - Al abrirse, la seleccionada sigue siendo START.
  - Con una nave COMING SOON (hoy no hay ninguna, pero el código lo mantiene), PAINT va **desactivada** igual que START.
- PAINT va **dentro de la escena del menú**, como SELECT SHIP, sin cambiar de escena. Se ocultan el bloque del nombre y el panel de SELECT SHIP.
- El holograma **es el mismo**: no se rehace ni se recoloca. Conserva la orientación y el giro (`HologramSpin`) al entrar y al salir.
- El Enter que abre la pantalla no elige nada en ella: se ignora ese frame, como en RECORDS y en SELECT SHIP.

### 4.2 Composición

Medidas sobre la pantalla de referencia de 1280×720, escaladas con `UiScale`. Colores desde `Palette`. Fuentes con `MenuFonts`.

**Zona izquierda** (la de SELECT SHIP):
- Fondo, rejilla, anillo de proyección y holograma: **los de SELECT SHIP**, sin cambios.
- Arriba a la izquierda (x = 72, y = 56): `RACE · CIRCUIT 01 · SELECT SHIP · PAINT` (o `TIME TRIAL · …`). Share Tech Mono ~15 px, cian, espaciado ~0.22 em. Si no cabe antes del panel (x = 836), se reduce con `FitFontSize`.
- Debajo, el **nombre de la nave**, como en SELECT SHIP (Orbitron Black ~54 px, cian, con halo).
- Debajo (y ≈ 150): `PAINT · ` + nombre de la pintura seleccionada. Share Tech Mono ~14 px, azul grisáceo, con el nombre en cian. Espaciado ~0.12 em.
- Sin navegación `‹ ›` de naves: aquí no se cambia de nave.

**Panel** (derecha): el mismo de SELECT SHIP (desde x = 836, fondo #05070D al ~92 %, línea cian a la izquierda con halo, márgenes 52 / 40 / 48 / 40 px).

| Bloque | Posición (dentro del panel) | Contenido y estilo |
|---|---|---|
| Cabecera | y = 52 | `PAINT`. Share Tech Mono ~13 px, cian, espaciado ~0.22 em |
| Nombre de la pintura | y = 76 | Orbitron Medium ~24 px, blanco azulado (`#E9F6FF`), espaciado ~0.06 em. A su derecha, a 10 px, la etiqueta de estado (4.4). Si no cabe, `FitFontSize` |
| Línea de datos | y = 112 | Share Tech Mono ~12 px, azul grisáceo, espaciado ~0.12 em (4.5) |
| Grupo 1 | y = 150 | `PLAIN`. Share Tech Mono ~11 px, azul grisáceo, espaciado ~0.2 em |
| Muestras lisas | y = 170 | Cuadrícula de **5 × 4** (4.3) |
| Grupo 2 | y = 336 | Nombre de las marcas de la nave: `RACING LIVERY` (VECTOR), `MARKINGS` (BLADE), `FLEET MARKINGS` (HAMMER) |
| Muestras con marcas | y = 356 | Cuadrícula de **5 × 4** |
| Opciones | abajo del panel | **APPLY** y **BACK**, con la `OptionList` y el estilo de SELECT SHIP |

**Pie**: la versión abajo a la izquierda, como en el menú. A su derecha, en x ≈ 110:
`← → ↑ ↓ PAINT · M <grupo 2> · ENTER APPLY · ESC BACK · DRAG ROTATE`. Share Tech Mono ~13 px, azul grisáceo al ~70 %.

### 4.3 Muestras

- **Tamaño**: 66 × 30 px, separadas 8 px. Borde azul de 1 px al ~70 %; en hover, cian.
- **Orden**: el del catálogo. Las 20 lisas en la primera cuadrícula y las 20 con marcas en la segunda, de izquierda a derecha y de arriba abajo.
- **Seleccionada**: contorno cian de 2 px separado 2 px, con halo cian.
- **Equipada** (la guardada): un rombo cian de 7 px arriba a la izquierda, con halo. Puede coincidir con la seleccionada.
- **Dibujo**: es un **dibujo plano** con los colores del catálogo, no un render de la nave. Por nave:

| Nave | Lisa | Con marcas (además de lo de la lisa) |
|---|---|---|
| VECTOR | Degradado a 135° del color de la pintura (55 %) a negro. Abajo, una barra del **color de acento** de 3 px de alto, a 4 px de los bordes, con halo | Dos rayas verticales de 2 px del color de las **franjas**, a un 38 % del ancho y separadas 3 px. Arriba a la derecha, el **dorsal** en Orbitron Black cursiva ~10 px, del color del fondo del dorsal |
| BLADE camuflaje | Fondo `c0` con manchas de `c1` y `c2` en sitios **fijos** (los de la maqueta, iguales en todas las muestras) | Arriba a la derecha, un círculo de 11 px como el distintivo (relleno claro con borde oscuro) |
| BLADE lisa | Degradado a 135° del color (60 %) a negro | Igual |
| HAMMER lisa | Degradado a 135° del color (60 %) a negro | Abajo, una banda de 6 px de **franjas de peligro** amarillo y negro a 45° |
| HAMMER bicolor | Partida en diagonal a 135°: casco a un lado (58 %) y cabina al otro | Igual |

- Se dibujan **una vez** al abrir la pantalla (por ejemplo, en una textura o con `_Draw` en un control propio), no cada frame.

### 4.4 Etiqueta de estado

Junto al nombre de la pintura:
- `EQUIPPED` si la seleccionada es la guardada: borde cian de 1 px al ~60 %, texto cian.
- `PREVIEW` si no: borde azul grisáceo al ~45 %, texto azul grisáceo.
- Share Tech Mono ~11 px, espaciado ~0.18 em, relleno 2 × 7 px.

### 4.5 Línea de datos

Formato: partes separadas por ` · `.

| Nave | Lisa | Con marcas |
|---|---|---|
| VECTOR | `<ACABADO> · 07 / 40` | `<ACABADO> · NO. <dorsal> · 27 / 40` |
| BLADE | `CAMO · MATTE · 02 / 40` o `MATTE · 11 / 40` / `SATIN · 14 / 40` | igual + `· MARKINGS` antes del contador |
| HAMMER | `INDUSTRIAL · 01 / 40` o, en las bicolor, `TWO-TONE · 16 / 40` | igual + `· FLEET MARKINGS` antes del contador |

- El **contador** es la posición en las 40 combinaciones, con dos cifras: las lisas son 01–20 y las con marcas 21–40.
- El dorsal de VECTOR es el de la pintura. No se puede cambiar.

### 4.6 Navegación

La selección está **en las muestras** o **en las opciones**. Al abrir la pantalla, en la muestra equipada.

**En las muestras:**

| Tecla | Qué hace |
|---|---|
| ← / → (A / D) | Muestra anterior / siguiente **dentro de su cuadrícula**, con vuelta (de la 20 a la 1) |
| ↑ / ↓ (W / S) | Fila de arriba / abajo. Desde la última fila de lisas, ↓ baja a la primera de marcas (misma columna) y al revés. Desde la última fila de marcas, ↓ pasa a las **opciones** (APPLY) |
| M | Cambia entre lisa y con marcas de la **misma pintura** |
| Enter / Espacio | **APPLY** |
| ESC | **BACK** |

**En las opciones:** ↑ / ↓ entre APPLY y BACK. Desde APPLY, ↑ vuelve a la última muestra seleccionada. Enter elige. ESC es BACK.

**Ratón:**
- Clic en una muestra: la selecciona (vista previa). No guarda.
- Hover y clic en APPLY / BACK.
- Arrastre sobre la nave: el giro de siempre (misma zona que en SELECT SHIP).

Teclado y ratón comparten una sola selección. Cada cambio de muestra **pinta la nave al momento**.

### 4.7 Criterio de aceptación

- Se ve como la composición A de la maqueta, con el casco real.
- APPLY guarda y la nave sale con esa pintura en carrera. BACK no guarda nada.
- Moverse rápido por las 40 muestras no da tirones.

## 5. Parámetros en el Inspector

| Parámetro | Valor |
|---|---|
| Tamaño de las muestras | 66 × 30 px |
| Separación entre muestras | 8 px |
| Tamaño del nombre de la pintura | 24 px |

Las pinturas por defecto ya están en el Inspector de cada casco (specs de los cascos). No hay más parámetros nuevos.

## 6. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Catálogos de pinturas** (puros, ya existen): ganan el **nombre para pantalla** y, si no lo tienen, el **acabado para pantalla**. Sin más cambios.
- **`PaintChoice`** (puro): id de pintura y marcas. Con la **posición en las 40** (0–39) y la vuelta de una a otra, dado un catálogo.
- **Pintura guardada** (en `GameSettings`, pura): leer y escribir `shipPaints` con las reglas de 3.2. `PaintFor(shipId)` devuelve la guardada o la por defecto.
- **`PaintSelectLogic`** (pura):
  - nave, catálogo, selección y si está en las muestras o en las opciones;
  - la navegación de 4.6;
  - qué hacer: `None`, `Apply` (con la `PaintChoice`) o `Back`.
  - Sin archivos ni escenas.
- **`PaintSelectFormat`** (puro): la cabecera, `PAINT · <nombre>`, la línea de datos de 4.5, el contador y la etiqueta de estado.
- **Estilo de las muestras** (puro): de (nave, pintura, marcas) a lo que hay que dibujar (colores, partes que se ven, dorsal). El nodo solo dibuja.
- **`PaintSelectScreen`** (nodo, en `scripts/Menu`): dibuja el panel y las muestras, traduce teclado y ratón, llama a la lógica, pinta el holograma con la vista previa y guarda con APPLY.
- **`ShipSelectScreen` / `ShipSelectLogic`**: la opción PAINT y la acción de abrir la pantalla. Al volver, pinta el holograma con la pintura guardada.
- **Carrera**: al montar la nave del jugador, la pintura sale de `PaintFor(shipId)`. La nave no lee archivos: se la pasan.

## 7. Pruebas

### Pruebas unitarias mínimas

Pintura guardada (etapa 1):
1. Sin archivo o sin `shipPaints`: VECTOR `graphite_red` lisa, BLADE `woodland` con marcas y HAMMER `site_yellow` con marcas.
2. Guardar `ship_02` → `desert` con marcas y volver a cargar: BLADE sale así y las otras dos siguen por defecto.
3. Una pintura que no es del catálogo de esa nave (por ejemplo, `site_yellow` en `ship_01`): por defecto de esa nave.
4. Guardar una pintura no cambia `lastShipDesignId` ni `ghostEnabled`, y RESET RECORDS no borra `shipPaints`.

Catálogos (etapa 1):
5. Las 60 pinturas tienen nombre en inglés, sin repetir dentro de cada nave, y los `id` son los de antes.
6. Acabados de VECTOR en inglés: `graphite_red` → `SATIN`, `chameleon` → `CHAMELEON`.

Formato (etapa 2):
7. Cabecera: `RACE · CIRCUIT 01 · SELECT SHIP · PAINT` y `TIME TRIAL · …`.
8. Línea de datos: VECTOR `graphite_red` lisa → `SATIN · 01 / 40`; con librea → `SATIN · NO. 07 · 21 / 40`; BLADE `desert` con marcas → `CAMO · MATTE · MARKINGS · 22 / 40`; HAMMER `orange_white` lisa → `TWO-TONE · 17 / 40`.
9. Etiqueta: `EQUIPPED` si la selección es la guardada; `PREVIEW` si no.

Lógica (etapa 2):
10. Al abrir, la selección es la pintura guardada.
11. → desde la 20 lisa va a la 1 lisa; ← desde la 1 con marcas va a la 20 con marcas.
12. ↓ desde la fila 4 de lisas va a la fila 1 de marcas, misma columna. ↓ desde la fila 4 de marcas pasa a APPLY. ↑ desde APPLY vuelve a esa muestra.
13. M cambia lisa ↔ con marcas sin cambiar de pintura.
14. Enter en una muestra devuelve `Apply` con la selección. ESC devuelve `Back`. Ninguna escribe en disco (eso lo hace la pantalla).
15. En SELECT SHIP, las opciones son START, PAINT y BACK. Con una nave no elegible (catálogo de prueba), PAINT y START van desactivadas.

Estilo de las muestras (etapa 2):
16. VECTOR con librea lleva rayas y dorsal. Lisa, no.
17. HAMMER bicolor lleva los dos colores (casco y cabina). Con marcas, la banda de peligro.

Las pruebas existentes siguen pasando. Las de SELECT SHIP que comprueban START / BACK se adaptan a START / PAINT / BACK.

## 8. Prueba manual

**Etapa 1 · Pintura guardada**
1. Sin tocar nada: las tres naves se ven como antes en SELECT SHIP y en carrera.
2. Con el juego cerrado, poner a mano en `settings.json` `"ship_01": { "paint": "racing_red", "marks": true }`. Abrir: VECTOR roja con librea en el holograma y en carrera (cámara exterior). BLADE y HAMMER, igual que antes.
3. Poner una pintura que no existe: VECTOR sale con su pintura por defecto, sin errores.
4. Pausa → RESTART: la misma pintura. F10 en depuración: cada nave con la suya.

**Etapa 2 · Pantalla PAINT**
5. SELECT SHIP: las opciones son START, PAINT y BACK, con START seleccionada.
6. PAINT con VECTOR: se abre la pantalla con la pintura guardada seleccionada y marcada como equipada. El holograma no salta ni cambia de orientación.
7. Comparar con la composición A de la maqueta: panel, cuadrículas, textos y pie.
8. Moverse con las flechas por las 40 muestras: la nave cambia al momento, sin tirones. La etiqueta pasa a `PREVIEW`. La línea de datos y el contador cuadran.
9. M: cambia entre lisa y con marcas de la misma pintura.
10. BACK: vuelve a SELECT SHIP con la pintura de antes y PAINT seleccionada.
11. Volver a PAINT, elegir otra y APPLY: SELECT SHIP con la nueva. START: la nave sale con ella.
12. Lo mismo con BLADE y HAMMER (comprobar camuflajes y bicolores en las muestras y en la nave).
13. Cerrar el juego y abrirlo: las tres naves conservan su pintura. RESET RECORDS no la borra.
14. Todo con el ratón: clic en muestras, APPLY, BACK y arrastre de la nave. En ventana y a pantalla completa.
15. TIME TRIAL: la cabecera dice `TIME TRIAL · …` y la nave sale con su pintura. El fantasma sigue wireframe.

## 9. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Pintura guardada** | Sección 3 | Pruebas 1–6 · Prueba manual 1–4 · Commit |
| **2 · Pantalla PAINT** | Sección 4 · versión | Pruebas 7–17 · Prueba manual 5–15 · Commit |

No se empieza una etapa sin validar la anterior.

## 10. Fuera de alcance

- **Pinturas de los rivales.** Decidido para su spec:
  - en carrera suelta, cada rival lleva una pintura **aleatoria** (de las 40 de su nave) en cada carrera;
  - en **campeonato**, cada rival lleva **la misma** pintura en todas las carreras del campeonato.
- Cambiar el dorsal o el número de las marcas: va siempre el de la pintura.
- Pinturas por circuito, desbloqueables o de pago.
- HANGAR (sigue COMING SOON).
- Las composiciones B y C de la maqueta.
- Que el fantasma o la nave del fondo del menú lleven pintura.
- Animaciones de transición, sonido y mando.
