# VECTORY — Spec: Elementos del circuito (rediseño visual)

Versión 1.1 · 2026-11-03 · Estado: **definido, listo para implementar**

Rediseño de todo lo que se ve en el circuito, decidido sobre maquetas 3D. Regla principal: **nada se pinta en la pista**. Todos los elementos son estructuras que se alzan sobre la pista o junto a ella. La única excepción son las marcas de parrilla, que se apagan al dar la salida.

Elementos: **suelo de la pista**, **meta**, **checkpoints**, **parrilla**, **semáforo**, **túneles**, **avisos de curva (chevrons)** y **señal de cresta**.

Se apoya en lo que ya existe: `Track` / `TrackDefinition` (`Centerline`, ancho, meta, checkpoints configurables, parrilla), `Palette`, la lógica de carrera (estados, sectores, vueltas), el HUD, las barreras y el **marco local de la pista** de la spec `pista-3d` (posición, T, N y B en cualquier `s`). Esta spec **depende de `pista-3d`**: todos los elementos se construyen con ese marco, así que siguen pendientes y peraltes.

Rama: `feature/circuit-elements`, que sale de `develop` con `feature/track-3d` ya mergeada.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

### Maquetas de referencia

Son las maquetas sobre las que se decidió cada elemento. Si algo de esta spec no queda claro, manda lo que se ve en ellas, salvo donde la spec dice otra cosa.

- Meta: https://claude.ai/artifact/EN6E4VqGvGQuRSj93BYwBV
- Checkpoint: https://claude.ai/artifact/QefksqWa2kx9HaHnoEg4Hr
- Túnel: https://claude.ai/artifact/Vy6LdmZiktPN6vGFZAnMTa
- Parrilla: https://claude.ai/artifact/AhiFs254xPDHEjzPhze4ha
- Avisos de curva: https://claude.ai/artifact/HG5Tgvmh3iZ5LU4PVFPxzn
- Señal de cresta: https://claude.ai/artifact/67nwvhNK1eYPK2w7z2orXs
- Semáforo: https://claude.ai/artifact/X5yyN4sfpUtbYoyJ9r6VM6
- Suelo (diseño 0): https://claude.ai/artifact/1FwCx282n5LK89WRpGThRb

---

## 1. Resumen

| Elemento | Forma | Color | Animación |
|---|---|---|---|
| Suelo | Bordes, 2 carriles discontinuos, línea central y transversales cada 10 m | Bordes cian, el resto azul | — |
| Meta | Pórtico doble de medio hexágono | Naranja | Destello al cruzar |
| Checkpoint | Pórtico doble rectangular con panel | Cian al 50 % | Destello suave al cruzar + tiempo en el panel |
| Parrilla | Marco de luz en el suelo por puesto, con número | Cian, el del jugador blanco | Se apagan en la salida |
| Semáforo | 5 luces octogonales colgadas de la meta | Naranja → destello cian | Secuencia F1 |
| Túnel | Sección octogonal en celosía | Anillos azules, largueros cian | Pulso constante |
| Curva | Chevrons dobles en la barrera exterior | Naranja | Ola en el sentido de la marcha |
| Cresta | Balizas ^ + barrera iluminada | Naranja | Latido |

- Colores siempre desde `Palette`. "Brillo" o "intensidad" > 1 significa multiplicar el color para que entre en el glow, como ya se hace con las líneas que brillan.
- Todo se genera por código como **mallas de líneas agrupadas** (nada de objetos sueltos por línea), una vez al crear el circuito. Las partes animadas (destellos, pulsos, olas) van en mallas propias para poder cambiarles el color por material o por parámetro de shader, sin regenerar geometría.

---

## 2. Suelo de la pista

Diseño **0** de la maqueta del suelo. Esa opción se hizo como aproximación de la malla actual: si la malla real ya coincide con esto, no hay que tocarla; si no, se deja exactamente así.

- **Bordes**: las dos líneas del borde, continuas, en **cian** (`Palette.Cyan`), con el grosor normal de línea luminosa.
- **Carriles**: dos líneas **discontinuas** a **±1/3 del medio ancho** desde el eje, con trazos de **5 m** cada **10 m**. **Azul** (`Palette.Blue`), opacidad **~85 %**.
- **Línea central**: continua sobre el eje, **azul**, opacidad **~85 %**.
- **Transversales**: una línea de borde a borde **cada 10 m** sobre `s`, **azul**, opacidad **~65 %**, algo más finas que los carriles.
- Las barreras **no cambian**.
- Parámetros en el Inspector (circuito): separación de transversales (10 m), largo y periodo de los trazos de carril (5 m / 10 m), opacidad de carriles y línea central (0,85) y de transversales (0,65).
- Afecta a todos los circuitos, también `circuit_01` y los de prueba. Es un cambio solo visual: **la firma del trazado no cambia** y récords y fantasmas siguen valiendo.
- Son líneas de la propia pista, no un elemento pintado: la regla de "nada se pinta en la pista" se refiere a meta, checkpoints y demás elementos.

## 3. Meta

Sustituye el arco de medio hexágono actual y cualquier línea de meta dibujada en el suelo.

### 3.1 Forma

- **Dos medios hexágonos** iguales, uno delante y otro detrás del punto de meta, separados **4 m** a lo largo de `s` (a −2 m y +2 m).
- Perfil de cada medio hexágono, en (lateral `d`, altura `h`) sobre la superficie, con `W` = medio ancho de pista + **2 m** de holgura y `H` = **12 m**:
  `(−W, 0) → (−W, 0,6H) → (−0,5W, H) → (0,5W, H) → (W, 0,6H) → (W, 0)`
- Los pilares quedan **por fuera de las barreras**.
- **Uniones** entre los dos arcos: una línea en cada vértice (las 6 esquinas, base incluida) y una **X en cada pilar** (de la base de un arco al quiebro del otro, en los dos sentidos).
- Líneas principales de los arcos: grosor normal de línea luminosa; uniones y X algo más finas y al ~70 %.
- Color: **naranja** (`Palette.Orange`).
- Nada en el suelo.

### 3.2 Cruce y destello

- La **línea de cruce** es el plano en `s` de meta, a mitad entre los dos arcos. La lógica de vueltas no cambia: sigue contando por `s`.
- Cada vez que **cualquier nave** cruza la meta (también en la primera pasada tras la salida), el pórtico **destella**:
  - Al instante: color hacia blanco cálido (~70 %) e intensidad × 3,5.
  - Vuelve al naranja normal en **0,6 s**, con caída cuadrática (rápida al principio y suave al final).
  - Si destella otra vez antes de acabar, el destello vuelve a empezar.

## 4. Checkpoints

Sustituye el medio rectángulo actual. El número de checkpoints sigue siendo configurable.

### 4.1 Forma

- **Pórtico doble rectangular**, de la misma familia que la meta: dos arcos separados **3 m** (a −1,5 m y +1,5 m del punto del checkpoint).
- Perfil de cada arco, con `W` = medio ancho + **2 m** y `H` = **9 m**: `(−W, 0) → (−W, H) → (W, H) → (W, 0)`.
- Uniones en las 4 esquinas y una **X en cada pilar**, como la meta.
- **Panel**: un marco rectangular colgado bajo el travesaño del **arco de entrada** (el que se ve al acercarse), de unos **2,2 m** de alto, de pilar a pilar menos 1,2 m por lado. Ahí va el tiempo (4.3).
- Color: **cian** (`Palette.Cyan`) al **50 %** de brillo en reposo.

### 4.2 Destello

- Al cruzarlo cualquier nave: brillo hasta × 1,6 y color hacia blanco (~40 %), y vuelve al 50 % en **0,45 s**. Más suave que la meta.

### 4.3 Tiempo en el panel

- El panel hace de **marcador de circuito**: muestra el tiempo de **la nave que acaba de cruzarlo**.
- Contenido, en una línea: `S<n>   <tiempo del sector>` a la izquierda y la **diferencia con el mejor sector de esa nave** a la derecha (`−0.214` en cian si mejora, `+0.164` en naranja si empeora). Si esa nave aún no tiene mejor tiempo en ese sector, solo se muestra el tiempo.
- Visible **2 s**: aparece en 0,1 s y se desvanece en los últimos 0,3 s. Si cruza otra nave, se sustituye.
- Formato de tiempo: el mismo del HUD. Tipografía monoespaciada en negrita, con un brillo **contenido** (el glow no puede emborronar los números).
- El panel mira hacia las naves que se acercan, así que el jugador normalmente **no ve su propio tiempo** (cuando aparece, el arco ya lo tiene detrás). Lo ven quienes vienen detrás y las cámaras exteriores. **El jugador ve su tiempo de sector en el HUD.**
- **HUD**: si todavía no lo muestra, al cruzar un checkpoint o la meta aparece el tiempo del sector recién cerrado con su diferencia (mismos colores), 2 s, en la zona de tiempos que ya tiene el HUD. Si ya lo hace, no se toca.
- Implementación libre (textura de un `SubViewport`, `Label3D` o similar), siempre que se vea nítido y con el estilo de líneas luminosas.

## 5. Parrilla

Sustituye a la sección 3.2 de la spec `carrera` (corchetes azules fijos). La disposición de la sección 3.1 de `carrera` **no cambia** (8 puestos, columnas alternas, distancias y desplazamiento lateral).

- Cada puesto tiene un **marco rectangular de luz en el suelo**, centrado en el puesto, del tamaño aproximado de la nave con algo de margen (parámetro, ~3,6 × 6,4 m), apenas por encima de la superficie para que no parpadee.
- Delante de cada marco, su **número** (1–8), plano sobre el suelo y legible desde detrás de la nave.
- Color: **cian**. El marco y el número del **puesto del jugador**, en **blanco**.
- **Se apagan al dar la salida**: a 0 en **0,5 s** desde el GO, y ya no vuelven en toda la carrera. Por eso son la **única excepción** a la regla de no pintar la pista.
- En TIME TRIAL (salida lanzada) no se dibujan.
- Esto también sustituye "Sin números dibujados" y "No desaparecen" de la spec `carrera`.

## 6. Semáforo

Sustituye a la sección 4.4 de la spec `carrera` (semáforo 2D en el HUD). La **secuencia y los tiempos de la sección 4.1 no cambian**: una luz por segundo, todas encendidas a los 5 s y apagado a los 6 s, sin retardo aleatorio.

### 6.1 Aspecto

- **Panel** colgado del travesaño de la **meta**, en el arco de entrada (el que mira a la parrilla), con el centro a **~9,9 m** de altura.
  - Marco rectangular cian tenue (~55 %) de ~10,8 × 2,3 m, colgado del travesaño con dos tirantes cortos.
- **5 luces** en fila dentro del panel, separadas ~2,1 m. Cada luz:
  - **Apagada**: un octógono de ~0,8 m de radio, en naranja tenue (~50 %).
  - **Encendida**: el octógono a pleno brillo con **4 octógonos concéntricos** dentro (al 72, 50, 30 y 12 % del radio), naranja intenso con un toque de blanco. Así parece una luz llena hecha de líneas.
- **Salida (GO)**: en el momento del apagado, las 5 luces dan un **destello cian** (todas con los anillos, brillo × 2,8) que se apaga en **0,7 s**. Después quedan apagadas.
- El semáforo **se queda en la meta** el resto de la carrera, apagado.

### 6.2 HUD

- Se **quita** el semáforo 2D del HUD.
- El **número de la cuenta atrás y el GO** del HUD (sección 4.4 de `carrera`) **se mantienen**, en la misma posición, colores y tiempos. Si una vez probado sobra, se quita en otra spec.

## 7. Túneles

Primera versión de los túneles. Son **solo geometría**: no tienen colisión propia ni cambian la física. Las barreras siguen siendo el límite lateral también dentro.

### 7.1 Datos

`TrackDefinition` gana una lista opcional `Tunnels`, exportada en el Inspector: pares (**inicio**, **fin**) en metros de `s` desde la meta.

- Lista vacía = sin túneles. Ningún circuito actual lleva túneles.
- Validación al cargar (aviso en consola, sin colgar): inicio ≥ fin, solapes entre túneles o un túnel que cruza la meta → se ignora ese túnel. Un túnel de menos de 40 m → aviso.
- Aviso también si hay una **cresta de despegue** (sección 9) dentro de un túnel o a menos de 50 m de su entrada. La nave no debería llegar al techo, pero es mejor no diseñarlo así.

### 7.2 Forma

- **Sección octogonal abierta por abajo**: los anillos no cruzan el suelo, acaban a ras de pista por fuera de las barreras.
- Perfil, en (`d`, `h`) sobre la superficie, con `W` = medio ancho + **2 m** y `H` = **11 m**:
  `(−W, 0) → (−W, 0,42H) → (−0,72W, 0,84H) → (−0,32W, H) → (0,32W, H) → (0,72W, 0,84H) → (W, 0,42H) → (W, 0)`
- **Anillos** cada **8 m** sobre `s`, en **azul** (`Palette.Blue`). Si el último tramo no cuadra, el último anillo va justo en el fin.
- **Largueros**: una línea en cada uno de los 8 vértices, de principio a fin, en **cian** al ~75 %, más finos que los anillos.
- **Celosía**: entre cada dos anillos consecutivos, una **diagonal en cada cara**, alternando el sentido como un tablero de ajedrez. Azul, finas, al ~45 %.
- **Bocas**: en la entrada y en la salida, **anillo doble**, con un segundo anillo a 1,2 m hacia fuera del túnel y los dos unidos por sus vértices. Algo más gruesos que los normales.
- Todo sigue el marco local: en curva el túnel se curva y en peralte se ladea.

### 7.3 Pulso

- **Dos pulsos** recorren el túnel **en el sentido de la marcha**, en bucle, a **120 m/s**, repartidos a la misma distancia uno del otro.
- Cada anillo se ilumina según su distancia `x` al pulso más cercano: `g = exp(−(x / 7 m)²)`. El color pasa del azul a casi blanco cian (~75 %), con brillo × (1 + 1,8·g) y algo más de grosor.
- Es **constante**: no depende de la nave ni de la carrera. Funciona igual en la previa, en pausa no (se congela) y en el menú no aplica.
- Implementación recomendada: un shader de líneas que reciba la posición de los pulsos y calcule `g` por vértice con la `s` guardada en el vértice, para no tener un material por anillo. Si es más sencillo un material por anillo, vale mientras el rendimiento sea bueno.

## 8. Avisos de curva: chevrons

Sustituyen a los arcos de aviso de curva, si los arcos actuales de `TrackDefinition` cumplen esa función (ver sección 13).

### 8.1 Datos

`TrackDefinition` gana una lista opcional `CurveWarnings`: pares (**inicio**, **fin**) en metros de `s`, uno por curva técnica.

- El **lado** se calcula solo: los chevrons van en la barrera **exterior** de la curva y apuntan hacia el interior (el sentido del giro), según la curvatura del eje en el tramo.
- Validación como en los túneles (orden, solapes, fuera de la vuelta → se ignora y se avisa).

### 8.2 Forma

- Desde el inicio hasta el fin, **cada 9 m**, un **chevron doble** (`>>`) sobre la barrera exterior:
  - Un poste corto de 0,4 m sobre la barrera y el chevron encima, centrado a ~1,6 m por encima del borde superior de la barrera.
  - Cada `>` mide ~1 m de ancho y ~2 m de alto, y los dos van separados 0,9 m.
  - Plano a lo largo de la pista (mirando hacia dentro de la curva), orientado con el marco local.
- Color: **naranja**, brillo base ~0,55.

### 8.3 Ola

- Una ola recorre los chevrons **en el sentido de la marcha** y se repite unas **2,2 veces por segundo**, con un desfase de 1 ciclo cada 45 m.
- Al pasar la ola, cada chevron sube a brillo ~2,1 con un toque de blanco (~40 %) y baja enseguida (pico gaussiano estrecho).
- Es constante, como el pulso del túnel.

## 9. Señal de cresta

### 9.1 Datos

`TrackDefinition` gana una lista opcional `CrestWarnings`: la `s` de lo alto de cada cresta que se quiera señalizar.

- Normalmente se ponen en las crestas donde se despega (spec `pista-3d`, 3.2). Calcularlo solo no entra aquí: se ponen a mano.
- TEST HILLS lleva una en su cresta de la recta 2.

### 9.2 Balizas

- Tres paneles a **150, 100 y 50 m** antes de la cresta, en **las dos barreras**.
- Cada panel: un poste de 2,2 m sobre la barrera y encima un marco rectangular de ~3 × 2,2 m, desplazado un poco hacia dentro de la pista, mirando a la nave que llega.
- Dentro, flechas `^` apiladas: **1, 2 y 3 flechas** a 150, 100 y 50 m (más flechas cuanto más cerca).
- Color: **naranja**. Marco al ~75 % y flechas más gruesas, con un **latido** suave (brillo 1,0–1,5 a ~0,6 Hz).

### 9.3 Barrera iluminada

- El tramo de barrera de **45 m antes a 45 m después** de la cresta, en los dos lados, cambia a **naranja**:
  - El borde superior de la barrera se duplica con una segunda línea 0,35 m por encima, y los postes verticales de ese tramo suben hasta ella.
  - Brillo con latido entre ~0,9 y 1,5, un poco más rápido que las balizas (~0,8 Hz).
- Fuera de ese tramo, la barrera sigue igual.
- Ojo: la barrera cian normal de ese tramo **no se dibuja**. Se sustituye por la naranja, no se superponen.

## 10. Parámetros en el Inspector

Valores iniciales. Los definitivos son los que se ajusten probando y luego pasan a ser los valores por defecto, como en las otras specs.

| Parámetro | Valor | Dónde |
|---|---|---|
| Separación de transversales del suelo | 10 m | circuito |
| Trazos de carril: largo / periodo | 5 m / 10 m | circuito |
| Opacidad de carriles y línea central | 0,85 | circuito |
| Opacidad de transversales | 0,65 | circuito |
| Meta: altura / separación / holgura | 12 m / 4 m / 2 m | circuito |
| Meta: duración del destello | 0,6 s | circuito |
| Checkpoint: altura / separación / holgura | 9 m / 3 m / 2 m | circuito |
| Checkpoint: brillo en reposo | 0,5 | circuito |
| Checkpoint: duración del destello | 0,45 s | circuito |
| Checkpoint: tiempo visible en el panel | 2 s | circuito |
| Parrilla: tamaño del marco | 3,6 × 6,4 m | circuito |
| Parrilla: apagado tras el GO | 0,5 s | circuito |
| Semáforo: duración del destello de salida | 0,7 s | circuito |
| Túnel: altura / holgura | 11 m / 2 m | circuito |
| Túnel: distancia entre anillos | 8 m | circuito |
| Túnel: velocidad del pulso / pulsos a la vez | 120 m/s / 2 | circuito |
| Chevrons: separación | 9 m | circuito |
| Chevrons: frecuencia de la ola | 2,2 Hz | circuito |
| Cresta: distancias de las balizas | 150 / 100 / 50 m | circuito |
| Cresta: medio largo de la barrera iluminada | 45 m | circuito |

Iguales para todos los circuitos (son del constructor del circuito, no de cada `TrackDefinition`). En `TrackDefinition` solo van las listas `Tunnels`, `CurveWarnings` y `CrestWarnings`.

## 11. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Perfiles de los elementos** (puro): funciones que devuelven los puntos (`d`, `h`) de cada perfil (meta, checkpoint, túnel) a partir de sus parámetros. `Track` los convierte a 3D con el marco de la pista.
- **Disposición** (puro): a partir de los datos y los parámetros, la lista de `s` donde va cada cosa (anillos del túnel, chevrons, balizas, tramo de barrera naranja) y el lado de los chevrons según la curvatura.
- **Animaciones** (puro): funciones de tiempo → intensidad/color (destello de meta y checkpoints, pulso del túnel, ola de chevrons, latidos, secuencia del semáforo). Sin nodos de Godot, para poder probarlas.
- **Validación de datos** (puro): túneles, avisos de curva y crestas (sección 7.1 y 8.1).
- **`Track`** y sus constructores: generan las mallas y conectan las animaciones. Sin reglas dentro.
- **Lógica de carrera**: avisa cuando una nave cruza meta o checkpoint, con su tiempo de sector y la diferencia. Meta, checkpoints, semáforo y parrilla solo **escuchan** y se iluminan. No contienen lógica de carrera.
- **HUD**: solo lee el estado, como ahora.

### Pruebas unitarias mínimas

1. Perfil de la meta con medio ancho 10 y holgura 2: 6 puntos, extremos en `d = ±12` y `h = 0`, techo a 12 m.
2. Perfil del túnel: 8 puntos, simétrico, extremos en `h = 0` (no cruza el suelo).
3. Túnel de 0 a 100 m con anillos cada 8 m: anillos en 0, 8, …, 96 y 100.
4. Validación: túnel con inicio > fin, túneles solapados y túnel que cruza la meta se ignoran con aviso; cresta dentro de un túnel da aviso.
5. Chevrons en una curva a la derecha van en la barrera izquierda, y en una curva a la izquierda en la derecha.
6. Balizas de cresta en `s` − 150, − 100 y − 50, con 1, 2 y 3 flechas. Barrera naranja de `s` − 45 a `s` + 45.
7. Destello de meta: intensidad máxima en t = 0 y normal en t ≥ 0,6 s. Volver a disparar a mitad lo reinicia.
8. Pulso del túnel: con un pulso en `s` = 50, el anillo de 50 tiene `g = 1` y el de 64 tiene `g ≈ exp(−4)`.
9. Semáforo: a 3,5 s hay 3 luces encendidas; a 6,2 s destello cian; a 7 s todo apagado.
10. Marcas de parrilla: opacidad 1 antes del GO, 0 a los 0,5 s del GO, y siguen a 0 en vueltas siguientes.
11. Panel del checkpoint: diferencia negativa → color de mejora; positiva → color de empeora; sin mejor previo → sin diferencia.
12. La firma de `circuit_01` y `test_oval` no cambia con esta spec.

Las pruebas existentes siguen pasando sin cambiarlas.

## 12. Prueba manual

1. **Suelo**: en CIRCUIT 01 y TEST OVAL, bordes cian, dos carriles discontinuos, línea central y transversales cada 10 m, como en el diseño 0 de la maqueta. Récords y fantasmas siguen ahí.
2. **Meta**: pórtico doble naranja, sin nada en el suelo. Destella cada vez que pasas, también en la primera pasada tras la salida.
3. **Checkpoints**: pórtico rectangular cian tenue. Destellan al pasar. Con la cámara exterior, el panel muestra tu tiempo de sector y la diferencia con el color correcto. En el HUD ves tu tiempo de sector.
4. **Parrilla**: marcos con número en el suelo y el tuyo en blanco. Se apagan al dar la salida y no vuelven. En TIME TRIAL no aparecen.
5. **Semáforo**: las luces de la meta se encienden de una en una con 5, 4, 3, 2, 1 en el HUD; en el GO destellan en cian. Ya no hay semáforo 2D en el HUD.
6. **Túnel**: en un circuito de prueba con un túnel (por ejemplo, añadir uno temporal en TEST OVAL o en TEST HILLS), la sección octogonal en celosía con bocas dobles y los dos pulsos corriendo. En curva y en peralte sigue a la pista sin huecos. Desde cabina, a 300 km/h, no marea ni tapa la pista.
7. **Chevrons**: en una curva con aviso, en la barrera exterior, apuntando al giro, con la ola en el sentido de la marcha.
8. **Cresta**: en TEST HILLS, balizas a 150, 100 y 50 m con 1, 2 y 3 flechas, y barrera naranja en la zona de despegue. El resto de la barrera, cian.
9. Pausa: se congelan el pulso, la ola, los latidos y los destellos.
10. Rendimiento: con todos los elementos activos en un mismo circuito, los FPS no bajan de forma apreciable.

## 13. Pendiente de confirmar al implementar

- **Arcos actuales de `TrackDefinition`**: si son los arcos de aviso de curva del documento maestro, se sustituyen por los chevrons y se quitan. Si son otra cosa (decorativos u otro uso), **se avisa antes** de tocarlos.
- Si algún elemento queda mal en una pendiente o un peralte fuerte, **se avisa** antes de cambiar su diseño (como en `pista-3d`).
- Cuando haya una cresta justo antes de una curva, la barrera naranja y los chevrons pueden juntarse. Se revisa al diseñar el circuito definitivo.

## 14. Fuera de alcance

- Calcular solas las curvas técnicas o las crestas a señalizar.
- Colisión con el techo o las paredes del túnel.
- Señal de peralte, separadores de sector y zona de recolocación (descartados).
- Efectos de partículas, sonido y cambios en el menú o el selector (la miniatura no muestra túneles ni avisos).
- El circuito definitivo (spec propia).
