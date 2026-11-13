using System.Numerics;
using Vectory.Radar;

namespace Vectory.Ship;

// Paleta del estilo INDUSTRIAL de la cabina 3D (spec docs/cabinas-3d-3-hammer.md, 5): colores de ST y LIGHT del
// prototipo de HAMMER, en 0xRRGGBB. Es propia de este estilo y no usa los colores del juego. Lógica pura.
public static class IndustrialPalette
{
	// Pantallas CRT de fósforo ámbar
	public const uint Main = 0xFFB000;
	public const uint Dim = 0x7A520C;
	public const uint Label = 0xC98A12;
	public const uint Over = 0xFF5A1F;     // naranja rojizo: por encima de TOP o con boost
	public const uint Ok = 0x9CFF3A;
	public const uint Warn = 0xFFE04A;
	public const uint Crit = 0xFF3B1F;
	public const uint Rival = 0xFF5A1F;
	public const uint Frame = 0xFFB000;
	public const float FrameOpacity = 0.75f;
	public const uint Bar = 0x3A2608;
	public const uint ScreenBackground = 0x0B0703;
	public const uint OrbOff = 0x4A320A;
	public const float ScanOpacity = 0.07f;   // línea de barrido en Main

	// Relojes pequeños (decorativos)
	public const uint GaugeFace = 0x100E0A;
	public const uint Cream = 0xE9DFC6;
	public const uint GaugeRed = 0xC8321E;
	public const uint Needle = 0xFF7A1A;
	public const uint GaugeHub = 0x2A251C;

	// Textos fijos: placa y rótulos
	public const uint LabelBackground = 0x16130D;
	public const uint LabelText = 0xCFC3A4;

	// Luces de la cabina
	public const uint KeyLight = 0xFFE2B8;
	public const uint HemisphereSky = 0x9A8F80;
	public const uint HemisphereGround = 0x15110C;
	public const uint Ambient = 0x2C241A;
	public const uint LampLight = 0xFFB870;
	public const uint ScreenLight = 0xFFB000;
	public const uint BoostLight = 0xFF9D32;
	public const uint DamageLight = 0xFF4D3A;
	public const uint Accent = 0xFFA436;
	public const uint Beacon = 0xFF9A1A;
	public const uint BeaconRed = 0xFF2A10;
	public const uint LampOff = 0x3A2A10;

	public static Vector4 Rgba(uint rgb, float alpha = 1f) => RacingPalette.Rgba(rgb, alpha);

	private static Vector3 Rgb(uint rgb)
	{
		Vector4 c = Rgba(rgb);
		return new Vector3(c.X, c.Y, c.Z);
	}

	// Los colores de la sección 6.4, para las clases del radar y el minimapa: anillos dim, haz y trazado main, borde y
	// banda label, rivales y meta en sus colores
	public static RadarPalette Radar() => new(
		Rgb(ScreenBackground), Rgb(Label), Rgb(Dim), Rgb(Main),
		Rgb(Rival), Rgb(Crit), Rgb(Main), Rgb(Label),
		Rgb(Main), Rgb(Label), Rgb(Crit));

	// Los roles del estado del HUD de cabina (CockpitHudState) en este estilo (tabla 6.1). Dim es el apagado tenue.
	public static Vector4 Role(CockpitColor role) => role switch
	{
		CockpitColor.Cyan => Rgba(Main),
		CockpitColor.Orange => Rgba(Over),
		CockpitColor.Red => Rgba(Crit),
		CockpitColor.Green => Rgba(Ok),
		CockpitColor.Yellow => Rgba(Warn),
		CockpitColor.Blue => Rgba(OrbOff),
		CockpitColor.Dim => Rgba(Dim),
		_ => Rgba(Label)
	};

	// Carga llena en ámbar y en cooldown en naranja rojizo, como en BLADE con sus colores
	public static readonly CockpitHudLook Look = new()
	{
		EmptyOrbColor = CockpitColor.Blue,
		EmptyOrbFill = 0.5f,
		EmptyOrbStroke = 0.9f,
		SegmentOffColor = CockpitColor.BlueGray,
		SegmentOffStroke = 0.6f,
		DisabledSegmentColor = CockpitColor.Dim,
		DisabledSegmentStroke = 0.6f,
		FullOrbColor = CockpitColor.Cyan,
		CooldownOrbColor = CockpitColor.Orange
	};
}
