using System;
using System.Globalization;
using Vectory.Game;

namespace Vectory.Menu;

// Textos de la pantalla SELECT SHIP (lógica pura, en inglés)
public static class ShipSelectFormat
{
	public const string None = CircuitSelectFormat.None;
	public const string ComingSoon = "COMING SOON";
	public const string InDevelopment = "SHIP IN DEVELOPMENT";
	public const string DamageOff = "OFF";
	public const string FooterHint = "← → SHIP · ↑ ↓ OPTION · ENTER SELECT · DRAG ROTATE";

	private const double KmhPerMs = 3.6;
	private const double ReferenceSpeedKmh = 200.0;   // la de los datos de aceleración y frenada

	// "RACE · CIRCUIT 01 · SELECT SHIP"
	public static string Header(GameMode mode, string circuitName) =>
		$"{CircuitSelectFormat.ModeName(mode)} · {(circuitName ?? "").ToUpperInvariant()} · SELECT SHIP";

	// "02 / 03"
	public static string Counter(int index, int count) => CircuitSelectFormat.Counter(index, count);

	// 110 m/s -> "396 KM/H"
	public static string TopSpeed(float metersPerSecond) => $"{Round(metersPerSecond * KmhPerMs)} KM/H";

	// 25 m/s² -> "0–200 · 2.2 S"
	public static string Acceleration(float metersPerSecond2) => $"0–200 · {SecondsTo200(metersPerSecond2)} S";

	// 45 m/s² -> "200–0 · 1.2 S"
	public static string Braking(float metersPerSecond2) => $"200–0 · {SecondsTo200(metersPerSecond2)} S";

	// 1,3 rad/s -> "74 °/S"
	public static string Handling(float radiansPerSecond) => $"{Round(radiansPerSecond * 180.0 / Math.PI)} °/S";

	// "3 × +100 KM/H"
	public static string Boost(int charges, float speedGainKmh) =>
		$"{charges.ToString(CultureInfo.InvariantCulture)} × +{Round(speedGainKmh)} KM/H";

	// "3 LEVELS" en RACE; en TIME TRIAL no hay daños
	public static string Damage(GameMode mode, int levels) =>
		mode == GameMode.TimeTrial ? DamageOff : $"{levels.ToString(CultureInfo.InvariantCulture)} LEVELS";

	private static string Round(double value) =>
		Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

	private static string SecondsTo200(float metersPerSecond2)
	{
		double seconds = metersPerSecond2 > 0f ? ReferenceSpeedKmh / KmhPerMs / metersPerSecond2 : 0.0;
		return Math.Round(seconds, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);
	}
}
