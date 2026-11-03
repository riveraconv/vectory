using System;
using System.Collections.Generic;

namespace Vectory.Circuit;

// Animaciones de los elementos del circuito (spec docs/elementos-circuito.md, 11): tiempo → brillo / mezcla con blanco.
// Lógica pura: los nodos solo pasan el resultado a sus materiales.

// Destello de un pórtico (meta y checkpoints): al dispararlo sube al pico y vuelve al brillo base en
// "duration" s con caída cuadrática (rápida al principio y suave al final). Volver a dispararlo lo reinicia.
public sealed class GateFlash
{
	private readonly float _duration;
	private readonly float _baseBrightness;
	private readonly float _peakBrightness;
	private readonly float _peakWhite;
	private float _elapsed;
	private bool _active;

	public GateFlash(float duration, float baseBrightness, float peakBrightness, float peakWhite)
	{
		_duration = MathF.Max(0.001f, duration);
		_baseBrightness = baseBrightness;
		_peakBrightness = peakBrightness;
		_peakWhite = peakWhite;
	}

	// 1 en el disparo, 0 al terminar
	public float Level
	{
		get
		{
			if (!_active)
				return 0f;
			float k = 1f - _elapsed / _duration;
			return k > 0f ? k * k : 0f;
		}
	}

	public float Brightness => _baseBrightness + (_peakBrightness - _baseBrightness) * Level;

	// Cuánto se mezcla el color con blanco (0 = color normal)
	public float WhiteMix => _peakWhite * Level;

	public void Trigger()
	{
		_active = true;
		_elapsed = 0f;
	}

	public void Update(float delta)
	{
		if (!_active || delta <= 0f)
			return;
		_elapsed += delta;
		if (_elapsed >= _duration)
			_active = false;
	}
}

// Pulsos del túnel: "count" pulsos repartidos a la misma distancia que recorren el túnel en bucle, en el
// sentido de la marcha, a "speed" m/s. Cada anillo brilla según su distancia al pulso más cercano.
public static class TunnelPulse
{
	public const float DefaultWidth = 7f;

	// Posición de cada pulso (m desde la entrada del túnel) en el instante "time"
	public static float[] Positions(float time, float tunnelLength, float speed, int count)
	{
		count = Math.Max(0, count);
		var positions = new float[count];
		if (tunnelLength <= 0f)
			return positions;
		for (int k = 0; k < count; k++)
			positions[k] = PosMod(time * speed + k * tunnelLength / count, tunnelLength);
		return positions;
	}

	// g = exp(−(x / width)²), con x la distancia del anillo al pulso más cercano
	public static float Glow(float ringDistance, IReadOnlyList<float> pulses, float width = DefaultWidth)
	{
		if (pulses == null || pulses.Count == 0 || width <= 0f)
			return 0f;
		float best = float.MaxValue;
		foreach (float p in pulses)
			best = MathF.Min(best, MathF.Abs(ringDistance - p));
		float x = best / width;
		return MathF.Exp(-x * x);
	}

	private static float PosMod(float x, float m)
	{
		float r = x % m;
		return r < 0f ? r + m : r;
	}
}

// Ola de los chevrons: se repite "frequency" veces por segundo con un desfase de un ciclo cada "cycleLength" m,
// así avanza en el sentido de la marcha. Pico gaussiano estrecho (0 lejos de la ola, 1 en su centro).
public static class ChevronWave
{
	public const float BaseBrightness = 0.55f;
	public const float PeakBrightness = 2.15f;
	public const float PeakWhite = 0.4f;

	public static float Glow(float time, float distanceFromStart, float frequency, float cycleLength)
	{
		float phase = time * frequency - distanceFromStart / MathF.Max(0.001f, cycleLength);
		phase -= MathF.Floor(phase);
		float x = (phase - 0.1f) / 0.09f;
		return MathF.Exp(-x * x);
	}

	public static float Brightness(float glow) => BaseBrightness + (PeakBrightness - BaseBrightness) * glow;
}

// Latido suave: brillo entre min y max con una frecuencia en Hz
public static class Heartbeat
{
	public static float Brightness(float time, float frequency, float min, float max)
	{
		float k = 0.5f + 0.5f * MathF.Sin(2f * MathF.PI * frequency * time);
		return min + (max - min) * k;
	}
}

// Estado del semáforo de la meta a partir de la secuencia de salida: luces encendidas y destello cian del GO
// (1 en la salida, 0 a los "flashDuration" s, con caída cuadrática). Después todo queda apagado.
public readonly struct StartLightsState
{
	public readonly int LightsOn;
	public readonly float GoFlash;

	public StartLightsState(int lightsOn, float goFlash)
	{
		LightsOn = lightsOn;
		GoFlash = goFlash;
	}

	public static StartLightsState From(StartSequence sequence, float flashDuration)
	{
		if (sequence == null)
			return new StartLightsState(0, 0f);
		if (!sequence.HasStarted)
			return new StartLightsState(sequence.LightsOn, 0f);

		float k = 1f - (float)(sequence.Elapsed - sequence.GoTime) / MathF.Max(0.001f, flashDuration);
		return new StartLightsState(0, k > 0f ? k * k : 0f);
	}
}

// Marcas de parrilla: opacidad 1 hasta el GO y 0 a los "fadeTime" s; ya no vuelven
public static class GridMarksFade
{
	// timeSinceGo: null antes de la salida
	public static float Opacity(double? timeSinceGo, float fadeTime)
	{
		if (!timeSinceGo.HasValue || timeSinceGo.Value <= 0)
			return 1f;
		if (fadeTime <= 0f)
			return 0f;
		return Math.Clamp(1f - (float)timeSinceGo.Value / fadeTime, 0f, 1f);
	}

	// Tiempo desde el GO de una secuencia de salida (null si no hay o aún no se ha dado)
	public static double? TimeSinceGo(StartSequence sequence)
	{
		if (sequence == null || !sequence.HasStarted)
			return null;
		return sequence.Elapsed - sequence.GoTime;
	}
}

// Texto que se ve un tiempo: aparece en "fadeIn" s y se desvanece en los últimos "fadeOut" s
public static class TimedDisplay
{
	public static float Opacity(float elapsed, float visibleTime, float fadeIn, float fadeOut)
	{
		if (elapsed < 0f || elapsed >= visibleTime)
			return 0f;
		float a = fadeIn > 0f ? MathF.Min(1f, elapsed / fadeIn) : 1f;
		float remaining = visibleTime - elapsed;
		if (fadeOut > 0f && remaining < fadeOut)
			a = MathF.Min(a, remaining / fadeOut);
		return a;
	}
}
