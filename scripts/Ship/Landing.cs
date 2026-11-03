using System;

namespace Vectory.Ship;

// Intensidad de un aterrizaje según la velocidad de caída contra la superficie
public enum LandingLevel
{
	None,
	Medium,
	Strong
}

public struct LandingSettings
{
	public float MediumSpeed;        // m/s de caída desde los que hay sacudida media
	public float StrongSpeed;        // m/s desde los que la sacudida es fuerte
	public float CompressionDepth;   // m que baja la nave en un aterrizaje de intensidad 1
	public float CompressionTime;    // s en bajar y volver
}

// Aterrizaje (spec docs/pista-3d.md, 3.4): nada / media / fuerte y la intensidad de la compresión visual.
// Sin daño: no suma puntos ni cuenta como impacto contra la barrera. Lógica pura.
public static class LandingImpact
{
	public static LandingLevel Level(float fallSpeed, in LandingSettings s)
	{
		if (fallSpeed >= s.StrongSpeed)
			return LandingLevel.Strong;
		if (fallSpeed >= s.MediumSpeed)
			return LandingLevel.Medium;
		return LandingLevel.None;
	}

	// 0 sin efecto; desde la caída media, proporcional a la caída hasta 1 en la fuerte
	public static float Intensity(float fallSpeed, in LandingSettings s)
	{
		if (Level(fallSpeed, s) == LandingLevel.None || s.StrongSpeed <= 0f)
			return 0f;
		return Math.Clamp(fallSpeed / s.StrongSpeed, 0f, 1f);
	}
}

// Compresión visual al aterrizar: la nave baja hasta la profundidad × intensidad y vuelve a su altura
// en CompressionTime, con una curva suave (medio seno, sin saltos). Solo visual. Lógica pura.
public sealed class LandingCompression
{
	private float _time = float.MaxValue;
	private float _intensity;

	public void Reset()
	{
		_time = float.MaxValue;
		_intensity = 0f;
	}

	public void Trigger(float intensity)
	{
		if (intensity <= 0f)
			return;
		_intensity = Math.Clamp(intensity, 0f, 1f);
		_time = 0f;
	}

	public void Update(float dt)
	{
		if (_time < float.MaxValue)
			_time += dt;
	}

	// Metros que la nave está por debajo de su altura (0 sin compresión)
	public float Offset(in LandingSettings s)
	{
		if (s.CompressionTime <= 0f || _time >= s.CompressionTime)
			return 0f;
		return s.CompressionDepth * _intensity * MathF.Sin(MathF.PI * _time / s.CompressionTime);
	}
}
