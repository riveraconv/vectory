# VECTORY — Spec: Daños

Versión 1.0 · 2026-10-30 · Estado: **definido, listo para implementar**

Añade **daños acumulativos** a la nave en **RACE**: los impactos medios y fuertes contra la barrera suman puntos, los puntos suben el **nivel de daño (0–3)** y cada nivel tiene consecuencias. Un **indicador de tres segmentos** en el HUD muestra el nivel y la nave **parpadea** en algunas aristas según el daño.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Daños**: *Indicador A · tres segmentos en fila junto a las esferas* (elegida), *Cabina · nivel 3* y *Exterior · nivel 2*. Las variantes B y C del indicador quedan descartadas. La nave de la pantalla exterior es aproximada: solo sirve de referencia del parpadeo, no de la geometría. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: el **nivel de impacto** de la spec de barreras (roce / medio / fuerte, con la espera de 0,3 s entre impactos del mismo lado), `Player` / `Spaceship`, la cabina, la lógica del boost, `RaceSession` / `RaceTracker`, `GameSession` (modo), `Hud` y `Palette`.

Cambios respecto al documento maestro y a specs anteriores:
- **Sin daños en TIME TRIAL**: solo RACE.
- El indicador usa **segmentos** (no una silueta) y se añade un **amarillo** a la paleta.
- La reparación entre carreras de campeonato **no** entra (no hay campeonato).

Rama: `feature/damage` (sale de `develop`, con `feature/barriers` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- Solo en **RACE**. En TIME TRIAL los impactos hacen lo de siempre (rebote, pérdida, destello, sacudida) pero no dañan, y no hay indicador.
- Puntos por impacto: **roce 0, medio +1, fuerte +3**.
- Nivel de daño según puntos: **0** (0–2), **1** (3–5), **2** (6–8), **3** (9 o más).
- Consecuencias:

| Nivel | Efecto en la nave | Indicador |
|---|---|---|
| 0 | Ninguno | Verde |
| 1 | Solo visual | Amarillo |
| 2 | Velocidad punta **−8 %** | Rojo fijo |
| 3 | Velocidad punta **−15 %**, giro **−15 %**, **boost bloqueado** (sigue recargando) | Rojo parpadeante |

- El daño **no baja** durante la carrera. Una carrera nueva (RESTART, volver a entrar en RACE) empieza en nivel 0.
- Sin destrucción ni eliminación: con nivel 3 se sigue corriendo hasta el final.

---

## 2. Acumulación

- Cada impacto **medio o fuerte** que la spec de barreras cuenta como impacto (respetando su espera de 0,3 s por lado) suma sus puntos una vez. El roce no suma nada, por largo que sea.
- Los puntos no tienen tope práctico, pero el nivel se queda en 3 a partir de 9.
- Ejemplos: 3 fuertes → nivel 3. 3 medios → nivel 1. 1 fuerte + 3 medios → nivel 2.
- Cuándo suma:
  - **En carrera**: sí.
  - **Previa**: la nave está quieta, no hay impactos.
  - **Terminada**: **no** suma (la nave rueda sola y puede rozar o chocar, pero ya no cuenta).
- La **recolocación automática** ni suma ni repara daño.
- El nivel se expone para el HUD, la nave y la futura IA de rivales (mismas reglas para todos cuando existan).

## 3. Consecuencias

### 3.1 Velocidad punta

- Nivel 2: punta × **0,92**. Nivel 3: punta × **0,85**.
- Se aplica a la punta base de la nave; el boost sigue pudiendo superarla (+100 km/h sobre la velocidad actual, como siempre).
- Si al subir de nivel la nave va por encima de la nueva punta, **pierde el exceso** de forma gradual con la misma regla de la spec del boost (20 km/h por segundo), no de golpe.

### 3.2 Giro (nivel 3)

- La velocidad de giro × **0,85**.
- Se combina (multiplicando) con la corrección temporal de un impacto fuerte de la spec de barreras.
- Aceleración y freno no cambian por el daño.

### 3.3 Boost (nivel 3)

- **No se puede activar** el boost.
- Las cargas **se siguen ganando** con las reglas de siempre (1 cada 35 s, máximo 3).
- Un impulso **en curso** al llegar a nivel 3 **no se cancela**: termina normalmente (coherente con que un impacto no cancela el boost).
- HUD: las esferas con carga se ven en estado **bloqueado** (ver 5.3). Las vacías, igual que ahora.

## 4. Daño visible en la nave

- A partir del nivel 1, algunas aristas de la **nave** (vista exterior) y de la **cabina** (vista de cabina) **parpadean**: bajan al ~20 % de su opacidad durante un instante y vuelven.

| Nivel | Aristas afectadas | Frecuencia de cada parpadeo |
|---|---|---|
| 0 | Ninguna | — |
| 1 | ~10 % | Cada 1,5–3 s (aleatorio) |
| 2 | ~20 % | Cada 0,8–1,6 s |
| 3 | ~35 % | Cada 0,3–0,8 s |

- Cada parpadeo dura entre **60 y 120 ms**.
- Las aristas afectadas se eligen al subir de nivel y se **mantienen** (las del nivel anterior siguen y se añaden más). Generador aleatorio inyectado, para poder fijar semilla en pruebas.
- No cambia el color de las líneas, solo su opacidad. El glow baja con ellas.
- **No** afecta al HUD, al fantasma (no hay en RACE) ni a la nave del menú principal.
- La pausa congela el parpadeo.
- Para poder parpadear aristas sueltas hay que separar las líneas afectadas en una malla o material aparte (o un shader con un valor por arista). Si eso obliga a tocar cómo `Spaceship` o la cabina construyen sus líneas, **se avisa antes**.

## 5. HUD

### 5.1 Indicador

- **Tres segmentos** horizontales en fila, **abajo a la derecha**, en la misma fila que las esferas de boost, **a su izquierda**, separados **24 px** y centrados en vertical con ellas, todo encima del número de velocidad y alineado a la derecha (como en el lienzo).
- Tamaño inicial: cada segmento **22 × 8 px** (sobre 1280×720), separación **4 px**, borde de 1 px. Sin números ni texto.
- Los segmentos se encienden **de izquierda a derecha** con cada nivel, todos del color del nivel:

| Nivel | Segmentos encendidos | Aspecto |
|---|---|---|
| 0 | 0 | Los tres apagados, solo el borde en **verde** al ~60 % |
| 1 | 1 | Encendido relleno en **amarillo**, con halo suave; los apagados con borde amarillo al ~60 % |
| 2 | 2 | Encendidos en **rojo**, con halo; el apagado con borde rojo al ~60 % |
| 3 | 3 | Los tres en **rojo parpadeante** |

- Parpadeo del nivel 3: alterna entre opacidad 100 % y ~25 % a **2 Hz** (0,25 s y 0,25 s), sin parar mientras dure la carrera. La pausa lo congela.
- Al subir de nivel, el cambio es inmediato, sin animación.
- En TIME TRIAL el indicador **no se muestra**.
- En la pantalla de resultados no se muestra nada del daño.

### 5.2 Colores

- Verde **#3DFF8A** y rojo **#FF4D5E**: los colores base que ya existen (`Green`, `Red`). Se actualiza su comentario en `Palette`: ya no son solo para el delta, también para el daño.
- **Amarillo nuevo: #FFE04A**, color base `Yellow` en `Palette`, con comentario de que amplía la paleta del documento maestro para el indicador de daño.
- Roles nuevos derivados: `DamageNone` (verde), `DamageLight` (amarillo), `DamageHeavy` (rojo), sus halos (`…Glow`, opacidad ~0,5) y el borde de segmento apagado (~0,6).

### 5.3 Esferas de boost bloqueadas

- Con nivel 3, cada esfera **con carga** se ve **bloqueada**: rojo #FF4D5E atenuado (relleno ~30 %, borde ~55 %), **sin brillo**, igual que el estilo del cooldown pero en rojo.
- Las vacías siguen en azul. Una carga ganada estando en nivel 3 aparece directamente bloqueada.
- El estado bloqueado tiene prioridad sobre el de cooldown.

## 6. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Puntos por impacto medio / fuerte | 1 / 3 | daños |
| Puntos para nivel 1 / 2 / 3 | 3 / 6 / 9 | daños |
| Factor de punta nivel 2 / 3 | 0,92 / 0,85 | daños |
| Factor de giro nivel 3 | 0,85 | daños |
| Proporción de aristas que parpadean por nivel | 0,10 / 0,20 / 0,35 | nave |
| Intervalo entre parpadeos por nivel | 1,5–3 / 0,8–1,6 / 0,3–0,8 s | nave |
| Duración de un parpadeo | 60–120 ms | nave |
| Opacidad en el parpadeo | 0,2 | nave |
| Tamaño de segmento / separación | 22 × 8 px / 4 px | HUD |
| Separación entre indicador y esferas | 24 px | HUD |
| Frecuencia de parpadeo del indicador | 2 Hz | HUD |

- Mismos valores para todas las naves y circuitos.

## 7. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin dependencias de nodos de Godot:

- **Modelo de daño** (`DamageModel` o similar): `AddImpact(nivelDeImpacto)`, puntos, nivel (0–3), factor de punta, factor de giro, si el boost está permitido. Aviso cuando cambia el nivel. Se le puede indicar que deje de sumar (carrera terminada).
- **Parpadeo de aristas** (puro): dado el número de aristas, el nivel y un generador inyectado, elige qué aristas parpadean (manteniendo las anteriores al subir) y, con `Update(delta)`, qué aristas están "apagadas" en cada momento.
- **Boost**: su lógica recibe si está permitido activar (no conoce el daño). `CanActivate` es falso si no está permitido; la recarga no cambia.
- **`Player`**: recibe los factores de punta y giro (no conoce el daño). Si hay que tocar cómo guarda la punta o el giro, **se avisa antes**.
- **`Main.cs`**: solo en RACE crea el modelo de daño, lo conecta con los impactos de la colisión y lo reparte a `Player`, boost, nave y HUD. Sin reglas dentro.
- **HUD**: lee nivel y si el boost está bloqueado; no calcula nada.

### Pruebas unitarias mínimas

Modelo de daño:
1. Empieza en nivel 0 con 0 puntos.
2. Un roce no suma. Un medio suma 1, un fuerte suma 3.
3. 3 medios → nivel 1. 1 fuerte + 3 medios → nivel 2. 3 fuertes → nivel 3. 5 fuertes → sigue en 3.
4. Factores: nivel 0 y 1 → punta 1, giro 1, boost permitido. Nivel 2 → punta 0,92. Nivel 3 → punta 0,85, giro 0,85, boost no permitido.
5. El aviso de cambio de nivel se lanza una vez por subida (también si un impacto salta dos niveles: un solo aviso con el nivel final).
6. Tras marcar la carrera como terminada, los impactos no suman.

Boost:
7. Con boost no permitido y cargas disponibles, activar no hace nada; la recarga sigue (a los 35 s hay una carga más).
8. Un impulso en curso no se cancela al dejar de estar permitido.

Parpadeo:
9. Nivel 0 → ninguna arista. Nivel 1 con 100 aristas → 10; nivel 2 → 20 incluyendo las 10 anteriores; nivel 3 → 35 incluyendo las 20.
10. Con la misma semilla, las mismas aristas.

Las pruebas actuales siguen pasando.

## 8. Prueba manual

1. RACE: el indicador aparece abajo a la derecha, a la izquierda de las esferas, con los tres segmentos apagados y borde verde. La nave no parpadea.
2. Rozar la barrera un buen rato: el indicador no cambia.
3. Tres impactos medios: un segmento amarillo y algunas aristas de la cabina titilan de vez en cuando. Mirar también en exterior (C).
4. Más impactos hasta nivel 2: dos segmentos rojos, más parpadeo, y la punta máxima en recta baja un poco (≈ −8 %). Si iba a tope, baja poco a poco, no de golpe.
5. Hasta nivel 3: tres segmentos rojos parpadeando, punta ≈ −15 %, el giro se nota algo más lento, las esferas con carga en rojo atenuado y Espacio no hace nada. Esperar 35 s: aparece otra carga, también bloqueada.
6. Llegar a nivel 3 con un boost en curso: el boost termina normalmente.
7. Pausar: indicador y parpadeo de la nave congelados.
8. Terminar la carrera y chocar después de la meta: el nivel no sube.
9. RESTART: indicador verde, nave sin parpadeo, boost normal.
10. TIME TRIAL: chocar fuerte varias veces: sin indicador, sin parpadeo, boost normal, punta normal.
11. Volver al menú principal: la nave del fondo no parpadea.
12. Comparar el indicador con el lienzo (*Indicador A*) en ventana y a pantalla completa.

## 9. Fuera de alcance

- Daños en TIME TRIAL.
- Reparación entre carreras de campeonato o reparación durante la carrera.
- Daños por colisión entre naves (no hay rivales).
- Mostrar el daño en la pantalla de resultados.
- Animación al subir de nivel, avisos de texto, sonido, vibración.
- Humo, chispas, partículas o piezas sueltas.
- Destrucción o eliminación de la nave.
- Que el daño afecte a la aceleración o al freno.
- Mando.
