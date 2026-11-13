using System;
using System.Collections.Generic;
using System.Linq;
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
	MilitaryMap,        // minimapa con el progreso de la vuelta (6.4)
	IndustrialSpeed,    // tira de velocidad con el boost (HAMMER, spec docs/cabinas-3d-3-hammer.md, 6.3)
	IndustrialDamage,   // casco en tres sectores (6.4)
	IndustrialRadar,    // radar con TGT y la nave más cercana (6.4)
	IndustrialMap       // minimapa con el progreso de la vuelta (6.4)
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
	Mono,   // Share Tech Mono
	Vt323   // pantallas CRT de HAMMER
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

// Luz de estado de HAMMER que cambia de color o late según el HUD (setas y balizas: IndustrialLamps)
public enum Cockpit3DStatusLampKind
{
	Seta,          // una por carga del boost; Index de 0 a 2, de izquierda a derecha
	BoostBeacon,
	DamageBeacon
}

public sealed record Cockpit3DStatusLamp(string Piece, Cockpit3DStatusLampKind Kind, int Index = 0);

// Esfera decorativa de un reloj pequeño (HAMMER, spec docs/cabinas-3d-3-hammer.md, 4.6): se pinta una vez, con la aguja
// fija en Value (0–1)
public sealed record Cockpit3DGauge(string Piece, string Label, float Value);

// Colores de las luces de la escena de la cabina, y cuánto pesa el tinte de daño en su luz. Las intensidades de la luz
// principal y de la lámpara son las del Inspector (las de VECTOR) × su factor, ajustado contra el prototipo de cada
// cabina; la lámpara tiene el color de la luz principal si no lleva el suyo. Hemisferio y ambiente: sus fuerzas en el
// prototipo.
public sealed record Cockpit3DScene(
	uint KeyLight,
	uint HemisphereSky,
	uint HemisphereGround,
	uint Ambient,
	uint ScreenLight,
	uint BoostLight,
	uint DamageLight,
	float DamageLightFactor,
	uint? LampLight = null,
	float KeyFactor = 1f,
	float LampFactor = 1f,
	float HemisphereStrength = Cockpit3DScene.VectorHemisphereStrength,
	float AmbientStrength = Cockpit3DScene.VectorAmbientStrength)
{
	public const float VectorHemisphereStrength = 0.6f;
	public const float VectorAmbientStrength = 0.45f;

	public uint Lamp => LampLight ?? KeyLight;
}

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
	bool BrightnessFollowsGlow,   // brillo de las pantallas × (0,8 + 0,4 × brillo), como BLADE; VECTOR, fijo
	IReadOnlyList<Cockpit3DGauge> Gauges = null,
	Cockpit3DFont? LabelFont = null,   // textos fijos y esferas, si no es la de las pantallas
	IReadOnlyList<Cockpit3DStatusLamp> StatusLamps = null)
{
	public IReadOnlyList<Cockpit3DStatusLamp> StatusLampList => StatusLamps ?? Array.Empty<Cockpit3DStatusLamp>();
	public IReadOnlyList<Cockpit3DGauge> GaugeFaces => Gauges ?? Array.Empty<Cockpit3DGauge>();
	public Cockpit3DFont LabelsFont => LabelFont ?? Font;

	public static Cockpit3DLayout For(Cockpit3DStyle style) => style switch
	{
		Cockpit3DStyle.Racing => Cockpit3DLayouts.Racing,
		Cockpit3DStyle.Military => Cockpit3DLayouts.Military,
		Cockpit3DStyle.Industrial => Cockpit3DLayouts.Industrial,
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

	// HAMMER (spec docs/cabinas-3d-3-hammer.md): pantallas CRT en VT323; rótulos y relojes, en Share Tech Mono
	public static readonly Cockpit3DLayout Industrial = new(
		Cockpit3DStyle.Industrial,
		IndustrialPieces.Contract,
		new[]
		{
			IndustrialScreen(IndustrialPieces.ScreenRadar, IndustrialScreenDrawing.RadarWidth, IndustrialScreenDrawing.RadarHeight,
				0.17f, 0.13f, Cockpit3DScreenContent.IndustrialRadar),
			IndustrialScreen(IndustrialPieces.ScreenDamage, IndustrialScreenDrawing.DamageSize, IndustrialScreenDrawing.DamageSize,
				0.12f, 0.12f, Cockpit3DScreenContent.IndustrialDamage),
			IndustrialScreen(IndustrialPieces.ScreenMap, IndustrialScreenDrawing.MapWidth, IndustrialScreenDrawing.MapHeight,
				0.17f, 0.13f, Cockpit3DScreenContent.IndustrialMap),
			IndustrialScreen(IndustrialPieces.ScreenSpeed, IndustrialScreenDrawing.SpeedWidth, IndustrialScreenDrawing.SpeedHeight,
				0.40f, 0.075f, Cockpit3DScreenContent.IndustrialSpeed)
		},
		IndustrialPieces.Screens.Select(IndustrialPieces.Shade).ToArray(),
		IndustrialPieces.FlickerLights,
		0,
		new[]
		{
			IndustrialLabel(IndustrialPieces.Plate, 0.13f, 0.016f, "HAMMER · HMR-03"),
			IndustrialLabel(IndustrialPieces.LabelBoost(1), 0.034f, 0.010f, "BST 1"),
			IndustrialLabel(IndustrialPieces.LabelBoost(2), 0.034f, 0.010f, "BST 2"),
			IndustrialLabel(IndustrialPieces.LabelBoost(3), 0.034f, 0.010f, "BST 3"),
			IndustrialLabel(IndustrialPieces.LabelAux, 0.17f, 0.011f, "E-STOP    IGN    MODE")
		},
		new Cockpit3DScene(IndustrialPalette.KeyLight, IndustrialPalette.HemisphereSky, IndustrialPalette.HemisphereGround,
			IndustrialPalette.Ambient, IndustrialPalette.ScreenLight, IndustrialPalette.BoostLight, IndustrialPalette.DamageLight,
			1.2f, IndustrialPalette.LampLight, IndustrialKeyFactor, IndustrialLampFactor, 0.55f, 0.5f),
		Cockpit3DFont.Vt323,
		true,
		IndustrialPalette.Role,
		IndustrialPalette.Look,
		true,
		new[]
		{
			new Cockpit3DGauge(IndustrialPieces.GaugeHydFace, "HYD", 0.62f),
			new Cockpit3DGauge(IndustrialPieces.GaugeTempFace, "TEMP", 0.38f)
		},
		Cockpit3DFont.Mono,
		new[]
		{
			new Cockpit3DStatusLamp(IndustrialPieces.BoostLamp(1), Cockpit3DStatusLampKind.Seta, 0),
			new Cockpit3DStatusLamp(IndustrialPieces.BoostLamp(2), Cockpit3DStatusLampKind.Seta, 1),
			new Cockpit3DStatusLamp(IndustrialPieces.BoostLamp(3), Cockpit3DStatusLampKind.Seta, 2),
			new Cockpit3DStatusLamp(IndustrialPieces.BeaconBoost, Cockpit3DStatusLampKind.BoostBeacon),
			new Cockpit3DStatusLamp(IndustrialPieces.BeaconDamage, Cockpit3DStatusLampKind.DamageBeacon)
		});

	// Luz principal y lámpara de HAMMER sobre las del Inspector (las de VECTOR), ajustadas midiendo zonas de la cabina
	// contra el prototipo. Sus proporciones en los prototipos (2,2 / 2,6 y 0,9 / 0,08) dejaban la cabina mucho más clara:
	// la lámpara, × 11 tal cual, sobre todo en el salpicadero.
	public const float IndustrialKeyFactor = 0.5f;
	public const float IndustrialLampFactor = 3f;

	// Textos fijos de HAMMER, como labelPlane del prototipo: lienzo de 256 px de ancho y alto max(16, redondeo(256 × alto /
	// ancho)), letra del 62 % del alto, centrada. La línea base es la de BLADE: el centro + 1 + 0,3 × la letra.
	private const int LabelCanvasWidth = 256;
	private const int LabelMinHeight = 16;
	private const float LabelFontFraction = 0.62f;
	private const float LabelBaselineFraction = 0.3f;

	public static Cockpit3DLabel IndustrialLabel(string piece, float widthM, float heightM, string text)
	{
		int height = Math.Max(LabelMinHeight, JsRound(LabelCanvasWidth * heightM / widthM));
		float font = JsRound(height * LabelFontFraction);
		return new Cockpit3DLabel(piece, LabelCanvasWidth, height, IndustrialPalette.LabelBackground, IndustrialPalette.LabelText,
			font, false, new[] { new Cockpit3DLabelLine(text, height / 2f + 1f + LabelBaselineFraction * font) });
	}

	// Math.round de JavaScript: la mitad hacia arriba
	private static int JsRound(float x) => (int)MathF.Floor(x + 0.5f);

	private static Cockpit3DScreen IndustrialScreen(string piece, int width, int height, float widthM, float heightM,
		Cockpit3DScreenContent content) =>
		new(piece, width, height, content, Cockpit3DScreenLight.Large, MilitaryLightFactor(widthM, heightM),
			ScreenLightDistance - MilitarySurfaceOffset);

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

// Nombres de las piezas de la cabina 3D de HAMMER: el contrato con el script de Blender
// (spec docs/cabinas-3d-3-hammer.md, 3.7). Godot las busca así.
public static class IndustrialPieces
{
	public const string ScreenRadar = "Screen_Radar";
	public const string ScreenDamage = "Screen_Damage";
	public const string ScreenMap = "Screen_Map";
	public const string ScreenSpeed = "Screen_Speed";
	public const string BeaconBoost = "Beacon_Boost_Lens";
	public const string BeaconDamage = "Beacon_Damage_Lens";
	public const string Plate = "Plate";
	public const string LabelAux = "Label_Aux";
	public const string GaugeHydFace = "Gauge_Hyd_Face";
	public const string GaugeTempFace = "Gauge_Temp_Face";
	public const string GlassFront = "Glass_Front";
	public const string GlassSmudge = "Glass_Smudge";
	public const int BoostLamps = 3;
	public const int PanelLeds = 6;

	public static readonly string[] Screens = { ScreenRadar, ScreenDamage, ScreenMap, ScreenSpeed };
	public static readonly string[] Accents = { "Accent_Radar", "Accent_Damage", "Accent_Map" };
	public static readonly string[] NavLeds = { "Led_Nose_L", "Led_Nose_R", "Led_Canard_L", "Led_Canard_R" };

	public static string Shade(string screen) => screen + "_Shade";
	public static string Glass(string screen) => screen + "_Glass";
	public static string BoostLamp(int i) => $"Lamp_Boost_{i}";      // 1–3, de izquierda a derecha
	public static string LabelBoost(int i) => $"Label_Boost_{i}";
	public static string PanelLed(int i) => $"Led_Panel_{i}";        // 1–6, de izquierda a derecha

	// Las luces que titilan con daño (spec, 6.6): acentos y LEDs; ni las pantallas, ni las setas, ni las balizas
	public static readonly IReadOnlyList<string> FlickerLights = BuildFlickerLights();

	public static readonly IReadOnlyList<string> Contract = BuildContract();

	private static IReadOnlyList<string> BuildFlickerLights()
	{
		var names = new List<string>(Accents);
		names.AddRange(NavLeds);
		for (int i = 1; i <= PanelLeds; i++)
			names.Add(PanelLed(i));
		return names;
	}

	// En el orden de la tabla 3.7
	private static IReadOnlyList<string> BuildContract()
	{
		var names = new List<string>(Screens);
		names.AddRange(Screens.Select(Shade));
		names.AddRange(Accents);
		names.AddRange(NavLeds);
		for (int i = 1; i <= BoostLamps; i++)
			names.Add(BoostLamp(i));
		names.AddRange(new[] { BeaconBoost, BeaconDamage, Plate });
		for (int i = 1; i <= BoostLamps; i++)
			names.Add(LabelBoost(i));
		names.Add(LabelAux);
		for (int i = 1; i <= PanelLeds; i++)
			names.Add(PanelLed(i));
		names.AddRange(new[] { GaugeHydFace, GaugeTempFace, GlassFront, GlassSmudge });
		names.AddRange(Screens.Select(Glass));
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
