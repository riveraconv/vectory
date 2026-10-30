using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Vectory.Records;

// Lectura tolerante del archivo (para poder ampliar el formato sin romper archivos existentes):
// los campos desconocidos se ignoran y los opcionales (fecha, tiempos de paso...) pueden faltar.
// Solo lo marcado con [JsonRequired] es imprescindible: si falta, el archivo se trata como corrupto.

// Un tiempo récord y cuándo se hizo (fecha local yyyy-MM-dd; los récords antiguos pueden traer
// la fecha ISO completa o no traer ninguna)
public sealed class TimedRecord
{
	[JsonRequired] public double Time { get; set; }
	public string Date { get; set; } = "";
}

// Récord de TIME TRIAL de un circuito: la mejor vuelta y sus tiempos de paso por cada checkpoint
// (tiempo de vuelta acumulado desde la meta al cruzar cada uno).
public sealed class TimeTrialRecord
{
	[JsonRequired] public double LapTime { get; set; }
	public double[] Splits { get; set; } = Array.Empty<double>();
	public string Date { get; set; } = "";

	// Los tiempos de paso solo sirven para el delta si son del mismo número de checkpoints que el circuito actual
	public bool SplitsMatch(int checkpointCount) => Splits != null && Splits.Length == checkpointCount && checkpointCount > 0;
}

// Récords de RACE de un circuito: mejor tiempo total y mejor vuelta, por separado
public sealed class RaceRecord
{
	public TimedRecord TotalTime { get; set; }
	public TimedRecord BestLap { get; set; }
}

// Récords de un circuito, por modo
public sealed class CircuitRecords
{
	public TimeTrialRecord TimeTrial { get; set; }
	public RaceRecord Race { get; set; }
}

// Qué récords de RACE ha batido una carrera terminada
public readonly record struct RaceRecordResult(bool NewTotalTime, bool NewBestLap);

// Lo que pasó con una vuelta de TIME TRIAL frente al récord
public readonly record struct TimeTrialLapResult(bool IsNewRecord, double? PreviousLapTime);

// Formato del archivo: versión y récords por Id de circuito
public sealed class RecordFile
{
	[JsonRequired] public int Version { get; set; }
	public Dictionary<string, CircuitRecords> Circuits { get; set; } = new();
}
