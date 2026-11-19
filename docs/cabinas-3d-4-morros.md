# VECTORY — Spec: Cabinas 3D · 4 · Morros y cristales

Versión 1.0 · 2026-11-19 · Estado: **definido, listo para implementar**

Spec corta que va **antes de `efectos-7-luces`**. Hace que la **vista de cabina cuadre con la de persecución**.

Hoy, desde la cabina se ve el **morro antiguo**: cada cabina 3D trae su propio morro, dibujado antes de que existieran los cascos 3D, y no se parece al casco que se ve por fuera.

Con esta spec:
- el morro que se ve desde dentro es una **copia del casco 3D real**, con su forma, sus piezas y su pintura;
- el ojo de cada cabina queda **fijado respecto al casco**;
- se hacen los cambios del cristal pedidos en cada nave:
  - **VECTOR**: fuera la **viga central** del cristal.
  - **BLADE**: el **cristal HUD más alto**, para ver bien el horizonte, y por dentro los **barrotes** de la cúpula ASALTO, los mismos que se ven por fuera.
  - **HAMMER**: por fuera, fuera la **rejilla del parabrisas**, porque desde dentro no se ve. Las rejas de las ventanas laterales se quedan.

Es **solo visual**: no cambia la conducción, la cámara exterior, el HUD ni la física.

Maqueta de referencia: **`design/cabinas-morro-vector.html`**, **`design/cabinas-morro-blade.html`** y **`design/cabinas-morro-hammer.html`** («VECTORY · Cabinas: morro y cristal · antes y después»).
- **ANTES** es la cabina de hoy. **DESPUÉS** es lo que pide esta spec. Se cambia con la tecla X.
- **PERSECUCIÓN** sirve para comparar con la vista de fuera.
- Los deslizadores del ojo y del HUD se dejaron en los valores de partida de esta spec.
- La maqueta pinta la cabina **encima del mundo con su propia cámara** (como el juego) y deja ver el casco real por el cristal. En el juego, el morro va **dentro de la cabina** (sección 3), pero se tiene que ver igual.
- También trae los faros, el retro y las luces de freno de `efectos-7`, que **no** son de esta spec.

Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa.

Se apoya en lo que ya existe:
- Las **cabinas 3D** de las tres naves (`cabinas-3d-1-vector`, `-2-blade`, `-3-hammer`): sus scripts `art/blender/cockpit_<nave>.py`, su grupo `Nose` (y `Canard` en HAMMER), su `SubViewport` con mundo propio, su cámara y su inclinación.
- Los **cascos 3D** (`cascos-3d-1-vector`, `-2-blade`, `-3-hammer`): sus scripts `art/blender/hull_<nave>.py`, sus materiales de pintura (catálogo y selector de pinturas) y el daño de sus materiales.
- `art/blender/lib/` y `art/blender/run.ps1`.

Cambios respecto a specs anteriores:
- La cabina de VECTOR estaba dada por **definitiva** (`cabinas-3d-1-vector`). Esta spec le quita solo la viga central y el morro. El salpicadero no se toca.
- El grupo `Nose` de las tres cabinas (y `Canard` de HAMMER) **se sustituye** por la copia del casco. Las comprobaciones de punta del morro de esas specs (z ≈ −3,1, −4,75 y −2,55) se cambian por las de la sección 3.3.
- El casco de HAMMER pierde `Guard_*` (`cascos-3d-3-hammer`).
- El **ojo de la cabina** pasa a estar definido **en coordenadas del casco** (sección 4.1), no como altura sobre la pista.

Rama: `feature/cockpit-noses`, que sale de `develop`. Versión: se sube el **tercer número** de la que haya, en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 0** · Inventario.
- **Etapa 1** · Blender:
  - cada script de cabina construye el casco de su nave con el script del casco, se queda con la parte de delante y la pone en su grupo `Nose`, movida al ojo;
  - VECTOR pierde la viga central;
  - BLADE sube el cristal HUD y gana los barrotes;
  - HAMMER pierde la rejilla del parabrisas en su casco.
- **Etapa 2** · Godot:
  - el ojo de cabina en el sitio nuevo;
  - la copia del morro con la **misma pintura** y el **mismo daño** que el casco;
  - el juego, igual que la maqueta en DESPUÉS.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **Scripts de los cascos**: si `hull_<nave>.py` se puede **llamar desde otro script** para construir el casco en memoria, sin exportar. Si no, qué hay que separar (por ejemplo, una función `build_hull(...)` en `art/blender/lib/` o en el propio script). **No** se cambia el resultado del casco.
2. **Scripts de las cabinas**: dónde se construyen `Nose` (y `Canard` en HAMMER), la viga central de VECTOR (`Halo`) y el cristal HUD de BLADE (`Combiner`).
3. **Ojo de la cámara de cabina** de cada nave en el juego:
   - dónde está respecto al **origen del casco** (x, y, z), y no solo su altura sobre la pista;
   - de dónde sale (perfil visual, constante, cálculo con la altura de levitación);
   - cómo se mueve con la nave (inclinación en curva, pista 3D).
4. **Materiales de pintura y daño** del casco en Godot: cómo pone hoy el juego la pintura elegida y las quemaduras del daño en los materiales del casco, para hacer lo mismo en la copia del morro.
5. **Luces de la cabina** (su mundo propio): con qué luces se verá iluminada la copia del morro.
6. **Triángulos** de cada cabina hoy, para comparar después.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Blender

### 3.1 La copia del morro

Cada `cockpit_<nave>.py`:

1. Construye en memoria el **casco de su nave** con el script del casco (punto 1 del inventario), con todas sus piezas y materiales, tal y como se exporta.
2. **Quita**:
   - todo lo que queda **detrás del corte**: se corta con el plano z = z_ojo + 0,30 (coordenadas del casco) y se queda la parte de delante;
     - las piezas enteras detrás del corte se borran;
     - las que lo cruzan se cortan (bisección) y se cierra el corte si se va a ver;
   - las piezas que taparían la vista desde dentro:
     - VECTOR: `Canopy_Glass`;
     - BLADE: `Canopy_Glass`;
     - HAMMER: todo el grupo `Cab`.
3. **Mueve** lo que queda restando el ojo (sección 4.1): un punto del casco `p` va a la cabina en `p − ojo`. Así cae en coordenadas de la cabina, con el origen en el ojo.
4. Lo cuelga del grupo **`Nose`** de la cabina, en lugar del morro de hoy. En HAMMER, el grupo **`Canard`** de la cabina también desaparece: el canard viene con el casco.
5. Las piezas **conservan sus nombres** del casco, con el prefijo `Nose_` (por ejemplo `Nose_Fuselage_Upper`, `Nose_Light_Nose_L`, `Nose_BullBar_Top`). Así Godot puede encontrar las luces y la pintura.
   - Las piezas vivas del casco (`Light_*`, `Accent_*`) siguen siendo piezas vivas dentro de la cabina, con su propio material.

Detalles:
- Los **materiales** son los del casco, con sus mismas texturas. La pintura y el daño los pone Godot (sección 4.2).
- Si una pieza del casco atraviesa el salpicadero o el marco de la cabina y se ve por dentro, **se avisa con capturas**. No se recorta la cabina sin preguntar.
- Si el corte deja ver un hueco o una cara abierta, se cierra con una tapa del material de la pieza.
- **Triángulos**: la copia suma triángulos a la cabina. Si suma más de 60 000, se avisa con el dato y se propone simplificar las piezas que menos se ven (remaches, juntas).

### 3.2 Cambios del cristal

**VECTOR · sin viga central**
- Fuera las dos piezas del bloque «halo: barra central y travesaño inferior → Halo» de `buildCockpit()`:
  - la barra central de (0; 0,375; −0,54) a (0; −0,27; −0,74);
  - el travesaño de (−0,06; −0,275; −0,74) a (0,06; −0,275; −0,74).
- El larguero de arriba, los pilares A y el resto del marco se quedan.
- Fuera también el morro escalonado de `buildCockpit()` («Morro escalonado → grupo Nose»), con sus LED (`Led_Nose_L/R`): los sustituye la copia.
- El contrato de nombres pierde `Led_Nose_L/R`. Las luces del morro son ahora las del casco (`Nose_Light_Nose_*`).

**BLADE · cristal HUD más alto y barrotes**
- El cristal HUD (`Combiner_Glass`, con su `Screen_HUD` y su marco) sube **6 cm**: su centro pasa de y −0,086 a **y −0,026**.
- Los dos postes (`Combiner_Post`) van de su base a y **+0,042** (antes −0,018).
- La base, la botonera y la inclinación del cristal no cambian.
- **Barrotes**: los tres `Canopy_Bar_0..2` de la cúpula ASALTO del casco van a la cabina, en el grupo **`Frame`**, movidos al ojo como la copia del morro. Mismo material del casco.
  - Pasan por fuera del arco de la cabina (`Canopy_Bow`) y por fuera del cristal.
  - Si chocan con el arco o con los retrovisores, se avisa con capturas.
- Fuera el morro de la cabina (grupo `Nose`, con la sonda, la antena y `Led_Nose_L/R`): lo sustituye la copia, que ya trae la sonda del casco.

**HAMMER · sin rejilla del parabrisas por fuera**
- En `hull_hammer.py`, fuera la rejilla de delante de la cabina de grúa:
  - `Guard_Bar_0..2`;
  - `Guard_Cross_0`, `Guard_Cross_1`;
  - los `Guard_Standoff`.
- Las rejas laterales (`SideGuard_*`), los arcos antivuelco y el resto **se quedan**.
- Se regenera el `.glb` del casco y se actualiza su contrato y su manifiesto.
- En la cabina, fuera `Nose` y `Canard` (los sustituye la copia).
- El contrato de nombres de la cabina pierde `Led_Nose_L/R` y `Led_Canard_L/R`. Las balizas ámbar son ahora las del casco (`Nose_Light_Nose_*`, `Nose_Light_Canard_*`).

### 3.3 Ojo y comprobaciones

El ojo de cada cabina, en coordenadas del casco (metros, X derecha, Y arriba, morro hacia −Z):

| Nave | Ojo en el casco | Inclinación de la cámara | Ojo sobre la pista con la levitación de la maqueta |
|---|---|---|---|
| VECTOR | (0; 0,80; −0,95) | 0° | 1,45 m (casco a 0,65) |
| BLADE | (0; 0,37; −0,45) | −5,7° (−0,10 rad) | 1,02 m (casco a 0,65) |
| HAMMER | (0; 0,805; −1,03) | −4,9° (−0,085 rad) | 1,56 m (casco a 0,75) |

- El ojo se eligió para que la punta del morro de la cabina de hoy caiga donde está la punta del morro del casco. La vista se parece a la de hoy, pero con el morro de verdad.
- Las inclinaciones no cambian: son las de cada cabina.
- La altura sobre la pista de BLADE (antes 0,90 m) y de HAMMER (antes 2,03 m) cambia. Manda el ojo en el casco.

Comprobación antes de exportar (coordenadas de Godot de la cabina, tolerancia 5 mm):
- La punta del morro de la copia en **ojo_morro − ojo**:
  - VECTOR (0; −0,80; −3,05);
  - BLADE (0; −0,35; −4,75), y la sonda hasta z ≈ −5,30;
  - HAMMER: cara del morro en z −2,57 y defensa en z ≈ −2,77.
- No queda ninguna pieza del casco por detrás de z = +0,30.
- VECTOR: no hay ninguna pieza en x = 0 entre el larguero y el salpicadero.
- BLADE: centro de `Combiner_Glass` en y = −0,026.
- HAMMER: el casco exportado no tiene ninguna pieza `Guard_*`.

Capturas nuevas del script, desde el ojo con su inclinación y FOV 70°:
- `eye_new.png` de cada cabina;
- una de HAMMER desde fuera, sin la rejilla.

## 4. Etapa 2 · Godot

### 4.1 Ojo de la cámara de cabina

- La cámara de cabina (la del mundo, en la vista de cabina) va en **origen del casco + ojo** de la tabla 3.3, y la cabina 3D con su origen en ese mismo punto. Se mueve con la nave, igual que hoy.
- El ojo va en el **perfil visual** de cada nave, en coordenadas del casco. Si hoy está como altura sobre la pista (punto 3 del inventario), se pasa a coordenadas del casco.
- Los efectos de cámara de cabina de hoy (balanceo, vibración, contragiro y empuje del boost) **no cambian**.
- Lo que se ve por el cristal (pista, rivales, ciudad) tiene que cuadrar con la copia del morro: la cámara de la cabina y la del mundo miran desde el mismo punto y con la misma inclinación.

### 4.2 Pintura y daño en la copia del morro

- Las piezas de la copia que en el casco llevan **pintura del catálogo** usan, en la cabina, **la misma pintura** que tiene el casco: la elegida en el selector o la del rival.
- Si el casco usa una instancia de material con la pintura, la copia usa la misma instancia o una copia que se actualiza a la vez. Lo más sencillo según el inventario (punto 4).
- **Marcas y libreas**: las calcomanías del casco que queden por delante del corte también van, con la misma regla que el casco (con marcas o sin ellas).
- **Daño**: las quemaduras del daño del casco también se ven en la copia, con el mismo nivel.
- Las **piezas vivas** de la copia (`Nose_Light_*`, `Nose_Accent_*`) se encienden y titilan igual que las del casco, con su mismo estado.
- Hoy, las luces del morro de las cabinas son `Led_Nose_*` (y `Led_Canard_*` en HAMMER). Ahora pasan a estas piezas.

### 4.3 Luz de la copia del morro

- La copia está en el mundo propio de la cabina, así que la iluminan las **luces de la cabina**.
- Si se ve muy distinta del casco en persecución (más clara, más oscura u otro color), se ajusta **solo con la intensidad de los reflejos** de sus materiales en la cabina, igual que hacía `M_PaintNose` (reflejos × 0,35–0,6).
  - No se tocan las luces de la cabina.
  - Se apunta el valor.

### 4.4 Lo que no cambia

- La **cámara exterior** y el casco en persecución (salvo HAMMER sin rejilla).
- El HUD de las pantallas, las shift lights, las luces de boost y daño y la pausa.
- Los chorros del retro de `efectos-5` son del mundo.
  - Si en la vista de cabina la copia del morro tapa su arranque (en VECTOR salen del lomo del morro), **se avisa con capturas**.
  - Propuesta: pintar los chorros de tu nave también en el mundo de la cabina.

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Blender**:
  - la construcción del casco se puede **llamar** desde los scripts de las cabinas (punto 1 del inventario);
  - la copia del morro (cortar, quitar piezas, mover al ojo, poner el prefijo) es una función de `art/blender/lib/`, común a las tres cabinas, que recibe:
    - el casco construido;
    - el ojo;
    - el corte (+0,30);
    - la lista de piezas que se quitan.
- **Perfil visual de cada nave**: gana el **ojo en coordenadas del casco**. La inclinación ya está.
- **Pintura de la cabina**: el mismo sistema que pinta el casco pinta también la copia del morro. Sin reglas nuevas en `Cockpit3DView`.
- **Manifiesto de cada cabina**: lista las piezas `Nose_*` con el material del casco de cada una y si son vivas.

## 6. Pruebas

### Pruebas unitarias mínimas

1. **Perfil visual**: VECTOR, BLADE y HAMMER con su ojo de la tabla 3.3 (±1 mm) y su inclinación de siempre.
2. **Manifiestos de las cabinas**:
   - tienen piezas `Nose_*` sacadas del casco;
   - no tienen `Led_Nose_*` ni, en HAMMER, `Led_Canard_*`;
   - VECTOR sin las piezas de la viga central;
   - BLADE con `Canopy_Bar_0..2` en `Frame` y `Combiner_Glass` a y −0,026;
   - ninguna pieza `Nose_*` por detrás de z = +0,30.
3. **Manifiesto del casco de HAMMER**: sin `Guard_*`, con `SideGuard_*`.
4. **Pintura**: cambiar la pintura del casco cambia la de la copia del morro (las mismas piezas, el mismo color).
5. **Piezas vivas**: las luces de la copia del morro siguen el mismo estado que las del casco (encendido y parpadeo con daño).

Las pruebas existentes siguen pasando. Las que buscaban `Led_Nose_*` / `Led_Canard_*` en las cabinas se adaptan a las piezas nuevas.

### Comprobación de los scripts de Blender

- Los scripts fallan si falta una pieza del contrato o si queda alguna pieza del casco por detrás del corte.
- Dos ejecuciones seguidas dan el **mismo** resultado.

### Prueba manual

**Etapa 1 · Blender**
1. `eye_new.png` de cada cabina al lado de la maqueta en DESPUÉS: el mismo morro desde el mismo sitio.
   - VECTOR, sin viga central.
   - BLADE, con el HUD más alto y los tres barrotes.
   - HAMMER, con el morro amarillo, la chapa lagrimada y la defensa.
2. Abrir el `.blend` de cada cabina:
   - la copia del morro bien colocada;
   - nada del casco atravesando el salpicadero;
   - sin caras abiertas en el corte.
3. Captura de HAMMER desde fuera: sin la rejilla del parabrisas, con las rejas laterales.

**Etapa 2 · Godot**
4. RACE, las tres naves (F10), vista de cabina: el morro que se ve es el del casco. Pasar a persecución con C: es el mismo morro y la misma pintura.
5. Cambiar la pintura en el selector: la copia del morro cambia también.
6. Dañar la nave: las quemaduras también salen en el morro de la cabina, y sus luces titilan como las del casco.
7. Curvas, crestas y boost: la vista por el cristal cuadra con el morro y no hay saltos.
8. BLADE: el horizonte se ve bien a través del cristal HUD, con los barrotes por fuera.
9. HAMMER en persecución: sin la rejilla del parabrisas.
10. Fps en cabina antes y después: no bajan de forma notable.
11. Menú principal: la versión nueva.

## 7. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Blender** | Sección 3 | Pruebas 2–3 · Comprobación de los scripts · Prueba manual 1–3 · Commit `feat:` |
| **2 · Godot** | Secciones 4 y 5 · versión | Pruebas 1, 4 y 5 · Prueba manual 4–11 · Commit `feat:` |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas.

## 8. Fuera de alcance

- Los faros, las toberas de retro y las luces de freno (`efectos-7`). Esa spec vuelve a ejecutar los scripts de las cabinas para que su copia del morro los lleve.
- Cambiar el salpicadero, las pantallas o el HUD de cualquier cabina.
- Mirar alrededor con el ratón dentro de la cabina.
- Iluminación del mundo (túneles, ciudad) sobre la copia del morro.
- Ver el resto del casco (alas, góndolas, toberas) desde la cabina.
