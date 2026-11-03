using System;
using System.Numerics;

namespace Vectory.Ship;

// Superficie de la pista bajo la nave en un paso de física
public readonly struct SurfaceSample
{
	public readonly float Height;      // Y del punto de la superficie justo debajo de la nave
	public readonly Vector3 Normal;    // "arriba" de la superficie (unitario)
	public readonly Vector2 Tangent;   // sentido del eje en planta (X, Z de Godot; unitario)
	public readonly float Curvature;   // d²h/ds² del perfil de altura (1/m): negativa en las crestas

	public SurfaceSample(float height, Vector3 normal, Vector2 tangent, float curvature)
	{
		Height = height;
		Normal = normal;
		Tangent = tangent;
		Curvature = curvature;
	}

	// Pista plana a una altura: N hacia arriba y sin curvatura
	public static SurfaceSample Flat(float height) => new(height, Vector3.UnitY, Vector2.UnitX, 0f);
}

public struct VerticalSettings
{
	public float Gravity;   // m/s², hacia abajo en el mundo
	public float Hover;     // altura de vuelo sobre la superficie (m)
}

// Capa vertical de la nave (spec docs/pista-3d.md, 3.2–3.4), encima de la dinámica en planta (ShipDynamics).
// Lógica pura: altura (Y del mundo), velocidad vertical y si está en el suelo o en el aire.
//
// En el suelo va a la altura de vuelo sobre la superficie y su velocidad vertical es la de la superficie
// bajo su velocidad horizontal. Se despega en una cresta cuando v² × curvatura del perfil (la aceleración
// hacia abajo que haría falta para seguirla) supera la gravedad; sale con la velocidad que llevaba,
// incluida la vertical. En hondonadas y subidas la superficie la empuja y nunca despega. En el aire cae
// con la gravedad (parábola exacta) y aterriza al llegar a la altura de vuelo: se quita la velocidad
// contra la superficie (a lo largo de N) y se conserva la paralela. Nunca queda por debajo de la superficie.
public sealed class ShipVertical
{
	// En el aire, tocando una cresta, no se aterriza mientras la curvatura pida más de esta fracción de la gravedad
	private const float StayAirborneMargin = 0.7f;

	public bool Grounded { get; private set; } = true;
	public bool Airborne => !Grounded;

	// Y del mundo del origen de la nave
	public float Height { get; private set; }

	// m/s, positiva hacia arriba
	public float VerticalSpeed { get; private set; }

	// En el último Update: si despegó, si aterrizó y con qué velocidad de caída contra la superficie (m/s)
	public bool TookOff { get; private set; }
	public bool Landed { get; private set; }
	public float LandingSpeed { get; private set; }

	// En el suelo a esta altura, sin velocidad vertical (colocación, recolocación)
	public void Reset(float height)
	{
		Grounded = true;
		Height = height;
		VerticalSpeed = 0f;
		TookOff = false;
		Landed = false;
		LandingSpeed = 0f;
	}

	// Avanza un paso con la superficie bajo la nave (ya movida en planta) y su velocidad horizontal.
	// Devuelve la velocidad horizontal: la misma, salvo al aterrizar (se quita la parte contra la superficie).
	public Vector2 Update(float dt, in SurfaceSample surface, Vector2 horizontalVelocity, in VerticalSettings s)
	{
		TookOff = false;
		Landed = false;
		LandingSpeed = 0f;

		float target = surface.Height + s.Hover;
		float gravity = MathF.Max(0f, s.Gravity);

		if (Grounded)
		{
			VerticalSpeed = SurfaceVerticalSpeed(surface.Normal, horizontalVelocity);
			Height = target;

			float along = Vector2.Dot(horizontalVelocity, surface.Tangent);
			if (TakesOff(surface.Curvature, along, gravity))
			{
				Grounded = false;
				TookOff = true;
			}
			return horizontalVelocity;
		}

		// En el aire: parábola exacta con la gravedad
		Height += VerticalSpeed * dt - 0.5f * gravity * dt * dt;
		VerticalSpeed -= gravity * dt;

		if (Height > target)
			return horizontalVelocity;

		// Justo al despegar, en una cresta que casi cumple la regla (con un margen, porque la curvatura del
		// perfil da pequeños saltos entre claves), la superficie cae casi igual que la nave: se queda en la
		// superficie sin aterrizar (sin un despegue y un aterrizaje en cada paso)
		float speedAlong = Vector2.Dot(horizontalVelocity, surface.Tangent);
		if (TakesOff(surface.Curvature, speedAlong, gravity * StayAirborneMargin))
		{
			Height = target;
			VerticalSpeed = SurfaceVerticalSpeed(surface.Normal, horizontalVelocity);
			return horizontalVelocity;
		}

		// Aterrizaje: fuera la velocidad contra la superficie, se conserva la que va a lo largo de ella
		var velocity = new Vector3(horizontalVelocity.X, VerticalSpeed, horizontalVelocity.Y);
		float intoSurface = Vector3.Dot(velocity, surface.Normal);
		if (intoSurface < 0f)
		{
			velocity -= surface.Normal * intoSurface;
			LandingSpeed = -intoSurface;
		}

		Grounded = true;
		Landed = true;
		Height = target;
		VerticalSpeed = velocity.Y;
		return new Vector2(velocity.X, velocity.Z);
	}

	// Regla de despegue: en una cresta de radio R (curvatura -1/R) a velocidad v se despega si v² / R > g
	public static bool TakesOff(float curvature, float speedAlong, float gravity)
	{
		return curvature * speedAlong * speedAlong < -gravity;
	}

	// Velocidad vertical de la superficie (plano tangente) bajo una velocidad horizontal
	public static float SurfaceVerticalSpeed(Vector3 normal, Vector2 horizontalVelocity)
	{
		if (normal.Y <= 1e-4f)
			return 0f;
		return -(normal.X * horizontalVelocity.X + normal.Z * horizontalVelocity.Y) / normal.Y;
	}
}
