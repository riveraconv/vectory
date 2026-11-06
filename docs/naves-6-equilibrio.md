# VECTORY — Spec: Naves 6 · Equilibrio con piloto automático

Versión 1.0 · 2026-11-05 · Estado: **hecha y validada** (versión del juego 0.14.2). Lo que se hizo distinto y los valores finales, en la sección 12.

Sexta spec de naves. Su objetivo es encontrar los valores de las tres naves para que **cada una tenga personalidad** pero **ninguna sea imbatible**.

Para eso se crea un **piloto automático** que conduce las tres naves con el mismo código. Al ser siempre el mismo conductor, las diferencias de tiempo vienen solo de la nave. El piloto **mide**. Los números buenos los decide el usuario y se confirman conduciendo.

Plan de las specs de naves:

1. Definición de nave (hecha).
2. Perfil técnico, colisión y ajuste en pruebas (hecha).
3. Cascos (hecha).
3b. Cabinas (hecha).
4. Integración y nombres definitivos (hecha).
5. Curva de aceleración (hecha).
6. **Equilibrio con piloto automático** (esta).

Se apoya en lo que ya existe tras las specs 1 a 5: `ShipTuning`, `ShipDynamics` con la curva de aceleración, las definiciones de las tres naves (multiplicadores y curva), el ancho de colisión, el marco de la pista (`s`, `d`, `h`, punto más cercano), la capa vertical, pendientes y peraltes, la colisión con barreras, el boost, `CircuitCatalog`, los checkpoints y sectores, el panel de pruebas (F9–F12) y `ShipStats`.

Rama: `feature/ship-balance`, que sale de `develop` con `feature/acceleration-curve` ya mergeada.

Versión del juego: **0.14.1 → 0.14.2**. Se sube en el commit de la última etapa, con los valores definitivos.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- **Etapa 0 · Inventario**: el agente comprueba si la física completa de una vuelta se puede simular **sin escena** y cuántos sectores tiene `circuit_01`. No cambia nada.
- **Etapa 1 · Simulador y piloto automático**: un simulador que da vueltas con la misma física del juego, sin dibujar nada, y un piloto que conduce cualquier nave. Saca una **tabla de tiempos por nave y sector**. El piloto también se puede ver conduciendo dentro del juego.
- **Etapa 2 · Barrido de parámetros**: el simulador prueba un parámetro de una nave entre dos valores y dice con cuáles se cumplen las reglas.
- **Etapa 3 · Valores definitivos**: se fijan los valores en las definiciones y el usuario los confirma conduciendo.
- Todo esto es **solo de depuración**. El jugador no ve nada de esta spec, salvo que cambian los números de las naves.

## 2. Reglas del equilibrio

Son las que el simulador comprueba. Los márgenes son **de arranque**: si en la práctica no convencen, se cambian.

| Regla | Qué comprueba | Dónde es obligatoria |
|---|---|---|
| **1 · Nadie imbatible** | La vuelta lanzada de BLADE y de HAMMER queda a **±0,3 s** o menos de la de VECTOR | `circuit_01` |
| **2 · Personalidad** | **Cada nave gana al menos un sector**, y **ninguna gana todos** | `circuit_01` |
| **3 · Todos los circuitos** | Las reglas 1 y 2 también en los demás circuitos | Solo informativa en los circuitos de pruebas |

- **Vuelta lanzada**: la segunda vuelta de una tanda, ya en marcha desde la meta. Es la que se compara. La primera vuelta, que sale parada, se apunta aparte.
- **Ganar un sector**: tener el tiempo más bajo de las tres naves en ese sector, en la vuelta lanzada.
- Los circuitos de pruebas (`test_oval` y los que haya) son extremos a propósito. Por ejemplo, el óvalo premia la punta. Por eso ahí las reglas **se informan pero no obligan**. Cuando haya más circuitos reales, la regla 3 pasa a ser obligatoria en ellos.
- La personalidad que se busca, como orientación (no es una regla):
  - **BLADE** gana en las rectas largas.
  - **HAMMER** gana en las salidas de curva y en el sector técnico.
  - **VECTOR** no es la mejor en nada concreto, pero nunca la peor por mucho.

**Lo que el piloto no puede medir**, y por eso lo confirma el usuario conduciendo (etapa 3): la poca tolerancia de BLADE a los errores, lo que molesta el ancho de cada nave al conducir y la sensación de cada una.

## 3. Etapa 0 · Inventario

Antes de cambiar nada, el agente escribe un **informe corto** con:

1. Qué partes de una vuelta ya son **puras** (sin nodos) y se pueden llamar desde fuera del juego: dinámica, curva de aceleración, capa vertical, pendientes, peraltes, colisión con barreras, recolocación, boost, daño, vueltas y sectores.
2. Qué lógica vive todavía **dentro de `Player`** u otros nodos y haría falta sacar para simular sin escena. **Si hay que sacar algo, se avisa antes** de hacerlo, con la lista.
3. A qué paso fijo corre la física en el juego (por ejemplo, `_PhysicsProcess` a 60 Hz).
4. Qué entrada recibe la dinámica: si el giro, el acelerador y el freno son **todo o nada** (teclado) o admiten **valores intermedios**.
5. Cuántos **sectores** tiene `circuit_01` (checkpoints + 1). Si son **menos de 3**, la regla 2 no se puede cumplir con tres naves: **se avisa** y se propone dónde añadir checkpoints.
6. Si el eje del circuito y sus perfiles se pueden cargar **sin montar la carrera** (la spec del selector de circuito ya lo pedía).

## 4. Etapa 1 · Simulador y piloto automático

### 4.1 Simulador

- Clase **pura** que monta una carrera **sin escena**: circuito, una nave con su definición y la física completa (dinámica, capa vertical, pendientes, peraltes, barreras, recolocación y boost).
- Usa **las mismas clases que el juego**, no una copia. Si el juego cambia una regla, el simulador la hereda sola.
- Avanza con el **mismo paso fijo** que la física del juego y tan rápido como pueda: no espera al reloj.
- **Determinista**: la misma tanda con los mismos valores da **exactamente** el mismo resultado.
- Una **tanda** son **3 vueltas** desde parado en la posición 1 de la parrilla, sin rivales y sin daño. El daño por contacto sí se aplica si hay choques, y se apunta.

### 4.2 Trazada

El piloto sigue una línea calculada una vez por circuito **y por nave**:

- Empieza en el centro de la pista y se **suaviza** varias veces, desplazándola hacia el interior de las curvas, para reducir la curvatura (el método iterativo sencillo de la trazada de mínima curvatura).
- La línea nunca se acerca a la barrera más de lo que permite **esa nave**: desplazamiento lateral máximo = medio ancho de la pista − semiancho de colisión de la nave − **margen de 1 m**. Así una nave ancha tiene menos sitio, como pasa conduciendo.
- Usa el marco de la pista (`s`, `d`) y funciona con pendientes y peraltes.

### 4.3 Velocidad objetivo

1. **Calibración de curva por nave**: antes de correr, el simulador mide con la física real a qué velocidad máxima puede esa nave **mantener un radio** sin salirse más de **0,5 m** de la línea. Se prueba con una tabla de radios (por ejemplo, de 30 a 600 m) en pista plana. El resultado es una tabla radio → velocidad máxima de esa nave, que ya incluye su giro, su agarre y su deslizamiento.
2. **Perfil de velocidad**: en cada punto de la trazada, la velocidad objetivo es la de la tabla para su radio, como mucho la punta.
3. **Frenadas y salidas**: se recorre el perfil hacia atrás con la frenada real de la nave, para empezar a frenar a tiempo, y hacia delante con su curva de aceleración, para no pedir más de lo que puede acelerar. El peralte se tiene en cuenta con los multiplicadores de agarre y giro que ya existen.
4. Margen de seguridad sobre la velocidad objetivo: **97 %** (parámetro), para que el piloto no vaya justo al límite y no choque.

### 4.4 Conducción

- **Dirección**: apunta a un punto de la trazada por delante (*pure pursuit*), a una distancia que crece con la velocidad.
- **Acelerador y freno**: W si va por debajo del objetivo, S si va por encima con margen, y nada (suelta W) en medio.
- La entrada es **la misma que da el teclado**. Si la dinámica solo admite todo o nada, el piloto también. Si admite valores intermedios, el piloto los usa, y se apunta en el informe, porque un humano con teclado no puede hacerlo.
- **No derrapa.**
- **Boost**: por defecto **no lo usa**. Con la opción `USE BOOST`, gasta una carga, si la tiene, al entrar en la recta más larga. Es el mismo punto para las tres naves.
- En el aire (crestas) no hace nada especial: W y S no actúan, como en el juego.
- Si la nave toca la barrera, se cuenta el **contacto** y su nivel. Si hay recolocación, la tanda se marca como **no válida**.

### 4.5 Resultados

Por cada nave y circuito:

| Dato | Detalle |
|---|---|
| Vuelta lanzada | La que se compara |
| Primera vuelta y tiempo total | Para ver la salida |
| Tiempo por sector | De la vuelta lanzada |
| Velocidad máxima | Y en qué `s` |
| Velocidad mínima en cada curva | Usando la detección de curvas del selector de circuito |
| % del tiempo con W a fondo | Pista de si la nave va corta de punta |
| Contactos con barrera | Por nivel; si hubo recolocación, la tanda no vale |

Además, la **comprobación de las reglas** (sección 2), con ✓ o ✗ y la diferencia en segundos.

- Se escribe en la **consola** con `GD.Print`, en una tabla legible.
- Y en un archivo **CSV** en `user://balance/` con fecha y hora en el nombre (por ejemplo `balance_2026-11-05_19-30.csv`). En Windows: `%APPDATA%\Godot\app_userdata\VECTORY\balance\`. La ruta completa se escribe en la consola al terminar.

### 4.6 Cómo se lanza

- **Escena de herramienta** `res://tools/BalanceRunner.tscn`: se abre en el editor y se ejecuta con **F6**. No dibuja la carrera, solo un texto de progreso.
- En el Inspector de esa escena: naves (por defecto las tres), circuitos (por defecto todos los del catálogo, de pruebas incluidos), `USE BOOST` y margen de velocidad.
- También se puede lanzar sin ventana: `godot --headless res://tools/BalanceRunner.tscn`.
- **No está en el juego exportado** ni sale en ningún menú.

### 4.7 Piloto visible en el juego

- En una carrera de depuración, **F8** activa o desactiva el piloto automático en la nave del jugador. Así se puede ver si conduce con sentido.
- Al activarlo sale `AUTOPILOT` en una esquina, con el estilo del panel de pruebas.
- Cuenta como **usar el panel**: esa carrera no guarda récords ni fantasma.
- Con el piloto visible, el tiempo de vuelta tiene que salir **parecido al del simulador** (±0,1 s). Si no, hay algo que el simulador no hace igual que el juego, y **se avisa**.

### 4.8 Criterio de aceptación

- El piloto da vueltas a `circuit_01` y a `test_oval` con las tres naves, **sin recolocaciones**.
- Dos tandas iguales dan **exactamente** el mismo resultado.
- Con F8, el piloto conduce de forma razonable y su tiempo cuadra con el del simulador.
- Las cifras del piloto con las tres naves quedan **más o menos en línea** con lo que nota el usuario: HAMMER claramente más lenta en `circuit_01` antes de equilibrar. Si sale lo contrario, se revisa el piloto antes de seguir.

## 5. Etapa 2 · Barrido de parámetros

### 5.1 Qué hace

- En el Inspector de `BalanceRunner`, un modo **`SWEEP`**: nave, parámetro, valor inicial, valor final y paso.
- Parámetros que se pueden barrer: cualquiera de los **multiplicadores** de la spec 2 (`MaxSpeed`, `Acceleration`, `TurnRate`, `LateralGrip`, `BrakeDeceleration`) y la **curva** de la spec 5.
- Para cada valor, corre la tanda en los circuitos elegidos y apunta la vuelta lanzada, la diferencia con VECTOR y los sectores ganados.
- Al final, una tabla con todos los valores y una marca en los que **cumplen las reglas 1 y 2** en `circuit_01`.
- Modo **`MATCH`**: dada una nave y un parámetro, busca por bisección el valor con el que su vuelta lanzada queda **a menos de 0,05 s** de la de VECTOR. Si no hay ninguno dentro del rango, lo dice.

### 5.2 Límites

- El barrido respeta los rangos de siempre: multiplicadores **0,90–1,10** y curva **1,5–3,0**. Puede salir de ellos para experimentar, pero entonces lo marca en naranja, como el panel.
- **No cambia las definiciones.** Solo propone números. Los pasa a las definiciones el usuario o el agente cuando el usuario lo diga.
- Un parámetro cada vez. El barrido de dos parámetros a la vez queda fuera de alcance.

## 6. Etapa 3 · Valores definitivos

1. Con el barrido, el usuario elige los valores de BLADE y HAMMER y, si hace falta, la curva de VECTOR. Los porcentajes de VECTOR **no cambian**, porque es la referencia.
2. Se pasan a las definiciones. `ShipStats` (selector) se actualiza solo.
3. `BalanceRunner` en modo normal: las reglas 1 y 2 en `circuit_01` dan ✓.
4. **El usuario conduce** las tres naves en RACE y TIME TRIAL y da el visto bueno a la sensación: la tolerancia de BLADE, el ancho de cada una y si se notan distintas.
5. Si conduciendo algo no convence, se vuelve al paso 1. El piloto mide; manda lo que se siente conduciendo.

## 7. Parámetros

| Parámetro | Valor | Dónde |
|---|---|---|
| Margen de la regla 1 | ±0,3 s | Constante de la comprobación de reglas |
| Margen de la trazada a la barrera | 1 m | Piloto |
| Margen de velocidad del piloto | 97 % | Piloto (Inspector de `BalanceRunner`) |
| Error de línea en la calibración | 0,5 m | Piloto |
| Radios de la calibración | 30 a 600 m | Piloto |
| Precisión del modo `MATCH` | 0,05 s | `BalanceRunner` |
| Vueltas por tanda | 3 | `BalanceRunner` |

## 8. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Simulador** (puro): monta circuito + nave + física y avanza con paso fijo. No conoce el piloto: recibe la entrada de fuera.
- **Piloto automático** (puro), en su **propio namespace** y sin depender del panel de pruebas ni de `BalanceRunner`. Servirá de base para los rivales: trazada, calibración, perfil de velocidad y conducción, cada uno en su clase.
- **Comprobación de reglas** (puro): resultados → ✓/✗ por regla, con diferencias.
- **Informe** (puro): resultados → texto de consola y CSV.
- **Barrido** (puro): `SWEEP` y `MATCH` sobre el simulador.
- **`BalanceRunner`** (nodo, solo herramienta): lee el Inspector, llama a lo anterior y escribe el archivo. Sin reglas.
- **`Player`** (solo depuración): con F8, toma la entrada del piloto en vez del teclado. Sin reglas nuevas.

### Pruebas unitarias mínimas

Trazada:
1. En una recta, la trazada va por el centro.
2. En una curva, la trazada se desplaza hacia el interior y nunca pasa del desplazamiento máximo de la nave.
3. Con más semiancho de colisión, el desplazamiento máximo es menor.

Velocidad:
4. Calibración: a mayor radio, mayor velocidad máxima; con un radio enorme, la punta. Con más `TurnRate` o más agarre, la velocidad máxima no baja para el mismo radio.
5. El perfil de velocidad nunca pasa de la velocidad de la tabla ni de la punta.
6. Las frenadas del perfil respetan la frenada real de la nave: entre dos puntos, la velocidad no baja más de lo que la nave puede frenar en esa distancia.

Simulador y piloto:
7. Dos tandas iguales dan exactamente los mismos tiempos.
8. El simulador se puede usar sin nodos de Godot (corre dentro de las pruebas).
9. En `test_oval`, las tres naves completan la tanda sin recolocaciones.
10. Con `USE BOOST`, el boost se gasta en la recta más larga y en el mismo `s` para las tres naves.

Reglas, informe y barrido:
11. Reglas con datos inventados: diferencia de 0,29 s → ✓; 0,31 s → ✗. Una nave que gana todos los sectores → regla 2 ✗. Una nave que no gana ninguno → regla 2 ✗.
12. Los circuitos de pruebas salen en el informe, pero no hacen fallar las reglas.
13. El CSV tiene una fila por nave y circuito, con todas las columnas de la sección 4.5.
14. `SWEEP` da un resultado por valor del rango.
15. `MATCH` encuentra el valor con una función de prueba conocida, y avisa si no hay ninguno dentro del rango.

Las pruebas existentes siguen pasando sin cambiarlas. Si para simular sin escena hay que sacar lógica de `Player`, las pruebas de esa lógica tienen que dar lo mismo antes y después.

## 9. Prueba manual

**Etapa 1 · Simulador y piloto**
1. Abrir `BalanceRunner.tscn` y pulsar F6: progreso en pantalla y tabla en la consola con las tres naves y todos los circuitos. Sale la ruta del CSV y el archivo está ahí.
2. Ejecutarlo otra vez: los mismos números, al milésimo.
3. RACE en `circuit_01` con F8: el piloto conduce solo, frena antes de las curvas, no va haciendo eses y no choca. `AUTOPILOT` en la esquina.
4. Comparar el tiempo de esa vuelta con el del simulador: ±0,1 s.
5. Mirar si la tabla cuadra con lo que se nota conduciendo (HAMMER más lenta antes de equilibrar).

**Etapa 2 · Barrido**
6. `SWEEP` de la punta de HAMMER de 0,94 a 1,00 con paso 0,01: tabla con 7 filas y marcados los valores que cumplen las reglas.
7. `MATCH` de la curva de HAMMER: da un valor o dice que no lo hay.

**Etapa 3 · Valores definitivos**
8. Con los valores elegidos ya en las definiciones, `BalanceRunner`: reglas 1 y 2 en ✓ en `circuit_01`.
9. Conducir las tres naves: cada una se nota distinta y ninguna se siente imposible de batir.
10. SHIP SELECTION: los datos y barras de las naves reflejan los valores nuevos.
11. Menú principal: la versión es 0.14.2.

## 10. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Informe de la sección 3. Sin cambios | Validación del informe |
| **1 · Simulador y piloto** | Secciones 4.1–4.8 | Pruebas 1–13 · Prueba manual 1–5 · Commit `feat:` |
| **2 · Barrido** | Sección 5 | Pruebas 14–15 · Prueba manual 6–7 · Commit `feat:` |
| **3 · Valores definitivos** | Sección 6 · versión 0.14.2 | Prueba manual 8–11 · Commit `feat:` |

Si en la etapa 0 sale que hay que sacar lógica de `Player`, eso va **antes** de la etapa 1, en un commit `refactor:` aparte, y se valida que el juego se conduce exactamente igual.

No se empieza una etapa sin validar la anterior.

## 11. Fuera de alcance

- Rivales e IA de carrera: personalidades, errores, dificultad, adelantamientos. El piloto queda preparado para servir de base.
- Que el piloto derrape o use el boost de forma inteligente.
- Barrido de dos o más parámetros a la vez y optimización automática.
- Cambiar boost, daño, pendientes o peraltes por nave.
- Que el barrido cambie solo las definiciones.
- Cualquier cosa visible para el jugador fuera de los números de las naves.

## 12. Resultado

### 12.1 Valores finales

| Parámetro (× VECTOR) | BLADE | HAMMER |
|---|---|---|
| Punta | 1,04 | 0,98 |
| Aceleración | 0,94 | 1,10 |
| Giro | 0,90 | 1,04 |
| Agarre | 0,90 | 1,10 |
| Frenada | 1,02 | 0,96 |
| Curva de aceleración | 2,0 | 1,5 |

Los porcentajes y la curva de VECTOR no cambian.

En `circuit_01`, con el piloto y sin boost: BLADE 40,333 (−0,217), VECTOR 40,550 y HAMMER 40,783 (+0,233), sin contactos. BLADE gana S1 y S2, HAMMER gana S3 y VECTOR gana S4. Reglas 1 y 2 en ✓. Lo midió el piloto y el usuario lo dio por bueno conduciendo.

### 12.2 Lo que se hizo distinto de esta spec

- **F7 en vez de F8** para el piloto visible (4.7): F8 parece ser el atajo con el que Godot detiene el juego lanzado desde el editor, aunque no se ha confirmado.
- **Récords con el piloto** (4.7): la carrera no guarda récord ni fantasma, como pide la spec, pero no por "contar como usar el panel": desde la spec 5 el panel ya no los bloquea.
- **Margen del 97 %** (4.3): va solo sobre la velocidad de curva. Sobre la punta, la nave no llegaría nunca a ella.
- **Trazada** (4.2): es la mínima curvatura clásica, en la que cada punto toma la curvatura media de sus vecinos. Abre antes de la curva, en vez de solo desplazarse hacia el interior, porque eso cerraba el radio y penalizaba a la nave estrecha. A más de 160 m de una curva va forzada por el centro.
- **Empates en un sector** (2): los ganan todas las naves empatadas.
- **`MATCH`** (5.1): busca siempre en el rango permitido y compara en el primer circuito que no sea de pruebas.
- **`SWEEP` y `MATCH`** (5.1): solo escriben en la consola, sin CSV.
- **Los valores finales** (6) salieron de una búsqueda temporal de combinaciones, que no forma parte de `BalanceRunner`. El barrido de varios parámetros sigue fuera de alcance en la herramienta.

### 12.3 Límites conocidos

- El equilibrio está medido en `circuit_01`, sin boost ni derrape. En `test_oval` gana HAMMER los cuatro sectores (−0,233) y en `test_hills` los gana BLADE (−0,700).
- VECTOR gana S4 por solo 0,033 s: un cambio pequeño en cualquier nave se lo puede quitar. Lo vigila un test.
- La curva 1,5 de HAMMER está en el límite del rango. Sin ella, HAMMER ganaría también S4 y VECTOR se quedaría sin sectores.
- No se ha visto en pantalla elegir `Sweep` o `Match` desde el Inspector, ni el naranja de lo que se sale de rango.
- `tools/` hay que excluirlo en el preset cuando se exporte el juego por primera vez.
