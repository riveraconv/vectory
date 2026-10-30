# VECTORY — Spec: Nave fantasma en Time Trial

Versión 1.0 · 2026-10-30 · Estado: **definido, listo para implementar**

Añade al **TIME TRIAL** una **nave fantasma** que repite la **vuelta récord del circuito**, con la **nave que hizo esa vuelta**. Se activa o desactiva desde la **pausa del Time Trial** y el ajuste se guarda entre partidas.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Fantasma**: *Fantasma B · gris azulado 35 %* y *Pausa TT 1 · GHOST: ON / OFF*. Las variantes A, C y la pausa 2 quedan descartadas. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: récords de TIME TRIAL (`user://records.json`, tiempos de paso, `TrackDefinition.Id`), `RaceTracker` (vueltas en modo libre), `GameSession` (modo y circuito), `Spaceship`, `PauseMenu` y su lógica, la lista de opciones reutilizable, `Palette` y la pantalla RECORDS (RESET).

Cambios respecto a specs anteriores:
- La pausa del **TIME TRIAL** gana una opción `GHOST: ON / OFF` (la de RACE no cambia).
- **RESET RECORDS** (spec de Records, 5.4) borra también los fantasmas.
- Se añade un archivo de **ajustes** del juego (`user://settings.json`), de momento con un único ajuste.

Rama: `feature/ghost` (sale de `develop`, con `feature/records` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- Solo en **TIME TRIAL**. En RACE no hay fantasma.
- El fantasma es la **vuelta récord de TIME TRIAL del circuito** (la misma que el `BEST` del HUD y la que usa el delta). No la mejor de la sesión.
- Va con la **nave que hizo el récord**: se guarda el identificador del diseño de nave junto al fantasma. Hoy solo hay uno (`ship_01`).
- Aspecto: la misma geometría de la nave, en **un solo color, azul grisáceo #A6B8D8 al ~35 %**, **sin halo ni efectos**.
- Arranca cuando **cruzas la meta** empezando vuelta y recorre su vuelta en tiempo real. Si termina antes que tú, desaparece hasta que empieces la siguiente.
- **Sin colisión**: te atraviesa. Se **desvanece** cuando está muy cerca de ti, para no tapar la vista.
- Se graba **siempre** tu vuelta en curso; si es récord, se guarda como nuevo fantasma en ese momento, junto con el récord.
- Opción `GHOST: ON / OFF` en la pausa del Time Trial. El ajuste **se guarda**. Por defecto, **ON**.
- Sin fantasma válido para el circuito, la opción aparece **desactivada**.

---

## 2. Qué vuelta es el fantasma

- Uno por **circuito** (por su `Id`), ligado al **récord de TIME TRIAL** de ese circuito.
- Cuando se bate el récord, el fantasma se **sustituye** por el de la vuelta nueva, a la vez que se guarda el récord (spec de Time Trial, 4.2).
- La vuelta siguiente a batir el récord ya corre con el **fantasma nuevo**.
- La primera vuelta completada sin récord previo (`FIRST RECORD`) también guarda fantasma.
- Los récords de RACE **no** tienen fantasma.
- Récords guardados antes de esta spec (sin archivo de fantasma): no hay fantasma hasta que se haga un récord nuevo.

## 3. Grabación

- Empieza al **cruzar la meta** iniciando una vuelta (incluida la vuelta 1). Durante la vuelta de salida (`OUT LAP`) no se graba nada.
- Se toma una **muestra cada 1/20 s** (20 por segundo) de tiempo de vuelta: **tiempo, posición y rotación** de la nave. Además, una muestra exactamente en el inicio (t = 0) y otra en el cruce de meta al terminar (t = tiempo de la vuelta).
- El tiempo de las muestras es el **tiempo de vuelta** (el del cronómetro), no el tiempo real: la pausa no cuenta y no deja huecos.
- Al completar la vuelta:
  - Si es **récord nuevo**: la grabación pasa a ser el fantasma del circuito y se escribe en su archivo en ese momento.
  - Si no: se descarta.
- Se graba **aunque el fantasma esté en OFF**. Así, al volver a ponerlo en ON, el fantasma corresponde al récord actual.
- Al empezar una vuelta nueva, se empieza una grabación nueva desde cero.
- RESTART o BACK TO MAIN MENU desde la pausa descartan la grabación en curso (los récords y fantasmas ya guardados se mantienen).

## 4. Reproducción

- Solo si: modo TIME TRIAL, ajuste en **ON** y hay **fantasma válido** para el circuito (sección 6.3).
- **Vuelta de salida**: el fantasma **no se ve**.
- Al **cruzar la meta** empezando cualquier vuelta: el fantasma aparece en la meta y empieza su vuelta (t = 0).
- Su posición y rotación en cada frame son las de la grabación en el **tiempo de vuelta actual**:
  - Posición: interpolación **lineal** entre las dos muestras más cercanas.
  - Rotación: interpolación **esférica** (slerp) entre esas dos muestras.
- Si tu vuelta dura más que la del fantasma: al llegar al final de su grabación, el fantasma **desaparece** (de golpe, sin animación) y vuelve a aparecer al empezar tu siguiente vuelta.
- La pausa lo congela con todo lo demás (pausa del árbol de Godot). Al continuar, sigue sincronizado porque depende del tiempo de vuelta.
- El fantasma **no** interactúa con nada: sin colisión, sin físicas, sin input, no cruza checkpoints ni meta a efectos de carrera, no afecta al delta ni a los récords.

### 4.1 Desvanecimiento por cercanía

Para que no tape la vista (sobre todo en cabina):

- Distancia entre el fantasma y tu nave ≥ **20 m**: opacidad normal (35 %).
- Entre **20 m y 6 m**: la opacidad baja de forma lineal.
- A **6 m o menos**: invisible.
- Vale para todas las cámaras.

### 4.2 Cambio de ajuste en mitad de una vuelta

- **ON → OFF**: el fantasma desaparece al continuar.
- **OFF → ON**: el fantasma aparece al continuar, en la posición que le toca según el tiempo de vuelta actual (no espera a la siguiente vuelta). Si estás en la vuelta de salida, aparecerá al cruzar la meta.

## 5. Aspecto

- La **misma geometría** que la nave del diseño guardado (`Spaceship`), sin copiarla ni redibujarla.
- Todas sus líneas en **azul grisáceo #A6B8D8**, opacidad **~35 %**, grosor de línea igual que la nave normal.
- **Sin halo**: el material del fantasma no debe disparar el glow/bloom del `WorldEnvironment` (sin emisión, o por debajo del umbral del glow). Debe verse como en el lienzo: líneas apagadas, sin brillo.
- Sin efectos ahora ni en el futuro: cuando la nave tenga estela, partículas o efectos de boost, el fantasma **no** los tendrá.
- Colores desde `Palette`: nuevo rol `GhostLine` (azul grisáceo con opacidad 0,35), derivado del color base, como el resto de roles.
- Opacidad y distancias del desvanecimiento, parámetros en el Inspector.

## 6. Datos

### 6.1 Diseño de nave

- Se introduce un identificador de **diseño de nave** (texto), por ejemplo en una clase o lista única `ShipCatalog`, con un solo elemento hoy: `ship_01`, que es la nave actual.
- `GameSession` guarda el diseño de nave en uso (por defecto `ship_01`). El día que haya HANGAR, será el que lo cambie.
- La grabación guarda el diseño de nave con el que se hizo la vuelta.
- Al reproducir, si ese diseño **no existe** en el catálogo, se usa el de por defecto y se avisa en consola. El fantasma se reproduce igual.
- Si para poder instanciar `Spaceship` en "modo fantasma" (otro diseño, otro material, sin físicas ni input) hay que tocar `Spaceship`, **se avisa antes** de hacerlo.

### 6.2 Archivo del fantasma

- Uno por circuito: `user://ghosts/<IdCircuito>.json` (por ejemplo `user://ghosts/circuit_01.json`). La carpeta se crea si no existe.
- Separado de `records.json` para no inflarlo.
- Contenido:

| Campo | Contenido |
|---|---|
| `version` | Versión del formato (`1`) |
| `circuitId` | `Id` del circuito |
| `trackSignature` | Firma del trazado (6.4) |
| `checkpointCount` | Número de checkpoints del circuito al grabar |
| `shipDesignId` | Diseño de nave (`ship_01`) |
| `lapTime` | Tiempo de la vuelta, en segundos |
| `sampleRate` | Muestras por segundo (`20`) |
| `samples` | Lista de muestras: tiempo, posición (x, y, z) y rotación (cuaternión x, y, z, w) |

- Las muestras se guardan de forma compacta (por ejemplo, cada muestra como un array de 8 números, con 3–4 decimales), no como objetos con nombres de campo.
- Mismas reglas que `records.json`:
  - Escritura segura: archivo temporal y reemplazo.
  - Lectura tolerante: campos desconocidos se ignoran.
  - Corrupto, versión mayor que la que entiende el juego o falta algo imprescindible (versión, `circuitId`, `lapTime`, `samples`): aviso en consola, se renombra a `.bak` y se trata como si no hubiera fantasma. El juego **nunca** se cuelga por este archivo.
- Circuito sin `Id`: no se guarda fantasma (igual que los récords).

### 6.3 Fantasma válido

El fantasma de un circuito se usa solo si se cumplen **todas**:

1. El archivo existe y se lee bien.
2. `circuitId` coincide con el circuito actual.
3. `checkpointCount` coincide con el del circuito actual.
4. `trackSignature` coincide con la del circuito actual.
5. Hay récord de TIME TRIAL para el circuito y su tiempo coincide con `lapTime` (tolerancia de 1 ms).
6. Tiene al menos 2 muestras.

Si no es válido: no hay fantasma (y la opción de la pausa sale desactivada). El archivo **no se borra**: se sobrescribe con el siguiente récord.

### 6.4 Firma del trazado

- Valor calculado a partir de los puntos del eje del circuito (`Centerline`) y el ancho de pista del `TrackDefinition`, redondeados (por ejemplo a 2 decimales), y pasados por un hash estable (que dé lo mismo en cada ejecución; **no** `GetHashCode()` de .NET).
- Sirve para detectar que el trazado ha cambiado: un fantasma grabado en el trazado viejo iría por fuera de la pista.
- Función pura, en la parte de lógica.

### 6.5 Ajustes

- Nuevo archivo `user://settings.json` con `version` y `ghostEnabled` (true / false).
- Por defecto (archivo inexistente o sin el campo): `ghostEnabled = true`.
- Se escribe **al cambiar** el ajuste en la pausa.
- Mismas reglas de escritura segura, lectura tolerante y `.bak` que los récords.
- Se reutiliza la interfaz de almacenamiento de texto de los récords (implementación de archivo y en memoria para pruebas).
- Es el sitio donde irán los demás ajustes cuando exista SETTINGS.

### 6.6 RESET RECORDS

- YES en la confirmación de RESET RECORDS borra también **todos los fantasmas** (todos los archivos de `user://ghosts/`).
- La pantalla RECORDS no cambia de aspecto.
- El ajuste `ghostEnabled` **no** se toca.

## 7. Pausa del Time Trial

Referencia: *Pausa TT 1 · GHOST: ON / OFF*.

- Opciones en TIME TRIAL: **CONTINUE, GHOST: ON / GHOST: OFF, RESTART, BACK TO MAIN MENU**. En RACE siguen siendo las tres de siempre.
- El texto muestra el estado actual: `GHOST: ON` o `GHOST: OFF`.
- Elegirla (Enter / Espacio / clic) **cambia** el ajuste al momento: el texto se actualiza, se guarda en `settings.json`, la pausa **sigue abierta** y la selección se queda en esa opción. No pide confirmación.
- **Desactivada** si no hay fantasma válido para el circuito: estilo de opción desactivada del menú principal (azul #245BFF al ~55 %), **sin** `COMING SOON`. Muestra el valor guardado (`GHOST: ON` o `GHOST: OFF`) pero no se puede seleccionar ni cambiar. La navegación la salta y el ratón no reacciona.
- La opción se evalúa **cada vez que se abre la pausa**: si acabas de hacer tu primer récord, la siguiente vez que pauses ya está activa.
- Con cuatro opciones, la lista sigue centrada en vertical en la franja de 260 px y el separador vertical crece hasta **204 px** (centrado), como en el lienzo. El resto de la franja, igual.
- RESTART y BACK TO MAIN MENU siguen con confirmación, como dice la spec de Time Trial.
- ESC sigue siendo CONTINUE.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Muestras por segundo de la grabación | 20 | grabador del fantasma |
| Opacidad del fantasma | 0,35 | nodo del fantasma |
| Distancia a la que empieza a desvanecerse | 20 m | nodo del fantasma |
| Distancia a la que es invisible | 6 m | nodo del fantasma |

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Datos del fantasma** (puro, sin Godot): grabación (diseño de nave, tiempo de vuelta, muestras con tiempo, posición y rotación usando tipos propios o `System.Numerics`, no tipos de Godot), conversión a/desde JSON con versión de formato y lectura tolerante.
- **Grabador** (puro): `Begin()`, `Sample(tiempoVuelta, posición, rotación)` que solo añade una muestra cuando toca según la frecuencia, `Finish(tiempoVuelta, posición, rotación)` que añade la última y devuelve la grabación, `Discard()`.
- **Reproductor** (puro): dado una grabación y un tiempo de vuelta, devuelve posición y rotación interpoladas, o "terminado" si el tiempo pasa del final.
- **Validez** (puro): las reglas de 6.3, con la firma del trazado de 6.4.
- **Desvanecimiento** (puro): opacidad según distancia.
- **Almacenamiento**: lectura y escritura de fantasmas por `Id` de circuito y borrado de todos, con la interfaz de almacenamiento de texto ya existente (archivo y memoria).
- **Ajustes**: modelo `GameSettings` y su almacenamiento, igual de simples.
- **Lógica de la pausa**: la lista de opciones depende del modo. En TIME TRIAL incluye la opción del fantasma, activa o no según se le indique al abrir. Elegirla devuelve la acción **"cambiar fantasma"** y deja la pausa en `Open`. Sigue usando `MenuModel`.
- **Nodo del fantasma** (`GhostShip` o similar): instancia la nave del diseño guardado en modo fantasma, aplica posición, rotación y opacidad cada frame. Solo lee; no tiene reglas.
- **`Main.cs`**: en TIME TRIAL monta grabador, fantasma y ajustes y los conecta con el tracker (inicio de vuelta, vuelta completada, récord nuevo). Sin reglas dentro.
- **RESET RECORDS**: la lógica de la pantalla no cambia; el nodo, al borrar, borra también los fantasmas.

### Pruebas unitarias mínimas

Grabador:
1. A 20 muestras/s, grabar 1 s con actualizaciones cada 1/60 s da 21 muestras (t = 0 … 1,0), más la final si `Finish` cae en otro tiempo.
2. `Finish` añade una última muestra con el tiempo de la vuelta exacto.
3. `Discard` deja el grabador vacío; un `Begin` nuevo empieza desde cero.

Reproductor:
4. En el tiempo exacto de una muestra devuelve esa muestra.
5. A mitad entre dos muestras, la posición es el punto medio y la rotación la slerp al 50 %.
6. Pasado el último tiempo, devuelve "terminado". Antes de t = 0, la primera muestra.

Validez y firma:
7. Fantasma con `circuitId`, checkpoints, firma y tiempo correctos → válido.
8. Cambia cualquiera de ellos (o hay menos de 2 muestras, o no hay récord) → no válido.
9. La firma da lo mismo para el mismo trazado y cambia si se mueve un punto del eje o cambia el ancho.

Datos y almacenamiento:
10. JSON ida y vuelta: mismas muestras (con la precisión guardada), mismo diseño de nave y tiempo.
11. JSON con campo desconocido → se carga. Corrupto o versión mayor → sin fantasma, sin excepción.
12. Borrar todos los fantasmas deja el almacenamiento sin ninguno.

Ajustes:
13. Sin archivo → `ghostEnabled = true`. Guardar `false` y volver a cargar → `false`.

Desvanecimiento:
14. A 25 m → 0,35. A 13 m → 0,175. A 6 m y a 2 m → 0.

Pausa:
15. En TIME TRIAL con fantasma válido: cuatro opciones; elegir GHOST devuelve "cambiar fantasma" y sigue en `Open` con GHOST seleccionada.
16. En TIME TRIAL sin fantasma válido: GHOST desactivada; `MoveNext()` desde CONTINUE va a RESTART.
17. En RACE: tres opciones, como antes.

Las pruebas actuales siguen pasando.

## 10. Prueba manual

1. Borrar `records.json` y la carpeta `ghosts`. Entrar en TIME TRIAL y pausar: `GHOST: ON` desactivada.
2. Hacer una vuelta completa (`FIRST RECORD`). Pausar: `GHOST: ON` activa. Continuar.
3. En la vuelta siguiente, al cruzar la meta aparece el fantasma gris azulado, translúcido y sin brillo, y repite tu vuelta récord. Compararlo con el lienzo.
4. Ir más lento que el fantasma: se aleja, y al terminar su vuelta desaparece. Al cruzar la meta, vuelve a salir contigo.
5. Acercarse o atravesar el fantasma (en las dos cámaras): se desvanece cerca y no hay choque.
6. Batir el récord: en la vuelta siguiente el fantasma es el de la vuelta nueva.
7. Pausar con el fantasma en pantalla: se congela. Continuar: sigue sincronizado.
8. Pausa → `GHOST: OFF`: el texto cambia, la pausa sigue abierta. Continuar: no hay fantasma. Hacer un récord con el fantasma en OFF y volver a ON: el fantasma es ese récord nuevo.
9. Cerrar el juego y volver a entrar: el ajuste ON/OFF se mantiene y el fantasma sigue ahí.
10. En RACE, pausar: solo las tres opciones de siempre y ningún fantasma.
11. RECORDS → RESET → YES. Entrar en TIME TRIAL: sin fantasma y opción desactivada. El ajuste ON/OFF sigue como estaba.
12. Cambiar un punto del trazado o el número de checkpoints y entrar: sin fantasma y opción desactivada, aunque el récord sigue apareciendo en `BEST`.
13. Estropear a mano el archivo del fantasma: el juego funciona sin fantasma, aviso en consola y aparece el `.bak`.

## 11. Fuera de alcance

- Fantasma en RACE o fantasmas de rivales.
- Fantasma de la mejor vuelta de la sesión, o varios fantasmas a la vez.
- Fantasmas de otros jugadores o descargados.
- Elegir qué fantasma correr.
- Ver o reproducir fantasmas desde RECORDS.
- Más diseños de nave y HANGAR (solo se deja preparado el identificador).
- Pantalla SETTINGS (solo se crea el archivo de ajustes).
- Efectos, estela, sonido o animaciones del fantasma.
- Mando.
