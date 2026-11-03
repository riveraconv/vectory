# VECTORY — Spec: Pista 3D (desniveles, peraltes y saltos)

Versión 1.1 · 2026-11-03 · Estado: **implementado y validado** (v1.1: valores ajustados en pista: gravedad 14 m/s² y pérdida al aterrizar del 10 %)

Primera spec de la pista 3D. La pista deja de ser plana: cada punto del eje puede tener **altura** y **peralte**. La nave sigue la superficie, **se despega en las crestas** y vuela un poco, **se frena al subir y acelera al bajar**, y el **peralte da agarre** en curva. Toda la vista se ladea con la pista.

Los túneles van en una spec aparte, y el circuito definitivo con todo esto en otra. Para probar esta se crea un tercer circuito de pruebas, **TEST HILLS**.

`circuit_01` y `test_oval` **no cambian**: siguen planos, con la misma firma de trazado, así que sus récords y fantasmas siguen valiendo. Sirven además para comprobar que en llano todo se conduce exactamente igual que antes.

Se apoya en lo que ya existe: `Track` / `TrackDefinition` (`Centerline`, ancho, meta, checkpoints, parrilla, arcos), las barreras y su colisión respecto al eje, `Player` / `Spaceship` y la dinámica de la nave (rumbo, velocidad longitudinal y lateral, agarre, derrape, inclinación), el boost y la regla de pérdida del exceso sobre la punta, el daño, la recolocación, las dos cámaras y sus efectos (sacudida, vibración, inclinación de cabina, boost), el fantasma, `CircuitCatalog` (`IsTest`), el selector de circuito (perfil de elevación, miniatura, `MAX BANKING`) y la firma del trazado.

Rama: `feature/track-3d`, que sale de `develop` con `feature/circuit-select` ya mergeada.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Altura y peralte en el eje**: un circuito puede definir cómo sube y baja la pista y cuánto se ladea, a lo largo de la vuelta. Sin datos, la pista es plana, como hasta ahora.
- **Todo el circuito sigue la superficie**: bordes, carriles, transversales, barreras, meta, checkpoints, parrilla y arcos se construyen sobre el marco local de la pista (adelante, arriba y lado), así que suben, bajan y se ladean con ella.
- **La nave va pegada a la superficie** y se orienta con ella: cabecea en las pendientes y se ladea en los peraltes.
- **Crestas**: si la pista cae más deprisa de lo que la gravedad puede bajar la nave, la nave **se despega y vuela**. La gravedad en el aire es reducida, así que no cae a plomo. **Aterriza** con una sacudida que depende de lo fuerte que caiga, **sin daño**.
- **Pendientes**: subiendo, la nave **se frena hasta un 30 %**; bajando, **acelera hasta un 30 %** (sección 4).
- **Peralte**: ladea la nave y **toda la vista** (cabina y exterior), y en curva **da agarre y algo más de giro**, de forma que se puede entrar más rápido (sección 5).
- Mismas reglas en RACE y TIME TRIAL y para todas las naves.

---

## 2. Datos de la pista

### 2.1 Definición en el circuito

`TrackDefinition` gana dos listas opcionales, exportadas en el Inspector:

| Campo | Contenido | Ejemplo |
|---|---|---|
| `ElevationKeys` | Pares (distancia sobre la vuelta en m desde la meta, altura en m) | (0, 0), (820, 0), (960, 20)… |
| `BankKeys` | Pares (distancia en m, peralte en grados). Positivo = el borde **derecho** más alto que el izquierdo, en el sentido de la marcha | (300, 0), (360, 25), (710, 25), (770, 0) |

- Entre claves, **interpolación suave** (cúbica monótona o similar): sin escalones en la altura ni en la pendiente, y sin pasarse de los valores de las claves (que una subida no haga una joroba de más).
- La vuelta es cerrada: después de la última clave se interpola hacia la primera, pasando por la meta.
- Lista vacía = 0 en toda la vuelta. `circuit_01` y `test_oval` las dejan vacías.
- La distancia de cada clave se mide sobre el eje en **planta** (la misma distancia que ya usa el circuito), así que añadir altura no mueve las claves.

### 2.2 Validación

Al cargar el circuito se avisa en consola, sin colgar el juego, si:
- algún peralte pasa de **35°**;
- alguna pendiente pasa de **15°**;
- hay claves desordenadas o con la distancia fuera de la vuelta (se ignoran esas claves).

### 2.3 Marco local de la pista

Para cada distancia `s` sobre la vuelta se calcula, por interpolación entre los puntos del eje (sin saltos de un segmento a otro):

- **Posición** del eje: la de planta más la altura.
- **Adelante (T)**: tangente del eje en 3D. Incluye la pendiente.
- **Arriba (N)**: perpendicular a T, girada alrededor de T el ángulo del peralte.
- **Lado (B)**: perpendicular a T y N.

Un punto de la pista se describe con (`s`, desplazamiento lateral `d` sobre la superficie, altura `h` sobre la superficie). Su posición es: eje(`s`) + B·`d` + N·`h`.

Hacen falta las dos consultas, en una clase pura:
- **De (`s`, `d`, `h`) a posición y orientación.**
- **De una posición al punto más cercano**: `s`, `d`, `h` y el marco en ese punto. Ya existe algo parecido en planta para la colisión y la recolocación; se amplía para que funcione con altura y peralte.

### 2.4 Firma del trazado

- La firma ya incluye los puntos del eje, así que la altura entra sola.
- El **peralte** se añade a la firma **solo si** algún punto tiene peralte distinto de 0. Así la firma de los circuitos planos no cambia.
- Prueba obligatoria: la firma de `circuit_01` y la de `test_oval` son las mismas que antes de esta spec.

## 3. La nave sobre la superficie

### 3.1 Modelo

- La **dinámica actual de la nave no cambia de reglas**: rumbo, velocidad longitudinal y lateral, agarre, derrape, boost y colisión con las barreras siguen funcionando igual, en el plano de la superficie bajo la nave.
- Se añade una **capa vertical**: la altura de la nave sobre la superficie, su velocidad vertical y si está **en el suelo** o **en el aire**.
- En el suelo, la nave va a la **altura de vuelo** actual sobre la superficie (la que ya tiene hoy sobre la pista plana) y su velocidad sigue la superficie.
- Enfoque recomendado: mantener la dinámica actual trabajando en el plano local y añadir la capa vertical y la orientación encima, en vez de rehacer la nave con física de Godot. Si para esto hay que cambiar cómo `Player` guarda la posición, la velocidad o el rumbo, **se avisa antes**.
- El resultado tiene que ser **idéntico al actual en pista plana**: misma aceleración, punta, giro, derrape y colisiones. Las pruebas existentes de la dinámica siguen pasando sin tocarlas.

### 3.2 Despegue en crestas

- La nave tiene una **gravedad** (hacia abajo en el mundo) de **14 m/s²** (ajustada en pista; la v1.0 proponía 6 m/s², pero a la punta del juego los saltos eran de más de 500 m).
- En el suelo, la nave sigue la superficie mientras la pista "caiga" con una aceleración vertical que la gravedad pueda seguir. En una cresta de radio vertical `R`, a velocidad `v`, la nave **se despega** cuando `v² / R` supera la gravedad.
  - Ejemplo: con 14 m/s², una cresta de radio 600 m despega a partir de ~330 km/h (con 6 m/s², a partir de ~215 km/h).
- Al despegar, la nave sale con la velocidad que llevaba, incluida su componente vertical (si subía una rampa, sale hacia arriba).
- En **hondonadas y subidas** (compresiones) la superficie empuja a la nave hacia arriba: nunca despega por ahí y simplemente la sigue.
- La nave no puede quedar nunca por debajo de la superficie: si por cualquier motivo pasara, se coloca a la altura de vuelo en ese frame.

### 3.3 En el aire

- Gravedad de 14 m/s², sin sustentación.
- La velocidad horizontal se conserva: el agarre lateral no actúa, así que el deslizamiento que hubiera se mantiene.
- **W y S no hacen nada** en el aire.
- **Giro con A/D al 30 %** del normal: se puede corregir el morro, pero poco.
- **No se puede entrar en derrape**. Si se despega derrapando, el estado de derrape se mantiene y se resuelve al aterrizar con las reglas de siempre (al tocar suelo, si ya no se cumple la entrada, sale y recupera el agarre).
- **Boost**: se puede activar en el aire. El impulso se suma a la velocidad longitudinal, como siempre.
- **Barreras**: el límite lateral funciona igual en el aire, como si la barrera fuera infinitamente alta. Así la nave nunca sale del circuito por un salto.
- **Orientación**: el morro se va inclinando poco a poco hacia la dirección del movimiento (sube al despegar de una rampa y baja al caer), con un máximo de **20°** y un suavizado de **0,3 s**. El ladeo del peralte se va a 0 (nivelado con el mundo) con el mismo suavizado. La inclinación visual por giro sigue como siempre.

### 3.4 Aterrizaje

- Se aterriza al llegar a la altura de vuelo sobre la superficie.
- Se quita la velocidad que va contra la superficie (la componente a lo largo de N). La que va a lo largo de la superficie se conserva.
- **Sin daño**: el aterrizaje no suma puntos de daño ni cuenta como impacto contra la barrera.
- **Pérdida de velocidad al aterrizar**: parámetro, **10 %** por defecto (ajustado en pista).
- **Intensidad** según la velocidad de caída contra la superficie:

| Velocidad de caída | Efecto |
|---|---|
| Menos de 3 m/s | Nada |
| De 3 a 8 m/s | Sacudida de cámara **media** de la spec de barreras |
| 8 m/s o más | Sacudida **fuerte** |

- Se usa el mismo sistema de sacudida que en los impactos, con su regla de sustitución, y en cabina al 50 %.
- **Compresión visual**: la nave baja **0,3 m** y vuelve a su altura en **0,2 s**, en proporción a la intensidad. Solo visual, no cambia la física.
- La orientación vuelve a alinearse con la superficie en **0,15 s**, sin saltos.

### 3.5 Orientación en el suelo

- La nave se orienta con el marco de la superficie: cabecea con la pendiente y se ladea con el peralte.
- Encima se suma la inclinación visual por giro y derrape de la spec de física, como ahora.
- Los cambios de pendiente y de peralte ya son suaves por la interpolación (2.1). Si aun así se nota algún tirón en la orientación, se suaviza con un tiempo corto (parámetro, ~0,1 s).

## 4. Pendientes: frenar subiendo, acelerar bajando

- **Pendiente** = ángulo del eje respecto a la horizontal en el punto de la nave, en el sentido de la marcha. Positiva subiendo y negativa bajando.
- **Efecto**, proporcional a la pendiente y completo a la **pendiente de referencia de 10°** (con tope a partir de ahí):
  - Subiendo a 10°: la **velocidad punta** y la **aceleración** se multiplican por **0,7** (un 30 % menos).
  - Bajando a 10°: se multiplican por **1,3** (un 30 % más).
  - A 5°, la mitad del efecto: × 0,85 subiendo y × 1,15 bajando.
- Si al empezar una subida la nave va por encima de la nueva punta, **pierde el exceso de forma gradual**, con la misma regla que ya usa el daño (20 km/h por segundo), no de golpe. Igual al volver a llano tras una bajada en la que se había pasado de la punta normal.
- Sin pulsar W, la desaceleración natural tampoco cambia de reglas: subiendo, la nave baja hacia la punta reducida con la regla anterior; bajando, puede ganar velocidad hasta la punta aumentada **solo si se acelera con W**.
- El boost suma sus +100 km/h sobre la velocidad que haya, como siempre.
- El efecto se combina multiplicando con el del daño (punta × 0,92 o × 0,85).
- En el aire, la pendiente no tiene efecto (solo la gravedad, sección 3.3).

## 5. Peralte

### 5.1 Vista

- La nave se ladea con la pista (sección 3.5) y **toda la vista se ladea con ella**:
  - **Cabina**: la cámara va con la nave, así que la vista se ladea el peralte completo. La inclinación sutil de cabina de la spec de cámara se suma encima, como ahora.
  - **Exterior**: el "arriba" de la cámara sigue el "arriba" de la superficie bajo la nave, con un suavizado de **0,3 s**, así que el horizonte se ladea en los peraltes. En el aire, el "arriba" vuelve al del mundo con el mismo suavizado.
- **Factor de peralte en cámara de cabina**: parámetro, **1** (100 %) por defecto. Si en la prueba marea, se baja sin tocar la física. Va al objeto de ajustes de cámara, para el futuro SETTINGS.

### 5.2 Agarre

En curva, el peralte a favor del giro (el lado de fuera más alto) ayuda:

- **Peralte efectivo** = peralte × (1 si está a favor del giro, −1 si está en contra). "A favor" = la pista sube hacia el exterior de la curva que traza la nave en ese momento, según el sentido en que gira.
- **Agarre lateral** × (1 + **0,40** × peralte efectivo / 25°).
- **Velocidad de giro** × (1 + **0,15** × peralte efectivo / 25°).
- Tope de los multiplicadores: lo que salga a **35°**.
- Peralte **en contra** (contraperalte): los mismos factores con signo negativo, así que quita agarre y giro.
- Sin girar, el peralte no cambia nada: la nave no se desliza hacia el lado bajo.
- Se combina multiplicando con el agarre y el giro de derrape, contravolante, corrección por impacto fuerte y daño nivel 3.
- En el aire no hay agarre por peralte.

## 6. Geometría del circuito

- **Todo** lo que se dibuja del circuito se construye con el marco local (2.3), sin huecos ni solapes en el cierre de la vuelta:
  - Bordes, carriles, línea central y transversales: siguen la superficie y se ladean con ella.
  - **Barreras**: siguen el borde y sus verticales van a lo largo de N (perpendiculares a la superficie, ladeadas con el peralte), con la misma altura de 3 m. Destello igual.
  - **Meta, checkpoints, arcos y marcas de parrilla**: apoyados en la superficie y orientados con el marco en su punto.
- Las líneas se siguen generando como mallas de líneas agrupadas (nada de objetos sueltos) y solo al crear el circuito.
- La colisión con la barrera mide el desplazamiento lateral `d` **sobre la superficie** (2.3), no en planta. En llano es lo mismo que hoy.
- Si algún elemento (por ejemplo, un arco) queda mal en una pendiente o un peralte fuerte, **se avisa** antes de cambiar su diseño.

## 7. Otros sistemas

- **Vueltas, checkpoints y meta**: siguen contando por la distancia `s` sobre la vuelta. Cruzarlos en el aire cuenta igual.
- **Parrilla y salida lanzada**: la nave aparece apoyada en la superficie y alineada con ella. En TIME TRIAL, la salida lanzada sale con la velocidad de siempre a lo largo de la superficie.
- **Recolocación**: coloca la nave en el punto más cercano del eje, a la altura de vuelo, **en el suelo**, alineada con la superficie, con velocidad 0. Si estaba en el aire, se cancela el vuelo.
- **Atasco**: no cuenta mientras la nave está en el aire.
- **Daño**: aterrizar no daña (3.4). Los impactos contra la barrera, también en el aire, siguen sus reglas.
- **Fantasma**: graba posición y rotación en 3D, que ya están en el formato. El formato no cambia. Los fantasmas de los circuitos planos siguen siendo válidos.
- **Cámara**: vibración, efectos del boost e inclinación de cabina funcionan igual. La sacudida de aterrizaje usa la sacudida existente (3.4).
- **Pausa**: congela todo, también el vuelo, la compresión y los suavizados.
- **Selector de circuito**: el perfil de elevación y la miniatura ya leen la altura del eje. `MAX BANKING` pasa a mostrar el **peralte máximo real** (valor absoluto, en grados enteros). La miniatura no hace falta que muestre el peralte.
- **Menú principal**: sin cambios.

## 8. Circuito de pruebas TEST HILLS

- Nuevo `TrackDefinition` en `res://circuits/test_hills.tres`, con `Id = "test_hills"`, `DisplayName = "Test Hills"` e `IsTest = true`. En el catálogo va detrás de `test_oval`.
- **Planta**: un óvalo en sentido horario, con rectas de **600 m** y curvas de 180° de radio **150 m** (≈ 2.142 m). Meta en la mitad de la recta 1. 3 checkpoints, como en `test_oval`.
- Distancias desde la meta (aproximadas): recta 1 (segunda mitad) 0–300 m · curva 1 300–771 m · recta 2 771–1.371 m · curva 2 1.371–1.842 m · recta 1 (primera mitad) 1.842–2.142 m.
- **Recta 2, desniveles**, en este orden:
  1. Llano hasta ~820 m.
  2. **Subida** a ~8° hasta unos **+20 m**.
  3. **Cresta** con radio vertical de unos **600 m**: en ella se despega por encima de ~325 km/h con la gravedad de 14 m/s² (radio mínimo real de la interpolación: ~580 m).
  4. **Bajada** a ~8° hasta unos **−6 m**.
  5. **Hondonada** (compresión) y subida suave de vuelta a 0 antes de la curva 2.
- **Curva 1**: peralte de **25°** a favor, con entrada y salida de ~60 m.
- **Curva 2**: peralte de **12°** a favor, con las mismas transiciones.
- **Recta 1**: plana. Es la zona de parrilla y salida.
- Las claves exactas las pone quien implemente, siempre que se cumpla lo anterior y la validación (2.2). Si algo no encaja (por ejemplo, que la cresta no permita despegar a la velocidad dicha), **se avisa**.
- Funciona en RACE y TIME TRIAL, con récords y fantasma como cualquier otro circuito.

## 9. Parámetros en el Inspector

Valores iniciales para la primera prueba. Los definitivos son los que se ajusten en el Inspector probando en pista y luego pasan a ser los valores por defecto del código, como en las otras specs.

| Parámetro | Valor inicial | Dónde |
|---|---|---|
| Gravedad | 14 m/s² (v1.0: 6) | nave |
| Giro en el aire | 30 % | nave |
| Inclinación máxima del morro en el aire | 20° | nave |
| Suavizado de la orientación en el aire | 0,3 s | nave |
| Alineación al aterrizar | 0,15 s | nave |
| Pérdida de velocidad al aterrizar | 10 % (v1.0: 0 %) | nave |
| Umbrales de sacudida al aterrizar | 3 m/s (media) / 8 m/s (fuerte) | nave |
| Compresión visual al aterrizar | 0,3 m durante 0,2 s | nave |
| Suavizado de la orientación en el suelo | 0,1 s | nave |
| Pendiente de referencia | 10° | nave |
| Efecto de la pendiente en punta y aceleración | 30 % | nave |
| Agarre extra por peralte (a 25°) | 40 % | nave |
| Giro extra por peralte (a 25°) | 15 % | nave |
| Suavizado del "arriba" de la cámara exterior | 0,3 s | cámara |
| Factor de peralte en cámara de cabina | 1 | ajustes de cámara |
| Peralte máximo permitido (aviso) | 35° | circuito |
| Pendiente máxima permitida (aviso) | 15° | circuito |

## 10. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin dependencias de nodos de Godot (`System.Numerics` o tipos propios):

- **Perfil de la pista** (puro): claves de altura y de peralte → valor en cualquier `s`, con interpolación suave, cerrada y monótona; validación.
- **Marco de la pista** (puro): eje en planta + perfiles → posición, T, N y B en cualquier `s`; (`s`, `d`, `h`) ↔ posición; punto más cercano. Lo usan `Track`, la colisión, la recolocación, la miniatura y los datos del selector.
- **Capa vertical** (puro): estado (en el suelo / en el aire, altura, velocidad vertical) + `Update(delta, superficie)`. Regla de despegue, gravedad, aterrizaje y velocidad de caída.
- **Efecto de la pendiente** (puro): pendiente → multiplicadores de punta y aceleración.
- **Efecto del peralte** (puro): peralte y sentido de giro → multiplicadores de agarre y giro.
- **Orientación** (puro): marco de la superficie, estado vertical y velocidad → orientación objetivo, y su suavizado.
- **Aterrizaje** (puro): velocidad de caída → nada / media / fuerte, e intensidad de la compresión.
- **`Player`**: aplica todo lo anterior junto a la dinámica actual. Sin reglas dentro.
- **`Track`** (y sus constructores): genera toda la geometría con el marco.
- **Cámaras**: la exterior lee el "arriba" de la superficie; la de cabina va con la nave. No conocen la física.

### Pruebas unitarias mínimas

Perfil y marco:
1. Sin claves: altura y peralte 0 en toda la vuelta, y el marco es el de hoy (N hacia arriba).
2. Interpolación entre (0, 0) y (100, 10): monótona, sin pasarse de 10, continua al cerrar la vuelta.
3. Con un peralte de 20° en un punto, N forma 20° con la vertical y el borde derecho queda más alto cuando el peralte es positivo.
4. Ida y vuelta: (`s`, `d`, `h`) → posición → punto más cercano devuelve los mismos valores (con tolerancia), con pendiente y con peralte.
5. Validación: un peralte de 40° o una pendiente de 20° dan aviso; las claves desordenadas se ignoran.
6. La firma de `circuit_01` y la de `test_oval` no cambian. Un circuito con peralte tiene una firma distinta a la del mismo circuito sin peralte.

Capa vertical:
7. En llano: siempre en el suelo, a la altura de vuelo, sin velocidad vertical.
8. Cresta de radio 600 m: a 200 km/h no despega; a 230 km/h despega.
9. En el aire, la altura sigue una parábola con la gravedad dada (las pruebas 8 y 9 usan 6 m/s²) y la velocidad horizontal se conserva.
10. Hondonada: nunca despega y nunca queda por debajo de la superficie.
11. Aterrizaje a 2, 5 y 10 m/s de caída → nada, media y fuerte. Tras aterrizar, la velocidad contra la superficie es 0 y la paralela se conserva. Sin daño.
12. En el aire: W y S no cambian la velocidad, el giro es el 30 %, no se entra en derrape y el boost suma a la longitudinal.

Pendiente y peralte:
13. Pendiente 0 → × 1. Subiendo a 10° → × 0,7. Bajando a 10° → × 1,3. A 5° → × 0,85 / × 1,15. A 20° → igual que a 10° (tope).
14. Al pasar de llano a subida por encima de la nueva punta, el exceso se pierde a 20 km/h por segundo.
15. Peralte de 25° a favor del giro → agarre × 1,4 y giro × 1,15. En contra → × 0,6 y × 0,85. Sin girar → × 1. En el aire → × 1.
16. Combinación con el daño nivel 3: giro × 0,85 × el factor del peralte.

Otros:
17. La recolocación desde el aire deja la nave en el suelo, alineada y a 0 km/h.
18. Con la nave en el aire, el detector de atasco no cuenta tiempo.

Las pruebas existentes siguen pasando sin cambiarlas, en especial las de la dinámica de la nave, las barreras y el fantasma.

## 11. Prueba manual

1. **Regresión en llano**: en CIRCUIT 01 y TEST OVAL, la conducción, el derrape, las barreras, el boost y las cámaras se sienten exactamente igual que antes. Los récords y fantasmas guardados siguen ahí y siguen siendo válidos.
2. **TEST HILLS**, recta 2 despacio (menos de 150 km/h): la nave sube, pasa la cresta pegada y baja. Al subir pierde velocidad y al bajar la gana.
3. Misma recta a fondo (por encima de ~325 km/h en la cresta): la nave **despega**, vuela un poco, el morro sigue la trayectoria y aterriza con sacudida. Sin daño (comprobar el indicador). Con boost, salta más lejos.
4. En el aire: W y S no hacen nada, A/D corrigen poco y Espacio no inicia derrape.
5. Hondonada a fondo: la nave se aplasta contra la pista y no salta.
6. **Curva 1 (25°)**: la nave y la vista se ladean, en cabina y en exterior. Se puede entrar más rápido que en una curva plana del mismo radio (comparar con TEST OVAL a la misma velocidad). Comprobar que no marea; si marea, bajar el factor de peralte de cabina.
7. **Curva 2 (12°)**: el efecto es menor.
8. Derrapar en las curvas peraltadas: sigue funcionando y se combina con el agarre del peralte.
9. Chocar contra la barrera en la curva peraltada y en la cresta, también en el aire: la barrera está bien colocada y la nave nunca sale del circuito.
10. Quedarse atascado en la curva peraltada: la recolocación deja la nave alineada con la pista.
11. Toda la geometría (bordes, barreras, meta, checkpoints, parrilla y arcos) sigue la superficie, sin huecos ni piezas flotando o enterradas.
12. RACE completa y TIME TRIAL con fantasma en TEST HILLS: el fantasma sube, baja, salta y se ladea igual que tu vuelta.
13. Selector de circuito: TEST HILLS muestra su perfil de elevación con la subida, la cresta y la bajada, y `MAX BANKING 25°`.
14. Pausa en mitad de un salto: todo congelado; al continuar, sigue.
15. Comprobar que los valores ajustados en el Inspector siguen ahí tras cerrar y abrir el proyecto.

## 12. Fuera de alcance

- Túneles (siguiente spec).
- El circuito definitivo con todo esto (spec propia).
- Daño por aterrizaje.
- Que el peralte empuje la nave hacia el lado bajo sin girar.
- Caídas fuera de la pista, huecos o tramos sin barrera.
- Loops, tramos invertidos o paredes verticales.
- Peralte en la miniatura del selector.
- Partículas o efectos al despegar o aterrizar.
- Sonido y mando.
