# VECTORY — Spec: Conducción avanzada (física)

Versión 1.0 · 2026-11-02 · Estado: **definido, con prueba de controles pendiente**

Añade a la conducción la **inclinación de la nave en curva**, el **derrape** y el **frenado contextual**, sin ayudas automáticas. Es la primera de dos specs: esta cubre solo la **física** (y la inclinación visual de la nave). La **cámara** (inclinación en curvas, vibración a alta velocidad, reacción al boost) va en una spec aparte, después.

**Cómo se derrapa todavía no está decidido.** Esta spec implementa **dos modos de control** que se cambian desde el Inspector para probarlos y elegir (sección 3). Después de la prueba se elige uno y el otro se quita en un commit aparte (o se deja como opción si se decide así).

Se apoya en lo que ya existe: `Player` / `Spaceship` (conducción básica: aceleración, frenado, giro progresivo, desaceleración, inercia lateral), la lógica del boost, la colisión con barreras (`Track`, nivel de impacto, recolocación), el modelo de daño, `RaceSession` / `RaceTracker`, el fantasma (grabador) y `GameSession`.

Cambios respecto al documento maestro y a specs anteriores:
- La **recarga del boost sigue siendo solo por tiempo**: derrapar o trazar bien **no** da cargas. El boost es una ayuda puntual, no una mecánica de la que depender todo el rato.
- En el modo de control B, el **boost deja la barra espaciadora** (ver 3.2).

Rama: `feature/advanced-driving` (sale de `develop`, con `feature/damage` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- La nave distingue entre **hacia dónde apunta el morro** (rumbo) y **hacia dónde se mueve** (dirección de movimiento). El ángulo entre las dos es el **ángulo de deslizamiento**.
- **Conducción normal**: agarre alto, como ahora. El deslizamiento es pequeño y se corrige solo por el agarre (inercia lateral moderada de la demo).
- **Derrape**: a partir de cierta velocidad y girando, se pierde agarre lateral y el morro gira más rápido. La nave se cruza y desliza; hay que **sostenerlo y corregirlo** con A/D. Pierde algo de velocidad.
- **Frenado contextual**: frenar mientras se gira a alta velocidad provoca un derrape controlado (es la forma de entrar en derrape en el modo A).
- **Inclinación**: la nave se ladea al girar, más cuanto más gira y más rápido va, y algo más en derrape. Solo visual; no cambia la física.
- **Sin ayudas**: nada de enderezar el morro solo, corregir la trazada ni frenar automáticamente. Al salir del derrape vuelve el agarre, pero el rumbo lo pone el jugador.
- Mismas reglas en RACE y TIME TRIAL y para todas las naves.

---

## 2. Modelo de movimiento

### 2.1 Rumbo, movimiento y agarre

- La nave guarda su **rumbo** y su **vector de velocidad** por separado (si ya lo hace para la inercia lateral, se reutiliza).
- La velocidad se divide en dos partes respecto al rumbo:
  - **Longitudinal**: la que va en la dirección del morro. Es la que suben W y el boost y la que bajan S y la desaceleración.
  - **Lateral**: la perpendicular al morro (lo que la nave desliza).
- El **agarre** reduce la velocidad lateral cada frame, pasando una parte a longitudinal y perdiendo el resto (de forma independiente del framerate, con decaimiento exponencial).
- **Agarre normal**: el que ya da la inercia lateral actual. La conducción normal debe sentirse **igual que ahora**: si se nota distinta sin derrapar, es un fallo.

### 2.2 Ángulo de deslizamiento

- Ángulo entre rumbo y dirección de movimiento, con signo.
- **Máximo: 35°**. Si se pasara, se recorta la velocidad lateral para quedarse en 35° (la nave no puede ir de lado ni hacia atrás).
- Por debajo de 20 km/h el ángulo se considera 0 (evita giros raros casi parado).

## 3. Entrar en derrape: dos modos de control

Parámetro en el Inspector **Modo de derrape**: `BrakeTurn` (A) o `DedicatedKey` (B). Por defecto, **A**. Todo lo demás (agarre, pérdidas, salida, inclinación) es igual en los dos modos.

### 3.1 Modo A · Frenado contextual (S + giro)

- Se entra en derrape cuando, a la vez:
  - **S** está pulsada,
  - **A o D** está pulsada, y
  - la velocidad es de **120 km/h o más**.
- Mientras se derrapa, S **frena al 50 %** de su fuerza normal (frena, pero deja deslizar).
- Si se suelta A/D manteniendo S, se sale del derrape y S vuelve a frenar al 100 %.
- Con S pulsada **sin girar**, o por debajo de 120 km/h, S es frenado normal, como ahora.
- El boost sigue en **Espacio**.

### 3.2 Modo B · Tecla dedicada (Espacio)

- **Espacio** pasa a ser la acción nueva `drift`.
- El **boost** pasa a la acción `boost` en **Shift**. Las teclas se cambian en el Input Map (Project Settings) sin tocar código, para poder probar también Q, E o R.
- En specs anteriores Shift aparece entre las teclas que controlan la nave. **Si Shift ya tiene una función, se avisa antes** y se propone otra tecla para el boost en la prueba (E por defecto).
- Se entra en derrape cuando: **Espacio** pulsado + **A o D** pulsada + velocidad de **120 km/h o más**.
- Espacio **no frena**. S sigue frenando como siempre (también durante el derrape, al 100 %).
- Espacio solo (sin girar) no hace nada.

### 3.3 Cambio de modo

- El modo se lee al cargar la escena de carrera; no hace falta poder cambiarlo en marcha.
- El cambio de teclas del boost entre modos se gestiona en un sitio único (no repartido por el código): en modo A, `boost` = Espacio; en modo B, `drift` = Espacio y `boost` = Shift.
- El texto de las specs anteriores que dice "Espacio" para el boost se entiende como "la tecla de boost" (prueba manual, previa, pausa…).

## 4. Durante el derrape

| Regla | Valor |
|---|---|
| Agarre lateral | **35 %** del normal |
| Velocidad de giro del rumbo | **× 1,4** |
| Pérdida fija por derrapar | **15 km/h por s** |
| Pérdida extra por deslizamiento | hasta **30 km/h por s**, proporcional al ángulo (0 a 0°, el máximo a 35°) |
| Aceleración con W | **70 %** de la normal |

- El morro gira más rápido que la dirección de movimiento: la nave **se cruza** hacia dentro de la curva y desliza hacia fuera.
- **Contravolante**: girando hacia el lado contrario al deslizamiento, el agarre lateral sube al **70 %** mientras se contravolantea (el deslizamiento se corrige más rápido). Así se controla el ángulo.
- El giro del derrape se multiplica por los factores que ya existen: corrección temporal de impacto fuerte (barreras) y daño nivel 3 (giro × 0,85).
- El derrape **puede durar lo que se quiera** mientras se mantengan las condiciones: no hay tiempo máximo. Lo limita la pérdida de velocidad.

## 5. Salir del derrape

- Se sale cuando deja de cumplirse la entrada (se suelta S/Espacio o A/D) o cuando la velocidad baja de **80 km/h**.
- Para volver a entrar hay que volver a cumplir la entrada completa (incluida la velocidad de 120 km/h).
- Al salir, el agarre vuelve del 35 % al **100 %** de forma lineal en **0,4 s**. El deslizamiento que quede se va corrigiendo con ese agarre: **no** se endereza de golpe y **no** se gira el morro solo.
- Ese tiempo de recuperación es el riesgo del derrape: si se sale mal cruzado, se va hacia la barrera.

## 6. Interacción con otros sistemas

### 6.1 Boost

- Se puede activar durante un derrape.
- El impulso se suma a la **velocidad longitudinal** (en la dirección del morro), no a la dirección de movimiento. Bien usado, ayuda a salir del derrape.
- Un derrape no cancela el boost. Frenar con S en modo A durante el derrape **sí** cancela el impulso, como cualquier frenada (spec del boost).
- La recarga del boost no cambia: solo por tiempo.

### 6.2 Barreras

- La colisión usa la **velocidad real** (dirección de movimiento), no el rumbo, para calcular la velocidad perpendicular y el nivel de impacto. Si hoy usa el rumbo, se cambia.
- **Roce**: el derrape sigue.
- **Medio o fuerte**: el derrape termina al momento y el agarre empieza su recuperación de 0,4 s.
- **Recolocación**: termina el derrape, deslizamiento 0, agarre al 100 % al momento.

### 6.3 Daños y estados de la carrera

- El daño no cambia el derrape salvo lo que ya hace con el giro (nivel 3) y la punta (niveles 2 y 3).
- Solo se puede derrapar cuando la nave tiene control (**En carrera**, Time Trial tras el `GO!`). En **Terminada** no hay input: si se estaba derrapando, se sale del derrape y el agarre se recupera normal.
- La pausa lo congela todo, incluida la recuperación del agarre.

### 6.4 Fantasma

- El fantasma ya graba posición y rotación: reproduce el derrape (la nave cruzada) sin cambios.
- La inclinación (sección 7) **se incluye** en la rotación que se graba, para que el fantasma también se ladee. El formato del archivo no cambia (sigue siendo un cuaternión). Los fantasmas grabados antes siguen siendo válidos (simplemente no se ladean).

## 7. Inclinación de la nave

- La nave se **ladea** (roll) hacia el interior de la curva al girar.
- Inclinación objetivo = máximo × (giro actual / giro máximo) × (velocidad / velocidad punta, con tope en 1).
- **Máximo en conducción normal: 12°**. **En derrape: 20°**.
- Llega al objetivo con suavizado (constante de tiempo **0,15 s**): sin saltos al pulsar o soltar A/D.
- Solo **visual**: se aplica a la geometría de la nave, no al rumbo ni a la física ni a la colisión.
- Se ve en la **cámara exterior**. En **cabina**, la cámara y el marco **no** se inclinan en esta spec (va en la spec de cámara).
- La nave del menú principal no se inclina (está quieta).
- Si para inclinar solo la geometría hay que tocar cómo está montada `Spaceship` (nodos o pivote), **se avisa antes**.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Modo de derrape | A (`BrakeTurn`) | nave |
| Velocidad mínima para entrar en derrape | 120 km/h | nave |
| Velocidad a la que se sale del derrape | 80 km/h | nave |
| Agarre lateral en derrape | 35 % | nave |
| Agarre lateral contravolanteando | 70 % | nave |
| Multiplicador de giro en derrape | 1,4 | nave |
| Pérdida fija en derrape | 15 km/h por s | nave |
| Pérdida extra máxima por deslizamiento | 30 km/h por s | nave |
| Aceleración en derrape | 70 % | nave |
| Fuerza de frenado en derrape (modo A) | 50 % | nave |
| Ángulo de deslizamiento máximo | 35° | nave |
| Tiempo de recuperación del agarre | 0,4 s | nave |
| Inclinación máxima normal / en derrape | 12° / 20° | nave |
| Suavizado de la inclinación | 0,15 s | nave |

- Los valores son iniciales: están para ajustarse en la prueba. Usar la misma conversión km/h ↔ unidades de Godot que ya usa el HUD.
- Los parámetros de conducción básica que ya existen (aceleración, punta, frenado, giro, desaceleración, inercia lateral) **no cambian**.

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin dependencias de nodos de Godot (tipos propios o `System.Numerics`):

- **Dinámica de la nave** (puro): estado (rumbo, velocidad longitudinal y lateral, en derrape sí/no, recuperación del agarre) + `Update(delta, entrada)`. La entrada son valores ya traducidos (acelerar, frenar, girar −1/0/1, botón de derrape) más los factores externos (punta, giro, control). Devuelve el estado nuevo. Si la conducción básica ya está en una clase pura, se amplía; si está dentro de `Player`, se saca aquí **avisando antes** y sin cambiar cómo se siente la conducción normal.
- **Regla de entrada en derrape** (puro): dado el modo, las teclas y la velocidad, si se entra / sigue / sale.
- **Inclinación** (puro): objetivo según giro, velocidad y derrape, y suavizado con `delta`.
- **Mapeo de teclas por modo**: un sitio único que, según el modo, decide qué acción es boost y cuál derrape.
- **`Player`**: traduce input a la entrada de la dinámica, aplica el resultado y la inclinación a la geometría. No contiene reglas de derrape.
- **Colisión**: recibe la velocidad real y avisa de impactos medio/fuerte para terminar el derrape.

### Pruebas unitarias mínimas

Normal:
1. Sin derrape, con los mismos parámetros y entradas, la dinámica da el mismo resultado que la conducción actual (o, si no se puede comparar directo, la velocidad lateral decae con el agarre normal).
2. Por debajo de 20 km/h el ángulo de deslizamiento es 0.

Entrada y salida (modo A):
3. S + D a 150 km/h → derrape. S + D a 100 km/h → no. S sin girar → no (frenado normal).
4. En derrape, S frena al 50 %.
5. Soltar D → sale; el agarre está al 35 % justo al salir, ~67,5 % a los 0,2 s y 100 % a los 0,4 s.
6. Bajar de 80 km/h en derrape → sale.

Entrada (modo B):
7. Espacio + A a 150 km/h → derrape. Espacio solo → no. En derrape, S frena al 100 %.

Durante el derrape:
8. El ángulo de deslizamiento nunca pasa de 35°.
9. Pérdida de velocidad: a ángulo 0 → 15 km/h por s; a 35° → 45 km/h por s.
10. Contravolante: el agarre sube al 70 % y el ángulo baja más rápido que sin contravolante.
11. El giro en derrape es 1,4× el normal, y × 0,85 con daño nivel 3.

Otros sistemas:
12. Boost en derrape suma a la velocidad longitudinal.
13. Impacto medio o fuerte termina el derrape; roce no.
14. Recolocación → sin derrape, deslizamiento 0, agarre 100 %.
15. Sin control (Terminada) no se puede entrar en derrape.

Inclinación:
16. Sin girar → objetivo 0. Giro máximo a velocidad punta → 12°; en derrape → 20°.
17. Tras un cambio de objetivo, la inclinación se acerca de forma suave (no llega en un frame).

Las pruebas actuales siguen pasando.

## 10. Prueba manual

**Prueba de controles** (lo importante de esta spec): hacer las pruebas 3–9 primero en modo A y luego en modo B, en el mismo circuito, y decidir qué modo se queda.

1. Conducir sin derrapar unas vueltas: la conducción normal se siente **igual que antes**. Si algo cambia, es un fallo.
2. Cámara exterior (C), girar en recta a distintas velocidades: la nave se ladea suave, más cuanto más rápido. En cabina, la vista no se inclina.
3. Entrar a una curva a alta velocidad y derrapar: la nave se cruza hacia dentro, desliza hacia fuera y pierde algo de velocidad. Se ladea más.
4. Sostener el derrape corrigiendo con A/D (contravolante): se puede controlar el ángulo y trazar la curva.
5. Salir del derrape mal cruzado: el agarre vuelve en poco menos de medio segundo; no se endereza solo y es fácil acabar en la barrera si no se corrige.
6. Intentar derrapar a poca velocidad (< 120 km/h): no entra.
7. Derrapar contra la barrera: un roce no corta el derrape; un golpe medio o fuerte sí.
8. Boost en mitad del derrape: impulso hacia donde apunta el morro.
9. En modo A, frenar en recta (sin girar): frenado normal, sin derrape. En modo B, boost en Shift (o la tecla que se use), derrape en Espacio, y S frena siempre igual.
10. Con daño nivel 3 (RACE): el derrape gira algo menos.
11. TIME TRIAL: hacer un récord derrapando; en la vuelta siguiente el fantasma también se cruza y se ladea.
12. Pausar en mitad de un derrape: todo congelado; al continuar sigue igual.
13. Terminar una carrera derrapando: la nave sale del derrape y se va parando sola.

## 11. Fuera de alcance

- Cámara: inclinación en curvas, vibración a alta velocidad, reacción al boost, inclinación del marco de la cabina (siguiente spec).
- Recarga del boost por derrapes, trazadas o conducción limpia.
- Indicador de derrape en el HUD, puntuación de derrapes.
- Partículas, estela, chispas, sonido.
- Peraltes, desniveles y su efecto en el agarre.
- Valores distintos por nave.
- Ayudas a la conducción de cualquier tipo.
- Mando.
- Elegir el modo de derrape desde un menú (si se decide dejar los dos, irá en SETTINGS).
