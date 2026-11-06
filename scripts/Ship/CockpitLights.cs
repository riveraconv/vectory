using System;

namespace Vectory.Ship;

// Un LED de las shift lights: su color (0xRRGGBB) y su intensidad
public readonly record struct ShiftLight(uint Color, float Intensity);

// Shift lights de la cabina 3D (spec docs/cabinas-3d-1-vector.md, 6.4). v es la velocidad que enseña el HUD y top la
// punta base de la nave. Lógica pura.
public static class ShiftLights
{
	public const int Count = Cockpit3DPieces.ShiftLightCount;
	public const float LitIntensity = 3.5f;
	public const float FlashIntensity = 4f;
	public const float StartFraction = 0.55f;
	public const float RangeFraction = 0.45f;
	public const float FlashPeriod = 0.2f;   // 5 Hz: encendidos la primera mitad
	private const float RoundingMargin = 0.0001f;

	public static int LitCount(float speed, float top)
	{
		float fraction = top > 0f ? Math.Clamp((speed - StartFraction * top) / (RangeFraction * top), 0f, 1f) : 0f;
		// Las mitades, hacia arriba, como en JavaScript. El margen es por los float: 7,5 puede llegar como 7,49998
		return (int)MathF.Round(Count * fraction + RoundingMargin, MidpointRounding.AwayFromZero);
	}

	// Color de cada LED, de izquierda a derecha: 5 verdes, 5 rojos y 5 azules
	public static uint BaseColor(int index) => index < 5 ? RacingPalette.ShiftGreen : index < 10 ? RacingPalette.ShiftRed : RacingPalette.ShiftBlue;

	public static ShiftLight Light(int index, float speed, float top, bool overTop, float time)
	{
		if (overTop)
			return new ShiftLight(RacingPalette.ShiftBlue, FlashOn(time) ? FlashIntensity : 0f);
		return new ShiftLight(BaseColor(index), index < LitCount(speed, top) ? LitIntensity : 0f);
	}

	private static bool FlashOn(float time)
	{
		float phase = time % FlashPeriod;
		return phase < FlashPeriod * 0.5f;
	}
}

// Intensidad de cada luz de boost y daño: multiplica el valor del diseño. A 0, se apaga.
public readonly record struct CockpitLightIntensities(float BoostFront, float BoostRear, float Damage);

// Las tres luces de boost y daño, en las unidades del prototipo
public readonly record struct CockpitLightValues(float BoostFront, float BoostRear, float Damage);

// Luz de boost y daño de la cabina 3D (spec, 6.6). El tinte de la luz va más rápido que el de los bordes de pantalla,
// como en el prototipo. El daño sale de CockpitHudState, así que en TIME TRIAL no hay. Lógica pura.
public sealed class CockpitLightMix
{
	public const float BoostFrontFactor = 3.2f;
	public const float BoostRearFactor = 2f;
	public const float DamageFactor = 3f;
	public const float TintRate = 10f;   // por segundo: en unos 0,1 s

	public float BoostTint { get; private set; }

	public void Update(bool boostActive, bool overTop, float dt)
	{
		float target = boostActive ? CockpitHudState.BoostTintActive : overTop ? CockpitHudState.BoostTintOverTop : 0f;
		BoostTint += (target - BoostTint) * MathF.Min(1f, dt * TintRate);
	}

	public CockpitLightValues Values(float damageTint, in CockpitLightIntensities intensity)
	{
		return new CockpitLightValues(
			BoostTint * BoostFrontFactor * intensity.BoostFront,
			BoostTint * BoostRearFactor * intensity.BoostRear,
			damageTint * DamageFactor * intensity.Damage);
	}
}
