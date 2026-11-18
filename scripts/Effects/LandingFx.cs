using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Effects;

// Lo que saca un nivel de aterrizaje (valores en HIGH): chispas y brasas desde la panza, bocanadas de polvo con su
// mínimo, y la vida y el radio de las bocanadas
public sealed class LandingLevelFx
{
	public int Sparks;
	public int Embers;
	public int Puffs;
	public int MinPuffs;
	public float PuffLifeMin, PuffLifeMax;
	public float PuffStartRadius, PuffEndRadius;

	public LandingLevelFx(int sparks, int embers, int puffs, int minPuffs, float lifeMin, float lifeMax, float startRadius,
		float endRadius)
	{
		Sparks = sparks;
		Embers = embers;
		Puffs = puffs;
		MinPuffs = minPuffs;
		PuffLifeMin = lifeMin;
		PuffLifeMax = lifeMax;
		PuffStartRadius = startRadius;
		PuffEndRadius = endRadius;
	}
}

// Ajustes de los efectos de aterrizaje (spec docs/efectos-3-aterrizaje.md, 6), con los valores de la maqueta
// designs/efectos-aterrizaje.html. Los umbrales de nivel no van aquí: son los de pista-3d (LandingSettings).
// Distancias en m, velocidades en m/s, tiempos en s. Lógica pura: solo datos.
public sealed class LandingFxSettings
{
	public LandingLevelFx Medium = new(20, 0, 4, 1, 0.9f, 1.3f, 0.5f, 1.6f);
	public LandingLevelFx Strong = new(70, 12, 12, 2, 1.1f, 1.6f, 0.6f, 2.4f);

	// --- Chispas (3). Nacen bajo la panza real de la nave (decisión del usuario: la nave flota a 2 m y en la pista se
	// verían separadas de ella), con su inclinación.
	public float SparkWidthFraction = 0.8f;     // del semiancho, a cada lado
	public float SparkAhead = 2f;               // m por delante del centro; hasta la popa
	public float SparkBellyDrop = 0.06f;        // m por debajo de la panza
	public float SparkSideMin = 3f, SparkSideMax = 9f;     // m/s hacia fuera por su lado
	public float SparkSideRandom = 1f;                     // ± m/s de lado
	public float SparkUpMin = 1f, SparkUpMax = 5f;         // m/s por la normal de la superficie
	public float EmberSideMin = 1f, EmberSideMax = 3f;
	public float EmberUpMin = 0.5f, EmberUpMax = 2f;
	public float SparkInherit = 0.7f;                      // de la velocidad de la nave
	public float SparkAlongMin = -4f, SparkAlongMax = 2f;  // m/s a lo largo: de 4 menos a 2 más
	public float SparkLifeMin = 0.2f, SparkLifeMax = 0.6f;
	public float EmberLifeMin = 0.9f, EmberLifeMax = 1.4f;

	// --- Polvo (4). Sus distancias de lado son las de la maqueta, cuya nave tiene 1,87 m de semiancho: se escalan con
	// el de cada nave (decisión del usuario)
	public float DustReferenceHalfWidth = 1.87f;
	public float DustSideMin = 0.5f, DustSideMax = 1.8f;   // m del centro, con la nave de la maqueta
	public float DustAhead = 2.5f, DustBehind = 2f;        // m a lo largo, delante y detrás del centro
	public float DustLift = 0.3f;                          // m sobre la superficie
	public float DustWindow = 0.06f;                       // s de retraso aleatorio
	public float DustOutMin = 2f, DustOutMax = 6f;
	public float DustUpMin = 0.2f, DustUpMax = 0.6f;
	public float DustInherit = 0.5f;
	public float DustAlongRandom = 1f;                     // ± m/s a lo largo
	public SmokeStyle Dust = new()
	{
		DampHorizontal = 2.2f,
		DampVertical = 0.6f,
		Opacity = 0.38f,
		FadeIn = 0.08f,
		HeightScale = 0.7f,       // alto 1,4 × radio, ancho 2 × radio
		BarrierGap = 0.5f
	};

	public LandingLevelFx LevelOf(LandingLevel level) => level switch
	{
		LandingLevel.Medium => Medium,
		LandingLevel.Strong => Strong,
		_ => null
	};

	public static LandingFxSettings Default => new();
}

// Cuánto ha salido de un aterrizaje
public readonly record struct LandingBurst(int Sparks, int Embers, int Puffs);

// Chispas y polvo de un aterrizaje (spec docs/efectos-3-aterrizaje.md, 3 y 4), con PARTICLES (quality: 0,25 / 0,5 / 1)
// y un generador inyectado; se meten en las chispas y el humo de los choques. Lógica pura.
public sealed class LandingEmitter
{
	private readonly LandingFxSettings _s;
	private readonly Random _random;

	public LandingEmitter(LandingFxSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	// Cuántas chispas, brasas y bocanadas saca un nivel con PARTICLES: al menos 1 chispa y las bocanadas de su mínimo;
	// las brasas pueden quedarse en 0
	public LandingBurst CountFor(LandingLevel level, float quality)
	{
		LandingLevelFx l = _s.LevelOf(level);
		if (l == null)
			return default;
		return new LandingBurst(Math.Max(1, SparkEmitter.Scaled(l.Sparks, quality)), SparkEmitter.Scaled(l.Embers, quality),
			Math.Max(l.MinPuffs, SparkEmitter.Scaled(l.Puffs, quality)));
	}

	// Un aterrizaje. pose: la nave que se ve, con su inclinación; velocity: su velocidad en planta (X, Z); halfWidth y
	// back: su semiancho de colisión y lo que llega su popa desde el centro; bellyY: altura de su panza en sus ejes;
	// frame: la superficie (null: suelo plano en y = 0). Devuelve lo que ha entrado.
	public LandingBurst Emit(LandingLevel level, TrackFrame frame, in ShipPose pose, Vector2 velocity, float halfWidth,
		float back, float bellyY, float quality, SparkField sparks, CrashSmoke smoke)
	{
		LandingLevelFx l = _s.LevelOf(level);
		if (l == null)
			return default;
		LandingBurst count = CountFor(level, quality);

		(_, Vector3 normal) = Surface(frame, pose.Origin.X, pose.Origin.Z);
		Vector3 side = OnSurface(pose.Right, normal, Vector3.UnitX);
		Vector3 shipVelocity = SurfaceVelocity(velocity, normal);
		Vector3 forward = shipVelocity.LengthSquared() > 1e-4f ? Vector3.Normalize(shipVelocity) : OnSurface(-pose.Back, normal, -Vector3.UnitZ);

		int sparkCount = 0, emberCount = 0;
		for (int i = 0; i < count.Sparks + count.Embers; i++)
		{
			bool ember = i >= count.Sparks;
			float x = Range(-_s.SparkWidthFraction, _s.SparkWidthFraction) * halfWidth;
			float z = Range(-_s.SparkAhead, back);
			float sign = x < 0f ? -1f : 1f;
			Vector3 position = pose.Point(new Vector3(x, bellyY - _s.SparkBellyDrop, z));
			float outward = ember ? Range(_s.EmberSideMin, _s.EmberSideMax) : Range(_s.SparkSideMin, _s.SparkSideMax);
			float up = ember ? Range(_s.EmberUpMin, _s.EmberUpMax) : Range(_s.SparkUpMin, _s.SparkUpMax);
			Vector3 v = side * (sign * outward + Range(-_s.SparkSideRandom, _s.SparkSideRandom))
				+ normal * up
				+ shipVelocity * _s.SparkInherit
				+ forward * Range(_s.SparkAlongMin, _s.SparkAlongMax);
			float life = ember ? Range(_s.EmberLifeMin, _s.EmberLifeMax) : Range(_s.SparkLifeMin, _s.SparkLifeMax);
			float hint = frame != null ? frame.SurfaceUnder(position.X, position.Z).Frame.Distance : -position.Z;
			if (!sparks.Add(new Spark { Position = position, Velocity = v, Age = 0f, Life = life, Ember = ember, Hint = hint }))
				break;
			if (ember)
				emberCount++;
			else
				sparkCount++;
		}

		// Polvo: alternando lados (empieza por la izquierda, como la maqueta), pegado a la superficie de cada punto
		Vector3 planRight = Plan(pose.Right, Vector3.UnitX);
		Vector3 planBack = Plan(pose.Back, Vector3.UnitZ);
		float scale = _s.DustReferenceHalfWidth > 0f ? halfWidth / _s.DustReferenceHalfWidth : 1f;
		int puffs = 0;
		for (int i = 0; i < count.Puffs; i++)
		{
			float sign = i % 2 == 1 ? 1f : -1f;
			float lateral = sign * Range(_s.DustSideMin, _s.DustSideMax) * scale;
			float along = Range(-_s.DustAhead, _s.DustBehind);
			Vector3 plan = pose.Origin + planRight * lateral + planBack * along;
			(float y, Vector3 n) = Surface(frame, plan.X, plan.Z);
			Vector3 position = new Vector3(plan.X, y, plan.Z) + n * _s.DustLift;
			Vector3 v = side * (sign * Range(_s.DustOutMin, _s.DustOutMax))
				+ n * Range(_s.DustUpMin, _s.DustUpMax)
				+ shipVelocity * _s.DustInherit
				+ forward * Range(-_s.DustAlongRandom, _s.DustAlongRandom);
			var puff = new SmokePuff
			{
				Position = position,
				Velocity = v,
				Age = -Range(0f, _s.DustWindow),
				Life = Range(l.PuffLifeMin, l.PuffLifeMax),
				StartRadius = l.PuffStartRadius,
				EndRadius = l.PuffEndRadius * Range(0.8f, 1.2f),
				Style = _s.Dust,
				Hint = frame != null ? frame.SurfaceUnder(position.X, position.Z).Frame.Distance : -position.Z
			};
			if (!smoke.Add(puff))
				break;
			puffs++;
		}
		return new LandingBurst(sparkCount, emberCount, puffs);
	}

	// Altura y normal de la superficie en (x, z)
	private static (float Y, Vector3 Normal) Surface(TrackFrame frame, float x, float z)
	{
		if (frame == null)
			return (0f, Vector3.UnitY);
		SurfacePoint p = frame.SurfaceUnder(x, z);
		return (p.SurfaceY, p.Frame.Normal);
	}

	// La velocidad en planta llevada a la superficie: misma planta, con la subida o bajada de la pendiente
	private static Vector3 SurfaceVelocity(Vector2 velocity, Vector3 normal)
	{
		float y = normal.Y > 1e-3f ? -(normal.X * velocity.X + normal.Z * velocity.Y) / normal.Y : 0f;
		return new Vector3(velocity.X, y, velocity.Y);
	}

	private static Vector3 OnSurface(Vector3 v, Vector3 normal, Vector3 fallback)
	{
		Vector3 p = v - normal * Vector3.Dot(v, normal);
		return p.LengthSquared() > 1e-6f ? Vector3.Normalize(p) : fallback;
	}

	private static Vector3 Plan(Vector3 v, Vector3 fallback)
	{
		var p = new Vector3(v.X, 0f, v.Z);
		return p.LengthSquared() > 1e-6f ? Vector3.Normalize(p) : fallback;
	}

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();
}
