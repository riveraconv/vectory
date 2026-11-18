using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Ship;

namespace Vectory.Effects;

// Lo que saca un nivel de daño (valores en HIGH): bocanadas por segundo en Smoke y en los otros dos focos, vida, radios
// (de partida y final, con su aleatorio), subida inicial, color y opacidad máxima
public sealed class DamageSmokeLevel
{
	public float Rate;              // por segundo en Smoke
	public float SecondaryRate;     // por segundo en Smoke2 y en Smoke3 (solo con tres focos)
	public float LifeMin, LifeMax;
	public float StartRadius, EndRadius;
	public float RiseMin, RiseMax;
	public Vector3 Color;
	public float Opacity;

	public DamageSmokeLevel(float rate, float secondaryRate, float lifeMin, float lifeMax, float startRadius, float endRadius,
		float riseMin, float riseMax, Vector3 color, float opacity)
	{
		Rate = rate;
		SecondaryRate = secondaryRate;
		LifeMin = lifeMin;
		LifeMax = lifeMax;
		StartRadius = startRadius;
		EndRadius = endRadius;
		RiseMin = riseMin;
		RiseMax = riseMax;
		Color = color;
		Opacity = opacity;
	}
}

// Ajustes del humo de daños (spec docs/efectos-4-humo-danos.md, 8), con los valores de la maqueta
// designs/efectos-humo-danos.html (versión 2). Distancias en m, velocidades en m/s, tiempos en s. Lógica pura: datos.
public sealed class DamageSmokeSettings
{
	public DamageSmokeLevel Medium = new(6.5f, 0f, 0.9f, 1.3f, 0.35f, 1.5f, 0.6f, 1.0f, new Vector3(0.42f, 0.44f, 0.5f), 0.42f);
	public DamageSmokeLevel Strong = new(10f, 7f, 1.0f, 1.5f, 0.45f, 2.2f, 0.8f, 1.4f, new Vector3(0.17f, 0.175f, 0.2f), 0.62f);

	// Tamaño del fuego en Smoke2 y Smoke3 (Smoke, 1: el mayor). Distintos, para que las llamas no parezcan copias
	// (decisión del usuario)
	public float Smoke2Size = 0.7f;
	public float Smoke3Size = 0.5f;

	// --- Penacho (4.2): hereda la velocidad de la nave menos un retraso hacia atrás
	public float LagFraction = 0.06f;            // de la velocidad de la nave
	public float LagMin = 1.5f, LagMax = 7f;
	public float LagScaleMin = 0.8f, LagScaleMax = 1.2f;
	public float StartJitter = 0.08f;            // ± m a los lados al nacer
	public float SideSpeed = 0.4f;               // ± m/s a los lados
	public float SideDamping = 0.8f;             // por segundo
	public float RiseDamping = 0.5f;             // por segundo
	public float SwirlSide = 0.6f, SwirlSideFrequency = 3.1f;
	public float SwirlUp = 0.12f, SwirlUpFrequency = 2.3f, SwirlUpPhase = 1.7f;
	public float PhaseMax = 10f;                 // fase del remolino, al azar entre 0 y esto

	// --- Aspecto (4.3)
	public float StartRadiusScaleMin = 0.8f, StartRadiusScaleMax = 1.1f;
	public float EndRadiusScaleMin = 0.8f, EndRadiusScaleMax = 1.2f;
	public float ShadeMin = 0.85f, ShadeMax = 1.15f;
	public float Spin = 0.7f;                    // ± rad/s
	public float FadeIn = 0.08f;
	public float FadePower = 1.4f;
	public int TextureVariants = 4;

	// --- Máximo (4.4): para todas las naves. La spec dice 300; la maqueta, 420 (decisión del usuario: 420, porque 8 naves
	// en nivel 3 llenan casi 300)
	public int MaxPuffs = 420;

	// --- B · ENVUELTO (la otra variante de la maqueta; decisión del usuario, para probarla: el penacho le parecía demasiado
	// grande). Va en los ejes de la nave y se mueve con ella: se abre por los lados del casco hacia atrás, igual a
	// cualquier velocidad, y es más corto y más pequeño. Con Wrap en false, A · PENACHO.
	public bool Wrap = true;
	public float WrapJitter = 0.1f;                // ± m al nacer, de lado y a lo largo
	public float WrapSideSpeed = 0.6f;             // ± m/s de lado
	public float WrapOutward = 0.4f;               // m/s hacia fuera, por el lado del foco
	public float WrapRiseScale = 0.8f;             // de la subida del nivel
	public float WrapBackMin = 1.5f, WrapBackMax = 3.2f;   // m/s hacia la popa
	public float WrapLifeScale = 0.85f;
	public float WrapEndRadiusScale = 0.85f;

	// --- Fuego y brasas (5 y 6)
	public DamageFireSettings Fire = new();
	public DamageEmberSettings Embers = new();

	public DamageSmokeLevel LevelOf(int level) => level switch
	{
		2 => Medium,
		>= 3 => Strong,
		_ => null
	};

	public static DamageSmokeSettings Default => new();
}

// Un foco activo: su punto en los ejes de la nave, su tamaño (el del fuego: 1 en Smoke, 0,7 y 0,5 en los otros), su
// índice y lo que hay de él a la piel de debajo (el fuego nace ahí)
public readonly record struct SmokeFocus(int Index, Vector3 Local, float Size, float SkinGap = 0f)
{
	// Base del fuego: en la piel, debajo del foco
	public Vector3 FireBase => Local - Vector3.UnitY * SkinGap;
}

// Focos del humo de daños (3): nivel 0 y 1, ninguno; 2, Smoke; 3, los tres. Lógica pura.
public static class DamageSmokeFocusRules
{
	// Cuántos focos tiene un nivel, y cada uno sin crear listas (el emisor lo llama en cada paso de cada nave)
	public static int Count(int level) => level < 2 ? 0 : level == 2 ? 1 : 3;

	public static SmokeFocus At(int index, in DamageSmokeFoci foci, DamageSmokeSettings s) => index switch
	{
		0 => new SmokeFocus(0, foci.Smoke, 1f, foci.SkinGaps.X),
		1 => new SmokeFocus(1, foci.Smoke2, s.Smoke2Size, foci.SkinGaps.Y),
		_ => new SmokeFocus(2, foci.Smoke3, s.Smoke3Size, foci.SkinGaps.Z)
	};

	public static IReadOnlyList<SmokeFocus> Active(int level, in DamageSmokeFoci foci, DamageSmokeSettings s)
	{
		if (level < 2)
			return Array.Empty<SmokeFocus>();
		var active = new SmokeFocus[Count(level)];
		for (int i = 0; i < active.Length; i++)
			active[i] = At(i, foci, s);
		return active;
	}
}

// Una bocanada del humo de daños. Carry: la velocidad heredada (la de la nave menos el retraso), que no se frena; Side
// y Rise: la de los lados (a lo largo de SideAxis) y la de subida, que sí. Age crece desde 0.
public struct DamagePuff
{
	public int ShipId;
	public int Level;
	public Vector3 Position;
	public Vector3 Carry;
	public Vector3 SideAxis;
	public float Side;
	public float Rise;
	public float Age;
	public float Life;
	public float StartRadius;
	public float EndRadius;
	public float Rotation;
	public float Spin;
	public int Texture;
	public float Shade;
	public float Phase;
	// B · ENVUELTO: en los ejes de su nave (Local), con su velocidad en esos ejes; Position no se usa
	public bool Wrap;
	public Vector3 Local;
	public Vector3 LocalVelocity;
}

// Humo de daños de todas las naves (spec docs/efectos-4-humo-danos.md, 4): emisor con un acumulador por nave y foco y
// simulación del penacho. Con el máximo lleno, las nuevas no salen. Lógica pura.
public sealed class DamageSmoke
{
	private readonly DamageSmokeSettings _s;
	private readonly Random _random;
	private readonly List<DamagePuff> _puffs = new();
	private readonly Dictionary<(int Ship, int Focus), float> _accumulators = new();

	public DamageSmoke(DamageSmokeSettings settings, Random random)
	{
		_s = settings ?? throw new ArgumentNullException(nameof(settings));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	public DamageSmokeSettings Settings => _s;
	public int Count => _puffs.Count;
	public DamagePuff this[int index] => _puffs[index];

	// Retraso del penacho (4.2): 6 % de la velocidad de la nave, entre 1,5 y 7 m/s
	public float Lag(float speed) => Math.Clamp(_s.LagFraction * MathF.Max(0f, speed), _s.LagMin, _s.LagMax);

	// Un paso de dt segundos de una nave con daño level. pose: la nave que se ve; velocity: su velocidad en el mundo;
	// quality: PARTICLES. Devuelve cuántas han salido.
	public int Emit(int shipId, int level, in DamageSmokeFoci foci, in ShipPose pose, Vector3 velocity, float quality, float dt)
	{
		DamageSmokeLevel l = _s.LevelOf(level);
		if (l == null || dt <= 0f)
		{
			if (l == null)
				ClearAccumulators(shipId);
			return 0;
		}
		Vector3 back = BackOf(velocity, pose);
		var side = new Vector3(-back.Z, 0f, back.X);
		float speed = velocity.Length();
		int added = 0;
		int focusCount = DamageSmokeFocusRules.Count(level);
		for (int f = 0; f < focusCount; f++)
		{
			SmokeFocus focus = DamageSmokeFocusRules.At(f, foci, _s);
			float rate = focus.Index == 0 ? l.Rate : l.SecondaryRate;
			var key = (shipId, focus.Index);
			_accumulators.TryGetValue(key, out float acc);
			acc += rate * quality * dt;
			while (acc >= 1f)
			{
				acc -= 1f;
				if (_puffs.Count >= _s.MaxPuffs)
					continue;
				if (_s.Wrap)
				{
					_puffs.Add(WrapPuff(shipId, level, l, focus));
					added++;
					continue;
				}
				float lag = Lag(speed) * Range(_s.LagScaleMin, _s.LagScaleMax);
				_puffs.Add(new DamagePuff
				{
					ShipId = shipId,
					Level = level,
					Position = pose.Point(focus.Local) + side * Range(-_s.StartJitter, _s.StartJitter),
					Carry = velocity + back * lag,
					SideAxis = side,
					Side = Range(-_s.SideSpeed, _s.SideSpeed),
					Rise = Range(l.RiseMin, l.RiseMax),
					Age = 0f,
					Life = Range(l.LifeMin, l.LifeMax),
					StartRadius = l.StartRadius * Range(_s.StartRadiusScaleMin, _s.StartRadiusScaleMax),
					EndRadius = l.EndRadius * Range(_s.EndRadiusScaleMin, _s.EndRadiusScaleMax),
					Rotation = Range(0f, MathF.Tau),
					Spin = Range(-_s.Spin, _s.Spin),
					Texture = Math.Min(_s.TextureVariants - 1, (int)(_random.NextSingle() * _s.TextureVariants)),
					Shade = Range(_s.ShadeMin, _s.ShadeMax),
					Phase = Range(0f, _s.PhaseMax)
				});
				added++;
			}
			_accumulators[key] = acc;
		}
		return added;
	}

	// Con dt 0 (pausa) no cambia nada
	public void Update(float dt)
	{
		if (dt <= 0f)
			return;
		float sideDamp = MathF.Max(0f, 1f - _s.SideDamping * dt);
		float riseDamp = MathF.Max(0f, 1f - _s.RiseDamping * dt);
		for (int i = _puffs.Count - 1; i >= 0; i--)
		{
			DamagePuff p = _puffs[i];
			p.Age += dt;
			if (p.Age >= p.Life)
			{
				_puffs.RemoveAt(i);
				continue;
			}
			float t = p.Age;
			float swirlSide = _s.SwirlSide * MathF.Sin(t * _s.SwirlSideFrequency + p.Phase);
			if (p.Wrap)
			{
				// Como la maqueta: el remolino solo de lado y la subida se frena
				p.Local += new Vector3(p.LocalVelocity.X + swirlSide, p.LocalVelocity.Y, p.LocalVelocity.Z) * dt;
				p.LocalVelocity.Y *= riseDamp;
				_puffs[i] = p;
				continue;
			}
			float swirlUp = _s.SwirlUp * MathF.Cos(t * _s.SwirlUpFrequency + p.Phase * _s.SwirlUpPhase);
			p.Position += (p.Carry + p.SideAxis * (p.Side + swirlSide) + Vector3.UnitY * (p.Rise + swirlUp)) * dt;
			p.Rise *= riseDamp;
			p.Side *= sideDamp;
			_puffs[i] = p;
		}
	}

	public void Clear()
	{
		_puffs.Clear();
		_accumulators.Clear();
	}

	// Dónde está una bocanada en el mundo: las de B · ENVUELTO, con la nave que se ve ahora (pose)
	public static Vector3 WorldPosition(in DamagePuff p, in ShipPose pose) => p.Wrap ? pose.Point(p.Local) : p.Position;

	// Una bocanada de B · ENVUELTO: nace en el foco y se abre hacia atrás y por su lado
	private DamagePuff WrapPuff(int shipId, int level, DamageSmokeLevel l, SmokeFocus focus) => new()
	{
		ShipId = shipId,
		Level = level,
		Wrap = true,
		Local = focus.Local + new Vector3(Range(-_s.WrapJitter, _s.WrapJitter), 0f, Range(-_s.WrapJitter, _s.WrapJitter)),
		LocalVelocity = new Vector3(Range(-_s.WrapSideSpeed, _s.WrapSideSpeed) + MathF.Sign(focus.Local.X) * _s.WrapOutward,
			Range(l.RiseMin, l.RiseMax) * _s.WrapRiseScale, Range(_s.WrapBackMin, _s.WrapBackMax)),
		Age = 0f,
		Life = Range(l.LifeMin, l.LifeMax) * _s.WrapLifeScale,
		StartRadius = l.StartRadius * Range(_s.StartRadiusScaleMin, _s.StartRadiusScaleMax),
		EndRadius = l.EndRadius * Range(_s.EndRadiusScaleMin, _s.EndRadiusScaleMax) * _s.WrapEndRadiusScale,
		Rotation = Range(0f, MathF.Tau),
		Spin = Range(-_s.Spin, _s.Spin),
		Texture = Math.Min(_s.TextureVariants - 1, (int)(_random.NextSingle() * _s.TextureVariants)),
		Shade = Range(_s.ShadeMin, _s.ShadeMax),
		Phase = Range(0f, _s.PhaseMax)
	};

	// Recolocación (7): fuera las bocanadas de esa nave
	public void Clear(int shipId)
	{
		_puffs.RemoveAll(p => p.ShipId == shipId);
		ClearAccumulators(shipId);
	}

	public float Radius(in DamagePuff p)
	{
		float u = Math.Clamp(p.Age / p.Life, 0f, 1f);
		return p.StartRadius + (p.EndRadius - p.StartRadius) * MathF.Sqrt(u);
	}

	public float Opacity(in DamagePuff p)
	{
		DamageSmokeLevel l = _s.LevelOf(p.Level);
		if (l == null || p.Life <= 0f)
			return 0f;
		float u = Math.Clamp(p.Age / p.Life, 0f, 1f);
		float fadeIn = _s.FadeIn > 0f ? MathF.Min(1f, p.Age / _s.FadeIn) : 1f;
		return l.Opacity * MathF.Pow(1f - u, _s.FadePower) * fadeIn;
	}

	// Color de la bocanada con su tono; con fuego (nivel 3), naranja al salir (4.3)
	public Vector3 Color(in DamagePuff p)
	{
		DamageSmokeLevel l = _s.LevelOf(p.Level);
		if (l == null)
			return Vector3.Zero;
		Vector3 color = l.Color * p.Shade;
		if (!DamageFire.Burns(p.Level) || p.Life <= 0f)
			return color;
		return DamageFire.Tint(color, Math.Clamp(p.Age / p.Life, 0f, 1f), _s.Fire);
	}

	public static float RotationOf(in DamagePuff p) => p.Rotation + p.Spin * p.Age;

	// Hacia atrás en horizontal: contra la velocidad de la nave, o contra su morro si va parada
	private static Vector3 BackOf(Vector3 velocity, in ShipPose pose)
	{
		var v = new Vector3(velocity.X, 0f, velocity.Z);
		if (v.LengthSquared() > 0.01f)
			return -Vector3.Normalize(v);
		var b = new Vector3(pose.Back.X, 0f, pose.Back.Z);
		return b.LengthSquared() > 1e-6f ? Vector3.Normalize(b) : Vector3.UnitZ;
	}

	private void ClearAccumulators(int shipId)
	{
		for (int f = 0; f < 3; f++)
			_accumulators.Remove((shipId, f));
	}

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();
}

// Textura de nube del humo de daños (4.3), como cloudTex de la maqueta: ruido de valor suavizado en dos rejillas
// (8 y 16 celdas) con el generador de la maqueta (Park–Miller), alfa redondeado y más claro arriba. Lógica pura.
public static class CloudTexture
{
	public const int Size = 128;
	public static readonly int[] Seeds = { 3, 17, 41, 77 };

	// Brillo y alfa (0–1) de cada píxel, fila a fila desde arriba
	public static (float Light, float Alpha)[] Pixels(int seed)
	{
		long state = seed;
		double Next()
		{
			state = state * 16807 % 2147483647;
			return state / 2147483647.0;
		}
		const int g1 = 8, g2 = 16;
		var grid1 = new double[(g1 + 1) * (g1 + 1)];
		for (int i = 0; i < grid1.Length; i++)
			grid1[i] = Next();
		var grid2 = new double[(g2 + 1) * (g2 + 1)];
		for (int i = 0; i < grid2.Length; i++)
			grid2[i] = Next();

		var pixels = new (float, float)[Size * Size];
		for (int j = 0; j < Size; j++)
			for (int i = 0; i < Size; i++)
			{
				double u = i / (double)(Size - 1), v = j / (double)(Size - 1);
				double d = Math.Sqrt((u - 0.5) * (u - 0.5) + (v - 0.5) * (v - 0.5)) * 2.0;
				double n = 0.65 * Value(grid1, g1, u, v) + 0.35 * Value(grid2, g2, u, v);
				double a = Math.Clamp((1.0 - d * d) * (0.35 + 0.9 * n) - 0.08, 0.0, 1.0);
				double light = 0.62 + 0.38 * (1.0 - v) + 0.15 * (n - 0.5);
				pixels[j * Size + i] = ((float)Math.Min(1.0, light), (float)a);
			}
		return pixels;
	}

	private static double Value(double[] grid, int n, double u, double v)
	{
		double fx = u * n, fy = v * n;
		int i = (int)Math.Floor(fx), j = (int)Math.Floor(fy);
		double a = fx - i, b = fy - j;
		double sa = a * a * (3 - 2 * a), sb = b * b * (3 - 2 * b);
		double Q(int ii, int jj) => grid[Math.Min(n, jj) * (n + 1) + Math.Min(n, ii)];
		double top = Q(i, j) + (Q(i + 1, j) - Q(i, j)) * sa;
		double bottom = Q(i, j + 1) + (Q(i + 1, j + 1) - Q(i, j + 1)) * sa;
		return top + (bottom - top) * sb;
	}
}
