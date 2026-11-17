using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Effects;

// Un destello en el punto de contacto: dónde, cómo se mueve, edad, duración y tamaño de su nivel, onda (solo el
// fuerte), intensidad aleatoria del roce y ángulos de sus rayos
public sealed class CrashFlash
{
	public Vector3 Position;
	public Vector3 Velocity;
	public float Age;
	public float Duration;
	public float Size;
	public bool Ring;
	public float Flicker = 1f;
	public float[] Rays;
}

// Destellos en el punto de contacto (spec docs/efectos-1-choques.md, 5): alta por nivel, parpadeo del roce, intensidad,
// brillo, rayos y onda del fuerte. Como mucho MaxFlashes: con uno más, fuera el más viejo. No cambian con PARTICLES.
// Lógica pura.
public sealed class CrashFlashes
{
	private readonly CollisionFxSettings _s;
	private readonly Random _random;
	private readonly List<CrashFlash> _flashes = new();

	public CrashFlashes(CollisionFxSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	public IReadOnlyList<CrashFlash> All => _flashes;

	public FlashLevel LevelOf(ImpactLevel level) => level switch
	{
		ImpactLevel.Graze => _s.FlashGraze,
		ImpactLevel.Medium => _s.FlashMedium,
		_ => _s.FlashStrong
	};

	// El destello de un contacto: el golpe saca el de su nivel; cada paso de roce, al azar GrazeFlashRate veces por
	// segundo, uno pequeño con intensidad aleatoria (parpadea). Contra la barrera queda quieto; entre naves, con ellas.
	public void Emit(in CrashEvent e, float dt)
	{
		if (e.IsStart)
		{
			Add(e, e.Level, 1f);
			return;
		}
		if (e.Kind == CrashKind.Ships && e.Graze != Contact.GrazeKind.Side)
			return;
		if (_random.NextSingle() < dt * _s.GrazeFlashRate)
			Add(e, ImpactLevel.Graze, _s.GrazeFlashMin + (_s.GrazeFlashMax - _s.GrazeFlashMin) * _random.NextSingle());
	}

	public CrashFlash Add(in CrashEvent e, ImpactLevel level, float flicker)
	{
		FlashLevel l = LevelOf(level);
		var rays = new float[_s.RayCount];
		for (int i = 0; i < rays.Length; i++)
			rays[i] = _random.NextSingle() * MathF.Tau;
		var flash = new CrashFlash
		{
			Position = e.Point,
			Velocity = e.Kind == CrashKind.Barrier ? Vector3.Zero : e.Velocity * _s.ShipFlashInherit,
			Duration = l.Duration,
			Size = l.Size,
			Ring = level == ImpactLevel.Strong,
			Flicker = flicker,
			Rays = rays
		};
		_flashes.Add(flash);
		while (_flashes.Count > Math.Max(0, _s.MaxFlashes))
			_flashes.RemoveAt(0);
		return flash;
	}

	public void Clear() => _flashes.Clear();

	// Con dt 0 (pausa) no cambia nada
	public void Update(float dt)
	{
		if (dt <= 0f)
			return;
		for (int i = _flashes.Count - 1; i >= 0; i--)
		{
			CrashFlash f = _flashes[i];
			f.Age += dt;
			f.Position += f.Velocity * dt;
			float end = f.Ring ? MathF.Max(f.Duration, _s.RingDuration) : f.Duration;
			if (f.Age > end)
				_flashes.RemoveAt(i);
		}
	}

	private static float U(CrashFlash f) => f.Duration > 0f ? Math.Clamp(f.Age / f.Duration, 0f, 1f) : 1f;

	// (1 − u)² × la intensidad del roce
	public static float Intensity(CrashFlash f)
	{
		float u = U(f);
		return (1f - u) * (1f - u) * f.Flicker;
	}

	// Diámetro del brillo: se abre un poco mientras se apaga
	public static float GlowSize(CrashFlash f) => f.Size * (0.7f + 0.5f * U(f));

	// Rayos: desde RayStart hasta esto
	public float RayEnd(CrashFlash f) => f.Size * _s.RayLength * (1f - 0.4f * U(f));

	// Onda del fuerte: radio e intensidad (0 si no tiene o ya terminó)
	public float RingRadius(CrashFlash f)
	{
		float v = Math.Clamp(f.Age / _s.RingDuration, 0f, 1f);
		return _s.RingStartRadius + (_s.RingEndRadius - _s.RingStartRadius) * (1f - (1f - v) * (1f - v));
	}

	public float RingStrength(CrashFlash f)
	{
		if (!f.Ring || f.Age >= _s.RingDuration)
			return 0f;
		return _s.RingIntensity * (1f - f.Age / _s.RingDuration);
	}
}
