# VECTORY — Spec: Efectos 3 · Aterrizaje

Versión 1.1 · 2026-11-18 · Estado: **implementada** (etapa 1, versión 0.23.0)

Tercera spec de efectos de nave. Al aterrizar tras una cresta, la nave saca un chorro corto de **chispas** desde la panza y levanta **polvo** a ras de suelo. Todo depende de lo fuerte que caiga. Es **solo visual**: no cambia el aterrizaje, la sacudida ni la compresión de la spec `pista-3d`.

Plan de las specs de efectos de nave:

1. Choques (hecha).
2. Derrape (hecha).
3. **Aterrizaje** (esta).
4. Humo del daño fuerte.

Maqueta de referencia: **`designs/efectos-aterrizaje.html`** («VECTORY · Efectos de aterrizaje · Maqueta»). Se decidió con **ONDA · NO**, **CHISPAS · SÍ**, **LEVITACIÓN · NORMAL** y **POLVO · SÍ**. La onda (anillo y rombo) queda descartada. El **destello del brillo de levitación** queda **aplazado**: puede volver más adelante. Los valores de esta spec son los de la maqueta. Si algo no cuadra entre esta spec y la maqueta, **manda la maqueta** y se avisa. La pista, la cresta, la nave y la cámara de la maqueta son aproximadas: no son parte de esta spec.

Se apoya en lo que ya existe:
- El **aterrizaje** de la spec `pista-3d` (sección 3.4): el momento en que la nave toca la superficie y su **velocidad de caída** contra ella. La sacudida y la compresión **no cambian**.
- Las **chispas** y el **humo** de la spec `efectos-1-choques`: simulación, aspecto, colores, máximos y PARTICLES.
- Los **manifiestos** de los cascos 3D (semiancho y largo de cada nave).
- La pausa, la recolocación y `Palette`.

Cambios respecto a specs anteriores:
- Sustituye el punto «efectos por aterrizaje» del fuera de alcance de `efectos-velocidad`.

Rama: `feature/fx-landing`, que sale de `develop` con `feature/fx-collisions` ya mergeada (`feature/fx-drift` no hace falta). Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la etapa 1.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación. Nada de destellos a pantalla completa.

**Cambios de la v1.1** (al implementar, decisiones del usuario):
- Las **chispas nacen bajo la panza real** de la nave (lo más bajo de su casco 3D, con su inclinación), 6 cm por debajo, no a 6 cm de la pista: la nave flota a 2 m y en la pista se verían separadas de ella, como pasó con el derrape. Siguen saliendo hacia fuera y hacia arriba por la normal de la superficie.
- Las **distancias de lado del polvo** (0,5–1,8 m) son de la nave de la maqueta (1,87 m de semiancho): se **escalan con el semiancho** de cada nave. El polvo sigue a 0,3 m de la pista.
- El **brillo de levitación ya no existe** (se quitó al empezar `efectos-2-derrape`): lo que esta spec dice de él no aplica.
- El humo de los choques no cambia: cada bocanada de polvo lleva sus propios valores (frenado, opacidad, entrada, forma y tope en la barrera).

---

## 1. Resumen

- Mismos rangos que la sacudida de `pista-3d`, con la **velocidad de caída** contra la superficie:

| Velocidad de caída | Nivel | Chispas | Polvo |
|---|---|---|---|
| Menos de 3 m/s | Suave | Nada | Nada |
| De 3 a 8 m/s | **Medio** | 20 | 4 bocanadas |
| 8 m/s o más | **Fuerte** | 70 + 12 brasas | 12 bocanadas |

  (Valores en HIGH.)
- **Chispas**: las de los choques (trazos naranjas), saliendo de la panza hacia los lados.
- **Polvo**: el humo en nube de los choques, más bajo y achatado, saliendo hacia los lados.
- **PARTICLES**: 25 / 50 / 100 % de chispas y polvo.
- Para todas las naves, en RACE y en TIME TRIAL. El fantasma, no.
- Etapas:
  - **0** · Inventario.
  - **1** · Chispas y polvo.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Dónde se detecta hoy el **aterrizaje** (spec `pista-3d`), qué datos da (velocidad de caída, punto, normal de la superficie, velocidad de la nave) y si se puede escuchar desde fuera para las 8 naves, como los choques.
2. Cómo se le pide a la simulación de **chispas** y al **humo** de `efectos-1-choques` que emitan con otros valores desde otro sitio, sin tocarlos.
3. Cómo se obtienen la **altura y la normal de la superficie** en un punto cualquiera, para que el polvo y las chispas salgan pegados a la pista en pendiente.
4. El **semiancho** y el **largo** de cada nave en el manifiesto (VECTOR, BLADE y HAMMER).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Chispas

Usan la **misma simulación, el mismo aspecto en trazos y los mismos colores** que las chispas de `efectos-1-choques` (gravedad, aire, rebote en el suelo y en la barrera, trazo, de blanco cálido a naranja y rojo oscuro, y brasas en naranja). Comparten su **máximo de 3000** chispas vivas.

| Parámetro | Medio | Fuerte |
|---|---|---|
| Chispas (HIGH) | **20** | **70** |
| Brasas (HIGH) | 0 | **12** |

Cada chispa:

- **Sitio**: un punto al azar bajo la **panza**, en coordenadas de la nave: de lado, hasta el **80 % del semiancho** a cada lado; a lo largo, desde 2 m por delante del centro hasta la popa. A **6 cm** sobre la superficie.
- **Velocidad**:
  - **De lado**, alejándose del centro de la nave por el lado donde nace: 3–9 m/s (brasas, 1–3 m/s), ±1 m/s aleatorio.
  - **Hacia arriba** (normal de la superficie): 1–5 m/s (brasas, 0,5–2 m/s).
  - **A lo largo**: el **70 %** de la velocidad de la nave, con un aleatorio de 2 m/s más a 4 m/s menos.
- **Vida**: 0,2–0,6 s. Brasas, 0,9–1,4 s.
- **PARTICLES**: 25 / 50 / 100 %, redondeando, con **al menos 1** chispa en el medio y en el fuerte. Las brasas pueden quedarse en 0.
- Sin destello en el punto de contacto (el de los choques no se usa aquí).

Las medidas de la panza son de la maqueta. En cada nave se ajustan con su semiancho y su popa (inventario, punto 4); los 2 m por delante no cambian. **v1.1**: a 6 cm por debajo de la panza real, no de la superficie.

## 4. Polvo

Es el **humo en nube** de `efectos-1-choques` (sprites suaves, mezcla normal), con otros valores:

| Parámetro | Medio | Fuerte |
|---|---|---|
| Bocanadas (HIGH) | **4** (mínimo 1) | **12** (mínimo 2) |
| Vida | 0,9–1,3 s | 1,1–1,6 s |
| Radio | 0,5 → 1,6 m | 0,6 → 2,4 m (× 0,8–1,2) |

Cada bocanada:

- **Sitio**: alternando un lado y otro de la nave, a **0,5–1,8 m** del centro, a lo largo desde 2,5 m por delante hasta 2 m por detrás del centro, a **0,3 m** sobre la superficie. **v1.1**: las distancias de lado, × semiancho de la nave / 1,87.
- **Aparece** con un retraso aleatorio de 0–0,06 s.
- **Velocidad**:
  - de lado, hacia fuera por su lado: 2–6 m/s;
  - hacia arriba: 0,2–0,6 m/s;
  - a lo largo: el **50 %** de la velocidad de la nave, ±1 m/s.
- **Se frena**: 2,2 por segundo en horizontal y 0,6 por segundo en vertical.
- **No pasa la barrera**: si llega a 0,5 m de ella, se queda ahí sin velocidad lateral.
- **Forma**: más ancha que alta (ancho 2 × radio, alto 1,4 × radio). El radio crece con la raíz de la edad.
- **Color**: gris azulado **#4A5670**, el del humo de los choques, con opacidad **0,38** × (1 − `u`)^1,3. Entra en 0,08 s.
- Comparte el **máximo de bocanadas** del humo de los choques (120).
- **PARTICLES**: 25 / 50 / 100 %, con los mínimos de la tabla.

## 5. Interacción con otros sistemas

- **La sacudida y la compresión** de `pista-3d` siguen igual y salen en los mismos aterrizajes.
- **El brillo de levitación**: ya no existe (v1.1).
- **Derrape**: si se aterriza derrapando, salen estos efectos y, si sigue en derrape, vuelven los de `efectos-2-derrape` con un trazo nuevo.
- **Barrera**: si al aterrizar también se toca la barrera, salen los dos efectos.
- **Fantasma**: nada.
- **Rivales**: los mismos efectos cuando aterrizan.
- **Cabina**: los efectos están en el mundo. Si en cabina tapan al aterrizar, **se avisa**.
- **Pausa**: todo congelado.
- **Recolocación**: no borra lo que ya hay.
- **RESTART** o salir de la carrera: se borra todo.

## 6. Parámetros en el Inspector

Van al **objeto de ajustes de efectos**, en un bloque propio de aterrizaje.

| Parámetro | Valor |
|---|---|
| Umbrales suave → medio → fuerte | 3 / 8 m/s (los de `pista-3d`) |
| Chispas medio / fuerte | 20 / 70 |
| Brasas medio / fuerte | 0 / 12 |
| Chispas: zona de la panza | 80 % del semiancho · de 2 m por delante a la popa · 6 cm |
| Chispas: de lado / arriba | 3–9 m/s / 1–5 m/s (brasas 1–3 / 0,5–2) |
| Chispas: a lo largo | 70 % de la nave, de +2 a −4 m/s |
| Chispas: vida | 0,2–0,6 s (brasas 0,9–1,4 s) |
| Polvo medio / fuerte | 4 / 12 (mínimos 1 / 2) |
| Polvo: sitio | 0,5–1,8 m del centro · de +2,5 a −2 m · 0,3 m |
| Polvo: de lado / arriba / a lo largo | 2–6 / 0,2–0,6 m/s / 50 % ±1 |
| Polvo: frenado horizontal / vertical | 2,2 /s / 0,6 /s |
| Polvo: vida medio / fuerte | 0,9–1,3 s / 1,1–1,6 s |
| Polvo: radio medio / fuerte | 0,5 → 1,6 m / 0,6 → 2,4 m |
| Polvo: forma | 2 × 1,4 (ancho × alto) |
| Polvo: color / opacidad | #4A5670 / 0,38 |

Los umbrales se leen de los de `pista-3d` si ya son un parámetro; no se duplican.

## 7. Arquitectura

Respetar la estructura de carpetas y namespaces actual. Lógica pura, sin nodos de Godot:

- **Ajustes de efectos de aterrizaje** (datos): todo lo de la sección 6.
- **Nivel del aterrizaje** (puro): velocidad de caída → suave / medio / fuerte, con los umbrales de `pista-3d`. Si `pista-3d` ya tiene esta clasificación para la sacudida, **se reutiliza**.
- **Emisor del aterrizaje** (puro): con el nivel, la posición y orientación de la nave, la normal de la superficie, la velocidad de la nave, el semiancho y el largo, la calidad de partículas y un generador aleatorio inyectado, da las chispas y las bocanadas nuevas. Las mete en la **simulación de chispas** y en el **humo** de `efectos-1-choques`, que no cambian (el humo necesita admitir la forma achatada y el tope en la barrera: si no los admite, se añaden como opción y **se avisa**).
- **Nodo de efectos de choque**: escucha también los avisos de aterrizaje y llama al emisor. Sin reglas dentro.

## 8. Pruebas

### Pruebas unitarias mínimas

1. Caída de 2,9 m/s → nada. 3 → medio. 7,9 → medio. 8 → fuerte.
2. Medio en HIGH: 20 chispas, 0 brasas, 4 bocanadas. Fuerte: 70, 12 y 12.
3. LOW: medio → 5 chispas y 1 bocanada; fuerte → 18 chispas, 3 brasas y 3 bocanadas. Un nivel que daría 0 chispas da 1.
4. Todas las chispas nacen dentro de la zona de la panza y salen hacia fuera por el lado donde nacen.
5. Las bocanadas salen alternando lados, con la velocidad lateral hacia fuera.
6. Una bocanada que llega a la barrera se queda a 0,5 m de ella.
7. Con la misma semilla, los mismos efectos.
8. Con `delta` 0 (pausa) nada cambia.

Las pruebas existentes siguen pasando.

## 9. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Chispas y polvo** | Secciones 3–7 · versión | Pruebas 1–8 · Prueba manual 1–10 · Commit `feat:` |

### Prueba manual

1. TIME TRIAL, cámara exterior: pasar una cresta despacio, sin despegar o con un salto pequeño. Aterrizaje suave: nada, ni sacudida.
2. Salto medio: al tocar, unas pocas chispas hacia los lados, unas nubes grises bajas y la sacudida media de siempre.
3. Salto fuerte: chorro de chispas más grande, brasas que caen y rebotan, más polvo que se abre a los lados, y la sacudida fuerte.
4. Comparar con la maqueta (CHISPAS SÍ · POLVO SÍ · sin onda ni destello) en LENTO: que se parezcan.
5. ~~El brillo de levitación hace lo de siempre~~ (ya no existe, v1.1).
6. Aterrizar en una bajada: las chispas y el polvo salen pegados a la pista, sin hundirse ni flotar.
7. Aterrizar cerca de la barrera: el polvo no la atraviesa.
8. PARTICLES en LOW: muchas menos chispas y menos polvo.
9. Cabina: se ve y no tapa. Si tapa, avisar.
10. Pausa justo al aterrizar: congelado. RESTART: se borra.

## 10. Fuera de alcance

- **Onda** en la pista al aterrizar (descartada).
- **Destello del brillo de levitación** al aterrizar (aplazado).
- Cambios en la sacudida o en la compresión de `pista-3d`.
- Marcas en la pista al aterrizar.
- Luz de las chispas sobre la pista y las naves (iluminación del circuito).
- Sonido y vibración del mando.
