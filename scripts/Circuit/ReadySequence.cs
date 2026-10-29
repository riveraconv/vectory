using System;

namespace Vectory.Circuit;

// Parámetros de la preparación de la salida lanzada (TIME TRIAL)
public struct ReadySettings
{
	public double ReadyDuration;   // segundos con READY? antes de la salida
	public double GoDuration;      // segundos que se ve el GO! tras la salida
}

// Preparación de la salida lanzada: READY? durante ReadyDuration segundos y, al acabar,
// la salida (GO!), que se ve GoDuration segundos. Sin luces ni números (eso es StartSequence, en RACE).
// Lógica pura (sin Godot): se avanza con Update(delta) y se consulta su estado.
//
// Con los valores por defecto: 0–5 s → READY?, 5 s → salida y GO!, 6 s → nada.
public sealed class ReadySequence
{
	private readonly ReadySettings _settings;

	public double Elapsed { get; private set; }

	// Se está mostrando READY? (antes de la salida)
	public bool ShowReady => !HasStarted;

	// Se está mostrando GO! (justo después de la salida)
	public bool ShowGo { get; private set; }

	// La salida ya se ha dado
	public bool HasStarted { get; private set; }

	// Se lanza una sola vez, en el momento de la salida
	public event Action Started;

	public ReadySequence(ReadySettings settings)
	{
		if (settings.ReadyDuration < 0)
			throw new ArgumentOutOfRangeException(nameof(settings), "La espera no puede ser negativa.");
		if (settings.GoDuration < 0)
			throw new ArgumentOutOfRangeException(nameof(settings), "El tiempo del GO! no puede ser negativo.");

		_settings = settings;
	}

	public void Update(double delta)
	{
		if (delta > 0)
			Elapsed += delta;

		if (Elapsed < _settings.ReadyDuration)
			return;

		ShowGo = Elapsed < _settings.ReadyDuration + _settings.GoDuration;

		if (!HasStarted)
		{
			HasStarted = true;
			Started?.Invoke();
		}
	}
}
