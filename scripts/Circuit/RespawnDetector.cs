using System;

namespace Vectory.Circuit;

public struct RespawnSettings
{
	public float BarrierOffset;   // desplazamiento lateral de la barrera (m)
	public float StuckSpeed;     // por debajo de esta velocidad (m/s) y en contacto, la nave está atascada
	public float StuckTime;      // segundos seguidos atascada antes de recolocar
	public float OutsideMargin;  // metros más allá de la barrera a partir de los que la nave está fuera de la pista
}

// Detecta cuándo hay que recolocar la nave (spec docs/barreras-y-colisiones.md, 7.1):
//  - Atasco: en contacto con la barrera y por debajo de StuckSpeed durante StuckTime seguidos.
//    Si deja de haber contacto o la velocidad sube, el contador vuelve a 0.
//  - Fuera de la pista: |desplazamiento lateral| > barrera + margen. Recoloca al momento.
// Update devuelve true una sola vez por cada situación (luego el contador empieza de nuevo).
// Quién decide en qué estados de la carrera se recoloca es Main.
// Lógica pura: no depende de Godot.
public sealed class RespawnDetector
{
	// Tolerancia para que la suma de muchos pasos pequeños llegue al tiempo exacto
	private const float TimeEpsilon = 1e-4f;

	private readonly RespawnSettings _settings;
	private float _stuckTimer;
	private bool _wasOutside;

	public RespawnDetector(RespawnSettings settings)
	{
		_settings = settings;
	}

	// Segundos seguidos que lleva atascada
	public float StuckTimer => _stuckTimer;

	public void Reset()
	{
		_stuckTimer = 0f;
		_wasOutside = false;
	}

	public bool Update(float dt, bool contact, float speed, float lateral)
	{
		bool outside = MathF.Abs(lateral) > _settings.BarrierOffset + _settings.OutsideMargin;
		bool justWentOutside = outside && !_wasOutside;
		_wasOutside = outside;
		if (justWentOutside)
		{
			_stuckTimer = 0f;
			return true;
		}

		if (!contact || speed >= _settings.StuckSpeed)
		{
			_stuckTimer = 0f;
			return false;
		}

		_stuckTimer += dt;
		if (_stuckTimer + TimeEpsilon < _settings.StuckTime)
			return false;

		_stuckTimer = 0f;
		return true;
	}
}
