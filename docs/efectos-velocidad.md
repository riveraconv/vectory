# VECTORY — Spec: Efectos de velocidad

Versión 1.0 · 2026-11-03 · Estado: **definido, listo para implementar**

Efectos visuales que venden la velocidad y el boost: **visión túnel**, **líneas de velocidad**, **FOV según la velocidad**, **tobera** y **estela** (partículas y estelas de las alas). Todo es **solo visual**: no cambia la física, el control, la colisión ni el fantasma.

Maqueta de referencia (se decidió con todo activado y estos valores): https://claude.ai/artifact/VsogytUEtwSENXiybS5fnq. En la maqueta el boost va con Espacio; **en el juego sigue siendo Shift**, como ya está.

Se apoya en lo que ya existe: `Player` / `Spaceship` (velocidad, punta, boost), las dos cámaras y la spec `camara` (vibración, FOV y empuje del boost, objeto de ajustes de cámara), el glow del `WorldEnvironment`, la pausa y la recolocación.

Rama: `feature/speed-effects`, que sale de `develop` con `feature/camera` ya mergeada.

Todo lo demás del documento maestro sigue aplicando. Esta spec sustituye el punto "partículas, estelas, líneas de velocidad, bloom extra y otros efectos visuales" del fuera de alcance de la spec `camara`.

---

## 1. Factores comunes

Todos los efectos se calculan a partir de tres valores, en una clase pura:

- **Factor de velocidad** `f` = (velocidad − 25 % de la punta) / (punta − 25 % de la punta), limitado a 0–1. Es 0 por debajo del 25 % de la punta y 1 a la punta.
- **Empuje** `th` = velocidad / punta, limitado a 0–1.
- **Boost** `b`: 0–1, sube a 1 en **0,25 s** al activarse y baja a 0 en **0,5 s** al acabar o cancelarse (los mismos tiempos de entrada y salida que la spec `camara`).

"Punta" es la **punta base** de la nave, sin daño ni pendientes, para que el aspecto de los efectos no cambie cuando se pierde punta. Con la punta de la maqueta (400 km/h), `f` empieza a 100 km/h.

## 2. Visión túnel

Efecto de pantalla completa (shader de post-proceso), después del glow.

- **Desenfoque radial**: los píxeles se estiran desde el centro hacia los bordes. El centro de la pantalla queda nítido y el efecto crece hacia los bordes (empieza al 22 % de la distancia del centro a la esquina y es completo en el 95 %). Unas 16 muestras por píxel.
- **Oscurecimiento** de los bordes, desde el 42 % de esa distancia.
- **Con boost**:
  - Desdoble de color en los bordes (rojo hacia fuera y azul hacia dentro, muy leve).
  - Halo **ámbar** (`Palette.Orange`) en las esquinas, al 10 %.
- Intensidad = mín(1,2; 0,85·`f` + 0,45·`b`).
- Solo en **cabina** y **cámara exterior**. Nunca en menús, selector ni pantalla de resultados.
- Parámetros: fuerza del desenfoque (0,11), oscurecimiento (0,62), desdoble (0,010), halo (0,10), número de muestras (16).

## 3. Líneas de velocidad

- Unos **240 trazos** finos alrededor de la cámara, en un cilindro de 4,5–25 m de radio alrededor del eje de la vista (nunca en el centro, para no tapar la pista) y de 20 a 170 m por delante.
- Están quietos en el espacio de la cámara y se mueven hacia ella a la velocidad de la nave. Al pasar la cámara reaparecen al fondo. Aparecen con fundido.
- **Largo** = 2 m + velocidad (m/s) × 0,06 + 6 m × `b`, con un factor aleatorio de 0,5–1 por trazo.
- **Visibles** desde el **40 % de la punta** y completos al **90 %**. Intensidad máxima 0,55.
- Color blanco-cian, que pasa a ámbar claro con boost (60 %). Mezcla aditiva.
- Solo en cabina y exterior. Una sola malla de líneas que se actualiza cada frame.

## 4. FOV según la velocidad

- La cámara abre el campo de visión con la velocidad: **+14°** a la punta (× `f`), en las dos cámaras.
- Se **suma** al FOV extra del boost de la spec `camara` (+8°), que no cambia.
- Suavizado igual que el del boost, sin saltos.
- La **vibración** de la spec `camara` no cambia. La maqueta tenía una sacudida propia en boost: no se añade, porque ya está la de la spec `camara`.

## 5. Tobera

Dos toberas en la popa de la nave (en la maqueta, a ±0,55 m del centro, a la altura de la popa). Se ajustan a la geometría real de `Spaceship`.

- Cada una tiene:
  - **Boca**: un hexágono de ~0,24 m de radio.
  - **Llama**: un cono de líneas desde la boca hasta la punta.
  - **Núcleo**: línea central blanca más tres líneas interiores más cortas.
  - **Halo**: un sprite redondo y suave con mezcla aditiva.
- **Largo de la llama** = (0,35 + 2,4·`th` + 3,6·`b`) m, con un parpadeo de ±15 % (suma de senos rápidos, determinista).
- Con boost la llama es un **35 % más ancha**.
- **Color**:
  - Llama: cian (`Palette.Cyan`), que pasa a naranja (`Palette.Orange`) con boost. Brillo 0,6 + 1,0·`th` + 0,9·`b`.
  - Núcleo: blanco, que pasa a blanco cálido con boost. Brillo 0,7 + 0,8·`th` + 0,8·`b`.
  - Halo: tamaño (0,5 + 0,6·`th` + 0,9·`b`) m, brillo contenido (0,25 + 0,3·`th` + 0,4·`b`), para que no tape la nave en la cámara exterior.
- Con la nave parada (parrilla) la llama está al ralentí: corta y cian.
- Las toberas van en la geometría de la nave, así que salen también en la nave del menú y en el **fantasma** (este último con su transparencia de siempre). Su estado se calcula desde la velocidad del fantasma, sin cambiar el formato del archivo. El boost del fantasma no se graba, así que en el fantasma la tobera no muestra boost.

## 6. Estela

### 6.1 Partículas de las toberas

- Sistema de partículas en **espacio de mundo** (las partículas se quedan donde se emiten y la nave se aleja). Lo más sencillo es `GPUParticles3D`; si da problemas con los colores por partícula, `CPUParticles3D` o un sistema propio con una malla de puntos.
- **Emisión** por segundo = 25 + 260·`th` + 220·`b`, repartida entre las dos toberas.
- **Vida** = (0,95 − 0,45·`f`) s, con ±30 % aleatorio. Despacio viven más; rápido, menos.
- **Velocidad inicial**:
  - Hacia atrás desde la tobera, a (2 + 7·`th`) m/s.
  - Más una dispersión aleatoria de (0,25 + 1,6·(1 − `f`)) m/s, y una deriva hacia arriba de 0,7·(1 − `f`) m/s.
  - Se frenan con el aire (amortiguación ~1,5/s).
- **Comportamiento**:
  - Despacio: nube azul que flota y se abre.
  - Rápido: rastro cian fino y largo.
  - Con boost: ámbar.
- **Color**: azul (#3A7DFF) → blanco-cian (#BFF6FF) según `f`, que pasa a ámbar (#FFB45A) con boost (75 %), al 50 % de brillo. Se apaga con (1 − edad/vida)^1,6. Mezcla aditiva.
- Tamaño ~0,24 m, con perspectiva. Textura de punto suave.
- **Chispas de boost**: con `b` > 0,2, unas 80·`b` por segundo. Blanco cálido, en todas direcciones a 4–10 m/s más un empuje hacia atrás, con vida de 0,25–0,55 s.
- Máximo de partículas vivas a la vez: **4000** (parámetro).

### 6.2 Estelas de las alas

- Dos líneas de luz que salen de las **puntas de las alas** y se quedan atrás.
- Se guarda el recorrido de cada punta durante (0,12 + 0,45·`f` + 0,25·`b`) s y se dibuja como una línea que se apaga hacia atrás.
- **Visibles** desde el **37 % de la punta**, completas al **82 %**. Intensidad máxima 0,9.
- Color cian, que pasa a ámbar con boost (80 %). Mezcla aditiva.

### 6.3 Tobera y estela en otras naves

- Los rivales (cuando los haya) usan la misma tobera y la misma estela, con su velocidad y su boost.
- El fantasma: tobera sí, estela **no** (para que no ensucie la vista en TIME TRIAL).

## 7. Interacción con otros sistemas

- **Pausa**: se congela todo. Las partículas no avanzan ni envejecen, las líneas de velocidad se quedan quietas y la visión túnel se mantiene.
- **Recolocación**: se borran al momento las partículas, las estelas y las líneas de velocidad, y la visión túnel vuelve a 0. Así no queda una estela cruzando la pista.
- **En el aire** (spec `pista-3d`): los efectos siguen la velocidad igual que en el suelo.
- **Túneles**: sin cambios. La visión túnel ya da el efecto.
- **Carrera terminada**: los efectos bajan solos a medida que la nave se frena.
- **Parrilla y cuenta atrás**: tobera al ralentí, sin estela.
- **Menú principal**: la nave del menú lleva la tobera al ralentí. Sin estela ni efectos de pantalla.
- **Daño**: sin cambios, porque los efectos usan la punta base.

## 8. Ajustes (preparado para SETTINGS)

Los parámetros van a un **objeto de ajustes de efectos**, como el de la cámara. Ahora se tocan en el Inspector; el menú SETTINGS los leerá más adelante.

Propuesta para SETTINGS (no se hace ahora):
- Visión túnel: 0–100 % (multiplica la intensidad). Es lo que más puede marear, así que tiene que poder bajarse.
- Líneas de velocidad: activado o desactivado.
- FOV según la velocidad: 0–100 %.
- Calidad de partículas: baja, media o alta (máximo de partículas y emisión al 25, 50 o 100 %).

Pensando en Android: en calidad baja, la visión túnel usa 6 muestras en lugar de 16.

## 9. Parámetros en el Inspector

| Parámetro | Valor |
|---|---|
| Inicio del factor de velocidad | 25 % de la punta |
| Visión túnel: peso de la velocidad / del boost / máximo | 0,85 / 0,45 / 1,2 |
| Visión túnel: desenfoque / oscurecimiento / desdoble / halo | 0,11 / 0,62 / 0,010 / 0,10 |
| Visión túnel: muestras | 16 |
| Líneas: número / radio / distancia | 240 / 4,5–25 m / 20–170 m |
| Líneas: visibles desde / completas en | 40 % / 90 % de la punta |
| Líneas: largo | 2 m + v × 0,06 s + 6 m × boost |
| FOV extra a la punta | +14° |
| Tobera: largo de la llama | 0,35 + 2,4·th + 3,6·b m |
| Tobera: parpadeo | ±15 % |
| Partículas: emisión | 25 + 260·th + 220·b por segundo |
| Partículas: vida | 0,95 − 0,45·f s |
| Partículas: máximo | 4000 |
| Chispas de boost | 80·b por segundo |
| Estela de alas: duración | 0,12 + 0,45·f + 0,25·b s |
| Estela de alas: visible desde / completa en | 37 % / 82 % de la punta |

## 10. Arquitectura y pruebas

- **Factores de efecto** (puro): velocidad, punta y boost activo → `f`, `th` y `b` suavizado con `delta`.
- **Curvas de los efectos** (puro): de los factores a la intensidad del túnel, el largo y la visibilidad de las líneas, el FOV extra, el largo, el color y el brillo de la llama, la emisión y la vida de las partículas, y la duración y la visibilidad de las estelas.
- **Ajustes de efectos** (datos).
- **Nodos de Godot**: el post-proceso del túnel, las líneas de velocidad (cámara), la tobera y la estela (nave) solo leen las curvas y pintan. Sin reglas dentro.

### Pruebas unitarias mínimas

1. `f`: 0 por debajo del 25 % de la punta, 1 a la punta, 0,5 a mitad entre las dos.
2. `b` sube a 1 en 0,25 s y baja a 0 en 0,5 s. Con `delta` 0 (pausa) no cambia.
3. Túnel: 0 parado, 0,85 a la punta sin boost, 1,2 como máximo con boost.
4. Líneas: invisibles por debajo del 40 % de la punta y completas desde el 90 %.
5. FOV extra: 0 parado, 14° a la punta. Se suma al del boost de `camara` sin cambiarlo.
6. Llama: 0,35 m parada, 2,75 m a la punta sin boost, 6,35 m con boost (sin parpadeo).
7. Emisión de partículas: 25/s parada, 285/s a la punta, 505/s con boost.
8. Los efectos usan la punta base: con daño, a la misma velocidad, los valores son los mismos.
9. Recolocación: todo a 0 y sin partículas al momento.

Las pruebas existentes siguen pasando.

## 11. Prueba manual

1. Cabina: arrancar despacio y acelerar. A media velocidad aparecen las líneas y empieza a notarse el túnel. A fondo, el centro sigue nítido y los bordes estirados. No marea.
2. Boost con Shift: túnel más fuerte, halo ámbar, líneas ámbar y más largas, y el FOV se abre algo más. Al acabar, todo vuelve suave.
3. Cámara exterior despacio: llama corta cian y nube azul que flota detrás.
4. Exterior a fondo: llama larga, rastro cian fino y largo, y estelas en las puntas de las alas.
5. Exterior con boost: llama naranja y ancha, rastro ámbar con chispas. La nave se sigue viendo bien (el halo no la tapa).
6. Pausa con todo activo: congelado. Al reanudar sigue igual.
7. Recolocación: sin estelas cruzando la pista y efectos a 0.
8. Parrilla: tobera al ralentí, sin estela ni túnel.
9. TIME TRIAL: el fantasma lleva tobera, pero no estela.
10. Rendimiento: con todo activo y la escenografía de la ciudad, los FPS no bajan de forma apreciable.

## 12. Fuera de alcance

- Menú SETTINGS y guardado de ajustes.
- Efectos por impactos contra barreras, por derrape (chispas en el suelo) o por aterrizaje.
- Grabar el boost en el fantasma.
- Sonido.
