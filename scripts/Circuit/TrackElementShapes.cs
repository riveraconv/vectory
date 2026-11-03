using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Circuit;

// Tramo del circuito (inicio, fin) en m sobre el eje desde la meta: túneles y avisos de curva
public readonly struct TrackSpan
{
	public readonly float Start;
	public readonly float End;

	public TrackSpan(float start, float end)
	{
		Start = start;
		End = end;
	}

	public float Length => End - Start;

	public bool Contains(float distance) => distance >= Start && distance <= End;
}

// Baliza de cresta: dónde va (m sobre el eje) y cuántas flechas ^ lleva
public readonly struct CrestBeacon
{
	public readonly float Distance;
	public readonly int Arrows;

	public CrestBeacon(float distance, int arrows)
	{
		Distance = distance;
		Arrows = arrows;
	}
}

// Formas y disposición de los elementos del circuito (spec docs/elementos-circuito.md, 11), sin Godot.
// Los perfiles son puntos (d = lateral sobre la superficie, h = altura a lo largo de N) que Track pasa a 3D
// con el marco de la pista; la disposición da las distancias sobre el eje donde va cada cosa.
public static class TrackElementShapes
{
	// Meta: medio hexágono con W = medio ancho + holgura
	public static Vector2[] FinishProfile(float halfTrackWidth, float clearance, float height)
	{
		float w = halfTrackWidth + clearance;
		return new[]
		{
			new Vector2(-w, 0f),
			new Vector2(-w, 0.6f * height),
			new Vector2(-0.5f * w, height),
			new Vector2(0.5f * w, height),
			new Vector2(w, 0.6f * height),
			new Vector2(w, 0f)
		};
	}

	// Checkpoint: medio rectángulo con W = medio ancho + holgura
	public static Vector2[] CheckpointProfile(float halfTrackWidth, float clearance, float height)
	{
		float w = halfTrackWidth + clearance;
		return new[]
		{
			new Vector2(-w, 0f),
			new Vector2(-w, height),
			new Vector2(w, height),
			new Vector2(w, 0f)
		};
	}

	// Túnel: octógono abierto por abajo (los extremos quedan a ras de pista)
	public static Vector2[] TunnelProfile(float halfTrackWidth, float clearance, float height)
	{
		float w = halfTrackWidth + clearance;
		return new[]
		{
			new Vector2(-w, 0f),
			new Vector2(-w, 0.42f * height),
			new Vector2(-0.72f * w, 0.84f * height),
			new Vector2(-0.32f * w, height),
			new Vector2(0.32f * w, height),
			new Vector2(0.72f * w, 0.84f * height),
			new Vector2(w, 0.42f * height),
			new Vector2(w, 0f)
		};
	}

	// Anillos del túnel: cada "spacing" m desde el inicio; si el último tramo no cuadra, el último va justo en el fin
	public static float[] TunnelRings(TrackSpan span, float spacing)
	{
		var rings = new List<float>();
		if (span.Length <= 0f)
			return rings.ToArray();

		spacing = MathF.Max(0.5f, spacing);
		const float Epsilon = 0.01f;
		for (int k = 0; span.Start + k * spacing < span.End - Epsilon; k++)
			rings.Add(span.Start + k * spacing);
		rings.Add(span.End);
		return rings.ToArray();
	}

	// Chevrons de un aviso de curva: cada "spacing" m desde el inicio, sin pasar del fin
	public static float[] ChevronPositions(TrackSpan span, float spacing)
	{
		var positions = new List<float>();
		spacing = MathF.Max(0.5f, spacing);
		for (int k = 0; span.Start + k * spacing <= span.End + 0.01f; k++)
			positions.Add(span.Start + k * spacing);
		return positions.ToArray();
	}

	// Giro del eje en planta a lo largo de un tramo, en radianes: positivo = a la derecha (sentido horario visto
	// desde arriba), negativo = a la izquierda. Suma los cambios de la tangente en planta cada "step" m.
	public static float SignedTurn(TrackFrame frame, TrackSpan span, float step = 2f)
	{
		float turn = 0f;
		Vector3 previous = frame.At(span.Start).PlanTangent;
		for (float s = span.Start + step; ; s += step)
		{
			bool last = s >= span.End;
			Vector3 tangent = frame.At(last ? span.End : s).PlanTangent;
			// Y del producto vectorial: negativo al girar a la derecha (X derecha, -Z adelante, Y arriba)
			float cross = Vector3.Cross(previous, tangent).Y;
			turn -= MathF.Atan2(cross, Vector3.Dot(previous, tangent));
			previous = tangent;
			if (last)
				break;
		}
		return turn;
	}

	// Lado de los chevrons: en la barrera exterior de la curva (a la derecha → barrera izquierda)
	public static BarrierSide ChevronSide(float signedTurn) => signedTurn >= 0f ? BarrierSide.Left : BarrierSide.Right;

	// Balizas de una cresta: a las distancias dadas antes de ella, con más flechas cuanto más cerca
	// (150, 100 y 50 m → 1, 2 y 3 flechas). Las distancias pueden venir en cualquier orden.
	public static CrestBeacon[] CrestBeacons(float crest, IReadOnlyList<float> distances)
	{
		var sorted = new List<float>(distances ?? Array.Empty<float>());
		sorted.RemoveAll(d => d <= 0f);
		sorted.Sort((a, b) => b.CompareTo(a));

		var beacons = new CrestBeacon[sorted.Count];
		for (int i = 0; i < sorted.Count; i++)
			beacons[i] = new CrestBeacon(crest - sorted[i], i + 1);
		return beacons;
	}

	// Tramo de barrera naranja de una cresta: de "halfLength" m antes a "halfLength" m después
	public static TrackSpan CrestBarrier(float crest, float halfLength) => new(crest - halfLength, crest + halfLength);

	// ¿Está "distance" dentro del tramo de barrera naranja de alguna cresta? Con la vuelta cerrada
	// (una cresta cerca de la meta cubre el final y el principio de la vuelta).
	public static bool InCrestBarrier(float distance, IReadOnlyList<float> crests, float halfLength, float lapLength)
	{
		if (crests == null)
			return false;
		foreach (float crest in crests)
		{
			float d = MathF.Abs(distance - crest);
			if (lapLength > 0f)
			{
				d %= lapLength;
				d = MathF.Min(d, lapLength - d);
			}
			if (d <= halfLength)
				return true;
		}
		return false;
	}
}
