# VECTORY — Spec: Naves 3b · Cabinas de Vector, Hoja y Martillo

Versión 1.1 · 2026-11-04 · Estado: **etapa 1 validada; etapas 2 y 3 pendientes**

Segunda parte de la spec 3. Mete en el juego las **cabinas nuevas** de las tres naves: **VECTOR · Doble pantalla**, **HOJA · Burbuja** y **MARTILLO · Búnker**, con sus paneles sólidos, su luz y el **HUD de velocidad, boost y daño integrado en el cuadro de mando**. También el **ojo de la cámara de cabina** de cada nave.

Plan de las specs de naves:

1. Definición de nave (hecha).
2. Perfil técnico, colisión y ajuste en pruebas (hecha).
3. Cascos de Hoja y Martillo (hecha).
3b. **Cabinas nuevas de Vector, Hoja y Martillo** (esta).
4. Integración: desbloquearlas en el selector y usarlas en carrera y fantasma.
5. Piloto automático de equilibrio (opcional).

Se apoya en lo que ya existe tras las specs 1, 2 y 3: `ShipDefinition` con su perfil visual (la cabina y el ojo de la cámara de cabina ya son de cada nave), `Cockpit`, las dos cámaras y los efectos de la spec `camara`, `Hud`, la lógica del boost, el modelo de daño y el parpadeo de aristas de la spec `danos`, `Palette` y el panel de pruebas (F10).

Diseño de referencia:
- **`designs/cabina-vector.js`**: Vector = `Cab_Vector_3` (DOBLE PANTALLA).
- **`designs/cabinas-hoja-martillo-def.js`**: Hoja = `Cab_Hoja_1` (BURBUJA) y Martillo = `Cab_Martillo_1` (BÚNKER).
- **`designs/lib.js`**: piezas comunes, colores, degradados y lógica del HUD vivo.
- Lienzo **"VECTORY — Naves"**, página **Cabinas**: las tres cabinas definitivas.
- Las opciones de cabina descartadas **no** se usan.

Si algo no cuadra entre el lienzo y los archivos, **mandan los archivos** y se avisa.

Rama: `feature/ship-cockpits`, que sale de `develop` con `feature/ship-geometry` ya mergeada. Commits `feat:`.

Versión del juego: **0.13.3 → 0.13.4** (se sube en el commit de la última etapa).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- La cabina deja de ser líneas 3D: pasa a ser una **capa 2D** que se dibuja **encima de la vista 3D**, traducida de los archivos de diseño.
- Cada nave tiene la suya. Vector cambia de cabina (es un **cambio visual buscado**).
- En la vista de cabina, la **velocidad, el boost y el daño** salen en el cuadro de mando de cada nave, con su disposición propia. El HUD de pantalla de esos tres datos se oculta en cabina.
- **Posición, vuelta y tiempos no cambian**: siguen en las esquinas, igual que ahora, en las dos cámaras.
- La **cámara exterior no cambia**: sin capa de cabina y con el HUD de pantalla de siempre.
- Entra la **luz** del diseño: tinte naranja en los bordes con el boost, tinte rojo con daño fuerte y brillo de las pantallas según la velocidad. Todo con su intensidad en el Inspector.
- Cada nave tiene su **ojo de cámara de cabina**.
- Se hace en **tres etapas** (sección 10): base + Vector, luego Hoja y luego Martillo. Validación y commit tras cada una.

## 2. Antes de empezar: inventario

Antes de tocar nada, el agente mira cómo se comporta **hoy** la cabina y apunta en un **informe corto**:

1. **Cómo se mueve el marco en pantalla** con la inclinación de cabina, la vibración y el empuje del boost (spec `camara`). Por ejemplo: si el marco va en el mismo pivote que la cámara, en pantalla se queda quieto y lo que se ladea es el mundo.
2. **Cómo se mueve el HUD de pantalla en cabina** con la inclinación (si hoy rota o no).
3. Si desde la cabina se ve **alguna parte del casco 3D** de la nave.
4. Qué **fuente** usa el HUD y si `Share Tech Mono` está en el proyecto.
5. Qué **modo de escalado** usa el proyecto (stretch mode y aspect) y qué pasa hoy con pantallas que no son 16:9.
6. Dónde está hoy el **ojo de la cámara de cabina** de Vector y en qué altura de la pantalla cae el **punto de fuga** de la pista en una recta.

Con eso:
- La capa nueva se mueve en pantalla **exactamente igual que se mueve hoy el marco** (si hoy no se mueve, tampoco).
- El HUD de las esquinas se queda **como está**.
- Si algo del punto 3 se ve hoy, **se avisa antes** de ocultarlo.
- **Si hay algo dudoso, se pregunta antes de moverlo.**

## 3. La capa de cabina

### 3.1 Espacio de referencia

- Los diseños están en **1280 × 720 px**, con el origen arriba a la izquierda, X a la derecha e Y hacia abajo (como el SVG).
- En el juego, la capa se dibuja a **× 1,5** sobre el tamaño base del proyecto (1920 × 1080).
- El proyecto usa `canvas_items` con aspecto `keep`: en pantallas que no son 16:9, Godot pone **bandas negras**, también sobre la vista 3D. Por eso la capa no calcula franjas, recortes ni centrado.

### 3.2 Qué se dibuja y qué no

- Se dibuja todo lo que construye `svg()` de cada cabina, **en el mismo orden** (lo de delante tapa a lo de detrás, como en el SVG), más los **reflejos del cristal** (`glass()`) y los **tintes** (`tints()`, sección 5).
- **No** se dibuja el fondo del diseño (`scene()`: cielo, torres y pista): en el juego eso es el mundo 3D. Todo lo que no es panel queda **transparente** y se ve la carrera.
- **No** se dibujan los textos de esquina del diseño (`1 / 8`, `LAP 2/3`, tiempos): son el HUD de siempre, que no cambia.
- Los paneles son **opacos**: no dejan ver el mundo a través.
- La capa va **por debajo del HUD de las esquinas**.

### 3.3 Traducción

- Se traduce a C# **tal cual**: los mismos puntos, el mismo orden, los mismos colores, opacidades, grosores y degradados. **Sin redondear ni "mejorar" coordenadas.** Los bucles y fórmulas del archivo (curvas de Bézier, `inset`, `mirrorX`, morros por niveles…) se traducen como código, no como listas de puntos copiadas.
- Las piezas comunes de `designs/lib.js` (`panel`, `pillar`, `screw`, `vent`, `switches`, `seam`, `brackets`, `screen`, `micro`, `label`…) van en una **clase pura reutilizable**. Las piezas propias de cada cabina (`wideNoseN`, `hojaLongNose`, `ventOut`, `martilloNose`, `ventArmor`, `pillarOne`, `weather`, `sinkH`…) van con su cabina.
- El desgaste de Martillo (`weather`) usa un generador con semilla fija (11, 23, 37 y 51). Se traduce **el mismo generador**, para que salga idéntico.
- **Degradados**: se dibujan sobre la caja de cada polígono, como hace el SVG por defecto (de 0 a 1 en su ancho o alto). Vale una textura de degradado con UV de esa caja o cualquier método que dé lo mismo. Los degradados de varias paradas (`pTop`, `cowl`…) tienen que respetar todas sus paradas.
- **Líneas de barrido** de las pantallas (`scan`): una línea de 1 px cada 3 px, cian al 8 % y con la opacidad de la pantalla (50 %).
- **Halo** (`glowSoft`): se puede aproximar con una copia más gruesa y tenue del mismo trazo. **Sin shaders nuevos** salvo que sea lo más sencillo; si hace falta uno, se avisa.
- **Textos**: con `Share Tech Mono`, el tamaño, el espaciado entre letras, el anclaje (izquierda, centro, derecha) y la opacidad del archivo. Si la fuente no está en el proyecto, se añade (licencia OFL) en la carpeta de fuentes.
- **Grupos girados** (las dos pantallas de Vector a ±8°) y **desplazados** (todo el cuadro de mando subido 34 px con `translate(0 -34)`): se respeta la transformación del archivo.

### 3.4 Captura de referencia

Igual que con los cascos:
- Antes de traducir, el agente ejecuta los archivos de diseño con Node (creando la carpeta `project/` de salida) y guarda en las pruebas una **captura** de cada cabina: todos sus elementos en orden, con tipo, puntos, colores, opacidades, grosores y textos. Las partes vivas (`{{…}}`) quedan marcadas como **huecos**, sin valor.
- Una prueba compara la cabina generada en C# con su captura, **elemento a elemento**. El archivo redondea los puntos a un decimal, así que la tolerancia es **0,06 px**.
- Si al traducir sale otro número de elementos, **algo se ha traducido mal**.
- La **prueba de captura de la cabina de Vector de la spec 1** (líneas 3D) se **retira**: esa cabina ya no existe. La sustituye la captura nueva. La del **casco** de Vector sigue igual.

### 3.5 La cabina vieja

- El constructor de la cabina 3D de Vector y lo que solo servía para ella se **quitan** (no se deja código muerto).
- `Cockpit` sigue siendo el nodo de la cabina: recibe la cabina del perfil visual y dibuja la capa.
- Si quitar algo rompe otra cosa (otra escena, una prueba, una herramienta), **se avisa antes**.

## 4. HUD en el cuadro de mando

### 4.1 Qué se ve en cada cámara

| | Cabina | Exterior |
|---|---|---|
| Capa de cabina | Sí | No |
| Velocidad, boost y daño | En el cuadro de mando | HUD de pantalla de siempre |
| Posición, vuelta y tiempos | Esquinas, como ahora | Esquinas, como ahora |

- Al cambiar con **C**, se cambia al momento, sin transición.
- El panel de pruebas (F10) y la pausa se ven encima, como ahora.

### 4.2 Estado del HUD

Un **estado del HUD de cabina** común a las tres naves, calculado a partir de lo que ya da el juego: velocidad (km/h, la misma que enseña el HUD de pantalla), punta base de la nave, boost activo, cargas, cooldown, nivel de daño y modo (RACE o TIME TRIAL). Sale de `script()` de `designs/lib.js`:

- **Escala de velocidad**: `TOP` = punta **base** de la nave en km/h (sin daño, para que la marca naranja no se mueva) y `MAX` = `TOP` + 100 (el impulso del boost). En el diseño son 396 y 496, los de Vector. Fracción `f` = velocidad / `MAX`, con tope en 1.
- **Color de la velocidad**: naranja si va por encima de `TOP` o con el boost activo; cian si no.
- **Cargas de boost** (3 esferas o luces), con prioridad en este orden:
  1. Con carga y daño 3 → **bloqueada**: rojo, relleno 30 %, borde 55 %, sin halo.
  2. Con carga y cooldown → naranja, relleno 30 %, borde 55 %, sin halo.
  3. Con carga → naranja lleno, con halo.
  4. Vacía → azul, relleno 25 %, borde 70 %.
- **Texto del boost**: `LOCKED` (daño 3 y alguna carga), `ACTIVE` (boost activo), `COOLDOWN` (cooldown y alguna carga), `READY` (alguna carga) o `CHARGING` (ninguna).
- **Daño** (3 segmentos o placas): se encienden tantos como el nivel, todos del color del nivel (verde 0, amarillo 1, rojo 2 y 3). Los apagados, solo el borde al 60 %. Con nivel 3 parpadean (100 % / 25 %, a 2 Hz, como el indicador de la spec `danos`).
- **Texto del daño**: `NOMINAL`, `LIGHT`, `HEAVY` o `CRITICAL`, del color del nivel.
- **En TIME TRIAL no hay daños**: los segmentos y placas se quedan apagados con el borde azul tenue, sin texto del daño, y nunca sale el tinte rojo. Las rotulaciones fijas (`HULL STATUS`, `INTEGRITY`…) se quedan, porque son parte del panel.
- **Barra de velocidad**: se rellena `ancho × f`. Lleva la marca naranja en `TOP`.
- **Transiciones**: los cambios de color y opacidad del HUD se suavizan en **0,25 s**, como en el diseño.

Nota: en el lienzo, la barra de velocidad de Hoja no se rellena por un fallo del archivo (el hueco `bar260` no se calcula). En el juego **se rellena** con la misma regla que la de Vector.

### 4.3 Disposición de cada nave

Las posiciones, tamaños y formas salen de cada archivo. Resumen:

| Nave | Daño | Velocidad | Boost |
|---|---|---|---|
| **Vector · Doble pantalla** | Pantalla izquierda (girada +8°): `HULL STATUS`, 3 segmentos, texto del daño | Pantalla derecha (girada −8°): número grande, `KM/H` y barra | Pantalla derecha: texto del boost y 3 esferas |
| **Hoja · Burbuja** | Pantalla larga central, a la izquierda: `HULL`, 3 segmentos, texto del daño | Pantalla larga, en el centro: número, `KM/H` y barra | Pantalla larga, a la derecha: texto del boost y 3 esferas |
| **Martillo · Búnker** | Panel izquierdo: 3 placas atornilladas y texto del daño | **Dial central** con arco y aguja, y número debajo | Panel derecho: 3 luces bajo tapas de seguridad y texto del boost |

**Dial de Martillo** (`gauge`: centro (640, 672), radio 48, de 150° a 390°):
- Arco de fondo azul, tramo rojo desde `TOP` hasta el final y arco de velocidad con el color de la velocidad hasta `f`.
- 11 marcas y los números 0, 100, 200, 300 y 400 colocados según `MAX` (en Vector caen donde el diseño; en Martillo, con su propia punta).
- La aguja apunta al ángulo de `f`.

### 4.4 Parpadeo de daños en la cabina

- La spec `danos` hace parpadear **aristas de la cabina** desde el nivel 1. Ahora las aristas son los **trazos de la capa** (bordes de paneles, pilares y líneas), no los rellenos ni el HUD.
- Mismas reglas y mismos valores del Inspector de siempre (proporción de aristas, intervalos, duración y opacidad, hoy 0,1), sobre el número de trazos de cada cabina. Se comprueba que funciona con las tres.

## 5. Luz

Sale de `script()` y `tints()` de `designs/lib.js`. **Solo en la vista de cabina.**

| Efecto | Cuándo | Valor del diseño |
|---|---|---|
| **Tinte naranja** en los bordes | Boost activo / por encima de `TOP` sin boost | 0,55 / 0,25 de opacidad; entra y sale en 0,3 s |
| **Tinte rojo** en los bordes | Daño 2 / daño 3 | 0,15 fijo / late entre 0,45 y 0,20 cada 1 s |
| **Brillo de las pantallas** (halo detrás de cada pantalla y del dial) | Siempre | 0,35 + 0,35 × `f` |

- Los tintes son degradados radiales que solo tiñen los **bordes** de la pantalla (el centro queda limpio). No es un destello de pantalla completa.
- Cada efecto tiene su **intensidad** en el Inspector, que multiplica el valor del diseño. A 0, se apaga.
- La pausa congela el latido del tinte rojo.

## 6. Ojo de la cámara de cabina

- El ojo pasa a ser **de cada nave** en su perfil visual (ya lo era en la definición; hasta ahora Hoja y Martillo usaban el de Vector).
- Los tres diseños ponen el **punto de fuga de la pista en y ≈ 300** (de 720). El cuadro de mando está pensado para esa vista.
- El punto de fuga depende de la **inclinación** de la cámara, no de la altura del ojo. Con la cámara recta cae en el centro (y = 360). Por eso el encuadre de cabina de cada nave lleva una **inclinación de −8°** (hacia abajo), que lo deja en y ≈ 300 con el FOV 80. Decisión del usuario.
- **Vector**: la posición de su ojo no cambia; solo se añade la inclinación.
- **Hoja y Martillo**: valores de arranque, que se ajustan probando:
  - Altura del ojo: la de Vector × la altura de la nave respecto a Vector (Hoja × 0,62, Martillo × 1,40).
  - Posición adelante/atrás: la de Vector.
  - Inclinación: −8°, como Vector.
- Los efectos de la spec `camara` (inclinación, vibración, FOV y empuje del boost) se siguen sumando encima.

## 7. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Intensidad del tinte del boost | 1 | Cabina (común) |
| Intensidad del tinte de daño | 1 | Cabina (común) |
| Intensidad del brillo de las pantallas | 1 | Cabina (común) |
| Ojo e inclinación de la cámara de cabina | Vector: el ojo actual · Hoja y Martillo: sección 6 · inclinación −8° | Perfil visual |

- El resto (tiempos de transición, parpadeo y latido) son los del diseño y van como constantes con nombre.

## 8. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Primitivas 2D** (puro, `System.Numerics`): polígono (relleno de color o degradado, trazo, grosor, opacidad), polilínea (color, grosor, opacidad, discontinua), círculo, elipse, rectángulo y texto (posición, tamaño, espaciado, anclaje, color, opacidad), con grupos girados o desplazados. Degradados lineales y radiales con sus paradas. **Sin nodos.**
- **Piezas de cabina** (puro): traducción de las piezas comunes de `designs/lib.js`.
- **Cabinas de Vector, Hoja y Martillo** (puros): su parte fija y la **disposición de su HUD** (dónde va cada pieza viva).
- **Estado del HUD de cabina** (puro): la sección 4.2 y los valores de luz de la sección 5, a partir del estado del juego. Con `Update(delta)` para transiciones, parpadeo y latido.
- **Piezas vivas** (puro): esferas, luces con tapa, segmentos, placas, barra, número y dial, que dan primitivas a partir del estado.
- **`Cockpit`** (Godot): dibuja la parte fija **una vez** (en caché) y la viva cada frame, en el orden del diseño: lo vivo puede quedar entre piezas fijas (el halo de las pantallas), así que se dibuja por tramos seguidos. Sin reglas dentro.
- **`Hud`**: oculta velocidad, boost y daño en la vista de cabina. Nada más cambia.
- **Perfiles visuales**: la cabina, el ojo y la inclinación de cada nave.
- **`Player` y `TuningLabPanel`**: F10 pasa a cambiar también la cabina y el ojo, a partir de la etapa 2 (hasta entonces las tres naves tienen los mismos). Corrige lo que decía la spec 3.
- **Colores**: los colores nuevos del diseño van en `Palette`, como todos.

### Pruebas unitarias mínimas

1. La cabina de Vector coincide con su captura (elemento a elemento, ±0,06 px).
2. La cabina de Hoja coincide con su captura.
3. La cabina de Martillo coincide con su captura, desgaste incluido.
4. El perfil visual de cada nave da su cabina: Vector → Doble pantalla, Hoja → Burbuja, Martillo → Búnker.
5. Estado del HUD:
   - Color de la velocidad: cian por debajo de `TOP`, naranja por encima y con boost.
   - `f` con `TOP` 396 → `MAX` 496; con velocidad 0, 248 y 600 → 0, 0,5 y 1.
   - Las cuatro formas de las cargas, con su prioridad (bloqueada antes que cooldown).
   - Los cinco textos del boost.
   - Segmentos y textos del daño de 0 a 3; parpadeo solo en 3.
   - En TIME TRIAL: daño apagado, sin texto y sin tinte rojo.
6. Dial de Martillo: con `f` 0, `TOP`/`MAX` y 1, la aguja va a 150°, a su ángulo y a 390°.
7. Barra de velocidad: ancho × `f`, también la de Hoja.
8. Luz: valores de la tabla de la sección 5, multiplicados por su intensidad; a 0 se apagan.
9. Inclinación: el encuadre de cabina de las tres naves lleva −8°.
10. Qué se ve en cada cámara (tabla 4.1).
11. El parpadeo de daños elige aristas válidas con el número de trazos de cada cabina.

Las pruebas existentes siguen pasando, salvo la captura de la cabina 3D de Vector, que se retira (sección 3.4).

## 9. Prueba manual

Se hace la parte de cada etapa al cerrarla (sección 10).

**Vector (etapa 1)**
1. RACE con Vector, cabina: se ve la Doble pantalla. Comparar con el lienzo, en ventana y a pantalla completa.
2. El mundo se ve por el cristal; los paneles no transparentan.
3. Acelerar: el número, la barra y el brillo de las pantallas suben. Por encima de la punta, en naranja.
4. Boost: tinte naranja en los bordes, `ACTIVE`, luego `COOLDOWN` con las esferas atenuadas.
5. Dañar hasta nivel 3: segmentos y texto del daño, tinte rojo latiendo, `LOCKED` con las esferas en rojo y aristas de la cabina titilando.
6. Curvas, vibración cerca de la punta y boost: la cabina se mueve en pantalla igual que antes.
7. C: cámara exterior sin cabina y con el HUD de pantalla de siempre. Volver con C.
8. Posición, vuelta y tiempos: en las esquinas, igual que antes, en las dos cámaras.
9. TIME TRIAL: daño apagado y nunca tinte rojo.
10. Pausa con boost y daño 3: todo congelado.
11. Ventana que no sea 16:9: bandas negras, con la cabina y la vista 3D igual de encajadas.

**Hoja (etapa 2)**
12. F10 → Hoja, cabina: se ve la Burbuja, con el morro largo. Comparar con el lienzo.
13. La barra de velocidad de la pantalla larga se rellena.
14. Punto de fuga cerca de y ≈ 300 y la vista se ve bien (más baja que Vector).
15. Repetir 3, 4, 5, 7 y 9 con Hoja.

**Martillo (etapa 3)**
16. F10 → Martillo, cabina: se ve el Búnker, con el canard delante. Comparar con el lienzo.
17. Dial: la aguja sigue la velocidad y entra en el tramo rojo por encima de la punta.
18. Boost en las tres luces con tapa y daño en las tres placas.
19. Punto de fuga cerca de y ≈ 300.
20. Repetir 3, 4, 5, 7 y 9 con Martillo.
21. Menú principal: la versión es 0.13.4.

## 10. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Base y Vector** | Inventario (sección 2), primitivas, piezas comunes, dibujo de la capa, inclinación de la cámara, estado del HUD, piezas vivas comunes, luz, parpadeo de aristas, ocultar el HUD de pantalla en cabina, quitar la cabina 3D y la cabina de Vector | Pruebas 1, 4, 5, 7–11 · Prueba manual 1–11 · Commit |
| **2 · Hoja** | Cabina de Hoja y su ojo | Pruebas 2, 7 y 11 · Prueba manual 12–15 · Commit |
| **3 · Martillo** | Cabina de Martillo (dial incluido) y su ojo · versión 0.13.4 | Pruebas 3, 6 y 11 · Prueba manual 16–21 · Commit |

- Mientras no esté su etapa, Hoja y Martillo usan la cabina nueva de **Vector** (y su ojo), como hasta ahora.
- No se empieza una etapa sin validar la anterior.

## 11. Fuera de alcance

- Desbloquear las naves en el selector, el holograma de Hoja y Martillo y el fantasma con ellas (spec 4).
- Cambios en el HUD de las esquinas (posición, vuelta y tiempos) o en el HUD de pantalla de la cámara exterior.
- Cambios en el diseño de las cabinas: se traducen como están.
- Las demás opciones de cabina del lienzo (se quedan solo como diseño guardado).
- Cabina en 3D, interior con profundidad real o animaciones de piezas (interruptores, tapas…).
- Menú SETTINGS (las intensidades de luz quedan listas para él, como los ajustes de cámara).
- Adaptación a Android y a pantallas táctiles.
- Sonido.
