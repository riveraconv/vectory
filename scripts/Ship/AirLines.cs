using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Parámetros de las líneas de aire del rebufo en la cabina (spec docs/rivales-4-personalidades.md, 4.2), en m
public struct AirLineSettings
{
	public const int DefaultCount = 10;
	public const float DefaultIntensity = 0.5f;

	public int Count;            // de 8 a 12
	public float Intensity;      // brillo = esto × r
	public float StartAhead;     // m por delante del ojo donde nacen (unos metros por delante del morro)
	public float EndBehind;      // m por detrás del ojo donde acaban (pasan por su lado)
	public float StartRadius;    // m del eje de la vista al nacer
	public float EndRadius;      // m al pasar junto al cristal
	public float Spread;         // rad a cada lado de "arriba" (0 = arriba): por los lados y por arriba, no por abajo
	public float FlowFactor;     // fracción de la velocidad de la nave a la que corren
	public float MinLength;      // fracción del recorrido que mide cada línea, parada…
	public float MaxLength;      // …y a la punta
	public float FullLengthSpeed;   // m/s a los que llega a MaxLength

	public static AirLineSettings Default => new()
	{
		Count = DefaultCount,
		Intensity = DefaultIntensity,
		StartAhead = 16f,
		EndBehind = 1f,
		StartRadius = 0.5f,
		EndRadius = 2.4f,
		Spread = 2.1f,
		FlowFactor = 0.35f,
		MinLength = 0.08f,
		MaxLength = 0.35f,
		FullLengthSpeed = 110f
	};
}

// Un trozo de línea de aire: de A a B en el espacio del ojo (x derecha, y arriba, −z delante) con su brillo
public readonly record struct AirLineSegment(Vector3 A, Vector3 B, float Brightness);

// Líneas de aire del rebufo (spec 4.2): finas y tenues, nacen por delante del morro y se abren alrededor del cristal, por
// los lados y por arriba, como el aire que rodea la burbuja de la nave de delante. Corren hacia el ojo con la velocidad de
// la nave; su largo crece con ella. Brillo = Intensity × r, más tenue en las puntas. Solo geometría: lógica pura.
public static class AirLines
{
	private const int Pieces = 4;   // trozos rectos por línea, para la curva

	// Los trozos que hay que dibujar ahora; vacío con r = 0. time: s que lleva corriendo (con la pausa no avanza).
	public static List<AirLineSegment> Segments(float strength, float speed, float time, in AirLineSettings s)
	{
		var segments = new List<AirLineSegment>();
		int count = Math.Clamp(s.Count, 0, 64);
		if (strength <= 0f || count == 0)
			return segments;

		float path = s.StartAhead + s.EndBehind;
		float length = s.MinLength + (s.MaxLength - s.MinLength) * Math.Clamp(speed / MathF.Max(1f, s.FullLengthSpeed), 0f, 1f);
		float flow = MathF.Max(0f, speed) * s.FlowFactor / MathF.Max(1f, path);
		for (int k = 0; k < count; k++)
		{
			// Repartidas en el abanico, cada una con su fase fija (desordenadas, para que no vayan en fila)
			float angle = count == 1 ? 0f : -s.Spread + 2f * s.Spread * k / (count - 1);
			float phase = Fraction(k * 0.618034f);
			float head = Fraction(phase + time * flow);
			float tail = head - length;
			for (int p = 0; p < Pieces; p++)
			{
				float u0 = tail + (head - tail) * p / Pieces;
				float u1 = tail + (head - tail) * (p + 1) / Pieces;
				if (u1 <= 0f)
					continue;
				u0 = MathF.Max(0f, u0);
				float middle = 0.5f * (u0 + u1);
				// Más tenue al nacer y al acabar, y hacia la cola
				float fade = MathF.Sin(MathF.PI * Math.Clamp(middle, 0f, 1f)) * ((p + 1f) / Pieces);
				segments.Add(new AirLineSegment(At(u0, angle, s), At(u1, angle, s), s.Intensity * strength * fade));
			}
		}
		return segments;
	}

	// Punto de una línea: u de 0 (nace) a 1 (pasa junto al ojo); se abre más cuanto más cerca
	public static Vector3 At(float u, float angle, in AirLineSettings s)
	{
		u = Math.Clamp(u, 0f, 1f);
		float z = -s.StartAhead + (s.StartAhead + s.EndBehind) * u;
		float radius = s.StartRadius + (s.EndRadius - s.StartRadius) * u * u;
		return new Vector3(MathF.Sin(angle) * radius, MathF.Cos(angle) * radius, z);
	}

	private static float Fraction(float x) => x - MathF.Floor(x);
}
