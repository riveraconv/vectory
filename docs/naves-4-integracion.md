# VECTORY — Spec: Naves 4 · Integración y nombres definitivos

Versión 1.1 · 2026-11-05 · Estado: **etapa 0 validada; etapas 1 a 3 pendientes**

Cuarta spec de naves. Deja las tres naves **jugables de verdad**: se eligen en el selector y corren en RACE y TIME TRIAL con su conducción, su casco, su cabina, su colisión y su fantasma. Además, **fija los nombres definitivos** en todo el código.

**Nombres definitivos** (en inglés, en el juego y en el código):

| Id | Nombre | Antes | En el lienzo |
|---|---|---|---|
| `ship_01` | **VECTOR** | Vector | Vector |
| `ship_02` | **BLADE** | Interceptor / Hoja | Hoja |
| `ship_03` | **HAMMER** | Vanguard / Martillo | Martillo |

Los **ids no cambian**: los fantasmas y los récords guardados los usan.

Plan de las specs de naves:

1. Definición de nave (hecha).
2. Perfil técnico, colisión y ajuste en pruebas (hecha).
3. Cascos (hecha).
3b. Cabinas (hecha).
4. **Integración y nombres definitivos** (esta).
5. Piloto automático de equilibrio (opcional).

Se apoya en lo que ya existe tras las specs 1 a 3b: `ShipCatalog`, `ShipDefinitions` y las tres definiciones, `ShipStats`, `ShipSelectScreen` / `ShipSelectLogic`, `ShipHologram`, `HologramSpin`, `GameSession.ShipDesignId`, `Player`, `Spaceship`, `Cockpit`, el panel de pruebas (F9–F12), el fantasma y su archivo, los récords (`records.json`) y la pantalla RECORDS, los resultados de RACE y `user://settings.json`.

Rama: `feature/ship-integration`, que sale de `develop` con `feature/ship-cockpits` ya mergeada. La etapa 0 va con commits `refactor:` y el resto con `feat:`.

Versión del juego: **0.13.4 → 0.14.0** (minor: primeras naves nuevas jugables). Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 0 · Nombres**: Interceptor, Vanguard, Hoja y Martillo pasan a llamarse **BLADE** y **HAMMER** en todo el código. Es un refactor sin cambios para el jugador, salvo el texto del selector y el rótulo de la cabina de BLADE.
- **Etapa 1 · Selector**: las tres naves se pueden elegir. BLADE y HAMMER dejan de ser COMING SOON y tienen holograma, datos y START. Las tres se enseñan a la **misma escala**. La **última nave elegida se guarda en disco**.
- **Etapa 2 · Carrera, TIME TRIAL, fantasma y resultados**: la nave elegida es la que corre. El fantasma va con la nave del récord. Los resultados de RACE dicen con qué nave se ha corrido.
- **Etapa 3 · Récords**: cada récord guarda con qué nave se hizo y RECORDS lo enseña. Los récords siguen siendo **por circuito, sea cual sea la nave**.
- Boost, daños, efectos, cámara, HUD y menú principal no cambian. La nave del fondo del menú sigue siendo VECTOR.

## 2. Etapa 0 · Nombres definitivos

### 2.1 Inventario

Antes de cambiar nada, el agente busca en el código, las escenas, los recursos, las pruebas, las capturas, los comentarios y los textos de consola las apariciones de `Interceptor`, `Vanguard`, `Hoja` y `Martillo`, en cualquier combinación de mayúsculas, y apunta en un **informe corto** dónde están y qué son.

- Se renombra solo lo que se refiere a las **naves**. Por ejemplo, "hoja" como palabra normal en un comentario no se toca.
- **Si hay algo dudoso, se pregunta antes de cambiarlo.**

### 2.2 Qué se renombra

| Antes | Después |
|---|---|
| `Interceptor`, `Hoja` (clases, archivos, métodos, variables, pruebas) | `Blade` |
| `Vanguard`, `Martillo` | `Hammer` |
| `INTERCEPTOR`, `VANGUARD` (textos que ve el jugador) | `BLADE`, `HAMMER` |

- Entran las clases y archivos de las specs 2, 3 y 3b: definiciones, cascos, cabinas, perfiles visuales, sus pruebas y sus **capturas de referencia** (cambian los nombres, el contenido no, salvo el rótulo de abajo). También `CLAUDE.md`.
- **Rótulo de la cabina**: el panel inferior de la cabina de BLADE pasa de `HOJA · INT-02` a `BLADE · BLD-02`, en línea con `VECTOR · VCT-01`. Es lo único que cambia en las capturas. HAMMER no lleva rótulo.
- El panel de pruebas (F9–F12) y lo que escribe en consola usan los nombres nuevos.
- Los **ids** (`ship_01`, `ship_02`, `ship_03`) **no cambian**.

### 2.2b Qué no se renombra

- **`designs/`** se queda como está. En el lienzo las naves siguen siendo Hoja y Martillo, y el archivo guarda también las opciones descartadas. Los comentarios del código que citan una nave del archivo (`Interceptor_E de designs/naves-geometria.js`, `Cab_Hoja_1`…) conservan ese identificador, para poder encontrarla.
- **Las specs ya hechas y el documento maestro** se quedan como están: son historia. Esta spec documenta la equivalencia en su tabla de nombres.
- **`docs/efectos-velocidad.md`** no se toca hasta implementarla.

### 2.3 Criterio de aceptación

- Se conduce, se choca y se ve **exactamente igual** que antes, salvo el rótulo de la cabina de BLADE. Todas las pruebas pasan sin cambiar ningún valor esperado, solo nombres y ese rótulo.
- En el selector salen **BLADE** y **HAMMER**, todavía como COMING SOON (se desbloquean en la etapa 1).
- Ninguna aparición de los nombres viejos referida a las naves queda en el código, las pruebas ni `CLAUDE.md`, salvo las citas a `designs/` de la sección 2.2b. Se comprueba con la misma búsqueda del inventario.

## 3. Etapa 1 · Selector

### 3.1 Catálogo

| Id | Nombre | Descripción | Elegible |
|---|---|---|---|
| `ship_02` | `BLADE` | `HIGH TOP SPEED · LOW TOLERANCE` | **Sí** |
| `ship_01` | `VECTOR` | `BALANCED · ALL-ROUND` | **Sí** |
| `ship_03` | `HAMMER` | `HIGH ACCELERATION · STRONG GRIP` | **Sí** |

- Mismo orden en el selector: BLADE, VECTOR, HAMMER. `Default` sigue siendo `ship_01`.
- La marca de elegible y el aspecto COMING SOON **se mantienen** en el código para naves futuras. Se siguen probando con un catálogo de prueba.
- Los datos de SHIP DATA salen de `ShipStats`, como ya hace la spec 2 (tabla 2.3). Si los números se han retocado con el panel, salen los nuevos.

### 3.2 Holograma: misma escala para las tres

- Hoy el encuadre deja a VECTOR con unos **480 px** de punta a punta de las alas. **VECTOR se queda igual.**
- Esa escala (px por metro) pasa a ser **común** a las tres naves. BLADE y HAMMER se ven con su **tamaño real** respecto a VECTOR: BLADE más larga y estrecha, HAMMER más corta y compacta.
- Cada nave se sigue centrando por el centro de su caja. Hoy ese centro se calcula una vez, con VECTOR: pasa a recalcularse al cambiar de nave. La escala y la cámara se quedan fijas.
- La profundidad con la que se atenúan las aristas lejanas también se recalcula con la esfera de cada nave, para que se atenúen igual en las tres.
- Como el holograma gira en cualquier dirección, lo que cuenta es la **esfera que envuelve cada nave**. Con las medidas de la spec 3, la de VECTOR es la más grande (unos 10 m de diámetro, frente a unos 9,4 de BLADE y 8,4 de HAMMER). Así que las otras dos caben siempre donde ya cabe VECTOR.
- Una prueba comprueba que el diámetro de la esfera de BLADE y de HAMMER no pasa del de VECTOR. Si una nave futura lo pasara, la escala común se calcularía con la más grande, pero eso **no** se implementa ahora: solo se avisa en la prueba.
- Aristas lejanas más tenues, giro, arrastre e inercia: igual que ahora, con las tres. Se comprueba que el material del holograma conserva los colores de cada casco (cian y el azul de las toberas).

### 3.3 Última nave elegida

- Al pulsar **START** en SELECT SHIP, la nave se guarda en `user://settings.json` en un campo nuevo, `lastShipDesignId`.
- Al arrancar el juego, la primera vez que se abre el menú principal, `GameSession.ShipDesignId` toma ese valor si existe y es una nave **elegible**. Si no, `Default` (VECTOR). Al volver al menú desde una carrera no se vuelve a leer. Con `Main.tscn` ejecutada sola desde el editor se corre con VECTOR.
- El selector se abre con `GameSession.ShipDesignId`, como ya hace.
- Mismas reglas del archivo de ajustes de la spec del fantasma: escritura segura, lectura tolerante y `.bak`. Un archivo antiguo sin el campo se lee bien y da VECTOR. El ajuste `ghostEnabled` no se toca.
- **RESET RECORDS no lo borra**: es un ajuste, no un récord.
- La nave del fondo del menú principal **no cambia**: sigue siendo VECTOR.

## 4. Etapa 2 · Carrera, TIME TRIAL, fantasma y resultados

### 4.1 Carrera y TIME TRIAL

- La carrera ya lee `GameSession.ShipDesignId` y todo sale de la definición (specs 1 a 3b). Esta etapa **comprueba** que con cada nave elegida desde el selector salen su conducción, su colisión, su casco con sus toberas, su cabina con su HUD y su ojo, y su cámara exterior. La llama y la estela son de `docs/efectos-velocidad.md`, que sigue sin implementarse.
- Si algo solo funcionaba al cambiar con F10 y no al entrar con la nave elegida (por ejemplo, algo que se monta antes de leer la nave), se arregla aquí y se avisa de qué era.
- **Pausa → RESTART** repite modo, circuito y nave, sin pasar por los selectores.
- El panel de pruebas (F9–F12) sigue solo en depuración. **Cambia una cosa de la spec 2**: al entrar a una carrera desde el selector, el panel pasa a la nave elegida, y F10 recorre las naves empezando por ella. Hoy el panel se queda con la nave de la primera carrera de la ejecución, y si ya se ha usado esa nave se impone a la elegida.
- Los valores retocados en el panel se conservan, RESTART no cambia la nave del panel y, una vez usado, no se guardan récords ni fantasma hasta cerrar el juego, como ya dice la spec 2.

### 4.2 Fantasma

- El archivo del fantasma **ya guarda** `shipDesignId`, y el formato **no cambia**.
- El fantasma se dibuja con el **casco de la nave del récord**, sea cual sea la nave con la que corres. Si el récord es de BLADE y corres con HAMMER, ves una BLADE fantasma.
- Al batir el récord con otra nave, el fantasma nuevo es de la nave nueva.
- Aspecto, desvanecimiento, validez y RESET: igual que en la spec del fantasma.
- Un fantasma con un id desconocido usa VECTOR y avisa en consola, como ya hace.

### 4.3 Resultados de RACE

- En la columna izquierda de la franja de resultados, debajo de la posición (`1st / 8`), sale el **nombre de la nave** con la que se ha corrido: Share Tech Mono ~16 px, azul grisáceo, espaciado ~0.18 em, a 14 px de la posición.
- Es la nave de la sesión: si se cambia con F10 durante la carrera, sigue diciendo la elegida. Es solo de depuración.
- No cambia nada más de la franja. Si con la fuente real no cabe sin mover el resto, se avisa antes de mover nada.

## 5. Etapa 3 · Récords con nave

### 5.1 Qué se guarda

- Cada récord de `records.json` (TIME TRIAL mejor vuelta; RACE tiempo total y mejor vuelta) gana un campo **opcional** `shipDesignId`: la nave con la que se hizo.
- Se escribe al guardar un récord nuevo, con `GameSession.ShipDesignId`.
- Los récords siguen siendo **uno por circuito y modo**, sea cual sea la nave. Batir un récord con otra nave lo sustituye, como ahora.
- **La versión de formato no cambia**: es un campo opcional y la lectura ya es tolerante (spec de RECORDS, 5.2).
- Un récord **sin** el campo (guardado antes de esta spec) se da por hecho con **VECTOR** (`ship_01`).
- Un récord con un id que no está en el catálogo: se carga igual y **no se muestra nave**.

### 5.2 RECORDS

- En cada fila con récord, el **nombre de la nave** va detrás de la fecha, separado por ` · `, con el mismo estilo que la fecha (Share Tech Mono ~14 px, azul grisáceo). Por ejemplo: `29 OCT 2026 · BLADE`.
- Filas sin récord: `NO RECORD`, como ahora.
- Récord sin fecha: `— · BLADE`.
- Si con la fuente real no cabe antes del separador de la derecha (x = 915), se reduce el tamaño de la fecha y la nave, con el mismo criterio que `FitFontSize` en las opciones de los menús. Si queda demasiado pequeña, **se avisa**.
- RESET RECORDS: igual que ahora.
- SELECT CIRCUIT sigue enseñando los récords sin nave.

## 6. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Ancho de VECTOR en el holograma (fija la escala común) | ~480 px | Holograma (el de siempre) |
| Tamaño del nombre de la nave en resultados | 16 px | Resultados |

No hay más parámetros nuevos.

## 7. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **`ShipCatalog`** (puro): las tres naves elegibles, con nombres y descripciones nuevos.
- **Escala del holograma** (puro): de las medidas de cada casco, la esfera envolvente y la escala común a partir del ancho de VECTOR.
- **Ajustes** (`GameSettings`): campo `lastShipDesignId` y la regla de arranque de la sección 3.3.
- **Récords**: campo opcional `shipDesignId` en el modelo, el JSON y la regla de la sección 5.1.
- **Formato de RECORDS** (puro): `29 OCT 2026 · BLADE`, `— · BLADE` y `NO RECORD`.
- **`ShipSelectScreen`**: al elegir START, guarda la nave en los ajustes. Sin reglas nuevas.
- **`TuningLab`** (puro): pasar a una nave conservando los valores retocados. **`TuningLabPanel`** lo llama al entrar a una carrera con otra nave distinta de la elegida la última vez.
- **`ResultsView`**: pinta el nombre de la nave. El nombre le llega con el resultado; no lo busca él.

### Pruebas unitarias mínimas

Nombres (etapa 0):
1. El catálogo tiene BLADE, VECTOR y HAMMER en ese orden, con ids `ship_02`, `ship_01` y `ship_03`.
2. Todas las pruebas de las specs 1 a 3b siguen pasando con los mismos valores esperados, salvo el rótulo `BLADE · BLD-02` en la captura de su cabina.

Selector (etapa 1):
3. Las tres naves son elegibles; START está activa en las tres y devuelve su id.
4. Con un catálogo de prueba con una nave no elegible, sigue funcionando el COMING SOON (START desactivada y BACK seleccionada).
5. `ShipStats` de BLADE y HAMMER da los datos de la spec 2 (o los retocados, si se cambiaron).
6. La escala común deja a VECTOR en ~480 px; BLADE y HAMMER usan los mismos px por metro.
7. El diámetro de la esfera envolvente de BLADE y HAMMER no pasa del de VECTOR.

Ajustes (etapa 1):
8. Sin archivo, o sin el campo: arranca con VECTOR.
9. Guardar `ship_02` y volver a cargar: arranca con BLADE.
10. Un id desconocido o no elegible en el archivo: arranca con VECTOR.
11. Guardar la nave no cambia `ghostEnabled`, y RESET RECORDS no borra la nave.

Fantasma y resultados (etapa 2):
12. Un fantasma grabado con `ship_03` se reproduce con el casco de HAMMER aunque la nave de la sesión sea otra.
13. El resultado de una carrera lleva la nave de la sesión.
13b. `TuningLab`: pasar a otra nave la deja como actual, conserva los valores retocados de todas y no cambia `Touched`; F10 sigue desde ella.

Récords (etapa 3):
14. Guardar un récord con BLADE y volver a cargar: el récord es de `ship_02`.
15. Un récord sin `shipDesignId` se carga como `ship_01`.
16. Un récord con un id desconocido se carga, y la vista no enseña nave.
17. Formato: `29 OCT 2026 · BLADE`, `— · HAMMER` y `NO RECORD`.
18. Batir un récord con otra nave sustituye el récord y su nave.

Las pruebas existentes siguen pasando. Las del selector que comprueban INTERCEPTOR y VANGUARD como no elegibles se adaptan a los nombres nuevos y al catálogo de prueba (prueba 4).

## 8. Prueba manual

**Etapa 0 · Nombres**
1. Todo igual que antes: conducir, chocar, cabina, cámara exterior y F10 con las tres.
2. Selector: BLADE y HAMMER con su nombre nuevo, todavía COMING SOON.
3. F9 y F11: el panel y la consola usan los nombres nuevos.
3b. F10 a BLADE, cabina: el rótulo inferior dice `BLADE · BLD-02`.

**Etapa 1 · Selector**
4. SELECT SHIP: BLADE, VECTOR y HAMMER con holograma, datos y START activa. Sin COMING SOON.
5. Holograma: VECTOR se ve igual que antes. BLADE se ve más larga y estrecha y HAMMER más compacta, a la misma escala. Girarlas en todas direcciones: ninguna se sale más de lo que se sale VECTOR y las tres giran alrededor de su centro, con las aristas lejanas igual de tenues.
6. Las descripciones: `HIGH TOP SPEED · LOW TOLERANCE` y `HIGH ACCELERATION · STRONG GRIP`.
7. Elegir HAMMER y empezar. Cerrar el juego, abrirlo y entrar al selector: sale HAMMER.
8. Borrar `settings.json` a mano: el selector sale con VECTOR y el fantasma sigue en ON.

**Etapa 2 · Carrera, TIME TRIAL, fantasma y resultados**
9. RACE con cada nave elegida desde el selector, sin tocar F10: se ve, se conduce y se choca como con F10. Cabina y cámara exterior propias.
10. Pausa → RESTART: repite con la misma nave.
11. Terminar una RACE: en los resultados sale el nombre de la nave bajo la posición.
12. TIME TRIAL con BLADE hasta hacer récord. Volver con HAMMER: el fantasma es una BLADE.
13. Batir ese récord con HAMMER: en la vuelta siguiente el fantasma es una HAMMER.
14. F10 en una TIME TRIAL: no se guarda récord ni fantasma, como antes.
14b. Usar F10, volver al selector y elegir otra nave: la carrera va con la elegida y F10 sigue desde ella. RESTART conserva la del panel. Fijarse también en los marcos de la parrilla: BLADE y HAMMER se salen, igual que VECTOR a lo ancho, y no se corrige en esta spec.

**Etapa 3 · Récords**
15. RECORDS: los récords de antes de esta spec salen con `· VECTOR`. Los nuevos, con su nave.
16. Las fechas y las naves no pisan el separador, en ventana y a pantalla completa.
17. RESET RECORDS → YES: todo vacío. La última nave elegida se mantiene.
18. Menú principal: la nave del fondo es VECTOR y la versión es 0.14.0.

## 9. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Nombres** | Inventario y renombrado (sección 2) | Pruebas 1–2 · Prueba manual 1–3 · Commit `refactor:` |
| **1 · Selector** | Catálogo, holograma a escala común y última nave en disco (sección 3) | Pruebas 3–11 · Prueba manual 4–8 · Commit |
| **2 · Carrera y fantasma** | Comprobación con cada nave, panel de pruebas, fantasma y resultados (sección 4) | Pruebas 12–13b · Prueba manual 9–14 · Commit |
| **3 · Récords** | Nave en los récords y en RECORDS (sección 5) · versión 0.14.0 | Pruebas 14–18 · Prueba manual 15–18 · Commit |

No se empieza una etapa sin validar la anterior.

## 10. Fuera de alcance

- Récords **por nave** (un récord de cada nave por circuito). Siguen siendo por circuito.
- Ver la nave del récord en el HUD de TIME TRIAL (`BEST`).
- Que la nave del fondo del menú principal sea la última elegida.
- Rivales con otras naves e IA.
- HANGAR y personalización.
- Cambios en los números técnicos (se ajustan con el panel de la spec 2).
- Piloto automático de equilibrio (spec 5).
- Animaciones de transición, sonido y mando.
