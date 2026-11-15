using System;
using System.Collections.Generic;
using System.Linq;

namespace Vectory.Ship;

// Nombres de las piezas del casco 3D de BLADE, variante ASALTO: el contrato con art/blender/hull_blade.py (spec
// docs/cascos-3d-2-blade.md, 3.8). Godot las busca así.
public static class MilitaryHullPieces
{
	public const string Root = "HullBlade";
	public const string Probe = "Nose_Pitot";

	public static readonly string[] Painted = { "Fuselage_Upper", "Prong_L_Base", "Prong_R_Base", "Pod_L_Hump", "Pod_R_Hump", "Fairing" };
	public static readonly string[] Tips = { "Prong_L_Tip", "Prong_R_Tip" };
	public static readonly string[] Armor =
	{
		"Armor_0_L_Plate", "Armor_0_R_Plate", "Armor_1_L_Plate", "Armor_1_R_Plate",
		"Armor_2_L_Plate", "Armor_2_R_Plate", "Armor_3_L_Plate", "Armor_3_R_Plate"
	};
	public static readonly string[] Bottom = { "Fuselage_Lower" };
	public static readonly string[] Accents = { "Accent_FormNose_L", "Accent_FormNose_R", "Accent_FormProng_L", "Accent_FormProng_R" };
	public static readonly string[] NavLights = { "Light_Nav_L", "Light_Nav_R", "Light_Tip_L", "Light_Tip_R" };
	public static readonly string[] TailLights = { "Light_Tail" };
	public static readonly string[] Strobes = { "Light_Strobe_Top", "Light_Strobe_Bottom" };
	public static readonly string[] NozzleLights = { "Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R" };
	public static readonly string[] Glass = { "Canopy_Glass", "Sensor_Ball" };
	public static readonly string[] Decals = { "Decal_Number_L", "Decal_Number_R", "Decal_Insignia_L", "Decal_Insignia_R" };
	// Suelo de la cabina: el color base de la pintura, liso, para que el camuflaje no se vea a través del cristal
	public static readonly string[] Floor = { "Canopy_Floor" };

	// Las 9 luces que titilan con el daño (spec 6.5): la anticolisión y las toberas no
	public static readonly IReadOnlyList<string> FlickerLights = Accents.Concat(NavLights).Concat(TailLights).ToList();

	public static readonly IReadOnlyList<string> Contract = Painted.Concat(Tips).Concat(Armor).Concat(Bottom).Concat(Accents)
		.Concat(NavLights).Concat(TailLights).Concat(Strobes).Concat(NozzleLights).Concat(Glass).Concat(Decals).Append(Probe).Concat(Floor).ToList();
}

// Una pintura de camuflaje de tres tonos (base, claro y oscuro) en 0xRRGGBB. Las lisas llevan los tres iguales.
public sealed record MilitaryPaint(string Id, string Name, uint Base, uint Light, uint Dark);

// Cómo se aplica una pintura militar (spec 6.2), sin Godot: los tonos del blindaje (× 0,85), el color de la panza
// (botOf) y el color del camuflaje a partir de su máscara (camoSet). Lógica pura.
public static class MilitaryHullPaint
{
	// Pintura de por defecto: BOSQUE, la que sale al abrir el prototipo
	public static readonly MilitaryPaint Woodland = new("woodland", "BOSQUE", 0x566040, 0x767A60, 0x2C3326);

	// Imprimación metálica que asoma en los desconchones
	public const uint ChipMetal = 0x767874;
	public const float ArmorDarkening = 0.85f;
	// Tono neutro hacia el que se aclara la panza, y cuánto
	public const uint BottomNeutral = 0x969691;
	public const float BottomMix = 0.35f;
	// Rugosidad de la panza: la textura (≈ 0,25 de media) × 2,6 del prototipo; Godot no admite factores mayores que 1
	public const float BottomRoughness = 0.65f;

	private static int R(uint c) => (int)((c >> 16) & 0xFF);
	private static int G(uint c) => (int)((c >> 8) & 0xFF);
	private static int B(uint c) => (int)(c & 0xFF);
	private static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

	// Math.round de JavaScript: la mitad hacia arriba
	private static int JsRound(double v) => (int)Math.Floor(v + 0.5);

	public static uint[] Tones(MilitaryPaint p) => new[] { p.Base, p.Light, p.Dark };

	// Los tres tonos × 0,85, redondeados
	public static uint[] ArmorTones(MilitaryPaint p) =>
		Tones(p).Select(c => Rgb(JsRound(R(c) * 0.85), JsRound(G(c) * 0.85), JsRound(B(c) * 0.85))).ToArray();

	// Media (2·base + claro + oscuro) / 4, por canal
	public static double[] Average(MilitaryPaint p)
	{
		Func<uint, int>[] ch = { R, G, B };
		return ch.Select(f => (f(p.Base) * 2.0 + f(p.Light) + f(p.Dark)) / 4.0).ToArray();
	}

	// botOf: la media aclarada un 35 % hacia el gris neutro
	public static uint Bottom(MilitaryPaint p)
	{
		double[] avg = Average(p);
		int[] neutral = { R(BottomNeutral), G(BottomNeutral), B(BottomNeutral) };
		int[] c = avg.Select((v, i) => JsRound(v + (neutral[i] - v) * BottomMix)).ToArray();
		return Rgb(c[0], c[1], c[2]);
	}

	// Color del camuflaje desde su máscara RGB8 (R tono: 0 base, 128 claro, 255 oscuro · G desconchón · B grano):
	// (desconchón ? metal : tono) × (0,9 + 0,2 × B), como camoSet del prototipo. Devuelve RGB8 del mismo tamaño.
	public static byte[] BakeCamo(byte[] mask, uint[] tones)
	{
		var out_ = new byte[mask.Length];
		for (int i = 0; i + 2 < mask.Length; i += 3)
		{
			byte tone = mask[i];
			uint c = mask[i + 1] > 127 ? ChipMetal : tones[tone < 64 ? 0 : tone < 192 ? 1 : 2];
			double k = 0.9 + 0.2 * mask[i + 2] / 255.0;
			out_[i] = Channel(R(c) * k);
			out_[i + 1] = Channel(G(c) * k);
			out_[i + 2] = Channel(B(c) * k);
		}
		return out_;
	}

	// Como Uint8ClampedArray: recorta a 0–255 y redondea a par
	private static byte Channel(double v) => (byte)Math.Round(Math.Clamp(v, 0.0, 255.0), MidpointRounding.ToEven);
}
