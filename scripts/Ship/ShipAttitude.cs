using System;
using System.Numerics;

namespace Vectory.Ship;

public struct AttitudeSettings
{
	public float GroundSmoothTime;    // s: suavizado de la orientación en el suelo
	public float AirSmoothTime;       // s: en el aire (morro hacia el movimiento, ladeo a 0)
	public float LandingSmoothTime;   // s: al aterrizar, hasta alinearse con la superficie
	public float AirMaxPitch;         // radianes: inclinación máxima del morro en el aire
}

// Orientación de la nave respecto a la pista (spec docs/pista-3d.md, 3.3–3.5), sin el rumbo: cabeceo
// (positivo = morro arriba) y ladeo (positivo = lado derecho arriba) que se aplican sobre el rumbo
// (orden de Godot YXZ: rumbo, cabeceo, ladeo). Solo se suaviza esta inclinación, nunca el rumbo.
// En el suelo, alineada con la superficie; en el aire, el morro hacia la dirección del movimiento (con
// tope) y el ladeo a 0 (nivelada con el mundo); al aterrizar vuelve a la superficie con su propio
// suavizado. La inclinación visual por giro va aparte (ShipBank). Lógica pura.
public sealed class ShipAttitude
{
	public float Pitch { get; private set; }
	public float Roll { get; private set; }

	private float _landingTimer;

	public void Reset(float pitch = 0f, float roll = 0f)
	{
		Pitch = pitch;
		Roll = roll;
		_landingTimer = 0f;
	}

	// Cabeceo y ladeo que dejan el "arriba" de la nave en la normal de la superficie, con el morro hacia el rumbo
	public static void SurfaceAngles(Vector3 normal, float yaw, out float pitch, out float roll)
	{
		var forward = new Vector3(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw));
		var right = new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
		float nForward = Vector3.Dot(normal, forward);
		float nRight = Vector3.Dot(normal, right);
		float nUp = normal.Y;

		pitch = MathF.Atan2(-nForward, nUp);
		roll = MathF.Atan2(-nRight, MathF.Sqrt(nUp * nUp + nForward * nForward));
	}

	// Cabeceo hacia la dirección del movimiento, con tope
	public static float AirPitch(float verticalSpeed, float horizontalSpeed, float maxPitch)
	{
		float max = MathF.Max(0f, maxPitch);
		return Math.Clamp(MathF.Atan2(verticalSpeed, MathF.Max(horizontalSpeed, 1e-3f)), -max, max);
	}

	public void Update(bool grounded, bool justLanded, Vector3 normal, float yaw, float verticalSpeed, float horizontalSpeed,
		in AttitudeSettings s, float dt)
	{
		if (justLanded)
			_landingTimer = 3f * s.LandingSmoothTime;

		float targetPitch;
		float targetRoll;
		float smoothTime;
		if (grounded)
		{
			SurfaceAngles(normal, yaw, out targetPitch, out targetRoll);
			smoothTime = _landingTimer > 0f ? s.LandingSmoothTime : s.GroundSmoothTime;
			_landingTimer = MathF.Max(0f, _landingTimer - dt);
		}
		else
		{
			targetPitch = AirPitch(verticalSpeed, horizontalSpeed, s.AirMaxPitch);
			targetRoll = 0f;
			smoothTime = s.AirSmoothTime;
			_landingTimer = 0f;
		}

		float k = smoothTime > 0f ? 1f - MathF.Exp(-dt / smoothTime) : 1f;
		Pitch += (targetPitch - Pitch) * k;
		Roll += (targetRoll - Roll) * k;
	}
}
