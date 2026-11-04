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
			BuildCockpit = BuildCockpit,
			Cockpit = new CockpitFraming(new Vector3(0f, 0.45f, -1.2f), 80f),
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

	// ---------------------------------------------------------------
	// Cabina
	// ---------------------------------------------------------------

	// Marco en forma de capó/salpicadero central con dos alas hacia los lados.
	// Se dibuja en un plano a 1 m delante de la cámara; con FOV 80 y pantalla 16:9 ese plano
	// cubre x de -1,49 a 1,49 e y de -0,84 a 0,84. El lado izquierdo es el espejo del derecho.
	private static IReadOnlyList<ShipLine> BuildCockpit()
	{
		const float z = -1f;
		const float Edge = 0.012f;   // separación del segundo trazo, para dar grosor a las líneas principales
		var lines = new List<ShipLine>();
		int edges = 0;

		void Trace(float x1, float y1, float x2, float y2, ShipLineColor c, int edge)
		{
			lines.Add(new ShipLine(new Vector3(x1, y1, z), new Vector3(x2, y2, z), c, edge));
		}

		void Seg(float x1, float y1, float x2, float y2, ShipLineColor c)
		{
			Trace(x1, y1, x2, y2, c, edges++);
		}

		// Línea gruesa: la línea y una copia desplazada un poco hacia dentro
		void Thick(float x1, float y1, float x2, float y2, ShipLineColor c)
		{
			int edge = edges++;
			Trace(x1, y1, x2, y2, c, edge);
			if (MathF.Abs(x2 - x1) >= MathF.Abs(y2 - y1))
			{
				Trace(x1, y1 - Edge, x2, y2 - Edge, c, edge);
			}
			else
			{
				float dx = (x1 + x2) > 0f ? -Edge : Edge;
				Trace(x1 + dx, y1, x2 + dx, y2, c, edge);
			}
		}

		// Dibuja la línea y su espejo respecto al eje vertical
		void Mirror(float x1, float y1, float x2, float y2, ShipLineColor c, bool thick)
		{
			if (thick)
			{
				Thick(x1, y1, x2, y2, c);
				Thick(-x1, y1, -x2, y2, c);
			}
			else
			{
				Seg(x1, y1, x2, y2, c);
				Seg(-x1, y1, -x2, y2, c);
			}
		}

		// Capó exterior (cian, grueso): borde superior plano, chaflanes y laterales
		Thick(-0.26f, -0.28f, 0.26f, -0.28f, ShipLineColor.Cyan);
		Mirror(0.26f, -0.28f, 0.56f, -0.56f, ShipLineColor.Cyan, true);
		Mirror(0.56f, -0.56f, 0.50f, -0.90f, ShipLineColor.Cyan, true);

		// Contorno interior (azul): enmarca la zona donde queda la velocidad digital
		Seg(-0.20f, -0.44f, 0.20f, -0.44f, ShipLineColor.Blue);
		Mirror(0.20f, -0.44f, 0.42f, -0.62f, ShipLineColor.Blue, false);
		Mirror(0.42f, -0.62f, 0.38f, -0.90f, ShipLineColor.Blue, false);

		// Alas laterales hacia los bordes de la pantalla
		Mirror(0.26f, -0.28f, 1.49f, -0.62f, ShipLineColor.Cyan, false);
		Mirror(0.56f, -0.56f, 1.49f, -0.80f, ShipLineColor.Blue, false);

		return lines;
	}
}
