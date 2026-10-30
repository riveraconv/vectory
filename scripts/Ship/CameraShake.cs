using System;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Ship;

// Sacudida de un nivel de impacto: desplazamiento máximo (m), giro máximo (rad) y duración (s)
public struct ShakeLevelSettings
{
	public float Amplitude;
	public float Roll;
	public float Duration;
}

public struct CameraShakeSettings
{
	public ShakeLevelSettings Medium;
	public ShakeLevelSettings Strong;
	public float Frequency;   // Hz de la vibración

	// Valores de la spec (tabla de la sección 6, cámara exterior)
	public static CameraShakeSettings Default => new()
	{
		Medium = new ShakeLevelSettings { Amplitude = 0.06f, Roll = 0.8f * MathF.PI / 180f, Duration = 0.25f },
		Strong = new ShakeLevelSettings { Amplitude = 0.15f, Roll = 2f * MathF.PI / 180f, Duration = 0.4f },
		Frequency = 25f
	};
}

// Sacudida de cámara tras un impacto medio o fuerte (spec docs/barreras-y-colisiones.md, 6).
// Vibración rápida (senoidales a ~Frequency Hz) que se amortigua hasta 0 en su duración.
// El roce no sacude. Un impacto nuevo sustituye a la sacudida en curso solo si es más fuerte.
// Da los valores de la cámara exterior; quien la aplica escala para la cabina.
// Lógica pura: no depende de Godot.
public sealed class CameraShake
{
	private CameraShakeSettings _settings;
	private ShakeLevelSettings _current;
	private ImpactLevel _level;
	private float _time;

	public CameraShake(CameraShakeSettings settings)
	{
		_settings = settings;
	}

	public CameraShakeSettings Settings
	{
		get => _settings;
		set => _settings = value;
	}

	public bool IsActive { get; private set; }

	// Desplazamiento máximo en este momento (envolvente de la vibración), en m
	public float Amplitude => IsActive ? _current.Amplitude * Envelope() : 0f;

	// Desplazamiento de la cámara en su plano (X derecha, Y arriba), en m
	public Vector2 Offset { get; private set; }

	// Giro sobre el eje de la vista, en rad
	public float Roll { get; private set; }

	public void Trigger(ImpactLevel level)
	{
		if (level == ImpactLevel.Graze)
			return;
		if (IsActive && level <= _level)
			return;

		_level = level;
		_current = level == ImpactLevel.Strong ? _settings.Strong : _settings.Medium;
		_time = 0f;
		IsActive = _current.Duration > 0f;
		Sample();
	}

	public void Update(float dt)
	{
		if (!IsActive)
			return;

		_time += dt;
		if (_time >= _current.Duration)
		{
			IsActive = false;
			Offset = Vector2.Zero;
			Roll = 0f;
			return;
		}
		Sample();
	}

	// Caída suave: cuadrática de 1 a 0
	private float Envelope()
	{
		float k = 1f - Math.Clamp(_time / _current.Duration, 0f, 1f);
		return k * k;
	}

	private void Sample()
	{
		float env = Envelope();
		float w = 2f * MathF.PI * _settings.Frequency * _time;
		// Frecuencias y fases distintas por eje para que no vibre en una sola dirección
		Offset = new Vector2(MathF.Sin(w + 0.9f), MathF.Sin(1.3f * w + 2.1f)) * (_current.Amplitude * env);
		Roll = MathF.Sin(0.8f * w + 0.4f) * _current.Roll * env;
	}
}
