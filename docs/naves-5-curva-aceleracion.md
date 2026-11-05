# VECTORY — Spec: Naves 5 · Curva de aceleración

Versión 1.0 · 2026-11-05 · Estado: **propuesta, pendiente de validar**

Quinta spec de naves. Cambia **cómo llegan las naves a su velocidad punta**. Hoy la aceleración es lineal: empuja igual a 50 km/h que a 380, y VECTOR llega a la punta en unos **4,4 s**. A partir de ahí la aceleración ya no cuenta, y la punta decide casi toda la vuelta. Por eso HAMMER pierde más de 3 s en el circuito principal.

Con esta spec, la aceleración **pierde fuerza de forma suave al acercarse a la punta**, como si hubiera marchas pero sin cambios ni tirones. Abajo se arranca casi igual, pero la punta cuesta mucho más. Así la aceleración cuenta en casi toda la vuelta, y cada nave puede tener su propia forma de curva.

**Esta spec no equilibra las naves.** Deja el sistema y las herramientas listas. El equilibrio va en la spec 6.

Plan de las specs de naves:

1. Definición de nave (hecha).
2. Perfil técnico, colisión y ajuste en pruebas (hecha).
3. Cascos (hecha).
3b. Cabinas (hecha).
4. Integración y nombres definitivos (hecha).
5. **Curva de aceleración** (esta).
6. Equilibrio de las tres naves (con el panel de pruebas y, si hace falta, piloto automático).

Se apoya en lo que ya existe tras las specs 1 a 4: `ShipTuning`, `ShipDynamics`, los multiplicadores por nave, `ShipStats`, el panel de pruebas (F9–F12), el boost, el derrape, las pendientes y los daños.

Rama: `feature/acceleration-curve`, que sale de `develop` con `feature/ship-integration` ya mergeada.

Versión del juego: **0.14.0 → 0.14.1**. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 0 · Inventario**: el agente explica cómo se aplican hoy la aceleración, la punta, el `Drag`, las pendientes, el daño, el derrape y el boost en `ShipDynamics`, y qué pruebas dependen de que la aceleración sea lineal. No cambia nada.
- **Etapa 1 · Curva común**: la aceleración sigue una curva suave que baja al acercarse a la punta. **Misma curva para las tres naves.** El panel de pruebas puede volver a la aceleración lineal para comparar.
- **Etapa 2 · Curva por nave y selector**: cada nave tiene su propia forma de curva, ajustable en el panel. El dato `ACCELERATION` del selector se calcula con la curva.
- Cambia la sensación de **las tres naves, también VECTOR**, también abajo. Es lo esperado.
- **Los récords y los fantasmas no se tocan.** No se borran ni se marcan.
- Boost, derrape, frenada, giro, agarre, colisión, daño, pendientes y peraltes **no cambian de reglas**.

## 2. Etapa 0 · Inventario

Antes de cambiar nada, el agente escribe un **informe corto** con:

1. Cómo aplica `ShipDynamics` la aceleración con W hoy (fórmula y orden de cálculo dentro del frame).
2. Cómo funciona el `Drag`: si actúa solo al soltar W o siempre, y si es fijo o depende de la velocidad.
3. Cómo se limita la velocidad a la punta (la "regla gradual").
4. Dónde entran los multiplicadores de **pendiente** (punta y aceleración), **daño** (punta en niveles 2 y 3), **derrape** (aceleración al 50 %) y **boost** (velocidad por encima de la punta, W sin efecto).
5. El valor real de `Acceleration` de VECTOR (la tabla de la sección 3.3 está hecha con ~25,25 m/s², sacado de `0–200 · 2.2 S`).
6. La lista de **pruebas existentes que dan por hecho que la aceleración es lineal**, y qué valor esperado cambiaría en cada una.

**Si algo de esto no cuadra con lo que dice esta spec, se pregunta antes de seguir.**

## 3. Etapa 1 · Curva común

### 3.1 Regla

Con W pulsada y la velocidad por debajo de la punta:

```
x       = velocidad / punta
factor  = max(empujeMinimo, 1 − x^curva)
acel    = Acceleration × factor
```

- `punta` y `Acceleration` son los **efectivos** de ese momento, ya con los multiplicadores de nave, pendiente, daño y derrape aplicados. Así la curva se adapta sola: con daño (punta más baja) o cuesta arriba, la curva se calcula contra esa punta.
- `curva` (exponente): **2** en las tres naves en esta etapa. Cuanto más alto, más aguanta el empuje arriba (con un valor enorme es casi lineal, como ahora).
- `empujeMinimo`: **10 %**. Cerca de la punta la nave sigue empujando un poco, así que **llega a la punta de verdad** en un tiempo razonable y el velocímetro llega a la cifra redonda. Es común a todas las naves.
- Con la velocidad **igual o mayor que la punta**, W no añade nada, como ahora (también tras el boost).
- La curva solo cambia la **aceleración con W**. La frenada, el `Drag` al soltar W, la pérdida de exceso tras el boost y la pérdida en derrape **no cambian**.
- Independiente del framerate, como el resto de la dinámica.

### 3.2 Boost

- El boost sigue igual: suma +100 km/h sobre la velocidad actual, sin pasar por la curva.
- Al acabar, si la nave está por debajo de la punta, vuelve a acelerar con la curva normal.
- Efecto a vigilar: como ahora cuesta más llegar arriba, el boost **vale más a media velocidad** que antes. No se cambia en esta spec, solo se apunta en la prueba.

### 3.3 Qué se va a notar (VECTOR, con `Acceleration` ≈ 25,25 y punta 396 km/h)

| | Ahora (lineal) | Con curva (2 · 10 %) |
|---|---|---|
| 0–100 km/h | 1,1 s | 1,1 s |
| 0–200 km/h | 2,2 s | 2,4 s |
| 0–300 km/h | 3,3 s | 4,3 s |
| 0–350 km/h | 3,9 s | 6,1 s |
| Hasta la punta | 4,4 s | ~10,2 s |

Con los multiplicadores actuales de la spec 2, para tener una idea:

| | VECTOR | BLADE | HAMMER |
|---|---|---|---|
| 0–200 km/h | 2,4 s | 2,5 s | 2,2 s |
| 0–300 km/h | 4,3 s | 4,4 s | 4,2 s |
| Hasta su punta | ~10,2 s | ~11,7 s | ~8,7 s |

HAMMER va por delante hasta unos 300 km/h y llega antes a su punta. Por encima, VECTOR y BLADE le van ganando. Si eso basta para quitarle los 3 s **no se sabe hasta probar**: es trabajo de la spec 6.

Si con el valor real de `Acceleration` las cifras salen algo distintas, **mandan las del cálculo** y se ajustan las pruebas.

### 3.4 Panel de pruebas: comparar con la aceleración lineal

- En el panel (F9) se añade una casilla **`LINEAR ACCEL (OLD)`**, desactivada por defecto.
- Activada, las tres naves aceleran como antes de esta spec. Sirve para comparar tiempos por vuelta con y sin curva.
- Usarla cuenta como "se ha tocado algo", como el resto del panel. **Cambio respecto a la spec 2, decisión del usuario:** el panel ya no bloquea récords ni fantasma, porque guardarlos sirve de herramienta para equilibrar. El récord se guarda con la nave que se conduce (también con F10). Solo afecta a depuración: el panel no existe fuera de ella.
- Solo en depuración, como todo el panel.

### 3.5 Criterio de aceptación

- Con W a fondo en recta, la aceleración baja de forma **continua**: sin escalones, sin tirones, sin oscilaciones al llegar a la punta.
- VECTOR llega a su punta y el velocímetro marca su cifra (396 km/h), en unos 10 s.
- Con la casilla `LINEAR ACCEL (OLD)` todo se comporta exactamente como en la 0.14.0.

## 4. Etapa 2 · Curva por nave y selector

### 4.1 Curva por nave

- El exponente `curva` pasa a ser un dato de la **definición de cada nave**, en el perfil técnico, junto a sus multiplicadores.
- Valor de arranque: **2** en las tres. Los valores buenos salen de la spec 6.
- No es un multiplicador, así que **no entra en el tope de ±10 %**. Tiene su propio rango permitido: **1,5 a 3,0**. Se comprueba en una prueba.
- `empujeMinimo` sigue siendo **común** a todas las naves.

### 4.2 Panel de pruebas

- Nueva fila **`ACCEL CURVE`** por nave: deslizador de **1,0 a 4,0**, paso **0,1**. Fuera de 1,5–3,0 se marca en naranja, como los porcentajes fuera del ±10 %.
- En VECTOR esta fila **sí se puede mover** (a diferencia de sus porcentajes), porque la forma de la curva de VECTOR también se ajusta en el equilibrio.
- Al lado de la fila, el dato que vería el jugador: **0–200** y **0–300** en segundos, calculados con la curva.
- **F11** vuelca también la curva de las tres naves. **F12** la devuelve a la de la definición.
- Fila nueva en el panel: **`MIN THRUST`** (común), de 0 % a 30 %, paso 1 %. F12 también la devuelve a su valor.

### 4.3 Selector de naves (SHIP DATA)

- `ACCELERATION` sigue mostrando **`0–200 · X.X S`**, pero ahora el tiempo se calcula **integrando la curva** (paso fijo pequeño, en `ShipStats`, sin nodos), no con la fórmula lineal.
- La **barra** pasa a compararse por **tiempo**, para que cuente la forma de la curva: segmentos = 5 + redondeo(50 × (tiempo 0–200 de VECTOR / tiempo 0–200 de la nave − 1)), limitado a 0–10. VECTOR sigue llenando 5.
- Con los valores de arranque saldría más o menos: VECTOR 2,4 s · 5, BLADE 2,5 s · 3, HAMMER 2,2 s · 9. Manda el cálculo.
- El resto de datos (`TOP SPEED`, `HANDLING`, `GRIP`, `BRAKING`) no cambian.

## 5. Parámetros

| Parámetro | Valor | Dónde |
|---|---|---|
| Exponente de la curva (`curva`) | 2 en las tres naves | Definición de cada nave |
| Rango permitido de la curva | 1,5–3,0 | Definiciones (constante) y panel (aviso) |
| Empuje mínimo cerca de la punta | 10 % | Común (constante en `AccelerationCurve`, sin Inspector: el selector de naves también la usa y no tiene acceso al `Player`) |
| Casilla de aceleración lineal | Desactivada | Panel de pruebas (solo depuración) |

## 6. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Curva de aceleración** (puro): dada velocidad, punta efectiva, `curva` y `empujeMinimo`, devuelve el factor. Sin nodos.
- **`ShipDynamics`**: multiplica la aceleración con W por ese factor. Nada más cambia dentro.
- **Definiciones**: campo `curva` en el perfil técnico de cada nave.
- **`ShipStats`**: tiempo 0–200 integrando la dinámica con la curva, y barra por tiempo.
- **Estado del laboratorio** y **`TuningLabPanel`**: curva por nave, empuje mínimo y la casilla lineal. Sin reglas nuevas en el nodo.

### Pruebas unitarias mínimas

Curva (etapa 1):
1. Factor a velocidad 0 = 1.
2. Con `curva` = 2 y empuje mínimo 10 %: a la mitad de la punta el factor es 0,75; al 97 % de la punta es 0,10 (el mínimo).
3. Con la velocidad igual o mayor que la punta, W no añade velocidad.
4. Con `curva` muy alta (por ejemplo 1000), el tiempo 0–punta queda a menos de un 2 % del lineal.
5. VECTOR con W a fondo desde 0: 0–200, 0–300 y tiempo hasta la punta salen como en la tabla 3.3 (con el valor real de `Acceleration`), y la velocidad llega exactamente a la punta.
6. Con daño nivel 2 (punta reducida), la curva se calcula contra la punta reducida: la nave llega a esa punta, no se queda por debajo.
7. En derrape, la aceleración es la de la curva × 50 %.
8. Tras un boost, por encima de la punta, W no añade; al bajar por debajo, acelera con la curva.
9. El tiempo 0–300 sale igual (±2 %) integrando a 30 fps y a 144 fps.
10. Con la casilla lineal activada, la dinámica da exactamente lo mismo que en la 0.14.0.

Curva por nave y selector (etapa 2):
11. La curva de las tres definiciones está dentro de 1,5–3,0.
12. Cambiar la curva de una nave en el laboratorio no cambia la de las otras; `Reset` la devuelve a la de la definición.
13. El texto de F11 incluye la curva de las tres naves y el empuje mínimo, y marca lo que está fuera de rango.
14. `ShipStats`: el 0–200 sale de integrar la curva, y la barra de VECTOR es 5.
15. Una nave con 0–200 más corto que VECTOR tiene más segmentos; con uno más largo, menos.

Las pruebas que daban por hecho la aceleración lineal se adaptan a los valores nuevos. **Antes de cambiar un valor esperado, se avisa de cuál y por qué** (sale del informe de la etapa 0). Las demás siguen pasando sin tocarlas.

## 7. Prueba manual

**Etapa 1 · Curva común**
1. TIME TRIAL con VECTOR. W a fondo en la recta principal: arranca casi igual, pero se nota que **cuesta llegar arriba**. Sin tirones ni escalones.
2. Llega a 396 km/h en la recta larga (si la recta da para unos 10 s).
3. Salir de una curva lenta: la diferencia con antes se nota más que en la salida.
4. Boost a media velocidad y a la punta: funciona como siempre. Apuntar si a media velocidad se siente demasiado fuerte.
5. Derrape, cuestas, crestas y daño: nada raro.
6. F9 → `LINEAR ACCEL (OLD)`: vuelve la aceleración de antes. Hacer una vuelta con y otra sin y apuntar los dos tiempos.

**Etapa 2 · Curva por nave**
7. F9 → mover `ACCEL CURVE` de una nave: se nota al momento. Con valores bajos se queda "corta" arriba; con valores altos empuja casi hasta el final.
8. F11: salen las curvas de las tres naves y el empuje mínimo.
9. F12: vuelve a la curva de la definición.
10. SHIP SELECTION: los tiempos 0–200 y las barras de aceleración salen con la curva.
11. Las tres naves, una vuelta cada una al circuito principal: **apuntar los tiempos**. Son el punto de partida de la spec 6.
12. Menú principal: la versión es 0.14.1.

## 8. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Informe de la sección 2. Sin cambios | Validación del informe |
| **1 · Curva común** | Curva en `ShipDynamics`, casilla lineal en el panel (sección 3) | Pruebas 1–10 · Prueba manual 1–6 · Commit `feat:` |
| **2 · Curva por nave** | Curva en las definiciones, panel, selector (sección 4) · versión 0.14.1 | Pruebas 11–15 · Prueba manual 7–12 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

## 9. Fuera de alcance

- Equilibrar las naves y fijar sus valores definitivos (spec 6).
- Piloto automático para medir el equilibrio (spec 6, si hace falta).
- Cambios en el boost, aunque ahora valga más a media velocidad.
- Marchas, cambios o tirones de cualquier tipo.
- Sonido de motor ligado a la curva.
- Efectos visuales nuevos (FOV, vibración) ligados a la aceleración.
- Borrar o marcar récords y fantasmas.
- Rivales e IA.
