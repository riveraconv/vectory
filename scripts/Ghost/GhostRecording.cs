using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ghost;

// Una muestra de la vuelta: tiempo de vuelta (s), posición y rotación de la nave
public readonly record struct GhostSample(double Time, Vector3 Position, Quaternion Rotation);

// Vuelta grabada para la nave fantasma de TIME TRIAL (lógica pura, sin Godot).
// Además de las muestras guarda con qué circuito, trazado y checkpoints se hizo y con qué diseño
// de nave, para saber si sigue sirviendo (GhostValidity).
public sealed record GhostRecording
{
	public string CircuitId { get; init; } = "";
	public string TrackSignature { get; init; } = "";
	public int CheckpointCount { get; init; }
	public string ShipDesignId { get; init; } = "";
	public double LapTime { get; init; }
	public int SampleRate { get; init; }
	public IReadOnlyList<GhostSample> Samples { get; init; } = Array.Empty<GhostSample>();
}
