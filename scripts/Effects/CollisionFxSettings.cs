using System.Numerics;

namespace Vectory.Effects;

// Chispas de un nivel de contacto (spec docs/efectos-1-choques.md, 4.1). Count: de golpe (medio y fuerte); Rate: por
// segundo mientras dura el roce; Embers: brasas del golpe. Velocidades en m/s, vidas en s. Valores en HIGH.
public sealed class SparkLevel
{
	public int Count;
	public float Rate;
	public float SpeedMin;
	public float SpeedMax;
	public float LifeMin;
	public float LifeMax;
	public int Embers;

	public SparkLevel(int count, float rate, float speedMin, float speedMax, float lifeMin, float lifeMax, int embers)
	{
		Count = count;
		Rate = rate;
		SpeedMin = speedMin;
		SpeedMax = speedMax;
		LifeMin = lifeMin;
		LifeMax = lifeMax;
		Embers = embers;
	}
}

// Dirección de las chispas de un tipo de contacto (4.2): cuánto hacia la normal (dentro de la pista), arriba y la popa,
// más un desorden de ±Random en cada eje. SideBothWays: la componente lateral va al azar a un lado o al otro.
public sealed class SparkDirection
{
	public float NormalMin;
	public float NormalMax;
	public bool SideBothWays;
	public float UpMin;
	public float UpMax;
	public float BackMin;
	public float BackMax;
	public float RandomSide;
	public float RandomUp;
	public float RandomBack;

	public SparkDirection(float normalMin, float normalMax, bool sideBothWays, float upMin, float upMax, float backMin, float backMax,
		float randomSide, float randomUp, float randomBack)
	{
		NormalMin = normalMin;
		NormalMax = normalMax;
		SideBothWays = sideBothWays;
		UpMin = upMin;
		UpMax = upMax;
		BackMin = backMin;
		BackMax = backMax;
		RandomSide = randomSide;
		RandomUp = randomUp;
		RandomBack = randomBack;
	}
}

// Destello de un nivel (5): tamaño en m y duración en s
public sealed class FlashLevel
{
	public float Size;
	public float Duration;

	public FlashLevel(float size, float duration)
	{
		Size = size;
		Duration = duration;
	}
}

// Humo de un nivel (6): bocanadas en HIGH y mínimo con cualquier PARTICLES, ventana del retraso, vida, radio inicial y
// final, y escala aleatoria del final
public sealed class SmokeLevel
{
	public int Count;
	public int Minimum;
	public float Window;
	public float LifeMin;
	public float LifeMax;
	public float StartRadius;
	public float EndRadius;
	public float EndScaleMin;
	public float EndScaleMax;

	public SmokeLevel(int count, int minimum, float window, float lifeMin, float lifeMax, float startRadius, float endRadius,
		float endScaleMin, float endScaleMax)
	{
		Count = count;
		Minimum = minimum;
		Window = window;
		LifeMin = lifeMin;
		LifeMax = lifeMax;
		StartRadius = startRadius;
		EndRadius = endRadius;
		EndScaleMin = endScaleMin;
		EndScaleMax = endScaleMax;
	}
}

// Ajustes de los efectos de choque (spec docs/efectos-1-choques.md, 9), con los valores de la maqueta
// designs/efectos-choques.html. Los colores van aquí y no en Palette: no se derivan de sus colores (la spec lo permite).
// Lógica pura: solo datos.
public sealed class CollisionFxSettings
{
	// --- Punto de contacto (3)
	public float BarrierContactHeight = 0.75f;   // m sobre la pista
	public float BarrierContactAhead = 2f;    // m del centro de la nave hacia el morro (negativo: hacia la popa)
	public float BarrierContactInset = 0.05f;    // m por dentro de la barrera
	public float ShipContactHeight = 0.7f;

	// --- Chispas (4)
	public SparkLevel BarrierGraze = new(0, 140f, 3f, 9f, 0.2f, 0.5f, 0);
	public SparkLevel BarrierMedium = new(55, 0f, 5f, 15f, 0.3f, 0.7f, 8);
	public SparkLevel BarrierStrong = new(130, 0f, 7f, 22f, 0.35f, 0.9f, 30);
	public SparkLevel ShipGraze = new(0, 100f, 2f, 7f, 0.2f, 0.45f, 0);
	public SparkLevel ShipMedium = new(45, 0f, 4f, 12f, 0.3f, 0.65f, 6);
	public SparkLevel ShipStrong = new(110, 0f, 6f, 18f, 0.35f, 0.85f, 24);

	public SparkDirection BarrierDirection = new(0.2f, 1f, false, 0.1f, 0.8f, 0f, 0.9f, 0.45f, 0.45f, 0.45f);
	public SparkDirection ShipSideDirection = new(0.1f, 0.6f, true, 0.2f, 1f, 0.3f, 1f, 0.4f, 0.4f, 0.4f);
	// Alcance por detrás: a los lados, aleatorio ±0,9 (la normal no cuenta)
	public SparkDirection ShipRearDirection = new(0f, 0f, false, 0.3f, 1f, -0.2f, 0.6f, 0.9f, 0.3f, 0.4f);

	public float BarrierInherit = 0.5f;      // fracción de la velocidad de la nave
	public float ShipInherit = 0.92f;
	public float ShipBackMin = 4f;           // m/s extra hacia atrás entre naves
	public float ShipBackMax = 12f;
	public float EmberSpeedMin = 1f;
	public float EmberSpeedMax = 4f;
	public float EmberLifeMin = 1f;
	public float EmberLifeMax = 1.6f;
	// Chispas altas contra la barrera (decisión del usuario, para que el roce se vea pasar por el lateral del cristal de
	// la cabina): esta fracción sale con más velocidad hacia arriba, como si rebotara en la barrera
	public float HighSparkFraction = 0.25f;
	public float HighSparkUpMin = 4f;        // m/s extra hacia arriba
	public float HighSparkUpMax = 8f;
	public float SparkJitterSide = 0.15f;    // desorden al salir, m
	public float SparkJitterUp = 0.1f;
	public float SparkJitterAlong = 0.3f;

	public float Gravity = 14f;              // m/s²
	public float AirDamping = 0.8f;          // por segundo
	public float GroundBounceVertical = 0.3f;
	public float GroundBounceHorizontal = 0.6f;
	public float GroundClearance = 0.02f;    // m sobre la superficie al rebotar
	public float BarrierBounce = 0.3f;
	public float StreakTime = 0.03f;         // s: largo del trazo = velocidad relativa × esto
	public float StreakMax = 2.2f;           // m
	public int MaxSparks = 3000;

	public Vector3 SparkHot = new(1f, 0.95f, 0.82f);       // blanco cálido
	public Vector3 SparkOrange = new(1f, 0.6f, 0.18f);
	public Vector3 SparkCold = new(0.85f, 0.22f, 0.04f);   // rojo oscuro
	public float SparkHotUntil = 0.3f;                     // u del naranja
	public float SparkFadePower = 1.1f;
	public Vector3 EmberColor = new(1f, 0.55f, 0.14f);
	public float EmberBrightness = 0.85f;

	// --- Destello (5)
	public FlashLevel FlashGraze = new(0.9f, 0.06f);
	public FlashLevel FlashMedium = new(2.4f, 0.16f);
	public FlashLevel FlashStrong = new(3.8f, 0.26f);
	public float GrazeFlashRate = 30f;          // veces por segundo mientras dura el roce
	public float GrazeFlashMin = 0.5f;          // intensidad aleatoria
	public float GrazeFlashMax = 1f;
	public float ShipFlashInherit = 0.92f;      // entre naves se mueve con ellas; contra la barrera, quieto
	public int RayCount = 6;
	public float RayStart = 0.1f;               // m del centro
	public float RayLength = 1.3f;              // × tamaño
	public float RingStartRadius = 0.5f;
	public float RingEndRadius = 7f;
	public float RingDuration = 0.4f;
	public float RingIntensity = 0.9f;
	public int RingSegments = 32;
	public int MaxFlashes = 8;
	public Vector3 FlashColor = new(1f, 0xE2 / 255f, 0xB0 / 255f);   // #FFE2B0
	public Vector3 RayColor = new(1f, 0.92f, 0.75f);
	public Vector3 RingColor = new(1f, 0.62f, 0.2f);

	// --- Humo (6)
	public SmokeLevel SmokeMedium = new(2, 1, 0.08f, 1f, 1.4f, 0.4f, 1.6f, 1f, 1f);
	public SmokeLevel SmokeStrong = new(10, 2, 0.3f, 1.3f, 2.1f, 0.6f, 3f, 0.8f, 1.2f);
	public float SmokeBarrierInherit = 0.3f;
	public float SmokeShipInherit = 0.85f;
	public float SmokeInward = 1.2f;        // m/s hacia dentro de la pista, contra la barrera
	public float SmokeDrift = 0.8f;         // ±m/s a los lados
	public float SmokeRiseMin = 0.8f;
	public float SmokeRiseMax = 1.6f;
	public float SmokeDampHorizontal = 1.2f;
	public float SmokeDampVertical = 0.4f;
	public float SmokeJitterSide = 0.4f;
	public float SmokeJitterUp = 0.4f;
	public float SmokeJitterAlong = 0.6f;   // la maqueta da ±0,6 m a lo largo de la pista
	public Vector3 SmokeColor = new(0x4A / 255f, 0x56 / 255f, 0x70 / 255f);   // #4A5670
	public float SmokeOpacity = 0.42f;
	public float SmokeFadePower = 1.3f;
	public float SmokeFadeIn = 0.1f;
	public int MaxPuffs = 120;

	// --- Derrape (spec docs/efectos-2-derrape.md)
	public DriftFxSettings Drift = new();

	// --- Aterrizaje (spec docs/efectos-3-aterrizaje.md)
	public LandingFxSettings Landing = new();

	// --- Humo de daños (spec docs/efectos-4-humo-danos.md)
	public DamageSmokeSettings DamageSmoke = new();

	public static CollisionFxSettings Default => new();
}
