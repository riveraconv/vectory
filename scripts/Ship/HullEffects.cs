using System;

namespace Vectory.Ship;

// Daño visible del casco 3D (spec docs/cascos-3d-1-vector.md, 6.5): quemaduras, parpadeo de luces y humo. Solo en
// RACE; en TIME TRIAL no hay nada. Lógica pura.
public static class HullDamage
{
	// Brillo de una luz apagada por el parpadeo, sobre el suyo
	public const float DimFactor = 0.2f;

	public const float SmokeLifetime = 1.6f;
	public const float SmokeRiseMin = 0.7f;
	public const float SmokeRiseMax = 1.1f;
	public const float SmokeSideSpread = 0.2f;
	public const float SmokeStartSize = 0.35f;
	public const float SmokeEndSize = 1.95f;
	public const float SmokeSpeedGrowth = 0.01f;   // m de tamaño final por m/s de la nave, como el prototipo
	public const float SmokeStartOpacity = 0.55f;
	public const uint SmokeColorLevel2 = 0x4A4A4A;
	public const uint SmokeColorLevel3 = 0x262626;
	// Casi negro, como el oscurecido del prototipo
	public const uint ScorchColor = 0x050404;

	// Máscara de quemaduras del nivel (1–3; 0 = ninguna)
	public static int ScorchMask(int level, bool timeTrial) => timeTrial ? 0 : Math.Clamp(level, 0, DamageModel.MaxLevel);

	public static float SmokePerSecond(int level, bool timeTrial)
	{
		if (timeTrial)
			return 0f;
		return Math.Clamp(level, 0, DamageModel.MaxLevel) switch
		{
			2 => 7f,
			3 => 20f,
			_ => 0f
		};
	}

	public static uint SmokeColor(int level) => level >= 3 ? SmokeColorLevel3 : SmokeColorLevel2;

	// Tamaño final de una bocanada: más grande cuanto más rápido va la nave (m/s)
	public static float SmokeFinalSize(float speed) => SmokeEndSize + MathF.Max(0f, speed) * SmokeSpeedGrowth;

	// Las luces que titilan (14): las de acento y los pilotos traseros, no las toberas
	public static int FlickerLightCount => 14;
}
