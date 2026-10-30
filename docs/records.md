# VECTORY — Spec: Pantalla RECORDS

Versión 1.1 · 2026-10-29 · Estado: **definido, listo para implementar**

Activa la opción **RECORDS** del menú principal: una pantalla para consultar los récords locales guardados por la feature de Time Trial (`user://records.json`) y, si se quiere, borrarlos.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Records**: *Records · con récords*, *Records · sin récords* y *Records · confirmar RESET*. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

Se apoya en lo que ya existe: `MainMenu`, `MenuBackground`, `MenuModel` / `MenuOption`, la lista de opciones reutilizable y la franja de la pausa / resultados, y la lógica y el almacenamiento de récords de la spec de Time Trial.

Rama: `feature/records` (sale de `develop`, con `feature/time-trial` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- **RECORDS** pasa a estar activa en el menú principal.
- Muestra, para **un circuito cada vez**:
  - **TIME TRIAL**: mejor vuelta y fecha.
  - **RACE**: mejor tiempo total y fecha, y mejor vuelta y fecha.
- Si hay más de un circuito, se cambia de circuito con **A/D** o **flechas izquierda/derecha**. Hoy solo hay uno.
- Opciones: **BACK** y **RESET RECORDS**. RESET pide confirmación (NO / YES, con NO por defecto) y borra **todos** los récords.
- Mismo lenguaje visual que la pausa y los resultados: franja horizontal en tres columnas sobre el fondo del menú atenuado.
- Todos los textos en inglés.

---

## 2. Flujo

| Desde | Acción | Resultado |
|---|---|---|
| Menú principal | Elegir **RECORDS** | Se abre la pantalla RECORDS con el primer circuito y **BACK** seleccionada |
| RECORDS | **BACK**, o ESC | Vuelve al menú principal, con **RECORDS** seleccionada |
| RECORDS | A/D o ←/→ | Circuito anterior / siguiente (da la vuelta). Con un solo circuito, no hace nada |
| RECORDS | **RESET RECORDS** | Confirmación `RESET RECORDS?` con NO seleccionado |
| Confirmación | NO, o ESC | Vuelve a RECORDS con **RESET RECORDS** seleccionada |
| Confirmación | YES | Borra todos los récords, vuelve a RECORDS (estado sin récords) con **BACK** seleccionada |

- RECORDS se muestra **dentro de la escena del menú principal**, sobre el mismo fondo 3D (nave y rejilla), sin cambiar de escena. Mientras está abierta, el título VECTORY / RACING y la lista de opciones del menú se ocultan. La versión de abajo a la izquierda sigue visible.
- Al volver con BACK, el menú reaparece con **RECORDS** seleccionada (no RACE).
- Si para hacerlo así hubiera que complicar mucho `MainMenu`, se puede hacer como escena aparte que reutilice `MenuBackground`, siempre que al volver quede RECORDS seleccionada. Avisar antes de elegir esa vía.

## 3. Aspecto

Medidas sobre la pantalla de referencia de 1280×720, escaladas como el resto de menús.

### 3.1 Fondo y franja

- Fondo: el del menú principal (nave de frente, rejilla, horizonte, sombra izquierda), con un **atenuado** de #05070D al ~50 % por encima.
- **Franja**: la misma que la de resultados: de lado a lado, **340 px** de alto, y = 190, fondo #05070D al ~90 %, línea cian de 1 px arriba y abajo con halo. Mismo componente de franja.
- Tres columnas separadas por líneas verticales de 1 px, azul #245BFF al ~70 %, de 244 px de alto y centradas en la franja, en **x = 450** y **x = 915**.
- Cada columna se centra en vertical dentro de la franja.

### 3.2 Columna izquierda (x = 96)

- `RECORDS`: Orbitron Black ~52 px, cian, espaciado ~0.08 em, con halo.
- Debajo (16 px): selector de circuito `‹  CIRCUIT 01  ›`:
  - Nombre del circuito (`DisplayName` en mayúsculas): Orbitron Bold ~20 px, azul grisáceo, espaciado ~0.1 em.
  - Flechas `‹` `›` a los lados, Orbitron ~22 px. **Con un solo circuito**: azul #245BFF al ~55 % (desactivadas). Con más de uno: cian.
- Debajo: `1 / 1` (circuito actual / total) en Share Tech Mono ~13 px, azul grisáceo.

### 3.3 Columna central (x = 490)

Dos bloques, separados ~16 px:

- **TIME TRIAL**
  - Cabecera `TIME TRIAL`: Share Tech Mono ~15 px, **cian**, espaciado ~0.22 em.
  - Fila `BEST LAP`.
- **RACE**
  - Cabecera `RACE`, mismo estilo.
  - Fila `TOTAL TIME`.
  - Fila `BEST LAP`.

Cada fila, con tres columnas alineadas entre filas:

| Columna | Ancho | Estilo |
|---|---|---|
| Etiqueta (`BEST LAP`, `TOTAL TIME`) | ~116 px | Share Tech Mono ~13 px, azul grisáceo, espaciado ~0.22 em |
| Tiempo | ~150 px | Orbitron Medium, cian. ~24 px el BEST LAP de TIME TRIAL, ~20 px los de RACE |
| Fecha | resto | Share Tech Mono ~14 px, azul grisáceo |

- Tiempo con el formato de `TimeFormat`.
- Fecha en inglés y mayúsculas, formato `29 OCT 2026`, **independiente del idioma del sistema** (meses fijos: JAN, FEB, MAR, APR, MAY, JUN, JUL, AUG, SEP, OCT, NOV, DEC).
- **Sin récord** en una fila: tiempo `--:--.---` en azul grisáceo al ~60 %, y `NO RECORD` en lugar de la fecha.

### 3.4 Columna derecha (x = 950)

- Opciones **BACK** y **RESET RECORDS**, centradas en vertical en la franja.
- Mismo estilo que las de resultados: Orbitron ~22 px, filas de 44 px, barra lateral 4×30 px, texto seleccionado desplazado 16 px.
- **RESET RECORDS desactivada** cuando no hay **ningún** récord guardado (en ningún circuito ni modo): estilo de opción desactivada del menú principal (azul al ~55 %), sin `COMING SOON`, no se puede seleccionar.
- Si `RESET RECORDS` no cabe sin salirse por la derecha con la fuente real, se reduce el tamaño de las opciones (parámetro en el Inspector).

### 3.5 Confirmación del borrado

Dentro de la misma franja, como en la pausa:

- La columna izquierda se sustituye por:
  - `RESET RECORDS?` en dos líneas (`RESET` / `RECORDS?`), Orbitron Black ~36 px, **naranja** #FF9D32 con halo naranja.
  - Debajo: `ALL CIRCUITS AND MODES` / `CANNOT BE UNDONE` en dos líneas, Share Tech Mono ~15 px, azul grisáceo.
- La columna central sigue mostrando los récords del circuito actual.
- La columna derecha muestra **NO / YES**, con **NO** seleccionado al abrirse.
- Mientras está la confirmación, A/D y ←/→ **no** cambian de circuito.

## 4. Navegación

- **W / S** y **flechas arriba/abajo**: mueven la selección entre las opciones (saltando las desactivadas y dando la vuelta).
- **Enter / Espacio**: eligen (`menu_accept`).
- **A / D** y **flechas izquierda/derecha**: cambian de circuito (acciones nuevas `menu_left` y `menu_right`). Dan la vuelta.
- **Ratón**: pasar por encima selecciona, clic izquierdo elige. Clic en las flechas `‹` `›` cambia de circuito cuando están activas.
- **ESC**: en RECORDS equivale a BACK; en la confirmación equivale a NO.
- Teclado y ratón comparten una sola selección.

## 5. Datos

### 5.1 Lista de circuitos

- Hace falta saber qué circuitos existen para el selector. Se crea una **lista única de circuitos** del juego (por ejemplo `CircuitCatalog`, con las rutas de los `TrackDefinition`), con un solo elemento hoy: `circuit_01`.
- Esa misma lista es la que usa el menú para poner el circuito en `GameSession` (sustituye a la constante de la spec de Time Trial, 2.1, si encaja sin forzar).
- Orden del selector: el de la lista.
- Récords guardados de circuitos que ya no están en la lista: no se muestran (siguen en el archivo y se borran con RESET).

### 5.2 Lectura

- La pantalla lee los récords con el mismo almacenamiento y modelo de la spec de Time Trial. No duplica la lectura del JSON.
- Se leen **al abrir** la pantalla (no hace falta refrescar mientras está abierta, salvo tras el borrado).
- Si el archivo está corrupto, se aplica la misma regla que en Time Trial (`.bak` y empezar vacío) y la pantalla muestra el estado sin récords.
- **Lectura tolerante**, para poder ampliar el formato más adelante (por ejemplo, con el nombre del jugador) sin romper archivos existentes:
  - Un **campo desconocido** en el JSON se ignora, no es un error.
  - Si falta un **campo opcional** (fecha, tiempos de paso, y los que se añadan en el futuro), el récord se carga igual con ese dato vacío.
  - Solo es error (archivo corrupto) si falta lo imprescindible: la versión de formato o el tiempo de un récord.
  - Una versión de formato **mayor** que la que entiende el juego se trata como corrupta (regla del `.bak`), para no sobrescribir datos de una versión más nueva sin entenderlos.
  - Al guardar, se escribe siempre la versión de formato actual.

### 5.3 Fecha

- La spec de Time Trial guarda la fecha de cada récord. Se guarda como fecha local `yyyy-MM-dd` en el JSON.
- Si un récord no tiene fecha (por ejemplo, guardado antes de esto) o la fecha no se puede leer: se muestra `—` en lugar de la fecha. El récord se muestra igual.

### 5.4 Borrado

- YES en la confirmación borra **todos** los récords: todos los circuitos y los dos modos.
- Se hace con el mismo almacenamiento: se guarda un archivo de récords **vacío** (con su versión de formato), no se borra el archivo a mano.
- Tras borrar, la pantalla pasa al estado sin récords y RESET RECORDS queda desactivada.

## 6. Parámetros en el Inspector

| Parámetro | Valor por defecto |
|---|---|
| Opacidad del atenuado del fondo | 0,5 |
| Alto de la franja | 340 px |
| Tamaño de RECORDS | 52 px |
| Tamaño del título de confirmación | 36 px |
| Tamaño de las opciones | 22 px |

## 7. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Lógica de la pantalla** (clase propia, sin Godot):
  - Estados: `Viewing` y `ConfirmReset`.
  - Circuito actual (índice), `NextCircuit()` / `PreviousCircuit()` con vuelta, sin efecto con un solo circuito o en `ConfirmReset`.
  - Opciones con `MenuModel` (BACK / RESET RECORDS; NO / YES). Sabe si hay algún récord para activar o desactivar RESET.
  - Devuelve qué hacer: nada, volver al menú, borrar récords.
  - No toca archivos ni cambia de escena.
- **Formato de la vista** (puro, probado): texto de tiempo o `--:--.---`, fecha `29 OCT 2026` / `NO RECORD` / `—`.
- **Nodo de la pantalla** (`RecordsScreen` o similar, en `scripts/Menu`): dibuja la franja y las columnas reutilizando los componentes existentes (franja, lista de opciones, fuentes), traduce el input y llama al almacenamiento de récords para leer y borrar.
- **`MainMenu`**: activa RECORDS y abre/cierra la pantalla; al volver, selecciona RECORDS en su `MenuModel`.
- **`CircuitCatalog`** (o similar): la lista de circuitos, en un sitio único.

### Pruebas unitarias mínimas

Lógica de la pantalla:
1. Al abrir: estado `Viewing`, circuito 0, BACK seleccionada.
2. Sin ningún récord: RESET RECORDS desactivada; `MoveNext()` se queda en BACK.
3. Con algún récord: RESET RECORDS activa y seleccionable.
4. RESET RECORDS → `ConfirmReset` con NO seleccionado. NO → `Viewing` con RESET RECORDS seleccionada. ESC en `ConfirmReset` → igual que NO.
5. YES → acción "borrar récords"; tras borrar, `Viewing`, BACK seleccionada y RESET desactivada.
6. BACK o ESC en `Viewing` → acción "volver al menú".
7. Con un circuito, `NextCircuit()` / `PreviousCircuit()` no cambian nada. Con tres, dan la vuelta en los dos sentidos.
8. En `ConfirmReset`, cambiar de circuito no tiene efecto.

Formato:
9. Tiempo con récord → formato de `TimeFormat`; sin récord → `--:--.---`.
10. Fecha `2026-10-29` → `29 OCT 2026`, con cualquier cultura del sistema (probar con una cultura en español).
11. Sin récord → `NO RECORD`; récord sin fecha o fecha ilegible → `—`.

Récords:
12. Borrar deja el almacenamiento vacío con versión de formato, y al volver a cargar no hay récords.
13. Un JSON con un campo desconocido (por ejemplo `"player": "ARI"`) se carga sin error y con los mismos récords.
14. Un récord sin fecha ni tiempos de paso se carga; uno sin tiempo hace que el archivo se trate como corrupto.
15. Un JSON con una versión de formato mayor que la actual se trata como corrupto.

Las pruebas actuales siguen pasando.

## 8. Prueba manual

1. En el menú, RECORDS está activa (sin COMING SOON).
2. Sin récords guardados (borrar `records.json` a mano antes): todas las filas con `--:--.---` y `NO RECORD`; RESET RECORDS desactivada; BACK seleccionada.
3. ESC: vuelve al menú con RECORDS seleccionada. Volver a entrar.
4. Hacer una contrarreloj con un récord y una carrera completa. Volver a RECORDS: aparecen la mejor vuelta de TIME TRIAL y el total y la mejor vuelta de RACE, con la fecha de hoy en formato `29 OCT 2026`. Los tiempos coinciden con los del juego.
5. A/D y flechas izquierda/derecha: con un solo circuito no pasa nada y las flechas `‹ ›` se ven desactivadas.
6. RESET RECORDS: franja con `RESET RECORDS?` en naranja, NO seleccionado. ESC: vuelve, RESET RECORDS seleccionada. RESET → YES: todo vuelve a `--:--.---` / `NO RECORD`, RESET desactivada.
7. Entrar en TIME TRIAL: `BEST --:--.---`, sin delta. Confirma que el borrado es real.
8. Probar todo también con el ratón.
9. Ventana y pantalla completa: la franja y las columnas en su sitio, `RESET RECORDS` y las fechas sin pisar los separadores.

## 9. Fuera de alcance

- Borrar récords de un solo circuito o de un solo modo.
- Mostrar parciales por checkpoint, sectores o vuelta ideal.
- Nombre del jugador (el formato queda preparado para añadirlo, ver 5.2).
- Historial de tiempos (solo el mejor).
- Récords en línea o tablas de puntuación.
- Miniatura 3D del circuito en el selector.
- Animaciones, sonido, mando.
