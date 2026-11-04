namespace Vectory.Ship;

// VANGUARD · MARTILLO (ship_03): más aceleración y agarre que Vector, a cambio de punta, giro y frenada. Lógica pura.
// El perfil visual es provisional: el de Vector con su propio ancho, hasta que la spec 3 traiga su geometría.
public static class MartilloShip
{
	private static readonly ShipMultipliers Multipliers = new()
	{
		MaxSpeed = 0.94f,
		Acceleration = 1.10f,
		TurnRate = 0.96f,
		LateralGrip = 1.04f,
		BrakeDeceleration = 0.96f
	};

	public static readonly ShipDefinition Definition = new(
		ShipCatalog.Ship03,
		Multipliers.Apply(VectorTuning.Values),
		VectorShip.Definition.Visual with { CollisionHalfWidth = 2.5f },   // las puntas del canard
		Multipliers);
}
