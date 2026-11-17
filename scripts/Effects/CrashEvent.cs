using System;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Contact;

namespace Vectory.Effects;

public enum CrashKind
{
	Barrier,
	Ships
}

// Un contacto que saca efectos (spec docs/efectos-1-choques.md, 10): tipo, nivel, tipo de roce entre naves, punto,
// ejes en ese punto (Normal: hacia dentro de la pista; Up: normal de la superficie; Back: hacia la popa, sobre la
// superficie), velocidad de la nave en el mundo y distancia sobre el eje (para que las chispas sigan la pista).
// IsStart: golpe medio o fuerte que cuenta; si no, un paso de roce. Source: quién roza, para su acumulador.
// Lógica pura.
public readonly record struct CrashEvent(CrashKind Kind, ImpactLevel Level, GrazeKind Graze, bool IsStart, int Source,
	Vector3 Point, Vector3 Normal, Vector3 Up, Vector3 Back, Vector3 Velocity, float AxisDistance)
{
	// Contacto con la barrera de una nave en este paso, o null si no la toca. Punto: en la barrera del lado golpeado,
	// BarrierContactHeight sobre la pista, a la altura del centro de la nave BarrierContactAhead m hacia el morro (la
	// colisión es un límite lateral y no dice qué parte toca). forward y velocity, en planta (x, z).
	public static CrashEvent? Barrier(TrackFrame frame, float barrierOffset, int shipId, BarrierSide side, ImpactLevel level,
		bool isNewImpact, float axisDistance, Vector2 forward, Vector2 velocity, CollisionFxSettings s)
	{
		if (frame == null)
			return null;
		FramePoint here = frame.At(axisDistance);
		var planForward = new Vector3(forward.X, 0f, forward.Y);
		float alongShip = Vector3.Dot(planForward, here.PlanTangent);
		// El punto BarrierContactAhead m hacia el morro, proyectado sobre el eje
		float distance = axisDistance + s.BarrierContactAhead * alongShip;
		FramePoint f = frame.At(distance);
		float sign = side == BarrierSide.Right ? 1f : -1f;
		Vector3 point = f.PointAt(sign * (barrierOffset - s.BarrierContactInset), s.BarrierContactHeight);
		ImpactLevel effective = isNewImpact ? level : ImpactLevel.Graze;
		return new CrashEvent(CrashKind.Barrier, effective, GrazeKind.None, isNewImpact && level != ImpactLevel.Graze,
			shipId * 2 + (side == BarrierSide.Right ? 1 : 0), point, -sign * f.Right, f.Normal, -f.Tangent,
			new Vector3(velocity.X, 0f, velocity.Y), f.Distance);
	}

	// Contacto entre dos naves en este paso (spec 3): en el centro del solape (planta), ShipContactHeight sobre la
	// superficie. Graze: el del roce (Side / Rear; None si ya se separan) o, en un golpe que cuenta, si es de lado o por
	// detrás (facing). Normal: la derecha de la pista (las chispas salen a cualquiera de los dos lados).
	// velocity: la del que embiste, o la media si es mutuo o un roce (planta).
	public static CrashEvent? Ships(TrackFrame frame, int a, int b, ImpactLevel level, bool isNewImpact, GrazeKind graze,
		GrazeKind facing, Vector2 point, Vector2 velocity, CollisionFxSettings s)
	{
		if (frame == null)
			return null;
		SurfacePoint surface = frame.SurfaceUnder(point.X, point.Y);
		FramePoint f = surface.Frame;
		Vector3 position = f.PointAt(surface.Lateral, s.ShipContactHeight);
		bool start = isNewImpact && level != ImpactLevel.Graze;
		return new CrashEvent(CrashKind.Ships, start ? level : ImpactLevel.Graze, start ? facing : graze, start,
			ShipSource(a, b), position, f.Right, f.Normal, -f.Tangent, new Vector3(velocity.X, 0f, velocity.Y), f.Distance);
	}

	// Acumulador del roce de una pareja: aparte de los de la barrera (id × 2 + lado)
	public static int ShipSource(int a, int b) => 1_000_000 + a * 1000 + b;
}
