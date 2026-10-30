# VECTORY — Spec: Barreras y colisiones

Versión 1.1 · 2026-10-30 · Estado: **definido, listo para implementar**

Añade **barreras wireframe** a los dos lados de todos los circuitos y un **sistema de colisiones** con tres niveles de impacto (roce, medio, fuerte), **destello** en la barrera, **sacudida de cámara** y **recolocación automática** si la nave se atasca o se sale. Sustituye a los "límites laterales simples" de la etapa 3 de la demo.

Diseño de referencia: lienzo **"VECTORY — Menú principal"**, página **Barreras**: *Mejora 1 · B reforzada · franjas gruesas · cabina / exterior* (elegida) y *Destello de impacto (sobre A)* como referencia del destello (se dibujó sobre la valla A; en esta spec se aplica a la barrera elegida). Las barreras A, C, D, la B original, la B ligera y las mejoras 2, 3 y 4 quedan descartadas. El fondo de las artboards con "fondo de prueba" **no** es parte de esta spec. Si algo no cuadra con el lienzo, manda esta spec y se avisa.

**Cambios en v1.1** (sobre lo ya implementado de la v1.0): solo cambia el **aspecto de la barrera** (secciones 1, 2, 8 y 10). La B original parecía una red de tenis: se sustituye la cuadrícula completa por **dos franjas cian gruesas** (arriba y abajo) con un relleno ligero entre ellas. Colisiones, destello, sacudida y recolocación quedan igual.

Se apoya en lo que ya existe: `Track` / `TrackDefinition` (eje `Centerline`, ancho de pista, malla de líneas), `Player` / `Spaceship` (conducción), la cámara de cabina y la exterior (tecla C), `BoostSystem` (o como se llame la lógica del boost), `RaceSession` / `RaceTracker`, `GameSession` (modo), `Palette`.

Cambios respecto al documento maestro y a specs anteriores:
- Los **daños** (3 niveles acumulativos e indicador) **no** entran: irán en una spec aparte que usará el nivel de impacto calculado aquí.
- Un impacto **no cancela el boost** (ver 4.5).

Rama: `feature/barriers` (sale de `develop`, con `feature/ghost` ya mergeada).

Todo lo demás del documento maestro (principios, flujo por etapa, SOLID, pruebas, commits tras validación) sigue aplicando.

---

## 1. Resumen

- Cada borde de la pista lleva un **muro de 3 m** con una **franja cian gruesa arriba** (de 2,4 a 3 m) y otra **abajo** (de 0 a 0,4 m), y entre ellas un relleno ligero: una línea azul tenue a media altura y verticales azules cada 4 m. Sin huecos, en todo el circuito, generado por código.
- La nave **no puede atravesar la barrera**. Al tocarla se calcula un **nivel de impacto** según la velocidad con la que se va contra ella (velocidad × ángulo):
  - **Roce**: se desliza pegada a la barrera, con un pequeño desvío y una pequeña pérdida de velocidad.
  - **Medio**: rebote y pérdida de velocidad.
  - **Fuerte**: rebote, pérdida grande de velocidad y control reducido durante un momento.
- **Destello naranja** en el tramo de barrera golpeado, que se apaga en una fracción de segundo.
- **Sacudida de cámara** corta en impactos medios y fuertes, más suave en cabina.
- **Recolocación automática** si la nave se queda atascada contra la barrera o acaba fuera de la pista.
- Mismas reglas en RACE y TIME TRIAL, y para todas las naves. El fantasma no colisiona.

---

## 2. Barrera

### 2.1 Geometría

- Una barrera a **cada lado** de la pista, siguiendo el eje (`Centerline`), a **0,5 m por fuera** del borde de la pista (desplazamiento lateral = ancho de pista / 2 + 0,5 m).
- Continua en todo el circuito cerrado, **sin huecos ni solapes** en el cierre de la vuelta.
- Líneas horizontales (siguen toda la barrera):

| Elemento | Altura | Color | Opacidad | Grosor |
|---|---|---|---|---|
| Base (borde exterior de la franja inferior) | 0 m | **Cian #36E7FF** | 100 % | **Grueso** |
| Borde interior de la franja inferior | 0,4 m | Cian #36E7FF | ~85 % | Normal |
| Línea media | 1,4 m | Azul #245BFF | ~50 % | Normal |
| Borde interior de la franja superior | 2,4 m | Cian #36E7FF | ~85 % | Normal |
| Remate (borde exterior de la franja superior) | 3 m | **Cian #36E7FF** | 100 % | **Grueso** |

- Líneas verticales, todas cada **4 m** y alineadas entre sí:

| Elemento | De → a | Color | Opacidad |
|---|---|---|---|
| Travesaños de la franja inferior | 0 → 0,4 m | Cian #36E7FF | ~60 % |
| Verticales del relleno | 0,4 → 2,4 m | Azul #245BFF | ~40 % |
| Travesaños de la franja superior | 2,4 → 3 m | Cian #36E7FF | ~60 % |

- **Grueso** = aproximadamente el **doble** de grosor visible que una línea normal. Si la malla de líneas actual no admite grosor (líneas de 1 px), se hace con el mismo sistema que ya use el proyecto para líneas gruesas o, si no hay ninguno, con **dos líneas paralelas muy juntas** (separación ~4 cm en vertical). Si nada de eso queda bien, **se avisa** antes de cambiar cómo se dibujan las líneas.
- La separación de 4 m se **ajusta** para que quepa un número entero de tramos en la vuelta (sin un último hueco raro en el cierre).
- En las curvas, las líneas horizontales siguen la curva con la misma resolución que el borde de la pista (sin esquinas visibles).
- Las barreras **son las mismas en todos los circuitos** (se generan con estos parámetros, no a mano).
- Si la barrera se cruza visualmente con la meta, los checkpoints o las marcas de parrilla y queda mal, **se avisa** antes de cambiar nada de esos elementos.

### 2.2 Dibujo

- Se generan en `Track` (o en una clase de construcción que use `Track`) como **malla de líneas**, igual que la pista: nada de un objeto por poste.
- Colores desde `Palette`, con roles derivados de los colores base: `BarrierBand` (cian, bordes exteriores gruesos), `BarrierBandInner` (cian ~0,85), `BarrierBandTie` (cian ~0,6, travesaños), `BarrierFill` (azul ~0,5, línea media), `BarrierPost` (azul ~0,4, verticales del relleno), `BarrierFlash` (naranja). Los roles de la v1.0 que ya no se usen (`BarrierTop`, `BarrierGrid`) se quitan.
- Mismo brillo (glow) que el resto de la pista.

## 3. Detección de colisión

- **Sin físicas de Godot** para esto (nada de RigidBody ni cuerpos de colisión en la barrera): la barrera es un **límite lateral respecto al eje**, igual que los límites actuales, que se sustituyen.
- Cada frame, con la posición de la nave se obtiene el punto más cercano del eje, la tangente y el **desplazamiento lateral** de la nave (con signo: izquierda / derecha).
- Hay contacto cuando: |desplazamiento lateral| + **semiancho de colisión de la nave** ≥ desplazamiento lateral de la barrera.
- Semiancho de colisión de la nave: **1,9 m** (parámetro; ajustar a la nave real).
- Al haber contacto, la nave se **recoloca justo en el límite** (nunca queda dentro de la barrera, ni un frame) y se aplica la respuesta de la sección 4.
- La detección tiene que ser fiable a velocidad máxima con boost: si en un frame la nave pasaría de dentro a fuera del límite, cuenta como contacto (no se la puede "saltar").

## 4. Respuesta al impacto

### 4.1 Nivel de impacto

Se calcula con la **velocidad perpendicular a la barrera** en el momento del contacto: la parte de la velocidad que va "contra" la barrera (velocidad × seno del ángulo entre la dirección de la nave y la barrera).

| Velocidad perpendicular | Nivel |
|---|---|
| Menos de 30 km/h | **Roce** |
| De 30 a 90 km/h | **Medio** |
| 90 km/h o más | **Fuerte** |

Ejemplos orientativos: a 300 km/h, unos 6° contra la barrera es roce, unos 12° es medio y desde unos 18° es fuerte. Los umbrales son valores iniciales para ajustar probando.

- Un nuevo impacto **medio o fuerte** contra el mismo lado no se vuelve a contar hasta pasados **0,3 s** (evita dobles impactos por un mismo golpe). El roce es continuo y no tiene esa espera.
- El nivel se expone (para la futura spec de daños, el destello y la cámara).

### 4.2 Roce

- La nave **no rebota**: se le quita la velocidad perpendicular y **se desliza** a lo largo de la barrera.
- **Pequeño desvío**: la dirección de la nave se endereza hacia la de la barrera (se le quita un **30 %** del ángulo con el que iba contra ella).
- **Pérdida de velocidad**: **4 %** al entrar en contacto y, mientras siga rozando, **20 km/h por segundo** de fricción.
- El control no cambia.

### 4.3 Medio

- **Rebote**: la velocidad perpendicular se invierte con un **40 %** de su valor (restitución 0,4), es decir, la nave sale despedida hacia dentro de la pista.
- La dirección de la nave **se gira hacia fuera** de la barrera: queda con un ángulo hacia dentro igual a la **mitad** del ángulo con el que entró.
- **Pérdida de velocidad**: **20 %** de la velocidad que llevaba.
- El control no cambia.

### 4.4 Fuerte

- **Rebote**: restitución **0,5**.
- Dirección girada hacia fuera como en el medio.
- **Pérdida de velocidad**: **45 %**.
- **Corrección temporal**: durante **1 s**, el giro y la aceleración funcionan al **50 %** y vuelven al 100 % de forma lineal a lo largo de ese segundo. El freno funciona siempre al 100 %.
- Si llega otro impacto fuerte durante la corrección, la corrección **vuelve a empezar** (no se suman).

### 4.5 Boost y estados de la carrera

- Un impacto **no cancela el boost** ni devuelve cargas.
- Las pérdidas de velocidad de los impactos se aplican **siempre**, también durante la subida y el mantenimiento del boost. Si el impacto llega durante el mantenimiento, el mantenimiento sigue, pero **desde la velocidad que quede tras el impacto**. Si llega durante la subida, la subida sigue sumando lo que le falte.
- Las colisiones funcionan en todos los estados en los que la nave se mueve: **En carrera**, vuelta de salida de TIME TRIAL y **Terminada** (la nave sigue rodando sola). En la previa / READY? la nave está quieta y no hace falta comprobar nada.
- Rozar o chocar no afecta a vueltas, checkpoints ni récords.

### 4.6 Nave fantasma

- El fantasma **no colisiona** con nada (ya lo dice su spec). Si al reproducirse atraviesa la barrera por la interpolación, da igual.

## 5. Destello en la barrera

- Al haber contacto, el tramo de barrera de ese lado alrededor del **punto de impacto** (el punto del eje donde está la nave, proyectado sobre la barrera) se pinta en **naranja #FF9D32** con un halo más fuerte.
- El naranja es más intenso en el punto de impacto y se va apagando hacia los lados, mezclándose con el color normal de cada línea, hasta desaparecer en el **radio** del destello.

| Nivel | Radio (sobre el eje) | Intensidad máxima | Duración |
|---|---|---|---|
| Roce | 3 m | 40 % | Mientras dure el roce, y se apaga en 0,2 s al separarse. Sigue a la nave mientras se desliza |
| Medio | 7 m | 100 % | 0,4 s, apagándose lineal |
| Fuerte | 10 m | 100 % | 0,6 s, apagándose lineal |

- Afecta a **todas las líneas** de la barrera dentro del radio (franjas, travesaños, línea media y verticales).
- Un destello por lado: un impacto nuevo en el mismo lado **sustituye** al anterior.
- Sin partículas, chispas ni destellos de pantalla completa.
- Implementación libre siempre que no se rehaga toda la malla de la barrera cada frame: por ejemplo, una malla pequeña aparte con las líneas del tramo afectado, o un shader con la posición, radio e intensidad del destello.
- La pausa lo congela.

## 6. Sacudida de cámara

- Solo en impactos **medio** y **fuerte** (el roce no sacude).
- Desplazamiento corto de la cámara (no de la nave) que **se amortigua** hasta cero, más un pequeño giro (roll).

| Nivel | Amplitud (exterior) | Roll (exterior) | Duración |
|---|---|---|---|
| Medio | 0,06 m | 0,8° | 0,25 s |
| Fuerte | 0,15 m | 2° | 0,4 s |

- En **cabina**, todo al **50 %** (para no marear).
- Movimiento de vibración rápido (por ejemplo, ruido o senoidal de ~25 Hz) con caída suave, no un salto único.
- Un impacto nuevo **sustituye** la sacudida en curso si es más fuerte; si es igual o más débil, se ignora.
- No cambia la orientación ni la trayectoria de la nave, ni el HUD.
- Cambiar de cámara (C) durante la sacudida no la rompe: sigue con la amplitud de la cámara nueva.
- La pausa la congela.

## 7. Recolocación automática

### 7.1 Cuándo

- **Atasco**: la nave está **en contacto con la barrera** y va a **menos de 10 km/h** durante **3 s** seguidos.
- **Fuera de la pista**: la nave está más allá de la barrera (desplazamiento lateral mayor que el de la barrera + 2 m). No debería pasar nunca con la sección 3; es una red de seguridad. Recoloca **al momento**.
- Solo cuando la nave tiene control: **En carrera** y TIME TRIAL tras el `GO!` (incluida la vuelta de salida). No en la previa / READY? ni en **Terminada**.
- Pararse en mitad de la pista **sin** tocar la barrera **no** recoloca.

### 7.2 Qué hace

- La nave se coloca **al instante** en el **punto del eje más cercano**, centrada, mirando en el sentido de la marcha, con **velocidad 0**.
- Sin animación ni aviso en el HUD en esta versión.
- La recolocación **no** cuenta como cruce de checkpoint ni de meta (no avanza ni retrocede vueltas).
- El boost no cambia (cargas, cooldown e impulso en curso siguen igual).
- Se cancela la corrección temporal de un impacto fuerte, si la había.
- En TIME TRIAL la grabación del fantasma sigue sin cortarse.
- El penalizador es solo el tiempo perdido.

## 8. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Separación de la barrera respecto al borde | 0,5 m | circuito |
| Altura de la barrera | 3 m | circuito |
| Alto de las franjas (superior e inferior) | 0,4 m inferior / 0,6 m superior | circuito |
| Altura de la línea media | 1,4 m | circuito |
| Separación entre verticales y travesaños | 4 m | circuito |
| Grosor de los bordes gruesos | ~2× línea normal | circuito |
| Semiancho de colisión de la nave | 1,9 m | nave |
| Umbral roce → medio | 30 km/h | colisiones |
| Umbral medio → fuerte | 90 km/h | colisiones |
| Enderezado del roce | 30 % | colisiones |
| Pérdida al entrar en roce | 4 % | colisiones |
| Fricción mientras roza | 20 km/h por s | colisiones |
| Restitución medio / fuerte | 0,4 / 0,5 | colisiones |
| Pérdida de velocidad medio / fuerte | 20 % / 45 % | colisiones |
| Duración de la corrección (fuerte) | 1 s | colisiones |
| Control durante la corrección | 50 % | colisiones |
| Espera entre impactos en el mismo lado | 0,3 s | colisiones |
| Radio, intensidad y duración del destello por nivel | tabla de la sección 5 | barrera / circuito |
| Amplitud, roll y duración de la sacudida por nivel | tabla de la sección 6 | cámara |
| Factor de sacudida en cabina | 0,5 | cámara |
| Velocidad de atasco | 10 km/h | recolocación |
| Tiempo de atasco | 3 s | recolocación |
| Margen de "fuera de pista" | 2 m | recolocación |

- Mismos valores para todos los circuitos y todas las naves.
- Usar la misma conversión km/h ↔ unidades de Godot que ya usa el HUD.

## 9. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin dependencias de nodos de Godot (tipos propios o `System.Numerics`), para poder probarla:

- **Geometría de la barrera** (puro): dada la longitud de la vuelta y los parámetros, devuelve las distancias de las verticales (con la separación ajustada) y las alturas de las líneas (base, bordes de las franjas, línea media, remate). `Track` la usa para construir la malla.
- **Colisión con la barrera** (puro): entrada = desplazamiento lateral, semiancho de la nave, desplazamiento de la barrera, velocidad (componente a lo largo de la barrera y perpendicular) y dirección relativa. Salida = si hay contacto, lado, nivel, posición lateral corregida, nueva velocidad y nueva dirección relativa.
- **Clasificación** (puro): velocidad perpendicular → roce / medio / fuerte.
- **Corrección temporal** (puro): estado + `Update(delta)`, factor de control de 0,5 a 1.
- **Destello** (puro): por lado, punto de impacto, nivel y tiempo; devuelve la intensidad en una distancia dada a lo largo del eje.
- **Sacudida** (puro): estado + `Update(delta)`, devuelve desplazamiento y roll; regla de sustitución por nivel.
- **Detector de atasco / fuera de pista** (puro): estado + `Update(delta, contacto, velocidad, desplazamiento lateral)`, avisa una vez cuando hay que recolocar.
- **`Track`**: expone el punto más cercano del eje, la tangente, el desplazamiento lateral y el desplazamiento de la barrera (probablemente ya tiene parte de esto para la parrilla y la salida lanzada). Dibuja la barrera y el destello.
- **`Player`** (controlador de la nave): llama a la colisión cada frame y aplica el resultado; aplica el factor de control. Quita los límites laterales antiguos (no dejar los dos sistemas a la vez). Si hace falta tocar `Spaceship` o cómo se guardan velocidad y dirección en `Player`, **se avisa antes**.
- **Cámaras**: leen la sacudida; no conocen la colisión.
- **`Main.cs`**: conecta colisión, destello, cámara y recolocación, y decide en qué estados se recoloca. Sin reglas dentro.

### Pruebas unitarias mínimas

Geometría:
1. Con 1000 m de vuelta y 4 m: 250 verticales. Con 1001 m: separación ajustada y número entero, sin hueco final distinto.
1b. Alturas por defecto: base 0, franja inferior 0,4, línea media 1,4, franja superior 2,4, remate 3.

Colisión y clasificación:
2. Nave a 1 m del límite: sin contacto.
3. Nave pasando el límite: contacto, y la posición lateral corregida queda justo en el límite (no dentro).
4. Nave que en un solo paso iría de dentro a fuera de la barrera: contacto (no se la salta).
5. Velocidad perpendicular 20 km/h → roce; 60 → medio; 120 → fuerte. 30 → medio; 90 → fuerte.
6. Roce: sin velocidad perpendicular tras el contacto, pierde un 4 %, y el ángulo se reduce un 30 %.
7. Medio: velocidad perpendicular invertida al 40 %, pierde un 20 %.
8. Fuerte: velocidad perpendicular invertida al 50 %, pierde un 45 %, y arranca la corrección.
9. Dos impactos medios en el mismo lado separados 0,1 s cuentan como uno; separados 0,5 s, como dos.

Corrección:
10. Justo tras un fuerte, factor 0,5; a los 0,5 s, 0,75; a 1 s, 1.
11. Otro fuerte a los 0,5 s reinicia a 0,5.

Destello:
12. Medio: intensidad 1 en el punto de impacto en t = 0; 0 a 7 m; 0 a los 0,4 s.
13. Un impacto nuevo en el mismo lado sustituye al anterior; en el otro lado conviven.

Sacudida:
14. Tras un medio, amplitud > 0 y 0 a los 0,25 s. Un medio durante un fuerte se ignora; un fuerte durante un medio lo sustituye.

Recolocación:
15. Contacto y 5 km/h durante 2,9 s → no recoloca; a 3 s → recoloca, una sola vez.
16. 5 km/h sin contacto durante 10 s → no recoloca.
17. Fuera de pista → recoloca al momento.
18. Si deja de haber contacto o sube de 10 km/h, el contador vuelve a 0.

Las pruebas actuales siguen pasando.

## 10. Prueba manual

1. Arrancar RACE: se ven las barreras a los dos lados, como en el lienzo (*Mejora 1 · B reforzada · franjas gruesas*): franja cian gruesa arriba y abajo, relleno azul tenue en medio, y ya no parece una red. En todo el circuito, sin huecos en el cierre de la vuelta ni esquinas raras en las curvas. Mirar también en la cámara exterior.
2. Comprobar meta, checkpoints y parrilla junto a la barrera: si algo se pisa feo, avisar.
3. Acercarse en paralelo y tocar la barrera: roce, la nave se desliza, pierde algo de velocidad y hay un brillo naranja suave que la acompaña. No sacude.
4. Ir contra la barrera con algo de ángulo: rebote, pérdida clara, destello (también en las franjas) y sacudida corta.
5. Ir contra la barrera a mucha velocidad y con bastante ángulo: rebote fuerte, gran pérdida, el giro y la aceleración se notan flojos un segundo, destello más largo y sacudida más fuerte.
6. Repetir 3–5 en cabina: la sacudida es más suave pero se nota.
7. Con boost en mantenimiento, chocar: pierde velocidad pero el boost no se corta.
8. A máxima velocidad con boost, ir de lado contra la barrera: la nave nunca la atraviesa.
9. Quedarse parado contra la barrera (o empujando contra ella): a los 3 s la nave aparece en el centro de la pista, parada y mirando hacia delante. La vuelta y los checkpoints no cambian.
10. Pararse en mitad de la pista sin tocar nada: no recoloca.
11. Pausar justo tras un impacto: destello y sacudida congelados; al continuar, terminan.
12. TIME TRIAL: lo mismo en la vuelta de salida y en vueltas cronometradas. El fantasma atraviesa la barrera sin efectos.
13. Terminar una carrera pegado a la barrera: la nave sigue frenándose sola y puede rozar, pero no se recoloca.

## 11. Fuera de alcance

- Daños de la nave y su indicador (spec aparte).
- Colisiones entre naves (no hay rivales).
- Que un impacto cancele el boost.
- Chevrones en la franja superior (descartados por ahora; quedan en el lienzo como *Mejora 2* por si se recuperan).
- Partículas, chispas, estela, destellos de pantalla completa.
- Sonido y vibración del mando.
- Aviso en el HUD al recolocar, animación o fundido de recolocación.
- Detección de sentido contrario (wrong way).
- Caídas, huecos o desniveles en la pista.
- Barreras distintas por circuito o por tramo, aberturas, carriles de boxes.
- Fondo del circuito.
- Recolocación manual con una tecla.
