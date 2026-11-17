using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Effects;

// Ajustes de los efectos del derrape (spec docs/efectos-2-derrape.md, 8), con los valores de la maqueta
// designs/efectos-derrape.html. Ángulos en grados, distancias en m, tiempos en s. Lógica pura: solo datos.
public sealed class DriftFxSettings
{
	// --- Intensidad (3)
	public float StartAngle = 4f;     // hasta aquí, sin efectos
	public float FullAngle = 22f;     // desde aquí, intensidad completa

	// --- Marcas (4)
	public float TipFraction = 0.95f;          // de semiancho de la nave
	public float MarkSpacing = 0.8f;           // m que avanza el centro entre las dos puntas entre muestras
	public float MarkLift = 0.03f;             // m sobre la superficie, por su normal
	public float MarkLife = 8f;
	public float MarkFadeFrom = 0.75f;         // fracción de la vida desde la que se apaga
	public float MarkBrightness = 0.32f;
	public float MarkMinFactor = 0.35f;        // brillo × (MarkMinFactor + (1 − MarkMinFactor) · d)
	public Vector3 MarkColor = new(1f, 0.62f, 0.2f);
	public int MaxMarkSamples = 700;           // por nave

	public static DriftFxSettings Default => new();
}

// Lo que el derrape de una nave enseña en un paso: si hay efectos, su intensidad d (0–1) y el lado del roce
public readonly record struct DriftState(bool Active, float D, BarrierSide Side)
{
	public static DriftState None => new(false, 0f, BarrierSide.Left);
}

// Intensidad del derrape (3). Lógica pura.
public static class DriftIntensity
{
	// slipAngle: el de ShipDynamics, en radianes (positivo = desliza hacia la derecha). Hay efectos derrapando, en el
	// suelo y con más de StartAngle; el lado del roce es el interior, el contrario al del deslizamiento.
	public static DriftState Evaluate(bool drifting, bool airborne, float slipAngle, DriftFxSettings s)
	{
		float beta = MathF.Abs(slipAngle) * 180f / MathF.PI;
		float span = s.FullAngle - s.StartAngle;
		float d = span > 0f ? Math.Clamp((beta - s.StartAngle) / span, 0f, 1f) : (beta > s.StartAngle ? 1f : 0f);
		BarrierSide side = slipAngle > 0f ? BarrierSide.Left : BarrierSide.Right;
		bool active = drifting && !airborne && beta > s.StartAngle;
		return new DriftState(active, active ? d : 0f, side);
	}
}

// Dónde roza una nave al derrapar (4.1): las puntas traseras de las alas, sobre la superficie. Lógica pura.
public static class DriftContact
{
	// Puntos de las dos marcas: (±TipFraction × semiancho, tipZ) de la nave (z positivo hacia la popa), en planta con
	// su rumbo, llevados a la superficie de la pista y subidos MarkLift por su normal
	public static (Vector3 Left, Vector3 Right) MarkPoints(TrackFrame frame, Vector3 position, Vector2 forward,
		float halfWidth, float tipZ, DriftFxSettings s)
	{
		float x = halfWidth * s.TipFraction;
		return (OnSurface(frame, position, forward, -x, tipZ, s.MarkLift), OnSurface(frame, position, forward, x, tipZ, s.MarkLift));
	}

	// Un punto de la nave (lateral x a la derecha, z hacia la popa) en planta, llevado a la superficie y subido lift
	public static Vector3 OnSurface(TrackFrame frame, Vector3 position, Vector2 forward, float x, float z, float lift)
	{
		var right = new Vector2(-forward.Y, forward.X);
		Vector2 plan = new Vector2(position.X, position.Z) + right * x - forward * z;
		if (frame == null)
			return new Vector3(plan.X, lift, plan.Y);
		SurfacePoint p = frame.SurfaceUnder(plan.X, plan.Y);
		return new Vector3(plan.X, p.SurfaceY, plan.Y) + Vector3.Normalize(p.Frame.Normal) * lift;
	}
}

// Una muestra del rastro: los dos puntos, cuándo se tomó, su intensidad y su trazo
public readonly record struct DriftMarkSample(Vector3 Left, Vector3 Right, float Time, float D, int Stroke);

// Rastro de marcas de una nave (4.2): una muestra cada MarkSpacing m del centro entre las puntas mientras hay efectos;
// cada corte cierra el trazo. Como mucho MaxMarkSamples: con el rastro lleno, fuera la más vieja. Lógica pura.
public sealed class DriftMarkTrail
{
	private readonly DriftFxSettings _s;
	private readonly DriftMarkSample[] _ring;
	private int _start;
	private int _stroke;
	private bool _open;
	private Vector2 _last;

	public DriftMarkTrail(DriftFxSettings settings)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_ring = new DriftMarkSample[Math.Max(1, settings.MaxMarkSamples)];
	}

	public int Count { get; private set; }

	// La muestra i, de la más vieja (0) a la más nueva
	public DriftMarkSample this[int index] => _ring[(_start + index) % _ring.Length];

	// Un paso: con efectos, guarda una muestra si el centro avanzó MarkSpacing (o empieza un trazo); sin ellos, corta
	public void Update(bool active, float d, Vector3 left, Vector3 right, float time)
	{
		Prune(time);
		if (!active)
		{
			_open = false;
			return;
		}
		var center = new Vector2((left.X + right.X) * 0.5f, (left.Z + right.Z) * 0.5f);
		if (!_open)
		{
			_stroke++;
			_open = true;
		}
		else if (Vector2.Distance(center, _last) < _s.MarkSpacing)
			return;
		_last = center;
		Push(new DriftMarkSample(left, right, time, d, _stroke));
	}

	// Corta el trazo (recolocación): lo que ya hay se queda
	public void Cut() => _open = false;

	public void Clear()
	{
		Count = 0;
		_start = 0;
		_open = false;
	}

	// Quita las que ya se han apagado del todo
	public void Prune(float time)
	{
		while (Count > 0 && time - this[0].Time >= _s.MarkLife)
		{
			_start = (_start + 1) % _ring.Length;
			Count--;
		}
	}

	// Brillo de una muestra en el momento time (4.3)
	public float BrightnessOf(in DriftMarkSample sample, float time) => DriftMarks.Brightness(sample.D, time - sample.Time, _s);

	private void Push(in DriftMarkSample sample)
	{
		if (Count == _ring.Length)
		{
			_start = (_start + 1) % _ring.Length;
			Count--;
		}
		_ring[(_start + Count) % _ring.Length] = sample;
		Count++;
	}
}

// Las marcas de todas las naves, con su propio reloj: Advance lo mueve (con la pausa, 0) y las muestras nuevas llevan
// su hora. Lógica pura: quien dibuja recorre Trails y une las muestras seguidas del mismo trazo.
public sealed class DriftMarks
{
	private readonly DriftFxSettings _s;
	private readonly Dictionary<int, DriftMarkTrail> _trails = new();

	public DriftMarks(DriftFxSettings settings)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
	}

	public float Now { get; private set; }
	public IEnumerable<DriftMarkTrail> Trails => _trails.Values;

	public void Advance(float dt)
	{
		if (dt > 0f)
			Now += dt;
	}

	// El paso de una nave
	public void Update(int shipId, in DriftState state, Vector3 left, Vector3 right)
	{
		if (!_trails.TryGetValue(shipId, out DriftMarkTrail trail))
		{
			if (!state.Active)
				return;
			_trails[shipId] = trail = new DriftMarkTrail(_s);
		}
		trail.Update(state.Active, state.D, left, right, Now);
	}

	public void Cut(int shipId)
	{
		if (_trails.TryGetValue(shipId, out DriftMarkTrail trail))
			trail.Cut();
	}

	public void Clear()
	{
		foreach (DriftMarkTrail trail in _trails.Values)
			trail.Clear();
	}

	// Brillo de una muestra de intensidad d con edad age: MarkBrightness × (MarkMinFactor + (1 − MarkMinFactor) · d) ×
	// desvanecido (1 hasta MarkFadeFrom de la vida; luego baja en línea recta hasta 0)
	public static float Brightness(float d, float age, DriftFxSettings s)
	{
		if (s.MarkLife <= 0f || age >= s.MarkLife)
			return 0f;
		float u = MathF.Max(0f, age) / s.MarkLife;
		float fade = u < s.MarkFadeFrom ? 1f : (1f - u) / (1f - s.MarkFadeFrom);
		return s.MarkBrightness * (s.MarkMinFactor + (1f - s.MarkMinFactor) * Math.Clamp(d, 0f, 1f)) * fade;
	}
}
