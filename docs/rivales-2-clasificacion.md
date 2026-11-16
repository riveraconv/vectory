# VECTORY — Spec: Rivales 2 · Clasificación y pilotos

Versión 1.0 · 2026-11-16 · Estado: **propuesta, pendiente de validar**

Segunda spec de rivales. Da **nombre** a los pilotos de la IA y calcula la **clasificación**: la posición en directo, el intervalo con cada rival, el orden de llegada y la **estimación** de los que no han terminado cuando llegas tú. En el HUD, la posición `N / 8` se sustituye por un **panel de clasificación**, y el **minimapa** pasa a la derecha, debajo del cronómetro.

Plan de las specs de rivales:

1. Parrilla llena (hecha).
2. **Clasificación y pilotos** (esta).
3. Contacto: choques entre naves, esquivar y adelantar.
4. Personalidades y dificultad.

Diseño de referencia: **`design/clasificacion-hud.html`** («VECTORY · Clasificación en el HUD · Versiones»). Es la versión elegida: **B · PANEL**, con **apodos** e **intervalo**, **sin** rombo de nave y con el minimapa **redondo y pequeño a la derecha**. Las funciones `standings`, `drawColumn` y `drawHud` de la página son la referencia de medidas y colores. Si algo no cuadra entre esta spec y la página, **manda la página** y se avisa. Los intervalos de la página son inventados: los de verdad salen de la sección 3.3.

Se apoya en lo que ya existe tras la spec 1: la alineación de rivales, las naves de la IA con su `RaceTracker`, la lista de naves en carrera, `RaceResult`, el `Hud` (posición, vuelta, tiempos, radar y minimapa), la franja de resultados y el panel de pruebas (F9).

Rama: `feature/rivals-standings`, que sale de `develop` con `feature/rivals-grid` ya mergeada. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Cambio respecto a specs anteriores: la regla del HUD «la posición, la vuelta y los tiempos no se mueven nunca» pasa a ser «**la clasificación, la vuelta y los tiempos no se mueven nunca**». La posición ya no es un número suelto: va dentro de la clasificación.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 0 · Inventario**: mirar y apuntar, sin cambiar nada.
- **Etapa 1 · Pilotos y clasificación en directo**:
  - 20 apodos; en cada carrera salen 7 al azar. Tú eres `PLAYER`.
  - Posición de los 8 en todo momento.
  - Intervalo de cada rival contigo.
  - En el HUD: el **panel de clasificación** arriba a la izquierda, sin el `N / 8`, y el **minimapa** a la derecha.
- **Etapa 2 · Clasificación final**:
  - Orden de llegada real.
  - Cuando llegas tú, **estimación** del tiempo de los que faltan.
  - La clasificación se va corrigiendo con los tiempos reales mientras ellos terminan, aunque tú ya estés en resultados.
  - No hace falta esperar a nadie: los resultados salen como siempre y se pueden dejar en cualquier momento.
- **Etapa 3 · Clasificación en resultados**: la tabla de los 8 en la franja de resultados. **Necesita maqueta antes** (sección 7).
- TIME TRIAL: sin clasificación. Solo cambia el sitio del minimapa (3.4).

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo se calcula hoy la posición (`RaceResult`, la carrera) y si hay ya una **distancia recorrida** por nave a lo largo del eje (vueltas + `s`).
2. Qué guarda cada `RaceTracker` de los rivales (vueltas, checkpoint, tiempo de llegada) y si la carrera sabe **en qué orden** han terminado.
3. Cómo dibuja hoy el `Hud` la posición y el minimapa, y qué parte se usa también en la vista de cabina (las cabinas 3D llevan el radar y el minimapa en sus pantallas).
4. En qué sitio sale el minimapa en TIME TRIAL.
5. Cómo se identifica hoy a cada rival (`rival_1`…`rival_7`) en el panel de pruebas.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Pilotos y clasificación en directo

### 3.1 Pilotos

- Catálogo **fijo** de 20 pilotos, con un `id` y un **apodo** en mayúsculas. Los apodos son los de la página de referencia:

| `id` | Apodo | `id` | Apodo |
|---|---|---|---|
| `pilot_01` | RAZOR | `pilot_11` | RIPTIDE |
| `pilot_02` | EDGE | `pilot_12` | WRAITH |
| `pilot_03` | BUTCHER | `pilot_13` | KESTREL |
| `pilot_04` | VIPER | `pilot_14` | SPARK |
| `pilot_05` | NOMAD | `pilot_15` | TITAN |
| `pilot_06` | HEX | `pilot_16` | VANDAL |
| `pilot_07` | BLITZ | `pilot_17` | ORBIT |
| `pilot_08` | CINDER | `pilot_18` | FURY |
| `pilot_09` | JACKAL | `pilot_19` | SHADE |
| `pilot_10` | ONYX | `pilot_20` | ROOK |

- La **alineación** de la spec 1 gana el piloto: en cada carrera salen **7 pilotos distintos** al azar, con el mismo generador inyectado. Con la misma semilla, los mismos pilotos.
- El piloto **no** decide la nave ni la pintura: siguen saliendo al azar por separado, como en la spec 1.
- Tú eres **`PLAYER`**, de momento como texto fijo. Cuando haya nombre de jugador, irá en su sitio.
- El catálogo es el sitio donde irá la **personalidad** de cada piloto en la spec 4. En esta spec solo tiene `id` y apodo.
- En el panel de pruebas (F9), el bloque RIVALS enseña el **apodo** en vez de `rival_N`.

### 3.2 Posición en directo

Cada nave tiene una **distancia de carrera**: vueltas completadas × largo del circuito + distancia recorrida desde la meta (`s`).

- En la parrilla, antes del primer cruce de meta, la distancia es **negativa** (`s` − largo). Así el orden de salida es el de la parrilla.
- Para que no cuente ir hacia atrás ni cortar, la distancia solo se usa **entre el último checkpoint pasado y el siguiente**: una nave no puede ir por delante de otra que lleva más checkpoints de esa vuelta.
- Orden:
  1. Las que **han terminado**, en el orden en que cruzaron la meta.
  2. Las demás, de **más a menos distancia de carrera**.
  3. Empate exacto: va delante la que salió **más adelante en la parrilla**.
- Se recalcula **cada frame**.
- La posición que da la carrera es **la misma** para el HUD y para resultados. Nadie la calcula aparte.
- Las naves de prueba (F7) y el fantasma **no** entran.

### 3.3 Intervalo

Lo que se enseña al lado de cada rival es su diferencia **contigo**, en segundos:

- **Rival delante**: cuánto tiempo hace que pasó por el punto donde estás tú ahora. Se enseña `▲ 0.84`.
- **Rival detrás**: cuánto tiempo hace que pasaste tú por el punto donde está él ahora. Se enseña `▼ 1.20`.
- Para eso, cada nave apunta el **tiempo de carrera** al pasar cada **10 m** de distancia de carrera (sin pausa, con el cronómetro). El intervalo se interpola entre las dos marcas más cercanas.
- Dos decimales. A partir de 60 s, `1:02.31`.
- **Una vuelta o más** de diferencia en distancia: `+1 LAP`, `+2 LAPS` en vez del tiempo (o `−1 LAP` si es él el que te saca la vuelta).
- **Antes de tener dato** (al salir de la parrilla, hasta que los dos hayáis pasado ese punto): `—`.
- Se **refresca cada 0,25 s**, no cada frame, para que los números no tiemblen. La posición sí cambia al momento.
- Un rival que **ya ha terminado** sigue con el intervalo de su llegada. Tú, al terminar, dejas de actualizar los tuyos: se quedan los del momento de cruzar la meta.

### 3.4 HUD

**Se quita** el texto de posición `N / 8` de arriba a la izquierda.

**Panel de clasificación** (variante B de la página), arriba a la izquierda, en el espacio de 1280 × 720 del HUD:

- **Cinco huecos fijos**: tú en el **tercero**, los dos que van delante encima y los dos que van detrás debajo.
  - Si vas 1º o 2º, los huecos de arriba que no tienen a nadie se quedan **vacíos**. Igual abajo si vas 7º u 8º. **La lista no se recoloca**: tu fila está siempre en el mismo sitio.
- Filas de **22 px**. Tu fila con la línea de base en **y = 80**. Columna con el borde izquierdo en **x = 52**.
- Contenido de cada fila (tamaños de la página):
  - **Puesto**: Share Tech Mono 15 px, alineado a la derecha. Tuyo en cian, de los rivales en magenta.
  - Solo en tu fila, al lado del puesto, **`/8`** (participantes) pequeño: 10 px, azul grisáceo, un poco más alto.
  - **Apodo**: 15 px. `PLAYER` en cian; los rivales en blanco azulado `#E9F6FF`.
  - **Intervalo** (3.3): 13 px, azul grisáceo, alineado a la derecha. Tu fila no lleva.
- **Opacidad** según lo lejos que esté de ti en puestos: tú y los pegados **100 %** / **62 %** a un puesto / **30 %** a dos.
- **Panel**:
  - fondo `#05070D` con un **degradado vertical**: transparente arriba, 66 % entre el 30 % y el 70 % de su alto y transparente abajo;
  - línea cian de **2 px** a la izquierda con el mismo degradado;
  - **franja** cian al 12 % detrás de tu fila.
- Colores desde `Palette`. Fuente: la del HUD, como en la página.

**Minimapa**:

- Se queda **redondo** y igual por dentro (ventana, gira contigo, rivales como puntos), pero:
  - **radio 56 px** (antes 78);
  - **arriba a la derecha, debajo del cronómetro**: centrado en horizontal con el bloque de LAP, tiempo de vuelta y BEST (tomando su línea más ancha), con el borde de arriba **32 px por debajo** de la línea de BEST.
- Así en **RACE y en TIME TRIAL**: un solo sitio para el minimapa. En TIME TRIAL, arriba a la izquierda sigue `TIME TRIAL` y el nombre del circuito.

**Vistas**:

- En **persecución** se ve todo lo anterior.
- En **cabina**, el panel de clasificación va también arriba a la izquierda de la pantalla, como iba la posición, y se inclina con la nave como el resto del HUD de cabina. El minimapa y el radar siguen en las **pantallas de la cabina 3D** y no cambian. Si alguna cabina todavía usa el minimapa del HUD de pantalla, ese pasa a la derecha igual.
- Radar, velocidad, boost, vueltas y tiempos: sin cambios.

### 3.5 Criterio de aceptación de la etapa 1

- Se ve como la página de referencia, con datos reales.
- Al salir, el orden es el de la parrilla. Al adelantar a un rival (atravesándolo, aún no hay contacto) las filas cambian al momento.
- Los intervalos cuadran con lo que se ve en pista y no tiemblan.
- En 1º y 8º, los huecos vacíos se quedan vacíos y tu fila no se mueve.
- El minimapa está debajo del cronómetro, en RACE y en TIME TRIAL.

## 4. Etapa 2 · Clasificación final

### 4.1 Llegada

- Cada nave que completa la última vuelta queda **clasificada** con su **tiempo total real**, en el orden en que cruza la meta.
- Tu llegada no cambia: se para tu cronómetro, pierdes el control y la franja de resultados sale a los 1,5 s.

### 4.2 Estimación

En el momento en que **llegas tú**, cada rival que no ha terminado recibe un **tiempo estimado**:

- Ritmo: su velocidad media en la **última vuelta completa**. Si aún no ha completado ninguna, su media desde el GO.
- Tiempo estimado = tiempo de carrera ahora + distancia que le falta / ese ritmo.
- **Orden** de los no terminados: el que llevan **en pista** en ese momento (3.2), no el de la estimación. Si una estimación saliera más baja que la del que va delante, se sube a la de ese + 0,001 s, para que los tiempos no contradigan el orden.
- Un rival estimado nunca queda por delante de ti: si llegas tú antes, estás delante.

### 4.3 Cuando terminan los demás

- Tras tu llegada, los rivales **siguen corriendo** como en la spec 1 (también con la franja de resultados en pantalla).
- Cuando uno cruza la meta, su tiempo **estimado** pasa a **real**. **Tu puesto y el de los que llegaron antes que tú no cambian nunca.** Los de detrás de ti se **reordenan** con cada llegada: primero los que ya han llegado, por su tiempo real; después los que siguen en pista, en su orden. Si una llegada cambia de puesto a alguien, se apunta en la consola. *(Decisión del usuario en la etapa 3: con los rivales en grupo, pasaba a menudo que se adelantaban entre tu llegada y la suya, y la tabla final enseñaba tiempos que contradecían los puestos.)*
- Cada fila sabe si su tiempo es **estimado** o **real**. Los resultados lo enseñan (etapa 3).
- **Saltar**: no hay que esperar. RESTART RACE y BACK TO MAIN MENU funcionan desde el primer momento, como ahora. Al salir, lo que quede se queda estimado y no se guarda nada de los rivales.
- La pausa congela también a los rivales y la clasificación.

### 4.4 Lo que guarda `RaceResult`

- La **clasificación completa**: por cada uno de los 8, puesto, piloto (`PLAYER` o apodo), nave, tiempo total, si es **estimado**, mejor vuelta y diferencia con el primero.
- Tu posición final sale de ahí. La franja de resultados actual la sigue enseñando igual (`3rd / 8`) hasta la etapa 3.
- Los **récords** de RACE no cambian: son tuyos, sea cual sea tu puesto.

### 4.5 Criterio de aceptación de la etapa 2

- Llegar 1º: los demás salen estimados y van pasando a reales mientras miras los resultados (comprobar en la consola o en el panel F9).
- Llegar en mitad: los que llegaron antes tienen tiempo real; los de detrás, estimado.
- Dejar los resultados al momento no da errores.

## 5. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Huecos del panel | 5 (2 encima, tú, 2 debajo) | HUD |
| Alto de fila | 22 px | HUD |
| Opacidad a 1 / 2 puestos | 62 % / 30 % | HUD |
| Refresco del intervalo | 0,25 s | HUD |
| Paso de las marcas de tiempo | 10 m | Carrera |
| Radio del minimapa | 56 px | HUD |
| Hueco entre BEST y el minimapa | 32 px | HUD |

## 6. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Catálogo de pilotos** (puro, datos): `id` y apodo. Preparado para la personalidad de la spec 4.
- **Alineación**: gana el sorteo de pilotos (7 distintos de los 20).
- **Clasificación** (pura):
  - con el estado de cada nave (distancia de carrera, checkpoints de la vuelta, terminada y cuándo, puesto de parrilla), da el orden;
  - en tu llegada, fija el orden y calcula las estimaciones;
  - luego acepta las llegadas reales.
  - Sin nodos.
- **Marcas de tiempo** (puro): por nave, tiempo de carrera cada 10 m; y la función de intervalo entre dos naves.
- **Formato** (puro): intervalo (`▲ 0.84`, `▼ 1.20`, `+1 LAP`, `—`) y los cinco huecos del panel a partir de la clasificación y tu puesto.
- **`Hud`**: el panel nuevo y el minimapa en su sitio nuevo. Solo **lee** la clasificación.
- **`RaceResult`**: la clasificación completa (4.4).

## 7. Etapa 3 · Clasificación en resultados

La franja de resultados tiene hoy tres columnas: resultado (posición), tiempos (TOTAL TIME, BEST LAP y sectores) y opciones. La tabla de los 8 no cabe tal cual, así que **antes de esta etapa se hace una maqueta** con varias composiciones para elegir. Lo que tiene que enseñar, como mínimo:

- los 8 por orden: puesto, apodo (o `PLAYER`, resaltado) y tiempo total o diferencia con el primero;
- qué tiempos son **estimados** y que se corrigen en directo al llegar los rivales;
- lo que ya hay: tu posición, TOTAL TIME, BEST LAP, sectores, NEW RECORD y las opciones.

Esta sección se completa con la maqueta elegida. Hasta entonces, las etapas 1 y 2 se pueden implementar y validar.

### 7.1 Composición elegida

Maqueta: **`designs/clasificacion-resultados.html`**, versión **B · TABLA A LA DERECHA**, con **tiempos totales** (no diferencia) y la **etiqueta `EST`** en los estimados (la opción por defecto de la maqueta). Su función `variantB` y las que usa (`resultBlock`, `timesBlock`, `table`, `optionsBlock` en horizontal) son la referencia de medidas y colores. Si algo no cuadra, **manda la maqueta**.

- La franja crece de 340 a **440 px**. Tres columnas como hoy con separadores en x = 470 y 860: **resultado** (RACE FINISHED, posición, nave) y **tiempos** (TOTAL TIME, BEST LAP y sectores, con NEW RECORD en la línea de la etiqueta, como ahora) no cambian; la **tabla** ocupa la tercera columna (x = 890, 340 px de ancho).
- **Opciones** RESTART RACE / BACK TO MAIN MENU en **una fila**, centradas, abajo de la franja, debajo de una línea azul tenue. ↑ ↓ (y ← →) cambian la opción; ratón igual que ahora.
- **Tabla**: cabecera `CLASSIFICATION` (Share Tech Mono 14, azul grisáceo) y, a la derecha, `N PROVISIONAL` en naranja con un punto que late mientras quedan estimados, o `FINAL` en cian. Ocho filas de 32 px: puesto (magenta; cian el tuyo), apodo (blanco azulado; `PLAYER` en cian), nave (11 px, azul grisáceo tenue) y tiempo total alineado a la derecha (el primero en Orbitron cian; el tuyo en cian; el resto en blanco azulado). Tu fila con franja cian al 12 % y una línea de 2 px a la izquierda.
- **Estimados**: tiempo al 60 % con la etiqueta `EST` en un recuadro a su derecha. Al llegar el rival, la fila **destella** en cian (0,6 s) y el tiempo pasa a real.
- La tabla se actualiza en directo con la franja en pantalla; la pausa la congela.

## 8. Pruebas

### Pruebas unitarias mínimas

Pilotos y alineación (etapa 1):
1. El catálogo tiene 20 pilotos con `id` y apodo sin repetir.
2. La alineación da 7 pilotos distintos. Con la misma semilla, los mismos.

Clasificación en directo (etapa 1):
3. En la parrilla, el orden es el de las marcas.
4. Con más distancia de carrera, va delante. Con más vueltas, va delante aunque su `s` sea menor.
5. Una nave que va hacia atrás o se salta un checkpoint no adelanta a una que lleva más checkpoints de esa vuelta.
6. Las terminadas van primero, en orden de llegada.

Intervalo y formato (etapa 1):
7. Rival que pasó por tu punto hace 0,84 s → `▲ 0.84`. Tú pasaste por su punto hace 1,2 s → `▼ 1.20`.
8. Una vuelta de diferencia → `+1 LAP`; dos → `+2 LAPS`. Sin dato → `—`.
9. Huecos del panel: en 4º, puestos 2–6. En 1º, los dos de arriba vacíos y tú en el tercero. En 8º, los dos de abajo vacíos.

Clasificación final (etapa 2):
10. Al llegar tú, los no terminados reciben tiempo estimado con la fórmula de 4.2, en orden de pista, y ninguno por delante de ti.
11. Si una estimación es menor que la del de delante, queda en la del de delante + 0,001 s.
12. Una llegada real pasa la fila de estimada a real; los de detrás de ti se reordenan por su tiempo real, delante de los estimados, y tu puesto y el de los llegados antes que tú no cambian.
13. `RaceResult` lleva los 8 con todos los campos de 4.4.

Las pruebas existentes siguen pasando. Las que comprobaban el texto `N / 8` del HUD se adaptan al panel.

## 9. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Pilotos y clasificación en directo** | Sección 3 | Pruebas 1–9 · Prueba manual 1–7 · Commit `feat:` |
| **2 · Clasificación final** | Sección 4 | Pruebas 10–13 · Prueba manual 8–11 · Commit `feat:` |
| **3 · Resultados** | Sección 7, tras la maqueta · versión | Se define con la maqueta |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1**
1. RACE: arriba a la izquierda, el panel con tu fila en el centro, los apodos y sin el `N / 8`. Comparar con la página de referencia.
2. Al salir, el orden es el de la parrilla. Atravesar a un rival: cambia de fila al momento.
3. Los intervalos tienen sentido con lo que se ve en pista: se acercan al alcanzar a alguien y no tiemblan.
4. Ir 1º y 8º (con el ritmo de los rivales del panel F9): los huecos vacíos se quedan vacíos y tu fila no se mueve.
5. El minimapa está a la derecha, debajo del cronómetro, más pequeño. Igual en TIME TRIAL.
6. Cabina de las tres naves: el panel arriba a la izquierda, inclinándose con la nave; radar y minimapa en sus pantallas como antes.
7. RESTART varias veces: cambian los apodos. F9 enseña los apodos.

**Etapa 2**
8. Ganar la carrera: en la consola o en F9, los demás estimados, y pasando a reales al ir llegando con los resultados en pantalla.
9. Llegar en mitad: delante, tiempos reales; detrás, estimados.
10. En resultados, BACK TO MAIN MENU nada más salir: sin errores.
11. Pausar en los resultados: los rivales se paran; al continuar, siguen llegando.

## 10. Fuera de alcance

- La **tabla** de clasificación en la franja de resultados (etapa 3, tras la maqueta).
- **Personalidad** de los pilotos (spec 4). Decidido: va con el **piloto**, no con la nave.
- Nombre del jugador elegible.
- Nombres de los rivales en el radar, en el minimapa o encima de las naves.
- Campeonatos y puntos. Decidido para cuando lleguen: los pilotos, sus naves y sus pinturas se mantienen durante todo el campeonato.
- Choques entre naves (spec 3).
- Animaciones al cambiar de puesto, sonido y mando.
