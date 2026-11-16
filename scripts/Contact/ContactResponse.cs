using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Contact;

// Parámetros del choque entre naves (velocidades en m/s; fracciones de 0 a 1). Umbrales y restituciones son los de la
// barrera; las pérdidas, las de la tabla de la spec docs/rivales-3-contacto.md, 3.3.
public struct ShipContactSettings
{
	// Valores por defecto, en las unidades del Inspector de Main (grupo Colisiones entre naves)
	public const float DefaultMaxHeightDifference = 1.5f;
	public const float DefaultCullDistance = 20f;
	public const float DefaultMutualMargin = 0.7f;
	public const float DefaultSideGrazeAngle = 45f;   // grados

	public float MaxHeightDifference;  // m de diferencia de altura sobre la superficie desde los que no chocan
	public float CullDistance;         // m entre naves (en planta) desde los que no se miran
	public float MediumSpeed;          // velocidad de cierre desde la que el golpe es medio
	public float StrongSpeed;          // ídem fuerte
	public float MediumRestitution;
	public float StrongRestitution;
	public float GrazeEntryLoss;       // fracción que pierden las dos al empezar un roce
	public float GrazeFriction;        // m/s por segundo que pierden mientras sigue el roce
	public float MediumSpeedLoss;      // fracción que pierde el que embiste (y cada una si es mutuo) en un medio
	public float StrongSpeedLoss;      // ídem en un fuerte
	public float MutualMargin;         // la aportación menor desde esta fracción de la mayor: golpe mutuo
	public float PairCooldown;         // s sin contar otro golpe medio o fuerte de la misma pareja
	public float SideGrazeAngle;       // rad: entre la normal y la marcha, por encima es roce de lado y si no, por detrás
	public float RecoveryDuration;     // corrección tras un golpe fuerte (la de la barrera)
	public float RecoveryControl;

	public static ShipContactSettings Default => new()
	{
		MaxHeightDifference = DefaultMaxHeightDifference,
		CullDistance = DefaultCullDistance,
		MediumSpeed = ShipPhysicsDefaults.ImpactMediumSpeed / 3.6f,
		StrongSpeed = ShipPhysicsDefaults.ImpactStrongSpeed / 3.6f,
		MediumRestitution = ShipPhysicsDefaults.MediumRestitution,
		StrongRestitution = ShipPhysicsDefaults.StrongRestitution,
		GrazeEntryLoss = ShipPhysicsDefaults.GrazeEntryLoss,
		GrazeFriction = ShipPhysicsDefaults.GrazeFriction / 3.6f,
		MediumSpeedLoss = ShipPhysicsDefaults.MediumSpeedLoss,
		StrongSpeedLoss = ShipPhysicsDefaults.StrongSpeedLoss,
		MutualMargin = DefaultMutualMargin,
		PairCooldown = ShipPhysicsDefaults.ImpactCooldown,
		SideGrazeAngle = DefaultSideGrazeAngle * MathF.PI / 180f,
		RecoveryDuration = ShipPhysicsDefaults.RecoveryDuration,
		RecoveryControl = ShipPhysicsDefaults.RecoveryControl
	};
}

// Tipo de roce (spec docs/rivales-3-contacto.md v1.1, 3.3): de lado (puerta con puerta) o por detrás (morro contra cola)
public enum GrazeKind
{
	None,   // no hay roce (golpe medio o fuerte, o ya se separaban)
	Side,
	Rear
}

// Papel de una nave en un golpe
public enum ContactRole
{
	Rammer,     // embiste
	Receiver,   // recibe
	Mutual      // puerta con puerta: los dos igual
}

// Lo que el golpe le hace a una de las dos naves
public struct ContactShipEffect
{
	public Vector2 Move;             // separación en planta
	public Vector2 VelocityChange;   // empujón (m/s)
	public float SpeedScale;         // factor de la velocidad (1 = sin pérdida)
	public float SpeedLoss;          // m/s que se restan además (fricción del roce)
	public bool EndDrift;            // un golpe medio o fuerte corta el derrape
	public bool StartRecovery;       // corrección de 1 s (golpe fuerte)
	public ContactRole Role;
}

// Resultado de un contacto entre dos naves, a y b, con la normal de a hacia b
public struct ContactResult
{
	public ImpactLevel Level;        // Graze también si un medio o fuerte cae en la espera de la pareja
	public bool IsNewImpact;         // golpe medio o fuerte que cuenta (daño, sacudida, consola)
	public float ClosingSpeed;       // m/s con las que se acercaban por la normal (0 si ya se separaban)
	public GrazeKind Graze;          // en un roce, de lado o por detrás
	public ContactShipEffect A;
	public ContactShipEffect B;
}

// Respuesta a un contacto entre dos naves que pesan lo mismo (spec docs/rivales-3-contacto.md, 3.3 y 3.4):
// separación a medias por la normal (entera para la otra si una no puede moverse por la barrera), nivel por la velocidad
// de cierre con los umbrales de la barrera, empujón de la mitad del cambio de velocidad para cada una con la restitución
// del nivel, pérdidas según el papel y corrección para las dos en un fuerte. No gira el morro: el empujón deja velocidad
// de lado y el agarre la corrige. El roce solo frena si es de lado: por detrás (v1.1), únicamente el empujón, así ir
// pegado a otra nave no para a ninguna. Lógica pura: no depende de Godot.
public static class ContactResponse
{
	// continuing: la pareja ya estaba en contacto en el paso anterior; inCooldown: dentro de la espera de la pareja.
	// aBlocked / bBlocked: esa nave está contra la barrera hacia donde la separación la llevaría.
	// forwardA / forwardB: hacia dónde apunta cada una, para saber si un roce es de lado o por detrás.
	public static ContactResult Resolve(Vector2 velocityA, Vector2 velocityB, in ContactHit hit, bool aBlocked, bool bBlocked,
		bool continuing, bool inCooldown, in ShipContactSettings s, float dt, Vector2 forwardA = default, Vector2 forwardB = default)
	{
		Vector2 n = hit.Normal;
		var result = new ContactResult
		{
			Level = ImpactLevel.Graze,
			A = new ContactShipEffect { SpeedScale = 1f },
			B = new ContactShipEffect { SpeedScale = 1f }
		};

		// Separación
		float shareA = aBlocked == bBlocked ? 0.5f : aBlocked ? 0f : 1f;
		result.A.Move = -n * (hit.Depth * shareA);
		result.B.Move = n * (hit.Depth * (1f - shareA));

		// Papeles (con la velocidad de cada una hacia la otra)
		(ContactRole roleA, ContactRole roleB) = Roles(Vector2.Dot(velocityA, n), -Vector2.Dot(velocityB, n), s.MutualMargin);
		result.A.Role = roleA;
		result.B.Role = roleB;

		// Si ya se separan, no hay golpe: solo separación
		float closing = Vector2.Dot(velocityA - velocityB, n);
		if (closing <= 0f)
			return result;
		result.ClosingSpeed = closing;

		ImpactLevel level = ImpactClassifier.Classify(closing, s.MediumSpeed, s.StrongSpeed);
		if (level != ImpactLevel.Graze && inCooldown)
			level = ImpactLevel.Graze;
		result.Level = level;

		float restitution = level switch
		{
			ImpactLevel.Medium => s.MediumRestitution,
			ImpactLevel.Strong => s.StrongRestitution,
			_ => 0f
		};
		float half = 0.5f * (1f + restitution) * closing;
		result.A.VelocityChange = -n * half;
		result.B.VelocityChange = n * half;

		if (level == ImpactLevel.Graze)
		{
			// Por detrás, solo el empujón (restitución 0): el de detrás queda a la velocidad del de delante y nadie frena
			result.Graze = Kind(n, forwardA, forwardB, s.SideGrazeAngle);
			if (result.Graze == GrazeKind.Rear)
				return result;

			// De lado, las dos igual: al entrar, una fracción; mientras dure, la fricción
			if (!continuing)
			{
				result.A.SpeedScale = result.B.SpeedScale = 1f - s.GrazeEntryLoss;
			}
			else
			{
				result.A.SpeedLoss = result.B.SpeedLoss = s.GrazeFriction * dt;
			}
			return result;
		}

		result.IsNewImpact = true;
		bool strong = level == ImpactLevel.Strong;
		float loss = strong ? s.StrongSpeedLoss : s.MediumSpeedLoss;
		result.A.SpeedScale = roleA == ContactRole.Receiver ? 1f : 1f - loss;
		result.B.SpeedScale = roleB == ContactRole.Receiver ? 1f : 1f - loss;
		result.A.EndDrift = result.B.EndDrift = true;
		result.A.StartRecovery = result.B.StartRecovery = strong;
		return result;
	}

	// Roce de lado si la normal se aparta de la dirección media de las dos naves más de sideAngle; si no (también justo
	// en él), por detrás. Sin dirección (naves enfrentadas o sin datos), de lado.
	public static GrazeKind Kind(Vector2 normal, Vector2 forwardA, Vector2 forwardB, float sideAngle)
	{
		Vector2 mean = forwardA + forwardB;
		float length = mean.Length();
		if (length < 1e-4f)
			return GrazeKind.Side;
		float cos = Math.Clamp(MathF.Abs(Vector2.Dot(normal, mean / length)), 0f, 1f);
		float angle = MathF.Acos(cos);
		return angle > sideAngle + 1e-4f ? GrazeKind.Side : GrazeKind.Rear;
	}

	// Papeles con lo que va cada nave hacia la otra (m/s; lo que se aleja cuenta 0): embiste la de más aportación, y es
	// mutuo si la menor llega a mutualMargin de la mayor
	public static (ContactRole A, ContactRole B) Roles(float towardA, float towardB, float mutualMargin)
	{
		float a = MathF.Max(0f, towardA);
		float b = MathF.Max(0f, towardB);
		float high = MathF.Max(a, b);
		float low = MathF.Min(a, b);
		if (high <= 0f || low >= mutualMargin * high)
			return (ContactRole.Mutual, ContactRole.Mutual);
		return a > b ? (ContactRole.Rammer, ContactRole.Receiver) : (ContactRole.Receiver, ContactRole.Rammer);
	}
}
