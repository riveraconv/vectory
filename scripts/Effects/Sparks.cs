using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Contact;

namespace Vectory.Effects;

// Una chispa viva: posición y velocidad en el mundo, edad y vida (s), si es brasa y la distancia sobre el eje de su
// última consulta al suelo
public struct Spark
{
	public Vector3 Position;
	public Vector3 Velocity;
	public float Age;
	public float Life;
	public bool Ember;
	public float Hint;
}

// Chispas vivas de todas las naves (spec docs/efectos-1-choques.md, 4.3 y 4.4): gravedad, aire, rebotes en el suelo
// y en la barrera, color por edad y trazo. Capacity: máximo de vivas con el PARTICLES de ahora; con el grupo lleno, las
// nuevas no salen. Lógica pura.
public sealed class SparkField
{
	private readonly List<Spark> _sparks = new();
	private readonly CollisionFxSettings _s;

	public SparkField(CollisionFxSettings settings)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		Capacity = settings.MaxSparks;
	}

	public int Count => _sparks.Count;
	public int Capacity { get; private set; }
	public Spark this[int index] => _sparks[index];

	// Nuevo máximo (PARTICLES cambiado en plena carrera): las vivas siguen; las que sobran, fuera
	public void SetCapacity(int capacity)
	{
		Capacity = Math.Max(0, capacity);
		if (_sparks.Count > Capacity)
			_sparks.RemoveRange(Capacity, _sparks.Count - Capacity);
	}

	public bool Add(in Spark spark)
	{
		if (_sparks.Count >= Capacity)
			return false;
		_sparks.Add(spark);
		return true;
	}

	public void Clear() => _sparks.Clear();

	// Un paso. Con dt 0 (pausa) no cambia nada.
	public void Update(float dt, ICrashGround ground)
	{
		if (dt <= 0f)
			return;
		float damping = MathF.Max(0f, 1f - _s.AirDamping * dt);
		for (int i = _sparks.Count - 1; i >= 0; i--)
		{
			Spark p = _sparks[i];
			p.Age += dt;
			if (p.Age >= p.Life)
			{
				int last = _sparks.Count - 1;
				_sparks[i] = _sparks[last];
				_sparks.RemoveAt(last);
				continue;
			}
			p.Velocity.Y -= _s.Gravity * dt;
			p.Velocity *= damping;
			p.Position += p.Velocity * dt;
			if (ground != null)
				Collide(ref p, ground);
			_sparks[i] = p;
		}
	}

	// Suelo: rebota con un 30 % de la velocidad contra la superficie y un 60 % de la otra. Barrera: vuelve al límite y
	// rebota hacia dentro con un 30 % de la velocidad lateral.
	private void Collide(ref Spark p, ICrashGround ground)
	{
		GroundPoint g = ground.Locate(p.Position, p.Hint);
		p.Hint = g.Distance;
		float height = g.Height;
		if (height < _s.GroundClearance)
		{
			float vn = Vector3.Dot(p.Velocity, g.Up);
			if (vn < 0f)
			{
				p.Position += g.Up * (_s.GroundClearance - height);
				Vector3 tangential = p.Velocity - g.Up * vn;
				p.Velocity = tangential * _s.GroundBounceHorizontal - g.Up * (vn * _s.GroundBounceVertical);
			}
		}
		float limit = ground.BarrierOffset;
		if (MathF.Abs(g.Lateral) > limit)
		{
			float sign = MathF.Sign(g.Lateral);
			p.Position -= g.Right * (g.Lateral - sign * limit);
			float vl = Vector3.Dot(p.Velocity, g.Right);
			if (vl * sign > 0f)
				p.Velocity -= g.Right * (vl * (1f + _s.BarrierBounce));
		}
	}

	// Color de la cabeza del trazo, ya multiplicado por su brillo (u = edad / vida)
	public static Vector3 ColorAt(float u, bool ember, CollisionFxSettings s)
	{
		u = Math.Clamp(u, 0f, 1f);
		if (ember)
			return s.EmberColor * (s.EmberBrightness * (1f - u));
		Vector3 c = u < s.SparkHotUntil
			? Vector3.Lerp(s.SparkHot, s.SparkOrange, u / s.SparkHotUntil)
			: Vector3.Lerp(s.SparkOrange, s.SparkCold, (u - s.SparkHotUntil) / (1f - s.SparkHotUntil));
		return c * MathF.Pow(1f - u, s.SparkFadePower);
	}

	public Vector3 ColorOf(in Spark p) => ColorAt(p.Age / p.Life, p.Ember, _s);

	// Cola del trazo (desde la cabeza): hacia atrás de la velocidad relativa a reference, de largo velocidad relativa
	// × StreakTime, como mucho StreakMax
	public static Vector3 Tail(Vector3 velocity, Vector3 reference, CollisionFxSettings s)
	{
		Vector3 rel = velocity - reference;
		float length = rel.Length() * s.StreakTime;
		if (length <= 0f)
			return Vector3.Zero;
		float scale = length > s.StreakMax ? s.StreakMax / length : 1f;
		return -rel * (s.StreakTime * scale);
	}
}

// Chispas nuevas de un contacto (spec docs/efectos-1-choques.md, 4.1 y 4.2), con PARTICLES (quality: 0,25 / 0,5 / 1)
// y un generador inyectado. Lleva un acumulador del roce por cada quien roza, así el número no depende de los fps.
// Lógica pura.
public sealed class SparkEmitter
{
	private readonly CollisionFxSettings _s;
	private readonly Random _random;
	private readonly Dictionary<int, float> _accumulators = new();

	public SparkEmitter(CollisionFxSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	// Redondeo de la maqueta (Math.round de JavaScript): las mitades hacia arriba
	public static int Scaled(int value, float quality) => (int)MathF.Round(value * quality, MidpointRounding.AwayFromZero);

	public SparkLevel LevelOf(CrashKind kind, ImpactLevel level) => (kind, level) switch
	{
		(CrashKind.Barrier, ImpactLevel.Graze) => _s.BarrierGraze,
		(CrashKind.Barrier, ImpactLevel.Medium) => _s.BarrierMedium,
		(CrashKind.Barrier, _) => _s.BarrierStrong,
		(_, ImpactLevel.Graze) => _s.ShipGraze,
		(_, ImpactLevel.Medium) => _s.ShipMedium,
		_ => _s.ShipStrong
	};

	// Chispas nuevas de un contacto en field. dt: el del paso, para el roce. Devuelve cuántas han salido.
	public int Emit(in CrashEvent e, float quality, float dt, SparkField field)
	{
		// El roce por detrás entre naves no saca chispas: no hay fricción
		if (e.Kind == CrashKind.Ships && !e.IsStart && e.Graze == GrazeKind.Rear)
			return 0;
		SparkLevel level = LevelOf(e.Kind, e.IsStart ? e.Level : ImpactLevel.Graze);
		SparkDirection direction = DirectionOf(e);
		int added = 0;
		if (e.IsStart)
		{
			int count = Math.Max(1, Scaled(level.Count, quality));
			int embers = Scaled(level.Embers, quality);
			for (int i = 0; i < count; i++)
				added += One(e, level, direction, false, field) ? 1 : 0;
			for (int i = 0; i < embers; i++)
				added += One(e, level, direction, true, field) ? 1 : 0;
			return added;
		}

		_accumulators.TryGetValue(e.Source, out float acc);
		acc += level.Rate * quality * dt;
		while (acc >= 1f)
		{
			acc -= 1f;
			added += One(e, level, direction, false, field) ? 1 : 0;
		}
		_accumulators[e.Source] = acc;
		return added;
	}

	public void Clear() => _accumulators.Clear();

	private SparkDirection DirectionOf(in CrashEvent e)
	{
		if (e.Kind == CrashKind.Barrier)
			return _s.BarrierDirection;
		return e.Graze == GrazeKind.Rear ? _s.ShipRearDirection : _s.ShipSideDirection;
	}

	private bool One(in CrashEvent e, SparkLevel level, SparkDirection d, bool ember, SparkField field)
	{
		float ux = Range(-1f, 1f);
		float uy = Range(-1f, 1f);
		float uz = Range(-1f, 1f);
		float sideSign = d.SideBothWays && _random.NextSingle() < 0.5f ? -1f : 1f;
		float side = sideSign * Range(d.NormalMin, d.NormalMax) + ux * d.RandomSide;
		float up = Range(d.UpMin, d.UpMax) + uy * d.RandomUp;
		float back = Range(d.BackMin, d.BackMax) + uz * d.RandomBack;
		Vector3 dir = e.Normal * side + e.Up * up + e.Back * back;
		float length = dir.Length();
		dir = length > 0f ? dir / length : e.Up;
		float speed = ember ? Range(_s.EmberSpeedMin, _s.EmberSpeedMax) : Range(level.SpeedMin, level.SpeedMax);

		bool barrier = e.Kind == CrashKind.Barrier;
		float inherit = barrier ? _s.BarrierInherit : _s.ShipInherit;
		float extraBack = barrier ? 0f : Range(_s.ShipBackMin, _s.ShipBackMax);
		Vector3 position = e.Point
			+ e.Normal * Range(-_s.SparkJitterSide, _s.SparkJitterSide)
			+ e.Up * Range(-_s.SparkJitterUp, _s.SparkJitterUp)
			+ e.Back * Range(-_s.SparkJitterAlong, _s.SparkJitterAlong);
		float life = ember ? Range(_s.EmberLifeMin, _s.EmberLifeMax) : Range(level.LifeMin, level.LifeMax);
		Vector3 velocity = e.Velocity * inherit + dir * speed + e.Back * extraBack;
		if (barrier && !ember && _s.HighSparkFraction > 0f && _random.NextSingle() < _s.HighSparkFraction)
			velocity += e.Up * Range(_s.HighSparkUpMin, _s.HighSparkUpMax);
		var spark = new Spark
		{
			Position = position,
			Velocity = velocity,
			Age = 0f,
			Life = life,
			Ember = ember,
			Hint = e.AxisDistance
		};
		return field.Add(spark);
	}

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();
}
