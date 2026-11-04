namespace Vectory.Ship;

// INTERCEPTOR · HOJA (ship_02): más punta y menos agarre que Vector. Lógica pura.
// El perfil visual es provisional: el de Vector con su propio ancho, hasta que la spec 3 traiga su geometría.
public static class HojaShip
{
	private static readonly ShipMultipliers Multipliers = new()
	{
		MaxSpeed = 1.08f,
		Acceleration = 0.94f,
		TurnRate = 1f,
		LateralGrip = 0.92f,
		BrakeDeceleration = 1.02f
	};

	public static readonly ShipDefinition Definition = new(
		ShipCatalog.Ship02,
		Multipliers.Apply(VectorTuning.Values),
		VectorShip.Definition.Visual with { CollisionHalfWidth = 1.6f },   // las puntas traseras en flecha
		Multipliers);
}
