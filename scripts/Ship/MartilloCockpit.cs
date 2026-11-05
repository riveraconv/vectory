using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Vectory.Ship;

// Cabina de MARTILLO: BÚNKER (Cab_Martillo_1 de designs/cabinas-hoja-martillo-def.js), traducida tal cual: mismos
// puntos, orden, colores y opacidades, y las mismas fórmulas, también el generador del desgaste con su semilla.
// Cristal dividido por barras gruesas, dial central con aguja, boost con tapas de seguridad y daño en placas.
// Pantalla de 1280×720. Lógica pura.
public static class MartilloCockpit
{
	private const float DialX = 640f;
	private const float DialY = 672f;
	private const float GaugeRadius = 48f;
	private const float GaugeStart = 150f;   // grados, con Y hacia abajo
	private const float GaugeEnd = 390f;

	private static Vector2 V(float x, float y) => new(x, y);

	public static CockpitLayout Build()
	{
		var c = new CockpitCanvas();
		c.Glass();

		Nose(c, 600f, 548f);

		c.Panel(new[] { V(0f, 0f), V(1280f, 0f), V(1280f, 70f), V(1280f, 90f), V(0f, 90f) }, CockpitGradientId.Pillar, depth: 8f, bevel: 8f);
		VentArmor(c, 510f, 22f, 260f, 38f, 7, null);
		c.Screw(30f, 30f);
		c.Screw(1250f, 30f);
		c.Screw(470f, 50f);
		c.Screw(810f, 50f);

		// Barras gruesas
		c.Panel(new[] { V(0f, 590f), V(230f, 590f), V(420f, 600f), V(860f, 600f), V(1050f, 590f), V(1280f, 590f), V(1280f, 720f), V(0f, 720f) },
			bevel: 12f, highlight: new[] { V(0f, 593f), V(230f, 593f), V(420f, 603f), V(860f, 603f), V(1050f, 593f), V(1280f, 593f) });

		Vector2[] outerLeft = { V(110f, 90f), V(190f, 90f), V(300f, 580f), V(230f, 590f) };
		Vector2[] outerRight = { V(1090f, 90f), V(1170f, 90f), V(1050f, 590f), V(980f, 580f) };
		Vector2[] innerLeft = { V(452f, 90f), V(480f, 90f), V(436f, 598f), V(414f, 598f) };
		Vector2[] innerRight = { V(800f, 90f), V(828f, 90f), V(866f, 598f), V(844f, 598f) };
		Pillar(c, outerLeft, new[] { V(190f, 90f), V(222f, 90f), V(330f, 574f), V(300f, 580f) }, 20f);
		Pillar(c, outerRight, new[] { V(1058f, 90f), V(1090f, 90f), V(980f, 580f), V(950f, 574f) }, 20f);
		Pillar(c, innerLeft, new[] { V(480f, 90f), V(492f, 90f), V(446f, 598f), V(436f, 598f) }, 12f);
		Pillar(c, innerRight, new[] { V(788f, 90f), V(800f, 90f), V(844f, 598f), V(834f, 598f) }, 12f);
		c.AddDetail(() => Weather(c, outerLeft, 11));
		c.AddDetail(() => Weather(c, outerRight, 23));
		c.AddDetail(() => Weather(c, innerLeft, 37));
		c.AddDetail(() => Weather(c, innerRight, 51));

		for (int x = 30; x < 1280; x += 80)
			c.Screw(x, 708f);

		Dial(c);
		BoostPanel(c);
		DamagePanel(c);
		VentArmor(c, 46f, 622f, 188f, 58f, 8, "INTAKE L");
		VentArmor(c, 1046f, 622f, 188f, 58f, 8, "INTAKE R");

		c.Tints();
		return c.Finish();
	}

	// Morro corto y ancho, con el canard cruzando delante y las puntas caídas
	private static void Nose(CockpitCanvas c, float y0, float cy)
	{
		foreach (float k in new[] { -1f, 1f })
		{
			float X(float x) => 640f + k * x;

			c.Polygon(new[] { V(X(110f), cy), V(X(520f), cy - 10f), V(X(530f), cy + 10f), V(X(510f), cy + 22f), V(X(120f), cy + 30f) }, CockpitGradientId.Top, CockpitColor.Cyan, 1.6f);
			c.Line(V(X(140f), cy + 12f), V(X(500f), cy + 4f), CockpitColor.Blue, 1f, 0.55f);

			// Punta caída
			c.Polygon(new[] { V(X(520f), cy - 10f), V(X(560f), cy + 40f), V(X(550f), cy + 52f), V(X(530f), cy + 10f) }, CockpitGradientId.Front, CockpitColor.Cyan, 1.4f);
			c.Circle(X(540f), cy + 34f, 2f, CockpitColor.Orange, opacity: 0.8f);
		}

		c.Polygon(new[] { V(500f, y0), V(560f, cy - 6f), V(720f, cy - 6f), V(780f, y0) }, CockpitGradientId.NoseLeft, CockpitColor.Cyan, 1.6f);
		c.Line(V(560f, cy - 6f), V(720f, cy - 6f), CockpitColor.Highlight, 1f, 0.5f);
		c.Line(V(600f, cy + 10f), V(680f, cy + 10f), CockpitColor.Blue, 1f, 0.5f);
	}

	// Rejilla blindada: marco con bisel, hueco, lamas con luz y sombra, travesaño, tornillos y placa
	private static void VentArmor(CockpitCanvas c, float x, float y, float w, float h, int n, string tag)
	{
		c.Rect(x - 6f, y - 6f, w + 12f, h + 12f, CockpitGradientId.Console, CockpitColor.Cyan, 1.4f);
		c.Rect(x - 2f, y - 2f, w + 4f, h + 4f, null, CockpitColor.Blue, 1f, 0.6f);
		c.Rect(x, y, w, h, CockpitColor.Sink);
		c.Rect(x, y, w, MathF.Min(10f, h / 3f), CockpitGradientId.BezelIn);

		float step = h / n;
		for (int i = 0; i < n; i++)
		{
			float yy = y + step * (i + 0.5f);
			c.Rect(x + 2f, yy - step * 0.28f, w - 4f, step * 0.42f, CockpitColor.SinkWall);
			c.AddDetail(() =>
			{
				c.Line(V(x + 2f, yy - step * 0.28f), V(x + w - 2f, yy - step * 0.28f), CockpitColor.Highlight, 1f, 0.22f);
				c.Line(V(x + 2f, yy + step * 0.16f), V(x + w - 2f, yy + step * 0.16f), CockpitColor.Black, 1f, 0.7f);
			});
		}

		c.Rect(x + w / 2f - 3f, y, 6f, h, CockpitGradientId.Pillar, CockpitColor.Blue);
		c.Screw(x - 1f, y - 1f);
		c.Screw(x + w + 1f, y - 1f);
		c.Screw(x - 1f, y + h + 1f);
		c.Screw(x + w + 1f, y + h + 1f);

		if (tag == null)
			return;

		c.Rect(x + 4f, y + h + 8f, tag.Length * 6f + 10f, 11f, CockpitColor.Vent, CockpitColor.Blue, 0.8f);
		c.Micro(x + 9f, y + h + 17f, tag, opacity: 0.6f, size: 7f);
		c.Rect(x + w - 62f, y + h + 8f, 62f, 11f, CockpitColor.Vent);
		for (int k = 0; k < 6; k++)
		{
			c.Polygon(new[]
			{
				V(x + w - 60f + k * 10f, y + h + 19f),
				V(x + w - 54f + k * 10f, y + h + 8f),
				V(x + w - 50f + k * 10f, y + h + 8f),
				V(x + w - 56f + k * 10f, y + h + 19f)
			}, CockpitColor.Orange, opacity: 0.55f);
		}
	}

	// Altura del salpicadero en cada x, por tramos
	private static float DashY(float x)
	{
		Vector2[] d = { V(0f, 590f), V(230f, 590f), V(420f, 600f), V(860f, 600f), V(1050f, 590f), V(1280f, 590f) };
		for (int i = 1; i < d.Length; i++)
		{
			if (x <= d[i].X)
				return d[i - 1].Y + (d[i].Y - d[i - 1].Y) * ((x - d[i - 1].X) / (d[i].X - d[i - 1].X));
		}
		return 590f;
	}

	// Pilar de una sola pieza: el mismo polígono se abre en curva abajo y apoya en el salpicadero, sin corte
	private static void Pillar(CockpitCanvas c, Vector2[] f, Vector2[] side, float flare)
	{
		float yTop = MathF.Max(f[2].Y, f[3].Y) - 60f;
		Vector2 topLeft = Vector2.Lerp(f[0], f[3], (yTop - f[0].Y) / (f[3].Y - f[0].Y));
		Vector2 topRight = Vector2.Lerp(f[1], f[2], (yTop - f[1].Y) / (f[2].Y - f[1].Y));
		float xl = MathF.Min(f[3].X, f[2].X) - flare, xr = MathF.Max(f[3].X, f[2].X) + flare;
		Vector2 bottomLeft = V(xl, DashY(xl) + 8f), bottomRight = V(xr, DashY(xr) + 8f);
		Vector2[] curveLeft = CockpitGeometry.Bezier(topLeft, V(topLeft.X + (f[3].X - topLeft.X) * 0.9f, bottomLeft.Y - 8f), bottomLeft, 14);
		Vector2[] curveRight = CockpitGeometry.Bezier(topRight, V(topRight.X + (f[2].X - topRight.X) * 0.9f, bottomRight.Y - 8f), bottomRight, 14);

		// Cara lateral prolongada hasta el salpicadero
		Vector2[] sd = { side[0], side[1], V(side[2].X, DashY(side[2].X) + 8f), V(side[3].X, DashY(side[3].X) + 8f) };
		c.Polygon(sd, CockpitGradientId.Side);
		c.Line(new[] { sd[0], sd[1], sd[2] }, CockpitColor.Blue, 1f, 0.7f);
		c.Line(sd[0], sd[3], CockpitColor.Blue, 1f, 0.7f);

		c.Polygon(new[] { f[0] }.Concat(curveLeft).Concat(curveRight.Reverse()).Append(f[1]), CockpitGradientId.Pillar);

		// Contorno sin la base: así no queda ninguna línea de corte con el salpicadero
		c.Line(new[] { f[1], f[0] }.Concat(curveLeft), CockpitColor.Cyan, 1.6f, 1f);
		c.Line(new[] { f[1] }.Concat(curveRight), CockpitColor.Cyan, 1.6f, 1f);
		c.Line(f[0], topLeft, CockpitColor.Highlight, 1f, 0.25f);

		// Sombra suave donde apoya
		c.Polygon(curveLeft.Skip(6).Concat(curveRight.Skip(6).Reverse()), CockpitGradientId.FootShade);
	}

	// Desgaste de un pilar: manchas, remaches, arañazos, desconchones y una abolladura. Mismo generador que el diseño,
	// en el mismo orden de llamadas, para que salga idéntico.
	private static void Weather(CockpitCanvas c, Vector2[] f, long seed)
	{
		long r = seed;

		double Rnd()
		{
			r = r * 16807 % 2147483647;
			return (r - 1) / 2147483646.0;
		}

		Vector2 P(double u, double v) => Vector2.Lerp(Vector2.Lerp(f[0], f[3], (float)u), Vector2.Lerp(f[1], f[2], (float)u), (float)v);

		static float Hundredths(double value) => (float)(Math.Round(value * 100.0) / 100.0);

		// Manchas de suciedad (más abajo, donde se acumula)
		for (int i = 0; i < 5; i++)
		{
			double u = 0.35 + Rnd() * 0.6;
			double v = 0.2 + Rnd() * 0.6;
			Vector2 spot = P(u, v);
			float rx = (float)(4.0 + Rnd() * 8.0);
			float ry = (float)(10.0 + Rnd() * 24.0);
			c.Ellipse(spot, V(rx, ry), CockpitColor.Black, Hundredths(0.18 + Rnd() * 0.2));
		}

		// Remaches en los dos bordes
		for (double u = 0.04; u < 0.98; u += 0.055)
		{
			foreach (double v in new[] { 0.12, 0.88 })
			{
				Vector2 rivet = P(u, v);
				c.Circle(rivet.X, rivet.Y, 2.2f, CockpitColor.PillarLight, CockpitColor.Blue, 0.8f);
				c.Circle(rivet.X - 0.7f, rivet.Y - 0.7f, 0.7f, CockpitColor.Highlight, opacity: 0.4f);
			}
		}

		// Arañazos finos
		for (int i = 0; i < 6; i++)
		{
			double u = 0.05 + Rnd() * 0.9;
			double v = 0.2 + Rnd() * 0.6;
			Vector2 a = P(u, v);
			double length = 6.0 + Rnd() * 22.0;
			double angle = -0.9 + Rnd() * 1.8;
			Vector2 b = a + V((float)(Math.Cos(angle) * length * 0.5), (float)(Math.Sin(angle) * length));
			c.Line(a, b, CockpitColor.Highlight, 0.7f, Hundredths(0.22 + Rnd() * 0.25));
			if (Rnd() > 0.6)
				c.Line(a + V(1.5f, 1f), b + V(1.5f, 1f), CockpitColor.Black, 0.6f, 0.4f);
		}

		// Desconchones en el borde (óxido apagado)
		for (int i = 0; i < 6; i++)
		{
			double u = 0.08 + Rnd() * 0.85;
			double v = Rnd() > 0.5 ? 0.03 : 0.97;
			Vector2 chip = P(u, v);
			c.Polygon(new[] { chip + V(-2f, -3f), chip + V(3f, -1f), chip + V(1f, 4f), chip + V(-3f, 2f) }, CockpitColor.Orange, opacity: Hundredths(0.18 + Rnd() * 0.18));
		}

		// Abolladura con su brillo
		Vector2 dent = P(0.3 + Rnd() * 0.4, 0.5);
		c.Ellipse(dent, V(7f, 11f), CockpitColor.Black, 0.3f);
		c.Curve(dent + V(-6f, -4f), dent + V(0f, -12f), dent + V(6f, -4f), CockpitColor.Highlight, 0.8f, 0.3f);
	}

	private static void Dial(CockpitCanvas c)
	{
		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Circle,
			Center = V(DialX, DialY),
			Radii = V(110f, 110f),
			Fill = CockpitGradientId.ScreenGlow,
			Slot = CockpitSlot.ScreenGlow
		});
		c.Circle(DialX, DialY, 104f, CockpitColor.Sink, CockpitColor.Cyan, 1.4f);
		c.Circle(DialX, DialY, 104f, CockpitGradientId.BezelIn, opacity: 0.9f);
		c.Arc(V(DialX, DialY), 100f, 180f, 360f, CockpitColor.Highlight, 1f, 0.25f);
		c.Circle(DialX, 680f, 92f, CockpitColor.DialShadow, CockpitColor.Blue);
		c.Circle(DialX, DialY, 92f, CockpitGradientId.Console, CockpitColor.Cyan, 2f);
		c.Circle(DialX, DialY, 80f, CockpitColor.Screen, CockpitColor.Blue);
		c.Arc(V(DialX, DialY), 80f, 180f, 360f, CockpitColor.Black, 10f, 0.45f);

		for (int k = 0; k < 40; k++)
		{
			double a = (150.0 + 240.0 * k / 40.0) * Math.PI / 180.0;
			Vector2 dir = V((float)Math.Cos(a), (float)Math.Sin(a));
			c.AddDetail(() => c.Line(V(DialX, DialY) + dir * 74f, V(DialX, DialY) + dir * 77f, CockpitColor.Blue, 1f, 0.5f));
		}

		for (int k = 0; k < 8; k++)
		{
			double a = k / 8.0 * Math.PI * 2.0;
			float y = DialY + (float)Math.Sin(a) * 86f;
			c.ScrewRoundedX(DialX + (float)Math.Cos(a) * 86f, MathF.Round(y * 10f) / 10f);
		}

		c.Gauge(V(DialX, DialY), GaugeRadius, GaugeStart, GaugeEnd);
		c.Circle(DialX, DialY, 6f, CockpitColor.Screw, CockpitColor.Cyan);

		c.SpeedText(DialX, 706f, 22f, CockpitTextAnchor.Middle);
		c.Micro(DialX, 718f, "KM/H", CockpitTextAnchor.Middle, opacity: 0.7f, size: 8f);
	}

	// Boost: tres luces con tapa de seguridad
	private static void BoostPanel(CockpitCanvas c)
	{
		Vector2[] frame = { V(790f, 628f), V(990f, 628f), V(1000f, 708f), V(780f, 708f) };
		c.Sink(frame, 14f);
		c.Panel(frame, CockpitGradientId.Console, depth: 10f, bevel: 6f);
		c.Polygon(new[] { V(790f, 628f), V(990f, 628f), V(991f, 642f), V(789f, 642f) }, CockpitGradientId.ShadowDown, opacity: 0.8f);
		c.Screw(796f, 634f);
		c.Screw(984f, 634f);
		c.Screw(788f, 700f);
		c.Screw(992f, 700f);
		c.Micro(890f, 704f, "ARM · FIRE", CockpitTextAnchor.Middle, opacity: 0.45f, size: 7f);
		c.Micro(804f, 646f, "BOOST");
		c.Micro(986f, 646f, "", CockpitTextAnchor.End, CockpitColor.Orange, 0.8f, slot: CockpitSlot.BoostText);

		foreach (float x in new[] { 830f, 890f, 950f })
		{
			c.Rect(x - 22f, 656f, 44f, 40f, CockpitColor.Vent, CockpitColor.Blue);
			c.Rect(x - 22f, 656f, 44f, 8f, CockpitGradientId.BezelIn);
			c.Line(new[] { V(x - 22f, 656f), V(x - 14f, 648f), V(x + 14f, 648f), V(x + 22f, 656f) }, CockpitColor.Orange, 1f, 0.5f);
			c.Rect(x - 16f, 645f, 8f, 4f, CockpitColor.Screw, CockpitColor.Blue, 0.8f);
			c.Rect(x + 8f, 645f, 8f, 4f, CockpitColor.Screw, CockpitColor.Blue, 0.8f);
		}

		c.BoostOrbs(830f, 676f, 11f, 38f);
	}

	// Daño: tres placas atornilladas
	private static void DamagePanel(CockpitCanvas c)
	{
		Vector2[] frame = { V(290f, 628f), V(490f, 628f), V(500f, 708f), V(280f, 708f) };
		c.Sink(frame, 14f);
		c.Panel(frame, CockpitGradientId.Console, depth: 10f, bevel: 6f);
		c.Polygon(new[] { V(290f, 628f), V(490f, 628f), V(491f, 642f), V(289f, 642f) }, CockpitGradientId.ShadowDown, opacity: 0.8f);
		c.Screw(296f, 634f);
		c.Screw(484f, 634f);
		c.Screw(288f, 700f);
		c.Screw(492f, 700f);
		c.Micro(390f, 704f, "HULL PLATING", CockpitTextAnchor.Middle, opacity: 0.45f, size: 7f);
		c.Micro(304f, 646f, "HULL");
		c.DamageText(486f, 646f, CockpitTextAnchor.End);

		const float PlateWidth = 52f, PlateHeight = 40f, PlateGap = 10f, PlateX = 306f, PlateY = 656f;
		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			float x = PlateX + i * (PlateWidth + PlateGap);
			c.DamageSegment(x, PlateY, PlateWidth, PlateHeight, i, 1.4f);
			c.Screw(x + 5f, PlateY + 5f);
			c.Screw(x + PlateWidth - 5f, PlateY + PlateHeight - 5f);
		}
	}
}
