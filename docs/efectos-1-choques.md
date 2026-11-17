# VECTORY — Spec: Efectos 1 · Choques

Versión 1.0 · 2026-11-17 · Estado: **definido, listo para implementar**

Primera spec de efectos de nave. Los choques contra la **barrera** y **entre naves** sacan **chispas**, un **destello en el punto de contacto** y, en los golpes medios y fuertes, **humo**. Todo es **solo visual**: no cambia la física, el daño, la sacudida ni el destello de la barrera que ya existen.

Plan de las specs de efectos de nave:

1. **Choques** (esta).
2. Derrape.
3. Aterrizaje tras una cresta.
4. Humo del daño fuerte.

La iluminación del circuito sobre las naves queda para más adelante.

Maqueta de referencia: **`designs/efectos-choques.html`** («VECTORY · Efectos de choque · Maqueta»). Se decidió con **CHISPAS A · TRAZOS**, **HUMO B · NUBE** y **ENTRE NAVES · NARANJA**. Los puntos, los anillos y el color de las naves quedan descartados. Los valores de esta spec son los de la maqueta. Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa. La pista, la ciudad, las naves y las toberas de la maqueta son aproximados: no son parte de esta spec.

Se apoya en lo que ya existe:
- La **colisión con la barrera** de la spec `barreras-y-colisiones`: niveles (roce, medio, fuerte), lado, punto de impacto, espera de 0,3 s, destello naranja en la barrera y sacudida.
- El **contacto entre naves** de la spec `rivales-3-contacto`: niveles, normal, velocidad de cierre, **tipo de roce** (de lado o por detrás, v1.1), espera por pareja y atravesables.
- El **objeto de ajustes de efectos** de la spec `efectos-velocidad` y el ajuste **PARTICLES** (LOW / MEDIUM / HIGH) de la spec `settings`.
- `Palette` y la pausa.

Cambios respecto a specs anteriores:
- Las chispas, destellos y humo que `barreras-y-colisiones` (sección 5 y fuera de alcance), `danos` (fuera de alcance) y `rivales-3-contacto` (secciones 6 y 14) dejaban fuera **entran aquí**. El destello naranja de la barrera y la sacudida **no cambian**.
- Sustituye el punto «efectos por impactos contra barreras» del fuera de alcance de `efectos-velocidad`. El derrape y el aterrizaje siguen fuera (specs 2 y 3).

Rama: `feature/fx-collisions`, que sale de `develop`. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. **Nada de destellos a pantalla completa.**

---

## 1. Resumen

- **Chispas en trazos**: líneas cortas que se alargan con la velocidad, de blanco cálido a naranja y que se apagan. Caen, rebotan en el suelo y en la barrera, y se quedan atrás.
- **Destello en el punto de contacto**: un brillo pequeño con 6 rayos. En el fuerte, además, una **onda** que se abre.
- **Humo en nube**: bocanadas grises suaves, solo en golpes **medios** y **fuertes**.
- Entre naves, las chispas son **naranjas**, igual que contra la barrera.
- **Roce**: chispas continuas mientras dura el contacto y destello que parpadea. Sin humo.
- **PARTICLES**: chispas y humo al 25 / 50 / 100 %.
- En RACE y en TIME TRIAL (barrera). Para todas las naves, también entre dos rivales.
- Etapas:
  - **0** · Inventario.
  - **1** · Barrera.
  - **2** · Entre naves.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Qué **expone** hoy la colisión con la barrera al haber contacto: nivel, lado, punto de impacto, si es roce continuo, y velocidad de la nave en ese momento. Lo mismo para el **contacto entre naves**: nivel, normal, punto de contacto (o si hay que sacarlo de los rectángulos), tipo de roce y las dos naves. Si falta algo, se dice qué y dónde se sacaría.
2. Si esos avisos se pueden escuchar desde **un solo sitio** de la carrera (por ejemplo, un evento o una lista de impactos del paso de física), para las 8 naves.
3. Cómo dibuja hoy la spec `efectos-velocidad` las **líneas de velocidad** y las **partículas de la tobera** (malla de líneas que se actualiza, `GPUParticles3D`, `CPUParticles3D` o propio), para reutilizar lo mismo.
4. Dónde se lee hoy la **calidad de partículas** (objeto de ajustes de efectos o SETTINGS, según lo que esté mergeado).
5. Si el material de las líneas de la pista admite **mezcla aditiva** con colores por vértice, y si hay un sprite o textura de **punto suave** ya hecha.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Punto de contacto

- **Barrera**: sobre la barrera, en el lado golpeado, a la altura de la nave en ese punto del eje, a **0,75 m** sobre la pista. A lo largo del eje, en la parte de la nave que toca (si el inventario no da ese punto, el centro de la nave **1,3 m hacia la popa**). La **normal** apunta de la barrera hacia dentro de la pista.
- **Entre naves**: el punto medio del solape de los dos rectángulos, a **0,7 m** sobre la pista. En un alcance por detrás queda en el morro del que embiste; puerta con puerta, entre los dos costados.
- Con pista 3D (pendientes, peraltes, crestas), «arriba» es la **normal de la superficie** y la altura se mide sobre ella.
- **Velocidad de la nave**: la del que roza o embiste. Si es mutuo, la media de las dos.

## 4. Chispas

### 4.1 Cantidad y vida

Valores en **HIGH**:

| Contacto | Nivel | Chispas | Velocidad | Vida | Brasas |
|---|---|---|---|---|---|
| Barrera | Roce | **140 por segundo** mientras dure | 3–9 m/s | 0,2–0,5 s | — |
| Barrera | Medio | **55** de golpe | 5–15 m/s | 0,3–0,7 s | 8 |
| Barrera | Fuerte | **130** de golpe | 7–22 m/s | 0,35–0,9 s | 30 |
| Naves | Roce de lado | **100 por segundo** mientras dure | 2–7 m/s | 0,2–0,45 s | — |
| Naves | Roce por detrás | **Ninguna** | — | — | — |
| Naves | Medio | **45** de golpe | 4–12 m/s | 0,3–0,65 s | 6 |
| Naves | Fuerte | **110** de golpe | 6–18 m/s | 0,35–0,85 s | 24 |

- El **roce por detrás** (ir pegado detrás de otra nave, spec `rivales-3-contacto` v1.1) no saca chispas: no hay fricción y llenaría la pantalla todo el rato.
- Las **brasas** son chispas lentas (1–4 m/s) que duran más (1–1,6 s), solo en naranja.
- El roce continuo reparte las chispas con un acumulador por frame, para que no dependan de los fps.
- Máximo de chispas vivas: **3000** en HIGH, para todas las naves juntas (parámetro). Es un grupo aparte de las partículas de la tobera. Si se llena, las nuevas no salen (no se quitan las vivas).

### 4.2 Dirección

Cada chispa sale con una dirección que se normaliza y se multiplica por su velocidad:

| Contacto | Hacia la normal (dentro de la pista) | Hacia arriba | Hacia la popa | Aleatorio |
|---|---|---|---|---|
| Barrera | 0,2–1 | 0,1–0,8 | 0–0,9 | ±0,45 |
| Naves, de lado | ±0,1–0,6 (a cualquiera de los dos lados) | 0,2–1 | 0,3–1 | ±0,4 |
| Naves, alcance por detrás | ±0,9 a los lados (aleatorio) | 0,3–1 | −0,2–0,6 | ±0,3 a ±0,4 |

- **Velocidad heredada**:
  - **Barrera**: el **50 %** de la velocidad de la nave (la barrera está quieta).
  - **Entre naves**: el **92 %**, más un extra de **4–12 m/s hacia atrás**, para que las chispas viajen con las naves y se queden atrás poco a poco.
- Salen del punto de contacto con un desorden de ±0,15 m (±0,3 m a lo largo de la pista).

### 4.3 Movimiento

- **Gravedad**: 14 m/s² (más que la real, para que caigan rápido y se vean).
- **Aire**: la velocidad se amortigua 0,8 por segundo.
- **Suelo**: al tocarlo rebotan, con un 30 % de la velocidad vertical y un 60 % de la horizontal.
- **Barrera**: no la atraviesan. Rebotan hacia dentro con un 30 % de la velocidad lateral.
- No chocan con las naves.

### 4.4 Aspecto

- **Trazo**: una línea desde la chispa hacia atrás, de largo = su velocidad **relativa a la nave del jugador** × **0,03 s**, con un máximo de **2,2 m**. Así, a 250 km/h, se ven como rayas que se quedan atrás. La cabeza tiene el color de la chispa y la cola se apaga a negro.
- **Color** según la edad (`u` = edad / vida):
  - de 0 a 0,3: de blanco cálido (1; 0,95; 0,82) a naranja (1; 0,6; 0,18);
  - de 0,3 a 1: de naranja a rojo oscuro (0,85; 0,22; 0,04);
  - por un brillo de (1 − `u`)^1,1.
- **Brasas**: naranja (1; 0,55; 0,14), con brillo 0,85 × (1 − `u`).
- Colores desde `Palette` si se pueden derivar. Si no, como valores de los ajustes de efectos.
- Mezcla **aditiva**, sin escribir profundidad, con el glow de siempre.
- Entre naves, **mismos colores** que contra la barrera.

## 5. Destello en el punto de contacto

- Un **brillo** redondo y suave (sprite con mezcla aditiva) de color blanco cálido **#FFE2B0**, más **6 rayos** en ángulos aleatorios. Los dos miran siempre a la cámara.
- **Intensidad** = (1 − `u`)², con `u` = edad / duración.
- **Brillo**: tamaño × (0,7 + 0,5·`u`): se abre un poco mientras se apaga.
- **Rayos**: desde 0,1 m del centro hasta 1,3 × tamaño × (1 − 0,4·`u`). Blanco cálido en el centro y se apagan hacia la punta.

| Nivel | Tamaño | Duración | Extra |
|---|---|---|---|
| Roce | 0,9 m | 0,06 s | Mientras dura el roce, aparece al azar unas **30 veces por segundo** con una intensidad aleatoria de 0,5–1: parpadea |
| Medio | 2,4 m | 0,16 s | — |
| Fuerte | 3,8 m | 0,26 s | **Onda** (5.1) |

- Contra la barrera, el destello se queda **quieto** donde fue el golpe. Entre naves, se mueve con ellas (92 % de su velocidad).
- Como mucho **8 destellos** a la vez. Si hay más, se quita el más viejo.
- No es un destello de pantalla completa: es un objeto en el mundo, del tamaño de la tabla, que se ve más pequeño cuanto más lejos está.

### 5.1 Onda del fuerte

- Un círculo de líneas (32 segmentos) de cara a la cámara, centrado en el punto de contacto.
- Radio de **0,5 a 7 m** en **0,4 s**, abriéndose rápido al principio (1 − (1 − `v`)², con `v` = edad / 0,4 s).
- Naranja (1; 0,62; 0,2), con intensidad 0,9 × (1 − `v`). Mezcla aditiva.

## 6. Humo

Bocanadas grises suaves (sprites con textura redonda y difuminada), en mezcla **normal** (no aditiva), que miran a la cámara.

| Nivel | Bocanadas (HIGH) | Aparecen durante | Vida | Radio |
|---|---|---|---|---|
| Roce | — | — | — | — |
| Medio | **2** (mínimo 1) | 0–0,08 s | 1–1,4 s | 0,4 → 1,6 m |
| Fuerte | **10** (mínimo 2) | 0–0,3 s | 1,3–2,1 s | 0,6 → 3 m (× 0,8–1,2) |

- Cada bocanada aparece con un retraso aleatorio dentro de su ventana, así el fuerte suelta humo durante un momento y no todo de golpe.
- **Radio**: crece con la raíz de la edad (rápido al principio).
- **Movimiento**:
  - Hereda el **30 %** de la velocidad de la nave contra la barrera (más 1,2 m/s hacia dentro de la pista) y el **85 %** entre naves.
  - ±0,8 m/s aleatorio a los lados, y sube a 0,8–1,6 m/s.
  - Se frena: 1,2 por segundo en horizontal y 0,4 por segundo en vertical.
- **Color**: gris azulado **#4A5670**, con opacidad 0,42 × (1 − `u`)^1,3. Entra en 0,1 s (sin aparecer de golpe).
- Sale desde el punto de contacto con un desorden de ±0,4 m a los lados y 0–0,4 m hacia arriba.
- Como mucho **120 bocanadas** a la vez en HIGH (parámetro).
- Este humo es **solo del golpe**. El humo de la nave con daño fuerte va en la spec 4.

## 7. PARTICLES

| Ajuste | Chispas por golpe, por segundo y máximo | Humo |
|---|---|---|
| LOW | 25 % | 25 % |
| MEDIUM | 50 % | 50 % |
| HIGH | 100 % | 100 % |

- Redondeando, con los mínimos de la tabla de humo y **al menos 1 chispa** por golpe medio o fuerte. Las brasas pueden quedarse en 0.
- El destello y la onda no cambian con PARTICLES.
- Cambiar PARTICLES en plena carrera (desde la pausa) vale para las chispas y el humo **nuevos**. Las vivas siguen; si pasan del máximo nuevo, se quitan las sobrantes.

## 8. Interacción con otros sistemas

- **Todo lo de hoy sigue igual**: el destello naranja en la barrera, la sacudida de cámara (solo si el golpe es contigo), las pérdidas, el daño y la recolocación.
- **Cuándo**:
  - **Barrera**: en todos los estados en los que hay colisión (En carrera, vuelta de salida de TIME TRIAL y Terminada), para todas las naves.
  - **Entre naves**: solo en RACE y solo con contacto. Un golpe entre dos rivales también saca chispas, destello y humo.
  - Las **atravesables** (terminadas y recién recolocadas) no chocan con naves, así que tampoco sacan efectos entre naves.
- **Fantasma y naves de prueba (F6)**: nada.
- **Cabina**: los efectos están en el mundo, así que también se ven desde la cabina. Si en cabina las chispas del roce tapan demasiado, **se avisa** y se ve si hace falta bajarlas solo ahí.
- **Pausa**: se congela todo (chispas, destellos, onda y humo no avanzan ni envejecen).
- **Recolocación**: lo que ya está en el aire **no se borra**: está donde fue el golpe, no sigue a la nave.
- **RESTART / salir de la carrera**: se borra todo.
- **Espera de 0,3 s**: los golpes medios y fuertes que no cuentan por la espera (barrera por lado, naves por pareja) tampoco sacan efectos.
- **SETTINGS**: COCKPIT SHAKE no toca nada de esto. SPEED LINES tampoco.

## 9. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de choques.

| Parámetro | Valor |
|---|---|
| Chispas por nivel (barrera y naves) | Tabla de 4.1 |
| Dirección por contacto | Tabla de 4.2 |
| Velocidad heredada barrera / naves | 50 % / 92 % |
| Extra hacia atrás entre naves | 4–12 m/s |
| Brasas: velocidad / vida | 1–4 m/s / 1–1,6 s |
| Gravedad de las chispas | 14 m/s² |
| Amortiguación del aire | 0,8 /s |
| Rebote en el suelo (vertical / horizontal) | 0,3 / 0,6 |
| Rebote en la barrera | 0,3 |
| Largo del trazo | velocidad relativa × 0,03 s, máximo 2,2 m |
| Máximo de chispas vivas | 3000 |
| Destello: tamaño y duración por nivel | Tabla de 5 |
| Destello del roce: veces por segundo / intensidad | 30 / 0,5–1 |
| Onda del fuerte: radio / duración | 0,5–7 m / 0,4 s |
| Máximo de destellos | 8 |
| Humo por nivel | Tabla de 6 |
| Humo: velocidad heredada barrera / naves | 30 % / 85 % |
| Humo: subida / frenado horizontal / vertical | 0,8–1,6 m/s / 1,2 /s / 0,4 /s |
| Humo: color / opacidad | #4A5670 / 0,42 |
| Máximo de bocanadas | 120 |
| PARTICLES LOW / MEDIUM / HIGH | 25 / 50 / 100 % |

## 10. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot (tipos propios o `System.Numerics`):

- **Ajustes de efectos de choque** (datos): todo lo de la sección 9.
- **Evento de choque** (datos): tipo (barrera / naves), nivel, tipo de roce, punto, normal, «arriba», velocidad de la nave y si es el inicio de un golpe o un frame de roce.
- **Emisor de chispas** (puro): con un evento, los ajustes, la calidad de partículas, `delta` (para el roce) y un **generador aleatorio inyectado**, da las chispas nuevas (posición, velocidad, vida, si es brasa). Lleva el acumulador del roce.
- **Simulación de chispas** (pura): lista de chispas + `Update(delta)` con gravedad, aire y rebotes en suelo y barrera. Da el color, el brillo y el trazo de cada una. Necesita la altura del suelo y el límite de la barrera en cada punto: los pide a una interfaz que implementa `Track`.
- **Destellos** (puro): alta por nivel, `Update(delta)`, intensidad, tamaño, rayos y onda. Máximo de 8.
- **Humo** (puro): alta por nivel con el retraso aleatorio, `Update(delta)`, radio y opacidad.
- **Nodo de efectos de choque** (uno en la carrera): escucha los eventos de choque (inventario, punto 2), llama a la lógica y **solo dibuja**:
  - chispas: **una** malla de líneas que se actualiza cada frame, con color por vértice y mezcla aditiva (como las líneas de velocidad);
  - destellos: unos pocos sprites con mezcla aditiva y una malla de líneas para rayos y onda;
  - humo: sprites o un `MultiMeshInstance3D` de cuadrados que miran a la cámara.

  Si el inventario dice que con `GPUParticles3D` sale igual y más barato, se puede usar, **avisando antes**. Las curvas y los valores siguen saliendo de la lógica pura.
- **Colisiones**: no cambian sus reglas. Si hay que añadir el punto de contacto o el aviso por frame de roce a lo que exponen, **se avisa antes**.

## 11. Pruebas

### Pruebas unitarias mínimas

Chispas (etapa 1):
1. Barrera medio en HIGH: 55 chispas y 8 brasas. Fuerte: 130 y 30.
2. Barrera roce a 140/s: en 1 s con `delta` 1/60, salen 140 (±1). Con `delta` 1/30, las mismas.
3. LOW: medio → 14 chispas y 2 brasas; un golpe que daría 0 da al menos 1 chispa.
4. Máximo: con el grupo lleno, un golpe nuevo no saca chispas y las vivas no cambian.
5. Barrera: todas las chispas salen con la componente hacia la normal ≥ 0 y la heredada es el 50 % de la velocidad de la nave.
6. Una chispa con vida 0,5 s desaparece a los 0,5 s. Con `delta` 0 (pausa), nada cambia.
7. Una chispa que baja hasta el suelo rebota con un 30 % de la velocidad vertical. Una que pasa la barrera vuelve al límite y rebota hacia dentro.
8. Color: en `u` = 0, blanco cálido; en `u` = 0,3, naranja; en `u` = 1, brillo 0.
9. Trazo: con velocidad relativa 0, largo 0. Muy rápida, como mucho 2,2 m.
10. Con la misma semilla, las mismas chispas.

Destello y humo (etapa 1):
11. Destello medio: intensidad 1 al empezar, 0 a los 0,16 s. Fuerte: la onda mide 0,5 m al empezar y 7 m a los 0,4 s.
12. Con 9 destellos, el primero se quita.
13. Humo: roce → ninguna bocanada. Medio → 2. Fuerte → 10. En LOW, medio → 1 y fuerte → 2 (los mínimos).
14. Una bocanada con retraso no se ve hasta que pasa su retraso. A la mitad de su vida, su radio está más cerca del final que del principio.

Entre naves (etapa 2):
15. Roce de lado → chispas continuas a 100/s. Roce por detrás → ninguna.
16. Medio → 45 chispas, 6 brasas, 2 bocanadas. Fuerte → 110, 24 y 10.
17. Las chispas heredan el 92 % de la velocidad de la nave y van más hacia atrás.
18. Un golpe que no cuenta por la espera de la pareja no saca nada.

Las pruebas existentes siguen pasando.

## 12. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Barrera** | Secciones 3–9 para la barrera, en TIME TRIAL y RACE | Pruebas 1–14 · Prueba manual 1–10 · Commit `feat:` |
| **2 · Entre naves** | Lo mismo para el contacto entre naves · versión | Pruebas 15–18 · Prueba manual 11–17 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Barrera**
1. TIME TRIAL, cámara exterior: rozar la barrera en paralelo. Sale un chorro de chispas en trazos que se quedan atrás, con un destello pequeño que parpadea, junto al destello naranja de la barrera de siempre. Sin humo.
2. Ir contra la barrera con ángulo (medio): una ráfaga de chispas, un destello más grande, un par de bocanadas grises y la sacudida de siempre.
3. Golpe fuerte: ráfaga grande, brasas que caen y rebotan, destello grande con la onda y varias bocanadas que salen durante un momento.
4. Comparar con la maqueta (CHISPAS A, HUMO B) en LENTO: que se parezcan.
5. Repetir en cabina: se ven y no tapan la pista. Si tapan, avisar.
6. En ningún caso se ilumina o se pone blanca la pantalla entera.
7. Pausar justo después de un fuerte: todo congelado. Al continuar, termina.
8. Poner PARTICLES en LOW: muchas menos chispas y menos humo; el destello igual. MEDIUM: a medias.
9. Quedarse atascado contra la barrera hasta la recolocación: el humo que había se queda en su sitio y no sigue a la nave.
10. RACE con los 8: los golpes de los rivales contra la barrera también sacan efectos. Mirar los fps en la salida.

**Etapa 2 · Entre naves**
11. Rozar puerta con puerta a un rival: chispas continuas naranjas entre los dos costados, que viajan con las naves y se quedan atrás.
12. Ir pegado detrás de un rival tocándole: sin chispas.
13. Alcanzarlo por detrás (medio): ráfaga en tu morro, destello y un par de bocanadas.
14. Que te embistan de lado (fuerte): ráfaga grande, onda y humo.
15. Mirar un golpe entre dos rivales: sacan efectos, pero tu cámara no se sacude.
16. Terminar la carrera: las naves que han terminado se atraviesan y no sacan nada entre ellas.
17. F9 con CONTACT desactivado: sin contacto y sin efectos entre naves.

## 13. Fuera de alcance

- Chispas o marcas del **derrape** (spec 2).
- Efectos del **aterrizaje** tras una cresta (spec 3).
- **Humo del daño fuerte** continuo (spec 4).
- Luz de las chispas o los destellos sobre la pista y las naves, e iluminación del circuito.
- Marcas o arañazos que se queden en la barrera o en la pista.
- Piezas sueltas o restos.
- Sonido y vibración del mando.
- Efectos con el fantasma o las naves de prueba (F6).

## 14. Notas de la implementación

### Etapa 0 · Inventario (2026-11-17)

1. **Barrera**: `BarrierTouch` da contacto, lado, nivel, si el golpe cuenta (`IsNewImpact`), roce y distancia sobre el eje; la velocidad sale de `ShipDynamics.Velocity`. No dice qué parte de la nave toca (la colisión es un límite lateral): se usa el centro de la nave 1,3 m hacia la popa. Un medio o fuerte dentro de la espera ya llega como roce. **Entre naves**: `ShipContactEvent` da naves, nivel, papeles, velocidad de cierre y tipo de roce; **faltan la normal y el punto**, que hay que añadir (etapa 2).
2. Un solo sitio: `Main._PhysicsProcess`, después de `RivalField.StepPhysics` y de la barrera del jugador.
3. `efectos-velocidad` no está hecha: no hay líneas de velocidad ni partículas de tobera ni objeto de ajustes de efectos. Se dibuja como `AirLinesView` (suma con color por vértice) y con el punto suave radial del humo del casco.
4. PARTICLES estaba en COMING SOON: **se activa** (decisión del usuario) con el texto *Amount of sparks and smoke from crashes*.
5. Sí hay material aditivo con color por vértice; el punto suave es un `GradientTexture2D` radial.

### Etapa 1 · Barrera

- Lógica pura en `scripts/Effects/` (`Vectory.Effects`): `CollisionFxSettings`, `CrashEvent` (con `Barrier`, el punto de contacto), `ICrashGround` + `TrackCrashGround` (consulta local desde la última distancia de cada chispa, para no recorrer todo el eje), `SparkField` + `SparkEmitter`, `CrashFlashes`, `CrashSmoke` y `CollisionFx`, que los junta. Nodo `CollisionEffects` en `Main.tscn` con los parámetros principales en el Inspector (grupos **Chispas**, **Destello** y **Humo**); el resto, en `CollisionFxSettings`.
- Los colores de chispas, destello, onda y humo van en los ajustes, no en `Palette`: no salen de sus colores.
- **Se aparta de la spec, manda la maqueta**:
  - Prueba 5: la maqueta suma ±0,45 de desorden a la componente hacia la normal, así que alguna chispa sale un poco hacia la barrera (y rebota en ella al momento). La prueba comprueba que la media va hacia dentro y que ninguna queda fuera tras un paso.
  - Prueba 13: el redondeo de la maqueta sube las mitades (10 × 0,25 = 2,5 → **3** bocanadas en LOW con un fuerte; la spec dice 2). Igual con las chispas (130 × 0,25 → 33).
  - Humo: ±0,6 m a lo largo de la pista al salir y ±0,8 m/s también a lo largo (la maqueta), no solo a los lados.
- **Decisión del usuario tras probarla**: el destello de la barrera y el punto de contacto iban muy atrasados y desde la cabina no se sabía si se estaba rozando.
  - Los dos se adelantan **2 m** hacia el morro desde el centro de la nave (sección 3 decía 1,3 m hacia la popa, y el destello de la barrera iba en el centro): `FlashAhead` en el grupo **Destello de la barrera** del `Track` (también para los rivales) y `BarrierContactAhead` en el grupo **Punto de contacto** de `CollisionEffects`. Se probaron 4–8 m para verlo por el cristal, pero dejaba de parecer real.
  - En su lugar, la cabina nota el choque (`CockpitCrashFeel`, solo el jugador): **luz de las chispas** por el lado tocado —una luz naranja en la cabina 3D que ilumina pilar y salpicadero de ese lado y un tinte en ese borde de la pantalla—, que parpadea entre 0,5 y 1 unas 30 veces por segundo mientras se roza, se apaga en 0,1 s al dejar de rozar y da un fogonazo de 1,8 / 2,6 en 0,3 / 0,45 s con un medio / fuerte; y **temblor fino** mientras se roza (12 mm a 31 Hz, con COCKPIT SHAKE; el mundo entero y la cabina 3D a la mitad). Grupo **Cabina: choques** del `Player`.
  - **Chispas altas**: el 25 % de las chispas de la barrera sale con 4–8 m/s más hacia arriba, como si rebotara en ella, para que alguna pase por el lateral del cristal.
  - Esto amplía el fuera de alcance de la sección 13 («luz de las chispas sobre la pista y las naves»): la luz solo va dentro de la cabina.
- El humo sube en la vertical del mundo; las chispas rebotan con la normal de la superficie.
- Las mallas (chispas, rayos y onda, brillos y humo) se rehacen cada frame en `_Process`, con los cuadrados de cara a la cámara calculados en C#: la pausa lo congela todo.

### Etapa 2 · Entre naves

- `ShipContactEvent` gana `Point` (centro del solape de los dos rectángulos en planta, `ContactPoint.Between`, calculado antes de separarlas), `Normal` y `Facing` (de lado o por detrás con la regla del roce, también en los golpes). Solo para los efectos: las reglas del choque no cambian (aviso de la sección 10).
- `CrashEvent.Ships`: punto a 0,7 m sobre la superficie; la normal es la derecha de la pista (las chispas salen a cualquiera de los dos lados); dirección de lado o de alcance por detrás según `Facing`; velocidad del que embiste, o la media si es mutuo o un roce.
- Un golpe dentro de la espera de la pareja llega como roce: de lado saca las chispas del roce de ese paso; por detrás, nada. Un contacto en el que ya se separan tampoco saca nada.
- La luz y el temblor de la cabina de la etapa 1 también valen entre naves: un roce de lado o un golpe con tu nave encienden el lado de la cabina donde queda el punto de contacto.
- Comprobado con sondas en RACE: golpes medios y fuertes con rivales (destello entre las dos naves, onda y humo). A 144 Hz, 4 s de choques en la salida: 0–1 frames lentos, peor frame 15–28 ms, como antes.
- **Decisión del usuario tras probarla, toques por detrás con tu nave** (fuera no cambia nada: el roce por detrás sigue sin chispas):
  - Roce por detrás: solo un temblor en cabina a la mitad del de lado (`RearGrazeRumble` 0,5), sin luz.
  - Golpe medio o fuerte por detrás: el fogonazo no va a un lado, sino detrás si te embisten (dos luces tras el asiento, una por arriba y otra a la altura de los mandos, esta a la mitad para que no queme) o delante si embistes tú (una luz delante, que ilumina los cantos de los pilares). Más suave que el de los lados (`CrashEndLightEnergy` 0,5) y sin tinte en los bordes.

