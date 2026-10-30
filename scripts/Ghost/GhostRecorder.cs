using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ghost;

// Graba la vuelta en curso para el fantasma (lógica pura, sin Godot).
//  - Begin() empieza una grabación nueva desde cero (al cruzar la meta empezando vuelta).
//  - Sample() se llama cada paso con el tiempo de vuelta; solo guarda una muestra cuando toca según
//    SampleRate (la primera, en t = 0). El tiempo es el del cronómetro: la pausa no deja huecos.
//  - Finish() añade la muestra del cruce de meta con el tiempo de vuelta exacto y devuelve la grabación
//    (sin circuito ni diseño de nave: los pone quien la guarda).
//  - Discard() tira la grabación en curso.
public sealed class GhostRecorder
{
	public const int DefaultSampleRate = 20;

	private const double TimeEpsilon = 1e-6;

	private readonly List<GhostSample> _samples = new();
	private readonly double _interval;
	private int _nextSlot;   // siguiente muestra según la frecuencia: toca en _nextSlot / SampleRate segundos

	public int SampleRate { get; }

	public bool IsRecording { get; private set; }

	public IReadOnlyList<GhostSample> Samples => _samples;

	public GhostRecorder(int sampleRate = DefaultSampleRate)
	{
		if (sampleRate < 1)
			throw new ArgumentOutOfRangeException(nameof(sampleRate), "Debe haber al menos 1 muestra por segundo.");

		SampleRate = sampleRate;
		_interval = 1.0 / sampleRate;
	}

	public void Begin()
	{
		_samples.Clear();
		_nextSlot = 0;
		IsRecording = true;
	}

	// Añade una muestra si ya toca. Devuelve si la ha añadido.
	public bool Sample(double lapTime, Vector3 position, Quaternion rotation)
	{
		if (!IsRecording || lapTime + TimeEpsilon < _nextSlot * _interval)
			return false;

		_samples.Add(new GhostSample(lapTime, position, rotation));
		_nextSlot = (int)Math.Floor(lapTime / _interval + TimeEpsilon) + 1;
		return true;
	}

	// Cierra la grabación con la muestra del cruce de meta. null si no se estaba grabando.
	public GhostRecording Finish(double lapTime, Vector3 position, Quaternion rotation)
	{
		if (!IsRecording)
			return null;

		// La muestra final sustituye a las que caigan en el mismo tiempo (o después)
		while (_samples.Count > 0 && _samples[^1].Time >= lapTime - TimeEpsilon)
			_samples.RemoveAt(_samples.Count - 1);
		_samples.Add(new GhostSample(lapTime, position, rotation));

		var recording = new GhostRecording
		{
			LapTime = lapTime,
			SampleRate = SampleRate,
			Samples = _samples.ToArray()
		};

		IsRecording = false;
		_samples.Clear();
		return recording;
	}

	public void Discard()
	{
		_samples.Clear();
		_nextSlot = 0;
		IsRecording = false;
	}
}
