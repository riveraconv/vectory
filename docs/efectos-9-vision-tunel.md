# VECTORY — Spec: Efectos 9 · Visión túnel

Versión 1.3 · 2026-11-24 · Estado: **hecha (etapas 1 y 2)**

**v1.3**: las líneas de velocidad (3D) no gustaron al probarlas en el juego y se sustituyen por un **cono de vapor** (sección 5), elegido en la maqueta `designs/efectos-cono-sonico.html` («A · VAPOR», a la mitad de opacidad, sin el anillo del estallido). Lo que dice este documento de líneas de velocidad fuera de la sección 5 vale para el cono, salvo el factor de cámara: el cono tiene una opacidad propia en cada cámara.

Última spec de la **segunda tanda de efectos de nave**. Hace la **visión túnel** y las **líneas de velocidad** de `efectos-velocidad` (secciones 1, 2 y 3), que **nunca se implementaron**, con tres cambios sobre lo que decía aquella spec:

1. La **aberración** (desdoble de color en los bordes) se ve **siempre**: leve con la velocidad y más fuerte con boost. En `efectos-velocidad` solo salía con boost.
2. En la **cámara de persecución**, la visión túnel y las líneas de velocidad van al **50 %**. En cabina, al 100 %.
3. En cabina, la visión túnel es **solo de fuera**: se ve por el parabrisas, y el interior de la cabina queda nítido.

Es **solo visual**: no cambia la conducción, el boost, el daño ni la física.

Maqueta de referencia: **`designs/efectos-vision-tunel.html`** («VECTORY · Visión túnel · Maqueta»). Se decidió con:
- **CUÁNDO ENTRA · C · SEGÚN LA VELOCIDAD**
- **VIÑETA · OSCURA + ÁMBAR**
- **DESENFOQUE · SÍ**
- **LÍNEAS · 3D** (sustituidas en la v1.3)
- **ABERRACIÓN · SIEMPRE**
- **EN PERSECUCIÓN · SUAVE (50 %)**
- **MARCO DE CABINA · NÍTIDO** (v1.2; en la v1.1 era SE DESENFOCA)

Descartado: entrar solo con boost (A) o cerca de la punta (B), las líneas en pantalla y el marco de cabina desenfocado. En el juego el marco nítido no necesita la máscara de la maqueta: basta con pintar el túnel por debajo de la cabina 3D.

En la maqueta, «COMO HOY» quiere decir **lo que definía `efectos-velocidad`**, no lo que hay en el juego. La pista, la nave, la cabina, el piloto automático y el bloom de la maqueta son aproximados. Las constantes del shader de la maqueta también: **mandan los valores de esta spec** (que son los de `efectos-velocidad` sección 9, salvo lo que cambia aquí). Si algo no cuadra, se avisa.

Se apoya en lo que ya existe:
- Las dos cámaras (cabina y persecución), la cabina 3D (`Cockpit3DView`, pintada encima del mundo) y cómo se sabe cuál está activa (`Player.View`, `ViewChanged`).
- El boost suavizado de la spec `camara` (`BoostCameraEffect`: sube en 0,25 s y baja en 0,5 s).
- La **punta base** de la nave (la misma que usa `efectos-6-toberas` para su `th`).
- Los ajustes **TUNNEL VISION**, **SPEED LINES** y **PARTICLES** de `settings`, y su vista previa.
- La pausa y la recolocación.

Cambios respecto a specs anteriores:
- `efectos-velocidad` 1, 2 y 3 se hacen aquí, con los cambios de las secciones 3 y 4. El resto de `efectos-velocidad` (FOV +14°, partículas y chispas de las toberas) **sigue sin hacerse**. Las toberas y las estelas ya las sustituyeron `efectos-6-toberas` y `efectos-7-estelas`.
- `efectos-velocidad` 2: el desdoble deja de ser «solo con boost».
- `efectos-velocidad` 2 y 3: la intensidad del túnel y la de las líneas se multiplican por un **factor de cámara**.
- `settings`: TUNNEL VISION sale de COMING SOON, y la vista previa de EFFECTS oscurece los bordes con él (settings 3.4, que estaba pendiente).

Rama: `feature/fx-tunnel-vision` (ya creada). Versión: **0.29.0**. Se sube en el commit de la etapa 1.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

---

## 1. Factores

- **Factor de velocidad** `f` = (velocidad − 25 % de la punta) / (punta − 25 % de la punta), limitado a 0–1. Es 0 por debajo del 25 % de la punta y 1 a la punta.
- **Boost** `b`: 0–1, el mismo que ya usa la cámara (sube en 0,25 s y baja en 0,5 s). No se duplica.
- "Punta" es la **punta base** de la nave, sin daño ni pendientes, para que el aspecto no cambie cuando se pierde punta.

## 2. Visión túnel

Efecto de pantalla completa, **después del glow**.

- **Intensidad** = mín(1,2; 0,85·`f` + 0,45·`b`), **suavizada** con una constante de **0,12 s**, como en la maqueta, para que no salte cuando la velocidad cambia de golpe (un choque, por ejemplo).
- **Desenfoque radial**: los píxeles se estiran desde el centro hacia los bordes. Fuerza 0,11 × la máscara de bordes. La máscara va de 0 al 22 % de la distancia del centro a la esquina a 1 en el 95 %, multiplicada por la intensidad. **16 muestras** (6 con PARTICLES en LOW).
- **Viñeta**: oscurecimiento 0,62 desde el 42 % de esa distancia, × mín(1, intensidad).
- **Halo ámbar** (`Palette.Orange`) en las esquinas: 0,10 × `b` × mín(1, intensidad / 0,45), como en la maqueta.
- **Desdoble**: sección 3.
- **Dónde va**: en una capa de pantalla **por debajo de la cabina 3D** y del HUD. Solo toca el mundo: en cabina se ve por el parabrisas, y el marco, las pantallas, el cristal y el HUD quedan nítidos. En persecución toca toda la imagen menos el HUD.
- Un **solo shader, siempre puesto** en carrera: con intensidad 0 no hace nada, pero no se quita ni se cambia (regla de no cambiar de shader en plena carrera).

## 3. Aberración siempre

- **Desdoble** (rojo hacia fuera y azul hacia dentro) = 0,010 × (0,4 + 0,6·`b`) × la máscara de bordes del túnel (la misma que el desenfoque).
- Sin boost, el 40 % del máximo. Con boost completo (`b` = 1), el máximo.
- Como va con la intensidad del túnel, despacio no se ve, y crece con la velocidad.
- Parámetros en el Inspector: desdoble máximo (0,010) y parte sin boost (0,4).

## 4. Factor de cámara

- **Cabina**: 1,0. **Persecución**: **0,5**.
- Intensidad final del túnel = intensidad × factor de cámara × TUNNEL VISION (SETTINGS).
  - Eso afecta a todo lo que sale de la intensidad: desenfoque, viñeta, halo y desdoble.
- El cono de vapor no usa este factor (sección 5).
- Al **cambiar de cámara**, el factor cambia **al momento**, sin suavizado. Es un corte de cámara, así que el salto no se nota.
- Parámetros en el Inspector: factor de cabina (1,0) y factor de persecución (0,5).
- **Repeticiones o cámaras futuras**: usan el factor de persecución mientras no se diga otra cosa.

## 5. Cono de vapor

Como el de un avión al romper la barrera del sonido: sutil, transparente y sin trazos.

- **Cantidad**: 0 hasta el **75 % de la punta base**, 1 a la punta, con curva suave (smoothstep). Si ya se ve, el boost suma 0,3 × `b`. Máximo 1,3. Suavizada con 0,18 s.
- **Opacidad** con cantidad 1: **0,03 en cabina** y **0,24 en persecución** (ajustadas con capturas del juego; mandan sobre la maqueta, donde son 0,10 y 0,17).
- **Persecución**: campana abierta alrededor de la nave, de 0,5 a 4 m de radio (aplastada al 72 %; arriba, el radio baja de forma gradual hasta el 80 %, más pegada a la nave) y de 2,4 m por delante a 3,8 m por detrás del origen, a la escala de VECTOR; en las otras naves se escala con el semiancho (mínimo 0,75) y el largo del morro. Más visible en el contorno; entra entre el 10 y el 45 % del largo y se deshace del 65 % a la boca y al llegar al suelo.
- **Cabina**: cono con la punta a 60 m y el ojo dentro. Solo cerca de los bordes del parabrisas; el centro, limpio.
- **Textura**: ruido suave que corre hacia atrás a 0,5 + v (m/s) × 0,022 + 1,2 × `b`.
- **Color**: blanco-cian que pasa a ámbar claro con boost (60 %). Mezcla aditiva. Los colores, en `Palette`.
- **Sin estallido**: nada de anillo ni onda al llegar a la punta.
- Mallas fijas y siempre puestas; por frame solo cambian parámetros del shader.
- **SPEED LINES** en OFF quita el cono y las líneas de aire del rebufo.

### 5.1 Rivales (decisión del usuario)

- La misma campana que el jugador en persecución, con su punta base, su boost y su escala. Igual en las dos cámaras.
- **Opacidad 0,20** con cantidad 1 (menos que la del jugador; con 0,12 no se distinguía), con **fundido por distancia a la cámara**: invisible a menos de 6 m y completa desde 18 m, para no tapar un adelantamiento.
- Boost suavizado: 0,25 s al entrar y 0,5 s al salir. Recolocado, a 0 al momento. SPEED LINES en OFF lo quita. El fantasma no lo lleva.

## 6. Lo que no cambia

- **FOV**: el que ya hay (spec `camara` y SPEED FOV). El +14° de `efectos-velocidad` no entra.
- **Vibración** de las cámaras y de la cabina.
- **Líneas de aire del rebufo**: siguen como están, aparte de las de velocidad.

## 7. Interacción con otros sistemas

- **Pausa**: todo congelado. La intensidad no cambia y las líneas se quedan quietas. Si se cambia de cámara en pausa, el factor nuevo se aplica.
- **Recolocación**: intensidad a 0 al momento y líneas borradas.
- **SETTINGS**:
  - TUNNEL VISION sale de COMING SOON: se elige, se guarda y se aplica al momento, también desde la pausa.
  - Vista previa de EFFECTS (2D): con TUNNEL VISION, oscurece los bordes (solo viñeta, sin desenfoque). Las líneas de la vista previa no cambian.
  - PARTICLES en LOW: 6 muestras en el túnel.
- **Menús, selectores, resultados**: sin visión túnel ni líneas.
- **Parrilla**: parada, túnel a 0 y sin líneas.
- **Carrera terminada**: los efectos bajan solos a medida que la nave frena.
- **Daño**: sin cambios, porque se usa la punta base.

## 8. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Factores y curvas** (puro, `System.Numerics`): velocidad, punta base, `b`, cámara activa y ajustes → `f`, intensidad suavizada con `delta`, desdoble, halo, visibilidad y largo de las líneas, con el factor de cámara y los ajustes de SETTINGS aplicados.
- **Ajustes de efectos** (datos): los parámetros de las secciones 2–5, con los valores de esta spec por defecto.
- **Nodos de Godot**: el post-proceso (capa de pantalla) y las líneas de velocidad (malla en la cámara) solo leen los valores ya calculados y pintan. Sin reglas dentro.

## 9. Parámetros en el Inspector

| Parámetro | Valor |
|---|---|
| Inicio del factor de velocidad | 25 % de la punta |
| Túnel: peso de la velocidad / del boost / máximo | 0,85 / 0,45 / 1,2 |
| Túnel: suavizado | 0,12 s |
| Túnel: desenfoque / oscurecimiento / halo | 0,11 / 0,62 / 0,10 |
| Túnel: máscara de bordes | 22 % → 95 % |
| Túnel: viñeta desde | 42 % |
| Túnel: muestras / muestras en LOW | 16 / 6 |
| Desdoble: máximo / parte sin boost | 0,010 / 0,4 |
| Factor de cámara: cabina / persecución | 1,0 / 0,5 |
| Cono: entra / completo | 75 % / 100 % de la punta |
| Cono: peso del boost / máximo / suavizado | 0,3 / 1,3 / 0,18 s |
| Cono: opacidad en cabina / persecución | 0,03 / 0,24 |
| Cono: flujo base / velocidad / boost | 0,5 / 0,022 / 1,2 |
| Cono: mezcla al ámbar | 0,6 |
| Cono de rivales: opacidad / fundido | 0,20 / 6–18 m |

## 10. Pruebas

### Pruebas unitarias mínimas

1. `f`: 0 por debajo del 25 % de la punta, 1 a la punta, 0,5 a mitad entre las dos. Con daño, a la misma velocidad, el mismo `f`.
2. Túnel en cabina (sin suavizado): 0 parado, 0,85 a la punta sin boost, 1,2 como máximo con boost.
3. Factor de cámara: a la punta sin boost, 0,85 en cabina y 0,425 en persecución. Con boost a la punta, 1,2 y 0,6.
4. TUNNEL VISION al 50 % en persecución: a la punta sin boost, 0,2125. Al 0 %, nada en las dos cámaras.
5. Suavizado: con `delta` 0 (pausa) la intensidad no cambia. Con un paso de 0,12 s llega al 63 % del objetivo.
6. Desdoble: con `b` = 0, el 40 % del máximo; con `b` = 1, el máximo (0,010). Con intensidad 0, nada.
7. Cono: invisible hasta el 75 % de la punta, 0,5 al 87,5 % y 1 a la punta; con boost, 1,3. El boost no suma si aún no se ve.
8. Cono: a la punta, 0,03 en cabina y 0,24 en persecución, cada una solo en su cámara. Con SPEED LINES en OFF, 0 en las dos.
9. Recolocación: todo a 0 al momento en las dos cámaras.
10. PARTICLES en LOW: 6 muestras; en MEDIUM y HIGH, 16.
11. SETTINGS: TUNNEL VISION ya no es COMING SOON.
12. Cono de rivales: 0,20 con cantidad 1; con SPEED LINES en OFF, 0. Su boost llega a 1 en 0,25 s y baja a la mitad en 0,25 s.

Las pruebas existentes siguen pasando. Las de SETTINGS que esperaban TUNNEL VISION en COMING SOON se adaptan.

### Prueba manual

1. Cabina, acelerando: el túnel crece desde ~100 km/h. A media velocidad ya se nota un desdoble leve en los bordes. El centro sigue nítido. No marea.
2. Cabina con boost: el desdoble se hace más fuerte, con halo ámbar en las esquinas. Al acabar, todo vuelve suave.
3. En cabina, el túnel se ve por el parabrisas y el interior (marco, pantallas, cristal) queda nítido. El HUD de las esquinas, también.
4. Persecución a fondo: el túnel se nota claramente más suave que en cabina, y la nave se ve limpia.
5. Cambiar de cámara a fondo varias veces: el efecto cambia al momento, sin parpadeos raros.
6. Choque a fondo: el túnel baja sin saltos.
7. Pausa con el túnel puesto: congelado. Al reanudar, sigue igual. Recolocación: túnel a 0.
8. SETTINGS: TUNNEL VISION al 0 % quita el túnel en las dos cámaras. La vista previa oscurece los bordes según el valor. PARTICLES en LOW: el túnel sigue bien.
9. Rendimiento: peor frame y frames lentos en las dos cámaras, con y sin túnel, comparados con el commit anterior.
10. (Etapa 2) Cono: aparece cerca de la punta, sutil y transparente; ámbar con boost. SPEED LINES en OFF lo quita en las dos cámaras, y también las líneas del rebufo.
11. Comparar con la maqueta con las opciones elegidas: que se parezca.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **1 · Túnel** | Secciones 1–4, 6 y 7 (sin líneas) · TUNNEL VISION fuera de COMING SOON · vista previa · versión 0.29.0 | Pruebas 1–6, 9 (túnel), 10 y 11 · Prueba manual 1–9 · Commit `feat:` |
| **2 · Cono de vapor** | Sección 5 · lo de las líneas de la sección 7, para el cono | Pruebas 7–9 · Prueba manual 5, 7, 9, 10 y 11 · Commit `feat:` |

## 12. Fuera de alcance

- Entrar solo con boost o cerca de la punta (opciones A y B).
- Las líneas de velocidad, en 3D o en pantalla, y el anillo del estallido.
- Desenfocar el interior de la cabina.
- El resto de `efectos-velocidad`: FOV +14°, partículas y chispas de las toberas.
- Cambiar el FOV o la vibración.
- Sonido.
