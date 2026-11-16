using System;
using System.Numerics;
using Vectory.Ship;

namespace Vectory.Contact;

// Forma de una nave para el choque entre naves (spec docs/rivales-3-contacto.md, 3.1): un rectángulo en planta, orientado
// con el morro, de su semiancho de colisión y con el largo de la caja total de su casco 3D. La caja no está centrada en el
// origen de la nave: Front es la distancia hasta lo más adelantado y Back hasta lo más atrasado.
// Lógica pura: no depende de Godot.
public readonly record struct ShipShape(float HalfWidth, float Front, float Back)
{
	public float HalfLength => 0.5f * (Front + Back);

	// Del centro del rectángulo al origen de la nave, hacia el morro (positivo = el centro va por delante)
	public float CenterOffset => 0.5f * (Front - Back);

	// La medida más pequeña del rectángulo (ancho o largo)
	public float MinDimension => MathF.Min(2f * HalfWidth, Front + Back);

	public static ShipShape From(ShipVisualProfile visual) =>
		new(visual.CollisionHalfWidth, visual.CollisionFront, visual.CollisionBack);

	// Centro del rectángulo con la nave en position (planta) y rumbo yaw
	public Vector2 Center(Vector2 position, float yaw) => position + Forward(yaw) * CenterOffset;

	// Mismas convenciones que ShipDynamics: yaw positivo = a la izquierda; adelante con yaw = 0 es -Z
	public static Vector2 Forward(float yaw) => new(-MathF.Sin(yaw), -MathF.Cos(yaw));
	public static Vector2 Right(float yaw) => new(MathF.Cos(yaw), -MathF.Sin(yaw));
}
