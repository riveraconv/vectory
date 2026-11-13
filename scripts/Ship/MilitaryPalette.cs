using System.Numerics;
using Vectory.Radar;

namespace Vectory.Ship;

// Paleta del estilo MILITAR de la cabina 3D (spec docs/cabinas-3d-2-blade.md, 5): colores de ST y LIGHT del prototipo
// de BLADE, en 0xRRGGBB. Es propia de este estilo y no usa los colores del juego. Lógica pura.
public static class MilitaryPalette
{
	// Pantallas
	public const uint Main = 0xA8FF8A;     // fósforo verde
	public const uint Dim = 0x3F7F34;
	public const uint Label = 0x86A07E;
	public const uint Over = 0xFFB020;     // ámbar
	public const uint Ok = 0x8CFF6E;
	public const uint Warn = 0xFFD23A;
	public const uint Crit = 0xFF3B2F;
	public const uint Rival = 0xFFB020;
	public const uint Frame = 0x5FAF4C;
	public const float FrameOpacity = 0.8f;
	public const uint Bar = 0x1A2618;
	public const uint ScreenBackground = 0x020502;
	public const uint OrbOff = 0x22301F;
	public const uint Scan = 0xAAFF8C;     // rgba(170, 255, 140, 0.028)
	public const float ScanOpacity = 0.028f;

	// Textos fijos: pilotos MASTER y placa
	public const uint LampText = 0xFFFFFF;
	public const uint LampBackground = 0x000000;
	public const uint PlateText = 0x9AA894;
	public const uint PlateBackground = 0x0D100D;

	// Luces de la cabina
	public const uint LightWhite = 0xFFFFFF;
	public const uint HemisphereSky = 0xB0B8C8;
	public const uint HemisphereGround = 0x101010;
	public const uint Ambient = 0x1C221C;
	public const uint ScreenLight = 0xCFFFC0;
	public const uint BoostLight = 0xFFA040;
	public const uint DamageLight = 0xFF4D3A;
	public const uint Caution = 0xFFB020;
	public const uint Warning = 0xFF2A1E;
	public const uint Accent = 0x7CFF5A;
	public const uint NavLeft = 0xFF2A1E;
	public const uint NavRight = 0x3DFF6A;
	public const uint Sensor = 0x7CFF5A;

	public static Vector4 Rgba(uint rgb, float alpha = 1f) => RacingPalette.Rgba(rgb, alpha);

	private static Vector3 Rgb(uint rgb)
	{
		Vector4 c = Rgba(rgb);
		return new Vector3(c.X, c.Y, c.Z);
	}

	// Los roles del estado del HUD de cabina (CockpitHudState) en este estilo. PanelDark es el apagado tenue (dim).
	public static Vector4 Role(CockpitColor role) => role switch
	{
		CockpitColor.Cyan => Rgba(Main),
		CockpitColor.Orange => Rgba(Over),
		CockpitColor.Red => Rgba(Crit),
		CockpitColor.Green => Rgba(Ok),
		CockpitColor.Yellow => Rgba(Warn),
		CockpitColor.Blue => Rgba(OrbOff),
		CockpitColor.PanelDark => Rgba(Dim),
		_ => Rgba(Label)
	};

	// Tabla 6.1 de la spec: la carga llena va en fósforo verde y la de cooldown en ámbar; lo apagado de la columna de
	// daño, con el borde de las rotulaciones al 60 %, o tenue en TIME TRIAL
	public static readonly CockpitHudLook Look = new()
	{
		EmptyOrbColor = CockpitColor.Blue,
		EmptyOrbFill = 0.5f,
		EmptyOrbStroke = 0.9f,
		SegmentOffColor = CockpitColor.BlueGray,
		SegmentOffStroke = 0.6f,
		DisabledSegmentColor = CockpitColor.PanelDark,
		DisabledSegmentStroke = 0.6f,
		FullOrbColor = CockpitColor.Cyan,
		CooldownOrbColor = CockpitColor.Orange
	};

	// Los colores de la sección 6.4, para las clases del radar y el minimapa
	public static RadarPalette Radar() => new(
		Rgb(ScreenBackground), Rgb(Label), Rgb(Dim), Rgb(Main),
		Rgb(Rival), Rgb(Crit), Rgb(Main), Rgb(Label),
		Rgb(Main), Rgb(Label), Rgb(Crit));
}
