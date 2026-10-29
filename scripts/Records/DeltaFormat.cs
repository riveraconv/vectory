using System;

namespace Vectory.Records;

// Delta respecto al récord: tiempo actual − tiempo del récord en el mismo punto.
// Negativo = mejor (se muestra con "-"), positivo = peor ("+"), cero = "±0.000" y cuenta como mejor.
// Se redondea al milisegundo, como TimeFormat.
public static class DeltaFormat
{
	public static double Delta(double current, double reference) => current - reference;

	// Mejor o igual que el récord
	public static bool IsBetter(double delta) => delta < 0 || Milliseconds(delta) == 0;

	// "-0.145", "+0.312", "±0.000"; con 60 s o más, "+1:02.315"
	public static string Format(double delta)
	{
		long ms = Milliseconds(delta);
		if (ms == 0)
			return "±0.000";

		string sign = delta < 0 ? "-" : "+";
		if (ms < 60_000)
			return $"{sign}{ms / 1000}.{ms % 1000:000}";

		return $"{sign}{ms / 60_000}:{ms / 1000 % 60:00}.{ms % 1000:000}";
	}

	private static long Milliseconds(double delta)
	{
		return (long)Math.Round(Math.Abs(delta) * 1000.0, MidpointRounding.AwayFromZero);
	}
}
