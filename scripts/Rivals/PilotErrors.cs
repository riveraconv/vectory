using System;

namespace Vectory.Rivals;

// Los errores de la IA (spec docs/rivales-4-personalidades.md, 5.1)
public enum ErrorKind
{
	None,
	LateBraking,   // frena 5–15 m tarde y se va largo 2–4 m por fuera
	WideExit,      // acelera antes de tiempo en la salida y se abre hasta rozar o tocar la barrera
	BadStart       // en el GO, 0,3–0,8 s más de reacción
}

// El error de una curva: de qué tipo y cuánto (m de frenada tardía, m que se va por fuera y s que pierde)
public readonly record struct CurveError(ErrorKind Kind, float LateMeters, float WideMeters, float Loss = 0f)
{
	public static CurveError None => new(ErrorKind.None, 0f, 0f);
}

// Parámetros de los errores (spec 5 y 7)
public struct PilotErrorSettings
{
	public float RatePerLap;        // frenada tardía + salida abierta por vuelta, mitad y mitad
	public float PressureFactor;    // atacando, defendiendo o con alguien a menos de PressureDistance
	public float PressureDistance;  // m
	public float BadStartChance;    // por carrera
	public float LateMin, LateMax;  // m de frenada tardía
	public float WideMin, WideMax;  // m que se va largo por fuera
	public float LossMin, LossMax;  // s que pierde en esa curva
	public float StartMin, StartMax;   // s de más en una mala salida
	public float AlongsideGap;      // m entre cascos: con una nave al lado más cerca, ningún error

	public static PilotErrorSettings Default => new()
	{
		RatePerLap = 0.25f,
		PressureFactor = 1.5f,
		PressureDistance = 20f,
		BadStartChance = 0.10f,
		LateMin = 5f,
		LateMax = 15f,
		WideMin = 2f,
		WideMax = 4f,
		LossMin = 0.3f,
		LossMax = 0.8f,
		StartMin = 0.3f,
		StartMax = 0.8f,
		AlongsideGap = 3f
	};
}

// Cuándo se equivoca un piloto (spec 5.2 y 5.3). Todo sale de su generador: con la misma semilla, los mismos errores.
//  - Por curva: probabilidad = tasa por vuelta / curvas de la vuelta × errores del piloto × dificultad, × 1,5 bajo
//    presión; la mitad frenadas tardías y la mitad salidas abiertas. Una sola tirada por curva: nunca dos en la misma.
//  - Mala salida: BadStartChance por carrera × errores × dificultad.
//  - Con una nave al lado (solapadas a lo largo, a menos de AlongsideGap entre cascos), ninguno.
//  - Sin Enabled (ERRORS de F9), ninguno.
// Lógica pura: no depende de Godot.
public sealed class PilotErrors
{
	private readonly Random _random;

	public PilotErrorSettings Settings { get; set; } = PilotErrorSettings.Default;
	public float Multiplier { get; }     // errores del piloto × dificultad
	public bool Enabled { get; set; } = true;

	public int LateBrakings { get; private set; }
	public int WideExits { get; private set; }
	public int BadStarts { get; private set; }

	public PilotErrors(float multiplier, Random random)
	{
		Multiplier = MathF.Max(0f, multiplier);
		_random = random ?? new Random(0);
	}

	// Probabilidad de error en una curva
	public float CurveChance(int curvesPerLap, bool underPressure)
	{
		if (curvesPerLap <= 0)
			return 0f;
		PilotErrorSettings s = Settings;
		float chance = s.RatePerLap / curvesPerLap * Multiplier * (underPressure ? s.PressureFactor : 1f);
		return Math.Clamp(chance, 0f, 1f);
	}

	// La tirada de una curva. alongside: con una nave al lado, nunca.
	public CurveError ForCurve(int curvesPerLap, bool underPressure, bool alongside)
	{
		// Se tira siempre, así los errores de las curvas siguientes no dependen de quién iba al lado
		double roll = _random.NextDouble();
		double kind = _random.NextDouble();
		double amount = _random.NextDouble();
		double wide = _random.NextDouble();
		double loss = _random.NextDouble();
		if (!Enabled || alongside || roll >= CurveChance(curvesPerLap, underPressure))
			return CurveError.None;

		PilotErrorSettings s = Settings;
		float wideMeters = Lerp(s.WideMin, s.WideMax, (float)wide);
		float seconds = Lerp(s.LossMin, s.LossMax, (float)loss);
		if (kind < 0.5)
		{
			LateBrakings++;
			return new CurveError(ErrorKind.LateBraking, Lerp(s.LateMin, s.LateMax, (float)amount), wideMeters, seconds);
		}
		WideExits++;
		return new CurveError(ErrorKind.WideExit, 0f, wideMeters, seconds);
	}

	// La reacción de más de esta salida (0 si sale bien)
	public float StartDelay()
	{
		double roll = _random.NextDouble();
		double amount = _random.NextDouble();
		PilotErrorSettings s = Settings;
		if (!Enabled || roll >= Math.Clamp(s.BadStartChance * Multiplier, 0f, 1f))
			return 0f;
		BadStarts++;
		return Lerp(s.StartMin, s.StartMax, (float)amount);
	}

	private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
