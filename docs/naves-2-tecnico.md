# VECTORY — Spec: Naves 2 · Perfil técnico, colisión y ajuste en pruebas

Versión 1.1 · 2026-11-04 · Estado: **propuesta, pendiente de validar**

Segunda spec de naves. Da a **INTERCEPTOR (Hoja)** y **VANGUARD (Martillo)** su perfil técnico y su **ancho de colisión**, sacado de su **forma real** como el de Vector, y añade un **panel de ajuste solo para pruebas** para afinar los números conduciendo.

**Los números de esta spec son de arranque, no definitivos.** Se ajustan probando con el panel y los que se den por buenos pasan a las definiciones.

Plan de las specs de naves:

1. Definición de nave (hecha).
2. **Perfil técnico, colisión y ajuste en pruebas** (esta).
3. Geometría de los cascos y las cabinas de Hoja y Martillo, y las cabinas nuevas de las tres naves.
4. Integración: desbloquearlas en el selector y usarlas en carrera y fantasma.
5. Piloto automático de equilibrio (opcional).

Se apoya en lo que ya existe tras la spec 1: `ShipDefinition` (perfil técnico y visual), `ShipDefinitions` y su resolución, la definición de Vector, `ShipTuning`, `ShipDynamics`, `Player`, la colisión con la barrera, `ShipStats`, `ShipCatalog`, `GameSession`, el modelo de daño, el boost, los récords y el fantasma.

Diseños de referencia: `docs/naves-disenos.md` (medidas de los cascos elegidos).

Rama: `feature/ship-tuning`, que sale de `develop` con `refactor/ship-definition` ya mergeada.

Versión del juego: **0.13.1 → 0.13.2**. Para el jugador no cambia nada (como en la spec 1): Vector ya choca con 3,4 m y todavía no hay naves nuevas jugables. El minor (0.14.0) se reserva para la spec 4.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Hoja** (`ship_02`) y **Martillo** (`ship_03`) pasan a tener **definición**. Su perfil técnico son **multiplicadores sobre el de Vector** (sección 2).
- **Ancho de colisión de la forma real** en las tres naves: Vector **3,4 m** (ya es su valor actual, no cambia), Hoja **1,6 m** y Martillo **2,5 m** (sección 3).
- **Panel de ajuste para pruebas** (sección 4): solo en compilaciones de depuración. Permite cambiar los porcentajes en vivo, cambiar de nave en plena carrera y volcar los valores a la consola.
- En el selector, Hoja y Martillo **siguen como COMING SOON**. Solo se pueden conducir desde el panel y, mientras no llegue la spec 3, **con la geometría y la cabina de Vector**.
- Boost, daños, pendientes, peraltes y efectos: **iguales para las tres naves**.

## 2. Perfil técnico

### 2.1 Regla

- Vector sigue siendo la referencia y **sus valores no cambian**.
- El perfil técnico de Hoja y Martillo es el `ShipTuning` de Vector con un **multiplicador por parámetro**. Los parámetros sin multiplicador valen lo mismo que en Vector.
- Los multiplicadores se guardan en la definición de cada nave, en un único sitio. No se escriben valores absolutos a mano, para que cualquier cambio en Vector arrastre a las otras dos.
- **Tope**: ningún multiplicador puede salir de **0,90–1,10** (±10 %). Se comprueba en una prueba.

### 2.2 Valores de arranque

| Parámetro (`ShipTuning`) | Vector | Hoja (Interceptor) | Martillo (Vanguard) |
|---|---|---|---|
| `MaxSpeed` (velocidad punta) | × 1,00 | **× 1,08** | × 0,94 |
| `Acceleration` | × 1,00 | × 0,94 | **× 1,10** |
| `TurnRate` (giro) | × 1,00 | × 1,00 | × 0,96 |
| `LateralGrip` (agarre) | × 1,00 | **× 0,92** | × 1,04 |
| `BrakeDeceleration` (frenada) | × 1,00 | × 1,02 | × 0,96 |
| Resto (`Drag`, `SteerResponse`, derrape…) | × 1,00 | × 1,00 | × 1,00 |

Carácter:
- **Hoja**: más punta en recta; acelera algo menos y agarra menos en curva (la "poca tolerancia"). Es la más estrecha y la que menos choca; el agarre bajo tiene que compensar esa ventaja.
- **Martillo**: sale disparada y agarra bien; llega menos arriba, gira algo peor y frena peor. Es más estrecha que Vector, así que **el ancho no le cuesta nada**: su aceleración solo la pagan la punta, el giro y la frenada.
- **Vector**: es **la más ancha de las tres** y no tiene ninguna compensación por ello. Hay que comprobar al probar si queda en desventaja (sección 3.3).

### 2.3 Lo que se vería en el selector (para las pruebas de `ShipStats`)

Calculado con las fórmulas de `ShipStats` y `ShipSelectFormat` y los valores actuales de Vector:

| Dato | Vector | Hoja | Martillo |
|---|---|---|---|
| `TOP SPEED` | 396 KM/H · 5 | 428 KM/H · 9 | 372 KM/H · 2 |
| `ACCELERATION` | 0–200 · 2.2 S · 5 | 0–200 · 2.4 S · 2 | 0–200 · 2.0 S · 10 |
| `HANDLING` | 74 °/S · 5 | 74 °/S · 5 | 72 °/S · 3 |
| `GRIP` | · 5 | · 1 | · 7 |
| `BRAKING` | 200–0 · 1.2 S · 5 | 200–0 · 1.2 S · 6 | 200–0 · 1.3 S · 3 |

El número es el texto y la cifra tras el punto son los segmentos encendidos.

## 3. Ancho de colisión

### 3.1 Valores

| Nave | Semiancho de colisión | De dónde sale |
|---|---|---|
| Vector | **3,4 m** (valor actual, no cambia) | Punta de ala, del casco actual |
| Hoja | **1,6 m** | Punta trasera en flecha, de `docs/naves-disenos.md` |
| Martillo | **2,5 m** | Punta del canard, de `docs/naves-disenos.md` |

- Va en el **perfil visual** de cada nave, como dejó la spec 1.
- Es el punto **más ancho** del casco medido desde el eje. Sigue siendo un semiancho simple: no se tiene en cuenta si el punto más ancho está delante (Martillo) o detrás (Hoja).
- La colisión, los niveles de impacto, el daño y la recolocación no cambian de reglas: solo cambia el número de cada nave.

### 3.2 Comprobaciones (ya hechas)

- **Casco de Vector**: el punto más ancho está en x = ±3,4 m (puntas de ala), igual que su `CollisionHalfWidth`.
- **Espacio libre** con la pista de 40 m de todos los circuitos actuales (barrera a 20,5 m del eje), por lado: Vector **17,1 m**, Martillo **18,0 m**, Hoja **18,9 m**. Muy por encima del mínimo de 2 m.
- **Parrilla**: columnas a ±10 m (20 m entre ellas) y 16 m entre filas de una misma columna, así que las naves no se solapan. El marco de luz de cada puesto (3,6 m) ya es más estrecho que la envergadura de Vector (6,8 m); solo se nota a la vista y esta spec no lo toca.

### 3.3 Efectos que hay que vigilar en la prueba

- Con el mismo trazado, Hoja y Martillo tocan la barrera **más tarde** que Vector. En RACE eso también es menos daño.
- Hoja tiene 3,6 m más de pista útil que Vector en total, y Martillo 1,8 m. Si esa ventaja pesa demasiado, **se compensa en los multiplicadores, no en el ancho**.
- `HIGH TOP SPEED · LOW TOLERANCE`, la descripción de INTERCEPTOR en el catálogo, se refiere al agarre: es la que menos choca. Esta spec no cambia el texto.
- En esta spec no se tocan los puntos de daño.

## 4. Panel de ajuste para pruebas

### 4.1 Cuándo existe

- Solo en **compilaciones de depuración** (editor o `OS.IsDebugBuild()`). En una exportación de release no se crea el nodo y las teclas no hacen nada.
- Solo en la escena de carrera, en RACE y TIME TRIAL.

### 4.2 Teclas

| Tecla | Acción |
|---|---|
| **F9** | Muestra / oculta el panel |
| **F10** | Cambia la nave en plena carrera: Vector → Hoja → Martillo → Vector |
| **F11** | Escribe en la consola los multiplicadores actuales de las tres naves, listos para copiar a las definiciones |
| **F12** | Devuelve la nave actual a sus valores de arranque (los de la definición) |

- **F10** conserva posición, dirección y velocidad (la velocidad se limita a la nueva punta con la regla gradual de siempre). También conserva el boost y el daño.
- Las teclas se añaden como acciones nuevas del mapa de entrada (`debug_tuning_toggle`, etc.), no como teclas leídas a mano.
- Hay que comprobar que F9–F12 llegan al juego si se ejecuta incrustado en el editor, porque allí son atajos del depurador. Si no llegan, se prueba en ventana aparte o se cambian las teclas.

### 4.3 Contenido del panel

- Arriba, la **nave actual** y su semiancho de colisión (solo lectura).
- Una fila por parámetro de la tabla 2.2, con el **porcentaje** respecto a Vector (deslizador de −20 % a +20 %, paso 1 %) y, al lado, el **dato que vería el jugador** (km/h, 0–200 en segundos, °/s, 200–0 en segundos, segmentos de la barra), calculado con `ShipStats`.
- En Vector los deslizadores se ven pero están desactivados: Vector es la referencia.
- El deslizador permite pasar del tope de ±10 % para experimentar. Si un valor lo pasa, se marca en naranja y **F11 lo avisa** en la consola.
- Los deslizadores se manejan con el ratón y no cogen el foco: las flechas siguen siendo solo para conducir.
- Los cambios se aplican **al momento**, sin reiniciar la carrera.
- Los multiplicadores y la nave actual duran **hasta cerrar el juego**, también con RESTART o al volver al menú, y sin pasar por el selector, que no se entera.
- **No se guarda nada en disco.** Al cerrar el juego se vuelve a los valores de la definición.
- Estilo sencillo, con `Palette` y `MenuFonts`; no hace falta lienzo. Va en una capa por encima del HUD y no lo mueve.

### 4.4 Récords y fantasma

- En cuanto se usa **F10** o se cambia algún deslizador, **no se guardan récords ni fantasma hasta cerrar el juego**, en ningún circuito ni modo. F12 y RESTART no lo levantan. Al terminar una vuelta o una carrera que se habría guardado, se avisa en consola.
- Mientras tanto no sale `NEW RECORD`.
- `BEST`, el delta y el fantasma que ya existían siguen visibles, para poder comparar.
- El selector, el menú y RECORDS no se enteran del panel.

## 5. Definiciones y catálogo

- `ship_02` y `ship_03` pasan a tener definición:
  - **Perfil técnico**: el de la sección 2.
  - **Perfil visual provisional**: el de Vector (casco, cabina y cámaras) **con su propio ancho de colisión** (sección 3). La spec 3 lo sustituye por su geometría. Mientras tanto, las alas de Vector atraviesan la barrera al conducir Hoja o Martillo: es lo esperado. Sirve para juzgar los números, no cómo se siente el ancho.
- Hoy `ShipEntry.Selectable` equivale a tener definición, así que **pasa a ser un dato explícito de cada entrada del catálogo**. Sin ese cambio, las dos naves se desbloquearían solas.
- En el catálogo siguen **no elegibles**: el selector las muestra como COMING SOON, con datos `—`, igual que ahora. Las barras de la sección 2.3 solo se comprueban en pruebas.
- Resolver `ship_02` o `ship_03` devuelve ya **su** definición, no la de Vector. Un id desconocido sigue devolviendo Vector. Se adaptan las pruebas de la spec 1 que dan por hecho que no tienen definición.
- La regla "una entrada elegible tiene definición" sigue valiendo; ahora hay definiciones que todavía no son elegibles.

## 6. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Rango de los deslizadores | −20 % a +20 %, paso 1 % | Panel de ajuste |
| Tope de equilibrio | ±10 % | Definiciones (constante) y panel (aviso) |

## 7. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Multiplicadores técnicos** (puro): por nave, un multiplicador por parámetro (1 por defecto) y la función que, dado el `ShipTuning` de Vector, devuelve el de la nave.
- **Definiciones de Hoja y Martillo** (puro): multiplicadores de la sección 2 y perfil visual provisional con su ancho.
- **`ShipCatalog`** (puro): `Selectable` explícito en cada entrada; solo `ship_01` es elegible.
- **Estado del laboratorio** (puro): multiplicadores en vivo por nave, nave actual, `Cycle()`, `Reset(nave)`, si se ha tocado algo y el texto que vuelca F11. Sin nodos. Dura toda la ejecución del juego (sobrevive a la recarga de escena) y "se ha tocado algo" no vuelve a falso.
- **`TuningLabPanel`** (nodo, solo en depuración): dibuja el panel, traduce teclas y deslizadores, y aplica el `ShipTuning` resultante al `Player`. Sin reglas.
- **`Player`**: tiene que poder recibir un `ShipTuning` nuevo en plena carrera y cambiar su ancho de colisión. No hay que rehacer `ShipDynamics`: `Player` ya lee el perfil técnico y el semiancho de la definición en cada paso. Basta con poder sustituir esos dos datos sin rehacer casco, cabina ni cámaras.
- **`Main.cs`**: crea el panel solo en depuración y bloquea récords y fantasma si el laboratorio se ha usado.

### Pruebas unitarias mínimas

Perfil técnico:
1. Vector con multiplicadores 1 da exactamente su `ShipTuning` de siempre.
2. Hoja: `MaxSpeed` = Vector × 1,08, `Acceleration` × 0,94, `TurnRate` × 1, `LateralGrip` × 0,92, `BrakeDeceleration` × 1,02, y el resto igual que Vector.
3. Martillo: × 0,94, × 1,10, × 0,96, × 1,04, × 0,96 en el mismo orden, y el resto igual.
4. Ningún multiplicador de las definiciones sale de 0,90–1,10.

Datos del selector:
5. `ShipStats` de Hoja y Martillo da los textos y segmentos de la tabla 2.3.

Colisión:
6. Semiancho de colisión: Vector 3,4, Hoja 1,6, Martillo 2,5.
7. Con la barrera a la misma distancia, una nave con más semiancho entra en contacto antes (con un desplazamiento lateral menor).

Definiciones:
8. Resolver `ship_02` da Hoja; `ship_03` da Martillo; un id desconocido da Vector.
9. `ship_02` y `ship_03` tienen definición y siguen sin ser elegibles en el catálogo; `ship_01` sí lo es.

Laboratorio:
10. `Cycle()` recorre Vector → Hoja → Martillo → Vector.
11. Cambiar un porcentaje cambia el `ShipTuning` de esa nave y no el de las otras.
12. `Reset` devuelve la nave a los multiplicadores de su definición.
13. Tras cualquier cambio o `Cycle()`, "se ha tocado algo" es verdadero, y sigue siéndolo después de `Reset`; al empezar, falso.
14. El texto de F11 contiene los multiplicadores de las tres naves y marca los que pasan del ±10 %.

Las pruebas existentes siguen pasando, salvo las que dan por hecho que `ship_02` y `ship_03` no tienen definición, que se adaptan.

## 8. Prueba manual

Primero, que Vector y el selector no han cambiado:

1. RACE con Vector, sin tocar el panel: conduce y choca como antes.
2. Selector de naves: Hoja y Martillo siguen en COMING SOON.

Luego las tres naves:

3. F9: aparece el panel con Vector y sus deslizadores desactivados. El HUD de cabina, la posición, la vuelta y los tiempos no se mueven.
4. F10 en una recta: pasa a Hoja (se sigue viendo Vector, es lo esperado). Debe notarse más punta y menos agarre en curva. F10 otra vez: Martillo, más aceleración y menos punta.
5. Pegarse a la barrera con cada nave: Hoja y Martillo la tocan más tarde que Vector, con las alas ya dentro de la barrera. Comparar si la ventaja de ancho pesa demasiado.
6. Cambiar porcentajes con el panel: se notan al momento. Pasar uno del 10 %: se marca en naranja. Las flechas no mueven los deslizadores.
7. F11: en la consola salen los multiplicadores de las tres naves, listos para copiar.
8. F12: la nave vuelve a sus valores de arranque.
9. RESTART: siguen la nave y los valores del panel.
10. TIME TRIAL usando F10: al terminar una vuelta mejor que el récord no se guarda récord ni fantasma, no sale `NEW RECORD` y la consola lo avisa. Lo mismo después de F12 y de RESTART.
11. Exportar en release (o simularlo) y comprobar que F9–F12 no hacen nada y no hay panel.

Los valores que se den por buenos al probar se pasan a las definiciones antes del commit final.

## 9. Fuera de alcance

- Geometría de los cascos y cabinas de Hoja y Martillo, y las cabinas nuevas (spec 3). Esa spec necesita `design/naves-geometria.js` y `design/cabinas-vector.js`, que todavía no están en el repo.
- Desbloquear las naves en el selector y en el fantasma (spec 4).
- Colisión con forma (punto más ancho delante o detrás) en lugar de semiancho.
- Toberas y estela en el perfil visual (`efectos-velocidad`).
- Valores de daño, boost, pendientes o peraltes por nave.
- Guardar en disco los ajustes del panel.
- Piloto automático de equilibrio (spec 5).
- Rivales e IA.
