# VECTORY — Spec: Cabinas 3D · 2 · Blade (militar)

Versión 1.0 · 2026-11-12 · Estado: **propuesta, pendiente de validar**

Segunda spec de las **cabinas en 3D**. Hace la cabina de **BLADE** en estilo **MILITAR** (pintura gris verdosa desgastada, antideslizante, pintura rugosa y metal pavonado), partiendo de la idea de la **Burbuja**: arco fino de la burbuja, cristal casi sin marcos y **morro larguísimo** de cubierta plana.

Repite la cadena que ya validó VECTOR (spec `cabinas-3d-1-vector`): script de Blender → `.glb` → Godot con **su propia cámara** → **HUD vivo en las pantallas**. Lo nuevo de esta cabina:

- **Cristal HUD** (combiner) sobre el cubre-instrumentos, con velocidad, rumbo y boost proyectados.
- **Tres pantallas militares (MFD)** en **cajas que sobresalen** del frontal, con **botonera** y **visera**: velocidad, boost y daño a la izquierda, **radar grande** en el centro y **minimapa** con el progreso de la vuelta a la derecha.
- **Pilotos MASTER CAUTION y MASTER WARN** que reaccionan al daño.
- La vista mira **5,7° hacia abajo** (sección 4.5).

Plan de las specs de cabinas 3D:

1. Vector · carreras (hecha).
2. **Blade · militar** (esta).
3. HAMMER (estilo por decidir, p. ej. industrial).
4. Después: cascos exteriores realistas de las tres naves.

Diseño de referencia:
- **`designs/cabina-3d-blade.html`**: el prototipo. Se abre en el navegador y se puede conducir. **Solo vale la disposición 4 · DEFINITIVA** (la que sale al abrirlo). Las disposiciones 1 COMBATE, 2 RELOJES y 3 TÁCTICO se quedan guardadas como diseño, pero **su código no se traduce** (salvo las piezas que usa la 4, ver abajo).
  - **Va a Blender** todo lo que construye `buildCockpit(4)`: la parte común (morro, salpicadero, laterales, arco, cristal, pilotos MASTER y placa), `buildCombiner()` y las tres cajas de `makeMFD()` con `pod` y `visor`. También las texturas (`paintSet` ×2, `crinkleSet`, `antiSlipSet`, `gunmetalSet`, `hazardSet`, `smudgeTex`) y el entorno de reflejos (`buildEnv`).
  - **Va al código del juego** la lógica viva: `hudState`, `orbStyle`, el dibujo de las pantallas (`base`, `corners`, `T`, `osb`, `ownShip`, `radarCore`, `mapCore`, `drawCombiner`, `drawRadar3`, `drawSpeed3`, `drawMap3`), los pilotos MASTER, el parpadeo con daño, las luces de boost y daño, el brillo de las pantallas y el movimiento de la cámara de cabina. La **lógica** del radar y del minimapa (barrido, ecos, proyección, ventana) **no** se copia del prototipo: es la de la spec `radar-minimapa`, que ya está en el juego.
  - **No va a ningún sitio**: el mundo, el circuito y las naves simuladas, la conducción simulada, los botones, la mirada con el ratón, `makeGauge`, `drawRadar1/2`, `drawMap1/2`, `drawSpeed2`, `drawDamage1` y `drawAnnun2`.
  - Los comentarios **`→ Nombre`** y los nombres que se pasan a `B.mesh`/`B.group` dicen cómo se llama cada pieza en Blender.

Si algo no cuadra entre esta spec y el prototipo, **manda el prototipo** y se avisa.

Rama: `feature/cockpit-3d-blade`, que sale de `develop` con `feature/cockpit-3d-vector` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.4 → 0.14.5** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. El cambio de dirección visual (naves y cabinas realistas, cada nave con su estilo) ya entró con la spec de Vector: **no hay cambios en el documento maestro**.

---

## 1. Resumen

- **Etapa 1 · Blender**: un script nuevo, `cockpit_blade.py`, construye la cabina reutilizando `art/blender/lib/`, la exporta a `.glb` y guarda el `.blend`, las capturas y el manifiesto. En el juego no cambia nada.
- **Etapa 2 · Godot**: la cabina 3D entra para BLADE en la vista de cabina, con su cámara, su luz, sus reflejos y su **inclinación de 5,7°**. Sustituye a la Burbuja 2D. Las pantallas enseñan una **imagen de prueba**.
- **Etapa 3 · HUD y luces vivas**: el cristal HUD y las tres pantallas enseñan **velocidad, boost, daño, radar y minimapa**; entran los **pilotos MASTER**, el **parpadeo con daño** y las **luces de boost y daño**. Se quita la Burbuja 2D.
- VECTOR **no cambia**. HAMMER **no cambia**: sigue con su Búnker 2D de la spec 3b.
- Lo que **no cambia**: la cámara exterior (con su HUD de siempre), el HUD de las esquinas (posición, vuelta y tiempos), el casco exterior de BLADE, la pista, el entorno y los efectos de velocidad.

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **Lo que dejó Vector**: qué hay en `art/blender/lib/` y si sus funciones de ruido (`mkRng`, `valueNoise`, `fbm`) y de UV (`boxUV`) dan lo mismo que las de este prototipo. Si alguna es distinta, se añade la versión de Blade **sin cambiar** la que usa Vector (la cabina de Vector no se toca).
2. **`Cockpit3DView`** y el lector del manifiesto: qué tienen escrito a fuego para Vector (nombres de piezas, número de pantallas, shift lights, paleta) y qué hay que generalizar para que sirvan a cualquier nave (sección 5).
3. **Ojo de la cámara de cabina de BLADE**: altura sobre la pista, posición adelante/atrás y si el perfil visual admite hoy una **inclinación** (pitch) por nave. El prototipo tiene el ojo a **0,90 m** sobre la pista, FOV vertical **70°** e inclinación **−5,7°** (−0,10 rad).
4. **Burbuja 2D** (spec 3b): qué clases y pruebas son solo suyas, para quitarlas en la etapa 3.
5. **Datos que piden las pantallas nuevas**: de dónde se saca el **rumbo** de la nave, la **entrada de giro** (para el marcador de trayectoria del cristal), la **vuelta actual y total** y la **distancia recorrida en la vuelta** (para la barra de progreso).
6. **Fuente**: que `Share Tech Mono` sigue en el proyecto (la usan la 3b y el HUD).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · La cabina en Blender

### 3.1 Cómo se usa Blender

Igual que en Vector (sección 3.1 de `cabinas-3d-1-vector`): Blender sin ventana, desde la terminal, con la misma ruta guardada en `art/blender/run.ps1`:

```
blender -b --factory-startup --python art/blender/cockpit_blade.py -- --out <carpeta>
```

Todo vive en el script, el `.blend` se regenera y el script es **determinista**.

### 3.2 Archivos

| Archivo | Qué es | ¿Va a Git? |
|---|---|---|
| `art/blender/cockpit_blade.py` | Construye la cabina, exporta y guarda todo | Sí |
| `art/blender/lib/` | Piezas comunes de Vector + lo nuevo que haga falta (segmento de esfera, ver 3.4) | Sí |
| `assets/…/cockpit_blade.glb` | Modelo exportado, con las texturas dentro | Sí |
| `assets/…/cockpit_blade.manifest.json` | Nombres, tamaños y recuentos (sección 3.7) | Sí |
| `assets/…/cockpit_blade_env.png` | Panorama de reflejos (sección 3.6) | Sí |
| `art/blender/out/cockpit_blade.blend` | Escena de Blender | No (`.gitignore`) |
| `art/blender/out/previews/blade_*.png` | Capturas de previsualización | No (`.gitignore`) |

La carpeta de `assets/` es la misma que usó Vector.

### 3.3 Ejes, unidades y origen

Igual que Vector: metros, ejes del prototipo = ejes de Godot (X derecha, Y arriba, el piloto mira a −Z), todo dentro de un objeto raíz **`CockpitBlade`** girado **+90° en X** en Blender, con los números del prototipo copiados **tal cual**. El origen es el **ojo del piloto**.

- La **inclinación de 5,7°** **no** va en el modelo: la ponen las cámaras (sección 4.5). El modelo se construye con el ojo mirando recto, como en el prototipo.
- Comprobación antes de exportar: la punta del morro (`Nose_Tip`) en Godot en z ≈ −4,75, la punta de la sonda (`Nose_PitotTip`) en z ≈ −5,48 y lo más alto del arco (`Canopy_Bow`) en y ≈ +0,456.

### 3.4 Geometría

Se traduce `buildCockpit(4)` **pieza a pieza**, con los mismos números y en el mismo orden. Las piezas de `B` son las de Vector (`poly`, `strip`, `rbox`, `beam`, `tube`, `cyl`, `sphere`, `group`) con las mismas reglas: caja redondeada con **2 segmentos** y radio limitado como en `B.rbox`, tubo de **10 lados** por **Catmull-Rom centrípeta** muestreada en `seg` tramos, esferas de **10 × 8**, grupos con `position`, `rotation` (orden XYZ) o `lookAt` (en objetos que no son cámara, gira para que su **+Z** apunte al punto).

Lo nuevo respecto a Vector:

| Prototipo | En Blender |
|---|---|
| `B.beam` | Base del prototipo: `Z` = de `p0` a `p1`; `T` = `toward` − punto medio; `X` = `T × Z`; `Y` = `Z × X` |
| `B.cyl(rt, rb, h, n)` | Cilindro (o cono si un radio es 0) de `n` lados, eje Y, como `CylinderGeometry` |
| `SphereGeometry(0,95, 48, 24, 1,12π, 0,76π, 0,12π, 0,42π)` (`Glass_Canopy`) | Segmento de esfera con la fórmula de three.js: `x = −r·cos φ·sen θ`, `y = r·cos θ`, `z = r·sen φ·sen θ`, con φ de 1,12π a 1,88π y θ de 0,12π a 0,54π. Va a `lib/` |
| `PlaneGeometry(w, h)` | Plano de w × h mirando a +Z, UV 0–1 entero |
| Escalas sueltas (`r.scale.y = 0.4` en remaches) | Se aplica la escala antes de exportar |

Partes de la cabina (detalle en el prototipo):

- **`Nose`**: 7 secciones de 6 puntos (`st` y `sec`) unidas por 5 tiras, la tapa de la punta, el panel antirreflejos (`Nose_AntiGlare`), 4 juntas transversales, 2 juntas longitudinales, los remaches, las luces de posición `Led_Nose_L` (roja) y `Led_Nose_R` (verde), la sonda (`Nose_Pitot` + `Nose_PitotTip`) y la antena.
- **`Dash`**: frontal (`Dash_Face`), cubre-instrumentos (`Glare_Top`, `Glare_Lip`, `Glare_Under`, `Glare_Edge`) con las curvas `cowlY` y `cowlZ`, la luz bajo el labio (`Accent_Glare`), los pilotos `Lamp_Caution` y `Lamp_Warning` y la placa (`Dash_Plate`).
- **`Side_L` / `Side_R`**: mejilla, tapa de la mejilla, alféizar, carril del cristal y remaches. En el izquierdo, el tirador del cristal (`Canopy_Handle`, con franjas, y `Canopy_HandleStem`).
- **`Frame`**: arco (`Canopy_Bow`) y su junta (`Canopy_Seal`), sensor con su piloto (`Led_Sensor`), retrovisores (`Mirror_L`, `Mirror_R`), cristal (`Glass_Canopy`) y manchas (`Glass_Smudge`).
- **`Console`**:
  - **`Combiner`**: base con 6 teclas, 2 postes, cristal (`Combiner_Plate`), la superficie del HUD (`Screen_HUD`) y su marco.
  - Tres cajas de pantalla, cada una con: carcasa de **10 cm de fondo**, bisel, botones OSB, balancines de las esquinas, tornillos, **pestaña de anclaje** con 4 tornillos (`_Flange`), 4 nervios (`_Rib`), junta inferior (`_Seam`), **visera** (`_Visor`: chapa, canto de goma, laterales, tira de luz y 2 tornillos), superficie (`Screen_*`), sombra (`Screen_*_Shade`) y línea de luz (`Accent_*`):

| Pantalla | Centro del grupo `_Panel` | Mira hacia | Pantalla (m) | OSB (horiz. × vert.) |
|---|---|---|---|---|
| `Screen_Speed` (izquierda) | (−0,232, −0,279, −0,463) | (0, 0,02, 0,12) | 0,105 × 0,14 | 4 × 5 |
| `Screen_Radar` (centro) | (0, −0,279, −0,455) | (0, 0,02, 0,12) | 0,17 × 0,14 | 7 × 5 |
| `Screen_Map` (derecha) | (+0,232, −0,279, −0,463) | (0, 0,02, 0,12) | 0,105 × 0,14 | 4 × 5 |

  El número de OSB sale de `max(3, redondeo(lado / 0,026))`, como en el prototipo; la tabla es solo para comprobar.

- **Añadido sobre el prototipo** (pedido por el usuario al validar la etapa 1: el frontal quedaba vacío alrededor de las cajas). **No está en el prototipo**: solo se ve en las capturas y en el juego. Todo en coordenadas del frontal (`Dash_Face`), solo decoración, sin piezas vivas ni cambios en el contrato con Godot:
  - A cada lado de las cajas, una **placa de interruptores** atornillada (`M_Paint`, 0,24 × 0,17 m, centrada en x = ±0,47): 2 filas de 3 **interruptores de palanca** con **guardas** compartidas entre columnas y un **selector giratorio** con 5 marcas hacia fuera.
  - En la placa izquierda, un **interruptor con tapa de seguridad a franjas** (`M_Hazard`, tipo *MASTER ARM*, cerrada); en la derecha, un segundo selector más pequeño.
  - **Juntas** verticales en x = ±0,132, ±0,335 y ±0,628 y una horizontal sobre cada placa, con **remaches**.
  - Las piezas pequeñas del añadido usan caja redondeada de 1 segmento y esferas más bajas que las del prototipo: suman unos 8.000 triángulos (sin eso eran ~30.000).

- **Caras por los dos lados**: el prototipo dibuja todo con `DoubleSide`. Igual que en Vector, los materiales van **con doble cara** salvo que el agente compruebe que una pieza solo se ve por un lado.
- **Sombreado**: plano en polígonos y tiras; suave en tubos, esferas, cilindros y redondeos.
- Las piezas con `noShadow` **no proyectan sombra** (pantallas, sombras de pantalla, luces, LEDs, pilotos, cristales, tornillos, remaches, juntas y la placa). Se apuntan en el manifiesto y se aplican en Godot.

### 3.5 Materiales y texturas

- Como en Vector: **Principled BSDF** con **texturas de imagen**, sin nodos procedurales.
- Texturas generadas **en Python** con el **mismo algoritmo y las mismas semillas** del prototipo, a **512 × 512**, guardadas como PNG: color (sRGB), rugosidad y normal (no color, convención OpenGL). `mkRng` enmascarado a 32 bits sin signo en cada paso.

| Conjunto | Semilla | Qué es |
|---|---|---|
| `paintSet(61, [72, 80, 72])` | 61 | Pintura gris verdosa del interior, con desconchones |
| `paintSet(67, [92, 100, 94])` | 67 | Pintura gris clara del morro |
| `crinkleSet(62)` | 62 | Pintura rugosa negra de frontal, carcasas y viseras |
| `antiSlipSet(63)` | 63 | Antideslizante del cubre-instrumentos y del panel antirreflejos |
| `gunmetalSet(64)` | 64 | Metal pavonado cepillado |
| `hazardSet(65)` | 65 | Franjas amarillas y negras del tirador |
| `smudgeTex(66)` | 66 | Manchas del cristal |

- **Repetición**: UV = **metros** con la regla `boxUV` del prototipo, **multiplicadas por la repetición** del material (sin `KHR_texture_transform`).

| Material | Base | Metal | Rugosidad | Repetición | Extra |
|---|---|---|---|---|---|
| `M_Paint` | pintura interior | 0,15 | textura | ×3 | — |
| `M_PaintDark` | pintura interior × (0,55, 0,58, 0,56) | 0,15 | textura | ×3 | Arco, sensor, retrovisores, antena |
| `M_PaintNose` | pintura del morro | 0,12 | textura | ×1,5 | Reflejos ×0,5 |
| `M_Crinkle` | rugosa negra | 0,1 | textura | ×8 | — |
| `M_AntiSlip` | antideslizante | 0 | textura | ×10 | — |
| `M_Gunmetal` | pavonado | 1 | textura | ×2 | — |
| `M_Hazard` | franjas | 0,1 | textura | ×12 | — |
| `M_Rubber` | `#0B0C0B` | 0 | 0,95 | — | Juntas, OSB, teclas, canto de visera |
| `M_Dark` | `#050605` | 0 | 0,9 | — | Bisel, juntas, bajo el labio |
| `M_Mirror` | `#AAB0B0` | 1 | 0,04 | — | Cristal de los retrovisores |
| `M_Emissive_*` | negro + emisión | 0 | 0,4 | — | Una por color: `Accent` (`#7CFF5A`), `NavL` (`#FF2A1E`), `NavR` (`#3DFF6A`), `Sensor` (`#7CFF5A`) |
| `M_Screen` | negro (la textura la pone Godot) | 0 | 1 | — | `Screen_Radar`, `Screen_Speed`, `Screen_Map` |
| `M_ScreenHUD` | negro (la textura la pone Godot) | 0 | 1 | — | `Screen_HUD`: Godot lo pone en modo **suma** |
| `M_ScreenShade` | negro al 70 % | 0 | 1 | — | Transparente |
| `M_Lamp` | negro (la textura la pone Godot) | 0 | 1 | — | Caras de los pilotos MASTER |
| `M_Plate` | negro (la textura la pone Godot) | 0,3 | 0,6 | — | Placa `BLADE · BLD-02` |
| `M_Glass` | negro al 5 % | 0 | 0,04 | — | Transparente, reflejos ×1,3 |
| `M_CombinerGlass` | `#0A2A10` al 10 % | 0 | 0,03 | — | Transparente, reflejos ×1,5 |
| `M_GlassSmudge` | textura de manchas | 0 | 1 | — | Godot la pone en modo **suma**, opacidad 0,05 |

- Reflejos (`envMapIntensity`), modo suma y transparencias que glTF no lleva se apuntan en el manifiesto y los aplica Godot.
- Cada pieza viva (sección 3.7) tiene su **propio material o instancia**, para que Godot pueda encenderla y apagarla sola.
- Los **textos fijos** (pilotos MASTER y placa) **no** se generan en Blender: el modelo lleva el plano con UV 0–1 y Godot le pone la textura (sección 4.6).

### 3.6 Panorama de reflejos

Se monta la escena de `buildEnv()` del prototipo (es la misma idea que la de Vector: noche, ciudad naranja en el horizonte y luces azules arriba, con semilla **77**) y se renderiza como equirectangular de **1024 × 512** → `cockpit_blade_env.png`. Si sale **igual** que el de Vector (mismo algoritmo y semilla), se reutiliza `cockpit_env.png` y se avisa.

### 3.7 Nombres de las piezas (contrato con Godot)

Godot busca estas piezas **por nombre**. El script comprueba antes de exportar que **están todas** y, si falta una, **falla con un mensaje claro**.

| Nombre | Qué es | Cuántas |
|---|---|---|
| `CockpitBlade` | Raíz (origen = ojo) | 1 |
| `Screen_HUD` | Superficie del cristal HUD: plano de **0,22 × 0,13 m**, UV **0–1 entero**, U a la derecha y V arriba vistas por el piloto | 1 |
| `Screen_Speed`, `Screen_Radar`, `Screen_Map` | Superficie de cada pantalla: **0,105 × 0,14**, **0,17 × 0,14** y **0,105 × 0,14 m**, UV 0–1, misma orientación | 3 |
| `Screen_Speed_Shade`, `Screen_Radar_Shade`, `Screen_Map_Shade` | Sombra interior de cada pantalla | 3 |
| `Accent_Speed`, `Accent_Radar`, `Accent_Map` | Línea de luz bajo cada pantalla | 3 |
| `Accent_Glare` | Luz bajo el labio del cubre-instrumentos | 1 |
| `Led_Visor_Speed`, `Led_Visor_Radar`, `Led_Visor_Map` | Tira de luz bajo cada visera | 3 |
| `Led_Nose_L`, `Led_Nose_R` | Luces de posición del morro (roja, verde) | 2 |
| `Led_Sensor` | Piloto del sensor del arco | 1 |
| `Lamp_Caution`, `Lamp_Warning` | Cara de cada piloto MASTER: plano de **0,048 × 0,019 m**, UV 0–1 | 2 |
| `Plate` (dentro de `Dash_Plate`) | Cara de la placa: plano de **0,10 × 0,0125 m**, UV 0–1 | 1 |
| `Combiner_Plate` | Cristal del HUD | 1 |
| `Glass_Canopy` | Cúpula del cristal | 1 |
| `Glass_Smudge` | Manchas del cristal | 1 |

El resto se agrupa por zonas (`Nose`, `Dash`, `Side_L`, `Side_R`, `Frame`, `Console`), con los nombres del prototipo dentro.

**Manifiesto** (`cockpit_blade.manifest.json`), con el mismo formato que el de Vector:
- Versión de Blender y fecha.
- Piezas vivas de la tabla, con su tamaño (pantallas, pilotos y placa) y su color de emisión (luces).
- Piezas sin sombra, materiales con reflejos distintos de 1, materiales en modo suma y materiales transparentes con su opacidad.
- Triángulos totales y caja que ocupa la cabina (en coordenadas de Godot).

### 3.8 Exportación

Igual que Vector: `.glb`, +Y arriba, modificadores aplicados, normales, UV y texturas **dentro**, sin cámaras, luces ni animaciones, y solo lo que cuelga de `CockpitBlade`.

### 3.9 Capturas de previsualización

En `art/blender/out/previews/`:
1. `blade_eye.png`: desde el ojo, **FOV vertical 70°** e **inclinación −5,7°**, 1280 × 720, con luz parecida a la del prototipo. Es la que se compara con el prototipo.
2. `blade_three_quarter.png`: desde atrás, arriba y a un lado.
3. `blade_screens.png`: de cerca, a las tres cajas con sus viseras y al cristal HUD.

Motor: el mismo que se usó con Vector. El agente **mira las capturas** antes de cerrar la etapa y las compara con el prototipo abierto en el navegador (disposición 4, sin mover la vista).

## 4. Etapa 2 · La cabina en Godot

### 4.1 Importación

Igual que Vector: nombres tal cual, sin colisiones, sin LOD ni luz horneada. Al cargar, el script de la cabina aplica lo del manifiesto (sin sombra, reflejos, suma, transparencias). Si algo no llega bien, se arregla **en Godot con su material**, sin tocar la geometría, y se avisa.

### 4.2 Cámara propia

La misma estructura que Vector (`SubViewport` con su mundo, fondo transparente, tamaño de la pantalla, deja de dibujarse en la cámara exterior):

- `Camera3D` en el origen, **FOV vertical 70°**, cercano **0,02**, lejano **30**, con la **inclinación de la sección 4.5**.
- `Environment`: cielo con el panorama de reflejos, tonemap **ACES**, exposición **1,05**, glow como el de Vector (fuerza 0,55, radio 0,45, umbral 0,8).
- Se pinta en el sitio de la capa de cabina 2D: por debajo del HUD de las esquinas, el panel de pruebas y la pausa.

### 4.3 Luces de la cabina

Valores del prototipo como **punto de partida**. Se ajustan **a ojo comparando con el prototipo** y se apuntan los valores finales.

| Luz | Tipo | Posición | Color · Intensidad en el prototipo | Notas |
|---|---|---|---|---|
| Principal | Direccional | desde (0,7, 2,2, −1,6) hacia (0, −0,3, −0,4) | blanca · 2,6 | Con sombras, suaves |
| Ambiente | Del cielo + color | — | hemisferio `#B0B8C8` / `#101010` · 0,6; ambiente `#1C221C` · 0,45 | — |
| Lámpara | Omni | (0, 0,32, −0,25) | blanca · 0,08 · alcance 1,6 | — |
| Pantallas (×3) | Omni | 6 cm delante de cada pantalla | `#CFFFC0` · `brillo × 0,03 × área / 0,017` · alcance 0,9 | `área` = ancho × alto de la pantalla; `brillo` en 6.1 |
| Boost delantera | Omni | (0, 0,25, 0,45) | `#FFA040` · `tinte × 3,2` · alcance 3,5 | Etapa 3 |
| Boost trasera | Omni | (0, −0,2, −1,8) | `#FFA040` · `tinte × 2,0` · alcance 4 | Etapa 3 |
| Daño | Omni | (0, 0,1, −0,35) | `#FF4D3A` · `daño × 1,2` · alcance 2,5 | Etapa 3 |

### 4.4 Movimiento de la cámara de cabina

**El mismo que Vector**, con el mismo código y los mismos parámetros (balanceo 0,03, vibración 1,6 / 0,35, contragiro 0,25, empuje 0,05), leyendo los datos de cámara del juego. Se suma a la inclinación de la sección 4.5.

### 4.5 Ojo, inclinación y encuadre

- La cabina se diseñó con el ojo a **0,90 m** de la pista (Vector × 0,62, como en la 3b), FOV 70° y la vista **5,7° hacia abajo** (−0,10 rad). Así el horizonte cae a la altura de la Burbuja 2D (y ≈ 300 de 720) y las tres pantallas se ven enteras.
- La inclinación va en las **dos cámaras** de la vista de cabina de BLADE: la del mundo y la de la cabina. Tienen que ser **iguales**, o el morro no casará con la pista.
- Se guarda en el **perfil visual de BLADE**, junto al ojo: altura 0,90 m e inclinación −5,7°. VECTOR (inclinación **0**) y HAMMER (**−8°**, la de su Búnker 2D) **no cambian**.
- La cámara exterior **no** se inclina.
- Si el ojo actual de BLADE (punto 3 del inventario) es muy distinto y el morro o el salpicadero tapan de más o de menos, **se avisa con capturas** antes de tocar nada.

### 4.6 Pantallas de prueba y textos fijos

- Cada pantalla enseña una **imagen de prueba** generada por código: fondo `#020502`, las líneas de barrido y las esquinas de `base` y `corners`, y el texto `HUD`, `SPD`, `RADAR` o `MAP` grande en el centro. En `Screen_HUD`, fondo **negro** (en suma no se ve) y el texto en verde. Sirve para comprobar la **orientación**, el **tamaño** y el **brillo**.
- Tamaños de los `SubViewport`: `Screen_HUD` **512 × 302**, `Screen_Radar` **512 × 420**, `Screen_Speed` y `Screen_Map` **300 × 400**.
- `Screen_Radar`, `Screen_Speed` y `Screen_Map`: **sin luz** (unshaded), sin sombras, con brillo **×1,35 × (0,8 + 0,4 × brillo)** (sección 6.1).
- `Screen_HUD`: sin luz, **modo suma**, sin escribir profundidad, sin tonemap, a dos caras.
- **Textos fijos**, dibujados **una vez** al cargar:
  - `Lamp_Caution` y `Lamp_Warning`: 256 × 112, fondo negro, texto blanco en negrita de 40 px, `MASTER` / `CAUTION` y `MASTER` / `WARN` en dos líneas (y = 50 y 94), centrado. Material sin luz que **multiplica** el color del piloto (sección 6.4).
  - `Plate`: 256 × 32, fondo `#0D100D`, texto `#9AA894` de 20 px, `BLADE · BLD-02`, centrado en y = 23.

### 4.7 Qué se ve en cada caso

| | VECTOR · cabina | BLADE · cabina | HAMMER · cabina | Cualquier nave · exterior |
|---|---|---|---|---|
| Cabina | 3D carreras | **3D militar** | Búnker 2D | No |
| Velocidad, boost y daño | Pantallas 3D | **Cristal HUD y pantalla izquierda** | Cuadro de mando 2D | HUD de pantalla |
| Radar y minimapa | Pantallas pequeñas 3D | **Pantallas central y derecha** | HUD de pantalla | HUD de pantalla |
| Posición, vuelta y tiempos | Esquinas | Esquinas | Esquinas | Esquinas |

En la etapa 2, mientras las pantallas son de prueba, BLADE en cabina enseña **también el HUD de pantalla** de velocidad, boost, daño, radar y minimapa, para poder conducir y probar. Se quita en la etapa 3.

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual. **Generalizar** lo de Vector sin cambiar cómo se ve Vector:

- **Perfil visual de nave**: la cabina 3D lleva la ruta del `.glb`, la del manifiesto, el **estilo** y su **disposición viva** (qué pantallas hay, qué pinta cada una y qué luces tiene). Además, **ojo con inclinación**. VECTOR → 3D carreras; BLADE → 3D militar; HAMMER → 2D Búnker.
- **`Cockpit3DView`**: deja de saber de Vector. Monta el `SubViewport`, la cámara (con la inclinación del perfil), el `Environment`, las luces y el modelo; aplica el manifiesto y busca las piezas vivas **según la disposición del perfil**. Si falta una, error claro. **Sin reglas dentro.**
- **Disposición viva de Blade** (pura): sus 4 pantallas con su tamaño y su dibujante, sus `Accent_*` y `Led_*`, y los pilotos MASTER. Las shift lights son solo de la disposición de Vector.
- **Paleta del estilo militar** (pura), centralizada junto a la de carreras:

| Clave | Color | Uso |
|---|---|---|
| `main` | `#A8FF8A` | Fósforo verde: trazos, números, velocidad normal, carga llena |
| `dim` | `#3F7F34` | Anillos del radar |
| `label` | `#86A07E` | Rotulaciones, bordes, escalas |
| `over` | `#FFB020` | Ámbar: por encima de `TOP` o con boost, carga en cooldown |
| `ok` / `warn` / `crit` | `#8CFF6E` / `#FFD23A` / `#FF3B2F` | Daño 0 / 1 / 2–3, nave pegada, carga bloqueada |
| `rival` | `#FFB020` | Rivales en radar y minimapa |
| `frame` | `#5FAF4C` | Esquinas de las pantallas |
| `bar` | `#1A2618` | Fondo de barras |
| `bg` | `#020502` | Fondo de las pantallas |
| `orbOff` | `#22301F` | Carga vacía |

  Y los colores de luz: boost `#FFA040`, daño `#FF4D3A`, caution `#FFB020`, warning `#FF2A1E`, acentos `#7CFF5A`, posición `#FF2A1E` / `#3DFF6A`, sensor `#7CFF5A`.
- **Dibujantes de las pantallas de Blade** (puros): piezas que dan primitivas 2D a partir del estado (como en Vector), una por pantalla, más las comunes `base`, `corners`, `osb` y `ownShip`.
- **Lector del manifiesto**: el de Vector, sin cambios si ya es genérico.

## 6. Etapa 3 · HUD y luces vivas

### 6.1 Estado

Se **reutiliza el estado del HUD de cabina** de la spec 3b (como en Vector): `TOP`, `MAX`, `f`, color de la velocidad, cargas con su prioridad, textos del boost y del daño, TIME TRIAL sin daño, transiciones de 0,25 s y **parpadeo del daño a 2 Hz**. **No se duplica.** Lo que cambia es cómo se pinta:

| Dato | Color en militar |
|---|---|
| Velocidad normal / por encima de `TOP` o con boost | `main` / `over` |
| Carga llena | `main`, relleno 100 %, borde 100 %, con halo |
| Carga en cooldown | `over`, relleno 30 %, borde 55 %, sin halo |
| Carga bloqueada (daño 3) | `crit`, relleno 30 %, borde 55 %, sin halo |
| Carga vacía | `orbOff`, relleno 50 %, borde 90 % |
| Daño 0 / 1 / 2–3 | `ok` / `warn` / `crit` |
| Texto del boost | `crit` si es `LOCKED`, `over` con boost activo, `main` si no |

- `brillo` = 0,35 + 0,35 × `f` (el de Vector).
- Datos nuevos que pide esta cabina (punto 5 del inventario): **rumbo** de la nave en grados, **entrada de giro** (−1 a 1), **vuelta actual / total** y **fracción recorrida de la vuelta** (0 a 1).

### 6.2 Cristal HUD (`Screen_HUD`, 512 × 302)

Como `drawCombiner` del prototipo: mismas posiciones, tamaños, grosores, opacidades y textos, en `Share Tech Mono`. Fondo **negro** (en suma no se ve).

- **Rumbo**: cinta arriba (recortada a x 140–372, y 6–40), una marca cada 5° y número cada 10° (en decenas, dos cifras), 4 px por grado, con el triángulo en el centro.
- **Velocidad**: cinta a la izquierda (recortada a x 10–102), **1,7 px por km/h**, marca cada 10 y número cada 50; triángulo `over` en `TOP`; caja con la punta hacia la derecha y el número (30 px, color de la velocidad) y `KM/H` debajo.
- **Nave y trayectoria**: símbolo fijo en el centro y marcador de trayectoria desplazado **entrada de giro × 34 px**, 34 px por encima.
- **Boost**: `BST`, tres galones de abajo arriba con el estilo de la tabla 6.1, el texto del boost y `TOP` con la punta base.
- Se redibuja **cada frame** (rumbo y cinta se mueven).

### 6.3 Pantalla izquierda (`Screen_Speed`, 300 × 400)

Como `drawSpeed3`: `SPD` y `KM/H` arriba, número grande (68 px), **escala vertical** (x 30–64, y 130–336) que se rellena `alto × f` con el color de la velocidad, cortada en 20 tramos, con 11 marcas y la **línea `over` en `TOP / MAX`**; **tres celdas de boost** (de abajo arriba); **columna de daño** de tres bloques (de abajo arriba, se encienden tantos como el nivel, en su color, con el parpadeo a 2 Hz en el nivel 3; en TIME TRIAL, apagados con borde `dim` y sin texto); y abajo los textos del boost y del daño. Se redibuja **solo si su estado cambia**.

### 6.4 Radar y minimapa en sus pantallas

La **lógica** es la de la spec `radar-minimapa`. **No se duplica**: sus clases puras dan las primitivas y aquí solo cambian el **tamaño**, la **paleta** y lo que se dibuja alrededor.

- **Radar** (`Screen_Radar`, 512 × 420), como `drawRadar3`:
  - Cinta de rumbo arriba (x 96–416, y 30–62), en `label`, con el triángulo `main`.
  - Círculo en (256, 236), radio **160**: anillos y cruz `dim`, **diagonales**, haz `main` (28 líneas de 2,5 px, de 0,38 a 0), ecos de **6,6 px** en `rival` o **8,4 px** en `crit` si la nave estaba pegada, arco de nave pegada en `crit` (7 px, radio + 8), tu nave de **13,2 px**. Rótulos `40`, `80` y `120` en los anillos.
  - A la izquierda: `TGT` con el número de naves **dentro de los 120 m** (dos cifras), `NRST` y la distancia a la más cercana en metros (tres cifras, `rival`; `crit` si está pegada; `---` si no hay). A la derecha: `SWP 1.6S`.
  - Abajo, rótulos de los OSB: `RDR` (seleccionado), `RNG 120`, `AUTO`, `DCL`.
  - Se redibuja **cada frame**.
- **Minimapa** (`Screen_Map`, 300 × 400), como `drawMap3`:
  - `MAP` y `TRK UP` arriba. Ventana en (150, 182), radio **128**: banda `label` de 14 px al 35 %, línea `main` de 3 px al 90 %, meta en `crit` de 4 px, naves en `rival` (radio 6), tu nave de 11 px. `450 M` debajo.
  - **Progreso de la vuelta**: barra (x 20–280, y 348–360) que se rellena con la **fracción recorrida de la vuelta**, con `LAP n/m` a la izquierda y el **porcentaje** a la derecha.
  - Se redibuja **cada frame**.

### 6.5 Pilotos MASTER

- **MASTER CAUTION**: encendido (color × **2,2**) con daño **1 o más**; apagado (× **0,12**) si no.
- **MASTER WARN**: con daño **3**, parpadea con el **mismo parpadeo del daño (2 Hz)** a × **2,6**; apagado (× 0,12) si no.
- En TIME TRIAL, los dos apagados. **No** titilan con el parpadeo de daño (sección 6.6). La pausa los congela.

### 6.6 Parpadeo con daño

- Desde el nivel 1, las luces `Accent_*` y `Led_*` titilan bajando al **20 %** de su intensidad, con las **reglas de la spec `danos`** (como en Vector).
- Intensidad base de cada una (emisión, se ajusta a ojo): `Accent_Speed/Radar/Map` **1,6**, `Accent_Glare` **0,45**, `Led_Visor_*` **0,35**, `Led_Nose_L/R` **3**, `Led_Sensor` **2,5**.
- Las pantallas, el cristal HUD y los pilotos MASTER **no** titilan.

### 6.7 Luz de boost y daño

Igual que en Vector: `tinte` hacia 0,55 con boost, 0,25 por encima de `TOP` y 0 si no, en unos 0,1 s; `daño` 0,15 fijo con nivel 2 y latiendo entre 0,45 y 0,20 cada 1 s con nivel 3. Cada luz con su **intensidad en el Inspector**; a 0, apagada. En TIME TRIAL nunca hay luz de daño. Los tintes de los bordes de la 3b siguen igual. La pausa congela parpadeos y latidos.

### 6.8 Burbuja 2D

Al cerrar la etapa 3, si el usuario valida la 3D, se **quita** la Burbuja 2D del código y su prueba de captura (el diseño sigue guardado en `designs/`). Lo común de la 3b (primitivas, estado, tintes y la cabina de HAMMER) se queda.

## 7. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Altura e inclinación del ojo de BLADE | 0,90 m · −5,7° | Perfil visual |
| FOV de la cámara de cabina 3D | 70 | Cabina 3D (común) |
| Balanceo · vibración · contragiro · empuje | Los de Vector | Cabina 3D (común) |
| Intensidad de cada luz (principal, lámpara, pantallas, boost, daño) | La ajustada en la etapa 2 | Cabina de BLADE |
| Brillo de las pantallas | 1,35 | Cabina de BLADE |
| Intensidad de los pilotos MASTER (encendido / apagado) | 2,2 y 2,6 / 0,12 | Cabina de BLADE |

Tiempos y colores como **constantes con nombre**.

## 8. Pruebas

### Pruebas unitarias mínimas

1. **Manifiesto**: el de `cockpit_blade.manifest.json` tiene todas las piezas de la tabla 3.7 con las cantidades exactas; las pantallas miden lo de la tabla (±1 mm).
2. **Perfil visual**: VECTOR → 3D carreras, inclinación 0; BLADE → 3D militar, ojo 0,90 m, inclinación −5,7°; HAMMER → Búnker 2D, inclinación −8° (sin cambios).
3. **Qué se ve en cada caso** (tabla 4.7), incluida la regla temporal de la etapa 2.
4. **Inclinación**: en la vista de cabina de BLADE, la cámara del mundo y la de la cabina tienen la **misma** inclinación; en la exterior, ninguna.
5. **Pintado en militar**: colores y opacidades de la tabla 6.1 para cada estado (incluido el orden bloqueada → cooldown → llena → vacía).
6. **Escala de velocidad**: relleno `alto × f`; la línea de `TOP` en `TOP / MAX` del alto.
7. **Cinta del cristal**: con 300 km/h, la marca de 300 en el centro (y = 157) y la de 350 a 85 px por encima; el triángulo de `TOP` en su sitio.
8. **Columna de daño**: bloques encendidos = nivel; parpadeo solo en 3; en TIME TRIAL, apagados y sin texto.
9. **Pilotos MASTER**: tabla de la sección 6.5 para los niveles 0 a 3 y TIME TRIAL.
10. **Parpadeo con daño**: solo elige entre `Accent_*` y `Led_*`.
11. **Radar y minimapa en militar**: con la paleta militar y sus tamaños, las clases de `radar-minimapa` dan las mismas posiciones (escaladas) y los colores de la sección 6.4; `TGT` cuenta solo las naves dentro de 120 m; `NRST` da la más cercana.
12. **Progreso de la vuelta**: fracción 0, 0,5 y 1 → barra vacía, a la mitad y llena; porcentaje 0, 50 y 100.

Las pruebas existentes siguen pasando (también las de Vector). La de captura de la Burbuja 2D se retira en la etapa 3.

### Comprobación del script de Blender

- El script falla si falta una pieza del contrato.
- Dos ejecuciones seguidas dan el **mismo** `.glb`.
- Ejecutar el de Vector después: su `.glb` **no cambia** (los cambios en `lib/` no le afectan).

## 9. Prueba manual

**Etapa 1 · Blender**
1. Ejecutar el script: termina sin errores y deja el `.glb`, el manifiesto, el panorama (o el aviso de que se reutiliza), el `.blend` y las tres capturas.
2. `blade_eye.png` al lado del prototipo (disposición 4, sin mover la vista): **la misma cabina**. Arco con retrovisores, morro largo con la sonda, cubre-instrumentos con los pilotos MASTER, cristal HUD, tres cajas que sobresalen con visera y botonera, pintura gris verdosa con desconchones.
3. Abrir el `.blend` y dar una vuelta: sin piezas sueltas, agujeros ni caras negras.
4. Ejecutar el script otra vez: mismo resultado. Ejecutar el de Vector: su `.glb` no cambia.

**Etapa 2 · Godot**
5. RACE con BLADE, cabina: se ve la cabina 3D, con el mundo por el cristal y el morro alineado con la pista.
6. El horizonte cae algo por encima del centro (vista 5,7° hacia abajo) y las tres pantallas se ven enteras.
7. Acelerar a fondo: el FOV del mundo se abre pero la cabina no se deforma.
8. Curvas, vibración y boost: la cabina se mueve como la de Vector y no marea.
9. Pantallas de prueba: `SPD` a la izquierda, `RADAR` en el centro, `MAP` a la derecha y `HUD` en el cristal, **derechas y sin espejo**. El cristal deja ver la pista a través.
10. Pilotos MASTER y placa con su texto, legibles y derechos.
11. Reflejos en el arco, el morro y el cristal; sombras del arco y las viseras.
12. C: cámara exterior sin cabina, sin inclinación y con el HUD de siempre. Volver con C.
13. F10 → VECTOR: su cabina 3D igual que antes. F10 → HAMMER: su Búnker 2D igual que antes.
14. Ventana estirada y pantalla completa: sin cortes raros.
15. Rendimiento: los FPS en cabina no bajan de forma notable (apuntar antes y después).

**Etapa 3 · HUD y luces**
16. Acelerar: número, escala y cinta del cristal suben; por encima de la punta, en ámbar. El rumbo gira en curva y el marcador de trayectoria se va al lado del giro.
17. Boost: luz naranja, `ACTIVE`, galones y celdas en ámbar, luego `COOLDOWN`.
18. Daño 1: MASTER CAUTION encendido, luces titilando. Daño 3: columna en rojo parpadeando, `CRITICAL`, `LOCKED`, MASTER WARN parpadeando y luz roja latiendo.
19. TIME TRIAL: columna apagada, pilotos MASTER apagados y nunca luz roja.
20. Con naves de prueba (F6): el radar central barre con ecos ámbar, cuenta `TGT` y da `NRST`; arco rojo y `NRST` en rojo si una va pegada. El minimapa gira contigo con las naves en ámbar.
21. La barra de progreso avanza con la vuelta y `LAP n/m` es la vuelta real.
22. Pausa con boost y daño 3: todo congelado.
23. El HUD de pantalla de velocidad, boost, daño, radar y minimapa ya no sale en la cabina de BLADE. En la cámara exterior, sí.
24. Menú principal: la versión es 0.14.5.

## 10. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Blender** | Inventario (sección 2) y sección 3 completa | Comprobación del script · Prueba manual 1–4 · Commit |
| **2 · Godot** | Secciones 4 y 5 | Pruebas 1–4 · Prueba manual 5–15 · Commit |
| **3 · HUD y luces** | Sección 6 · versión 0.14.5 | Pruebas 5–12 · Prueba manual 16–24 · Commit |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas y el `.blend`.

## 11. Fuera de alcance

- Cabina 3D de HAMMER (spec 3 de cabinas 3D).
- Casco exterior realista de cualquier nave.
- Mirar alrededor con el ratón dentro de la cabina.
- Luz del mundo dentro de la cabina (túneles, ciudad, otras naves).
- Animar piezas (botones, teclas, retrovisores con reflejo real) o sonido.
- Que los botones OSB hagan algo: son decorativos, igual que sus rótulos.
- Cambios en el HUD de las esquinas, en la cámara exterior o en los efectos de la cámara del mundo (salvo la inclinación de BLADE en cabina).
- Las disposiciones 1, 2 y 3 del prototipo.
- Menú SETTINGS.
