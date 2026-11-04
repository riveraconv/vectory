# VECTORY — Spec: Naves 3 · Cascos de Hoja y Martillo

Versión 1.1 · 2026-11-04 · Estado: **implementada y validada**

Tercera spec de naves. Mete en el juego la **geometría del casco** de **INTERCEPTOR (Hoja)** y **VANGUARD (Martillo)**, con sus medidas y el encuadre de su cámara exterior. Así, al cambiar de nave con el panel de pruebas (F10) se ve la nave real.

Las **cabinas** nuevas de las tres naves (paneles sólidos y HUD en el cuadro de mando) **no entran aquí**: van en su propia spec, la **3b**.

La **llama y la estela** tampoco entran: son de `docs/efectos-velocidad.md`, que todavía no está implementada. Los datos de cada nave se han pasado a esa spec (su sección "Datos de cada nave").

Plan de las specs de naves:

1. Definición de nave (hecha).
2. Perfil técnico, colisión y ajuste en pruebas (hecha).
3. **Cascos de Hoja y Martillo** (esta).
3b. Cabinas nuevas de Vector, Hoja y Martillo.
4. Integración: desbloquearlas en el selector y usarlas en carrera y fantasma.
5. Piloto automático de equilibrio (opcional).

Se apoya en lo que ya existe tras las specs 1 y 2: `ShipDefinition` con su perfil visual (constructor del casco, cámaras, ancho de colisión, morro y caja física), las definiciones de Hoja y Martillo con su perfil visual provisional (el de Vector), `Spaceship`, el parpadeo de daños, el fantasma, la cámara exterior y el panel de ajuste (F10).

Diseño de referencia:
- **`designs/naves-geometria.js`**: geometría exacta. Hoja = `Interceptor_E` (HOJA) y Martillo = `Vanguard_C` (MARTILLO). Las demás naves del archivo **no** se usan.
- **`docs/naves-disenos.md`**: medidas y descripción.
- Lienzo **"VECTORY — Naves"**, página **Cascos**: *Interceptor E · Hoja · ELEGIDA* y *Vanguard C · Martillo · ELEGIDA* (planta, perfil y tres cuartos).

Si algo no cuadra entre el lienzo y el archivo, **manda el archivo** y se avisa.

Rama: `feature/ship-geometry`, que sale de `develop` con `feature/ship-tuning` ya mergeada. Commits `feat:`.

Versión del juego: **0.13.2 → 0.13.3**.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- Hoja y Martillo dejan de usar el casco de Vector: cada una tiene su **constructor de casco**, traducido de `designs/naves-geometria.js`. Las toberas se dibujan como parte del casco.
- **F10** pasa a cambiar también el casco y el encuadre de la cámara exterior.
- La **cámara exterior** se encuadra según el tamaño de cada nave.
- El **morro** y la **caja física** de cada nave salen de su casco.
- La **cabina** de Hoja y Martillo sigue siendo, de momento, la de Vector (provisional hasta la spec 3b), y el **ojo de la cámara de cabina** también.
- El **ancho de colisión** ya era el de su forma real (spec 2). Esta spec comprueba que coincide con el casco.
- En el selector siguen como **COMING SOON** (se desbloquean en la spec 4).
- Vector no cambia nada.

## 2. Cascos

### 2.1 De dónde sale la geometría

- Unidades y ejes iguales que el casco de Vector: **metros, morro hacia −Z, X a la derecha, Y arriba**.
- El archivo construye cada nave con unas pocas piezas: `loft` (secciones en rombo), `poly` (polígonos cerrados), `add` (segmentos sueltos), `nozzle` (toberas) y `mirror` (pieza simétrica a los dos lados). `loft6` no se traduce: Hoja y Martillo no la usan.
- Se traducen a C# **tal cual**: los mismos puntos, el mismo orden y los mismos colores (cian, salvo la boca y el núcleo de las toberas, que son azules, igual que en Vector). **Sin redondear ni "mejorar" coordenadas.**
- Las piezas de construcción van en una clase pura nueva, solo para Hoja y Martillo. **El constructor de Vector no se reutiliza ni se toca**: su geometría es la misma, pero sus líneas van en otro orden, y cambiarlo renumeraría sus aristas (sigue su prueba de captura de la spec 1).

### 2.2 Medidas que tienen que salir

| | Hoja | Martillo | Vector (no cambia) |
|---|---|---|---|
| Largo | 8,8 m | 6,6 m | 7,2 m |
| Semiancho (punto más ancho) | 1,6 m | 2,5 m | 3,4 m |
| Alto | 0,62 m | 1,40 m | 1,0 m |
| Toberas | 2 | 2 | 2 |
| Segmentos | 138 | 126 | 118 |

Son las que da `designs/naves-geometria.js` al ejecutarlo (comprobado). Si al traducirlo sale otra cifra, **algo se ha traducido mal**.

### 2.3 Captura de referencia

Antes de traducir, el agente ejecuta `designs/naves-geometria.js` con Node, sobre una copia fuera del proyecto porque el script escribe archivos junto a él, y guarda en las pruebas una **captura** con todos los segmentos de Hoja y Martillo (puntos y color). Una prueba compara la geometría generada en C# con esa captura, **segmento a segmento**, con tolerancia 1e-5. Es la misma idea que la captura de Vector de la spec 1.

### 2.4 Morro y caja física

Salen del casco de cada nave, para que no queden los de Vector:

| | Hoja | Martillo |
|---|---|---|
| Distancia del origen al morro | 5,2 m | 3,6 m |
| Caja física (ancho × alto × largo) | 3,2 × 0,62 × 8,8 m | 5,0 × 1,40 × 6,6 m |
| Centro de la caja (x, y, z) | (0, 0,17, −0,8) | (0, 0,2, −0,3) |

Hasta la spec 4 no tienen efecto: el `Player` monta la caja y se coloca en la parrilla con la nave elegida en el selector, y F10 no recoloca la nave ni cambia la caja.

## 3. Cámaras

### 3.1 Cámara exterior

- El encuadre base de la cámara exterior se **escala con el largo** de la nave respecto a Vector:
  - Hoja: × 1,22 (8,8 / 7,2).
  - Martillo: × 0,92 (6,6 / 7,2).
- El factor se aplica a la altura, la distancia, la distancia extra y el punto al que mira, para que **el ángulo no cambie**.
- Son valores de arranque: se exponen en el perfil visual de cada nave y se ajustan probando. Los efectos de la spec `camara` (vibración, FOV y empuje del boost) se siguen sumando encima.

### 3.2 Cámara de cabina

- **Provisional hasta la spec 3b**: Hoja y Martillo usan la cabina y la posición del ojo de Vector.
- Desde la cabina no se ve el casco, así que se verá la misma cabina con las tres naves. **No se arregla aquí**.

## 4. F10: cambiar de nave en carrera

Hasta ahora F10 solo cambiaba la conducción y el ancho de colisión. Pasa a cambiar también:

- El **casco** visible (`Spaceship.SetVisual`).
- El **encuadre de la cámara exterior**.

Y sigue sin cambiar la cabina, la caja física ni la posición. También funciona tras RESTART: la nave del panel sigue siendo la misma y se ve su casco.

### Parpadeo de daños al cambiar de casco

El parpadeo se crea para un número fijo de aristas (118, 138 o 126), tanto en `EdgeFlicker` como en `EdgeFlickerMaterial`. Al cambiar de casco con daño:

- Se rehacen los dos para el casco nuevo, **manteniendo el nivel de daño**. Qué aristas parpadean se vuelve a sortear.
- Sin daño no se crea nada, como hasta ahora.
- El parpadeo de la cabina no cambia, porque la cabina tampoco.

## 5. Ancho de colisión

- La spec 2 ya dejó Hoja en **1,6 m** y Martillo en **2,5 m**.
- Una prueba comprueba que el ancho de colisión de cada nave es igual al **semiancho real de su casco** (el mayor |x| de sus segmentos), con tolerancia 0,01 m. También para Vector (3,4 m).
- Si no coincide, **manda el casco** y se avisa antes de cambiar el ancho.

## 6. Lo que tiene que funcionar igual con las tres naves

- **Parpadeo de daños**: mismas reglas (proporción de aristas, intervalos) con 138 y 126 segmentos.
- **Fantasma**: dibuja el casco de la nave guardada en su archivo. Con el panel de pruebas no se guardan fantasmas (spec 2), así que en la práctica sigue siendo Vector. Se comprueba solo que, con un id de Hoja o Martillo, la definición que usa el fantasma trae su casco.
- **Inclinación y derrape**: no dependen del casco y siguen igual.
- **Selector y menú**: sin cambios. Hoja y Martillo siguen como COMING SOON y el holograma no las enseña (spec 4).

## 7. Parámetros

| Parámetro | Valor de arranque | Dónde |
|---|---|---|
| Factor de la cámara exterior | Hoja 1,22 · Martillo 0,92 · Vector 1 | Perfil visual |

## 8. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **Piezas de construcción de casco** (puro, `System.Numerics`): `Loft`, `Poly`, `Segment`, `Nozzle`, `Mirror`. Devuelven líneas con color y número de arista, sin nodos.
- **Casco de Hoja** y **casco de Martillo** (puros): sus constructores, traducidos del archivo.
- **Perfiles visuales de Hoja y Martillo**: su casco, factor de cámara exterior, ancho de colisión, morro y caja física. La cabina y el ojo, provisionalmente los de Vector.
- **`Player` y `TuningLabPanel`**: F10 aplica el perfil visual de la nave además del técnico. El `Player` cambia el casco, usa el encuadre de esa nave y rehace el parpadeo si hay daño.
- **`Spaceship`**: al cambiar de casco, deja de usar el material de parpadeo del casco anterior.
- La carrera, el fantasma y el selector no se tocan.

### Pruebas unitarias mínimas

1. El casco de Hoja coincide con la captura de referencia (segmento a segmento).
2. El casco de Martillo coincide con la captura de referencia.
3. El casco de Vector sigue coincidiendo con su captura de la spec 1.
4. Medidas de la tabla 2.2: largo, semiancho, alto y número de segmentos. Las toberas se comprueban por los segmentos azules: 12 por tobera (6 de la boca y 6 del núcleo).
5. Morro y caja física de la tabla 2.4, que coinciden con el casco.
6. El ancho de colisión de cada nave es igual al semiancho de su casco (±0,01 m).
7. El factor de cámara exterior es 1,22 / 0,92 / 1 y el encuadre de cada nave es el de Vector por ese factor.
8. El parpadeo de daños elige aristas válidas con 138 y 126 segmentos.
9. La definición de `ship_02` y `ship_03` trae su casco, no el de Vector.

Las pruebas existentes siguen pasando.

## 9. Prueba manual

1. RACE con Vector, cámara exterior: Vector se ve exactamente igual que antes.
2. F10 → Hoja, cámara exterior: se ve la cuña plana y baja en flecha, con las dos toberas pequeñas. Comparar con el lienzo (planta, perfil y tres cuartos).
3. F10 → Martillo: se ve el canard ancho delante con las puntas caídas y las dos toberas grandes.
4. F10 → Vector: vuelve a verse igual que al principio.
5. La cámara exterior encuadra bien a las tres (ni muy cerca con Hoja ni muy lejos con Martillo).
6. Rozar la barrera con cada nave: toca donde se ve el punto más ancho del casco.
7. En RACE, dañar a una nave hasta nivel 3 y pulsar F10 varias veces: parpadean aristas del casco que se ve, sin errores, y el nivel de daño no cambia.
8. Cabina con Hoja y Martillo: se ve la cabina de Vector (provisional, esperado).
9. RESTART con Hoja o Martillo: se sigue viendo su casco.
10. Selector: Hoja y Martillo siguen en COMING SOON.

## 10. Fuera de alcance

- Llama, estela y demás efectos de `docs/efectos-velocidad.md`, con sus datos de cada nave.
- Cabinas nuevas de las tres naves, HUD en el cuadro de mando y ojo de la cabina de cada nave (spec 3b).
- Desbloquear las naves en el selector, el holograma de Hoja y Martillo y el fantasma con ellas (spec 4).
- Las otras 8 opciones de casco del archivo de diseño.
- Colisión con forma en lugar de semiancho.
- Cambios en los números técnicos (spec 2 y panel de pruebas).
