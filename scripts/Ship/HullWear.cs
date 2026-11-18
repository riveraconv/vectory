using System;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Ship;

// Zona del casco que se raya: costados, morro y popa
public enum WearZone
{
	Left,
	Right,
	Front,
	Rear
}

// Ajustes del daño visible del casco (spec docs/efectos-5-danos-casco.md): cuánto raya cada golpe, dónde empiezan morro
// y popa, y el hollín y las grietas de cada nivel. Lógica pura: datos.
public sealed class HullWearSettings
{
	// Rozaduras: lo que suma cada golpe en su zona (0–1, con tope en 1)
	public float GrazePerSecond = 0.15f;      // mientras roza
	public float Medium = 0.25f;
	public float Strong = 0.5f;
	public float EndFraction = 0.6f;          // más allá de esta fracción del largo hacia el morro o la popa, es esa zona

	// Hollín alrededor de los focos del humo: radio (m, a tamaño 1) y fuerza por nivel; los focos 2 y 3, más pequeños
	public float SootRadius2 = 0.9f, SootRadius3 = 1.4f;
	public float SootStrength2 = 0.8f, SootStrength3 = 0.95f;
	public float Focus2Scale = 0.75f, Focus3Scale = 0.55f;

	// Grietas incandescentes del nivel 3: hasta esta fracción del radio del hollín
	public float CrackReach = 0.45f;

	public static HullWearSettings Default => new();
}

// Un foco del hollín en los ejes del casco: centro, radio (0 = apagado) y fuerza
public readonly record struct SootSpot(Vector3 Center, float Radius, float Strength);

// Daño visible del casco que se va acumulando en la carrera (spec docs/efectos-5-danos-casco.md): rozaduras por zonas
// según dónde ha chocado, que no se borran (tampoco al recolocar). Lógica pura.
public sealed class HullWear
{
	private readonly HullWearSettings _s;
	private readonly float[] _zones = new float[4];

	public HullWear(HullWearSettings settings) => _s = settings ?? throw new ArgumentNullException(nameof(settings));

	public float this[WearZone zone] => _zones[(int)zone];

	// Sube cada vez que cambia algo: así quien lo pinta solo lo pasa cuando hace falta
	public int Version { get; private set; }

	// Las cuatro zonas en un vector (izquierda, derecha, morro, popa), para el shader
	public Vector4 Zones => new(_zones[0], _zones[1], _zones[2], _zones[3]);

	public void Reset()
	{
		Array.Clear(_zones);
		Version++;
	}

	// Lo que raya un golpe: un roce, mientras dura (por segundo); un medio o un fuerte, al empezar
	public float AmountOf(ImpactLevel level, bool isNewImpact, float dt) => level switch
	{
		ImpactLevel.Medium when isNewImpact => _s.Medium,
		ImpactLevel.Strong when isNewImpact => _s.Strong,
		ImpactLevel.Graze => _s.GrazePerSecond * MathF.Max(0f, dt),
		_ => 0f
	};

	// Contra la barrera: raya el costado de ese lado
	public void Barrier(BarrierSide side, ImpactLevel level, bool isNewImpact, float dt) =>
		Add(side == BarrierSide.Left ? WearZone.Left : WearZone.Right, AmountOf(level, isNewImpact, dt));

	// Contra otra nave: la zona sale de dónde queda el punto del contacto en los ejes de la nave (localX a la derecha,
	// localZ hacia la popa); front y back, lo que llega su caja hacia el morro y hacia la popa
	public void Contact(float localX, float localZ, float front, float back, ImpactLevel level, bool isNewImpact, float dt) =>
		Add(ZoneOf(localX, localZ, front, back, _s), AmountOf(level, isNewImpact, dt));

	// Contra otra nave, con el punto del contacto en planta y la nave en position con rumbo yaw (las convenciones de
	// ShipDynamics: adelante con yaw = 0 es −Z, yaw positivo a la izquierda)
	public void ContactAt(Vector2 point, Vector2 position, float yaw, float front, float back, ImpactLevel level,
		bool isNewImpact, float dt)
	{
		Vector2 d = point - position;
		var forward = new Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw));
		var right = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
		Contact(Vector2.Dot(d, right), -Vector2.Dot(d, forward), front, back, level, isNewImpact, dt);
	}

	public static WearZone ZoneOf(float localX, float localZ, float front, float back, HullWearSettings s)
	{
		if (localZ < -front * s.EndFraction)
			return WearZone.Front;
		if (localZ > back * s.EndFraction)
			return WearZone.Rear;
		return localX < 0f ? WearZone.Left : WearZone.Right;
	}

	// Hollín de un foco con un nivel de daño: nivel 2, solo Smoke; nivel 3, los tres (más pequeños los otros dos)
	public static SootSpot Soot(int level, int focus, in DamageSmokeFoci foci, HullWearSettings s)
	{
		if (level < 2 || (level == 2 && focus > 0) || focus < 0 || focus > 2)
			return default;
		float radius = level >= 3 ? s.SootRadius3 : s.SootRadius2;
		float strength = level >= 3 ? s.SootStrength3 : s.SootStrength2;
		float scale = focus == 0 ? 1f : focus == 1 ? s.Focus2Scale : s.Focus3Scale;
		Vector3 center = focus switch
		{
			0 => foci.Smoke - Vector3.UnitY * foci.SkinGaps.X,
			1 => foci.Smoke2 - Vector3.UnitY * foci.SkinGaps.Y,
			_ => foci.Smoke3 - Vector3.UnitY * foci.SkinGaps.Z
		};
		return new SootSpot(center, radius * scale, strength);
	}

	public static bool Cracks(int level) => level >= 3;

	private void Add(WearZone zone, float amount)
	{
		if (amount <= 0f)
			return;
		int i = (int)zone;
		float next = MathF.Min(1f, _zones[i] + amount);
		if (next == _zones[i])
			return;
		_zones[i] = next;
		Version++;
	}
}
