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

// Dónde roza una nave al derrapar (spec docs/efectos-2-derrape.md, 4.1): TipZ, la z de las puntas de las alas (positivo
// hacia la popa), donde van las marcas al TipFraction del semiancho de colisión
public readonly record struct DriftContactShape(float TipZ);

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

	// Distancia del origen de la nave a la punta del morro (colocación en la parrilla)
	public required float NoseDistance { get; init; }

	// Caja física del nodo Player
	public required Vector3 PhysicsBoxSize { get; init; }
	public required Vector3 PhysicsBoxCenter { get; init; }
}
