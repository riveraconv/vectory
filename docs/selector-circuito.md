# VECTORY — Spec: Selector de circuito

Versión 1.0 · 2026-11-03 · Estado: **implementada y validada**

> Desviación aceptada: los checkpoints de TEST OVAL van a distancias iguales de la vuelta (1/4, 2/4, 3/4), no a la salida de cada curva (sección 6): el formato de `TrackDefinition` no lo permite y se decidió no cambiarlo.

Añade una pantalla **SELECT CIRCUIT** entre el menú principal y la carrera, en **RACE** y en **TIME TRIAL**. Además se crea un segundo circuito, **TEST OVAL**, plano y sencillo, solo de pruebas. Sirve para probar el selector de verdad y, más adelante, la física 3D en algo simple.

El circuito actual (`circuit_01`) **no cambia**: su trazado y su firma siguen igual, así que los récords y fantasmas guardados siguen valiendo. El circuito con peraltes y cambios de rasante irá después, en sus propias specs, como un circuito más.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Selector de circuito**: *Selector C · briefing con panel de telemetría · elegida*. A y B quedan descartadas. Los datos del lienzo son de ejemplo: los reales son los de la sección 5. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: `MainMenu`, `MenuModel` / `MenuOption`, la lista de opciones reutilizable, `GameSession` (modo y circuito), `CircuitCatalog`, `TrackDefinition` (`Id`, `DisplayName`), `Track` y su `Centerline`, checkpoints y parrilla, el almacenamiento de récords y la validación de fantasmas, la pantalla RECORDS, `Palette`, las fuentes y `SceneChanger`.

Rama: `feature/circuit-select`, que sale de `develop` con `feature/camera` ya mergeada.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- En el menú principal, **RACE** y **TIME TRIAL** ya no cargan la carrera directamente: abren **SELECT CIRCUIT** en ese modo.
- La pantalla es tipo *briefing*:
  - A la izquierda, el **nombre del circuito** en grande y debajo una **miniatura 3D** enorme del trazado wireframe, inclinada y girando despacio sobre una rejilla.
  - A la derecha, un **panel de telemetría** con los datos del trazado, el **perfil de elevación** por sectores, los **récords del modo** y las opciones **START** / **BACK**.
- Se cambia de circuito con **A/D o ←/→** (o con las flechas `‹ ›`) y se elige con **START**.
- Nuevo circuito **TEST OVAL** (`test_oval`), marcado como circuito de pruebas con una etiqueta naranja `TEST`. Un único interruptor oculta todos los circuitos de pruebas cuando toque.
- La pantalla RECORDS pasa a tener dos circuitos (sus flechas `‹ ›` se activan solas).
- Todos los textos en inglés.

## 2. Flujo

| Desde | Acción | Resultado |
|---|---|---|
| Menú principal | **RACE** | SELECT CIRCUIT en modo RACE |
| Menú principal | **TIME TRIAL** | SELECT CIRCUIT en modo TIME TRIAL |
| SELECT CIRCUIT | A/D o ←/→, o clic en `‹ ›` | Circuito anterior / siguiente (da la vuelta) |
| SELECT CIRCUIT | **START** | `GameSession` queda con el modo y el circuito; se carga la carrera |
| SELECT CIRCUIT | **BACK**, o ESC | Vuelve al menú principal con la opción del modo (RACE o TIME TRIAL) seleccionada |
| Pausa | **RESTART** | Repite el mismo modo y circuito, sin pasar por el selector (como ahora) |
| Pausa / resultados | **BACK TO MAIN MENU** | Menú principal (como ahora) |

- SELECT CIRCUIT va **dentro de la escena del menú principal**, como RECORDS, sin cambiar de escena. Mientras está abierta se ocultan:
  - el título VECTORY / RACING y la lista del menú;
  - la nave, la rejilla de horizonte y la sombra del fondo del menú. La pantalla tiene su propio fondo (sección 3.1).
- La versión de abajo a la izquierda sigue visible.
- Si hacerlo dentro de `MainMenu` lo complica mucho, se puede hacer como escena aparte, siempre que al volver quede seleccionada la opción del modo. Avisar antes de elegir esa vía.
- **Circuito recordado**: al abrir el selector aparece el último circuito elegido en esta ejecución del juego (el de `GameSession`). La primera vez, el primero del catálogo. No se guarda en disco.
- Al abrirse, la opción seleccionada es **START**.

## 3. Aspecto

Medidas sobre la pantalla de referencia de 1280×720, escaladas como el resto de menús. Colores siempre desde `Palette`.

### 3.1 Fondo y miniatura 3D

- Fondo #05070D (`Palette.Background`), con el mismo glow/bloom del `WorldEnvironment` que el resto del juego.
- **Miniatura**: el trazado del circuito como modelo 3D de líneas, generado a partir de su `Centerline` y su ancho:
  - Dos bordes de pista en **cian**.
  - Línea central discontinua en **azul** #245BFF.
  - Línea de meta en **naranja** #FF9D32.
  - Sigue la altura real del eje. Hoy todos los circuitos son planos; el día que haya desniveles, la miniatura los enseñará sin cambiar nada.
  - Sin barreras, checkpoints, parrilla ni arcos: solo el trazado, para que se lea.
- **Rejilla** cuadrada bajo el trazado, en azul tenue, como en el lienzo. Se reutiliza el generador de rejilla del menú si encaja.
- El trazado se **escala para que quepa** siempre en el mismo tamaño en pantalla, sea cual sea la longitud del circuito, y se centra por su caja.
- Encuadre: ocupa la zona izquierda (en el lienzo, unos 700×700 px a partir de x ≈ 60, y ≈ 70, por debajo del bloque del nombre). Se ve **inclinado** (~62° respecto a la vista cenital) y **gira** sobre su eje vertical, una vuelta cada **50 s**.
- Al cambiar de circuito, la miniatura cambia **al momento**. El giro sigue con el ángulo que llevaba.
- Implementación libre (por ejemplo, un `SubViewport` con su propia cámara, o nodos 3D en la escena del menú con otra cámara), siempre que no se rehaga la malla cada frame: se genera al cambiar de circuito.

### 3.2 Bloque del nombre (arriba a la izquierda, x = 72, y = 56)

- `RACE · SELECT CIRCUIT` o `TIME TRIAL · SELECT CIRCUIT`: Share Tech Mono ~15 px, cian, espaciado ~0.22 em.
- Nombre del circuito (`DisplayName` en mayúsculas): Orbitron Black ~54 px, cian, espaciado ~0.08 em, con halo.
  - Circuito de pruebas: a su derecha, a 16 px, la etiqueta `TEST` en Share Tech Mono ~13 px, **naranja**, con borde naranja de 1 px al ~70 %.
- Línea de descripción: Share Tech Mono ~14 px, azul grisáceo. Formato `CLOSED LOOP · CLOCKWISE` (o `COUNTERCLOCKWISE`). En un circuito de pruebas: `TEST TRACK · CLOCKWISE`.
- Fila de navegación: botón `‹` (44×44, borde azul de 1 px), contador `01 / 02` en Share Tech Mono ~14 px y botón `›`. Con un solo circuito, las flechas en azul al ~40 % (desactivadas); con más de uno, cian.

### 3.3 Panel de telemetría (derecha)

- Panel de **x = 836** hasta el borde derecho y de arriba abajo. Fondo #05070D al ~92 %, línea cian de 1 px a la izquierda con halo hacia la izquierda.
- Márgenes interiores: 52 px arriba, 40 px a la izquierda, 48 px a la derecha y 40 px abajo.
- Cabeceras de sección (`TRACK DATA`, `ELEVATION PROFILE`, `RECORDS · RACE`): Share Tech Mono ~13 px, cian, espaciado ~0.22 em. Separación de ~14 px antes de cada sección.

**TRACK DATA**: rejilla de 2 columnas, 4 filas. Cada dato lleva una línea azul vertical de 2 px a su izquierda, la etiqueta (Share Tech Mono ~11 px, azul grisáceo) y debajo el valor (Orbitron Medium ~17 px, cian). Orden y contenido en la sección 5.1.

**ELEVATION PROFILE**: gráfico de 356×70 px.
- Línea del perfil en cian, de la salida (izquierda) a la meta (derecha), según la distancia recorrida.
- Líneas guía horizontales arriba y abajo, en azul tenue, con las etiquetas `+20 M` y `−20 M` (sección 5.2).
- Una línea vertical discontinua en cada **checkpoint**, en su posición real sobre la vuelta, y las etiquetas `S1`, `S2`… centradas en cada sector, abajo.
- Hoy, en los dos circuitos, una línea plana en el centro.

**RECORDS · RACE / RECORDS · TIME TRIAL**: dos filas, etiqueta (Share Tech Mono ~12 px, azul grisáceo, columna de ~120 px) y valor (Orbitron Medium ~19 px, cian). Contenido en la sección 5.3.

**Opciones**: abajo del panel, **START** y **BACK**, con el estilo de la lista de opciones de RECORDS y resultados (Orbitron ~22 px, filas de 44 px, barra lateral 4×30 px con halo, texto seleccionado desplazado 16 px).

### 3.4 Pie

- Versión abajo a la izquierda, como en el menú.
- A su derecha, en x ≈ 300: `← → CIRCUIT · ↑ ↓ OPTION · ENTER SELECT`, en Share Tech Mono ~13 px, azul grisáceo al ~70 %.

## 4. Navegación

- **A / D** y **← / →** (`menu_left` / `menu_right`): circuito anterior / siguiente. Dan la vuelta, como en RECORDS. Con un solo circuito no hacen nada.
- **W / S** y **↑ / ↓**: mueven la selección entre START y BACK, dando la vuelta.
- **Enter / Espacio** (`menu_accept`): eligen la opción.
- **ESC**: equivale a BACK.
- **Ratón**: pasar por encima de START o BACK los selecciona y el clic los elige; clic en `‹ ›` cambia de circuito.
- Teclado y ratón comparten una sola selección.

## 5. Datos de cada circuito

Todos se **calculan** a partir del circuito (eje, ancho, checkpoints y parrilla), no se escriben a mano. Así el circuito 3D tendrá sus datos sin tocar nada. Se calculan al abrir la pantalla, una vez por circuito, y se guardan mientras está abierta.

### 5.1 TRACK DATA

| Etiqueta | Valor | Cómo se calcula |
|---|---|---|
| `LENGTH` | `4.2 KM` | Longitud del eje cerrado, en km con 1 decimal |
| `LAPS` | `3` en RACE, `FREE` en TIME TRIAL | El número de vueltas de RACE que ya usa la carrera, sin duplicarlo |
| `TURNS` | `9` | Número de curvas (sección 5.4) |
| `LONGEST STRAIGHT` | `820 M` | Tramo continuo más largo sin curva (sección 5.4), en m redondeado a 10 |
| `TRACK WIDTH` | `24 M` | Ancho de pista del `TrackDefinition`, en m |
| `SECTORS` | `4` | Número de sectores: checkpoints + 1, los mismos que usa el delta de TIME TRIAL |
| `MAX BANKING` | `0°` | Peralte máximo. Hoy no hay datos de peralte: siempre `0°`. Lo sustituirá la spec de pista 3D |
| `GRID` | `8 SHIPS` en RACE, `—` en TIME TRIAL | Número de marcas de parrilla del circuito |

Los valores de la tabla son de ejemplo, salvo `LAPS` y `MAX BANKING`.

### 5.2 Perfil de elevación

- Se muestrea la altura (Y) del eje a lo largo de la vuelta, desde la meta, en **120 puntos** repartidos por distancia.
- Escala vertical: de −E a +E, con E = la mayor altura absoluta del eje redondeada hacia arriba a múltiplos de 10 m, y como mínimo **20 m**. Las etiquetas muestran `+E M` / `−E M`.
- Las líneas de sector van en la distancia de cada checkpoint sobre el eje.

### 5.3 Récords

Del mismo almacenamiento que RECORDS, sin duplicar la lectura:

| Modo | Fila 1 | Fila 2 |
|---|---|---|
| RACE | `TOTAL TIME` y el mejor tiempo total | `BEST LAP` y la mejor vuelta de RACE |
| TIME TRIAL | `BEST LAP` y la mejor vuelta de TIME TRIAL | `GHOST`: `AVAILABLE` si hay fantasma válido para el circuito (mismas reglas que la pausa del Time Trial), `NONE` si no |

- Sin récord: `--:--.---` en azul grisáceo al ~60 %, como en RECORDS.
- Sin fechas en esta pantalla (están en RECORDS).

### 5.4 Curvas y rectas

Cálculo puro sobre el eje, en planta (ignorando la altura):

- En cada punto del eje se calcula el **radio de curvatura** (con un suavizado sobre una ventana de unos 20 m, para que el ruido de los puntos no cuente).
- Un punto es **de curva** si su radio es menor que **350 m**, y **de recta** si es mayor o igual.
- **Curva**: tramo continuo de puntos de curva con giro del mismo sentido. Cuenta como curva si gira en total **15° o más**. Dos tramos del mismo sentido separados por menos de **30 m** de recta son la misma curva. Si cambia el sentido del giro, empieza una curva nueva (una chicane son dos curvas).
- **Recta**: tramo continuo de puntos de recta. `LONGEST STRAIGHT` es el más largo. Como el circuito es cerrado, el tramo que cruza la meta se une con el del principio.
- Umbrales (350 m, 15°, 30 m, 20 m) como constantes en un único sitio, para ajustarlos si al verlo en pantalla el número de curvas no convence.

## 6. Circuito de pruebas TEST OVAL

- Nuevo `TrackDefinition` en `res://circuits/test_oval.tres`, con:
  - `Id = "test_oval"` y `DisplayName = "Test Oval"`.
  - Un campo nuevo **`IsTest`** (bool, exportado). `true` en el óvalo y `false` en `circuit_01`.
- Trazado: un óvalo plano en sentido horario, con dos rectas de **420 m** y dos curvas de 180° de radio constante **~120 m**, unos 1,6 km en total. Ancho, barreras, parrilla de 8, meta en la mitad de una recta y checkpoints con las mismas reglas y el mismo aspecto que `circuit_01`, todo generado como ya hace `Track`.
- **3 checkpoints**: a la salida de la primera curva, en la mitad de la recta opuesta y a la salida de la segunda curva.
- Si con el formato actual de `TrackDefinition` no se puede describir el óvalo de forma limpia, **se avisa antes** de cambiar el formato. `circuit_01` no puede cambiar de trazado ni de firma.
- Funciona en RACE y en TIME TRIAL con todo lo que ya existe: récords, fantasma, daño, barreras, recolocación, cámara y HUD.

### 6.1 Ocultar los circuitos de pruebas

- `CircuitCatalog` tiene **un único interruptor**, `IncludeTestCircuits` (constante o ajuste del proyecto, en un sitio), hoy en `true`.
- Con `false`, los circuitos con `IsTest` no salen ni en SELECT CIRCUIT ni en RECORDS. Sus récords y fantasmas se quedan en los archivos y se borran con RESET RECORDS, como los de cualquier circuito que no esté en la lista.
- Orden del catálogo: `circuit_01` y después `test_oval`.

## 7. Cambios en otras pantallas

- **Menú principal**: RACE y TIME TRIAL abren el selector en su modo en vez de cargar la carrera. Al volver con BACK, el menú selecciona la opción del modo.
- **RECORDS**: no cambia de código. Con dos circuitos en el catálogo, las flechas `‹ ›` y A/D funcionan y el contador muestra `1 / 2`. Comprobarlo en la prueba manual.
- **Carrera**: carga el circuito de `GameSession`, como ya hace. Sin cambios, salvo que haga falta para el óvalo.
- **HUD de TIME TRIAL**: ya muestra el `DisplayName` del circuito. Sin cambios.

## 8. Parámetros en el Inspector

| Parámetro | Valor |
|---|---|
| Tiempo de una vuelta completa de la miniatura | 50 s |
| Inclinación de la miniatura | 62° |
| Tamaño de la miniatura en pantalla | el del lienzo (~700 px) |
| Tamaño del nombre del circuito | 54 px |
| Tamaño de las opciones | 22 px |
| Puntos del perfil de elevación | 120 |
| Altura mínima de la escala del perfil | 20 m |

Los umbrales de curvas y rectas (sección 5.4) van como constantes en la lógica, no en el Inspector.

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Datos del trazado** (puro, sin Godot, con `System.Numerics` o tipos propios). A partir de los puntos del eje (cerrado), el ancho, las distancias de los checkpoints y el número de marcas de parrilla, devuelve:
  - longitud, sentido de giro (por el área con signo en planta), número de curvas, recta más larga;
  - perfil de elevación (muestras y escala), posiciones de los sectores sobre la vuelta, número de sectores y de plazas de parrilla.
- **Eje sin escena**: para calcular los datos y la miniatura hace falta el eje de cada circuito **sin montar la carrera**. Si hoy el eje solo se genera dentro del nodo `Track`, se saca a una clase pura que usen `Track`, la miniatura y los datos, sin duplicar código. Esto no debe cambiar ni un punto del eje de `circuit_01`: la prueba 10 lo comprueba con la firma del trazado.
- **Lógica de la pantalla** (pura): modo, circuito actual (índice), `NextCircuit()` / `PreviousCircuit()` con vuelta, opciones START / BACK con `MenuModel`, y qué hacer (nada, empezar con el circuito X, volver al menú). Sin archivos ni escenas.
- **Formato** (puro): `4.2 KM`, `820 M`, `24 M`, `0°`, `8 SHIPS` / `—`, `FREE`, `01 / 02`, `CLOSED LOOP · CLOCKWISE`, `TEST TRACK · …`, tiempos con `TimeFormat` o `--:--.---`, y `AVAILABLE` / `NONE`.
- **Miniatura** (nodo Godot): construye la malla de líneas desde el eje y el ancho, la escala y la centra, y la hace girar. Sin reglas.
- **Nodo de la pantalla** (`CircuitSelectScreen` o similar, en `scripts/Menu`): dibuja el bloque del nombre y el panel reutilizando los componentes existentes, traduce el input, llama a la lógica y rellena `GameSession` al empezar.
- **`CircuitCatalog`**: lista con los dos circuitos, el campo `IsTest` y el interruptor `IncludeTestCircuits`.

### Pruebas unitarias mínimas

Datos del trazado:
1. Un círculo de radio 100 m: longitud ≈ 628 m, 1 curva, recta más larga 0, sentido según el orden de los puntos (probar los dos).
2. El óvalo (rectas de 420 m, radio 120 m): longitud ≈ 1594 m, **2 curvas**, recta más larga ≈ 420 m.
3. Un rectángulo con esquinas redondeadas: 4 curvas.
4. Una chicane (izquierda y derecha seguidas): 2 curvas.
5. Una curva suave de menos de 15° no cuenta. Dos curvas del mismo sentido separadas por 20 m de recta cuentan como una; separadas por 50 m, como dos.
6. La recta que cruza la meta se une con la del principio.
7. Eje plano: perfil todo a 0 y escala ±20 m. Eje con un punto a 27 m de altura: escala ±30 m.
8. Sectores = checkpoints + 1, y sus posiciones en el perfil coinciden con las distancias de los checkpoints.

Lógica y catálogo:
9. Al abrir: START seleccionada y el circuito de `GameSession` (o el primero si no hay). Next / Previous dan la vuelta; con un circuito no cambian nada. START devuelve el circuito actual; BACK y ESC devuelven "volver al menú".
10. La firma del trazado de `circuit_01` es la misma que antes de esta spec (récords y fantasmas siguen valiendo).
11. Con `IncludeTestCircuits = false`, el catálogo no devuelve `test_oval`; con `true`, sí, detrás de `circuit_01`.

Formato:
12. 4213 m → `4.2 KM`. 816 m → `820 M`. En RACE `LAPS 3` y `GRID 8 SHIPS`; en TIME TRIAL `FREE` y `—`. Índice 0 de 2 → `01 / 02`.

Las pruebas existentes siguen pasando.

## 10. Prueba manual

1. Menú → RACE: se abre SELECT CIRCUIT con `RACE · SELECT CIRCUIT`, `CIRCUIT 01`, la miniatura girando despacio y START seleccionada. No se ven la nave ni la rejilla del menú.
2. Comparar con el lienzo (Selector C): composición, colores y tamaños.
3. Revisar los datos de CIRCUIT 01: longitud, curvas y recta más larga tienen que cuadrar con lo que se ve en pista. Si el número de curvas no convence, se ajustan los umbrales de 5.4.
4. D / → / clic en `›`: pasa a `TEST OVAL` con la etiqueta `TEST` en naranja, `TEST TRACK · CLOCKWISE`, 2 curvas y `02 / 02`. Otra vez: vuelve a CIRCUIT 01.
5. El perfil de elevación es una línea plana, con las líneas de sector en su sitio.
6. Récords: en RACE, los de RACE de cada circuito. En el óvalo, `--:--.---`.
7. ESC: vuelve al menú con RACE seleccionada. Entrar de nuevo: sigue en el circuito que estaba.
8. START con TEST OVAL: carrera completa en el óvalo con parrilla, semáforo, 3 vueltas, checkpoints, barreras, daño y resultados. Al terminar, RESTART repite en el óvalo.
9. Menú → TIME TRIAL → TEST OVAL: salida lanzada, vueltas, delta y récord. Volver al selector: `BEST LAP` del óvalo y `GHOST AVAILABLE`. En CIRCUIT 01, los récords y el fantasma de antes siguen ahí.
10. RECORDS: se puede cambiar entre los dos circuitos con A/D y `‹ ›`, con `1 / 2` y `2 / 2`. RESET borra los dos.
11. Poner `IncludeTestCircuits = false`: el óvalo desaparece del selector y de RECORDS, y las flechas quedan desactivadas. Volver a dejarlo en `true`.
12. Probar todo también con el ratón, y en ventana y a pantalla completa.
13. Ejecutar la escena de carrera sola desde el editor: sigue funcionando como antes.

## 11. Fuera de alcance

- Pista 3D (alturas, peraltes, túneles) y el circuito nuevo con desniveles: specs propias.
- Elegir nave (HANGAR) o dificultad en esta pantalla.
- Número de vueltas configurable.
- Guardar en disco el último circuito elegido.
- Miniatura con barreras, checkpoints, arcos o la nave.
- Animaciones de transición al cambiar de circuito o de pantalla.
- Fechas de los récords en esta pantalla.
- Bloquear o desbloquear circuitos.
- Sonido y mando.
