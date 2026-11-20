using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Effects;

// Los efectos de choque de toda la carrera (spec docs/efectos-1-choques.md): chispas, destellos y humo de los contactos
// de todas las naves. Quality: la fracción de PARTICLES (0,25 / 0,5 / 1), que escala chispas, humo y sus máximos; un
// cambio vale para lo nuevo y quita lo que sobre. Lógica pura: quien dibuja lo lee de Sparks, Flashes y Smoke.
public sealed class CollisionFx
{
	public CollisionFx(CollisionFxSettings settings, Random random)
	{
		Settings = settings ?? throw new ArgumentNullException(nameof(settings));
		if (random == null)
			throw new ArgumentNullException(nameof(random));
		Sparks = new SparkField(settings);
		Emitter = new SparkEmitter(settings, random);
		Flashes = new CrashFlashes(settings, random);
		Smoke = new CrashSmoke(settings, random);
		Landing = new LandingEmitter(settings.Landing, random);
		_random = random;
	}

	private readonly Random _random;

	// Chispas de una luz que vuelve de un apagón o revive (spec docs/efectos-8-luces.md, 9.3): 3 (× PARTICLES), con la
	// velocidad de su nave más ±2 m/s de lado y adelante-atrás y 0,5–3 m/s hacia arriba; vida 0,15–0,30 s
	public void LightSpark(Vector3 position, Vector3 shipVelocity, float axisDistance)
	{
		int count = SparkEmitter.Scaled(LightSparkCount, Quality);
		for (int i = 0; i < count; i++)
		{
			var velocity = shipVelocity + new Vector3(Range(-2f, 2f), Range(0.5f, 3f), Range(-2f, 2f));
			Sparks.Add(new Spark { Position = position, Velocity = velocity, Life = Range(0.15f, 0.30f), Hint = axisDistance });
		}
	}

	public const int LightSparkCount = 3;

	private float Range(float a, float b) => a + (b - a) * _random.NextSingle();

	public CollisionFxSettings Settings { get; }
	public SparkField Sparks { get; }
	public SparkEmitter Emitter { get; }
	public CrashFlashes Flashes { get; }
	public CrashSmoke Smoke { get; }
	public LandingEmitter Landing { get; }
	public float Quality { get; private set; } = 1f;

	public void SetQuality(float quality)
	{
		Quality = Math.Clamp(quality, 0f, 1f);
		Sparks.SetCapacity(SparkEmitter.Scaled(Settings.MaxSparks, Quality));
		Smoke.SetCapacity(SparkEmitter.Scaled(Settings.MaxPuffs, Quality));
	}

	// Un contacto en un paso de física de dt segundos
	public void Crash(in CrashEvent e, float dt)
	{
		Emitter.Emit(e, Quality, dt, Sparks);
		Flashes.Emit(e, dt);
		Smoke.Emit(e, Quality);
	}

	// Un aterrizaje (spec docs/efectos-3-aterrizaje.md): chispas desde la panza y polvo a ras de suelo
	public LandingBurst Land(LandingLevel level, TrackFrame frame, in ShipPose pose, System.Numerics.Vector2 velocity,
		float halfWidth, float back, float bellyY) =>
		Landing.Emit(level, frame, pose, velocity, halfWidth, back, bellyY, Quality, Sparks, Smoke);

	public void Update(float dt, ICrashGround ground)
	{
		Sparks.Update(dt, ground);
		Flashes.Update(dt);
		Smoke.Update(dt, ground);
	}

	public void Clear()
	{
		Sparks.Clear();
		Emitter.Clear();
		Flashes.Clear();
		Smoke.Clear();
	}
}
