# VECTORY — Spec: Cabinas 3D · 1 · Vector (piloto Blender → Godot)

Versión 1.3 · 2026-11-06 · Estado: **hecha y validada por el usuario** (versión del juego 0.14.4)

Cambios respecto a la 1.2, al cerrar: valores finales de las luces (sección 7) y lo que se aparta de la spec en la etapa 3 (sección 13).

Cambios de la 1.2 respecto a la 1.1: carpeta `designs/` y tecla F6 corregidas; el movimiento de la cámara de cabina dice de qué datos del juego sale (4.4); el encuadre del mundo pasa a ser una decisión con capturas en la etapa 2 (4.5); criterio de rendimiento medible (4.8); la vista 3D no se rehace con F10 (5); la fuente se descarga con permiso (6.2); y se apuntan los tests y documentos que cambian al cerrar (6.7 y 11).

Cambios de la 1.1 respecto a la 1.0: la cabina gana **dos pantallas pequeñas** para el **radar** y el **minimapa** (colocación «junto a pantallas»), la **consola central se estrecha** para dejarles sitio y el prototipo de referencia pasa a ser `designs/cabina-3d-vector-radar.html`. Esta spec va **después** de `radar-minimapa`.

Primera spec de las **cabinas en 3D**. Hace la cabina de **VECTOR** en estilo **CARRERAS** (carbono, aluminio y halo) y, con ella, **valida toda la cadena**: script de Blender → exportar → meterla en Godot con **su propia cámara** → **HUD vivo en las pantallas del salpicadero**, incluidos el **radar** y el **minimapa** de la spec `radar-minimapa`.

Es un piloto: lo que se aprenda aquí (nombres, carpetas, materiales, cámara) se repite después con BLADE, HAMMER y los cascos exteriores.

Plan de las specs de cabinas 3D:

1. **Vector · carreras** (esta): cadena completa con una cabina.
2. BLADE (estilo por decidir, p. ej. militar).
3. HAMMER (estilo por decidir, p. ej. industrial).
4. Después: cascos exteriores realistas de las tres naves.

Diseño de referencia:
- **`designs/cabina-3d-vector-radar.html`**: el prototipo. Es la variante **«3 · CARRERAS»** de la cabina 3D de prueba, sin los otros estilos, con el radar y el minimapa. Se abre en el navegador y se puede conducir. **Solo vale la colocación 1 · JUNTO A PANTALLAS, sin intercambiar** (la que sale al abrirlo): radar a la **izquierda**, junto a HULL STATUS, y minimapa a la **derecha**, junto a la velocidad. La colocación 2 (consola) está descartada: su código no se traduce.
  - **Va a Blender** todo lo que construye `buildCockpit()` y las texturas (`carbonSet`, `brushedSet`, `fabricSet`, `smudgeTex`) y el entorno de reflejos.
  - **Va al código del juego** la lógica viva: HUD de las pantallas (`hudState`, `drawLeft`, `drawRight`, `screenBase`, `drawRadarScreen`, `drawMapScreen`), las shift lights, el parpadeo con daño, las luces de boost y daño y el movimiento de la cámara de cabina. La **lógica** del radar y el minimapa (barrido, ecos, proyección, ventana) **no** se copia del prototipo: es la de la spec `radar-minimapa`, que ya está en el juego.
  - **No va a ningún sitio**: el mundo (pista, torres y rivales simulados), el circuito simulado del minimapa, la conducción simulada, los botones y la mirada con el ratón. El juego tiene los suyos.
  - Los comentarios **`→ Nombre`** del archivo dicen cómo se llama cada pieza en Blender.

Si algo no cuadra entre esta spec y el prototipo, **manda el prototipo** y se avisa.

Rama: `feature/cockpit-3d-vector`, que sale de `develop` con `feature/radar-minimap` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.3 → 0.14.4** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. **Esta spec incluye el cambio de dirección visual** del documento maestro (sección 11).

---

## 1. Resumen

- **Etapa 1 · Blender**: un **script de Python** construye la cabina en Blender desde cero, con sus materiales y texturas, y la **exporta a `.glb`**. También guarda el `.blend`, unas **capturas de previsualización** y un **manifiesto** con los nombres de las piezas. En el juego no cambia nada.
- **Etapa 2 · Godot**: la cabina 3D entra en el juego para VECTOR, en la vista de cabina, con **su propia cámara** (no se deforma con el FOV del mundo), su luz y sus reflejos. Sustituye a la capa 2D de Vector. Las pantallas aún enseñan una **imagen de prueba**.
- **Etapa 3 · HUD y luces vivas**: las dos pantallas grandes enseñan **daño, boost y velocidad** y las dos pequeñas el **radar** y el **minimapa**, como en el prototipo; entran las **shift lights**, el **parpadeo con daño** y las **luces de boost y daño**. Se quita la cabina 2D de Vector.
- BLADE y HAMMER **no cambian**: siguen con su capa 2D de la spec 3b.
- Lo que **no cambia**: la cámara exterior (con su HUD de siempre, radar y minimapa incluidos), el HUD de las esquinas (posición, vuelta y tiempos), el casco exterior wireframe de Vector, la pista, el entorno y los efectos de velocidad.

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **Blender**: si `blender --version` responde y qué versión es. Si no está en el PATH, se pregunta la ruta al usuario (ver sección 3.1). Si el Python de Blender trae `numpy`.
2. **Godot**: versión exacta del motor.
3. **Ojo de la cámara de cabina de Vector**: altura sobre la pista y FOV base. El prototipo tiene el ojo a **1,45 m** sobre la pista y FOV vertical **70°**.
4. **Capa de cabina 2D** (spec 3b): en qué `CanvasLayer` va y en qué orden respecto al HUD de las esquinas.
5. **Efectos de cámara** (spec `camara`): de dónde salen hoy la inclinación en curva, la vibración cerca de la punta y el empuje del boost, para poder leerlos (sección 5.4).
6. **Glow y antialiasing** del juego: ajustes del `WorldEnvironment` principal y MSAA/FXAA/TAA del proyecto.
7. **Carpetas**: dónde van hoy los recursos (modelos, texturas, fuentes) para elegir dónde poner los nuevos.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · La cabina en Blender

### 3.1 Cómo se usa Blender

- Versión: **Blender 5.2 LTS** (o la que tenga instalada el usuario, si es 5.x; se apunta en el informe).
- El agente ejecuta Blender **sin ventana**, desde la terminal:

  ```
  blender -b --factory-startup --python art/blender/cockpit_vector.py -- --out <carpeta>
  ```

  Si `blender` no está en el PATH, se usa la ruta completa del `.exe` que diga el usuario, y se apunta en un sitio fijo del repo (p. ej. una variable en un script `art/blender/run.ps1`) para no preguntarla otra vez.
- **Todo vive en el script.** El `.blend` se **regenera** cada vez; nada se cambia a mano dentro de él. Si el usuario o el agente prueban algo en Blender abierto (a mano o con el MCP de Blender), el cambio bueno se pasa al script.
- El script es **determinista**: dos ejecuciones dan el mismo `.glb` (mismas semillas, mismo orden).

### 3.2 Archivos

| Archivo | Qué es | ¿Va a Git? |
|---|---|---|
| `art/blender/cockpit_vector.py` | Construye la cabina, exporta y guarda todo | Sí |
| `art/blender/lib/` | Piezas comunes (caja redondeada, viga, tubo, rejilla, polígono, tira, UV en metros, texturas) para reutilizarlas en BLADE y HAMMER | Sí |
| `assets/…/cockpit_vector.glb` | Modelo exportado, con las texturas dentro | Sí |
| `assets/…/cockpit_vector.manifest.json` | Nombres, tamaños y recuentos (sección 3.7) | Sí |
| `assets/…/cockpit_env.png` | Panorama de reflejos (sección 3.6) | Sí |
| `art/blender/out/cockpit_vector.blend` | Escena de Blender, para abrirla y mirarla | No (`.gitignore`) |
| `art/blender/out/previews/*.png` | Capturas de previsualización | No (`.gitignore`) |

La carpeta exacta de `assets/` la elige el agente siguiendo las que ya hay (punto 7 del inventario).

### 3.3 Ejes, unidades y origen

- Unidades: **metros**.
- El prototipo usa los **mismos ejes que Godot**: X derecha, Y arriba, el piloto mira hacia **−Z**.
- En Blender (Z arriba) se construye **todo en las coordenadas del prototipo** dentro de un objeto raíz **`CockpitVector`** girado **+90° en X**. Así un punto del prototipo `(x, y, z)` queda en Blender en `(x, −z, y)`, y al exportar a glTF (Y arriba) vuelve a ser `(x, y, z)` en Godot. Los números del prototipo se copian **tal cual**, sin convertirlos a mano.
- El **origen** de `CockpitVector` es el **ojo del piloto** `(0, 0, 0)`.
- Antes de exportar se comprueba con dos puntos: la punta del morro debe quedar en Godot en z ≈ −3,1 y el techo en y ≈ +0,45.

### 3.4 Geometría

Se traduce `buildCockpit()` **pieza a pieza**, con los mismos números y en el mismo orden. Las piezas comunes van en `art/blender/lib/`:

| Prototipo | En Blender |
|---|---|
| `B.poly(P)` | Polígono plano en abanico desde el primer punto, una cara por triángulo |
| `B.strip(A, B)` | Tira de cuadriláteros entre dos polilíneas |
| `B.rbox(w, h, d, …, r)` | Caja con las aristas redondeadas con radio `r` y **2 segmentos** (mismo radio por defecto que el prototipo). El modificador se **aplica** antes de exportar |
| `B.beam(p0, p1, …)` | Caja redondeada entre dos puntos, con la base que calcula el prototipo (`X`, `Y`, `Z` mirando hacia `toward`) |
| `B.tube(pts, r, …, seg)` | Tubo de **10 lados** por los puntos, siguiendo una **Catmull-Rom centrípeta** (la de three.js por defecto) muestreada en `seg` tramos. Se muestrea con la misma fórmula, no con las curvas de Blender |
| `B.vent(…)` | Rejilla con marco, fondo y lamas giradas 0,6 rad, orientada con `lookAt` como en el prototipo |
| `B.led(…)` / esferas | Esfera UV de 10 × 8 |
| Cilindros (mandos, botones) | Cilindro con los mismos radios, alto y lados |
| Grupos con `position`, `rotation` o `lookAt` | Objeto vacío padre con la misma transformación (rotaciones en el orden XYZ de three.js; `lookAt`, ver nota) |

**Pantallas pequeñas y consola** (cambios del prototipo respecto a la cabina carreras de la 1.0):
- `makeScreen` pasa a ser `makePanel(padre, posición, orientación, ancho, alto, fondo, …)`. Las dos pantallas grandes salen igual que antes.
- **Radar**: `makePanel` en **(−0,205, −0,265, −0,545)**, mirando a (0, 0,02, 0,12), de **0,09 × 0,09 m** y **0,035 m** de fondo, con su bisel y su línea de luz, como las grandes.
- **Minimapa**: igual, en **(+0,205, −0,265, −0,545)**.
- **Consola central**: **0,26 m** de ancho (antes 0,32), con los tres mandos donde estaban y los cuatro botones en x = −0,105, −0,035, 0,035 y 0,105.

Nota sobre `lookAt`: en three.js, `Object3D.lookAt` en un objeto que **no es cámara** gira el objeto para que su **+Z** apunte al punto. Se reproduce igual.

- **Caras por los dos lados**: el prototipo dibuja todo con `DoubleSide`. En el `.glb` los materiales van con **doble cara** (desactivar «Backface Culling»), salvo que el agente compruebe que una pieza solo se ve por un lado.
- **Sombreado**: plano en los polígonos y tiras (como el prototipo, que calcula normales por cara) y suave en tubos, esferas, cilindros y redondeos.
- Las piezas que el prototipo marca con `noShadow` **no proyectan sombra** (pantallas, LEDs, luces, manchas del cristal, techo). En glTF eso no existe: se apunta en el manifiesto y se aplica en Godot.

### 3.5 Materiales y texturas

- Solo materiales que el exportador glTF entiende: **Principled BSDF** con **texturas de imagen**. **Nada de nodos procedurales** sin pasar a imagen.
- Las texturas se generan **en Python** con el **mismo algoritmo y las mismas semillas** del prototipo (`mkRng`, `valueNoise`, `fbm`, `fromArrays` y los cuatro conjuntos), a **512 × 512**, y se guardan como PNG: color (sRGB), rugosidad y normal (no color). El generador `mkRng` es xorshift de 32 bits: hay que enmascarar a 32 bits sin signo en cada paso para que dé lo mismo que en JavaScript.
- **Normal**: el prototipo la escribe en convención OpenGL (Y hacia arriba), que es la de glTF. Se guarda igual.
- **Repetición**: el prototipo pone UV = **metros** según el eje dominante de la normal de cada cara (`boxUV`) y luego repite la textura. En Blender se calculan esas UV con la misma regla y se **multiplican por la repetición** del material (carbono ×20, aluminio ×2, tejido ×5, manchas ×2,2). Así no hace falta `KHR_texture_transform`.

| Material | Base | Metal | Rugosidad | Extra |
|---|---|---|---|---|
| `M_Carbon` | carbono | 0,25 | textura | Barniz 1, rugosidad del barniz 0,06 |
| `M_CarbonNose` | carbono | 0,25 | textura | Barniz 0,6 · reflejos ×0,35 (ver abajo) |
| `M_CarbonNoseSide` | carbono | 0,25 | textura | Barniz 1 · reflejos ×0,35 |
| `M_Alu` | aluminio claro (182, 188, 196), semilla 41 | 1 | textura | — |
| `M_AluDark` | aluminio oscuro (58, 62, 70), semilla 42 | 1 | textura | — |
| `M_Fabric` | tejido (25, 26, 29) | 0 | textura (0,96) | — |
| `M_Paint` | rojo `#B5121B` | 0,1 | 0,32 | Barniz 1, rugosidad del barniz 0,05 |
| `M_Dark` | `#060606` | 0 | 0,9 | — |
| `M_Emissive_*` | negro + emisión del color de cada luz | 0 | 0,4 | Una por color: `Accent` (`#FF2D2D`), `LedConsole` (`#FF8A00`), `ShiftGreen`, `ShiftRed`, `ShiftBlue` |
| `M_Screen` | negro (la textura la pone Godot) | 0 | 1 | — |
| `M_ScreenShade` | negro al 70 % | 0 | 1 | Transparente |
| `M_GlassSmudge` | textura de manchas | 0 | 1 | Godot la pone en modo **suma**, opacidad 0,012 |

- Reflejos ×0,35 (`envMapIntensity`) y el modo suma de las manchas no existen en glTF: se apuntan en el manifiesto y los aplica Godot.
- Cada pieza viva (sección 3.7) tiene su **propio material o instancia**, para que Godot pueda encenderla y apagarla sola.

### 3.6 Panorama de reflejos

El prototipo refleja una noche con la ciudad naranja en el horizonte y luces azules arriba (bloque «Entorno para reflejos»). El script lo **monta en una escena aparte** con los mismos planos, colores y posiciones y lo **renderiza como panorama equirectangular** de **1024 × 512** desde el origen → `cockpit_env.png`. Godot lo usa de cielo de la cabina (sección 4.2).

### 3.7 Nombres de las piezas (contrato con Godot)

Godot busca estas piezas **por nombre**. El script comprueba antes de exportar que **están todas** y, si falta una, **falla con un mensaje claro**.

| Nombre | Qué es | Cuántas |
|---|---|---|
| `CockpitVector` | Raíz (origen = ojo) | 1 |
| `Screen_L`, `Screen_R` | Superficie de cada pantalla: plano de **0,27 × 0,085 m**, UV **0–1 entero**, U hacia la derecha y V hacia arriba vistas por el piloto | 2 |
| `Screen_L_Shade`, `Screen_R_Shade` | Sombra interior del techo de cada pantalla | 2 |
| `Screen_Radar`, `Screen_Map` | Superficie de las pantallas pequeñas: plano de **0,09 × 0,09 m**, UV **0–1 entero**, igual orientación que las grandes. Radar a la izquierda, minimapa a la derecha | 2 |
| `Screen_Radar_Shade`, `Screen_Map_Shade` | Su sombra interior | 2 |
| `ShiftLight_00` … `ShiftLight_14` | LEDs de las shift lights, de **izquierda a derecha** | 15 |
| `ShiftLight_Base_00` … `_14` | Sus carcasas | 15 |
| `Accent_Visor` | Línea de luz bajo la visera | 1 |
| `Accent_Screen_L`, `Accent_Screen_R` | Línea de luz bajo cada pantalla grande | 2 |
| `Accent_Radar`, `Accent_Map` | Línea de luz bajo cada pantalla pequeña | 2 |
| `Led_Nose_L`, `Led_Nose_R` | LEDs de la punta del morro | 2 |
| `Led_Console_0` … `_3` | LEDs de los botones de la consola, de izquierda a derecha | 4 |
| `Glass_Smudge` | Manchas del cristal | 1 |

El resto de piezas se agrupa por zonas (`Nose`, `Dash`, `Console`, `Frame`, `Halo`, `Side_L`, `Side_R`), con nombres libres dentro.

**Manifiesto** (`cockpit_vector.manifest.json`), que escribe el script:
- Versión de Blender y fecha de exportación.
- Lista de las piezas vivas de la tabla, con su tamaño (las pantallas) y su color de emisión (las luces).
- Piezas sin sombra, materiales con reflejos ×0,35 y materiales en modo suma.
- Número de triángulos total y caja que ocupa la cabina (en coordenadas de Godot).

### 3.8 Exportación

- glTF binario (`.glb`), **+Y arriba**, con modificadores aplicados, normales, UV y texturas **dentro** del archivo. Sin cámaras ni luces (las pone Godot), sin animaciones.
- Se exporta solo lo que cuelga de `CockpitVector`.

### 3.9 Capturas de previsualización

El script renderiza en `art/blender/out/previews/`:
1. `eye.png`: desde el ojo, **FOV vertical 70°**, 1280 × 720, fondo oscuro, con una luz parecida a la del prototipo (sección 4.3). Es la que se compara con el prototipo.
2. `three_quarter.png`: desde atrás, arriba y a un lado, para ver el volumen.
3. `screens.png`: de cerca, a las dos pantallas y la consola.

Motor: **Eevee** si funciona sin ventana en el PC del usuario; si no, **Cycles** con pocas muestras. Se apunta cuál.

El agente **mira las capturas** antes de dar la etapa por cerrada y las compara con el prototipo abierto en el navegador.

## 4. Etapa 2 · La cabina en Godot

### 4.1 Importación

- El `.glb` se importa con los nombres tal cual, **sin colisiones**, sin LOD y sin luz horneada.
- Después de importar, un script de la cabina (en el juego, no en el importador) aplica lo que glTF no lleva (manifiesto): piezas **sin sombra**, reflejos ×0,35, **modo suma** sin escribir profundidad para `Glass_Smudge`, y comprueba que el **barniz** (clearcoat) ha llegado. Si algo no llega bien, se arregla **en Godot con su material**, sin tocar la geometría, y se avisa. Comprobado en la etapa 2: el barniz **no llega** (Godot no lee `KHR_materials_clearcoat`) y se pone por código; la textura de rugosidad, que es gris, se leería también como metal, así que el metal queda como un valor por material.

### 4.2 Cámara propia

La cabina se pinta **encima del mundo** con **su propia cámara**, como el prototipo:

- Un `SubViewport` con **su propio mundo 3D** (`own_world_3d`), **fondo transparente** y del **tamaño de la pantalla** (sigue los cambios de tamaño).
- Dentro: la cabina, su `Camera3D` en el origen con **FOV vertical 70°** (no cambia con los efectos de FOV del mundo), plano cercano **0,02** y lejano **30**, sus luces (sección 4.3) y su `Environment`:
  - Cielo con el panorama `cockpit_env.png`, usado para **reflejos** y luz ambiente.
  - Tonemap **ACES**, exposición **1,05** del prototipo. En Godot son 1,05 / 0,6 = 1,75, porque el ACES de three.js divide la exposición entre 0,6 y el de Godot no.
  - **Glow** parecido al bloom del prototipo (fuerza 0,55, radio 0,45, umbral 0,8), para que brillen pantallas y LEDs.
- La imagen del `SubViewport` se enseña a pantalla completa **en el sitio de la capa 2D de cabina**: por debajo del HUD de las esquinas, el panel de pruebas y la pausa.
- Con la **cámara exterior** (C), el `SubViewport` **deja de dibujarse** (no solo se oculta), para no gastar.
- MSAA del `SubViewport`: el mismo que el del juego.

Si el glow con fondo transparente deja halos raros en el borde de las piezas, o el tonemap se nota doble, **se avisa con capturas** y se propone el arreglo más sencillo antes de hacerlo.

### 4.3 Luces de la cabina

Valores del prototipo como **punto de partida**. Las unidades de three.js no son las de Godot: se ajustan **a ojo comparando con el prototipo** y se apuntan los valores finales.

| Luz | Tipo | Posición | Color · Intensidad en el prototipo | Notas |
|---|---|---|---|---|
| Principal | Direccional | desde (0,7, 2,2, −1,6) hacia (0, −0,3, −0,4) | blanca · 2,6 | Con sombras, suaves |
| Ambiente | Del cielo + color | — | hemisferio cielo `#B0B8C8` / suelo `#101010` · 0,6; ambiente `#1C1C22` · 0,45 | Lo más parecido con el `Environment` |
| Lámpara | Omni | (0, 0,32, −0,25) | blanca · 0,08 · alcance 1,6 | — |
| Pantallas grandes (×2) | Omni | 6 cm delante de cada pantalla | blanca · `brillo × 0,3` · alcance 0,9 | `brillo` = 0,35 + 0,35 × `f` (sección 6.1) |
| Pantallas pequeñas (×2) | Omni | 6 cm delante de cada pantalla | blanca · `brillo × 0,2` · alcance 0,9 | — |
| Boost delantera | Omni | (0, 0,25, 0,45) | naranja `#FF9D32` · `tinte × 3,2` · alcance 3,5 | Etapa 3 |
| Boost trasera | Omni | (0, −0,2, −1,8) | naranja · `tinte × 2,0` · alcance 4 | Etapa 3 |
| Daño | Omni | (0, 0,1, −0,35) | rojo `#FF4D5E` · `daño × 3,0` · alcance 2,5 | Etapa 3 |

### 4.4 Movimiento de la cámara de cabina

La **cámara del mundo no cambia**: sigue con todos los efectos de la spec `camara`. La cámara de cabina se mueve un poco **por su cuenta**, como en el prototipo, para dar la sensación de ir a bordo:

| Efecto | Fórmula del prototipo | Parámetro (Inspector) |
|---|---|---|
| Balanceo lateral | x += `dirección × 0,03 × (0,3 + 0,7 f)` | 0,03 |
| Vibración | posición ± `ruido × vib × 1,6`; giro ± `ruido × vib × 0,35` | 1,6 y 0,35 |
| Contragiro en curva | roll = `−inclinación × 0,25` | 0,25 |
| Empuje del boost | z += `0,05 × empuje` | 0,05 |

De dónde sale cada dato. Se **leen** de lo que ya calcula el juego, sin duplicar esa lógica, pero **sin usar sus metros**: los efectos de la cámara del mundo ya llevan su amplitud y aplicarles estos factores la multiplicaría dos veces (la vibración de cabina del juego llega a 4,5 cm; la del prototipo, a menos de 1 cm).

| Dato | Viene de | Rango |
|---|---|---|
| `dirección` | `−ShipDynamics.Steer`: el del juego es positivo a la izquierda y el del prototipo, a la derecha | −1 a 1 |
| `f` | La de la sección 6.1 | 0 a 1 |
| `inclinación` | `CockpitBank.Angle` (sin el peralte de la pista). El signo se comprueba en pista: la cabina gira **al revés** que el mundo | hasta ±8° |
| `empuje` | `BoostCameraEffect.Eased`, **no** `Push` | 0 a 1 |
| `vib` | `CameraVibration.Intensity × 0,0025 + empuje × 0,003`, constantes del prototipo. No `Offset` ni `Amplitude` | 0 a ~0,007 |
| `ruido(t, a)` | `sin(37,1 t + a) × 0,6 + sin(61,7 t + 2,3 a) × 0,4`, del prototipo, con `a` = 7 y 9 en posición y 4 y 1 en giro; `t` = `CameraVibration.Time`, que la pausa congela | −1 a 1 |

- Diferencias con el prototipo, aceptadas: con boost, `Intensity` llega a 1,5 (el prototipo, a 1), y el contragiro llega a 2° (el prototipo, a 1,4°), porque el juego se inclina más. Se ajustan con sus parámetros si marean.
- Con los cuatro parámetros a 0, la cabina queda **quieta** en pantalla.

### 4.5 Ojo y encuadre

La cámara del mundo de Vector y la del prototipo **no coinciden**:

| | Juego (`VectorShip`) | Prototipo |
|---|---|---|
| Altura del ojo sobre la pista | ~2,45 m (0,45 m + 2 m de vuelo) | 1,45 m |
| FOV vertical | 80° | 70° |
| Inclinación | −8° | 0° |
| Horizonte en pantalla (720 px) | y ≈ 300 | y = 360 |

Los −8° se pusieron para cuadrar con el dibujo de la cabina 2D, que en Vector desaparece, así que esa razón ya no vale. Por eso, **decisión del usuario en la etapa 2**:

- Se empieza con la cámara del mundo **sin cambios**.
- El agente enseña **capturas de las dos opciones** en la misma recta y en una curva: A) la cámara actual; B) la del prototipo, con el ojo a 1,45 m de la pista, FOV 70° e inclinación 0°.
- **Elige el usuario**, también después de conducirlas si quiere. Con B cambian la sensación de velocidad y lo que se ve de la pista. Solo afecta a la cabina de VECTOR, no a BLADE, HAMMER ni la cámara exterior.
- Hasta entonces no se toca nada de esa cámara. Cualquier cosa intermedia se apunta con sus valores finales.

**Decidido en la etapa 2: opción B.** VECTOR va con el ojo a 1,45 m de la pista (−0,55 m respecto al `Player`), FOV 70° e inclinación 0°. BLADE y HAMMER siguen con el encuadre para el que están dibujadas sus cabinas 2D (`CockpitFraming.For2DCockpit`), con su altura de ojo. Límite conocido: desde los puestos de delante, el semáforo puede salirse por arriba.

### 4.6 Pantallas de prueba

En esta etapa cada pantalla enseña una **imagen de prueba** generada por código (sin archivos): fondo `#050505`, las líneas de barrido y las esquinas rojas de `screenBase`, y el texto `L`, `R`, `RADAR` o `MAP` grande en el centro. Sirve para comprobar que la **orientación** (no sale al revés ni en espejo), el **tamaño** y el **brillo** son buenos.

- Las grandes son un `SubViewport` de **640 × 200** cuya imagen va en el material de `Screen_L` / `Screen_R`; las pequeñas, de **256 × 256**, en `Screen_Radar` / `Screen_Map`. Todas **sin luz** (unshaded), con brillo **×1,35** como el prototipo, sin sombras.

### 4.7 Qué se ve en cada caso

| | VECTOR · cabina | BLADE / HAMMER · cabina | Cualquier nave · exterior |
|---|---|---|---|
| Cabina 3D | Sí | No | No |
| Capa de cabina 2D (spec 3b) | No | Sí, como ahora | No |
| Velocidad, boost y daño | En las pantallas 3D | En su cuadro de mando 2D | HUD de pantalla de siempre |
| Radar y minimapa | En las pantallas pequeñas 3D | HUD de pantalla (spec `radar-minimapa`) | HUD de pantalla |
| Posición, vuelta y tiempos | Esquinas | Esquinas | Esquinas |

En la etapa 2, mientras las pantallas son de prueba, VECTOR en cabina enseña **también el HUD de pantalla** de velocidad, boost, daño, radar y minimapa, para poder conducir y probar. Se quita en la etapa 3.

### 4.8 Rendimiento

Que la cabina no dé tirones es **muy importante** (ver «Rendimiento de las cabinas» en `CLAUDE.md`). Los FPS medios no bastan.

- **Cómo se mide**: en cabina con VECTOR, conduciendo con daño 3 y 15 s por caso, con vsync a 144 Hz. Se apuntan el **peor frame** y los **frames de más de 16,7 ms**, medidos con `Time.get_ticks_usec` y sin capturas dentro de la ventana medida. Aparte, sin vsync, el coste medio por frame.
- **Cuándo**: la referencia, con la cabina 2D, antes de empezar la etapa 2; después, al cerrar las etapas 2 y 3.
- **Objetivo**: los mismos frames lentos que con la cabina 2D, o casi.
- **Si no se llega**, se avisa con los datos antes de cerrar la etapa y se propone qué recortar, de lo que menos se ve a lo que más: resolución de sombras, glow, MSAA y frecuencia de redibujado de las pantallas pequeñas.

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Perfil visual de nave**: la cabina pasa a ser **2D** (la de la spec 3b) o **3D** (ruta del `.glb` y su estilo). VECTOR → 3D carreras; BLADE y HAMMER → 2D.
- **`Cockpit3DView`** (Godot): monta el `SubViewport`, la cámara, el `Environment`, las luces y el modelo, aplica lo del manifiesto (sección 4.1) y busca las piezas vivas por nombre. Si falta una, error claro en la consola. **Sin reglas dentro**: recibe estados y los pinta.
- **`Cockpit`** sigue siendo el nodo de la cabina: según el perfil, crea la capa 2D o `Cockpit3DView`. Al cambiar de nave con F10, la vista 3D **no se destruye**: se queda creada y parada, sin dibujarse, para no dar un tirón al volver. Se crea la primera vez que hace falta (la primera entrada a VECTOR con F10 puede tardar: es solo de depuración).
- **Movimiento de la cámara de cabina** (puro): de los datos de cámara del juego y los parámetros a posición y giro (sección 4.4).
- **Paleta del estilo carreras** (pura): los colores de `STYLES.racing.screen` y de las luces, **centralizados** como el resto de colores del juego (junto a `Palette` o en un perfil de estilo). Nada de colores escritos a mano en los scripts. Esta paleta **no usa** los colores del juego: es propia del estilo.
- **Lector del manifiesto** (puro): lee el JSON y da la lista de piezas y sus ajustes.

## 6. Etapa 3 · HUD y luces vivas

### 6.1 Estado

Se **reutiliza el estado del HUD de cabina de la spec 3b** (`TOP`, `MAX`, `f`, color de la velocidad, cargas de boost con su prioridad, textos del boost y del daño, TIME TRIAL sin daño, transiciones de 0,25 s y parpadeo del daño a 2 Hz). **No se duplica.** Lo que cambia es **cómo se pinta**, con la paleta del estilo carreras:

| Dato | Color en carreras |
|---|---|
| Velocidad normal / por encima de `TOP` o con boost | `main` `#FFFFFF` / `over` `#FF8A00` |
| Carga llena | `orb` `#FF8A00`, relleno 100 %, borde 100 %, con halo |
| Carga en cooldown | `orb`, relleno 30 %, borde 55 %, sin halo |
| Carga bloqueada (daño 3) | `crit` `#FF2D2D`, relleno 30 %, borde 55 %, sin halo |
| Carga vacía | `orbOff` `#3A2A1A`, relleno 50 %, borde 90 % |
| Daño 0 / 1 / 2–3 | `ok` `#3DFF8A` / `warn` `#FFE04A` / `crit` `#FF2D2D` |
| Rotulaciones | `label` `#9A9A9A` |
| Marcos de las esquinas | `frame` `#FF3B30` |
| Fondo de la barra | `bar` `#2A2A2A` |

- Los valores de relleno y borde de esta tabla son los del **prototipo** y mandan sobre los de la 3b **solo en esta cabina**.
- En el prototipo el parpadeo del daño va a 4 Hz: en el juego se queda el de la **3b (2 Hz)**, igual en todas las cabinas.

### 6.2 Dibujo de las pantallas

Cada pantalla (640 × 200 px) se dibuja **como `drawLeft` / `drawRight` del prototipo**: mismas posiciones, tamaños, grosores, opacidades y textos.

- **Izquierda**: `HULL STATUS`, texto del daño, 3 segmentos de 150 × 44, `INTEGRITY` y su línea.
- **Derecha**: `BOOST` y su texto, 3 esferas con anillo, número de velocidad (88 px) con `KM/H`, barra con 10 marcas y la **marca naranja en `TOP`**.
- Fondo con líneas de barrido (1 px cada 3 px, blanco al 2,5 %) y las cuatro esquinas.
- Fuente: **Rajdhani Bold** (licencia OFL), tamaños del prototipo **× 1,15**. Se añade a la carpeta de fuentes con su licencia. El agente pide permiso antes de descargarla, o la aporta el usuario.
- **Halo** de los textos y las esferas (`shadowBlur`): se puede aproximar con una copia más gruesa y tenue. Sin shaders nuevos, salvo que sea lo más sencillo.
- En **TIME TRIAL**, la regla de la 3b: segmentos apagados y sin texto del daño.
- El dibujo se separa en **piezas puras** que dan primitivas 2D a partir del estado (como en la 3b, reutilizando sus primitivas si sirven) y un nodo que las pinta. El `SubViewport` de cada pantalla **solo se redibuja si su estado cambia**.

### 6.3 Radar y minimapa en sus pantallas

- La **lógica** es la de la spec `radar-minimapa` (proyección, alcance de 120 m, barrido de 1,6 s, ecos, nave pegada a 12 m, ventana de 450 m que gira contigo). **No se duplica**: sus clases puras dan las primitivas y aquí solo cambian el **tamaño** y la **paleta**.
- Dibujo como `drawRadarScreen` y `drawMapScreen` del prototipo, en **256 × 256 px**, sobre `screenBase` (fondo, líneas de barrido y esquinas):
  - **Radar**: centro (128, 122), radio 94. Borde `label` 2 px al 80 %; anillos y cruz `label` 1,5 px al 35 %; haz en `main` (28 líneas, 2,5 px, de 0,35 a 0); ecos de 5,5 px en `rival` o de 7 px en `crit` si la nave estaba pegada; arco de nave pegada en `crit`, 7 px, por fuera del borde (radio + 8); tu nave, flecha `main` de 11 px; rótulo `120 M` centrado abajo, `label`, 18 px × 1,15.
  - **Minimapa**: centro (128, 128), radio 106. Banda `label` de 14 px al 35 % y línea `main` de 3 px al 90 %; meta en `frame`, 4 px; naves en `rival`, radio 6; borde `label` 2 px al 80 %; tu nave, flecha `main` de 11 px.
- Colores del estilo carreras: `main` `#FFFFFF`, `label` `#9A9A9A`, `rival` **`#FF8A00`**, `crit` `#FF2D2D`, `frame` `#FF3B30`. Van en la **paleta del estilo carreras**, junto al resto.
- El `SubViewport` del radar se redibuja **cada frame** (el haz se mueve siempre); el del minimapa también (gira contigo).

### 6.4 Shift lights

15 LEDs sobre la visera: 5 verdes `#3DFF8A`, 5 rojos `#FF2D2D` y 5 azules `#3D7BFF`, de izquierda a derecha.

- Encendidos: `n = redondeo(15 × límite((v − 0,55 · TOP) / (0,45 · TOP), 0, 1))`, con el redondeo de JavaScript (las mitades hacia arriba: `MidpointRounding.AwayFromZero`). Los `n` primeros, a intensidad **3,5**; el resto, apagados.
- Por **encima de `TOP`** (más de 1 km/h) **o con boost**: los 15 en **azul**, parpadeando a **5 Hz** (encendidos a 4 / apagados).
- `v` es la velocidad que enseña el HUD; `TOP` la punta **base** de la nave (como en la 3b).

### 6.5 Parpadeo con daño

- Desde el nivel 1, las luces `Accent_*` y `Led_*` titilan bajando al **20 %** de su intensidad, con las **reglas de la spec `danos`** (proporción, intervalos y duración), igual que las aristas de la capa 2D.
- Las shift lights y las cuatro pantallas **no** titilan.

### 6.6 Luz de boost y daño

- Las tres luces de boost y daño de la tabla 4.3, con `tinte` (boost) y `daño` calculados como el prototipo: el tinte va hacia 0,55 con boost, 0,25 por encima de `TOP` y 0 si no, en unos 0,1 s; el daño es 0,15 fijo con nivel 2 y late entre 0,45 y 0,20 cada 1 s con nivel 3.
- Los **tintes de los bordes de pantalla** de la 3b siguen igual (son comunes a todas las cabinas).
- Cada luz tiene su **intensidad en el Inspector**, que multiplica el valor. A 0, se apaga. En TIME TRIAL nunca hay luz de daño.
- La **pausa** congela parpadeos, latidos y shift lights.

### 6.7 Cabina 2D de Vector

Al cerrar la etapa 3, si el usuario valida la 3D, se **quita** la cabina 2D de Vector del código y su prueba de captura (el diseño sigue guardado en `designs/`). Lo común de la 3b (primitivas, estado, tintes, cabinas de BLADE y HAMMER) se queda. Las pruebas de lo común que usen a Vector como ejemplo pasan a BLADE o HAMMER, sin perder lo que comprueban.

## 7. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| FOV de la cámara de cabina 3D | 70 | Cabina 3D |
| Balanceo lateral · vibración (posición / giro) · contragiro · empuje | 0,03 · 1,6 / 0,35 · 0,25 · 0,05 | Cabina 3D |
| Intensidad de cada luz (principal, lámpara, pantallas, boost, daño) | La ajustada en la etapa 2 | Cabina 3D |
| Glow de la cabina (fuerza, radio, umbral) | 0,55 · 0,45 · 0,8 (o su equivalente) | Cabina 3D |
| Brillo de las pantallas | 1,35 | Cabina 3D |

Tiempos (parpadeos, latidos, transiciones) y colores van como **constantes con nombre**.

**Valores finales**, en el grupo **Cabina 3D** del `Player`, ajustados midiendo 16 zonas de la cabina contra el prototipo, a punta y en recta: principal 0,65 · ambiente 0,32 · lámpara 0,025 · pantallas grandes 0,2 · pantallas pequeñas 0,13 · glow 0,8 / 0,9 / 0,8 · boost delantera y trasera 0,85 · daño 0,7 · brillo de las pantallas 1,35. Se añaden `Cockpit3DClearcoat` 1 (por encima de 1, Godot oscurece lo de debajo y la consola se queda negra), `Cockpit3DGlowWide` 1 (reparte el halo como el bloom del prototipo), `Cockpit3DReflections` 1 y `Cockpit3DCarbonSpecular` 1 (compensa que Godot pinta el barniz más flojo). El limitador de rugosidad se apaga en `project.godot`: apagaba el carbono, y el resto del juego son líneas sin luz. Las shift lights siguen como constantes, sin Inspector.

## 8. Pruebas

### Pruebas unitarias mínimas

1. **Manifiesto**: el de `cockpit_vector.manifest.json` tiene todas las piezas de la tabla 3.7, con las cantidades exactas (15 shift lights, 4 LEDs de consola…); las pantallas grandes miden 0,27 × 0,085 m y las pequeñas 0,09 × 0,09 m (±1 mm).
2. **Perfil visual**: VECTOR → cabina 3D carreras; BLADE → Burbuja 2D; HAMMER → Búnker 2D.
3. **Qué se ve en cada caso** (tabla 4.7), incluida la regla temporal de la etapa 2.
4. **Movimiento de la cámara de cabina**: con datos conocidos da las fórmulas de la tabla 4.4, también `vib` a partir de `Intensity` y `Eased`; a fondo con boost, la posición no se aparta más de ~1,1 cm por vibración; con los parámetros a 0, queda en el origen sin giro.
5. **Shift lights**:
   - v = 0 y v = 0,55 · TOP → 0 encendidas.
   - v = TOP → 15.
   - v = 0,775 · TOP (`n` = 7,5) → 8.
   - Por encima de TOP + 1 o con boost → las 15 azules, encendidas en la primera mitad de cada 0,2 s y apagadas en la segunda.
6. **Pintado en carreras**: los colores y opacidades de la tabla 6.1 para cada estado (incluido el orden bloqueada → cooldown → llena → vacía).
7. **Barra de velocidad**: ancho × `f`; la marca en `TOP / MAX` del ancho.
8. **Luz de boost y daño**: valores de la sección 6.6 multiplicados por su intensidad; a 0 se apagan; en TIME TRIAL no hay daño.
9. **Parpadeo con daño**: solo elige entre `Accent_*` y `Led_*`.
10. **Radar y minimapa en carreras**: con la paleta del estilo carreras y 256 px, las clases de `radar-minimapa` dan las mismas posiciones (escaladas) que con la paleta del HUD de pantalla y los colores de la sección 6.3.

Las pruebas existentes siguen pasando. La de captura de la cabina 2D de Vector se retira en la etapa 3, con la cabina.

### Comprobación del script de Blender

- El propio script falla si falta una pieza del contrato (sección 3.7).
- Ejecutarlo dos veces seguidas da el **mismo** `.glb` (se compara el tamaño y un hash; si cambia la fecha interna, se comparan las piezas del manifiesto).

## 9. Prueba manual

**Etapa 1 · Blender**
1. Ejecutar el script: termina sin errores y deja el `.glb`, el manifiesto, el panorama, el `.blend` y las tres capturas.
2. `eye.png` al lado del prototipo en el navegador (colocación 1, cámara propia, sin mover la vista): **la misma cabina** desde el mismo sitio. Carbono, aluminio, tejido, franja roja, halo, shift lights, las dos pantallas grandes, las dos pequeñas y la consola estrecha en su sitio.
3. Abrir el `.blend` en Blender y dar una vuelta: no hay piezas sueltas, agujeros ni caras negras.
4. Ejecutar el script otra vez: mismo resultado.

**Etapa 2 · Godot**
5. RACE con VECTOR, cabina: se ve la cabina 3D, con el mundo por el cristal.
6. Acelerar a fondo: el FOV del mundo se abre pero **la cabina no se deforma**.
7. Curvas, vibración cerca de la punta y boost: la cabina se mueve un poco, como en el prototipo, y no marea.
8. Pantallas de prueba: `L` y `RADAR` a la izquierda, `MAP` y `R` a la derecha, **derechas y sin espejo**, con brillo y halo.
9. Reflejos en el carbono y el aluminio; sombras de los pilares en el salpicadero.
10. C: cámara exterior sin cabina y con el HUD de siempre. Volver con C.
11. F10 → BLADE y HAMMER: siguen con su cabina 2D, igual que antes.
12. Ventana a pantalla completa y estirada: la cabina 3D llena la pantalla sin cortes raros.
13. Rendimiento: medido como dice la sección 4.8, con el antes y el después apuntados.
13b. Encuadre: elegir entre las opciones A y B de la sección 4.5, con sus capturas.

**Etapa 3 · HUD y luces**
14. Acelerar: el número, la barra y el brillo de las pantallas suben; por encima de la punta, en naranja.
15. Shift lights: se van encendiendo de verde a rojo a azul y, por encima de la punta o con boost, parpadean en azul.
16. Boost: luz naranja en la cabina, `ACTIVE`, luego `COOLDOWN` con las esferas atenuadas.
17. Dañar hasta nivel 3: segmentos y texto del daño, luz roja latiendo, `LOCKED` con las esferas en rojo y las luces de la cabina titilando.
18. TIME TRIAL: daño apagado y nunca luz roja.
18b. Con naves de prueba (F6): el radar de la izquierda barre y enseña los ecos en naranja, con el arco rojo si una va pegada; el minimapa de la derecha gira contigo con las naves en naranja. Lo mismo que en el HUD de pantalla, en su sitio.
19. Pausa con boost y daño 3: todo congelado.
20. El HUD de pantalla de velocidad, boost, daño, radar y minimapa ya no sale en la cabina de VECTOR. En la cámara exterior, sí.
20b. Rendimiento con todo vivo: medido otra vez como dice la sección 4.8, con radar y minimapa en sus pantallas y parpadeo con daño 3.
21. Menú principal: la versión es 0.14.4.

## 10. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Blender** | Inventario (sección 2) y sección 3 completa | Comprobación del script · Prueba manual 1–4 · Commit |
| **2 · Godot** | Secciones 4 y 5 | Pruebas 1–4 · Prueba manual 5–13 · Commit |
| **3 · HUD y luces** | Sección 6 · versión 0.14.4 | Pruebas 5–10 · Prueba manual 14–21 · Commit |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas y el `.blend`.

## 11. Cambio en el documento maestro

Se actualiza el apartado de **identidad y arte** del documento maestro (en la versión que esté en curso):

> **Naves y cabinas realistas** (2026-11-06). Las naves y sus cabinas dejan el estilo wireframe y pasan a un estilo **realista**: volumen, materiales con texturas (PBR), reflejos y luz. **Cada nave tiene su propio estilo**, sin una temática común (por ejemplo, VECTOR carreras, BLADE militar y HAMMER industrial), y se pueden inventar estilos nuevos sobre la marcha. Los colores de las pantallas de cada cabina son los de su estilo.
>
> **Todo lo demás sigue igual**: circuitos, entorno, efectos de velocidad, menús y HUD de las esquinas mantienen la estética vectorial y luminosa y la paleta del juego.
>
> Las naves y cabinas se **modelan en Blender con scripts de Python** que ejecuta el agente, y se exportan a Godot en **glTF (`.glb`)**. El script es la fuente: el `.blend` se regenera.

Y en la regla «solo líneas y aristas luminosas, sin superficies sólidas texturizadas» se añade: **«salvo naves y cabinas»**.

En `CLAUDE.md`, al cerrar cada etapa, se recoge lo mismo en **Estilo visual**, junto con otra excepción: la paleta del estilo carreras es propia de esa cabina y no sale de los colores generales de `Palette`.

## 12. Fuera de alcance

- Cabinas 3D de BLADE y HAMMER (specs 2 y 3 de cabinas 3D).
- Casco exterior realista de cualquier nave: VECTOR sigue con su casco wireframe en la cámara exterior, el selector y el menú.
- Mirar alrededor con el ratón dentro de la cabina.
- Luz del mundo dentro de la cabina (túneles, ciudad, otras naves): la cabina tiene su propia luz.
- Animar piezas (mandos, botones) o sonido.
- Cambios en el HUD de las esquinas, en la cámara exterior o en los efectos de la cámara del mundo.
- Menú SETTINGS (las intensidades quedan listas para él).

## 13. Lo que se aparta de la spec en la etapa 3

- **Estado común con aspecto propio**: `CockpitHudState` recibe un `CockpitHudLook` con lo apagado de cada estilo, para que la tabla 6.1 mande solo en esta cabina.
- **Radar y minimapa**: se reutilizan `RadarView` y `MinimapView` dentro de sus `SubViewport`, con `RadarStyle.CockpitScreen` y `MinimapStyle.CockpitScreen`. El borde del minimapa va debajo del trazado (en el prototipo, encima): lo fijo se dibuja una vez.
- **Parpadeo con daño**: usa `EdgeFlicker` con una "arista" por luz, las 11 de `Accent_*` y `Led_*`, y los valores de siempre del Inspector.
- **Luces de boost y daño**: las intensidades del Inspector multiplican el valor del prototipo, y la vista lo pasa a Godot (÷ π).
- **Diferencias visibles que quedan**, medidas en valor de rojo sobre 255 (juego / prototipo): biseles de aluminio 63 / 98, pared lateral 66 / 30, mando izquierdo 133 / 102 y morro derecho 95 / 64. El rojo de la pintura es más puro en el juego. Acercar más una zona aleja otra.
