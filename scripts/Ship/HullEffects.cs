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

// Brillo de levitación bajo el casco 3D (spec 6.6). Lógica pura.
public static class HoverGlow
{
	public const float Width = 3.2f;
	public const float Length = 7.5f;
	public const float CenterZ = 0.2f;
	public const float Base = 0.32f;
	public const float Pulse = 0.06f;
	public const float PulseSpeed = 3f;          // rad/s de sen(3t)
	public const float FadeOutHeight = 3f;       // m por encima de la altura de vuelo: del todo apagado
	public const float ColorR = 0.14f, ColorG = 0.36f, ColorB = 1.0f;

	// Fuerza en el tiempo t (s) con la intensidad del Inspector (1 = la del prototipo)
	public static float Strength(float time, float intensity) => MathF.Max(0f, intensity * (Base + Pulse * MathF.Sin(PulseSpeed * time)));

	// Cuánto queda en el aire: 1 a la altura de vuelo o por debajo, 0 a FadeOutHeight por encima
	public static float AirFade(float heightAboveHover) => Math.Clamp(1f - heightAboveHover / FadeOutHeight, 0f, 1f);

	// Caída radial del prototipo: (1 − d²)², con d de 0 en el centro a 1 en el borde del plano
	public static float Falloff(float dx, float dy)
	{
		float d2 = dx * dx + dy * dy;
		float k = MathF.Max(0f, 1f - d2);
		return k * k;
	}
}
