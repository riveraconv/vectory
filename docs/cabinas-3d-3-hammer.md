# VECTORY — Spec: Cabinas 3D · 3 · Hammer (industrial)

Versión 1.0 · 2026-11-13 · Estado: **propuesta, pendiente de validar**

Tercera y última spec de las **cabinas en 3D**. Hace la cabina de **HAMMER** en estilo **INDUSTRIAL** (chapa pintada atornillada, acero gastado, chapa lagrimada, franjas de peligro, cables y tubería), con la disposición **COLGANTE**:

- Un **bloque de pantallas colgado del techo**, como en la cabina de una grúa: **radar** a la izquierda, **casco** en el centro y **minimapa** a la derecha.
- El **salpicadero queda bajo y despejado**, con la **velocidad en una tira de segmentos** a lo ancho.
- El **boost en tres setas iluminadas** a la derecha y, a la izquierda, parada de emergencia, llave y selector (decorativos), los dos pods por debajo del labio del cubre-instrumentos.
- Bajo la tira de velocidad, un **panel inferior decorativo** con seis interruptores con LED, dos relojes pequeños (`HYD` y `TEMP`) y franja de peligro, y **dos tomas de aire** grandes justo debajo de los pods.
- **Balizas con jaula** en el larguero del techo: ámbar con el boost y roja con daño.
- Desde dentro se ve el **morro corto y ancho** en amarillo de maquinaria y el **canard** cruzando delante, con las puntas caídas.
- Pantallas **CRT de fósforo ámbar** con la fuente **VT323**.

Repite la cadena de VECTOR y BLADE: script de Blender → `.glb` → Godot con **su propia cámara** → **HUD vivo en las pantallas**. Con esta spec **las tres naves tienen cabina 3D** y la capa de cabina 2D de la spec 3b desaparece (sección 6.8).

Plan de las specs de cabinas 3D:

1. Vector · carreras (hecha).
2. Blade · militar (hecha).
3. **Hammer · industrial** (esta).
4. Después: cascos exteriores realistas de las tres naves.

Diseño de referencia:
- **`design/cabina-3d-hammer.html`**: el prototipo. Se abre en el navegador y se puede conducir. **Solo vale la disposición 2 · COLGANTE** (la que sale al abrirlo: `VARIANT = 2`). Las disposiciones 1 MANÓMETROS y 3 OPERARIO siguen en el archivo como diseño guardado, pero **su código no se traduce**.
  - **Va a Blender** todo lo que construye `buildCockpit()` con `VARIANT = 2`: la parte común (morro, canard, salpicadero, laterales, marco, balizas, cristal y limpiaparabrisas) y `buildColgante()`, con `makeCRT()`, `beacon()`, `labelPlane()` (solo el plano: el texto lo pone Godot), `hexBolt()` y `boltAt()`. También las texturas (`plateSet` ×3, `steelSet`, `treadSet`, `hazardSet`, `smudgeTex`, con `scratchMap`) y el entorno de reflejos (`buildEnv`).
  - **Va al código del juego** la lógica viva: `hudState`, `orbStyle`, `boostCol`, el dibujo de las pantallas (`base`, `crtFx`, `corners`, `T`, `ownShip`, `radarCore`, `mapCore`, `lapBar`, `drawSpeed2`, `drawRadar2`, `drawMap2`, `drawDamage2`), las setas del boost, las balizas, el parpadeo con daño, las luces de boost y daño, el brillo de las pantallas y el movimiento de la cámara de cabina. La **lógica** del radar y del minimapa (barrido, ecos, proyección, ventana) **no** se copia del prototipo: es la de la spec `radar-minimapa`, que ya está en el juego.
  - **No va a ningún sitio**: el mundo, el circuito y las naves simuladas, la conducción simulada, la mirada con el ratón, `buildManometros`, `buildOperario`, `makeGauge`, `lens`, `degRing`, `headingTape`, `stripTxt`, `drawHullTxt`, `drawBoostTxt`, `drawDial1`, `drawScope1`, `drawMap1`, `drawSpeed3`, `drawRadar3`, `drawMap3` y `drawHull3`.
  - Los nombres que se pasan a `B.mesh`/`B.group` dicen cómo se llama cada pieza en Blender.

Si algo no cuadra entre esta spec y el prototipo, **manda el prototipo** y se avisa.

Rama: `feature/cockpit-3d-hammer`, que sale de `develop` con `feature/cockpit-3d-blade` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.5 → 0.14.6** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. **No hay cambios en el documento maestro.**

---

## 1. Resumen

- **Etapa 1 · Blender**: un script nuevo, `cockpit_hammer.py`, construye la cabina reutilizando `art/blender/lib/`, la exporta a `.glb` y guarda el `.blend`, las capturas y el manifiesto. En el juego no cambia nada.
- **Etapa 2 · Godot**: la cabina 3D entra para HAMMER en la vista de cabina, con su cámara, su luz, sus reflejos y su **inclinación de 4,9°**. Sustituye al Búnker 2D. Las pantallas enseñan una **imagen de prueba**.
- **Etapa 3 · HUD y luces vivas**: las cuatro pantallas enseñan **velocidad, boost, daño, radar y minimapa**; entran las **setas del boost**, las **balizas**, el **parpadeo con daño** y las **luces de boost y daño**. Se quita el Búnker 2D y, con él, **todo lo que solo servía a la capa de cabina 2D**.
- VECTOR y BLADE **no cambian**.
- Lo que **no cambia**: la cámara exterior (con su HUD de siempre), el HUD de las esquinas (posición, vuelta y tiempos), el casco exterior de HAMMER, la pista, el entorno y los efectos de velocidad.

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **Lo que dejaron Vector y Blade** en `art/blender/lib/`: qué piezas hay ya (`poly`, `strip`, `rbox`, `beam`, `tube`, `cyl`, `sphere`, `group`, ruido, `boxUV`) y si dan lo mismo que las de este prototipo. Lo que falte (sección 3.4) se añade **sin cambiar** lo que usan Vector y Blade.
2. **`Cockpit3DView`** y la **disposición viva** de Blade: si ya es genérico (pantallas, luces y pilotos según el perfil) o queda algo escrito a fuego para Vector o Blade. Lo nuevo de Hammer son **luces de estado con color cambiante** (setas y balizas, sección 6.5): ver cómo encajan en lo que hay (los pilotos MASTER de Blade son lo más parecido).
3. **Ojo de la cámara de cabina de HAMMER**: altura sobre la pista y posición adelante/atrás actuales. El prototipo tiene el ojo a **2,03 m** sobre la pista (Vector 1,45 × 1,40, como en la 3b), FOV vertical **70°** e inclinación **−4,9°** (−0,085 rad).
4. **Capa de cabina 2D** (spec 3b): con el Búnker fuera, **qué queda que use alguien más**. En concreto:
   - El **estado del HUD de cabina** (lo usan Vector y Blade: se queda).
   - Los **tintes de los bordes** de boost y daño: dónde se dibujan hoy y si dependen de la capa 2D.
   - Primitivas 2D, piezas de cabina, escala de la capa, captura de referencia del Búnker, parpadeo de aristas de la capa: si alguien más las usa.
5. **Datos que piden las pantallas**: los mismos que Blade (vuelta actual y total, fracción de la vuelta, naves dentro de 120 m y la más cercana). Comprobar que ya están.
6. **Fuentes**: `Share Tech Mono` sigue en el proyecto; `VT323` **no** está y hay que añadirla (licencia OFL).

**Si algo es dudoso, se pregunta antes de seguir.** Lo del punto 4 se pregunta siempre antes de quitar nada.

## 3. Etapa 1 · La cabina en Blender

### 3.1 Cómo se usa Blender

Igual que Vector y Blade: Blender sin ventana, desde la terminal, con la ruta de `art/blender/run.ps1`:

```
blender -b --factory-startup --python art/blender/cockpit_hammer.py -- --out <carpeta>
```

Todo vive en el script, el `.blend` se regenera y el script es **determinista**.

### 3.2 Archivos

| Archivo | Qué es | ¿Va a Git? |
|---|---|---|
| `art/blender/cockpit_hammer.py` | Construye la cabina, exporta y guarda todo | Sí |
| `art/blender/lib/` | Piezas comunes + lo nuevo de la sección 3.4 | Sí |
| `assets/…/cockpit_hammer.glb` | Modelo exportado, con las texturas dentro | Sí |
| `assets/…/cockpit_hammer.manifest.json` | Nombres, tamaños y recuentos (sección 3.7) | Sí |
| `art/blender/out/cockpit_hammer.blend` | Escena de Blender | No (`.gitignore`) |
| `art/blender/out/previews/hammer_*.png` | Capturas de previsualización | No (`.gitignore`) |

La carpeta de `assets/` es la misma que usaron Vector y Blade.

### 3.3 Ejes, unidades y origen

Igual que Vector y Blade: metros, ejes del prototipo = ejes de Godot (X derecha, Y arriba, el piloto mira a −Z), todo dentro de un objeto raíz **`CockpitHammer`** girado **+90° en X** en Blender, con los números del prototipo copiados **tal cual**. El origen es el **ojo del piloto**.

- La **inclinación de 4,9°** **no** va en el modelo: la ponen las cámaras (sección 4.5).
- Comprobación antes de exportar (en coordenadas de Godot):
  - Punta del morro (`Nose_Tip`) en z ≈ −2,55.
  - Puntas caídas del canard (`Canard_Tip`) hasta x ≈ ±2,50 e y ≈ −1,12.
  - Bloque colgante (`Overhead`) centrado en (0, 0,205, −0,535).
  - Larguero (`Header`) en y ≈ +0,37.

### 3.4 Geometría

Se traduce `buildCockpit()` con `VARIANT = 2` **pieza a pieza**, con los mismos números y en el mismo orden. Las piezas de `B` son las de Vector y Blade, con las mismas reglas (caja redondeada de **2 segmentos** con el radio limitado como en `B.rbox`, tubo de **10 lados** por **Catmull-Rom centrípeta**, esferas de **10 × 8**, grupos con `position`, `rotation` en orden XYZ o `lookAt` que gira el **+Z** hacia el punto).

Lo nuevo respecto a Blade:

| Prototipo | En Blender |
|---|---|
| `hexBolt` | Cilindro de **6 lados**, radio `r`, alto `r × 0,8`, girado 90° en X y desplazado `r × 0,4` hacia +Z |
| `boltAt` | Grupo en el punto, con `lookAt` hacia `look`, y dentro un `hexBolt` en el origen |
| `TorusGeometry(R, r, 6, 16, arco)` | Toro con los mismos radios y segmentos; en las jaulas de las balizas, **medio toro** (arco π) |
| `SphereGeometry(0,016, 14, 10)` (lente de las balizas) | Esfera de **14 × 10** |
| Cilindros de 6, 12, 20 y 24 lados (selector, bisagras, collarines, setas) | Cilindros con esos lados |

Nota sobre `lookAt`: en el prototipo, los grupos con `look` se crean **antes** de colgar la cabina del rig, así que el punto está en coordenadas de la raíz. Los hijos del bloque colgante y de los pods se colocan **sin** `look` (heredan el giro de su grupo).

Partes de la cabina (detalle en el prototipo):

- **`Nose`**: 5 secciones de 6 puntos (`st` y `sec`) unidas por 5 tiras, la tapa de la punta, la banda central de chapa lagrimada (`Nose_Tread`), 2 juntas con sus 7 remaches cada una, el parachoques de acero (`Nose_Bumper`) y las balizas de los hombros (`Led_Nose_L`, `Led_Nose_R`).
- **`Canard`**: por cada lado, cara de arriba (`Canard_Top`) y de abajo (`Canard_Bottom`), borde de ataque con franjas (`Canard_LeadingEdge`), borde de salida, punta caída (`Canard_Tip` y `Canard_TipInner`), su baliza (`Led_Canard_L` / `Led_Canard_R`), 4 remaches y la junta (`Canard_Seam`).
- **`Dash`**: frontal (`Dash_FacePanel`), cubre-instrumentos de chapa lagrimada (`Glare_Top`, `Glare_Lip`, `Glare_Under`, `Glare_Edge`) con las curvas `cowlY` y `cowlZ` y los 9 tornillos del labio. **Sin** luz bajo el labio: `Accent_Glare` del prototipo solo existe en las disposiciones 1 y 3.
- **`Side_L` / `Side_R`**: mejilla, tapa de la mejilla, alféizar, pared, pilar A con sus tornillos, tira exterior, carril del techo, cable y cable con funda (amarilla a la izquierda, roja a la derecha), asidero y tubería con abrazaderas.
- **`Frame`**: larguero (`Header`) con sus franjas (`Header_Hazard`), 13 tornillos y la placa (`Header_Plate` → `Plate`), techo (`Roof`), las dos balizas con jaula (`Beacon_Boost`, `Beacon_Damage`), cristal (`Glass_Front`), manchas (`Glass_Smudge`) y limpiaparabrisas (`Wiper_Pivot`, `Wiper_Arm`, `Wiper_Blade`).
- **`Console`** (`buildColgante`):
  - **`Overhead`**: carcasa de 0,72 × 0,19 × 0,10 m, 6 aletas de acero, 4 tornillos y tres CRT. Dos brazos con bisagra y dos cables lo cuelgan del larguero.
  - **`Screen_Speed_Panel`**: CRT a lo ancho del salpicadero, **sin** línea de luz debajo (`accent: false`).
  - **`Boost_Pod`** (derecha, en (0,372, −0,288, −0,474), mirando a (0,372, 0,10, 0,20)): placa con 4 tornillos y tres setas (`Boost_Button_1…3`), cada una con collarín de acero, casquete luminoso (`Lamp_Boost_n`) y rótulo.
  - **`Aux_Pod`** (izquierda, simétrico): placa con 4 tornillos, parada de emergencia (anillo amarillo y seta roja), llave, selector de 6 caras y rótulo.
  - **`Lower_Panel`** (bajo la tira, en (0, −0,362, −0,472)): placa de 0,46 × 0,055 m con 4 tornillos y franja de peligro abajo; seis interruptores (`Panel_Toggle_1…6`, base, palanca inclinada alternando arriba y abajo y su LED `Led_Panel_n`); dos relojes pequeños (`miniGauge`: `Gauge_Hyd` a la izquierda y `Gauge_Temp` a la derecha, radio 0,019 m, bisel de acero, esfera y cristal).
  - **`Dash_Vent_L` / `Dash_Vent_R`** (justo debajo de cada pod, en (±0,372, −0,36, −0,476), mirando a (±0,372, 0,10, 0,20)): toma de aire de 0,205 × 0,068 m con marco, fondo, 6 lamas de acero inclinadas 0,6 rad y 4 tornillos.

Cada CRT (`makeCRT`) lleva: carcasa de chapa (`_Housing`), cuello trasero de acero (`_Neck`), junta de goma (`_Gasket`), 4 tornillos, superficie (`Screen_*`), cristal (`_Glass`), sombra (`_Shade`) y línea de luz (`Accent_*`), salvo la tira de velocidad, que no lleva línea de luz.

| Pantalla | Grupo y posición | Mira hacia | Pantalla (m) | Fondo · bisel (m) |
|---|---|---|---|---|
| `Screen_Radar` | dentro de `Overhead`, (−0,228, 0, 0) | la del `Overhead` | 0,17 × 0,13 | 0,04 · 0,016 |
| `Screen_Damage` | dentro de `Overhead`, (0, 0, 0) | la del `Overhead` | 0,12 × 0,12 | 0,04 · 0,014 |
| `Screen_Map` | dentro de `Overhead`, (+0,228, 0, 0) | la del `Overhead` | 0,17 × 0,13 | 0,04 · 0,016 |
| `Screen_Speed` | (0, −0,262, −0,47) | (0, 0,02, 0,12) | 0,40 × 0,075 | 0,06 · 0,022 |

El `Overhead` está en (0, 0,205, −0,535) y mira hacia (0, −0,25, 0,25).

- **Caras por los dos lados**: el prototipo dibuja todo con `DoubleSide`. Como en Vector y Blade, los materiales van **con doble cara** salvo que el agente compruebe que una pieza solo se ve por un lado.
- **Sombreado**: plano en polígonos y tiras; suave en tubos, esferas, cilindros, toros y redondeos.
- Las piezas con `noShadow` **no proyectan sombra** (pantallas, sombras de pantalla, cristales, luces, LEDs, setas, lentes de las balizas, tornillos, remaches, juntas, rótulos, placa y techo). Se apuntan en el manifiesto y se aplican en Godot.

### 3.5 Materiales y texturas

- Como en Vector y Blade: **Principled BSDF** con **texturas de imagen**, sin nodos procedurales.
- Texturas generadas **en Python** con el **mismo algoritmo y las mismas semillas** del prototipo, a **512 × 512**, guardadas como PNG: color (sRGB), rugosidad y normal (no color, convención OpenGL). `mkRng` enmascarado a 32 bits sin signo en cada paso. Los generadores usan **solo aritmética sobre arrays** (no hay dibujo de canvas), así que se pueden copiar tal cual.
- **Ojo con los redondeos**: `scratchMap` usa `Math.round`, que redondea las mitades **hacia arriba** (−2,5 → −2; 2,5 → 3). En Python hay que usar `math.floor(x + 0.5)`, no `round()`. Los módulos negativos se corrigen igual que en el prototipo (`((a % N) + N) % N`).
- El orden de llamadas al generador importa: cada conjunto pide sus ruidos **en el mismo orden** que el prototipo (en `plateSet`: `m`, `grime`, `chip`, `fine` y después los arañazos).

| Conjunto | Semilla | Qué es |
|---|---|---|
| `plateSet(81, [84, 88, 76])` | 81 | Chapa gris oliva del interior: paneles de 256 px con filas desplazadas 128, juntas, remaches, pocos desconchones, 50 arañazos y algo de mugre |
| `plateSet(82, [50, 52, 48])` | 82 | Chapa oscura de marcos y carcasas |
| `plateSet(87, [170, 132, 40])` | 87 | Amarillo de maquinaria del morro y el canard |
| `steelSet(83)` | 83 | Acero desnudo gastado (rayado en filas, manchas, 90 arañazos suaves) |
| `treadSet(84)` | 84 | Chapa lagrimada (diamante en relieve, celdas de 32 px con orientación alterna) |
| `hazardSet(85)` | 85 | Franjas amarillas y negras poco gastadas |
| `smudgeTex(86)` | 86 | Manchas del cristal |

- **Repetición**: UV = **metros** con la regla `boxUV`, **multiplicadas por la repetición** del material (sin `KHR_texture_transform`).

| Material | Base | Metal | Rugosidad | Repetición | Extra |
|---|---|---|---|---|---|
| `M_Plate` | chapa oliva | 0,45 | textura | ×1,2 | — |
| `M_PlateDark` | chapa oscura | 0,55 | textura | ×1,6 | — |
| `M_PaintNose` | amarillo | 0,35 | textura | ×0,9 | Reflejos ×0,6 |
| `M_Steel` | acero | 1 | textura | ×2 | — |
| `M_Tread` | chapa lagrimada | 0,9 | textura | ×4 | — |
| `M_Hazard` | franjas | 0,3 | textura | ×6 | — |
| `M_Rubber` | `#111111` | 0 | 0,6 | — | Juntas, cables, selector |
| `M_Dark` | `#0B0B0A` | 0 | 0,9 | — | — |
| `M_SleeveR` / `M_SleeveY` | `#6A1410` / `#8A6A10` | 0 | 0,6 | — | Fundas de cable, asidero y anillo de la parada |
| `M_RedKnob` | `#A01810` | 0,05 | 0,45 | — | Seta de parada de emergencia |
| `M_Emissive_Accent` | negro + emisión `#FFA436` | 0 | 0,4 | — | Una instancia por `Accent_*` |
| `M_Emissive_Nav` | negro + emisión `#FF9A1A` | 0 | 0,4 | — | Una instancia por `Led_*` |
| `M_Beacon` | `#1A0D04` + emisión | 0 | 0,4 | — | Ámbar `#FF9A1A` (boost) y roja `#FF2A10` (daño), una instancia por baliza |
| `M_Lamp` | `#2A1A06` + emisión (Godot cambia el color) | 0 | 0,4 | — | Una instancia por seta |
| `M_Screen` | negro (la textura la pone Godot) | 0 | 1 | — | Las cuatro pantallas |
| `M_ScreenShade` | negro al 70 % | 0 | 1 | — | Transparente |
| `M_CrtGlass` | negro al 14 % | 0 | 0,35 | — | Transparente, reflejos ×1,4. Rugosidad alta a propósito: con 0,05 la luz de cada pantalla dejaba un punto brillante en su propio cristal (sección 4.3) |
| `M_Glass` | negro al 6 % | 0 | 0,05 | — | Transparente, reflejos ×1,3 |
| `M_GlassSmudge` | textura de manchas | 0 | 1 | — | Godot la pone en modo **suma**, opacidad 0,06 |
| `M_Emissive_PanelLed` | negro + emisión | 0 | 0,4 | — | Una instancia por `Led_Panel_n`: verde `#7CFF3A` (1, 3, 4, 6) y ámbar `#FFA020` (2, 5) |
| `M_Label` | negro (la textura la pone Godot) | 0,3 (0,1 en las esferas) | 0,6 | — | Placa, rótulos y esferas de los relojes pequeños |

- Reflejos, modo suma y transparencias que glTF no lleva se apuntan en el manifiesto y los aplica Godot.
- Cada pieza viva (sección 3.7) tiene su **propio material o instancia**, para que Godot pueda encenderla, apagarla o cambiarle el color sola.
- Los **textos fijos** (placa y rótulos) **no** se generan en Blender: el modelo lleva el plano con UV 0–1 y Godot le pone la textura (sección 4.6).

### 3.6 Panorama de reflejos

`buildEnv()` es **el mismo** que el de Blade (mismo algoritmo y semilla 77). Se **reutiliza** el panorama de Blade (o el de Vector, si Blade ya reutilizó ese) y no se genera uno nuevo. Si el agente ve alguna diferencia, lo genera como `cockpit_hammer_env.png` y avisa.

### 3.7 Nombres de las piezas (contrato con Godot)

Godot busca estas piezas **por nombre**. El script comprueba antes de exportar que **están todas** y, si falta una, **falla con un mensaje claro**.

| Nombre | Qué es | Cuántas |
|---|---|---|
| `CockpitHammer` | Raíz (origen = ojo) | 1 |
| `Screen_Radar`, `Screen_Map` | Superficie del radar y del minimapa: **0,17 × 0,13 m**, UV **0–1 entero**, U a la derecha y V arriba vistas por el piloto | 2 |
| `Screen_Damage` | Superficie del casco: **0,12 × 0,12 m**, misma orientación | 1 |
| `Screen_Speed` | Superficie de la tira de velocidad: **0,40 × 0,075 m**, misma orientación | 1 |
| `Screen_*_Shade` | Sombra interior de cada pantalla | 4 |
| `Accent_Radar`, `Accent_Damage`, `Accent_Map` | Línea de luz bajo cada pantalla del bloque colgante | 3 |
| `Led_Nose_L`, `Led_Nose_R` | Balizas de los hombros del morro | 2 |
| `Led_Canard_L`, `Led_Canard_R` | Balizas de las puntas del canard | 2 |
| `Lamp_Boost_1`, `Lamp_Boost_2`, `Lamp_Boost_3` | Casquete luminoso de cada seta, de **izquierda a derecha** | 3 |
| `Beacon_Boost_Lens`, `Beacon_Damage_Lens` | Lente de cada baliza del larguero | 2 |
| `Plate` (dentro de `Header_Plate`) | Placa: plano de **0,13 × 0,016 m**, UV 0–1 | 1 |
| `Label_Boost_1`, `Label_Boost_2`, `Label_Boost_3` | Rótulo de cada seta (en el prototipo, `Button_Label` dentro de `Boost_Button_n`): plano de **0,034 × 0,010 m**, UV 0–1 | 3 |
| `Label_Aux` | Rótulo del pod izquierdo (en el prototipo, `Aux_Label`): plano de **0,17 × 0,011 m**, UV 0–1 | 1 |
| `Led_Panel_1` … `Led_Panel_6` | LEDs del panel inferior, de izquierda a derecha | 6 |
| `Gauge_Hyd_Face`, `Gauge_Temp_Face` | Esfera de cada reloj pequeño: círculo de radio **0,019 m**, UV 0–1 | 2 |
| `Glass_Front`, `Glass_Smudge` | Cristal y manchas | 2 |
| `Screen_*_Glass` | Cristal de cada CRT | 4 |

Los rótulos se **renombran** en Blender para que sean únicos. El resto se agrupa por zonas (`Nose`, `Canard`, `Dash`, `Side_L`, `Side_R`, `Frame`, `Console`), con los nombres del prototipo dentro.

**Manifiesto** (`cockpit_hammer.manifest.json`), con el mismo formato que el de Vector y Blade:
- Versión de Blender y fecha.
- Piezas vivas de la tabla, con su tamaño (pantallas, placa y rótulos) y su color de emisión (luces, setas y balizas).
- Piezas sin sombra, materiales con reflejos distintos de 1, materiales en modo suma y materiales transparentes con su opacidad.
- Triángulos totales y caja que ocupa la cabina (en coordenadas de Godot).

### 3.8 Exportación

Igual que Vector y Blade: `.glb`, +Y arriba, modificadores aplicados, normales, UV y texturas **dentro**, sin cámaras, luces ni animaciones, y solo lo que cuelga de `CockpitHammer`.

### 3.9 Capturas de previsualización

En `art/blender/out/previews/`:
1. `hammer_eye.png`: desde el ojo, **FOV vertical 70°** e **inclinación −4,9°**, 1280 × 720, con luz parecida a la del prototipo. Es la que se compara con el prototipo.
2. `hammer_three_quarter.png`: desde atrás, arriba y a un lado.
3. `hammer_overhead.png`: de cerca, al bloque colgante con sus tres CRT, sus brazos y sus bisagras.

Motor: el mismo que con Vector y Blade. El agente **mira las capturas** antes de cerrar la etapa y las compara con el prototipo abierto en el navegador (sin mover la vista).

## 4. Etapa 2 · La cabina en Godot

### 4.1 Importación

Igual que Vector y Blade: nombres tal cual, sin colisiones, sin LOD ni luz horneada. Al cargar, el script de la cabina aplica lo del manifiesto (sin sombra, reflejos, suma, transparencias). Si algo no llega bien, se arregla **en Godot con su material**, sin tocar la geometría, y se avisa.

### 4.2 Cámara propia

La misma estructura que Vector y Blade (`SubViewport` con su mundo, fondo transparente, tamaño de la pantalla, deja de dibujarse en la cámara exterior):

- `Camera3D` en el origen, **FOV vertical 70°**, cercano **0,02**, lejano **30**, con la **inclinación de la sección 4.5**.
- `Environment`: cielo con el panorama de reflejos, tonemap **ACES**, exposición **1,05**, glow como el de Vector y Blade (fuerza 0,55, radio 0,45, umbral 0,8).
- Se pinta en el sitio de la capa de cabina: por debajo del HUD de las esquinas, el panel de pruebas y la pausa.

**Ojo con el umbral del glow**: las setas del boost están ajustadas para quedar **justo por encima** del umbral (sección 6.5). Si el glow de Godot no se comporta igual que el del prototipo y las setas no brillan o deslumbran, se ajusta su intensidad, **no** el glow de la cabina, y se avisa.

### 4.3 Luces de la cabina

Valores del prototipo como **punto de partida**. Se ajustan **a ojo comparando con el prototipo** y se apuntan los valores finales.

| Luz | Tipo | Posición | Color · Intensidad en el prototipo | Notas |
|---|---|---|---|---|
| Principal | Direccional | desde (0,7, 2,2, −1,6) hacia (0, −0,3, −0,4) | `#FFE2B8` · 2,2 | Con sombras, suaves |
| Ambiente | Del cielo + color | — | hemisferio `#9A8F80` / `#15110C` · 0,55; ambiente `#2C241A` · 0,5 | Más cálida que Blade |
| Lámpara | Omni | (0, 0,32, −0,25) | `#FFB870` · 0,9 · alcance 1,6 · caída 1,4 | Luz de cabina, más fuerte que en Blade |
| Pantallas (×4) | Omni | 6 cm delante de cada pantalla | `#FFB000` · `brillo × 0,03 × área / 0,017` · alcance 0,9 | `área` = ancho × alto de la pantalla. **No** debe dejar un punto de brillo en el cristal de su CRT: si con la rugosidad de `M_CrtGlass` aún se ve, se saca el cristal de la máscara de esa luz (`light_cull_mask`) |
| Boost delantera | Omni | (0, 0,25, 0,45) | `#FF9D32` · `tinte × 3,2` · alcance 3,5 | Etapa 3 |
| Boost trasera | Omni | (0, −0,2, −1,8) | `#FF9D32` · `tinte × 2,0` · alcance 4 | Etapa 3 |
| Daño | Omni | (0, 0,1, −0,35) | `#FF4D3A` · `daño × 1,2` · alcance 2,5 | Etapa 3 |

### 4.4 Movimiento de la cámara de cabina

**El mismo que Vector y Blade**, con el mismo código y los mismos parámetros, leyendo los datos de cámara del juego. Se suma a la inclinación de la sección 4.5.

### 4.5 Ojo, inclinación y encuadre

- La cabina se diseñó con el ojo a **2,03 m** de la pista (Vector × 1,40, como en la 3b), FOV 70° y la vista **4,9° hacia abajo** (−0,085 rad). Así el bloque colgante se ve entero arriba y, abajo, la tira de velocidad, los pods, las tomas de aire y el panel inferior.
- La inclinación va en las **dos cámaras** de la vista de cabina de HAMMER (mundo y cabina), **iguales**, como en Blade.
- Se guarda en el **perfil visual de HAMMER**: altura 2,03 m e inclinación −4,9°. VECTOR queda con 0 y BLADE con −5,7° (no cambian).
- La cámara exterior **no** se inclina.
- Si el ojo actual de HAMMER (punto 3 del inventario) es muy distinto y el morro, el canard o el bloque colgante se ven de más o de menos, **se avisa con capturas** antes de tocar nada.

### 4.6 Pantallas de prueba y textos fijos

- Cada pantalla enseña una **imagen de prueba** generada por código: fondo `#0B0703`, las líneas de barrido y las esquinas de `base` y `corners`, y el texto `SPD`, `RADAR`, `HULL` o `MAP` grande en el centro, en ámbar. Sirve para comprobar la **orientación**, el **tamaño** y el **brillo**.
- Tamaños de los `SubViewport`: `Screen_Speed` **768 × 144**, `Screen_Radar` y `Screen_Map` **384 × 294**, `Screen_Damage` **300 × 300**.
- Las cuatro: **sin luz** (unshaded), sin sombras, con brillo **×1,35 × (0,8 + 0,4 × brillo)** (sección 6.1).
- **Textos fijos**, dibujados **una vez** al cargar, como `labelPlane` del prototipo: fondo `#16130D`, texto `#CFC3A4` en `Share Tech Mono`, centrado, de alto = **62 %** del lienzo. Lienzo de 256 px de ancho y alto = `max(16, redondeo(256 × alto / ancho))`:
  - `Plate`: 256 × 32, `HAMMER · HMR-03`.
  - `Label_Boost_1…3`: 256 × 75, `BST 1`, `BST 2`, `BST 3`.
  - `Label_Aux`: 256 × 17, `E-STOP    IGN    MODE` (con los espacios del prototipo).
- **Esferas de los relojes pequeños** (`Gauge_Hyd_Face`, `Gauge_Temp_Face`), dibujadas **una vez** como `miniGauge` del prototipo: 128 × 128, fondo `#100E0A`, arco rojo `#C8321E` en el último 20 %, 9 marcas `#E9DFC6`, rótulo `HYD` / `TEMP` y **aguja fija** `#FF7A1A` al 62 % y al 38 %. Son decorativas: no se mueven.

### 4.7 Qué se ve en cada caso

| | VECTOR · cabina | BLADE · cabina | HAMMER · cabina | Cualquier nave · exterior |
|---|---|---|---|---|
| Cabina | 3D carreras | 3D militar | **3D industrial** | No |
| Velocidad y boost | Pantallas 3D | Cristal HUD y pantalla izquierda | **Tira de velocidad y setas** | HUD de pantalla |
| Daño | Pantalla 3D | Pantalla izquierda | **Pantalla central del bloque colgante** | HUD de pantalla |
| Radar y minimapa | Pantallas pequeñas 3D | Pantallas central y derecha | **Pantallas del bloque colgante** | HUD de pantalla |
| Posición, vuelta y tiempos | Esquinas | Esquinas | Esquinas | Esquinas |

En la etapa 2, mientras las pantallas son de prueba, HAMMER en cabina enseña **también el HUD de pantalla** de velocidad, boost, daño, radar y minimapa, para poder conducir y probar. Se quita en la etapa 3.

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lo de Vector y Blade **no cambia de aspecto**:

- **Perfil visual de nave**: HAMMER → 3D industrial, con su `.glb`, su manifiesto, su estilo, su disposición viva y su ojo con inclinación. Ya no queda ninguna nave con cabina 2D.
- **`Cockpit3DView`**: sin cambios si ya es genérico tras Blade (punto 2 del inventario). Si hay que tocarlo para las luces de estado, se generaliza sin cambiar cómo se ven Vector y Blade.
- **Disposición viva de Hammer** (pura): sus 4 pantallas con su tamaño y su dibujante, sus `Accent_*` y `Led_*`, las 3 setas del boost y las 2 balizas.
- **Luces de estado** (pura): dado el estado del HUD, dicen **color e intensidad** de cada seta y de cada baliza (sección 6.5). Si los pilotos MASTER de Blade ya tienen algo parecido, se reutiliza.
- **Paleta del estilo industrial** (pura), centralizada junto a las de carreras y militar:

| Clave | Color | Uso |
|---|---|---|
| `main` | `#FFB000` | Fósforo ámbar: trazos, números, velocidad normal, carga llena |
| `dim` | `#7A520C` | Anillos del radar, bordes en TIME TRIAL |
| `label` | `#C98A12` | Rotulaciones, bordes, escalas |
| `over` | `#FF5A1F` | Naranja rojizo: por encima de `TOP` o con boost, carga en cooldown |
| `ok` / `warn` / `crit` | `#9CFF3A` / `#FFE04A` / `#FF3B1F` | Daño 0 / 1 / 2–3, nave pegada, carga bloqueada |
| `rival` | `#FF5A1F` | Rivales en radar y minimapa |
| `frame` | `#FFB000` | Esquinas de las pantallas |
| `bar` | `#3A2608` | Fondo de barras y segmentos apagados |
| `bg` | `#0B0703` | Fondo de las pantallas |
| `orbOff` | `#4A320A` | Carga vacía |

  Y los colores de luz: boost `#FF9D32`, daño `#FF4D3A`, acentos `#FFA436`, balizas `#FF9A1A` (ámbar) y `#FF2A10` (roja), luz de pantallas `#FFB000`, seta apagada `#3A2A10`.
- **Dibujantes de las pantallas de Hammer** (puros): piezas que dan primitivas 2D a partir del estado (como en Vector y Blade), una por pantalla, más las comunes `base`, `crtFx`, `corners`, `ownShip` y `lapBar`.
- **Fuente**: `VT323` (OFL) en la carpeta de fuentes, con su licencia.

## 6. Etapa 3 · HUD y luces vivas

### 6.1 Estado

Se **reutiliza el estado del HUD de cabina** (como Vector y Blade): `TOP`, `MAX`, `f`, color de la velocidad, cargas con su prioridad, textos del boost y del daño, TIME TRIAL sin daño, transiciones de 0,25 s y **parpadeo del daño a 2 Hz**. **No se duplica.** Lo que cambia es cómo se pinta:

| Dato | Color en industrial |
|---|---|
| Velocidad normal / por encima de `TOP` o con boost | `main` / `over` |
| Daño 0 / 1 / 2–3 | `ok` / `warn` / `crit` |
| Texto del boost | `crit` si es `LOCKED`, `over` con boost activo, `main` si no |

- `brillo` = 0,35 + 0,35 × `f` (el de Vector y Blade).
- Datos de vuelta, naves cercanas y la más cercana: los mismos que Blade.

### 6.2 Efecto CRT (común a las cuatro pantallas)

- `base`: fondo `bg` y una línea de barrido `main` al **7 %** de 1 px cada 3 px.
- `crtFx` (al final de cada pantalla): **viñeta** radial (de transparente en el 30 % del lado menor a negro al 65 % en el 62 % del lado mayor) y una **banda** de 6 px, `main` al 5 %, que baja a **60 px/s** y vuelve a empezar arriba. La banda usa el tiempo de la cabina: la pausa la congela.
- `corners`: esquinas en `frame` al 75 %, 2,5 px, de 18 px a 8 px del borde (en la tira de velocidad, 14 px a 6 px).
- Textos en **`VT323`**, con halo (`shadowBlur`) aproximado como en Vector y Blade.

### 6.3 Tira de velocidad (`Screen_Speed`, 768 × 144)

Como `drawSpeed2` del prototipo: mismas posiciones, tamaños y textos.

- `SPD` arriba a la izquierda; `BST` debajo y el **texto del boost** a su derecha, con el color de la tabla 6.1.
- **Número** de la velocidad grande (112 px) alineado a la derecha en x = 610, con `KM/H` a su derecha.
- **48 segmentos** de x 24 a 744 (y 104, alto 24, separación 3): se encienden `redondeo(48 × f)` en el color de la velocidad, con halo; el resto en `bar`.
- **Marca de `TOP`** en `over`, 3 px, en `TOP / MAX` del ancho.
- Se redibuja **cada frame** (por la banda del CRT).

### 6.4 Bloque colgante

- **Radar** (`Screen_Radar`, 384 × 294), como `drawRadar2`. La **lógica** es la de la spec `radar-minimapa`; aquí solo cambian el **tamaño**, la **paleta** y lo de alrededor:
  - `RDR` arriba a la izquierda y `120 M` arriba a la derecha.
  - Círculo en (192, 158), radio **112**: anillos y cruz `dim`, diagonales, haz `main` (28 líneas de 2,5 px, de 0,38 a 0), ecos **cuadrados** de **6,05 px** en `rival` o **7,7 px** en `crit` si la nave estaba pegada, arco de nave pegada en `crit` (7 px, radio + 8) y tu nave de **12,1 px**.
  - Abajo: `TGT` con el número de naves **dentro de los 120 m** (dos cifras) a la izquierda y la distancia a la **más cercana** a la derecha (tres cifras + `M`, `rival`; `crit` si está pegada; `---` si no hay).
- **Casco** (`Screen_Damage`, 300 × 300), como `drawDamage2`:
  - `HULL` arriba.
  - Tres sectores de 220 × 46 px (de abajo arriba: `SECT I`, `SECT II`, `SECT III`): se encienden tantos como el nivel, rellenos del color del daño con halo y el rótulo en `bg`; con nivel 3, parpadeando (100 % / 25 %, 2 Hz). Apagados: borde `label` al 60 % y rótulo `label`.
  - Texto del daño abajo, en su color.
  - En **TIME TRIAL**: sectores apagados con borde `dim` y sin texto.
- **Minimapa** (`Screen_Map`, 384 × 294), como `drawMap2`:
  - `MAP` y `TRK UP` arriba. Ventana en (192, 146), radio **104**: banda `label` de 12 px al 35 %, línea `main` de 3 px al 90 %, meta en `crit` de 4 px, naves como **cuadrados** de 10 px en `rival` y tu nave de 11 px.
  - **Progreso de la vuelta**: barra (x 104–280, y 262, alto 10) con la fracción de la vuelta, `LAP n/m` a la izquierda y el porcentaje a la derecha.
- Las tres se redibujan **cada frame**.

### 6.5 Setas del boost y balizas

**Setas** (`Lamp_Boost_1…3`, una por carga, de izquierda a derecha), según la carga de la tabla de cargas del estado:

| Carga | Color de emisión | Intensidad |
|---|---|---|
| Llena | `main` `#FFB000` | **1,7** (justo por encima del umbral del glow: halo suave) |
| Cooldown | `over` `#FF5A1F` | 0,6 (encendida, sin halo) |
| Bloqueada (daño 3) | `crit` `#FF3B1F` | 2,0 |
| Vacía | `#3A2A10` | 0,25 |

**Balizas** del larguero:
- **Ámbar** (`Beacon_Boost_Lens`): con el boost activo, **4 + 1,5 × sen(20 × t)** (late rápido); si no, 0,05.
- **Roja** (`Beacon_Damage_Lens`): con daño 2, fija a **5**; con daño 3, a 5 con el **parpadeo del daño (2 Hz)** y a 0,05 en la otra mitad; si no, 0,05. En TIME TRIAL, siempre 0,05.

Las setas y las balizas **no** titilan con el parpadeo de daño (6.6). La pausa las congela.

### 6.6 Parpadeo con daño

- Desde el nivel 1, las luces `Accent_*` y `Led_*` titilan bajando al **20 %** de su intensidad, con las **reglas de la spec `danos`** (como en Vector y Blade).
- Intensidad base (emisión, se ajusta a ojo): `Accent_Radar/Damage/Map` **1,4**, `Led_Nose_L/R` y `Led_Canard_L/R` **3**, `Led_Panel_1…6` **1,5** (siempre encendidos, solo cambian al titilar).
- Las pantallas, las setas y las balizas **no** titilan.

### 6.7 Luz de boost y daño

Igual que en Vector y Blade: `tinte` hacia 0,55 con boost, 0,25 por encima de `TOP` y 0 si no, en unos 0,1 s; `daño` 0,15 fijo con nivel 2 y latiendo entre 0,45 y 0,20 cada 1 s con nivel 3. Cada luz con su **intensidad en el Inspector**; a 0, apagada. En TIME TRIAL nunca hay luz de daño. Los tintes de los bordes siguen igual (ver 6.8). La pausa congela parpadeos y latidos.

### 6.8 Fuera la cabina 2D

Al cerrar la etapa 3, si el usuario valida la 3D:

- Se **quita** el Búnker 2D y su prueba de captura (el diseño sigue guardado en `design/`).
- Con el Búnker fuera **ninguna nave usa la capa de cabina 2D**. Se quita también lo que **solo** servía a esa capa (primitivas 2D, piezas de cabina, escala de la capa, parpadeo de aristas de la capa…), según el punto 4 del inventario, **preguntando antes**.
- **Se quedan**: el estado del HUD de cabina (lo usan las tres cabinas 3D) y los **tintes de los bordes** de boost y daño. Si hoy los tintes viven dentro de la capa 2D, se sacan a una capa propia, ligera, que solo dibuja los tintes, y se avisa.
- No se deja código muerto.

## 7. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Altura e inclinación del ojo de HAMMER | 2,03 m · −4,9° | Perfil visual |
| FOV de la cámara de cabina 3D | 70 | Cabina 3D (común) |
| Balanceo · vibración · contragiro · empuje | Los de Vector y Blade | Cabina 3D (común) |
| Intensidad de cada luz (principal, lámpara, pantallas, boost, daño) | La ajustada en la etapa 2 | Cabina de HAMMER |
| Brillo de las pantallas | 1,35 | Cabina de HAMMER |
| Intensidad de las setas (llena / cooldown / bloqueada / vacía) | 1,7 / 0,6 / 2,0 / 0,25 | Cabina de HAMMER |
| Intensidad de las balizas (encendida / apagada) | 4 ± 1,5 y 5 / 0,05 | Cabina de HAMMER |

Tiempos y colores como **constantes con nombre**.

## 8. Pruebas

### Pruebas unitarias mínimas

1. **Manifiesto**: el de `cockpit_hammer.manifest.json` tiene todas las piezas de la tabla 3.7 con las cantidades exactas; las pantallas, la placa y los rótulos miden lo de la tabla (±1 mm).
2. **Perfil visual**: VECTOR → 3D carreras, inclinación 0; BLADE → 3D militar, ojo 0,90 m, −5,7°; HAMMER → 3D industrial, ojo 2,03 m, −4,9°. Ninguna nave con cabina 2D.
3. **Qué se ve en cada caso** (tabla 4.7), incluida la regla temporal de la etapa 2.
4. **Inclinación**: en la vista de cabina de HAMMER, la cámara del mundo y la de la cabina tienen la **misma** inclinación; en la exterior, ninguna.
5. **Pintado en industrial**: colores de la tabla 6.1 para la velocidad, el daño y el texto del boost.
6. **Tira de velocidad**: con `f` = 0, 0,5 y 1 → 0, 24 y 48 segmentos encendidos; la marca de `TOP` en `TOP / MAX` del ancho.
7. **Casco**: sectores encendidos = nivel; parpadeo solo en 3; en TIME TRIAL, apagados con borde `dim` y sin texto.
8. **Setas**: tabla 6.5 para llena, cooldown, bloqueada y vacía, en el orden de prioridad de las cargas (bloqueada → cooldown → llena → vacía).
9. **Balizas**: ámbar solo con boost; roja fija con daño 2, parpadeando con daño 3, apagada con 0–1 y en TIME TRIAL.
10. **Parpadeo con daño**: solo elige entre `Accent_*` y `Led_*`.
11. **Radar y minimapa en industrial**: con la paleta industrial y sus tamaños, las clases de `radar-minimapa` dan las mismas posiciones (escaladas) y los colores de la sección 6.4; `TGT` cuenta solo las naves dentro de 120 m; la distancia es la de la más cercana.
12. **Progreso de la vuelta**: fracción 0, 0,5 y 1 → barra vacía, a la mitad y llena; porcentaje 0, 50 y 100.

Las pruebas existentes siguen pasando (también las de Vector y Blade). Las que solo probaban la capa 2D se retiran con ella (6.8).

### Comprobación del script de Blender

- El script falla si falta una pieza del contrato.
- Dos ejecuciones seguidas dan el **mismo** `.glb`.
- Ejecutar los de Vector y Blade después: sus `.glb` **no cambian** (los cambios en `lib/` no les afectan).
- **Texturas**: una prueba compara unos cuantos píxeles de cada textura generada en Python con los mismos píxeles generados en el navegador por el prototipo (el agente los saca ejecutando los generadores con Node). Sirve para pillar diferencias de redondeo en `scratchMap`.

## 9. Prueba manual

**Etapa 1 · Blender**
1. Ejecutar el script: termina sin errores y deja el `.glb`, el manifiesto, el `.blend` y las tres capturas (y el aviso de qué panorama se reutiliza).
2. `hammer_eye.png` al lado del prototipo (sin mover la vista): **la misma cabina**. Morro amarillo corto con el canard cruzando delante y sus puntas caídas, bloque colgante con sus tres CRT, tira de velocidad, pods con setas y parada de emergencia por debajo del labio, panel inferior con interruptores y relojes pequeños, tomas de aire bajo los pods, **sin** líneas ámbar en el salpicadero, balizas con jaula, pilares atornillados, cables y tubería, chapa con **poco desgaste**.
3. Abrir el `.blend` y dar una vuelta: sin piezas sueltas, agujeros ni caras negras. Los brazos del bloque colgante llegan al larguero.
4. Ejecutar el script otra vez: mismo resultado. Ejecutar los de Vector y Blade: sus `.glb` no cambian.

**Etapa 2 · Godot**
5. RACE con HAMMER, cabina: se ve la cabina 3D, con el mundo por el cristal y el morro alineado con la pista.
6. El bloque colgante se ve entero arriba; la tira de velocidad, los dos pods (sin mezclarse con el labio del cubre-instrumentos) con sus tomas de aire debajo y el panel inferior, abajo.
7. Acelerar a fondo: el FOV del mundo se abre pero la cabina no se deforma.
8. Curvas, vibración y boost: la cabina se mueve como las de Vector y Blade y no marea.
9. Pantallas de prueba: `RADAR`, `HULL` y `MAP` en el bloque colgante (de izquierda a derecha) y `SPD` en la tira, **derechas y sin espejo**.
10. Placa y rótulos legibles y derechos.
11. Reflejos en el morro, el acero y los cristales de los CRT; sombras del bloque colgante y de los pilares. **Ningún punto brillante fijo** en el cristal de las pantallas (se confunde con una nave en el radar y el minimapa).
12. C: cámara exterior sin cabina, sin inclinación y con el HUD de siempre. Volver con C.
13. F10 → VECTOR y BLADE: sus cabinas 3D igual que antes.
14. Ventana estirada y pantalla completa: sin cortes raros.
15. Rendimiento: los FPS en cabina no bajan de forma notable (apuntar antes y después).

**Etapa 3 · HUD y luces**
16. Acelerar: el número y los segmentos suben; por encima de la punta, en naranja rojizo. Las pantallas tienen la viñeta y la banda del CRT.
17. Setas con las tres cargas: se ven **encendidas con un halo suave**, sin deslumbrar.
18. Boost: luz naranja en la cabina, baliza ámbar latiendo, `ACTIVE`, la seta gastada pasa a vacía y las demás a cooldown; luego `COOLDOWN`.
19. Daño 2: baliza roja fija y sectores I y II en rojo. Daño 3: sector III, `CRITICAL` parpadeando, baliza roja parpadeando, `LOCKED`, setas en rojo y luz roja latiendo. Desde el nivel 1, las luces titilan.
20. TIME TRIAL: sectores apagados, sin texto de daño, baliza roja apagada y nunca luz roja.
21. Con naves de prueba (F7): el radar barre con ecos naranjas, cuenta `TGT` y da la más cercana; arco rojo y distancia en rojo si una va pegada. El minimapa gira contigo con las naves y la barra de la vuelta avanza.
22. Pausa con boost y daño 3: todo congelado, también la banda del CRT y las balizas.
23. El HUD de pantalla de velocidad, boost, daño, radar y minimapa ya no sale en la cabina de HAMMER. En la cámara exterior, sí.
24. Los tintes de los bordes de boost y daño siguen saliendo con las tres naves.
25. Menú principal: la versión es 0.14.6.

## 10. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Blender** | Inventario (sección 2) y sección 3 completa | Comprobación del script · Prueba manual 1–4 · Commit |
| **2 · Godot** | Secciones 4 y 5 | Pruebas 1–4 · Prueba manual 5–15 · Commit |
| **3 · HUD y luces** | Sección 6 · versión 0.14.6 | Pruebas 5–12 · Prueba manual 16–25 · Commit |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas y el `.blend`.

## 11. Fuera de alcance

- Casco exterior realista de cualquier nave (siguiente spec).
- Mirar alrededor con el ratón dentro de la cabina.
- Luz del mundo dentro de la cabina (túneles, ciudad, otras naves).
- Animar piezas (setas al pulsarlas, limpiaparabrisas, balizas giratorias, brazos del bloque colgante) o sonido.
- Que la parada de emergencia, la llave, el selector, los interruptores del panel inferior o los relojes pequeños hagan algo: son decorativos.
- Cambios en el HUD de las esquinas, en la cámara exterior o en los efectos de la cámara del mundo (salvo la inclinación de HAMMER en cabina).
- Las disposiciones 1 MANÓMETROS y 3 OPERARIO del prototipo.
- Menú SETTINGS.
