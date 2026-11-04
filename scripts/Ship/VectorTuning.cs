namespace Vectory.Ship;

// Valores de conducción de VECTOR (ship_01): el único sitio del que salen. Son el perfil técnico de su definición
// (VectorShip) y para tocar uno se cambia aquí. Lógica pura: no depende de Godot.
public static class VectorTuning
{
	private const float KmhPerMs = 3.6f;

	public const float MaxSpeed = 110f;
	public const float Acceleration = 25f;
	public const float BrakeDeceleration = 45f;
	public const float Drag = 5f;
	public const float OverspeedDecayKmh = 20f;   // km/h que pierde cada segundo por encima de MaxSpeed
	public const float TurnRate = 1.3f;
	public const float SteerResponse = 8f;
	public const float LateralGrip = 5f;

	// Los mismos valores con las unidades de ShipDynamics
	public static ShipTuning Values => new()
	{
		MaxSpeed = MaxSpeed,
		Acceleration = Acceleration,
		BrakeDeceleration = BrakeDeceleration,
		Drag = Drag,
		OverspeedDecay = OverspeedDecayKmh / KmhPerMs,
		TurnRate = TurnRate,
		SteerResponse = SteerResponse,
		LateralGrip = LateralGrip
	};
}
