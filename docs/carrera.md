# VECTORY — Spec: Ciclo de carrera (parrilla, salida y llegada)

Versión 1.0 · 2026-10-28 · Estado: **definido, listo para implementar**

Esta spec amplía la demo con el ciclo completo de una carrera: colocación en parrilla, salida con semáforo y cuenta atrás, y llegada a meta con pantalla de resultados. Se apoya en lo que ya existe (circuito con meta y checkpoints en `Track.cs`, lógica de carrera aparte con vueltas, cronómetro, vuelta rápida y tiempos por sector, HUD en `Hud.cs`, boost por cargas).

Cambios respecto al documento maestro:
- Una carrera tiene siempre **8 participantes** (antes 7: jugador + 6 rivales).
- La "ligera ventaja por buena reacción" en la salida **no entra** en esta versión.

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- La pista tiene **8 marcas de parrilla** unos metros por detrás de la meta, en dos columnas alternadas: impares a la izquierda (1, 3, 5, 7), pares a la derecha y un poco más atrás (2, 4, 6, 8).
- Al empezar cada carrera, el jugador se coloca en una **posición de parrilla aleatoria** (1–8). Las otras 7 quedan vacías (no hay rivales todavía).
- La salida se da con un **semáforo de 5 luces tipo F1** y una **cuenta atrás numérica** (5 → 1). Cuando se apagan las luces: **GO**, empieza la carrera.
- Antes de la salida la nave no se mueve, el cronómetro está a 0 y el boost no recarga.
- Al completar la última vuelta la carrera **termina**: se para el cronómetro, se quita el control y aparece la **pantalla de resultados** con posición final, mejor vuelta y mejor tiempo de cada sector (indicando en qué vuelta).
- **Enter** en la pantalla de resultados repite la carrera.

---

## 2. Estados de la carrera

La carrera pasa por tres estados, en este orden:

| Estado | Desde | Hasta | Qué pasa |
|---|---|---|---|
| **Previa** (`PreStart`) | Carga de la escena | Se apagan las luces | Nave quieta en su marca, semáforo y cuenta atrás, cronómetro a 0 |
| **En carrera** (`Running`) | Se apagan las luces | Se completa la última vuelta | Conducción normal, cronómetro corriendo, boost activo |
| **Terminada** (`Finished`) | Se completa la última vuelta | Se repite la carrera | Sin control, cronómetro parado, pantalla de resultados |

- No hay vuelta atrás entre estados salvo al repetir la carrera (recarga la escena y vuelve a **Previa**).

## 3. Parrilla

### 3.1 Colocación de las marcas

- Las marcas se colocan **siguiendo el eje del circuito (`Centerline`) hacia atrás desde la meta**, en sentido contrario al de la marcha. Así funcionan en cualquier circuito aunque la meta no esté en una recta perfecta.
- Dos columnas:
  - **Impares (1, 3, 5, 7)**: a la **izquierda** del eje.
  - **Pares (2, 4, 6, 8)**: a la **derecha** del eje, cada una un poco más atrás que la impar de delante.
- Distancias (medidas a lo largo del eje, hacia atrás desde la meta):

| Marca | Lado | Distancia a la meta |
|---|---|---|
| 1 | Izquierda | 12 |
| 2 | Derecha | 20 |
| 3 | Izquierda | 28 |
| 4 | Derecha | 36 |
| 5 | Izquierda | 44 |
| 6 | Derecha | 52 |
| 7 | Izquierda | 60 |
| 8 | Derecha | 68 |

  Fórmula: distancia = distancia a la primera marca + (fila × separación entre filas) + (desfase si es par), con fila = (posición − 1) / 2 en división entera.
- Desplazamiento lateral: **25 % del ancho de pista** a cada lado del eje (izquierda para impares, derecha para pares).
- Los valores son iniciales: ajustar a la escala de la nave y la pista si no encajan. Usar las mismas unidades que ya usa el circuito.
- Las marcas son **las mismas en todos los circuitos** (se generan con estos parámetros, no a mano).
- **No desaparecen** después de la primera vuelta: son parte fija del circuito.

### 3.2 Aspecto de las marcas

- Cada marca es un **corchete wireframe**: una línea transversal delante (donde va el morro de la nave) y dos patas cortas hacia atrás, una a cada lado.
- Color: **azul de la paleta (#245BFF)**, desde `Palette`.
- Se generan dentro de `Track.cs` en la misma malla de líneas que el resto del circuito (como la meta y los checkpoints), no como objetos sueltos.
- Sin números dibujados en esta versión.

### 3.3 Asignación de posiciones

- Al empezar cada carrera, el jugador recibe una **posición aleatoria entre 1 y 8**.
- La nave se coloca **centrada en su marca**, con el morro justo detrás de la línea transversal, mirando en el sentido de la marcha, con velocidad 0.
- La lógica de asignación debe poder repartir las 8 posiciones sin repetir (una permutación aleatoria), aunque de momento solo se use la del jugador. Así sirve tal cual cuando haya rivales.
- El generador aleatorio se inyecta (para poder fijar una semilla en las pruebas).
- Las 7 posiciones restantes quedan **vacías**: no se crean naves de relleno.

## 4. Salida

### 4.1 Secuencia

| Tiempo desde la carga | Luces encendidas | Número en pantalla |
|---|---|---|
| 0 s | 0 | — |
| 1 s | 1 | 5 |
| 2 s | 2 | 4 |
| 3 s | 3 | 3 |
| 4 s | 4 | 2 |
| 5 s | 5 | 1 |
| 6 s | 0 (se apagan todas) | **GO** |

- A los 6 s la carrera pasa a **En carrera**.
- El texto **GO** se queda visible **1 s** y después desaparecen el semáforo y la cuenta atrás.
- Sin retardo aleatorio antes del apagado: el momento de la salida es siempre el mismo, para que la cuenta atrás sea coherente.

### 4.2 Durante la previa

- **W, S, A, D y Espacio no hacen nada.** La nave no se mueve ni gira.
- Pulsar W antes de tiempo no tiene penalización (no hay salida en falso).
- El cronómetro está a 0 y no corre.
- El **contador de recarga del boost no corre**. Empieza en el momento del GO (esto sustituye el "empieza al cargar la escena" de la spec del boost).

### 4.3 En el GO

- Se habilita el control de la nave.
- Arranca el cronómetro de carrera y el de la vuelta 1.
- Arranca el contador de recarga del boost.

### 4.4 Aspecto del semáforo

- Overlay en el HUD (2D), **centrado horizontalmente en el tercio superior** de la pantalla.
- **5 círculos** en fila: 28 px de diámetro, 14 px de separación.
- Apagado: azul de la paleta **#245BFF** atenuado (mismo estilo que las esferas de boost vacías).
- Encendido: **magenta de la paleta #FF43D0** (color de competición), relleno pleno con halo suave.
- Número de la cuenta atrás: grande, debajo del semáforo, en cian **#36E7FF**.
- **GO**: mismo sitio y tamaño que el número, en naranja **#FF9D32**.
- Colores desde `Palette`, no hardcodeados.

## 5. Vueltas y tiempos con salida desde parrilla

Como la parrilla está detrás de la meta, la nave cruza la meta poco después de salir:

- El **primer cruce de meta tras el GO no completa ninguna vuelta**. La nave ya está en la vuelta 1.
- La **vuelta 1 se cronometra desde el GO** (incluye el tramo desde la parrilla hasta la meta, como en F1). Lo mismo para el **sector 1 de la vuelta 1**.
- A partir de ahí, las vueltas se completan como ya funciona ahora: cruzando la meta tras pasar los checkpoints en orden.
- La carrera tiene el número de vueltas que ya está configurado.

### 5.1 Mejor sector

- Un sector es el tramo de checkpoint a checkpoint, con la meta como límite (como ya está definido). Con N checkpoints hay N + 1 sectores.
- Para **cada sector** se guarda el mejor tiempo y **en qué vuelta** se hizo.
- Si hay empate, se queda la vuelta **anterior**.
- Si la lógica de carrera ya guarda tiempos por sector, solo hay que añadir el seguimiento del mejor por sector con su vuelta. Si todavía no los guarda, se añade en esta spec.

## 6. Llegada a meta

- La carrera termina al **cruzar la meta completando la última vuelta**.
- En ese momento:
  - El estado pasa a **Terminada**.
  - Se registra el tiempo de la última vuelta (puede ser la mejor vuelta) y su último sector.
  - El cronómetro **se para**.
  - Se **quita el control**: W, S, A, D y Espacio dejan de hacer nada.
  - Si había un boost en curso, **se cancela** (como si se frenara, pero sin frenar): si está por encima de la punta, pierde el exceso como siempre.
  - La nave **sigue recta y va perdiendo velocidad** con la desaceleración natural de soltar W hasta pararse.
- Cruces de meta posteriores **no cuentan nada**.
- El HUD de vuelta se queda en la última (por ejemplo, 3/3) con el tiempo final.

## 7. Pantalla de resultados

- Aparece **1,5 s** después de cruzar la meta.
- Panel centrado, con borde wireframe cian **#36E7FF** y fondo **#05070D** semitransparente. La pista se sigue viendo detrás.
- Contenido, de arriba abajo:

| Bloque | Contenido | Ejemplo |
|---|---|---|
| Título | `CARRERA TERMINADA` | |
| Posición final | Posición y total de participantes | `1º / 8` |
| Mejor vuelta | Tiempo y número de vuelta | `1:12.384 · vuelta 2` |
| Mejores sectores | Una fila por sector: nombre, tiempo y vuelta | `S1 · 0:11.902 · vuelta 3` |
| Pie | Instrucción para repetir | `ENTER · Repetir carrera` |

- Formato de tiempo: el mismo que ya usa el HUD.
- Textos principales en cian, información secundaria (número de vuelta, pie) en azul grisáceo **#A6B8D8**. Desde `Palette`.
- **Posición final**: sin rivales, el jugador termina siempre **1º**. La posición la debe dar la lógica de carrera (no escribirla fija en la pantalla), para que cuando haya rivales se calcule por orden de llegada sin tocar la pantalla de resultados.
- **Enter** recarga la escena: nueva carrera, nueva posición aleatoria en parrilla. Acción de input: `restart_race`.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Unidad |
|---|---|---|
| Participantes | 8 | naves |
| Distancia de la meta a la marca 1 | 12 | unidades |
| Separación entre filas de una columna | 16 | unidades |
| Desfase de la columna par | 8 | unidades |
| Desplazamiento lateral de las columnas | 25 | % del ancho de pista |
| Ancho de la marca | 4 | unidades |
| Largo de las patas de la marca | 2 | unidades |
| Retardo antes de la primera luz | 1 | s |
| Intervalo entre luces | 1 | s |
| Número de luces | 5 | luces |
| Tiempo con el GO visible | 1 | s |
| Retardo de la pantalla de resultados | 1,5 | s |

- Los parámetros de parrilla, en el circuito. Los de salida y resultados, donde ya viva la lógica de carrera / HUD.
- Mismos valores para todos los circuitos.

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces que ya hay. Lógica pura, sin dependencias de nodos de Godot, para poder probarla:

- **Secuencia de salida** (clase propia): estado + `Update(delta)`. Expone luces encendidas, número de la cuenta atrás (o ninguno), si se muestra el GO y si la salida ya se ha dado. Lanza un aviso **una sola vez** en el momento de la salida.
- **Disposición de parrilla** (clase propia): dada una posición 1–8 y los parámetros, devuelve la distancia hacia atrás desde la meta y el lado/desplazamiento lateral. Y una función que reparte las posiciones de forma aleatoria con el generador inyectado. `Track.cs` la usa para dibujar las marcas y para calcular dónde colocar la nave (convirtiendo distancia sobre el eje + lateral en posición y orientación 3D).
- **Lógica de carrera existente**: se le añaden los estados (Previa / En carrera / Terminada), la regla del primer cruce, el mejor sector con su vuelta, la posición final y un resultado de carrera (datos que lee la pantalla de resultados).
- **Controlador de la nave**: solo acepta input en **En carrera**. Lo consulta, no conoce la secuencia de salida.
- **Boost**: su lógica no recibe `Update` (o no recarga) hasta la salida; se cancela al terminar.
- **HUD y pantalla de resultados**: solo **leen** estado; no contienen lógica de carrera.

### Pruebas unitarias mínimas

Secuencia de salida:
1. Al empezar: 0 luces, sin número, salida no dada.
2. A 1 s: 1 luz y número 5. A 5 s: 5 luces y número 1.
3. A 6 s: 0 luces, GO visible, salida dada.
4. El aviso de salida se lanza una sola vez aunque se siga actualizando.
5. A 7 s el GO ya no se muestra.

Parrilla:
6. Marca 1 a 12 a la izquierda, marca 2 a 20 a la derecha, marca 7 a 60 a la izquierda, marca 8 a 68 a la derecha.
7. Todas las impares a la izquierda y todas las pares a la derecha.
8. El reparto aleatorio de 8 posiciones da 1–8 sin repetir.
9. Con la misma semilla, el reparto es el mismo.

Lógica de carrera:
10. En Previa el cronómetro no avanza.
11. El primer cruce de meta tras la salida no completa vuelta.
12. La vuelta 1 cuenta desde la salida.
13. Completar la última vuelta pasa a Terminada y para el cronómetro.
14. Tras terminar, otro cruce de meta no cambia nada.
15. La mejor vuelta guarda su número de vuelta correcto.
16. El mejor de cada sector guarda su vuelta; con empate se queda la anterior.
17. Sin rivales, la posición final es 1 de 8.

## 10. Prueba manual

1. Arrancar la escena: la nave aparece en una de las 8 marcas, quieta. Las 3 esferas de boost en azul.
2. Pulsar W, A, D y Espacio durante la cuenta atrás: no pasa nada.
3. Las luces se encienden de una en una con 5, 4, 3, 2, 1; se apagan todas con GO y la nave ya responde.
4. El cronómetro empieza en el GO. Cruzar la meta por primera vez no cambia la vuelta.
5. A los 35 s del GO (no de la carga) se enciende la primera esfera de boost.
6. Completar todas las vueltas: al cruzar la meta el cronómetro se para, la nave deja de responder y se va parando sola.
7. A los 1,5 s aparece la pantalla de resultados con 1º / 8, mejor vuelta con su número y el mejor de cada sector con su vuelta. Comprobar que cuadran con lo visto en el HUD.
8. Pulsar Enter: nueva carrera. Repetir varias veces y comprobar que la posición de salida cambia y que las marcas siguen visibles en todas las vueltas.

## 11. Fuera de alcance

- Rivales, IA y naves de relleno en la parrilla.
- Salida en falso y penalizaciones.
- Ventaja por buena reacción en la salida.
- Retardo aleatorio del apagado de luces.
- Semáforo 3D sobre la pista (pórtico).
- Números dibujados en las marcas de parrilla.
- Posición en vivo durante la carrera en el HUD.
- Tiempo total, tiempos de cada vuelta, vuelta ideal y comparación con récords en la pantalla de resultados.
- Guardado de récords.
- Menús (volver al menú, elegir circuito); solo Enter para repetir.
- Cámara de llegada, animaciones, efectos y sonido.
