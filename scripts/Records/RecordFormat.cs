using System;
using System.Globalization;
using Vectory.Core;

namespace Vectory.Records;

// Textos de los récords para la pantalla RECORDS (lógica pura).
// Fechas en inglés y mayúsculas ("29 OCT 2026"), siempre igual sea cual sea el idioma del sistema.
public static class RecordFormat
{
	// Formato con el que se guarda la fecha de un récord
	public const string DateFormat = "yyyy-MM-dd";

	public const string NoTime = "--:--.---";
	public const string NoRecord = "NO RECORD";
	public const string NoDate = "—";

	private static readonly string[] Months =
		{ "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

	// Tiempo del récord, o --:--.--- si no hay
	public static string Time(double? seconds)
	{
		return seconds.HasValue ? TimeFormat.Format(seconds.Value) : NoTime;
	}

	// Fecha del récord: "29 OCT 2026"; NO RECORD si no hay récord; "—" si el récord no tiene fecha o no se puede leer
	public static string Date(bool hasRecord, string storedDate)
	{
		if (!hasRecord)
			return NoRecord;

		if (!TryParseDate(storedDate, out DateTime date))
			return NoDate;

		return $"{date.Day} {Months[date.Month - 1]} {date.Year}";
	}

	// Acepta la fecha guardada (yyyy-MM-dd) y, por compatibilidad, la fecha ISO completa de récords anteriores
	private static bool TryParseDate(string stored, out DateTime date)
	{
		date = default;
		if (string.IsNullOrWhiteSpace(stored))
			return false;

		if (DateTime.TryParseExact(stored.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
			return true;

		if (DateTimeOffset.TryParse(stored.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset full))
		{
			date = full.Date;
			return true;
		}

		return false;
	}
}
