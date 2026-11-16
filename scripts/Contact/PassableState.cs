using System;

namespace Vectory.Contact;

// Si una nave se puede atravesar (spec docs/rivales-3-contacto.md, 5): no choca con otras naves (con la barrera, sí).
// Lo es desde que termina la carrera y, tras una recolocación, durante RespawnTime; si al acabarse sigue solapada con
// otra nave, lo sigue siendo hasta que deje de estarlo. Lógica pura: no depende de Godot.
public sealed class PassableState
{
	public const float DefaultRespawnTime = 2f;

	private float _timer;
	private bool _held;

	public float RespawnTime { get; set; } = DefaultRespawnTime;

	// Ha cruzado la meta en la última vuelta
	public bool Finished { get; set; }

	// Atravesable por la recolocación (la nave parpadea)
	public bool FromRespawn => _timer > 0f || _held;

	public bool IsPassable => Finished || FromRespawn;

	public void Respawned()
	{
		_timer = RespawnTime;
		_held = false;
	}

	// Un paso: overlapping, si en este paso se solapa con alguna otra nave
	public void Update(float dt, bool overlapping)
	{
		if (_timer > 0f)
		{
			_timer = MathF.Max(0f, _timer - dt);
			if (_timer <= 0f)
				_held = overlapping;
		}
		else if (_held && !overlapping)
		{
			_held = false;
		}
	}
}

// Parpadeo de la nave atravesable tras recolocarla: baja a Opacity y vuelve, Frequency veces por segundo
public readonly record struct RespawnBlink(float Opacity, float Frequency)
{
	public const float DefaultOpacity = 0.4f;
	public const float DefaultFrequency = 4f;

	public static RespawnBlink Default => new(DefaultOpacity, DefaultFrequency);

	// Opacidad en el tiempo time (s): la primera mitad de cada ciclo, baja
	public float At(float time)
	{
		if (Frequency <= 0f)
			return Opacity;
		float phase = time * Frequency - MathF.Floor(time * Frequency);
		return phase < 0.5f ? Opacity : 1f;
	}
}
