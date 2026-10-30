using System;
using Vectory.Circuit;

namespace Vectory.Ship;

// Parámetros de los daños (spec docs/danos.md). Los valores se editan en el Inspector de Main.
public struct DamageSettings
{
	public int MediumPoints;          // puntos por impacto medio
	public int StrongPoints;          // puntos por impacto fuerte
	public int Level1Points;          // puntos desde los que se llega a cada nivel
	public int Level2Points;
	public int Level3Points;
	public float Level2TopSpeedFactor;   // factor de la velocidad punta con nivel 2
	public float Level3TopSpeedFactor;   // factor de la velocidad punta con nivel 3
	public float Level3TurnFactor;       // factor de la velocidad de giro con nivel 3

	// Valores de la spec
	public static DamageSettings Default => new()
	{
		MediumPoints = 1,
		StrongPoints = 3,
		Level1Points = 3,
		Level2Points = 6,
		Level3Points = 9,
		Level2TopSpeedFactor = 0.92f,
		Level3TopSpeedFactor = 0.85f,
		Level3TurnFactor = 0.85f
	};
}

// Daños acumulativos de una nave en RACE: los impactos medios y fuertes suman puntos y los puntos
// dan el nivel (0–3). El nivel no baja durante la carrera; una carrera nueva crea un modelo nuevo.
// Lógica pura: no depende de Godot. No sabe nada de la nave: da factores que otros aplican.
public class DamageModel
{
	public const int MaxLevel = 3;

	private readonly DamageSettings _settings;

	public DamageModel(DamageSettings settings)
	{
		_settings = settings;
	}

	public int Points { get; private set; }

	public int Level { get; private set; }

	// Con la carrera terminada los impactos ya no suman
	public bool Stopped { get; private set; }

	// Factor de la velocidad punta base (1 sin efecto)
	public float TopSpeedFactor => Level >= 3 ? _settings.Level3TopSpeedFactor
		: Level == 2 ? _settings.Level2TopSpeedFactor
		: 1f;

	// Factor de la velocidad de giro (1 sin efecto)
	public float TurnFactor => Level >= 3 ? _settings.Level3TurnFactor : 1f;

	// Con nivel 3 el boost no se puede activar
	public bool BoostAllowed => Level < MaxLevel;

	// Se lanza una vez por subida de nivel, con el nivel nuevo (si un impacto salta dos niveles, un solo aviso)
	public event Action<int> LevelChanged;

	// Suma los puntos de un impacto que cuenta (el roce no suma)
	public void AddImpact(ImpactLevel impact)
	{
		if (Stopped)
			return;

		int points = impact switch
		{
			ImpactLevel.Medium => _settings.MediumPoints,
			ImpactLevel.Strong => _settings.StrongPoints,
			_ => 0
		};
		if (points <= 0)
			return;

		Points += points;
		int level = LevelFor(Points);
		if (level == Level)
			return;

		Level = level;
		LevelChanged?.Invoke(Level);
	}

	// La carrera ha terminado: a partir de aquí no se suma nada
	public void Stop()
	{
		Stopped = true;
	}

	private int LevelFor(int points)
	{
		if (points >= _settings.Level3Points)
			return 3;
		if (points >= _settings.Level2Points)
			return 2;
		if (points >= _settings.Level1Points)
			return 1;
		return 0;
	}
}
