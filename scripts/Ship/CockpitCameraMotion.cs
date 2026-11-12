using System;
using System.Numerics;

namespace Vectory.Ship;

// Parámetros del movimiento de la cámara de la cabina 3D (spec docs/cabinas-3d-1-vector.md, 4.4). A 0, queda quieta.
public readonly record struct CockpitCameraSettings(float Sway, float VibrationPosition, float VibrationRotation, float CounterRoll, float Push);

// Lo que ya calcula el juego para la cámara, sin sus metros: los efectos de la cámara del mundo llevan su propia amplitud
public readonly record struct CockpitCameraInput(
	float Steer,                // ShipDynamics.Steer: positivo a la izquierda
	float SpeedFraction,        // f: velocidad / tope con boost
	float Bank,                 // CockpitBank.Angle (rad), lo que se ladea la cámara del mundo sin el peralte de la pista
	float VibrationIntensity,   // CameraVibration.Intensity
	float BoostAmount,          // BoostCameraEffect.Eased
	float Time);                // CameraVibration.Time (la pausa lo congela)

// Posición (m) y giro (rad, orden YXZ) de la cámara, con el origen en el ojo del piloto
public readonly record struct CockpitCameraPose(Vector3 Position, Vector3 Rotation);

// Movimiento propio de la cámara de cabina, como el prototipo: balanceo lateral, vibración, contragiro y empuje. Lógica pura.
public static class CockpitCameraMotion
{
	// Constantes del prototipo: cuánto vibra en la punta y cuánto añade el boost
	public const float VibrationAtTop = 0.0025f;
	public const float VibrationWithBoost = 0.003f;

	// El prototipo tiene el giro positivo a la derecha y el juego a la izquierda: por eso cambia de signo
	public static CockpitCameraPose Compute(in CockpitCameraInput input, in CockpitCameraSettings s)
	{
		float vibration = input.VibrationIntensity * VibrationAtTop + input.BoostAmount * VibrationWithBoost;
		float direction = -input.Steer;
		float t = input.Time;

		var position = new Vector3(
			direction * s.Sway * (0.3f + 0.7f * input.SpeedFraction) + Noise(t, 7f) * vibration * s.VibrationPosition,
			Noise(t, 9f) * vibration * s.VibrationPosition,
			s.Push * input.BoostAmount);

		var rotation = new Vector3(
			Noise(t, 4f) * vibration * s.VibrationRotation,
			Noise(t, 1f) * vibration * s.VibrationRotation,
			-input.Bank * s.CounterRoll);

		return new CockpitCameraPose(position, rotation);
	}

	// La inclinación de la vista (rad; negativa = hacia abajo) sumada al giro: la de la cámara del mundo, para que el morro
	// case con la pista (spec docs/cabinas-3d-2-blade.md, 4.5)
	public static CockpitCameraPose Pitched(in CockpitCameraPose pose, float pitch) =>
		pose with { Rotation = pose.Rotation with { X = pose.Rotation.X + pitch } };

	// El ruido del prototipo: dos senos, entre -1 y 1
	public static float Noise(float t, float a) => MathF.Sin(t * 37.1f + a) * 0.6f + MathF.Sin(t * 61.7f + a * 2.3f) * 0.4f;
}
