namespace Vectory.Ship;

// Parámetros del boost por cargas que usa ShipBoost. Los valores se editan en el Inspector
// del nodo Player, que rellena esta estructura en cada paso de física.
// Lógica pura: no depende de Godot. Velocidades en m/s (el Player convierte desde km/h).
public struct BoostTuning
{
	public int MaxCharges;
	public int InitialCharges;
	public float RechargeTime;   // segundos para ganar una carga
	public float SpeedGain;      // m/s que suma cada carga
	public float RampDuration;   // segundos de subida (en los que se reparte el impulso)
	public float HoldDuration;   // segundos de mantenimiento de la velocidad alcanzada
	public float Cooldown;       // segundos entre usos
}
