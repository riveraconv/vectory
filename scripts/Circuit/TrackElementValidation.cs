using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Circuit;

// Validación de los datos de los elementos del circuito (spec docs/elementos-circuito.md, 7.1, 8.1 y 9.1), sin Godot.
// Lo que no vale se ignora con un aviso en "warnings"; nunca lanza.
public static class TrackElementValidation
{
	// Tramos (inicio, fin) en el orden de la lista. Se ignoran: inicio ≥ fin, fuera de la vuelta (cruzaría la
	// meta) y los que se solapan con uno ya aceptado. Los aceptados de menos de minLength m solo dan aviso.
	public static List<TrackSpan> ValidateSpans(IReadOnlyList<Vector2> raw, float lapLength, string label,
		float minLength, ICollection<string> warnings)
	{
		var accepted = new List<TrackSpan>();
		if (raw == null)
			return accepted;

		for (int i = 0; i < raw.Count; i++)
		{
			var span = new TrackSpan(raw[i].X, raw[i].Y);
			string name = $"{label}[{i}] ({span.Start:0.#}–{span.End:0.#} m)";

			if (!float.IsFinite(span.Start) || !float.IsFinite(span.End) || span.Start >= span.End)
			{
				warnings?.Add($"{name}: start must be before end (it cannot cross the finish line); ignored");
				continue;
			}
			if (span.Start < 0f || span.End > lapLength)
			{
				warnings?.Add($"{name}: crosses the finish line or is outside the lap (0–{lapLength:0.#} m); ignored");
				continue;
			}
			if (accepted.Exists(other => span.Start < other.End && other.Start < span.End))
			{
				warnings?.Add($"{name}: overlaps another one; ignored");
				continue;
			}
			if (span.Length < minLength)
				warnings?.Add($"{name}: shorter than {minLength:0.#} m");

			accepted.Add(span);
		}
		return accepted;
	}

	// Crestas a señalizar: se ignoran las que están fuera de la vuelta
	public static List<float> ValidateCrests(IReadOnlyList<float> raw, float lapLength, ICollection<string> warnings)
	{
		var accepted = new List<float>();
		if (raw == null)
			return accepted;

		for (int i = 0; i < raw.Count; i++)
		{
			float crest = raw[i];
			if (!float.IsFinite(crest) || crest < 0f || crest >= lapLength)
			{
				warnings?.Add($"CrestWarnings[{i}] ({crest:0.#} m): outside the lap (0–{lapLength:0.#} m); ignored");
				continue;
			}
			accepted.Add(crest);
		}
		return accepted;
	}

	// Aviso si una cresta queda dentro de un túnel o a menos de entryMargin m antes de su entrada
	public static void CheckCrestsNearTunnels(IReadOnlyList<float> crests, IReadOnlyList<TrackSpan> tunnels,
		float entryMargin, ICollection<string> warnings)
	{
		if (crests == null || tunnels == null)
			return;

		foreach (float crest in crests)
		{
			foreach (TrackSpan tunnel in tunnels)
			{
				if (tunnel.Contains(crest))
					warnings?.Add($"crest at {crest:0.#} m is inside the tunnel {tunnel.Start:0.#}–{tunnel.End:0.#} m");
				else if (crest < tunnel.Start && tunnel.Start - crest < entryMargin)
					warnings?.Add($"crest at {crest:0.#} m is less than {entryMargin:0.#} m before the tunnel {tunnel.Start:0.#}–{tunnel.End:0.#} m");
			}
		}
	}
}
