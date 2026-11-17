using System;

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
	}

	public CollisionFxSettings Settings { get; }
	public SparkField Sparks { get; }
	public SparkEmitter Emitter { get; }
	public CrashFlashes Flashes { get; }
	public CrashSmoke Smoke { get; }
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

	public void Update(float dt, ICrashGround ground)
	{
		Sparks.Update(dt, ground);
		Flashes.Update(dt);
		Smoke.Update(dt);
	}

	public void Clear()
	{
		Sparks.Clear();
		Emitter.Clear();
		Flashes.Clear();
		Smoke.Clear();
	}
}
