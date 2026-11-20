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
	private readonly float[] _damage;
	private readonly float[] _external;

	public ShipLightState(IReadOnlyList<ShipLightDef> defs, ShipLightSettings settings = null)
	{
		_defs = defs;
		_s = settings ?? ShipLightSettings.Default;
		int n = defs.Count;
		_intensity = new float[n];
		_color = new Vector3[n];
		_baseColor = new Vector3[n];
		_haloOpacity = new float[n];
		_haloSize = new float[n];
		_damage = new float[n];
		_external = new float[n];
		for (int i = 0; i < n; i++)
		{
			_baseColor[i] = defs[i].BaseColor;
			_damage[i] = 1f;
		}
		Compute(default);
	}

	public int Count => _defs.Count;
	public ShipLightDef Def(int i) => _defs[i];

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

	// Factor del parpadeo con daño de una luz (1 = normal)
	public void SetDamage(int i, float factor) => _damage[i] = factor;

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
		Compute(input.Boost);
	}

	// Recolocación: sin freno ni giro al momento
	public void Reset()
	{
		Brake = 0f;
		Turn = 0f;
		InnerSide = 0;
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
				value = d.BaseIntensity;
				enhanced = value;
			}
			else
			{
				enhanced = d.BaseIntensity * _s.Boost(d.Type);
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
				value *= _damage[i];
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
	}

	private static float Approach(float value, float target, float tau, float dt) =>
		tau <= 0f ? target : value + (target - value) * (1f - MathF.Exp(-dt / tau));
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
