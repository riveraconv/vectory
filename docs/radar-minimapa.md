# VECTORY — Spec: Radar de proximidad y minimapa

Versión 1.0 · 2026-11-06 · Estado: **propuesta, pendiente de validar**

Añade dos piezas al HUD de carrera:

- **Radar de proximidad** de **barrido**: un círculo con tu nave en el centro y un haz que gira. Avisa de las naves que tienes cerca en los 360° (delante, a los lados y detrás), hasta **120 m**.
- **Minimapa** de **ventana**: el trozo de circuito que te rodea, **girando** para que siempre mires hacia arriba, con los rivales como puntos.

Se hacen **antes que las cabinas 3D**, para que la cabina de VECTOR ya salga de Blender con su sitio para las dos pantallas (spec `cabinas-3d-1-vector`).

Diseño de referencia:
- **Persecución**: página **«VECTORY · Radar y minimapa · Versiones»**, botones **ELEGIDO** (sitio), **C · BARRIDO** (radar), **120 m** (alcance), **S** (tamaño) y **A · VENTANA** (minimapa). Es lo que sale al abrirla. El resto de variantes quedan descartadas.
- **Cabina**: `design/cabina-3d-vector-radar.html`, colocación **1 · JUNTO A PANTALLAS**. Es de la spec de la cabina 3D, no de esta.
- La lógica de la página (`update`, `local`, `drawRadar`, `drawMap`) es la referencia de las fórmulas. Si algo no cuadra entre esta spec y la página, **manda la página** y se avisa.

Rama: `feature/radar-minimap`, que sale de `develop` con `feature/ship-balance` ya mergeada. Commits `feat:`.

Versión del juego: **0.14.2 → 0.14.3** (se sube en el commit de la última etapa; si la actual es otra, se sube el tercer número de la que haya).

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 1 · Naves de prueba y radar**: todavía no hay rivales. Para probar se añaden **naves de prueba** que se activan desde el panel de depuración. El radar sale en el HUD de pantalla, **abajo a la izquierda**.
- **Etapa 2 · Minimapa**: sale en el HUD de pantalla, **justo debajo de la posición**, arriba a la izquierda.
- Se ven en **todas las cámaras**: persecución y cabina de las tres naves. Cuando llegue la cabina 3D de VECTOR, en su cabina pasarán a sus pantallas (lo hace esa spec, no esta).
- **El fantasma no sale** ni en el radar ni en el minimapa.
- Lo que **no cambia**: posición, vuelta y tiempos (esquinas de arriba), velocidad y boost (abajo a la derecha), cabinas, cámaras y circuitos.

## 2. Antes de empezar: inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo se monta hoy el `Hud`: nodos, `CanvasLayer`, escala con el tamaño de la pantalla (espacio de 1280 × 720 o el que use) y cómo se inclina en cabina con el giro.
2. Dónde se dibuja hoy la **posición** (`1 / 7`) y qué alto ocupa, para poner el minimapa justo debajo.
3. Si hay ya una forma de saber **qué naves hay en carrera** aparte de la tuya (lista, grupo, nada). El fantasma no cuenta.
4. Qué teclas del panel de depuración están libres (hoy se usan F8 a F12).
5. De dónde se saca la **línea central del circuito en planta** (los puntos del eje que ya usan la colisión, la recolocación y el selector).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Naves en carrera

### 3.1 Quién sale

- El radar y el minimapa enseñan **las demás naves de la carrera**: hoy, las **naves de prueba** (sección 3.2); mañana, los **rivales con IA**.
- Se leen de **una sola lista** de naves en carrera (posición y orientación de cada una), que no sabe nada del HUD. Cuando lleguen los rivales, se añadirán a esa misma lista y el radar y el minimapa funcionarán sin tocarlos.
- **El fantasma no entra** en esa lista. Tu nave, tampoco.

### 3.2 Naves de prueba (solo depuración)

- Una tecla libre del panel de depuración (propuesta: **F7**) va cambiando entre **0, 2, 4 y 6** naves de prueba. El número sale en el panel (F9). Solo en compilaciones de depuración, como el resto del panel.
- Se mueven **pegadas a tu nave** con el mismo patrón que la página de referencia, sin física:
  - Distancia sobre la pista respecto a ti: `s = tu s + base + A · sen(w · t + fase)`.
  - Desplazamiento lateral: `d = a · sen(w2 · t + fase2)`, limitado a la pista.
  - Si quedarían encima de ti (menos de 7 m por delante o por detrás y menos de 3,8 m de lado), se apartan a 3,8 m de tu lado.
  - Las dos últimas van **lejos** (una delante y otra detrás, a más de 900 m) y avanzan un poco más lentas o más rápidas que tú: solo se ven en el minimapa cuando se acercan.

| Nave | base (m) | A (m) | w | fase | a (m) | w2 | fase2 |
|---|---|---|---|---|---|---|---|
| 1 | 35 | 45 | 0,23 | 0 | 6 | 0,31 | 1 |
| 2 | −25 | 35 | 0,19 | 2 | 7 | 0,27 | 4 |
| 3 | 5 | 20 | 0,37 | 4 | −6 | 0,40 | 2 |
| 4 | 110 | 70 | 0,11 | 1 | 5 | 0,22 | 0 |
| 5 (lejos) | +920 | — | — | — | 4 | 0,20 | 1 |
| 6 (lejos) | −1550 | — | — | — | −4 | 0,25 | 3 |

  Las lejanas se alejan o se acercan a **−1 m/s** y **+1,5 m/s** respecto a ti.
- Se colocan con el **marco de la pista** (`s`, `d`, `h` = altura de vuelo de las naves), así que siguen pendientes y peraltes.
- Aspecto: el **casco wireframe de VECTOR** en **magenta** (`Palette.Magenta`, el color de los rivales del documento maestro).
- **Sin colisión** con nada, no cuentan para la posición, ni para vueltas, récords o fantasma. No afectan a la carrera.
- Con naves de prueba activas, el HUD de posición **no cambia** (no son participantes).

## 4. Radar

### 4.1 Qué detecta

Para cada nave de la lista se calcula su posición **en los ejes de tu nave**:
- `x` = hacia tu **derecha**, `y` = hacia tu **frente** (en metros), usando el lado y el frente de tu nave.
- Se ignora la altura para dibujar. Una nave con más de **15 m** de diferencia de altura respecto a tu plano **no sale** (otro nivel del circuito, si algún día hay cruces).
- Distancia = √(x² + y²).
- **Alcance**: **120 m**. Más lejos, no sale.
- **Nave pegada**: a menos de **12 m**.

### 4.2 Barrido

- El haz gira en **sentido horario** desde el frente, una vuelta cada **1,6 s**.
- Cuando el haz **pasa** por el ángulo de una nave dentro del alcance, se apunta un **eco** con su posición **en ese momento** y si estaba pegada. El ángulo de la nave es `atan2(x, y)`, de 0 a 2π, en el mismo sentido que el haz.
- Cada eco se **apaga** de forma lineal en **1,6 s** (opacidad 1 → 0) y luego se borra. Entre una pasada y la siguiente **no se mueve**.
- El **aviso de nave pegada** no espera al haz: se calcula **cada frame**.
- La pausa congela el haz, los ecos y su apagado.

### 4.3 Dibujo (HUD de pantalla)

En el espacio de **1280 × 720** del HUD:

- **Sitio**: abajo a la izquierda. Centro en **(100, 620)**, **diámetro 120 px** (radio 60).
- **Fondo**: círculo `#05070D` al **72 %**.
- **Borde**: círculo cian, 2 px, al 85 %, con halo de 6 px.
- **Anillos** a 1/3 y 2/3 del radio, azul, 1 px, al 55 %. **Cruz** (horizontal y vertical) azul al 35 %.
- **Haz**: 28 líneas del centro al borde, desde el ángulo del haz hacia atrás cada **0,035 rad**, cian, 2 px, con opacidad de 0,4 (la primera) bajando a 0. Recortado al círculo.
- **Ecos**: puntos de radio **3,4 px** en **magenta**, o **4,2 px** en **naranja** si la nave estaba pegada. Halo de 8 px. Opacidad del apagado. Posición: centro + (`x`/120 · radio, −`y`/120 · radio).
- **Aviso de nave pegada**: arco **naranja** de 4 px por fuera del borde (radio + 4 px), centrado en la dirección de la nave en pantalla (`atan2(−y, x)`) y de **±0,42 rad**. Opacidad `0,4 + 0,6 · (1 − distancia / 12)`. Halo de 10 px. Uno por cada nave pegada.
- **Tu nave**: flecha cian en el centro, apuntando arriba, de **6 px** (`max(6, radio × 0,075)`), con halo.
- **Rótulo** `120 M` en azul grisáceo, 11 px, al 80 %, abajo a la derecha del círculo.

## 5. Minimapa

### 5.1 Qué enseña

- La **línea central del circuito en planta** (sin altura), girada para que **tu frente quede arriba**, con **tu nave en el centro**.
- **Radio de la ventana**: **450 m** de circuito alrededor de ti.
- La **meta**: una raya perpendicular al eje, de ±3 semianchos de pista.
- **Las demás naves de la lista** como puntos, a cualquier distancia (solo se ven las que caen dentro de la ventana).
- La rotación usa tu **frente en planta**. Con eso, en una curva el mapa gira a la vez que tú.

### 5.2 Dibujo (HUD de pantalla)

- **Sitio**: arriba a la izquierda, **justo debajo de la posición**. Diámetro **156 px** (radio 78), con el borde izquierdo a **40 px** y el de arriba a **78 px**: centro en **(118, 156)**. Si la posición del inventario ocupa más alto, se baja lo justo para que no se toquen y se avisa.
- **Fondo y borde**: como el radar.
- **Trazado**: el mismo camino dos veces, con uniones redondeadas: una **banda azul** de 7 px al 55 % y encima una **línea cian** de 1,6 px al 90 % con halo de 5 px. Recortado al círculo.
- **Meta**: naranja, 2,5 px.
- **Naves**: puntos **magenta** de radio 3,2 px con halo.
- **Tu nave**: flecha cian en el centro, de `max(5,5, radio × 0,06)` px.
- **Marca de «arriba»**: triangulito cian al 80 % sobre el borde de arriba (de 10 px de ancho y 7 de alto), para que se lea que el mapa gira contigo.

### 5.3 Rendimiento

- La línea central en planta se prepara **una vez** al cargar el circuito (con unos **400 puntos** repartidos por distancia basta) y se reutiliza.
- Cada frame solo se transforman esos puntos y se dibujan. Si se nota en los FPS, se avisa y se propone algo más ligero (por ejemplo, dibujar solo los puntos cercanos).

## 6. En cada vista

| Vista | Radar | Minimapa |
|---|---|---|
| Persecución | HUD de pantalla, abajo a la izquierda | HUD de pantalla, bajo la posición |
| Cabina de cualquier nave (cabinas 2D actuales) | Igual, y se inclina con el giro como el resto del HUD de cabina | Igual |
| Pausa | Congelado, visible debajo del menú | Congelado |
| Resultados | No sale | No sale |
| Previa y cuenta atrás | Sí | Sí |
| TIME TRIAL | Sí (sale vacío, salvo naves de prueba) | Sí |

- La spec de la cabina 3D de VECTOR pasará después el radar y el minimapa a dos **pantallas propias** en su cabina, con los colores de su estilo. Para eso, el **dibujo** del radar y del minimapa tiene que poder usar **otra paleta y otro tamaño** sin cambiar la lógica (sección 7).

## 7. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Lista de naves en carrera** (puro): naves que no son la tuya, con posición y orientación. La rellenan las naves de prueba (y luego los rivales). El fantasma no se añade nunca.
- **Naves de prueba** (Godot, depuración): el patrón de la sección 3.2 (su cálculo de `s` y `d`, puro y probado) y su malla.
- **Radar** (puro):
  - Proyección a tus ejes (sección 4.1).
  - Estado del barrido: ángulo, ecos y su apagado, con `Update(delta)`.
  - Avisos de nave pegada con su ángulo y opacidad.
  - Da **primitivas** (círculos, líneas, arcos, flecha, texto) en un espacio de tamaño dado, con una **paleta** dada.
- **Minimapa** (puro): de la línea central y tu posición y frente a puntos en pantalla, más meta y naves, en un círculo de tamaño dado y con una paleta dada.
- **Paleta del radar y el minimapa**: un objeto con los colores (fondo, borde, anillos, haz, rival, pegado, tu nave, trazado, banda, meta, rótulo). La del HUD de pantalla sale de `Palette`. Nada de colores escritos a mano.
- **Widgets del HUD** (Godot): dibujan las primitivas. **Sin reglas dentro.**
- **Teclas de depuración**: la de las naves de prueba, junto a las demás del panel.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Alcance del radar | 120 m | Radar |
| Distancia de nave pegada | 12 m | Radar |
| Diferencia de altura máxima | 15 m | Radar |
| Vuelta del barrido | 1,6 s | Radar |
| Apagado de los ecos | 1,6 s | Radar |
| Diámetro del radar | 120 px | HUD |
| Radio de la ventana del minimapa | 450 m | Minimapa |
| Diámetro del minimapa | 156 px | HUD |

Grosores, opacidades y tamaños de puntos, como **constantes con nombre**.

## 9. Pruebas

### Pruebas unitarias mínimas

1. **Proyección**: una nave 50 m delante sale en (0, 50); 20 m a la derecha, en (20, 0); detrás, con `y` negativa. Girando tu nave 90°, los ejes giran con ella.
2. **Alcance y altura**: a 119 m sale, a 121 m no; con 16 m de diferencia de altura no sale.
3. **Barrido**:
   - Tras 0,4 s el haz está en π/2 (a tu derecha).
   - Una nave delante (ángulo 0) se apunta al cruzar el haz por 0 (al dar la vuelta) y no antes.
   - Un eco se apaga a 0,8 s al 50 % y desaparece a 1,6 s.
   - El eco no se mueve aunque la nave se mueva.
4. **Nave pegada**: a 11 m hay aviso, a 13 m no; el ángulo del arco apunta a la nave; la opacidad sigue la fórmula.
5. **Minimapa**: con tu frente hacia el norte del mundo, un punto 100 m al norte sale arriba; girando tu nave 90°, ese punto pasa a un lado. Tu nave siempre en el centro. La escala es radio / 450 m.
6. **Lista de naves**: el fantasma no está; tu nave no está; las naves de prueba sí.
7. **Naves de prueba**: con la tabla de la sección 3.2, `s` y `d` dan los valores esperados en t = 0 y t = 10 s, y se apartan si quedarían encima de ti.
8. **Paleta**: el mismo radar con dos paletas da las mismas primitivas con colores distintos.

Las pruebas existentes siguen pasando.

## 10. Prueba manual

**Etapa 1 · Naves de prueba y radar**
1. RACE, cámara de persecución, F7 hasta 4 naves: aparecen naves magenta alrededor que te adelantan y se quedan atrás. Con 6, las dos lejanas no se ven.
2. Radar abajo a la izquierda: el haz gira y las naves aparecen al pasar, en su sitio (delante arriba, detrás abajo, a los lados).
3. Una nave pasa a tu lado: arco naranja en el borde de ese lado, que se mueve con ella. Si va pegada, su eco sale en naranja.
4. Cabina con las tres naves (F10): el radar se ve igual y se inclina con el HUD al girar.
5. Pausa: el haz y los ecos se quedan quietos.
6. TIME TRIAL con fantasma y sin naves de prueba: el radar sale vacío. El fantasma no sale.
7. Compilación release: F7 no hace nada y no hay naves de prueba.

**Etapa 2 · Minimapa**
8. Minimapa justo debajo de la posición, sin tocarla.
9. En una recta, el trazado va hacia arriba; en una curva, el mapa gira contigo y siempre miras arriba.
10. Las naves de prueba salen como puntos magenta en su sitio; las lejanas aparecen al entrar en los 450 m.
11. Al acercarte a la meta, la raya naranja baja por el mapa hasta tu nave.
12. Circuitos con pendientes y peraltes (TEST HILLS): el trazado se ve bien en planta.
13. Rendimiento: los FPS no bajan de forma notable (apuntar antes y después).
14. Menú principal: la versión es 0.14.3.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Naves de prueba y radar** | Inventario y secciones 3, 4, 6 (solo el radar) y 7 | Pruebas 1–4, 6–8 · Prueba manual 1–7 · Commit |
| **2 · Minimapa** | Sección 5 y lo del minimapa de la 6 · versión 0.14.3 | Prueba 5 · Prueba manual 8–14 · Commit |

No se empieza una etapa sin validar la anterior.

## 12. Fuera de alcance

- Rivales con IA (la lista queda lista para ellos).
- Radar y minimapa en las pantallas de la cabina 3D de VECTOR (spec `cabinas-3d-1-vector`).
- Que el fantasma salga en el radar o en el minimapa.
- Mapa del circuito entero, zoom, o elegir la vista en SETTINGS.
- Avisos de sonido.
- Mostrar túneles, checkpoints o curvas en el minimapa.
