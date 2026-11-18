# VECTORY — Spec: Efectos 6 · Toberas

Versión 1.1 · 2026-11-18 · Estado: **definido, listo para implementar**

Cambios de la 1.1 (repaso contra el juego, antes de empezar): `efectos-velocidad` no está implementada, así que esta spec va por su cuenta (no hay tobera vieja que quitar, ni partículas ni chispas del boost); número 6 (el 5 es `efectos-5-danos-casco`); radio y achatado de la llama por nave; `βmáx` de 22°; luz de los rivales sobre los demás rivales; color de la boca; bocas del wireframe; colores nuevos en `Palette`; "parada" a menos de 1 m/s.

Primera spec de la **segunda tanda de efectos de nave**: la que exprime Godot para que se vea espectacular. **Hace la llama de las toberas** con **shader**, de dos capas, que reacciona a **todo lo que hace el piloto**: acelerar, soltar, frenar, girar, derrapar y el boost. Añade **diamantes de choque**, **distorsión de calor**, **luz de las toberas sobre el casco**, **retroempuje** en el morro al frenar y el **tartamudeo** con daño 3.

Es **solo visual**: no cambia la conducción, el boost, el daño ni la física.

Plan de la segunda tanda (se irá completando sobre la marcha):

6. **Toberas** (esta).
7. Estelas.
8. Luces de la nave (freno, boost, luces del casco con la conducción).
9. Visión túnel por cámara.

Maqueta de referencia: **`designs/efectos-toberas.html`** («VECTORY · Toberas · Maqueta»). Se decidió con **LLAMA · NUEVA**, **DIAMANTES · SÍ**, **CALOR · SÍ**, **LUZ EN EL CASCO · SÍ**, **RETROEMPUJE · SÍ** y el **DAÑO 3**. La llama de HOY queda sustituida. Los valores de esta spec son los de la maqueta. Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa. La pista, la nave, la conducción del piloto automático, la cámara (también su FOV) y el bloom de la maqueta son aproximados: no son parte de esta spec. El bloom de la maqueta hace el papel del **glow** de Godot.

Se apoya en lo que ya existe:
- Los **cascos 3D** de VECTOR, BLADE y HAMMER: las bocas de las toberas (`Exhaust_L` / `Exhaust_R`, dos por nave), el fondo y el núcleo luminosos (`Nozzle_Glow_*`, `Nozzle_Core_*`), la **capa visual propia del casco** (`HullLayer`, 11, la tuya; `RivalLayer`, 12, la de los siete rivales) y el `light_cull_mask` de las luces.
- Los **cascos wireframe** (fantasma y fondo del menú): sus toberas en la tabla "Datos de cada nave" de `efectos-velocidad` (sale de `naves-3-cascos`).
- La **conducción**: entradas de acelerar, frenar, girar y derrape (`ShipInput`, todo o nada) y boost (`conduccion-avanzada`, `boost`), y el ángulo de deslizamiento (`ShipDynamics.SlipAngle`). **Los rivales nunca derrapan**: el doblado por derrape solo se verá en tu nave.
- El **nivel de daño** (`danos`) y el humo de daño con fuego (`efectos-4-humo-danos`).
- El **glow** del `WorldEnvironment` y el ajuste **PARTICLES** (`settings`).
- La pausa, la recolocación, el fantasma y la nave del menú.

Cambios respecto a specs anteriores:
- **`efectos-velocidad` no está implementada.** Su sección 5 (boca hexagonal, llama de líneas, núcleo y halo) **queda sustituida por esta spec antes de hacerse**: cuando se implemente `efectos-velocidad`, su tobera no se hace. El resto de esa spec (visión túnel, líneas de velocidad, FOV, partículas de las toberas, chispas del boost y estelas) sigue sin hacer y no entra aquí. Esta spec define su propio `th` (sección 3.1).
- La emisión de `Nozzle_Glow_*` y `Nozzle_Core_*` de los cascos deja de ser fija y sigue la potencia de la llama (sección 4.6). Hoy no cambia con el boost. **Cambia el color en reposo**: hoy el fondo es azul `#245BFF` y el núcleo `#7FA6FF`; pasan al color medio de la llama (cian), como en la maqueta.

Rama: `feature/fx-thrusters`, que sale de `develop`. Versión: la siguiente **minor** (0.26.0 sobre la 0.25.0 de hoy). Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

---

## 1. Resumen

- **Llama de dos capas** por tobera, hecha con **shader** sobre un cono de malla: un **núcleo** blanco-cian estrecho y un **penacho** cian y azul más ancho, con **ruido animado** que corre hacia atrás y la punta que se **deshilacha**. Con boost, blanco cálido, naranja y rojo. Muy por encima de 1 de brillo, para que el **glow** la haga brillar.
- **Potencia** (0–1) de cada tobera, que sigue a lo que pide el piloto con subida rápida y bajada lenta. De ella salen el largo, el ancho, el brillo y la velocidad del ruido.
- **Reacciones**:
  - **Acelerar**: se estira y se aviva rápido.
  - **Soltar**: baja despacio.
  - **Frenar**: se encoge de golpe y titila.
  - **Girar**: la tobera de **fuera** empuja más que la de dentro.
  - **Derrapar**: tiembla y la punta se dobla hacia **dentro** de la curva.
  - **Boost**: fogonazo con un **anillo de choque** en cada boca y la llama se dispara.
- **Diamantes de choque**: bandas brillantes quietas dentro de la llama, a fondo y con boost.
- **Calor**: el aire tiembla detrás de cada tobera (distorsión de pantalla).
- **Luz en el casco**: cada tobera ilumina la popa de su nave con su color.
- **Retroempuje**: dos chorros azules cortos en el morro al frenar.
- **Daño 3**: la llama tartamudea, con cortes y chasquidos naranjas.
- Para todas las naves: tú y los rivales. Fantasma y menú, con una versión reducida (sección 9).
- Etapas:
  - **0** · Inventario.
  - **1** · Llama nueva.
  - **2** · Diamantes, calor y luz en el casco.
  - **3** · Retroempuje y daño 3.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo se pone hoy la emisión de `Nozzle_Glow_*` / `Nozzle_Core_*` en cada estilo de casco (en VECTOR la pone la pintura, `HullPaintLook.LightColor`; BLADE y HAMMER, del manifiesto) y cómo pasarla a la potencia sin tocar el resto de luces.
2. Dónde están las bocas (`Exhaust_L` / `Exhaust_R`) de cada casco y cómo se orientan. El **radio** y el **achatado** de cada boca, medidos en las piezas `Nozzle_Glow_*` de los `.glb` (ancho / alto), para el perfil visual. En los cascos wireframe, los de la tabla de `efectos-velocidad`.
3. Cómo se leen, **para cada nave** (tú y los rivales), las entradas de **acelerar, frenar y girar** (`ShipInput`, todo o nada: el giro vale −1, 0 o 1), si está en **derrape**, el **ángulo de deslizamiento** y si el **boost** está activo. Los rivales dan su `RivalCommand` (`ShipInput` + boost) y no derrapan.
4. La **capa visual del casco** y el `light_cull_mask` de sus luces, para que la luz de las toberas solo toque el casco (ver 5.3: los rivales comparten capa).
5. Que la **textura de pantalla** (`hint_screen_texture`) funciona en un shader espacial con Forward+ y el glow actual: se copia después de lo opaco y el glow va al final. Comprobarlo con una prueba rápida.
6. Un punto en el **morro** de cada casco, a cada lado, para el retroempuje (en VECTOR, en la maqueta, a ±0,55 m, y 0,42 y z = −2,7). Como no hay vacíos, se proponen en el perfil visual, medidos en los `.glb`.
7. Cómo colgar la llama del casco wireframe en el **fantasma** (`GhostShip`) y en la **nave del menú** (`MenuBackground`).

**Si algo es dudoso, se pregunta antes de seguir.**

### 2.1 Resultado del inventario (2026-11-18)

1. La emisión de las bocas sale del manifiesto y es fija (fondo 2,2, núcleo 3,5): no cambia con el boost ni titila con el daño. VECTOR y BLADE en azul (`#245BFF` / `#7FA6FF`; en VECTOR la vuelve a poner la pintura); **HAMMER en naranja** (`#FF5A14` / `#FFB46A`).
2. Bocas `Exhaust_L/R`, dos por nave, apuntando a +Z: VECTOR (±0,75, 0,10, 3,20), BLADE (±0,60, 0,08, 3,30), HAMMER (±0,45, 0,20, 3,00). Las del wireframe están en el mismo sitio. Fondo luminoso medido: VECTOR 0,62 × 0,24 m, BLADE 0,42 × 0,14, HAMMER 0,41 × 0,41. **Se usa la escala de la llama ya decidida en las specs de los cascos** (de sus prototipos; en BLADE coincide con lo medido): VECTOR 1,2 × 0,55, BLADE 0,80 × 0,28 y largo × 0,85, HAMMER 0,95 redonda. Así, `es` = ancho / 1,2: **VECTOR 1, BLADE 0,67, HAMMER 0,79**; achatado = alto / ancho: **0,46, 0,35 y 1**; y largo × **1, 0,85 y 1**.
3. Tu nave calcula su `ShipInput` en cada paso, pero no lo guarda; en la cuenta atrás es `default`, aunque el teclado se puede leer aparte solo para la llama: **la llama ruge con W en la cuenta atrás**. `Drifting`, `SlipAngle` (positivo = desliza a la derecha: girando a la izquierda en derrape, positivo) e `IsImpulseActive` ya se exponen. Los rivales crean su `RivalCommand` en `RivalCar` y lo descartan: se guarda igual.
4. Capas: la tuya en `HullLayer` (11) y los siete rivales en `RivalLayer` (12), con `HullLightRig` filtrando por `LightCullMask`. Confirma el límite de 5.3.
5. `hint_screen_texture` funciona en la carrera (probado con un plano que invierte y ondula lo de detrás) y el glow va después.
6. Retroempuje, en el borde del fuselaje: **VECTOR (±0,46, 0,12, −2,9)**, **BLADE (±0,37, 0,08, −3,8)**, **HAMMER (±0,46, 0,15, −3,2)**, justo delante del borde de los canards.
7. Fantasma y fondo del menú: `Spaceship` wireframe. El fantasma pone su material en las mallas del casco, así que la llama va en nodos aparte y recibe su opacidad (que cambia con la distancia).

**Decisión del usuario, color de HAMMER**: como decidió `cascos-3d-3-hammer` (4.2), la llama de HAMMER sin boost es **naranja**, a juego con sus bocas: núcleo `#FFB46A`, medio `#FF5A14` y fuera rojo oscuro (0,55; 0,10; 0,04). Con boost, los colores de boost de todas las naves. Su boca toma su medio, así que sigue naranja. Los colores normales son un dato de cada nave en su perfil visual (VECTOR y BLADE, los de la tabla 4.4).

## 3. Estado de cada tobera

Todo se calcula **por nave** y se suaviza con aproximación exponencial: `x ← x + (objetivo − x) · (1 − e^(−Δt/τ))`.

### 3.1 Potencia `P`

| Situación | Objetivo | τ |
|---|---|---|
| Acelerando (W) o con boost | 0,35 + 0,65 × `th` | subida **0,12 s** |
| Frenando (S) | **0,06** | bajada **0,06 s** |
| Sin gas | 0,12 + 0,22 × `th` | bajada **0,3 s** |
| Parada (a menos de **1 m/s**) | el de arriba, pero como mínimo **0,14** (ralentí) | — |

- `th` = velocidad / punta base, limitado a 0–1. La punta base es la de la nave, sin daño ni pendientes (como en `efectos-velocidad`, que no está hecha: este `th` es propio de esta spec y lo podrá usar después).
- Frenando en marcha, el objetivo es 0,06; parada, aunque se frene, nunca baja de 0,14.
- Si el objetivo está por encima, se usa la τ de subida; si está por debajo, la de bajada (0,06 s al frenar y 0,3 s si no).

### 3.2 Boost `bt`, encendido y frenada

- **`bt`** (boost de la llama): objetivo 1 con boost activo y 0 si no. τ de subida **0,18 s** y de bajada **0,45 s**. Es propio de la llama: la `b` de `efectos-velocidad` (0,25 / 0,5 s) no existe todavía y, cuando exista, será otra.
- **Encendido** `kick`: se pone a **1** en el frame en que empieza un boost y cae con τ **0,35 s**.
- **Frenada** `fr`: 1 mientras se pulsa S; al soltar, cae con τ **0,2 s**.

### 3.3 Giro y derrape

- **Giro** `g`: la entrada de girar (+1 izquierda, −1 derecha, 0 sin girar; es todo o nada), suavizada con τ **0,12 s**.
- **Lado** `ℓ` de cada tobera: +1 la derecha, −1 la izquierda (vistas desde detrás).
- **Factor de giro**: `tm` = 1 + 0,28 × `g` × `ℓ` × mín(1; 1,4 × `th`). Girando a la izquierda, la derecha (la de fuera) se alarga hasta un 28 % y la izquierda se acorta lo mismo.
- **Derrape**: el ángulo de deslizamiento `β` con signo y **`βmáx` = 22°**. No es el máximo del juego (60°): con él, a los ~20° de un derrape normal la punta apenas se doblaría. 22° es el de la maqueta (0,38 rad) y el tope de la intensidad del derrape (`DriftIntensity`, `efectos-2-derrape`). Por encima, `bend` se queda en su máximo.

### 3.4 Parpadeo `q`

`q` = base × derrape × frenada × corte, con:

- **base** = 1 + 0,07 × sen(37·`t` + 2k) × sen(23·`t` + k);
- **derrape** (solo derrapando) = 1 + 0,18 × sen(61·`t` + 3k);
- **frenada** (con `fr` > 0) = 1 − 0,35 × `fr` × (0,5 + 0,5 × sen(47·`t` + k));
- **corte**: 1, o el del daño 3 (sección 7.2).

`t` es el tiempo de la carrera (se para con la pausa), `k` el índice de la tobera (0, 1…) y cada nave lleva un **desfase** de tiempo propio para que dos naves no parpadeen igual.

## 4. Llama (etapa 1)

### 4.1 Malla

- Por tobera, **dos conos** colgados de la boca y apuntando hacia atrás: **penacho** y **núcleo**.
- Cada cono es una malla de **28 segmentos × 28 anillos**, con dos datos por vértice: `v` (0 en la boca, 1 en la punta) y `θ` (ángulo, 0–2π). Las posiciones las pone el shader de vértices. Una sola malla compartida por todas las toberas y naves.
- Caja de recorte grande o recorte desactivado, porque el shader la deforma.

### 4.2 Forma

| | Penacho | Núcleo |
|---|---|---|
| Largo | `len` | `len` × 0,55 |
| Radio en la boca `r0` | 0,27 m × `es` | 0,15 m × `es` |
| Radio en la punta `r1` | 0,10 m × `es` | 0,02 m × `es` |
| Doblado | `bend` | `bend` × 0,5 |

- **Escala de la boca** `es` = radio de la boca de la nave / el de VECTOR (inventario, punto 2). Los radios de la tabla son los de VECTOR. Escala los radios, el anillo (4.5), los chasquidos (7.1) y la boca (4.6); **no** el largo, que sigue a la potencia, ni el calor ni la luz. Si con BLADE (boca de unos la mitad) la llama queda demasiado fina, se ajusta a ojo y se apunta.

- **Largo** `len` = (0,45 + 3,1·`P` + 4,2·`bt`) × `tm` × (1 + 0,45·`kick`) m.
- **Ancho** = (0,85 + 0,25·`P`) × (1 + 0,35·`bt`).
- **Doblado** `bend` (desplazamiento lateral de la punta, en el eje x de la tobera) = −signo(`β`) × mín(1; |`β`| / `βmáx`) × `len` × 0,22 − `g` × 0,06 × `len`. Derrapando, la punta se va hacia **dentro** de la curva, porque el aire le llega de lado.
- **Vértice** (para `v`, `θ`):
  - abombado = 1 + 0,35 × sen(π·`v`) (en el núcleo, 0,4 de ese abombado);
  - radio = mezcla(`r0`, `r1`, `v`^0,8) × ancho × abombado × (1 + 0,06 × sen(31·`t` + 9·`v`));
  - posición = (cos `θ` × radio + `bend` × `v`², sen `θ` × radio × achatado, `v` × largo).
- **Achatado**: alto / ancho de la boca de cada nave, medido en el inventario y guardado en su perfil visual (en la maqueta, 0,62). Una boca redonda, 1. Hoy no está en el perfil.

### 4.3 Shader

Espacial, **sin luz** (`unshaded`), mezcla **aditiva**, las **dos caras**, **sin escribir profundidad** (sí la lee: la llama queda detrás de lo que tape). Con `n` un ruido fbm de 4 octavas (ruido de valor, como `fbm` de la maqueta):

- **Ruido**: `n` = fbm(`θ` × 1,6 + 0,9·`t`, `v` × largo × 1,4 − `t` × `vr`), con `vr` = 4 + 10·`P` + 8·`bt`: el ruido corre hacia atrás más deprisa cuanto más empuja. **Concreción de la etapa 1**: `t` × `vr` va integrado (lo que ha corrido, `∫ vr dt`): con `t` de varios minutos, `t` × `vr` daba saltos enormes del ruido cada vez que cambiaba la potencia.
- **Borde**: `e` = |N · V|^1,6 en el penacho y ^0,8 en el núcleo (los bordes de lado se ven más suaves).
- **Forma**: `s` = (1 − `v`)^1,3 en el penacho y ^2,2 en el núcleo, × smoothstep(0; 0,04; `v` + 0,02).
- **Deshilachado**: `d` = smoothstep(0,15; 0,75; `n` + 0,55 − 0,9·`v`).
- **Color**: mezcla(núcleo, medio, smoothstep(0,02; 0,32; `v`)) y luego mezcla(…, fuera, smoothstep(0,3; 0,95; `v`)). En el **núcleo**, los tres colores son (núcleo, núcleo, medio).
- **Intensidad** = `I` × `q` × `s` × `e` × (0,45 + 0,75·`n`) × `d` + diamantes (sección 5.1). En el núcleo, × 1,6.
- **Salida**: color × intensidad.

### 4.4 Brillo y colores

- **Brillo** `I` = (0,9 + 1,6·`P` + 1,6·`bt`) × (0,85 + 0,15·`tm`). En el núcleo, × 1,25.
- **Colores**, mezclados de normal a boost con `bt`:

| | Normal | Boost |
|---|---|---|
| Núcleo | (0,85; 0,97; 1,0) | (1,0; 0,95; 0,85) |
| Medio | (0,21; 0,90; 1,0) · cian | (1,0; 0,62; 0,20) · naranja |
| Fuera | (0,14; 0,36; 1,0) · azul | (1,0; 0,30; 0,12) |

- Medio y fuera normales y medio de boost coinciden con cian, azul y naranja de `Palette` (a ±1 nivel). **Concreción de la etapa 1**: como los demás efectos (choques, fuego), los colores van en la lógica pura y no salen de `Palette` (que es de Godot): los de boost en los ajustes y los normales de cada nave en su perfil visual (`ThrusterColors`). Se pasan al shader tal cual los da la maqueta; los hexadecimales de la maqueta (anillo #FFB060), pasados a lineal como hace three.js.
- Se ajusta a ojo con el **glow** del juego, para que se vea como la maqueta (el bloom de la maqueta tiene umbral 0,82, fuerza 0,85 y radio 0,55). Se apuntan los valores finales.

### 4.5 Anillo de encendido del boost

- Al empezar un boost, en cada boca, un **plano** que mira hacia atrás, 0,15 m por detrás de la boca, con una textura de **anillo** suave (transparente en el centro y brillante hacia el 70 % del radio).
- Tamaño de **0,3 a 2,7 m** en **0,35 s** (lineal), con el alto × 0,75. Color **#FFB060**, aditivo, intensidad 1,4 × (1 − edad / 0,35).
- La segunda tobera, **0,04 s** después que la primera.

### 4.6 Boca

- El fondo y el núcleo luminosos de la boca (`Nozzle_Glow_*`, `Nozzle_Core_*`) toman el **color medio** y su emisión se multiplica por (0,6 + 1,6·`P` + 1,5·`bt`) × `q` / 1,08. Con potencia de crucero (`P` = 0,3) y sin boost, da la intensidad de hoy.
- Hoy la emisión es fija (no cambia con el boost) y de otro color: fondo azul `#245BFF` y núcleo `#7FA6FF` (`HullPaints.NozzleGlowColor` / `NozzleCoreColor`). Pasan al color medio en todas las pinturas: la boca en reposo se verá cian en vez de azul.

## 5. Diamantes, calor y luz (etapa 2)

### 5.1 Diamantes de choque

- Bandas brillantes **quietas respecto a la boca** dentro del **penacho** (no en el núcleo):
  - `dm` = max(0; cos(`v` × largo × 2,4 − 0,6))^10 × e^(−2,2·`v`) × `D`;
  - se suman a la intensidad como `dm` × `e` × 2,2.
- **Fuerza** `D` = max(0; (`P` − 0,6) / 0,4) × 0,55 + `bt`. Salen a partir del 60 % de potencia y, sobre todo, con boost.

### 5.2 Calor

- Por tobera, un **plano que mira a la cámara** detrás de la boca, a **0,8 + 0,55 × `len`** m, de **(1,3 + 0,25·`len`) × (1,1 + 0,5·`len`)** m.
- Shader espacial que lee la **textura de pantalla** y la desplaza:
  - dos ruidos fbm que suben con el tiempo: uno con UV × (5; 3) + (0; 2,6·`t`) y otro con UV × (4; 3,5) + (4,7; 3,1·`t`), centrados en 0;
  - máscara elíptica = smoothstep(0,5; 0,1; |(UV − 0,5) × (1,6; 1)|);
  - desplazamiento en coordenadas de pantalla = (ruido1, ruido2) × máscara × `k` × **0,045**;
  - **fuerza** `k` = (0,25 + 0,75·`P` + 1,0·`bt`) × corte.
- No escribe profundidad. Se dibuja después de lo opaco.
- Con **PARTICLES en LOW**, el calor **no se dibuja** (es lo que más cuesta en un PC flojo).
- Como la textura de pantalla se copia después de lo opaco, el calor no distorsiona las llamas (son transparentes); está bien así. Si en el inventario (punto 5) no se puede usar, se avisa con lo que sí se puede.

### 5.3 Luz en el casco

- Una `OmniLight3D` por tobera, **0,7 m detrás de la boca**, del **color medio**, con rango **7 m** y atenuación **1,6**.
- **Energía** = `E0` × (0,3 + 0,9·`P` + 1,0·`bt`) × `q`. `E0` se ajusta a ojo (empezar en 1) hasta que la popa se ilumine como en la maqueta, y se apunta.
- Solo en la **capa del casco** (`light_cull_mask`): no ilumina la pista ni el entorno.
- **Límite conocido**: tus luces van en `HullLayer` (11) y solo tocan tu casco; las de los siete rivales van en `RivalLayer` (12), que comparten todos, así que la luz de un rival **también ilumina a los rivales que tenga a menos de 7 m** (sobre todo el morro del que va detrás). Separarlos pediría una capa por rival; se acepta así. Si en la prueba se nota feo, se prueba a bajar el rango de los rivales.
- Sin sombras. 16 luces como mucho (8 naves × 2): con Forward+ no hay tope práctico, pero se miden los fps (prueba manual 13).
- **Etapa 2**: `E0` = **4** (con 1 la popa apenas se teñía; con 4 se ve el fuselaje trasero y los bordes de las toberas del color de su llama, sin quemar). La luz y la boca toman el color medio de la llama de cada nave: **en HAMMER, naranja**. Solo con casco 3D (el wireframe del fantasma y del menú no se ilumina).

### 5.4 Resultado de la etapa 2

- El calor se dibuja **antes que las llamas** (prioridad −1): la textura de pantalla no lleva lo transparente, y dibujado después borraba la llama que tenía detrás. Su `ALPHA` es la máscara elíptica, así fuera de ella no toca nada. Lo transparente que se dibuje antes que él dentro de la elipse (otro calor) se pierde ahí; no se nota.
- **Calor con la distancia** (pedido por el usuario tras probarlo): el desplazamiento de la maqueta va en coordenadas de pantalla, así que una nave lejana (con su plano pequeño) tomaba píxeles de muy lejos y se veía doble, como espectros difuminados, a 2–3 m de la nave. Ahora, hasta **8 m** de la cámara (la cámara exterior), el de la maqueta; más lejos, × 8 / distancia; y de **30 a 60 m** se apaga del todo (y deja de dibujarse). En el grupo **Toberas** del `Player`: `ThrusterHeatReference`, `ThrusterHeatFadeStart` y `ThrusterHeatFadeEnd`. Prueba `Heat_shrinks_with_distance_and_fades_out_far`.
- Diamantes, calor y luz solo en la carrera (tú y los rivales); fantasma y menú, solo la llama.
- Medido en RACE con VECTOR y las 8 naves, 8 s a fondo desde el GO a 144 Hz: 144 FPS y frame medio igual (6,95 ms); sin vsync, **2,52 ms con las toberas y 1,85 sin ellas** (unos 0,67 ms). Los frames lentos que salen (en el GO y hacia los 7 s, de 18–27 ms) salen **igual con las toberas apagadas**: ya estaban. No se ha probado en un PC flojo.

## 6. Retroempuje (etapa 3)

- Dos chorros **cortos** en el morro, en los puntos del inventario (punto 6), que apuntan **hacia delante**.
- **Fuerza** `R`: objetivo 1 mientras se pulsa S **a más de 20 km/h**, y 0 si no. τ de subida **0,05 s** y de bajada **0,15 s**.
- Misma malla y mismo shader que la llama, sin diamantes ni doblado:

| | Penacho | Núcleo |
|---|---|---|
| Largo | `rl` | `rl` × 0,6 |
| Radio en la boca / punta | 0,13 / 0,03 m | 0,07 / 0,03 m |
| Ancho | 0,55 | 0,55 |
| Brillo | 1,8 × `R` | 2,6 × `R` |

  - `rl` = (0,35 + 0,9·`R`) × (1 + 0,25 × sen(53·`t` + 2k)) m.
  - Colores: núcleo (0,9; 0,98; 1,0), medio (0,45; 0,8; 1,0), fuera (0,14; 0,36; 1,0). Velocidad del ruido 12.
- No se ve con `R` < 0,01.
- Sin calor ni luz.

## 7. Daño 3 (etapa 3)

Solo con **nivel de daño 3** (RACE). Por cada tobera, por separado:

### 7.1 Chasquidos

- Cada **0,4–1,2 s** (al azar), un **chasquido**: un sprite redondo y suave **#FF7A2A**, aditivo, **0,6 m** por detrás de la boca.
- Su fuerza salta a 1 y cae con τ **0,08 s**. Intensidad 1,6 × fuerza. Tamaño 0,6 + 1,2 × (1 − fuerza) m: se abre mientras se apaga.

### 7.2 Cortes

- Un temporizador por tobera. Al acabar, con un **18 %** de probabilidad hay **corte**: el factor de corte baja a **0,15** durante **0,05–0,12 s**. Si no, sigue a 1 y el temporizador vuelve a empezar con **0,12–0,5 s**.
- El corte multiplica la llama (parpadeo `q`), la boca, la luz y el calor.
- Convive con el fuego y el humo del daño 3 (`efectos-4-humo-danos`).

### 7.3 Resultado de la etapa 3

- Retroempuje en los puntos del perfil de cada nave (2.1), con su propio ruido (velocidad 12) y sin escalar con la boca. Solo en carrera (tú y los rivales); en tu cabina no se ve, como el resto de tu llama.
- Daño 3: el nivel llega por `ThrusterInput.DamageLevel` (el tuyo solo en RACE; el de cada rival, el suyo). El azar de cortes y chasquidos sale de una semilla por nave (de su desfase), así que con la misma semilla salen en los mismos momentos. El corte también baja la boca (va con `q`). Los chasquidos se escalan con la boca, como el anillo; sin daño 3 no salen nuevos y el que haya se apaga solo.
- Comprobado con capturas en RACE con VECTOR y daño 3 forzado con una sonda: cortes en una tobera y chasquidos naranjas, junto al humo; al frenar, los dos chorros en el morro (cámara orbital F4). Fantasma, pausa y recolocación, el usuario jugando.

## 8. Interacción con otros sistemas

- **Partículas de las toberas y chispas del boost** (`efectos-velocidad` 6.1): no existen todavía y no entran aquí. Cuando se hagan, salen de las mismas bocas.
- **Parrilla y cuenta atrás**: ralentí (`P` ≥ 0,14). Si durante la cuenta atrás se puede leer W aunque la nave no se mueva, la llama **ruge** con W (solo visual). Si no se puede, ralentí y se apunta.
- **En el aire** (pista 3D): igual que en el suelo, con las entradas que haya.
- **Recolocación**: `P` vuelve al ralentí y `kick`, `R` y `fr` a 0, al momento.
- **Pausa**: todo congelado (el tiempo `t` se para).
- **Terminada**: la nave rueda sola sin gas; la llama hace lo de sin gas.
- **Cabina**: en tu nave, la llama, el calor y la luz no se ven (el casco no se dibuja). Las de los rivales sí.
- **Atravesables** (recolocación de `rivales-3`): la llama no parpadea con el casco.
- **SETTINGS**: PARTICLES LOW quita el calor. El resto no cambia con ningún ajuste.

## 9. Fantasma y menú

- El fantasma y la nave del menú son **wireframe**: no tienen `Exhaust_*`. La llama sale de las bocas de la tabla "Datos de cada nave" de `efectos-velocidad` (0,4 m por detrás de la z trasera de cada tobera), con el radio de esa tabla para `es` y boca redonda (achatado 1).
- **Fantasma**: llama nueva con `P` = 0,35 + 0,65 × `th` de su velocidad, sin boost (`bt` = 0), sin giro ni derrape, **sin** diamantes, calor, luz, retroempuje ni daño. Su intensidad × la transparencia del fantasma.
- **Nave del menú principal** (VECTOR wireframe): ralentí (`P` = 0,14), sin calor ni luz.
- **Holograma del selector**: sin llama, como hasta ahora.

## 10. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de toberas. **Concreción de la etapa 1**: `ThrusterFxSettings` (puro) con todos los valores, y los principales en el grupo **Toberas** del Inspector del `Player` (brillo global para ajustar con el glow, tiempos, encendido, largo, empuje de fuera, doblado, `βmáx` y anillo), que también usan los rivales; el fantasma y el menú, los valores por defecto.

| Parámetro | Valor |
|---|---|
| Potencia: objetivos (gas / freno / sin gas / ralentí) | 0,35 + 0,65·th / 0,06 / 0,12 + 0,22·th / 0,14 |
| Potencia: τ (subida / bajada / bajada al frenar) | 0,12 / 0,3 / 0,06 s |
| Boost de la llama: τ (subida / bajada) | 0,18 / 0,45 s |
| Encendido: aumento / τ | 45 % / 0,35 s |
| Frenada: τ al soltar | 0,2 s |
| Giro: suavizado / empuje de fuera | 0,12 s / 28 % |
| Parpadeo (base / derrape / frenada) | 0,07 / 0,18 / 0,35 |
| Largo | 0,45 + 3,1·P + 4,2·bt m |
| Ancho | (0,85 + 0,25·P) × (1 + 0,35·bt) |
| Doblado (derrape / giro) | 0,22 / 0,06 × largo |
| Radios penacho / núcleo | 0,27 → 0,10 / 0,15 → 0,02 m (VECTOR; × `es` en cada nave) |
| Derrape: `βmáx` | 22° |
| Largo del núcleo | 55 % |
| Brillo | (0,9 + 1,6·P + 1,6·bt) × (0,85 + 0,15·tm) · núcleo × 1,25 |
| Velocidad del ruido | 4 + 10·P + 8·bt |
| Colores normal / boost | Tabla de 4.4 |
| Anillo: tamaño / duración / color / desfase | 0,3 → 2,7 m / 0,35 s / #FFB060 / 0,04 s |
| Diamantes | desde P = 0,6 · × 0,55 · + bt |
| Calor: sitio / tamaño / fuerza / desplazamiento | 0,8 + 0,55·len / (1,3 + 0,25·len) × (1,1 + 0,5·len) / 0,25 + 0,75·P + bt / 0,045 |
| Luz: distancia / rango / atenuación / energía | 0,7 m / 7 m / 1,6 / E0 × (0,3 + 0,9·P + bt) |
| Retroempuje: velocidad mínima / τ / largo | 20 km/h / 0,05 y 0,15 s / 0,35 + 0,9·R m |
| Daño 3: chasquidos / cortes | cada 0,4–1,2 s / 18 % · 0,05–0,12 s a 0,15 · espera 0,12–0,5 s |

## 11. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot:

- **Ajustes de toberas** (datos): todo lo de la sección 10.
- **Entradas de la tobera** (datos): acelerar, frenar, girar, derrape, `β`, boost activo, `th`, nivel de daño, en carrera o no.
- **Datos de la boca por nave** (perfil visual): radio, achatado y los dos puntos del retroempuje. Se rellenan desde el `Player` o el conductor del rival (inventario, punto 3).
- **Estado de la tobera** (puro, uno por nave): `Update(delta, entradas, rng)` con `P`, `bt`, `kick`, `fr`, `g`, `R`, los cortes y los chasquidos. Da, por tobera: largo, ancho, doblado, brillo, parpadeo, colores, diamantes, fuerza del calor, energía de la luz, el anillo y los chasquidos.
- **Nodos de Godot** (solo dibujan): la malla y el shader de la llama, el anillo, los chasquidos, el plano del calor, la `OmniLight3D` y la emisión de la boca, todos colgados de la boca de cada casco. Sin reglas dentro.
- No hay lógica vieja de tobera que quitar. `th` lo calcula esta spec; cuando se haga `efectos-velocidad`, sus factores `f` y `b` se añaden aparte y pueden reutilizar este `th`.

## 12. Pruebas

### Pruebas unitarias mínimas

Llama (etapa 1):
1. Con W a punta, `P` tiende a 1. Sin gas a punta, a 0,34. Frenando en marcha, a 0,06. Parada (< 1 m/s), nunca baja de 0,14, aunque se frene.
2. `P` sube de 0,14 a más de 0,9 en 0,5 s con W, y baja más despacio sin gas (τ 0,3 s) que frenando (τ 0,06 s).
3. Largo: `P` = 0,3, sin boost, sin giro → 1,38 m. `P` = 1 con boost → 7,75 m (sin `kick`).
4. Al empezar un boost, `kick` = 1 y el largo es un 45 % mayor; a los 0,35 s queda el 37 % (e^−1) de ese extra.
5. Giro a la izquierda a punta: la tobera derecha 28 % más larga y la izquierda 28 % más corta. Parada, sin diferencia.
6. Derrapando con `β` positivo de 22° o más (sin girar): `bend` negativo de 0,22 × largo; a 11°, la mitad.
6b. Escala de la boca: con `es` = 0,5, radios a la mitad y el largo igual.
7. Colores: con `bt` = 0, los de normal; con `bt` = 1, los de boost; a la mitad, la mezcla.
8. Con `delta` 0 (pausa), nada cambia.

Diamantes, calor y luz (etapa 2):
9. Diamantes: `P` = 0,5 sin boost → 0. `P` = 1 → 0,55. Con boost → +1.
10. Calor: con PARTICLES LOW, fuerza 0. Con `P` = 1 y boost, 2.
11. Luz: energía `E0` × 0,57 a `P` = 0,3 sin boost, y `E0` × 2,2 a `P` = 1 con boost.

Retroempuje y daño (etapa 3):
12. S a 100 km/h → `R` tiende a 1. S a 10 km/h → 0. Soltar → 0 con τ 0,15 s.
13. Daño 3 con semilla fija: los cortes y los chasquidos salen en los mismos momentos. Sin daño 3, nunca.
14. Un corte multiplica por 0,15 el parpadeo, la luz y el calor.

Las pruebas existentes siguen pasando. Las de la tobera de la spec `efectos-velocidad` (largo 0,35 / 2,75 / 6,35 m) ya no se escriben cuando se haga.

## 13. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Llama nueva** | Datos de la boca en el perfil visual. Secciones 3 y 4, y lo de la sección 8 y 9 que toca la llama | Pruebas 1–8 · Prueba manual 1–8 · Commit `feat:` |
| **2 · Diamantes, calor y luz** | Sección 5 | Pruebas 9–11 · Prueba manual 9–13 · Commit `feat:` |
| **3 · Retroempuje y daño 3** | Secciones 6 y 7 · versión | Pruebas 12–14 · Prueba manual 14–18 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Llama nueva**
1. TIME TRIAL, cámara exterior, parado en la parrilla: llama corta al ralentí, con el ruido moviéndose.
2. Acelerar a fondo: la llama se estira y se aviva rápido, el ruido corre más deprisa. A punta, larga y viva.
3. Soltar W: baja despacio. Frenar: se encoge de golpe y titila mientras se frena.
4. Girar a fondo a cada lado: la tobera de fuera más larga que la de dentro.
5. Derrapar: la llama tiembla y la punta se dobla hacia dentro de la curva.
6. Boost: fogonazo con el anillo en las dos bocas (casi a la vez) y la llama larga, blanca, naranja y roja. Al acabar, vuelve suave al cian.
7. Comparar con la maqueta (LLAMA NUEVA) en LENTO, conduciendo con el teclado: que se parezca. Ajustar el glow y apuntar los valores.
8. Mirar las tres naves y un rival: cada boca con su llama, su tamaño y su achatado. La boca en reposo, cian.

**Etapa 2 · Diamantes, calor y luz**
9. A fondo: aparecen bandas brillantes quietas dentro de la llama. Con boost, mucho más marcadas.
10. Detrás de la nave, el aire tiembla. Más a fondo y con boost. Parado, un temblor suave.
11. PARTICLES en LOW: sin temblor.
12. La popa del casco se ilumina de cian y, con boost, de naranja. La pista no se ilumina.
13. Mirar los fps con los 8 en la salida y con todos a fondo.

**Etapa 3 · Retroempuje y daño 3**
14. Frenar fuerte: dos chorros azules cortos en el morro. Frenar casi parado: no salen.
15. Daño 3 (RACE): la llama tartamudea con cortes cortos y chasquidos naranjas en las bocas, junto al fuego y al humo del daño.
16. Pausa con todo activo: congelado.
17. Recolocarse: la llama vuelve al ralentí al momento.
18. TIME TRIAL con fantasma: su llama sigue su velocidad, sin calor, luz ni anillo, con su transparencia.

## 14. Fuera de alcance

- Las **estelas** (spec 7), las **luces de la nave** (spec 8) y la **visión túnel** por cámara (spec 9).
- El resto de `efectos-velocidad`: visión túnel, líneas de velocidad, FOV, partículas de las toberas, chispas del boost y estelas de las alas.
- Que la luz de las toberas ilumine la pista o el entorno (iluminación del circuito), y separar la luz de cada rival de los demás (5.3).
- Sonido y vibración del mando.
