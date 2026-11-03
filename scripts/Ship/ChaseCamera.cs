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
	public float UpSmoothTime;   // s: suavizado del "arriba" de la cámara
}

// Cámara de tercera persona: va detrás de la nave sin retraso en posición, pero su orientación
// se retrasa un poco al girar (efecto dron) y se aleja y abre el FOV con la velocidad.
// Se mueve sola (TopLevel), no hereda la inclinación de la nave. Su "arriba" sigue, suavizado, al que
// le pasa la nave (spec docs/pista-3d.md, 5.1: el de la superficie en el suelo, el del mundo en el aire),
// así el horizonte se ladea en los peraltes y la cámara sigue la pendiente.
public partial class ChaseCamera : Camera3D
{
	private float _camYaw;
	private Vector3 _up = Vector3.Up;
	private Vector2 _shakeOffset;
	private float _shakeRoll;
	private float _fov;        // FOV por velocidad, suavizado (sin el extra del boost)
	private Vector2 _vibration;
	private float _push;
	private float _fovExtra;

	// Efectos de cámara (spec docs/camara.md): vibración (m, en el plano de la vista), empuje hacia
	// atrás (m) y FOV extra (grados) del boost. Se aplican la próxima vez que se coloque.
	public void SetEffects(Vector2 vibration, float push, float fovExtra)
	{
		_vibration = vibration;
		_push = push;
		_fovExtra = fovExtra;
	}

	// Sacudida (desplazamiento en el plano de la vista, en m, y giro sobre la vista, en rad).
	// Solo mueve la cámara; se aplica la próxima vez que se coloque.
	public void SetShake(Vector2 offset, float roll)
	{
		_shakeOffset = offset;
		_shakeRoll = roll;
	}

	public override void _Ready()
	{
		TopLevel = true;
	}

	// Coloca la cámara de golpe, sin suavizado
	public void Snap(Vector3 targetPosition, float yaw, float speedFactor, in ChaseCameraSettings s, Vector3 up)
	{
		_camYaw = yaw;
		_up = up.Normalized();
		Place(targetPosition, speedFactor, s);
		_fov = Mathf.Lerp(s.BaseFov, s.MaxFov, speedFactor);
		Fov = _fov + _fovExtra;
	}

	// Sigue al objetivo con suavizado. speedFactor va de 0 a 1.
	public void UpdateFollow(float dt, Vector3 targetPosition, float yaw, float speedFactor, in ChaseCameraSettings s, Vector3 up)
	{
		float t = 1f - Mathf.Exp(-s.FollowRate * dt);
		_camYaw = Mathf.LerpAngle(_camYaw, yaw, t);

		float u = s.UpSmoothTime > 0f ? 1f - Mathf.Exp(-dt / s.UpSmoothTime) : 1f;
		_up = _up.Lerp(up.Normalized(), u).Normalized();

		Place(targetPosition, speedFactor, s);

		float targetFov = Mathf.Lerp(s.BaseFov, s.MaxFov, speedFactor);
		_fov = Mathf.Lerp(_fov, targetFov, t);
		Fov = _fov + _fovExtra;
	}

	private void Place(Vector3 targetPosition, float speedFactor, in ChaseCameraSettings s)
	{
		// Rumbo de la cámara, inclinado para que su "arriba" sea _up (en llano, sin inclinación)
		var camBasis = new Basis(Vector3.Up, _camYaw);
		if (!_up.IsEqualApprox(Vector3.Up))
			camBasis = new Basis(new Quaternion(Vector3.Up, _up)) * camBasis;
		var offset = new Vector3(0f, s.Height, s.Distance + speedFactor * s.ExtraDistance + _push);

		GlobalPosition = targetPosition + camBasis * offset;
		LookAt(targetPosition + camBasis * new Vector3(0f, 0.3f, -10f), camBasis * Vector3.Up);

		Vector2 planeOffset = _shakeOffset + _vibration;
		if (planeOffset != Vector2.Zero || _shakeRoll != 0f)
		{
			GlobalPosition += GlobalBasis * new Vector3(planeOffset.X, planeOffset.Y, 0f);
			RotateObjectLocal(Vector3.Back, _shakeRoll);
		}
	}
}
