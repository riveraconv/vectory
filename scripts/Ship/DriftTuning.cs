namespace Vectory.Ship;

// Parámetros del derrape que usa ShipDynamics. Los valores se editan en el Inspector
// del nodo Player, que rellena esta estructura en cada paso de física.
// Lógica pura: no depende de Godot. Velocidades en m/s y ángulos en radianes (el Player convierte).
// Con Enabled = false (el valor por defecto) la nave no derrapa nunca.
public struct DriftTuning
{
	public bool Enabled;
	public DriftMode Mode;
	public float EnterSpeed;         // velocidad real mínima para entrar en derrape
	public float ExitSpeed;          // por debajo, se sale del derrape
	public float Grip;               // fracción del agarre lateral normal durante el derrape
	public float CountersteerGrip;   // fracción del agarre mientras se contravolantea
	public float TurnMultiplier;     // multiplicador de la velocidad de giro del rumbo
	public float FixedLoss;          // m/s que se pierden cada segundo por derrapar
	public float SlipLoss;           // m/s por segundo extra a ángulo máximo (proporcional al ángulo)
	public float AccelerationFactor; // fracción de la aceleración normal con W
	public float BrakeFactor;        // fracción de la frenada normal en derrape (solo modo A)
	public float MaxSlipAngle;       // ángulo de deslizamiento máximo
	public float GripRecoveryTime;   // segundos para volver del agarre de derrape al 100 %
}

// Regla de entrada y salida del derrape (spec 3 y 5). Lógica pura.
public static class DriftRule
{
	// Si la nave debe estar derrapando en este paso.
	// held: se mantienen a la vez la tecla de derrape del modo (S en A, "drift" en B) y A o D.
	// Para entrar hace falta EnterSpeed; para seguir basta con no bajar de ExitSpeed.
	public static bool ShouldDrift(bool drifting, bool held, float speed, in DriftTuning t)
	{
		if (!t.Enabled || !held)
			return false;
		return speed >= (drifting ? t.ExitSpeed : t.EnterSpeed);
	}

	// La tecla que dispara el derrape en cada modo
	public static bool TriggerHeld(in ShipInput input, DriftMode mode)
	{
		return mode == DriftMode.BrakeTurn ? input.Brake : input.Drift;
	}
}
