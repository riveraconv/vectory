# VECTORY — Spec: Efectos 8 · Luces de la nave

Versión 1.2 · 2026-11-20 · Estado: **definido, listo para implementar**

Tercera spec de la **segunda tanda de efectos de nave**. Deja listas **todas las luces de las tres naves**:

- **Pilotos y bandas realzados**, con **halo** en las luces puntuales.
- **Freno**: los pilotos se encienden fuerte al frenar y pasan a **naranja con boost**.
  - BLADE gana una **luz de freno nueva** debajo de la de cola, en forma de «=».
  - HAMMER gana una **luz de freno central** entre las dos toberas.
  - Las luces de freno **iluminan la pista y la popa de su nave**.
- **Luz en pista**: las luces laterales, las traseras y las toberas **tiñen el suelo** de la pista y del túnel, con su reflejo.
- **Faros**: cada nave lleva **faros delanteros diseñados** (VISERA en VECTOR, OJOS en BLADE, DEFENSA en HAMMER) con su haz por delante. En HAMMER, además, los **4 focos de trabajo** iluminan de verdad.
- **Toberas de retro**: las toberas del retroempuje de `efectos-6` pasan a ser **piezas del casco**, con su boca luminosa.
- **Sombra de contacto** bajo la nave, que crece y se desvanece en el aire.
- **Reacción al giro**: al girar fuerte o derrapar suben las luces del lado de dentro.
- **Parpadeo con daño nuevo**, mucho más visible: apagones, tartamudeo, chispas y, en el nivel 3, luces muertas.

Todo vale para **tu nave y para los rivales**.

Es **solo visual**: no cambia la conducción, el boost, el daño ni la física.

Plan de la segunda tanda (se irá completando sobre la marcha):

6. Toberas (hecha).
7. Estelas (hecha).
8. **Luces de la nave** (esta).
9. Visión túnel por cámara.

Maquetas de referencia:

- **`designs/efectos-luces.html`** («VECTORY · Luces de la nave · Maqueta»). Se decidió **todo en NUEVO** (botón TODO NUEVO):
  - PILOTOS Y BANDAS · REALZADOS;
  - FRENO Y BOOST · SÍ;
  - LUZ EN PISTA · SÍ;
  - FAROS Y FOCOS · SÍ;
  - SOMBRA · CONTACTO;
  - REACCIÓN AL GIRO · SÍ;
  - PARPADEO CON DAÑO · NUEVO.
- **`designs/faros-retro-vector.html`**, **`designs/faros-retro-blade.html`** y **`designs/faros-retro-hammer.html`** («VECTORY · Faros delanteros y retro · Maqueta»), sobre los cascos 3D reales. Se eligió:
  - VECTOR · **C · VISERA**;
  - BLADE · **C · OJOS**;
  - HAMMER · **A · DEFENSA**.

  En el proyecto, los enlaces entre las tres páginas no funcionan (los archivos tienen otro nombre). Se abren por separado.

Los valores de esta spec son los de las maquetas. Si algo no cuadra entre esta spec y una maqueta, **manda la maqueta** y se avisa.

Lo que **no** es parte de esta spec:
- en `efectos-luces`: la pista, el túnel, las naves (siluetas aproximadas), la llama, la conducción del piloto automático, el rival y la cámara;
- en `faros-retro-*`: la pista, la ciudad, el cambio NOCHE (baja el sol para ver los faros) y la pista algo más clara que lleva la maqueta para que se lea la mancha de luz;
- el bloom de las maquetas, que hace el papel del **glow** de Godot.

Las **posiciones** de las luces son las de los cascos 3D (sus manifiestos), no las de `efectos-luces`. Las de los faros y el retro son las de `faros-retro-*`, que ya usan los cascos 3D reales.

**Ojo con las unidades de las luces**:
- Las intensidades de las luces que pintan la pista y de los faros están en las unidades de three.js (candelas con caída 2), no en las de Godot.
- Valen como **proporción** entre ellas.
- La intensidad final se ajusta **a ojo** comparando con la maqueta en la misma situación (túnel, frenando, boost), y se apuntan los valores finales.

Se apoya en lo que ya existe:
- La spec **`cabinas-3d-4-morros`**, que va **antes**: las cabinas 3D llevan dentro una **copia del morro del casco** (su grupo `Nose`), sacada de los scripts de los cascos.
- Los **cascos 3D** de VECTOR, BLADE y HAMMER:
  - sus piezas vivas (`Accent_*`, `Light_*`, `Beacon_*`);
  - su **capa visual propia** y las luces del casco con `light_cull_mask`;
  - el **brillo de levitación**;
  - el **catálogo de pinturas** (color de acento de VECTOR).
- El **daño** (`danos`) y el parpadeo de luces que hoy hace cada casco (sección 6.5 de cada spec de casco).
- La **anticolisión** de BLADE y las **balizas del techo** de HAMMER, que no cambian.
- La **conducción**: frenar, girar, derrape y boost (`conduccion-avanzada`, `boost`).
- El **estado de la tobera** de `efectos-6-toberas`: potencia `P`, boost de la llama `bt` y fuerza del retro `R`.
- La **altura sobre la pista** que usa el brillo de levitación (`pista-3d`).
- El **suelo de pista** y los **túneles** (`elementos-circuito`).
- El **glow** del `WorldEnvironment`, los ajustes **BLOOM** y **PARTICLES** de `settings`, la pausa, la recolocación, el fantasma y la cámara activa.

Cambios respecto a specs anteriores:
- El **parpadeo de luces con daño** de las specs `cascos-3d-1-vector`, `cascos-3d-2-blade` y `cascos-3d-3-hammer` (bajar al 20 %) **se sustituye** por el de la sección 9.
  - Las cabinas no cambian: siguen con su parpadeo.
- La **sombra en la pista**, que esas specs dejaban fuera de alcance, entra aquí como sombra de contacto (sección 8).
- Piezas nuevas en Blender (sección 3). Hay que regenerar los `.glb` de los **tres** cascos:
  - BLADE gana **`Light_Brake`**;
  - HAMMER gana **`Light_Brake`** con su soporte;
  - las tres ganan **faros** (`Headlight_*`, `Light_Head_*`) y **toberas de retro** (`Retro_*`).
- **`efectos-6-toberas`, sección 6 (retroempuje)** cambia:
  - los chorros salen de los vacíos **`Retro_L_Point` / `Retro_R_Point`** de las toberas nuevas, no de los puntos del morro del inventario (punto 6 de `efectos-6`);
  - cada nave escala el chorro a su boca (sección 3.4);
  - HAMMER pasa a retro **naranja**;
  - la boca de las toberas nuevas se ilumina con `R`.

  Si `efectos-6` ya está hecha, el cambio entra en la etapa 3 de esta spec. Si no, entra directamente en la suya.
- **Cabinas** (`cabinas-3d-4-morros`): como su morro se copia del casco, al añadir faros y toberas de retro a los cascos hay que **volver a ejecutar los tres scripts de cabina** para que su copia del morro los lleve (sección 3.5).

**Cambios de la versión 1.1** (sobre la 1.0):
- Faros: los de la 1.0 (lentes provisionales) se sustituyen por los diseños elegidos (secciones 3.2 y 7.4).
- Toberas de retro como piezas del casco (secciones 3.4 y 7.5).
- Luz de freno central de HAMMER (sección 3.1).
- Luz de freno sobre el casco (sección 6.3).

**Cambios de la versión 1.2** (repaso contra el juego, antes de empezar; decisiones del usuario):
- **Numeración**: esta spec es la 8 (`efectos-8-luces`). Las toberas son `efectos-6-toberas` (el 5 es `efectos-5-danos-casco`), las estelas `efectos-7-estelas` y la visión túnel será la 9. Las maquetas están en `designs/`.
- El **retroempuje de `efectos-6` ya está hecho** (versión 0.26.0, chorros en los puntos `ThrusterShape.Retro` del perfil visual): su cambio entra en la etapa 3.
- **Suelo de pista y túnel fuera de esta spec** (sección 7.2): no se tocan sus materiales; lo harán otras specs de circuitos. La luz en pista y los faros se dejan **preparados**: creados, con sus valores y su `light_cull_mask` hacia las capas de pista y túnel, para que iluminen el suelo cuando lo reciba. Mientras tanto no se ven en la pista; el haz visible de los faros sí. Sus intensidades finales se ajustarán a ojo en esa spec.
- **Sombra de contacto aplazada** (sección 8): la pista y el túnel son solo líneas y el brillo de levitación ya no existe (se quitó al empezar `efectos-2-derrape`), así que la sombra no tendría sobre qué verse. Se hace en la spec del suelo, junto con la luz en pista. Las **siluetas** de la sección 3.3 sí se sacan en la etapa 1, para dejarlas listas.
- **Capa de la luz en pista**: como no hay suelo ni paredes de túnel que reciban luz, la luz en pista y los faros apuntan a una **capa propia libre** (la 2), que hoy no usa nada. El suelo de la spec futura irá en esa capa.
- **Retro a 45°** (secciones 3.4 y 7.5): los chorros salen **inclinados 45° hacia arriba** (en vez de casi horizontales), en el casco y en la cabina, para que cuadre lo que se ve por fuera con lo de dentro: desde la cabina se ven unos 3/4 del chorro por encima de la copia del morro. Los `Retro_*_Point` de las tres naves apuntan a (0; 1; −1). Los cuerpos de las toberas no cambian; si con las capturas de la etapa 1 la boca no cuadra con el chorro, se avisa y se propone el ajuste. En la vista de cabina de tu nave, además, **más discretos**. Ángulo e intensidad, a ojo jugando.
- **Trampilla de BLADE en cabina** (secciones 7.5 y 10): la trampilla de la copia del morro se abre igual que la del casco.

Rama: `feature/fx-lights`, que sale de `develop`. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

---

## 1. Resumen

- Cada pieza viva del casco tiene un **tipo** (sección 4.1). El tipo decide su realce, su halo, si hace de freno y si titila con el daño.
- La intensidad final de cada luz sale de multiplicar, en este orden:
  1. la **base** (la de hoy, del manifiesto);
  2. el **realce** (sección 5);
  3. el **freno y el boost** (sección 6);
  4. el **giro** (sección 7.1);
  5. el **daño** (sección 9).
- **Luz en pista**: 4 luces por nave (2 laterales, 1 trasera, 1 de toberas) que **solo iluminan la pista y el túnel**. Las sombras van apagadas.
- **Luz de freno en el casco**: 1 luz por nave que **solo ilumina el casco**, detrás de las luces de freno.
- **Faros**: `SpotLight3D` que también iluminan **solo la pista y el túnel**, con su **haz visible**.
- **Sombra de contacto**: un plano oscuro con la silueta de la nave, como el del brillo de levitación.
- Etapas:
  - **0** · Inventario.
  - **1** · Blender: piezas nuevas y siluetas.
  - **2** · Pilotos, halos, freno y giro.
  - **3** · Luz en pista, faros y retro (la sombra, aplazada: v1.2).
  - **4** · Daño nuevo.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. **Piezas vivas** de los tres cascos:
   - cómo las encuentra hoy `ShipHull3D` (manifiesto, nombres);
   - cómo les pone la intensidad;
   - dónde vive el **parpadeo con daño** de hoy;
   - si una pieza se puede marcar con un **tipo** (sección 4.1) desde el manifiesto o desde el perfil visual.
2. **Suelo de pista y túnel**: qué material llevan y si **reciben luz**.
   - Si son *unshaded*, una luz no los cambia.
   - Hay que decir cómo hacer que reciban luz **sin cambiar cómo se ven hoy** sin luces (sección 7.2).
   - Qué **capas visuales** llevan.
3. **Capas visuales libres**, para separar:
   - lo que ilumina la luz del casco (solo el casco, como hoy);
   - lo que ilumina la luz de pista y de los faros (solo pista y túnel).

   Si hay otras luces en la escena, cuáles son y a qué capas llegan. Si la capa del casco es **la misma para todas las naves**, se apunta (la luz de freno en el casco tocaría también a un rival pegado detrás, y no pasa nada).
4. **Freno**: cómo se lee, para cada nave (tú y los rivales), si está **frenando**. También cómo se leen el **giro** (entrada suavizada), el **derrape** y la velocidad.
5. El **`bt`**, la **`P`** y la **`R`** del estado de la tobera (`efectos-6`), por nave. Si el retroempuje de `efectos-6` ya está hecho: de dónde salen hoy sus chorros y cómo se escalan.
6. **Altura sobre la pista** y orientación de la pista bajo la nave, las que usa el brillo de levitación.
7. **Chispas**: si se pueden reutilizar las de `efectos-1-choques` para las del daño (sección 9.3).
8. **Rendimiento**:
   - cuántas `OmniLight3D` y `SpotLight3D` aguanta el renderizador que se usa (Forward+ o Mobile) con 8 naves;
   - cómo se reparten por objeto.

   Con esta versión, cada nave lleva **5 omni** (4 de pista y 1 de freno en el casco) y **2 focos** (VECTOR y BLADE) o **4 focos** (HAMMER), más las luces de las toberas de `efectos-6`.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Blender: piezas nuevas y siluetas

Coordenadas del casco: metros, morro hacia −Z, X a la derecha, Y arriba, origen el del casco (como en los scripts). Las medidas son las de las maquetas `faros-retro-*`. El agente las coloca **sobre la superficie real** del casco, y **enseña capturas**. Si algo choca con otra pieza o no apoya bien, se avisa y se propone el ajuste.

Materiales nuevos (uno compartido por casco salvo que se diga otra cosa):

| Material | Qué es | Valores |
|---|---|---|
| `M_Chrome` | Aros y reflectores sin luz | Metal 1, rugosidad 0,10, `#DFE4EA` |
| `M_LampHousing` | Carcasas, soportes y marcos oscuros | Metal 0,6, rugosidad 0,45, `#15171B` |
| `M_LampGlass` | Tapa de cristal de los faros | Transparente, opacidad 0,16, rugosidad 0,03, *clearcoat* 1 |
| `M_Reflector` | Reflector del faro, que brilla un poco con el faro encendido | Metal 1, rugosidad 0,14, `#CFD6DE`, emisión `#FFF4E6` × 0,35. **Instancia por faro** (es pieza viva, tipo `head`) |
| `M_Emissive_Head` | Lentes y bandas de los faros | `#FFF4E6` (BLADE `#F4F7FF`), intensidad de partida **6–7** (la de la maqueta). **Instancia por pieza** |
| `M_RetroTitanium` | Cuerpo de las toberas de retro | Metal 1, rugosidad 0,30, `#80858C` |
| `M_RetroInner` | Fondo de la boca del retro | `#040404`, rugosidad 0,95 |
| `M_Emissive_RetroGlow` | Boca luminosa del retro | Color **medio** del retro de su nave (sección 3.4). Intensidad 0,15 en reposo. **Instancia por pieza** |
| `M_Emissive_Brake` | Luz de freno nueva | `#FF2A1E`, intensidad de partida **2,4**. **Instancia por pieza** |

Todas las piezas que se encienden van con `noShadow`, como el resto de luces.

### 3.1 Luces de freno nuevas

**BLADE · freno en «=»**
- Pieza nueva **`Light_Brake`** en `hull_blade.py`, **justo debajo** de `Light_Tail`.
- Misma forma, mismo ancho y misma profundidad que `Light_Tail`.
- Separada de ella **lo mismo que su alto**, para que las dos formen un «=».
- En la maqueta de luces, cada barra mide 0,34 × 0,045 m: la de cola en y 0,10 y la de freno en y 0,02.
- Si debajo de `Light_Tail` no cabe en la tapa trasera, se coloca lo más cerca posible y **se avisa con capturas**.
- Material `M_Emissive_Brake`.

**HAMMER · freno central encima de las toberas**
- En la **tapa trasera del casco** (`Fuselage_Rear`, z 2,2), entre los dos motores y por debajo del lomo, queda un **hueco** libre encima de las toberas: los motores (radio 0,40 sobre (±0,45; 0,2)) dejan libre, a la altura y 0,55, una franja de unos ±0,27 m.
- Ahí va una barra **horizontal**, pegada a la tapa:
  - **`Brake_Mount`**: carcasa de 0,36 × 0,08 × 0,03 m, centrada en (0; 0,555; 2,215). Material `M_LampHousing`.
  - **`Light_Brake`**: barra de **0,30 × 0,05 m** en (0; 0,555; 2,2315), mirando hacia atrás. Material `M_Emissive_Brake`.
  - **`Brake_Frame`**: un marco de `M_Chrome` de 1 cm alrededor de la luz.
- Se ve desde detrás, entre los dos motores. No toca las tuberías de cobre que bajan del compartimento del motor a los motores (x ±0,32 a ±0,45). Si choca con algo o queda muy tapada, se ajusta la altura y **se avisa con capturas**.
- En reposo va casi apagada, como la de BLADE (sección 6.2).
- Maqueta: `designs/cabinas-morro-hammer.html`, vista TRASERA, frenando con S.

### 3.2 Faros

Cada faro es una **carcasa** con su cristal (piezas de Blender) y una **lente** viva (`Light_Head*`, tipo `head`). El foco (`SpotLight3D`) no va a Blender: solo se marca su origen con un vacío **`Spot_Head_L` / `Spot_Head_R`** en la lente (sección 7.4).

**VECTOR · VISERA**
- **`Light_Head`**: una **banda** que abraza la punta del morro de lado a lado, por encima y por los costados:
  - de z **−3,70 a −3,64**;
  - en la sección del casco, de θ −0,32 a π + 0,32 rad (θ = 0 la arista derecha, π/2 el lomo, π la izquierda): cubre todo el lomo y baja un poco por los costados;
  - **5 mm por fuera** de la piel.
  - Material `M_Emissive_Head`.
- **`Headlight_Frame_F`** y **`Headlight_Frame_R`**: dos bandas oscuras de 1,5 cm delante y detrás de la luz (z −3,715 a −3,70 y −3,64 a −3,625), **6 mm por fuera** de la piel, que la enmarcan. Material `M_LampHousing`.
- En ese tramo, el morro mide unos 0,18–0,21 m de semiancho y 0,13–0,15 m de alto sobre el origen.
- Vacíos de los focos: `Spot_Head_L/R` en (±0,10; 0,03; −3,74).
- Sustituye a las lentes `Light_Head_L/R` de la versión 1.0.

**BLADE · OJOS**
- Cuatro **rendijas** finas siguiendo el filo del morro, dos por lado:
  - **`Light_Head_L_Top` / `Light_Head_R_Top`**: un tubo de radio **7 mm** sobre la piel de arriba, 2 cm por dentro del filo, de z **−4,15 a −3,60**;
  - **`Light_Head_L_Bottom` / `Light_Head_R_Bottom`**: un tubo de radio **6 mm** sobre la piel de abajo, 2 cm por dentro del filo, de z **−4,00 a −3,70**.
  - 4 mm por fuera de la piel. Material `M_Emissive_Head` (`#F4F7FF`).
- En ese tramo, el morro mide de 0,29 a 0,39 m de semiancho, con el filo en y ≈ 0,03.
- Vacíos de los focos: `Spot_Head_L/R` en (±0,333; 0,027; −3,98), en el filo.
- Sustituye a la lente `Light_Head` de la versión 1.0.

**HAMMER · DEFENSA**
- Dos **faros redondos de camión** encima del tubo de arriba de la defensa (`BullBar_Top`, y 0,14), mirando hacia delante:
  - **`Headlight_L` / `Headlight_R`**, centrados en (±0,40; 0,255; −3,70), con la boca en z −3,70 y el cuerpo hacia atrás. Lente de radio **r = 0,085 m**:
    - `_Housing`: cilindro abierto de radio 1,14·r y 0,13 m de fondo, con su tapa trasera (`_Back`). `M_LampHousing`.
    - `_Bezel`: aro de radio 1,1·r y grosor 0,075·r en la boca. `M_Chrome`.
    - `_Reflector`: parábola de radio 1,04·r en la boca a 0,34·r a 0,72·r de fondo. `M_Reflector`.
    - **`Light_Head_L` / `Light_Head_R`**: bombilla, disco de radio 0,36·r al fondo del reflector (0,7·r). `M_Emissive_Head`.
    - `_Glass`: disco de radio 1,08·r, 4 mm por delante de la boca. `M_LampGlass`.
  - **`Headlight_L_Bracket` / `_R_Bracket`**: soporte de 0,03 × 0,09 × 0,03 m que lo une al tubo, en (±0,40; 0,17; −3,64). `M_LampHousing`.
- Los **4 focos de trabajo** del techo (`Light_Work_*`) siguen como están y hacen de **luces largas** (sección 7.4).
- Vacíos de los focos: `Spot_Head_L/R` en (±0,40; 0,255; −3,72).

### 3.3 Silueta para la sombra

Cada script de casco guarda además una **silueta vista desde arriba**: `hull_<nave>_shadow.png`.

- Cámara **ortográfica** cenital, que encuadra la planta del casco (sin la sonda de BLADE) con **1,2 m de margen** por cada lado.
- Imagen en gris (blanco = nave), de **256 px de ancho**. El alto va en proporción.
- Desenfoque gaussiano de unos **7 px** sobre 256 (como `blur(7px)` de la maqueta).
- En el manifiesto:
  - el tamaño en metros del plano (ancho × largo, con el margen);
  - su centro en x/z respecto al origen del casco.

### 3.4 Toberas de retro

Dos toberas por nave, **`Retro_L` / `Retro_R`**, con la boca mirando **hacia delante**. En cada una:
- las piezas del cuerpo (tabla);
- **`Retro_*_Inner`**: el fondo oscuro de la boca (`M_RetroInner`);
- **`Retro_*_Glow`**: la boca luminosa (`M_Emissive_RetroGlow`), justo delante del fondo;
- **`Retro_*_Point`**: un **vacío** en el centro de la boca, mirando hacia donde sale el chorro. De aquí sale el chorro de `efectos-6`.

Las coordenadas son las del grupo de cada tobera (su origen). Las de las piezas son relativas a él.

**VECTOR · tomas dorsales** (sobre el lomo del morro; los chorros pasan por encima del morro)
- Origen en (±0,28; piel de arriba + 0,022; −2,05). En ese punto la piel está a y ≈ 0,31.
- Inclinado −0,08 rad en x (la boca un poco más baja que la cola), siguiendo el lomo.
- `Retro_*_Scoop`: caja de 0,10 × 0,06 × 0,28 m, centrada en z +0,02. Con la **pintura del casco** (`M_Paint`).
- `Retro_*_Inner`: 0,075 × 0,04 m, en z −0,115.
- `Retro_*_Glow`: 0,072 × 0,036 m, en z −0,126.
- `Retro_*_Point`: en z −0,128, apuntando a (0; 1; −1) (45° hacia arriba, v1.2; antes (0; 0,1; −1)).
- Chorro: ancho **× 0,40**, alto **× 0,28** (boca aplastada).

**BLADE · ranuras con trampilla** (en el lomo, junto a la cúpula)
- Origen en (±0,35; piel de arriba; −2,30). En ese punto la piel está a y ≈ 0,09.
- `Retro_*_Slot`: hueco de 0,17 × 0,03 × 0,12 m, hundido 1,2 cm en la piel. `M_RetroInner`.
- `Retro_*_Glow`: 0,15 × 0,014 m, en el frente del hueco (z −0,061).
- **`Retro_*_FlapHinge`**: vacío de la bisagra, en el borde trasero del hueco (y +0,006; z +0,065), con el eje en x.
- **`Retro_*_Flap`**: trampilla de 0,19 × 0,012 × 0,135 m colgada de la bisagra hacia delante, con el material de las placas de blindaje de la variante ASALTO. Cerrada, queda plana sobre el hueco.
- `Retro_*_Point`: en z −0,062, apuntando a (0; 1; −1) (45° hacia arriba, v1.2; antes (0; 0,18; −1)).
- Chorro: ancho **× 0,42**, alto **× 0,15**.
- La trampilla **se abre con el retro** (sección 7.5).

**HAMMER · toberas laterales de acero** (a los lados del morro)
- Origen en (±0,64; 0,10; −2,85), por fuera de la piel del costado.
- `Retro_*_Barrel`: cilindro de radio 0,09 (atrás) a 0,085 (delante) y 0,40 m de largo, de z +0,20 a −0,20.
- `Retro_*_Lip`: aro de radio 0,09 y grosor 0,014 en la boca (z −0,20).
- `Retro_*_Clamp_0` / `_1`: abrazaderas de radio 0,093 y grosor 0,012 en z −0,08 y +0,10. `M_LampHousing`.
- `Retro_*_Mount`: caja de 0,10 × 0,04 × 0,24 m que la une al casco, desplazada 0,06 m hacia dentro. `M_LampHousing`.
- `Retro_*_Inner`: disco de radio 0,078 en z −0,19.
- `Retro_*_Glow`: disco de radio 0,065 en z −0,192.
- `Retro_*_Point`: en z −0,205, apuntando a (0; 1; −1) (45° hacia arriba, v1.2; antes hacia delante).
- Chorro: ancho y alto **× 0,72**.

**Colores del retro** (cambio en `efectos-6`, sección 6):

| | Núcleo | Medio | Fuera |
|---|---|---|---|
| VECTOR y BLADE | `#E6FAFF` | `#73CCFF` | `#245BFF` |
| HAMMER | `#FFD9A8` | `#FF8A3A` | `#FF5A14` |

Los de VECTOR y BLADE son los de `efectos-6`. HAMMER pasa a **naranja**, a juego con sus toberas calientes.

### 3.5 Contrato y comprobaciones

- El contrato de nombres de cada casco gana sus piezas nuevas:
  - VECTOR: `Light_Head`, `Headlight_Frame_F/R`, `Spot_Head_L/R`, `Retro_L/R` y sus piezas;
  - BLADE: `Light_Brake`, `Light_Head_L/R_Top`, `Light_Head_L/R_Bottom`, `Spot_Head_L/R`, `Retro_L/R` y sus piezas, `Retro_*_FlapHinge`, `Retro_*_Flap`;
  - HAMMER: `Brake_Mount`, `Light_Brake`, `Brake_Frame`, `Headlight_L/R` y sus piezas, `Light_Head_L/R`, `Spot_Head_L/R`, `Retro_L/R` y sus piezas.
- Las comprobaciones del script, las capturas y el manifiesto se actualizan.
- Los scripts de las **cabinas** se vuelven a ejecutar después de los de los cascos: su copia del morro (`cabinas-3d-4-morros`) gana los faros y las toberas de retro con **los mismos nombres**. Solo cambia el grupo `Nose` de cada cabina; el resto, igual.
- Dos ejecuciones seguidas dan el mismo resultado.

## 4. Luces de cada nave

### 4.1 Tipos

| Tipo | Piezas | Realce | Halo (m) | Freno | Titila con daño |
|---|---|---|---|---|---|
| `profile` | VECTOR: `Accent_Chine_*`, `Accent_WingLE_*`, `Accent_Endplate_*`, `Accent_PodBand_*` | × 2,4 | — | — | Sí |
| `nose` | VECTOR: `Light_Nose_*` | × 1,7 | 0,55 | — | Sí |
| `fin` | VECTOR: `Light_Fin_*` | × 1,7 | 0,60 | — | Sí |
| `brake` | VECTOR: `Light_Tail_*` · HAMMER: `Light_Tail_*`, `Light_TailBar_*`, `Light_Brake` · BLADE: `Light_Brake` | × 1,7 | 0,85 | **Sí** | Sí |
| `nav` | BLADE: `Light_Nav_*`, `Light_Tip_*` | × 1,7 | 0,65 | — | Sí |
| `form` | BLADE: `Accent_Form*` | × 2,8 | — | — | Sí |
| `tailw` | BLADE: `Light_Tail` (cola blanca) | × 1,5 | 0,75 | — | Sí |
| `amber` | HAMMER: `Light_Nose_*`, `Light_Canard_*` | × 1,7 | 0,50 | — | Sí |
| `work` | HAMMER: `Light_Work_*` | × 1,8 | 0,55 | — | Sí |
| `head` | VECTOR: `Light_Head` · BLADE: `Light_Head_*_Top`, `Light_Head_*_Bottom` · HAMMER: `Light_Head_L/R` · los `M_Reflector` | × 1 | 0,70 (solo HAMMER) | — | No (sección 9) |
| `retro` | `Retro_*_Glow` de las tres | Ver 7.5 | — | — | No |
| `strobe` | BLADE: `Light_Strobe_*` | × 1 | 1,30 | — | No |
| `beacon` | HAMMER: `Beacon_Boost_Lens`, `Beacon_Damage_Lens` | × 1 | 0,70 | — | No |

- Las **toberas** (`Nozzle_Glow_*`, `Nozzle_Core_*`) no son de esta spec: siguen con `efectos-6`.
- Las intensidades **base** son las de hoy (manifiestos y tabla 6.4 de cada casco). Las de las piezas nuevas, las de la sección 3.
- **Halo de los faros**: solo en los faros redondos de HAMMER. La visera de VECTOR y las rendijas de BLADE son **tiras**: no llevan halo (sección 5.2).
- `Light_Brake` de HAMMER lleva su halo en el centro de la barra, como un punto.
- **Lado** de cada pieza: izquierda si su x < 0, derecha si x > 0 y ninguno si está en el centro. Sirve para el giro (7.1) y para la luz en pista (7.3). La visera de VECTOR y la `Light_Brake` de BLADE y HAMMER, ninguno.
- Los tipos van en el **perfil visual** de cada nave o en su manifiesto, lo que el inventario diga que es más limpio. **No** se escriben en el código de `ShipHull3D`.

### 4.2 Anticolisión y balizas

- Siguen como hoy: anticolisión de BLADE (0,06 s cada 1,2 s, la de abajo desfasada 0,6 s) y balizas del techo de HAMMER (ámbar con boost, roja con daño 2 y 3).
- Ganan su **halo** (tabla 4.1).
- No las tocan ni el realce, ni el giro, ni el parpadeo con daño.

## 5. Pilotos y bandas realzados (etapa 2)

### 5.1 Intensidad

- Intensidad = base × **realce** del tipo (tabla 4.1).
- Se aplica a tu nave y a los rivales, en cámara exterior.
- Los **valores finales** se ajustan a ojo con el glow del juego hasta que se vea como la maqueta (REALZADOS frente a HOY), y se apuntan.
- Con el ajuste HOY de la maqueta, la diferencia con el juego de hoy debe ser parecida.

### 5.2 Halo

- Un **sprite** por luz puntual (los tipos con halo de la tabla 4.1):
  - mezcla aditiva, siempre de cara a la cámara;
  - lee la profundidad (las piezas del casco lo tapan) y no la escribe;
  - textura de punto suave.
- Va en la posición de la pieza, desplazado **5–6 cm hacia fuera** de su cara (hacia atrás en los pilotos traseros, hacia el lado en los laterales, hacia delante en los faros).
- **Color**: el de la luz en ese momento (también el naranja del boost).
- **Opacidad** = limitar(0,32 × `k` × `I`; 0; 1,4), con `I` la intensidad final de la luz (sección 1).
  - `k` = **0,42** de partida: es la escala de la maqueta.
  - Se ajusta a ojo y se apunta.
- **Tamaño** = halo × (0,75 + 0,25 × mín(2; `I` / `I`realzada)). Al frenar o con boost crece hasta × 1,25.
- Si `I` < 0,05, el halo no se dibuja.
- Las **tiras** (`profile`, `form` y las de los faros de VECTOR y BLADE) no llevan halo: solo suben de intensidad y el glow hace el resto.
- El ajuste **BLOOM** de SETTINGS multiplica también la opacidad de los halos. A 0 % no hay halos.

## 6. Freno y boost (etapa 2)

### 6.1 Estado por nave

- **Freno** `kB` (0–1): objetivo 1 si la nave está **frenando** con velocidad > 1 m/s; si no, 0.
  - τ **0,04 s** al subir y **0,15 s** al bajar.
  - Rivales: su orden de frenar.
- **Boost** `bt`: el boost de la llama de `efectos-6`.

### 6.2 Luces de freno (tipo `brake`)

- Intensidad × (1 + **2,2** × `kB`): a tope, × 3,2.
- Con boost:
  - color = mezcla(rojo de la pieza; **naranja `#FF8A1E`**; `bt`);
  - intensidad × (1 + **1,4** × `bt`): a tope, × 2,4.
- Frenar cancela el boost, así que no se pisan.
- **`Light_Brake` de BLADE y de HAMMER** (las nuevas): en reposo van **casi apagadas**, × (0,35 + 0,65 × máx(`kB`; `bt`)).
  - En BLADE, la cola blanca de arriba no cambia con el freno.
  - En HAMMER, sus pilotos de siempre (`Light_Tail_*`, `Light_TailBar_*`) van como los de VECTOR.
- **VECTOR y los pilotos de siempre de HAMMER**: en reposo, con la intensidad realzada de siempre.
- Las demás luces **no** cambian con el freno.

### 6.3 Luz de freno sobre el casco (etapa 3)

Las luces de freno **iluminan su nave**: tiñen de rojo (o de naranja con boost) la popa, las toberas y las aletas al frenar. La **pista** la ilumina la luz trasera de la sección 7.3, que va con el mismo freno.

- Una `OmniLight3D` por nave, **sin sombras**, solo en la **capa del casco** (`light_cull_mask`, como la luz de las toberas de `efectos-6`).
- Sitio, **0,35 m por detrás** de:
  - VECTOR, el punto medio de `Light_Tail_L/R`;
  - BLADE, `Light_Brake`;
  - HAMMER, `Light_Brake`.
- Rango **2,5 m**, atenuación **1,6**.
- **Color**: el rojo del freno, que pasa a `#FF8A1E` con `bt` (como 6.2).
- **Energía** = `EB` × (0,15 + 1,0 × `kB` + 0,6 × `bt`) × daño medio de las luces `brake` de la nave (9.4).
  - En reposo casi no se nota. Al frenar, la popa se tiñe.
  - `EB` se ajusta a ojo (empezar en 1), y se apunta. Que no queme el casco.
- En la **vista de cabina** de tu nave no se ve (el casco está oculto).

## 7. Giro, luz en pista, faros y retro

### 7.1 Reacción al giro (etapa 2)

- **Carga del giro** `G` por nave = objetivo mín(1; |giro| × mín(1; 1,3 × `th`) + 0,5 si derrapa), τ **0,15 s**.
  - giro = la entrada de giro suavizada (τ 0,12 s, la que ya inclina la nave), de −1 a 1;
  - `th` = velocidad / punta base.
- **Lado de dentro**: el del sentido del giro (girando a la izquierda, el izquierdo).
- Las luces del lado de dentro: intensidad × (1 + **0,7** × `G`).
- La luz en pista lateral del lado de dentro: × (1 + **1,5** × `G`).

### 7.2 Suelo de pista y túnel que reciben luz (etapa 3)

> **v1.2: fuera de esta spec.** El suelo y el túnel no se tocan aquí (otras specs de circuitos). Lo de abajo queda como referencia para entonces; en esta spec solo se dejan las luces preparadas con sus capas.

- El suelo de pista y las paredes y techo de los túneles tienen que **recibir la luz** de las naves.
- **Sin luces de nave**, se ven **igual que hoy**. En la maqueta:
  - el suelo lleva emisión con su color de siempre, albedo oscuro `#262E44`, rugosidad **0,42** y metal 0;
  - las paredes del túnel, emisión `#04060B`, albedo `#1C2130` y rugosidad 0,62.
- La rugosidad baja es la que da el **reflejo** alargado de las luces sobre el suelo.
- **Solo** las luces de esta sección (pista y faros) llegan al suelo y al túnel. La luz del casco sigue **solo en el casco**.
  - Se hace con capas visuales y `light_cull_mask` (punto 3 del inventario).
  - Si la luz de pista también ilumina un poco el casco, **no pasa nada**, pero hay que evitar que lo queme (en la maqueta, lo quemaba la luz de las toberas, de ahí su posición lejana).
- Las líneas de la pista, los postes y la ciudad **no** cambian.

### 7.3 Luz en pista (etapa 3)

Cuatro `OmniLight3D` por nave, colgadas de la nave:
- **sin sombras**;
- solo en las capas de pista y túnel;
- con alcance **16 m** las laterales y la trasera y **18 m** la de las toberas.

Posiciones (coordenadas de la nave; y negativa = debajo del casco, cerca del suelo):

| | Laterales (izq. / der.) | Trasera | Toberas |
|---|---|---|---|
| VECTOR | (±2,2, −0,30, 1,6) | (0, −0,30, 3,4) | (0, −0,35, 6,8) |
| BLADE | (±1,6, −0,30, 2,6) | (0, −0,30, 3,5) | (0, −0,35, 6,8) |
| HAMMER | (±2,4, −0,40, −2,2) | (0, −0,40, 2,9) | (0, −0,45, 6,4) |

Colores:
- **Laterales**:
  - VECTOR, el **color de acento** de su pintura;
  - BLADE, **rojo `#FF2A1E`** la izquierda y **verde `#3DFF6A`** la derecha;
  - HAMMER, **ámbar `#FF9A1A`**.
- **Trasera**: el rojo de sus pilotos, que pasa al naranja del boost con `bt` (como 6.2). Es el **reflejo del freno en la pista**.
- **Toberas**: el color medio de la llama de `efectos-6` (azul en VECTOR y BLADE, naranja en HAMMER), que pasa al de boost con `bt`.

Intensidad (unidades de la maqueta, ver la nota de unidades del principio):

| Luz | Intensidad |
|---|---|
| Laterales | 8 × 1,4 (realzado) × giro (7.1) × daño medio de su lado (9.4) |
| Trasera | 1,2 + 13 × `kB` + 9 × `bt` |
| Toberas | 1 + 3,5 × `P` + 8 × `bt` |

- Todas × **altura**: máx(0; 1 − `h` / 3,5), con `h` la altura sobre la pista. Las de pista se apagan del todo a 3,5 m.
- Con la nave en la parrilla, la de toberas × 0,4 (la llama va floja), como en la maqueta.
- En la **vista de cabina** de tu nave, la luz en pista **sí** se ve (el casco está oculto, pero el suelo se ilumina).

### 7.4 Faros (etapa 3)

**Focos**: `SpotLight3D` colgadas de la nave:
- **sin sombras**;
- solo en las capas de pista y túnel;
- alcance **70 m**, penumbra **0,55** de la maqueta.

**Dónde van y adónde miran.** Con el foco en la lente, casi toda la luz se queda en el primer metro de pista delante del morro (cae con la distancia al cuadrado) y por delante no se ve nada. Por eso, como en la maqueta:
- el foco va **0,9 m por encima y 0,8 m por detrás** de su vacío `Spot_Head_*`;
- apunta a un punto **de la pista** a **15 m por delante** del vacío, en su dirección en planta (tabla);
- así la mancha de luz cae lejos, por delante del morro, y se ve desde la cámara exterior y desde la cabina.

| | Focos | Semiángulo | Dirección en planta (x; z) | Intensidad (cada uno) |
|---|---|---|---|---|
| VECTOR | 2 · `Spot_Head_L/R` | 0,20 rad (≈ 11°) | (±0,07; −1) | 1320 |
| BLADE | 2 · `Spot_Head_L/R` | 0,19 rad (≈ 11°) | (±0,08; −1) | 1320 |
| HAMMER · faros | 2 · `Spot_Head_L/R` | 0,18 rad (≈ 10°) | (±0,04; −1) | 1800 |
| HAMMER · trabajo | 2 · cada par de `Light_Work_*` (x ±0,16) | 0,32 rad (≈ 18°) | (±0,12; −1), a 15 m | 840 |

- Las intensidades son las de la maqueta (unidades de three.js) y se ajustan a ojo (nota de unidades del principio).
- **Lentes y reflectores** con los faros: el tipo `head` (tabla 4.1). Los focos de HAMMER (`Light_Work_*`) × **1,6** sobre su realce.
- Intensidad × daño medio de todas las luces de la nave (9.4).
- En **cabina**, tus faros iluminan la pista por delante. Son los mismos.
- Si el inventario dice que 4 focos por HAMMER son demasiados con 8 naves, se avisa con los datos (propuesta: los de trabajo, solo en tu nave).

**Haz visible** (como en la maqueta):
- Un **cono** aditivo por foco, sin luz, que sale del vacío `Spot_Head_*` en la dirección de su foco (hacia el punto al que apunta), de **10 m** de largo:
  - radio 0,05 m en la lente y tan(semiángulo) × 0,75 × 10 m al final;
  - se desvanece a lo largo: (1 − `v`)^2,4, con `v` de 0 en la lente a 1 al final, y más por los bordes vistos de lado (el borde de la llama de `efectos-6`);
  - color del foco; opacidad de partida **0,07** (0,04 en los de trabajo), × daño medio, como los focos.
- **Se apaga** cuando la cámara está **dentro del cono**, mirando al faro de frente (si no, la pantalla se llena de blanco). Con `a` la distancia de la cámara a lo largo del eje y `d` su distancia al eje: × limitar((`d` − tan(semiángulo) × 0,75 × `a`) / (0,6 + 0,3 × `a`); 0; 1) si `a` > 0.
- En la **vista de cabina** de tu nave, × 0,5.
- Respeta **BLOOM**: a 0 %, × 0,5.

### 7.5 Toberas de retro (etapa 3)

- Los chorros del retroempuje de `efectos-6` (sección 6) salen de los vacíos **`Retro_*_Point`** y miran hacia donde apuntan.
- **Escala** del chorro (radios y largo de `efectos-6`): el ancho y el alto de cada nave (sección 3.4). El largo, × el ancho.
- **Colores**: los de la sección 3.4.
- **Boca** (`Retro_*_Glow`, tipo `retro`): emisión = 0,15 + **4** × `R`, con el color medio de su nave. No titila con el daño y no la tocan el realce ni el giro.
- **Luz del retro**: una `OmniLight3D` por tobera, 0,25 × su ancho por delante de la boca, del color medio, rango **3,5 m**, atenuación 2, energía `ER` × **2** × `R` (`ER` = 1, a ojo). Solo en la **capa del casco**. Ilumina el morro al frenar fuerte.
- **Trampilla de BLADE**: `Retro_*_FlapHinge` gira en x de 0 a **0,75 rad** (≈ 43°) con mín(1; 1,4 × `R`): se abre del todo antes de que el chorro llegue a tope y se cierra con él. La boca y el chorro se ven al abrirse.
- En la **vista de cabina** de tu nave, los chorros sí se ven por delante, **inclinados 45° hacia arriba**, como en el casco, y más discretos (v1.2). Las piezas del casco, no; las de la copia del morro, sí (también la trampilla de BLADE).
- Si el retroempuje de `efectos-6` ya está hecho con otros puntos del morro, se cambian a estos vacíos y se quitan los viejos.

## 8. Sombra de contacto (aplazada)

> **v1.2: aplazada a la spec del suelo.** No hay suelo ni brillo de levitación sobre los que se vea. Lo de abajo queda como referencia; en esta spec solo se sacan las siluetas (sección 3.3).

- Un **plano** como el del brillo de levitación:
  - a la altura de la pista bajo la nave (+2,5 cm), siguiendo su inclinación;
  - girado con el rumbo de la nave.
- Textura: la **silueta** de la sección 3.3, con su tamaño y su centro del manifiesto.
- Color negro. Mezcla normal con transparencia. Lee la profundidad y no la escribe.
- Se dibuja **después** del brillo de levitación, para que lo oscurezca debajo de la nave.
- Con `h` = altura sobre la pista (la del brillo de levitación):
  - **opacidad** = 0,62 × máx(0; 1 − `h`/5) × (1 − 0,15 × mín(1; `h`));
  - **escala** = 1 + 0,14 × `h`.
- Se ve sobre todo encima de la luz en pista y del brillo de levitación. Sobre el suelo oscuro casi no se nota, y es lo esperado.
- Para tu nave y los rivales. En la **vista de cabina**, la tuya no se dibuja.
- El fantasma, la nave del menú y el holograma **no** llevan sombra.

## 9. Parpadeo con daño nuevo (etapa 4)

Solo con daño 1–3 y solo en RACE (en TIME TRIAL, nada, como hoy). Cada nave con su nivel de daño. Afecta a las luces que titilan según la tabla 4.1.

Cada luz guarda su **factor de daño** `fd` (1 = normal). El azar sale de un generador **inyectado**.

### 9.1 Reglas por luz

| | Nivel 1 | Nivel 2 | Nivel 3 |
|---|---|---|---|
| Apagones por segundo (por luz) | 0,30 | 0,75 | 1,30 |
| Duración del apagón | 0,07–0,16 s | 0,10–0,32 s | 0,12–0,50 s |
| Zumbido de fondo | — | 0,88 + 0,12 · sen(41·t + 3·i) | Igual |
| Chispa al volver | — | Sí | Sí |
| Luces muertas | — | — | **30 %** |

- **Apagón**: cada frame, con probabilidad (apagones por segundo × Δt), la luz se apaga (`fd` = 0) durante la duración sorteada.
- **Al volver**: **tartamudea 0,16 s**, alternando 0,15 y 1,15 cada 0,035 s. En los niveles 2 y 3, suelta una **chispa** (9.3).
- **Zumbido**: con nivel 2 y 3, fuera de los apagones, `fd` = 0,88 + 0,12 × sen(41·t + 3·`i`), con `i` el número de la luz en su nave.
- **Muertas** (nivel 3): el 30 % de las luces de la nave, elegidas con un **hash fijo** del número de la luz y de la nave, siempre las mismas.
  - `fd` = 0.
  - Cada 1,2–3,2 s **reviven 0,07 s** a 1,3, con chispa.

### 9.2 Lo que no titila

- Anticolisión, balizas del techo, toberas, faros (`head`) y bocas del retro (`retro`): tabla 4.1.
- Los focos de HAMMER (`work`) **sí** titilan.

### 9.3 Chispas

- **3 chispas** por suceso, en la posición de la luz:
  - velocidad ±2 m/s de lado y adelante-atrás y 0,5–3 m/s hacia arriba, con gravedad de 9 m/s²;
  - vida 0,15–0,30 s;
  - tamaño 0,12 + 0,18 × (vida que queda / vida) m;
  - color `#FFB060`, aditivo, opacidad 1,6 × (vida que queda / vida).
- Se reutilizan las chispas de `efectos-1-choques` si se puede (punto 7 del inventario).
- Respetan **PARTICLES** (25 / 50 / 100 %).

### 9.4 Daño medio

- **Daño medio** = la media de mín(1; `fd`) de las luces que titilan, de un lado, de un tipo o de toda la nave.
- La luz lateral en pista usa la de su lado; la luz de freno en el casco, la de las luces `brake`; y los faros, la de toda la nave.
- Así, cuando las luces de un lado se apagan, su luz en la pista también.

## 10. Interacción con otros sistemas

- **Vista de cabina**:
  - tu casco está oculto, así que no se ven sus piezas, halos, sombra ni la luz de freno en el casco;
  - las lentes de los faros (`Light_Head*`, `M_Reflector`) y las bocas del retro (`Retro_*_Glow`) de la **copia del morro de la cabina** se encienden igual que las del casco, con el mismo estado (sin halo);
  - tu luz en pista, tus faros (con el haz a la mitad) y los chorros del retro **sí**;
  - los rivales, todo.
- **Fantasma**: nada de esta spec (sigue siendo wireframe).
- **Nave del menú y holograma del selector**:
  - las piezas con el **realce**, con los faros encendidos y las bocas del retro en reposo;
  - sin halos, luz en pista, luz de freno en el casco, focos, haz, sombra ni daño.
- **Atravesables** (recolocación de `rivales-3`): las luces, los halos y la sombra siguen la visibilidad del casco. La luz en pista, la luz de freno en el casco, los faros y su haz se apagan mientras la nave está atravesable.
- **Recolocación**: se borran los apagones, los tartamudeos y las chispas, y `kB` y `G` vuelven a 0 al momento. Las luces muertas siguen muertas (el daño no cambia). La trampilla de BLADE se cierra con `R` (que `efectos-6` pone a 0).
- **Pausa**: todo congelado (parpadeos, chispas, anticolisión, balizas y trampilla).
- **Terminada**: las luces siguen; al frenar sola la nave no frena, así que no se enciende el freno.
- **En el aire**: la luz en pista y la sombra bajan con la altura (7.3 y 8). Las piezas, los halos, los faros y la luz de freno en el casco, igual.
- **SETTINGS**:
  - BLOOM multiplica los halos (5.2) y el haz de los faros (7.4);
  - PARTICLES, las chispas (9.3);
  - nada más.

## 11. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de luces. Los colores, en `Palette`.

| Parámetro | Valor |
|---|---|
| Realce por tipo | Tabla 4.1 |
| Halo por tipo (m) / escala de opacidad `k` / desplazamiento | Tabla 4.1 / 0,42 / 5–6 cm |
| Freno: subida / bajada / × a tope | 0,04 s / 0,15 s / 3,2 |
| Boost en el freno: color / × a tope | `#FF8A1E` / 2,4 |
| `Light_Brake` de BLADE y HAMMER en reposo | 35 % |
| Luz de freno en el casco: distancia / rango / atenuación / energía | 0,35 m / 2,5 m / 1,6 / EB × (0,15 + kB + 0,6·bt) |
| Giro: τ / × luces de dentro / × luz en pista de dentro | 0,15 s / 1 + 0,7·G / 1 + 1,5·G |
| Luz en pista: posiciones y alcances | Tabla 7.3 · 16 / 16 / 18 m |
| Luz en pista: intensidades | Tabla 7.3 (a ojo, sección 7.3) |
| Luz en pista: apagado por altura | 3,5 m |
| Faros: subida y retroceso del foco / distancia al punto de la pista | 0,9 m y 0,8 m / 15 m |
| Faros: semiángulos, direcciones, alcance, penumbra, intensidades | Tabla 7.4 · 70 m · 0,55 |
| Haz visible: largo / opacidad / apagado dentro del cono | 10 m / 0,07 (trabajo 0,04) / sección 7.4 |
| Focos de trabajo de HAMMER | × 1,6 |
| Retro: escala por nave / colores | Sección 3.4 |
| Retro: boca / luz / trampilla | 0,15 + 4·R / ER × 2·R, 3,5 m / 0,75 rad con 1,4·R |
| Sombra: opacidad / se apaga a / crece | 0,62 / 5 m / 0,14 por m |
| Daño: apagones, duraciones, zumbido, muertas | Tabla 9.1 |
| Chispas: número, vida, color | 3 · 0,15–0,30 s · `#FFB060` |

## 12. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot:

- **Ajustes de luces** (datos): todo lo de la sección 12.
- **Tipos de luz** (datos): de cada nave, qué pieza es de qué tipo y de qué lado. Se leen del perfil visual o del manifiesto.
- **Entradas de luces** (datos), por nave: frenando, velocidad, `th`, giro suavizado, derrape, `P`, `bt`, `R`, altura sobre la pista, nivel de daño y modo (RACE / TIME TRIAL). Se rellenan desde el `Player` o el conductor del rival.
- **Estado de luces** (puro, uno por nave):
  - `Update(delta, entradas, generador)`: lleva `kB`, `G` y el daño de cada luz;
  - da la **intensidad y el color** de cada pieza, la opacidad y el tamaño de su halo, las intensidades y colores de la luz en pista, de la luz de freno en el casco y de los faros, la opacidad del haz (sin la parte de la cámara), la emisión de las bocas del retro y el ángulo de la trampilla, la opacidad y la escala de la sombra, y las **chispas** que hay que soltar;
  - `Reset()` para la recolocación.
- El **apagado del haz con la cámara dentro** depende de la cámara: es una función pura aparte (posición de la cámara, eje y semiángulo del foco → factor), que llama el nodo.
- **Nodo de Godot** (solo dibuja), colgado de `ShipHull3D` o a su lado:
  - pone las intensidades en los materiales;
  - mueve los sprites de halo;
  - pone las `OmniLight3D`, las `SpotLight3D`, los conos del haz y el plano de la sombra;
  - gira la trampilla de BLADE;
  - lanza las chispas.

  Sin reglas dentro.
- El **parpadeo con daño** que hoy hace cada casco se quita y pasa al estado de luces.
- La anticolisión de BLADE y las balizas de HAMMER siguen con su lógica. Solo ganan el halo.
- El chorro del retro sigue siendo de `efectos-6`. Esta spec solo le da sus puntos, su escala y sus colores, y lleva la boca, la luz y la trampilla.

## 13. Pruebas

### Pruebas unitarias mínimas

Pilotos, freno y giro (etapa 2):
1. **Realce**: cada tipo multiplica su base por lo de la tabla 4.1. Anticolisión y balizas no cambian.
2. **Freno**:
   - con `kB` = 1, las luces `brake` × 3,2 y el resto igual;
   - con `bt` = 1, color `#FF8A1E` y × 2,4;
   - en reposo, `Light_Brake` de BLADE y de HAMMER al 35 %; `Light_Tail` de BLADE y los pilotos de siempre de HAMMER sin cambios.
3. **Freno** sube a > 0,9 en 0,1 s y baja a < 0,1 en 0,4 s.
4. **Giro**: con giro = 1 a la punta y `G` = 1, las luces del lado izquierdo × 1,7, las del derecho y las del centro sin cambios.
5. **Halo**: con `I` < 0,05, sin halo. Las tiras (también la visera de VECTOR y las rendijas de BLADE) nunca tienen halo. Con el freno a tope, el halo crece a × 1,25.

Luz en pista, faros, retro y sombra (etapa 3):
6. **Luz en pista**:
   - trasera en reposo 1,2, frenando a tope 14,2 y con boost a tope 10,2 (unidades de la maqueta);
   - a 3,5 m de altura, todas a 0;
   - la lateral de dentro × (1 + 1,5·G).
7. **Luz de freno en el casco**: en reposo `EB` × 0,15, frenando a tope × 1,15 y con boost a tope × 0,75. Color naranja con `bt` = 1.
8. **Faros**:
   - VECTOR y BLADE con 2 focos y HAMMER con 4, con sus semiángulos de la tabla 7.4;
   - cada foco 0,9 m por encima y 0,8 m por detrás de su vacío, apuntando a la pista a 15 m;
   - haz: con la cámara en el eje, delante del faro, factor 0; detrás del faro o lejos del eje, 1.
9. **Retro**: con `R` = 0, la boca a 0,15 y la trampilla cerrada; con `R` = 1, la boca a 4,15 y la trampilla a 0,75 rad; con `R` = 0,5, la trampilla ya abierta del todo.
10. **Sombra** (aplazada, v1.2): con `h` = 0, opacidad 0,62 y escala 1; con `h` = 5, opacidad 0 y escala 1,7.
11. **Manifiestos**:
    - las piezas nuevas de la sección 3.5 en cada casco;
    - las tres con su silueta y su tamaño;
    - `Light_Brake` debajo de `Light_Tail` en BLADE y entre las toberas en HAMMER.

Daño (etapa 4):
12. **Daño 0**: `fd` = 1 siempre.
13. **Muertas**: con nivel 3, el 30 % (redondeado) de las luces que titilan, **siempre las mismas** en dos carreras. Anticolisión, balizas, toberas, faros y bocas del retro nunca.
14. **Apagones**: con un generador fijo, la duración cae en el rango de su nivel. Al volver hay tartamudeo de 0,16 s y, en nivel ≥ 2, una chispa.
15. **TIME TRIAL**: con cualquier nivel, `fd` = 1 y ninguna chispa.
16. **Pausa**: con `delta` 0, ni apagones, ni tartamudeo, ni cambio de `kB` ni `G`.
17. `Reset()`: sin apagones ni chispas, `kB` y `G` a 0.

Las pruebas existentes siguen pasando. Las del parpadeo con daño de los cascos (bajar al 20 %) se quitan o se adaptan. Si el retroempuje de `efectos-6` ya está hecho, sus pruebas siguen pasando con los puntos nuevos.

### Prueba manual

**Etapa 1 · Blender**
1. Capturas por detrás:
   - BLADE: el «=» con la cola blanca arriba y el freno debajo;
   - HAMMER: la barra horizontal de freno en la tapa trasera, encima de las toberas, entre los dos motores.
2. Capturas por delante y de cerca del morro, comparadas con la maqueta `faros-retro-*` en la vista MORRO:
   - VECTOR: la visera bien pegada a la punta del morro, con sus dos marcos, y las dos tomas dorsales;
   - BLADE: las cuatro rendijas siguiendo el filo y las dos ranuras con su trampilla cerrada;
   - HAMMER: los dos faros encima de la defensa con su soporte, y las dos toberas laterales.
3. Las tres siluetas: la forma de la nave en planta, desenfocada, con margen.

**Etapa 2 · Pilotos, halos, freno y giro**
4. RACE, cámara exterior, las tres naves (F10): pilotos y bandas **mucho más visibles** que antes, con halo en los puntuales, sin deslumbrar.
5. Frenar: los pilotos se encienden fuerte y su halo crece. Soltar: vuelven en ~0,15 s.
6. Boost: los pilotos pasan a naranja.
7. BLADE: el «=»; la de abajo casi apagada, roja al frenar y naranja con boost. HAMMER: la luz central, igual.
8. Curva cerrada y derrape: suben las luces del lado de dentro.
9. Ver frenar a un rival por delante: se le encienden los pilotos.
10. Comparar con la maqueta (REALZADOS, FRENO SÍ, GIRO SÍ) y apuntar los valores.

**Etapa 3 · Luz en pista, faros, retro y sombra**
11. Sin naves cerca, el suelo y el túnel **igual que antes**.
12. En el túnel:
    - las laterales tiñen el suelo con su color;
    - la trasera, roja al frenar y naranja con boost;
    - la de las toberas, detrás;
    - el reflejo alargado en el suelo.
13. Frenar fuerte en cámara exterior: la **popa** de tu nave se tiñe de rojo (las toberas, las aletas, la tapa trasera de HAMMER) y la pista detrás también. Con boost, de naranja.
14. Faros: la mancha de luz cae **por delante del morro**, no pegada a él, y se ve el haz. Más fuerte en el túnel. HAMMER, con sus faros y sus 4 focos, ilumina bastante más.
15. Ponerse delante de una nave parada, mirándola de frente: el haz no llena la pantalla.
16. Frenar fuerte: los chorros del retro salen de las toberas nuevas, con la boca encendida y la luz en el morro. En BLADE, la trampilla se abre y se cierra. En HAMMER, naranja.
17. El casco **no** se quema con la luz en pista, los faros, la luz de freno ni la del retro.
18. (Sombra aplazada, v1.2.) Sombra bajo la nave, que oscurece el brillo de levitación. En una cresta, al saltar, crece y se desvanece, y la luz en pista se apaga con la altura.
19. Cabina: tus faros, tu luz en pista y tus chorros del retro sí se ven; tu casco, tus halos, tu sombra y tu luz de freno en el casco no.
20. Comparar con las maquetas (LUZ EN PISTA, FAROS, SOMBRA en `efectos-luces`; MORRO, CABINA y RETRO en `faros-retro-*`) y apuntar los valores.
21. Mirar los fps con los 8 en la salida y con todos dentro de un túnel. Si bajan mucho, se avisa con los datos (propuesta: luz en pista y focos de trabajo solo para tu nave y los 4 rivales más cercanos).

**Etapa 4 · Daño**
22. Daño 1, 2 y 3: apagones cada vez más largos y frecuentes, tartamudeo al volver, chispas desde el 2 y, en el 3, luces muertas que reviven a ratos. Mucho más visible que antes en las tres naves.
23. Con daño en un lado, la luz en pista de ese lado parpadea con él.
24. Anticolisión, balizas, toberas, faros y bocas del retro no titilan.
25. TIME TRIAL: sin parpadeo.
26. Pausa: todo congelado. Recolocarse: sin chispas sueltas.
27. Comparar con la maqueta (DAÑO 1–3 NUEVO). Menú principal: la versión nueva.

## 14. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Blender** | Sección 3 | Prueba 11 · Prueba manual 1–3 · Commit `feat:` |
| **2 · Pilotos, halos, freno y giro** | Secciones 4, 5, 6.1, 6.2 y 7.1 | Pruebas 1–5 · Prueba manual 4–10 · Commit `feat:` |
| **3 · Luz en pista, faros y retro** | Secciones 6.3, 7.3–7.5 (7.2 y 8, aplazadas) | Pruebas 6–9 · Prueba manual 11–21 sin la 18 (lo que pide suelo se ve en la spec del suelo) · Commit `feat:` |
| **4 · Daño** | Secciones 9 y 10 · versión | Pruebas 12–17 · Prueba manual 22–27 · Commit `feat:` |

No se empieza una etapa sin validar la anterior. En la etapa 1, el usuario valida mirando las capturas.

## 15. Fuera de alcance

- **Iluminación del circuito** sobre las naves (luces de bordes, túneles y ciudad que tiñan el casco). Esta spec solo hace que el suelo y el túnel reciban la luz **de las naves**.
- Que las luces de una nave iluminen **a propósito** a las otras naves (si la capa del casco es compartida, puede pasar un poco, y vale).
- Sombras reales con sombras de luces.
- Que el color de las luces laterales de BLADE y HAMMER salga de la pintura.
- Intermitentes, luces de posición que cambian con la pintura, ráfagas de faros o luces que elija el jugador.
- Los diseños de faros y retro **no elegidos** de las maquetas `faros-retro-*`.
- La **visión túnel** por cámara (spec 9).
- Cambios en la llama de las toberas (`efectos-6`, salvo los puntos, la escala y los colores del retro), las estelas (`efectos-7`) o el humo de daños (`efectos-4`).
- Cabinas: sus luces y su parpadeo no cambian.
- Sonido y vibración del mando.
