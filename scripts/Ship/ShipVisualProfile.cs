using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Rol de color de una línea de la nave. El nodo que la dibuja lo traduce con Palette (ShipLineMesh).
public enum ShipLineColor
{
	Cyan,
	Blue
}

// Una línea del casco. Edge es el número de su arista: cada arista parpadea por su cuenta con los daños.
public readonly record struct ShipLine(Vector3 A, Vector3 B, ShipLineColor Color, int Edge);

// Encuadre base de la cámara del mundo en cabina, sin efectos
public readonly record struct CockpitFraming(Vector3 Eye, float Fov, float PitchDegrees)
{
	public float PitchRadians => PitchDegrees * MathF.PI / 180f;
}

// Encuadre base de la cámara exterior, sin efectos: altura, distancia, distancia extra a máxima velocidad
// y punto al que mira (en el espacio de la cámara, sobre la nave)
public readonly record struct ChaseFraming(float Height, float Distance, float ExtraDistance, Vector3 LookTarget)
{
	// Más lejos o más cerca en la misma proporción: el ángulo de la cámara no cambia
	public ChaseFraming Scaled(float factor) => new(Height * factor, Distance * factor, ExtraDistance * factor, LookTarget * factor);
}

// Estilo de una cabina 3D: de él salen los colores de sus pantallas y sus luces
public enum Cockpit3DStyle
{
	Racing,     // VECTOR
	Military,   // BLADE
	Industrial  // HAMMER
}

// Una cabina 3D: su modelo exportado de Blender, lo que el modelo no puede llevar, su panorama de reflejos y su estilo,
// del que sale su disposición viva
public sealed record Cockpit3DProfile(string ModelPath, string ManifestPath, string EnvironmentPath, Cockpit3DStyle Style)
{
	public Cockpit3DLayout Layout => Cockpit3DLayout.For(Style);
}

// Estilo de un casco 3D: de él salen su contrato de piezas y cómo se pinta
public enum HullStyle
{
	Racing,     // VECTOR (spec docs/cascos-3d-1-vector.md)
	Military,   // BLADE (spec docs/cascos-3d-2-blade.md)
	Industrial  // HAMMER (spec docs/cascos-3d-3-hammer.md)
}

// Dónde roza una nave al derrapar (spec docs/efectos-2-derrape.md, 4.1, 5 y 6), con z positivo hacia la popa y los
// laterales en fracción del semiancho de colisión. TipZ: las puntas de las alas, donde van las marcas (en la pista).
// Borde que roza, por donde salen las chispas: de (EdgeStartZ, EdgeStartFraction) a (EdgeEndZ, EdgeEndFraction), en
// línea recta, a la altura EdgeY de la nave (su parte de abajo). Foco de chispas: (FocusFraction, FocusZ), a esa altura.
public readonly record struct DriftContactShape(float TipZ, float EdgeStartZ, float EdgeStartFraction, float EdgeEndZ,
	float EdgeEndFraction, float EdgeY, float FocusZ, float FocusFraction);

// Focos del humo de daños (spec docs/efectos-4-humo-danos.md, 3), en los ejes de la nave: Smoke es el vacío del
// manifiesto de su casco (una prueba lo compara); Smoke2 y Smoke3, dos puntos más sobre la piel, solo para el nivel 3.
// SkinGaps: lo que hay de cada foco (X, Y, Z = Smoke, Smoke2, Smoke3) a la piel que tiene debajo, medido en el .glb; el
// fuego nace ahí, pegado al casco, y el humo en el foco
public readonly record struct DamageSmokeFoci(Vector3 Smoke, Vector3 Smoke2, Vector3 Smoke3, Vector3 SkinGaps = default);

// Colores de la llama de las toberas sin boost (spec docs/efectos-6-toberas.md, 4.4): núcleo, medio y fuera, como los da
// la maqueta (se pasan tal cual al shader). El medio es también el de la boca del casco 3D.
public readonly record struct ThrusterColors(Vector3 Core, Vector3 Mid, Vector3 Out)
{
	// VECTOR y BLADE: blanco cian, cian y azul
	public static readonly ThrusterColors Standard = new(new(0.85f, 0.97f, 1f), new(0.21f, 0.90f, 1f), new(0.14f, 0.36f, 1f));

	// HAMMER, a juego con sus bocas naranjas (spec docs/cascos-3d-3-hammer.md, 4.2): #FFB46A, #FF5A14 y rojo oscuro
	public static readonly ThrusterColors Industrial = new(new(1f, 0.706f, 0.416f), new(1f, 0.353f, 0.078f), new(0.55f, 0.10f, 0.04f));

	// Retro (spec docs/efectos-8-luces.md, 3.4): VECTOR y BLADE #E6FAFF, #73CCFF y #245BFF; HAMMER, naranja, #FFD9A8,
	// #FF8A3A y #FF5A14
	public static readonly ThrusterColors Retro = new(new(0.9f, 0.98f, 1f), new(0.45f, 0.8f, 1f), new(0.14f, 0.36f, 1f));
	public static readonly ThrusterColors RetroIndustrial = new(new(1f, 0.851f, 0.659f), new(1f, 0.541f, 0.227f), new(1f, 0.353f, 0.078f));
}

// Toberas de una nave (spec docs/efectos-6-toberas.md, 2.1 y 4.2), en sus ejes: Nozzle es la boca derecha (la izquierda,
// con x cambiada de signo; las bocas Exhaust_L/R del casco 3D, en el mismo sitio que las del wireframe), con la llama
// hacia +Z. Scale: radio de la boca respecto a VECTOR; Squash: alto / ancho de la boca; LengthScale: largo de la llama.
// Retro: escala y colores de su chorro (los puntos son los vacíos Retro_*_Point del casco 3D). Colors: los de la llama
// sin boost.
public readonly record struct ThrusterShape(Vector3 Nozzle, float Scale, float Squash, float LengthScale, RetroJetShape Retro,
	ThrusterColors Colors);

// Chorro del retro de una nave (spec docs/efectos-8-luces.md, 3.4): su ancho y su alto respecto al de efectos-6 (el largo,
// × el ancho) y sus colores
public readonly record struct RetroJetShape(float Width, float Height, ThrusterColors Colors);

// Un casco 3D: su modelo exportado de Blender, lo que el modelo no puede llevar, su panorama de reflejos y su estilo,
// del que sale su contrato de piezas
public sealed record ShipHull3DProfile(string ModelPath, string ManifestPath, string EnvironmentPath, HullStyle Style)
{
	public HullContract Contract => HullContract.For(Style);
}

// Cómo es y cómo se ve una nave (spec docs/naves-1-definicion.md, 3). El casco es un constructor: devuelve sus líneas,
// sin nodos de Godot. Lógica pura.
public sealed record ShipVisualProfile
{
	public required Func<IReadOnlyList<ShipLine>> BuildHull { get; init; }
	// Cabina 3D (spec docs/cabinas-3d-1-vector.md): la tienen todas las naves
	public required Cockpit3DProfile Cockpit3D { get; init; }
	// Casco 3D de la nave del jugador (null: el wireframe de BuildHull). El fantasma, el menú y las naves de prueba
	// siguen con el wireframe aunque la nave lo tenga.
	public ShipHull3DProfile Hull3D { get; init; }

	public required CockpitFraming Cockpit { get; init; }
	public required ChaseFraming Chase { get; init; }

	// Factor con el que Chase se escala respecto al encuadre de Vector, según el largo de la nave
	public float ChaseFactor { get; init; } = 1f;

	// Semiancho que usa la colisión con la barrera. Sale del diseño: el de una nave con alas llega a la punta del ala.
	public required float CollisionHalfWidth { get; init; }

	// Dónde roza al derrapar (spec docs/efectos-2-derrape.md): las puntas de las alas, a esta z (positivo hacia la popa)
	public required DriftContactShape DriftContact { get; init; }

	// Largo que usa el choque entre naves (spec docs/rivales-3-contacto.md, 3.1): del origen de la nave a lo más adelantado
	// y a lo más atrasado de la caja total del manifiesto de su casco 3D (sin la sonda de BLADE). La caja no está centrada
	// en el origen, así que van por separado. Como el semiancho, es un dato del perfil y una prueba lo compara con el manifiesto.
	public required float CollisionFront { get; init; }
	public required float CollisionBack { get; init; }

	// Altura de lo más bajo del casco 3D en los ejes de la nave (la y mínima de su manifiesto): de ahí salen las chispas
	// del aterrizaje (spec docs/efectos-3-aterrizaje.md, 3). Una prueba lo compara con el manifiesto.
	public required float BellyY { get; init; }

	// De dónde sale el humo de daños (spec docs/efectos-4-humo-danos.md, 3)
	public required DamageSmokeFoci SmokeFoci { get; init; }

	// Toberas: dónde salen las llamas, su tamaño y sus colores (spec docs/efectos-6-toberas.md)
	public required ThrusterShape Thrusters { get; init; }

	// Punta derecha del ala, de donde sale la estela (spec docs/efectos-7-estelas.md): el vacío Trail_R del manifiesto de su
	// casco 3D (una prueba lo compara); la izquierda, con x cambiada de signo (Trail_L)
	public required Vector3 TrailTip { get; init; }

	// Distancia del origen de la nave a la punta del morro (colocación en la parrilla)
	public required float NoseDistance { get; init; }

	// Caja física del nodo Player
	public required Vector3 PhysicsBoxSize { get; init; }
	public required Vector3 PhysicsBoxCenter { get; init; }
}
