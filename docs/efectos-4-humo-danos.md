# VECTORY — Spec: Efectos 4 · Humo de daños

Versión 1.1 · 2026-11-18 · Estado: **etapa 1 implementada**

Cuarta y última spec de efectos de nave. **Sustituye el humo de daño** de los cascos 3D por uno nuevo, más elaborado y **pegado a la nave**:

- **Nivel 2**: humo gris medio y unas pocas brasas.
- **Nivel 3**: mucho más humo, más oscuro, desde **3 focos**, con **fuego** pequeño en cada uno y más brasas.

Es **solo visual**: no cambia el modelo de daño, sus consecuencias ni el resto del daño visible (quemaduras y parpadeo de luces).

Plan de las specs de efectos de nave:

1. Choques (hecha).
2. Derrape (hecha).
3. Aterrizaje (hecha).
4. **Humo de daños** (esta).

Maqueta de referencia: **`designs/efectos-humo-danos.html`** («VECTORY · Humo de daños · Maqueta»). Se decidió con **HUMO A · PENACHO**, **FOCOS EN 3 · 3**, **FUEGO EN 3 · SÍ** y **BRASAS · SÍ**. HOY (el humo actual) y B · ENVUELTO quedan descartados. Los valores de esta spec son los de la maqueta **en su versión 2**: el humo de **HIGH** es el que en la primera versión salía con LOW, y hay brasas también en el nivel 2. Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa. La pista, la ciudad, la silueta de la nave, la posición de los focos y la cámara de la maqueta son aproximadas: no son parte de esta spec.

**Por qué se cambia**: el humo actual sale de un solo punto, se queda **quieto en el mundo** y vive 1,6 s. A 300–400 km/h deja un chorro de más de 100 m detrás de la nave y no se lee como «esta nave está tocada». El nuevo **hereda casi toda la velocidad de la nave**: se queda atrás despacio y va centrado en ella a cualquier velocidad.

Se apoya en lo que ya existe:
- El **nivel de daño** (0–3) de la spec `danos`.
- Los cascos 3D (`cascos-3d-1-vector`, `cascos-3d-2-blade`, `cascos-3d-3-hammer`): el vacío **`Smoke`** de cada uno y su humo actual (sección 6.5), que **se quita**.
- El **perfil visual** de cada nave y los **manifiestos**.
- El ajuste **PARTICLES** (`settings`, `efectos-velocidad`) y el objeto de ajustes de efectos.
- La pausa, la recolocación y `Palette`.

Cambios respecto a specs anteriores:
- **Se elimina** el humo de la sección 6.5 de las tres specs de cascos 3D (partículas por segundo 7 / 20, colores `#4A4A4A` / `#262626`, vida 1,6 s, tamaños de 0,35 a 1,95 m). Las **quemaduras** y el **parpadeo** de esas secciones **no cambian**.
- Sustituye el punto «humo, chispas, partículas o piezas sueltas» del fuera de alcance de `danos` (solo lo del humo y las brasas).

Rama: `feature/fx-damage-smoke`, que sale de `develop`. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

**Cambios de la v1.1** (al implementar, decisiones del usuario):
- **Máximo de bocanadas: 420**, el de la maqueta, no 300: con 8 naves en nivel 3 en HIGH hay unas 290 vivas.
- **Brasas en un sistema pequeño aparte** (etapa 2): las chispas de los choques no admiten gravedad ni vaivén por chispa, chocan con el suelo y su color no parpadea. Su trazo, con la velocidad relativa a tu nave, como las chispas.
- **El humo se ordena de lejos a cerca** de la cámara cada frame: es oscuro y con mezcla normal, y el orden se nota.
- Con tres focos, cada uno tiene su acumulador: en LOW, en el primer segundo salen 2 + 1 + 1 bocanadas; de media, 6 por segundo (prueba 2).
- Quitar el humo viejo arregla también la bola gris que salía en el cielo al recolocar una nave.
- El humo se dibuja con un **`MultiMeshInstance3D`** (un cuadrado por bocanada con un shader que lo pone de cara a la cámara, lo gira y elige la nube) y un búfer de floats fijo: rehacer una malla con arrays de Godot cada frame dejaba basura toda la carrera y adelantaba las pausas del recolector de .NET. Las mallas de los demás efectos reutilizan sus arrays.

---

## 1. Resumen

| | Nivel 2 · medio | Nivel 3 · grave |
|---|---|---|
| Focos | 1 (`Smoke`) | 3 (`Smoke`, `Smoke_2`, `Smoke_3`) |
| Humo (HIGH) | **6,5** bocanadas/s | **10**/s en `Smoke` y **7**/s en cada uno de los otros dos |
| Humo: color y opacidad | gris medio, 0,42 | casi negro, 0,62 |
| Fuego | — | Llamas pequeñas en cada foco y humo recién salido iluminado de naranja |
| Brasas (HIGH) | **3**/s | **5**/s por foco (15/s) |

- **Penacho**: nubes con textura de ruido, más claras por arriba, que giran y se arremolinan, y que heredan casi toda la velocidad de la nave.
- Solo en **RACE** (el daño solo existe ahí), para todas las naves.
- **PARTICLES**: humo y brasas al 25 / 50 / 100 %. El fuego no cambia.
- Nivel 0 y 1: nada.
- Etapas:
  - **0** · Inventario.
  - **1** · Humo nuevo (y quitar el viejo).
  - **2** · Fuego y brasas.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo está hecho hoy el **humo** de los cascos 3D (nodo, `GPUParticles3D` o propio, dónde se crea, cómo lee el nivel de daño) y todo lo que habría que quitar.
2. La posición del vacío **`Smoke`** de VECTOR, BLADE y HAMMER, y dónde están las **toberas o motores** y las **alas** de cada uno en el manifiesto (para `Smoke_2` y `Smoke_3`, sección 3).
3. Cómo se dibujan hoy las bocanadas del humo de `efectos-1-choques` (si ya está hecho), por si se puede reutilizar el mismo sistema de sprites con otra textura.
4. Cómo se lee el nivel de daño de **cada nave** (también de los rivales) y si hay aviso al cambiar.
5. Si el humo de un rival se ve desde la vista de cabina cuando lo tienes delante (orden de dibujo con la cabina 3D y su cristal).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Focos

- **`Smoke`**: el vacío que ya tiene cada casco. No cambia.
- **`Smoke_2`** y **`Smoke_3`**: dos puntos nuevos en el **perfil visual** de cada nave (no en el modelo de Blender: no hace falta volver a exportar los `.glb`). Valores de partida:

| Nave | `Smoke` (ya existe) | `Smoke_2` | `Smoke_3` |
|---|---|---|---|
| VECTOR | el suyo | la **otra góndola**, simétrica a `Smoke` | sobre el **ala** del lado de `Smoke`, a ~70 % del semiancho, en la mitad trasera |
| BLADE | (0,60, 0,35, 2,40), sobre la góndola derecha | (−0,60, 0,35, 2,40), la góndola izquierda | sobre el ala derecha, a ~70 % del semiancho (≈ 1,1 m), en la mitad trasera |
| HAMMER | (0,40, 1,33, −0,06), la boca del escape | sobre el **motor izquierdo**, hacia la tobera | sobre el **motor derecho**, hacia la tobera |

- Cada foco va **sobre la piel** del casco (unos centímetros por fuera), no dentro. El agente ajusta los puntos con el manifiesto, los mira en el juego y **apunta los valores finales** en la spec.
- Si en VECTOR `Smoke` no está en una góndola, se avisa y se decide dónde va `Smoke_2`.

**Valores finales (v1.1)**, en los ejes de la nave (x a la derecha, y arriba, z hacia la popa), medidos sobre los `.glb` y 5 cm por encima de la piel. Los tres van en el perfil visual (`SmokeFoci`); `Smoke` es el vacío del manifiesto y una prueba lo compara.

| Nave | `Smoke` | `Smoke_2` | `Smoke_3` |
|---|---|---|---|
| VECTOR | (0,75, 0,50, 2,20) · góndola derecha (las toberas van a ±0,75) | (−0,75, 0,50, 2,20) · góndola izquierda | (1,84, 0,18, 1,60) · ala derecha, 70 % del semiancho |
| BLADE | (0,60, 0,35, 2,40) | (−0,60, 0,35, 2,40) | (1,10, 0,19, 2,00) · ala derecha |
| HAMMER | (0,40, 1,33, −0,06) · boca del escape | (−0,45, 0,72, 2,50) · motor izquierdo, cerca de la tobera | (0,45, 0,72, 2,50) · motor derecho |

## 4. Humo

### 4.1 Cuánto

| | Nivel 2 | Nivel 3 |
|---|---|---|
| Bocanadas por segundo (HIGH) | 6,5 en `Smoke` | 10 en `Smoke` · 7 en `Smoke_2` · 7 en `Smoke_3` |
| Vida | 0,9–1,3 s | 1,0–1,5 s |
| Radio (crece con la raíz de la edad) | 0,35 × (0,8–1,1) → 1,5 × (0,8–1,2) m | 0,45 × (0,8–1,1) → 2,2 × (0,8–1,2) m |
| Subida inicial | 0,6–1,0 m/s | 0,8–1,4 m/s |
| Color | (0,42; 0,44; 0,50) | (0,17; 0,175; 0,20) |
| Opacidad máxima | 0,42 | 0,62 |

- El reparto por segundo va con acumulador por foco, para que no dependa de los fps.
- Cada bocanada tiene un **tono** aleatorio × 0,85–1,15 sobre el color de su nivel.
- **PARTICLES**: bocanadas por segundo × 0,25 / 0,5 / 1.

### 4.2 Cómo se mueve (penacho)

- Nace en su foco (±0,08 m a los lados) con la **velocidad de la nave** menos un **retraso** hacia atrás:
  - retraso = 6 % de la velocidad de la nave, limitado entre **1,5 y 7 m/s**, × 0,8–1,2 por bocanada;
  - así, a 400 km/h el humo se queda atrás a unos 7 m/s y la estela mide unos **10 m**; parado, se queda atrás a 1,5 m/s.
- Además: ±0,4 m/s a los lados (se frena 0,8 por segundo) y la subida de la tabla (se frena 0,5 por segundo).
- **Remolino**: se suma una velocidad lateral de 0,6 × sen(3,1·`t` + φ) y una vertical de 0,12 × cos(2,3·`t` + 1,7·φ), con `t` la edad de la bocanada y φ una fase aleatoria por bocanada.
- Una vez nacida, la bocanada **no sigue a la nave**: se mueve con su propia velocidad. Si la nave frena, la gira o la adelanta, el humo se queda donde iba.
- Con pista 3D: «arriba» es el **arriba del mundo** (el humo sube aunque la nave esté en una pendiente o un peralte).

### 4.3 Aspecto

- **Sprites** que miran a la cámara, con mezcla **normal** (no aditiva), sin escribir profundidad.
- **Textura**: **4 variantes** de una nube con ruido, más clara por arriba, generadas por código con semillas fijas (3, 17, 41 y 77), como `cloudTex` de la maqueta:
  - 128 × 128;
  - ruido de valor suavizado en dos rejillas (8 y 16 celdas), mezcladas 0,65 / 0,35;
  - alfa = (1 − `d`²) × (0,35 + 0,9·ruido) − 0,08, limitada a 0–1, con `d` la distancia al centro (0 en el centro y 1 en el borde);
  - brillo = 0,62 + 0,38 × (1 − altura) + 0,15 × (ruido − 0,5): más claro arriba.

  Se pueden guardar como imágenes o generar al arrancar. Cada bocanada usa una al azar.
- **Giro**: un ángulo inicial al azar y un giro de ±0,7 rad/s.
- **Opacidad** = máxima del nivel × (1 − `u`)^1,4, con `u` = edad / vida. Entra en **0,08 s** (sin aparecer de golpe).
- **Con fuego** (nivel 3, etapa 2): durante el primer **30 %** de la vida, el color se mezcla con naranja (0,75; 0,30; 0,10), con peso 0,8 × (1 − `u` / 0,3). Así el humo sale iluminado por las llamas y se apaga a gris.

### 4.4 Máximo

- Como mucho **420 bocanadas** (v1.1; la spec decía 300) de humo de daño vivas a la vez, para todas las naves (parámetro). Es un grupo aparte del humo de los choques.
- Si se llena, las nuevas no salen.

## 5. Fuego (nivel 3)

Va **con la nave** (en coordenadas de la nave), no en el mundo. En cada uno de los 3 focos:

- **4 llamas**: sprites con una textura de **gota** (punta arriba, redonda abajo, como `texFlame` de la maqueta), mezcla **aditiva**, colores alternos **#FFB050** y **#FF6A1E**.
  - Parpadeo de cada llama `k` (0–3) en el foco `f`: `fk` = 0,6 + 0,4 × sen((23 + 7k)·`t` + 3f + k) × sen((13 + 5k)·`t` + 2k), con `t` el tiempo de la carrera.
  - Posición: el foco, con ±0,06 m de temblor a los lados, subida de 0,18 + 0,15·k·`fk` m y 0,1·k m hacia atrás.
  - Tamaño: ancho 0,32 × `s` × (0,7 + 0,5·`fk`) y alto 0,62 × `s` × (0,6 + 0,7·`fk`) m, con `s` = 1 en `Smoke` y 0,75 en los otros dos.
  - Opacidad: 0,85 × (0,5 + 0,5·`fk`).
- **Halo**: un sprite redondo y suave de 1,7 × `s` m, **#FF6A1E**, aditivo, 0,15 m por encima del foco, con opacidad 0,28 × (0,75 + 0,25 × sen(31·`t` + f) × sen(17·`t`)).
- Con un **desfase** de tiempo por nave, para que dos naves con fuego no parpadeen igual.
- No cambia con PARTICLES.
- No ilumina el casco ni la pista (iluminación, fuera de alcance).

## 6. Brasas

| | Nivel 2 | Nivel 3 |
|---|---|---|
| Brasas por segundo (HIGH) | **3** (en `Smoke`) | **5** por foco (15 en total) |

- Nacen en un foco al azar (de los activos), 0,1 m por encima.
- **Velocidad**: la de la nave, menos un retraso de mín(9 m/s; 10 % de la velocidad de la nave) × 0,6–1,4 hacia atrás; más ±1,5 m/s a los lados y 1–3 m/s hacia arriba.
- **Movimiento**: gravedad suave de **2 m/s²** y un vaivén lateral de 2 × sen(5·`t` + fase) m/s².
- **Vida**: 0,5–1,0 s.
- **Aspecto**: un trazo corto (velocidad × 0,04 s) de naranja (1; 0,55; 0,14) × (1 − `u`) × (0,7 + 0,3 × sen(40·`t` + fase)), mezcla aditiva, la cola a negro. No rebotan ni chocan.
- **PARTICLES**: × 0,25 / 0,5 / 1.
- Como mucho **400** brasas vivas. Si la simulación de chispas de `efectos-1-choques` admite gravedad y vaivén por chispa, se usa esa (y su máximo de 3000). Si no, va un sistema pequeño aparte y **se avisa**.

## 7. Interacción con otros sistemas

- **Al subir de nivel**: el humo, el fuego y las brasas del nivel nuevo empiezan al momento. El daño no baja en carrera, así que no hay bajada.
- **Solo RACE**. En TIME TRIAL no hay daño, así que no hay nada.
- **Rivales**: mismos efectos con su nivel de daño.
- **Terminada**: sigue saliendo (la nave sigue dañada).
- **Pausa**: todo congelado (humo, fuego, brasas y sus parpadeos).
- **Recolocación**: se borran **al momento** el humo y las brasas **de esa nave** (si no, quedaría una nube donde estaba). El fuego sigue, porque va con la nave.
- **RESTART** o salir de la carrera: se borra todo y la nave vuelve a nivel 0.
- **Atravesables** (recolocación): el humo y el fuego no parpadean con el casco.
- **Vista de cabina**: en tu nave el casco no se dibuja; el humo y las brasas de tus focos **sí** se ven si caen en la vista, y el fuego **no** (va pegado al casco). Si molesta, **se avisa**.
- **Holograma del selector, menú principal y fantasma**: nada (como hasta ahora).
- **Efectos de velocidad, choques, derrape y aterrizaje**: conviven sin cambios.

## 8. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de humo de daños.

| Parámetro | Valor |
|---|---|
| Focos por nivel | 2: `Smoke` · 3: `Smoke`, `Smoke_2`, `Smoke_3` |
| Posición de `Smoke_2` y `Smoke_3` | Perfil visual de cada nave (sección 3) |
| Bocanadas por segundo | 2: 6,5 · 3: 10 / 7 / 7 |
| Vida | 2: 0,9–1,3 s · 3: 1,0–1,5 s |
| Radio | 2: 0,35 → 1,5 m · 3: 0,45 → 2,2 m |
| Subida | 2: 0,6–1,0 m/s · 3: 0,8–1,4 m/s |
| Retraso del penacho | 6 % de la velocidad, entre 1,5 y 7 m/s |
| Remolino lateral / vertical | 0,6 / 0,12 m/s |
| Color / opacidad | 2: (0,42; 0,44; 0,50) / 0,42 · 3: (0,17; 0,175; 0,20) / 0,62 |
| Tono aleatorio | × 0,85–1,15 |
| Giro | ±0,7 rad/s |
| Entrada | 0,08 s |
| Tinte del fuego | (0,75; 0,30; 0,10) · 30 % de la vida · peso 0,8 |
| Máximo de bocanadas | 420 (v1.1) |
| Llamas por foco / tamaño / opacidad | 4 / 0,32 × 0,62 m / 0,85 |
| Halo del fuego | 1,7 m · 0,28 |
| Tamaño en los focos 2 y 3 | 0,75 |
| Brasas por segundo | 2: 3 · 3: 5 por foco |
| Brasas: retraso / lados / arriba | mín(9; 10 % v) / ±1,5 / 1–3 m/s |
| Brasas: gravedad / vida | 2 m/s² / 0,5–1,0 s |
| Máximo de brasas | 400 |
| PARTICLES LOW / MEDIUM / HIGH | 25 / 50 / 100 % (humo y brasas) |

## 9. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot:

- **Ajustes del humo de daños** (datos): todo lo de la sección 8.
- **Focos de humo** (puro): con el nivel de daño y el perfil visual de la nave, da los focos activos y su tamaño (1 o 0,75).
- **Emisor del humo** (puro): con los focos, la posición y la velocidad de la nave, `delta`, la calidad de partículas y un generador aleatorio inyectado, da las bocanadas nuevas. Lleva un acumulador por foco.
- **Simulación del humo** (pura): `Update(delta)` con el retraso, el frenado y el remolino. Da posición, radio, giro, color y opacidad de cada bocanada, con el tinte del fuego.
- **Fuego** (puro): con el tiempo, el desfase de la nave y el foco, da la posición local, el tamaño y la opacidad de cada llama y del halo.
- **Brasas** (puro): emisor y simulación (o los de las chispas de `efectos-1-choques`, sección 6).
- **Nodos de Godot**: solo dibujan:
  - un sistema de sprites para el humo de **todas** las naves (por ejemplo, un `MultiMeshInstance3D` de cuadrados que miran a la cámara, con un índice de textura por instancia, o el sistema del humo de los choques si sirve);
  - las llamas y el halo, colgados de cada nave;
  - las brasas, en una malla de líneas.

  Si con `GPUParticles3D` sale igual y más barato, se puede usar, **avisando antes**. Sin reglas dentro.
- **Cascos 3D**: se quita el humo viejo y queda solo el vacío `Smoke`, que ahora lee este sistema.

## 10. Pruebas

### Pruebas unitarias mínimas

Focos y humo (etapa 1):
1. Nivel 0 y 1: ningún foco. Nivel 2: solo `Smoke`. Nivel 3: los tres, con tamaño 1 / 0,75 / 0,75.
2. Nivel 2 en HIGH: 6,5 bocanadas en 1 s (±1), con `delta` 1/60 y 1/30. Nivel 3: 24 (10 + 7 + 7). LOW, nivel 3: 6.
3. Retraso: a 400 km/h, 6,67 m/s; a 200 km/h, 3,33; parado, 1,5; a 500 km/h, como mucho 7.
4. Una bocanada recién nacida va a la velocidad de la nave menos el retraso (dentro del ±20 %).
5. Opacidad: 0 al nacer, la máxima del nivel a los 0,08 s (× el desvanecido), 0 al acabar su vida.
6. Radio: a la mitad de la vida, más cerca del final que del principio.
7. Con 300 bocanadas vivas, no salen más.
8. Con `delta` 0 (pausa), nada cambia.
9. Con la misma semilla, las mismas bocanadas.

Fuego y brasas (etapa 2):
10. Sin fuego por debajo del nivel 3. Con nivel 3: 4 llamas y un halo por foco.
11. Tinte: en `u` = 0, color mezclado con naranja al 80 %; en `u` = 0,3 y después, sin tinte.
12. Brasas: nivel 2 → 3 por segundo; nivel 3 → 15. LOW en nivel 3 → 3,75 (±1 en 1 s).
13. Recolocación: se borran las bocanadas y las brasas de esa nave, no las de las demás.

Las pruebas existentes siguen pasando. Las de los cascos que comprobaban el humo viejo (7 / 20 partículas, colores) se quitan.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Humo nuevo** | Quitar el humo viejo de los tres cascos. Secciones 3 y 4, y las partes de 7 que tocan el humo | Pruebas 1–9 · Prueba manual 1–8 · Commit `feat:` |
| **2 · Fuego y brasas** | Secciones 5 y 6, el tinte de 4.3 y lo que queda de 7 · versión | Pruebas 10–13 · Prueba manual 9–15 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Humo nuevo**
1. RACE, cámara exterior: chocar hasta el nivel 2. Sale humo gris de `Smoke`, en nubes con forma que giran y se arremolinan, **pegado a la nave**: una estela corta, no un chorro de 100 m.
2. Acelerar a fondo y con boost: el humo se sigue quedando cerca de la nave.
3. Frenar hasta pararse: el humo sube y se queda atrás despacio.
4. Seguir chocando hasta el nivel 3: humo oscuro y mucho más denso, desde los tres focos.
5. Comparar con la maqueta (A · PENACHO · 3 focos) en LENTO, a 150, 300 y 400 km/h: que se parezca.
6. Mirar las tres naves (VECTOR, BLADE y HAMMER): los focos salen de sitios con sentido en cada una.
7. Recolocarse con daño: no queda una nube en el sitio de antes.
8. TIME TRIAL: nunca hay humo.

**Etapa 2 · Fuego y brasas**
9. Nivel 2: unas pocas brasas que suben y se quedan atrás. Sin fuego.
10. Nivel 3: llamas pequeñas que parpadean en cada foco, con su halo, y el humo saliendo naranja que se vuelve gris. Más brasas.
11. Pausa: llamas, humo y brasas congelados.
12. PARTICLES en LOW: mucho menos humo y brasas. El fuego, igual.
13. Cabina con daño 3: se ve algo de tu humo y de tus brasas, sin fuego. Si molesta, avisar.
14. Ir detrás de un rival con daño 3: se ve su humo, su fuego y sus brasas, desde fuera y desde la cabina.
15. RACE con varios rivales dañados: mirar los fps.

## 12. Fuera de alcance

- **B · ENVUELTO** (descartado).
- Humo en los niveles 0 y 1.
- Luz del fuego sobre el casco, la pista u otras naves.
- Piezas sueltas, chispas eléctricas o explosiones.
- Humo en TIME TRIAL, en el fantasma o en el selector.
- Cambiar el modelo de daño, las quemaduras o el parpadeo de luces.
- Sonido y vibración del mando.
