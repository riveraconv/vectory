# VECTORY — Spec: Conducción avanzada (física)

Versión 1.1 · 2026-11-02 · Estado: **definido; prueba de controles hecha (se queda el modo B)**

Añade a la conducción la **inclinación de la nave en curva** y el **derrape**, sin ayudas automáticas. Es la primera de dos specs: esta cubre solo la **física** (y la inclinación visual de la nave). La **cámara** (inclinación en curvas, vibración a alta velocidad, reacción al boost) va en una spec aparte, después.

**Cambios en v1.1** (tras la prueba de controles):
- Se queda el **modo B (tecla dedicada)**: derrape con **Espacio** + A/D y boost en **Shift**. El modo A (frenado contextual, S + giro) se probó y se quitó, con su parámetro de frenada en derrape y el selector de modo.
- Valores del derrape y de la inclinación ajustados en la prueba (sección 8).
- Decisiones de la implementación: el tope del ángulo solo se aplica en derrape y mientras se recupera el agarre (fuera de eso la conducción y los rebotes son los de antes); tras un impacto medio o fuerte, para volver a derrapar hay que soltar Espacio o A/D; la cabina no se inclina.

Se apoya en lo que ya existe: `Player` / `Spaceship` (conducción básica: aceleración, frenado, giro progresivo, desaceleración, inercia lateral), la lógica del boost, la colisión con barreras (`Track`, nivel de impacto, recolocación), el modelo de daño, `RaceSession` / `RaceTracker`, el fantasma (grabador) y `GameSession`.

Cambios respecto al documento maestro y a specs anteriores:
- La **recarga del boost sigue siendo solo por tiempo**: derrapar o trazar bien **no** da cargas. El boost es una ayuda puntual, no una mecánica de la que depender todo el rato.
- El **boost deja la barra espaciadora** y pasa a **Shift** (ver 3).

Rama: `feature/advanced-driving` (sale de `develop`, con `feature/damage` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- La nave distingue entre **hacia dónde apunta el morro** (rumbo) y **hacia dónde se mueve** (dirección de movimiento). El ángulo entre las dos es el **ángulo de deslizamiento**.
- **Conducción normal**: agarre alto, como ahora. El deslizamiento es pequeño y se corrige solo por el agarre (inercia lateral moderada de la demo).
- **Derrape**: a partir de cierta velocidad y girando, se pierde agarre lateral y el morro gira más rápido. La nave se cruza y desliza; hay que **sostenerlo y corregirlo** con A/D. Pierde algo de velocidad.
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
- **Máximo: 60°**. Si se pasara, se recorta la velocidad lateral para quedarse en 60° (la nave no puede ir de lado ni hacia atrás).
- Por debajo de 20 km/h el ángulo se considera 0 (evita giros raros casi parado).

## 3. Entrar en derrape: tecla dedicada (Espacio)

- **Espacio** es la acción `drift`.
- El **boost** es la acción `boost` en **Shift**. Las teclas se cambian en el Input Map (Project Settings) sin tocar código.
- Se entra en derrape cuando: **Espacio** pulsado + **A o D** pulsada + velocidad de **120 km/h o más**.
- Espacio **no frena**. S sigue frenando como siempre (también durante el derrape, al 100 %).
- Espacio solo (sin girar) no hace nada.
- El texto de las specs anteriores que dice "Espacio" para el boost se entiende como "la tecla de boost" (Shift).

## 4. Durante el derrape

| Regla | Valor |
|---|---|
| Agarre lateral | **0 %** del normal |
| Velocidad de giro del rumbo | **× 1,4** |
| Pérdida fija por derrapar | **5 km/h por s** |
| Pérdida extra por deslizamiento | hasta **10 km/h por s**, proporcional al ángulo (0 a 0°, el máximo a 60°) |
| Aceleración con W | **50 %** de la normal |

- El morro gira más rápido que la dirección de movimiento: la nave **se cruza** hacia dentro de la curva y desliza hacia fuera.
- **Contravolante**: girando hacia el lado contrario al deslizamiento, el agarre lateral sube al **50 %** mientras se contravolantea (el deslizamiento se corrige más rápido). Así se controla el ángulo.
- El giro del derrape se multiplica por los factores que ya existen: corrección temporal de impacto fuerte (barreras) y daño nivel 3 (giro × 0,85).
- El derrape **puede durar lo que se quiera** mientras se mantengan las condiciones: no hay tiempo máximo. Lo limita la pérdida de velocidad.

## 5. Salir del derrape

- Se sale cuando deja de cumplirse la entrada (se suelta Espacio o A/D) o cuando la velocidad baja de **60 km/h**.
- Para volver a entrar hay que volver a cumplir la entrada completa (incluida la velocidad de 120 km/h).
- Al salir, el agarre vuelve del de derrape al **100 %** de forma lineal en **3 s** (desde el agarre que tuviera al salir, al mismo ritmo). El deslizamiento que quede se va corrigiendo con ese agarre: **no** se endereza de golpe y **no** se gira el morro solo.
- Ese tiempo de recuperación es el riesgo del derrape: si se sale mal cruzado, se va hacia la barrera.

## 6. Interacción con otros sistemas

### 6.1 Boost

- Se puede activar durante un derrape.
- El impulso se suma a la **velocidad longitudinal** (en la dirección del morro), no a la dirección de movimiento. Bien usado, ayuda a salir del derrape.
- Un derrape no cancela el boost. Frenar con S durante el derrape **sí** cancela el impulso, como cualquier frenada (spec del boost).
- La recarga del boost no cambia: solo por tiempo.

### 6.2 Barreras

- La colisión usa la **velocidad real** (dirección de movimiento), no el rumbo, para calcular la velocidad perpendicular y el nivel de impacto. Si hoy usa el rumbo, se cambia.
- **Roce**: el derrape sigue.
- **Medio o fuerte**: el derrape termina al momento y el agarre empieza su recuperación. Para volver a derrapar hay que soltar Espacio o A/D.
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
- **Máximo en conducción normal: 25°**. **En derrape: 20°**.
- Llega al objetivo con suavizado (constante de tiempo **0,15 s**): sin saltos al pulsar o soltar A/D.
- Solo **visual**: se aplica a la geometría de la nave, no al rumbo ni a la física ni a la colisión.
- Se ve en la **cámara exterior**. En **cabina**, la cámara y el marco **no** se inclinan en esta spec (va en la spec de cámara).
- La nave del menú principal no se inclina (está quieta).
- Si para inclinar solo la geometría hay que tocar cómo está montada `Spaceship` (nodos o pivote), **se avisa antes**.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Velocidad mínima para entrar en derrape | 120 km/h | nave |
| Velocidad a la que se sale del derrape | 60 km/h | nave |
| Agarre lateral en derrape | 0 % | nave |
| Agarre lateral contravolanteando | 50 % | nave |
| Multiplicador de giro en derrape | 1,4 | nave |
| Pérdida fija en derrape | 5 km/h por s | nave |
| Pérdida extra máxima por deslizamiento | 10 km/h por s | nave |
| Aceleración en derrape | 50 % | nave |
| Ángulo de deslizamiento máximo | 60° | nave |
| Tiempo de recuperación del agarre | 3 s | nave |
| Inclinación máxima normal / en derrape | 25° / 20° | nave |
| Suavizado de la inclinación | 0,15 s | nave |

- Valores ajustados en la prueba de controles (v1.1). Usar la misma conversión km/h ↔ unidades de Godot que ya usa el HUD.
- Los parámetros de conducción básica que ya existen (aceleración, punta, frenado, giro, desaceleración, inercia lateral) **no cambian**.

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin dependencias de nodos de Godot (tipos propios o `System.Numerics`):

- **Dinámica de la nave** (puro): estado (rumbo, velocidad longitudinal y lateral, en derrape sí/no, recuperación del agarre) + `Update(delta, entrada)`. La entrada son valores ya traducidos (acelerar, frenar, girar −1/0/1, tecla de derrape) más los factores externos (punta, giro, control). Devuelve el estado nuevo. Si la conducción básica ya está en una clase pura, se amplía; si está dentro de `Player`, se saca aquí **avisando antes** y sin cambiar cómo se siente la conducción normal.
- **Regla de entrada en derrape** (puro): dadas las teclas y la velocidad, si se entra / sigue / sale.
- **Inclinación** (puro): objetivo según giro, velocidad y derrape, y suavizado con `delta`.
- **`Player`**: traduce input a la entrada de la dinámica, aplica el resultado y la inclinación a la geometría. No contiene reglas de derrape.
- **Colisión**: recibe la velocidad real y avisa de impactos medio/fuerte para terminar el derrape.

### Pruebas unitarias mínimas

Normal:
1. Sin derrape, con los mismos parámetros y entradas, la dinámica da el mismo resultado que la conducción actual (o, si no se puede comparar directo, la velocidad lateral decae con el agarre normal).
2. Por debajo de 20 km/h el ángulo de deslizamiento es 0.

Entrada y salida:
3. Espacio + D a 150 km/h → derrape. Espacio + D a 100 km/h → no. Espacio solo → no.
4. En derrape, S frena al 100 %.
5. Soltar D → sale; el agarre sube de forma lineal desde el de derrape hasta el 100 % en el tiempo de recuperación.
6. Bajar de 60 km/h en derrape → sale.
7. Tras un impacto medio o fuerte, con Espacio y D aún pulsados, no se vuelve a entrar hasta soltar uno de los dos.

Durante el derrape:
8. El ángulo de deslizamiento nunca pasa de 60°.
9. Pérdida de velocidad: a ángulo 0 → 5 km/h por s; a 60° → 15 km/h por s.
10. Contravolante: el agarre sube al 50 % y el ángulo baja más rápido que sin contravolante.
11. El giro en derrape es 1,4× el normal, y × 0,85 con daño nivel 3.

Otros sistemas:
12. Boost en derrape suma a la velocidad longitudinal.
13. Impacto medio o fuerte termina el derrape; roce no.
14. Recolocación → sin derrape, deslizamiento 0, agarre 100 %.
15. Sin control (Terminada) no se puede entrar en derrape.

Inclinación:
16. Sin girar → objetivo 0. Giro máximo a velocidad punta → 25°; en derrape → 20°.
17. Tras un cambio de objetivo, la inclinación se acerca de forma suave (no llega en un frame).

Las pruebas actuales siguen pasando.

## 10. Prueba manual

1. Conducir sin derrapar unas vueltas: la conducción normal se siente **igual que antes**. Si algo cambia, es un fallo.
2. Cámara exterior (C), girar en recta a distintas velocidades: la nave se ladea suave, más cuanto más rápido. En cabina, la vista no se inclina.
3. Entrar a una curva a alta velocidad y derrapar: la nave se cruza hacia dentro, desliza hacia fuera y pierde algo de velocidad. Se ladea más.
4. Sostener el derrape corrigiendo con A/D (contravolante): se puede controlar el ángulo y trazar la curva.
5. Salir del derrape mal cruzado: el agarre vuelve poco a poco (3 s); no se endereza solo y es fácil acabar en la barrera si no se corrige.
6. Intentar derrapar a poca velocidad (< 120 km/h): no entra.
7. Derrapar contra la barrera: un roce no corta el derrape; un golpe medio o fuerte sí.
8. Boost en mitad del derrape: impulso hacia donde apunta el morro.
9. Boost en Shift, derrape en Espacio, y S frena siempre igual (también derrapando).
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
- Volver al derrape con S + giro (modo A, descartado en la prueba) o elegir el modo desde un menú.
