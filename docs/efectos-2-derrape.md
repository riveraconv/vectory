# VECTORY — Spec: Efectos 2 · Derrape

Versión 1.2 · 2026-11-17 · Estado: **etapa 1 hecha**

**Cambios en v1.2** (etapa 1): el brillo de levitación ya no existe (decisión del usuario); en HAMMER, que no tiene alas detrás, las marcas y el roce van en los **patines de los canards, delante** (decisión del usuario). Notas de la implementación en la sección 13.

**Cambios en v1.1** (antes de implementar): las chispas y el brillo salen del **borde interior** del ala, el que baja con la inclinación de la nave hacia dentro de la curva, y no del exterior. Es más realista. La maqueta está actualizada.

Segunda spec de efectos de nave. Al derrapar, la nave deja **marcas naranjas** en la pista que duran unos segundos, y del **borde interior del ala** salen **chispas** con un **brillo naranja** debajo. Todo es **solo visual**: no cambia el derrape, el agarre ni nada de la spec `conduccion-avanzada`.

Plan de las specs de efectos de nave:

1. Choques (hecha).
2. **Derrape** (esta).
3. Aterrizaje tras una cresta.
4. Humo del daño fuerte.

Maqueta de referencia: **`design/efectos-derrape.html`** («VECTORY · Efectos de derrape · Maqueta»). Se decidió con **MARCAS C · DURADERAS**, **COLOR MARCAS · NARANJA** y **CHISPAS B · BORDE + BRILLO**. Las marcas A y B, los colores cian y azul y las chispas A y C quedan descartados. Los valores de esta spec son los de la maqueta. Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa. El circuito, la ciudad, la nave, la cámara y el derrape automático de la maqueta son aproximados: no son parte de esta spec.

**Por qué hay chispas si la nave levita**: al derrapar, la nave se inclina hacia dentro de la curva (spec `conduccion-avanzada`, sección 7) y se decide que el **borde interior del ala**, el que queda más bajo, **roza la pista**. Es una licencia visual: la altura de vuelo, la inclinación y la física no cambian.

Se apoya en lo que ya existe:
- El **derrape** de la spec `conduccion-avanzada`: estado en derrape y **ángulo de deslizamiento** (con signo).
- La capa vertical de la spec `pista-3d`: en el suelo o en el aire, y el marco de la superficie bajo la nave.
- Las **chispas** de la spec `efectos-1-choques`: simulación, aspecto en trazos, colores, máximo de chispas y PARTICLES.
- Los **manifiestos** de los cascos 3D (puntas traseras de las alas y semiancho de cada nave).
- El **brillo de levitación** de los cascos 3D (referencia de cómo se pinta un plano sobre la pista). **v1.2**: se quitó del juego; queda como referencia en el historial.
- La pausa, la recolocación y `Palette`.

Cambios respecto a specs anteriores:
- Sustituye el punto «partículas, estela, chispas» del fuera de alcance de `conduccion-avanzada` (solo lo visual del derrape) y el de «efectos por derrape» de `efectos-velocidad`.

Rama: `feature/fx-drift`, que sale de `develop` con `feature/fx-collisions` ya mergeada. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

---

## 1. Resumen

- Solo mientras la nave está **en derrape** y **en el suelo**.
- **Intensidad** del efecto según el ángulo de deslizamiento: 0 hasta **4°**, completa a **22°**.
- **Marcas**: dos líneas finas, naranjas y tenues bajo las **puntas traseras de las alas**, que se quedan **8 s** en la pista y luego se apagan.
- **Chispas**: trazos naranjas desde el **borde interior** del ala (el del lado de dentro de la curva, que baja con la inclinación), como las de los choques.
- **Brillo**: una mancha naranja que parpadea en la pista bajo ese borde.
- **PARTICLES** cambia las chispas. Las marcas y el brillo, no.
- Para todas las naves que derrapen, en RACE y en TIME TRIAL. El fantasma, no.
- Etapas:
  - **0** · Inventario.
  - **1** · Marcas.
  - **2** · Chispas y brillo.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo se leen desde fuera de la dinámica el **estado de derrape** y el **ángulo de deslizamiento** (con signo), para la nave del jugador y para los rivales. Si los rivales no derrapan nunca, se apunta.
2. Cómo se sabe si la nave está **en el suelo** y cómo se obtiene el **punto y la normal de la superficie** bajo un punto cualquiera (spec `pista-3d`), para dejar las marcas pegadas a la pista con pendientes y peraltes.
3. Dónde están, en el manifiesto de cada casco 3D, las **puntas traseras de las alas** y el **borde** del ala (de qué z a qué z y a qué semiancho). Apuntar los valores de VECTOR, BLADE y HAMMER.
4. Cómo quedó la simulación de chispas de `efectos-1-choques` y si se le puede pedir que **emita** desde otro sitio con otros valores sin tocarla.
5. Cómo se pinta el **brillo de levitación** (plano en la pista, material, mezcla), para hacer el brillo naranja igual.
6. El signo de la **inclinación** visual de la nave, para confirmar que el lado que baja es el de dentro de la curva.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Intensidad

- `β` = ángulo de deslizamiento en grados, sin signo.
- **Intensidad** `d` = (`β` − 4) / 18, limitada a 0–1. Es 0 hasta 4° y 1 desde 22°.
- **Lado del roce** = el lado **interior**: el **contrario** al del deslizamiento, que es también el lado hacia el que se inclina la nave. En una curva a la izquierda derrapando, la nave desliza hacia la derecha y se inclina a la izquierda: el lado del roce es el **izquierdo**.
- **Hay efectos** solo si, a la vez:
  - la nave está **en derrape** (estado de `conduccion-avanzada`);
  - está **en el suelo** (spec `pista-3d`);
  - `β` es mayor que 4°.
- Al salir del derrape, al despegar o al bajar de 4°, los efectos se **cortan al momento**: dejan de salir chispas, el brillo se apaga y el trazo de las marcas se corta. Lo que ya hay en la pista o en el aire sigue su vida.
- Los 4° y 22° son de la maqueta (con un deslizamiento máximo de 24°). Si con el máximo que el usuario dejó en la escena no se llega a los 22° o se llega siempre, se avisa y se ajustan.

## 4. Marcas

### 4.1 Dónde

- Dos marcas por nave, una bajo cada **punta trasera de las alas**, al **95 %** del semiancho de la nave y a la altura de la popa del ala (inventario, punto 3).
- Cada punto se proyecta sobre la **superficie** de la pista y se sube **3 cm** por su normal, para que no parpadee con el suelo.
- Como la nave va cruzada, las dos marcas se separan más que el ancho de la nave y dibujan el derrape.

### 4.2 Muestras y trazos

- Mientras hay efectos, se guarda una **muestra** cada vez que el centro entre las dos puntas avanza **0,8 m** (no una por frame). Cada muestra guarda los dos puntos, el momento y la intensidad `d`.
- Un **trazo** es una serie de muestras seguidas. Cada vez que los efectos se cortan (sección 3), el trazo se cierra y el siguiente derrape empieza otro: no se unen dos derrapes con una línea.
- Se dibujan líneas entre muestras consecutivas del mismo trazo, una por cada punta.
- Como mucho **700 muestras** por nave (parámetro): más de 8 s a la velocidad de derrape. Si se llenan, se quita la más vieja.

### 4.3 Aspecto

- **Color**: naranja **(1; 0,62; 0,2)**, derivado de `Palette.Orange` si se puede.
- **Brillo** de cada muestra = **0,32** × (0,35 + 0,65·`d`) × desvanecido.
- **Desvanecido**, con `u` = edad / 8 s:
  - de 0 a 0,75: 1 (la marca se queda igual 6 s);
  - de 0,75 a 1: baja en línea recta hasta 0.
- Mezcla **aditiva**, con color por vértice, sin escribir profundidad. Líneas de 1 px, como las de la pista: son **finas y tenues** a propósito.
- Una sola malla de líneas para **todas las naves**, que se actualiza cada frame.

### 4.4 Cuándo se borran

- **Pausa**: se congelan (no envejecen).
- **Recolocación**: se corta el trazo, pero las marcas que hay **se quedan**.
- **RESTART** o salir de la carrera: se borran todas.
- Las marcas **no dependen de PARTICLES**.

## 5. Chispas

Usan la **misma simulación, el mismo aspecto en trazos y los mismos colores** que las chispas de `efectos-1-choques` (gravedad, aire, rebote en el suelo y en la barrera, trazo de 0,03 s con máximo de 2,2 m, de blanco cálido a naranja y rojo oscuro). Comparten el **máximo de 3000** chispas vivas. Solo cambia de dónde y cómo salen:

| Parámetro | Valor (HIGH) |
|---|---|
| Chispas por segundo | **160 × `d`** |
| Sitio | El **borde interior** del ala, por debajo: a lo largo del ala desde z = 0,2 m hasta la popa del ala, con el semiancho creciendo del 55 % al 100 % (siguiendo el borde), a 8 cm sobre la superficie |
| Velocidad heredada | **60 %** de la velocidad de la nave |
| Hacia fuera de la nave (por el lado del roce) | 2–6 m/s |
| Hacia arriba (normal de la superficie) | 1–4 m/s |
| Aleatorio | ±1 m/s a los lados y a lo largo |
| Vida | 0,15–0,4 s |

- «Hacia fuera de la nave» es alejándose de ella por el lado del roce: en una curva a la izquierda, hacia la izquierda (hacia dentro de la curva).
- El reparto por segundo va con acumulador, como el roce de los choques, para que no dependa de los fps.
- **PARTICLES**: 25 / 50 / 100 % de las chispas por segundo, como en los choques.
- Sin brasas, sin destello en el punto de contacto y sin humo (eso es de los choques).
- Las z y el semiancho del borde son de la maqueta. En cada nave se ajustan a su ala real con el manifiesto (inventario, punto 3).

## 6. Brillo bajo el borde

Dos piezas, en el lado del roce, centradas en el punto de la nave a **80 % del semiancho** y **z = 1,0 m** (hacia la popa), y llevadas a la superficie:

| Pieza | Forma | Color | Intensidad |
|---|---|---|---|
| Mancha en la pista | Plano de **1,6 × 3,2 m** sobre la superficie (3 cm), orientado con la nave, textura redonda y suave | Naranja **#FF9D32** | **0,75** × `d` × parpadeo |
| Halo | Sprite de **1,4 m** a 0,2 m sobre la superficie, que mira a la cámara | Naranja claro **#FFC070** | **0,6** × `d` × parpadeo |

- **Parpadeo** = 0,75 + 0,25 × sen(41·`t`) × sen(23·`t`), con `t` el tiempo de la carrera (se para con la pausa). Con un desfase por nave, para que no parpadeen todas a la vez.
- Mezcla **aditiva**, sin escribir profundidad.
- Se apaga al momento cuando los efectos se cortan.
- No cambia con PARTICLES.
- ~~El brillo azul de levitación del casco 3D sigue igual debajo de la nave.~~ **v1.2**: ya no existe.

## 7. Interacción con otros sistemas

- **En el aire** (spec `pista-3d`): nada. Al aterrizar derrapando, si sigue en derrape, los efectos vuelven con un trazo nuevo.
- **Barrera**: rozar derrapando saca los efectos del derrape **y** los del roce de `efectos-1-choques`, a la vez. Un golpe medio o fuerte corta el derrape (spec `conduccion-avanzada`), así que corta estos efectos.
- **Boost**: sin cambios.
- **Daño**: sin cambios.
- **Fantasma**: no deja marcas, ni chispas, ni brillo (no se graba el estado de derrape).
- **Rivales**: si derrapan, mismos efectos. Si no derrapan nunca (inventario, punto 1), no hay nada que hacer para ellos.
- **Terminada**: al terminar se sale del derrape, así que los efectos se cortan.
- **Cabina**: las marcas, las chispas y el brillo están en el mundo, así que se ven si caen en la vista. Si en cabina el brillo o las chispas molestan, **se avisa**.
- **Túneles**: sin cambios.
- **Pausa**: todo congelado.

## 8. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de derrape.

| Parámetro | Valor |
|---|---|
| Ángulo de inicio / intensidad completa | 4° / 22° |
| Lado del roce | Interior (contrario al deslizamiento) |
| Marcas: posición de las puntas | 95 % del semiancho, popa del ala |
| Marcas: separación entre muestras | 0,8 m |
| Marcas: altura sobre la superficie | 3 cm |
| Marcas: duración / empieza a apagarse | 8 s / al 75 % |
| Marcas: brillo | 0,32 × (0,35 + 0,65·d) |
| Marcas: color | (1; 0,62; 0,2) |
| Marcas: máximo de muestras por nave | 700 |
| Chispas por segundo | 160 × d |
| Chispas: borde (z, semiancho) | 0,2 m → popa del ala, 55 % → 100 % |
| Chispas: velocidad heredada / hacia fuera / hacia arriba | 60 % / 2–6 m/s / 1–4 m/s |
| Chispas: vida | 0,15–0,4 s |
| Brillo: posición | 80 % del semiancho, z = 1,0 m |
| Brillo: mancha / halo (tamaño, intensidad) | 1,6 × 3,2 m, 0,75 / 1,4 m, 0,6 |
| Brillo: parpadeo | 0,75 + 0,25·sen(41t)·sen(23t) |

## 9. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot:

- **Ajustes de efectos de derrape** (datos): todo lo de la sección 8.
- **Intensidad del derrape** (pura): con el estado de derrape, si está en el suelo y el ángulo de deslizamiento, da si hay efectos, `d` y el lado del roce.
- **Rastro de marcas** (puro, uno por nave): `Update(hayEfectos, d, puntaIzquierda, puntaDerecha, tiempo)`, con la regla de los 0,8 m, el corte de trazos y el máximo de muestras. Da los segmentos con su brillo en un momento dado.
- **Emisor de chispas del derrape** (puro): con `d`, el lado del roce, el borde del ala, la velocidad de la nave, `delta`, la calidad de partículas y un generador aleatorio inyectado, da las chispas nuevas. Las mete en la **simulación de chispas** de `efectos-1-choques`, que no cambia.
- **Brillo del derrape** (puro): intensidad de la mancha y del halo con `d`, el tiempo y el desfase de la nave.
- **Nodos de Godot**: solo dibujan:
  - una malla de líneas para las marcas de todas las naves;
  - las chispas, en la malla de las chispas de los choques;
  - la mancha y el halo, un par por nave, colgados de la nave pero llevados a la superficie como el brillo de levitación.

  Sin reglas dentro.

## 10. Pruebas

### Pruebas unitarias mínimas

Intensidad (etapa 1):
1. `β` 3° → sin efectos. 4° → `d` = 0. 13° → `d` = 0,5. 22° y 30° → `d` = 1.
2. Sin estado de derrape, aunque `β` sea grande → sin efectos. En el aire → sin efectos.
3. Deslizando hacia la derecha → lado del roce izquierdo. Hacia la izquierda → derecho.

Marcas (etapa 1):
4. Avanzando 0,5 m no se guarda muestra; a los 0,8 m, sí.
5. Cortar los efectos y volver: dos trazos, sin segmento entre el último del primero y el primero del segundo.
6. Brillo: con `d` = 1, 0,32 a los 0 s y a los 6 s; 0,16 a los 7 s; 0 a los 8 s. Con `d` = 0, 0,112 al empezar.
7. Con 701 muestras se quita la más vieja.
8. Con `delta` 0 (pausa) nada envejece.

Chispas y brillo (etapa 2):
9. `d` = 1 en HIGH: 160 chispas en 1 s (±1), con `delta` 1/60 y 1/30. `d` = 0,5: 80. LOW con `d` = 1: 40.
10. Todas salen por el lado del roce y con la componente hacia fuera de la nave ≥ 2 m/s.
11. Sin efectos → ninguna chispa nueva y brillo 0.
12. Brillo de la mancha con `d` = 1: entre 0,375 y 0,75. Con `d` = 0: 0.
13. Con la misma semilla, las mismas chispas.

Las pruebas existentes siguen pasando.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Marcas** | Secciones 3 y 4, y las partes de 7 que tocan las marcas | Pruebas 1–8 · Prueba manual 1–7 · Commit `feat:` |
| **2 · Chispas y brillo** | Secciones 5 y 6, y lo que queda de 7 · versión | Pruebas 9–13 · Prueba manual 8–15 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Marcas**
1. TIME TRIAL, cámara exterior: derrapar en una curva. Quedan dos líneas naranjas finas y tenues bajo las puntas de las alas, que se abren más que la nave porque va cruzada.
2. Mirar atrás o dar la vuelta: las marcas siguen ahí unos 6 s y se apagan en los 2 s siguientes.
3. Derrapar poco (ángulo pequeño): marcas más tenues. Mucho: más fuertes.
4. Dos derrapes seguidos con un hueco: dos trazos, sin línea que los una.
5. En una curva con peralte o pendiente: las marcas van pegadas a la pista, sin hundirse ni flotar.
6. Despegar en una cresta derrapando: las marcas se cortan en el aire.
7. Pausa: no se apagan. RESTART: desaparecen.

**Etapa 2 · Chispas y brillo**
8. Derrapar: del borde interior del ala (el de dentro de la curva, el que baja con la inclinación) salen chispas en trazos naranjas que caen y se quedan atrás, con una mancha naranja que parpadea en la pista bajo ese borde.
9. Comparar con la maqueta (C · NARANJA · B) en LENTO: que se parezcan.
10. Derrapar en una curva al otro lado: las chispas y el brillo pasan al otro borde.
11. Contravolante hasta bajar de 4°: chispas y brillo se cortan al momento.
12. PARTICLES en LOW: muchas menos chispas. Las marcas y el brillo, igual.
13. Derrapar rozando la barrera: salen las chispas del derrape y las del roce a la vez.
14. Cabina: no molesta. Si molesta, avisar.
15. RACE con los 8: mirar los fps en una curva con varias naves derrapando (si los rivales derrapan).

## 12. Fuera de alcance

- Bajar la nave o cambiar su inclinación al derrapar.
- Humo o polvo del derrape.
- Marcas en las paredes o en la barrera.
- Luz de las chispas o del brillo sobre las naves y la pista (iluminación del circuito).
- Indicador de derrape en el HUD o puntuación de derrapes.
- Grabar el derrape en el fantasma.
- Sonido y vibración del mando.

## 13. Notas de la implementación

### Etapa 0 · Inventario

1. Derrape: `ShipDynamics.Drifting` y `ShipDynamics.SlipAngle` (radianes, positivo = desliza hacia la derecha); el `Player` los expone. **Los rivales no derrapan nunca**: su piloto no pulsa la tecla. El ángulo máximo del juego es 60° (la maqueta, 24°), así que los 22° se alcanzan sin problema.
2. En el suelo: `Player.IsAirborne` (`ShipVertical.Airborne`). Superficie bajo cualquier punto: `TrackFrame.SurfaceUnder(x, z)`, con su altura y su normal.
3. Los manifiestos no guardan los bordes de las alas (solo el semiancho y los vacíos `Trail_*`). De los scripts de Blender: VECTOR, punta trasera del ala en ±2,60 m y z 2,7 (semiancho 2,634); BLADE, puntas traseras de z 2,75 a 3,60 a 1,55–1,60 m (semiancho 1,612); **HAMMER no tiene alas detrás**: lo más ancho son los canards, delante, con la punta caída y un patín de acero en ±2,465 m, z −2,2, casi a ras de pista.
4. Las chispas de los choques admiten chispas nuevas con `SparkField.Add` sin tocar la simulación.
5. Brillo de levitación: plano con un shader sin luz, en suma y con caída radial. **Quitado del juego** (decisión del usuario).
6. Inclinación: al girar a la izquierda baja el ala izquierda. Contravolanteando, la nave se inclina al otro lado, pero el lado del roce sigue saliendo del deslizamiento.

### Etapa 1 · Marcas

- Puntas por nave en el perfil visual (`DriftContactShape.TipZ`): VECTOR 2,7, BLADE 3,6 y **HAMMER −2,2, en los patines de los canards** (decisión del usuario). Lateral: el 95 % del semiancho de colisión.
- Las muestras se toman en el paso de física; su hora es la de un reloj propio de las marcas, que avanza en el frame y se para con la pausa.
- Color desde `Palette.Orange` (#FF9D32), casi igual al (1; 0,62; 0,2) de la maqueta.

