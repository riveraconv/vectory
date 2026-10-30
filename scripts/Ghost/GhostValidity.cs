using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Vectory.Records;

namespace Vectory.Ghost;

// Cuándo se puede usar el fantasma guardado de un circuito (spec ghost 6.3). Lógica pura.
// Tiene que coincidir con el circuito actual (Id, checkpoints y trazado) y con su récord de TIME TRIAL.
public static class GhostValidity
{
	public const double LapTimeTolerance = 0.001;   // 1 ms
	public const int MinSamples = 2;

	public static bool IsValid(GhostRecording ghost, string circuitId, int checkpointCount, string trackSignature,
		double? recordLapTime)
	{
		return ghost != null
			&& RecordBook.IsValidCircuitId(circuitId)
			&& ghost.CircuitId == circuitId
			&& ghost.CheckpointCount == checkpointCount
			&& ghost.TrackSignature == trackSignature
			&& recordLapTime.HasValue
			&& Math.Abs(ghost.LapTime - recordLapTime.Value) <= LapTimeTolerance + 1e-9
			&& ghost.Samples != null
			&& ghost.Samples.Count >= MinSamples;
	}
}

// Firma del trazado (spec ghost 6.4): sale de los puntos del eje del circuito y del ancho de pista,
// redondeados a 2 decimales, con un hash estable (FNV-1a de 64 bits; da lo mismo en cada ejecución,
// al contrario que GetHashCode). Si el trazado cambia, la firma cambia y el fantasma viejo no se usa.
public static class TrackSignature
{
	private const ulong FnvOffset = 14695981039346656037UL;
	private const ulong FnvPrime = 1099511628211UL;

	public static string Compute(IEnumerable<Vector3> centerline, float trackWidth)
	{
		var text = new StringBuilder();
		if (centerline != null)
		{
			foreach (Vector3 p in centerline)
				text.Append(Round(p.X)).Append(',').Append(Round(p.Y)).Append(',').Append(Round(p.Z)).Append(';');
		}
		text.Append('|').Append(Round(trackWidth));

		ulong hash = FnvOffset;
		foreach (byte b in Encoding.UTF8.GetBytes(text.ToString()))
		{
			hash ^= b;
			hash *= FnvPrime;
		}
		return hash.ToString("x16", CultureInfo.InvariantCulture);
	}

	// + 0.0 quita el signo de -0.00 (si no, -0.001 y 0.001 darían firmas distintas)
	private static string Round(float value) =>
		(Math.Round((double)value, 2, MidpointRounding.AwayFromZero) + 0.0).ToString("F2", CultureInfo.InvariantCulture);
}
