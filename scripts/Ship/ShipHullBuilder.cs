using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Sección en rombo de un casco: arriba, derecha, abajo e izquierda, con los lados a la altura Side
public readonly record struct LoftSection(float Z, float HalfWidth, float Top, float Bottom, float Side = 0.1f);

// Piezas con las que se construyen los cascos de Hoja y Martillo, traducidas de designs/naves-geometria.js: mismos
// puntos, mismo orden y mismos colores, así sus líneas coinciden con la captura de referencia. Cada línea es una
// arista. El casco de Vector tiene su propio constructor: sus líneas van en otro orden. Lógica pura.
public sealed class ShipHullBuilder
{
	private const int NozzleSides = 6;

	private readonly List<ShipLine> _lines = new();

	public IReadOnlyList<ShipLine> Lines => _lines;

	public void Segment(Vector3 a, Vector3 b, ShipLineColor color = ShipLineColor.Cyan)
	{
		_lines.Add(new ShipLine(a, b, color, _lines.Count));
	}

	// Polígono cerrado
	public void Poly(params Vector3[] points)
	{
		for (int i = 0; i < points.Length; i++)
			Segment(points[i], points[(i + 1) % points.Length]);
	}

	// Casco por secciones en rombo, con punta delantera y trasera opcionales
	public void Loft(LoftSection[] sections, Vector3? nose = null, Vector3? tail = null)
	{
		Vector3[] previous = null;
		for (int i = 0; i < sections.Length; i++)
		{
			LoftSection s = sections[i];
			Vector3[] ring =
			{
				new(0f, s.Top, s.Z),
				new(s.HalfWidth, s.Side, s.Z),
				new(0f, s.Bottom, s.Z),
				new(-s.HalfWidth, s.Side, s.Z)
			};
			Poly(ring);

			for (int j = 0; j < ring.Length; j++)
			{
				if (previous != null)
					Segment(previous[j], ring[j]);
				else if (nose.HasValue)
					Segment(nose.Value, ring[j]);
			}
			previous = ring;
		}

		if (tail.HasValue && previous != null)
		{
			foreach (Vector3 point in previous)
				Segment(point, tail.Value);
		}
	}

	// Tobera: anillo delantero y trasero, boca azul 0,4 m por detrás y núcleo azul
	public void Nozzle(float cx, float cy, float z0, float z1, float radius)
	{
		Vector3[] front = NozzleRing(cx, cy, z0, radius);
		Vector3[] back = NozzleRing(cx, cy, z1, radius);
		Vector3[] mouth = NozzleRing(cx, cy, z1 + 0.4f, radius * 0.7f);
		var core = new Vector3(cx, cy, z1 + 0.2f);

		for (int k = 0; k < NozzleSides; k++)
		{
			int next = (k + 1) % NozzleSides;
			Segment(front[k], front[next]);
			Segment(back[k], back[next]);
			Segment(front[k], back[k]);
			Segment(mouth[k], mouth[next], ShipLineColor.Blue);
			Segment(back[k], mouth[k]);
			Segment(core, mouth[k], ShipLineColor.Blue);
		}
	}

	// Pieza simétrica: se construye primero a la derecha (1) y luego a la izquierda (-1)
	public void Mirror(Action<float> piece)
	{
		piece(1f);
		piece(-1f);
	}

	private static Vector3[] NozzleRing(float cx, float cy, float z, float radius)
	{
		var ring = new Vector3[NozzleSides];
		for (int k = 0; k < NozzleSides; k++)
		{
			float angle = MathF.Tau * k / NozzleSides;
			ring[k] = new Vector3(cx + MathF.Cos(angle) * radius, cy + MathF.Sin(angle) * radius, z);
		}
		return ring;
	}
}
