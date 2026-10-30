using System;
using System.Collections.Generic;

namespace Vectory.Ship;

// Parpadeo de un nivel de daño: qué parte de las aristas parpadea y cada cuánto (s, aleatorio entre mínimo y máximo)
public struct FlickerLevelSettings
{
	public float Fraction;
	public float MinInterval;
	public float MaxInterval;
}

// Parámetros del parpadeo de las aristas dañadas (spec docs/danos.md, 4). Se editan en el Inspector del Player.
public struct EdgeFlickerSettings
{
	public FlickerLevelSettings Level1;
	public FlickerLevelSettings Level2;
	public FlickerLevelSettings Level3;
	public float MinDuration;   // segundos que dura un parpadeo (aleatorio entre mínimo y máximo)
	public float MaxDuration;

	// Valores de la spec
	public static EdgeFlickerSettings Default => new()
	{
		Level1 = new FlickerLevelSettings { Fraction = 0.10f, MinInterval = 1.5f, MaxInterval = 3f },
		Level2 = new FlickerLevelSettings { Fraction = 0.20f, MinInterval = 0.8f, MaxInterval = 1.6f },
		Level3 = new FlickerLevelSettings { Fraction = 0.35f, MinInterval = 0.3f, MaxInterval = 0.8f },
		MinDuration = 0.06f,
		MaxDuration = 0.12f
	};
}

// Parpadeo de las aristas de una malla según el nivel de daño. Lógica pura: no depende de Godot.
//
// SetLevel elige qué aristas parpadean: al subir de nivel se añaden más y las anteriores se mantienen.
// Update avanza el tiempo: cada arista elegida espera un intervalo aleatorio, se apaga un instante y vuelve.
// IsDimmed dice si una arista está apagada ahora. El generador se inyecta (semilla fija en las pruebas).
public class EdgeFlicker
{
	private readonly Random _random;
	private readonly List<int> _candidates = new();   // aristas que todavía no parpadean
	private readonly List<int> _affected = new();
	private readonly bool[] _dimmed;
	private readonly Dictionary<int, float> _wait = new();      // segundos hasta el siguiente parpadeo
	private readonly Dictionary<int, float> _dimLeft = new();   // segundos que le quedan apagada

	public EdgeFlicker(int edgeCount, EdgeFlickerSettings settings, Random random)
	{
		EdgeCount = Math.Max(0, edgeCount);
		Settings = settings;
		_random = random ?? throw new ArgumentNullException(nameof(random));
		_dimmed = new bool[EdgeCount];
		for (int i = 0; i < EdgeCount; i++)
			_candidates.Add(i);
	}

	public int EdgeCount { get; }

	// Se pueden cambiar en vivo (Inspector); afectan a los siguientes intervalos y a la próxima subida de nivel
	public EdgeFlickerSettings Settings { get; set; }

	public int Level { get; private set; }

	// Aristas que parpadean, en el orden en que se eligieron
	public IReadOnlyList<int> AffectedEdges => _affected;

	public bool IsDimmed(int edge) => edge >= 0 && edge < EdgeCount && _dimmed[edge];

	// Sube al nivel indicado (0–3) y añade aristas hasta la proporción del nivel. Nunca quita aristas.
	public void SetLevel(int level)
	{
		level = Math.Clamp(level, 0, DamageModel.MaxLevel);
		if (level <= Level)
			return;

		Level = level;
		FlickerLevelSettings s = Current();
		int target = Math.Min(EdgeCount, (int)MathF.Round(s.Fraction * EdgeCount));

		// Las que ya parpadeaban no esperan más que el intervalo máximo del nivel nuevo
		foreach (int edge in _affected)
			_wait[edge] = MathF.Min(_wait[edge], s.MaxInterval);

		while (_affected.Count < target && _candidates.Count > 0)
		{
			int pick = _random.Next(_candidates.Count);
			int edge = _candidates[pick];
			_candidates[pick] = _candidates[^1];
			_candidates.RemoveAt(_candidates.Count - 1);

			_affected.Add(edge);
			// Primera espera repartida al azar, así no parpadean todas a la vez
			_wait[edge] = NextInterval(s) * (float)_random.NextDouble();
			_dimLeft[edge] = 0f;
		}
	}

	// Avanza dt segundos. Devuelve true si alguna arista se ha apagado o encendido.
	public bool Update(float dt)
	{
		if (dt <= 0f || _affected.Count == 0)
			return false;

		FlickerLevelSettings s = Current();
		bool changed = false;
		foreach (int edge in _affected)
		{
			if (_dimmed[edge])
			{
				_dimLeft[edge] -= dt;
				if (_dimLeft[edge] > 0f)
					continue;
				_dimmed[edge] = false;
				_wait[edge] = NextInterval(s);
				changed = true;
			}
			else
			{
				_wait[edge] -= dt;
				if (_wait[edge] > 0f)
					continue;
				_dimmed[edge] = true;
				_dimLeft[edge] = Between(Settings.MinDuration, Settings.MaxDuration);
				changed = true;
			}
		}
		return changed;
	}

	private FlickerLevelSettings Current() => Level switch
	{
		1 => Settings.Level1,
		2 => Settings.Level2,
		_ => Settings.Level3
	};

	private float NextInterval(in FlickerLevelSettings s) => Between(s.MinInterval, s.MaxInterval);

	private float Between(float min, float max)
	{
		if (max <= min)
			return min;
		return min + (max - min) * (float)_random.NextDouble();
	}
}
