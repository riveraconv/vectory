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
}
