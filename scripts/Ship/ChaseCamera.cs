using Godot;

namespace Vectory.Ship;

// Parámetros de la cámara de tercera persona (los edita el Inspector del nodo Player)
public struct ChaseCameraSettings
{
	public float FollowRate;     // más alto = cámara más pegada, menos efecto dron
	public float Height;
	public float Distance;
	public float ExtraDistance;  // distancia extra a máxima velocidad
	public float BaseFov;
	public float MaxFov;
}

// Cámara de tercera persona: va detrás de la nave sin retraso en posición, pero su orientación
// se retrasa un poco al girar (efecto dron) y se aleja y abre el FOV con la velocidad.
// Se mueve sola (TopLevel), no hereda la inclinación de la nave.
public partial class ChaseCamera : Camera3D
{
	private float _camYaw;

	public override void _Ready()
	{
		TopLevel = true;
	}

	// Coloca la cámara de golpe, sin suavizado
	public void Snap(Vector3 targetPosition, float yaw, float speedFactor, in ChaseCameraSettings s)
	{
		_camYaw = yaw;
		Place(targetPosition, speedFactor, s);
		Fov = Mathf.Lerp(s.BaseFov, s.MaxFov, speedFactor);
	}

	// Sigue al objetivo con suavizado. speedFactor va de 0 a 1.
	public void UpdateFollow(float dt, Vector3 targetPosition, float yaw, float speedFactor, in ChaseCameraSettings s)
	{
		float t = 1f - Mathf.Exp(-s.FollowRate * dt);
		_camYaw = Mathf.LerpAngle(_camYaw, yaw, t);
		Place(targetPosition, speedFactor, s);

		float targetFov = Mathf.Lerp(s.BaseFov, s.MaxFov, speedFactor);
		Fov = Mathf.Lerp(Fov, targetFov, t);
	}

	private void Place(Vector3 targetPosition, float speedFactor, in ChaseCameraSettings s)
	{
		var camBasis = new Basis(Vector3.Up, _camYaw);
		var offset = new Vector3(0f, s.Height, s.Distance + speedFactor * s.ExtraDistance);

		GlobalPosition = targetPosition + camBasis * offset;
		LookAt(targetPosition + camBasis * new Vector3(0f, 0.3f, -10f), Vector3.Up);
	}
}
