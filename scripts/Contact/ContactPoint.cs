using System;
using System.Numerics;

namespace Vectory.Contact;

// Punto de contacto entre dos naves en planta (spec docs/efectos-1-choques.md, 3): el centro del solape de sus
// rectángulos, recortando uno con el otro (Sutherland–Hodgman). En un alcance por detrás queda en el morro del de
// detrás; puerta con puerta, entre los dos costados. Si ya no se solapan (redondeos), el punto medio entre los dos
// puntos más cercanos de cada centro hacia el otro. Lógica pura.
public static class ContactPoint
{
	public static Vector2 Between(Vector2 positionA, float yawA, in ShipShape shapeA, Vector2 positionB, float yawB, in ShipShape shapeB)
	{
		Span<Vector2> a = stackalloc Vector2[4];
		Span<Vector2> b = stackalloc Vector2[4];
		Corners(positionA, yawA, shapeA, a);
		Corners(positionB, yawB, shapeB, b);

		// El recorte de un cuadrilátero convexo con otro tiene como mucho 8 vértices
		Span<Vector2> poly = stackalloc Vector2[16];
		Span<Vector2> next = stackalloc Vector2[16];
		int count = 4;
		a.CopyTo(poly);
		for (int e = 0; e < 4 && count > 0; e++)
		{
			Vector2 p0 = b[e];
			Vector2 p1 = b[(e + 1) % 4];
			int n = 0;
			for (int i = 0; i < count; i++)
			{
				Vector2 cur = poly[i];
				Vector2 prev = poly[(i + count - 1) % count];
				bool curIn = Inside(p0, p1, cur);
				bool prevIn = Inside(p0, p1, prev);
				if (curIn)
				{
					if (!prevIn)
						next[n++] = Cross(prev, cur, p0, p1);
					next[n++] = cur;
				}
				else if (prevIn)
				{
					next[n++] = Cross(prev, cur, p0, p1);
				}
			}
			next.Slice(0, n).CopyTo(poly);
			count = n;
		}

		if (count >= 3)
		{
			Vector2 c = Centroid(poly.Slice(0, count));
			if (float.IsFinite(c.X) && float.IsFinite(c.Y))
				return c;
		}
		if (count > 0)
		{
			Vector2 sum = Vector2.Zero;
			for (int i = 0; i < count; i++)
				sum += poly[i];
			return sum / count;
		}
		Vector2 ca = shapeA.Center(positionA, yawA);
		Vector2 cb = shapeB.Center(positionB, yawB);
		return (Closest(a, cb) + Closest(b, ca)) * 0.5f;
	}

	// Esquinas del frente derecho al frente izquierdo: en los ejes de planta (x, z) van en sentido antihorario
	private static void Corners(Vector2 position, float yaw, in ShipShape shape, Span<Vector2> corners)
	{
		Vector2 f = ShipShape.Forward(yaw);
		Vector2 r = ShipShape.Right(yaw);
		Vector2 c = shape.Center(position, yaw);
		Vector2 hl = f * shape.HalfLength;
		Vector2 hw = r * shape.HalfWidth;
		corners[0] = c + hl + hw;
		corners[1] = c - hl + hw;
		corners[2] = c - hl - hw;
		corners[3] = c + hl - hw;
	}

	// Lado interior de la arista p0 → p1: la izquierda, con las esquinas en sentido antihorario
	private static bool Inside(Vector2 p0, Vector2 p1, Vector2 p) => Cross2(p1 - p0, p - p0) >= -1e-6f;

	private static float Cross2(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

	private static Vector2 Cross(Vector2 a, Vector2 b, Vector2 p0, Vector2 p1)
	{
		Vector2 d = b - a;
		Vector2 e = p1 - p0;
		float denom = Cross2(d, e);
		if (MathF.Abs(denom) < 1e-9f)
			return a;
		float t = Cross2(p0 - a, e) / denom;
		return a + d * t;
	}

	private static Vector2 Centroid(ReadOnlySpan<Vector2> poly)
	{
		float area = 0f;
		Vector2 c = Vector2.Zero;
		for (int i = 0; i < poly.Length; i++)
		{
			Vector2 p = poly[i];
			Vector2 q = poly[(i + 1) % poly.Length];
			float k = Cross2(p, q);
			area += k;
			c += (p + q) * k;
		}
		if (MathF.Abs(area) < 1e-8f)
		{
			Vector2 sum = Vector2.Zero;
			foreach (Vector2 p in poly)
				sum += p;
			return sum / poly.Length;
		}
		return c / (3f * area);
	}

	private static Vector2 Closest(ReadOnlySpan<Vector2> corners, Vector2 point)
	{
		Vector2 best = corners[0];
		float bestD = float.MaxValue;
		for (int i = 0; i < 4; i++)
		{
			Vector2 a = corners[i];
			Vector2 b = corners[(i + 1) % 4];
			Vector2 ab = b - a;
			float t = Math.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared(), 0f, 1f);
			Vector2 p = a + ab * t;
			float d = Vector2.DistanceSquared(p, point);
			if (d < bestD)
			{
				bestD = d;
				best = p;
			}
		}
		return best;
	}
}
