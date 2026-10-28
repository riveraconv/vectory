# VECTORY — Spec: Boost por cargas

Versión 1.1 · 2026-10-28 · Estado: **definido, listo para implementar**

Cambios respecto a la 1.0 (tras la primera prueba):
- Recarga más lenta: **35 s** por carga (antes 20 s).
- Nueva fase de **mantenimiento**: el extra de +100 km/h se mantiene **2 s** antes de empezar a perderse.
- Pérdida del exceso sobre la punta más lenta: **~5 s** para perder 100 km/h (antes ~3 s).
- El cooldown ahora **se ve en el HUD**: las esferas cargadas se atenúan mientras no se pueden usar.

Esta spec amplía el alcance de la demo con el impulso (boost). Sustituye lo que dice el documento maestro sobre recuperar energía con conducción limpia y derrapes: **en esta versión las cargas se recuperan solo por tiempo**. Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- La nave tiene **cargas de boost**: empieza con **0**, gana **1 cada 35 s**, máximo **3**.
- Gastar una carga da **+100 km/h** sobre la velocidad actual:
  - **Subida** progresiva en **0,5 s**.
  - **Mantenimiento** del extra durante **2 s**.
  - Después, si está por encima de la punta, **vuelve a ella poco a poco** (~5 s para perder 100 km/h).
- El boost **puede superar la velocidad punta**.
- **Frenar cancela** el impulso en curso (subida o mantenimiento) al instante.
- **Cooldown de 10 s** entre usos, visible en el HUD.
- Mismos valores para **todas las naves**.
- En el HUD: **3 esferas** encima de la velocidad. Naranja brillante = carga disponible, naranja atenuado = carga en cooldown, azul = vacía.

---

## 2. Cargas

| Regla | Valor |
|---|---|
| Cargas al empezar | 0 |
| Cargas máximas | 3 |
| Tiempo para ganar una carga | 35 s |

- El contador de recarga corre siempre que haya **menos de 3 cargas**.
- Al llegar a 3, el contador se para y se pone a 0.
- Al gastar una carga estando en 3, el contador arranca desde 0.
- Si hay menos de 3 y se gasta una, el contador **no se reinicia**: sigue con el progreso que llevaba.
- El contador sigue corriendo durante el cooldown y durante el impulso.
- De momento la carrera "empieza" al cargar la escena. Cuando haya cuenta atrás, el contador debe empezar al terminar la cuenta atrás (no implementar ahora).

## 3. Activación

- **Tecla: Espacio** (en la demo no tenía función). Acción de input: `boost`.
- Se activa al **pulsar** (just pressed), no al mantener.
- Solo se activa si:
  - hay **al menos 1 carga**, y
  - **no hay cooldown activo**.
- Si no se cumple, no pasa nada (sin sonido ni aviso aparte del estado de las esferas).
- Al activarse: se resta 1 carga **en ese momento** y arranca el cooldown de 10 s.

## 4. Impulso

El impulso tiene dos fases:

### 4.1 Subida (0,5 s)

- Objetivo: **+100 km/h** sobre la velocidad que lleva la nave al activarlo.
- Se suma de forma **progresiva y lineal durante 0,5 s** (unos 200 km/h por segundo extra).

### 4.2 Mantenimiento (2 s)

- Al terminar la subida, la nave **mantiene la velocidad alcanzada durante 2 s**: no la recorta el límite de punta, ni la desaceleración natural al soltar W, ni la pérdida de exceso.
- Durante el mantenimiento **W no añade velocidad** por encima de lo alcanzado.
- Si la velocidad alcanzada está por debajo de la punta (boost a baja velocidad), durante el mantenimiento W sí puede seguir acelerando normalmente hasta la punta; lo que no hace es perder velocidad por soltar W.
- Al acabar los 2 s, el impulso **termina** y entra en juego la vuelta a la punta (sección 5) o la conducción normal.

### Generales

- Durante el impulso la velocidad **puede superar la velocidad punta**.
- Funciona a cualquier velocidad, también parado.
- No se puede encadenar otro boost durante el impulso porque el cooldown (10 s) lo impide.

### Cancelación por frenada

- Si se pulsa **S (frenar)** durante la **subida o el mantenimiento**, el impulso **se cancela al instante**.
- La nave conserva la velocidad que lleve en ese momento; a partir de ahí actúa el frenado normal (y, si está por encima de la punta, también la pérdida de exceso).
- La carga gastada **no se devuelve**.
- El cooldown **sigue corriendo** igualmente.
- Frenar después de que el impulso haya terminado no tiene nada especial: es frenado normal.

## 5. Vuelta a la velocidad punta

- Si, al terminar el impulso, la velocidad está por encima de la punta, la nave **pierde el exceso de forma gradual**, no de golpe.
- Valor inicial: perder el exceso a **20 km/h por segundo** (unos 5 s para perder 100 km/h).
- Mientras esté por encima de la punta:
  - **W no añade velocidad** (no se acelera por encima de la punta con el motor).
  - **S frena** normalmente (más fuerte que la pérdida de exceso).
  - Soltar W aplica la desaceleración natural de siempre si es mayor que la pérdida de exceso.
- Al bajar a la punta o por debajo, todo vuelve al comportamiento normal de conducción.

## 6. HUD

Referencia visual: artefacto "VECTORY HUD — Velocidad y boost".

- **3 esferas pequeñas** en fila horizontal, **justo encima del indicador de velocidad**, alineadas a la derecha del bloque de velocidad.
- Tamaño de cada esfera: **16 px** de diámetro, separación de **10 px**.
- Tres estados por esfera:

| Estado | Cuándo | Aspecto |
|---|---|---|
| **Disponible** | Hay carga y no hay cooldown | Naranja **#FF9D32**, relleno pleno, con brillo/halo naranja suave |
| **En cooldown** | Hay carga pero el cooldown está activo | Naranja **#FF9D32** atenuado (relleno al ~30 % de opacidad, borde al ~55 %), **sin brillo** |
| **Vacía** | No hay carga en esa posición | Azul **#245BFF** apagado (relleno azul con transparencia y borde azul, sin brillo) |

- Las esferas se encienden **de izquierda a derecha** (con 1 carga, la de la izquierda).
- Al gastar una carga se apaga **la última encendida** (la de más a la derecha) y las cargas que queden pasan a **en cooldown** hasta que termine.
- Una carga que se gane durante el cooldown aparece directamente **en cooldown**.
- Al terminar el cooldown, todas las cargas pasan a **disponible** (cambio inmediato, sin animación por ahora).
- Con 3 cargas sin cooldown: las 3 naranjas brillantes. Con 0: las 3 azules.
- Los colores salen de la clase de paleta que ya existe (`Palette`), no hardcodeados en el HUD. La atenuación se hace con opacidad sobre el naranja de la paleta, sin crear un color nuevo.
- Fuera de alcance de esta spec:
  - Mover la velocidad a la esquina inferior derecha y la barra de velocidad (va en la spec del HUD). Si la velocidad sigue abajo en el centro, las esferas van encima de ella, donde esté.
  - Inclinación del HUD con la nave en cabina.

## 7. Parámetros en el Inspector

Todos expuestos y con estos valores por defecto:

| Parámetro | Valor | Unidad |
|---|---|---|
| Cargas máximas | 3 | cargas |
| Cargas iniciales | 0 | cargas |
| Tiempo de recarga por carga | 35 | s |
| Impulso por carga | 100 | km/h |
| Duración de la subida | 0,5 | s |
| Duración del mantenimiento | 2 | s |
| Cooldown entre usos | 10 | s |
| Pérdida de exceso sobre la punta | 20 | km/h por s |

- Mismos valores para todas las naves. No hacer valores por nave.
- Usar la misma conversión km/h ↔ unidades de Godot que ya usa el HUD de velocidad.

## 8. Arquitectura y pruebas

- La lógica de cargas, cooldown e impulso debe ir en una **clase propia sin dependencias de nodos** de Godot (estado + método de actualización con `delta` + activar + cancelar), para poder probarla con pruebas unitarias.
- Debe exponer, como mínimo: número de cargas, si el cooldown está activo, si se puede activar, y la fase actual del impulso (ninguna / subida / mantenimiento).
- El nodo/controlador de la nave la usa; el HUD solo **lee** el número de cargas y si el cooldown está activo (no conoce la lógica).
- Respetar la estructura de carpetas y namespaces que ya hay en el proyecto.

### Pruebas unitarias mínimas

1. Empieza con 0 cargas.
2. A los 35 s tiene 1 carga; a los 105 s tiene 3; a los 140 s sigue en 3.
3. Activar sin cargas no hace nada.
4. Activar con carga resta 1 y arranca el cooldown.
5. Activar durante el cooldown no hace nada; a los 10 s vuelve a poder.
6. La subida suma 100 km/h repartidos en 0,5 s (comprobar ~50 km/h a los 0,25 s).
7. Tras la subida, la fase de mantenimiento dura 2 s y después el impulso termina.
8. Cancelar durante la subida deja el extra ganado hasta ese momento y no devuelve la carga.
9. Cancelar durante el mantenimiento termina el impulso al instante y no devuelve la carga.
10. Con 3 cargas, gastar una arranca el contador de recarga desde 0.
11. El cooldown está activo justo tras activar y deja de estarlo a los 10 s.

## 9. Prueba manual

1. Arrancar la escena: las 3 esferas en azul.
2. Esperar 35 s: se enciende la primera esfera (izquierda), naranja brillante.
3. Pulsar Espacio: la esfera se apaga, la velocidad sube unos 100 km/h en medio segundo y se mantiene unos 2 s.
4. Con 2 o más cargas, gastar una: las que quedan se ven **atenuadas** durante 10 s; pulsar Espacio en ese tiempo no hace nada. Al terminar, vuelven a brillar y ya se puede usar.
5. Con la velocidad punta alcanzada, usar un boost: se pasa de la punta, se mantiene ~2 s y en unos 5 s vuelve a la punta.
6. Usar un boost y frenar durante la subida o el mantenimiento: el impulso se corta y la carga no vuelve.
7. Esperar sin gastar hasta tener 3 cargas: las 3 naranjas brillantes.

## 10. Fuera de alcance

- Recarga por conducción limpia, trazadas o derrapes.
- Pickups o recargas en pista.
- Animaciones en las esferas (parpadeo, transición al terminar el cooldown).
- Efectos visuales o de cámara del boost (partículas, FOV, vibración).
- Sonido.
- Boost para rivales/IA.
- Desactivación del boost por daños.
