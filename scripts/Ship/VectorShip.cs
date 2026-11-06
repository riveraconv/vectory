using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// VECTOR (ship_01): su definición y su geometría. Es el único sitio donde está esta nave, junto con sus valores de
// conducción (VectorTuning). Lógica pura.
public static class VectorShip
{
	public static readonly ShipDefinition Definition = new(
		ShipCatalog.Ship01,
		VectorTuning.Values,
		new ShipVisualProfile
		{
			BuildHull = BuildHull,
			Cockpit3D = new Cockpit3DProfile(
				"res://assets/cockpits/cockpit_vector.glb",
				"res://assets/cockpits/cockpit_vector.manifest.json",
				"res://assets/cockpits/cockpit_env.png",
				Cockpit3DStyle.Racing),
			// El de la cabina 3D, decisión del usuario: ojo a 1,45 m de la pista (la nave vuela a 2 m), FOV 70 y sin inclinar
			Cockpit = new CockpitFraming(new Vector3(0f, -0.55f, -1.2f), 70f, 0f),
			Chase = new ChaseFraming(1.8f, 7f, 3f, new Vector3(0f, 0.3f, -10f)),
			CollisionHalfWidth = 3.4f,   // las puntas de las alas
			NoseDistance = 4f,
			PhysicsBoxSize = new Vector3(6.8f, 1.2f, 7.2f),
			PhysicsBoxCenter = new Vector3(0f, 0.2f, -0.4f)
		},
		ShipMultipliers.One);

	// ---------------------------------------------------------------
	// Casco
	// ---------------------------------------------------------------

	// Eje de avance: -Z (convención de Godot). Morro en z = -4, motores atrás en z > 0.
	private static IReadOnlyList<ShipLine> BuildHull()
	{
		var hull = new HullBuilder();
		hull.Fuselage();
		foreach (float s in new[] { 1f, -1f })
		{
			hull.Wing(s);
			hull.Engine(s);
		}
		return hull.Lines;
	}

	private sealed class HullBuilder
	{
		private readonly List<Vector3> _points = new();
		private readonly List<ShipLineColor> _colors = new();

		public List<ShipLine> Lines { get; } = new();

		private int V(Vector3 p, ShipLineColor c)
		{
			_points.Add(p);
			_colors.Add(c);
			return _points.Count - 1;
		}

		private void L(int a, int b)
		{
			Lines.Add(new ShipLine(_points[a], _points[b], _colors[a], Lines.Count));
		}

		private int[] Ring(float cx, float cy, float z, float r, int seg, ShipLineColor c)
		{
			var ring = new int[seg];
			for (int k = 0; k < seg; k++)
			{
				float a = MathF.Tau * k / seg;
				ring[k] = V(new Vector3(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, z), c);
			}
			for (int k = 0; k < seg; k++)
				L(ring[k], ring[(k + 1) % seg]);
			return ring;
		}

		private void Connect(int[] a, int[] b)
		{
			for (int k = 0; k < a.Length; k++)
				L(a[k], b[k]);
		}

		public void Fuselage()
		{
			float[] zs     = { -2.5f, -0.5f, 1.5f, 2.6f };
			float[] halfW  = {  0.5f,  0.9f, 0.8f, 0.7f };
			float[] top    = {  0.35f, 0.6f, 0.55f, 0.5f };
			float[] bottom = { -0.2f, -0.3f, -0.3f, -0.3f };
			float[] side   = {  0.05f, 0.1f, 0.1f, 0.1f };

			int nose = V(new Vector3(0f, 0f, -4f), ShipLineColor.Cyan);

			int[][] rings = new int[zs.Length][];
			for (int i = 0; i < zs.Length; i++)
			{
				rings[i] = new[]
				{
					V(new Vector3(0f, top[i], zs[i]), ShipLineColor.Cyan),
					V(new Vector3(halfW[i], side[i], zs[i]), ShipLineColor.Cyan),
					V(new Vector3(0f, bottom[i], zs[i]), ShipLineColor.Cyan),
					V(new Vector3(-halfW[i], side[i], zs[i]), ShipLineColor.Cyan)
				};

				for (int k = 0; k < 4; k++)
					L(rings[i][k], rings[i][(k + 1) % 4]);

				if (i == 0)
				{
					for (int k = 0; k < 4; k++)
						L(nose, rings[0][k]);
				}
				else
				{
					Connect(rings[i - 1], rings[i]);
				}
			}
		}

		public void Wing(float s)
		{
			int rootFront = V(new Vector3(s * 0.9f, 0.1f, -0.5f), ShipLineColor.Cyan);
			int rootRear  = V(new Vector3(s * 0.8f, 0.1f, 2.0f), ShipLineColor.Cyan);
			int tipFront  = V(new Vector3(s * 3.4f, -0.05f, 2.2f), ShipLineColor.Cyan);
			int tipRear   = V(new Vector3(s * 3.4f, -0.05f, 2.7f), ShipLineColor.Cyan);

			L(rootFront, tipFront);
			L(tipFront, tipRear);
			L(tipRear, rootRear);
			L(rootRear, rootFront);
			L(rootFront, tipRear);

			// Aleta vertical en la punta del ala
			int finTop = V(new Vector3(s * 3.4f, 0.7f, 2.7f), ShipLineColor.Cyan);
			L(tipFront, finTop);
			L(finTop, tipRear);
		}

		public void Engine(float s)
		{
			float cx = s * 0.75f;
			float cy = 0.1f;
			const int seg = 6;

			int[] front  = Ring(cx, cy, 0.8f, 0.4f, seg, ShipLineColor.Cyan);
			int[] back   = Ring(cx, cy, 2.8f, 0.4f, seg, ShipLineColor.Cyan);
			Connect(front, back);

			// Tobera con brillo azul
			int[] nozzle = Ring(cx, cy, 3.2f, 0.28f, seg, ShipLineColor.Blue);
			Connect(back, nozzle);

			int core = V(new Vector3(cx, cy, 3.0f), ShipLineColor.Blue);
			for (int k = 0; k < seg; k++)
				L(core, nozzle[k]);
		}
	}
}
