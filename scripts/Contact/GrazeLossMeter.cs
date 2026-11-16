using System.Collections.Generic;

namespace Vectory.Contact;

// Velocidad que el roce le ha quitado a una nave en los últimos Window segundos (spec docs/rivales-3-contacto.md v1.1, 9),
// para el panel de pruebas. Un apunte por paso de física. Lógica pura: no depende de Godot.
public sealed class GrazeLossMeter
{
	public const float DefaultWindow = 5f;

	private readonly Queue<(double Time, float Loss)> _entries = new();
	private double _time;
	private float _total;

	public float Window { get; }

	// m/s perdidos por roce en la ventana
	public float Total => _total;

	// Tipo del último roce de este paso (None si no hay)
	public GrazeKind Kind { get; private set; }

	public GrazeLossMeter(float window = DefaultWindow)
	{
		Window = window;
	}

	public void Add(float dt, float loss, GrazeKind kind)
	{
		_time += dt;
		Kind = kind;
		if (loss > 0f)
		{
			_entries.Enqueue((_time, loss));
			_total += loss;
		}
		while (_entries.Count > 0 && _entries.Peek().Time <= _time - Window)
			_total -= _entries.Dequeue().Loss;
		if (_entries.Count == 0)
			_total = 0f;
	}
}
