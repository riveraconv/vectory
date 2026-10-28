using System;

namespace Vectory.Core;

// Formato de tiempos de carrera (m:ss.mmm), el mismo en HUD, consola y resultados
public static class TimeFormat
{
	// Se redondea al milisegundo más cercano: un tiempo como 11,902 s se guarda en double como
	// 11,90199999... y, sin redondear, se mostraría como 0:11.901.
	public static string Format(double seconds)
	{
		var t = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000.0));
		return $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds:000}";
	}
}
