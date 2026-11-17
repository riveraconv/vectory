# VECTORY — Spec: Rivales 4 · Personalidades y dificultad

Versión 1.1 · 2026-11-17 · Estado: **etapas 1–4 hechas**

Cuarta y última spec de rivales. Cada piloto gana una **personalidad**: cómo va en recta y en curva, cómo frena, cómo ataca y defiende, si le importa tocarte, cuándo gasta el boost y cuánto se equivoca. Llega el **rebufo** para todos, tú incluido. Y la **dificultad** FÁCIL / NORMAL / DIFÍCIL, que cambia **solo la IA**, sin *rubber banding*.

Plan de las specs de rivales:

1. Parrilla llena (hecha).
2. Clasificación y pilotos (hecha).
3. Contacto (hecha; parche 1.1 del roce por detrás).
4. **Personalidades y dificultad** (esta).

Después de esta spec va la de **SETTINGS**, que es donde se elegirá la dificultad. Hasta entonces, la dificultad se cambia en el panel de pruebas (F9).

Se apoya en lo que ya existe:
- El **catálogo de pilotos** de la spec `rivales-2-clasificacion` (20 apodos), preparado para la personalidad.
- El **conductor de rival** y la **caché de trazadas** de la spec `rivales-1-parrilla`.
- El **piloto automático** de `naves-6-equilibrio`: trazada, calibración, perfil de velocidad (con su margen) y `BalanceRunner`.
- La **trazada clara** de `rivales-3-contacto` (`CLEAR`, `PASS L/R`, `FOLLOW`), el contacto entre naves, el reparto de daño y el roce por detrás de la v1.1.
- `ShipDynamics` (`Drag`, punta), el boost por cargas, el daño, la cabina 3D, las líneas de velocidad de `efectos-velocidad`, el `Hud` y `Palette`.

Rama: `feature/rivals-personality`, que sale de `develop` con `feature/rivals-contact` y el parche `fix/rear-contact` ya mergeados. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Cambios respecto a specs anteriores:
- La regla del boost de los rivales («una carga al entrar en la recta más larga») se sustituye por las **estrategias** de la sección 4.6.
- El deslizador de **ritmo** de F9 (spec 1) pasa a ser un **ajuste global** de depuración que se suma al de la dificultad.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Seis perfiles**: equilibrado, velocista, especialista en curvas, agresivo, conservador y técnico.
- Cada uno de los 20 pilotos lleva **un perfil**, con **pequeñas variaciones** propias. El perfil va con el **piloto**, no con la nave.
- **Ningún perfil es más rápido en total**: se comprueba con `BalanceRunner`, igual que las naves.
- **Rebufo** para todas las naves: ir pegado detrás en recta da algo de punta. Se ve con un **indicador pequeño** en el HUD y con **líneas de aire** en la cabina.
- La IA **adelanta con cabeza**, **defiende**, cierra la puerta si es agresiva y gasta el boost con **estrategia**.
- **Errores** pequeños: frenar tarde, abrirse en la salida de curva y salir mal en el GO. La IA **no derrapa**.
- **Dificultad** FÁCIL / NORMAL / DIFÍCIL: cambia los márgenes, los errores, el ataque y la defensa, nunca la física. Sin *rubber banding*. Los récords no se separan por dificultad.
- Etapas:
  - **0** · Inventario.
  - **1** · Perfiles y ritmo.
  - **2** · Duelo: rebufo, adelantar, defender, contacto y boost.
  - **3** · Errores.
  - **4** · Dificultad (desde F9; SETTINGS la enlazará en su spec).

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo aplica hoy el perfil de velocidad del piloto el **margen** (97 %): si se puede tener un margen distinto para **rectas** y **curvas**, y uno para la **frenada** (cuánta frenada real usa el perfil al ir hacia atrás).
2. Si `ShipDynamics` tiene un `Drag` y una punta que se puedan modificar **por nave y por paso** sin tocar el `ShipTuning` (para el rebufo), igual que hace el daño con la punta.
3. Si existe ya alguna **reacción en la salida** (ventaja por buena reacción del documento maestro) para el jugador, y cómo arranca hoy el piloto en el GO.
4. Cómo detecta el piloto las **rectas** y los **puntos de frenada** de cada curva (la spec `selector-circuito` ya detectaba curvas). Hace falta saber, en cada momento, cuánta recta queda y a qué distancia está la siguiente frenada.
5. Si `BalanceRunner` puede correr el piloto con un **perfil** distinto y comparar perfiles con la misma nave.
6. Dónde dibujar un elemento pequeño nuevo del HUD (en persecución y en cabina) y cómo están hechas hoy las **líneas de velocidad** de la cabina, para las líneas de aire.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Perfiles y ritmo

### 3.1 Rasgos

Cada perfil es un conjunto de **rasgos**. En esta etapa entran los de ritmo; los demás se definen aquí y se usan en las etapas 2 y 3.

| Rasgo | Qué hace | Rango | Etapa |
|---|---|---|---|
| **Ritmo en recta** | Fracción de la **punta** que usa el perfil de velocidad donde manda la punta | 90–100 % | 1 |
| **Ritmo en curva** | Fracción de la **velocidad de curva** de la calibración donde manda la curva | 90–105 % | 1 |
| **Frenada** | Qué parte de la frenada del perfil de la spec 6 (el 90 % de la real) usa al calcular dónde frenar. Menos = frena antes | 0,85–1,0 | 1 |
| **Reacción** | Tiempo desde el GO hasta que pisa W | 0,15–0,40 s | 1 |
| **Ataque** | Ganas y riesgo al adelantar (4.3) | 0–1 | 2 |
| **Defensa** | Probabilidad de cubrir el interior (4.4) | 0–1 | 2 |
| **Contacto** | Cuánto hueco deja y si acepta rozarse (4.5) | 0–1 | 2 |
| **Boost** | Estrategia (4.6) | ADELANTAR / DEFENDER / ÚLTIMA VUELTA | 2 |
| **Errores** | Multiplicador de la frecuencia de errores (5) | 0,3–2,0 | 3 |

**v1.1, decidido en la etapa 1**: no hace falta separar recta y curva por el radio (600 m) ni una transición de 30 m. El límite de cada punto es el menor de los dos (punta × recta, curva × curva), así el paso es continuo y lo suavizan las pasadas de aceleración y frenada. Con recta 100 %, curva 97 % y frenada 1,0 sale exactamente el perfil de la spec 6 (el piloto de F7 y el equilibrio no cambian).

La curva puede pasar del 100 %: la calibración es prudente y al 106 % el piloto aún no roza la barrera en `circuit_01`.

### 3.2 Los seis perfiles

**v1.1**: márgenes ajustados en la etapa 1 (3.5), decisión del usuario. Con los de la v1.0 el equilibrado iba ~0,7 s por vuelta más lento que los rivales de antes, y el usuario ya les ganaba (2:03.700 con VECTOR en `circuit_01`, frente a 2:06–2:13). Ahora NORMAL va al ritmo de antes: el equilibrado queda a 0,03–0,12 s por vuelta del piloto de F7. Cada punto de recta vale unos 0,2 s por vuelta y cada uno de curva 0,1 s; la frenada casi no cuenta.

| Perfil | Recta | Curva | Frenada | Reacción | Ataque | Defensa | Contacto | Boost | Errores |
|---|---|---|---|---|---|---|---|---|---|
| **Equilibrado** | 99 % | 99 % | 0,95 | 0,25 s | 0,5 | 0,5 | 0,3 | ADELANTAR | 1,0 |
| **Velocista** | 100 % | 96 % | 0,92 | 0,20 s | 0,6 | 0,3 | 0,3 | ADELANTAR | 1,0 |
| **Especialista en curvas** | 98 % | 102 % | 1,00 | 0,25 s | 0,5 | 0,6 | 0,3 | DEFENDER | 1,0 |
| **Agresivo** | 99 % | 99 % | 1,00 | 0,18 s | 0,9 | 0,8 | 0,9 | ADELANTAR | 1,5 |
| **Conservador** | 99 % | 99 % | 0,88 | 0,32 s | 0,2 | 0,2 | 0,0 | ÚLTIMA VUELTA | 0,5 |
| **Técnico** | 98 % | 100 % | 0,98 | 0,25 s | 0,6 | 0,7 | 0,2 | ÚLTIMA VUELTA | 0,6 |

El conservador tiene los márgenes del equilibrado: frena antes (0,88) y sale más tarde.

En carrera se nota así:
- **Velocista**: te pasa en las rectas y te lo devuelves en las curvas.
- **Especialista en curvas**: lento en recta, pegajoso en el sector técnico, defiende bien.
- **Agresivo**: frena tarde, ataca mucho, cierra la puerta y se equivoca más. Embestir le cuesta daño, así que acaba la carrera tocado.
- **Conservador**: no pelea, deja sitio, casi no falla y se guarda el boost.
- **Técnico**: limpio y rápido en curva, defiende con cabeza y guarda el boost para el final.

### 3.3 Reparto de los 20 pilotos

| Perfil | Pilotos |
|---|---|
| Equilibrado | CINDER, JACKAL, ONYX, TITAN |
| Velocista | RAZOR, BLITZ, SPARK, VIPER |
| Especialista en curvas | EDGE, KESTREL, RIPTIDE |
| Agresivo | BUTCHER, VANDAL, FURY |
| Conservador | ROOK, NOMAD, ORBIT |
| Técnico | HEX, SHADE, WRAITH |

- El perfil va en el **catálogo de pilotos**, junto al apodo.
- La nave y la pintura siguen saliendo al azar, sin mirar el perfil.
- La alineación sigue sacando 7 pilotos al azar, así que en una carrera puede no haber ningún agresivo o haber tres.

### 3.4 Variaciones de cada piloto

- Cada piloto tiene su **variación fija** sobre los rasgos de su perfil, para que dos del mismo perfil no conduzcan igual:
  - márgenes: ±1 punto;
  - frenada: ±0,02;
  - reacción: ±0,03 s;
  - ataque, defensa y contacto: ±0,1 (dentro de 0–1);
  - errores: ±0,1.
- Es **fija por piloto**: sale de una semilla calculada con su `id`, no cambia de carrera a carrera. RAZOR siempre es el mismo RAZOR.
- La estrategia de boost no varía.
- Se guarda en el catálogo ya calculada, para poder verla y ajustarla a mano. **v1.1**: WRAITH se ajustó a mano (−0,4 puntos en los dos márgenes en vez de −0,9) para quedar dentro de ±0,4 s.
- **v1.1**: la reacción de cada salida (3.6) sustituye al ritmo sorteado por rival de la spec 3 (± 2 %): el ritmo es el del piloto. El PACE de F9 pasa a ser un ajuste en puntos (−15…+5, 0 por defecto) que se suma a los dos márgenes.

### 3.5 Ningún perfil es más rápido en total

- `BalanceRunner` gana un modo **PERFILES**: corre el escenario A (vuelta limpia, sin errores ni rivales) con **cada perfil** y **cada nave** en `circuit_01`.
- Regla: la vuelta lanzada de cada perfil queda a **±0,3 s** o menos de la del **equilibrado** con la misma nave. Con las variaciones de 3.4, cada piloto queda a **±0,4 s**.
- Si un perfil se sale, el agente ajusta **solo sus márgenes de recta y curva** (subiendo o bajando los dos lo mismo), sin cambiar su carácter, y **propone** los valores. Los fija el usuario.
- Lo que no mide la vuelta limpia (ataque, errores, boost) se mira conduciendo y en la etapa 3 con el informe de carreras de F9 (sección 8).

### 3.6 Salida

- En el GO, cada rival espera su **reacción** antes de pisar W, con ±0,05 s al azar en cada salida.
- Si el inventario encuentra una ventaja por buena reacción para el jugador, la IA usa la **misma regla**. Si no existe, no se crea en esta spec.

### 3.7 Criterio de aceptación de la etapa 1

- En pista se notan los perfiles: los velocistas tiran en recta, los especialistas en curva y los conservadores frenan antes.
- `BalanceRunner` en modo PERFILES da ✓ en `circuit_01` con las tres naves, o propone ajustes.
- F9 enseña el perfil de cada rival.

## 4. Etapa 2 · Duelo

### 4.1 Rebufo

Mecánica nueva para **todas las naves**, tú incluido.

- **Cuándo**: vas **detrás** de otra nave, a menos de **25 m** (de tu morro a su cola), con tu centro desviado del suyo **como mucho su semiancho**, los dos a más de **150 km/h** y en el suelo.
- **Fuerza** `r` (0–1): **1** a 5 m o menos, **0** a 25 m, lineal entre medias.
- **Efecto** (con `r`):
  - punta × (1 + **0,04**·`r`);
  - `Drag` × (1 − **0,30**·`r`).
- **Entrada y salida**: `r` sube hasta su valor en **0,5 s** y baja a 0 en **0,3 s** al salirte.
- **Con boost** en marcha no hay rebufo (no se suman). Al acabar el boost, vuelve con su entrada de 0,5 s.
- Con **daño**, la punta del rebufo sale de la punta ya reducida.
- A la nave de **delante no le afecta en nada**.
- Si hay varias delante, cuenta la más cercana.
- Las naves atravesables (terminadas y recolocadas) no dan rebufo.

### 4.2 Cómo se ve el rebufo

**Propuesta inicial.** Si al verlo no convence, se hace una maqueta antes de cambiarlo.

**Indicador del HUD** (persecución y cabina):
- Tres **chevrons** pequeños `› › ›`, arriba de la fila del daño y las esferas de boost, alineados a la derecha con ellas, 8 px por encima.
- Se enciende **uno por cada tercio** de `r`, en cian (`Palette`), con el halo suave del HUD.
- Con `r` = 0 **no se dibuja nada**.
- Unos **10 px** de alto: se ve de reojo y no tapa nada.

**Líneas de aire en la cabina**:
- Entre **8 y 12 líneas** finas y tenues, en cian, que nacen unos metros por delante del morro y **se abren alrededor del cristal**, pasando por los lados y por arriba. Es como si el aire rodeara la burbuja que deja la nave de delante.
- Intensidad = **0,5**·`r`. Largo según la velocidad, como las líneas de velocidad.
- Van **encima** de las líneas de velocidad de siempre, sin cambiarlas.
- Solo en **cabina**. En la cámara exterior, solo el indicador.
- Si las líneas de velocidad están desactivadas en el futuro SETTINGS, estas también.

### 4.3 Adelantar

Se añaden estados a los de la spec 3:

| Estado | Qué hace |
|---|---|
| `CLEAR` / `PASS L/R` / `FOLLOW` | Lo de la spec 3 |
| `SLIP` | Se pega detrás en recta, en el rebufo, preparando el adelantamiento |
| `ATTACK L/R` | Adelantamiento decidido por un lado |
| `DEFEND L/R` | Cubre un lado (4.4) |

**En recta (por rebufo)**:
1. Si alcanza a una nave en recta y quedan al menos **150 m** de recta, pasa a `SLIP`: se pone detrás, dentro del rebufo.
2. Sale del rebufo hacia el lado con hueco (`ATTACK`) cuando la distancia hasta la siguiente frenada es de **120 m + 120 m × ataque**. Un agresivo sale antes; uno con poco ataque se queda más tiempo, o no sale.
3. Si con `ataque` < 0,3 no ve hueco claro, se queda en `FOLLOW`.

**En frenada (por dentro)**:
- Si llega a la frenada a menos de **15 m** del de delante, con hueco por el **interior** y ataque ≥ 0,5, se tira por dentro (`ATTACK`).
- Frena un poco más tarde: frenada + 0,05 × ataque (como mucho 1,0, la frenada real).

**Abortar**: si al llegar al punto de frenada no tiene el morro **a la altura de la mitad** del otro, aborta y vuelve detrás (`FOLLOW`). Un piloto con contacto ≥ 0,7 sigue si tiene al menos una **cuarta parte** del largo en paralelo.

- Después de abortar, espera **5 s** antes de volver a intentarlo con el mismo.
- **Hueco mínimo** entre cascos al adelantar: 1 m − 0,7 × contacto (FURY deja unos 0,3 m).
- Con **la velocidad de la línea desplazada** (spec 3, 8.2.3): por fuera, más lento.

### 4.4 Defender

- Si una nave le persigue a menos de **20 m** (se acerca o va en su rebufo) y quedan al menos **100 m** hasta la siguiente frenada, el defensor decide **una vez por recta** si cubre el interior. Lo hace con una probabilidad igual a su **defensa**.
- **Cubrir** (`DEFEND`): se desplaza hacia el **interior** de la siguiente curva antes de frenar, dentro de su margen de barrera. Así cierra la frenada por dentro, a cambio de una curva más lenta.
- **Un solo cambio de línea por recta**: nada de zigzags para bloquear.
- No se mueve hacia una nave que ya tiene el morro a la altura de **una cuarta parte** de su largo o más.
- **Cerrar la puerta tarde**: con contacto ≥ 0,7, sí se mueve mientras el otro tenga **menos de la mitad** del largo en paralelo, aceptando el roce.
- Sale de la curva y vuelve a su trazada.

### 4.5 Contacto

- **Nunca embiste a propósito**: antes de moverse de lado, calcula la velocidad de cierre con la nave de al lado, y no se mueve si saldría un golpe **medio o fuerte** (≥ 30 km/h). El roce, según su contacto.
- **Roce de lado**:
  - contacto < 0,5 → **se aparta** 1 m al rozar;
  - contacto ≥ 0,5 → **aguanta** su línea.
- Contigo, las mismas reglas que con los rivales.

### 4.6 Boost

Sustituye a la regla de la spec 1 (y a la de la 3, que la limitaba a la trazada clara). Solo se usa en una recta con al menos **150 m** por delante.

| Estrategia | Cuándo gasta una carga |
|---|---|
| **ADELANTAR** | En `ATTACK` en recta, o con la trazada clara si persigue a alguien a menos de 40 m |
| **DEFENDER** | Cuando alguien le persigue a menos de 15 m en recta |
| **ÚLTIMA VUELTA** | Se las guarda. En la última vuelta, una en cada recta con la trazada clara o atacando |

- **Sin desperdiciar**: si tiene **3 cargas** (el máximo), gasta una en la siguiente recta con la trazada clara, sea cual sea su estrategia. Así no pierde recargas.
- Una carga cada vez, con el cooldown de siempre. En daño nivel 3, nada.
- Nunca con la trazada tapada y sin estar atacando.
- **v1.1**: mientras dura el impulso, el piloto sigue pisando W aunque pase de su objetivo. Medido en la etapa 1: el perfil de velocidad tiene la punta como techo, así que hasta ahora soltaba W con el boost y lo perdía (le daba 0,02–0,05 s por vuelta).

### 4.6b Concreciones de la etapa 2 (v1.1)

- **Recta y frenada** para el piloto: un punto es recta si ahí manda la punta (no la curva) y no va frenando; la siguiente frenada es donde deja de serlo. El interior es el lado contrario al giro en el punto más lento de los 400 m siguientes.
- **SLIP**: solo con las dos por encima de 150 km/h (sin rebufo no tiene sentido; con una nave parada se quedaba detrás para siempre) y con la de delante como mucho 15 km/h más lenta (si va mucho más lenta, se la pasa como en la spec 3). Hueco en el rebufo: el mayor de 6 m y 0,15 s.
- **En frenada por dentro**: llegando a menos de 60 m de la frenada. Aborta 30 m después del punto de frenada (frena más tarde que el otro). Frena más tarde con un perfil propio: frenada + 0,05 × ataque.
- **Defender**: se desplaza al 60 % de su límite lateral hacia el interior.
- **Roce de lado con contacto < 0,5**: se aparta 1 m durante 1 s.
- **Boost**: mientras dura el impulso, el piloto usa un perfil con la punta + el impulso y sin pasada de aceleración, y sigue pisando W.
- **Arreglo de la spec 3**: apartado en recta, la trazada clara ya no le pone de tope el margen de curva de la punta (le quitaba el boost y lo dejaba al 97 % de la punta).
- Medido sin ventana con el piloto de F7 en `circuit_01`: tiempos de los rivales iguales que en la etapa 1 (2:05–2:14) y unos 0,6 golpes medios o fuertes entre rivales por carrera (sin duelo, 0 en 10 carreras).

### 4.7 Criterio de aceptación de la etapa 2

- El rebufo se nota: pegado detrás en recta ganas velocidad y puedes salir a adelantar. El de delante no pierde nada.
- El indicador y las líneas de aire salen solo con rebufo, y no molestan.
- Los rivales se adelantan entre ellos y a ti con sentido: en recta saliendo del rebufo, y en frenada por dentro.
- Los que defienden cubren el interior una vez, sin zigzag.
- Los agresivos cierran la puerta tarde y acaban la carrera con más daño. Los conservadores casi no pelean.
- El boost se gasta en momentos con sentido, y los de ÚLTIMA VUELTA lo guardan.

## 5. Etapa 3 · Errores

### 5.1 Tipos

| Error | Qué pasa |
|---|---|
| **Frenada tardía** | Frena **5–15 m** tarde, se va largo **2–4 m** por fuera de la trazada y pierde unas **0,3–0,8 s**. Si no le cabe, roza la barrera |
| **Salida abierta** | Acelera antes de tiempo en la salida de curva y se abre hasta **rozar o tocar** la barrera |
| **Mala salida** | En el GO, **+0,3–0,8 s** de reacción |

- La física es la de siempre: si toca la barrera, se aplican sus reglas y su daño.
- Todo sale del generador inyectado, por piloto. Con la misma semilla, los mismos errores.
- La IA **no derrapa**, tampoco para corregir un error.

### 5.2 Frecuencia

- **Frenada tardía + salida abierta**: tasa base de **0,25 errores por vuelta**, mitad y mitad, × errores del piloto × dificultad.
- **Bajo presión** (atacando, defendiendo o con alguien a menos de 20 m): × **1,5**.
- **Mala salida**: **10 %** por carrera × errores del piloto × dificultad.
- Nunca dos errores en la misma curva.

### 5.3 Errores justos

- **No hay error** si tiene una nave **al lado** (solapadas a lo largo, con menos de 3 m entre cascos). Un error nunca te saca a ti de la pista.
- Con una nave detrás, sí puede haber error: la de detrás tiene que esquivar o frenar con la trazada clara.
- En `BalanceRunner` los errores están **apagados** por defecto, para que el equilibrio no dependa del azar.

### 5.3b Concreciones de la etapa 3 (v1.1)

- La tirada de cada curva se hace a 200 m de su frenada, con la presión y las naves de al lado de ese momento. Mientras dura un error no se tira el de la curva siguiente.
- **La pérdida se sortea (0,3–0,8 s) y se aplica**: medido con un rival solo en `circuit_01`, frenar 5–15 m tarde y acelerar antes en la salida apenas costaba tiempo (la frenada del perfil va al 90 % de la real y a la salida la trazada ya va por fuera). Así que, además de la frenada tardía o la salida abierta (que se ven), el objetivo de velocidad baja en el tramo del error (de la frenada, o del vértice, a la recta) lo que hace falta para perder ese tiempo. Medido: unos 0,45–0,6 s por error, con algún roce o golpe medio contra la barrera.
- Con una nave al lado (solapadas a lo largo y a menos de 3 m entre cascos), el error se corta también si ya había empezado.
- Errores, informe y adelantamientos: los adelantamientos se cuentan con la clasificación en directo, cuando dos naves que corren se cruzan. El informe sale en la consola cuando terminan todos los rivales, con el puesto de ese momento; del jugador solo puesto, adelantamientos y daño.

### 5.4 Criterio de aceptación de la etapa 3

- En una carrera se ven de vez en cuando rivales que se van largos o rozan la barrera. Más los agresivos, casi nunca los conservadores.
- Ningún error de un rival te saca de la pista cuando vas a su lado.
- En F9 se cuentan los errores por piloto y por tipo.

## 6. Etapa 4 · Dificultad

### 6.1 Qué cambia

Solo la IA, **nunca la física**: ni la tuya ni la de los rivales. Sin *rubber banding*: nada cambia según cómo vaya la carrera.

| | FÁCIL | NORMAL | DIFÍCIL |
|---|---|---|---|
| Márgenes de recta y curva | −4 puntos | 0 | +2 puntos (recta máx. 100 %, curva máx. 105 %) |
| Frenada | −0,04 | 0 | +0,02 (máx. 1,0) |
| Reacción | +0,15 s | 0 | −0,05 s |
| Ataque y defensa | × 0,6 | × 1 | × 1,2 (máx. 1) |
| Contacto | × 0,6 | × 1 | × 1 |
| Errores | × 2 | × 1 | × 0,4 |

- Se aplica sobre el perfil y la variación de cada piloto: en DIFÍCIL, un conservador sigue siendo conservador, pero mejor.
- **NORMAL** es lo que se ajusta en las etapas 1–3.

### 6.2 Dónde se elige

- En **SETTINGS**, cuando exista (spec aparte). Por defecto, **NORMAL**.
- Hasta entonces: un selector **DIFFICULTY** en F9, que vale para la carrera siguiente (RESTART). No se guarda en disco.
- La lógica queda lista para que SETTINGS solo tenga que **leer y guardar** el valor.
- Se lee **al montar la carrera**. Cambiarla no afecta a la carrera en curso.

### 6.3 Récords

- Los récords **no se separan** por dificultad. Son tus tiempos.

### 6.3b Concreciones de la etapa 4 (v1.1)

- La dificultad va en `GameSession.Difficulty` (NORMAL por defecto, no se guarda): SETTINGS solo tendrá que leerla y guardarla. La carrera la lee al montarse (`RivalRaceContext`).
- Solo se limitan los máximos de la tabla (recta 100 %, curva 130 %, frenada 1, ataque, defensa y contacto 1); en FÁCIL una frenada puede bajar de 0,85.
- `BalanceRunner` en modo PERFILES con `ProfilesAllDifficulties`: la regla se mira solo en NORMAL. Medido en `circuit_01`, vuelta media de perfiles y pilotos: NORMAL 40,598 s, FÁCIL 41,960 s (+1,36), DIFÍCIL 40,212 s (−0,39). En DIFÍCIL el velocista ya va al 100 % en recta y queda algo por detrás (+0,32 s en una nave).

### 6.3c Reajuste tras la prueba del usuario (v1.1)

- **Con la primera tabla, el usuario ganaba por ~5 s en las tres dificultades** (su mejor vuelta en carrera, 38,55 s; la de los rivales, 40,2–40,6 s). La calibración de curva es muy prudente: con un rival solo en `circuit_01`, al 130 % de su velocidad de curva aún no roza la barrera (al 140 %, VECTOR y HAMMER golpean).
- Objetivos acordados con el usuario, ganador de la carrera: FÁCIL ~2:08, NORMAL 2:03–2:04 (su mejor carrera, 2:03.7), DIFÍCIL 2:00–2:01.
- **Margen de curva hasta 130 %** y la curva de los seis perfiles **+13 puntos** (equilibrado 99 / 112, velocista 100 / 109, especialista 98 / 115, agresivo 99 / 112, conservador 99 / 112, técnico 98 / 113). WRAITH, sin variación de recta.
- **La dificultad mueve recta y curva por separado**: FÁCIL −3 / −12 puntos, DIFÍCIL +1 / +13 (la recta casi no tiene sitio: es la punta de la nave). El resto de la tabla 6.1 no cambia.
- **Boost en el mejor momento** (4.6): a la salida de una curva (a menos de 150 m) hacia una recta de al menos 500 m, con la trazada clara. ADELANTAR y DEFENDER gastan ahí si les queda más de una carga; ÚLTIMA VUELTA, solo en la última vuelta. Es lo que hace el usuario.
- Medido sin ventana, con solo los rivales en pista (5 carreras por dificultad): ganador FÁCIL 2:06.5–2:10.4, NORMAL 2:02.9–2:05.7, DIFÍCIL 2:00.2–2:01.9. PERFILES: NORMAL ✓ (vuelta media 39,77 s), FÁCIL 41,33 s, DIFÍCIL 38,94 s. Golpes medios o fuertes entre rivales: ~1,2 por carrera en NORMAL y ~1,8 en DIFÍCIL (antes ~0,6), por la mayor velocidad en curva.

### 6.4 Criterio de aceptación de la etapa 4

- En FÁCIL, conduciendo bien se gana con margen. En NORMAL, conduciendo bien se pelea. En DIFÍCIL, hay que hacerlo casi perfecto.
- Los perfiles se siguen notando en las tres.
- `BalanceRunner` en modo PERFILES puede correr cada dificultad y apunta la vuelta media de cada una.

## 7. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Rasgos de los perfiles | Tabla de 3.2 | Catálogo de pilotos |
| Variaciones por piloto | Tabla de 3.4 | Catálogo de pilotos |
| Margen de la regla de perfiles | ±0,3 s (±0,4 s por piloto) | `BalanceRunner` |
| Rebufo: distancia máxima / plena | 25 m / 5 m | Rebufo |
| Rebufo: velocidad mínima | 150 km/h | Rebufo |
| Rebufo: punta / `Drag` | +4 % / −30 % | Rebufo |
| Rebufo: entrada / salida | 0,5 s / 0,3 s | Rebufo |
| Líneas de aire: número / intensidad | 8–12 / 0,5·`r` | Cabina |
| Recta mínima para `SLIP` y boost | 150 m | Rival |
| Salida del rebufo antes de la frenada | 120 m + 120 m × ataque | Rival |
| Distancia para atacar en frenada | 15 m | Rival |
| Espera tras abortar | 5 s | Rival |
| Distancia para defender | 20 m · 100 m hasta la frenada | Rival |
| Tasa base de errores | 0,25 por vuelta · 10 % de mala salida | Rival |
| Factor de presión | × 1,5 | Rival |
| Ajustes por dificultad | Tabla de 6.1 | Carrera |

## 8. Depuración (F9)

- Bloque RIVALS, por rival: **perfil**, estado (`CLEAR`, `SLIP`, `ATTACK`, `DEFEND`…), `r` del rebufo, cargas y estrategia de boost, y errores por tipo.
- El dibujo de estados de la spec 3 gana los estados nuevos.
- Selector **DIFFICULTY** (6.2).
- Interruptor **ERRORS** (por defecto activado), para probar sin errores.
- **Informe de carrera** al terminar, en la consola: por piloto, perfil, nave, puesto, adelantamientos hechos y sufridos, golpes por papel, daño final, boosts y errores. Sirve para ver si un perfil gana demasiado.
- Tu `r` del rebufo, también en el panel.

## 9. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Catálogo de pilotos** (puro, datos): `id`, apodo, perfil y variación ya calculada.
- **Perfiles** (puro, datos): los seis con sus rasgos.
- **Rasgos efectivos** (puro): perfil + variación + dificultad → rasgos con los que conduce, limitados a sus rangos.
- **Perfil de velocidad**: gana margen de recta, margen de curva y frenada (si el inventario lo permite sin cambiar el resultado con los valores de hoy).
- **Rebufo** (puro): posición, velocidad y forma de la nave y de las de delante → `r`; y `r` → factores de punta y `Drag`. Lo aplica la dinámica de **todas** las naves.
- **Duelo** (puro, en el namespace del piloto): amplía la trazada clara con `SLIP`, `ATTACK` y `DEFEND`. Recibe los rasgos.
- **Estrategia de boost** (pura): rasgo + estado de la carrera → gastar o no.
- **Errores** (puro): rasgos, presión, naves al lado y generador → qué error y cuándo. El conductor de rival lo aplica cambiando la frenada y la línea de ese momento.
- **Conductor de rival**: junta lo anterior. Sin reglas propias.
- **HUD**: indicador de rebufo, solo lee `r`. **Cabina**: líneas de aire, solo leen `r` y la velocidad.
- **`BalanceRunner`**: modo PERFILES y dificultad.

## 10. Pruebas

### Pruebas unitarias mínimas

Perfiles (etapa 1):
1. El catálogo tiene 20 pilotos con perfil, y cada perfil tiene los pilotos de la tabla 3.3.
2. La variación de un piloto es siempre la misma y está dentro de los límites de 3.4.
3. Rasgos efectivos: perfil + variación, limitados a sus rangos.
4. Con márgenes 95 % en recta y 99 % en curva, la velocidad objetivo en recta es el 95 % de la punta y en curva el 99 % de la tabla, sin saltos.
5. Con frenada 0,9, el punto de frenada está antes que con 1,0.
6. Reacción: no hay entrada hasta pasado su tiempo desde el GO.

Rebufo (etapa 2):
7. A 5 m detrás y alineado: `r` = 1. A 15 m: 0,5. A 30 m: 0. Desviado más que el semiancho del de delante: 0.
8. Por debajo de 150 km/h, en el aire o con boost: 0.
9. `r` sube en 0,5 s y baja en 0,3 s.
10. Con `r` = 1, punta × 1,04 y `Drag` × 0,7. La nave de delante no cambia.

Duelo (etapa 2):
11. Alcanzando en recta con 200 m por delante: `SLIP`. Con 100 m: no.
12. Con ataque 0,5, sale del rebufo a 180 m de la frenada; con 1,0, a 240 m.
13. Aborta si en el punto de frenada no tiene el morro a la mitad del otro. Con contacto 0,8, sigue con una cuarta parte.
14. Defensa: un solo cambio de línea por recta. No se mueve hacia una nave con una cuarta parte en paralelo (salvo contacto ≥ 0,7 y menos de la mitad).
15. Nunca se mueve de lado si el cierre saldría ≥ 30 km/h.
16. Boost: cada estrategia gasta en su caso de la tabla 4.6. Con 3 cargas y trazada clara, gasta una sea cual sea su estrategia.

Errores (etapa 3):
17. Con la misma semilla, los mismos errores.
18. Con una nave al lado, nunca hay error.
19. Bajo presión, la tasa es × 1,5. Con ERRORS desactivado, ninguno.

Dificultad (etapa 4):
20. FÁCIL / NORMAL / DIFÍCIL aplican la tabla 6.1, con los límites.
21. La física de las naves (`ShipTuning`) es la misma en las tres.
22. La dificultad se lee al montar la carrera: cambiarla a mitad no cambia los rasgos en curso.

Las pruebas existentes siguen pasando. Las que comprobaban el boost «en la recta más larga» se adaptan a las estrategias.

## 11. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| **1 · Perfiles y ritmo** | Sección 3 · perfil y dibujo en F9 | Pruebas 1–6 · `BalanceRunner` PERFILES · Prueba manual 1–4 · Commit `feat:` |
| **2 · Duelo** | Sección 4 · lo de F9 | Pruebas 7–16 · Prueba manual 5–12 · Commit `feat:` |
| **3 · Errores** | Sección 5 · ERRORS e informe de carrera | Pruebas 17–19 · Prueba manual 13–15 · Commit `feat:` |
| **4 · Dificultad** | Sección 6 · DIFFICULTY en F9 · versión | Pruebas 20–22 · Prueba manual 16–18 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Perfiles y ritmo**
1. F9: cada rival con su perfil. Con el dibujo de estados, seguir a un velocista y a un especialista: el primero tira en recta y el segundo en curva.
2. Conservadores: frenan antes y salen más tarde en el GO.
3. `BalanceRunner` en modo PERFILES: tabla por perfil y nave, con ✓ o con propuesta de ajuste.
4. Varias carreras: los puestos se reparten entre perfiles. Ninguno gana siempre.

**Etapa 2 · Duelo**
5. Pegarse detrás de un rival en recta: los chevrons se encienden, salen las líneas de aire en la cabina y ganas velocidad. Al salir del rebufo, desaparecen.
6. Mirar en exterior: solo el indicador.
7. Que un rival se te pegue detrás: no pierdes nada.
8. Ver adelantamientos entre rivales: en recta saliendo del rebufo y en frenada por dentro. Que alguno aborte y vuelva detrás.
9. Atacar a un especialista o a un técnico: cubre el interior una vez antes de la curva, sin zigzag.
10. Ponerse al lado de un agresivo antes de una curva: te cierra la puerta si no tienes la mitad. A un conservador: te deja sitio.
11. Rozar de lado a un agresivo: aguanta la línea. A uno con poco contacto: se aparta.
12. Boost: un técnico o un conservador lo guarda para la última vuelta. Nadie se queda con 3 cargas mucho tiempo.

**Etapa 3 · Errores**
13. Una carrera entera: de vez en cuando alguien se va largo o roza al salir. Más los agresivos.
14. Ir al lado de un rival en curvas: nunca te saca por un error suyo.
15. Informe de carrera en la consola. ERRORS desactivado: ninguno.

**Etapa 4 · Dificultad**
16. FÁCIL: se gana con margen conduciendo bien. Los rivales fallan más y pelean menos.
17. DIFÍCIL: van casi al límite, fallan poco y defienden mejor. Ganar cuesta.
18. Cambiar la dificultad en F9 a mitad de carrera: no cambia nada hasta el RESTART.

## 12. Fuera de alcance

- **Menú SETTINGS** y guardar la dificultad: spec propia, a continuación de esta.
- **Derrape de la IA**. Decidido: de momento no.
- *Rubber banding* o ventajas según la carrera. Decidido: nunca.
- Separar los récords por dificultad. Decidido: no.
- Enseñar el perfil del piloto al jugador (resultados, HUD, fichas de piloto).
- Campeonatos. Decidido: pilotos, naves y pinturas se mantienen durante todo el campeonato.
- Que la nave dependa del perfil. Decidido: no.
- Rebufo con efectos en la cámara exterior, sonido del rebufo y sonido en general.
- Embestir a propósito y cualquier comportamiento sucio (empujar contra la barrera, bloquear con zigzag).
- Mando.
