namespace Vectory.Ship;

// Entrada de conducción de un instante, ya traducida de teclas a intenciones.
// Lógica pura: no depende de Godot.
public struct ShipInput
{
	public bool Accelerate;
	public bool Brake;
	public bool SteerLeft;
	public bool SteerRight;
}
