# VECTORY — Spec: Cascos 3D · 2 · Blade (Asalto)

Versión 1.0 · 2026-11-15 · Estado: **propuesta, pendiente de validar**

Segunda spec de los **cascos exteriores realistas**. Cambia el casco wireframe de **BLADE** por un casco 3D en estilo **militar**, variante **ASALTO**:

- **Silueta del wireframe HOJA**, con retoques mínimos:
  - misma cuña en flecha, con las 4 secciones del fuselaje;
  - mismas puntas traseras en flecha;
  - toberas en el mismo sitio.
- **Sección apenas curvada** (exponente p = 1,2): el lomo y la panza se curvan un poco, pero el **filo lateral sigue vivo**.
- **Cúpula burbuja** de una pieza, a juego con la cabina 3D, con un arco fino delante del piloto, tres barras de blindaje y un carenado detrás. Sustituye a la cubierta del wireframe.
- **Jorobas de motor** que salen del casco hacia las toberas, con una toma de aire enrasada y lamas.
- **Toberas planas** con flaps, paredes y fondo luminoso.
- **Placas de blindaje** atornilladas y **bola de sensores** bajo el morro.
- **Sonda del morro**: sale 0,55 m por delante de la punta. Es **más corta que la de la cabina 3D**, para que el holograma no pase de la esfera de 10 m (sección 7). No se ven nunca juntas: en la vista de cabina el casco exterior está oculto.
- **Materiales**:
  - camuflaje de tres tonos con desconchones;
  - blindaje del mismo color, más oscuro;
  - panza lisa;
  - antideslizante delante de la cúpula;
  - metal pavonado;
  - titanio con tintes de calor en las toberas.
- **Luces**:
  - posición en el morro y en las puntas (roja a la izquierda, verde a la derecha);
  - tiras de formación verdes;
  - luz blanca de cola;
  - **anticolisión** roja que destella (nueva).
- **Catálogo de 20 pinturas** (10 camuflajes y 10 lisas), cada una **con marcas** (número, distintivo y franjas de aviso en las puntas) y **sin marcas**: **40 combinaciones**, listas para el futuro selector de color.
- **Daño visible**: quemaduras, luces que titilan y humo, como en VECTOR.

Repite la cadena de VECTOR (spec `cascos-3d-1-vector`): script de Blender → `.glb` → Godot.
- El casco se ve en la **cámara exterior** y en el **holograma del selector**, y queda **preparado para los rivales con IA**.
- El **fantasma** sigue siendo wireframe.

Plan de las specs de cascos 3D:

1. Vector · Nocturna.
2. **BLADE · Asalto** (esta).
3. HAMMER (estilo industrial, por diseñar).

Diseño de referencia:
- **`design/casco-3d-blade.html`**: el prototipo.
  - Se abre en el navegador y se gira con el ratón (o con el dedo en el móvil).
  - Tiene vistas, boost, daño, velocidad, la silueta wireframe para comparar y el catálogo de pinturas.
  - **Solo vale la variante 3 · ASALTO** (la que sale al abrirlo, `VAR = 3`).
- **Va a Blender** todo lo que construye `buildHull()` con `VAR = 3`, **con marcas** (`LIV = true`), incluido el grupo `Markings` (la geometría; los dibujos los pone Godot).
  - También las texturas, como **máscaras** independientes de la pintura (sección 3.5): `camoSet` ×2, `paintSet` neutra, `brushedSet` ×2, `hazardSet`, `antiSlipSet` y `scorchTex`.
- **Va al código del juego**:
  - el **catálogo de pinturas** (`PAINTS`) y cómo se aplica cada una (`pcols`, `botOf`, `markCol`, el ×0,85 del blindaje y el acabado SATINADO);
  - el **dibujo de las calcomanías** (`numberTex`, `insigniaTex`);
  - el **daño** (quemaduras, parpadeo de luces y humo);
  - el **destello de la anticolisión**;
  - el **brillo de levitación**.
- **No va a ningún sitio**:
  - las variantes 1 · FURTIVO y 2 · NAVAL (se quedan en el archivo como diseño guardado, con sus texturas `ramSet` y `navalSet`, `stencilTex` y las piezas que solo usan ellas: juntas en sierra, tira del filo, gancho);
  - el mundo, la pista y la ciudad;
  - las cámaras del prototipo y el panel;
  - la silueta wireframe de comparación;
  - las llamas, estelas y la luz de boost del prototipo (el juego ya tiene las suyas, spec `efectos-velocidad`).
- Los nombres que se pasan a `mesh()` y `group()` dicen cómo se llama cada pieza en Blender.

Si algo no cuadra entre esta spec y el prototipo, **manda el prototipo** y se avisa.

Rama: `feature/hull-3d-blade`, que sale de `develop` con `feature/hull-3d-vector` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.7 → 0.14.8** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. **No hay cambios en el documento maestro.**

---

## 1. Resumen

- **Etapa 1 · Blender**: un script nuevo, `hull_blade.py`, construye el casco reutilizando y ampliando `art/blender/lib/`. Lo exporta a `.glb` y guarda:
  - el `.blend`;
  - las máscaras de camuflaje y las de quemaduras;
  - un manifiesto;
  - las capturas de previsualización.

  En el juego no cambia nada.
- **Etapa 2 · El casco en carrera**: BLADE lleva el casco 3D en la cámara exterior, con la pintura por defecto (BOSQUE con marcas), su luz y sus reflejos.
  - Las **toberas** y las **estelas** salen de los puntos del modelo.
  - El **ancho de colisión** pasa a **1,61 m**.
  - En la vista de cabina, el casco exterior **no tapa nada**.
- **Etapa 3 · Pinturas, marcas, luces y daño**:
  - entran las **40 combinaciones**, con la tecla de depuración de VECTOR;
  - entran las **calcomanías** dibujadas por Godot y la **anticolisión**;
  - entran el **daño visible** y el **brillo de levitación**.
- **Etapa 4 · Holograma del selector**: SELECT SHIP enseña el casco 3D de BLADE con su luz, con el mismo giro y la misma escala de siempre.
- VECTOR **no cambia** (su casco 3D sigue igual). HAMMER **no cambia**: sigue con su wireframe en todas partes.
- Lo que **no cambia**:
  - el fantasma (wireframe de su nave);
  - las naves de prueba del radar;
  - la nave del fondo del menú principal;
  - las cabinas 3D y el HUD;
  - la conducción, salvo el ancho de colisión de BLADE.

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **`art/blender/lib/`** tras VECTOR:
   - qué piezas del casco de VECTOR se pueden reutilizar tal cual (`herm`, `octRing`/`octLoft`/`octAnnulus`, `secAt`, `rrect`, `lathe`, tubos, `boxUV`, exportación, capturas y manifiesto);
   - qué hay que añadir (sección 3.4) **sin cambiar** lo que usan VECTOR y las tres cabinas.
   - **Ojo con los nombres**: la cabina de BLADE ya tiene un `hazardSet(65)` y un `antiSlipSet(63)` con **otro algoritmo**. Los del casco son distintos y van con otro nombre (por ejemplo, `hull_hazard_set` y `hull_antislip_set`).
2. **`ShipHull3D`**, su lector del manifiesto y el **catálogo de pinturas** de VECTOR:
   - qué tienen escrito a fuego para VECTOR (nombres de piezas, grupos de pintura, librea, luces, acabados);
   - qué hay que generalizar para cualquier nave (sección 5).
3. **Perfil visual de BLADE**: casco wireframe, toberas, estela, factor de cámara (1,22), ancho de colisión (1,6 m) y cabina 3D.
4. **Toberas de BLADE** (spec `naves-3-cascos`): hoy la llama se escala × 0,5 por el radio (0,20 / 0,40). Con la boca achatada (sección 4.2) cambia a ancho × alto × largo.
5. **Altura de vuelo** de BLADE sobre la pista. Lo más bajo del casco nuevo es la bola de sensores, a **y = −0,234**, y la panza a −0,14 (antes, −0,14 era lo más bajo). Hay que confirmar que la bola **no toca la pista** ni en las crestas ni en los peraltes (spec `pista-3d`).
6. **Luz y reflejos del casco** que dejó VECTOR (capa visual, luces colgadas de la nave, panorama de reflejos): se reutilizan tal cual.
7. **Fuentes**: `Black Ops One` no está en el proyecto y hace falta para el número (licencia OFL).
8. **Holograma del selector**: la escala común y la prueba de la **esfera de 10 m** que dejó VECTOR (sección 7).
9. **Simulador de equilibrio** de la spec `naves-6-equilibrio`: si se puede ejecutar tal cual para medir BLADE con el ancho de colisión nuevo.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · El casco en Blender

### 3.1 Cómo se usa Blender

Igual que con VECTOR: Blender sin ventana, desde la terminal, con la ruta de `art/blender/run.ps1`:

```
blender -b --factory-startup --python art/blender/hull_blade.py -- --out <carpeta>
```

Todo vive en el script, el `.blend` se regenera y el script es **determinista**.

### 3.2 Archivos

| Archivo | Qué es | ¿Va a Git? |
|---|---|---|
| `art/blender/hull_blade.py` | Construye el casco, exporta y guarda todo | Sí |
| `art/blender/lib/` | Piezas comunes + lo nuevo de la sección 3.4 | Sí |
| `assets/…/hull_blade.glb` | Modelo exportado, con las texturas dentro | Sí |
| `assets/…/hull_blade.manifest.json` | Nombres, medidas, puntos y recuentos (sección 3.8) | Sí |
| `assets/…/hull_blade_camo_mask.png`, `hull_blade_armor_mask.png` | Máscaras del camuflaje (sección 3.5) | Sí |
| `assets/…/hull_scorch_1.png`, `_2.png`, `_3.png` | Máscaras de quemaduras: **las de VECTOR** (misma semilla); no se duplican | Ya están |
| `art/blender/out/hull_blade.blend` | Escena de Blender | No (`.gitignore`) |
| `art/blender/out/previews/hull_blade_*.png` | Capturas de previsualización | No (`.gitignore`) |

La carpeta de `assets/` es la misma que usó VECTOR.

### 3.3 Ejes, unidades y origen

- Igual que VECTOR: metros, **X derecha, Y arriba, morro hacia −Z**, todo dentro de un objeto raíz **`HullBlade`** girado **+90° en X** en Blender, con los números del prototipo copiados **tal cual**.
- El **origen** es el del casco wireframe de BLADE de hoy, así que el casco nuevo cae en el mismo sitio que el viejo.
- La altura `HOVER` (0,65 m) es **solo del prototipo** y **no** va al modelo.
- Comprobación antes de exportar (en coordenadas de Godot, con marcas; tolerancia 5 mm):

| Punto | Valor |
|---|---|
| Punta del morro | z = −5,20 (y = 0,02) |
| Punta de la sonda (`Nose_Pitot`) | z = −5,75 |
| Final de las puntas traseras | z = +3,608 (con bisel) |
| Caja total en z, sin sonda | −5,20 → 3,608 (largo **8,81 m**) |
| Semiancho (puntas traseras, con bisel) | **1,612 m** |
| Caja total en y | −0,234 (bola de sensores) → **0,519**; alto **0,753 m** |
| Cima de la cúpula | y = 0,499 ± 0,01, cerca de z −0,55 |
| Boca de la tobera (`Exhaust_*`) | (±0,60, 0,08, 3,30) |
| Puntos de estela (`Trail_*`) | (±1,60, 0, 3,60) |

### 3.4 Geometría

Se traduce `buildHull()` con `VAR = 3` y `LIV = true` **pieza a pieza**, con los mismos números y en el mismo orden. Piezas nuevas para `art/blender/lib/`:

| Prototipo | En Blender |
|---|---|
| `prof(z)` | Perfil del fuselaje de BLADE: `PZ` = [−3; −1; 1,2; 2,8] y `PV` del wireframe HOJA. Entre secciones, **Hermite monótona** (`herm`, la de VECTOR). De la punta (0, **0,02**, −5,2) a la 1.ª sección, cada valor = punta + (sección − punta) × `1 − (1 − t)^1,6` |
| `fusPt(z, θ, off)` | Sección **de lente** (no es la superelipse de VECTOR): x = hw·cos θ; arriba y = arista + (techo − arista)·(1 − \|cos θ\|^p); abajo y = arista − (arista − panza)·(1 − \|cos θ\|^p), con **p = 1,2**. `off` desplaza por la normal de la sección (fórmula del prototipo) |
| `fusTopY`, `fusBotY`, `skinNormal` | Altura de la piel en (x, z) y su normal por diferencias centradas de 1 cm |
| `fusPatch` | Como el de VECTOR, con `NZ = max(4, redondeo(80·(u1 − u0)))` y `NT = max(3, redondeo(72·(θ1 − θ0)/2π))` |
| `fusCap` | Tapa trasera en abanico, 72 lados |
| `skinGrid(z0, z1, xr, off, nz, nx, uvFn)` | Rejilla pegada a la piel entre z0 y z1, con x entre `xr(z)`, desplazada `off` por la normal (antirreflejos y calcomanías) |
| `skinRibbon(pts, w, off)` | Cinta de ancho `w` pegada a la piel siguiendo una polilínea en planta (juntas y tiras de luz) |
| `canopyGeo`, `canopyY` (`CAN`) | Cúpula burbuja: planta elíptica con A 0,34, **Bf 1,15 hacia el morro y Br 1,7 hacia atrás** desde zc −0,55; altura = piel + 0,002 + 0,22·√(1 − r²); rejilla 20 × 84 |
| `fairingGeo` (`FAIR`) | Carenado: z 0,85 → 2,15, semiancho 0,30 → 0,10, alto 0,004 + 0,10·(1 − s)^1,2, perfil `(1 − (x/w)²)^0,7`; rejilla 26 × 20 |
| `prongGeo` | Punta trasera: polígono en planta extruido **0,04 m** con bisel de 0,008 × 0,006 y 2 segmentos, de y 0,012 a 0,068. Dos piezas: base y punta (`PRONG_BASE`, `PRONG_TIP`) |
| `plateAt` | Placa de blindaje: caja w × 0,022 × l apoyada en la piel con la base (derecha, normal, adelante) del prototipo, y 4 tornillos (cilindros de r 0,016 × 0,012, 10 lados) |
| `NAC`, `NOZ` | Joroba y tobera: secciones octogonales biseladas, con las funciones de VECTOR (`octLoft`, `secAt`, `octAnnulus`) |

Partes del casco (detalle en el prototipo):

- **`Fuselage`**:
  - piel de arriba (`Fuselage_Upper`, θ 0 → π, con pintura);
  - piel de abajo (`Fuselage_Lower`, θ π → 2π, panza);
  - tapa trasera (`Fuselage_Rear`);
  - antirreflejos (`AntiGlare`, antideslizante, z −2,65 → −1,62);
  - juntas de panel (`Seam_0` … `Seam_5`);
  - sonda (`Nose_Pitot`, torno de 16 lados hasta z −5,75);
  - luces de posición del morro (`Light_Nav_L` roja y `Light_Nav_R` verde, en la arista a z −3,3);
  - tiras de formación del morro (`Accent_FormNose_L/R`);
  - luz de cola (`Light_Tail`);
  - anticolisión (`Light_Strobe_Top` sobre el carenado en z 1,5, `Light_Strobe_Bottom` bajo la panza en z 0,3);
  - bola de sensores (`Sensor_Mount`, `Sensor_Ball`, en z −3,1).
- **`Canopy`**:
  - cristal (`Canopy_Glass`);
  - junta (`Canopy_Seal`, tubo de 0,014 m y 8 lados sobre el borde, 5 mm por encima de la piel);
  - arco (`Canopy_Bow`, en z −1,15);
  - 3 barras (`Canopy_Bar_0/1/2`, en x −0,13, 0 y 0,13);
  - carenado (`Fairing`);
  - suelo de la cabina (`Canopy_Floor`): la planta de la cúpula pegada a la piel, 1,5 mm por encima (y por encima del arranque del carenado, sin pasar de 1 mm por debajo del cristal), del **color base de la pintura, liso**: el camuflaje no se ve a través del cristal. Decisión del usuario, añadida también al prototipo.
- **`Prong_L` / `Prong_R`**:
  - base (`Prong_*_Base`, pintura);
  - punta (`Prong_*_Tip`: franjas de aviso con marcas, pintura sin ellas);
  - tira de formación (`Accent_FormProng_*`);
  - luz de posición (`Light_Tip_*`);
  - punto de la estela (`Trail_*`, vacío).
- **`Pod_L` / `Pod_R`** (en x = ±0,60, y = 0,08):
  - joroba (`Pod_*_Hump`, octLoft de 0,55 a 2,80 en 40 tramos; delante va enterrada en el casco);
  - toma de aire (`Pod_*_Intake`) con 6 lamas (`Pod_*_IntakeVane`);
  - 2 juntas (`Pod_*_Seam_0/1`, en z 1,9 y 2,55);
  - tobera (`Nozzle_*`), labio (`Nozzle_*_Lip`), interior (`Nozzle_*_Inner`);
  - flaps (`Nozzle_*_FlapTop` / `_FlapBottom`, con 5 ranuras `Nozzle_FlapGroove`);
  - paredes (`Nozzle_*_WallR` / `_WallL`);
  - fondo luminoso (`Nozzle_Glow_*`, 0,42 × 0,14 a z 3,0) y núcleo (`Nozzle_Core_*`, 0,26 × 0,05 a z 3,05);
  - la boca (`Exhaust_*`, vacío).
- **`Armor`**: 4 placas por lado (`Armor_<i>_<L/R>`, con `_Plate` y 4 `_Bolt`), en (x, z, ancho, largo):
  - (0,20, −2,95, 0,24, 0,55) (detrás del número del morro, para no taparlo);
  - (0,32, −2,2, 0,28, 0,66);
  - (0,62, −0,8, 0,34, 0,95);
  - (1,00, 0,6, 0,32, 0,8).
- **`Markings`** (piezas que **solo se ven con marcas**, sección 6.3):
  - número (`Decal_Number_L/R`, z −4,05 → −3,15, x de 0,04 a 0,84·hw, 12 × 6, a 4 mm);
  - distintivo (`Decal_Insignia_L/R`, z 1,6 → 2,1, x 0,95 → 1,40, 8 × 8, a 4 mm).
- **`Smoke`** (vacío nuevo, no está en el prototipo como pieza): en (0,60, 0,35, 2,40), de donde sale el humo con daño.

Reglas de las UV (en **metros × repetición**, sin `KHR_texture_transform`):
- **Piel**: `(z, θ × radio medio)`, como VECTOR. **Rejillas** sin `uvFn` y carenado: `(z, x)`. **Octógono**: `(z, perímetro)`. **Puntas traseras**: las de la forma, `(x, z)`.
- **Calcomanías**, **UV 0–1** con la orientación del prototipo:
  - número, lado derecho: (1 − iu, 1 − jv);
  - número, lado izquierdo: (iu, jv);
  - distintivo, los dos lados: (jv, 1 − iu).
- **Primitivas**: proyección de caja en metros (`boxUV`).
- **Segunda capa de UV (UV2)** en todas las piezas con pintura, blindaje, panza, antideslizante, franjas, metal pavonado o titanio: las mismas UV en metros **sin la repetición** y × **1,1** (quemaduras, sección 6.5).
- **Caras por los dos lados**, sombreado suave (salvo octógono, cajas y planos) y piezas `noShadow` sin sombra, igual que VECTOR.

### 3.5 Materiales y texturas

**Principled BSDF** con texturas de imagen, sin nodos procedurales. Texturas a **512 × 512** generadas **en Python** con el **mismo algoritmo, semillas y orden de llamadas** del prototipo.

**El camuflaje es una máscara, no un color.** La pintura cambia en el juego (20 pinturas), así que Blender **no** guarda colores del camuflaje. Guarda lo que **no depende de la pintura**:

| Archivo | Llamada del prototipo | Canales |
|---|---|---|
| `hull_blade_camo_mask.png` + su rugosidad y su normal | `camoSet(55, …)` | **R** = tono: 0 = base (`c0`), 0,5 = claro (`c1`), 1 = oscuro (`c2`) · **G** = desconchón (1 si `chips > 0,79`) · **B** = brillo del grano `k` = 0,93 + 0,1 × grano, guardado como (k − 0,9) / 0,2 |
| `hull_blade_armor_mask.png` + su rugosidad y su normal | `camoSet(57, …)` | Igual |

- Regla de `camoSet`: tono **oscuro** si `n1 < 0,44`; si no, **claro** si `n2 > 0,55`; si no, **base**. Desconchón: color metálico (118, 120, 116), rugosidad 0,38 y sin relieve. Rugosidad: 0,68 + 0,08 × grano; relieve: 0,35 + 0,1 × grano; normal × 1,4.
- La rugosidad y la normal **no** dependen de los colores, así que salen igual para cualquier pintura.
- Godot reconstruye el color (sección 6.2): `color = (desconchón ? (118, 120, 116) : tono[R]) × k`. Así sale **el mismo píxel** que el prototipo.

Resto de texturas (color, rugosidad y normal, como VECTOR):

| Conjunto | Llamada del prototipo | Qué es |
|---|---|---|
| Panza neutra | `paintSet(58, [255, 255, 255])` | La pintura lisa de VECTOR; el color lo pone Godot (`botOf`) |
| Metal pavonado | `brushedSet(31, [92, 96, 100], false)` | El cepillado de VECTOR con otro color |
| Titanio | `brushedSet(32, [150, 148, 146], true)` | El de VECTOR (se reutiliza) |
| Franjas de aviso | `hazardSet(65)` del casco | Diagonales amarillas (214, 170, 30) y negras de 64 px, gastadas |
| Antideslizante | `antiSlipSet(63)` del casco | Grano grueso oscuro |
| Quemaduras | `scorchTex(41)` | Las de VECTOR (ya están) |

| Material | Base | Metal | Rugosidad | Repetición | Extra |
|---|---|---|---|---|---|
| `M_Paint` | máscara del camuflaje (Godot pone el color) | 0,1 | textura | ×0,35 | Normal ×0,6. **Godot: shader del camuflaje** (6.2) |
| `M_PaintArmor` | máscara del blindaje (Godot pone el color) | 0,15 | textura | ×0,6 | Normal ×0,6 |
| `M_PaintBottom` | panza neutra × (Godot pone el color) | 0,1 | textura × 2,6 | ×1 | Normal ×0,4 |
| `M_AntiSlip` | antideslizante | 0 | textura | ×6 | — |
| `M_Hazard` | franjas de aviso | 0,1 | textura | ×3 | — |
| `M_Gunmetal` | pavonado | 1 | textura | ×2 | Arco, barras, junta, soporte de los sensores, tornillos, lamas |
| `M_Titanium` | titanio | 1 | textura | ×2 | Toberas, flaps, paredes |
| `M_Dark` | `#0B0B0D` | 0,2 | 0,7 | — | Tapa trasera, juntas, ranuras |
| `M_Intake` | `#050506` | 0,1 | 0,9 | — | Toma de aire, interior de tobera |
| `M_CanopyArmor` | `#0A0D0A` al 72 % | 0,1 | 0,06 | — | Transparente, barniz 1, reflejos ×1,4 |
| `M_SensorGlass` | `#05070A` | 0,3 | 0,05 | — | Barniz 1, **iridiscencia 0,6** (IOR 1,5, grosor 300–600 nm), con el shader de VECTOR |
| `M_Decal_*` | negro (la textura la pone Godot) | 0 | 0,6 | — | Transparente con alfa |
| `M_CanopyFloor` | color base de la pintura (lo pone Godot) | 0,1 | 0,7 | — | Suelo de la cabina, sin textura |
| `M_Emissive_*` | negro + emisión | 0 | 0,4 | — | **Una instancia por pieza viva** (sección 3.8) |
| `M_Emissive_NozzleGlow` / `_NozzleCore` | `#245BFF` · 2,2 / `#7FA6FF` · 3,5 | 0 | 0,4 | — | Los de VECTOR |

### 3.6 Exportación

Igual que VECTOR:
- `.glb`, +Y arriba, modificadores aplicados;
- normales, **las dos capas de UV** y texturas **dentro**;
- sin cámaras, luces ni animaciones;
- solo lo que cuelga de `HullBlade`, **con** `Markings` y los vacíos (`Exhaust_*`, `Trail_*`, `Smoke`).

### 3.7 Capturas de previsualización

En `art/blender/out/previews/`, 1280 × 720, fondo oscuro y la luz de VECTOR. Las cámaras son las del prototipo **con 0,65 m menos de altura**:

| Captura | Cámara | Mira a | FOV vertical |
|---|---|---|---|
| `hull_blade_tq.png` | (−8,6, 3,1, −8,4) | (0, 0,15, −0,3) | 38° |
| `hull_blade_top.png` | (0, 18,85, −0,8) | (0, 0, −0,8) | 34° |
| `hull_blade_side.png` | (12,5, 0,4, −0,8) | (0, 0,2, −0,8) | 34° |
| `hull_blade_rear.png` | (4,5, 1,8, 14,5) | (0, 0,2, 0,8) | 34° |
| `hull_blade_markings.png` | la de `top` | — | 34° |

- Todas con BOSQUE con marcas, coloreada con la regla de la sección 6.2 (en Blender, con un material de prueba).
- En `hull_blade_markings.png` las calcomanías llevan una **textura de prueba** (cuadrícula con una flecha hacia arriba), para comprobar que **ninguna sale en espejo ni girada**.

El agente **mira las capturas** antes de cerrar la etapa y las compara con el prototipo abierto en el navegador, en la misma vista, con BOSQUE con marcas.

### 3.8 Nombres de las piezas (contrato con Godot)

Godot busca estas piezas **por nombre**. El script comprueba antes de exportar que **están todas** y, si falta una, **falla con un mensaje claro**.

| Nombre | Qué es | Cuántas |
|---|---|---|
| `HullBlade` | Raíz (origen = el del casco wireframe) | 1 |
| `Exhaust_L`, `Exhaust_R` | Vacíos en la boca de cada tobera: (±0,60, 0,08, 3,30) | 2 |
| `Trail_L`, `Trail_R` | Vacíos de las estelas: (±1,60, 0, 3,60) | 2 |
| `Smoke` | Vacío del humo: (0,60, 0,35, 2,40) | 1 |
| `Fuselage_Upper`, `Prong_L/R_Base`, `Pod_L/R_Hump`, `Fairing` | Piezas con la pintura (`M_Paint`) | 6 |
| `Prong_L/R_Tip` | Puntas: franjas de aviso con marcas, pintura sin ellas | 2 |
| `Armor_<0–3>_<L/R>_Plate` | Placas con la pintura del blindaje (`M_PaintArmor`) | 8 |
| `Fuselage_Lower` | Panza (`M_PaintBottom`) | 1 |
| `Accent_FormNose_L/R`, `Accent_FormProng_L/R` | Tiras de formación | 4 |
| `Light_Nav_L/R`, `Light_Tip_L/R` | Luces de posición (roja izquierda, verde derecha) | 4 |
| `Light_Tail` | Luz blanca de cola | 1 |
| `Light_Strobe_Top`, `Light_Strobe_Bottom` | Anticolisión | 2 |
| `Nozzle_Glow_L/R`, `Nozzle_Core_L/R` | Fondo luminoso de las toberas | 4 |
| `Canopy_Glass` | Cúpula | 1 |
| `Canopy_Floor` | Suelo de la cabina (`M_CanopyFloor`, color base de la pintura) | 1 |
| `Sensor_Ball` | Bola de sensores (iridiscente) | 1 |
| `Decal_Number_L/R` | Número, UV 0–1 | 2 |
| `Decal_Insignia_L/R` | Distintivo, UV 0–1 | 2 |

El resto se agrupa por zonas (`Fuselage`, `Canopy`, `Prong_L/R`, `Pod_L/R`, `Armor`, `Markings`), con los nombres del prototipo dentro. Las piezas repetidas (tornillos, lamas, ranuras, juntas) pueden llevar sufijo numérico.

**Manifiesto** (`hull_blade.manifest.json`), con el formato del de VECTOR:
- Versión de Blender y fecha.
- Piezas vivas de la tabla con su color de emisión e intensidad de partida (sección 6.4), y los vacíos con su posición.
- Qué piezas llevan pintura, blindaje, panza, puntas y calcomanías.
- Piezas sin sombra, materiales con barniz, iridiscencia, reflejos distintos de 1 y transparencia.
- **Caja total** con y sin la sonda (coordenadas de Godot) y **semiancho** (mayor |x|).
- Triángulos totales sin marcas y con marcas. Como referencia, el prototipo da **≈ 30 300** y **≈ 30 900**.

## 4. Etapa 2 · El casco en carrera

### 4.1 Importación

Igual que VECTOR: nombres tal cual, sin colisiones, sin LOD ni luz horneada. Al cargar, `ShipHull3D` aplica lo del manifiesto. Si algo no llega bien, se arregla **en Godot con su material**, sin tocar la geometría, y se avisa.

### 4.2 Perfil visual de BLADE

- El perfil visual de BLADE gana su **casco 3D** (`.glb` y manifiesto) y su **catálogo de pinturas** (sección 6.1).
  - VECTOR → 3D Nocturna;
  - BLADE → 3D Asalto;
  - HAMMER → wireframe.
- **El casco wireframe de BLADE se queda** en el código y en su prueba de captura, porque lo sigue usando el **fantasma** (y el **holograma** hasta la etapa 4).
- **Toberas**: la llama, el núcleo y el halo se colocan en `Exhaust_L/R`, con la boca achatada **× 0,80 de ancho, × 0,28 de alto y × 0,85 de largo** respecto a la llama de referencia de VECTOR (como `ex.scale` del prototipo). Sustituye al × 0,5 por radio de la spec `naves-3-cascos`. Si no se puede sin tocar la lógica de los efectos, **se avisa antes**.
- **Estelas**: salen de `Trail_L/R` (el mismo punto que hoy).
- **Cámara exterior**: el factor de BLADE **sigue en 1,22** (el largo pasa de 8,8 a 8,81 m). Se ajusta probando si hace falta.
- Los puntos se leen del manifiesto o de los vacíos, **no** se copian a mano. Una prueba comprueba que el perfil y el manifiesto coinciden.

### 4.3 Luz y reflejos

**Los mismos del casco de VECTOR**:
- capa visual propia;
- las mismas luces colgadas de la nave;
- el mismo panorama de reflejos y glow.

No se crea nada nuevo. Si con el camuflaje mate hace falta otra intensidad, se ajusta **por nave** en el Inspector y se avisa.

### 4.4 Vista de cabina

Igual que VECTOR: en la vista de cabina, el casco exterior de BLADE **no se dibuja**. La sonda, que sale por delante, tampoco: la cabina 3D ya tiene la suya.

### 4.5 Ancho de colisión

- El ancho de colisión de BLADE pasa de **1,6 m** a su semiancho nuevo: **1,61 m** (el del manifiesto redondeado al centímetro; el bisel de las puntas suma unos milímetros).
- La prueba de `naves-3-cascos` (**ancho de colisión = semiancho real del casco**) mira el **manifiesto** cuando la nave tiene casco 3D: VECTOR y BLADE. HAMMER, igual que antes.

### 4.6 Equilibrio

Con 1 cm más por lado no debería notarse.
- Al cerrar la etapa, el agente ejecuta el **simulador de equilibrio** de la spec `naves-6-equilibrio` con BLADE y apunta los tiempos por vuelta antes y después.
- **No se toca ningún número** de las naves: si BLADE se sale del margen, **se avisa** con los datos.

### 4.7 Qué se ve en cada caso

| | VECTOR | BLADE | HAMMER |
|---|---|---|---|
| Cámara exterior (jugador) | Casco 3D | **Casco 3D** | Wireframe |
| Vista de cabina | Cabina 3D (casco oculto) | Cabina 3D (**casco oculto**) | Cabina 3D |
| Fantasma | Wireframe | Wireframe | Wireframe |
| Naves de prueba (F7) | Wireframe magenta | — | — |
| Menú principal | Wireframe (sin cambios) | — | — |
| Holograma del selector | Casco 3D | Wireframe hasta la etapa 4; **casco 3D** después | Wireframe |

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual. **Generalizar** lo de VECTOR sin cambiar cómo se ve VECTOR:

- **Perfil visual de nave**: casco 3D opcional (`.glb`, manifiesto, achatado de la boca en **ancho, alto y largo**) y su **catálogo de pinturas**.
- **`ShipHull3D`**:
  - deja de saber de VECTOR;
  - busca las piezas vivas y los **grupos de pintura** según el manifiesto de cada nave;
  - aplica la pintura con el **aplicador** de su catálogo;
  - si falta una pieza, da un error claro;
  - **sin reglas dentro**.
  - Mismas funciones que en VECTOR (`SetPaint`, `SetDamage`, `SetHoverVisible`, los puntos), más **`SetStrobes(on)`** si la nave tiene anticolisión.
  - Cada instancia lleva **sus propios materiales**.
- **Catálogo de pinturas** (puro, datos): uno **por nave**. El de VECTOR (colores y acabados) no cambia. El de BLADE es de **tres tonos** (sección 6.1).
- **Aplicadores de pintura** (puros): de (pintura, con marcas) a lo que hay que poner en cada grupo de piezas. VECTOR tiene el suyo (librea); BLADE, el de la sección 6.2.
- **Dibujantes de calcomanías** (puros): número de BLADE e insignia (sección 6.3). Los de VECTOR no cambian.
- **Daño visible** (puro): el de VECTOR, con la lista de luces que titilan **leída del manifiesto** de cada nave.
- **Anticolisión** (pura): de tiempo a intensidad de cada luz (sección 6.4).
- Los colores fijos (luces de posición, formación, cola, anticolisión, metal de los desconchones) van como **constantes con nombre**.

## 6. Etapa 3 · Pinturas, marcas, luces y daño

### 6.1 Catálogo de pinturas

Es `PAINTS` del prototipo. **20 pinturas**, cada una **sin marcas** y **con marcas**: **40 combinaciones**, en este orden: las 20 sin marcas y luego las 20 con marcas.

Camuflajes de tres tonos (base `c0`, claro `c1`, oscuro `c2`, en RGB 0–255):

| # | `id` | Nombre | Base | Claro | Oscuro |
|---|---|---|---|---|---|
| 1 | `woodland` | BOSQUE | 86, 96, 64 | 118, 122, 96 | 44, 51, 38 |
| 2 | `desert` | DESIERTO | 196, 170, 122 | 160, 128, 88 | 112, 92, 64 |
| 3 | `arctic` | ÁRTICO | 222, 224, 222 | 176, 182, 186 | 96, 102, 108 |
| 4 | `urban` | URBANO | 128, 130, 132 | 92, 94, 98 | 52, 54, 58 |
| 5 | `night` | NOCHE | 40, 44, 52 | 62, 66, 76 | 24, 26, 30 |
| 6 | `jungle` | SELVA | 58, 82, 46 | 92, 104, 60 | 32, 48, 30 |
| 7 | `tundra` | TUNDRA | 150, 148, 128 | 184, 182, 166 | 74, 78, 68 |
| 8 | `naval_split` | MARINO | 92, 110, 130 | 150, 160, 170 | 56, 70, 88 |
| 9 | `autumn` | OTOÑO | 120, 92, 56 | 146, 132, 90 | 56, 46, 34 |
| 10 | `ash` | CENIZA | 100, 96, 90 | 140, 136, 128 | 62, 60, 58 |

Lisas (los tres tonos iguales; los desconchones siguen saliendo):

| # | `id` | Nombre | Color | Acabado |
|---|---|---|---|---|
| 11 | `olive` | OLIVA | 86, 94, 62 | MATE |
| 12 | `sand` | ARENA | 194, 170, 124 | MATE |
| 13 | `field_grey` | GRIS CAMPAÑA | 104, 110, 104 | MATE |
| 14 | `ops_black` | NEGRO OPERACIONES | 30, 32, 34 | **SATINADO** |
| 15 | `nato_green` | VERDE OTAN | 64, 74, 56 | MATE |
| 16 | `earth` | MARRÓN TIERRA | 110, 86, 60 | MATE |
| 17 | `navy_grey` | GRIS NAVAL | 118, 126, 134 | **SATINADO** |
| 18 | `winter_white` | BLANCO INVIERNO | 214, 216, 212 | MATE |
| 19 | `slate_blue` | AZUL PIZARRA | 58, 70, 84 | MATE |
| 20 | `khaki` | CAQUI | 150, 138, 100 | MATE |

Los camuflajes son todos MATE.

- **Pintura por defecto**: BOSQUE **con marcas** (la 21 en el orden de la sección 6.1, la que sale al abrir el prototipo).
- Por ahora **no se guarda** ni se elige en ningún menú (selector de color: spec futura). Los rivales podrán llevar cualquiera.
- Los nombres son internos: si un día salen en pantalla, se revisan.

### 6.2 Cómo se aplica una pintura

**Acabados**:

| Acabado | Metal | Rugosidad | Barniz · rugosidad |
|---|---|---|---|
| MATE | 0,10 (blindaje 0,15) | textura × 1 | — |
| SATINADO | 0,20 (blindaje 0,25) | textura × 0,75 | 0,25 · 0,35 |

**Piel, puntas, jorobas y carenado** (`M_Paint`):
- **Shader del camuflaje**: `color = (G ? (118, 120, 116) : tono[R]) × k`, con `tono[0] = c0`, `tono[0,5] = c1`, `tono[1] = c2` (lectura con **filtro más cercano** o redondeando R, para que los bordes sean duros) y `k = 0,9 + 0,2 × B`.
- Se compara con el prototipo **píxel a píxel** en una prueba (sección 9).
- El shader lleva también las **quemaduras** (sección 6.5).

**Blindaje** (`M_PaintArmor`): igual, con su máscara y los tres tonos **× 0,85** (redondeados al entero).

**Panza** (`M_PaintBottom`): color `botOf`:
1. media = (2·c0 + c1 + c2) / 4;
2. panza = media + ((150, 150, 145) − media) × 0,35, redondeado.

**Suelo de la cabina** (`M_CanopyFloor`): el tono base (`c0`) de la pintura, liso y mate, con y sin marcas.

**No cambian** con la pintura: antirreflejos, metal pavonado, titanio, cristal, sensores y luces.

**Depuración**: la tecla de pinturas de VECTOR (**F6** / **Mayúsculas + F6**) recorre las 40 combinaciones de la nave que se lleve. El nombre (y `· MARCAS`) sale en el panel (F9). Solo en compilaciones de depuración.

### 6.3 Marcas

Con marcas, sobre la misma pintura:

- **Se ven** las piezas `Decal_*`. Sin marcas, **no se dibujan**.
- **Puntas** (`Prong_*_Tip`): con marcas, `M_Hazard`; sin marcas, `M_Paint`.
- **Color de las marcas** (`markCol`): si la suma R + G + B de la media de la sección 6.2 es **mayor que 360**, `#141612`; si no, `#DEDECE`.
  - Salen oscuras en DESIERTO, ÁRTICO, TUNDRA, ARENA, GRIS NAVAL, BLANCO INVIERNO y CAQUI.
  - Salen claras en el resto.

Calcomanías, dibujadas **una vez** por Godot al cambiar de pintura, con el mismo dibujo que el prototipo:

| Pieza | Lienzo | Dibujo (como el prototipo) |
|---|---|---|
| `Decal_Number_L/R` (`numberTex`) | 1024 × 320, transparente | `B-02` en **Black Ops One de 230 px**, centrado en (512, 168), en el color de las marcas al **90 %** de opacidad |
| `Decal_Insignia_L/R` (`insigniaTex`) | 512 × 512, transparente | Anillo de radio 228 y 26 px de grueso; dentro, la hoja en punta de flecha (256, 70) (376, 330) (256, 270) (136, 330) y una barra de 300 × 34 px en (106, 360). Todo en el color de las marcas, sin relleno |

- El distintivo es **inventado**: no es de ningún país ni organización.
- El número es el mismo en todas las pinturas. Un selector futuro podrá cambiarlo sin tocar el modelo.

### 6.4 Luces

Valores del prototipo (se ajustan a ojo con el glow del juego y se apuntan):

| Luces | Color | Intensidad |
|---|---|---|
| Posición (`Light_Nav_*`, `Light_Tip_*`) | izquierda `#FF2A1E`, derecha `#3DFF6A` | 2,4 |
| Cola (`Light_Tail`) | `#F4F7FF` | 2,88 |
| Formación (`Accent_Form*`) | `#B6FF6A` | 0,8 |
| Anticolisión (`Light_Strobe_*`) | `#FF2A1E` | 0 apagada · **9** en el destello |
| Fondo / núcleo de las toberas | los de VECTOR | 2,2 / 3,5 (× 1,6 con boost) |

**Anticolisión**:
- destello de **0,06 s** cada **1,2 s**;
- la de abajo va desfasada **0,6 s** (se alternan);
- siempre encendida, también en TIME TRIAL;
- **no** titila con el daño;
- la pausa la congela;
- en el Inspector: periodo, duración e intensidad.

### 6.5 Daño visible

Las mismas reglas que VECTOR (spec `cascos-3d-1-vector`, sección 6.5). Solo en RACE; en TIME TRIAL, nada.

- **Quemaduras**:
  - en pintura, blindaje, panza, puntas (también con franjas de aviso), antirreflejos, metal pavonado y titanio;
  - no en luces, cristal, sensores ni calcomanías;
  - en `M_Paint` y `M_PaintArmor` van **dentro del shader del camuflaje**: oscurece hacia (0,018, 0,014, 0,012) + color × 0,06 y la rugosidad sube hacia 0,85, como el prototipo;
  - en los demás, con el detalle de `StandardMaterial3D` como en VECTOR.
- **Parpadeo**: desde el nivel 1, las **9 luces** `Accent_Form*`, `Light_Nav_*`, `Light_Tip_*` y `Light_Tail` titilan bajando al 20 %, con las reglas de `danos`. La anticolisión y las toberas **no** titilan.
- **Humo** desde `Smoke`, con los números de VECTOR (7 / 20 partículas por segundo en los niveles 2 / 3, mismos colores, vida y tamaños).

### 6.6 Brillo de levitación

El de VECTOR con otro tamaño:
- un plano de **3,0 × 9,0 m**, centrado en **z = −0,7**;
- mismo color, caída, intensidad, latido y apagado en el aire.

El tamaño va en el perfil visual de cada nave.

## 7. Etapa 4 · Holograma del selector

- SELECT SHIP enseña el **casco 3D** de BLADE con la pintura por defecto, en lugar del wireframe. Es el mismo `ShipHull3D`, sin daño, humo, llamas, estelas ni levitación. **Sin anticolisión** (no destella en el menú).
- **Giro, luz fija respecto a la pantalla y reflejos**: los del holograma de VECTOR.
- **Escala común**: la de siempre, fija.
- **Prueba de la esfera de 10 m** (`naves-4-integracion`, ampliada en VECTOR): BLADE da **9,93 m con la sonda** (9,41 m sin ella). La prueba cuenta la nave **entera**, sonda incluida, sin excepciones.
  - Por eso la sonda es más corta que la de la cabina 3D (punta en z −5,75 en lugar de −5,93): con la de la cabina daría 10,10 m.
  - Si al cambiar algo del casco la esfera pasa de 10 m, **se avisa** antes de tocar la escala.
- HAMMER sigue con su holograma wireframe hasta su spec.

## 8. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Intensidad de las luces del casco | Las de VECTOR (ajustables por nave) | Casco 3D |
| Intensidad de posición / cola / formación / toberas | Tabla 6.4 | Casco 3D |
| Anticolisión (periodo, duración, desfase, intensidad) | 1,2 s · 0,06 s · 0,6 s · 9 | Casco 3D |
| Achatado de la llama (ancho × alto × largo) | 0,80 × 0,28 × 0,85 | Perfil visual de BLADE |
| Ancho de colisión de BLADE | 1,61 m | Perfil técnico (del manifiesto) |
| Brillo de levitación (tamaño, centro) | 3,0 × 9,0 m · z −0,7 | Perfil visual de BLADE |
| Pintura por defecto | `woodland`, con marcas | Casco 3D |

## 9. Pruebas

### Pruebas unitarias mínimas

1. **Manifiesto**: tiene todas las piezas y vacíos de la tabla 3.8 con las cantidades exactas. Los vacíos están donde dice la tabla (±1 mm).
2. **Medidas** del manifiesto (±0,01 m):
   - largo sin sonda **8,81 m** y con sonda **9,36 m**;
   - semiancho **1,61 m**;
   - alto **0,75 m**;
   - caja en z −5,20 → 3,61 (sin sonda) y en y −0,234 → 0,519.
3. **Perfil visual**:
   - BLADE → casco 3D, con su achatado de llama y su catálogo;
   - VECTOR → igual que antes;
   - HAMMER → wireframe;
   - el fantasma de BLADE → wireframe;
   - toberas y estelas del perfil = vacíos del manifiesto.
4. **Colisión**: ancho de colisión = semiancho real (VECTOR y BLADE: del manifiesto; HAMMER: de su wireframe), ±0,01 m.
5. **Catálogo de BLADE**:
   - 20 pinturas con `id` únicos;
   - 10 camuflajes y 10 lisas;
   - 2 satinadas (`ops_black`, `navy_grey`);
   - 40 combinaciones en orden, con vuelta adelante y atrás;
   - por defecto `woodland` con marcas.
6. **Aplicación de pintura**:
   - panza (`botOf`): BOSQUE → (107, 112, 93); ARENA → (179, 163, 131);
   - blindaje = tonos × 0,85;
   - con marcas se ven `Decal_*` y las puntas son `M_Hazard`; sin marcas, no se ven y las puntas son `M_Paint`;
   - el suelo de la cabina lleva el tono base de la pintura;
   - color de las marcas: BOSQUE → `#DEDECE`, ÁRTICO → `#141612`, CAQUI → `#141612`, NOCHE → `#DEDECE`.
7. **Shader del camuflaje**: para unos cuantos píxeles de la máscara (tono, desconchón y grano), el color reconstruido con BOSQUE es igual al del prototipo (±1 en 0–255).
8. **Calcomanías**: el número es `B-02` con el color de las marcas al 90 %, centrado; el distintivo, centrado en el lienzo.
9. **Anticolisión**: encendida en t ∈ [0; 0,06) de cada 1,2 s (la de abajo, desfasada 0,6 s); no está en la lista de parpadeo.
10. **Daño visible**: el parpadeo solo elige entre las 9 luces de la sección 6.5; humo 0 / 0 / 7 / 20; en TIME TRIAL, nada.
11. **Holograma**: escala fija; las esferas de las tres naves (BLADE con la sonda: 9,93 m) no pasan de 10 m.

Las pruebas existentes siguen pasando: las de VECTOR y la captura del wireframe de BLADE, que se queda.

### Comprobación del script de Blender

- El script falla si falta una pieza del contrato y comprueba los puntos de la sección 3.3.
- Dos ejecuciones seguidas dan el **mismo** `.glb`.
- Los `.glb` de VECTOR y de las tres cabinas **no cambian** después de tocar `lib/`.
- **Texturas**: una prueba compara unos cuantos píxeles de cada máscara y textura generadas en Python con los mismos píxeles generados por el prototipo con Node, como en VECTOR.
  - Para las máscaras del camuflaje se comparan **tono, desconchón y grano**.

## 10. Prueba manual

**Etapa 1 · Blender**
1. Ejecutar el script: termina sin errores y deja el `.glb`, el manifiesto, las dos máscaras de camuflaje, el `.blend` y las cinco capturas.
2. Las capturas al lado del prototipo (misma vista, BOSQUE con marcas), **la misma nave**:
   - cuña plana en flecha con el filo vivo;
   - cúpula burbuja con arco, barras y junta, y carenado detrás;
   - jorobas con toma de aire y toberas planas con flaps;
   - placas atornilladas;
   - bola de sensores y sonda;
   - puntas con franjas de aviso;
   - camuflaje con desconchones.
3. `hull_blade_markings.png`: todas las flechas de prueba hacia arriba y ninguna en espejo.
4. Abrir el `.blend` y dar una vuelta:
   - sin piezas sueltas, agujeros ni caras negras;
   - la cúpula y el carenado **asientan** en la piel;
   - las jorobas salen del casco **sin costura**;
   - las placas no flotan ni se hunden de más.
5. Ejecutar el script otra vez: mismo resultado. Los `.glb` de VECTOR y de las cabinas no cambian.

**Etapa 2 · El casco en carrera**
6. RACE con BLADE (F10), cámara exterior: se ve el casco 3D con luz, reflejos y sus luces de posición. La pista, la ciudad, los efectos y VECTOR se ven **igual que antes**.
7. Acelerar y boost: la llama sale de las bocas, **plana**; las estelas salen de las puntas traseras.
8. Curvas: el casco se inclina y el derrape se ve como antes.
9. Crestas y peraltes: la bola de sensores **no atraviesa** la pista.
10. Rozar la barrera: toca donde se ve la punta trasera (unos 1,61 m del centro).
11. C a cabina: la cabina 3D de BLADE como siempre, sin nada del casco exterior ni la sonda de fuera a través del cristal.
12. TIME TRIAL con BLADE: el fantasma es su wireframe.
13. Rendimiento: los FPS en cámara exterior no bajan de forma notable (apuntar antes y después).
14. Informe del simulador de equilibrio (4.6).

**Etapa 3 · Pinturas, marcas, luces y daño**
15. F6 recorre las 40 combinaciones:
    - cambian el camuflaje, la panza y el blindaje (más oscuro);
    - los bordes del camuflaje son duros y los desconchones están en el mismo sitio en todas;
    - el antirreflejos, el metal y las toberas no cambian.
16. Con marcas:
    - `B-02` a cada lado del morro, que se lee desde el costado;
    - el distintivo arriba, en los dos lados;
    - las puntas con franjas de aviso;
    - marcas oscuras en las pinturas claras y claras en las oscuras;
    - nada en espejo.
17. Sin marcas: ni número ni distintivo, y las puntas del color de la pintura.
18. NEGRO OPERACIONES y GRIS NAVAL con algo de brillo; el resto, mate.
19. Anticolisión: destellos rojos cortos, alternando arriba y abajo.
20. Dañar hasta nivel 3:
    - quemaduras que crecen;
    - las luces titilan (la anticolisión no);
    - humo desde la joroba derecha con nivel 2 y 3.
21. TIME TRIAL: sin quemaduras, parpadeo ni humo. Brillo de levitación bajo la nave; se apaga al saltar una cresta.
22. Pausa con humo y anticolisión: congelado.

**Etapa 4 · Holograma**
23. SELECT SHIP con BLADE: el casco 3D girando, con luz y reflejos, sin anticolisión; arrastrar, inercia y vuelta del giro automático igual que antes.
24. La nave cabe en su sitio en cualquier orientación, sonda incluida.
25. VECTOR, su casco 3D igual; HAMMER, su wireframe, con la misma escala.
26. Menú principal: la versión es 0.14.8.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Blender** | Inventario (sección 2) y sección 3 completa | Comprobación del script · Prueba manual 1–5 · Commit |
| **2 · Casco en carrera** | Secciones 4 y 5 (pintura fija BOSQUE con marcas) | Pruebas 1–4 · Prueba manual 6–14 · Commit |
| **3 · Pinturas, marcas, luces y daño** | Sección 6 | Pruebas 5–10 · Prueba manual 15–22 · Commit |
| **4 · Holograma** | Sección 7 · versión 0.14.8 | Prueba 11 · Prueba manual 23–26 · Commit |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas y el `.blend`.

## 12. Fuera de alcance

- Casco 3D de HAMMER (spec 3 de cascos).
- **Selector de color** en el juego, guardar la pintura elegida y cambiar el número desde un menú.
- Variantes 1 · FURTIVO y 2 · NAVAL del prototipo.
- Casco 3D en la nave del fondo del menú, en el fantasma y en las naves de prueba del radar.
- Rivales con IA (el casco queda preparado: varias instancias con pinturas distintas).
- Sombra del casco sobre la pista.
- LOD y versión ligera para Android.
- Animar piezas (flaps, lamas, bola de sensores) o sonido.
- Cambios en los efectos de velocidad más allá de colocarlos en las bocas y achatar la llama.
- Menú SETTINGS.
