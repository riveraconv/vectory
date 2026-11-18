using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// HAMMER (ship_03): más aceleración abajo, giro y agarre, a cambio de punta y frenada (equilibrio en docs/naves-6-equilibrio.md). Lógica pura.
// Casco: 3D industrial MINERO, de designs/casco-3d-hammer.html (spec docs/cascos-3d-3-hammer.md); el wireframe Vanguard_C
// de designs/naves-geometria.js lo siguen usando el fantasma y el menú. Cabina: 3D industrial, de designs/cabina-3d-hammer.html
// (el BÚNKER 2D de designs/cabinas-hoja-martillo-def.js ya no se usa).
public static class HammerShip
{
	private const float ChaseFactor = 0.92f;   // 6,6 m de largo frente a los 7,2 de Vector (el casco 3D, 6,83: se mantiene)
	// Ojo de la cabina 3D (spec docs/cabinas-3d-3-hammer.md, 4.5): FOV 70 y mirando 4,9° hacia abajo, dentro de la cabina
	// del casco 3D (ventanas en y 0,76–0,98), a 2,87 m de la pista: así los rivales se ven a la altura a la que van (spec
	// docs/rivales-1-parrilla.md). Antes iba a 2,03 m. La misma profundidad que VECTOR y BLADE.
	private const float EyeY = 0.87f;
	private const float EyeZ = -1.2f;
	private const float CockpitFov = 70f;
	private const float EyePitchRadians = -0.085f;

	private static readonly ShipMultipliers Multipliers = new()
	{
		MaxSpeed = 0.98f,
		Acceleration = 1.10f,
		TurnRate = 1.04f,
		LateralGrip = 1.10f,
		BrakeDeceleration = 0.96f
	};

	private const float AccelCurve = 1.5f;   // exponente de AccelerationCurve: no es un multiplicador

	public static readonly ShipDefinition Definition = new(
		ShipCatalog.Ship03,
		Multipliers.Apply(VectorTuning.Values) with { AccelCurve = AccelCurve },
		VectorShip.Definition.Visual with
		{
			BuildHull = BuildHull,
			// Casco 3D MINERO; su panorama es el de BLADE (mismo buildEnv)
			Hull3D = new ShipHull3DProfile(
				"res://assets/ships/hull_hammer.glb",
				"res://assets/ships/hull_hammer.manifest.json",
				"res://assets/cockpits/cockpit_blade_env.png",
				HullStyle.Industrial),
			Cockpit3D = new Cockpit3DProfile(
				"res://assets/cockpits/cockpit_hammer.glb",
				"res://assets/cockpits/cockpit_hammer.manifest.json",
				"res://assets/cockpits/cockpit_blade_env.png",   // mismo buildEnv que el prototipo de BLADE
				Cockpit3DStyle.Industrial),
			Cockpit = new CockpitFraming(new Vector3(0f, EyeY, EyeZ), CockpitFov, EyePitchRadians * 180f / MathF.PI),
			ChaseFactor = ChaseFactor,
			Chase = VectorShip.Definition.Visual.Chase.Scaled(ChaseFactor),
			// El semiancho del casco 3D (halfWidth del manifiesto, 2,502, al centímetro): las balizas de las puntas caídas
			// del canard. Es el mismo del wireframe, que siguen usando el fantasma y el menú.
			CollisionHalfWidth = 2.5f,
			// No tiene alas detrás: los patines de las puntas caídas de los canards, delante (decisión del usuario), de
			// 0,3 m a 2,465 m del centro y con la base en y −0,52; el foco, en el centro del patín
			DriftContact = new DriftContactShape(-2.2f, -2.35f, 0.986f, -2.05f, 0.986f, -0.52f, -2.2f, 0.986f),
			// Caja total del manifiesto del casco 3D, al centímetro
			CollisionFront = 3.83f,
			CollisionBack = 3f,
			// Lo más bajo de la caja del manifiesto del casco 3D
			BellyY = -0.52f,
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
