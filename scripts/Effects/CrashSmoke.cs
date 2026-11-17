using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Effects;

// Una bocanada de humo: Age negativa mientras espera su retraso
public struct SmokePuff
{
	public Vector3 Position;
	public Vector3 Velocity;
	public float Age;
	public float Life;
	public float StartRadius;
	public float EndRadius;
}

// Humo de los golpes medios y fuertes (spec docs/efectos-1-choques.md, 6): bocanadas con retraso aleatorio, radio que
// crece con la raíz de la edad, opacidad que entra en SmokeFadeIn y se apaga. Sube en vertical del mundo y se frena por
// separado en horizontal y en vertical. Capacity: máximo con el PARTICLES de ahora. Lógica pura.
public sealed class CrashSmoke
{
	private readonly CollisionFxSettings _s;
	private readonly Random _random;
	private readonly List<SmokePuff> _puffs = new();

	public CrashSmoke(CollisionFxSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
		Capacity = settings.MaxPuffs;
	}

	public int Count => _puffs.Count;
	public int Capacity { get; private set; }
	public SmokePuff this[int index] => _puffs[index];

	public void SetCapacity(int capacity)
	{
		Capacity = Math.Max(0, capacity);
		if (_puffs.Count > Capacity)
			_puffs.RemoveRange(Capacity, _puffs.Count - Capacity);
	}

	public SmokeLevel LevelOf(ImpactLevel level) => level switch
	{
		ImpactLevel.Medium => _s.SmokeMedium,
		ImpactLevel.Strong => _s.SmokeStrong,
		_ => null
	};

	// Cuántas bocanadas saca un nivel con PARTICLES (con sus mínimos; el roce, ninguna)
	public int CountFor(ImpactLevel level, float quality)
	{
		SmokeLevel l = LevelOf(level);
		return l == null ? 0 : Math.Max(l.Minimum, SparkEmitter.Scaled(l.Count, quality));
	}

	// Las bocanadas de un golpe (solo el inicio de un medio o un fuerte). Devuelve cuántas han entrado.
	public int Emit(in CrashEvent e, float quality)
	{
		SmokeLevel l = e.IsStart ? LevelOf(e.Level) : null;
		if (l == null)
			return 0;
		int count = CountFor(e.Level, quality);
		bool barrier = e.Kind == CrashKind.Barrier;
		float inherit = barrier ? _s.SmokeBarrierInherit : _s.SmokeShipInherit;
		Vector3 side = e.Normal;
		Vector3 along = e.Back;
		int added = 0;
		for (int i = 0; i < count; i++)
		{
			if (_puffs.Count >= Capacity)
				break;
			Vector3 velocity = e.Velocity * inherit
				+ side * Range(-_s.SmokeDrift, _s.SmokeDrift)
				+ along * Range(-_s.SmokeDrift, _s.SmokeDrift)
				+ (barrier ? e.Normal * _s.SmokeInward : Vector3.Zero);
			velocity.Y += Range(_s.SmokeRiseMin, _s.SmokeRiseMax);
			_puffs.Add(new SmokePuff
			{
				Position = e.Point + side * Range(-_s.SmokeJitterSide, _s.SmokeJitterSide) + e.Up * Range(0f, _s.SmokeJitterUp)
					+ along * Range(-_s.SmokeJitterAlong, _s.SmokeJitterAlong),
				Velocity = velocity,
				Age = -Range(0f, l.Window),
				Life = Range(l.LifeMin, l.LifeMax),
				StartRadius = l.StartRadius,
				EndRadius = l.EndRadius * Range(l.EndScaleMin, l.EndScaleMax)
			});
			added++;
		}
		return added;
	}

	public void Clear() => _puffs.Clear();

	// Con dt 0 (pausa) no cambia nada
	public void Update(float dt)
	{
		if (dt <= 0f)
			return;
		float dampH = MathF.Max(0f, 1f - _s.SmokeDampHorizontal * dt);
		float dampV = MathF.Max(0f, 1f - _s.SmokeDampVertical * dt);
		for (int i = _puffs.Count - 1; i >= 0; i--)
		{
			SmokePuff p = _puffs[i];
			p.Age += dt;
			if (p.Age < 0f)
			{
				_puffs[i] = p;
				continue;
			}
			p.Velocity = new Vector3(p.Velocity.X * dampH, p.Velocity.Y * dampV, p.Velocity.Z * dampH);
			p.Position += p.Velocity * dt;
			if (p.Age >= p.Life)
			{
				_puffs.RemoveAt(i);
				continue;
			}
			_puffs[i] = p;
		}
	}

	public static bool IsVisible(in SmokePuff p) => p.Age >= 0f && p.Age < p.Life;

	public static float Radius(in SmokePuff p)
	{
		float u = Math.Clamp(p.Age / p.Life, 0f, 1f);
		return p.StartRadius + (p.EndRadius - p.StartRadius) * MathF.Sqrt(u);
	}

	public float Opacity(in SmokePuff p)
	{
		if (!IsVisible(p))
			return 0f;
		float u = Math.Clamp(p.Age / p.Life, 0f, 1f);
		float fadeIn = _s.SmokeFadeIn > 0f ? MathF.Min(1f, p.Age / _s.SmokeFadeIn) : 1f;
		return _s.SmokeOpacity * MathF.Pow(1f - u, _s.SmokeFadePower) * fadeIn;
	}

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();
}
