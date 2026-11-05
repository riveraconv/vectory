using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// HAMMER (ship_03): más aceleración y agarre que Vector, a cambio de punta, giro y frenada. Lógica pura.
// Casco: Vanguard_C de designs/naves-geometria.js. Cabina: BÚNKER, de designs/cabinas-hoja-martillo-def.js.
public static class HammerShip
{
	private const float ChaseFactor = 0.92f;   // 6,6 m de largo frente a los 7,2 de Vector
	private const float EyeHeightFactor = 1.4f;    // 1,40 m de alto frente al 1,0 de Vector

	private static readonly ShipMultipliers Multipliers = new()
	{
		MaxSpeed = 0.94f,
		Acceleration = 1.10f,
		TurnRate = 0.96f,
		LateralGrip = 1.04f,
		BrakeDeceleration = 0.96f
	};

	private const float AccelCurve = 2f;   // exponente de AccelerationCurve: no es un multiplicador

	public static readonly ShipDefinition Definition = new(
		ShipCatalog.Ship03,
		Multipliers.Apply(VectorTuning.Values) with { AccelCurve = AccelCurve },
		VectorShip.Definition.Visual with
		{
			BuildHull = BuildHull,
			BuildCockpit = HammerCockpit.Build,
			Cockpit = VectorShip.Definition.Visual.Cockpit with
			{
				Eye = VectorShip.Definition.Visual.Cockpit.Eye * new Vector3(1f, EyeHeightFactor, 1f)
			},
			ChaseFactor = ChaseFactor,
			Chase = VectorShip.Definition.Visual.Chase.Scaled(ChaseFactor),
			CollisionHalfWidth = 2.5f,   // las puntas del canard
			NoseDistance = 3.6f,
			PhysicsBoxSize = new Vector3(5f, 1.4f, 6.6f),
			PhysicsBoxCenter = new Vector3(0f, 0.2f, -0.3f)
		},
		Multipliers);

	// Canard ancho en el morro con las puntas caídas, cuerpo estrecho detrás y dos toberas grandes
	private static IReadOnlyList<ShipLine> BuildHull()
	{
		var hull = new ShipHullBuilder();

		hull.Loft(new LoftSection[]
		{
			new(-2.6f, 0.6f, 0.45f, -0.2f, 0.08f),
			new(-1.0f, 0.85f, 0.7f, -0.3f),
			new(1.2f, 0.9f, 0.72f, -0.32f),
			new(2.2f, 0.85f, 0.68f, -0.32f)
		}, nose: new Vector3(0f, 0.05f, -3.6f));

		hull.Mirror(k =>
		{
			// Canard
			var a = new Vector3(k * 0.6f, 0.12f, -3.0f);
			var b = new Vector3(k * 2.5f, 0f, -2.6f);
			var c = new Vector3(k * 2.5f, 0f, -1.7f);
			var d = new Vector3(k * 0.8f, 0.12f, -1.5f);
			var drop = new Vector3(k * 2.5f, -0.5f, -2.2f);
			hull.Poly(a, b, c, d);
			hull.Segment(a, c);
			hull.Segment(b, drop);
			hull.Segment(drop, c);

			// Aletas traseras pequeñas
			hull.Poly(
				new Vector3(k * 0.85f, 0.3f, 1.0f),
				new Vector3(k * 1.3f, 0.9f, 2.0f),
				new Vector3(k * 1.3f, 0.9f, 2.4f),
				new Vector3(k * 0.8f, 0.3f, 2.2f));
		});

		hull.Mirror(k => hull.Nozzle(k * 0.45f, 0.2f, 1.4f, 2.6f, 0.4f));

		return hull.Lines;
	}
}
