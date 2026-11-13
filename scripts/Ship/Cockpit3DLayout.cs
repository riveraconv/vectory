using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Qué enseña una pantalla de la cabina 3D
public enum Cockpit3DScreenContent
{
	RacingLeft,    // casco (VECTOR)
	RacingRight,   // boost y velocidad (VECTOR)
	Radar,
	Minimap,
	MilitaryCombiner,   // cristal HUD de BLADE: rumbo, velocidad, trayectoria y boost (spec docs/cabinas-3d-2-blade.md, 6.2)
	MilitarySpeed,      // velocidad, boost y daño (6.3)
	MilitaryRadar,      // radar grande con cinta de rumbo y contactos (6.4)
	MilitaryMap         // minimapa con el progreso de la vuelta (6.4)
}

// Qué piloto MASTER es un texto fijo, si lo es
public enum Cockpit3DLamp
{
	None,
	Caution,
	Warning
}

// De qué ajuste del Inspector sale la luz de una pantalla (las de VECTOR, que son la referencia)
public enum Cockpit3DScreenLight
{
	None,
	Large,
	Small
}

// Fuente de las pantallas y los textos fijos
public enum Cockpit3DFont
{
	Rajdhani,
	Mono   // Share Tech Mono
}

// Una pantalla viva: pieza del modelo, píxeles de su lienzo, contenido y luz. La luz va LightOffset m por delante de la
// pieza, con la energía del ajuste de su grupo × LightFactor. Additive: va en suma (cristal HUD).
public sealed record Cockpit3DScreen(
	string Piece,
	int Width,
	int Height,
	Cockpit3DScreenContent Content,
	Cockpit3DScreenLight Light,
	float LightFactor,
	float LightOffset,
	bool Additive = false);

// Una línea de un texto fijo: su texto y su línea base, en píxeles del lienzo
public sealed record Cockpit3DLabelLine(string Text, float Y);

// Texto fijo pintado una vez al cargar (pilotos MASTER y placa). Con LampColor, la pieza es un piloto: sin luz y
// multiplicando su color, que enciende y apaga MasterLamps.
public sealed record Cockpit3DLabel(
	string Piece,
	int Width,
	int Height,
	uint Background,
	uint TextColor,
	float FontSize,
	bool Bold,
	IReadOnlyList<Cockpit3DLabelLine> Lines,
	uint? LampColor = null,
	Cockpit3DLamp Lamp = Cockpit3DLamp.None);

// Colores de las luces de la escena de la cabina, y cuánto pesa el tinte de daño en su luz
public sealed record Cockpit3DScene(
	uint KeyLight,
	uint HemisphereSky,
	uint HemisphereGround,
	uint Ambient,
	uint ScreenLight,
	uint BoostLight,
	uint DamageLight,
	float DamageLightFactor);

// Disposición viva de una cabina 3D (spec docs/cabinas-3d-2-blade.md, 5): qué piezas busca Godot por nombre, qué pinta
// cada pantalla, qué luces titilan con daño y con qué colores se ilumina. Cockpit3DView no sabe de ninguna nave: lo lee
// todo de aquí. Lógica pura.
public sealed record Cockpit3DLayout(
	Cockpit3DStyle Style,
	IReadOnlyList<string> Contract,
	IReadOnlyList<Cockpit3DScreen> Screens,
	IReadOnlyList<string> Shades,
	IReadOnlyList<string> FlickerLights,
	int ShiftLightCount,
	IReadOnlyList<Cockpit3DLabel> Labels,
	Cockpit3DScene Scene,
	Cockpit3DFont Font,
	bool ScreensShowHud,   // si las pantallas llevan velocidad, boost, daño, radar y minimapa (y el HUD de pantalla no)
	Func<CockpitColor, Vector4> Role,   // colores del estado del HUD de cabina en este estilo
	CockpitHudLook Look,
	bool BrightnessFollowsGlow)   // brillo de las pantallas × (0,8 + 0,4 × brillo), como BLADE; VECTOR, fijo
{
	public static Cockpit3DLayout For(Cockpit3DStyle style) => style switch
	{
		Cockpit3DStyle.Racing => Cockpit3DLayouts.Racing,
		Cockpit3DStyle.Military => Cockpit3DLayouts.Military,
		_ => throw new ArgumentOutOfRangeException(nameof(style))
	};
}

public static class Cockpit3DLayouts
{
	// Luz de pantalla del prototipo: 6 cm por delante de la boca de la pantalla
	public const float ScreenLightDistance = 0.06f;

	// VECTOR: la superficie de la pantalla está 4 mm por delante del fondo de su carcasa
	private const float RacingSurfaceOffset = 0.004f;
	private const float RacingLargeDepth = 0.045f;
	private const float RacingSmallDepth = 0.035f;

	// BLADE: la pantalla está 1,5 mm por delante del grupo de su caja, y la luz a 6 cm del grupo
	private const float MilitarySurfaceOffset = 0.0015f;

	// Luz de pantalla del prototipo de BLADE: brillo × 0,03 × área / 0,017. La de VECTOR es brillo × 0,3 en las
	// pantallas grandes, que es el ajuste LargeScreenLight: el factor es la proporción entre las dos.
	private const float MilitaryScreenLight = 0.03f;
	private const float MilitaryScreenArea = 0.017f;
	private const float RacingLargeScreenLight = 0.3f;

	public const float LampOffFactor = 0.12f;   // pilotos MASTER apagados

	public static readonly Cockpit3DLayout Racing = new(
		Cockpit3DStyle.Racing,
		Cockpit3DPieces.Contract,
		new[]
		{
			new Cockpit3DScreen(Cockpit3DPieces.ScreenLeft, RacingScreenDrawing.LargeWidth, RacingScreenDrawing.LargeHeight,
				Cockpit3DScreenContent.RacingLeft, Cockpit3DScreenLight.Large, 1f, RacingLightOffset(RacingLargeDepth)),
			new Cockpit3DScreen(Cockpit3DPieces.ScreenRight, RacingScreenDrawing.LargeWidth, RacingScreenDrawing.LargeHeight,
				Cockpit3DScreenContent.RacingRight, Cockpit3DScreenLight.Large, 1f, RacingLightOffset(RacingLargeDepth)),
			new Cockpit3DScreen(Cockpit3DPieces.ScreenRadar, RacingScreenDrawing.SmallSize, RacingScreenDrawing.SmallSize,
				Cockpit3DScreenContent.Radar, Cockpit3DScreenLight.Small, 1f, RacingLightOffset(RacingSmallDepth)),
			new Cockpit3DScreen(Cockpit3DPieces.ScreenMap, RacingScreenDrawing.SmallSize, RacingScreenDrawing.SmallSize,
				Cockpit3DScreenContent.Minimap, Cockpit3DScreenLight.Small, 1f, RacingLightOffset(RacingSmallDepth))
		},
		new[]
		{
			Cockpit3DPieces.Shade(Cockpit3DPieces.ScreenLeft), Cockpit3DPieces.Shade(Cockpit3DPieces.ScreenRight),
			Cockpit3DPieces.Shade(Cockpit3DPieces.ScreenRadar), Cockpit3DPieces.Shade(Cockpit3DPieces.ScreenMap)
		},
		Cockpit3DPieces.FlickerLights,
		Cockpit3DPieces.ShiftLightCount,
		Array.Empty<Cockpit3DLabel>(),
		new Cockpit3DScene(RacingPalette.LightWhite, RacingPalette.HemisphereSky, RacingPalette.HemisphereGround,
			RacingPalette.Ambient, RacingPalette.LightWhite, RacingPalette.BoostLight, RacingPalette.DamageLight,
			CockpitLightMix.DamageFactor),
		Cockpit3DFont.Rajdhani,
		true,
		RacingPalette.Role,
		RacingPalette.Look,
		false);

	public static readonly Cockpit3DLayout Military = new(
		Cockpit3DStyle.Military,
		MilitaryPieces.Contract,
		new[]
		{
			new Cockpit3DScreen(MilitaryPieces.ScreenHud, MilitaryScreenDrawing.CombinerWidth, MilitaryScreenDrawing.CombinerHeight,
				Cockpit3DScreenContent.MilitaryCombiner, Cockpit3DScreenLight.None, 0f, 0f, Additive: true),
			MilitaryScreen(MilitaryPieces.ScreenSpeed, MilitaryScreenDrawing.SpeedWidth, MilitaryScreenDrawing.SpeedHeight,
				0.105f, 0.14f, Cockpit3DScreenContent.MilitarySpeed),
			MilitaryScreen(MilitaryPieces.ScreenRadar, MilitaryScreenDrawing.RadarWidth, MilitaryScreenDrawing.RadarHeight,
				0.17f, 0.14f, Cockpit3DScreenContent.MilitaryRadar),
			MilitaryScreen(MilitaryPieces.ScreenMap, MilitaryScreenDrawing.MapWidth, MilitaryScreenDrawing.MapHeight,
				0.105f, 0.14f, Cockpit3DScreenContent.MilitaryMap)
		},
		new[]
		{
			MilitaryPieces.Shade(MilitaryPieces.ScreenSpeed), MilitaryPieces.Shade(MilitaryPieces.ScreenRadar),
			MilitaryPieces.Shade(MilitaryPieces.ScreenMap)
		},
		MilitaryPieces.FlickerLights,
		0,
		new[]
		{
			Lamp(MilitaryPieces.LampCaution, "CAUTION", MilitaryPalette.Caution, Cockpit3DLamp.Caution),
			Lamp(MilitaryPieces.LampWarning, "WARN", MilitaryPalette.Warning, Cockpit3DLamp.Warning),
			new Cockpit3DLabel(MilitaryPieces.Plate, 256, 32, MilitaryPalette.PlateBackground, MilitaryPalette.PlateText, 20f, false,
				new[] { new Cockpit3DLabelLine("BLADE · BLD-02", 23f) })
		},
		new Cockpit3DScene(MilitaryPalette.LightWhite, MilitaryPalette.HemisphereSky, MilitaryPalette.HemisphereGround,
			MilitaryPalette.Ambient, MilitaryPalette.ScreenLight, MilitaryPalette.BoostLight, MilitaryPalette.DamageLight,
			1.2f),
		Cockpit3DFont.Mono,
		true,
		MilitaryPalette.Role,
		MilitaryPalette.Look,
		true);

	// Factor de la luz de una pantalla de BLADE sobre el ajuste LargeScreenLight
	public static float MilitaryLightFactor(float width, float height) =>
		MilitaryScreenLight * width * height / MilitaryScreenArea / RacingLargeScreenLight;

	private static float RacingLightOffset(float depth) => depth - RacingSurfaceOffset + ScreenLightDistance;

	private static Cockpit3DScreen MilitaryScreen(string piece, int width, int height, float widthM, float heightM,
		Cockpit3DScreenContent content) =>
		new(piece, width, height, content, Cockpit3DScreenLight.Large, MilitaryLightFactor(widthM, heightM),
			ScreenLightDistance - MilitarySurfaceOffset);

	private static Cockpit3DLabel Lamp(string piece, string second, uint color, Cockpit3DLamp lamp) =>
		new(piece, 256, 112, MilitaryPalette.LampBackground, MilitaryPalette.LampText, 40f, true,
			new[] { new Cockpit3DLabelLine("MASTER", 50f), new Cockpit3DLabelLine(second, 94f) }, color, lamp);
}

// Nombres de las piezas de la cabina 3D de BLADE: el contrato con el script de Blender (spec docs/cabinas-3d-2-blade.md,
// 3.7). Godot las busca así.
public static class MilitaryPieces
{
	public const string ScreenHud = "Screen_HUD";
	public const string ScreenSpeed = "Screen_Speed";
	public const string ScreenRadar = "Screen_Radar";
	public const string ScreenMap = "Screen_Map";
	public const string LampCaution = "Lamp_Caution";
	public const string LampWarning = "Lamp_Warning";
	public const string Plate = "Plate";
	public const string CombinerPlate = "Combiner_Plate";
	public const string GlassCanopy = "Glass_Canopy";
	public const string GlassSmudge = "Glass_Smudge";

	public static readonly string[] Mfds = { ScreenSpeed, ScreenRadar, ScreenMap };
	public static readonly string[] Accents = { "Accent_Speed", "Accent_Radar", "Accent_Map", "Accent_Glare" };
	public static readonly string[] VisorLeds = { "Led_Visor_Speed", "Led_Visor_Radar", "Led_Visor_Map" };
	public static readonly string[] Leds = { "Led_Nose_L", "Led_Nose_R", "Led_Sensor" };

	public static string Shade(string screen) => screen + "_Shade";

	// Las luces que titilan con daño (spec, 6.6): acentos y LEDs; ni las pantallas ni los pilotos MASTER
	public static readonly IReadOnlyList<string> FlickerLights = BuildFlickerLights();

	public static readonly IReadOnlyList<string> Contract = BuildContract();

	private static IReadOnlyList<string> BuildFlickerLights()
	{
		var names = new List<string>(Accents);
		names.AddRange(VisorLeds);
		names.AddRange(Leds);
		return names;
	}

	private static IReadOnlyList<string> BuildContract()
	{
		var names = new List<string> { ScreenHud };
		names.AddRange(Mfds);
		foreach (string screen in Mfds)
			names.Add(Shade(screen));
		names.AddRange(Accents);
		names.AddRange(VisorLeds);
		names.AddRange(Leds);
		names.AddRange(new[] { LampCaution, LampWarning, Plate, CombinerPlate, GlassCanopy, GlassSmudge });
		return names;
	}
}

// Pilotos MASTER de BLADE (spec docs/cabinas-3d-2-blade.md, 6.5): cuánto se multiplica el color de cada uno. CAUTION se
// enciende con daño 1 o más; WARN parpadea con daño 3, con el mismo parpadeo del daño. En TIME TRIAL, los dos apagados.
// No titilan con el parpadeo de las luces. Lógica pura.
public static class MasterLamps
{
	public const float CautionOn = 2.2f;
	public const float WarningOn = 2.6f;
	public const float Off = Cockpit3DLayouts.LampOffFactor;

	public static float Caution(int damageLevel, bool damageEnabled) =>
		damageEnabled && damageLevel >= 1 ? CautionOn : Off;

	public static float Warning(int damageLevel, bool damageEnabled, bool blinkOn) =>
		damageEnabled && damageLevel >= DamageModel.MaxLevel && blinkOn ? WarningOn : Off;

	public static float For(Cockpit3DLamp lamp, int damageLevel, bool damageEnabled, bool blinkOn) => lamp switch
	{
		Cockpit3DLamp.Caution => Caution(damageLevel, damageEnabled),
		Cockpit3DLamp.Warning => Warning(damageLevel, damageEnabled, blinkOn),
		_ => 1f
	};
}
