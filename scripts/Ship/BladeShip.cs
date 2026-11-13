using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// BLADE (ship_02): más punta, a cambio de giro y agarre (equilibrio en docs/naves-6-equilibrio.md). Lógica pura.
// Casco: Interceptor_E de designs/naves-geometria.js. Cabina: 3D militar, de designs/cabina-3d-blade.html (la BURBUJA 2D
// de designs/cabinas-hoja-martillo-def.js se quitó en la etapa 3 de docs/cabinas-3d-2-blade.md; el diseño sigue allí).
public static class BladeShip
{
	private const float ChaseFactor = 1.22f;   // 8,8 m de largo frente a los 7,2 de Vector

	// Cabina 3D (spec docs/cabinas-3d-2-blade.md, 4.5): ojo a 0,90 m de la pista (el de VECTOR × 0,62; la nave vuela a
	// 2 m), FOV 70 y la vista 0,10 rad (5,7°) hacia abajo, como en el prototipo
	private const float EyeY = -1.10f;
	private const float EyeZ = -1.2f;
	private const float CockpitFov = 70f;
	private const float EyePitchRadians = -0.10f;

	private static readonly ShipMultipliers Multipliers = new()
	{
		MaxSpeed = 1.04f,
		Acceleration = 0.94f,
		TurnRate = 0.90f,
		LateralGrip = 0.90f,
		BrakeDeceleration = 1.02f
	};

	private const float AccelCurve = 2f;   // exponente de AccelerationCurve: no es un multiplicador

	public static readonly ShipDefinition Definition = new(
		ShipCatalog.Ship02,
		Multipliers.Apply(VectorTuning.Values) with { AccelCurve = AccelCurve },
		VectorShip.Definition.Visual with
		{
			BuildHull = BuildHull,
			Cockpit3D = new Cockpit3DProfile(
				"res://assets/cockpits/cockpit_blade.glb",
				"res://assets/cockpits/cockpit_blade.manifest.json",
				"res://assets/cockpits/cockpit_blade_env.png",
				Cockpit3DStyle.Military),
			Cockpit = new CockpitFraming(new Vector3(0f, EyeY, EyeZ), CockpitFov, EyePitchRadians * 180f / MathF.PI),
			ChaseFactor = ChaseFactor,
			Chase = VectorShip.Definition.Visual.Chase.Scaled(ChaseFactor),
			CollisionHalfWidth = 1.6f,   // las puntas traseras en flecha
			NoseDistance = 5.2f,
			PhysicsBoxSize = new Vector3(3.2f, 0.62f, 8.8f),
			PhysicsBoxCenter = new Vector3(0f, 0.17f, -0.8f)
		},
		Multipliers);

	// Cuña plana y baja en forma de flecha, con dos toberas pequeñas
	private static IReadOnlyList<ShipLine> BuildHull()
	{
		var hull = new ShipHullBuilder();

		hull.Loft(new LoftSection[]
		{
			new(-3.0f, 0.45f, 0.16f, -0.08f, 0.03f),
			new(-1.0f, 0.9f, 0.26f, -0.12f, 0.04f),
			new(1.2f, 1.35f, 0.3f, -0.14f, 0.04f),
			new(2.8f, 1.55f, 0.28f, -0.14f, 0.04f)
		}, nose: new Vector3(0f, 0.02f, -5.2f));

		// Cubierta de la cabina
		hull.Loft(new LoftSection[]
		{
			new(-1.8f, 0.2f, 0.3f, 0.18f, 0.22f),
			new(-0.4f, 0.32f, 0.48f, 0.24f, 0.26f),
			new(0.8f, 0.26f, 0.4f, 0.28f, 0.28f)
		}, nose: new Vector3(0f, 0.18f, -2.6f), tail: new Vector3(0f, 0.29f, 1.6f));

		// Puntas traseras en flecha
		hull.Mirror(k => hull.Poly(
			new Vector3(k * 1.55f, 0.04f, 2.8f),
			new Vector3(k * 1.6f, 0f, 3.6f),
			new Vector3(k * 1.1f, 0.04f, 2.8f)));

		hull.Mirror(k => hull.Nozzle(k * 0.6f, 0.08f, 2.2f, 2.9f, 0.2f));

		return hull.Lines;
	}
}
