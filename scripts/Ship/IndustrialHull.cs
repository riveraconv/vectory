using System;
using System.Collections.Generic;
using System.Linq;

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

	public static readonly IReadOnlyList<string> Contract = PaintedBody.Concat(PaintedCab).Concat(LeadingEdges)
		.Concat(LiveryHazards).Concat(AmberLights).Concat(TailLights).Concat(WorkLights).Concat(Beacons)
		.Concat(NozzleLights).Concat(Glass).Concat(Decals).ToList();
}

// Cómo se aplica una pintura industrial (spec 6.2), sin Godot: la pintura es una máscara,
// color = pintura × (MUL × 1,25) + ADD, con MUL en gris y ADD en RGB (lo que no depende de la pintura: metal de los
// desconchones, arañazos y brillo de los remaches). Lógica pura.
public static class IndustrialHullPaint
{
	// AMARILLO OBRA (casco y cabina), la que sale al abrir el prototipo: la pintura fija de la etapa 2
	public const uint SiteYellow = 0xAA8428;
	public const string SiteYellowName = "AMARILLO OBRA · MARCAS";
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
