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
	// Para el que están dibujadas las cabinas 2D: la inclinación (negativa = hacia abajo) deja el punto de fuga de la
	// pista donde la espera su diseño, y ≈ 300 de 720 con FOV 80. Cada nave ajusta la altura del ojo.
	public static readonly CockpitFraming For2DCockpit = new(new Vector3(0f, 0.45f, -1.2f), 80f, -8f);
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
	Racing
}

// Una cabina 3D: su modelo exportado de Blender, lo que el modelo no puede llevar y su panorama de reflejos
public sealed record Cockpit3DProfile(string ModelPath, string ManifestPath, string EnvironmentPath, Cockpit3DStyle Style);

// Cómo es y cómo se ve una nave (spec docs/naves-1-definicion.md, 3). El casco y la cabina son constructores:
// devuelven sus líneas o sus elementos 2D, sin nodos de Godot. Lógica pura.
public sealed record ShipVisualProfile
{
	public required Func<IReadOnlyList<ShipLine>> BuildHull { get; init; }
	// Capa 2D de la cabina. Sin ella, la nave lleva cabina 3D.
	public Func<CockpitLayout> BuildCockpit { get; init; }

	// Cabina 3D (spec docs/cabinas-3d-1-vector.md). Con ella, BuildCockpit no se usa.
	public Cockpit3DProfile Cockpit3D { get; init; }

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
