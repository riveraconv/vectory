using System;
using System.Collections.Generic;
using System.Linq;

namespace Vectory.Settings;

// Tipo de control de un ajuste (spec docs/settings.md, 3.2)
public enum SettingKind
{
	Choice,    // ‹ VALOR ›: con A/D no da la vuelta; con ENTER, sí
	Percent,   // 0–100 % en pasos del 10 %
	Toggle,    // OFF / ON
	Key        // una tecla de conducción
}

// Un ajuste: id (también su clave en el archivo), grupo, nombre en pantalla, tipo, valores (listas), valor por defecto
// (índice; en los porcentajes, pasos de 10 %; en sí / no, 0 o 1), explicación —con [b]…[/b] en lo resaltado— y si está
// en COMING SOON (no se elige ni se guarda). Applied: false si se ve pero aún no hace nada.
public sealed record SettingDef(string Id, string Group, string Label, SettingKind Kind, IReadOnlyList<string> Values, int Default,
	string Description, bool ComingSoon = false, bool Applied = true)
{
	public int Max => Kind switch
	{
		SettingKind.Choice => Math.Max(0, Values.Count - 1),
		SettingKind.Percent => SettingsCatalog.PercentSteps,
		SettingKind.Toggle => 1,
		_ => 0
	};
}

// Catálogo de ajustes (spec 4): todos, en el orden de la lista, con los textos de designs/settings.html. Lógica pura.
public static class SettingsCatalog
{
	public const int PercentSteps = 10;   // 0–100 % en pasos del 10 %

	public const string Gameplay = "GAMEPLAY";
	public const string Video = "VIDEO";
	public const string Effects = "EFFECTS";
	public const string Controls = "CONTROLS";
	public const string Audio = "AUDIO";

	public const string Difficulty = "difficulty";
	public const string DisplayMode = "displayMode";
	public const string Resolution = "resolution";
	public const string Bloom = "bloom";
	public const string TunnelVision = "tunnelVision";
	public const string SpeedLines = "speedLines";
	public const string SpeedFov = "speedFov";
	public const string Particles = "particles";
	public const string CockpitShake = "cockpitShake";

	public static readonly string[] Difficulties = { "EASY", "NORMAL", "HARD" };
	public static readonly string[] DisplayModes = { "WINDOWED", "FULLSCREEN", "BORDERLESS" };
	public static readonly string[] Resolutions = { "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440", "3840 × 2160" };

	private const string KeyHelp = "Press ENTER and then the new key. If the key is in use, the two actions swap keys.";

	private static SettingDef KeyRow(string id, string label, string key, bool fixedKey = false) =>
		new(id, Controls, label, SettingKind.Key, new[] { key }, 0, fixedKey ? "Pause is always ESC." : KeyHelp);

	public static IReadOnlyList<SettingDef> All { get; } = Array.AsReadOnly(new[]
	{
		new SettingDef(Difficulty, Gameplay, "DIFFICULTY", SettingKind.Choice, Difficulties, 1,
			"How good the rivals are. It only changes the [b]AI[/b], never the physics of any ship. Takes effect in the [b]next race[/b]."),

		// BORDERLESS es lo de antes de SETTINGS (decisión del usuario). La resolución, por defecto la mayor que cabe.
		new SettingDef(DisplayMode, Video, "DISPLAY MODE", SettingKind.Choice, DisplayModes, 2,
			"Windowed, exclusive fullscreen or a borderless window the size of the screen."),
		new SettingDef(Resolution, Video, "RESOLUTION", SettingKind.Choice, Resolutions, 2,
			"Render resolution. The HUD and the menus scale with it."),
		new SettingDef(Bloom, Video, "BLOOM", SettingKind.Percent, null, PercentSteps,
			"Glow of the lines of the track, the ships and the HUD. Lower it if the image looks blurry."),

		// Visión túnel es de efectos-velocidad, que aún no existe: COMING SOON (decisión del usuario). PARTICLES, desde la
		// spec docs/efectos-1-choques.md: chispas y humo de los choques
		new SettingDef(TunnelVision, Effects, "TUNNEL VISION", SettingKind.Percent, null, PercentSteps,
			"Blur and darkening of the edges of the screen at high speed. [b]Lower it if it makes you dizzy.[/b]", ComingSoon: true),
		new SettingDef(SpeedLines, Effects, "SPEED LINES", SettingKind.Toggle, null, 1,
			"Streaks that fly past the camera at high speed. Also turns off the [b]slipstream air lines[/b] in the cockpit."),
		new SettingDef(SpeedFov, Effects, "SPEED FOV", SettingKind.Percent, null, PercentSteps,
			"How much the field of view opens up at top speed and with the boost."),
		new SettingDef(Particles, Effects, "PARTICLES", SettingKind.Choice, new[] { "LOW", "MEDIUM", "HIGH" }, 2,
			"Amount of sparks and smoke from crashes. Lower it if the game runs slow."),
		new SettingDef(CockpitShake, Effects, "COCKPIT SHAKE", SettingKind.Percent, null, PercentSteps,
			"Vibration of the cockpit view at high speed and with the boost. [b]Lower it if it makes you dizzy.[/b] At 0 the cockpit stays still."),

		// Las teclas (KeyBindings): Values[0] es solo la de por defecto; la actual, en SettingsModel.Keys
		KeyRow("keyAccelerate", "ACCELERATE", "W"),
		KeyRow("keyBrake", "BRAKE", "S"),
		KeyRow("keySteerLeft", "STEER LEFT", "A"),
		KeyRow("keySteerRight", "STEER RIGHT", "D"),
		KeyRow("keyBoost", "BOOST", "SHIFT"),
		KeyRow("keyDrift", "DRIFT", "SPACE"),
		KeyRow("keyCamera", "CAMERA", "C"),
		KeyRow("keyPause", "PAUSE", "ESC", true),

		new SettingDef("audioMaster", Audio, "MASTER", SettingKind.Percent, null, 8, "", ComingSoon: true),
		new SettingDef("audioMusic", Audio, "MUSIC", SettingKind.Percent, null, 7, "", ComingSoon: true),
		new SettingDef("audioEngines", Audio, "ENGINES", SettingKind.Percent, null, 8, "", ComingSoon: true),
		new SettingDef("audioEffects", Audio, "EFFECTS", SettingKind.Percent, null, 8, "", ComingSoon: true)
	});

	public static IReadOnlyList<string> Groups { get; } = Array.AsReadOnly(new[] { Gameplay, Video, Effects, Controls, Audio });

	// Un grupo entero en COMING SOON (AUDIO)
	public static bool GroupComingSoon(string group) => All.Where(d => d.Group == group).All(d => d.ComingSoon);

	public static SettingDef Find(string id) => All.FirstOrDefault(d => d.Id == id);
}
