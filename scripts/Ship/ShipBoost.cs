using System;

namespace Vectory.Ship;

// Fase del impulso del boost
public enum BoostPhase
{
	None,   // sin impulso
	Ramp,   // subida: se va sumando el extra de velocidad
	Hold    // mantenimiento: la nave conserva la velocidad alcanzada
}

// Boost por cargas: cargas, recarga por tiempo, cooldown e impulso (subida + mantenimiento).
// Lógica pura: no depende de Godot, así que se puede probar sin ejecutar el juego.
//
// Cada paso de física: TryActivate (si se ha pulsado boost), CancelImpulse (si se frena)
// y Update, que devuelve cuánta velocidad (m/s) hay que sumar a la nave en ese paso.
//
// Recarga: el contador corre mientras haya menos cargas que el máximo y no se reinicia
// al gastar una; al llegar al máximo se para y se pone a 0.
public class ShipBoost
{
	public int Charges { get; private set; }

	// Segundos acumulados hacia la siguiente carga
	public float RechargeProgress { get; private set; }

	// Segundos que faltan para poder volver a usar el boost
	public float CooldownRemaining { get; private set; }

	public BoostPhase Phase { get; private set; }

	// Velocidad (m/s) de la subida en curso que todavía falta por sumar
	public float ImpulseRemaining { get; private set; }

	// Segundos que quedan de mantenimiento
	public float HoldRemaining { get; private set; }

	public bool IsImpulseActive => Phase != BoostPhase.None;

	public bool IsCooldownActive => CooldownRemaining > 0f;

	public bool CanActivate => Charges >= 1 && !IsCooldownActive;

	// Velocidad (m/s) que se suma por segundo durante la subida
	private float _impulseRate;

	// Vuelve al estado inicial (al empezar o reiniciar la carrera)
	public void Reset(in BoostTuning t)
	{
		Charges = Math.Clamp(t.InitialCharges, 0, Math.Max(0, t.MaxCharges));
		RechargeProgress = 0f;
		CooldownRemaining = 0f;
		EndImpulse();
	}

	// Gasta una carga y arranca la subida y el cooldown. Si no se puede, no hace nada.
	public bool TryActivate(in BoostTuning t)
	{
		if (!CanActivate)
			return false;

		Charges--;
		CooldownRemaining = t.Cooldown;
		Phase = BoostPhase.Ramp;
		ImpulseRemaining = MathF.Max(0f, t.SpeedGain);
		HoldRemaining = 0f;
		_impulseRate = t.RampDuration > 0f ? ImpulseRemaining / t.RampDuration : float.PositiveInfinity;
		return true;
	}

	// Corta el impulso en curso (subida o mantenimiento): se conserva lo ya sumado
	// y la carga no se devuelve
	public void CancelImpulse()
	{
		EndImpulse();
	}

	// Avanza dt segundos. Devuelve la velocidad (m/s) que hay que sumar a la nave en este paso.
	public float Update(in BoostTuning t, float dt)
	{
		if (dt <= 0f)
			return 0f;

		CooldownRemaining = MathF.Max(0f, CooldownRemaining - dt);
		Recharge(t, dt);

		switch (Phase)
		{
			case BoostPhase.Ramp:
				float gain = MathF.Min(_impulseRate * dt, ImpulseRemaining);
				ImpulseRemaining -= gain;
				if (ImpulseRemaining <= 0f)
					StartHold(t);
				return gain;

			case BoostPhase.Hold:
				HoldRemaining -= dt;
				if (HoldRemaining <= 0f)
					EndImpulse();
				return 0f;

			default:
				return 0f;
		}
	}

	// Al acabar la subida empieza el mantenimiento (si dura algo)
	private void StartHold(in BoostTuning t)
	{
		ImpulseRemaining = 0f;
		if (t.HoldDuration > 0f)
		{
			Phase = BoostPhase.Hold;
			HoldRemaining = t.HoldDuration;
		}
		else
		{
			EndImpulse();
		}
	}

	private void EndImpulse()
	{
		Phase = BoostPhase.None;
		ImpulseRemaining = 0f;
		HoldRemaining = 0f;
		_impulseRate = 0f;
	}

	private void Recharge(in BoostTuning t, float dt)
	{
		if (Charges >= t.MaxCharges)
		{
			RechargeProgress = 0f;
			return;
		}

		if (t.RechargeTime <= 0f)
		{
			Charges = t.MaxCharges;
			RechargeProgress = 0f;
			return;
		}

		RechargeProgress += dt;
		while (RechargeProgress >= t.RechargeTime && Charges < t.MaxCharges)
		{
			RechargeProgress -= t.RechargeTime;
			Charges++;
		}

		if (Charges >= t.MaxCharges)
			RechargeProgress = 0f;
	}
}
