using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Vectory.Records;

// Récords locales por circuito y por modo, y sus reglas (lógica pura, sin Godot).
//  - TIME TRIAL: mejor vuelta con sus tiempos de paso.
//  - RACE: mejor tiempo total y mejor vuelta, independientes entre sí y de TIME TRIAL.
//  - Solo cuenta lo que es estrictamente más rápido: un empate no es récord nuevo.
//  - Un circuito sin Id no guarda nada.
// Se convierte a / desde JSON con un número de versión del formato.
public sealed class RecordBook
{
	public const int FormatVersion = 1;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // archivo local: fechas legibles ("+01:00", no "+01:00")
	};

	private readonly Dictionary<string, CircuitRecords> _circuits = new();

	public static bool IsValidCircuitId(string circuitId) => !string.IsNullOrWhiteSpace(circuitId);

	public TimeTrialRecord GetTimeTrial(string circuitId)
	{
		return IsValidCircuitId(circuitId) && _circuits.TryGetValue(circuitId, out var c) ? c.TimeTrial : null;
	}

	public RaceRecord GetRace(string circuitId)
	{
		return IsValidCircuitId(circuitId) && _circuits.TryGetValue(circuitId, out var c) ? c.Race : null;
	}

	// Registra una vuelta de TIME TRIAL. Si es más rápida que el récord (o no había), pasa a ser el récord.
	public TimeTrialLapResult SubmitTimeTrialLap(string circuitId, double lapTime, IReadOnlyList<double> splits, DateTime when)
	{
		if (!IsValidCircuitId(circuitId))
			return new TimeTrialLapResult(false, null);

		TimeTrialRecord current = GetTimeTrial(circuitId);
		double? previous = current?.LapTime;
		if (previous.HasValue && !(lapTime < previous.Value))
			return new TimeTrialLapResult(false, previous);

		Circuit(circuitId).TimeTrial = new TimeTrialRecord
		{
			LapTime = lapTime,
			Splits = splits?.ToArray() ?? Array.Empty<double>(),
			Date = Stamp(when)
		};
		return new TimeTrialLapResult(true, previous);
	}

	// Registra una carrera terminada: tiempo total y mejor vuelta se comparan y guardan por separado
	public RaceRecordResult SubmitRace(string circuitId, double totalTime, double bestLap, DateTime when)
	{
		if (!IsValidCircuitId(circuitId))
			return new RaceRecordResult(false, false);

		RaceRecord race = Circuit(circuitId).Race ??= new RaceRecord();

		bool newTotal = race.TotalTime == null || totalTime < race.TotalTime.Time;
		if (newTotal)
			race.TotalTime = new TimedRecord { Time = totalTime, Date = Stamp(when) };

		bool newLap = race.BestLap == null || bestLap < race.BestLap.Time;
		if (newLap)
			race.BestLap = new TimedRecord { Time = bestLap, Date = Stamp(when) };

		return new RaceRecordResult(newTotal, newLap);
	}

	public string ToJson()
	{
		var file = new RecordFile { Version = FormatVersion, Circuits = new Dictionary<string, CircuitRecords>(_circuits) };
		return JsonSerializer.Serialize(file, JsonOptions);
	}

	// Lee un archivo de récords. Si está corrupto o es de otra versión, devuelve false (y un libro vacío).
	public static bool TryFromJson(string json, out RecordBook book, out string error)
	{
		book = new RecordBook();
		try
		{
			RecordFile file = JsonSerializer.Deserialize<RecordFile>(json, JsonOptions);
			if (file == null)
			{
				error = "empty file";
				return false;
			}
			if (file.Version != FormatVersion)
			{
				error = $"unknown format version {file.Version}";
				return false;
			}

			foreach (var (id, records) in file.Circuits ?? new Dictionary<string, CircuitRecords>())
			{
				if (IsValidCircuitId(id) && records != null)
					book._circuits[id] = records;
			}

			error = null;
			return true;
		}
		catch (Exception e) when (e is JsonException || e is NotSupportedException || e is ArgumentException)
		{
			book = new RecordBook();
			error = e.Message;
			return false;
		}
	}

	private CircuitRecords Circuit(string circuitId)
	{
		if (!_circuits.TryGetValue(circuitId, out var c))
		{
			c = new CircuitRecords();
			_circuits[circuitId] = c;
		}
		return c;
	}

	private static string Stamp(DateTime when) => when.ToString("o", CultureInfo.InvariantCulture);
}
