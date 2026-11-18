# VECTORY — Spec: Cascos 3D · 3 · Hammer (Minero)

Versión 1.0 · 2026-11-15 · Estado: **propuesta, pendiente de validar**

Tercera y última spec de los **cascos exteriores realistas**. Cambia el casco wireframe de **HAMMER** por un casco 3D en estilo **industrial**, variante **MINERO**:

- **Silueta del wireframe MARTILLO**, con retoques mínimos:
  - las 4 secciones del fuselaje, con el morro acabado en una **cara roma** en z −3,6 (en el wireframe era una punta);
  - el **canard ancho** con las **puntas caídas** hasta x ±2,5, ahora con grosor real y un patín de acero abajo;
  - las **aletas traseras** con grosor real;
  - los **dos motores grandes** en (±0,45, 0,2), que asoman por detrás del fuselaje como en el wireframe.
- **Sección casi cuadrada**: superelipse de exponente **n = 3,2** entre los 4 puntos del rombo.
- **Cabina de grúa** a juego con la cabina 3D COLGANTE: parabrisas plano inclinado, ventanas laterales, limpiaparabrisas, **balizas con jaula** (ámbar y roja, que funcionan como dentro) y **4 focos de trabajo**. El ojo de la cabina 3D cae dentro.
- **Compartimento del motor** en el lomo, a 3/4 del largo, con tapa atornillada, rejilla de admisión y lamas.
- **Motores de titanio** con anillos de refrigeración y **fondo naranja**, y **tuberías de cobre** de la cabina al compartimento y de este a los motores.
- **Protecciones**: defensa tubular delante del morro, rejilla sobre el parabrisas, rejas discretas en las ventanas laterales y dos arcos antivuelco.
- **Escape vertical** detrás de la cabina y una **escalerilla** a cada lado.
- **Materiales**: la **chapa pintada atornillada** de la cabina 3D, panza en chapa oscura, chapa lagrimada en el morro, acero, titanio, cobre y franjas de peligro.
- **Catálogo de 20 pinturas** (15 lisas y 5 bicolor, con la cabina de otro color), cada una **con marcas de flota** y **sin marcas**: **40 combinaciones**, listas para el futuro selector de color.
- **Daño visible**: quemaduras, luces que titilan y **humo por el escape vertical**.

Repite la cadena de VECTOR y BLADE (specs `cascos-3d-1-vector` y `cascos-3d-2-blade`): script de Blender → `.glb` → Godot.
- El casco se ve en la **cámara exterior** y en el **holograma del selector**, y queda **preparado para los rivales con IA**.
- El **fantasma** sigue siendo wireframe.

Con esta spec, **las tres naves tienen casco 3D**.

Plan de las specs de cascos 3D:

1. Vector · Nocturna (hecha).
2. Blade · Asalto (hecha).
3. **HAMMER · Minero** (esta).

Diseño de referencia:
- **`design/casco-3d-hammer.html`**: el prototipo.
  - Se abre en el navegador y se gira con el ratón (o con el dedo en el móvil).
  - Tiene vistas, boost, daño, velocidad, la silueta wireframe para comparar y el catálogo de pinturas.
  - **Solo vale la variante 1 · MINERO** (la que sale al abrirlo, `VAR = 1`).
- **Va a Blender** todo lo que construye `buildHull()` con `VAR = 1`, **con marcas** (`LIV = true`), incluido el grupo `Markings` (la geometría; los dibujos los pone Godot).
  - También las texturas: las de la **pintura como máscaras** independientes del color (sección 3.5) y las fijas.
- **Va al código del juego**:
  - el **catálogo de pinturas** (`PAINTS`) y cómo se aplica cada una (`cabOf`, `markCol`);
  - el **dibujo de las calcomanías** (`unitTex`, `logoTex`, `warnTex`, `stencilTex`);
  - las **balizas** del techo;
  - el **daño** (quemaduras, parpadeo de luces y humo);
  - el **brillo de levitación**.
- **No va a ningún sitio**:
  - las variantes 2 · REMOLCADOR, 3 · DEMOLICIÓN y 4 · FUNDICIÓN. Se quedan en el archivo como diseño guardado, con sus texturas (`tugBlack`, `tugRed`, `tugWhite`, `primer`, `armor`, `burnt`) y las piezas que solo usan ellas: defensas de goma, neumáticos, mástil, luces de costado, bitas, remaches, parachoques, ariete, placas soldadas, rejillas de DEMOLICIÓN, radiadores y el recorrido de tuberías de FUNDICIÓN;
  - el mundo, la pista y la ciudad;
  - las cámaras del prototipo y el panel;
  - la silueta wireframe de comparación;
  - las llamas, estelas y la luz de boost del prototipo (el juego ya tiene las suyas, spec `efectos-velocidad`). Lo único que pasa es el **color** de la llama (sección 4.2).
- Los nombres que se pasan a `mesh()` y `group()` dicen cómo se llama cada pieza en Blender.

Si algo no cuadra entre esta spec y el prototipo, **manda el prototipo** y se avisa.

Rama: `feature/hull-3d-hammer`, que sale de `develop` con `feature/hull-3d-blade` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.8 → 0.14.9** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. **No hay cambios en el documento maestro.**

---

## 1. Resumen

- **Etapa 1 · Blender**: un script nuevo, `hull_hammer.py`, construye el casco reutilizando y ampliando `art/blender/lib/`. Lo exporta a `.glb` y guarda:
  - el `.blend`;
  - las máscaras de la pintura;
  - un manifiesto;
  - las capturas de previsualización.

  En el juego no cambia nada.
- **Etapa 2 · El casco en carrera**: HAMMER lleva el casco 3D en la cámara exterior, con la pintura por defecto (AMARILLO OBRA con marcas), su luz y sus reflejos.
  - Las **toberas** y las **estelas** salen de los puntos del modelo.
  - El **ancho de colisión** se queda en **2,50 m** (el casco nuevo mide lo mismo).
  - En la vista de cabina, el casco exterior **no tapa nada**.
- **Etapa 3 · Pinturas, marcas, luces y daño**:
  - entran las **40 combinaciones**, con la tecla de depuración de VECTOR;
  - entran las **calcomanías** dibujadas por Godot y las **balizas** del techo;
  - entran el **daño visible** y el **brillo de levitación**.
- **Etapa 4 · Holograma del selector**: SELECT SHIP enseña el casco 3D de HAMMER con su luz, con el mismo giro y la misma escala de siempre.
- VECTOR y BLADE **no cambian**.
- Lo que **no cambia**:
  - el fantasma (wireframe de su nave);
  - las naves de prueba del radar;
  - la nave del fondo del menú principal;
  - las cabinas 3D y el HUD;
  - la conducción;
  - la iluminación del circuito (queda para más adelante).

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **`art/blender/lib/`** tras VECTOR, BLADE y las tres cabinas:
   - qué se puede reutilizar tal cual (`herm`, `lathe`, `rbox` con 2 segmentos, `boxUV`, `tube`, `skinGrid`, `skinRibbon`, `plateSet`, `steelSet`, `treadSet`, `hazardSet` y `scratchMap` de la cabina de HAMMER, el titanio y las quemaduras de VECTOR, exportación, capturas y manifiesto);
   - qué hay que añadir (sección 3.4) **sin cambiar** lo que usan los otros dos cascos y las tres cabinas.
   - **Ojo con los nombres**: `hazardSet` del casco de BLADE y el de la cabina de HAMMER tienen **algoritmos distintos**. Este casco usa **el de la cabina de HAMMER** (`hazardSet(85)`), igual que `plateSet`, `steelSet` y `treadSet`.
2. **`ShipHull3D`** tras BLADE: si ya es genérico (piezas vivas, grupos de pintura y aplicador según el manifiesto y el catálogo de cada nave) o queda algo escrito a fuego. Lo nuevo de HAMMER:
   - una **pintura de dos zonas** (casco y cabina, sección 6.2);
   - **balizas** con estado (sección 6.4), parecidas a las de la cabina 3D;
   - **piezas que solo existen con marcas** que no son calcomanías (franjas de las aletas y del compartimento, sección 6.3).
3. **Perfil visual de HAMMER**: casco wireframe, toberas, estela, factor de cámara (0,92), ancho de colisión (2,5 m) y cabina 3D.
4. **Toberas de HAMMER** (spec `naves-3-cascos`): hoy la llama tiene escala × 1 (radio 0,40). Con el casco nuevo la boca es redonda de radio 0,25 a 0,29 m (sección 4.2).
5. **Color de la llama** en `efectos-velocidad`: si el color base (sin boost) de la llama, el núcleo y el halo se puede cambiar **por nave** desde el perfil visual sin tocar la lógica (sección 4.2).
6. **Altura de vuelo** de HAMMER sobre la pista. Lo más bajo del casco nuevo son los **patines de las puntas caídas**, a **y = −0,52** (antes, −0,50). Hay que confirmar que **no tocan la pista** ni en las crestas ni en los peraltes (spec `pista-3d`).
7. **Luz y reflejos del casco** que dejaron VECTOR y BLADE (capa visual, luces colgadas de la nave, panorama de reflejos): se reutilizan tal cual.
8. **Fuentes**: `Black Ops One` ya está (la añadió BLADE); `Share Tech Mono` también.
9. **Holograma del selector**: la escala común y la prueba de la **esfera de 10 m** (sección 7).
10. **Simulador de equilibrio** de la spec `naves-6-equilibrio`: si se puede ejecutar tal cual para comprobar HAMMER.
11. **Rendimiento**: el casco del MINERO tiene unos **68 000 triángulos** (el doble que BLADE) y unas 200 piezas. Mirar si hace falta unir mallas por material al importar (sección 4.1).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · El casco en Blender

### 3.1 Cómo se usa Blender

Igual que VECTOR y BLADE: Blender sin ventana, desde la terminal, con la ruta de `art/blender/run.ps1`:

```
blender -b --factory-startup --python art/blender/hull_hammer.py -- --out <carpeta>
```

Todo vive en el script, el `.blend` se regenera y el script es **determinista**.

### 3.2 Archivos

| Archivo | Qué es | ¿Va a Git? |
|---|---|---|
| `art/blender/hull_hammer.py` | Construye el casco, exporta y guarda todo | Sí |
| `art/blender/lib/` | Piezas comunes + lo nuevo de la sección 3.4 | Sí |
| `assets/…/hull_hammer.glb` | Modelo exportado, con las texturas dentro | Sí |
| `assets/…/hull_hammer.manifest.json` | Nombres, medidas, puntos y recuentos (sección 3.8) | Sí |
| `assets/…/hull_hammer_body_mul.png`, `hull_hammer_body_add.png` | Máscaras de la pintura del casco (sección 3.5) | Sí |
| `assets/…/hull_hammer_cab_mul.png`, `hull_hammer_cab_add.png` | Máscaras de la pintura de la cabina | Sí |
| `assets/…/hull_scorch_1.png`, `_2.png`, `_3.png` | Máscaras de quemaduras: **las de VECTOR**; no se duplican | Ya están |
| `art/blender/out/hull_hammer.blend` | Escena de Blender | No (`.gitignore`) |
| `art/blender/out/previews/hull_hammer_*.png` | Capturas de previsualización | No (`.gitignore`) |

La carpeta de `assets/` es la misma que usaron VECTOR y BLADE.

### 3.3 Ejes, unidades y origen

- Igual que VECTOR y BLADE: metros, **X derecha, Y arriba, morro hacia −Z**, todo dentro de un objeto raíz **`HullHammer`** girado **+90° en X** en Blender, con los números del prototipo copiados **tal cual**.
- El **origen** es el del casco wireframe de HAMMER de hoy, así que el casco nuevo cae en el mismo sitio que el viejo.
- La altura `HOVER` (0,75 m) es **solo del prototipo** y **no** va al modelo.
- Comprobación antes de exportar (en coordenadas de Godot, con marcas; tolerancia 5 mm):

| Punto | Valor |
|---|---|
| Cara roma del morro | z = −3,60 |
| Delante de la defensa tubular | z = −3,829 |
| Boca de las toberas | z = +3,00 |
| Caja total en z | −3,829 → 3,00 (largo **6,83 m**) |
| Semiancho (balizas de las puntas caídas) | **2,502 m** |
| Caja total en y | −0,52 (patines) → **1,353** (boca del escape); alto **1,873 m** |
| Alto sin los extras del techo (balizas, focos, escape, arcos, barandillas) | −0,52 → 1,084 |
| Techo de la cabina | y = 1,055 |
| Tapa del compartimento del motor | y = 0,915 |
| Boca de las toberas (`Exhaust_*`) | (±0,45, 0,20, 3,00) |
| Puntos de estela (`Trail_*`) | (±2,50, −0,50, −2,20) |

### 3.4 Geometría

Se traduce `buildHull()` con `VAR = 1` y `LIV = true` **pieza a pieza**, con los mismos números y en el mismo orden. Piezas nuevas para `art/blender/lib/`:

| Prototipo | En Blender |
|---|---|
| `prof(z)` | Perfil del fuselaje de HAMMER: `PZ` = [−2,6; −1; 1,2; 2,2] y `PV` del wireframe MARTILLO. Entre secciones, **Hermite monótona** (`herm`, la de VECTOR). De la cara roma `TIP` (hw 0,20 · top 0,14 · bot 0 · side 0,06) a la 1.ª sección, cada valor = cara + (sección − cara) × `1 − (1 − t)²` |
| `fusPt(z, θ)` | Sección en **superelipse** centrada en (0, side): x = hw·sgn(cos θ)·\|cos θ\|^(2/n); arriba y = side + (top − side)·\|sen θ\|^(2/n); abajo y = side − (side − bot)·\|sen θ\|^(2/n), con **n = 3,2** |
| `skinY`, `fusTopY`, `fusBotY` | Altura de la piel en (x, z): r = \|x\|/hw, c = r^(n/2), s = (1 − c²)^(1/n)… exactamente la fórmula de `skinY` |
| `skinNormal` | Normal de la piel por diferencias centradas de 1 cm |
| `fusPatch` | Anillos con `z = Z0 + (Z1 − Z0)·u^1,3`, `NZ = max(4, redondeo(70·(u1 − u0)))`, `NT = max(3, redondeo(96·(θ1 − θ0)/2π))` |
| `fusCap(z)` | Tapa en abanico, 96 lados: **dos**, la cara roma del morro (`Fuselage_Front`, z −3,6) y la trasera (`Fuselage_Rear`, z 2,2) |
| `canPt`, `canardGeos` (`CAN`, `CT`) | Canard: planta bilineal entre A (0,30, 0,13, −3,05), B (2,47, 0, −2,62), C (2,47, 0, −1,70) y D (0,55, 0,13, −1,50); grosor 0,15 → 0,07 por la envergadura con perfil `0,3 + 0,7·min(1, t/0,12, (1 − t)/0,22)`; cuerdas en `CT` = [0; 0,12; 0,35; 0,6; 0,78; 1], 12 tramos de envergadura. **Facetado** (sombreado plano) |
| `slab(pts, O, U, V, t, bisel)` | Polígono plano en la base (O, U, V) extruido `t` a lo largo de U × V, centrado, con bisel de 2 segmentos. Puntas caídas (`DROOP`, t 0,05, bisel 0,006), aletas (`FIN`, t 0,05, bisel 0,008) y sus franjas. Con el modificador Bevel de Blender basta |
| `profileExtrude(perfil, semiancho, bisel)` | Perfil (z, y) extruido a lo ancho, centrado en x, con bisel. Cabina (`CABP`, 0,60, 0,015) y compartimento del motor (`BAYP`, 0,42, 0,015). Con Bevel basta |
| `gridGeo` | Rejilla facetada con UV en metros (z, x) |
| `polyTube` | Tubo por una polilínea: cada tramo se divide en 6 y pasa por **Catmull-Rom centrípeta**, 10 lados (8 en las juntas y 6 en las soldaduras) |
| `beacon` | Baliza: base `rbox` 0,09 × 0,025 × 0,09, lente esfera de r 0,035 (14 × 10) a 0,06 m, y 4 **medios toros** de R 0,042 · r 0,003 (6 × 16) girados de 45° en 45° |
| `workLight` | Foco: carcasa `rbox` 0,13 × 0,075 × 0,07, soporte y cristal de 0,11 × 0,055 m |
| `lathe`, `rbox`, `boxUV`, tubos, cilindros (6, 12 y 16 lados), esferas y toros | Los de VECTOR, BLADE y las cabinas, con las mismas medidas y lados |

Partes del casco (detalle en el prototipo):

- **`Fuselage`**:
  - piel de arriba (`Fuselage_Upper`, θ 0 → π, con pintura) y de abajo (`Fuselage_Lower`, θ π → 2π, chapa oscura);
  - cara del morro (`Fuselage_Front`, chapa oscura) y tapa trasera (`Fuselage_Rear`);
  - banda de chapa lagrimada en el lomo del morro (`Nose_Tread`, z −3,35 → −1,92, a 4 mm);
  - juntas de panel (`Seam_0` … `Seam_4`);
  - balizas ámbar de los hombros (`Light_Nose_L/R`, en la arista a z −2,4);
  - patines bajo la panza (`Skid_L/R`);
  - pilotos traseros en la tapa (`Light_TailBar_L/R`).
- **`Canard_L` / `Canard_R`**:
  - cara de arriba (`_Top`, pintura), de abajo (`_Bottom`, chapa oscura), borde de ataque (`_LeadingEdge`: franjas con marcas, chapa oscura sin ellas), borde de salida (`_TrailingEdge`) y cierre de la punta (`_TipEnd`);
  - punta caída (`_Droop`, pintura) y su patín (`_Skid`);
  - costilla de la raíz (`_Rib`);
  - baliza ámbar (`Light_Canard_*`) y el punto de la estela (`Trail_*`, vacío).
- **`Fin_L` / `Fin_R`**: placa (`_Plate`, pintura), franja de peligro arriba (`_Hazard`, **solo con marcas**) y piloto rojo (`Light_Tail_*`).
- **`Engine_L` / `Engine_R`** (en x = ±0,45, y = 0,20):
  - barril de titanio (`_Barrel`, torno de 40 lados, z 1,70 → 2,58) con collarín (`_Collar`) y 3 anillos de refrigeración (`_CoolRing_0/1/2`, en z 2,34, 2,42 y 2,50);
  - tobera (`Nozzle_*`, z 2,58 → 3,00);
  - fondo luminoso (`Nozzle_Glow_*`, círculo de r 0,205 a z 2,821) y núcleo (`Nozzle_Core_*`, r 0,10 a z 2,83);
  - la boca (`Exhaust_*`, vacío).
- **`Cab`**:
  - cuerpo (`Cab_Body`, pintura de la cabina);
  - parabrisas: fondo (`Cab_WindshieldBack`), cristal (`Cab_Windshield`) y junta (`Cab_WindshieldSeal`);
  - ventanas laterales: fondo (`Cab_SideBack_*`), cristal (`Cab_SideWindow_*`) y junta (`Cab_SideSeal_*`);
  - limpiaparabrisas (`Wiper_Pivot`, `Wiper_Arm`, `Wiper_Blade`);
  - barandillas del techo (`Cab_RoofRail_*`) y asideros (`Cab_Grab_*`, con franjas);
  - rótulo (`Decal_Cab_*`, **solo con marcas**);
  - balizas (`Beacon_Boost` a la izquierda y `Beacon_Damage` a la derecha, en (±0,40, 1,055, −1,30));
  - 4 focos (`Work_0` … `Work_3`, en x −0,24, −0,08, 0,08 y 0,24, z −1,44).
- **`EngineBay`**:
  - cuerpo (`EngineBay_Body`, pintura del casco), de z 0,55 a 1,90;
  - tapa de registro (`EngineBay_Hatch`) con 4 tornillos;
  - rejilla de admisión (`EngineBay_IntakeBack` y 7 `EngineBay_IntakeSlat`);
  - lamas a cada lado (`EngineBay_Vent_*`, 6 `EngineBay_VentSlat` y 4 `EngineBay_Bolt`);
  - franja de peligro del frente (`EngineBay_Hazard`, **solo con marcas**).
- **`Extras`**:
  - defensa tubular (`BullBar_Top`, `BullBar_Bottom`, `BullBar_Post_0/1/2`, `BullBar_Side_*`);
  - rejilla del parabrisas (`Guard_Bar_0/1/2`, `Guard_Cross_0/1`, 4 `Guard_Standoff`);
  - rejas de las ventanas laterales (`SideGuard_*_Bar_0/1`, `SideGuard_*_Cross`, 4 `SideGuard_Standoff`);
  - arcos antivuelco (`RollBar_0/1`, en z −1,24 y −0,60);
  - escape vertical (`Stack`, 2 `Stack_Clamp`, `Stack_Cap`, `Stack_Mouth`), en (0,40, —, −0,06);
  - escalerillas (`Ladder_*`: 2 largueros, 4 peldaños y 2 soportes, en z −0,92);
  - tuberías de cobre (`Pipe_Front_*`, `Pipe_Rear_*`) y 2 abrazaderas (`Pipe_Clamp`).
- **`Markings`** (piezas que **solo se ven con marcas**, sección 6.3):
  - número de unidad (`Decal_Unit_L/R`, 0,44 × 0,34 m en la cara de fuera de cada aleta);
  - logotipo (`Decal_Logo_L/R`, 0,46 × 0,46 m encima del canard);
  - NO STEP (`Decal_NoStep_L/R`, 0,44 × 0,14 m encima del canard, hacia la punta);
  - HOT (`Decal_Hot_L/R`, 0,17 × 0,055 m en los costados del compartimento, detrás de las lamas).
- **`Smoke`** (vacío nuevo, no está en el prototipo como pieza): en la boca del escape, (0,40, 1,33, −0,06).

Reglas de las UV (en **metros × repetición**, sin `KHR_texture_transform`):
- **Piel**: `(z, θ × radio medio)`, como VECTOR y BLADE. **Rejillas** (canard, banda del morro): `(z, x)`. **Tornos**: `(z, ángulo × 0,4)`.
- **Piezas extruidas** (cabina, compartimento, puntas caídas, aletas) y **primitivas**: proyección de caja en metros (`boxUV`). El prototipo usa las UV por defecto de three.js en las extruidas; si con `boxUV` la chapa sale girada o estirada en alguna cara, se avisa.
- **Calcomanías**, **UV 0–1** con la orientación del prototipo (los planos de three.js: U a la derecha y V arriba vistos desde su cara):
  - número: se lee desde fuera de cada aleta, con el morro delante;
  - logotipo y NO STEP: se leen desde arriba, con el morro arriba;
  - HOT y rótulo de la cabina: se leen desde el costado, con el morro delante.
- **Segunda capa de UV (UV2)** en todas las piezas con pintura, chapa oscura, chapa lagrimada, franjas, acero, titanio y cobre: las mismas UV en metros **sin la repetición** y × **1,1** (quemaduras, sección 6.5).
- **Caras por los dos lados**, sombreado suave (salvo canard, extruidas, cajas y planos) y piezas `noShadow` sin sombra, igual que VECTOR y BLADE.

### 3.5 Materiales y texturas

**Principled BSDF** con texturas de imagen, sin nodos procedurales. Texturas a **512 × 512** generadas **en Python** con el **mismo algoritmo, semillas y orden de llamadas** del prototipo.

- **Ojo con los redondeos** (como en la cabina de HAMMER): `scratchMap` usa `Math.round`, que redondea las mitades **hacia arriba**. En Python, `math.floor(x + 0.5)`, no `round()`.
- `plateSet(82)`, `steelSet(83)`, `treadSet(84)` y `hazardSet(85)` son **los de la cabina de HAMMER**, con las mismas semillas: salen **las mismas imágenes**. Se reutilizan si ya están guardadas; si no, se generan con el mismo código.

**La pintura es una máscara, no un color.** La pintura cambia en el juego (20 pinturas, casco y cabina por separado), así que Blender **no** guarda el color. `plateSet` es lineal en el color de la pintura:

```
color = pintura × MUL + ADD
```

- **MUL** (un canal) = `k · (1 − desconchón) · (1 − 0,35 · arañazo) · junta`, con `junta` = 0,35 en las juntas y 1 fuera. Va de 0 a 1,12: se guarda como **MUL / 1,25**.
- **ADD** (RGB) = lo que no depende de la pintura: el metal de los desconchones (118, 116, 108), los arañazos (172, 168, 156) y el brillo de los remaches (+26), con la misma junta.
- La rugosidad y la normal **no** dependen del color, así que se guardan una vez.
- Godot reconstruye el color (sección 6.2) y sale **el mismo píxel** que el prototipo (±1).

| Archivos | Llamada del prototipo | Para |
|---|---|---|
| `hull_hammer_body_mul.png`, `_add.png` + rugosidad y normal | `plateSet(87, …)` | Casco, canard, puntas caídas, aletas y compartimento |
| `hull_hammer_cab_mul.png`, `_add.png` + rugosidad y normal | `plateSet(89, …)` | Cabina |

Resto de texturas (color, rugosidad y normal):

| Conjunto | Llamada del prototipo | Qué es |
|---|---|---|
| Chapa oscura | `plateSet(82, [50, 52, 48])` | La de la cabina 3D |
| Acero | `steelSet(83)` | La de la cabina 3D |
| Chapa lagrimada | `treadSet(84)` | La de la cabina 3D |
| Franjas de peligro | `hazardSet(85)` | La de la cabina 3D |
| Titanio | `brushedSet(32, [150, 148, 146], true)` | El de VECTOR (se reutiliza) |
| Cobre | `brushedSet(33, [170, 100, 60], false)` | El cepillado de VECTOR con otro color y semilla |
| Quemaduras | `scorchTex(41)` | Las de VECTOR (ya están) |

| Material | Base | Metal | Rugosidad | Repetición | Extra |
|---|---|---|---|---|---|
| `M_Paint` | máscara del casco (Godot pone el color) | 0,35 | textura | ×0,5 | Normal ×0,8, reflejos ×0,6. **Godot: shader de la pintura** (6.2). También `M_Paint_Canard` y `M_Paint_Fin` (×0,5) y `M_Paint_Bay` (×0,8) |
| `M_PaintCab` | máscara de la cabina (Godot pone el color) | 0,35 | textura | ×0,8 | Igual que `M_Paint` |
| `M_PlateDark` | chapa oscura | 0,55 | textura | ×0,5 (`_Bay` ×0,8) | Normal ×0,8, reflejos ×1. Panza, cara del morro, bajo y bordes del canard, compartimento |
| `M_Steel` | acero | 1 | textura | ×1,5 | Tornillos, patines, peldaños, lamas, escape, collarines |
| `M_Titanium` | titanio | 1 | textura | ×2 | Barriles, anillos y toberas |
| `M_Copper` | cobre | 1 | textura | ×2 | Tuberías |
| `M_Tread` | chapa lagrimada | 0,9 | textura | ×2 | Banda del morro |
| `M_Hazard` | franjas | 0,3 | textura | ×1,2 | Borde del canard, aletas, compartimento, asideros |
| `M_BlackPaint` | `#17171A` | 0,35 | 0,5 | — | Defensa, rejillas, rejas, arcos, escalerillas, carcasas de los focos, abrazaderas del escape |
| `M_Rubber` | `#141414` | 0 | 0,78 | — | Juntas de los cristales y escobilla |
| `M_Dark` | `#0B0B0A` | 0,2 | 0,8 | — | Tapa trasera, juntas de panel, costilla |
| `M_Intake` | `#050506` | 0,1 | 0,9 | — | Fondo de lamas y rejilla, boca del escape |
| `M_CabInterior` | `#07080A` | 0 | 0,9 | — | Fondo de los cristales |
| `M_CabGlass` | `#0A0D10` al 55 % | 0,1 | 0,05 | — | Transparente, barniz 1, reflejos ×1,6 |
| `M_BeaconBase` / `M_BeaconCage` | `#202020` / `#8A8882` | 0,5 / 1 | 0,6 / 0,35 | — | Sin nombre en el prototipo: se nombran así |
| `M_Beacon` | `#1A0D04` + emisión | 0 | 0,4 | — | Una instancia por baliza (sección 6.4) |
| `M_Decal_*` | negro (la textura la pone Godot) | 0 | 0,6 | — | Transparente con alfa |
| `M_Emissive_*` | negro + emisión | 0 | 0,4 | — | **Una instancia por pieza viva** (sección 3.8) |
| `M_Emissive_NozzleGlow` / `_NozzleCore` | `#FF5A14` · 2,2 / `#FFB46A` · 3,5 | 0 | 0,4 | — | **Naranja** (los de VECTOR y BLADE son azules) |

### 3.6 Exportación

Igual que VECTOR y BLADE:
- `.glb`, +Y arriba, modificadores aplicados;
- normales, **las dos capas de UV** y texturas **dentro**;
- sin cámaras, luces ni animaciones;
- solo lo que cuelga de `HullHammer`, **con** `Markings`, las piezas que solo existen con marcas y los vacíos (`Exhaust_*`, `Trail_*`, `Smoke`).

### 3.7 Capturas de previsualización

En `art/blender/out/previews/`, 1280 × 720, fondo oscuro y la luz de VECTOR. Las cámaras son las del prototipo **con 0,75 m menos de altura**:

| Captura | Cámara | Mira a | FOV vertical |
|---|---|---|---|
| `hull_hammer_tq.png` | (−8,2, 3,4, −7,4) | (0, 0,3, −0,4) | 38° |
| `hull_hammer_top.png` | (0, 17,75, −0,4) | (0, 0, −0,4) | 34° |
| `hull_hammer_side.png` | (12,5, 0,5, −0,4) | (0, 0,3, −0,4) | 34° |
| `hull_hammer_rear.png` | (5, 2,2, 13,5) | (0, 0,3, 0,6) | 34° |
| `hull_hammer_markings.png` | la de `tq` | — | 38° |

- Todas con AMARILLO OBRA con marcas, coloreada con la regla de la sección 6.2 (en Blender, con un material de prueba).
- En `hull_hammer_markings.png` las calcomanías llevan una **textura de prueba** (cuadrícula con una flecha hacia arriba), para comprobar que **ninguna sale en espejo ni girada**. Desde esa vista se ven el número de la aleta izquierda, el logotipo y el NO STEP del canard izquierdo y los rótulos de la izquierda.

El agente **mira las capturas** antes de cerrar la etapa y las compara con el prototipo abierto en el navegador, en la misma vista, con AMARILLO OBRA con marcas.

### 3.8 Nombres de las piezas (contrato con Godot)

Godot busca estas piezas **por nombre**. El script comprueba antes de exportar que **están todas** y, si falta una, **falla con un mensaje claro**.

| Nombre | Qué es | Cuántas |
|---|---|---|
| `HullHammer` | Raíz (origen = el del casco wireframe) | 1 |
| `Exhaust_L`, `Exhaust_R` | Vacíos en la boca de cada tobera: (±0,45, 0,20, 3,00) | 2 |
| `Trail_L`, `Trail_R` | Vacíos de las estelas: (±2,50, −0,50, −2,20) | 2 |
| `Smoke` | Vacío del humo: (0,40, 1,33, −0,06) | 1 |
| `Fuselage_Upper`, `Canard_L/R_Top`, `Canard_L/R_Droop`, `Fin_L/R_Plate`, `EngineBay_Body` | Piezas con la pintura del casco (`M_Paint*`) | 8 |
| `Cab_Body` | Pieza con la pintura de la cabina (`M_PaintCab`) | 1 |
| `Canard_L/R_LeadingEdge` | Franjas con marcas, chapa oscura sin ellas | 2 |
| `Fin_L/R_Hazard`, `EngineBay_Hazard` | Franjas que **solo se ven con marcas** | 3 |
| `Light_Nose_L/R`, `Light_Canard_L/R` | Balizas ámbar | 4 |
| `Light_Tail_L/R`, `Light_TailBar_L/R` | Pilotos rojos | 4 |
| `Light_Work_0` … `Light_Work_3` | Cristales de los focos | 4 |
| `Beacon_Boost_Lens`, `Beacon_Damage_Lens` | Lentes de las balizas del techo | 2 |
| `Nozzle_Glow_L/R`, `Nozzle_Core_L/R` | Fondo luminoso de las toberas | 4 |
| `Cab_Windshield`, `Cab_SideWindow_L/R` | Cristales | 3 |
| `Decal_Unit_L/R` | Número de unidad, UV 0–1 | 2 |
| `Decal_Logo_L/R` | Logotipo, UV 0–1 | 2 |
| `Decal_NoStep_L/R`, `Decal_Hot_L/R` | Avisos, UV 0–1 | 4 |
| `Decal_Cab_L/R` | Rótulo HMR-03, UV 0–1 | 2 |

El resto se agrupa por zonas (`Fuselage`, `Canard_L/R`, `Fin_L/R`, `Engine_L/R`, `Cab`, `EngineBay`, `Extras`, `Markings`), con los nombres del prototipo dentro. Las piezas repetidas (tornillos, lamas, peldaños, jaulas) pueden llevar sufijo numérico. Las balizas y los focos son grupos (`Beacon_*`, `Work_*`) con sus piezas dentro.

**Manifiesto** (`hull_hammer.manifest.json`), con el formato del de BLADE:
- Versión de Blender y fecha.
- Piezas vivas de la tabla con su color de emisión e intensidad de partida (sección 6.4), y los vacíos con su posición.
- Qué piezas llevan pintura del casco, pintura de la cabina, piezas de solo-marcas, bordes que cambian con las marcas y calcomanías.
- Piezas sin sombra, materiales con barniz, reflejos distintos de 1 y transparencia.
- **Caja total** y caja **sin los extras del techo** (coordenadas de Godot) y **semiancho** (mayor |x|).
- Triángulos totales sin marcas y con marcas. Como referencia, el prototipo da **≈ 67 900** y **≈ 68 000**.

## 4. Etapa 2 · El casco en carrera

### 4.1 Importación

Igual que VECTOR y BLADE: nombres tal cual, sin colisiones, sin LOD ni luz horneada. Al cargar, `ShipHull3D` aplica lo del manifiesto. Si algo no llega bien, se arregla **en Godot con su material**, sin tocar la geometría, y se avisa.

- Si el inventario (punto 11) ve que las ~200 piezas pesan, se pueden **unir al importar** las piezas que no están vivas y comparten material (tornillos, lamas, peldaños, juntas…). Las del contrato (3.8) **no** se unen. Se avisa antes.

### 4.2 Perfil visual de HAMMER

- El perfil visual de HAMMER gana su **casco 3D** (`.glb` y manifiesto) y su **catálogo de pinturas** (sección 6.1).
  - VECTOR → 3D Nocturna;
  - BLADE → 3D Asalto;
  - HAMMER → 3D Minero.
- **El casco wireframe de HAMMER se queda** en el código y en su prueba de captura, porque lo sigue usando el **fantasma** (y el **holograma** hasta la etapa 4).
- **Toberas**: la llama, el núcleo y el halo se colocan en `Exhaust_L/R`, con escala **× 0,95** (redonda) respecto a la llama de referencia de VECTOR (como `ex.scale` del prototipo). Sustituye al × 1 por radio de la spec `naves-3-cascos`.
- **Color de la llama**: HAMMER tiene el fondo de las toberas **naranja**, así que la llama sin boost también:
  - llama `#FF5A14` y núcleo `#FFB46A`, en lugar de los azules de `efectos-velocidad`;
  - con boost, el **mismo naranja de boost** que las otras naves (no cambia).
  - Va como un color **por nave** en el perfil visual (VECTOR y BLADE: los azules de siempre).
  - Si no se puede sin tocar la lógica de los efectos, **se avisa antes** (punto 5 del inventario).
- **Estelas**: salen de `Trail_L/R` (las puntas caídas del canard, el mismo punto que hoy).
- **Cámara exterior**: el factor de HAMMER **sigue en 0,92** (el largo pasa de 6,6 a 6,83 m). Se ajusta probando si hace falta.
- Los puntos se leen del manifiesto o de los vacíos, **no** se copian a mano. Una prueba comprueba que el perfil y el manifiesto coinciden.

### 4.3 Luz y reflejos

**Los mismos del casco de VECTOR y BLADE**:
- capa visual propia;
- las mismas luces colgadas de la nave;
- el mismo panorama de reflejos y glow.

No se crea nada nuevo. Si la chapa pintada necesita otra intensidad, se ajusta **por nave** en el Inspector y se avisa.

La **iluminación del circuito** (luces de los bordes, túneles y ciudad que tiñan las naves, sombra en la pista) **no** entra aquí: el usuario la deja para más adelante.

### 4.4 Vista de cabina

Igual que VECTOR y BLADE: en la vista de cabina, el casco exterior de HAMMER **no se dibuja**. La cabina de grúa, las rejillas, los arcos y el escape tampoco: la cabina 3D ya tiene lo suyo.

### 4.5 Ancho de colisión

- El ancho de colisión de HAMMER **se queda en 2,50 m**: el semiancho del manifiesto (2,502, las balizas de las puntas caídas) redondeado al centímetro es el mismo que el del wireframe.
- La prueba de `naves-3-cascos` (**ancho de colisión = semiancho real del casco**) mira ya el **manifiesto** para las tres naves.

### 4.6 Equilibrio

No cambia el ancho de colisión, así que no debería notarse.
- Al cerrar la etapa, el agente ejecuta el **simulador de equilibrio** de la spec `naves-6-equilibrio` con HAMMER y apunta los tiempos por vuelta antes y después.
- **No se toca ningún número** de las naves: si HAMMER se sale del margen, **se avisa** con los datos.

### 4.7 Qué se ve en cada caso

| | VECTOR | BLADE | HAMMER |
|---|---|---|---|
| Cámara exterior (jugador) | Casco 3D | Casco 3D | **Casco 3D** |
| Vista de cabina | Cabina 3D (casco oculto) | Cabina 3D (casco oculto) | Cabina 3D (**casco oculto**) |
| Fantasma | Wireframe | Wireframe | Wireframe |
| Naves de prueba (F7) | Wireframe magenta | — | — |
| Menú principal | Wireframe (sin cambios) | — | — |
| Holograma del selector | Casco 3D | Casco 3D | Wireframe hasta la etapa 4; **casco 3D** después |

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lo de VECTOR y BLADE **no cambia de aspecto**:

- **Perfil visual de nave**: casco 3D, manifiesto, escala de la llama, **color de la llama sin boost** (nuevo, sección 4.2), catálogo de pinturas y tamaño del brillo de levitación.
- **`ShipHull3D`**: el mismo de VECTOR y BLADE. Si no es del todo genérico (punto 2 del inventario), se generaliza **sin cambiar** cómo se ven las otras dos naves:
  - **grupos de pintura** con nombre (en HAMMER, `body` y `cab`) que el aplicador de cada nave rellena;
  - piezas que **solo se ven con marcas**, leídas del manifiesto;
  - **balizas** con estado (`SetBeacons(boost, nivelDeDaño, timeTrial)`), igual de genérico que `SetStrobes` de BLADE: si la nave no tiene balizas, no hace nada.
- **Catálogo de pinturas de HAMMER** (puro, datos): sección 6.1.
- **Aplicador de pintura de HAMMER** (puro): de (pintura, con marcas) a lo que hay que poner en cada grupo (sección 6.2).
- **Dibujantes de calcomanías de HAMMER** (puros): número, logotipo, avisos y rótulo (sección 6.3).
- **Balizas** (puras): de (boost, daño, TIME TRIAL, tiempo) a la intensidad de cada lente (sección 6.4). Si la cabina 3D de HAMMER ya tiene esta lógica, **se reutiliza** la misma.
- **Daño visible** (puro): el de VECTOR y BLADE, con la lista de luces que titilan **leída del manifiesto**.
- Los colores fijos (balizas, pilotos, focos, toberas, marcas, metal de los desconchones y los arañazos) van como **constantes con nombre**.

## 6. Etapa 3 · Pinturas, marcas, luces y daño

### 6.1 Catálogo de pinturas

Es `PAINTS` del prototipo. **20 pinturas**, cada una **sin marcas** y **con marcas**: **40 combinaciones**, en este orden: las 20 sin marcas y luego las 20 con marcas.

Lisas (casco y cabina del mismo color), en RGB 0–255:

| # | `id` | Nombre | Color |
|---|---|---|---|
| 1 | `site_yellow` | AMARILLO OBRA | 170, 132, 40 |
| 2 | `bright_yellow` | AMARILLO VIVO | 196, 156, 30 |
| 3 | `orange` | NARANJA | 196, 98, 30 |
| 4 | `terracotta` | TEJA | 160, 70, 40 |
| 5 | `tractor_green` | VERDE TRACTOR | 52, 98, 48 |
| 6 | `crane_red` | ROJO GRÚA | 150, 36, 30 |
| 7 | `fleet_white` | BLANCO FLOTA | 206, 204, 196 |
| 8 | `site_blue` | AZUL OBRA | 40, 74, 128 |
| 9 | `plant_grey` | GRIS PLANTA | 112, 114, 110 |
| 10 | `coal_black` | NEGRO CARBÓN | 36, 36, 38 |
| 11 | `khaki` | CAQUI | 140, 128, 90 |
| 12 | `safety_green` | VERDE SEGURIDAD | 150, 170, 40 |
| 13 | `cream` | CREMA | 196, 182, 140 |
| 14 | `maroon` | GRANATE | 104, 34, 36 |
| 15 | `petrol_blue` | AZUL PETRÓLEO | 30, 78, 86 |

Bicolor (la cabina de otro color):

| # | `id` | Nombre | Casco | Cabina |
|---|---|---|---|---|
| 16 | `yellow_black` | AMARILLO · CABINA NEGRA | 170, 132, 40 | 36, 36, 38 |
| 17 | `orange_white` | NARANJA · CABINA BLANCA | 196, 98, 30 | 206, 204, 196 |
| 18 | `white_red` | BLANCO · CABINA ROJA | 206, 204, 196 | 150, 36, 30 |
| 19 | `green_white` | VERDE · CABINA BLANCA | 52, 98, 48 | 206, 204, 196 |
| 20 | `grey_orange` | GRIS · CABINA NARANJA | 112, 114, 110 | 196, 98, 30 |

- Todas con el mismo acabado (el de la tabla 3.5); no hay satinadas.
- **Pintura por defecto**: AMARILLO OBRA **con marcas** (la 21 en el orden de arriba, la que sale al abrir el prototipo).
- Por ahora **no se guarda** ni se elige en ningún menú (selector de color: spec futura). Los rivales podrán llevar cualquiera.
- Los nombres son internos: si un día salen en pantalla, se revisan.

### 6.2 Cómo se aplica una pintura

- **Casco** (`M_Paint`, `M_Paint_Canard`, `M_Paint_Fin`, `M_Paint_Bay`): color = `body`.
- **Cabina** (`M_PaintCab`): color = `cab` si la pintura lo tiene; si no, `body` (`cabOf`).
- **Shader de la pintura**: `color = pintura × (MUL × 1,25) + ADD`, recortado a 0–255, con la máscara del casco o de la cabina. Lleva también las **quemaduras** (sección 6.5).
- Se compara con el prototipo **píxel a píxel** en una prueba (sección 9).
- **No cambian** con la pintura: panza, cara del morro, bajo del canard, motores, toberas, tuberías, defensas, rejas, arcos, escalerillas, escape, cristales y luces.

**Depuración**: la tecla de pinturas de VECTOR y BLADE (**F6** / **Mayúsculas + F6**) recorre las 40 combinaciones de la nave que se lleve. El nombre (y `· MARCAS`) sale en el panel (F9). Solo en compilaciones de depuración.

### 6.3 Marcas de flota

Con marcas, sobre la misma pintura:

- **Se ven** las piezas `Decal_*`, `Fin_L/R_Hazard` y `EngineBay_Hazard`. Sin marcas, **no se dibujan**.
- **Borde del canard** (`Canard_*_LeadingEdge`): con marcas, `M_Hazard`; sin marcas, `M_PlateDark`.
- **Color de las marcas** (`markCol`): si la suma R + G + B de `body` es **mayor que 360**, `#141412`; si no, `#E8E2D0`.
  - Salen oscuras en AMARILLO VIVO, BLANCO FLOTA, CREMA y BLANCO · CABINA ROJA.
  - Salen claras en el resto (también en AMARILLO OBRA).

Calcomanías, dibujadas **una vez** por Godot al cambiar de pintura, con el mismo dibujo que el prototipo:

| Pieza | Lienzo | Dibujo (como el prototipo) |
|---|---|---|
| `Decal_Unit_L/R` (`unitTex`) | 512 × 384, transparente | `03` en **Black Ops One de 300 px**, centrado en (256, 206), en el color de las marcas |
| `Decal_Logo_L/R` (`logoTex`) | 512 × 512, transparente | Hexágono de radio 228 y 24 px de grueso (vértices a π/6 + i·π/3); tres picos rellenos (96, 300) (186, 150) (236, 230) (296, 120) (416, 300); `M·7` en Black Ops One de 120 px centrado en (256, 392). Todo en el color de las marcas |
| `Decal_NoStep_L/R`, `Decal_Hot_L/R` (`warnTex`) | 512 × 160, transparente | Triángulo (34, 128) (84, 32) (134, 128) relleno con un `!` vaciado (Share Tech Mono 700 de 70 px en (84, 96)) y el texto `NO STEP` o `HOT` en Black Ops One de 78 px desde x 160, ancho máximo 332 |
| `Decal_Cab_L/R` (`stencilTex`) | 512 × 128, transparente | Recuadro de 6 px a 10 px del borde y `HMR-03` en Black Ops One de 74 px centrado, ancho máximo 462 |

- El logotipo es **inventado**: no es de ninguna empresa.
- El número es el mismo en todas las pinturas. Un selector futuro podrá cambiarlo sin tocar el modelo.

### 6.4 Luces

Valores del prototipo (se ajustan a ojo con el glow del juego y se apuntan):

| Luces | Color | Intensidad |
|---|---|---|
| Balizas ámbar (`Light_Nose_*`, `Light_Canard_*`) | `#FF9A1A` | 2,4 |
| Pilotos de las aletas (`Light_Tail_*`) | `#FF2A1E` | 2,4 |
| Pilotos de la tapa (`Light_TailBar_*`) | `#FF2A1E` | 2,2 |
| Focos (`Light_Work_*`) | `#FFF2DA` | 2,0 |
| Fondo / núcleo de las toberas | `#FF5A14` / `#FFB46A` | 2,2 / 3,5 (× 1,6 con boost) |

**Balizas del techo** (como las de la cabina 3D de HAMMER):
- **Ámbar** (`Beacon_Boost_Lens`, `#FF9A1A`): con el boost activo, **4 + 1,5 × sen(20 × t)**; si no, 0,05.
- **Roja** (`Beacon_Damage_Lens`, `#FF2A10`): con daño 2, fija a **5**; con daño 3, a 5 con el **parpadeo del daño (2 Hz)** y a 0,05 en la otra mitad; si no, 0,05. En TIME TRIAL, siempre 0,05.
- **No** titilan con el daño (6.5). La pausa las congela.
- En el Inspector: las intensidades encendida y apagada.

### 6.5 Daño visible
> **Sustituido** (spec `efectos-5-danos-casco`): las **quemaduras** de esta sección y sus máscaras (`hull_scorch_1/2/3.png`) ya no existen; las sustituyen rozaduras donde se choca, hollín alrededor de los focos del humo y grietas incandescentes con daño 3. El **humo** es el de `efectos-4-humo-danos`. El **parpadeo de las luces** sigue como aquí.


Las mismas reglas que VECTOR y BLADE (spec `cascos-3d-1-vector`, sección 6.5). Solo en RACE; en TIME TRIAL, nada.

- **Quemaduras**:
  - en pintura, chapa oscura, chapa lagrimada, franjas, acero, titanio y cobre;
  - no en luces, cristales ni calcomanías;
  - en las pinturas van **dentro del shader de la pintura**: oscurece hacia (0,018, 0,014, 0,012) + color × 0,06 y la rugosidad sube hacia 0,85, como el prototipo;
  - en los demás, con el detalle de `StandardMaterial3D` como en VECTOR.
- **Parpadeo**: desde el nivel 1, las **12 luces** `Light_Nose_*`, `Light_Canard_*`, `Light_Tail_*`, `Light_TailBar_*` y `Light_Work_*` titilan bajando al 20 %, con las reglas de `danos`. Las balizas del techo y las toberas **no** titilan.
- **Humo** desde `Smoke` (la boca del escape vertical), con los números de VECTOR (7 / 20 partículas por segundo en los niveles 2 / 3, mismos colores, vida y tamaños).

### 6.6 Brillo de levitación

El de VECTOR con otro tamaño:
- un plano de **3,4 × 7,4 m**, centrado en **z = −0,5**;
- mismo color, caída, intensidad, latido y apagado en el aire.

El tamaño va en el perfil visual de cada nave.

## 7. Etapa 4 · Holograma del selector

- SELECT SHIP enseña el **casco 3D** de HAMMER con la pintura por defecto, en lugar del wireframe. Es el mismo `ShipHull3D`, sin daño, humo, llamas, estelas ni levitación. Las **balizas** del techo, **apagadas** (0,05).
- **Giro, luz fija respecto a la pantalla y reflejos**: los del holograma de VECTOR y BLADE.
- **Escala común**: la de siempre, fija.
- **Prueba de la esfera de 10 m** (`naves-4-integracion`, ampliada en VECTOR): HAMMER da **8,67 m** con todo (la diagonal de su caja). La prueba cuenta la nave **entera**, sin excepciones.
  - Si al cambiar algo del casco la esfera pasa de 10 m, **se avisa** antes de tocar la escala.
- Con esta etapa, **ninguna nave** enseña ya el wireframe en el selector.

## 8. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Intensidad de las luces del casco | Las de VECTOR y BLADE (ajustables por nave) | Casco 3D |
| Intensidad de balizas ámbar / pilotos / focos / toberas | Tabla 6.4 | Casco 3D |
| Balizas del techo (encendida / apagada) | 4 ± 1,5 y 5 / 0,05 | Casco 3D |
| Escala de la llama | 0,95 × 0,95 × 1 | Perfil visual de HAMMER |
| Color de la llama sin boost (llama / núcleo) | `#FF5A14` / `#FFB46A` | Perfil visual de HAMMER |
| Ancho de colisión de HAMMER | 2,50 m | Perfil técnico (del manifiesto) |
| Brillo de levitación (tamaño, centro) | 3,4 × 7,4 m · z −0,5 | Perfil visual de HAMMER |
| Pintura por defecto | `site_yellow`, con marcas | Casco 3D |

## 9. Pruebas

### Pruebas unitarias mínimas

1. **Manifiesto**: tiene todas las piezas y vacíos de la tabla 3.8 con las cantidades exactas. Los vacíos están donde dice la tabla (±1 mm).
2. **Medidas** del manifiesto (±0,01 m):
   - largo **6,83 m**;
   - semiancho **2,50 m**;
   - alto **1,87 m** con todo y **1,60 m** sin los extras del techo;
   - caja en z −3,83 → 3,00 y en y −0,52 → 1,35.
3. **Perfil visual**:
   - HAMMER → casco 3D, con su escala y su color de llama y su catálogo;
   - VECTOR y BLADE → igual que antes (llama azul);
   - el fantasma de HAMMER → wireframe;
   - toberas y estelas del perfil = vacíos del manifiesto.
4. **Colisión**: ancho de colisión = semiancho real del manifiesto para las tres naves, ±0,01 m.
5. **Catálogo de HAMMER**:
   - 20 pinturas con `id` únicos;
   - 15 lisas y 5 bicolor;
   - 40 combinaciones en orden, con vuelta adelante y atrás;
   - por defecto `site_yellow` con marcas.
6. **Aplicación de pintura**:
   - cabina = `cab` en las bicolor y `body` en las lisas: NARANJA · CABINA BLANCA → casco (196, 98, 30), cabina (206, 204, 196);
   - con marcas se ven `Decal_*`, `Fin_*_Hazard` y `EngineBay_Hazard` y el borde del canard es `M_Hazard`; sin marcas, no se ven y el borde es `M_PlateDark`;
   - color de las marcas: AMARILLO OBRA → `#E8E2D0`, AMARILLO VIVO → `#141412`, BLANCO FLOTA → `#141412`, NEGRO CARBÓN → `#E8E2D0`.
7. **Shader de la pintura**: para unos cuantos píxeles de la máscara (con desconchón, con arañazo, en una junta y en un remache), el color reconstruido con AMARILLO OBRA y con BLANCO FLOTA es igual al del prototipo (±1 en 0–255).
8. **Calcomanías**: el número es `03`, el rótulo `HMR-03` y los avisos `NO STEP` y `HOT`, con el color de las marcas, centrados como en la tabla 6.3.
9. **Balizas**: ámbar solo con boost; roja fija con daño 2, parpadeando con daño 3, apagada con 0–1 y en TIME TRIAL; ninguna está en la lista de parpadeo.
10. **Daño visible**: el parpadeo solo elige entre las 12 luces de la sección 6.5; humo 0 / 0 / 7 / 20; en TIME TRIAL, nada.
11. **Holograma**: escala fija; las esferas de las tres naves (HAMMER: 8,67 m) no pasan de 10 m.

Las pruebas existentes siguen pasando: las de VECTOR y BLADE y la captura del wireframe de HAMMER, que se queda.

### Comprobación del script de Blender

- El script falla si falta una pieza del contrato y comprueba los puntos de la sección 3.3.
- Dos ejecuciones seguidas dan el **mismo** `.glb`.
- Los `.glb` de VECTOR, BLADE y las tres cabinas **no cambian** después de tocar `lib/`.
- **Texturas**: una prueba compara unos cuantos píxeles de cada máscara y textura generadas en Python con los mismos píxeles generados por el prototipo con Node, como en VECTOR y BLADE.
  - Para las máscaras de la pintura se comparan **MUL** y **ADD**, y el color reconstruido con dos pinturas.
  - Sirve también para pillar diferencias de redondeo en `scratchMap`.

## 10. Prueba manual

**Etapa 1 · Blender**
1. Ejecutar el script: termina sin errores y deja el `.glb`, el manifiesto, las cuatro máscaras de la pintura, el `.blend` y las cinco capturas.
2. Las capturas al lado del prototipo (misma vista, AMARILLO OBRA con marcas), **la misma nave**:
   - fuselaje casi cuadrado con la cara roma del morro y la banda de chapa lagrimada;
   - canard con las puntas caídas y sus patines;
   - cabina de grúa con cristales, limpiaparabrisas, balizas con jaula y focos;
   - rejilla del parabrisas, rejas laterales, arcos y defensa tubular;
   - compartimento del motor con tapa, rejilla y lamas;
   - motores de titanio con anillos y tuberías de cobre;
   - escape vertical, escalerillas y aletas con franjas;
   - chapa atornillada con **poco desgaste**.
3. `hull_hammer_markings.png`: todas las flechas de prueba hacia arriba y ninguna en espejo.
4. Abrir el `.blend` y dar una vuelta:
   - sin piezas sueltas, agujeros ni caras negras;
   - la cabina y el compartimento **asientan** en el lomo;
   - el canard y las aletas salen del casco **sin huecos**;
   - las tuberías, la escalerilla y las rejas tocan donde se apoyan.
5. Ejecutar el script otra vez: mismo resultado. Los `.glb` de VECTOR, BLADE y las cabinas no cambian.

**Etapa 2 · El casco en carrera**
6. RACE con HAMMER (F10), cámara exterior: se ve el casco 3D con luz, reflejos y sus luces. La pista, la ciudad, los efectos, VECTOR y BLADE se ven **igual que antes**.
7. Acelerar y boost: la llama sale de las bocas, **redonda**, naranja sin boost y con el naranja de siempre con boost; las estelas salen de las puntas caídas.
8. Curvas: el casco se inclina y el derrape se ve como antes.
9. Crestas y peraltes: los patines **no atraviesan** la pista.
10. Rozar la barrera: toca donde se ve la punta caída (unos 2,50 m del centro).
11. C a cabina: la cabina 3D de HAMMER como siempre, sin nada del casco exterior a través del cristal.
12. TIME TRIAL con HAMMER: el fantasma es su wireframe.
13. Rendimiento: los FPS en cámara exterior no bajan de forma notable (apuntar antes y después, también con las tres naves en pantalla si hay naves de prueba).
14. Informe del simulador de equilibrio (4.6).

**Etapa 3 · Pinturas, marcas, luces y daño**
15. F6 recorre las 40 combinaciones:
    - cambian el casco, el canard, las aletas, el compartimento y la cabina (en las bicolor, la cabina de otro color);
    - los desconchones, arañazos y remaches están en el mismo sitio en todas;
    - la panza, los motores, el cobre y las protecciones no cambian.
16. Con marcas:
    - `03` en las aletas, que se lee desde el costado;
    - el logotipo y `NO STEP` encima del canard;
    - `HMR-03` en la cabina y `HOT` detrás de las lamas;
    - franjas en el borde del canard, en las aletas y en el frente del compartimento;
    - marcas oscuras en las pinturas claras y claras en las oscuras;
    - nada en espejo.
17. Sin marcas: ni calcomanías ni franjas, y el borde del canard en chapa oscura.
18. Boost: la baliza ámbar late. Daño 2: la roja fija. Daño 3: la roja parpadea.
19. Dañar hasta nivel 3:
    - quemaduras que crecen;
    - las luces titilan (las balizas no);
    - humo por el escape vertical con nivel 2 y 3.
20. TIME TRIAL: sin quemaduras, parpadeo, humo ni baliza roja. Brillo de levitación bajo la nave; se apaga al saltar una cresta.
21. Pausa con humo y balizas: congelado.

**Etapa 4 · Holograma**
22. SELECT SHIP con HAMMER: el casco 3D girando, con luz y reflejos y las balizas apagadas; arrastrar, inercia y vuelta del giro automático igual que antes.
23. La nave cabe en su sitio en cualquier orientación.
24. VECTOR y BLADE, sus cascos 3D igual, con la misma escala.
25. Menú principal: la versión es 0.14.9.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Blender** | Inventario (sección 2) y sección 3 completa | Comprobación del script · Prueba manual 1–5 · Commit |
| **2 · Casco en carrera** | Secciones 4 y 5 (pintura fija AMARILLO OBRA con marcas) | Pruebas 1–4 · Prueba manual 6–14 · Commit |
| **3 · Pinturas, marcas, luces y daño** | Sección 6 | Pruebas 5–10 · Prueba manual 15–21 · Commit |
| **4 · Holograma** | Sección 7 · versión 0.14.9 | Prueba 11 · Prueba manual 22–25 · Commit |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas y el `.blend`.

## 12. Fuera de alcance

- **Iluminación del circuito**: luces de bordes, túneles y ciudad que tiñan las naves, sombra en la pista, ajustes de exposición o SSR (spec futura).
- **Selector de color** en el juego, guardar la pintura elegida y cambiar el número de unidad desde un menú.
- Variantes 2 · REMOLCADOR, 3 · DEMOLICIÓN y 4 · FUNDICIÓN del prototipo.
- Casco 3D en la nave del fondo del menú, en el fantasma y en las naves de prueba del radar.
- Rivales con IA (el casco queda preparado: varias instancias con pinturas distintas).
- LOD y versión ligera para Android.
- Animar piezas (limpiaparabrisas, balizas giratorias, tapa del compartimento) o sonido.
- Cambios en los efectos de velocidad más allá de colocarlos en las bocas, su escala y el color de la llama sin boost.
- Menú SETTINGS.
