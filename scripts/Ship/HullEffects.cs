using System;

namespace Vectory.Ship;

// Daño visible del casco 3D (spec docs/cascos-3d-1-vector.md, 6.5): quemaduras y parpadeo de luces. El humo ya no es
// del casco: es el de docs/efectos-4-humo-danos.md (Effects/DamageSmoke). Solo en RACE; en TIME TRIAL no hay nada.
// Lógica pura.
public static class HullDamage
{
	// Brillo de una luz apagada por el parpadeo, sobre el suyo
	public const float DimFactor = 0.2f;

	// Casi negro, como el oscurecido del prototipo
	public const uint ScorchColor = 0x050404;

	// Máscara de quemaduras del nivel (1–3; 0 = ninguna)
	public static int ScorchMask(int level, bool timeTrial) => timeTrial ? 0 : Math.Clamp(level, 0, DamageModel.MaxLevel);

	// Las luces que titilan (14): las de acento y los pilotos traseros, no las toberas
	public static int FlickerLightCount => 14;
}
