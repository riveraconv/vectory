using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Vectory.Ship;

// Cabina de HOJA: BURBUJA (Cab_Hoja_1 de designs/cabinas-hoja-martillo-def.js), traducida tal cual: mismos puntos,
// orden, colores y opacidades, y las mismas fórmulas. Pantalla de 1280×720. Lógica pura.
public static class HojaCockpit
{
	private const float DashUp = 34f;   // todo el cuadro de mando sube esto

	private static Vector2 V(float x, float y) => new(x, y);

	public static CockpitLayout Build()
	{
		var c = new CockpitCanvas();
		c.Glass();

		Vector2[] cowlCurve = CockpitGeometry.Bezier(V(0f, 548f), V(200f, 608f), V(420f, 612f), 64);

		float CowlY0(float x)
		{
			float xx = x > 640f ? 1280f - x : x;
			if (xx >= 420f)
				return 612f;
			for (int i = 1; i < cowlCurve.Length; i++)
			{
				Vector2 p0 = cowlCurve[i - 1], p1 = cowlCurve[i];
				if (xx <= p1.X)
				{
					float width = p1.X - p0.X;
					float t = (xx - p0.X) / (width != 0f ? width : 1f);
					return p0.Y + (p1.Y - p0.Y) * t;
				}
			}
			return 612f;
		}

		float CowlY(float x) => CowlY0(x) - DashUp;

		// Sombra interior del cristal
		c.Rect(0f, 0f, 200f, 720f, CockpitGradientId.OcclusionLeft);
		c.Rect(1080f, 0f, 200f, 720f, CockpitGradientId.OcclusionRight);

		LongNose(c, 580f, 430f, 440f);

		// Arco de la burbuja: fino, con canto
		Vector2[] arcTop = CockpitGeometry.Bezier(V(0f, 112f), V(640f, -48f), V(1280f, 112f), 28);
		Vector2[] arcEdge = CockpitGeometry.Bezier(V(0f, 132f), V(640f, -28f), V(1280f, 132f), 28);
		c.Polygon(arcTop.Concat(arcEdge.Reverse()), CockpitGradientId.Front, CockpitColor.Blue, 1f, strokeOpacity: 0.7f);
		c.Polygon(new[] { V(0f, 0f), V(1280f, 0f) }.Concat(arcTop.Reverse()), CockpitGradientId.Pillar, CockpitColor.Cyan, 1.6f);
		c.Line(arcEdge, CockpitColor.Highlight, 1f, 0.25f);
		c.Line(CockpitGeometry.Bezier(V(120f, 70f), V(640f, -40f), V(1160f, 70f), 24), CockpitColor.Blue, 1f, 0.45f);
		c.Rect(604f, 12f, 72f, 18f, CockpitGradientId.Console, CockpitColor.Cyan, 1.2f);
		c.Circle(640f, 21f, 4f, CockpitColor.Vent, CockpitColor.Blue);
		c.Circle(640f, 21f, 1.8f, CockpitColor.Cyan, opacity: 0.7f);
		c.Screw(300f, 56f);
		c.Screw(980f, 56f);
		c.Screw(460f, 30f);
		c.Screw(820f, 30f);

		foreach ((float k, float o) in new[] { (1f, 0f), (-1f, 1280f) })
			SideWindow(c, k, o, CowlY);

		// Panel inferior robusto
		c.Rect(0f, 720f - DashUp - 8f, 1280f, DashUp + 8f, CockpitGradientId.Front, CockpitColor.Blue, strokeOpacity: 0.7f);
		c.Seam(V(0f, 720f - DashUp + 4f), V(1280f, 720f - DashUp + 4f));
		for (int x = 40; x < 1280; x += 80)
			c.Screw(x, 720f - DashUp / 2f + 2f);
		VentOut(c, 110f, 720f - DashUp + 12f, 240f, 12f, 26, -1f);
		VentOut(c, 930f, 720f - DashUp + 12f, 240f, 12f, 26, 1f);
		c.Micro(640f, 716f, "HOJA · INT-02", CockpitTextAnchor.Middle, opacity: 0.4f, size: 8f);

		c.PushGroup(Matrix3x2.CreateTranslation(0f, -DashUp));
		Dashboard(c);
		c.PopGroup();

		c.Tints();
		return c.Finish();
	}

	// Cubierta plana larguísima que llega casi al horizonte, con juntas que convergen y paneles por tramos
	private static void LongNose(CockpitCanvas c, float y0, float halfWidth, float tipY)
	{
		Vector2 tip = V(640f, tipY), left = V(640f - halfWidth, y0), right = V(640f + halfWidth, y0);
		c.Polygon(new[] { left, V(630f, tipY), V(650f, tipY), right }, CockpitGradientId.DeckFar, CockpitColor.Cyan, 1.6f);

		// Bisel de los bordes de la cubierta (canto fino)
		c.Polygon(new[] { left, V(630f, tipY), V(626f, tipY + 3f), V(640f - halfWidth - 10f, y0 + 6f) }, CockpitColor.FrontLight, CockpitColor.Blue, 0.8f, strokeOpacity: 0.6f);
		c.Polygon(new[] { right, V(650f, tipY), V(654f, tipY + 3f), V(640f + halfWidth + 10f, y0 + 6f) }, CockpitColor.FrontLight, CockpitColor.Blue, 0.8f, strokeOpacity: 0.6f);

		foreach (float f in new[] { 0.25f, 0.5f, 0.75f })
		{
			c.Line(V(640f - halfWidth * f, y0), tip, CockpitColor.Blue, 1f, 0.5f);
			c.Line(V(640f + halfWidth * f, y0), tip, CockpitColor.Blue, 1f, 0.5f);
		}
		c.Line(tip, V(640f, y0), CockpitColor.Highlight, 1f, 0.5f);

		// Juntas transversales, más juntas cuanto más lejos (sensación de largo)
		foreach (float k in new[] { 0.15f, 0.32f, 0.5f, 0.66f, 0.79f, 0.89f, 0.96f })
		{
			float y = y0 - (y0 - tipY) * k, w = halfWidth * (1f - k) + 10f * k;
			c.Seam(V(640f - w, y), V(640f + w, y));
		}

		// Pilotos de posición a lo largo del borde
		foreach (float k in new[] { 0.3f, 0.6f, 0.85f })
		{
			float y = y0 - (y0 - tipY) * k, w = halfWidth * (1f - k) + 10f * k;
			c.Circle(640f - w + 6f, y, 1.8f, CockpitColor.Cyan, opacity: 0.7f);
			c.Circle(640f + w - 6f, y, 1.8f, CockpitColor.Cyan, opacity: 0.7f);
		}
		c.Circle(640f, tipY + 2f, 2.2f, CockpitColor.Cyan, opacity: 0.9f);
	}

	// Franja exterior, ventana lateral y pilar fino hasta la visera
	private static void SideWindow(CockpitCanvas c, float k, float o, Func<float, float> cowlY)
	{
		float X(float x) => o + k * x;
		float Yb(float x) => cowlY(X(x)) + 2f;

		// Borde inferior del arco, que el diseño mide sin reflejar: los dos lados dan lo mismo solo por simetría de la x de entrada
		static float ArcAt(float x)
		{
			float t = x / 1280f;
			return (1f - t) * (1f - t) * 132f + 2f * (1f - t) * t * -28f + t * t * 132f;
		}

		c.Polygon(new[] { V(X(0f), 110f), V(X(22f), ArcAt(22f) - 2f), V(X(96f), Yb(96f)), V(X(0f), Yb(0f)) }, CockpitGradientId.Pillar, CockpitColor.Cyan, 1.4f);
		for (int y = 160; y < 520; y += 50)
			c.Screw(X(12f), y);

		c.Polygon(new[] { V(X(22f), ArcAt(22f)), V(X(120f), ArcAt(120f)), V(X(300f), Yb(300f)), V(X(96f), Yb(96f)) }, CockpitColor.Black, opacity: 0.18f);
		c.Line(V(X(26f), ArcAt(26f) + 4f), V(X(100f), Yb(100f) - 4f), CockpitColor.Cyan, 1f, 0.25f);

		Vector2[] front = { V(X(120f), ArcAt(120f)), V(X(146f), ArcAt(146f) + 4f), V(X(332f), Yb(332f)), V(X(300f), Yb(300f)) };
		Vector2[] side = { V(X(146f), ArcAt(146f) + 4f), V(X(164f), ArcAt(164f) + 6f), V(X(350f), Yb(350f)), V(X(332f), Yb(332f)) };
		c.Polygon(side, CockpitGradientId.Side, CockpitColor.Blue, 1f, strokeOpacity: 0.7f);
		c.Polygon(front, CockpitGradientId.Pillar, CockpitColor.Cyan, 1.6f);
		c.Line(front[1], front[2], CockpitColor.Highlight, 1f, 0.3f);
		c.Line(V(X(133f), ArcAt(133f) + 8f), V(X(316f), Yb(316f) - 6f), CockpitColor.Blue, 1f, 0.5f);

		for (double t = 0.15; t < 0.95; t += 0.16)
		{
			float tf = (float)t;
			float y = ArcAt(134f) + 8f + (Yb(316f) - ArcAt(134f) - 14f) * tf;
			c.ScrewRoundedX(X(134f + 182f * tf), MathF.Round(y * 10f) / 10f);
		}

		c.Polygon(new[] { V(X(290f), Yb(290f) - 14f), V(X(362f), Yb(362f) - 14f), V(X(368f), Yb(368f)), V(X(284f), Yb(284f)) }, CockpitGradientId.Console, CockpitColor.Cyan, 1.2f);
	}

	// Rejilla en paralelogramo inclinada hacia fuera (dir −1 = lado izquierdo, +1 = derecho): más ancha y baja
	private static void VentOut(CockpitCanvas c, float x, float y, float w, float h, int n, float dir)
	{
		float skew = dir * h * 0.9f;
		Vector2[] box = { V(x + skew, y), V(x + w + skew, y), V(x + w, y + h), V(x, y + h) };

		var pairs = new List<Vector2>(n * 2);
		for (int i = 0; i < n; i++)
		{
			float f = (i + 0.5f) / n;
			pairs.Add(V(x + w * f, y + h));
			pairs.Add(V(x + w * f + skew, y));
		}

		c.Polygon(box, CockpitColor.Vent, CockpitColor.Blue, 1f, 0.9f);
		c.Polygon(new[] { V(x + skew, y), V(x + w + skew, y), V(x + w + skew * 0.7f, y + 3f), V(x + skew * 0.7f, y + 3f) }, CockpitColor.Black, opacity: 0.6f);
		c.Segments(pairs, CockpitColor.Blue, 1.4f, 0.6f);
		c.Line(box[3], box[2], CockpitColor.Highlight, 1f, 0.2f);
	}

	// Salpicadero en dos planos, pantalla larga hundida y HUD. Todo dentro del grupo que sube DashUp.
	private static void Dashboard(CockpitCanvas c)
	{
		// Visera y frontal, con sombra
		Vector2[] cowlTopLeft = CockpitGeometry.Bezier(V(0f, 548f), V(200f, 608f), V(420f, 612f), 16);
		Vector2[] cowlTop = cowlTopLeft.Append(V(860f, 612f)).Concat(CockpitGeometry.MirrorX(cowlTopLeft).Reverse()).ToArray();
		Vector2[] cowlLip = CockpitGeometry.Offset(cowlTop, 0f, 16f);
		c.Polygon(cowlTop.Concat(cowlLip.Reverse()), CockpitGradientId.Cowl, CockpitColor.Cyan, 1.6f);
		c.Line(CockpitGeometry.Offset(cowlTop, 0f, 3f), CockpitColor.Highlight, 1f, 0.35f);
		c.Line(CockpitGeometry.Offset(cowlTop, 0f, 9f), CockpitColor.Blue, 1f, 0.45f, 3f, 4f);
		c.Polygon(cowlLip.Append(V(1280f, 720f)).Append(V(0f, 720f)), CockpitGradientId.Top, CockpitColor.Blue, 1f, strokeOpacity: 0.7f);
		c.Polygon(cowlLip.Concat(CockpitGeometry.Offset(cowlLip, 0f, 44f).Reverse()), CockpitGradientId.ShadowDown);

		// Pantalla larga hundida
		Vector2[] frame = { V(360f, 640f), V(920f, 640f), V(934f, 702f), V(346f, 702f) };
		Vector2[] screen = { V(380f, 650f), V(900f, 650f), V(912f, 692f), V(368f, 692f) };
		c.Sink(frame, 18f);
		c.Screen(screen, new Vector2(320f, 60f));
		c.Panel(frame, CockpitGradientId.Console, depth: 8f, bevel: 5f);
		c.Polygon(new[] { frame[0], frame[1], frame[1] + V(0f, 14f), frame[0] + V(0f, 14f) }, CockpitGradientId.ShadowDown, opacity: 0.8f);
		c.Screen(screen);
		c.Brackets(384f, 654f, 512f, 34f, 6f);

		// HUD: daño a la izquierda, velocidad en el centro y boost a la derecha
		c.Micro(396f, 666f, "HULL");
		c.DamageSegments(396f, 670f, 26f, 6f, 5f);
		c.DamageText(396f, 687f, CockpitTextAnchor.Start);
		c.SpeedText(640f, 684f, 34f, CockpitTextAnchor.Middle);
		c.Label(700f, 684f, "KM/H", 9f, CockpitTextAnchor.Start);
		c.SpeedBar(510f, 694f, 260f, 2f);
		c.Micro(884f, 666f, "", CockpitTextAnchor.End, CockpitColor.Orange, 0.8f, slot: CockpitSlot.BoostText);
		c.BoostOrbs(830f, 678f, 5f, 8f);

		c.Micro(352f, 714f, "MFD-01", opacity: 0.45f, size: 8f);
		VentOut(c, 50f, 646f, 250f, 16f, 28, -1f);
		VentOut(c, 980f, 646f, 250f, 16f, 28, 1f);
		c.Switches(100f, 690f, 10, 18f, 0, 3);
		c.Switches(1010f, 690f, 10, 18f, 2, 7, 9);
		c.Screw(70f, 630f);
		c.Screw(1210f, 630f);
	}
}
