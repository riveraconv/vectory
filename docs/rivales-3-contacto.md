# VECTORY — Spec: Rivales 3 · Contacto entre naves

Versión 1.0 · 2026-11-16 · Estado: **propuesta, pendiente de validar**

Tercera spec de rivales. Las naves **dejan de atravesarse**: chocan entre ellas y contigo con los niveles de siempre (roce, medio, fuerte), con un **reparto de daño** que castiga más al que embiste. Los rivales aprenden lo mínimo para no comerse por detrás: si no tienen la trazada clara, **se apartan o frenan**.

Plan de las specs de rivales:

1. Parrilla llena (hecha).
2. Clasificación y pilotos (hecha).
3. **Contacto** (esta).
4. Personalidades y dificultad. Ahí va **todo el comportamiento completo**: defender, cerrar la puerta, agresividad, errores, uso del boost con cabeza y FÁCIL / NORMAL / DIFÍCIL.

Se apoya en lo que ya existe:
- La **colisión con la barrera** de la spec `barreras-y-colisiones`: niveles, umbrales, restitución, pérdidas, corrección temporal, espera de 0,3 s, sacudida y recolocación.
- El **modelo de daño** de la spec `danos`.
- El **semiancho de colisión** de cada nave y los **manifiestos** de los cascos 3D (caja total).
- Las naves de la IA, su conductor de rival, la caché de trazadas y la lista de naves en carrera de la spec `rivales-1-parrilla`.
- La **distancia de carrera** y la clasificación de la spec `rivales-2-clasificacion`.
- El marco de la pista (`s`, `d`, `h`) y la capa vertical.

Rama: `feature/rivals-3-contact`, que sale de `master` con la spec 2 ya mergeada. Versión: la siguiente **minor** sobre la que haya al empezar. Se sube en el commit de la última etapa.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- Solo en **RACE** (en TIME TRIAL no hay más naves). El fantasma y las naves de prueba (F6) **no** chocan.
- Cada nave es un **rectángulo orientado** sobre la pista, con su semiancho y su medio largo.
- **Todas pesan lo mismo.**
- Nivel del golpe con la **velocidad relativa** entre las dos naves: los mismos umbrales que la barrera (30 / 90 km/h).
- **Daño**: el que **embiste** paga entero; el que **recibe**, solo en golpes fuertes; puerta con puerta, los dos igual.
- **Sacudida de cámara** como con la barrera, si el golpe es contigo.
- Sin chispas, destellos ni humo: llegan con las llamas y estelas.
- **Atravesables**: las naves que han terminado y, durante 2 s, las recién recolocadas.
- La salida es **normal**: los choques funcionan desde el GO.
- **Etapa 0 · Inventario**: mirar y apuntar, sin cambiar nada.
- **Etapa 1 · Choques**: detección, respuesta, daño y sacudida. Los rivales todavía conducen **sin mirar**.
- **Etapa 2 · Trazada clara**: los rivales se apartan o frenan si tienen a alguien delante.

## 2. Etapa 0 · Inventario

El agente mira y apunta en un **informe corto**, sin cambiar nada:

1. Cómo guarda la dinámica la nave: si la **velocidad es un vector** aparte de la orientación (con deslizamiento lateral que el agarre corrige) o si va siempre hacia el morro. El choque entre naves cambia la velocidad con un empujón que puede ir de lado (sección 3.3). Si la dinámica no lo admite, **se avisa antes** con una propuesta.
2. Cómo aplica hoy la colisión con la barrera sus cambios de velocidad y dirección, para **reutilizar** lo mismo.
3. El **medio largo** de cada nave: de la caja total del manifiesto de cada casco 3D (**sin la sonda** en BLADE). Apuntar los tres valores.
4. La **altura de levitación** de las naves y cuánto cambia en crestas, pendientes y peraltes, para el umbral de altura de 3.1.
5. En qué orden se mueven hoy las 8 naves en cada paso de física, y si hay un sitio común donde meter el paso de **contactos** después de mover todas.
6. Si el modelo de daño admite sumar **puntos sueltos** además de `AddImpact(nivel)`. El reparto de la sección 4 lo necesita.
7. Cómo podría hacerse **semitransparente** o parpadear una nave entera (`ShipHull3D`) para la recolocación (sección 5.2).

**Si algo es dudoso, se pregunta antes de seguir.**

## 3. Etapa 1 · Choques

### 3.1 Forma y detección

- Cada nave es un **rectángulo orientado** en el plano de la pista. Va centrado en la nave y orientado con su morro, y mide **semiancho de colisión × medio largo** (inventario, punto 3).

| Nave | Semiancho | Medio largo |
|---|---|---|
| VECTOR | 2,63 m (el del casco 3D) | del manifiesto |
| BLADE | 1,61 m | del manifiesto, sin sonda |
| HAMMER | 2,5 m | del manifiesto |

- Dos naves están en **contacto** cuando sus rectángulos se solapan (separación de ejes, SAT) **y** su diferencia de altura es menor que **1,5 m** (parámetro). Así, si una va en el aire por una cresta, pasa por encima.
- El contacto da la **normal** (el eje de menor solape, de una nave hacia la otra) y la **profundidad** del solape.
- Fiable a velocidad máxima con boost: si en un paso una nave pasaría de detrás a delante de otra, cuenta como contacto. Se comprueba con la posición del paso anterior (barrido) o subdividiendo el paso. Lo elige el agente.
- Con 8 naves son 28 parejas. Primero se descartan con la distancia de carrera: si dos naves están a más de **20 m**, no se miran.

### 3.2 Orden en cada paso de física

1. Todas las naves se mueven con su dinámica, **barrera incluida**, como hoy.
2. Se resuelven los contactos entre parejas, en un **orden fijo** (por identificador), para que sea determinista.
3. Si al separar dos naves alguna queda contra la barrera, se aplica la **barrera** otra vez. Ninguna nave queda dentro de la barrera ni un paso.

### 3.3 Respuesta

Con las dos naves y la normal del contacto:

- **Separación**: cada una se mueve **la mitad de la profundidad** por la normal, hacia fuera. Si una está contra la barrera, la otra se lleva la separación entera.
- **Velocidad de cierre**: la parte de la velocidad relativa que va de una hacia la otra por la normal. Si se están separando ya, no hay golpe: solo separación.
- **Nivel**, con la velocidad de cierre en km/h y los **umbrales de la barrera**:

| Velocidad de cierre | Nivel |
|---|---|
| Menos de 30 km/h | **Roce** |
| De 30 a 90 km/h | **Medio** |
| 90 km/h o más | **Fuerte** |

- **Empujón**: como pesan lo mismo, cada nave recibe la mitad del cambio de velocidad por la normal, con la **restitución** de la barrera:
  - **Roce**: 0. Se quita la velocidad de cierre y las dos se deslizan juntas.
  - **Medio**: 0,4.
  - **Fuerte**: 0,5.

  Con esto sale solo lo que se espera: en un **alcance** por detrás, el de detrás pierde velocidad y el de delante gana un poco. **Lado con lado**, las dos se separan.
- **Pérdida de velocidad extra**, además del empujón:

| Nivel | El que embiste | El que recibe | Mutuo (cada uno) |
|---|---|---|---|
| Roce | 4 % al entrar y 20 km/h/s mientras dure | Igual | Igual |
| Medio | 20 % | Ninguna | 20 % |
| Fuerte | 45 % | Ninguna | 45 % |

- **Corrección temporal** de la barrera (1 s, giro y aceleración del 50 % al 100 %): en golpes **fuertes**, para **las dos** naves. Al que recibe también lo descoloca.
- **Dirección**: el choque **no gira** el morro directamente. El empujón deja velocidad de lado y el agarre la corrige, como en un derrape. Si el inventario dice que la dinámica no puede, se avisa (punto 1).
- **Espera**: un golpe medio o fuerte entre la **misma pareja** no se vuelve a contar hasta pasados **0,3 s**. El roce es continuo.
- **Boost**: como con la barrera, un golpe no lo cancela. Las pérdidas se aplican también durante la subida y el mantenimiento.
- Rozar o chocar no cambia vueltas, checkpoints ni récords.

### 3.4 Quién embiste

En el momento del golpe (medio o fuerte), se mira cuánto va cada nave **hacia la otra** por la normal:

- Cada nave tiene una **aportación**: su velocidad proyectada en la normal, hacia la otra. Si se aleja, la aportación es 0.
- **Embiste** la de mayor aportación.
- **Mutuo**: si la menor es al menos el **70 %** de la mayor (diferencia de menos del 30 %).

En la práctica:
- **Alcance por detrás**: embiste siempre el de detrás.
- **Golpe de lado**: embiste el que se mete en la trayectoria del otro.
- **Puerta con puerta**: mutuo.

Se apunta en la consola de depuración: quién, a quién, nivel y si fue mutuo.

## 4. Daño

Solo en RACE, con el modelo de daño de siempre:

| Golpe | El que embiste | El que recibe | Mutuo (cada uno) |
|---|---|---|---|
| Roce | 0 | 0 | 0 |
| Medio | +1 | 0 | +1 |
| Fuerte | +3 | +1 | +3 |

- Los niveles (0–3) y sus consecuencias no cambian. Valen igual para ti y para los rivales.
- Se suman a los de la barrera en el mismo contador.
- Las naves que han terminado son atravesables (5.1), así que no suman nada después de la meta.

## 5. Atravesables

Una nave atravesable **no choca con otras naves**, pero **sí con la barrera**.

### 5.1 Naves que han terminado

- En cuanto una nave cruza la meta en la última vuelta, es atravesable hasta el final. Así nadie se estrella contra un rival que va a paseo al 60 %.
- No cambia de aspecto.

### 5.2 Recolocación

- Una nave recolocada es atravesable **2 s**.
- Durante esos 2 s, su casco **parpadea**: baja al 40 % de opacidad y vuelve, a **4 Hz**. Si no se puede hacer semitransparente, se avisa con lo que sí se puede (inventario, punto 7).
- Si al acabar los 2 s sigue solapada con otra nave, **sigue atravesable** hasta que deje de solaparse.
- En la vista de cabina, tu propia nave no parpadea (no se ve el casco).

### 5.3 Atasco

- El atasco de la recolocación pasa a contar con el contacto **con la barrera o con otra nave**: a menos de 10 km/h durante 3 s seguidos. Así no hay dos naves encajadas para siempre.
- Lo demás de la recolocación, igual.

### 5.4 Salida

- **Normal**: los choques funcionan desde el GO, sin ventana atravesable.
- En la previa las naves están quietas y no se comprueba nada. Si con los anchos dos marcas de parrilla se **solapan**, se avisa (ya lo pedía la spec 1) y no se cuenta como golpe.

## 6. Sacudida de cámara

- Si el golpe es **contigo** (embistas, recibas o sea mutuo), sacudida de cámara **como con la barrera**: solo medio y fuerte, con la misma tabla y al 50 % en cabina.
- Un golpe entre dos rivales no sacude tu cámara.
- Sin chispas, destellos, humo ni partículas: van con las **llamas y estelas** (fuera de alcance).

## 7. Criterio de aceptación de la etapa 1

- Ya no se atraviesa a nadie (salvo los atravesables).
- Rozar lado con lado: las naves se separan un poco y pierden algo de velocidad.
- Alcanzar a un rival por detrás: tú pierdes y él gana un poco. Si vas fuerte, tú te llevas el daño y él, como mucho, 1 punto.
- Que te embistan de lado: te empuja y te descoloca, sin daño en un medio.
- Nadie atraviesa la barrera al ser empujado.
- Los rivales todavía **no** esquivan: se chocan entre ellos y contigo. Es lo esperado en esta etapa.

## 8. Etapa 2 · Trazada clara

Lo mínimo para que los rivales no se coman por detrás. **No** defienden, no cierran la puerta, no son agresivos y no cometen errores: eso va completo en la spec 4.

### 8.1 Qué ve cada rival

- Mira las naves (rivales **y tú**) que van **delante** en distancia de carrera, hasta una **distancia de mirada**: la mayor entre **30 m** y **1,5 s** a su velocidad.
- Una nave **le tapa la trazada** si su rectángulo, con **0,5 m** de margen por lado, se cruza con el pasillo de su trazada (el ancho de la nave del rival centrado en la trazada).
- Solo cuenta si va **más lenta** o a la misma velocidad.
- Las atravesables no cuentan.

### 8.2 Apartarse

Si alguien le tapa la trazada:

1. Busca un **hueco a un lado**: un desplazamiento lateral respecto a la trazada que deje **1 m** entre los dos cascos. Prueba los dos lados y elige el que tenga **más hueco**. El hueco se cuenta con la barrera (margen de 1 m, como la trazada), con la nave de delante y con cualquier otra que haya al lado.
2. Si hay hueco, **se aparta** poco a poco, a **4 m/s** de velocidad lateral como mucho. Sigue con el *pure pursuit* de siempre, apuntando a la trazada desplazada.
3. **Velocidad objetivo** mientras va apartado: la de la tabla de su nave para el radio de la **línea desplazada** (el radio de la trazada más o menos el desplazamiento). Por fuera de una curva, va más lento.
4. Cuando ya ha pasado a la otra nave (su cola, **5 m** por delante del morro del otro), **vuelve** a la trazada poco a poco, a la misma velocidad lateral, y solo si no hay nadie en ese lado.
5. Nunca se aparta hacia un sitio donde ya hay una nave al lado.

### 8.3 Frenar

Si no hay hueco a ningún lado:

- Se queda **detrás**, con un hueco de **0,5 s** a su velocidad (como mínimo **8 m**) respecto a la nave de delante, y la iguala en velocidad.
- Frena con la frenada normal, sin bloquear: si va muy encima, frena más.
- En cuanto se abre un hueco, se aparta (8.2).

### 8.4 Boost

- Se gasta como en la spec 1 (una carga al entrar en la recta más larga), pero **solo con la trazada clara**.
- Si al entrar en la recta alguien la tapa, espera a que quede clara **mientras siga en esa recta**. Si la recta se acaba, no lo gasta en esa vuelta.

### 8.5 Criterio de aceptación de la etapa 2

- En la salida, los rivales salen juntos y se ordenan sin un choque en cadena.
- Con la misma nave y la misma trazada, en vez de comerse por detrás se ponen en fila o se abren para pasar.
- A un rival que alcanza a otro más lento lo pasa por el lado si hay hueco, y vuelve a la trazada.
- Si tú vas lento o parado en la trazada, te esquivan o frenan.
- En una carrera normal, los golpes **medios o fuertes entre rivales** son pocos. Se cuentan en F9 y se apunta el número.

## 9. Pausa y depuración

- La **pausa** congela los contactos, el parpadeo de la recolocación y la sacudida.
- **Panel de pruebas (F9)**, bloque RIVALS:
  - nuevas columnas por rival: **golpes con naves** (embestidas / recibidos / mutuos) y **puntos de daño**;
  - un interruptor **CONTACT** (por defecto activado) para volver a atravesarse y comparar. No se guarda en disco.
- Etapa 2: en depuración, una tecla o un interruptor del panel dibuja, encima de cada rival, el **estado** (`CLEAR`, `PASS L`, `PASS R`, `FOLLOW`) y su pasillo, para ver qué decide.

## 10. Parámetros en el Inspector

| Parámetro | Valor | Dónde |
|---|---|---|
| Diferencia de altura máxima para chocar | 1,5 m | Colisiones |
| Distancia de descarte entre parejas | 20 m | Colisiones |
| Umbrales roce → medio → fuerte | 30 / 90 km/h (los de la barrera) | Colisiones |
| Restitución roce / medio / fuerte | 0 / 0,4 / 0,5 (los de la barrera) | Colisiones |
| Pérdidas por nivel | Tabla de 3.3 | Colisiones |
| Margen para golpe mutuo | 70 % | Colisiones |
| Espera entre golpes de la misma pareja | 0,3 s | Colisiones |
| Puntos de daño por papel | Tabla de 4 | Daños |
| Tiempo atravesable tras recolocar | 2 s | Recolocación |
| Parpadeo tras recolocar | 40 % · 4 Hz | Nave |
| Distancia de mirada | máx(30 m, 1,5 s) | Rival |
| Margen del pasillo | 0,5 m por lado | Rival |
| Hueco entre cascos al apartarse | 1 m | Rival |
| Velocidad lateral máxima | 4 m/s | Rival |
| Distancia para volver a la trazada | 5 m | Rival |
| Hueco al seguir | 0,5 s (mínimo 8 m) | Rival |

## 11. Arquitectura

Respetar la estructura de carpetas y namespaces actual.

- **Forma de la nave** (pura): semiancho y medio largo, desde el perfil y el manifiesto.
- **Contacto entre naves** (puro): con las dos formas, posiciones, orientaciones, alturas y velocidades, da si hay contacto, la normal, la profundidad y la velocidad de cierre.
- **Respuesta al contacto** (pura): separación, nivel (con la clasificación de la barrera), empujón, pérdidas, corrección y **papeles** (embiste / recibe / mutuo). Sin nodos.
- **Reparto de daño** (puro): papel + nivel → puntos.
- **Espera por pareja** (pura): como la de la barrera, por pareja.
- **Estado atravesable** (puro): terminada, temporizador de recolocación y «sigue solapada».
- **Paso de contactos** (en el montaje de la carrera o donde diga el inventario): después de mover todas las naves, recorre las parejas en orden fijo, aplica la respuesta y vuelve a pasar la barrera. Sin reglas dentro.
- **Detector de atasco**: gana el contacto con naves como entrada. Sus reglas no cambian.
- **Trazada clara** (pura, en el namespace del piloto): con la trazada, la nave y las demás naves cercanas, da el estado (`CLEAR`, `PASS L/R`, `FOLLOW`), el desplazamiento lateral objetivo y la velocidad máxima. El **conductor de rival** la usa. El piloto automático de F7 **no** la usa.
- **Cámaras**: siguen leyendo solo la sacudida.

## 12. Pruebas

### Pruebas unitarias mínimas

Detección (etapa 1):
1. Dos rectángulos separados: sin contacto. Solapados: contacto, con la normal y la profundidad correctas.
2. Solapados en planta pero con 2 m de diferencia de altura: sin contacto.
3. Una nave que en un paso pasaría de detrás a delante de otra: contacto (no se la salta).
4. Con más semiancho, el contacto llega antes en lateral. Con más largo, antes en un alcance.

Respuesta (etapa 1):
5. Velocidad de cierre de 20 / 60 / 120 km/h → roce / medio / fuerte. 30 → medio; 90 → fuerte.
6. Alcance: el de detrás pierde y el de delante gana. Con restitución 0, después del golpe van a la misma velocidad por la normal.
7. Tras la separación, ninguna nave queda solapada ni dentro de la barrera. Si una está contra la barrera, la otra se lleva toda la separación.
8. Dos naves que ya se separan: sin golpe.
9. Dos golpes medios de la misma pareja separados 0,1 s cuentan como uno; separados 0,5 s, como dos.
10. Fuerte: corrección temporal para las dos.

Papeles y daño (etapa 1):
11. Alcance por detrás: embiste el de detrás.
12. Aportaciones 10 y 9: mutuo. 10 y 6: embiste el de 10.
13. Medio: +1 / 0 / +1 cada uno si es mutuo. Fuerte: +3 / +1 / +3 cada uno. Roce: 0.
14. Pérdidas: el que recibe un medio no pierde nada extra; el que embiste pierde un 20 %.

Atravesables (etapa 1):
15. Una nave terminada no choca con nadie, pero sí con la barrera.
16. Recolocada: atravesable 2 s. Si sigue solapada a los 2 s, sigue atravesable hasta que deje de estarlo.
17. Atasco contra otra nave a 5 km/h durante 3 s: recoloca, una sola vez.

Trazada clara (etapa 2):
18. Nadie delante: `CLEAR` y desplazamiento 0.
19. Una nave más lenta en el pasillo, con hueco a la izquierda: `PASS L`, sin pasar del límite de la barrera.
20. Sin hueco a ningún lado: `FOLLOW`, con la velocidad del de delante y el hueco de 0,5 s (mínimo 8 m).
21. Una nave más rápida delante: `CLEAR`.
22. El desplazamiento cambia como mucho 4 m/s.
23. Vuelve a la trazada solo después de pasar al otro (5 m) y si ese lado está libre.
24. Boost: con la trazada tapada no se gasta; al quedar clara en la misma recta, sí.

Las pruebas existentes siguen pasando. Si se toca la dinámica (inventario, punto 1), sus pruebas dan lo mismo antes y después.

## 13. Etapas

| Etapa | Qué entra | Cierre |
|---|---|---|
| **0 · Inventario** | Sección 2 | Informe corto. Sin commit |
| *(si hace falta) Refactor* | Lo que diga el inventario (velocidad lateral, puntos sueltos en el daño, paso común de física) | Pruebas existentes · el juego se conduce exactamente igual · Commit `refactor:` |
| **1 · Choques** | Secciones 3–7 y 9 (sin el dibujo de estados) | Pruebas 1–17 · Prueba manual 1–11 · Commit `feat:` |
| **2 · Trazada clara** | Sección 8 y el dibujo de estados de la 9 · versión | Pruebas 18–24 · Prueba manual 12–18 · Commit `feat:` |

No se empieza una etapa sin validar la anterior.

### Prueba manual

**Etapa 1 · Choques**
1. RACE: ya no se atraviesa a los rivales. En la salida, chocan entre ellos (todavía no miran).
2. Ponerse al lado de un rival y cerrarse poco a poco: roce, se separan, sin daño ni sacudida.
3. Alcanzar a un rival por detrás con poca diferencia: él sale un poco empujado y tú frenas. Medio: un punto de daño para ti (F9), sacudida, y él sin daño.
4. Alcanzarlo muy fuerte: tú +3, él +1, los dos con la corrección de 1 s.
5. Que un rival te embista de lado: te empuja y se nota el derrape, sin pasar por la barrera.
6. Pegado a la barrera, que te empujen contra ella: no la atraviesas y cuenta el golpe con la barrera.
7. Repetir en cabina: sacudida al 50 %.
8. Con el boost en marcha, chocar: no se corta.
9. Quedarse encajado contra un rival parado: a los 3 s, recolocación. La nave parpadea 2 s y atraviesa a los demás.
10. Terminar la carrera: los que han terminado se atraviesan con todo menos la barrera.
11. F9: columnas de golpes y puntos. Desactivar CONTACT: se vuelven a atravesar.

**Etapa 2 · Trazada clara**
12. Salida: los 8 salen y se ordenan sin un choque en cadena.
13. Mirar a dos rivales con la misma nave: en vez de comerse, uno sigue al otro o le pasa por el lado.
14. Activar el dibujo de estados: `CLEAR`, `PASS L/R` y `FOLLOW` tienen sentido con lo que se ve.
15. Ir lento por la trazada: los rivales que llegan te esquivan o frenan detrás. Parado en mitad de una recta, igual.
16. En la recta más larga, con un rival delante, el que va detrás no gasta el boost hasta tener la trazada clara.
17. Varias carreras: apuntar en F9 los golpes medios y fuertes entre rivales por carrera.
18. Mirar los fps en la salida, con los 8 juntos.

## 14. Fuera de alcance

- **Chispas, destellos y humo** en los choques: van con las **llamas y estelas**.
- **Masa por nave** (que HAMMER empuje más o BLADE salga más despedida). Decidido: todas pesan lo mismo.
- **Defender la posición, cerrar la puerta, agresividad, errores, rebufo** y uso del boost con cabeza: spec 4, con las personalidades.
- Penalizaciones o avisos por embestir, y mostrar los golpes en resultados.
- Ventana atravesable en la salida. Decidido: salida normal.
- Que el choque gire el morro directamente.
- Contacto con el fantasma o con las naves de prueba (F6).
- Sonido y vibración del mando.
