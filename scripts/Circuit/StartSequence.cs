using System;

namespace Vectory.Circuit;

// Parámetros de la secuencia de salida (semáforo tipo F1 y cuenta atrás)
public struct StartSettings
{
	public double FirstLightDelay;  // segundos antes de la primera luz
	public double LightInterval;    // segundos entre luces
	public int LightCount;          // número de luces
	public double GoDuration;       // segundos que se ve el GO tras la salida
}

// Secuencia de salida: las luces se encienden de una en una con la cuenta atrás
// (5, 4, 3, 2, 1) y, cuando se apagan todas, se da la salida (GO).
// Lógica pura (sin Godot): se avanza con Update(delta) y se consulta su estado.
//
// Con los valores por defecto: 1 s → 1 luz y "5" ... 5 s → 5 luces y "1", 6 s → GO.
public sealed class StartSequence
{
	private readonly StartSettings _settings;

	public double Elapsed { get; private set; }

	// Luces encendidas ahora (0 antes de la primera y después de la salida)
	public int LightsOn { get; private set; }

	public int LightCount => _settings.LightCount;

	// Número de la cuenta atrás (null si no hay ninguno que mostrar)
	public int? CountdownNumber { get; private set; }

	// El GO se está mostrando
	public bool ShowGo { get; private set; }

	// La salida ya se ha dado
	public bool HasStarted { get; private set; }

	// Momento de la salida, en segundos desde el principio de la secuencia
	public double GoTime => _settings.FirstLightDelay + _settings.LightCount * _settings.LightInterval;

	// Se lanza una sola vez, en el momento de la salida
	public event Action Started;

	public StartSequence(StartSettings settings)
	{
		if (settings.LightCount < 1)
			throw new ArgumentOutOfRangeException(nameof(settings), "Debe haber al menos 1 luz.");
		if (settings.LightInterval <= 0)
			throw new ArgumentOutOfRangeException(nameof(settings), "El intervalo entre luces debe ser mayor que 0.");

		_settings = settings;
	}

	public void Update(double delta)
	{
		if (delta > 0)
			Elapsed += delta;

		if (Elapsed >= GoTime)
		{
			LightsOn = 0;
			CountdownNumber = null;
			ShowGo = Elapsed < GoTime + _settings.GoDuration;

			if (!HasStarted)
			{
				HasStarted = true;
				Started?.Invoke();
			}
			return;
		}

		if (Elapsed < _settings.FirstLightDelay)
		{
			LightsOn = 0;
			CountdownNumber = null;
			return;
		}

		// La luz k (1..LightCount) se enciende en FirstLightDelay + (k - 1) * LightInterval
		int lights = (int)Math.Floor((Elapsed - _settings.FirstLightDelay) / _settings.LightInterval) + 1;
		LightsOn = Math.Clamp(lights, 1, _settings.LightCount);
		CountdownNumber = _settings.LightCount - LightsOn + 1;
	}
}
