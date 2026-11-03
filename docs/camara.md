# VECTORY — Spec: Cámara (conducción avanzada, parte 2)

Versión 1.1 · 2026-11-02 · Estado: **implementada y ajustada en pista**

Segunda parte de la conducción avanzada. La primera (`specs/conduccion-avanzada.md`) cubre la física, el derrape y la inclinación de la nave. Esta añade tres efectos de cámara: la **inclinación de la cabina en curva**, la **vibración a alta velocidad** y la **reacción al boost** (FOV y empuje hacia atrás).

Todos los efectos son **solo visuales**. No cambian el giro, el rumbo, la física, la colisión ni el control. A/D gira exactamente igual que antes.

Se apoya en lo que ya existe: `Player` / `Spaceship`, la `Cockpit`, las dos cámaras (cabina y exterior, que se cambian con C), la inclinación pura de la spec de física, la lógica del boost, `RaceSession` y la pausa.

Rama: `feature/camera`, que sale de `develop` con `feature/advanced-driving` ya mergeada (incluida su tarea de cierre).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

| Efecto | Cabina | Exterior |
|---|---|---|
| Inclinación en curva | Sí. Toda la vista (cámara y marco de la cabina juntos), **muy sutil** | No. La nave ya se ladea; la cámara no cambia |
| Vibración a alta velocidad | Sí, sutil | Sí, sutil |
| Boost: FOV más abierto | Sí | Sí |
| Boost: empuje hacia atrás | Sí, pequeño | Sí |

- Prioridad: **no marear**. Una versión anterior de la inclinación resultaba frenética y mareaba un poco. Esta tiene que ser lenta, pequeña y suave.
- La cámara exterior en derrape **no cambia**: se queda como está ahora, porque ya se ve bien.
- Los valores de los efectos van en un **objeto de ajustes de cámara**. Ahora se tocan en el Inspector; más adelante los ajustará el menú SETTINGS (sección 6).

## 2. Inclinación de la cabina

- En cabina se ladean **juntos la cámara y el marco de la cabina**, como si fueras dentro de la nave. El marco no se queda quieto mientras el mundo gira.
- Sigue la inclinación de la nave de la spec de física, pero mucho más reducida:
  - Inclinación objetivo de la cabina = inclinación objetivo de la nave × **factor de cabina**, con un tope máximo propio.
  - El suavizado es **más lento** que el de la nave, sin saltos al pulsar o soltar A/D.
- Si el factor es 0, la cabina no se inclina nada (es el valor que usará la opción de desactivarla en SETTINGS).
- Se aplica a un pivote que contiene la cámara y la cabina. Ni la nave ni su física se tocan.
- La inclinación visual de la nave en la cámara exterior queda igual que en la spec de física.
- Al cambiar de cámara con C, la inclinación de la cabina sigue calculándose, así que al volver a cabina no hay salto.

## 3. Vibración a alta velocidad

- Es un pequeño temblor de **posición** de la cámara. No rota la cámara, para no marear.
- Empieza a partir de un **umbral de velocidad** (porcentaje de la velocidad punta) y crece hasta su máximo al llegar a la punta.
- Durante el boost la amplitud se multiplica por un **multiplicador de boost**.
- Tiene una amplitud máxima distinta en cabina y en exterior.
- El temblor sale de una suma de senos de frecuencias distintas, en función del tiempo. Así es determinista y se puede probar en los tests, sin aleatorios.
- La intensidad sube y baja con suavizado. Al soltar el boost o frenar no se corta de golpe.
- Con amplitud 0 no hay vibración.

Es el primer "efecto" del juego, pero va aquí porque es un comportamiento de la **cámara**, no un efecto visual de la pista (partículas, estelas o líneas de velocidad, que irán en su propia spec). Si no convence al probarlo, se deja la amplitud a 0 y no se pierde nada.

## 4. Reacción al boost

Mientras el boost está activo:

- **FOV**: se abre unos grados sobre el FOV base de cada cámara.
- **Empuje hacia atrás**: la cámara se desplaza un poco hacia atrás respecto a su posición normal. En cabina el desplazamiento es mínimo, para que no se vea de más el interior de la cabina. En exterior es mayor.
- Los dos efectos entran con un **tiempo de entrada** corto y vuelven a su valor normal con un **tiempo de salida** más largo y suave.
- Si el boost se cancela (por ejemplo frenando con S), los efectos vuelven con el tiempo de salida, sin cortes.
- El FOV base y la posición base de cada cámara no cambian; el efecto es un añadido encima.
- En cabina, el marco se escala con el FOV del boost para que en pantalla se quede igual: solo se abre la vista del mundo.

## 5. Interacción con otros sistemas

- **Pausa**: congela todos los efectos (inclinación, vibración, FOV y empuje).
- **Recolocación**: la inclinación de la cabina, la vibración, el FOV y el empuje vuelven a sus valores base **al momento**.
- **Carrera terminada**: los efectos siguen la física. Al frenarse sola la nave, la vibración desaparece y el boost ya no está activo.
- **Cuenta atrás / parrilla**: la nave está parada, así que no hay efectos.
- **Fantasma**: no tiene cámara y no se ve afectado. El formato del archivo no cambia.
- **Menú principal**: la nave del menú no tiene efectos.
- **Daño**: no cambia los efectos. La velocidad punta reducida hace que la vibración se calcule sobre la punta actual.
- **Impactos contra barreras**: sin efecto de cámara en esta spec.

## 6. Ajustes (preparado para SETTINGS)

- Todos los parámetros de la sección 7 viven en un **objeto de ajustes de cámara**, una clase de datos pura con valores por defecto.
- Ahora se exponen en el Inspector, y los valores que se den por buenos al probar pasan a ser los valores por defecto del código (como en las otras specs).
- **No** se hace el menú SETTINGS ni se guarda nada en disco. Solo se deja el objeto listo para que SETTINGS lo lea y lo escriba más adelante.
- Propuesta para cuando se haga SETTINGS (no se implementa ahora):
  - Inclinación de cabina: 0–100 % (multiplica el factor de cabina).
  - Vibración: 0–100 %.
  - Efecto del boost en la cámara: activado o desactivado.

## 7. Parámetros en el Inspector

Valores iniciales para la primera prueba. Los definitivos son los que se ajusten en el Inspector probando en pista.

| Parámetro | Valor inicial |
|---|---|
| Factor de inclinación de cabina (respecto a la de la nave) | 0,4 (inicial 0,25) |
| Inclinación máxima de cabina | 8° (inicial 4°) |
| Suavizado de la inclinación de cabina | 0,35 s |
| Umbral de vibración (% de la punta) | 85 % |
| Amplitud máxima de vibración en cabina | 0,03 m (inicial 0,008 m) |
| Amplitud máxima de vibración en exterior | 0,018 m (inicial 0,02 m) |
| Multiplicador de vibración en boost | 1,5 |
| Frecuencia base de vibración | 8 Hz (inicial 18 Hz) |
| Suavizado de la intensidad de vibración | 0,2 s |
| FOV extra en boost | +8° |
| Empuje hacia atrás en boost, cabina | 0,05 m |
| Empuje hacia atrás en boost, exterior | 0,6 m |
| Tiempo de entrada de los efectos del boost | 0,25 s |
| Tiempo de salida de los efectos del boost | 0,5 s |

## 8. Arquitectura y pruebas

Lógica pura, sin dependencias de nodos de Godot:

- **Ajustes de cámara** (datos): los parámetros de la sección 7.
- **Inclinación de cabina** (puro): a partir de la inclinación objetivo de la nave da la de la cabina, con el factor, el tope y el suavizado con `delta`.
- **Vibración** (puro): dados la velocidad, la punta actual, si hay boost, el tiempo y el tipo de cámara, devuelve un desplazamiento. La intensidad va suavizada con `delta`.
- **Efecto del boost** (puro): dado si el boost está activo, devuelve el FOV extra y el empuje actuales, con entrada y salida suavizadas.
- **Nodo de cámara** (Godot): lee el estado de la nave, llama a las clases puras y aplica el roll al pivote de la cabina, el desplazamiento y el FOV a la cámara activa. No contiene reglas.

### Pruebas unitarias mínimas

1. Inclinación de cabina: sin girar → 0. Al máximo de la nave → objetivo × factor, sin pasar del tope. Con factor 0 → siempre 0.
2. La inclinación de cabina se acerca al objetivo de forma suave (nunca salta en un solo frame) y llega al objetivo con el tiempo.
3. Vibración: por debajo del umbral → 0. A la punta → amplitud máxima del tipo de cámara. Con boost → × multiplicador. Con amplitud 0 → siempre 0.
4. Vibración determinista: mismo tiempo y mismas entradas → mismo desplazamiento.
5. La vibración se calcula sobre la punta actual (con daño, la punta reducida).
6. Boost: al activarlo, el FOV extra y el empuje suben hasta su máximo en el tiempo de entrada. Al desactivarlo, vuelven a 0 en el tiempo de salida.
7. Recolocación → todos los efectos a su valor base al momento.
8. Con `delta` = 0 (pausa) no cambia nada.

Las pruebas existentes siguen pasando.

## 9. Prueba manual

1. Cabina, curvas normales: la vista se ladea un poco y despacio, con el marco incluido. No marea. El giro de la nave responde igual que antes.
2. Cabina en derrape: se ladea algo más, igual de suave.
3. Factor de cabina a 0 en el Inspector: la cabina no se inclina.
4. Cambiar a la cámara exterior con C: la cámara no se ladea; la nave sí, como antes. El derrape se ve como ahora.
5. Volver a cabina en mitad de una curva: sin salto.
6. A velocidad baja y media no hay vibración. Cerca de la punta aparece un temblor sutil, en las dos cámaras.
7. Boost con Shift: el FOV se abre, la cámara se echa un poco hacia atrás y la vibración sube. Al acabar, todo vuelve suave.
8. Cancelar el boost frenando con S: los efectos vuelven sin cortes.
9. Pausa con el boost activo y vibrando: todo congelado. Al reanudar sigue igual.
10. Recolocación tras salida grave: la cámara aparece sin inclinación, sin vibración y con el FOV normal.
11. Terminar la carrera a alta velocidad: la vibración se apaga sola al frenar la nave.
12. TIME TRIAL: los efectos funcionan igual y el fantasma no se ve afectado.
13. Comprobar que los valores ajustados en el Inspector siguen ahí tras cerrar y abrir el proyecto.

## 10. Fuera de alcance

- Menú SETTINGS y guardado de los ajustes en disco.
- Inclinación de la cámara exterior.
- Rotación de la cámara en la vibración.
- Efectos de cámara por impactos contra barreras.
- Partículas, estelas, líneas de velocidad, bloom extra y otros efectos visuales.
- Sonido.
- Cambios en la cámara exterior en derrape.
- Mando.
