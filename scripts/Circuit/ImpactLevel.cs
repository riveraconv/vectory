namespace Vectory.Circuit;

// Nivel de un contacto con la barrera (spec docs/barreras-y-colisiones.md, 4.1).
// Lo usan la respuesta al impacto, el destello, la sacudida de cámara y, más adelante, los daños.
public enum ImpactLevel
{
	Graze,   // roce: se desliza pegada a la barrera
	Medium,  // medio: rebote y pérdida de velocidad
	Strong   // fuerte: rebote, gran pérdida y control reducido un momento
}

// Lado de la pista: Right es el lado con desplazamiento lateral positivo (hacia Track.RightAt)
public enum BarrierSide
{
	Left,
	Right
}

// Clasificación del impacto según la velocidad perpendicular a la barrera.
// Lógica pura: los umbrales van en las mismas unidades que la velocidad.
public static class ImpactClassifier
{
	// Menos de mediumSpeed: roce; de mediumSpeed a strongSpeed (sin incluir): medio; desde strongSpeed: fuerte
	public static ImpactLevel Classify(float perpendicularSpeed, float mediumSpeed, float strongSpeed)
	{
		if (perpendicularSpeed >= strongSpeed)
			return ImpactLevel.Strong;
		if (perpendicularSpeed >= mediumSpeed)
			return ImpactLevel.Medium;
		return ImpactLevel.Graze;
	}
}
