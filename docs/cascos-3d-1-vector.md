# VECTORY — Spec: Cascos 3D · 1 · Vector (Nocturna)

Versión 1.0 · 2026-11-15 · Estado: **propuesta, pendiente de validar**

Primera spec de los **cascos exteriores realistas**. Cambia el casco wireframe de **VECTOR** por un casco 3D en estilo **carreras**, variante **NOCTURNA**:

- **Silueta del wireframe** con estos retoques:
  - **Alas más cortas**: semiancho de 2,6 m en lugar de 3,4.
  - **Derivas** a la mitad de alto.
  - **Cúpula baja**, sin halo, ajustada al lomo con su junta.
  - **Góndolas achatadas**, de sección octogonal biselada, con la toma de aire un poco levantada y una **tobera bidimensional** con flaps.
- **Materiales**: pintura del catálogo, lomo y alas de carbono, panza de carbono satinado y titanio con tintes de calor en las toberas.
- **Luces**: filetes luminosos del color de acento siguiendo las aristas del casco, ranuras en el morro, pilotos traseros y luz en las derivas.
- **Catálogo de 20 pinturas**, cada una también en versión **con librea de carreras** (franjas, dorsales, pegatinas y damero): **40 combinaciones**, listas para un futuro selector de color.
- **Daño visible**: quemaduras en la pintura, luces que titilan y humo.

Repite la cadena de las cabinas: script de Blender → `.glb` → Godot. El casco se ve en la **cámara exterior**, en el **holograma del selector** y queda **preparado para los rivales con IA**. El **fantasma** sigue siendo wireframe.

Plan de las specs de cascos 3D:

1. **Vector · Nocturna** (esta).
2. BLADE (estilo militar, por diseñar).
3. HAMMER (estilo industrial, por diseñar).

Diseño de referencia:
- **`design/casco-3d-vector.html`**: el prototipo. Se abre en el navegador; se gira con el ratón (o con el dedo en el móvil) y tiene vistas, boost, daño, velocidad, la silueta del wireframe para comparar y el catálogo de pinturas. **Solo vale la variante 3 · NOCTURNA** (la que sale al abrirlo, `VAR = 3`), con el **semiancho de ala a 2,6 m** (el valor por defecto del deslizador).
  - **Va a Blender** todo lo que construye `buildHull()` con `VAR = 3` y `SPAN = 2.6`, incluidas las piezas de librea de `addLivery()` (la geometría; los dibujos los pone Godot). También las texturas (`carbonSet` ×2, `paintSet` neutra, `brushedSet` ×2) y la máscara de quemaduras (`scorchTex`).
  - **Va al código del juego**: el **catálogo de pinturas** (`FINISH`, `PAINTS`), cómo se aplica cada pintura (`paintMat`, colores de acento, `stripeMat`, regla del color del texto), el **dibujo de las calcomanías** (`numberPlate`, `numberRoundel`, `sponsorTex`, `checkerTex`), el **daño** (quemaduras, parpadeo de luces y humo) y el **brillo de levitación**.
  - **No va a ningún sitio**: las variantes 1 · FÓRMULA y 2 · RESISTENCIA (se quedan en el archivo como diseño guardado), el mundo, la pista y la ciudad, las cámaras del prototipo, el panel, la silueta wireframe de comparación, las llamas y estelas del prototipo (el juego ya tiene las suyas, spec `efectos-velocidad`) y la luz de boost del prototipo.
  - Los nombres que se pasan a `mesh()` y `group()` dicen cómo se llama cada pieza en Blender.

Si algo no cuadra entre esta spec y el prototipo, **manda el prototipo** y se avisa.

Rama: `feature/hull-3d-vector`, que sale de `develop` con `feature/cockpit-3d-hammer` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.6 → 0.14.7** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. **No hay cambios en el documento maestro**: el paso de naves y cabinas a estilo realista ya se apuntó con la spec `cabinas-3d-1-vector`.

---

## 1. Resumen

- **Etapa 1 · Blender**: un script nuevo, `hull_vector.py`, construye el casco reutilizando y ampliando `art/blender/lib/`. Lo exporta a `.glb` y guarda:
  - el `.blend`;
  - las máscaras de quemaduras;
  - un manifiesto;
  - las capturas de previsualización.

  En el juego no cambia nada.
- **Etapa 2 · El casco en carrera**: VECTOR lleva el casco 3D en la cámara exterior, con la pintura por defecto (GRAFITO lisa), su luz y sus reflejos.
  - Las **toberas** (llama, núcleo y halo) y las **estelas** salen de los puntos del modelo.
  - El **ancho de colisión** pasa a **2,63 m**.
  - En la vista de cabina, el casco exterior **no tapa nada**.
- **Etapa 3 · Pinturas, libreas y daño**: entran las **40 combinaciones**, con una tecla de depuración para recorrerlas. También entran las **calcomanías** dibujadas por Godot, el efecto **camaleón/perla**, el **daño visible** (quemaduras, luces que titilan, humo) y el **brillo de levitación**.
- **Etapa 4 · Holograma del selector**: SELECT SHIP enseña el casco 3D de VECTOR con su luz, con el mismo giro y la misma escala de siempre.
- BLADE y HAMMER **no cambian**: siguen con su casco wireframe en todas partes.
- Lo que **no cambia**:
  - el fantasma (wireframe de VECTOR);
  - las naves de prueba del radar (wireframe magenta);
  - la nave del fondo del menú principal;
  - las cabinas 3D;
  - el HUD;
  - la conducción, salvo el ancho de colisión.

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **`art/blender/lib/`**: qué piezas hay ya de las cabinas (polígonos, tiras, cajas redondeadas, tubos, cilindros, esferas, ruido, `boxUV`, exportación, capturas) y si dan lo mismo que las de este prototipo. Lo que falte (sección 3.4) se añade **sin cambiar** lo que usan las tres cabinas.
2. **Casco de VECTOR hoy**: dónde se construye el wireframe (`Spaceship`, constructor del perfil visual) y quién lo usa:
   - nave del jugador;
   - fantasma;
   - holograma del selector;
   - nave del menú;
   - naves de prueba del radar (F7);
   - parpadeo de daños.

   Y cómo se esconde hoy en la vista de cabina.
3. **Perfil visual de nave** (spec `naves-1-definicion`): qué campos tiene hoy (constructor del casco, toberas, estela, cámaras, ancho de colisión, cabina) y dónde se leen.
4. **Toberas y estela** (spec `efectos-velocidad`):
   - cómo se colocan la llama, el núcleo y el halo en cada boca;
   - cómo se escalan con el radio (spec `naves-3-cascos`);
   - de dónde salen las estelas de las puntas de las alas.
5. **Luz y entorno del mundo de carrera**:
   - `WorldEnvironment`: fondo, luz ambiente, fuente de reflejos, tonemap y glow;
   - si hay luces en la escena;
   - qué capas visuales (`layers`) están libres.

   El casco realista necesita **luz y reflejos** que hoy no hacen falta (sección 4.3).
6. **Altura de vuelo** de la nave sobre la pista (la `h` del marco de pista) y si el juego sabe cuándo la nave va **en el aire** (spec `pista-3d`), para el brillo de levitación.
7. **Teclas libres** del panel de depuración (hoy se usan F7 a F12). Propuesta para las pinturas: **F6**.
8. **Fuentes**: `Orbitron` no está en el proyecto y hace falta para dorsales y pegatinas (licencia OFL).
9. **Holograma del selector**: cómo se monta hoy (`ShipHologram`, material con `SetMaterialOverride`) y la **escala común en píxeles por metro** que dejó la spec `naves-4-integracion`.
10. **Simulador de equilibrio** de la spec `naves-6-equilibrio`: si se puede ejecutar tal cual para medir VECTOR con el ancho de colisión nuevo (sección 4.6).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · El casco en Blender

### 3.1 Cómo se usa Blender

Igual que con las cabinas: Blender sin ventana, desde la terminal, con la ruta de `art/blender/run.ps1`:

```
blender -b --factory-startup --python art/blender/hull_vector.py -- --out <carpeta>
```

Todo vive en el script, el `.blend` se regenera y el script es **determinista**.

### 3.2 Archivos

| Archivo | Qué es | ¿Va a Git? |
|---|---|---|
| `art/blender/hull_vector.py` | Construye el casco, exporta y guarda todo | Sí |
| `art/blender/lib/` | Piezas comunes + lo nuevo de la sección 3.4 | Sí |
| `assets/…/hull_vector.glb` | Modelo exportado, con las texturas dentro | Sí |
| `assets/…/hull_vector.manifest.json` | Nombres, medidas, puntos y recuentos (sección 3.8) | Sí |
| `assets/…/hull_scorch_1.png`, `_2.png`, `_3.png` | Máscaras de quemaduras por nivel de daño (sección 3.6) | Sí |
| `art/blender/out/hull_vector.blend` | Escena de Blender | No (`.gitignore`) |
| `art/blender/out/previews/hull_vector_*.png` | Capturas de previsualización | No (`.gitignore`) |

La carpeta de `assets/` la elige el agente siguiendo la de las cabinas (por ejemplo, una carpeta hermana `ships/`).

### 3.3 Ejes, unidades y origen

- Metros. Ejes del prototipo = ejes de `Spaceship.cs` y de Godot: **X derecha, Y arriba, morro hacia −Z**.
- Todo dentro de un objeto raíz **`HullVector`** girado **+90° en X** en Blender, como las cabinas, con los números del prototipo copiados **tal cual**.
- El **origen** es el **origen del casco wireframe** de hoy (el de `Spaceship`), así que el casco nuevo cae en el mismo sitio que el viejo.
- **Ojo con la altura del prototipo**: el prototipo pone la nave a 0,65 m sobre su pista (`HOVER`). Esa altura es solo del prototipo y **no** va al modelo.
- Comprobación antes de exportar (en coordenadas de Godot, sin librea; tolerancia 5 mm salvo que se diga otra cosa):

| Punto | Valor |
|---|---|
| Punta del morro | z = −4,00 (y = 0) |
| Final de los flaps de las toberas | z = +3,31 |
| Caja total en z | −4,00 → 3,31 (largo **7,31 m**) |
| Semiancho (cara exterior de las derivas, con bisel) | **2,634 m** |
| Caja total en y | −0,31 (aletas del difusor) → **0,706** (cima de la cúpula); alto **1,016 m** |
| Cima de la cúpula | (0, 0,692 ± 0,015, −0,85) |
| Arriba de las derivas | y = 0,31 (con bisel, 0,316) |
| Góndolas | x = ±0,75 (centro), de x = 0,33 a 1,17; toma de aire en z = 0,80; techo a y = 0,42 en la toma |
| Boca de la tobera | z = 3,20 |

### 3.4 Geometría

Se traduce `buildHull()` con `VAR = 3` y `SPAN = 2.6` **pieza a pieza**, con los mismos números y en el mismo orden. Piezas nuevas para `art/blender/lib/`:

| Prototipo | En Blender |
|---|---|
| `prof(z)` + `herm()` | Perfil del fuselaje: de la punta (z −4) a la 1.ª sección (z −2,5), cada valor × `1 − (1 − t)²`; entre las secciones del wireframe (`PZ`, `PV`), **Hermite monótona** con las tangentes de `herm()` (también el recorte de Fritsch-Carlson, `s > 9`) |
| `fusPt(z, θ, n, off)` | Sección en **superelipse** de exponente **n = 1,7** (NOCTURNA) entre los 4 puntos del rombo; `off` desplaza en radial desde el centro (0, (top + bot)/2) |
| `fusPatch(θ0, θ1, z0, z1, n, off, uvFn)` | Trozo de piel: anillos repartidos con `z = Z0 + (Z1 − Z0)·u^1,5`, `NZ = max(4, redondeo(70·(u1 − u0)))`, `NT = max(3, redondeo(64·(θ1 − θ0)/2π))` |
| `fusCap(n)` | Tapa trasera en abanico, 64 lados |
| `fusTopY`, `canopyGeo` (`CAN`) | Cúpula: planta elíptica A 0,34 · B 1,15 · centro z −0,85; altura = piel del lomo + 0,002 + 0,11·(1 − r²)^0,6; rejilla 16 × 72. **En el borde toca la piel exacta** |
| `wingGeo(k, S, tA, tB, off)` | Ala: planta del wireframe con la punta en x = ±2,6; la raíz se prolonga hacia dentro hasta x = 0,5 (`t0`); perfil NACA simétrico de grosor 0,16 → 0,045; 12 × (16 + 14) puntos |
| `endplateGeo()` | Deriva: polígono (2,32, −0,08) (2,56, 0,31) (2,70, 0,31) (2,70, −0,08), extruido **0,035 m** con bisel de **0,006 m** y 2 segmentos. Con el modificador Bevel de Blender (aplicado) basta: no hace falta que los vértices del bisel coincidan uno a uno |
| `lathe(pts, seg)` | Torno de three.js (`LatheGeometry`) alrededor del eje Z, con los mismos lados |
| `octRing`, `secAt`, `octLoft`, `octAnnulus`, `octFan` (`POD`, `NOZ`) | Góndola y tobera: secciones **octogonales biseladas** `[z, a, b, c, u]` interpoladas con smoothstep; `u` sube el techo (la toma de aire). **Cada una de las 8 caras es una tira propia** (aristas vivas entre caras, suaves a lo largo) |
| `rrect(w, h, r)` | Rectángulo redondeado con curvas cuadráticas, 6 segmentos por esquina |
| `tube(pts, r, seg)` | Tubo de **12 lados** por **Catmull-Rom centrípeta** (como en las cabinas, pero 12 lados) |
| Cajas, esferas (12 × 8), cilindros, círculos y planos | Con las mismas medidas y lados |

Partes del casco (detalle en el prototipo):

- **`Fuselage`**:
  - piel de arriba (`Fuselage_Upper`, θ 0 → π, con pintura);
  - piel de abajo (`Fuselage_Lower`, θ π → 2π, carbono satinado);
  - tapa trasera (`Fuselage_Rear`);
  - **lomo de carbono** (`Livery_Spine_Front` y `Livery_Spine_Rear`, θ π/2 ± 0,26, a 4 mm, **cortado por la cúpula** con `cutCanopy`);
  - filetes luminosos (`Accent_Chine_R` / `_L`, θ 0,10–0,135 y su simétrico, de z −3,7 a 2,6, a 6 mm);
  - ranuras del morro (`Light_Nose_R` / `_L`, θ 0,2–0,36, z −3,55 a −3,0);
  - pilotos traseros (`Light_Tail_L` / `_R`);
  - 5 aletas del difusor (`Diffuser_Fin`);
  - splitter bajo el morro (`Splitter`).
- **`Canopy`**: cristal (`Canopy_Glass`) y junta (`Canopy_Seal`, tubo de 0,016 m sobre el borde, 4 mm por encima de la piel).
- **`Wing_L` / `Wing_R`**:
  - piel del ala (`Wing_*_Skin`);
  - borde de ataque luminoso (`Accent_WingLE_*`);
  - deriva (`Endplate_*`);
  - su filo luminoso (`Accent_Endplate_*`);
  - luz de la deriva (`Light_Fin_*`);
  - el punto de la estela (`Trail_*`, vacío).
- **`Pod_L` / `Pod_R`** (en x = ±0,75, y = 0,1):
  - carenado (`Pod_*_Cowl`);
  - banda luminosa (`Accent_PodBand_*`, z 1,30–1,34);
  - 2 juntas (`Pod_*_Seam_0/1`);
  - labio de la toma por fuera y por dentro (`Pod_*_IntakeLip`, `Pod_*_IntakeLipIn`);
  - conducto (`Pod_*_IntakeDuct`);
  - cara del compresor (`Pod_*_Face`);
  - ventilador (`Pod_*_Fan`, escalado 1 × 0,7 × 1 y subido 0,025), con cono (`Pod_*_Spinner`) y 16 álabes (`Pod_*_Blade`);
  - rejilla de refrigeración (`Pod_*_Vent` ×4 y `Pod_*_VentFrame`);
  - tobera (`Nozzle_*`), su labio (`Nozzle_*_Lip`) y su interior (`Nozzle_*_Inner`);
  - flaps (`Nozzle_*_FlapTop` / `_FlapBottom`, con 5 ranuras `Nozzle_FlapGroove` cada uno);
  - paredes laterales (`Nozzle_*_WallR` / `_WallL`);
  - actuadores (`Nozzle_*_Actuator`, `Nozzle_*_ActuatorBody`);
  - fondo luminoso azul (`Nozzle_Glow_*`, rectángulo redondeado de 0,62 × 0,24 a z 2,90) y su núcleo (`Nozzle_Core_*`, 0,36 × 0,08 a z 2,95);
  - la boca (`Exhaust_*`, vacío en (±0,75, 0,1, 3,2)).
- **`Livery`** (piezas que **solo se ven con librea**, sección 6.3). Geometría de `addLivery()`:
  - franjas dobles del lomo (`Livery_TwinStripe_R/L_Front/Rear`, θ π/2 − 0,40 a π/2 − 0,29 y su simétrico, a 6 mm, cortadas por la cúpula);
  - dorsal de la nariz (`Decal_Number_Nose`, θ π/2 ± 0,5, z −2,98 a −2,62, a 8 mm);
  - pegatinas de los flancos (`Decal_Sponsor_R` / `_L`, θ 0,26–0,78 y su simétrico, z −1,75 a −0,35, a 8 mm);
  - banda y filete de las alas (`Livery_WingBand_*`, t 0,42–0,60; `Livery_WingPin_*`, t 0,63–0,67; a 4 mm);
  - en cada góndola: dorsal (`Decal_Number_Pod_*`, plano de 0,34 × 0,204 m en la cara exterior, z 1,86), pegatina (`Decal_Sponsor_Pod_*`, 0,42 × 0,105 m, z 2,40) y damero (`Decal_Checker_*`, 0,56 × 0,14 m en el techo, z 2,56).
- **`Smoke`** (vacío nuevo, no está en el prototipo como pieza): en (0,75, 0,5, 2,2), de donde sale el humo con daño (el mismo punto que usa el prototipo).

Reglas de las UV (las texturas usan UV en **metros × repetición**, sin `KHR_texture_transform`):
- **Piel del fuselaje, librea del lomo**: `(z, θ × radio medio)` con radio medio = (hw + (top − bot)/2)/2 a esa z.
- **Calcomanías del fuselaje** (`uvFn`), **UV 0–1** con la orientación del prototipo, para que se lean:
  - lado derecho: (1 − u, v);
  - lado izquierdo: (u, 1 − v);
  - nariz: (1 − v, 1 − u).

  Los planos de las góndolas, UV 0–1 de un plano, con la imagen derecha vista desde fuera.
- **Ala**: `(z, x)`. **Torno**: `(z, ángulo × 0,4)` con la costura **por debajo**. **Octógono**: `(z, perímetro)`. **Deriva y splitter**: las de la forma (metros).
- **Primitivas** (cajas, esferas, cilindros, tubos): basta con proyección de caja en metros, como `boxUV` de las cabinas.
- **Segunda capa de UV (UV2)** en todas las piezas con pintura, carbono, aluminio o titanio: las mismas UV en metros **sin la repetición** y × **1,1**. Es para las quemaduras (sección 6.5).

- **Caras por los dos lados**: el prototipo dibuja todo con `DoubleSide`. Como en las cabinas, los materiales van **con doble cara**, salvo que el agente compruebe que una pieza solo se ve por un lado.
- **Sombreado**: suave en todo, salvo entre las caras del octógono, las cajas y los planos (aristas vivas).
- Las piezas con `noShadow` **no proyectan sombra** (luces, cristal, calcomanías). Se apuntan en el manifiesto.

### 3.5 Materiales y texturas

- **Principled BSDF** con **texturas de imagen**, sin nodos procedurales. Texturas generadas **en Python** con el **mismo algoritmo y las mismas semillas** del prototipo, a **512 × 512**: color (sRGB), rugosidad y normal (no color, OpenGL). `mkRng` enmascarado a 32 bits sin signo en cada paso. Mismo orden de llamadas al generador que el prototipo.

| Conjunto | Llamada del prototipo | Qué es |
|---|---|---|
| Carbono | `carbonSet(11, 0)` | Sarga 2×2 de mechas de 16 px; las dos direcciones con brillo distinto (0,62) |
| Carbono satinado | `carbonSet(12, 0.28)` | El mismo, con +0,28 de rugosidad |
| Pintura neutra | `paintSet(24, [255, 255, 255])` | Blanca con piel de naranja suave; el color lo pone el catálogo (textura × color) |
| Aluminio | `brushedSet(31, [176, 182, 190], false)` | Cepillado en filas |
| Titanio | `brushedSet(32, [150, 148, 146], true)` | Cepillado con tintes de calor azules y dorados |

| Material | Base | Metal | Rugosidad | Repetición | Extra |
|---|---|---|---|---|---|
| `M_Paint` | pintura neutra × `#222429` (GRAFITO) | 0,6 | textura × 1,7 | ×1 | Barniz 0,5 · rugosidad del barniz 0,22 · normal ×0,4. **Godot cambia color y acabado** (sección 6.1) |
| `M_Carbon` | carbono | 0,2 | textura | ×4 | Barniz 1 · 0,05 |
| `M_CarbonSatin` | carbono satinado | 0,2 | textura | ×4 | Barniz 0,3 · 0,35 |
| `M_Alu` | aluminio | 1 | textura | ×2 | — |
| `M_Titanium` | titanio | 1 | textura | ×2 | — |
| `M_Dark` | `#0B0B0D` | 0,2 | 0,7 | — | Tapa trasera, junta, juntas de góndola, rejillas, ranuras de los flaps |
| `M_Intake` | `#050506` | 0,1 | 0,9 | — | Conductos, cara del compresor, interior de tobera |
| `M_CanopyIris` | `#120709` al 70 % | 0,1 | 0,05 | — | Transparente, barniz 1, reflejos ×1,8, **iridiscencia 1** (IOR 1,6, grosor 220–620 nm). Godot: sección 6.2 |
| `M_LiveryStripe`, `M_LiveryPin` | pintura neutra | 0,1 | textura | ×1 | Barniz 1 · 0,04. Godot pone el color |
| `M_Decal_*` | negro (la textura la pone Godot) | 0 | 0,35 | — | Transparente con alfa, barniz 1 · 0,05 |
| `M_Emissive_*` | negro + emisión | 0 | 0,4 | — | **Una instancia por pieza viva** (sección 3.8). Color por defecto: acento de GRAFITO `#FF2D2D`; pilotos traseros `#FF1A1A` |
| `M_Emissive_NozzleGlow` / `_NozzleCore` | negro + emisión `#245BFF` · 2,2 / `#7FA6FF` · 3,5 | 0 | 0,4 | — | — |

- El **barniz** (clearcoat) y la **iridiscencia** se apuntan también en el manifiesto, por si no llegan por glTF.
- Las capas de librea y las calcomanías van **un poco por fuera** (4–8 mm) y en el prototipo además con *polygon offset*. En Godot, si se ve parpadeo de profundidad, se sube el desplazamiento de esas piezas en Blender y se avisa.

### 3.6 Máscaras de quemaduras

- `scorchTex(41)`: ruido fbm de celda 96 y 5 octavas, 512 × 512, un canal.
- El script guarda **tres máscaras**, una por nivel de daño: `k = smoothstep(th, th + 0,16, ruido)` con `th = 1 − [0,24; 0,36; 0,48]` para los niveles 1, 2 y 3 → `hull_scorch_1.png`, `_2.png`, `_3.png` (gris, sin color). Las usa Godot (sección 6.5).

### 3.7 Exportación

Igual que las cabinas:
- `.glb`, +Y arriba, modificadores aplicados;
- normales, **las dos capas de UV** y texturas **dentro**;
- sin cámaras, luces ni animaciones;
- solo lo que cuelga de `HullVector`, **con** las piezas de librea y los vacíos (`Exhaust_*`, `Trail_*`, `Smoke`).

### 3.8 Nombres de las piezas (contrato con Godot)

Godot busca estas piezas **por nombre**. El script comprueba antes de exportar que **están todas** y, si falta una, **falla con un mensaje claro**.

| Nombre | Qué es | Cuántas |
|---|---|---|
| `HullVector` | Raíz (origen = el del casco wireframe) | 1 |
| `Exhaust_L`, `Exhaust_R` | Vacíos en la boca de cada tobera: (±0,75, 0,1, 3,2) | 2 |
| `Trail_L`, `Trail_R` | Vacíos de las estelas: (±2,6, −0,05, 2,72) | 2 |
| `Smoke` | Vacío del humo: (0,75, 0,5, 2,2) | 1 |
| `Fuselage_Upper`, `Endplate_L/R`, `Pod_L/R_Cowl` | Piezas con la pintura del catálogo (`M_Paint`) | 5 |
| `Accent_Chine_L/R`, `Accent_WingLE_L/R`, `Accent_Endplate_L/R`, `Accent_PodBand_L/R` | Luces de perfil (color de acento) | 8 |
| `Light_Nose_L/R`, `Light_Fin_L/R` | Luces del morro y de las derivas (color de acento) | 4 |
| `Light_Tail_L/R` | Pilotos traseros (rojo fijo) | 2 |
| `Nozzle_Glow_L/R`, `Nozzle_Core_L/R` | Fondo luminoso de las toberas | 4 |
| `Canopy_Glass` | Cúpula | 1 |
| `Livery_TwinStripe_R_Front`, `_R_Rear`, `_L_Front`, `_L_Rear`, `Livery_WingBand_L/R` | Franjas de la librea (color de las franjas) | 6 |
| `Livery_WingPin_L/R` | Filete del ala (color del fondo del dorsal) | 2 |
| `Decal_Number_Nose` | Dorsal redondo de la nariz, UV 0–1 | 1 |
| `Decal_Number_Pod_L/R` | Dorsal de las góndolas: **0,34 × 0,204 m**, UV 0–1 | 2 |
| `Decal_Sponsor_L/R` | Pegatina de los flancos, UV 0–1 | 2 |
| `Decal_Sponsor_Pod_L/R` | Pegatina de las góndolas: **0,42 × 0,105 m**, UV 0–1 | 2 |
| `Decal_Checker_L/R` | Damero: **0,56 × 0,14 m**, UV 0–1 | 2 |

El resto se agrupa por zonas (`Fuselage`, `Canopy`, `Wing_L`, `Wing_R`, `Pod_L`, `Pod_R`, `Livery`), con los nombres del prototipo dentro. Las piezas repetidas (álabes, aletas, ranuras) pueden llevar sufijo numérico.

**Manifiesto** (`hull_vector.manifest.json`), con el formato de los de las cabinas:
- Versión de Blender y fecha.
- Piezas vivas de la tabla con su color de emisión e intensidad de partida (sección 6.4), y los vacíos con su posición.
- Qué piezas llevan pintura, franjas, filete y calcomanías.
- Piezas sin sombra, materiales con barniz, iridiscencia, reflejos distintos de 1 y transparencia.
- **Caja total** sin librea y con librea (coordenadas de Godot) y **semiancho** (mayor |x|).
- Triángulos totales sin librea y con librea. Como referencia, el prototipo da **≈ 23 800** y **≈ 27 600**; en Blender puede variar un poco por la triangulación.

### 3.9 Capturas de previsualización

En `art/blender/out/previews/`, a 1280 × 720, con fondo oscuro y una luz parecida a la del prototipo (sección 4.3). Las cámaras son las del prototipo **con 0,65 m menos de altura** (en el prototipo la nave flota a 0,65 m):

| Captura | Cámara | Mira a | FOV vertical |
|---|---|---|---|
| `hull_vector_tq.png` | (−7,6, 3,0, −7,4) | (0, 0,25, 0,2) | 38° |
| `hull_vector_top.png` | (0, 16,35, 0,25) | (0, 0, 0,2) | 34° |
| `hull_vector_side.png` | (11, 0,4, −0,3) | (0, 0,3, −0,3) | 34° |
| `hull_vector_rear.png` | (4, 1,8, 13,5) | (0, 0,3, 0,5) | 34° |
| `hull_vector_livery.png` | la de `tq` | — | 38° |

En `hull_vector_livery.png` se ven las piezas de librea con una **textura de prueba** (cuadrícula con una flecha hacia arriba) en las calcomanías, para comprobar que **ninguna sale en espejo ni girada**. Las demás, sin librea.

Motor: el mismo que con las cabinas. El agente **mira las capturas** antes de cerrar la etapa y las compara con el prototipo abierto en el navegador, en la misma vista, con la pintura GRAFITO lisa.

## 4. Etapa 2 · El casco en carrera

### 4.1 Importación

Igual que las cabinas: nombres tal cual, sin colisiones, sin LOD ni luz horneada. Al cargar, el script del casco aplica lo del manifiesto (sombras, barniz, reflejos, transparencias). Si algo no llega bien, se arregla **en Godot con su material**, sin tocar la geometría, y se avisa.

### 4.2 Perfil visual de VECTOR

- El perfil visual gana un **casco 3D**: ruta del `.glb` y del manifiesto. Si una nave lo tiene, la nave del jugador usa el casco 3D; si no, el wireframe de siempre. VECTOR → 3D; BLADE y HAMMER → wireframe.
- **El casco wireframe de VECTOR se queda** en el código y en su prueba de captura (spec `naves-1-definicion`), porque lo siguen usando:
  - el **fantasma**;
  - las **naves de prueba** del radar;
  - la **nave del fondo del menú**;
  - el **holograma** hasta la etapa 4.
- **Toberas**: la llama, el núcleo y el halo de `efectos-velocidad` se colocan en `Exhaust_L/R`, con la misma lógica, el mismo radio de referencia (0,40) y la **boca achatada**: **× 1,2 de ancho y × 0,55 de alto** (como `ex.scale` del prototipo), apuntado en el perfil visual. Si achatar la llama no se puede hacer sin tocar la lógica de los efectos, **se avisa antes**.
- **Estelas** de las alas: salen de `Trail_L/R` (las puntas de las alas nuevas, más cortas).
- **Cámara exterior**: el factor de VECTOR **sigue en 1** (el largo pasa de 7,2 a 7,31 m; no merece la pena cambiarlo). Se ajusta probando si hace falta.
- Los puntos se leen del manifiesto o de los vacíos del modelo, **no** se copian a mano en el código. Una prueba comprueba que el perfil y el manifiesto coinciden.

### 4.3 Luz y reflejos

El mundo de carrera está hecho para líneas luminosas; el casco realista necesita **luz y reflejos propios** sin cambiar cómo se ve el resto.

- El casco va en una **capa visual propia** (la que esté libre, punto 5 del inventario), y su luz **solo afecta a esa capa** (`light_cull_mask`). La pista, la ciudad y los efectos no cambian.
- Luces del prototipo como **punto de partida** (se ajustan a ojo comparando con el prototipo y se apuntan los valores finales):

| Luz | Tipo | Dirección (desde → hacia el centro) | Color · intensidad en el prototipo | Notas |
|---|---|---|---|---|
| Principal | Direccional | desde (4, 10, −5) | `#FFF1E0` · 2,4 | Con sombras suaves **sobre el propio casco** |
| Contraluz | Direccional | desde (−6, 2,5, 9) | `#FF9D50` · 1,3 | La luz ámbar de la ciudad por detrás |
| Relleno | Direccional | desde (8, 3, 2) | `#5F8CFF` · 0,55 | — |
| Ambiente | Hemisferio | — | cielo `#B0B8C8` / suelo `#101010` · 0,3 | Lo más parecido con el `Environment` |

- Las luces **giran con la nave** (van colgadas de ella), para que el casco se vea igual en cualquier curva.
- **Reflejos**: el casco refleja el **panorama de reflejos de las cabinas** (`cockpit_env.png`, el de Vector). Se busca la forma más sencilla de que **solo** el casco lo use:
  - cielo del `Environment` usado solo para reflejos, sin cambiar el fondo;
  - o una `ReflectionProbe` que siga a la nave.

  Si cualquiera de las dos cambia el aspecto de la pista o de la ciudad, **se avisa con capturas** antes de seguir.
- **Sombra en la pista**: fuera de alcance (el brillo de levitación de la etapa 3 hace de contacto con el suelo).
- **Glow**: el casco usa el glow del juego. Las intensidades de emisión son las del prototipo (umbral 0,85); se ajustan a ojo para que las luces de perfil se vean **suaves** y las del morro y las traseras, **vivas**, como en el prototipo.

### 4.4 Vista de cabina

- En la vista de cabina, el casco exterior **no se dibuja** (la cámara está dentro y vería la cúpula y el morro por dentro a través del cristal de la cabina 3D). Se hace como hoy con el wireframe (punto 2 del inventario) o con la capa visual del casco fuera de la máscara de la cámara.
- Si es sencillo, el casco puede quedarse en **«solo sombra»** en la vista de cabina. Como no hay sombra en la pista (4.3), no hace falta.

### 4.5 Ancho de colisión

- El ancho de colisión de VECTOR pasa de **3,4 m** a su semiancho nuevo: **2,63 m** (el del manifiesto, redondeado al centímetro).
- La prueba de la spec `naves-3-cascos` (**ancho de colisión = semiancho real del casco**, ±0,01 m) pasa a mirar el **manifiesto** cuando la nave tiene casco 3D. BLADE y HAMMER, igual que antes.

### 4.6 Equilibrio

Con las alas más cortas, VECTOR choca menos con las barreras.
- Al cerrar la etapa, el agente ejecuta el **simulador de equilibrio** de la spec `naves-6-equilibrio` con VECTOR y apunta los tiempos por vuelta antes y después.
- **No se toca ningún número** de las naves: si VECTOR se sale del margen de esa spec, **se avisa** con los datos y se decide aparte.

### 4.7 Qué se ve en cada caso

| | VECTOR | BLADE / HAMMER |
|---|---|---|
| Cámara exterior (jugador) | **Casco 3D** | Wireframe |
| Vista de cabina | Cabina 3D (casco exterior oculto) | Cabina 3D |
| Fantasma | Wireframe de VECTOR | Wireframe de su nave |
| Naves de prueba (F7) | Wireframe magenta | — |
| Menú principal | Wireframe (sin cambios) | — |
| Holograma del selector | Wireframe hasta la etapa 4; **casco 3D** después | Wireframe |

## 5. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Perfil visual de nave**: casco 3D opcional (`.glb`, manifiesto, achatado de la boca), además de lo que ya tiene.
- **`ShipHull3D`** (nodo Godot, escena reutilizable): carga el `.glb`, aplica el manifiesto, busca las piezas vivas por nombre (error claro si falta una) y expone:
  - `SetPaint(pintura, conLibrea)`;
  - `SetDamage(nivel, timeTrial)`;
  - `SetHoverVisible(…)`;
  - los puntos de toberas, estelas y humo.

  **Sin reglas dentro**: recibe estados y los pinta. Cada instancia tiene **sus propios materiales**, así que dos naves pueden llevar pinturas distintas a la vez (**preparado para los rivales**).
- **Lector del manifiesto** (puro): el de las cabinas, ampliado con vacíos, caja y semiancho si hace falta.
- **Catálogo de pinturas** (puro, datos): acabados y pinturas de la sección 6.1, con sus `id`.
- **Aplicación de pintura** (pura): de (pintura, con librea) a lo que hay que poner en cada grupo de piezas:
  - color y acabado de la pintura;
  - color de acento;
  - color de las franjas y del filete;
  - qué piezas se ven;
  - textos y colores de las calcomanías;
  - color del texto (regla de la sección 6.3).
- **Dibujantes de calcomanías** (puros): dan primitivas 2D (como los de las pantallas de las cabinas) para el dorsal rectangular, el dorsal redondo, la pegatina y el damero. Un nodo las pinta **una vez** en una textura al cambiar de pintura.
- **Daño visible** (puro):
  - del nivel de daño a la máscara de quemaduras;
  - el parpadeo de luces con las reglas de `danos`;
  - el humo por segundo.
- Los colores fijos (pilotos traseros, toberas, humo, levitación) van como **constantes con nombre**.

## 6. Etapa 3 · Pinturas, libreas y daño

### 6.1 Catálogo de pinturas

Es `FINISH` y `PAINTS` del prototipo. **20 pinturas**, cada una **lisa** y **con librea**: **40 combinaciones**, en este orden: las 20 lisas y luego las 20 con librea.

**Acabados** (metal, rugosidad, barniz y su rugosidad, iridiscencia). En el prototipo la rugosidad es un **factor** que multiplica la textura de la pintura (≈ 0,25 de media). Si Godot no deja un factor mayor que 1, se usa el **valor final** de la última columna, sin textura de rugosidad:

| Acabado | Metal | Factor de rugosidad | Barniz · rugosidad | Iridiscencia | Rugosidad final (≈ factor × 0,25) |
|---|---|---|---|---|---|
| BRILLO | 0,1 | 1,0 | 1,0 · 0,04 | — | 0,25 |
| METALIZADO | 0,6 | 1,4 | 0,8 · 0,10 | — | 0,35 |
| SATINADO | 0,6 | 1,7 | 0,5 · 0,22 | — | 0,42 |
| MATE | 0,2 | 3,2 | 0 · 0,50 | — | 0,80 |
| CROMADO | 1,0 | 0,5 | 1,0 · 0,03 | — | 0,12 |
| PERLA | 0,25 | 1,0 | 1,0 · 0,04 | 0,35 | 0,25 |
| CAMALEÓN | 0,5 | 1,0 | 1,0 · 0,04 | 1,0 | 0,25 |

**Pinturas** (color de la pintura, acabado, color de acento y datos de la librea: color de las franjas, fondo y número del dorsal):

| # | `id` | Nombre | Color | Acabado | Acento | Franjas | Fondo dorsal | Número | Dorsal |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `graphite_red` | GRAFITO | `#222429` | SATINADO | `#FF2D2D` | `#FF2D2D` | `#F2F2F2` | `#111111` | 07 |
| 2 | `piano_cyan` | NEGRO PIANO | `#0A0B0D` | BRILLO | `#36E7FF` | `#36E7FF` | `#36E7FF` | `#050505` | 21 |
| 3 | `black_chrome` | CROMO NEGRO | `#2A2C31` | CROMADO | `#FF9D32` | `#FF9D32` | `#111111` | `#FF9D32` | 88 |
| 4 | `stealth_matte` | SIGILO | `#2B2E33` | MATE | `#FF2D2D` | `#FF2D2D` | `#FF2D2D` | `#111111` | 00 |
| 5 | `pearl_white` | BLANCO PERLA | `#E4E6EA` | PERLA | `#FF2D2D` | `#1A42B0` | `#FFFFFF` | `#1A42B0` | 14 |
| 6 | `liquid_silver` | PLATA LÍQUIDA | `#C3C8CF` | CROMADO | `#3D7BFF` | `#B5121B` | `#FFFFFF` | `#B5121B` | 3 |
| 7 | `titanium` | TITANIO | `#6A6E75` | METALIZADO | `#FFB000` | `#FFB000` | `#111111` | `#FFB000` | 42 |
| 8 | `racing_red` | ROJO CARRERAS | `#B5121B` | BRILLO | `#FFF4E6` | `#F2F2F2` | `#F2F2F2` | `#B5121B` | 1 |
| 9 | `lava` | LAVA | `#5A0A0C` | METALIZADO | `#FF9D32` | `#FF9D32` | `#FF9D32` | `#1A0505` | 66 |
| 10 | `amber` | ÁMBAR | `#E0701A` | BRILLO | `#FFFFFF` | `#111111` | `#111111` | `#FFFFFF` | 27 |
| 11 | `taxi` | AMARILLO TAXI | `#E8B414` | BRILLO | `#FF2D2D` | `#111111` | `#111111` | `#E8B414` | 99 |
| 12 | `gold` | ORO | `#C8963A` | CROMADO | `#FFF4E6` | `#111111` | `#111111` | `#C8963A` | 24 |
| 13 | `acid_green` | VERDE ÁCIDO | `#7CC81A` | BRILLO | `#FFFFFF` | `#111111` | `#FFFFFF` | `#111111` | 55 |
| 14 | `british_green` | VERDE INGLÉS | `#0F3A28` | METALIZADO | `#3DFF8A` | `#E8D9A8` | `#E8D9A8` | `#0F3A28` | 9 |
| 15 | `cobalt` | COBALTO | `#1A42B0` | METALIZADO | `#36E7FF` | `#F2F2F2` | `#F2F2F2` | `#1A42B0` | 12 |
| 16 | `midnight` | MEDIANOCHE | `#0E1634` | METALIZADO | `#6F9BFF` | `#C8963A` | `#C8963A` | `#0E1634` | 33 |
| 17 | `ice` | HIELO | `#9FD8E8` | PERLA | `#36E7FF` | `#1A42B0` | `#FFFFFF` | `#1A42B0` | 71 |
| 18 | `violet` | VIOLETA | `#4B1A82` | METALIZADO | `#FF43D0` | `#E8B414` | `#E8B414` | `#4B1A82` | 18 |
| 19 | `neon_pink` | ROSA NEÓN | `#D42E7C` | BRILLO | `#36E7FF` | `#111111` | `#36E7FF` | `#111111` | 23 |
| 20 | `chameleon` | CAMALEÓN | `#1C1630` | CAMALEÓN | `#FF43D0` | `#E4E6EA` | `#E4E6EA` | `#1C1630` | 13 |

- **Pintura por defecto**: GRAFITO lisa (la 1).
- Por ahora **no se guarda** ni se elige en ningún menú (el selector de color es una spec futura). Las naves de los rivales podrán llevar cualquiera.
- El **número del dorsal** es un dato de la pintura; un selector futuro podrá cambiarlo sin tocar el modelo.
- Los nombres son internos: si un día salen en pantalla, se revisan.

### 6.2 Cómo se aplica una pintura

- **`M_Paint`** (piel de arriba, derivas y góndolas): color de la pintura × textura neutra, con el acabado de la tabla.
- **Lomo, alas, panza, toberas y el resto**: **no cambian** con la pintura.
- **Color de acento**:
  - **Llevan el acento**: `Accent_Chine_*`, `Accent_WingLE_*`, `Accent_Endplate_*`, `Accent_PodBand_*`, `Light_Nose_*` y `Light_Fin_*`.
  - **Rojo fijo** `#FF1A1A`: los pilotos traseros (`Light_Tail_*`).
  - **Azul fijo**: el fondo de las toberas.
- **Iridiscencia** (PERLA, CAMALEÓN y la cúpula `M_CanopyIris`): `StandardMaterial3D` de Godot no la tiene.
  - Se hace con un **shader pequeño** que añade un **cambio de color según el ángulo de vista** (aproximación de capa fina), con la fuerza de la columna «Iridiscencia».
  - Se compara a ojo con el prototipo con CAMALEÓN, BLANCO PERLA y la cúpula.
  - Si se complica, se aproxima con un degradado de borde (fresnel) entre dos colores y **se avisa**.
- **Depuración**: la tecla libre (propuesta **F6**, punto 7 del inventario) recorre las **40 combinaciones** en el orden de la sección 6.1; **Mayúsculas + F6** va hacia atrás. El nombre (y `#número` con librea) sale en el panel (F9). Solo en compilaciones de depuración.

### 6.3 Libreas de carreras

Con librea, sobre la misma pintura:

- **Se ven** las piezas `Livery_*` y `Decal_*`. Sin librea, **no se dibujan**.
- **Franjas dobles del lomo y banda del ala** (`Livery_TwinStripe_*`, `Livery_WingBand_*`): color de las **franjas**, acabado BRILLO (barniz 1 · 0,04).
- **Filete del ala** (`Livery_WingPin_*`): color del **fondo del dorsal**, acabado BRILLO.
- **Derivas** (`Endplate_*`): pasan al color de las **franjas**.
- **Color del texto** de las pegatinas: si la suma R + G + B del color de la pintura (0–255 cada uno) es **mayor que 360**, `#111111`; si no, `#F2F2F2`.

Calcomanías, dibujadas **una vez** por Godot al cambiar de pintura, con el mismo dibujo que el prototipo (fuente **Orbitron Black** en cursiva; Orbitron no tiene cursiva: se inclina con una `FontVariation` como hace el navegador, unos 12°):

| Pieza | Lienzo | Dibujo (como el prototipo) |
|---|---|---|
| `Decal_Number_Pod_*` (`numberPlate`) | 640 × 384, fondo transparente | Rectángulo redondeado del color de las franjas (margen 6, radio 46); dentro, otro del **fondo del dorsal** (margen 26, radio 34); el número centrado, en el color del número, **250 px** |
| `Decal_Number_Nose` (`numberRoundel`) | 512 × 512 | Círculo de radio 250 color de las franjas; dentro, círculo de radio 222 del fondo; el número centrado, 200 px (250 si es de una cifra) |
| `Decal_Sponsor_L/R` (`sponsorTex`) | 1024 × 256 | Barra del color de las franjas de 22 px a 46 px del borde de abajo y otra de 10 px y el 62 % del ancho, **centrada**; `NULLDRIVE` en cursiva de 104 px y `KAIROS GRID · VX-7` de 64 px, **centrados**, en el color del texto |
| `Decal_Sponsor_Pod_*` (`sponsorTex`) | 1024 × 256 | Igual, con `VECTORY` en cursiva de 150 px |
| `Decal_Checker_*` (`checkerTex`) | 512 × 128 | Damero de cuadros de 32 px, `#F2F2F2` y `#0B0B0B` |

- Todo **centrado**: así las pegatinas de los dos costados quedan **simétricas**.
- Los patrocinadores son **inventados**. No se añade ninguno más: el usuario prefiere pocas pegatinas.

### 6.4 Intensidad de las luces

Valores de emisión del prototipo (se ajustan a ojo con el glow del juego y se apuntan):

| Luces | Intensidad |
|---|---|
| Filetes (`Accent_Chine_*`) | **0,9** |
| Borde del ala, filo de la deriva, banda de la góndola | **1,0** |
| Ranuras del morro (`Light_Nose_*`) | 3 |
| Pilotos traseros (`Light_Tail_*`) | 3,5 |
| Luz de las derivas (`Light_Fin_*`) | 3,5 |
| Fondo / núcleo de las toberas | 2,2 / 3,5 (× 1,6 con boost, como el prototipo) |

Las luces de perfil van **suaves**. Las del morro y las traseras le gustan al usuario como están en el prototipo: **no se bajan**.

### 6.5 Daño visible

Solo en RACE, con el nivel de daño de la spec `danos`. En TIME TRIAL, nada.

- **Quemaduras**: en las piezas con pintura, carbono, franjas, aluminio y titanio (no en luces, cristal ni calcomanías).
  - Se usa el **detalle** de `StandardMaterial3D` sobre la **UV2**: máscara = `hull_scorch_<nivel>.png`, color del detalle casi negro (`#050404`), mezcla normal.
  - Nivel 0: sin detalle.
  - Si no se puede con el detalle (por ejemplo, en los materiales con shader de iridiscencia), se añade al shader y **se avisa**.
  - El prototipo además vuelve **mate** lo quemado (rugosidad hacia 0,85). Si con el detalle no se puede y se echa de menos, se avisa.
- **Parpadeo**: desde el nivel 1, las luces `Accent_*` y `Light_*` (incluidos morro, derivas y pilotos traseros) titilan bajando al **20 %**, con las **reglas de la spec `danos`** (proporción, intervalos y duración) sobre esas 14 luces. Las toberas **no** titilan.
- **Humo** desde `Smoke`, en espacio de mundo (se queda atrás al avanzar):

| | Nivel 2 | Nivel 3 |
|---|---|---|
| Partículas por segundo | 7 | 20 |
| Color | `#4A4A4A` | `#262626` |

  Común a los dos niveles:
  - vida **1,6 s**;
  - velocidad hacia arriba 0,7–1,1 m/s y ±0,2 de lado;
  - tamaño de 0,35 a 1,95 m (más grande cuanto más rápido, como el prototipo);
  - opacidad de 0,55 a 0;
  - textura de punto suave.

  La pausa lo congela y la recolocación lo borra (como las partículas de `efectos-velocidad`).

### 6.6 Brillo de levitación

- Un plano bajo la nave, sobre la pista, de **3,2 × 7,5 m**, centrado en z = 0,2.
- Mezcla aditiva, color (0,14, 0,36, 1,0) con caída radial `(1 − d²)²`, a **0,32 ± 0,06** (late con sen(3t)).
- Va a la altura de la pista bajo la nave (punto 6 del inventario) y sigue la inclinación de la pista, no la de la nave. **En el aire** se apaga según la altura (del todo a 3 m). Si saber la altura de la pista bajo la nave es complicado, se cuelga de la nave a su altura de vuelo y se avisa.
- Intensidad en el Inspector; a 0, apagado. Solo con el casco 3D.

## 7. Etapa 4 · Holograma del selector

- SELECT SHIP enseña el **casco 3D** de VECTOR con la pintura por defecto, en lugar del wireframe.
- **Es el mismo `ShipHull3D`**, con su cámara en el mundo del menú, **sin** daño, humo, llamas, estelas ni levitación.
- **Giro**: el de siempre (`HologramSpin`: tres cuartos de inicio, giro automático, arrastre en esfera, inercia y vuelta del giro automático). Sin cambios.
- **Luz**: la del casco en carrera (4.3), **fija respecto a la pantalla** (no gira con la nave), para que se vea el volumen al girarla. Reflejos del mismo panorama. Se ajusta a ojo para que quede bien sobre el fondo del selector.
- **Escala común**: se queda la de hoy (los **px por metro** que salen de los 480 px de VECTOR con 3,4 m), **fija**, para las tres naves. VECTOR se verá algo más estrecho (sus alas son más cortas), BLADE y HAMMER igual que ahora.
  - La prueba de la spec `naves-4-integracion` cambia: ahora comprueba que la **esfera** de cada nave no pasa de **10 m** (la del VECTOR de antes, que es lo que cabe en el encuadre).
  - Con las medidas nuevas, la de VECTOR baja a unos **9,1 m** y la más grande pasa a ser BLADE (9,4 m).
- **Nada de efecto «aristas lejanas más tenues»** en el casco 3D: es para el wireframe.
- BLADE y HAMMER siguen con su holograma wireframe. Que convivan los dos estilos es **temporal**, hasta sus specs de casco.
- La nave del fondo del **menú principal** no cambia (fuera de alcance).

## 8. Parámetros en el Inspector

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Intensidad de cada luz del casco (principal, contraluz, relleno, ambiente) | Los ajustados en la etapa 2 | Casco 3D |
| Intensidad de las luces de perfil / morro / traseras / derivas / toberas | Tabla 6.4 | Casco 3D |
| Achatado de la boca (ancho × alto) | 1,2 × 0,55 | Perfil visual de VECTOR |
| Ancho de colisión de VECTOR | 2,63 m | Perfil técnico (del manifiesto) |
| Humo (partículas por segundo, vida) | 7 / 20 · 1,6 s | Casco 3D |
| Brillo de levitación | 0,32 ± 0,06 | Casco 3D |
| Pintura por defecto | `graphite_red`, lisa | Casco 3D |

Tiempos y colores fijos como **constantes con nombre**.

## 9. Pruebas

### Pruebas unitarias mínimas

1. **Manifiesto**: tiene todas las piezas y vacíos de la tabla 3.8 con las cantidades exactas. Los planos de las calcomanías de las góndolas miden lo de la tabla (±1 mm). Los vacíos están donde dice la tabla (±1 mm).
2. **Medidas** del manifiesto, sin librea:
   - largo **7,31 m**;
   - semiancho **2,634 m**;
   - alto **1,016 m**;
   - caja en z −4,00 → 3,31 y en y −0,31 → 0,706.

   Tolerancia ±0,01 m.
3. **Perfil visual**:
   - VECTOR → casco 3D, con su achatado de boca;
   - BLADE y HAMMER → wireframe;
   - el fantasma de VECTOR → wireframe;
   - toberas y estelas del perfil = vacíos del manifiesto.
4. **Colisión**: el ancho de colisión de cada nave = su semiancho real (VECTOR: el del manifiesto; BLADE y HAMMER: el de su wireframe), ±0,01 m.
5. **Catálogo**:
   - 20 pinturas con `id` únicos, cada una con su librea completa;
   - los 7 acabados de la tabla;
   - las 40 combinaciones en el orden de la sección 6.1 (con vuelta adelante y atrás);
   - la pintura por defecto es `graphite_red` lisa.
6. **Aplicación de pintura**:
   - qué piezas llevan el acento y cuáles quedan fijas (traseras, toberas);
   - con librea se ven `Livery_*` y `Decal_*` y las derivas toman el color de las franjas;
   - sin librea, no;
   - color del texto:
     - BLANCO PERLA → `#111111`;
     - GRAFITO → `#F2F2F2`;
     - AMARILLO TAXI (232 + 180 + 20 = 432) → `#111111`;
     - VERDE INGLÉS → `#F2F2F2`.
7. **Calcomanías**: con GRAFITO y librea, el dorsal es `07` con fondo `#F2F2F2` y número `#111111`; las pegatinas están centradas (el centro del texto en el centro del lienzo).
8. **Daño visible**:
   - nivel → máscara (0: ninguna; 1–3: la suya);
   - el parpadeo solo elige entre las 14 luces `Accent_*` / `Light_*`;
   - humo 0 / 0 / 7 / 20 por segundo;
   - en TIME TRIAL, nada.
9. **Holograma**: la escala es la fija; las esferas de VECTOR, BLADE y HAMMER no pasan de 10 m.

Las pruebas existentes siguen pasando (también la captura del wireframe de VECTOR, que se queda).

### Comprobación del script de Blender

- El script falla si falta una pieza del contrato y comprueba los puntos de la sección 3.3.
- Dos ejecuciones seguidas dan el **mismo** `.glb`.
- Los `.glb` de las tres cabinas **no cambian** después de tocar `lib/`.
- **Texturas**: una prueba compara unos cuantos píxeles de cada textura y de una máscara de quemaduras generadas en Python con los mismos píxeles generados por el prototipo con Node, como en las cabinas.

## 10. Prueba manual

**Etapa 1 · Blender**
1. Ejecutar el script: termina sin errores y deja el `.glb`, el manifiesto, las tres máscaras, el `.blend` y las cinco capturas.
2. Las capturas al lado del prototipo (misma vista, GRAFITO lisa), **la misma nave**:
   - morro afilado;
   - cúpula baja con su junta;
   - lomo de carbono cortado por la cúpula;
   - alas cortas con derivas bajas;
   - góndolas octogonales con la toma de aire levantada, rejilla y toberas planas con flaps;
   - filetes rojos.
3. `hull_vector_livery.png`: todas las flechas de prueba hacia arriba y ninguna en espejo.
4. Abrir el `.blend` y dar una vuelta:
   - sin piezas sueltas, agujeros ni caras negras;
   - **sin hueco** entre las alas y el fuselaje;
   - la cúpula **asienta** en el lomo.
5. Ejecutar el script otra vez: mismo resultado. Los `.glb` de las cabinas no cambian.

**Etapa 2 · El casco en carrera**
6. RACE con VECTOR, cámara exterior (C):
   - se ve el casco 3D, con luz, reflejos y sus filetes;
   - la pista, la ciudad y los efectos se ven **igual que antes**.
7. Acelerar y boost:
   - la llama, el núcleo y el halo salen de las bocas, **achatados**;
   - las estelas salen de las puntas de las alas nuevas.
8. Curvas: el casco se inclina y el derrape se ve como antes; la luz gira con la nave.
9. Rozar la barrera: toca donde se ve la punta de la deriva (unos 2,63 m del centro).
10. C a cabina:
    - la cabina 3D como siempre;
    - **nada** del casco exterior a través del cristal.
11. TIME TRIAL: el fantasma sigue siendo el wireframe de VECTOR. Naves de prueba (F7): wireframe magenta.
12. F10 → BLADE y HAMMER: su wireframe de siempre.
13. Rendimiento: los FPS en cámara exterior no bajan de forma notable (apuntar antes y después).
14. Informe del simulador de equilibrio (4.6).

**Etapa 3 · Pinturas, libreas y daño**
15. F6 recorre las 40 combinaciones:
    - cambian la pintura, el acabado y el color de acento;
    - el lomo, las alas y las toberas no cambian;
    - los pilotos traseros siempre rojos.
16. Con librea:
    - franjas a los lados del lomo, **sin verse a través de la cúpula**;
    - dorsal en la nariz y en las dos góndolas, más pequeños que en la primera versión del prototipo;
    - NULLDRIVE en los flancos, **igual en los dos lados**, y VECTORY en las góndolas;
    - banda y filete en las alas, derivas del color de las franjas y damero en las góndolas;
    - todo legible y nada en espejo.
17. BLANCO PERLA, CAMALEÓN y la cúpula: cambio de color con el ángulo, parecido al prototipo.
18. Dañar hasta nivel 3:
    - quemaduras que crecen con el nivel;
    - las luces titilan;
    - humo desde la góndola derecha con nivel 2 y 3, que se queda atrás al avanzar.
19. TIME TRIAL: sin quemaduras, sin parpadeo y sin humo.
20. Brillo de levitación azul bajo la nave, sobre la pista. En una cresta, al saltar, se apaga.
21. Pausa con humo: congelado. Recolocación: sin humo.

**Etapa 4 · Holograma**
22. SELECT SHIP con VECTOR: el casco 3D girando, con luz y reflejos; arrastrar en esfera, inercia y vuelta del giro automático igual que antes.
23. BLADE y HAMMER: su holograma wireframe, con la misma escala que antes.
24. VECTOR se ve algo más estrecho que antes y cabe en su sitio en cualquier orientación.
25. Menú principal: la versión es 0.14.7.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Blender** | Inventario (sección 2) y sección 3 completa | Comprobación del script · Prueba manual 1–5 · Commit |
| **2 · Casco en carrera** | Secciones 4 y 5 (pintura fija GRAFITO) | Pruebas 1–4 · Prueba manual 6–14 · Commit |
| **3 · Pinturas, libreas y daño** | Sección 6 | Pruebas 5–8 · Prueba manual 15–21 · Commit |
| **4 · Holograma** | Sección 7 · versión 0.14.7 | Prueba 9 · Prueba manual 22–25 · Commit |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas y el `.blend`.

## 12. Fuera de alcance

- Cascos 3D de BLADE y HAMMER (specs 2 y 3 de cascos).
- **Selector de color** en el juego, guardar la pintura elegida y cambiar el número del dorsal desde un menú.
- Pinturas para BLADE y HAMMER.
- Casco 3D en la **nave del fondo del menú principal**, en el **fantasma** y en las **naves de prueba** del radar.
- Rivales con IA (el casco queda preparado: varias instancias con pinturas distintas).
- Sombra del casco sobre la pista.
- Variantes 1 · FÓRMULA y 2 · RESISTENCIA del prototipo.
- LOD y versión ligera para Android.
- Animar piezas (flaps de las toberas, ventilador) o sonido.
- Cambios en los efectos de velocidad más allá de colocarlos en las bocas y achatar la llama.
- Menú SETTINGS.
