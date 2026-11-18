# VECTORY — Spec: Efectos 5 · Daño visible del casco

Versión 1.1 · 2026-11-19 · Estado: **implementada y validada** (propuesta del agente, aceptada por el usuario sin maqueta)

Sustituye las **quemaduras de máscara** de las specs de los cascos 3D (`cascos-3d-1-vector`, `-2-blade`, `-3-hammer`, sección 6.5: tres máscaras de ruido por nivel que oscurecían la pintura en todo el casco). Se leían como un camuflaje de manchas, no como daño. El **parpadeo de las luces** de esas secciones no cambia. Las reglas del daño (`danos`) tampoco: es solo visual.

El daño cuenta lo que ha pasado:

1. **Rozaduras** donde la nave ha chocado.
2. **Hollín** alrededor de los focos del humo de daños (`efectos-4-humo-danos`).
3. **Grietas incandescentes** con daño 3.

Solo en **RACE**, para **todas las naves**, como el daño. En TIME TRIAL, nada. El fantasma, el menú y el holograma, nada.

## 1. Rozaduras

- El casco tiene **cuatro zonas**: izquierda, derecha, morro y popa. Cada una lleva un desgaste de 0 a 1 que **solo sube** en la carrera (tampoco baja al recolocar); RESTART empieza en 0.
- Lo que suma cada golpe:

| Golpe | Suma |
|---|---|
| Roce (barrera o nave) | 0,15 por segundo mientras dura |
| Medio | 0,25 al empezar |
| Fuerte | 0,5 al empezar |

- **Barrera**: raya el costado de su lado.
- **Otra nave**: la zona sale de dónde queda el punto del contacto en los ejes de la nave: más allá del 60 % de lo que llega su caja hacia el morro o hacia la popa, esa zona; si no, el costado de su lado.
- **Aspecto**: manchas de pintura desgastada (gris oscuro, poco opacas) que crecen con el desgaste y, dentro, **arañazos largos a lo largo de la nave**, de metal claro (no muy metálico: muy metálico solo reflejaría el entorno oscuro y saldría negro). Los costados se pintan por el lado (por la posición) y por lo de lado que mira la superficie; morro y popa, por el largo.

## 2. Hollín

- Alrededor de los focos del humo, **en la piel bajo cada foco** (`DamageSmokeFoci.SkinGaps`), con bordes irregulares (ruido):

| Nivel | Focos | Radio (m, a tamaño 1) | Fuerza |
|---|---|---|---|
| 0–1 | — | — | — |
| 2 | `Smoke` | 0,9 | 0,8 |
| 3 | los tres | 1,4 (× 0,75 en `Smoke2`, × 0,55 en `Smoke3`) | 0,95 |

## 3. Grietas incandescentes (nivel 3)

- Líneas finas de brasa naranja dentro del hollín, hasta el **45 %** de su radio, que laten (0,6 + 0,4 · sen(5·t + ruido)), con emisión × 2,2. El tiempo es el del casco: la pausa las congela.

## 4. Cómo está hecho

- **Puro** (`Ship/HullWear.cs`): `HullWear` (las cuatro zonas, `Barrier`, `Contact` / `ContactAt` con el rumbo de la nave, `ZoneOf`, `Version`), `HullWearSettings` (todos los valores de arriba) y `HullWear.Soot` / `Cracks` por nivel y foco.
- **Casco** (`ShipHull3D`): una pasada de shader (`WearShader`) detrás de cada material pintado (y detrás de su iridiscencia, si la tiene), una por casco, **puesta desde la carga** para que su shader se compile entonces y el primer golpe no dé un tirón. Trabaja en los ejes del casco: las piezas del `.glb` traen cada una su transformación, así que recibe la inversa de la raíz del modelo cada frame mientras haya algo que pintar. Va por las **dos caras**, porque algunas piezas traen las caras y las normales al revés.
- **Conexión**: `Main` pasa a `Player.WearBarrier` / `WearShip` y a `RivalShip.WearBarrier` / `WearShip` los roces y golpes de cada paso (solo RACE); cada nave lo lleva a su `Spaceship.SetHullWear` cuando cambia.
- Las máscaras `assets/ships/hull_scorch_1/2/3.png` se han **borrado**, con su escritura en `art/blender/hull_vector.py` (y `scorch_noise` / `scorch_mask` de `lib/textures_hull.py`) y la entrada `scorchMasks` de los tres manifiestos y sus scripts. `scorchMaterials` se queda: dice qué materiales llevan la pasada del daño.

## 5. Pruebas

- Unitarias (`HullWearTests`): costado de la barrera, roce por segundo y golpe solo al empezar, tope en 1 y versión, zona de un punto de contacto (también con la nave girada), hollín por nivel y foco en la piel, sin hollín en TIME TRIAL.
- Unitarias (`CockpitGlassDamageTests`): picadas, grietas y manchas por nivel, mismo sitio al subir de nivel, cada grieta desde su picada, dentro de la zona del cristal y según la semilla, y reparto hacia los bordes.
- Manual (el usuario, en RACE): rozar la barrera de un lado y ver arañazos solo en ese costado; un golpe de morro o de popa con un rival; daño 2 y 3 con su hollín y las grietas; las tres naves y varias pinturas (también las perladas de VECTOR); la pausa; rivales dañados; los fps; el cristal de las tres cabinas con daño 1, 2 y 3.

## 6. Cristal de la cabina (v1.1)

Pedido por el usuario: la cabina también se daña, con **picadas** y **grietas largas irregulares** en el cristal y alguna mancha. Solo **RACE**, solo tu nave (los rivales no tienen cabina); en TIME TRIAL, nada.

| Nivel | Picadas | Grietas largas (largo, en altos del cristal) | Manchas |
|---|---|---|---|
| 0 | — | — | — |
| 1 | 3 | 1 (0,22) | — |
| 2 | 6 | 2 (0,42 · 0,22) | 1 |
| 3 | 10 | 3 (0,65 · 0,42 · 0,30) | 3 |

- **Picada**: centro machacado de borde irregular (radio 0,006–0,014), medio anillo de ojo de buey y seis grietillas cortas alrededor.
- **Grieta larga**: sale de una picada (la de su mismo índice) hacia el centro del cristal (con un desvío al azar), así cruza el cristal desde los bordes, en una línea quebrada de tramos de 2–4 cm con giros al azar, que se estrecha y se apaga hacia la punta y suelta alguna rama corta. Al subir de nivel se **alarga por el mismo camino**.
- Grietas y picadas en **gris humo** (elegido por el usuario) al 45 %, con un brillo tenue alrededor, ~1,5 px de ancho en pantalla. **Mancha**: suciedad gris parda al 30 %, de borde irregular, con salpicaduras alrededor.
- **Para que no parezcan pegatinas** (pedido por el usuario): a trozos se ven más y a trozos menos (ruido a lo largo de la grieta, 30–100 %), y **cogen la luz de la cabina**: cada tramo es una cara de la fractura inclinada a su manera (normal desviada × 0,7, rugosidad 0,18, especular 0,9), así la luz principal, la del boost, la roja de los daños y la de las chispas las encienden a trozos y de su color. Sin brillo propio: con poca luz casi no se ven, y con la luz roja de los daños se encienden a trozos en rojo (validado así por el usuario; un brillo base las hacía demasiado visibles). El mundo de fuera no llega a esta pasada (la cabina se pinta en su propio `SubViewport`): no hay refracción real.
- Los sitios se sortean **una vez por carrera** (semilla del `Player`) por casi todo el cristal (x 0,04–0,96, y 0,04–0,80 del cristal, 0 arriba), **empujados hacia los bordes** (`Spread`: raíz de la distancia al centro, del todo en x y a medias en y), para no quedarse en el centro ni tapar lo que se mira; al subir de nivel, lo que había sigue en su sitio y sale más.
- Van **pegadas al cristal**: una pasada de shader detrás del material de la mancha del cristal (`Glass_Smudge`, que tienen las tres cabinas), en los ejes de esa pieza, puesta al cargar la cabina (sin tirón en el primer golpe). Lo que tapa el cristal (pilares, arco, bloque colgante) las tapa también.
- **Puro**: `CockpitGlassDamage` + `CockpitGlassSettings` + `GlassMark` (`Ship/CockpitGlassDamage.cs`). **Godot**: `CockpitGlass` (shader), `Cockpit3DView.SetGlassDamage` y `Cockpit.SetGlassDamage` (todas las cabinas, también las que no se ven, por F10), que llama `Player.SetFlickerLevel`.
