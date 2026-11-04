using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Vectory.Ship;

// Cabina de VECTOR: DOBLE PANTALLA (Cab_Vector_3 de designs/cabina-vector.js), traducida tal cual: mismos puntos,
// orden, colores y opacidades, y las mismas fórmulas. Pantalla de 1280×720. Lógica pura.
public static class VectorCockpit
{
	private const float DashUp = 34f;   // todo el cuadro de mando sube esto

	private static Vector2 V(float x, float y) => new(x, y);

	public static CockpitLayout Build()
	{
		var c = new CockpitCanvas();
		c.Glass();

		Vector2[] cowlCurve = CockpitGeometry.Bezier(V(0f, 540f), V(190f, 610f), V(440f, 614f), 64);

		float CowlY0(float x)
		{
			float xx = x > 640f ? 1280f - x : x;
			if (xx >= 440f)
				return 614f;
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
			return 614f;
		}

		float CowlY(float x) => CowlY0(x) - DashUp;

		// Sombra interior del cristal
		c.Rect(0f, 0f, 200f, 720f, CockpitGradientId.OcclusionLeft);
		c.Rect(1080f, 0f, 200f, 720f, CockpitGradientId.OcclusionRight);

		WideNose(c, new[] { (580f, 200f), (556f, 140f), (536f, 90f) }, 514f, 40f);

		// Marco del cristal: recto y facetado
		Vector2[] topBar = { V(0f, 0f), V(1280f, 0f), V(1280f, 18f), V(1120f, 18f), V(1080f, 34f), V(200f, 34f), V(160f, 18f), V(0f, 18f) };
		c.Polygon(CockpitGeometry.Offset(topBar, 0f, 8f), CockpitGradientId.Front, CockpitColor.Blue, 1f, strokeOpacity: 0.7f);
		c.Panel(topBar, CockpitGradientId.Pillar, bevel: null, highlight: new[] { V(200f, 37f), V(1080f, 37f) });
		c.Screw(640f, 14f);
		c.Screw(420f, 24f);
		c.Screw(860f, 24f);

		foreach ((float k, float o) in new[] { (1f, 0f), (-1f, 1280f) })
			SideWindow(c, k, o, CowlY);

		// Barra superior: sensor central, pilotos y rejillas
		c.Rect(600f, 18f, 80f, 20f, CockpitGradientId.Console, CockpitColor.Cyan, 1.2f);
		c.Circle(640f, 28f, 5f, CockpitColor.Vent, CockpitColor.Blue);
		c.Circle(640f, 28f, 2f, CockpitColor.Cyan, opacity: 0.7f);
		c.Vent(470f, 20f, 100f, 10f, 12, 1f);
		c.Vent(710f, 20f, 100f, 10f, 12, -1f);
		c.Circle(590f, 28f, 1.8f, CockpitColor.Cyan, opacity: 0.8f);
		c.Circle(692f, 28f, 1.8f, CockpitColor.Orange, opacity: 0.8f);
		c.Micro(240f, 30f, "VCT-01", opacity: 0.45f, size: 8f);
		c.Micro(1040f, 30f, "SYS", CockpitTextAnchor.End, opacity: 0.45f, size: 8f);

		// Material ganado abajo al subir el salpicadero: panel inferior robusto
		c.Rect(0f, 720f - DashUp - 8f, 1280f, DashUp + 8f, CockpitGradientId.Front, CockpitColor.Blue, strokeOpacity: 0.7f);
		c.Seam(V(0f, 720f - DashUp + 4f), V(1280f, 720f - DashUp + 4f));
		for (int x = 40; x < 1280; x += 80)
			c.Screw(x, 720f - DashUp / 2f + 2f);
		c.Vent(150f, 720f - DashUp + 10f, 180f, 16f, 18, 2f);
		c.Vent(950f, 720f - DashUp + 10f, 180f, 16f, 18, -2f);
		c.Micro(640f, 716f, "VECTOR · VCT-01", CockpitTextAnchor.Middle, opacity: 0.4f, size: 8f);

		c.PushGroup(Matrix3x2.CreateTranslation(0f, -DashUp));
		Dashboard(c);
		c.PopGroup();

		c.Tints();
		return c.Finish();
	}

	// Morro escalonado: varios niveles de anchura, cada uno más lejano y estrecho, con un escalón entre ellos
	private static void WideNose(CockpitCanvas c, (float Y, float HalfWidth)[] levels, float tipY, float tipHalfWidth)
	{
		var lv = levels.Append((tipY, tipHalfWidth)).ToArray();
		for (int i = 0; i < lv.Length - 1; i++)
		{
			(float ya, float ha) = lv[i];
			(float yb, float hb) = lv[i + 1];
			bool last = i == lv.Length - 2;
			float opacity = 1f - i * 0.06f;

			foreach (float k in new[] { -1f, 1f })
			{
				// Al no ser el último tramo, el siguiente nace un poco hundido (escalón)
				float inner = last ? hb : hb + 12f;
				Vector2[] face = { V(640f + k * ha, ya), V(640f + k * inner, yb), V(640f, yb), V(640f, ya) };
				c.Polygon(face, k < 0f ? CockpitGradientId.NoseLeft : CockpitGradientId.NoseRight, CockpitColor.Cyan, 1.6f - i * 0.15f, opacity);
				c.Line(V(640f + k * ha * 0.55f, ya - 2f), V(640f + k * inner * 0.6f, yb + 6f), CockpitColor.Blue, 1f, 0.5f);
				if (!last)
				{
					// Canto del escalón: cara oscura que baja del tramo siguiente
					c.Polygon(new[] { V(640f + k * inner, yb), V(640f + k * hb, yb - 8f), V(640f, yb - 8f), V(640f, yb) }, CockpitColor.Step, CockpitColor.Blue, 1f, strokeOpacity: 0.8f);
					c.ScrewRoundedX(640f + k * inner * 0.75f, yb + 8f);
				}
			}

			if (!last)
			{
				c.Line(V(640f - hb, yb - 8f), V(640f + hb, yb - 8f), CockpitColor.Highlight, 1f, 0.4f);
				c.Line(V(640f - (hb + 12f), yb), V(640f + hb + 12f, yb), CockpitColor.Cyan, 1f, 0.5f);
			}
		}

		// Punta ancha y plana
		c.Polygon(new[] { V(640f - tipHalfWidth, tipY), V(640f + tipHalfWidth, tipY), V(640f + tipHalfWidth + 6f, tipY + 7f), V(640f - tipHalfWidth - 6f, tipY + 7f) }, CockpitColor.PanelLight, CockpitColor.Cyan, 1.2f);
		c.Line(V(640f, tipY), V(640f, levels[0].Y), CockpitColor.Highlight, 1.2f, 0.7f);
		c.Circle(640f - tipHalfWidth + 9f, tipY + 3f, 2f, CockpitColor.Cyan, opacity: 0.8f);
		c.Circle(640f + tipHalfWidth - 9f, tipY + 3f, 2f, CockpitColor.Cyan, opacity: 0.8f);
	}

	// Ventana lateral: un segundo pilar fino separa el cristal frontal del lateral, así la cabina rodea sin cerrarse
	private static void SideWindow(CockpitCanvas c, float k, float o, Func<float, float> cowlY)
	{
		float X(float x) => o + k * x;
		float Yb(float x) => cowlY(X(x)) + 2f;

		// Franja exterior: baja hasta la visera
		c.Polygon(new[] { V(X(0f), 18f), V(X(26f), 18f), V(X(104f), Yb(104f)), V(X(0f), Yb(0f)) }, CockpitGradientId.Pillar, CockpitColor.Cyan, 1.4f);
		c.Line(V(X(8f), 30f), V(X(8f), Yb(8f) - 6f), CockpitColor.Blue, 1f, 0.5f);
		for (int y = 60; y < 520; y += 46)
			c.Screw(X(14f), y);

		// Cristal lateral algo más oscuro, con junta
		c.Polygon(new[] { V(X(26f), 18f), V(X(160f), 18f), V(X(326f), Yb(326f)), V(X(104f), Yb(104f)) }, CockpitColor.Black, opacity: 0.18f);
		c.Line(V(X(30f), 22f), V(X(108f), Yb(108f) - 4f), CockpitColor.Cyan, 1f, 0.25f);
		c.Line(V(X(156f), 22f), V(X(320f), Yb(320f) - 4f), CockpitColor.Cyan, 1f, 0.2f);

		// Pilar A recto con canto, que llega hasta la visera
		Vector2[] front = { V(X(160f), 18f), V(X(196f), 34f), V(X(360f), Yb(360f)), V(X(322f), Yb(322f)) };
		Vector2[] side = { V(X(196f), 34f), V(X(220f), 34f), V(X(382f), Yb(382f)), V(X(360f), Yb(360f)) };
		c.Polygon(side, CockpitGradientId.Side, CockpitColor.Blue, 1f, strokeOpacity: 0.7f);
		c.Polygon(front, CockpitGradientId.Pillar, CockpitColor.Cyan, 1.6f);
		c.Line(front[1], front[2], CockpitColor.Highlight, 1f, 0.3f);

		// Detalle del pilar: moldura central, remaches y tira de luz en el canto
		c.Line(V(X(178f), 30f), V(X(341f), Yb(341f) - 6f), CockpitColor.Blue, 1f, 0.55f);
		for (double t = 0.12; t < 0.95; t += 0.14)
		{
			float tf = (float)t;
			c.ScrewRoundedX(X(180f + 160f * tf), MathF.Round((30f + (Yb(340f) - 36f) * tf) * 10f) / 10f);
		}
		c.Line(V(X(200f), 60f), V(X(214f), 120f), CockpitColor.Cyan, 2f, 0.6f);
		c.Line(V(X(338f), 520f), V(X(352f), Yb(352f) - 14f), CockpitColor.Cyan, 2f, 0.6f);

		// Base del pilar: pieza de anclaje sobre la visera
		c.Polygon(new[] { V(X(312f), Yb(312f) - 16f), V(X(392f), Yb(392f) - 16f), V(X(398f), Yb(398f)), V(X(306f), Yb(306f)) }, CockpitGradientId.Console, CockpitColor.Cyan, 1.2f);
		c.Screw(X(326f), Yb(326f) - 8f);
		c.Screw(X(382f), Yb(382f) - 8f);
	}

	// Salpicadero en dos planos, pantallas hundidas y consola central. Todo dentro del grupo que sube DashUp.
	private static void Dashboard(CockpitCanvas c)
	{
		// Capota (visera) con canto, y frontal más abajo
		Vector2[] cowlTopLeft = CockpitGeometry.Bezier(V(0f, 540f), V(190f, 610f), V(440f, 614f), 16);
		Vector2[] cowlTop = cowlTopLeft.Append(V(840f, 614f)).Concat(CockpitGeometry.MirrorX(cowlTopLeft).Reverse()).ToArray();
		Vector2[] cowlLip = CockpitGeometry.Offset(cowlTop, 0f, 18f);
		c.Polygon(cowlTop.Concat(cowlLip.Reverse()), CockpitGradientId.Cowl, CockpitColor.Cyan, 1.6f);
		c.Line(CockpitGeometry.Offset(cowlTop, 0f, 3f), CockpitColor.Highlight, 1f, 0.35f);
		c.Polygon(cowlLip.Append(V(1280f, 720f)).Append(V(0f, 720f)), CockpitGradientId.Top, CockpitColor.Blue, 1f, strokeOpacity: 0.7f);

		// Sombra que proyecta la visera sobre el frontal
		c.Polygon(cowlLip.Concat(CockpitGeometry.Offset(cowlLip, 0f, 46f).Reverse()), CockpitGradientId.ShadowDown);

		// Detalle de la visera: costura, ranuras de ventilación y pilotos
		c.Line(CockpitGeometry.Offset(cowlTop, 0f, 10f), CockpitColor.Blue, 1f, 0.45f, 3f, 4f);
		for (int x = 470; x <= 810; x += 34)
		{
			if (x < 600 || x > 680)
				c.Line(V(x, 620f), V(x + 18f, 620f), CockpitColor.Blue, 2f, 0.6f);
		}
		c.Circle(452f, 624f, 1.8f, CockpitColor.Cyan, opacity: 0.8f);
		c.Circle(828f, 624f, 1.8f, CockpitColor.Orange, opacity: 0.8f);

		// Pantallas hundidas en el frontal, bajo la visera, giradas hacia el piloto
		Vector2[] frameL = { V(132f, 610f), V(398f, 640f), V(390f, 716f), V(120f, 694f) };
		Vector2[] screenL = { V(150f, 624f), V(380f, 652f), V(372f, 704f), V(140f, 684f) };
		(Vector2[] Frame, Vector2[] Screen)[] both =
		{
			(frameL, screenL),
			(MirrorSwapped(frameL), MirrorSwapped(screenL))
		};

		foreach ((Vector2[] frame, _) in both)
			Sink(c, frame, 18f);
		Vector2[] console = { V(500f, 656f), V(780f, 656f), V(800f, 720f), V(480f, 720f) };
		Sink(c, console, 16f);

		foreach ((Vector2[] frame, Vector2[] screen) in both)
		{
			c.Screen(screen, new Vector2(190f, 90f));
			c.Panel(frame, CockpitGradientId.Console, depth: 12f, bevel: 5f);
			c.Screen(screen);
			c.Polygon(new[] { frame[0], frame[1], frame[1] + V(0f, 16f), frame[0] + V(0f, 16f) }, CockpitGradientId.ShadowDown, opacity: 0.8f);

			// Canto interior: la pantalla queda hundida
			Vector2 c0 = screen[0], c1 = screen[1];
			float dx = c1.X > c0.X ? 4f : -4f;
			c.Polygon(new[] { c0, c1, V(c1.X - dx, c1.Y + 10f), V(c0.X + dx, c0.Y + 10f) }, CockpitGradientId.BezelIn);
		}

		// Pantalla izquierda: daño
		c.PushGroup(Matrix3x2.CreateRotation(8f * MathF.PI / 180f, V(262f, 668f)));
		c.Brackets(160f, 630f, 206f, 62f, 8f);
		c.Micro(170f, 646f, "HULL STATUS");
		c.DamageText(358f, 646f, CockpitTextAnchor.End);
		c.DamageSegments(184f, 658f, 52f, 16f, 8f);
		c.Micro(184f, 688f, "INTEGRITY");
		c.Segments(new[] { V(250f, 684f), V(358f, 684f) }, CockpitColor.Blue, 1f, 0.6f);
		c.PopGroup();

		// Pantalla derecha: velocidad y boost
		c.PushGroup(Matrix3x2.CreateRotation(-8f * MathF.PI / 180f, V(1018f, 668f)));
		c.Brackets(914f, 630f, 206f, 62f, 8f);
		c.Micro(924f, 646f, "BOOST");
		c.Micro(1112f, 646f, "", CockpitTextAnchor.End, CockpitColor.Orange, 0.8f, slot: CockpitSlot.BoostText);
		c.SpeedText(1066f, 682f, 40f, CockpitTextAnchor.End);
		c.Label(1074f, 682f, "KM/H", 10f, CockpitTextAnchor.Start);
		c.SpeedBar(926f, 690f, 176f, 4f);
		c.BoostOrbs(936f, 664f, 7f, 10f);
		c.PopGroup();

		// Etiquetas y marcas en los marcos de las pantallas
		c.Micro(140f, 714f, "L-MFD", opacity: 0.45f, size: 8f);
		c.Micro(1140f, 714f, "R-MFD", CockpitTextAnchor.End, opacity: 0.45f, size: 8f);
		c.Screw(130f, 604f);
		c.Screw(390f, 634f);
		c.Screw(1150f, 604f);
		c.Screw(890f, 634f);

		// Consola central con grosor
		c.Panel(console, CockpitGradientId.Console, bevel: 6f, highlight: new[] { V(502f, 659f), V(778f, 659f) });
		c.Polygon(new[] { V(500f, 656f), V(780f, 656f), V(782f, 672f), V(498f, 672f) }, CockpitGradientId.ShadowDown, opacity: 0.8f);
		c.Vent(530f, 670f, 220f, 20f, 22, 2f);
		c.Switches(546f, 708f, 10, 20f, 0, 3, 4, 8);
		c.Screw(494f, 666f);
		c.Screw(786f, 666f);
		c.Micro(540f, 700f, "NAV", opacity: 0.5f, size: 7f);
		c.Micro(640f, 700f, "COM", CockpitTextAnchor.Middle, opacity: 0.5f, size: 7f);
		c.Micro(740f, 700f, "AUX", CockpitTextAnchor.End, opacity: 0.5f, size: 7f);
		c.Line(V(510f, 694f), V(770f, 694f), CockpitColor.Blue, 1f, 0.4f);
	}

	// Espejo que conserva el orden de los vértices del lado izquierdo (arriba-exterior primero)
	private static Vector2[] MirrorSwapped(Vector2[] points)
	{
		Vector2[] mirrored = CockpitGeometry.MirrorX(points);
		return new[] { mirrored[1], mirrored[0], mirrored[3], mirrored[2] };
	}

	// Hueco en el frontal: las carcasas del HUD quedan hundidas
	private static void Sink(CockpitCanvas c, Vector2[] frame, float grow)
	{
		Vector2[] rim = CockpitGeometry.Inset(frame, -grow);
		c.Polygon(rim, CockpitColor.Sink, CockpitColor.Cyan, 1.4f);

		// Paredes interiores del hueco: arriba en sombra, lados algo más claros, abajo con un poco de luz
		(int I, int J, CockpitFill Fill, float Opacity)[] walls =
		{
			(0, 1, CockpitGradientId.BezelIn, 1f),
			(1, 2, CockpitColor.FrontLight, 0.9f),
			(3, 0, CockpitColor.FrontLight, 0.9f),
			(2, 3, CockpitColor.SinkWall, 0.8f)
		};
		foreach ((int i, int j, CockpitFill fill, float opacity) in walls)
			c.Polygon(new[] { rim[i], rim[j], frame[j], frame[i] }, fill, CockpitColor.Blue, 0.8f, opacity, 0.5f);

		c.Line(rim[0], rim[1], CockpitColor.Highlight, 1f, 0.3f);
	}
}
