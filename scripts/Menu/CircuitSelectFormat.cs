using System;
using System.Globalization;
using Vectory.Game;
using Vectory.Records;

namespace Vectory.Menu;

// Textos de la pantalla SELECT CIRCUIT (lógica pura, en inglés)
public static class CircuitSelectFormat
{
	public const string Free = "FREE";
	public const string None = "—";
	public const string GhostAvailable = "AVAILABLE";
	public const string GhostNone = "NONE";
	public const string TestTag = "TEST";

	// "RACE · SELECT CIRCUIT" / "TIME TRIAL · SELECT CIRCUIT"
	public static string Header(GameMode mode) => $"{ModeName(mode)} · SELECT CIRCUIT";

	// Cabecera de los récords: "RECORDS · RACE" / "RECORDS · TIME TRIAL"
	public static string RecordsHeader(GameMode mode) => $"RECORDS · {ModeName(mode)}";

	public static string ModeName(GameMode mode) => mode == GameMode.TimeTrial ? "TIME TRIAL" : "RACE";

	// "CLOSED LOOP · CLOCKWISE"; en un circuito de pruebas "TEST TRACK · …"
	public static string Description(bool isTest, bool clockwise) =>
		$"{(isTest ? "TEST TRACK" : "CLOSED LOOP")} · {(clockwise ? "CLOCKWISE" : "COUNTERCLOCKWISE")}";

	// "01 / 02"
	public static string Counter(int index, int count) =>
		$"{(index + 1).ToString("00", CultureInfo.InvariantCulture)} / {count.ToString("00", CultureInfo.InvariantCulture)}";

	// 4213 m -> "4.2 KM"
	public static string Length(float meters) =>
		$"{Math.Round(meters / 1000.0, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)} KM";

	// Recta más larga, redondeada a 10 m: 816 -> "820 M"
	public static string Straight(float meters) =>
		$"{(Math.Round(meters / 10.0, MidpointRounding.AwayFromZero) * 10).ToString("0", CultureInfo.InvariantCulture)} M";

	// Ancho de pista: "24 M"
	public static string Width(float meters) =>
		$"{Math.Round(meters, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)} M";

	// Peralte máximo: "0°"
	public static string Banking(float degrees) =>
		$"{Math.Round(degrees, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)}°";

	// Vueltas: las de RACE ("3") o FREE en TIME TRIAL
	public static string Laps(GameMode mode, int raceLaps) =>
		mode == GameMode.TimeTrial ? Free : raceLaps.ToString(CultureInfo.InvariantCulture);

	// Parrilla: "8 SHIPS" en RACE, "—" en TIME TRIAL
	public static string Grid(GameMode mode, int slots) =>
		mode == GameMode.TimeTrial ? None : $"{slots.ToString(CultureInfo.InvariantCulture)} SHIPS";

	public static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

	// Etiquetas de la escala del perfil: "+20 M" / "−20 M"
	public static string ScaleTop(float meters) => $"+{Math.Round(meters).ToString("0", CultureInfo.InvariantCulture)} M";

	public static string ScaleBottom(float meters) => $"−{Math.Round(meters).ToString("0", CultureInfo.InvariantCulture)} M";

	// Etiqueta de cada sector del perfil: "S1", "S2"…
	public static string Sector(int index) => $"S{(index + 1).ToString(CultureInfo.InvariantCulture)}";

	// Tiempo de un récord o --:--.---
	public static string Time(double? seconds) => RecordFormat.Time(seconds);

	public static string Ghost(bool available) => available ? GhostAvailable : GhostNone;
}
