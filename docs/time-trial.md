# VECTORY — Spec: Time Trial y récords

Versión 1.3 · 2026-10-29 · Estado: **definido, listo para implementar**

Activa la opción **TIME TRIAL** del menú principal: contrarreloj en solitario, con **salida lanzada** y **vueltas libres**, con **delta por sector** respecto a tu récord y aviso de **NEW RECORD** al completar una vuelta. Además, se empiezan a **guardar récords locales** por circuito, en TIME TRIAL y en RACE.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Time Trial**: *Delta · verde (mejor que récord)*, *Delta · rojo (peor que récord)*, *Fin de vuelta · NEW RECORD*, *B · READY? Orbitron con halo* y *B · GO! Orbitron con halo*. Los artboards de delta cian/naranja y los de READY?/GO! variante A quedan descartados. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: `RaceSession` / `RaceTracker` / `RaceResult` (vueltas y tiempos), `Track` y `TrackDefinition` (circuito), `Main.cs` (escena de carrera), `Hud`, `PauseMenu`, `MainMenu` y `SceneChanger`.

Cambios respecto al documento maestro y a specs anteriores:
- La cuenta atrás y el GO de RACE cambian de estilo (Orbitron con halo) y el GO pasa a ser `GO!` (ver 7.3; sustituye lo que dice la spec de carrera, sección 4.4, sobre el número y el GO).
- La contrarreloj **no tiene un número fijo de vueltas**: son libres, para mejorar la mejor vuelta.
- Se añaden a la paleta **dos colores nuevos**, verde y rojo, solo para el delta (ver 5.3).

La pantalla **RECORDS** del menú **no** entra aquí: sigue en COMING SOON. Esta spec solo guarda los récords y los usa en carrera; verlos en un menú será la siguiente spec.

Rama: `feature/time-trial` (sale de `develop`).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- **TIME TRIAL** pasa a estar activa en el menú principal. Carga la misma escena de carrera, en modo contrarreloj.
- **Salida lanzada**: la nave aparece unos metros antes de la meta y espera **5 s** con un aviso `READY?`; en el `GO!` sale lanzada (ya en movimiento) y se toma el control. Sin parrilla ni semáforo. El cronómetro arranca al **cruzar la meta** por primera vez.
- **Vueltas libres**: no hay última vuelta ni pantalla de resultados. Se sale con la pausa (RESTART o BACK TO MAIN MENU).
- **Récord de TIME TRIAL** por circuito: la mejor vuelta y sus tiempos de paso por cada checkpoint.
- **Delta por sector**: al cruzar cada checkpoint aparece unos segundos la diferencia con tu vuelta récord en ese punto, en **verde** si vas mejor y en **rojo** si vas peor.
- **Fin de vuelta**: una franja fina muestra 2,5 s el tiempo de la vuelta y, si lo has batido, **NEW RECORD**.
- **Récords de RACE** por circuito: mejor tiempo total y mejor vuelta. La franja de resultados avisa de NEW RECORD y pasa a mostrar también el tiempo total.
- Los récords se guardan en un archivo local y se mantienen entre partidas.

---

## 2. Modo de juego

- Hay dos modos: **RACE** y **TIME TRIAL**.
- El menú decide el modo antes de cargar la escena de carrera. La escena lee el modo al arrancar.
- Para pasar el modo de una escena a otra se usa un sitio único y sencillo, por ejemplo una clase estática `GameSession` con el modo y el circuito, que rellena el menú y lee `Main.cs`. Nada de autoloads complejos.

### 2.1 Circuito de la sesión

Preparación mínima para el futuro selector de circuito (que **no** entra en esta spec):

- `GameSession` guarda también el **circuito** elegido (la ruta del `TrackDefinition`, por ejemplo `res://circuits/circuit_01.tres`).
- El menú, al elegir RACE o TIME TRIAL, deja en `GameSession` el modo **y** el circuito. Mientras solo haya uno, siempre `circuit_01`; la ruta sale de una constante única, no repetida por el código.
- La escena de carrera carga el circuito **de `GameSession`**. Si no hay ninguno (escena ejecutada sola desde el editor), se usa el del nodo `Track` en el Inspector y, si tampoco hay, el de por defecto, como ahora.
- **RESTART** repite el mismo modo **y** el mismo circuito.
- El día que haya más circuitos, el selector será una pantalla entre el menú y la carrera que rellene `GameSession` con el circuito elegido, sin tocar carrera, récords ni HUD.
- Si la escena de carrera se ejecuta sola desde el editor, sin pasar por el menú, el modo es **RACE**, como ahora.
- **RESTART** desde la pausa repite el **mismo modo**.
- Al volver al menú y elegir otra opción, el modo cambia.

| Opción del menú | Modo | Escena |
|---|---|---|
| RACE | RACE | la de carrera actual |
| TIME TRIAL | TIME TRIAL | la misma escena de carrera |

En el menú principal, **TIME TRIAL** pasa a `Enabled = true` y se conecta su acción. CHAMPIONSHIP, HANGAR, RECORDS y SETTINGS siguen en COMING SOON.

## 3. TIME TRIAL: salida lanzada

### 3.1 Colocación

- La nave aparece **sobre el eje del circuito**, centrada en la pista, a **150 m antes de la meta** (medidos sobre `Centerline`, hacia atrás), mirando en el sentido de la marcha. Así funciona en cualquier circuito.
- Velocidad de salida: el **60 % de la velocidad punta** (sin boost), en el sentido de la marcha. Se aplica en el `GO!` (ver 3.2).
- Las marcas de parrilla siguen dibujadas (son parte del circuito), pero no se usan.

### 3.2 Preparación: READY? → GO!

Para no salir de golpe al entrar, hay **5 s** de preparación:

| Tiempo desde la carga | Qué se ve | Nave |
|---|---|---|
| 0 – 5 s | `READY?` | Quieta en su punto de salida, sin control. Cámara y HUD normales |
| 5 s | `GO!` (se queda 1 s) | Sale con el 60 % de la punta y se toma el control |
| 6 s | Desaparece el `GO!` | Conducción normal (vuelta de salida) |

- Durante `READY?`: **W, S, A, D, Shift y Espacio no hacen nada** (igual que en la previa de RACE). Sí funcionan ESC (pausa) y C (cámara).
- La velocidad de salida se aplica de golpe en el `GO!`; no hay aceleración progresiva desde 0.
- El contador de recarga del boost **empieza en el `GO!`**, como en RACE.
- El cronómetro sigue a 0 hasta cruzar la meta (3.4).
- Aspecto: el mismo texto de salida que la cuenta atrás de RACE, con el estilo nuevo de la sección 7.3 (Orbitron Black con halo), en el mismo sitio (centrado, tercio superior). `READY?` en **cian**, como los números de la cuenta atrás; `GO!` en **naranja**, como el GO de RACE. Sin semáforo ni números.
- La pausa congela la preparación: al continuar, `READY?` sigue el tiempo que le faltaba.
- **RESTART** desde la pausa vuelve a empezar desde `READY?`.
- Se reutiliza la lógica de la secuencia de salida (`StartSequence`) configurada sin luces: 5 s de espera y 1 s de GO, si encaja sin forzarla; si no, una secuencia propia igual de simple.

### 3.3 Antes de la primera meta (vuelta de salida)

- Desde el `GO!`, control total: W, S, A, D, Shift y boost funcionan.
- El cronómetro está parado, a 0.
- El boost funciona con sus reglas de siempre (su recarga empezó en el `GO!`).
- El HUD muestra `OUT LAP` en lugar del número de vuelta (ver 5.1).
- Si la nave se para o da la vuelta antes de cruzar la meta, no pasa nada: la vuelta 1 empieza solo cuando se cruza la meta en el sentido correcto.

### 3.4 Cruce de la meta

- Al cruzar la meta por primera vez **empieza la vuelta 1**: el cronómetro de vuelta y el de sector arrancan desde 0 en ese instante.
- A partir de ahí, las vueltas se completan como ahora: cruzando la meta tras pasar los checkpoints en orden.
- **No hay última vuelta**: tras completar una, empieza la siguiente sin parar. El número de vuelta sigue subiendo (1, 2, 3…).

## 4. Récords

### 4.1 Qué se guarda

Por **circuito** y por **modo**, por separado: una vuelta de RACE no cuenta como récord de TIME TRIAL ni al revés, porque la vuelta 1 de RACE sale desde parado.

| Modo | Récord | Datos guardados |
|---|---|---|
| TIME TRIAL | Mejor vuelta | Tiempo de la vuelta, tiempos de paso por cada checkpoint de esa vuelta (acumulados desde la meta) y fecha |
| RACE | Mejor tiempo total | Tiempo total y fecha |
| RACE | Mejor vuelta | Tiempo de la vuelta y fecha |

- "Tiempos de paso" = tiempo de vuelta en el momento de cruzar cada checkpoint (no el de cada sector suelto). Es lo que se usa para el delta.
- Si cambia el número de checkpoints de un circuito, los tiempos de paso guardados ya no sirven. En ese caso se conserva el tiempo del récord, pero **no se muestra delta** hasta que se haga una vuelta récord nueva con el número de checkpoints actual.

### 4.2 Cuándo se guarda

- **TIME TRIAL**: al completar una vuelta más rápida que el récord, o si no había récord. Se escribe en el archivo **en ese momento**, sin esperar a salir.
- **RACE**: al terminar la carrera, si el tiempo total o la mejor vuelta mejoran su récord. Una carrera abandonada (RESTART o BACK desde la pausa) **no** guarda nada.
- Empate con el récord: **no** cuenta como récord nuevo.

### 4.3 Identificador del circuito

- `TrackDefinition` gana un campo **`Id`** (texto), por ejemplo `circuit_01`, que se exporta en el Inspector.
- Los récords se guardan con ese `Id`, no con la ruta del archivo ni con el nombre visible, para que renombrar no los pierda.
- Si un circuito no tiene `Id`, se avisa en consola y **no se guardan** récords para él (la carrera funciona igual).
- `circuit_01.tres` tiene `Id = "circuit_01"`.

### 4.4 Archivo

- Un archivo JSON en `user://records.json`. En Windows, la carpeta de datos de usuario de Godot.
- Contiene un número de versión del formato (`"version": 1`) y los récords por circuito y modo.
- Archivo inexistente: sin récords; se crea al guardar el primero.
- Archivo corrupto o de una versión que no se entiende: se avisa en consola, se renombra a `records.json.bak` (sobrescribiendo una `.bak` anterior) y se empieza sin récords. El juego **nunca** se cuelga por el archivo de récords.
- Escritura segura: se escribe a un archivo temporal y luego se reemplaza el bueno, para no dejar un archivo a medias si el juego se cierra en mitad.

## 5. HUD en TIME TRIAL

### 5.1 Bloques

| Zona | RACE (como ahora) | TIME TRIAL |
|---|---|---|
| Arriba izquierda | Posición / participantes | `TIME TRIAL` y, debajo, el nombre del circuito (`DisplayName`) en pequeño |
| Arriba derecha, 1.ª línea | `LAP 2/3` | `LAP 4` (sin total). Antes de la primera meta: `OUT LAP` |
| Arriba derecha, 2.ª línea | Tiempo de la vuelta actual | Igual (a 0 durante la vuelta de salida) |
| Arriba derecha, 3.ª línea | Mejor vuelta de esta carrera | `BEST` + **récord de TIME TRIAL del circuito**. Sin récord: `BEST --:--.---` |
| Arriba centro | Semáforo y cuenta atrás | `READY?` / `GO!` al empezar (3.2); después, delta por sector (5.2) y franja de fin de vuelta (5.4) |
| Abajo derecha | Velocidad y boost | Igual |

- Colores y tamaños de los bloques que ya existen: los actuales del HUD, sin cambios de fuente.
- `TIME TRIAL` en Share Tech Mono ~22 px, cian; el nombre del circuito ~14 px, azul grisáceo (como en el lienzo).
- Cuando se bate el récord, `BEST` se actualiza al momento con el tiempo nuevo.

### 5.2 Delta por sector

- Se muestra al **cruzar cada checkpoint** y al **cruzar la meta** completando una vuelta (en la meta, dentro de la franja de fin de vuelta, ver 5.4).
- Valor = tiempo de vuelta actual en ese punto − tiempo de paso del récord en el mismo punto.
  - Negativo: vas **mejor** que tu récord. Se muestra con signo menos: `-0.145`.
  - Positivo: vas **peor**. Con signo más: `+0.312`.
  - Cero: `±0.000`, en el color de "mejor".
- Formato: segundos con 3 decimales; si pasa de 60 s, `+1:02.315`.
- Sin récord, o sin tiempos de paso válidos para el número de checkpoints actual: **no se muestra** delta.
- Aspecto, centrado en horizontal en el tercio superior (mismo sitio que el semáforo en RACE):
  - Etiqueta `SECTOR N` (N = número del sector que acaba de terminar, empezando en 1), Share Tech Mono ~15 px, azul grisáceo, espaciado ~0.24 em.
  - Debajo, el delta en Orbitron Bold ~44 px, con halo de su color.
- Está visible **2,5 s** y desaparece de golpe, sin animación. Si en ese tiempo se cruza otro checkpoint, se sustituye por el nuevo.

### 5.3 Colores del delta

- **Mejor**: verde **#3DFF8A**. **Peor**: rojo **#FF4D5E**.
- Se añaden a `Palette` como colores base nuevos (`Green`, `Red`), con un comentario que diga que amplían la paleta del documento maestro **solo para el delta**. Además, roles `DeltaBetter`, `DeltaWorse` y sus halos (`DeltaBetterGlow`, `DeltaWorseGlow`, el color con opacidad ~0,5), como el resto de roles.
- No se usan en ningún otro sitio.

### 5.4 Franja de fin de vuelta

- Al completar cada vuelta en TIME TRIAL aparece una **franja fina** de lado a lado, centrada en horizontal, en el tercio superior (y ≈ 104 sobre 720), de **120 px** de alto, con el mismo estilo que la de la pausa: fondo #05070D al ~90 %, línea cian de 1 px arriba y abajo con halo. Mismo componente de franja que la pausa y los resultados, con su alto como parámetro.
- **No** se atenúa la pantalla, **no** se pausa el juego y **no** quita el control: se sigue conduciendo.
- Contenido, centrado y separado por una línea vertical azul de 1 px:
  - Izquierda, alineado a la derecha: `LAP N` (Share Tech Mono ~15 px, azul grisáceo) y debajo el tiempo de la vuelta (Orbitron Bold ~44 px, cian, con halo).
  - Derecha, alineado a la izquierda:
    - **Récord batido** (o primera vuelta sin récord previo): `NEW RECORD` en Orbitron Black ~26 px, **naranja** #FF9D32 con halo naranja. Debajo, en Share Tech Mono ~16 px, azul grisáceo: la mejora y el récord anterior, por ejemplo `-0.482  ·  PREVIOUS 1:12.384`. Sin récord previo: solo `FIRST RECORD`.
    - **Sin récord**: en lugar de NEW RECORD, el delta de la vuelta completa con el mismo estilo y color que el delta por sector (rojo, o verde si es un empate), y debajo `BEST 1:12.384` en azul grisáceo.
- Visible **2,5 s**, sin animación. Mientras está visible no se muestra el delta suelto del último sector: va dentro de la franja.

## 6. Pausa en TIME TRIAL

- La pausa funciona igual que en RACE.
- **RESTART**: con confirmación, porque la sesión no "termina" nunca. Vuelve a empezar la contrarreloj con salida lanzada. Los récords ya guardados se mantienen.
- **BACK TO MAIN MENU**: con confirmación. Vuelve al menú.
- El texto de la confirmación (`CURRENT RACE PROGRESS WILL BE LOST`) se queda igual: los récords ya están guardados al completar cada vuelta, así que solo se pierde la vuelta en curso.
- La pausa congela también el tiempo del delta y de la franja de fin de vuelta.

## 7. RACE: récords y resultados

### 7.1 Tiempo total en resultados

- La franja de resultados añade, en la columna central y **encima** de BEST LAP, el bloque `TOTAL TIME` con el mismo estilo que BEST LAP: etiqueta Share Tech Mono ~14 px azul grisáceo y tiempo en Orbitron Medium ~30 px cian.
- `RaceResult` incluye el tiempo total (hoy solo lo da el evento `RaceFinished`).
- Si con la fila nueva no cabe en la franja, la franja crece en alto, como ya dice la spec de resultados.

### 7.2 NEW RECORD en resultados

- Si el **tiempo total** bate el récord de RACE del circuito (o no había): junto al tiempo total, a su derecha, `NEW RECORD` en Orbitron Black ~16 px, naranja, con halo naranja.
- Igual para la **mejor vuelta** frente al récord de vuelta de RACE.
- Si no hay récord nuevo, no se muestra nada extra (ni el récord anterior, por ahora).

### 7.3 Estilo de la cuenta atrás y del GO

El texto de la salida pasa a tener el mismo estilo que los títulos del menú y la pausa, **en RACE y en TIME TRIAL**:

- Fuente: **Orbitron Black** (la de los menús, cargada desde el sitio compartido de fuentes), en lugar de la fuente por defecto del HUD.
- Tamaño: ~**56 px sobre 1280×720** (≈ 84 px a 1920×1080), escalado como el resto de menús.
- Espaciado entre letras ~0,1 em.
- **Halo** del color del texto (opacidad ~0,55), como los títulos del menú.
- Colores, sin cambios: números de la cuenta atrás y `READY?` en cian (`Palette.Countdown`), GO en naranja (`Palette.Go`).
- El texto del GO pasa a ser **`GO!`** también en RACE, para que las dos salidas digan lo mismo.
- Posición: la de ahora, centrado bajo el semáforo en RACE. En TIME TRIAL, sin semáforo, en el mismo sitio en vertical.
- Solo cambia este texto: el semáforo (luces) y el resto del HUD se quedan como están.
- Se toca `StartLightsView` (o se saca el texto a una vista propia que usen las dos salidas). Tamaño de fuente como parámetro en el Inspector.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Distancia de la salida lanzada antes de la meta | 150 m | escena de carrera (`Main`) |
| Velocidad de la salida lanzada | 60 % de la punta | escena de carrera (`Main`) |
| Duración de READY? | 5 s | escena de carrera (`Main`) |
| Tiempo con el GO! visible | 1 s | escena de carrera (`Main`) |
| Tamaño del texto de salida (cuenta atrás, READY?, GO!) | 56 px sobre 1280×720 | HUD |
| Tiempo visible del delta | 2,5 s | HUD |
| Tiempo visible de la franja de fin de vuelta | 2,5 s | HUD |
| Alto de la franja de fin de vuelta | 120 px | HUD |
| Tamaño del delta | 44 px | HUD |
| Id del circuito | `circuit_01` | `circuit_01.tres` |

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Modo y sesión**: `GameMode` (RACE / TIME TRIAL) y un sitio único donde el menú deja el modo y el circuito y la escena los lee (`GameSession` o similar, en `scripts/Game`). Sin lógica de juego dentro. `Track` recibe el `TrackDefinition` de la sesión antes de generarse (o lo lee en `_Ready`), con la prioridad de 2.1.
- **Vueltas libres en `RaceTracker`**: admitir "sin límite de vueltas" (por ejemplo `totalLaps = null` o una constante explícita), en el que nunca se pasa a `Finished`. RACE sigue igual.
- **Salida lanzada**: en vez del semáforo, el tracker se arranca (`Start()`) en el **primer cruce de meta** en el sentido correcto. `RaceSession` (o una variante para TIME TRIAL) se encarga, según el modo. En TIME TRIAL la nave puede conducir antes de que arranque el tracker (`CanDrive`).
- **Colocación**: `Track` ya calcula posiciones sobre el eje para la parrilla; se reutiliza para "N metros antes de la meta, en el centro". `Player` necesita poder empezar con una velocidad inicial.
- **Tiempos de paso**: `RaceTracker` expone el tiempo de vuelta en cada checkpoint de la vuelta en curso (o lo pasa en el evento), para comparar con el récord.
- **Récords (lógica pura, sin Godot)**:
  - Modelo de datos de los récords (por circuito y modo) y su conversión a/desde JSON con versión de formato.
  - Reglas: si una vuelta / tiempo total es récord nuevo (empate no), actualizar el récord, validez de los tiempos de paso según el número de checkpoints.
  - Cálculo del delta (tiempo actual − tiempo de paso) y su formato (`-0.145`, `+1:02.315`, `±0.000`).
- **Almacenamiento**: una interfaz pequeña (leer texto / escribir texto) con una implementación que usa `user://records.json` (temporal + reemplazo, `.bak` si está corrupto) y otra en memoria para las pruebas.
- **HUD**: recibe ya calculados el modo, el texto de vuelta, el récord, el delta a mostrar y el contenido de la franja. No calcula récords ni deltas.
- **`Main.cs`**: monta la escena según el modo y conecta tracker, récords y HUD. Sin reglas de récords dentro.

### Pruebas unitarias mínimas

Vueltas libres y salida lanzada:
1. Con vueltas libres, completar 10 vueltas nunca pasa a `Finished` y el número de vuelta llega a 11.
2. Antes de `Start()`, cruzar checkpoints o tiempo transcurrido no cambian nada.
3. Tras `Start()` en el cruce de meta, la vuelta 1 cuenta desde 0.
3b. Secuencia de TIME TRIAL: de 0 a 5 s, READY? y sin salida; a los 5 s, GO! visible y salida dada (aviso una sola vez); a los 6 s, el GO! ya no se muestra.
4. Los tiempos de paso de la vuelta en curso se registran en cada checkpoint y se reinician al empezar la vuelta siguiente.

Récords:
5. Sin récord previo, la primera vuelta completada es récord nuevo.
6. Una vuelta más lenta no cambia el récord; una más rápida sí; un empate no.
7. Los récords de RACE y TIME TRIAL del mismo circuito son independientes.
8. RACE: tiempo total y mejor vuelta se comparan y guardan por separado.
9. Tiempos de paso con otro número de checkpoints → no válidos para delta, pero el tiempo del récord se conserva.
10. JSON: guardar y volver a cargar da los mismos récords (ida y vuelta).
11. JSON corrupto o versión desconocida → se carga vacío sin excepción.
12. Circuito sin `Id` → no se guarda nada.

Delta:
13. Actual 30.000 y paso 30.145 → `-0.145`, mejor. Actual 30.312 y paso 30.000 → `+0.312`, peor. Igual → `±0.000`, mejor.
14. Diferencia de más de 60 s → formato `+1:02.315`.

Las pruebas actuales siguen pasando.

## 10. Prueba manual

1. En el menú, TIME TRIAL está activa (sin COMING SOON) y se puede elegir con teclado y ratón.
2. Al entrar, la nave aparece quieta antes de la meta con `READY?` en cian. Durante 5 s, W/S/A/D/Shift no hacen nada. A los 5 s, `GO!` en naranja y la nave sale ya en movimiento y responde. No hay semáforo. HUD: `TIME TRIAL` y el circuito arriba a la izquierda, `OUT LAP`, tiempo a 0 y `BEST --:--.---` (primera vez).
2b. Pausar durante `READY?` 5 s y continuar: la espera sigue donde estaba. RESTART: vuelve a empezar desde `READY?`.
2c. `READY?` y `GO!` se ven en Orbitron Black con halo, como en el lienzo. En RACE, los números 5…1 y el `GO!` tienen ese mismo estilo; las luces del semáforo no cambian.
3. Al cruzar la meta: `LAP 1` y el cronómetro arranca.
4. Primera vuelta completa: franja con `LAP 1`, el tiempo y `FIRST RECORD`. `BEST` se actualiza. La nave no se para.
5. Segunda vuelta: al pasar cada checkpoint sale `SECTOR N` y el delta en verde o rojo, 2,5 s.
6. Hacer una vuelta peor: la franja muestra el delta en rojo y `BEST` con el récord. Hacer una mejor: `NEW RECORD` en naranja con la mejora y el récord anterior.
7. Pausar con un delta o la franja en pantalla: se congelan y, al continuar, terminan su tiempo.
8. RESTART (con confirmación): salida lanzada otra vez, y `BEST` sigue mostrando el récord.
9. BACK TO MAIN MENU, cerrar el juego, abrirlo y entrar en TIME TRIAL: `BEST` sigue con el récord. Pasar por checkpoints: el delta se calcula contra él.
10. Cambiar el número de checkpoints del circuito y entrar: `BEST` mantiene el tiempo, pero no sale delta hasta hacer un récord nuevo.
11. RACE: terminar una carrera. La franja de resultados muestra `TOTAL TIME` y la primera vez `NEW RECORD` en total y mejor vuelta. Repetir más lento: sin NEW RECORD. Abandonar una carrera desde la pausa: no guarda nada.
12. Estropear a mano `records.json` (por ejemplo, borrar media línea) y entrar: el juego funciona sin récords, hay aviso en consola y aparece `records.json.bak`.
13. Ejecutar la escena de carrera sola desde el editor: funciona en modo RACE con el circuito del Inspector / por defecto, como antes.
14. Desde el menú, RACE y TIME TRIAL cargan `circuit_01` a través de la sesión; RESTART desde la pausa repite el mismo modo y circuito.

## 11. Fuera de alcance

- Pantalla RECORDS del menú (siguiente spec).
- Fantasma (ghost) de la vuelta récord.
- Delta en vivo continuo (solo por sector).
- Elegir circuito (solo hay uno).
- Récords en línea o tablas de puntuación.
- Mostrar el récord anterior en los resultados de RACE cuando no se bate.
- Franja de fin de vuelta en RACE.
- Sonido, animaciones, mando.
