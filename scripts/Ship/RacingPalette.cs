using System.Numerics;
using Vectory.Radar;

namespace Vectory.Ship;

// Paleta del estilo CARRERAS de la cabina 3D (spec docs/cabinas-3d-1-vector.md, 5): colores de STYLES.racing del
// prototipo, en 0xRRGGBB. Es propia de este estilo y no usa los colores del juego. Lógica pura.
public static class RacingPalette
{
	// Pantallas
	public const uint ScreenBackground = 0x050505;
	public const uint Scan = 0xFFFFFF;
	public const float ScanOpacity = 0.025f;
	public const uint Frame = 0xFF3B30;
	public const float FrameOpacity = 0.75f;
	public const uint Label = 0x9A9A9A;
	public const uint Main = 0xFFFFFF;
	public const uint Over = 0xFF8A00;
	public const uint Orb = 0xFF8A00;
	public const uint OrbOff = 0x3A2A1A;
	public const uint Ok = 0x3DFF8A;
	public const uint Warn = 0xFFE04A;
	public const uint Crit = 0xFF2D2D;
	public const uint Bar = 0x2A2A2A;
	public const uint Rival = 0xFF8A00;

	// Luces de la cabina
	public const uint LightWhite = 0xFFFFFF;
	public const uint HemisphereSky = 0xB0B8C8;
	public const uint HemisphereGround = 0x101010;
	public const uint Ambient = 0x1C1C22;
	public const uint BoostLight = 0xFF9D32;
	public const uint DamageLight = 0xFF4D5E;
	public const uint ShiftGreen = 0x3DFF8A;
	public const uint ShiftRed = 0xFF2D2D;
	public const uint ShiftBlue = 0x3D7BFF;

	public static Vector4 Rgba(uint rgb, float alpha = 1f)
	{
		return new Vector4(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
	}

	private static Vector3 Rgb(uint rgb)
	{
		Vector4 c = Rgba(rgb);
		return new Vector3(c.X, c.Y, c.Z);
	}

	// Los roles del estado del HUD de cabina (CockpitHudState) en este estilo
	public static Vector4 Role(CockpitColor role) => role switch
	{
		CockpitColor.Cyan => Rgba(Main),
		CockpitColor.Orange => Rgba(Orb),
		CockpitColor.Red => Rgba(Crit),
		CockpitColor.Green => Rgba(Ok),
		CockpitColor.Yellow => Rgba(Warn),
		CockpitColor.Blue => Rgba(OrbOff),
		_ => Rgba(Label)
	};

	// Relleno y borde de la tabla 6.1: los del prototipo, que aquí mandan sobre los de la cabina 2D
	public static readonly CockpitHudLook Look = new()
	{
		EmptyOrbColor = CockpitColor.Blue,
		EmptyOrbFill = 0.5f,
		EmptyOrbStroke = 0.9f,
		SegmentOffColor = CockpitColor.BlueGray,
		SegmentOffStroke = 0.55f,
		DisabledSegmentColor = CockpitColor.BlueGray,
		DisabledSegmentStroke = 0.55f,
		FullOrbColor = CockpitColor.Orange,
		CooldownOrbColor = CockpitColor.Orange
	};

	// Los colores de la sección 6.3, para las clases del radar y el minimapa
	public static RadarPalette Radar() => new(
		Rgb(ScreenBackground), Rgb(Label), Rgb(Label), Rgb(Main),
		Rgb(Rival), Rgb(Crit), Rgb(Main), Rgb(Label),
		Rgb(Main), Rgb(Label), Rgb(Frame));
}
