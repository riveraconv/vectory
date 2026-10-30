using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ghost;

// Reproducción del fantasma (lógica pura, sin Godot): posición y rotación de la grabación en un tiempo
// de vuelta. Posición con interpolación lineal y rotación con slerp entre las dos muestras más cercanas.
// Antes de t = 0 devuelve la primera muestra; pasado el final, "terminado" (false).
public static class GhostPlayback
{
	public static bool TryGetPose(GhostRecording recording, double lapTime, out Vector3 position, out Quaternion rotation)
	{
		position = default;
		rotation = Quaternion.Identity;

		IReadOnlyList<GhostSample> samples = recording?.Samples;
		if (samples == null || samples.Count == 0 || lapTime > samples[^1].Time)
			return false;

		if (lapTime <= samples[0].Time)
		{
			position = samples[0].Position;
			rotation = samples[0].Rotation;
			return true;
		}

		// Última muestra con tiempo <= lapTime (lo) y la siguiente (hi)
		int lo = 0;
		int hi = samples.Count - 1;
		while (hi - lo > 1)
		{
			int mid = (lo + hi) / 2;
			if (samples[mid].Time <= lapTime)
				lo = mid;
			else
				hi = mid;
		}

		GhostSample a = samples[lo];
		GhostSample b = samples[hi];
		double span = b.Time - a.Time;
		float t = span > 0 ? (float)((lapTime - a.Time) / span) : 0f;
		t = System.Math.Clamp(t, 0f, 1f);

		position = Vector3.Lerp(a.Position, b.Position, t);
		rotation = Quaternion.Normalize(Quaternion.Slerp(a.Rotation, b.Rotation, t));
		return true;
	}
}
