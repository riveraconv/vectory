# VECTORY — Spec: Escenografía (pista suspendida sobre una megaciudad)

Versión 1.0 · 2026-11-03 · Estado: **definido, listo para implementar**

Hasta ahora el circuito flota en un vacío vacío. Esta spec le da entorno: la pista va **suspendida muy por encima de una megaciudad hundida en neblina ámbar**, con una **rejilla del abismo** abajo, **haces de anclaje** que cuelgan de la pista, **polvo de luz** alrededor y una **superficie de pista opaca**.

Maquetas de referencia (se decidió con todas las capas activadas):
- Entorno con ciudad: https://claude.ai/artifact/XK6RKXSCsukLTG62nqinh1
- Entorno sin ciudad (rejilla, anclajes y polvo): https://claude.ai/artifact/XzzDVEjqNtW2GL6gRVDpLi

Se apoya en: `Track` / `TrackDefinition`, el **marco local de la pista** de `pista-3d`, la spec `elementos-circuito` (suelo 0 y elementos), el `WorldEnvironment` y `Palette`.

Rama: `feature/scenery`, que sale de `develop` con `feature/circuit-elements` ya mergeada.

**Cambia el documento maestro** (ver sección 9): ahora se permiten la ciudad de fondo y una superficie de pista opaca, sin texturas.

---

## 1. Resumen

| Capa | Qué es | Color |
|---|---|---|
| Superficie de pista | Superficie oscura lisa bajo las líneas de la pista, con canto de 1,2 m | #070B16 + canto azul |
| Rejilla del abismo | Rejilla enorme muy por debajo del circuito | Azul |
| Haces de anclaje | Líneas de luz que cuelgan del canto de la pista y se desvanecen | Cian → fondo |
| Megaciudad | Rascacielos en líneas con ventanas y neones | Azul, ventanas variadas, neones cian y magenta |
| Neblina ámbar | Cielo con resplandor ámbar en el horizonte y niebla cálida | Ámbar oscuro |
| Polvo de luz | Partículas quietas junto a la pista y estrellas lejanas | Cian claro / azul grisáceo |

Todas las capas son **solo visuales**: no tienen colisión ni cambian la física. Cada una se puede activar o desactivar por separado (Inspector ahora, SETTINGS más adelante).

## 2. Superficie de pista

- Superficie lisa de **color #070B16**, sin textura y sin iluminación (material *unshaded*), de borde a borde de la pista más 0,3 m por lado, siguiendo el marco local (pendientes y peraltes).
- Es **opaca**: tapa lo que hay debajo. Se ve por los dos lados (al pasar por debajo de otro tramo se ve su parte inferior).
- Va justo por debajo de las líneas del suelo (bordes, carriles, central y transversales), con un pequeño desplazamiento de profundidad para que las líneas no parpadeen.
- **Canto**: la pista tiene 1,2 m de grosor visual. En la parte inferior de cada borde, una línea **azul** al ~60 %.
- Es lo que permite los haces de anclaje: salen del canto, no atraviesan la pista.
- Los elementos de `elementos-circuito` (meta, checkpoints, parrilla…) no cambian.

## 3. Rejilla del abismo

- Rejilla de líneas **azules** (~70 %) en un plano horizontal muy por debajo del circuito: **260 m por debajo del punto más bajo de la pista** (parámetro).
- Celdas de **120 m** y unos **8 km** de lado, centrada en el circuito.
- Se pierde en la niebla. No hay nada más en ese plano salvo la ciudad, que se apoya en él.

## 4. Haces de anclaje

- Cada **45 m** sobre `s`, en los **dos bordes**, una línea vertical que cuelga del canto de la pista hacia abajo.
- **Largo**: 160 m. Color **cian** en lo alto que se funde con el fondo hacia abajo (degradado de 8 tramos, que se apaga más deprisa al principio). Opacidad ~70 %.
- Si un haz atraviesa **otro tramo de la pista** (en un cruce), se **corta** justo encima de ese tramo.
- Dentro de los túneles se mantienen (cuelgan de la pista igual).

## 5. Megaciudad

### 5.1 Generación

- Se genera **por código** al crear el circuito, con una **semilla por circuito** (`TrackDefinition.SceneSeed`), así cada circuito tiene su ciudad y siempre la misma.
- Unos **900 edificios** repartidos en un disco de **2,6 km** de radio alrededor del centro del circuito, apoyados en el plano de la rejilla del abismo. Más densos cerca del centro.
- **Huecos**: bajo el circuito (dentro de su caja en planta, más un margen) solo hay **edificios bajos** (30–160 m), para que la pista se vea volar por encima de la ciudad.
- **Alturas**: normales de 60–300 m, algo más altos lejos (× 1,4 a más de 1,6 km). Un **5 %** son **torres altas** (380–560 m) fuera de la zona del circuito, que suben por encima de la pista.
- Planta de 30–80 m por lado.

### 5.2 Aspecto

- **Aristas** en **azul** (~75 %): verticales de las esquinas y contorno del tejado. Las torres altas, algo más gruesas y brillantes.
- **Franjas de pisos** cada 36 m en los edificios de más de 200 m, azul tenue (~35 %).
- **Ventanas**: puntos sueltos en las fachadas, unos cuantos por cada 10 m de altura. Colores: azul grisáceo (#A6B8D8), cian y ámbar claro (#FFC890), con más de los fríos. Opacidad ~80 %.
- **Neones**: en ~18 % de los edificios de más de 150 m, un rectángulo vertical en una fachada (12 × 25–60 m), en **cian** o **magenta**, brillante.
- Todo con líneas y puntos agrupados en pocas mallas. Ninguna superficie.
- La ciudad está **quieta**: no hay tráfico ni animación (fuera de alcance).

## 6. Neblina ámbar

- **Cielo**: degradado vertical de fondo:
  - Arriba, oscuro (#05070D).
  - Cerca del horizonte, un resplandor **ámbar** (#3A2210 → #5A3414 → #2A180C).
  - Hacia abajo, oscuro cálido (#120C0A → #0A0809).
- **Niebla**: color **#1D120A**, que empieza a **150 m** y es total a **2,3 km**. La ciudad se pierde en la bruma.
- Con la neblina desactivada: fondo #05070D y niebla del mismo color, como ahora.
- El resplandor es muy oscuro: no debe competir con el naranja de los avisos. Si al probar se confunden, se baja.

## 7. Polvo de luz

- **Polvo**: partículas **quietas** a los lados de la pista (de 6 a 76 m del borde, entre 40 m por debajo y 50 m por encima), unas 2 cada 3 m de vuelta. Cian claro, tamaño ~1 m, mezcla aditiva. Al correr pasan a tu lado y dan sensación de velocidad.
- **Estrellas**: unas **2500** en una esfera lejana (~2,3 km), sobre todo por encima del horizonte. Azul grisáceo, tamaño fijo en pantalla (no cambian con la distancia), sin niebla.

## 8. Ajustes y rendimiento

- Cada capa se puede **activar o desactivar** por separado (parámetros del constructor de escenografía en el Inspector).
- Propuesta para SETTINGS (no se hace ahora): calidad de la escenografía baja, media o alta.
  - **Baja**: ciudad con un 30 % de edificios, sin ventanas, sin polvo y sin franjas de pisos.
  - **Media**: ciudad con un 60 % de edificios.
  - **Alta**: todo.
- Es importante para Android: la ciudad son muchas líneas.
- Todo se genera **una vez** al crear el circuito y se agrupa en pocas mallas (una por tipo de línea y color).

## 9. Cambios al documento maestro (versión 1.2)

Antes de implementar esta spec, actualizar el documento maestro:

- **Identidad y arte**: «mundo 3D suspendido en el vacío sin ciudades ni paisajes» pasa a: «circuitos suspendidos en el vacío, muy por encima de una **megaciudad** hundida en neblina ámbar. La ciudad es solo fondo, en líneas y puntos luminosos, sin superficies».
- **Solo líneas y aristas**: se añade una excepción: «la pista tiene una **superficie opaca oscura y lisa**, sin textura ni iluminación, para que no se vea lo que hay debajo».
- **Paleta**: se añade el ámbar de la neblina (#1D120A para la niebla y el degradado del cielo) como color de ambiente, nunca para la interfaz ni los avisos. El magenta de los neones de la ciudad es solo de ambiente y va siempre lejos y apagado, para no confundirse con los rivales.

## 10. Arquitectura y pruebas

- **Generador de ciudad** (puro): semilla, caja del circuito y parámetros → lista de edificios (posición, planta, altura, si es torre), ventanas y neones. Determinista.
- **Disposición de anclajes** (puro): posiciones de los haces y corte cuando atraviesan otro tramo de la pista.
- **Constructor de escenografía** (Godot): construye las mallas de cada capa a partir de lo anterior. Sin reglas dentro.
- `TrackDefinition` gana `SceneSeed` (entero). Por defecto, 1.

### Pruebas unitarias mínimas

1. Misma semilla → misma ciudad. Semilla distinta → ciudad distinta.
2. Ningún edificio de más de 160 m dentro de la caja del circuito.
3. Alrededor de un 5 % de torres altas, todas fuera de la caja del circuito.
4. Anclajes cada 45 m en los dos bordes.
5. Un anclaje que pasa por debajo de otro tramo de la pista se corta antes de tocarlo.
6. La rejilla queda 260 m por debajo del punto más bajo de la pista.
7. La firma de los circuitos no cambia (la escenografía es solo visual).

## 11. Prueba manual

1. CIRCUIT 01 y TEST HILLS: la pista tiene superficie oscura y canto, y no se ve lo que hay debajo a través de ella.
2. Abajo se ve la rejilla y la ciudad perdiéndose en la bruma ámbar. Algunas torres suben por encima de la pista.
3. Los haces cuelgan del canto y se desvanecen. En un cruce (en TEST HILLS no hay; probar con un circuito que lo tenga, o comprobarlo en el definitivo) no atraviesan el tramo de abajo.
4. Al correr, el polvo pasa a los lados.
5. Los avisos naranjas se siguen distinguiendo bien del ámbar del fondo.
6. Desactivar cada capa en el Inspector: desaparece solo esa.
7. Rendimiento: con toda la escenografía y los efectos de velocidad activos, los FPS no bajan de forma apreciable.

## 12. Fuera de alcance

- Tráfico, luces que parpadean o cualquier animación de la ciudad.
- Megaestructuras abstractas (descartadas en favor de la ciudad).
- Menú SETTINGS.
- Escenografía distinta en el menú principal o en el selector.
- Diseño del circuito definitivo (spec propia, que usará todo esto).
