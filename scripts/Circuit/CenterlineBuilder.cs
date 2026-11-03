using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Circuit;

// Eje (línea central) de un circuito a partir de sus puntos de control, sin Godot: lo usan Track (carrera),
// la miniatura y los datos de SELECT CIRCUIT, así el eje se puede calcular sin montar la carrera.
//
// Los puntos de control (en píxeles de la imagen) se pasan a metros, se suavizan con una spline cerrada
// (Hermite con tangentes escaladas por la longitud del tramo) y se muestrean cada ~SampleStep metros.
// Las operaciones repiten EXACTAMENTE las de los vectores de Godot (mismo orden y redondeo en float):
// si cambiara un solo punto, cambiaría la firma del trazado y los fantasmas guardados dejarían de valer.
public static class CenterlineBuilder
{
	public const float SampleStep = 4f;   // metros entre muestras de la spline

	// Puntos del eje, empezando en la meta (el primer punto de control), en el plano XZ a la altura trackY.
	// clockwise = false: el mismo circuito al revés, manteniendo el primer punto como salida.
	public static List<Vector3> Build(IReadOnlyList<Vector2> controlPixels, Vector2 imageCenter, float metersPerPixel,
		bool clockwise, float trackY)
	{
		if (controlPixels == null)
			throw new ArgumentNullException(nameof(controlPixels));

		var control = new List<Vector3>(controlPixels.Count);
		foreach (Vector2 px in controlPixels)
			control.Add(new Vector3((px.X - imageCenter.X) * metersPerPixel, trackY, (px.Y - imageCenter.Y) * metersPerPixel));

		if (!clockwise && control.Count > 0)
		{
			Vector3 first = control[0];
			control.RemoveAt(0);
			control.Reverse();
			control.Insert(0, first);
		}

		return SampleSpline(control);
	}

	// Distancia acumulada sobre el eje cerrado hasta cada punto; la última entrada (n) es la vuelta completa
	public static float[] CumulativeDistances(IReadOnlyList<Vector3> centerline)
	{
		int n = centerline.Count;
		var cumulative = new float[n + 1];
		for (int i = 0; i < n; i++)
			cumulative[i + 1] = cumulative[i] + Distance(centerline[i], centerline[(i + 1) % n]);
		return cumulative;
	}

	// Índices del eje donde va cada checkpoint, en orden de carrera: repartidos a distancias iguales
	// sobre la vuelta (sin contar la meta). Vacío con count <= 0.
	public static List<int> CheckpointIndices(IReadOnlyList<Vector3> centerline, int count)
	{
		var indices = new List<int>();
		if (count <= 0 || centerline.Count == 0)
			return indices;

		int n = centerline.Count;
		float[] cumulative = CumulativeDistances(centerline);
		float total = cumulative[n];

		int index = 0;
		for (int k = 1; k <= count; k++)
		{
			float target = total * k / (count + 1);
			while (index < n - 1 && cumulative[index + 1] < target)
				index++;
			indices.Add(index);
		}
		return indices;
	}

	// Distancia entre dos puntos, como Vector3.DistanceTo de Godot: (b - a).Length()
	public static float Distance(Vector3 a, Vector3 b)
	{
		return Length(new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z));
	}

	private static List<Vector3> SampleSpline(List<Vector3> pts)
	{
		int n = pts.Count;
		var dirs = new Vector3[n];
		for (int i = 0; i < n; i++)
		{
			Vector3 next = pts[(i + 1) % n];
			Vector3 prev = pts[(i - 1 + n) % n];
			dirs[i] = Normalized(new Vector3(next.X - prev.X, next.Y - prev.Y, next.Z - prev.Z));
		}

		var result = new List<Vector3>();
		for (int i = 0; i < n; i++)
		{
			Vector3 p0 = pts[i];
			Vector3 p1 = pts[(i + 1) % n];
			float d = Distance(p0, p1);
			Vector3 m0 = Scale(dirs[i], d);
			Vector3 m1 = Scale(dirs[(i + 1) % n], d);

			int steps = Math.Max(2, (int)MathF.Ceiling(d / SampleStep));
			for (int s = 0; s < steps; s++)
			{
				float t = s / (float)steps;
				float t2 = t * t;
				float t3 = t2 * t;
				float a = 2f * t3 - 3f * t2 + 1f;
				float b = t3 - 2f * t2 + t;
				float c = -2f * t3 + 3f * t2;
				float e = t3 - t2;
				result.Add(new Vector3(
					a * p0.X + b * m0.X + c * p1.X + e * m1.X,
					a * p0.Y + b * m0.Y + c * p1.Y + e * m1.Y,
					a * p0.Z + b * m0.Z + c * p1.Z + e * m1.Z));
			}
		}
		return result;
	}

	// Como Vector3.Length de Godot (float, sumando x², y², z² en ese orden)
	private static float Length(Vector3 v)
	{
		float x2 = v.X * v.X;
		float y2 = v.Y * v.Y;
		float z2 = v.Z * v.Z;
		return MathF.Sqrt(x2 + y2 + z2);
	}

	// Como Vector3.Normalized de Godot (divide cada componente por la longitud; cero si la longitud es 0)
	private static Vector3 Normalized(Vector3 v)
	{
		float x2 = v.X * v.X;
		float y2 = v.Y * v.Y;
		float z2 = v.Z * v.Z;
		float lengthSq = x2 + y2 + z2;
		if (lengthSq == 0f)
			return Vector3.Zero;

		float length = MathF.Sqrt(lengthSq);
		return new Vector3(v.X / length, v.Y / length, v.Z / length);
	}

	private static Vector3 Scale(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
}
