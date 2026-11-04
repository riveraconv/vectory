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

// Una línea de la nave o de la cabina. Edge es el número de su arista: cada arista parpadea por su cuenta con los
// daños, y los dos trazos de una línea gruesa de la cabina son la misma arista.
public readonly record struct ShipLine(Vector3 A, Vector3 B, ShipLineColor Color, int Edge);

// Encuadre base de la cámara de cabina, sin efectos. El marco de la cabina está dibujado para este FOV.
public readonly record struct CockpitFraming(Vector3 Eye, float Fov);

// Encuadre base de la cámara exterior, sin efectos: altura, distancia, distancia extra a máxima velocidad
// y punto al que mira (en el espacio de la cámara, sobre la nave)
public readonly record struct ChaseFraming(float Height, float Distance, float ExtraDistance, Vector3 LookTarget)
{
	// Más lejos o más cerca en la misma proporción: el ángulo de la cámara no cambia
	public ChaseFraming Scaled(float factor) => new(Height * factor, Distance * factor, ExtraDistance * factor, LookTarget * factor);
}

// Cómo es y cómo se ve una nave (spec docs/naves-1-definicion.md, 3). El casco y la cabina son constructores de
// geometría: devuelven las líneas sin nodos de Godot. Lógica pura.
public sealed record ShipVisualProfile
{
	public required Func<IReadOnlyList<ShipLine>> BuildHull { get; init; }
	public required Func<IReadOnlyList<ShipLine>> BuildCockpit { get; init; }
	public required CockpitFraming Cockpit { get; init; }
	public required ChaseFraming Chase { get; init; }

	// Factor con el que Chase se escala respecto al encuadre de Vector, según el largo de la nave
	public float ChaseFactor { get; init; } = 1f;

	// Semiancho que usa la colisión con la barrera. Sale del diseño: el de una nave con alas llega a la punta del ala.
	public required float CollisionHalfWidth { get; init; }

	// Distancia del origen de la nave a la punta del morro (colocación en la parrilla)
	public required float NoseDistance { get; init; }

	// Caja física del nodo Player
	public required Vector3 PhysicsBoxSize { get; init; }
	public required Vector3 PhysicsBoxCenter { get; init; }
}
