using System;
using System.Collections.Generic;
using Vectory.Autopilot;

namespace Vectory.Rivals;

// Los seis perfiles de piloto (spec docs/rivales-4-personalidades.md, 3.2)
public enum PilotProfile
{
	Balanced,
	Sprinter,
	CornerSpecialist,
	Aggressive,
	Conservative,
	Technical
}

// Cuándo gasta el boost (spec 4.6; se usa en la etapa 2)
public enum BoostStrategy
{
	Overtake,
	Defend,
	LastLap
}

// La variación fija de un piloto sobre los rasgos de su perfil (spec 3.4): márgenes ±1 punto, frenada ±0,02, reacción
// ±0,03 s, ataque, defensa y contacto ±0,1 y errores ±0,1. La estrategia de boost no varía.
public readonly record struct PilotVariation(float StraightMargin, float CurveMargin, float Braking, float Reaction,
	float Attack, float Defense, float Contact, float Errors)
{
	public const float MaxMargin = 0.01f;
	public const float MaxBraking = 0.02f;
	public const float MaxReaction = 0.03f;
	public const float MaxDuel = 0.1f;     // ataque, defensa y contacto
	public const float MaxErrors = 0.1f;

	public bool IsWithinLimits =>
		Within(StraightMargin, MaxMargin) && Within(CurveMargin, MaxMargin) && Within(Braking, MaxBraking)
		&& Within(Reaction, MaxReaction) && Within(Attack, MaxDuel) && Within(Defense, MaxDuel) && Within(Contact, MaxDuel)
		&& Within(Errors, MaxErrors);

	private static bool Within(float value, float max) => MathF.Abs(value) <= max + 1e-6f;
}

// Los rasgos con los que conduce un piloto (spec 3.1). En la etapa 1 cuentan los de ritmo (márgenes, frenada y
// reacción); los demás están definidos y se usan en las etapas 2 y 3. Lógica pura.
//  - StraightMargin: fracción de la punta que usa el perfil de velocidad donde manda la punta.
//  - CurveMargin: fracción de la velocidad de curva de su tabla donde manda la curva.
//  - Braking: fracción de la frenada (sobre la del perfil de la spec 6) con la que calcula dónde frenar; menos = antes.
//  - Reaction: s desde el GO hasta que pisa W.
public readonly record struct PilotTraits(float StraightMargin, float CurveMargin, float Braking, float Reaction,
	float Attack, float Defense, float Contact, BoostStrategy Boost, float Errors)
{
	public const float MinMargin = 0.90f;
	public const float MaxStraightMargin = 1.00f;                          // la punta de la nave
	public const float MaxCurveMargin = AutopilotSettings.MaxCurveMargin;  // 1,05
	public const float MinBraking = 0.85f;
	public const float MaxBraking = 1.00f;
	public const float MinReaction = 0.15f;
	public const float MaxReaction = 0.40f;
	public const float MinErrors = 0.3f;
	public const float MaxErrors = 2.0f;

	// Lo mismo que el piloto automático de F7 y del equilibrio: punta entera, curva al 97 %, frenada de la spec 6 y sin
	// esperar en el GO. No es un perfil del juego: sirve para comparar.
	public static PilotTraits Autopilot => new(1f, AutopilotSettings.DefaultSpeedMargin, 1f, 0f, 0.5f, 0.5f, 0.3f, BoostStrategy.Overtake, 1f);

	// Perfil + variación, limitados a sus rangos
	public static PilotTraits Effective(in PilotTraits profile, in PilotVariation variation) => new PilotTraits(
		profile.StraightMargin + variation.StraightMargin,
		profile.CurveMargin + variation.CurveMargin,
		profile.Braking + variation.Braking,
		profile.Reaction + variation.Reaction,
		profile.Attack + variation.Attack,
		profile.Defense + variation.Defense,
		profile.Contact + variation.Contact,
		profile.Boost,
		profile.Errors + variation.Errors).Clamped();

	public PilotTraits Clamped() => new(
		Math.Clamp(StraightMargin, MinMargin, MaxStraightMargin),
		Math.Clamp(CurveMargin, MinMargin, MaxCurveMargin),
		Math.Clamp(Braking, MinBraking, MaxBraking),
		Math.Clamp(Reaction, MinReaction, MaxReaction),
		Math.Clamp(Attack, 0f, 1f),
		Math.Clamp(Defense, 0f, 1f),
		Math.Clamp(Contact, 0f, 1f),
		Boost,
		Math.Clamp(Errors, MinErrors, MaxErrors));

	// Los márgenes con el ajuste global de depuración (PACE de F9, en fracción), sin pasar de sus topes
	public float StraightWith(float paceOffset) => Math.Clamp(StraightMargin + paceOffset, MinPaceMargin, MaxStraightMargin);
	public float CurveWith(float paceOffset) => Math.Clamp(CurveMargin + paceOffset, MinPaceMargin, MaxCurveMargin);

	// Lo más bajo a lo que el ajuste de depuración puede llevar un margen
	public const float MinPaceMargin = 0.5f;
}

// Los seis perfiles con sus rasgos (spec 3.2). Márgenes ajustados con BalanceRunner en modo PERFILES (3.5) y la curva
// subida 13 puntos en la etapa 4, para que NORMAL pelee con el usuario (decisiones del usuario). Lógica pura: solo datos.
public static class PilotProfiles
{
	public static IReadOnlyList<PilotProfile> All { get; } = Array.AsReadOnly((PilotProfile[])Enum.GetValues(typeof(PilotProfile)));

	public static PilotTraits Traits(PilotProfile profile) => profile switch
	{
		PilotProfile.Balanced => new(0.99f, 1.12f, 0.95f, 0.25f, 0.5f, 0.5f, 0.3f, BoostStrategy.Overtake, 1.0f),
		PilotProfile.Sprinter => new(1.00f, 1.09f, 0.92f, 0.20f, 0.6f, 0.3f, 0.3f, BoostStrategy.Overtake, 1.0f),
		PilotProfile.CornerSpecialist => new(0.98f, 1.15f, 1.00f, 0.25f, 0.5f, 0.6f, 0.3f, BoostStrategy.Defend, 1.0f),
		PilotProfile.Aggressive => new(0.99f, 1.12f, 1.00f, 0.18f, 0.9f, 0.8f, 0.9f, BoostStrategy.Overtake, 1.5f),
		PilotProfile.Conservative => new(0.99f, 1.12f, 0.88f, 0.32f, 0.2f, 0.2f, 0.0f, BoostStrategy.LastLap, 0.5f),
		PilotProfile.Technical => new(0.98f, 1.13f, 0.98f, 0.25f, 0.6f, 0.7f, 0.2f, BoostStrategy.LastLap, 0.6f),
		_ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
	};

	// Nombre en pantalla (panel de pruebas, consola, BalanceRunner)
	public static string ScreenName(PilotProfile profile) => profile switch
	{
		PilotProfile.Balanced => "BALANCED",
		PilotProfile.Sprinter => "SPRINTER",
		PilotProfile.CornerSpecialist => "CORNERS",
		PilotProfile.Aggressive => "AGGRESSIVE",
		PilotProfile.Conservative => "CONSERVATIVE",
		PilotProfile.Technical => "TECHNICAL",
		_ => profile.ToString().ToUpperInvariant()
	};

	public static string ScreenName(BoostStrategy strategy) => strategy switch
	{
		BoostStrategy.Overtake => "OVERTAKE",
		BoostStrategy.Defend => "DEFEND",
		BoostStrategy.LastLap => "LAST LAP",
		_ => strategy.ToString().ToUpperInvariant()
	};
}
