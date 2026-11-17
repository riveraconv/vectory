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

	// --- Chispas (5): las de los choques, con otro sitio y otra salida. Nacen en el borde del ala, a su altura real (con
	// la inclinación de la nave), y caen a la pista (decisión del usuario). Valores en HIGH.
	public float SparkRate = 160f;             // por segundo × d, entre el borde y el foco
	public float JetFraction = 0.6f;           // parte de ellas que sale del foco, en chorro
	public float SparkInherit = 0.6f;          // de la velocidad de la nave
	public float SparkOutMin = 2f, SparkOutMax = 6f;   // m/s hacia fuera de la nave, por el lado del roce
	public float SparkUpMin = 1f, SparkUpMax = 4f;     // m/s hacia arriba de la nave
	public float SparkRandom = 1f;             // ±m/s a lo largo de la nave y en vertical (las del borde)
	public float JetRandom = 0.3f;             // ídem en el chorro del foco: abanico estrecho
	public float SparkLifeMin = 0.15f, SparkLifeMax = 0.4f;
	public float EmberRate = 8f;               // brasas por segundo × d, desde el foco
	public float EmberOutMin = 1f, EmberOutMax = 3f;
	public float EmberUpMin = 0.5f, EmberUpMax = 2f;
	public float EmberLifeMin = 0.5f, EmberLifeMax = 0.9f;

	// --- Foco de chispas (6, decisión del usuario: sustituye a la mancha y al halo): núcleo blanco caliente en el punto
	// que más roza y un halo naranja pequeño, de cara a la cámara, chisporroteando
	public float FocusDrop = 0.2f;             // m que bajan foco y chispas por debajo del borde, hacia abajo de la nave
	public float CoreSize = 0.2f;              // m
	public float CoreIntensity = 1f;
	public Vector3 CoreColor = new(1f, 0.95f, 0.82f);   // el blanco caliente de las chispas
	public float FocusHaloSize = 0.9f;         // m
	public float FocusHaloIntensity = 0.35f;
	public Vector3 FocusHaloColor = new(1f, 0x9D / 255f, 0x32 / 255f);   // #FF9D32
	public float FlickerA = 97f, FlickerB = 61f;   // 0,7 + 0,3 · sen(A·t) · sen(B·t): chisporroteo rápido
	public float FlickerShipPhase = 1.37f;         // s de desfase por id de nave

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
		GroundSpot g = Ground(frame, position, forward, x, z);
		return g.Point + g.Up * lift;
	}

	// Un punto de la nave sobre la superficie, con su normal y su distancia sobre el eje. Sin pista (pruebas), el
	// suelo plano en y = 0.
	public static GroundSpot Ground(TrackFrame frame, Vector3 position, Vector2 forward, float x, float z)
	{
		var right = new Vector2(-forward.Y, forward.X);
		Vector2 plan = new Vector2(position.X, position.Z) + right * x - forward * z;
		if (frame == null)
			return new GroundSpot(new Vector3(plan.X, 0f, plan.Y), Vector3.UnitY, -plan.Y);
		SurfacePoint p = frame.SurfaceUnder(plan.X, plan.Y);
		return new GroundSpot(new Vector3(plan.X, p.SurfaceY, plan.Y), Vector3.Normalize(p.Frame.Normal), p.Distance);
	}

	// Lateral con signo del lado del roce: a la izquierda, negativo
	public static float SideSign(BarrierSide side) => side == BarrierSide.Left ? -1f : 1f;

	// Punto del borde que roza en la fracción t (0 delante… 1 detrás del tramo), en coordenadas de la nave
	public static Vector3 EdgePoint(in DriftEdge edge, BarrierSide side, float t)
	{
		float fraction = edge.StartFraction + (edge.EndFraction - edge.StartFraction) * t;
		return new Vector3(SideSign(side) * edge.HalfWidth * fraction, edge.Y, edge.StartZ + (edge.EndZ - edge.StartZ) * t);
	}

	// El foco, en coordenadas de la nave
	public static Vector3 FocusPoint(in DriftEdge edge, BarrierSide side) =>
		new(SideSign(side) * edge.HalfWidth * edge.FocusFraction, edge.Y, edge.FocusZ);
}

// Un punto sobre la superficie de la pista: el punto, su normal y su distancia sobre el eje
public readonly record struct GroundSpot(Vector3 Point, Vector3 Up, float Distance);

// Cómo está la nave que se ve (con su inclinación y su cabeceo): origen y ejes en el mundo; z positivo hacia la popa
public readonly record struct ShipPose(Vector3 Origin, Vector3 Right, Vector3 Up, Vector3 Back)
{
	public Vector3 Point(Vector3 local) => Origin + Right * local.X + Up * local.Y + Back * local.Z;

	// Nave sin girar sobre el suelo plano, mirando a −Z (pruebas)
	public static ShipPose Flat(Vector3 origin) => new(origin, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
}

// El borde de una nave que roza al derrapar (5), con su semiancho de colisión: de (StartZ, StartFraction) a (EndZ,
// EndFraction) a la altura Y de la nave, y el foco en (FocusFraction, FocusZ)
public readonly record struct DriftEdge(float HalfWidth, float StartZ, float StartFraction, float EndZ, float EndFraction,
	float Y, float FocusZ, float FocusFraction);

// Chispas del derrape (5): del borde que roza y, en chorro, del foco, con alguna brasa; se meten en las chispas de los
// choques. Lleva un acumulador por nave, así el número no depende de los fps. Lógica pura.
public sealed class DriftSparkEmitter
{
	private readonly DriftFxSettings _s;
	private readonly Random _random;
	private readonly Dictionary<int, (float Sparks, float Embers)> _accumulators = new();

	public DriftSparkEmitter(DriftFxSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	// Un paso de dt segundos de una nave. pose: la nave que se ve; velocity: su velocidad en planta (X, Z); quality:
	// PARTICLES; frame: para la distancia sobre el eje de cada chispa (null: suelo plano). Devuelve cuántas han salido.
	public int Emit(int shipId, in DriftState state, TrackFrame frame, in ShipPose pose, Vector2 velocity, in DriftEdge edge,
		float quality, float dt, SparkField field)
	{
		if (!state.Active)
		{
			_accumulators.Remove(shipId);
			return 0;
		}
		_accumulators.TryGetValue(shipId, out (float Sparks, float Embers) acc);
		acc.Sparks += _s.SparkRate * state.D * quality * dt;
		acc.Embers += _s.EmberRate * state.D * quality * dt;
		float sign = DriftContact.SideSign(state.Side);
		Vector3 outward = pose.Right * sign;
		Vector3 along = -pose.Back;
		var inherited = new Vector3(velocity.X, 0f, velocity.Y) * _s.SparkInherit;
		Vector3 focus = pose.Point(DriftContact.FocusPoint(edge, state.Side));
		int added = 0;
		while (acc.Sparks >= 1f)
		{
			acc.Sparks -= 1f;
			bool jet = _random.NextSingle() < _s.JetFraction;
			Vector3 position = jet ? focus : pose.Point(DriftContact.EdgePoint(edge, state.Side, _random.NextSingle()));
			float spread = jet ? _s.JetRandom : _s.SparkRandom;
			Vector3 v = inherited
				+ outward * Range(_s.SparkOutMin, _s.SparkOutMax)
				+ pose.Up * Range(_s.SparkUpMin, _s.SparkUpMax)
				+ along * Range(-spread, spread)
				+ Vector3.UnitY * Range(-spread, spread);
			added += Add(field, frame, position, v, Range(_s.SparkLifeMin, _s.SparkLifeMax), false) ? 1 : 0;
		}
		while (acc.Embers >= 1f)
		{
			acc.Embers -= 1f;
			Vector3 v = inherited
				+ outward * Range(_s.EmberOutMin, _s.EmberOutMax)
				+ pose.Up * Range(_s.EmberUpMin, _s.EmberUpMax);
			added += Add(field, frame, focus, v, Range(_s.EmberLifeMin, _s.EmberLifeMax), true) ? 1 : 0;
		}
		_accumulators[shipId] = acc;
		return added;
	}

	public void Clear() => _accumulators.Clear();

	private static bool Add(SparkField field, TrackFrame frame, Vector3 position, Vector3 velocity, float life, bool ember)
	{
		float hint = frame != null ? frame.SurfaceUnder(position.X, position.Z).Distance : -position.Z;
		return field.Add(new Spark { Position = position, Velocity = velocity, Age = 0f, Life = life, Ember = ember, Hint = hint });
	}

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();
}

// Foco de chispas (6): núcleo y halo del punto que más roza, chisporroteando. Lógica pura.
public static class DriftFocus
{
	// 0,7 + 0,3 · sen(A·t) · sen(B·t), con el tiempo de la carrera y un desfase por nave
	public static float Flicker(float time, int shipId, DriftFxSettings s)
	{
		float t = time + shipId * s.FlickerShipPhase;
		return 0.7f + 0.3f * MathF.Sin(s.FlickerA * t) * MathF.Sin(s.FlickerB * t);
	}

	public static float Core(in DriftState state, float time, int shipId, DriftFxSettings s) =>
		state.Active ? s.CoreIntensity * state.D * Flicker(time, shipId, s) : 0f;

	public static float Halo(in DriftState state, float time, int shipId, DriftFxSettings s) =>
		state.Active ? s.FocusHaloIntensity * state.D * Flicker(time, shipId, s) : 0f;
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
