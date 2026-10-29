# VECTORY — Spec: Menú principal

Versión 1.2 · 2026-10-29 · Estado: **definido, listo para implementar**

Primera spec de menús. Solo cubre el **menú principal**: el juego arranca en él, desde él se entra a una carrera y se sale del juego. El resto de opciones aparecen pero marcadas como **COMING SOON**. Pausa, vuelta al menú desde la carrera y el resto de pantallas van en specs posteriores, una por una.

Diseño de referencia: lienzo **"VECTORY — Menú principal"** (artboards *Menú principal* y *Estados de las opciones*). Si algo de esta spec no cuadra con el lienzo, manda esta spec y se avisa.

Rama: `feature/menus` (sale de `develop`).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- Al abrir el juego aparece el **menú principal**, no la carrera.
- **Todos los textos del juego en inglés.**
- Opciones, en este orden: **RACE, CHAMPIONSHIP, TIME TRIAL, HANGAR, RECORDS, SETTINGS, QUIT**.
- Funcionales: **RACE** (empieza una carrera, como ahora al cargar la escena) y **QUIT** (cierra el juego).
- CHAMPIONSHIP, TIME TRIAL, HANGAR, RECORDS y SETTINGS se ven **desactivadas**, con la etiqueta **COMING SOON**, y no se pueden elegir.
- Composición: título y opciones **a la izquierda**; a la derecha, **la nave del juego vista de frente** sobre una **rejilla de horizonte** estilo años 80; **número de versión** abajo a la izquierda.
- Se maneja con **teclado** (W/S o flechas + Enter) y con **ratón**.

---

## 2. Flujo

| Desde | Acción | Resultado |
|---|---|---|
| Abrir el juego | — | Menú principal, con **RACE** seleccionada |
| Menú principal | Elegir **RACE** | Se carga la escena de carrera (empieza en Previa, con parrilla y semáforo, igual que ahora) |
| Menú principal | Elegir **QUIT** | Se cierra el juego |
| Menú principal | Elegir una opción desactivada | No pasa nada |
| Pantalla de resultados | Enter | Repite la carrera (sin cambios respecto a la spec de carrera) |

- La escena del menú pasa a ser la **escena principal** del proyecto (la que arranca al ejecutar).
- La escena de carrera actual no cambia: se sigue pudiendo ejecutar sola desde el editor para probar.
- De momento **no hay forma de volver al menú desde la carrera**. Eso entra en la siguiente spec (pausa / resultados).

## 3. Opciones

| Opción | Estado | Al elegirla |
|---|---|---|
| RACE | Activa | Carga la carrera |
| CHAMPIONSHIP | Coming soon | Nada |
| TIME TRIAL | Coming soon | Nada |
| HANGAR | Coming soon | Nada |
| RECORDS | Coming soon | Nada |
| SETTINGS | Coming soon | Nada |
| QUIT | Activa | Cierra el juego |

- Textos en mayúsculas.
- Las opciones se definen como **una lista de datos** (identificador, texto, activa sí/no). Activar una opción más adelante debe ser cambiar un valor en esa lista y conectar su acción, sin tocar el dibujado del menú.

## 4. Navegación

### Teclado

- **W / flecha arriba**: opción anterior.
- **S / flecha abajo**: opción siguiente.
- **Enter / Espacio**: elegir la opción seleccionada.
- La selección **salta las opciones desactivadas** (de RACE, bajando, se pasa directamente a QUIT).
- La navegación **da la vuelta**: desde QUIT, bajando, se vuelve a RACE; desde RACE, subiendo, a QUIT.
- **Esc** en el menú principal **no hace nada** (para no cerrar el juego sin querer).
- Acciones de input: `menu_up`, `menu_down`, `menu_accept`.

### Ratón

- Pasar el ratón por encima de una opción **activa** la selecciona.
- Clic izquierdo en una opción activa la elige.
- Las opciones desactivadas no reaccionan al ratón (ni se seleccionan ni hacen nada al hacer clic).
- Teclado y ratón comparten la misma selección: solo hay una opción seleccionada a la vez.

## 5. Aspecto

Estética del documento maestro: fondo oscuro, solo líneas y texto luminoso, nada de botones rellenos ni texturas. Medidas tomadas sobre una pantalla de referencia de **1280×720**; deben escalar con la resolución (anclajes y stretch del proyecto, no posiciones fijas en píxeles absolutos).

### 5.1 Fondo (escena 3D)

El fondo es una escena 3D real, no una imagen, para que la nave sea **exactamente la del juego**.

- **Color de fondo**: #05070D (`Palette.Background`).
- **WorldEnvironment**: el mismo glow/bloom que en la carrera, para que las líneas brillen igual.
- **Nave**: una instancia de `Spaceship` (la misma clase que en la carrera, sin copiar ni redibujar la geometría), quieta, **vista de frente**: el morro apunta a la cámara.
- **Cámara**: fija, delante de la nave y algo por encima, mirando a la nave. Referencia: nave en el origen, cámara en (0, 2.2, -12) mirando a (0, 0.1, 0). La nave queda en la **mitad derecha** de la pantalla (centro aprox. en x = 940 de 1280), con la base del ala apoyada más o menos en la línea del horizonte. Si para colocarla así hace falta desplazar la cámara o la nave, se ajusta; posición, objetivo y FOV expuestos en el Inspector.
- **Rejilla de horizonte**: suelo de líneas por debajo de la nave en azul #245BFF (`Palette.Blue`), líneas longitudinales que convergen hacia el horizonte (debajo de la nave) y transversales cada vez más juntas hacia el fondo. Se **desvanece** hacia el horizonte (lejos no se ve; cerca, nítida). Generada por código como un solo mesh de líneas, igual que la pista.
- **Línea del horizonte**: una línea fina cian (`Palette.Cyan`) a lo ancho, tenue en los extremos y más visible a la derecha, debajo de la nave.
- **Sombra izquierda**: un degradado horizontal de #05070D, casi opaco a la izquierda y transparente hacia el centro (aprox. el 60 % izquierdo de la pantalla), por encima del 3D y por debajo del texto, para que el menú se lea bien sobre la rejilla.
- Todo **estático**: sin rotación de la nave ni movimiento de la rejilla en esta versión.

### 5.2 Título

- Arriba a la izquierda (referencia: x = 96, y = 88).
- **VECTORY**: fuente Orbitron Black, ~84 px, cian #36E7FF, espaciado entre letras ~0.06 em, con halo cian.
- **Líneas de velocidad**: a la izquierda de VECTORY, tres trazos horizontales de longitud distinta (cian, azul y cian tenue), alineados a la derecha pegados al título.
- **RACING**: debajo de VECTORY, Orbitron Bold ~18 px, **naranja** #FF9D32, espaciado entre letras amplio (~0.6 em), con halo naranja suave; a su derecha una línea cian fina de ~300 px que se desvanece hacia la derecha.

### 5.3 Lista de opciones

- En columna, alineada a la izquierda con el título (referencia: x = 96, y = 264), separación vertical uniforme. Cada fila mide 48 px de alto.
- Opciones en Orbitron ~28 px, espaciado entre letras ~0.12 em.

| Estado de la opción | Aspecto |
|---|---|
| **Seleccionada** | **Barra lateral** vertical cian de 4×36 px a la izquierda, con halo; texto **desplazado 20 px a la derecha** respecto a las demás; Orbitron Bold, cian **#36E7FF** con halo |
| **Activa, sin seleccionar** | Sin barra; Orbitron Medium, azul grisáceo **#A6B8D8** |
| **Desactivada** | Sin barra; Orbitron Medium, azul **#245BFF** al ~55 % de opacidad y, a su derecha (separación ~18 px), `COMING SOON` en Share Tech Mono ~13 px, azul grisáceo **#A6B8D8** al ~60 %, espaciado entre letras amplio |

- Al cambiar de selección, la barra y el desplazamiento saltan a la nueva opción **al instante** (sin animación).
- El hueco de la barra se reserva en todas las filas, para que el texto no seleccionado quede alineado.

### 5.4 Versión

- Abajo a la izquierda (referencia: x = 100, 44 px desde abajo), Share Tech Mono ~14 px, azul grisáceo #A6B8D8 al ~70 %, con formato **`v0.1.0`** (tres números).
- El número **no se escribe a mano en el menú**: se lee de **`application/config/version`** del proyecto (Project Settings → Application → Config → Version), que es el número real de la versión. Para esta versión del juego: **0.1.0**.
- Si el valor no existe o está vacío, se muestra `v0.0.0`.

### 5.5 Fuentes y colores

- Fuentes: **Orbitron** (títulos y opciones) y **Share Tech Mono** (COMING SOON y versión), ambas con licencia libre (OFL, Google Fonts). Se añaden al proyecto en una carpeta de fuentes, junto a su archivo de licencia. En esta spec **solo las usa el menú**; pasar el HUD a estas fuentes será otra tarea, si se decide.
- Colores siempre desde `Palette`, sin hardcodear. Las atenuaciones se hacen con opacidad sobre colores de la paleta; si hace falta, se añaden roles nuevos a `Palette` (p. ej. `MenuSelected`, `MenuIdle`, `MenuDisabled`, `MenuHint`, `Version`, `MenuGrid`), derivados de los colores base, como el resto de roles.

## 6. Parámetros en el Inspector

| Parámetro | Valor por defecto |
|---|---|
| Ruta de la escena de carrera | la escena de carrera actual |
| Posición de la cámara del fondo | (0, 2.2, -12) |
| Punto al que mira la cámara | (0, 0.1, 0) |
| FOV de la cámara | el que deje la nave como en el lienzo |
| Tamaño de fuente del título | 84 px |
| Tamaño de fuente de RACING | 18 px |
| Tamaño de fuente de las opciones | 28 px |
| Tamaño de fuente de COMING SOON | 13 px |
| Tamaño de fuente de la versión | 14 px |
| Separación vertical entre opciones | 6 px (filas de 48 px) |
| Tamaño de la barra de selección | 4 × 36 px |
| Desplazamiento del texto seleccionado | 20 px |

## 7. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces que ya hay en el proyecto (`scripts/UI`, `scripts/Core`, etc.).

- **Modelo del menú** (clase propia, sin dependencias de nodos de Godot):
  - Recibe la lista de opciones (identificador, texto, activa).
  - Expone el índice seleccionado.
  - `MoveNext()` / `MovePrevious()`: mueven la selección saltando las desactivadas y dando la vuelta.
  - `Select(índice)`: selecciona una opción concreta (para el ratón); si está desactivada, no cambia nada.
  - `Confirm()`: devuelve el identificador de la opción elegida si está activa, o ninguno.
  - Al crearse, selecciona la primera opción activa.
- **Nodo del menú**: dibuja las opciones leyendo el modelo, traduce teclado y ratón a llamadas al modelo y ejecuta la acción que devuelve `Confirm()` (cargar carrera / salir). No contiene lógica de navegación.
- **Fondo del menú**: nodo propio (cámara, nave, rejilla, horizonte), separado del nodo del menú. Reutiliza `Spaceship` tal cual; si hiciera falta tocar `Spaceship` para poder instanciarla fuera de la carrera, se avisa antes.
- **Rejilla**: su generación en una clase propia, reutilizable.
- **Versión**: un método pequeño que lee la versión del proyecto y la formatea (`v` + número, `v0.0.0` si falta). El formateo es puro y se prueba.
- **Cambio de escena**: en un sitio único (una clase o método pequeño), para que más adelante pausa, resultados y otras pantallas lo reutilicen sin duplicar código.

### Pruebas unitarias mínimas

1. Al crearse, la opción seleccionada es RACE.
2. Desde RACE, `MoveNext()` lleva a QUIT (salta las 5 desactivadas).
3. Desde QUIT, `MoveNext()` vuelve a RACE.
4. Desde RACE, `MovePrevious()` lleva a QUIT.
5. `Select()` sobre una opción desactivada no cambia la selección.
6. `Select()` sobre una opción activa la selecciona.
7. `Confirm()` con RACE seleccionada devuelve RACE; con QUIT, devuelve QUIT.
8. Si la primera opción de la lista está desactivada, al crearse se selecciona la primera activa.
9. Con todas las opciones activas, `MoveNext()` pasa por todas en orden.
10. Formato de versión: `"0.1.0"` → `"v0.1.0"`; vacío o nulo → `"v0.0.0"`.

## 8. Prueba manual

1. Ejecutar el juego: aparece el menú con VECTORY / RACING arriba a la izquierda, las 7 opciones debajo, RACE seleccionada con la barra cian y la versión `v0.1.0` abajo a la izquierda.
2. A la derecha se ve la nave del juego de frente, sobre la rejilla, con el mismo brillo que en la carrera. Compararla con la vista exterior (tecla C) en la carrera: misma nave.
3. CHAMPIONSHIP, TIME TRIAL, HANGAR, RECORDS y SETTINGS se ven atenuadas con COMING SOON.
4. Pulsar S / flecha abajo: la selección salta de RACE a QUIT. Otra vez: vuelve a RACE. Probar también hacia arriba.
5. Pasar el ratón por encima de las opciones: RACE y QUIT se seleccionan; las desactivadas no.
6. Hacer clic o pulsar Enter en una opción desactivada: no pasa nada.
7. Pulsar Esc: no pasa nada.
8. Elegir RACE (con Enter, Espacio y clic, en ejecuciones distintas): empieza la carrera con parrilla y semáforo, igual que antes.
9. Elegir QUIT: el juego se cierra.
10. Cambiar la versión en Project Settings a otro valor, ejecutar: el menú muestra el nuevo número. Dejarla en 0.1.0.
11. Probar en ventana y a pantalla completa: todo en su sitio y legible.
12. Ejecutar la escena de carrera sola desde el editor: sigue funcionando como antes.

## 9. Fuera de alcance

- Pausa y volver al menú desde la carrera o desde los resultados (siguiente spec).
- CHAMPIONSHIP, TIME TRIAL, HANGAR, RECORDS y SETTINGS (cada una en su spec).
- Selección de circuito.
- Animaciones: nave girando, rejilla en movimiento, transiciones de selección o entre escenas.
- Cambiar la fuente del HUD.
- Traducir al inglés textos de otras pantallas (HUD, resultados, semáforo): se revisa en una tarea aparte.
- Sonido.
- Mando.
- Confirmación al salir.
