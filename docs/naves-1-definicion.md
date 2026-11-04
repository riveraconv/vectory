# VECTORY — Spec: Naves 1 · Definición de nave (técnico y visual separados)

Versión 1.2 · 2026-11-04 · Estado: **implementada y validada**

Cambios en v1.1: la rama pasa a ser `refactor/ship-definition` y se fija la versión del juego (0.13.1).

Cambios en v1.2: el semiancho de colisión de Vector es 3,4 m (no 1,9); toberas y estela fuera de esta spec, porque `efectos-velocidad` no está implementada; reparto de los parámetros de cámara (3.1 y sección 6); "valores de conducción" = los 8 campos de `ShipTuning` (sección 2); morro y caja física al perfil visual (3.1).

Primera de las specs de naves. Es un **refactor**: cada nave pasa a tener una **definición** con dos partes separadas, un **perfil técnico** (cómo conduce) y un **perfil visual** (cómo es y cómo se ve), y todo el juego lee la nave de esa definición. Por ahora la única definición es la de **VECTOR** (`ship_01`), con lo que ya existe.

**Criterio de aceptación: se conduce, se choca y se ve exactamente igual que antes.** No hay naves nuevas, ni valores nuevos, ni cambios visuales.

Plan de las specs de naves:

1. **Definición de nave** (esta).
2. Perfil técnico y equilibrio de Interceptor y Vanguard.
3. Diseño visual de Interceptor y Vanguard (lienzo y luego geometría).
4. Integración: desbloquearlas en el selector y usarlas en carrera y fantasma.
5. Piloto automático de equilibrio (opcional).

Se apoya en lo que ya existe: `ShipCatalog` (con sus entradas de la spec del selector), los valores base de Vector de la spec del selector, `ShipTuning`, `ShipDynamics`, `Player`, `Spaceship`, `Cockpit`, las dos cámaras (cabina y exterior), la colisión con la barrera (semiancho de 3,4 m), el fantasma, `ShipHologram`, la nave del menú principal y `GameSession.ShipDesignId`.

Rama: `refactor/ship-definition`, que sale de `develop` con `feature/ship-selector` ya mergeada. Al ser un refactor sin cambios para el jugador, los commits empiezan por `refactor:`.

Versión del juego: **0.13.0 → 0.13.1** (patch: refactor sin cambios visibles). Se actualiza donde ya está la versión (la que se ve abajo a la izquierda del menú) al hacer el commit final de la etapa validada.

Todo lo demás del documento maestro sigue aplicando: principios, flujo por etapa, SOLID, pruebas y commits tras validación.

---

## 1. Resumen

- Nueva **definición de nave** por id, con:
  - **Perfil técnico**: los parámetros de conducción (`ShipTuning`).
  - **Perfil visual**: casco, cabina, encuadre de las cámaras y **ancho de colisión**.
- El **ancho de colisión va en el perfil visual** porque sale del diseño de la nave: Vector tiene alas y por eso es ancha, y una nave sin alas será más estrecha. Aunque se use en la física, se define con la forma.
- **La cabina y la cámara exterior son de cada nave**: van en su perfil visual.
- Lo que es **común a todas las naves no entra** en la definición: boost, daños (reglas y aspecto), ajustes de cámara (inclinación, vibración, FOV de la cámara exterior) y la paleta.
- **Toberas y estela no entran en esta spec**: son de `efectos-velocidad`, que no está implementada. Esa spec añadirá sus puntos al perfil visual cuando se haga.
- Cada parte del juego que hoy usa la nave pide la definición de la nave actual (`GameSession.ShipDesignId`) y lee de ella lo que necesita.

## 2. Perfil técnico

- Es el `ShipTuning` de la nave, con **todos** sus parámetros de conducción.
- El de Vector son los **valores base de la spec del selector** (`MaxSpeed` 110, `Drag` 5, `TurnRate` 1,3, `SteerResponse` 8 y el resto los de siempre). No se copian: la definición de Vector usa esos mismos valores, en un único sitio.
- El `Player` toma su `ShipTuning` de la definición de la nave actual.
- **Fuente única**: no puede quedar ningún valor de `ShipTuning` en `Main.tscn` ni en un `[Export]` del `Player` que tape al de la definición. Sus 8 campos (`MaxSpeed`, `Acceleration`, `BrakeDeceleration`, `Drag`, `OverspeedDecay`, `TurnRate`, `SteerResponse`, `LateralGrip`) dejan de ser `[Export]` y salen **solo** de la definición.
  - Derrape, Vuelo, Pendiente y peralte, Inclinación y Colisiones (salvo el semiancho) siguen comunes y en el Inspector. La spec 2 decide si alguno pasa a ser de cada nave.
  - Los ajustes de conducción en el Inspector se recuperan de forma controlada en la spec 2, que es donde se ajustan los números. Hasta entonces, para tocar un valor se cambia en la definición.
  - Si quitar esos `[Export]` rompe algo más (otra escena, una prueba, una herramienta), **se avisa antes**.
- El selector (`ShipStats`) lee el perfil técnico de la definición, no los valores base sueltos.

## 3. Perfil visual

### 3.1 Qué entra

| Elemento | Qué es | De dónde sale el de Vector |
|---|---|---|
| **Casco** | Las líneas de la nave (lo que dibuja hoy `Spaceship`) | La geometría actual de `Spaceship` |
| **Cabina** | El marco y las líneas de primera persona (lo que dibuja hoy `Cockpit`) | La geometría actual de `Cockpit` |
| **Cámara de cabina** | Posición del ojo respecto a la nave y FOV base (el marco está dibujado para ese FOV) | La posición actual y FOV 80 |
| **Cámara exterior** | Altura, distancia, distancia extra a máxima velocidad y punto al que mira (el encuadre base, sin efectos) | `CameraHeight`, `CameraDistance`, `CameraExtraDistance` y el punto fijo de `ChaseCamera` |
| **Ancho de colisión** | Semiancho que usa la colisión con la barrera | **3,4 m**, el valor actual (las puntas de las alas) |
| **Morro** | Distancia del origen de la nave a la punta del morro, para colocarla en la parrilla | **4 m**, `Player.NoseDistance` |
| **Caja física** | Tamaño y centro de la caja de `Player` | **6,8 × 1,2 × 7,2** centrada en (0, 0,2, −0,4), los valores actuales |

- El casco y la cabina se guardan como **constructores de geometría** de la nave (el código que genera las líneas), no como listas de líneas copiadas a mano. Lo que hoy construye `Spaceship` pasa a ser el constructor del casco de Vector, y lo mismo con `Cockpit`.
- `Spaceship` y `Cockpit` siguen existiendo como nodos: reciben el perfil visual y dibujan con él. Sus colores, el glow, la transparencia del fantasma, el parpadeo de daños y el material del holograma no cambian.
- Las **cámaras** leen su encuadre base del perfil visual. Los efectos de la spec `camara` se siguen sumando encima igual que ahora.
- `CameraFollow`, `BaseFov`, `MaxFov` y `CameraUpSmoothing` de la cámara exterior son comunes y siguen en el Inspector del `Player`.

### 3.2 Ancho de colisión

- El ancho de colisión es el ancho de la nave: una con alas lo mide en la punta del ala y una sin alas tendrá uno menor.
- La colisión con la barrera lee el semiancho del perfil visual de la nave en lugar del valor fijo. Con Vector, **3,4 m**, así que la colisión no cambia. `CollisionHalfWidth` deja de ser `[Export]` del `Player`.
- **No se calcula** a partir de las líneas del casco en esta spec: se define como valor en el perfil. En Vector coincide con la punta del ala (x = 3,4 en el casco actual).

### 3.3 Antes de mover nada: inventario

Antes de empezar, el agente busca en el código **todos los sitios que dependen de la forma o el tamaño de Vector** (medidas, posiciones, márgenes y encuadres sueltos fuera de `Spaceship` y `Cockpit`) y los apunta en un **informe corto**: qué valor es, dónde está, qué lo usa y si va al perfil visual o es común. Ejemplos de lo que puede aparecer: el encuadre del holograma (~480 px de ancho), la nave del menú principal, la caja del fantasma y los offsets de las cámaras.

- Lo que sea propio de la forma de la nave va al perfil visual.
- Lo que sea común se queda donde está.
- **Si hay algo dudoso, se pregunta antes de moverlo.**
- Decidido: el encuadre del menú principal (`MenuBackground`) se queda donde está. Es la composición de esa pantalla, que siempre muestra la nave `Default`.

## 4. Catálogo y definiciones

- `ShipCatalog` sigue igual para el selector: entradas con id, nombre, descripción y si es elegible, en el orden INTERCEPTOR, VECTOR, VANGUARD.
- Nuevo acceso a las **definiciones**:
  - `ship_01` tiene definición: perfil técnico y perfil visual.
  - `ship_02` y `ship_03` **no tienen definición** todavía. Siguen sin poder elegirse.
  - Una entrada solo puede ser elegible si tiene definición. Se comprueba en una prueba.
- **Resolver la nave**: dado un id, devuelve su definición. Con un id desconocido o sin definición, devuelve la de `Default` (`ship_01`) y lo avisa en consola una vez.
- `GameSession.ShipDesignId` no cambia: es el id que se resuelve.

## 5. Quién lee la definición

| Parte | Lee | Hoy, con Vector |
|---|---|---|
| `Player` | Perfil técnico (conducción) y ancho de colisión | Igual que antes |
| `Spaceship` de la carrera | Casco | Igual que antes |
| `Cockpit` | Cabina | Igual que antes |
| Cámaras | Encuadre base de cabina y exterior | Igual que antes |
| Fantasma | El casco de **la nave guardada en su archivo** (su `ShipDesignId`), no la de la sesión | Siempre `ship_01` |
| Holograma del selector (`ShipHologram`) | Casco de la nave mostrada | Igual que antes |
| Selector (`ShipStats`) | Perfil técnico | Mismos datos y barras |
| Nave del menú principal | Casco de `Default` | Igual que antes |

- El **formato del archivo del fantasma no cambia**.
- Récords, HUD, boost y daños no cambian.

## 6. Parámetros en el Inspector

No hay parámetros nuevos. Dejan de estar en el Inspector del `Player` los 8 de `ShipTuning` (sección 2), `CollisionHalfWidth` (3.2) y `CameraHeight`, `CameraDistance` y `CameraExtraDistance` (3.1). Los demás de cámara, efectos y holograma siguen igual.

## 7. Arquitectura y pruebas

Respetar la estructura de carpetas y namespaces actual.

- **`ShipDefinition`** (puro): `Id`, `Technical` (perfil técnico) y `Visual` (perfil visual).
- **Perfil técnico** (puro): el `ShipTuning`.
- **Perfil visual** (puro, `System.Numerics`): constructor del casco, constructor de la cabina, encuadre de las cámaras, semiancho de colisión, morro y caja física. Los constructores devuelven las líneas (segmentos, rol de color —cian o azul, que el nodo traduce con `Palette`— y número de arista, en el orden actual) **sin nodos de Godot**, para poder probarlos. Si hoy la geometría de `Spaceship` o `Cockpit` se construye de una forma que no se puede sacar a código puro sin cambiarla, **se avisa antes**.
- **`ShipDefinitions`** (puro): definiciones por id y la resolución de la sección 4.
- **Definición de Vector** (puro): reúne los valores base y la geometría actual. Es el único sitio donde está Vector.
- `Spaceship`, `Cockpit`, cámaras, `Player`, fantasma, `ShipHologram` y menú: reciben la definición (o la parte que necesitan) y no contienen datos propios de la nave.

### Prueba de "se ve igual"

Antes de tocar nada, el agente guarda una **captura de referencia** de la geometría actual, generada con el código de hoy: todos los segmentos del casco y de la cabina de Vector (puntos, color y arista) y las posiciones de las cámaras. Después del refactor, una prueba compara la geometría generada desde la definición con esa captura, **segmento a segmento** y con una tolerancia mínima (1e-5).

### Pruebas unitarias mínimas

Definiciones:
1. `ship_01` tiene definición; `ship_02` y `ship_03` no.
2. Toda entrada elegible del catálogo tiene definición.
3. Resolver `ship_01` devuelve Vector; un id desconocido y `ship_02` devuelven Vector.

Perfil técnico:
4. El perfil técnico de Vector es `MaxSpeed` 110, `Drag` 5, `TurnRate` 1,3 y `SteerResponse` 8, y el resto los valores de siempre (la prueba de la spec del selector sigue pasando).
5. `ShipStats` con la definición de Vector da los mismos datos y barras que antes (`396 KM/H`, `0–200 · 2.2 S`, `74 °/S`, `200–0 · 1.2 S`, 5 segmentos).

Perfil visual:
6. El casco de Vector coincide con la captura de referencia.
7. La cabina de Vector coincide con la captura de referencia.
8. El encuadre de las cámaras coincide con los valores de antes.
9. El semiancho de colisión de Vector es 3,4 m.

Las pruebas existentes siguen pasando **sin tocarlas** (salvo cambiar de dónde sale un valor, sin cambiar el valor esperado). Si alguna necesita otro cambio, se avisa.

## 8. Prueba manual

Todo tiene que estar **igual que antes**; esta prueba es para buscar diferencias.

1. Menú principal: la nave del menú se ve igual y la versión de abajo a la izquierda es 0.13.1.
2. SELECT SHIP: holograma de VECTOR igual (forma, colores, aristas tenues, giro) y los mismos datos.
3. RACE: parrilla, semáforo y salida igual. En cabina, el marco y la posición de la vista son los mismos.
4. Conducir unas vueltas: punta, aceleración, giro, frenado y derrape iguales.
5. Rozar y chocar contra la barrera por los dos lados: la nave toca la barrera en el mismo punto que antes (ni más dentro ni más fuera) y los niveles de impacto se sienten igual.
6. Cambiar a la cámara exterior con C: mismo encuadre. Boost: FOV y empuje como antes.
7. Daños en RACE: niveles e indicador como antes.
8. TIME TRIAL: el fantasma de un récord guardado antes de este cambio se carga y se ve igual.
9. Pausa → RESTART y BACK TO MAIN MENU: como antes.
10. Ejecutar la escena de carrera sola desde el editor: arranca con Vector y conduce igual.
11. Comprobar que en `Main.tscn` y en el Inspector del `Player` ya no quedan los valores de `ShipTuning`, el semiancho de colisión ni el encuadre de la cámara exterior.

## 9. Fuera de alcance

- Perfil técnico, equilibrio y geometría de Interceptor y Vanguard (specs 2 y 3).
- Desbloquear naves en el selector y usarlas en carrera (spec 4).
- Calcular el ancho de colisión a partir de las líneas del casco.
- Toberas, estela y el resto de `efectos-velocidad`; `escenografia`.
- Ajustes de conducción en el Inspector por nave (spec 2).
- Guardar la nave en los récords o mostrarla en RECORDS y resultados.
- Cambios en boost, daños, efectos o ajustes de cámara.
- Piloto automático, rivales e IA.
- HANGAR.
