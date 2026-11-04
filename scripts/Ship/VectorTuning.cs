namespace Vectory.Ship;

// Valores base de VECTOR (ship_01): el único sitio del que salen. Los [Export] del Player los toman como valores
// por defecto y SELECT SHIP los lee sin montar la escena de carrera. Lógica pura: no depende de Godot.
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

	public const int BoostMaxCharges = 3;
	public const float BoostSpeedGainKmh = 100f;  // km/h que suma cada carga

	// Los mismos valores con las unidades de ShipDynamics, igual que los monta el Player sin daño
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
