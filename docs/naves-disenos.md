# VECTORY — Diseños de naves (casco y cabina)

2026-11-04 · Lienzo: **"VECTORY — Naves"** (https://claude.ai/artifact/5jM4zTQtPMVqRqEhS8P6UG), páginas **Cascos** y **Cabinas**

## Cascos elegidos

| Nave | Diseño elegido | Largo | Envergadura | Semiancho geométrico | Toberas |
|---|---|---|---|---|---|
| INTERCEPTOR (`ship_02`) | **E · HOJA**: cuña plana muy baja en forma de flecha, cubierta de cabina central, puntas traseras en flecha, 2 toberas planas | 8,8 m | 3,2 m | 1,6 m | 2 |
| VANGUARD (`ship_03`) | **C · MARTILLO**: ala canard ancha en el morro con puntas caídas, cuerpo estrecho detrás, aletas traseras pequeñas, 2 toberas grandes | 6,6 m | 5,0 m | 2,5 m | 2 |
| VECTOR (`ship_01`) | Nave actual (referencia) | 7,2 m | 6,8 m | 3,4 m | 2 |

## Todas las opciones de casco (guardadas)

Se guardan todas por si se usan más adelante (otras naves, rivales, variantes).

**Interceptor**
- A · DARDO: aguja larga sin alas, canards y estabilizadores cortos, 1 tobera. 9,6 m · semiancho 1,25 m.
- B · DELTA: fuselaje estrecho y ala delta muy barrida, 2 toberas juntas. 8,6 m · 2,3 m.
- C · AGUJA: sin alas horizontales, dos derivas altas inclinadas y quilla ventral, la más estrecha. 9,6 m · 0,95 m.
- D · CATAMARÁN: dos cascos finos unidos por un ala, cápsula de cabina central adelantada. 8,0 m · 1,6 m.
- **E · HOJA** (elegida). 8,8 m · 1,6 m.

**Vanguard**
- A · BLOQUE: casco hexagonal corto y alto, alas cortas, 4 toberas en bloque 2×2. 7,0 m · 2,2 m.
- B · TRIMOTOR: tres motores grandes en triángulo, alas cortas caídas. 6,6 m · 2,1 m.
- **C · MARTILLO** (elegida). 6,6 m · 2,5 m.
- D · ESCARABAJO: caparazón hexagonal con dos góndolas laterales que llevan los motores. 6,2 m · 2,2 m.
- E · ARIETE: cuña baja que sube a un bloque trasero, 3 motores en fila. 7,0 m · 1,45 m.

Geometría exacta de las 11 naves: **`design/naves-geometria.js`** (mismas unidades y ejes que `Spaceship.cs`: metros, morro hacia −Z, X a la derecha, Y arriba; líneas cian salvo boca y núcleo de las toberas en azul; Vector copiada tal cual de `Spaceship.cs`).

## Cabinas

### Decisiones

- **Cada nave tiene su cabina**, también Vector (se rediseña).
- Desde la cabina se ve el **marco y el morro propio** de la nave.
- **Partes sólidas**: paneles **opacos oscuros con aristas luminosas**, sin texturas. Con volumen: caras con degradado (más claras arriba), bisel interior, brillo en el borde superior, pilares y consolas con grosor. Cambia el principio del documento maestro "solo líneas, sin superficies sólidas" **para la cabina**.
- **HUD integrado en el cuadro de mando**: **velocidad, boost y daño** se muestran dentro de la cabina, con una disposición distinta en cada nave, para darle personalidad. Mismos datos, estados y colores que el HUD actual (boost: disponible, cooldown, vacío, bloqueado; daño: verde, amarillo, rojo, rojo parpadeante).
- **Posición, vuelta y tiempos se quedan donde están** (esquinas de la pantalla).
- Detalle mecánico **decorativo**: tornillos, juntas, rejillas, interruptores con piloto.
- Luz: las pantallas iluminan los paneles cercanos (más con más velocidad), reflejos tenues en el cristal, tinte naranja en los bordes con boost y rojo con daño 2 y 3 (latiendo en 3).
- Microtextos de estado: boost `READY` / `ACTIVE` / `COOLDOWN` / `LOCKED` / `CHARGING`, daño `NOMINAL` / `LIGHT` / `HEAVY` / `CRITICAL`.

### Vector: 5 opciones (guardadas todas, sin elegir todavía)

1. **CONSOLA**: simétrica; bitácora central con la velocidad, barra con marca de la punta; boost en módulo izquierdo (esferas), daño en módulo derecho (segmentos).
2. **ARCO**: velocímetro de arco con marcas, números y aguja, zona sobre la punta en naranja; boost dentro del arco; daño en barras verticales en panel izquierdo; estructura superior.
3. **DOBLE PANTALLA**: dos pantallas laterales inclinadas; izquierda `HULL STATUS`, derecha velocidad, barra y boost; consola central con rejilla e interruptores.
4. **YUGO**: yugo de control en primer plano; velocidad en el centro con anillo de progreso; boost en barras en el brazo derecho; daño en tira del salpicadero.
5. **VISOR**: visor proyectado sobre el salpicadero con la velocidad, escala lateral y boost en rombos; daño en panel bajo; arco superior de cabina.

Código: **`design/cabinas-vector.js`** (genera los artboards del lienzo; coordenadas en píxeles de una pantalla de 1280×720, no en metros).

Pendiente: cabinas de **Hoja** y **Martillo** (5 opciones cada una) y elegir una por nave.

## Notas para las specs siguientes

- Martillo tiene el punto más ancho **delante** (el canard) y Hoja **detrás** (las puntas en flecha). Puede importar si la colisión deja de ser un simple semiancho.
- Las cabinas son diseños en 2D de pantalla. La spec de geometría tendrá que pasarlas a 3D en la cabina de cada nave (marco, paneles sólidos y HUD en el cuadro de mando), y el HUD de cabina no se muestra en la cámara exterior, que sigue con el HUD de pantalla.
