using Godot;
using Vectory.Core;

namespace Vectory.Ship;

// Tintes de los bordes de la pantalla con el boost (naranja) y con daño (rojo), comunes a todas las cabinas: un degradado
// radial de transparente en el centro a su color en los bordes (spec docs/naves-3b-cabinas.md, 5). La opacidad del momento
// la pone quien los enseña (CockpitHudState.BoostTint y DamageTint).
public static class CockpitTints
{
	private const int TextureSize = 128;

	// Centro y radio del degradado en fracciones de la pantalla
	private static readonly Vector2 Center = new(0.5f, 0.55f);
	private static readonly Vector2 Edge = new(1.25f, 0.55f);

	private static GradientTexture2D _boost;
	private static GradientTexture2D _damage;

	public static GradientTexture2D Boost => _boost ??= Build(Palette.Orange, 0.55f, 0.35f);

	public static GradientTexture2D Damage => _damage ??= Build(Palette.DamageHeavy, 0.5f, 0.4f);

	// Transparente hasta clearUntil (fracción del radio) y el color con edgeOpacity en el borde
	private static GradientTexture2D Build(Color color, float clearUntil, float edgeOpacity)
	{
		var gradient = new Gradient
		{
			Offsets = new[] { clearUntil, 1f },
			Colors = new[] { new Color(color, 0f), new Color(color, color.A * edgeOpacity) }
		};
		return new GradientTexture2D
		{
			Gradient = gradient,
			Width = TextureSize,
			Height = TextureSize,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = Center,
			FillTo = Edge
		};
	}
}
