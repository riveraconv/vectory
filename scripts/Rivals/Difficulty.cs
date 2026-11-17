using System;

namespace Vectory.Rivals;

// Dificultad de los rivales (spec docs/rivales-4-personalidades.md, 6)
public enum Difficulty
{
	Easy,
	Normal,
	Hard
}

// Lo que cambia cada dificultad (spec 6.1): solo los rasgos de los pilotos, nunca la física, y sin rubber banding (nada
// depende de cómo vaya la carrera). Va sobre el perfil y la variación de cada piloto: en DIFÍCIL un conservador sigue
// siendo conservador, pero mejor. NORMAL no cambia nada. Lógica pura.
//
// Los márgenes van por separado (v1.1, etapa 4): la recta casi no tiene sitio (la punta de la nave) y la curva sí.
// |                     | FÁCIL     | NORMAL | DIFÍCIL                        |
// | Margen de recta     | −3 puntos | 0      | +1 punto (≤ 100 %)             |
// | Margen de curva     | −12 puntos| 0      | +13 puntos (≤ 130 %)           |
// | Frenada             | −0,04     | 0      | +0,02 (≤ 1)                    |
// | Reacción            | +0,15 s   | 0      | −0,05 s                        |
// | Ataque y defensa    | × 0,6     | × 1    | × 1,2 (≤ 1)                    |
// | Contacto            | × 0,6     | × 1    | × 1                            |
// | Errores             | × 2       | × 1    | × 0,4                          |
public static class DifficultyRules
{
	public const Difficulty Default = Difficulty.Normal;

	public static PilotTraits Apply(in PilotTraits t, Difficulty difficulty)
	{
		(float straight, float curve, float braking, float reaction, float duel, float contact, float errors) = difficulty switch
		{
			Difficulty.Easy => (-0.03f, -0.12f, -0.04f, 0.15f, 0.6f, 0.6f, 2f),
			Difficulty.Hard => (0.01f, 0.13f, 0.02f, -0.05f, 1.2f, 1f, 0.4f),
			_ => (0f, 0f, 0f, 0f, 1f, 1f, 1f)
		};
		return new PilotTraits(
			MathF.Min(PilotTraits.MaxStraightMargin, t.StraightMargin + straight),
			MathF.Min(PilotTraits.MaxCurveMargin, t.CurveMargin + curve),
			MathF.Min(PilotTraits.MaxBraking, t.Braking + braking),
			MathF.Max(0f, t.Reaction + reaction),
			MathF.Min(1f, t.Attack * duel),
			MathF.Min(1f, t.Defense * duel),
			MathF.Min(1f, t.Contact * contact),
			t.Boost,
			t.Errors * errors);
	}

	// Nombre en pantalla (panel de pruebas, consola, BalanceRunner)
	public static string ScreenName(Difficulty difficulty) => difficulty switch
	{
		Difficulty.Easy => "EASY",
		Difficulty.Hard => "HARD",
		_ => "NORMAL"
	};

	// La siguiente, con vuelta (selector del panel de pruebas)
	public static Difficulty Next(Difficulty difficulty) => difficulty switch
	{
		Difficulty.Easy => Difficulty.Normal,
		Difficulty.Normal => Difficulty.Hard,
		_ => Difficulty.Easy
	};
}
