# VECTORY — Spec: Rivales 1 · Parrilla llena

Versión 1.0 · 2026-11-16 · Estado: **propuesta, pendiente de validar**

Primera spec de los rivales con IA. Llena la parrilla de RACE con **7 rivales** que salen con el semáforo y dan las vueltas conduciendo con el **piloto automático** de la spec `naves-6-equilibrio`. Todos conducen igual: todavía no tienen nombre ni personalidad, y **no chocan** entre ellos ni contigo.

Plan de las specs de rivales:

1. **Parrilla llena** (esta).
2. Posición y resultados: posición en directo, orden de llegada, clasificación completa y **nombres de los pilotos**.
3. Contacto: choques entre naves con las reglas de daño de siempre, esquivar y adelantar.
4. Personalidades y dificultad: perfiles de los pilotos, boost, errores y FÁCIL / NORMAL / DIFÍCIL cambiando solo la IA. Sin *rubber banding*.

Se apoya en lo que ya existe:
- El **piloto automático** (trazada, calibración por nave, perfil de velocidad y conducción), en su propio namespace. Lo usan el simulador de equilibrio y F8.
- La **parrilla** de la spec `carrera` y su reparto aleatorio de las 8 posiciones sin repetir, con el generador inyectado.
- La **lista de naves en carrera** de la spec `radar-minimapa`, que leen el radar y el minimapa.
- `ShipHull3D` con `SetPaint` y `SetDamage`. Cada instancia tiene sus propios materiales, así que admite pinturas distintas a la vez. También se usan los **catálogos de pinturas** y `PaintChoice` de la spec `selector-pinturas`.
- La física completa de la nave: dinámica, curva de aceleración, capa vertical, pendientes, peraltes, barreras, recolocación, boost y daño. También `RaceTracker` (vueltas, checkpoints y sectores).

Rama: `feature/rivals-grid`, que sale de `develop` con `feature/paint-select` ya mergeada. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- Solo en **RACE**. TIME TRIAL sigue con el circuito vacío.
- **8 participantes**: tú, en una marca aleatoria como hoy, y 7 rivales en las otras 7.
- Cada rival lleva una **nave aleatoria** de las tres y una **pintura aleatoria** de las 40 de esa nave. Las dos cambian en cada carrera.
- Los rivales usan **la misma física y las mismas reglas que tú**: barreras, recolocación, daño por golpes contra la barrera y boost por cargas.
- Salen en el **radar** y en el **minimapa**.
- **No chocan** entre ellos ni contigo: se atraviesan. El contacto llega en la spec 3.
- La **posición del HUD y la de resultados no cambian** en esta spec. Las calcula la spec 2.
- **Etapa 0 · Inventario**: se mira y se apunta, sin cambiar nada.
- **Etapa 1 · Rivales en parrilla**: aparecen colocados, con su nave y su pintura, y se quedan quietos.
- **Etapa 2 · Rivales en carrera**: conducen, cuentan vueltas y terminan.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo está montada hoy tu nave en la escena de carrera: qué nodo tiene la física y qué parte es solo del jugador (cámaras, cabina, HUD, efectos de velocidad, input). Hay que saber qué se puede **reutilizar tal cual** para una nave de la IA. **Si hay que sacar algo de `Player`, se avisa antes**, con la lista.
2. Cómo cambia F8 la entrada de la nave del teclado al piloto. Es el mismo mecanismo que usarán los rivales.
3. Cuánto tarda en calcularse la **trazada y la calibración** de una nave en `circuit_01`. Con tres naves distintas como mucho, la carga de la carrera no debería alargarse más de **1 s**. Si se pasa, se avisa y se propone una caché.
4. Cómo se oculta tu casco en la vista de cabina (máscara de la cámara u otra cosa). Los cascos de los rivales **sí** tienen que verse desde la cabina.
5. Qué efectos lleva hoy tu nave por fuera (toberas, estela, luces del boost, humo del daño) y cuáles son de `ShipHull3D` y cuáles de la cámara o del jugador.
6. Cómo se apuntan hoy las naves de prueba (F7) en la lista de naves en carrera.
7. Si `RaceTracker` se puede tener **una instancia por nave** sin tocar su lógica.

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Rivales en parrilla

### 3.1 Alineación

Al montar una carrera de RACE se hace la **alineación**: la lista de los 7 rivales.

- **Parrilla**: se usa el reparto aleatorio de las 8 posiciones que ya existe. La tuya sale igual que hoy y los rivales ocupan las otras 7.
- **Nave**: cada rival recibe una de las tres naves al azar, con la **misma probabilidad** y **sin límite de repeticiones**. Puede haber siete HAMMER, aunque es muy raro.
- **Pintura**: cada rival recibe una de las **40 combinaciones** de su nave al azar, con la misma probabilidad (20 pinturas × lisa / con marcas).
  - Dos rivales con la misma nave **pueden** llevar la misma pintura, y un rival puede llevar la misma que tú. No se evita.
- Todo sale del **mismo generador inyectado** que la parrilla. Con la misma semilla, la alineación sale igual.
- Cada rival tiene un **identificador** fijo dentro de la carrera, de `rival_1` a `rival_7`. Los nombres llegan en la spec 2. En esta spec no se ve ningún nombre en pantalla.
- **RESTART** desde la pausa y **Enter** en resultados hacen una alineación **nueva**: otra parrilla, otras naves y otras pinturas.
- Los campeonatos todavía no existen. Lo decidido para cuando lleguen está en la sección 9.

### 3.2 Colocación

- Cada rival se coloca en su marca **igual que tú**: centrado, con el morro justo detrás de la línea transversal, mirando en el sentido de la marcha, a velocidad 0 y sin daño.
- Si con los anchos de colisión de las naves dos marcas quedan tan juntas que los cascos se **pisan**, se avisa con las medidas. Las marcas no se tocan en esta spec.

### 3.3 Aspecto

- El rival es la **nave entera de su modelo**: `ShipHull3D` con su pintura y los efectos exteriores que tenga tu nave en cámara exterior (toberas, estela, luces del boost y humo del daño), según lo que diga el inventario.
- **No** lleva nada de lo que es solo tuyo: cámaras, cabina 3D, HUD, efectos de velocidad de la cámara (visión túnel, líneas, FOV) ni sacudida.
- Se ve **desde la cabina y desde la cámara exterior**.
- El daño se ve como en tu nave (`SetDamage`).

### 3.4 Radar y minimapa

- Los 7 rivales se apuntan en la **lista de naves en carrera**. El radar y el minimapa los enseñan sin tocarlos.
- Las naves de prueba (F7) siguen existiendo en depuración y conviven con los rivales.

### 3.5 Criterio de aceptación de la etapa 1

- Al empezar una carrera, las 8 marcas están ocupadas: tú en una y los 7 rivales en las demás, con naves y pinturas variadas.
- En la previa, todos quietos. Tras el GO, los rivales **siguen quietos**: conducir es la etapa 2.
- Se ven bien desde la cabina y desde fuera, salen en el radar y el minimapa, y RESTART cambia la alineación.
- La carga de la carrera no se nota más lenta.

## 4. Etapa 2 · Rivales en carrera

### 4.1 Conducción

- Cada rival conduce con el **piloto automático** de la spec `naves-6-equilibrio`, con la trazada y la calibración **de su nave**.
- La trazada y la calibración se calculan **una vez por nave y circuito** al montar la carrera, y las comparten los rivales que llevan la misma nave. Solo se calculan las naves que hay en la parrilla.
- El piloto recibe **la misma entrada que da el teclado**, como con F8. No derrapa.
- **Salida**: en la previa no recibe entrada, como tú. En el GO arranca y, desde su marca, se incorpora a la trazada con la conducción de siempre (*pure pursuit*).
- **Ritmo**: un solo parámetro para todos los rivales, el **margen de velocidad** del piloto (el 97 % de la spec 6). Se ajusta probando hasta que llegue la dificultad en la spec 4.
  - Como todos conducen igual, los que llevan la misma nave irán casi en fila por la misma línea y se atravesarán. Es lo esperado en esta spec.

### 4.2 Boost

- Cargas como las tuyas en RACE: empieza con 0, gana 1 cada 35 s desde el GO y tiene un máximo de 3, con el cooldown de siempre.
- Lo usa como en el **escenario B** de la spec 6: gasta **una** carga al entrar en la recta más larga, si la tiene y no hay cooldown.
- En daño nivel 3 no hay boost, como para ti.

### 4.3 Barreras, recolocación y daño

- **Las mismas reglas que tú**: roce, medio y fuerte, rebote, corrección temporal, destello en la barrera, recolocación por atasco o salida y daño acumulado por golpes contra la barrera.
- La **sacudida de cámara** es solo tuya: un rival que choca no sacude tu cámara.

### 4.4 Vueltas y llegada

- Cada rival tiene **su propio `RaceTracker`**: vueltas, checkpoints, sectores y la regla del primer cruce de meta tras el GO, igual que tú.
- Al completar la última vuelta, el rival **termina**:
  - el boost en curso se cancela como en tu llegada;
  - sigue con el piloto a un **ritmo de vuelta de enfriamiento**, el **60 %** de su velocidad objetivo y sin boost, para no quedarse parado en mitad de la pista.
- Cuando terminas tú, los rivales que no han terminado **siguen corriendo** mientras sale la pantalla de resultados. Qué cuenta en la clasificación es de la spec 2.
- Los rivales **no** guardan récords ni afectan a los tuyos.

### 4.5 Pausa y depuración

- La **pausa** congela a los rivales con todo lo demás.
- **F8** sigue poniendo el piloto en tu nave, sin cambiar nada de los rivales.
- **Panel de pruebas (F9)**: gana un bloque **RIVALS**:
  - una fila por rival con identificador, nave, pintura, vuelta, nivel de daño, contactos con barrera y recolocaciones;
  - un deslizador de **ritmo** del 85 % al 100 %, que se aplica al momento y no se guarda en disco.

### 4.6 Criterio de aceptación de la etapa 2

- En el GO salen los 8 y los rivales dan las 3 vueltas en `circuit_01` y en `test_oval` sin quedarse atascados.
- Frenan antes de las curvas, no hacen eses y casi no chocan.
- Las tres naves de la IA se notan distintas: BLADE más rápida en las rectas y HAMMER en las salidas de curva.
- Con el ritmo por defecto, conduciendo bien se les puede **seguir el ritmo**. Si te sacan mucho o se quedan muy atrás, se ajusta el ritmo y se apunta el valor.
- Con 8 naves en pista el juego va **igual de fluido** que con una. Si baja de 60 fps en el PC del usuario, se avisa con lo que más cuesta.

## 5. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Número de rivales | participantes − 1 (7) | Carrera |
| Ritmo de los rivales (margen de velocidad del piloto) | 97 % | Carrera |
| Ritmo de la vuelta de enfriamiento | 60 % de la velocidad objetivo | Carrera |

## 6. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Alineación** (pura): con el generador, la parrilla y los catálogos de naves y pinturas, da la lista de rivales. Cada entrada tiene el identificador, la marca, la nave y la `PaintChoice`. Sin nodos ni archivos.
- **Conductor de rival** (puro): envuelve el piloto automático. Decide la entrada según el estado de la carrera (previa, en carrera, terminado), el boost (4.2) y el ritmo. Sin reglas de física propias: la física es la de siempre.
- **Caché de trazadas** (pura): trazada y calibración por (circuito, nave), calculadas una vez y compartidas.
- **Nave de la IA** (nodo): la misma nave y física que la tuya con la entrada del conductor de rival, su `ShipHull3D` y su `RaceTracker`. Si el inventario dice que la nave del jugador está muy atada a `Player`, primero va el `refactor:` de la sección 8.
- **Montaje de la carrera**: hace la alineación, crea las naves de la IA, las apunta en la lista de naves en carrera y las quita al salir. En TIME TRIAL no hace nada de esto.
- **HUD, radar y minimapa**: sin cambios, solo leen la lista.

## 7. Pruebas

### Pruebas unitarias mínimas

Alineación (etapa 1):
1. Con 8 participantes salen 7 rivales, con las marcas 1–8 sin repetir entre todos y la tuya fuera de las suyas.
2. Con la misma semilla, la misma alineación: marcas, naves y pinturas.
3. Todas las naves son de las tres que hay y todas las pinturas son del catálogo **de su nave**.
4. Con muchas semillas salen las tres naves, y lisas y con marcas.

Conductor de rival (etapa 2):
5. En la previa no da entrada. En carrera, sí.
6. Boost: solo se gasta al entrar en la recta más larga, con carga y sin cooldown, y de una en una. En daño nivel 3, nunca.
7. Terminado: ritmo del 60 % y sin boost.
8. El ritmo escala la velocidad objetivo: con el 90 %, el objetivo es el 90 % de la tabla.

Caché y carrera (etapa 2):
9. Dos rivales con la misma nave comparten trazada y calibración. Una nave que no está en la parrilla no se calcula.
10. Cada rival lleva su propia cuenta de vueltas: que uno complete una vuelta no cambia la de otro ni la tuya.
11. En TIME TRIAL no se crea ningún rival.

Las pruebas existentes siguen pasando. Si hay `refactor:` previo, sus pruebas dan lo mismo antes y después.

## 8. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| *(si hace falta) Refactor* | Sacar de `Player` lo que necesite la nave de la IA | Pruebas existentes · el juego se conduce exactamente igual · Commit `refactor:` |
| **1 · Rivales en parrilla** | Sección 3 | Pruebas 1–4 · Prueba manual 1–5 · Commit `feat:` |
| **2 · Rivales en carrera** | Sección 4 · versión | Pruebas 5–11 · Prueba manual 6–15 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Rivales en parrilla**
1. RACE en `circuit_01`: las 8 marcas ocupadas, tú en una y 7 rivales en las demás, con naves y pinturas variadas.
2. Mirar desde la cabina y desde fuera: los rivales se ven con su casco 3D y su pintura. Si dos cascos se pisan en la parrilla, apuntarlo.
3. Radar y minimapa: salen los rivales cercanos.
4. Tras el GO, los rivales siguen quietos (etapa 1). Tú conduces como siempre.
5. RESTART varias veces: cambian tu marca, las naves y las pinturas. TIME TRIAL: circuito vacío.

**Etapa 2 · Rivales en carrera**
6. En el GO salen todos y se incorporan a la trazada sin cosas raras.
7. Seguirlos un rato: frenan antes de las curvas, no hacen eses y se atraviesan entre ellos y contigo sin chocar.
8. En la recta más larga, algunos gastan boost. Se ven sus luces de boost.
9. Si alguno toca la barrera: destello, pérdida de velocidad y daño, como tú, pero sin sacudir tu cámara.
10. F9: el bloque RIVALS con las vueltas de cada uno. Mover el ritmo: van más rápido o más lento al momento.
11. Terminar la carrera: los rivales que ya han acabado siguen rodando despacio. Los demás siguen compitiendo detrás de la pantalla de resultados.
12. Pausa: todo congelado. Continuar: siguen.
13. F8: el piloto conduce tu nave y los rivales no cambian.
14. Varias carreras seguidas: con el ritmo por defecto, ¿se les puede seguir conduciendo bien? Apuntar el ritmo que mejor queda.
15. Mirar los fps con los 8 en pista, sobre todo en la salida, que es cuando están todos juntos.

## 9. Fuera de alcance

- **Posición** en directo y en resultados, orden de llegada, clasificación y **nombres de los pilotos** (spec 2).
- **Choques entre naves**, esquivar y adelantar (spec 3).
- **Personalidades**, errores, uso del boost con cabeza, derrape de la IA y **dificultad** (spec 4).
- **Campeonatos**. Decidido para cuando lleguen: cada rival mantiene la **misma nave y la misma pintura** en todas las carreras del campeonato.
- Que la nave o la pintura dependan de la personalidad: decidido que **no**. La nave no determina la personalidad.
- Rivales en TIME TRIAL, fantasmas de rivales y récords de rivales.
- Cambiar las marcas de la parrilla.
- Sonido de los motores de los rivales.
