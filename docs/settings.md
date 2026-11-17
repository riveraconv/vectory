# VECTORY — Spec: Settings

Versión 1.1 · 2026-11-17 · Estado: **etapa 1 en curso**

Añade la pantalla **SETTINGS**. Se entra desde el **menú principal** (deja de estar en COMING SOON) y desde la **pausa**. Tiene cinco grupos:

- **GAMEPLAY**: dificultad.
- **VIDEO**: modo de pantalla, resolución y bloom.
- **EFFECTS**: visión túnel, líneas de velocidad, FOV por velocidad, partículas y vibración de cabina.
- **CONTROLS**: teclas de conducción, que se pueden cambiar.
- **AUDIO**: preparado para cuando haya sonido, en COMING SOON.

Los cambios **se notan al momento** y **se guardan al salir**.

Diseño de referencia: **`designs/settings.html`** («VECTORY · Settings · 3 composiciones»). La elegida es la **C · BRIEFING**: todos los ajustes en un panel a la derecha y, a la izquierda, el ajuste elegido en grande con su explicación y una vista previa. Las composiciones A y B quedan descartadas. Las funciones `LAYOUTS.C`, `control`, `preview` y `trackPreview` y el CSS de la página son la referencia de medidas, colores y textos. Si algo no cuadra entre esta spec y la página, **manda la página** y se avisa.

Se apoya en lo que ya existe:
- `MainMenu` y la **lista de opciones reutilizable** de la spec `pausa-y-resultados`, con `MenuModel`, `SceneChanger`, `MenuFonts` y `Palette`.
- La **pausa** (`PauseMenu`) y su lógica.
- El **archivo de ajustes** donde se guarda la pintura de cada nave (spec `selector-pinturas`).
- El **objeto de ajustes de cámara** de la spec `camara` y el **objeto de ajustes de efectos** de la spec `efectos-velocidad`, preparados para SETTINGS.
- El glow del `WorldEnvironment` de la carrera, el menú y los selectores.
- Las **líneas de aire** del rebufo y la **dificultad** de la spec `rivales-4-personalidades`.
- El mapa de entrada de Godot (acciones de conducción).

Rama: `feature/settings`, que sale de `develop` con `feature/rivals-4-personality` ya mergeada. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Cambios respecto a specs anteriores:
- SETTINGS deja de estar en COMING SOON en el menú principal.
- La pausa gana la opción **SETTINGS**.
- Los objetos de ajustes de cámara y de efectos dejan de leer solo el Inspector: la base sale del Inspector y SETTINGS la **multiplica** (sección 4).
- La dificultad deja de elegirse en F9: se lee de SETTINGS. El selector de F9 se queda como **atajo de depuración** que no se guarda.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- Composición **C · BRIEFING** de la página.
- **Entrada**: menú principal → SETTINGS, y pausa → SETTINGS.
- **Aplicar**: cada cambio se nota al momento.
- **Guardar**: al salir (BACK o ESC), en el archivo de ajustes que ya existe.
- **Pantalla**: al cambiar el modo o la resolución, `KEEP THIS DISPLAY?` con cuenta atrás de **10 s**. Si no se confirma, vuelve a lo de antes.
- **Teclas**: se cambian con ENTER y la tecla nueva. Si está en uso, las dos acciones **se la intercambian**. ESC no se puede cambiar.
- **RESET DEFAULTS** pide confirmación.
- Etapas:
  - **0** · Inventario.
  - **1** · Pantalla, modelo y guardado, con GAMEPLAY, BLOOM y EFFECTS.
  - **2** · VIDEO: modo de pantalla y resolución.
  - **3** · CONTROLS.
  - **4** · Desde la pausa.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Dónde está y cómo es el **archivo de ajustes** de la pintura (ruta, formato, cómo se lee y se escribe), para guardar ahí también SETTINGS. Si no existe un archivo de ajustes como tal, se dice y se usa `user://settings.cfg` (`ConfigFile`).
2. Cómo están hoy el **objeto de ajustes de cámara** (spec `camara`) y el de **efectos** (spec `efectos-velocidad`): qué campos tienen, de dónde se leen y si se pueden cambiar en plena carrera.
3. Dónde está el **glow** de cada escena (carrera, menú principal, selectores) y si se puede multiplicar desde un sitio común.
4. Qué **modo de ventana** y qué **resolución** tiene hoy el juego al arrancar (Project Settings), y cómo está el *stretch*.
5. Cómo se llaman las **acciones de conducción** del mapa de entrada y qué teclas tienen. Qué otras acciones usan teclas que podrían chocar: menús, depuración (F6–F12) y pausa.
6. Si la **pausa** puede abrir otra pantalla encima sin salir de `GetTree().Paused`.
7. Cómo se lee hoy la **dificultad** (selector de F9 de la spec `rivales-4-personalidades`).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Pantalla

### 3.1 Composición

Medidas sobre 1280×720, escaladas como `MainMenu`. Todo como en `LAYOUTS.C` de la página:

- **Izquierda** (desde x = 72):
  - **Miga**: `MAIN MENU · SETTINGS` en Share Tech Mono 14 px, cian. Desde la pausa: `PAUSE · SETTINGS`.
  - **Nombre** del ajuste elegido: Orbitron Black 50 px, cian, con halo (y = 80).
  - **Grupo y valor**: `EFFECTS · 70 %`. El grupo en azul grisáceo, el valor en cian (y = 146).
  - **Explicación** del ajuste: Share Tech Mono 14 px, azul grisáceo, hasta 640 px de ancho (y = 176). Las partes resaltadas, en blanco azulado.
  - **Vista previa** (3.4): recuadro de 680 × 340 px en y = 262, con borde azul fino y `PREVIEW` arriba a la izquierda.
- **Derecha**: **panel** de 444 px de ancho, de arriba abajo de la pantalla, con la línea cian a la izquierda y su halo, como el del selector de pinturas.
  - Arriba, `SETTINGS` en Share Tech Mono 13 px, cian.
  - La **lista** con todos los ajustes, agrupados por grupo (cabecera pequeña, `AUDIO · COMING SOON`), en filas de **32 px**. Es una zona de 540 px de alto con desplazamiento.
  - Abajo, **RESET DEFAULTS** y **BACK** en horizontal, con el rombo de selección de los selectores.
- **Versión** abajo a la izquierda y **pie** con las teclas: `W/S SELECT · A/D CHANGE · ENTER · ESC BACK`.
- **Fondo**:
  - **desde el menú principal**: la rejilla de horizonte del menú, sin la nave;
  - **desde la pausa**: la carrera congelada con el atenuado de la pausa (#05070D al ~45 %).

### 3.2 Filas

Cada fila tiene el **nombre** a la izquierda (Orbitron 13 px) y el **control** a la derecha (Share Tech Mono 13 px):

| Tipo | Control |
|---|---|
| **Lista** | `‹ VALOR ›`. Las flechas desaparecen en el primer y el último valor. No da la vuelta |
| **Porcentaje** | 10 segmentos (cian los encendidos, borde azul los apagados) y el número, `70 %`. Pasos del 10 % |
| **Sí / no** | `OFF` / `ON`, con el elegido relleno en cian |
| **Tecla** | La tecla en un recuadro. Esperando una tecla nueva: `PRESS A KEY` en naranja, parpadeando. ESC, apagada |
| **Próximamente** | Toda la fila atenuada y `COMING SOON` en vez del control. No se puede elegir |

- **Fila elegida**: texto cian, barra cian de 3 px a la izquierda y un degradado cian suave detrás, como la fila elegida del selector de pinturas.

### 3.3 Navegación

**Teclado**:
- **W / S** o flechas arriba y abajo: fila anterior o siguiente, **saltando** las de AUDIO. De la última (BACK) se pasa a la primera.
- **A / D** o flechas izquierda y derecha: cambian el valor de la fila.
- **ENTER / ESPACIO**: en una lista pasa al siguiente valor (dando la vuelta), en sí/no cambia, en una tecla empieza a esperar la nueva, y en RESET DEFAULTS y BACK los elige.
- **ESC**: si está esperando una tecla, cancela. Si no, sale como BACK.
- La lista se **desplaza sola** para que la fila elegida se vea siempre, con 40 px de margen.

**Ratón**:
- Pasar por una fila la elige.
- Clic en las flechas, en un segmento, en `OFF` / `ON` o en la tecla los cambia.
- La rueda mueve la fila elegida.

Usa `MenuModel` para la selección. Las acciones de menú son las de siempre (`menu_up`, `menu_down`, `menu_accept`…), más dos nuevas, `menu_left` y `menu_right`.

### 3.4 Vista previa

Se dibuja en 2D, como en `preview` y `trackPreview` de la página. **No** es una escena 3D:

- **DIFFICULTY**: tres barras de alto creciente, EASY / NORMAL / HARD, encendidas hasta la elegida, y debajo `RIVALS: PACE · MISTAKES · ATTACK · DEFENCE`.
- **DISPLAY MODE / RESOLUTION**: un rectángulo de pantalla con la resolución y el modo. En ventana, más pequeño y con barra de título.
- **BLOOM** y todo **EFFECTS**: una pista en perspectiva con una nave rival magenta delante, que cambia con los valores:
  - el **bloom** sube o baja el halo;
  - la **visión túnel** oscurece los bordes;
  - las **líneas de velocidad** salen o no;
  - el **FOV** abre la perspectiva;
  - las **partículas** salen en más o menos cantidad;
  - la **vibración** hace temblar la pista.
- **Teclas**: la tecla elegida en grande y, al lado, la lista de todas las acciones con sus teclas.
- RESET DEFAULTS y BACK: sin vista previa.

## 4. Ajustes

Los textos en inglés son los de la página.

| Grupo | Ajuste | Valores | Por defecto | Qué hace |
|---|---|---|---|---|
| GAMEPLAY | **DIFFICULTY** | EASY / NORMAL / HARD | NORMAL | La de la spec `rivales-4-personalidades`. Se lee al montar la carrera |
| VIDEO | **DISPLAY MODE** | WINDOWED / FULLSCREEN / BORDERLESS | El de hoy (inventario, punto 4) | 4.1 |
| VIDEO | **RESOLUTION** | La lista de 4.2 | La de hoy | 4.2 |
| VIDEO | **BLOOM** | 0–100 % | 100 % | Multiplica la intensidad del glow de todas las escenas. 100 % = como hoy; 0 % = sin glow |
| EFFECTS | **TUNNEL VISION** | 0–100 % | 100 % | Multiplica la intensidad de la visión túnel |
| EFFECTS | **SPEED LINES** | OFF / ON | ON | Quita las líneas de velocidad **y las líneas de aire del rebufo** |
| EFFECTS | **SPEED FOV** | 0–100 % | 100 % | Multiplica el FOV extra por velocidad y el del boost |
| EFFECTS | **PARTICLES** | LOW / MEDIUM / HIGH | HIGH | Emisión y máximo de partículas al 25 / 50 / 100 %. En LOW, la visión túnel usa 6 muestras en vez de 16 (spec `efectos-velocidad`) |
| EFFECTS | **COCKPIT SHAKE** | 0–100 % | 100 % | Multiplica la vibración por velocidad y boost de la spec `camara`, en las dos cámaras. A 0 % no vibra |
| CONTROLS | Teclas | Sección 6 | Las de hoy | Sección 6 |
| AUDIO | MASTER, MUSIC, ENGINES, EFFECTS | — | — | COMING SOON. No se guardan |

- La base de cada efecto sigue saliendo del Inspector. SETTINGS solo la **multiplica**: con todo al 100 %, el juego se ve **exactamente igual que hoy**.
- **COCKPIT SHAKE** no toca la **sacudida por golpes** (barreras y naves): esa es un aviso del choque y se queda como está.
- Todos se aplican **al momento**, también si se cambian desde la pausa: al continuar, la carrera ya se ve con los valores nuevos.

### 4.1 Modo de pantalla

| Valor | Godot |
|---|---|
| WINDOWED | Ventana con marco, del tamaño de la resolución elegida, centrada |
| FULLSCREEN | Pantalla completa exclusiva |
| BORDERLESS | Ventana sin marco del tamaño de la pantalla |

### 4.2 Resolución

- Lista fija: 1280 × 720, 1600 × 900, 1920 × 1080, 2560 × 1440 y 3840 × 2160. Solo salen las que **caben en la pantalla** del jugador.
- En **WINDOWED** es el tamaño de la ventana.
- En **FULLSCREEN y BORDERLESS** la ventana ocupa la pantalla y la resolución es la del **render 3D**: se escala la imagen 3D (escala del 3D del viewport = resolución elegida / la de la pantalla). El HUD y los menús siempre se dibujan a la resolución de la pantalla, nítidos.
- Si al arrancar la guardada no cabe en la pantalla (por ejemplo, se cambió de monitor), se usa la mayor de la lista que quepa.
- Si el inventario dice que la escala del 3D da problemas, se avisa con lo que sí se puede hacer.

### 4.3 Confirmación de pantalla

- Al cambiar el **modo** o la **resolución**, el cambio se aplica al momento y sale un aviso encima de todo, con el estilo de la confirmación de la pausa:
  - `KEEP THIS DISPLAY?`
  - `REVERTING IN 10` (cuenta atrás de segundo en segundo)
  - **KEEP** / **REVERT**, con KEEP elegida.
- **KEEP**: se queda.
- **REVERT**, **ESC** o fin de la cuenta atrás: vuelve al modo y la resolución de antes.
- Mientras sale el aviso, el resto de la pantalla no responde.
- Si se cambia con A/D varias veces seguidas, el aviso sale **una vez**, 0,5 s después del último cambio, y REVERT vuelve a lo que había **antes del primero**.

### 4.4 Decisiones y concreciones (v1.1)

- **`efectos-velocidad` no está hecha** (decisión del usuario): TUNNEL VISION y PARTICLES salen en COMING SOON, como AUDIO (no se eligen ni se guardan). SPEED LINES funciona y, de momento, solo quita las líneas de aire del rebufo; cuando existan las líneas de velocidad, las quitará también. La prueba 10 no aplica.
- **Modo de pantalla** (decisión del usuario): hoy el juego es la pantalla completa sin bordes de Godot, que con las definiciones de 4.1 es BORDERLESS. Por defecto, **BORDERLESS** (así nada cambia); FULLSCREEN será la exclusiva.
- **Archivo**: el de ajustes ya existe, `user://settings.json`; SETTINGS va ahí en `gameplay`, `video` y `effects` (y `controls` en la etapa 3). Listas por su texto (`"HARD"`, `"1920x1080"`, sin distinguir mayúsculas), porcentajes de 0 a 100 en pasos de 10 y sí / no como `true` / `false`. Un archivo de antes de SETTINGS no avisa de nada: todo por defecto.
- **BLOOM** multiplica el glow de las escenas (también las que ya están en pantalla) y el de la cabina 3D. **SPEED FOV**: el FOV por velocidad de la cámara exterior (de `BaseFov` a `MaxFov`) y el FOV extra del boost en las dos. **COCKPIT SHAKE**: la vibración de las dos cámaras y la de la cabina 3D.
- RESET DEFAULTS y BACK, y NO / YES, van con la barra lateral de los selectores del juego, no con el rombo de la página.
- La dificultad se guarda en SETTINGS; F9 sigue cambiándola para la carrera siguiente sin guardarla, hasta el próximo cambio en SETTINGS.

## 5. Guardar y cargar

- Se guarda en el **archivo de ajustes** donde ya va la pintura de cada nave, en secciones propias (`gameplay`, `video`, `effects`, `controls`). Si no hay archivo de ajustes, en `user://settings.cfg`.
- **Cuándo**: al salir de SETTINGS (BACK o ESC) y al confirmar RESET DEFAULTS. No en cada cambio.
- Si se cierra el juego con SETTINGS abierta sin salir, lo no guardado se pierde. Es lo esperado.
- **Al arrancar el juego**, antes del menú principal, se cargan y se aplican: modo, resolución, bloom, efectos, vibración, teclas y dificultad.
- Valores que **faltan**, de **otro tipo** o **fuera de rango**: se usa el de por defecto **solo para ese valor** y se apunta en la consola. El juego arranca siempre.
- Se guarda la pintura de cada nave igual que hoy, sin tocar sus claves.

## 6. Teclas

### 6.1 Acciones

| Fila | Acción | Por defecto |
|---|---|---|
| ACCELERATE | acelerar | W |
| BRAKE | frenar | S |
| STEER LEFT | girar a la izquierda | A |
| STEER RIGHT | girar a la derecha | D |
| BOOST | boost | SHIFT |
| DRIFT | derrape | SPACE |
| CAMERA | cambiar de cámara | C |
| PAUSE | pausa | ESC (fija) |

- Los nombres de las acciones del mapa de entrada son los que diga el inventario. Si una de la tabla no existe como acción, se avisa.
- Si hoy una acción tiene **dos teclas** (por ejemplo, W y flecha arriba), SETTINGS cambia la **primera**. La segunda se queda como está.

### 6.2 Cambiar una tecla

1. ENTER (o clic) en la fila: `PRESS A KEY`.
2. La siguiente tecla que se pulse es la nueva.
   - **ESC** cancela y deja la que había.
   - **F1–F12** no valen (están reservadas para depuración): se ignoran y sigue esperando.
   - Si la tecla **ya la usa otra acción** de la tabla, las dos se **intercambian** y sale un aviso corto: `SWAPPED WITH BRAKE`.
3. La tecla nueva funciona **al momento** en la carrera.

- Las teclas de los **menús** (W/S, flechas, ENTER, ESPACIO, ESC) **no cambian**. Así nunca te quedas sin poder moverte por los menús.
- RESET DEFAULTS devuelve también las teclas.

## 7. Entrar y salir

### 7.1 Desde el menú principal

- SETTINGS pasa a estar **activa** en la lista de `MainMenu`. Solo cambia el valor de la lista, como dice su spec.
- Al entrar, la fila elegida es la **primera** (DIFFICULTY).
- **BACK / ESC**: guarda y vuelve al menú principal con **SETTINGS** elegida.

### 7.2 Desde la pausa

- La pausa queda: **CONTINUE, SETTINGS, RESTART, BACK TO MAIN MENU**.
- SETTINGS se abre **encima** de la carrera, que sigue en pausa. No se cambia de escena.
- **BACK / ESC**: guarda y vuelve a la pausa con **SETTINGS** elegida.
- Mientras está abierta, las teclas no llegan a la nave, igual que en la pausa. Al continuar, la nave no recibe nada de lo pulsado en SETTINGS.
- **DIFFICULTY** se puede cambiar, pero la explicación lo deja claro: vale para la **siguiente carrera**. RESTART ya la usa.
- Cambiar el modo o la resolución desde la pausa funciona igual, con su confirmación.

### 7.3 RESET DEFAULTS

- Pide confirmación: `RESET ALL SETTINGS?` con **NO** / **YES** y NO elegida, como la pausa.
- **YES**: todo vuelve a su valor por defecto, se aplica, se guarda y sale el aviso corto `DEFAULTS RESTORED`.
- Si con eso cambian el modo o la resolución, sale además la confirmación de pantalla (4.3).

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Alto de fila | 32 px | SETTINGS |
| Zona de la lista | 540 px | SETTINGS |
| Margen de desplazamiento | 40 px | SETTINGS |
| Paso de los porcentajes | 10 % | SETTINGS |
| Cuenta atrás de pantalla | 10 s | SETTINGS |
| Espera antes del aviso de pantalla | 0,5 s | SETTINGS |
| Resoluciones | Lista de 4.2 | SETTINGS |
| Partículas LOW / MEDIUM / HIGH | 25 / 50 / 100 % | Efectos |
| Teclas reservadas | ESC, F1–F12 | SETTINGS |

## 9. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Catálogo de ajustes** (puro, datos): cada ajuste con `id`, grupo, tipo, valores o rango, por defecto y explicación.
- **Modelo de ajustes** (puro): valores actuales, `Change(id, ±1)`, `Set`, `Reset`, y validar un valor (rango y tipo). Avisa cuando cambia un valor.
- **Serializador** (puro): modelo ↔ secciones del archivo, con la regla de 5 (valor a valor, por defecto si está mal).
- **Almacén de ajustes** (nodo o servicio): lee y escribe el archivo, junto a lo que ya guarda la pintura. Sin reglas.
- **Aplicadores** (cada uno, solo lo suyo):
  - pantalla: modo, resolución y escala del 3D, con la lista filtrada por la pantalla;
  - glow;
  - efectos y cámara: escriben los multiplicadores en los objetos de ajustes que ya existen;
  - entrada: cambia las teclas del mapa de entrada;
  - dificultad: la deja donde la lee la carrera.
- **Teclas** (puro): cambiar una tecla con la regla del intercambio y las reservadas.
- **Confirmación de pantalla** (pura): estado, cuenta atrás con `Update(delta)`, KEEP / REVERT y el valor al que volver, con la espera de 0,5 s.
- **Pantalla SETTINGS** (nodo): dibuja la composición C, traduce teclado y ratón al modelo y llama a los aplicadores. La vista previa es un `Control` propio que solo **lee** el modelo. Sin reglas.
- **Pausa**: gana SETTINGS en su lógica (`Choose`) y abre la pantalla encima.

## 10. Pruebas

### Pruebas unitarias mínimas

Modelo y guardado (etapa 1):
1. Con todo por defecto, los multiplicadores son 1 y la dificultad NORMAL.
2. Una lista no pasa del primer ni del último valor con A/D. Con ENTER da la vuelta.
3. Un porcentaje va de 0 a 100 en pasos de 10 y no sale de ahí.
4. `Reset` deja todos los valores por defecto.
5. Guardar y cargar da los mismos valores.
6. Un archivo con un valor que falta, uno de otro tipo y otro fuera de rango: esos tres por defecto, los demás como estaban.
7. Guardar SETTINGS no cambia las claves de la pintura.

Efectos (etapa 1):
8. Con BLOOM, TUNNEL VISION, SPEED FOV y COCKPIT SHAKE al 100 %, los valores que reciben el glow, el túnel, el FOV y la vibración son los de hoy. Al 50 %, la mitad. Al 0 %, nada.
9. SPEED LINES OFF quita las líneas de velocidad y las del rebufo.
10. PARTICLES LOW: emisión y máximo al 25 % y 6 muestras en el túnel.
11. COCKPIT SHAKE al 0 % no cambia la sacudida por golpes.

Pantalla (etapa 2):
12. La lista de resoluciones solo tiene las que caben en la pantalla.
13. Confirmación: KEEP se queda. REVERT, ESC o los 10 s vuelven al valor de antes.
14. Tres cambios seguidos en menos de 0,5 s: un solo aviso, y REVERT vuelve al valor de antes del primero.
15. Al cargar una resolución que no cabe, se usa la mayor que cabe.

Teclas (etapa 3):
16. Cambiar ACCELERATE a una tecla libre: la tiene ACCELERATE.
17. Cambiar ACCELERATE a S: ACCELERATE pasa a S y BRAKE a W.
18. ESC y F1–F12 no se aceptan. ESC cancela.
19. PAUSE no se puede cambiar.
20. Las teclas de los menús no cambian al cambiar las de conducción.

Pausa (etapa 4):
21. La pausa tiene CONTINUE, SETTINGS, RESTART y BACK TO MAIN MENU. Elegir SETTINGS la abre, y al salir se vuelve a la pausa con SETTINGS elegida.
22. Las pruebas de la lógica de la pausa siguen pasando, con la opción nueva.

Las pruebas existentes siguen pasando. Las de `MenuModel` que daban SETTINGS como desactivada se adaptan.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Pantalla, modelo y guardado** | Secciones 3 y 5, entrada desde el menú (7.1), RESET DEFAULTS (7.3), y de la 4: DIFFICULTY, BLOOM y EFFECTS. VIDEO y CONTROLS se ven pero sin aplicar | Pruebas 1–11 · Prueba manual 1–7 · Commit `feat:` |
| **2 · VIDEO** | Modo, resolución y confirmación (4.1–4.3) | Pruebas 12–15 · Prueba manual 8–11 · Commit `feat:` |
| **3 · CONTROLS** | Sección 6 | Pruebas 16–20 · Prueba manual 12–15 · Commit `feat:` |
| **4 · Desde la pausa** | 7.2 · versión | Pruebas 21–22 · Prueba manual 16–20 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Pantalla, modelo y guardado**
1. Menú principal: SETTINGS ya no está en COMING SOON. Entrar: se ve como la composición C de la página.
2. Moverse con W/S y con el ratón: la lista se desplaza sola y AUDIO no se puede elegir. La izquierda cambia con cada fila, con su vista previa.
3. Bajar BLOOM: la vista previa y el fondo brillan menos. Entrar en una carrera: el glow está bajo. Volver a 100 %: como siempre.
4. Bajar TUNNEL VISION, SPEED FOV y COCKPIT SHAKE a 0 %, quitar SPEED LINES y poner PARTICLES en LOW. En carrera, a fondo y con boost: sin túnel, sin FOV extra, sin vibración, sin líneas (tampoco las del rebufo) y menos partículas. Chocar: la sacudida por golpe sigue.
5. Cambiar DIFFICULTY a HARD y correr: los rivales van en difícil.
6. Salir con BACK, cerrar el juego y abrirlo: todo como se dejó. La pintura de cada nave sigue igual.
7. RESET DEFAULTS: pregunta. NO: nada cambia. YES: todo por defecto y `DEFAULTS RESTORED`.

**Etapa 2 · VIDEO**
8. Cambiar a WINDOWED: la ventana cambia y sale `KEEP THIS DISPLAY?` con la cuenta atrás. Esperar 10 s: vuelve a pantalla completa.
9. Cambiar la resolución y KEEP: se queda. En pantalla completa, el 3D se ve más o menos nítido según la resolución, y el HUD y los menús siempre nítidos.
10. Pasar varias resoluciones seguidas con D: un solo aviso. REVERT: vuelve a la de antes de empezar.
11. Cerrar y abrir el juego: arranca con el modo y la resolución guardados.

**Etapa 3 · CONTROLS**
12. Cambiar BOOST a E: en carrera, el boost va con E y SHIFT ya no hace nada.
13. Cambiar ACCELERATE a S: `SWAPPED WITH BRAKE`. En carrera, S acelera y W frena.
14. Intentar ESC o F9: no se aceptan. ESC cancela.
15. Los menús siguen yendo con W/S y las flechas aunque se hayan cambiado las teclas de conducción.

**Etapa 4 · Desde la pausa**
16. En carrera, ESC: la pausa tiene SETTINGS. Entrar: la carrera congelada y atenuada de fondo, y `PAUSE · SETTINGS` en la miga.
17. Bajar TUNNEL VISION y salir: vuelve a la pausa con SETTINGS elegida. CONTINUE: la carrera sigue con el túnel nuevo, y la nave no recibe las teclas pulsadas en SETTINGS.
18. Cambiar DIFFICULTY desde la pausa: la carrera en curso no cambia. RESTART: la nueva va con la dificultad elegida.
19. Cambiar una tecla desde la pausa: al continuar, ya va con la nueva.
20. Cambiar a WINDOWED desde la pausa: sale la confirmación y la carrera sigue congelada detrás.

## 12. Fuera de alcance

- **Audio**: el grupo se ve en COMING SOON. Llega con el sonido.
- **Mando**: teclas de mando y su configuración.
- Dos teclas por acción desde SETTINGS (la segunda tecla, si existe hoy, se queda como está).
- Inclinación de la cabina y efecto del boost en la cámara (propuestos en la spec `camara`): no entran de momento.
- VSync, límite de FPS, calidad de sombras y otros gráficos.
- Idioma y textos en otros idiomas.
- Perfiles de ajustes o varios jugadores.
- SETTINGS desde otras pantallas (selectores, resultados).
- Animaciones de entrada y salida.
