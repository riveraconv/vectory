namespace Vectory.Ship;

// Parámetros de conducción que usa ShipDynamics. Los valores se editan en el Inspector
// del nodo Player, que rellena esta estructura en cada paso de física.
// Lógica pura: no depende de Godot.
public struct ShipTuning
{
	public float MaxSpeed;
	public float BoostMaxSpeed;
	public float Acceleration;
	public float BoostAcceleration;
	public float BrakeDeceleration;
	public float Drag;
	public float TurnRate;       // rad/s de giro máximo
	public float SteerResponse;  // lo rápido que llega el giro a su valor máximo
	public float LateralGrip;    // más alto = menos inercia lateral
}
