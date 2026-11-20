using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Luces de la nave (spec docs/efectos-8-luces.md): tipos de las piezas vivas, ajustes, entradas y estado de las luces de
// una nave. Lógica pura: el nodo HullLights solo pone lo que sale de aquí en los materiales y en los halos.

// Tipo de una pieza viva (tabla 4.1): decide su realce, su halo, si hace de freno y si titila con el daño
public enum ShipLightType
{
	Profile,   // bandas de perfil de VECTOR
	Nose,      // luces del morro de VECTOR
	Fin,       // luces de las aletas de VECTOR
	Brake,     // pilotos traseros y luces de freno
	Nav,       // luces de posición de BLADE
	Form,      // tiras de formación de BLADE
	TailWhite, // cola blanca de BLADE
	Amber,     // balizas ámbar de HAMMER
	Work,      // focos de trabajo de HAMMER
	Head,      // faros
	Retro,     // bocas del retro
	Strobe,    // anticolisión de BLADE
	Beacon     // balizas del techo de HAMMER
}

public static class ShipLightTypes
{
	// El nombre del manifiesto ("tailw", "beacon"...); null si no se conoce
	public static ShipLightType? Parse(string name) => name switch
	{
		"profile" => ShipLightType.Profile,
		"nose" => ShipLightType.Nose,
		"fin" => ShipLightType.Fin,
		"brake" => ShipLightType.Brake,
		"nav" => ShipLightType.Nav,
		"form" => ShipLightType.Form,
		"tailw" => ShipLightType.TailWhite,
		"amber" => ShipLightType.Amber,
		"work" => ShipLightType.Work,
		"head" => ShipLightType.Head,
		"retro" => ShipLightType.Retro,
		"strobe" => ShipLightType.Strobe,
		"beacon" => ShipLightType.Beacon,
		_ => null
	};

	// Las que llevan su propia intensidad (su lógica de siempre): solo ganan el halo
	public static bool IsExternal(ShipLightType type) => type is ShipLightType.Strobe or ShipLightType.Beacon;

	// Las que titilan con el daño (tabla 4.1): ni la anticolisión, ni las balizas, ni los faros, ni las bocas del retro
	public static bool Flickers(ShipLightType type) =>
		type is not (ShipLightType.Strobe or ShipLightType.Beacon or ShipLightType.Head or ShipLightType.Retro);
}

// Una luz de la nave: pieza, tipo, lado (−1 izquierda, 1 derecha, 0 centro), centro y hacia dónde sale su halo (cero si
// no lleva: las tiras), intensidad y color de partida (los del manifiesto o la pintura) y si es una luz de freno nueva
// (BLADE y HAMMER), que en reposo va casi apagada
public sealed record ShipLightDef(string Name, ShipLightType Type, int Side, Vector3 Center, Vector3 HaloOutward,
	float BaseIntensity, Vector3 BaseColor, bool NewBrake)
{
	public bool HasHalo => HaloOutward != Vector3.Zero;

	// Lado por la x del centro (4.1): izquierda si x < 0, derecha si x > 0, ninguno en el centro
	public static int SideOf(float x) => x < -0.01f ? -1 : x > 0.01f ? 1 : 0;
}

// Ajustes de las luces (sección 12); los colores, aquí como los de las toberas (la lógica es pura)
public sealed class ShipLightSettings
{
	public static readonly ShipLightSettings Default = new();

	// Realce por tipo (tabla 4.1)
	public float ProfileBoost = 2.4f, NoseBoost = 1.7f, FinBoost = 1.7f, BrakeBoost = 1.7f, NavBoost = 1.7f;
	public float FormBoost = 2.8f, TailWhiteBoost = 1.5f, AmberBoost = 1.7f, WorkBoost = 1.8f, HeadBoost = 1f;

	// Tamaño del halo por tipo (m)
	public float NoseHalo = 0.55f, FinHalo = 0.60f, BrakeHalo = 0.85f, NavHalo = 0.65f, TailWhiteHalo = 0.75f;
	public float AmberHalo = 0.50f, WorkHalo = 0.55f, HeadHalo = 0.70f, StrobeHalo = 1.30f, BeaconHalo = 0.70f;

	// Halo (5.2): opacidad = limitar(HaloOpacity × k × I; 0; HaloMaxOpacity), tamaño × (0,75 + 0,25 × mín(2; I / Irealzada))
	public float HaloOpacity = 0.32f, HaloScale = 0.42f, HaloMaxOpacity = 1.4f, HaloMinIntensity = 0.05f;
	public float HaloOffset = 0.055f;
	public float HaloSizeBase = 0.75f, HaloSizeGrow = 0.25f, HaloGrowCap = 2f;

	// Freno (6.1 y 6.2)
	public float BrakeRise = 0.04f, BrakeFall = 0.15f, BrakeMinSpeed = 1f, BrakeGain = 2.2f;
	public Vector3 BoostColor = new(1f, 0x8A / 255f, 0x1E / 255f);   // #FF8A1E
	public float BoostGain = 1.4f;
	public float NewBrakeRest = 0.35f;

	// Giro (7.1)
	public float TurnTau = 0.15f, TurnThrottle = 1.3f, TurnDrift = 0.5f, TurnInner = 0.7f;

	// Luz de freno sobre el casco (6.3): energía EB × (base + kB + bt × …), 0,35 m por detrás de las luces de freno
	public float HullBrakeEnergy = 1f, HullBrakeBase = 0.15f, HullBrakeBoost = 0.6f;
	public float HullBrakeBehind = 0.35f, HullBrakeRange = 2.5f, HullBrakeAttenuation = 1.6f;

	// Luz en pista (7.3), en unidades de la maqueta: laterales 8 × 1,4 × giro; trasera 1,2 + 13·kB + 9·bt; toberas
	// 1 + 3,5·P + 8·bt. Se apagan con la altura (a 3,5 m, nada). Godot: × TrackEnergyScale (a ojo, con el suelo)
	public float TrackSide = 8f, TrackSideBoost = 1.4f, TrackTurnInner = 1.5f;
	public float TrackRear = 1.2f, TrackRearBrake = 13f, TrackRearBoost = 9f;
	public float TrackNozzle = 1f, TrackNozzlePower = 3.5f, TrackNozzleBoost = 8f, TrackGridNozzle = 0.4f;
	public float TrackFadeHeight = 3.5f, TrackRange = 16f, TrackNozzleRange = 18f, TrackAttenuation = 2f;
	public float TrackEnergyScale = 0.25f;

	// Faros (7.4): el foco 0,9 m por encima y 0,8 m por detrás de su vacío, apuntando a la pista 15 m por delante; las
	// intensidades de la tabla, en unidades de la maqueta (Godot: × SpotEnergyScale, a ojo con el suelo)
	public float SpotRise = 0.9f, SpotBack = 0.8f, SpotReach = 15f, SpotRange = 70f, SpotAttenuation = 2f;
	public float SpotEnergyScale = 1f / 220f;
	public float WorkLensGain = 1.6f;   // las lentes de los focos de trabajo de HAMMER, con los faros

	// Haz visible (7.4): cono de 10 m, de 5 cm en la lente a tan(semiángulo) × 0,75 × 10 al final
	public float BeamLength = 10f, BeamStartRadius = 0.05f, BeamWidth = 0.75f;
	public float BeamOpacity = 0.07f, WorkBeamOpacity = 0.04f, BeamFadePower = 2.4f;
	public float BeamInsideBase = 0.6f, BeamInsideGrow = 0.3f, BeamCockpit = 0.5f, BeamWithoutBloom = 0.5f;
	public Vector3 SpotColor = new(1f, 0xF1 / 255f, 0xDC / 255f);   // #FFF1DC, el de los focos y su haz

	// Retro (7.5): boca 0,15 + 4·R; luz ER × 2 × R a 0,25 × su ancho por delante; trampilla 0,75 rad con mín(1; 1,4·R)
	public float RetroMouthBase = 0.15f, RetroMouthGain = 4f;
	public float RetroLightEnergy = 1f, RetroLightGain = 2f, RetroLightAhead = 0.25f, RetroLightRange = 3.5f;
	public float RetroLightAttenuation = 2f;
	public float FlapMax = 0.75f, FlapSpeed = 1.4f;

	// Parpadeo con daño (9.1), por nivel 1, 2 y 3: apagones por segundo y su duración (s)
	public float[] BlackoutRate = { 0.30f, 0.75f, 1.30f };
	public float[] BlackoutMin = { 0.07f, 0.10f, 0.12f };
	public float[] BlackoutMax = { 0.16f, 0.32f, 0.50f };
	public float StutterTime = 0.16f, StutterStep = 0.035f, StutterLow = 0.15f, StutterHigh = 1.15f;
	public float HumBase = 0.88f, HumAmplitude = 0.12f, HumFrequency = 41f, HumPhase = 3f;
	public float DeadFraction = 0.30f, ReviveMin = 1.2f, ReviveMax = 3.2f, ReviveTime = 0.07f, ReviveLevel = 1.3f;
	public int SparksPerEvent = 3;

	public float Boost(ShipLightType type) => type switch
	{
		ShipLightType.Profile => ProfileBoost,
		ShipLightType.Nose => NoseBoost,
		ShipLightType.Fin => FinBoost,
		ShipLightType.Brake => BrakeBoost,
		ShipLightType.Nav => NavBoost,
		ShipLightType.Form => FormBoost,
		ShipLightType.TailWhite => TailWhiteBoost,
		ShipLightType.Amber => AmberBoost,
		ShipLightType.Work => WorkBoost,
		ShipLightType.Head => HeadBoost,
		_ => 1f
	};

	public float Halo(ShipLightType type) => type switch
	{
		ShipLightType.Nose => NoseHalo,
		ShipLightType.Fin => FinHalo,
		ShipLightType.Brake => BrakeHalo,
		ShipLightType.Nav => NavHalo,
		ShipLightType.TailWhite => TailWhiteHalo,
		ShipLightType.Amber => AmberHalo,
		ShipLightType.Work => WorkHalo,
		ShipLightType.Head => HeadHalo,
		ShipLightType.Strobe => StrobeHalo,
		ShipLightType.Beacon => BeaconHalo,
		_ => 0f
	};
}

// Lo que hace la nave, para sus luces: si frena, su velocidad y punta, el giro suavizado (τ 0,12 s, el de las toberas;
// positivo a la izquierda), si derrapa y el boost de la llama (bt)
public struct ShipLightInput
{
	public bool Braking;
	public float Speed;
	public float TopSpeed;
	public float Steer;
	public bool Drifting;
	public float Boost;
	public float Power;     // potencia de la llama (P)
	public float Retro;     // fuerza del retro (R)
	public float Altitude;  // altura sobre la pista por encima de la de vuelo (m)
	public bool OnGrid;     // en la parrilla, antes del GO
	public int DamageLevel; // 0–3
	public bool TimeTrial;  // sin parpadeo con daño
	public bool Passable;   // atravesable tras recolocarla: sin luz en pista, luz de freno en el casco ni faros
}

// Lo que la nave sabe de la carrera, para sus luces (secciones 7.3, 9 y 11)
public struct ShipLightContext
{
	public float Altitude;   // altura sobre la pista por encima de la de vuelo (m)
	public bool OnGrid;      // en la parrilla, antes del GO
	public bool Passable;    // atravesable tras recolocarla
	public bool TimeTrial;
}

// Estado de las luces de una nave (secciones 5, 6 y 7.1): freno kB, carga del giro G y, de cada luz, su intensidad y
// color y la opacidad y el tamaño de su halo. Sin delta no cambian kB ni G.
public sealed class ShipLightState
{
	private readonly IReadOnlyList<ShipLightDef> _defs;
	private readonly ShipLightSettings _s;
	private readonly float[] _intensity;
	private readonly Vector3[] _color;
	private readonly Vector3[] _baseColor;
	private readonly float[] _haloOpacity;
	private readonly float[] _haloSize;
	private readonly float[] _external;
	private readonly int _sideLeft = -1, _sideRight = -1, _brakeRed = -1;
	private Vector3 _nozzleColor = Vector3.One, _nozzleBoostColor = Vector3.One;
	private ShipLightInput _last;

	// shipSeed: elige siempre las mismas luces muertas de esta nave; random: el azar de los apagones
	public ShipLightState(IReadOnlyList<ShipLightDef> defs, ShipLightSettings settings = null, int shipSeed = 0,
		Random random = null)
	{
		_defs = defs;
		_s = settings ?? ShipLightSettings.Default;
		random ??= new Random(shipSeed);
		Damage = new ShipLightDamage(defs, _s, shipSeed, random);
		int n = defs.Count;
		_intensity = new float[n];
		_color = new Vector3[n];
		_baseColor = new Vector3[n];
		_haloOpacity = new float[n];
		_haloSize = new float[n];
		_external = new float[n];
		Vector3 brakeSum = Vector3.Zero, newBrake = Vector3.Zero;
		int brakes = 0;
		for (int i = 0; i < n; i++)
		{
			ShipLightDef d = defs[i];
			_baseColor[i] = d.BaseColor;
			// colores de la luz en pista lateral: el acento de VECTOR, las de posición de BLADE, el ámbar de HAMMER
			if (d.Type is ShipLightType.Profile or ShipLightType.Nav or ShipLightType.Amber)
			{
				if (d.Side < 0 && _sideLeft < 0)
					_sideLeft = i;
				if (d.Side > 0 && _sideRight < 0)
					_sideRight = i;
			}
			if (d.Type == ShipLightType.Brake)
			{
				if (_brakeRed < 0)
					_brakeRed = i;
				brakeSum += d.Center;
				brakes++;
				if (d.NewBrake)
					newBrake = d.Center;
			}
		}
		// luz de freno en el casco: detrás de la luz de freno nueva o, en VECTOR, del medio de sus pilotos
		Vector3 brakeCenter = newBrake != Vector3.Zero ? newBrake : brakes > 0 ? brakeSum / brakes : Vector3.Zero;
		HullBrakePosition = brakeCenter + new Vector3(0f, 0f, _s.HullBrakeBehind);
		HasBrakeLights = brakes > 0;
		Compute(default);
	}

	// Dónde va la luz de freno sobre el casco (coordenadas de la nave) y si la nave tiene luces de freno
	public Vector3 HullBrakePosition { get; }
	public bool HasBrakeLights { get; }

	// Luz en pista (7.3), en unidades de la maqueta, y sus colores
	public float TrackLeft { get; private set; }
	public float TrackRight { get; private set; }
	public float TrackRear { get; private set; }
	public float TrackNozzle { get; private set; }
	public Vector3 TrackLeftColor { get; private set; }
	public Vector3 TrackRightColor { get; private set; }
	public Vector3 TrackRearColor { get; private set; }
	public Vector3 TrackNozzleColor { get; private set; }

	// Luz de freno sobre el casco (6.3): energía y color
	public float HullBrakeEnergy { get; private set; }
	public Vector3 HullBrakeColor { get; private set; }

	// Faros (7.4): factor de los focos y del haz (1 con todo encendido; el encendido y el daño, en la etapa 4)
	public float HeadlightFactor { get; private set; } = 1f;

	// Retro (7.5): energía de la luz de cada tobera y ángulo de la trampilla (rad)
	public float RetroLight { get; private set; }
	public float FlapAngle { get; private set; }

	// Color medio de la llama de la nave, sin y con boost, para la luz en pista de las toberas
	public void SetNozzleColors(Vector3 normal, Vector3 boost)
	{
		_nozzleColor = normal;
		_nozzleBoostColor = boost;
	}

	public int Count => _defs.Count;
	public ShipLightDef Def(int i) => _defs[i];

	// Parpadeo con daño (9)
	public ShipLightDamage Damage { get; }

	// Freno (0–1) y carga del giro (0–1); lado de dentro: −1 izquierda, 1 derecha, 0 todavía ninguno
	public float Brake { get; private set; }
	public float Turn { get; private set; }
	public int InnerSide { get; private set; }

	public float Intensity(int i) => _intensity[i];
	public Vector3 Color(int i) => _color[i];
	public float HaloOpacity(int i) => _haloOpacity[i];
	public float HaloSize(int i) => _haloSize[i];

	// Color de partida (la pintura de VECTOR lo cambia)
	public void SetBaseColor(int i, Vector3 color) => _baseColor[i] = color;

	// Intensidad de las que llevan su propia lógica (anticolisión y balizas)
	public void SetExternal(int i, float intensity) => _external[i] = intensity;

	public void Update(float dt, in ShipLightInput input)
	{
		if (dt > 0f)
		{
			float target = input.Braking && input.Speed > _s.BrakeMinSpeed ? 1f : 0f;
			Brake = Approach(Brake, target, target > Brake ? _s.BrakeRise : _s.BrakeFall, dt);

			float th = input.TopSpeed > 0f ? input.Speed / input.TopSpeed : 0f;
			float load = MathF.Abs(input.Steer) * MathF.Min(1f, _s.TurnThrottle * th) + (input.Drifting ? _s.TurnDrift : 0f);
			Turn = Approach(Turn, MathF.Min(1f, load), _s.TurnTau, dt);
			if (input.Steer > 0f)
				InnerSide = -1;
			else if (input.Steer < 0f)
				InnerSide = 1;
		}
		Damage.Update(dt, input.TimeTrial ? 0 : input.DamageLevel);
		_last = input;
		Compute(input.Boost);
	}

	// Recolocación: sin freno ni giro al momento
	public void Reset()
	{
		Brake = 0f;
		Turn = 0f;
		InnerSide = 0;
		Damage.Reset();
		_last.Braking = false;
		_last.Steer = 0f;
		_last.Retro = 0f;
		_last.Boost = 0f;
		Compute(0f);
	}

	private void Compute(float boost)
	{
		float bt = Math.Clamp(boost, 0f, 1f);
		for (int i = 0; i < _defs.Count; i++)
		{
			ShipLightDef d = _defs[i];
			Vector3 color = _baseColor[i];
			float enhanced;
			float value;
			if (ShipLightTypes.IsExternal(d.Type))
			{
				value = _external[i];
				enhanced = value;
			}
			else if (d.Type == ShipLightType.Retro)
			{
				// la boca del retro: no la tocan el realce, el giro ni el daño
				value = _s.RetroMouthBase + _s.RetroMouthGain * Math.Clamp(_last.Retro, 0f, 1f);
				enhanced = value;
			}
			else
			{
				enhanced = d.BaseIntensity * _s.Boost(d.Type) * (d.Type == ShipLightType.Work ? _s.WorkLensGain : 1f);
				value = enhanced;
				if (d.Type == ShipLightType.Brake)
				{
					value *= (1f + _s.BrakeGain * Brake) * (1f + _s.BoostGain * bt);
					color = Vector3.Lerp(color, _s.BoostColor, bt);
					if (d.NewBrake)
						value *= _s.NewBrakeRest + (1f - _s.NewBrakeRest) * MathF.Max(Brake, bt);
				}
				if (d.Side != 0 && d.Side == InnerSide)
					value *= 1f + _s.TurnInner * Turn;
				value *= Damage.Factor(i);
			}
			_intensity[i] = value;
			_color[i] = color;

			float halo = d.HasHalo ? _s.Halo(d.Type) : 0f;
			if (halo > 0f && value >= _s.HaloMinIntensity)
			{
				_haloOpacity[i] = Math.Clamp(_s.HaloOpacity * _s.HaloScale * value, 0f, _s.HaloMaxOpacity);
				float ratio = enhanced > 0f ? value / enhanced : 1f;
				_haloSize[i] = halo * (_s.HaloSizeBase + _s.HaloSizeGrow * MathF.Min(_s.HaloGrowCap, ratio));
			}
			else
			{
				_haloOpacity[i] = 0f;
				_haloSize[i] = 0f;
			}
		}
		ComputeWorld(bt);
	}

	// Luz en pista, luz de freno en el casco y retro (6.3, 7.3 y 7.5)
	private void ComputeWorld(float bt)
	{
		// atravesable: sin luz en pista, luz de freno en el casco ni faros (11)
		float pw = _last.Passable ? 0f : 1f;
		float height = MathF.Max(0f, 1f - MathF.Max(0f, _last.Altitude) / _s.TrackFadeHeight) * pw;
		float side = _s.TrackSide * _s.TrackSideBoost * height;
		TrackLeft = side * (InnerSide < 0 ? 1f + _s.TrackTurnInner * Turn : 1f) * Damage.SideAverage(-1);
		TrackRight = side * (InnerSide > 0 ? 1f + _s.TrackTurnInner * Turn : 1f) * Damage.SideAverage(1);
		TrackRear = (_s.TrackRear + _s.TrackRearBrake * Brake + _s.TrackRearBoost * bt) * height;
		float power = Math.Clamp(_last.Power, 0f, 1f);
		TrackNozzle = (_s.TrackNozzle + _s.TrackNozzlePower * power + _s.TrackNozzleBoost * bt) * height
			* (_last.OnGrid ? _s.TrackGridNozzle : 1f);
		TrackLeftColor = _sideLeft >= 0 ? _baseColor[_sideLeft] : Vector3.One;
		TrackRightColor = _sideRight >= 0 ? _baseColor[_sideRight] : Vector3.One;
		Vector3 red = _brakeRed >= 0 ? _baseColor[_brakeRed] : new Vector3(1f, 0.165f, 0.118f);
		TrackRearColor = Vector3.Lerp(red, _s.BoostColor, bt);
		TrackNozzleColor = Vector3.Lerp(_nozzleColor, _nozzleBoostColor, bt);

		HullBrakeEnergy = HasBrakeLights
			? _s.HullBrakeEnergy * (_s.HullBrakeBase + Brake + _s.HullBrakeBoost * bt) * pw * Damage.BrakeAverage
			: 0f;
		HeadlightFactor = pw * Damage.Average;
		HullBrakeColor = TrackRearColor;

		float retro = Math.Clamp(_last.Retro, 0f, 1f);
		RetroLight = _s.RetroLightEnergy * _s.RetroLightGain * retro;
		FlapAngle = _s.FlapMax * MathF.Min(1f, _s.FlapSpeed * retro);
	}

	private static float Approach(float value, float target, float tau, float dt) =>
		tau <= 0f ? target : value + (target - value) * (1f - MathF.Exp(-dt / tau));
}

// Parpadeo de las luces con daño (sección 9), solo con daño 1–3: apagones sorteados que vuelven tartamudeando (y, desde
// el nivel 2, con una chispa), zumbido de fondo desde el nivel 2 y, en el 3, el 30 % de las luces muertas (siempre las
// mismas de la nave), que reviven a ratos. Un factor fd por luz (1 = normal). Sin delta no cambia nada.
public sealed class ShipLightDamage
{
	private readonly IReadOnlyList<ShipLightDef> _defs;
	private readonly ShipLightSettings _s;
	private readonly Random _random;
	private readonly bool[] _flickers;
	private readonly bool[] _dead;
	private readonly float[] _fd;
	private readonly float[] _blackout;
	private readonly float[] _stutter;
	private readonly float[] _reviveWait;
	private readonly float[] _revive;
	private readonly int[] _sparks;
	private float _time;

	public ShipLightDamage(IReadOnlyList<ShipLightDef> defs, ShipLightSettings settings, int shipSeed, Random random)
	{
		_defs = defs;
		_s = settings;
		_random = random;
		int n = defs.Count;
		_flickers = new bool[n];
		_fd = new float[n];
		_blackout = new float[n];
		_stutter = new float[n];
		_reviveWait = new float[n];
		_revive = new float[n];
		_sparks = new int[n];
		for (int i = 0; i < n; i++)
		{
			_flickers[i] = ShipLightTypes.Flickers(defs[i].Type);
			_fd[i] = 1f;
		}
		_dead = DeadLights(_flickers, shipSeed, settings.DeadFraction);
		Average = SideLeft = SideRight = BrakeAverage = 1f;
	}

	public float Factor(int i) => _fd[i];
	public bool IsDead(int i) => _dead[i];
	public bool Flickers(int i) => _flickers[i];

	// Luces que han soltado una chispa en el último paso (índices)
	public int SparkCount { get; private set; }
	public int Spark(int k) => _sparks[k];

	// Daño medio (9.4): media de mín(1; fd) de las que titilan, de toda la nave, de un lado o de las de freno
	public float Average { get; private set; }
	public float BrakeAverage { get; private set; }
	private float SideLeft { get; set; }
	private float SideRight { get; set; }
	public float SideAverage(int side) => side < 0 ? SideLeft : SideRight;

	public void Update(float dt, int level)
	{
		SparkCount = 0;
		if (dt <= 0f)
			return;
		_time += dt;
		level = Math.Clamp(level, 0, 3);
		for (int i = 0; i < _fd.Length; i++)
		{
			if (!_flickers[i] || level == 0)
			{
				_fd[i] = 1f;
				continue;
			}
			if (level == 3 && _dead[i])
			{
				// muerta: revive a ratos un instante, con chispa
				_reviveWait[i] -= dt;
				if (_revive[i] > 0f)
					_revive[i] -= dt;
				if (_reviveWait[i] <= 0f)
				{
					_reviveWait[i] = Range(_s.ReviveMin, _s.ReviveMax);
					_revive[i] = _s.ReviveTime;
					AddSpark(i);
				}
				_fd[i] = _revive[i] > 0f ? _s.ReviveLevel : 0f;
				continue;
			}
			if (_blackout[i] > 0f)
			{
				_blackout[i] -= dt;
				if (_blackout[i] > 0f)
				{
					_fd[i] = 0f;
					continue;
				}
				// vuelve: tartamudea desde este mismo paso y, desde el nivel 2, suelta una chispa
				_stutter[i] = _s.StutterTime;
				if (level >= 2)
					AddSpark(i);
			}
			if (_stutter[i] > 0f)
			{
				float elapsed = _s.StutterTime - _stutter[i];
				_fd[i] = (int)(elapsed / _s.StutterStep) % 2 == 0 ? _s.StutterLow : _s.StutterHigh;
				_stutter[i] -= dt;
				continue;
			}
			if (_random.NextSingle() < _s.BlackoutRate[level - 1] * dt)
			{
				_blackout[i] = Range(_s.BlackoutMin[level - 1], _s.BlackoutMax[level - 1]);
				_fd[i] = 0f;
				continue;
			}
			_fd[i] = level >= 2 ? _s.HumBase + _s.HumAmplitude * MathF.Sin(_s.HumFrequency * _time + _s.HumPhase * i) : 1f;
		}
		Averages();
	}

	// Recolocación: sin apagones, tartamudeos ni chispas; las muertas siguen muertas
	public void Reset()
	{
		Array.Clear(_blackout);
		Array.Clear(_stutter);
		Array.Clear(_revive);
		SparkCount = 0;
		for (int i = 0; i < _fd.Length; i++)
			if (!(_dead[i] && _fd[i] == 0f))
				_fd[i] = 1f;
		Averages();
	}

	private void AddSpark(int i)
	{
		if (SparkCount < _sparks.Length)
			_sparks[SparkCount++] = i;
	}

	private void Averages()
	{
		float all = 0f, left = 0f, right = 0f, brake = 0f;
		int nAll = 0, nLeft = 0, nRight = 0, nBrake = 0;
		for (int i = 0; i < _fd.Length; i++)
		{
			if (!_flickers[i])
				continue;
			float f = MathF.Min(1f, _fd[i]);
			all += f;
			nAll++;
			if (_defs[i].Side < 0) { left += f; nLeft++; }
			if (_defs[i].Side > 0) { right += f; nRight++; }
			if (_defs[i].Type == ShipLightType.Brake) { brake += f; nBrake++; }
		}
		Average = nAll > 0 ? all / nAll : 1f;
		SideLeft = nLeft > 0 ? left / nLeft : 1f;
		SideRight = nRight > 0 ? right / nRight : 1f;
		BrakeAverage = nBrake > 0 ? brake / nBrake : 1f;
	}

	private float Range(float a, float b) => a + (b - a) * _random.NextSingle();

	// Las muertas del nivel 3: el 30 % (redondeado) de las que titilan, por un hash fijo del número de la luz y de la nave
	public static bool[] DeadLights(IReadOnlyList<bool> flickers, int shipSeed, float fraction)
	{
		var candidates = new List<(uint Hash, int Index)>();
		for (int i = 0; i < flickers.Count; i++)
			if (flickers[i])
				candidates.Add((Hash((uint)i, (uint)shipSeed), i));
		candidates.Sort((a, b) => a.Hash != b.Hash ? a.Hash.CompareTo(b.Hash) : a.Index.CompareTo(b.Index));
		int count = (int)MathF.Round(candidates.Count * fraction, MidpointRounding.AwayFromZero);
		var dead = new bool[flickers.Count];
		for (int k = 0; k < count; k++)
			dead[candidates[k].Index] = true;
		return dead;
	}

	private static uint Hash(uint a, uint b)
	{
		uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u;
		h ^= h >> 15;
		h *= 0x2C1B3C6Du;
		h ^= h >> 12;
		return h;
	}
}

// Un foco de la nave (7.4): dónde está su lente (el vacío Spot_Head_* o el centro de una pareja de focos de trabajo de
// HAMMER), su dirección en planta (x; z), su semiángulo (rad), su intensidad en unidades de la maqueta y si es de trabajo
public readonly record struct ShipSpotDef(Vector3 Lens, Vector2 Direction, float HalfAngle, float Intensity, bool Work);

// Los focos de cada casco (tabla 7.4) y su geometría: el foco por encima y por detrás de la lente, apuntando a la pista
// por delante; el haz, de la lente hacia ese punto. Lógica pura.
public static class ShipSpots
{
	public static List<ShipSpotDef> From(HullManifest manifest, HullStyle style)
	{
		var spots = new List<ShipSpotDef>();
		(float half, float dx, float intensity) = style switch
		{
			HullStyle.Military => (0.19f, 0.08f, 1320f),
			HullStyle.Industrial => (0.18f, 0.04f, 1800f),
			_ => (0.20f, 0.07f, 1320f)
		};
		foreach ((string name, int k) in new[] { ("Spot_Head_L", -1), ("Spot_Head_R", 1) })
		{
			HullManifestEmpty empty = manifest.Empty(name);
			if (empty != null)
				spots.Add(new ShipSpotDef(empty.Point, new Vector2(k * dx, -1f), half, intensity, false));
		}
		if (style == HullStyle.Industrial)
		{
			// cada pareja de focos de trabajo del techo (x ±0,16) hace de luz larga
			foreach ((string a, string b, int k) in new[] { ("Light_Work_0", "Light_Work_1", -1), ("Light_Work_2", "Light_Work_3", 1) })
			{
				float[] ca = manifest.Piece(a)?.Center, cb = manifest.Piece(b)?.Center;
				if (ca is { Length: 3 } && cb is { Length: 3 })
				{
					var lens = (new Vector3(ca[0], ca[1], ca[2]) + new Vector3(cb[0], cb[1], cb[2])) / 2f;
					spots.Add(new ShipSpotDef(lens, new Vector2(k * 0.12f, -1f), 0.32f, 840f, true));
				}
			}
		}
		return spots;
	}

	// Punto de la pista al que apunta: SpotReach por delante de la lente en su dirección en planta, a la altura de la
	// pista (trackY: la y de la pista en los ejes de la nave)
	public static Vector3 Target(in ShipSpotDef spot, ShipLightSettings s, float trackY)
	{
		Vector2 d = Vector2.Normalize(spot.Direction);
		return new Vector3(spot.Lens.X + d.X * s.SpotReach, trackY, spot.Lens.Z + d.Y * s.SpotReach);
	}

	// Dónde va la luz: por encima y por detrás de la lente
	public static Vector3 LightPosition(in ShipSpotDef spot, ShipLightSettings s) =>
		spot.Lens + new Vector3(0f, s.SpotRise, s.SpotBack);

	// Eje del haz: de la lente hacia el punto de la pista
	public static Vector3 BeamAxis(in ShipSpotDef spot, ShipLightSettings s, float trackY) =>
		Vector3.Normalize(Target(spot, s, trackY) - spot.Lens);

	// Radio del haz al final
	public static float BeamEndRadius(in ShipSpotDef spot, ShipLightSettings s) =>
		MathF.Tan(spot.HalfAngle) * s.BeamWidth * s.BeamLength;

	// Opacidad de partida del haz (sin la cámara)
	public static float BeamOpacity(in ShipSpotDef spot, ShipLightSettings s) => spot.Work ? s.WorkBeamOpacity : s.BeamOpacity;

	// El haz se apaga con la cámara dentro del cono, mirando al faro de frente (si no, llena la pantalla): con a la
	// distancia de la cámara a lo largo del eje y d su distancia al eje, limitar((d − tan × 0,75 × a) / (0,6 + 0,3 × a))
	public static float BeamCameraFactor(Vector3 camera, Vector3 lens, Vector3 axis, float halfAngle, ShipLightSettings s)
	{
		Vector3 rel = camera - lens;
		float a = Vector3.Dot(rel, axis);
		if (a <= 0f)
			return 1f;
		float d = (rel - axis * a).Length();
		return Math.Clamp((d - MathF.Tan(halfAngle) * s.BeamWidth * a) / (s.BeamInsideBase + s.BeamInsideGrow * a), 0f, 1f);
	}
}

// Dónde van las cuatro luces en pista de cada nave (tabla 7.3), en sus ejes: laterales izquierda y derecha, trasera y de
// las toberas (y negativa: debajo del casco, cerca del suelo)
public static class ShipTrackLights
{
	public static (Vector3 Left, Vector3 Right, Vector3 Rear, Vector3 Nozzle) Positions(HullStyle style) => style switch
	{
		HullStyle.Military => (new(-1.6f, -0.30f, 2.6f), new(1.6f, -0.30f, 2.6f), new(0f, -0.30f, 3.5f), new(0f, -0.35f, 6.8f)),
		HullStyle.Industrial => (new(-2.4f, -0.40f, -2.2f), new(2.4f, -0.40f, -2.2f), new(0f, -0.40f, 2.9f), new(0f, -0.45f, 6.4f)),
		_ => (new(-2.2f, -0.30f, 1.6f), new(2.2f, -0.30f, 1.6f), new(0f, -0.30f, 3.4f), new(0f, -0.35f, 6.8f))
	};
}

// Las luces de un casco, desde su manifiesto (entrada "lights": tipo y hacia dónde sale el halo; la pieza da el centro,
// la intensidad y el color de partida). Las que no se pueden leer se saltan.
public static class ShipLightCatalog
{
	public const string NewBrakeName = "Light_Brake";

	public static List<ShipLightDef> From(HullManifest manifest)
	{
		var defs = new List<ShipLightDef>();
		foreach (HullManifestLight light in manifest.Lights)
		{
			ShipLightType? type = ShipLightTypes.Parse(light.Type);
			CockpitManifestPiece piece = manifest.Piece(light.Name);
			if (type == null || piece?.Center is not { Length: 3 } c)
				continue;
			var center = new Vector3(c[0], c[1], c[2]);
			Vector3 halo = light.Halo is { Length: 3 } h ? new Vector3(h[0], h[1], h[2]) : Vector3.Zero;
			defs.Add(new ShipLightDef(light.Name, type.Value, ShipLightDef.SideOf(center.X), center, halo,
				piece.Intensity ?? 0f, Rgb(piece.Emission), light.Name == NewBrakeName));
		}
		return defs;
	}

	// "#RRGGBB" → (r, g, b) de 0 a 1; blanco si no se puede leer
	public static Vector3 Rgb(string hex)
	{
		if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#'
			|| !uint.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out uint v))
			return Vector3.One;
		return new Vector3((v >> 16 & 0xFF) / 255f, (v >> 8 & 0xFF) / 255f, (v & 0xFF) / 255f);
	}
}
