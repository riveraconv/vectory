using System;

namespace Vectory.Ship;

// Corrección temporal tras un impacto fuerte (spec docs/barreras-y-colisiones.md, 4.4): durante
// Duration segundos el giro y la aceleración funcionan a MinFactor y vuelven a 1 en línea recta.
// Otro impacto fuerte la reinicia (no se suman). El freno no se ve afectado (lo decide ShipDynamics).
// Lógica pura: no depende de Godot.
public sealed class ImpactRecovery
{
	private float _elapsed;

	public float Duration { get; set; } = 1f;
	public float MinFactor { get; set; } = 0.5f;

	public bool IsActive { get; private set; }

	// Factor de control: MinFactor justo tras el impacto ... 1 al terminar (o sin corrección)
	public float Factor
	{
		get
		{
			if (!IsActive || Duration <= 0f)
				return 1f;
			float t = Math.Clamp(_elapsed / Duration, 0f, 1f);
			return MinFactor + (1f - MinFactor) * t;
		}
	}

	public void Start()
	{
		IsActive = true;
		_elapsed = 0f;
	}

	public void Cancel()
	{
		IsActive = false;
		_elapsed = 0f;
	}

	public void Update(float dt)
	{
		if (!IsActive)
			return;

		_elapsed += dt;
		if (_elapsed >= Duration)
			Cancel();
	}
}
