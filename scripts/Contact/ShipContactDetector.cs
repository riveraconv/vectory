using System;
using System.Numerics;

namespace Vectory.Contact;

// Una nave para la detección: posición en planta ahora y al empezar el paso, rumbo, altura sobre la superficie y forma
public readonly record struct ContactBody(Vector2 Position, Vector2 Previous, float Yaw, float Height, ShipShape Shape);

// Contacto entre dos naves: normal (unitaria, de la primera hacia la segunda) y profundidad del solape por ella (m)
public readonly record struct ContactHit(Vector2 Normal, float Depth);

// Detección del contacto entre dos naves (spec docs/rivales-3-contacto.md, 3.1): rectángulos orientados en planta que se
// solapan (separación de ejes) y con menos de maxHeight de diferencia de altura sobre la superficie (no de altura en el
// mundo: con peralte, dos naves pegadas van a alturas distintas). La normal es el eje de menor solape.
//
// Si ya se solapaban al empezar el paso, la normal es la de menor solape. Si no, se mira dónde empiezan a solaparse a lo
// largo del paso (con posiciones intermedias si el movimiento relativo pasa de la mitad de la medida más pequeña de las
// dos, como mucho MaxSubsteps, para que a mucha velocidad una nave no se salte a otra): la normal es el eje que más las
// separaba justo antes y la profundidad, lo que hay que separarlas por él con las posiciones del final (así una nave que
// ya ha pasado a la otra vuelve detrás).
// Lógica pura: no depende de Godot.
public static class ShipContactDetector
{
	public const int MaxSubsteps = 8;

	public static bool Detect(in ContactBody a, in ContactBody b, float maxHeight, out ContactHit hit)
	{
		hit = default;
		if (MathF.Abs(a.Height - b.Height) >= maxHeight)
			return false;

		Vector2 relative = (b.Position - b.Previous) - (a.Position - a.Previous);
		float step = 0.5f * MathF.Min(a.Shape.MinDimension, b.Shape.MinDimension);
		int substeps = step > 0f ? Math.Clamp((int)MathF.Ceiling(relative.Length() / step), 1, MaxSubsteps) : 1;

		// Si ya se solapaban al empezar el paso, el contacto sigue: la normal es la de menor solape ahora
		if (Overlap(a.Previous, a.Yaw, a.Shape, b.Previous, b.Yaw, b.Shape, out _))
			return Overlap(a.Position, a.Yaw, a.Shape, b.Position, b.Yaw, b.Shape, out hit);

		Vector2 lastA = a.Previous;
		Vector2 lastB = b.Previous;
		for (int k = 1; k <= substeps; k++)
		{
			float t = (float)k / substeps;
			Vector2 pa = Vector2.Lerp(a.Previous, a.Position, t);
			Vector2 pb = Vector2.Lerp(b.Previous, b.Position, t);
			if (Overlap(pa, a.Yaw, a.Shape, pb, b.Yaw, b.Shape, out _))
			{
				// Acaban de tocarse: la normal es el eje que más las separaba justo antes (con un solape hondo, el de
				// menor solape puede ser el de lado aunque vengan de detrás) y la profundidad, la del final por él
				Vector2 normal = SeparatingAxis(lastA, a.Yaw, a.Shape, lastB, b.Yaw, b.Shape);
				float depth = Separation(a.Position, a.Yaw, a.Shape, b.Position, b.Yaw, b.Shape, normal);
				if (depth <= 0f)
					return false;
				hit = new ContactHit(normal, depth);
				return true;
			}
			lastA = pa;
			lastB = pb;
		}
		return false;
	}

	// El eje (de los cuatro) por el que más separadas están dos naves que no se solapan, de a hacia b
	public static Vector2 SeparatingAxis(Vector2 positionA, float yawA, in ShipShape shapeA, Vector2 positionB, float yawB, in ShipShape shapeB)
	{
		Vector2 d = shapeB.Center(positionB, yawB) - shapeA.Center(positionA, yawA);
		Span<Vector2> axes = stackalloc Vector2[4]
		{
			ShipShape.Forward(yawA), ShipShape.Right(yawA), ShipShape.Forward(yawB), ShipShape.Right(yawB)
		};
		float best = float.MinValue;
		Vector2 bestAxis = axes[0];
		foreach (Vector2 axis in axes)
		{
			float distance = Vector2.Dot(d, axis);
			float gap = MathF.Abs(distance) - Radius(shapeA, yawA, axis) - Radius(shapeB, yawB, axis);
			if (gap > best)
			{
				best = gap;
				bestAxis = distance < 0f ? -axis : axis;
			}
		}
		return bestAxis;
	}

	// Separación de ejes de dos rectángulos orientados: true si se solapan, con la normal de a hacia b y la profundidad
	public static bool Overlap(Vector2 positionA, float yawA, in ShipShape shapeA, Vector2 positionB, float yawB, in ShipShape shapeB,
		out ContactHit hit)
	{
		hit = default;
		Vector2 ca = shapeA.Center(positionA, yawA);
		Vector2 cb = shapeB.Center(positionB, yawB);
		Vector2 d = cb - ca;

		Span<Vector2> axes = stackalloc Vector2[4]
		{
			ShipShape.Forward(yawA), ShipShape.Right(yawA), ShipShape.Forward(yawB), ShipShape.Right(yawB)
		};

		float best = float.MaxValue;
		Vector2 bestNormal = default;
		foreach (Vector2 axis in axes)
		{
			float distance = Vector2.Dot(d, axis);
			float overlap = Radius(shapeA, yawA, axis) + Radius(shapeB, yawB, axis) - MathF.Abs(distance);
			if (overlap <= 0f)
				return false;
			if (overlap < best)
			{
				best = overlap;
				bestNormal = distance < 0f ? -axis : axis;
			}
		}
		hit = new ContactHit(bestNormal, best);
		return true;
	}

	// Cuánto hay que separar a (hacia -normal) y b (hacia +normal) para que dejen de solaparse por esa normal
	public static float Separation(Vector2 positionA, float yawA, in ShipShape shapeA, Vector2 positionB, float yawB, in ShipShape shapeB,
		Vector2 normal)
	{
		Vector2 d = shapeB.Center(positionB, yawB) - shapeA.Center(positionA, yawA);
		return Radius(shapeA, yawA, normal) + Radius(shapeB, yawB, normal) - Vector2.Dot(d, normal);
	}

	// Medio ancho del rectángulo proyectado en un eje
	private static float Radius(in ShipShape shape, float yaw, Vector2 axis) =>
		shape.HalfLength * MathF.Abs(Vector2.Dot(ShipShape.Forward(yaw), axis))
		+ shape.HalfWidth * MathF.Abs(Vector2.Dot(ShipShape.Right(yaw), axis));
}
