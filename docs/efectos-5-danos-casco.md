# VECTORY — Spec: Efectos 5 · Daño visible del casco

Versión 1.0 · 2026-11-19 · Estado: **implementada y validada** (propuesta del agente, aceptada por el usuario sin maqueta)

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
- Manual (el usuario, en RACE): rozar la barrera de un lado y ver arañazos solo en ese costado; un golpe de morro o de popa con un rival; daño 2 y 3 con su hollín y las grietas; las tres naves y varias pinturas (también las perladas de VECTOR); la pausa; rivales dañados; los fps.
