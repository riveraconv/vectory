# VECTORY — Spec: Efectos 7 · Estelas

Versión 1.0 · 2026-11-19 · Estado: **definido, listo para implementar**

Segunda spec de la **segunda tanda de efectos de nave**. **Sustituye las estelas de las alas** (las líneas de `efectos-velocidad` 6.2) por una **cinta de luz** con shader que sale de cada punta de ala. Reacciona a la velocidad, a la **carga** (giro fuerte y derrape) y al boost. Añade el **vórtice**, que enrosca la estela en espiral con carga, y los **pulsos**: destellos que corren por la estela hacia atrás con boost.

Es **solo visual**: no cambia la conducción, el boost, el daño ni la física.

Plan de la segunda tanda (se irá completando sobre la marcha):

5. Toberas (hecha).
6. **Estelas** (esta).
7. Luces de la nave (freno, boost, luces del casco con la conducción).
8. Visión túnel por cámara.

Maqueta de referencia: **`designs/efectos-estelas.html`** («VECTORY · Estelas · Maqueta»). Se decidió con **ESTELA · A·CINTA**, **VÓRTICE AL GIRAR · SÍ**, **PULSOS CON BOOST · SÍ**, **COLOR · CIAN** y **ESTELA DE TOBERAS · NO**. El vapor (opción B) y la estela de toberas **no entran**. Los valores de esta spec son los de la maqueta. Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa. La pista, la nave, la conducción del piloto automático, la cámara y el bloom de la maqueta son aproximados: no son parte de esta spec. El bloom de la maqueta hace el papel del **glow** de Godot.

Se apoya en lo que ya existe:
- Las **estelas de las alas** y los factores `th` y `b` de `efectos-velocidad`.
- El **estado de la tobera** de `efectos-6-toberas` (su boost de la llama `bt` y las entradas por nave: derrape, `β`, `βmáx`).
- Los **cascos 3D**: los vacíos de las estelas (`Trail_L`, `Trail_R`) y los ejes de la nave.
- La **conducción**: rumbo de la nave, derrape y ángulo de deslizamiento (`conduccion-avanzada`).
- El **glow** del `WorldEnvironment`, la pausa, la recolocación, el fantasma y la cámara activa.

Cambios respecto a specs anteriores:
- **Se sustituye la sección 6.2 de `efectos-velocidad`** (estelas de las alas en líneas). Las **partículas de las toberas** y las **chispas del boost** de su sección 6.1 **no cambian**.

Rama: `feature/fx-trails`, que sale de `develop`. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

---

## 1. Resumen

- **Cinta de luz** por punta de ala, en espacio de mundo y siempre de cara a la cámara: un **núcleo** fino muy brillante dentro de un **halo** más ancho. Se **afina** y se **apaga** hacia atrás.
- **Carga** (0–1) por nave: sale de la aceleración lateral al girar y del derrape. Con carga, la estela se ve aunque se vaya despacio, es más ancha, más brillante y más larga.
- **Vórtice**: con carga, la estela se enrosca en espiral detrás de cada punta, cada lado en un sentido.
- **Pulsos**: con boost, destellos que corren por la estela hacia atrás.
- **Color**: cian, que pasa a naranja con boost (60 %). Cada nave recibe su **pareja de colores** (normal y boost), para que un día salga de la pintura (sección 7).
- Para todas las naves: tú y los rivales. El fantasma, sin estela (como hoy).
- Etapas:
  - **0** · Inventario.
  - **1** · Cinta nueva.
  - **2** · Vórtice y pulsos.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo están hechas hoy las **estelas de las alas** de `efectos-velocidad` (nodos, mallas, cómo guardan el recorrido, de dónde leen `f` y `b`) y qué hay que quitar.
2. Dónde están los vacíos **`Trail_L` / `Trail_R`** en los tres cascos (VECTOR: ±2,6; −0,05; 2,72) y si los rivales los tienen igual.
3. Cómo se lee, **para cada nave**, el **rumbo** (para la velocidad de giro), si está en **derrape**, `β` y `βmáx`, y el `bt` del estado de la tobera (`efectos-6`).
4. Cómo se sabe cuál es la **cámara activa** y su posición, para orientar la cinta.
5. Cómo se dibuja hoy (o no) la estela en el **fantasma**, la **nave del menú** y la **cabina**.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Estado de la estela

Todo se calcula **por nave**. Suavizados con aproximación exponencial: `x ← x + (objetivo − x) · (1 − e^(−Δt/τ))`.

### 3.1 Carga `L`

- **Velocidad de giro** `ω` = cambio del rumbo de la nave por segundo (rad/s), con el rumbo envuelto a ±π.
- **Por giro**: `Lg` = mín(1; `v` × |`ω`| / **25**). Es la aceleración lateral, completa a 25 m/s².
- **Por derrape** (solo derrapando): `Ld` = mín(1; |`β`| / `βmáx`) × **0,85**.
- **Objetivo** = máx(`Lg`; `Ld`) (sin derrape, `Lg`). τ **0,15 s**.

### 3.2 Muestras

- Cada frame, por cada punta de ala, se guarda una **muestra**:
  - posición en el mundo de `Trail_L` / `Trail_R`;
  - tiempo de la carrera `t` (se para con la pausa);
  - `th` (velocidad / punta base, 0–1), `bt` y `L` de ese momento;
  - los ejes **derecha** y **arriba** de la nave en ese momento (para el vórtice).
- Se guardan como mucho **400** muestras y **2,2 s** por punta. La más antigua sale primero.
- Con la pausa (`Δt` = 0) no se añaden muestras ni envejecen.

### 3.3 Vida

La cinta solo dibuja las muestras con **edad** = `t` − `t` de la muestra **≤ vida**, con la vida que da la muestra **más nueva**:

- **vida** = mín(1,6; 0,25 + 0,9·`th` + 0,5·`bt` + 0,25·`L`) s.
- `u` = edad / vida (0 en la punta del ala, 1 al final de la cinta).

## 4. Cinta (etapa 1)

### 4.1 Malla

- Por punta de ala, una **tira de triángulos** en **espacio de mundo** (no cuelga de la nave), rehecha cada frame: dos vértices por muestra viva.
- **Centro** de cada muestra: su posición (más el vórtice de la sección 5.1).
- **Dirección** en cada muestra: de la muestra siguiente (más antigua) a la anterior (más nueva). Si es casi nula, la de la marcha.
- **De cara a la cámara**: el lado = normalizar(dirección × (cámara − centro)). Los dos vértices van a ± ancho / 2 por ese lado.
- Datos por vértice: `u`, `v` (−1 a un lado, +1 al otro), intensidad, color, distancia `D` desde la punta del ala (m, sumada a lo largo de los centros) y boost de la muestra (para los pulsos).
- Caja de recorte grande o recorte desactivado.

### 4.2 Ancho, intensidad y color (por muestra)

- **Ancho** = (0,10 + 0,14·`th` + 0,22·`L` + 0,18·`bt`) × (1 − 0,5·`u`) m.
- **Visibilidad** = máx(limitar((`th` − 0,37) / 0,45; 0; 1); 0,8·`L`; `bt`). Por velocidad, empieza al **37 %** de la punta y es completa al **82 %**, como hoy. Con carga o boost se ve aunque se vaya más despacio.
- **Intensidad** = (1,4 + 1,2·`bt` + 0,8·`L`) × visibilidad.
- **Color** = mezcla(color normal; color boost; 0,6·`bt`), con la pareja de la nave (sección 7). Hoy, para todas: **cian** (0,21; 0,90; 1,0) y **naranja** (1,0; 0,62; 0,20), de `Palette`.

### 4.3 Shader

Espacial, **sin luz**, mezcla **aditiva**, las **dos caras**, **sin escribir profundidad** (sí la lee):

- **A lo largo**: `a` = (1 − `u`)^1,5 × smoothstep(0; 0,04; `u`).
- **De lado**: `c` = 0,6 × e^(−3,5·`v`²) + 1,2 × e^(−45·`v`²). Halo ancho y núcleo fino.
- **Salida** = color × (`c` × `a` + pulso × `a`) × intensidad. El pulso es 0 en la etapa 1 (sección 5.2).
- Muy por encima de 1 de brillo para que el **glow** la haga brillar. Se ajusta a ojo para que se vea como la maqueta y se apuntan los valores.

## 5. Vórtice y pulsos (etapa 2)

### 5.1 Vórtice

Con `L` de la muestra > **0,05**, su centro se desplaza en el plano **derecha / arriba** que guardó la muestra:

- **Lado** `ℓ`: −1 la punta izquierda, +1 la derecha.
- **Fase** = `ℓ` × 14 × edad × (0,4 + `L`) + (π en la izquierda, 0 en la derecha).
- **Radio** = 0,35 × `L` × mín(1; edad / 0,15) m. Nace en la punta y se abre en 0,15 s.
- **Centro** += radio × (cos(fase) × derecha + sen(fase) × arriba).

Cada lado gira en un sentido, como los vórtices reales de punta de ala. La distancia `D` se suma con los centros ya desplazados.

### 5.2 Pulsos

- En el shader: pulso = `B` × máx(0; sen(0,9·`D` − 25·`t`))^16 × 2,5 × e^(−8·`v`²).
- `B` = `bt` de la muestra. Sin boost, 0.
- Salen destellos cada ~7 m que corren hacia atrás a ~28 m/s. Con `t` parado (pausa), quietos.

## 6. Interacción con otros sistemas

- **Partículas de las toberas y chispas del boost** (`efectos-velocidad` 6.1): sin cambios.
- **Llama de las toberas** (`efectos-5`): sin cambios. **No hay estela de toberas.**
- **Parrilla y cuenta atrás**: sin estela (parada, visibilidad 0).
- **En el aire** (pista 3D): igual; las muestras siguen la posición real de la punta.
- **Recolocación**: se borran todas las muestras y `L` vuelve a 0 al momento. No queda estela cruzando la pista.
- **Pausa**: todo congelado.
- **Terminada**: la estela se apaga sola al frenar.
- **Cabina**: tu estela no se dibuja (como la llama). La de los rivales, sí.
- **Atravesables** (recolocación de `rivales-3`): la estela no parpadea con el casco.
- **Fantasma**: sin estela, como hoy. **Nave del menú** y **holograma**: sin estela.
- **SETTINGS**: no depende de ningún ajuste.

## 7. Color de la estela y pintura (preparado)

- Cada nave tiene una **pareja de colores de estela**: normal y boost. La estela **no la decide**: se la pasan al montar la nave.
- **Ahora**: cian y naranja para todas (tú y los rivales).
- **Más adelante** (spec aparte, no ahora): la pareja saldrá de la **pintura** de cada nave, también la de los rivales con su pintura aleatoria:
  - VECTOR ya tiene **color de acento** en su catálogo (por ejemplo, `graphite_red` → rojo);
  - BLADE y HAMMER **no tienen acento**: habrá que darles una pareja por pintura o dejar que el jugador la elija en PAINT;
  - con un acento naranja o ámbar, el boost necesitará otro color para que se note.
- Por eso el color va en un **proveedor** propio: (nave, pintura) → pareja. Hoy devuelve siempre cian y naranja.

## 8. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de estelas.

| Parámetro | Valor |
|---|---|
| Carga: aceleración lateral completa / derrape / τ | 25 m/s² / 0,85 / 0,15 s |
| Muestras: máximo / historia | 400 / 2,2 s |
| Vida | 0,25 + 0,9·th + 0,5·bt + 0,25·L s, máx. 1,6 |
| Ancho | (0,10 + 0,14·th + 0,22·L + 0,18·bt) × (1 − 0,5·u) m |
| Visible desde / completa en | 37 % / 82 % de la punta |
| Visibilidad por carga | 0,8·L |
| Intensidad | (1,4 + 1,2·bt + 0,8·L) × visibilidad |
| Mezcla al color de boost | 60 % |
| Apagado a lo largo | (1 − u)^1,5 |
| De lado (halo / núcleo) | 0,6·e^(−3,5v²) + 1,2·e^(−45v²) |
| Vórtice: carga mínima / vueltas / radio / apertura | 0,05 / 14 × (0,4 + L) / 0,35·L m / 0,15 s |
| Pulsos: separación / velocidad / fuerza | 0,9 rad/m / 25 rad/s / 2,5 |
| Colores (normal / boost) | cian (0,21; 0,90; 1,0) / naranja (1,0; 0,62; 0,20) |

## 9. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot:

- **Ajustes de estelas** (datos): todo lo de la sección 8.
- **Entradas de la estela** (datos): posición de cada punta, ejes de la nave, rumbo, velocidad, `th`, `bt`, derrape, `β`, `βmáx`. Se rellenan desde el `Player` o el conductor del rival.
- **Estado de la estela** (puro, uno por nave): `Update(delta, t, entradas)` con `L` y las muestras de cada punta. `Reset()` para la recolocación.
- **Constructor de la cinta** (puro): de (muestras, posición de la cámara, pareja de colores, ajustes) a los vértices de 4.1 y 4.2, con el vórtice de 5.1. Sin nodos.
- **Proveedor de colores de estela** (puro): (nave, pintura) → pareja. Hoy, cian y naranja.
- **Nodo de Godot** (solo dibuja): una `MeshInstance3D` con malla dinámica (`ImmediateMesh` o `ArrayMesh`) y el shader, en espacio de mundo, por nave. Sin reglas dentro.
- La lógica de `efectos-velocidad` 6.2 se quita. Sus factores `f`, `th` y `b` siguen para el resto de efectos.

## 10. Pruebas

### Pruebas unitarias mínimas

Cinta (etapa 1):
1. Carga: en recta, 0. Con `v` × |`ω`| = 25, tiende a 1. Derrapando con |`β`| = `βmáx` en recta, a 0,85.
2. Vida: parada, sin carga ni boost → 0,25 s. A la punta → 1,15 s. A la punta con boost → 1,6 s (límite).
3. Visibilidad: al 30 % de la punta sin carga ni boost → 0. Al 82 % → 1. Al 30 % con `L` = 1 → 0,8.
4. Ancho en la punta del ala: a la punta sin carga ni boost → 0,24 m. Con `L` = 1 y boost → 0,64 m. Al final de la cinta (`u` = 1), la mitad.
5. Color: con `bt` = 0, el normal. Con `bt` = 1, 60 % hacia el de boost.
6. Muestras: nunca más de 400 ni más viejas de 2,2 s. Solo se dibujan las de edad ≤ vida.
7. Con `delta` 0 (pausa), ni muestras nuevas ni cambio de edad.
8. `Reset()`: sin muestras y `L` = 0.
9. El proveedor de colores devuelve cian y naranja para las tres naves y cualquier pintura.

Vórtice y pulsos (etapa 2):
10. Vórtice: con `L` = 0, centros sin mover. Con `L` = 1 y edad ≥ 0,15 s, a 0,35 m de la muestra. Las dos puntas giran en sentidos opuestos.
11. Pulsos: con `bt` = 0 en todas las muestras, ningún pulso.

Las pruebas existentes siguen pasando. Las de las estelas de `efectos-velocidad` (duración 0,12 + 0,45·f + 0,25·b) se quitan o se adaptan.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Cinta nueva** | Quitar las líneas de 6.2. Secciones 3, 4, 6, 7 | Pruebas 1–9 · Prueba manual 1–7 · Commit `feat:` |
| **2 · Vórtice y pulsos** | Sección 5 · versión | Pruebas 10–11 · Prueba manual 8–12 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Cinta nueva**
1. TIME TRIAL, cámara exterior: parado y despacio, sin estela.
2. Acelerar: hacia el 40 % de la punta empieza a verse la cinta; a fondo, larga, con el núcleo brillante y el halo, afinándose y apagándose hacia atrás.
3. Curva cerrada a fondo y derrape: la estela se ensancha, brilla más y se alarga. Derrapando en una curva lenta, también se ve.
4. Boost: la estela pasa a naranja, más ancha y más larga. Al acabar, vuelve suave al cian.
5. Recolocarse: sin estela cruzando la pista. Pausa: congelada.
6. Cabina: tu estela no se ve; la de un rival, sí. Fantasma sin estela.
7. Comparar con la maqueta (A·CINTA, sin vórtice ni pulsos) en LENTO: que se parezca. Ajustar el glow y apuntar los valores.

**Etapa 2 · Vórtice y pulsos**
8. Curva cerrada o derrape: detrás de cada punta, la estela se enrosca en espiral, cada lado en un sentido. En recta, recta.
9. Boost: destellos que corren por la estela hacia atrás. Sin boost, ninguno.
10. Pausa con boost y vórtice: todo quieto.
11. Comparar con la maqueta (A·CINTA, VÓRTICE SÍ, PULSOS SÍ, CIAN) en LENTO.
12. Mirar los fps con los 8 en la salida y con todos en curva.

## 12. Fuera de alcance

- El **vapor** (opción B de la maqueta) y la **estela de toberas**.
- Que el color de la estela salga de la **pintura** o se elija en PAINT (sección 7: queda preparado).
- Las **luces de la nave** (spec 7) y la **visión túnel** por cámara (spec 8).
- Cambiar las partículas de las toberas o las chispas del boost de `efectos-velocidad`.
- Sonido y vibración del mando.
