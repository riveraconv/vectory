using System;

namespace Vectory.Ship;

// Intensidades de las setas del boost y de las balizas del larguero de HAMMER (spec docs/cabinas-3d-3-hammer.md, 6.5 y 7).
// Las setas llenas, justo por encima del umbral del glow: halo suave. Llena y bloqueada, ajustadas midiendo el halo
// contra el prototipo: con su 1,7 y su 2,0, el glow de Godot daba el doble de halo (bajan en la misma proporción).
public readonly record struct IndustrialLampSettings(
	float SetaFull,
	float SetaCooldown,
	float SetaLocked,
	float SetaEmpty,
	float BoostBeaconOn,
	float BoostBeaconPulse,
	float DamageBeaconOn,
	float BeaconOff)
{
	public static readonly IndustrialLampSettings Default = new(1.25f, 0.6f, 1.5f, 0.25f, 4f, 1.5f, 5f, 0.05f);
}

// Color (0xRRGGBB) e intensidad de emisión de una luz de estado
public readonly record struct IndustrialLamp(uint Color, float Intensity);

// Setas y balizas de HAMMER (spec 6.5), según el estado del HUD. No titilan con el parpadeo de daño. Lógica pura.
public static class IndustrialLamps
{
	public const float BoostBeaconRate = 20f;   // rad/s: la ámbar late rápido con el boost

	// Una seta por carga, de izquierda a derecha, con la prioridad de las cargas: bloqueada → cooldown → llena → vacía
	public static IndustrialLamp Seta(int index, int charges, bool cooldown, int damageLevel, bool damageEnabled,
		in IndustrialLampSettings s)
	{
		bool has = index < charges;
		bool blocked = damageEnabled && damageLevel >= DamageModel.MaxLevel;
		return has && blocked ? new IndustrialLamp(IndustrialPalette.Crit, s.SetaLocked)
			: has && cooldown ? new IndustrialLamp(IndustrialPalette.Over, s.SetaCooldown)
			: has ? new IndustrialLamp(IndustrialPalette.Main, s.SetaFull)
			: new IndustrialLamp(IndustrialPalette.LampOff, s.SetaEmpty);
	}

	// Ámbar: con el boost activo, encendida + pulso × sen(20 t); si no, apagada
	public static float BoostBeacon(bool boostActive, float time, in IndustrialLampSettings s) =>
		boostActive ? s.BoostBeaconOn + s.BoostBeaconPulse * MathF.Sin(BoostBeaconRate * time) : s.BeaconOff;

	// Roja: fija con daño 2, con el parpadeo del daño con daño 3; apagada con 0–1 y en TIME TRIAL
	public static float DamageBeacon(int damageLevel, bool damageEnabled, bool blinkOn, in IndustrialLampSettings s)
	{
		bool on = damageEnabled && (damageLevel == 2 || damageLevel >= DamageModel.MaxLevel && blinkOn);
		return on ? s.DamageBeaconOn : s.BeaconOff;
	}
}
