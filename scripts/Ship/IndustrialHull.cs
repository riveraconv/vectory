using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Vectory.Ship;

// Nombres de las piezas del casco 3D de HAMMER, variante MINERO: el contrato con art/blender/hull_hammer.py (spec
// docs/cascos-3d-3-hammer.md, 3.8). Godot las busca así.
public static class IndustrialHullPieces
{
	public const string Root = "HullHammer";

	// Con la pintura del casco (M_Paint*) y con la de la cabina (M_PaintCab)
	public static readonly string[] PaintedBody =
	{
		"Fuselage_Upper", "Canard_L_Top", "Canard_R_Top", "Canard_L_Droop", "Canard_R_Droop", "Fin_L_Plate", "Fin_R_Plate",
		"EngineBay_Body"
	};
	public static readonly string[] PaintedCab = { "Cab_Body" };
	// Borde del canard: franjas con marcas, chapa oscura sin ellas
	public static readonly string[] LeadingEdges = { "Canard_L_LeadingEdge", "Canard_R_LeadingEdge" };
	// Franjas que solo se ven con marcas
	public static readonly string[] LiveryHazards = { "Fin_L_Hazard", "Fin_R_Hazard", "EngineBay_Hazard" };
	public static readonly string[] AmberLights = { "Light_Nose_L", "Light_Nose_R", "Light_Canard_L", "Light_Canard_R" };
	public static readonly string[] TailLights = { "Light_Tail_L", "Light_Tail_R", "Light_TailBar_L", "Light_TailBar_R" };
	public static readonly string[] WorkLights = { "Light_Work_0", "Light_Work_1", "Light_Work_2", "Light_Work_3" };
	// Lentes de las balizas del techo: ámbar (boost) y roja (daño)
	public static readonly string[] Beacons = { "Beacon_Boost_Lens", "Beacon_Damage_Lens" };
	public static readonly string[] NozzleLights = { "Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R" };
	public static readonly string[] Glass = { "Cab_Windshield", "Cab_SideWindow_L", "Cab_SideWindow_R" };
	public static readonly string[] Decals =
	{
		"Decal_Unit_L", "Decal_Unit_R", "Decal_Logo_L", "Decal_Logo_R", "Decal_NoStep_L", "Decal_NoStep_R",
		"Decal_Hot_L", "Decal_Hot_R", "Decal_Cab_L", "Decal_Cab_R"
	};

	// Las 12 luces que titilan con el daño (spec 6.5): las balizas del techo y las toberas no
	public static readonly IReadOnlyList<string> FlickerLights = AmberLights.Concat(TailLights).Concat(WorkLights).ToList();

	// Las piezas que solo se ven con marcas: las calcomanías y las franjas de las aletas y del compartimento
	public static IEnumerable<string> LiveryPieces => Decals.Concat(LiveryHazards);

	// Freno central entre los motores, faros redondos sobre la defensa y toberas laterales del retro (spec
	// docs/efectos-8-luces.md, 3.1, 3.2 y 3.4)
	public static readonly string[] Brake = { "Brake_Mount", "Light_Brake", "Brake_Frame" };
	public static readonly string[] Headlights = { "Light_Head_L", "Light_Head_R" };
	private static readonly string[] LampParts = { "Housing", "Back", "Bezel", "Reflector", "Glass", "Bracket" };
	private static readonly string[] RetroParts = { "Barrel", "Back", "Lip", "Clamp_0", "Clamp_1", "Mount", "Inner", "Glow" };
	public static readonly IReadOnlyList<string> HeadlightParts =
		new[] { "L", "R" }.SelectMany(s => LampParts.Select(p => $"Headlight_{s}_{p}")).ToList();
	public static readonly string[] Reflectors = { "Headlight_L_Reflector", "Headlight_R_Reflector" };
	public static readonly IReadOnlyList<string> Retro =
		new[] { "L", "R" }.SelectMany(s => RetroParts.Select(p => $"Retro_{s}_{p}")).ToList();

	public static readonly IReadOnlyList<string> Contract = PaintedBody.Concat(PaintedCab).Concat(LeadingEdges)
		.Concat(LiveryHazards).Concat(AmberLights).Concat(TailLights).Concat(WorkLights).Concat(Beacons)
		.Concat(NozzleLights).Concat(Glass).Concat(Decals).Concat(Brake).Concat(HeadlightParts).Concat(Headlights)
		.Concat(Retro).ToList();
}

// Cómo se aplica una pintura industrial (spec 6.2), sin Godot: la pintura es una máscara,
// color = pintura × (MUL × 1,25) + ADD, con MUL en gris y ADD en RGB (lo que no depende de la pintura: metal de los
// desconchones, arañazos y brillo de los remaches). Lógica pura.
public static class IndustrialHullPaint
{
	// AMARILLO OBRA, la que sale al abrir el prototipo
	public const uint SiteYellow = 0xAA8428;
	// MUL se guarda dividido entre esto
	public const double MulScale = 1.25;

	// Color desde las máscaras: mul en L8 (un byte por píxel) y add en RGB8. Devuelve RGB8 del mismo número de píxeles.
	public static byte[] Bake(byte[] mul, byte[] add, uint paint)
	{
		if (add.Length != mul.Length * 3)
			throw new ArgumentException("ADD must have three bytes per MUL pixel");
		double r = (paint >> 16) & 0xFF, g = (paint >> 8) & 0xFF, b = paint & 0xFF;
		var color = new byte[add.Length];
		for (int i = 0; i < mul.Length; i++)
		{
			double k = mul[i] / 255.0 * MulScale;
			color[i * 3] = Channel(r * k + add[i * 3]);
			color[i * 3 + 1] = Channel(g * k + add[i * 3 + 1]);
			color[i * 3 + 2] = Channel(b * k + add[i * 3 + 2]);
		}
		return color;
	}

	// Como Uint8ClampedArray: recorta a 0–255 y redondea a par
	private static byte Channel(double v) => (byte)Math.Round(Math.Clamp(v, 0.0, 255.0), MidpointRounding.ToEven);
}

// Una pintura del MINERO en 0xRRGGBB: el casco (Body) y, en las bicolor, la cabina de otro color (Cab). Name es el
// nombre interno y ScreenName, el que se ve en pantalla.
public sealed record IndustrialPaint(string Id, string Name, string ScreenName, uint Body, uint? Cab = null)
{
	public bool IsTwoTone => Cab.HasValue;

	// cabOf del prototipo: la cabina de la pintura, o la del casco si no tiene
	public uint CabColor => Cab ?? Body;
}

// Una de las 40 combinaciones: una pintura, sin marcas o con marcas de flota (calcomanías y franjas)
public readonly record struct IndustrialPaintChoice(IndustrialPaint Paint, bool WithMarkings)
{
	public string DisplayName => WithMarkings ? $"{Paint.ScreenName} · MARKINGS" : Paint.ScreenName;
}

// Catálogo de pinturas del casco 3D de HAMMER (PAINTS del prototipo, spec docs/cascos-3d-3-hammer.md, 6.1): 15 lisas y 5
// bicolor, cada una sin y con marcas, todas con el mismo acabado. Datos puros, con su paleta propia como las cabinas,
// que no sale de Palette. Por ahora no se elige en ningún menú ni se guarda.
public static class IndustrialPaintCatalog
{
	private static uint C(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

	public static readonly IReadOnlyList<IndustrialPaint> Paints = new IndustrialPaint[]
	{
		new("site_yellow", "AMARILLO OBRA", "SITE YELLOW", C(170, 132, 40)),
		new("bright_yellow", "AMARILLO VIVO", "BRIGHT YELLOW", C(196, 156, 30)),
		new("orange", "NARANJA", "ORANGE", C(196, 98, 30)),
		new("terracotta", "TEJA", "TERRACOTTA", C(160, 70, 40)),
		new("tractor_green", "VERDE TRACTOR", "TRACTOR GREEN", C(52, 98, 48)),
		new("crane_red", "ROJO GRÚA", "CRANE RED", C(150, 36, 30)),
		new("fleet_white", "BLANCO FLOTA", "FLEET WHITE", C(206, 204, 196)),
		new("site_blue", "AZUL OBRA", "SITE BLUE", C(40, 74, 128)),
		new("plant_grey", "GRIS PLANTA", "PLANT GREY", C(112, 114, 110)),
		new("coal_black", "NEGRO CARBÓN", "COAL BLACK", C(36, 36, 38)),
		new("khaki", "CAQUI", "KHAKI", C(140, 128, 90)),
		new("safety_green", "VERDE SEGURIDAD", "SAFETY GREEN", C(150, 170, 40)),
		new("cream", "CREMA", "CREAM", C(196, 182, 140)),
		new("maroon", "GRANATE", "MAROON", C(104, 34, 36)),
		new("petrol_blue", "AZUL PETRÓLEO", "PETROL BLUE", C(30, 78, 86)),
		new("yellow_black", "AMARILLO · CABINA NEGRA", "YELLOW · BLACK CAB", C(170, 132, 40), C(36, 36, 38)),
		new("orange_white", "NARANJA · CABINA BLANCA", "ORANGE · WHITE CAB", C(196, 98, 30), C(206, 204, 196)),
		new("white_red", "BLANCO · CABINA ROJA", "WHITE · RED CAB", C(206, 204, 196), C(150, 36, 30)),
		new("green_white", "VERDE · CABINA BLANCA", "GREEN · WHITE CAB", C(52, 98, 48), C(206, 204, 196)),
		new("grey_orange", "GRIS · CABINA NARANJA", "GREY · ORANGE CAB", C(112, 114, 110), C(196, 98, 30))
	};

	// Las 20 sin marcas y luego las 20 con marcas
	public static readonly IReadOnlyList<IndustrialPaintChoice> Combinations =
		Paints.Select(p => new IndustrialPaintChoice(p, false)).Concat(Paints.Select(p => new IndustrialPaintChoice(p, true))).ToList();

	// AMARILLO OBRA con marcas (la 21), la que sale al abrir el prototipo
	public static int DefaultIndex => Paints.Count;
	public static IndustrialPaintChoice Default => Combinations[DefaultIndex];

	public static IndustrialPaint Find(string id) => Paints.FirstOrDefault(p => p.Id == id);
}

// Lo que hay que poner en cada grupo de piezas del casco de HAMMER con una pintura (spec 6.2 y 6.3). Lógica pura: el
// casco 3D lo recibe y lo pinta. Panza, motores, toberas, tuberías, protecciones, cristales y luces no cambian.
public sealed record IndustrialHullLook
{
	public const uint MarkDark = 0x141412;
	public const uint MarkLight = 0xE8E2D0;
	// Suma R + G + B del casco a partir de la cual las marcas van oscuras
	public const int LightPaintSum = 360;

	public required uint Body { get; init; }
	public required uint Cab { get; init; }
	// Calcomanías y franjas de las aletas y del compartimento
	public required bool MarkingsVisible { get; init; }
	// Borde del canard: franjas con marcas, chapa oscura sin ellas
	public required bool LeadingEdgeHazard { get; init; }
	public required uint MarkColor { get; init; }

	public static IndustrialHullLook For(IndustrialPaintChoice choice) => new()
	{
		Body = choice.Paint.Body,
		Cab = choice.Paint.CabColor,
		MarkingsVisible = choice.WithMarkings,
		LeadingEdgeHazard = choice.WithMarkings,
		MarkColor = MarkColorFor(choice.Paint)
	};

	// markCol: oscuras sobre pintura clara, claras sobre oscura (solo cuenta el casco)
	public static uint MarkColorFor(IndustrialPaint p) =>
		((p.Body >> 16) & 0xFF) + ((p.Body >> 8) & 0xFF) + (p.Body & 0xFF) > LightPaintSum ? MarkDark : MarkLight;
}

// Dibujo de las calcomanías del casco de HAMMER (spec 6.3): unitTex, logoTex, warnTex y stencilTex del prototipo, con
// sus mismas medidas, en el color de las marcas. El logotipo es inventado: no es de ninguna empresa. Lógica pura.
public static class IndustrialDecalDrawing
{
	public const string Unit = "03";
	public const string Logo = "M·7";
	public const string NoStep = "NO STEP";
	public const string Hot = "HOT";
	public const string Stencil = "HMR-03";

	// Número de unidad en Black Ops One de 300 px, centrado en (256, 206) con la línea base en el medio
	public static DecalCanvas UnitTex(uint color)
	{
		const int W = 512, H = 384;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Text, W / 2f, H / 2f + 14, 0, 0, 0, color, Unit, 300, false, DecalBaseline.Middle, W - 30, DecalFont.BlackOps)
		});
	}

	// Hexágono de radio 228 y 24 px de grueso con esquinas redondeadas, tres picos rellenos y M·7 en Black Ops One de 120 px
	public static DecalCanvas LogoTex(uint color)
	{
		const int W = 512, H = 512;
		const float C = W / 2f, R = 228f;
		var hexagon = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			double a = Math.PI / 6 + i * Math.PI / 3;
			hexagon[i] = new Vector2((float)(C + Math.Cos(a) * R), (float)(C + Math.Sin(a) * R));
		}
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Outline, 0, 0, 24, 0, 12, color, Points: hexagon),
			new DecalPrimitive(DecalShape.Polygon, 0, 0, 0, 0, 0, color, Points: new Vector2[]
			{
				new(96, 300), new(186, 150), new(236, 230), new(296, 120), new(416, 300)
			}),
			new DecalPrimitive(DecalShape.Text, C, 392, 0, 0, 0, color, Logo, 120, false, DecalBaseline.Middle, 0, DecalFont.BlackOps)
		});
	}

	// Triángulo de aviso con un ! vaciado (Share Tech Mono en negrita de 70 px) y el texto en Black Ops One de 78 px
	// desde x 160, con ancho máximo 332; el texto hereda la línea base en el medio del !, como en el prototipo
	public static DecalCanvas WarnTex(string text, uint color)
	{
		const int W = 512, H = 160;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Polygon, 0, 0, 0, 0, 0, color, Points: new Vector2[] { new(34, 128), new(84, 32), new(134, 128) }),
			new DecalPrimitive(DecalShape.Text, 84, 96, 0, 0, 0, color, "!", 70, false, DecalBaseline.Middle, 0, DecalFont.ShareTech,
				Erase: true, Bold: true),
			new DecalPrimitive(DecalShape.Text, 160, 86, 0, 0, 0, color, text, 78, false, DecalBaseline.Middle, W - 180, DecalFont.BlackOps,
				Align: DecalAlign.Left)
		});
	}

	// Recuadro de 6 px a 10 px del borde y el rótulo en Black Ops One de 74 px, centrado, con ancho máximo 462
	public static DecalCanvas StencilTex(uint color)
	{
		const int W = 512, H = 128;
		return new DecalCanvas(W, H, new[]
		{
			new DecalPrimitive(DecalShape.Outline, 0, 0, 6, 0, 0, color, Points: new Vector2[]
			{
				new(10, 10), new(W - 10, 10), new(W - 10, H - 10), new(10, H - 10)
			}),
			new DecalPrimitive(DecalShape.Text, W / 2f, H / 2f + 4, 0, 0, 0, color, Stencil, 74, false, DecalBaseline.Middle, W - 50, DecalFont.BlackOps)
		});
	}

	// El dibujo de cada calcomanía del contrato (solo se ven con marcas); las de los dos lados comparten lienzo
	public static IReadOnlyDictionary<string, DecalCanvas> For(IndustrialHullLook look)
	{
		DecalCanvas unit = UnitTex(look.MarkColor);
		DecalCanvas logo = LogoTex(look.MarkColor);
		DecalCanvas noStep = WarnTex(NoStep, look.MarkColor);
		DecalCanvas hot = WarnTex(Hot, look.MarkColor);
		DecalCanvas stencil = StencilTex(look.MarkColor);
		return new Dictionary<string, DecalCanvas>
		{
			["Decal_Unit_L"] = unit,
			["Decal_Unit_R"] = unit,
			["Decal_Logo_L"] = logo,
			["Decal_Logo_R"] = logo,
			["Decal_NoStep_L"] = noStep,
			["Decal_NoStep_R"] = noStep,
			["Decal_Hot_L"] = hot,
			["Decal_Hot_R"] = hot,
			["Decal_Cab_L"] = stencil,
			["Decal_Cab_R"] = stencil
		};
	}
}

// Balizas del techo del casco de HAMMER (spec 6.4): las de su cabina 3D (IndustrialLamps), con el parpadeo del daño a
// 2 Hz sacado del tiempo de la nave, así la pausa las congela. No titilan con el daño. Lógica pura.
public static class IndustrialHullBeacons
{
	// Intensidad de la lente ámbar (boost) y de la roja (daño). damageEnabled es false en TIME TRIAL.
	public static (float Boost, float Damage) Intensities(bool boostActive, int damageLevel, bool damageEnabled, float time,
		in IndustrialLampSettings settings) =>
		(IndustrialLamps.BoostBeacon(boostActive, time, settings),
			IndustrialLamps.DamageBeacon(damageLevel, damageEnabled, BlinkOn(time), settings));

	// Mitad encendida de cada medio segundo, como el resto del daño 3
	public static bool BlinkOn(float time)
	{
		float phase = time % CockpitHudState.BlinkPeriod;
		if (phase < 0f)
			phase += CockpitHudState.BlinkPeriod;
		return phase < CockpitHudState.BlinkPeriod * 0.5f;
	}
}
