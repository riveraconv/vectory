using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vectory.Records;

namespace Vectory.Ghost;

// Dónde se guardan los fantasmas: un archivo de texto por circuito (reutiliza IRecordStorage)
// y borrado de todos (RESET RECORDS)
public interface IGhostStorage
{
	// Almacenamiento del fantasma de un circuito
	IRecordStorage For(string circuitId);

	// Borra todos los fantasmas guardados
	void DeleteAll();
}

// Fantasmas en memoria, para las pruebas
public sealed class MemoryGhostStorage : IGhostStorage
{
	public Dictionary<string, MemoryRecordStorage> Files { get; } = new();

	public IRecordStorage For(string circuitId)
	{
		if (!Files.TryGetValue(circuitId, out MemoryRecordStorage storage))
		{
			storage = new MemoryRecordStorage();
			Files[circuitId] = storage;
		}
		return storage;
	}

	public void DeleteAll() => Files.Clear();
}

// Formato del archivo de un fantasma (spec ghost 6.2). Las muestras van compactas: cada una es
// [tiempo, x, y, z, qx, qy, qz, qw]. Lectura tolerante, como records.json: los campos desconocidos
// se ignoran; solo lo marcado con [JsonRequired] es imprescindible.
public static class GhostJson
{
	public const int FormatVersion = 1;

	private const int SampleLength = 8;
	private const int TimeDecimals = 4;
	private const int PositionDecimals = 3;
	private const int RotationDecimals = 4;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private sealed class GhostFile
	{
		[JsonRequired] public int Version { get; set; }
		[JsonRequired] public string CircuitId { get; set; }
		public string TrackSignature { get; set; } = "";
		public int CheckpointCount { get; set; }
		public string ShipDesignId { get; set; } = "";
		[JsonRequired] public double LapTime { get; set; }
		public int SampleRate { get; set; }
		[JsonRequired] public double[][] Samples { get; set; }
	}

	public static string ToJson(GhostRecording ghost)
	{
		var samples = new double[ghost.Samples.Count][];
		for (int i = 0; i < samples.Length; i++)
		{
			GhostSample s = ghost.Samples[i];
			samples[i] = new[]
			{
				Math.Round(s.Time, TimeDecimals),
				Math.Round(s.Position.X, PositionDecimals),
				Math.Round(s.Position.Y, PositionDecimals),
				Math.Round(s.Position.Z, PositionDecimals),
				Math.Round(s.Rotation.X, RotationDecimals),
				Math.Round(s.Rotation.Y, RotationDecimals),
				Math.Round(s.Rotation.Z, RotationDecimals),
				Math.Round(s.Rotation.W, RotationDecimals)
			};
		}

		var file = new GhostFile
		{
			Version = FormatVersion,
			CircuitId = ghost.CircuitId ?? "",
			TrackSignature = ghost.TrackSignature ?? "",
			CheckpointCount = ghost.CheckpointCount,
			ShipDesignId = ghost.ShipDesignId ?? "",
			LapTime = ghost.LapTime,
			SampleRate = ghost.SampleRate,
			Samples = samples
		};
		return JsonSerializer.Serialize(file, JsonOptions);
	}

	// Lee un fantasma. Si está corrupto, es de una versión que no se entiende o le falta algo
	// imprescindible, devuelve false (y null).
	public static bool TryFromJson(string json, out GhostRecording ghost, out string error)
	{
		ghost = null;
		try
		{
			GhostFile file = JsonSerializer.Deserialize<GhostFile>(json, JsonOptions);
			if (file == null)
			{
				error = "empty file";
				return false;
			}
			if (file.Version < 1 || file.Version > FormatVersion)
			{
				error = $"unknown format version {file.Version}";
				return false;
			}
			if (file.CircuitId == null || file.Samples == null)
			{
				error = "missing circuitId or samples";
				return false;
			}

			var samples = new GhostSample[file.Samples.Length];
			for (int i = 0; i < samples.Length; i++)
			{
				double[] s = file.Samples[i];
				if (s == null || s.Length != SampleLength)
				{
					error = $"sample {i} is not valid";
					return false;
				}
				var rotation = new Quaternion((float)s[4], (float)s[5], (float)s[6], (float)s[7]);
				samples[i] = new GhostSample(s[0], new Vector3((float)s[1], (float)s[2], (float)s[3]),
					rotation.LengthSquared() > 0f ? Quaternion.Normalize(rotation) : Quaternion.Identity);
			}

			ghost = new GhostRecording
			{
				CircuitId = file.CircuitId,
				TrackSignature = file.TrackSignature ?? "",
				CheckpointCount = file.CheckpointCount,
				ShipDesignId = file.ShipDesignId ?? "",
				LapTime = file.LapTime,
				SampleRate = file.SampleRate,
				Samples = samples
			};
			error = null;
			return true;
		}
		catch (Exception e) when (e is JsonException || e is NotSupportedException || e is ArgumentException)
		{
			error = e.Message;
			return false;
		}
	}
}

// Carga, guarda y borra los fantasmas. Como RecordStore, nunca lanza excepciones por los archivos:
// si algo falla deja un aviso en Warning y sigue (sin fantasma, o sin guardar).
// Un circuito sin Id no tiene fantasma.
public sealed class GhostStore
{
	private readonly IGhostStorage _storage;

	// Último problema con los archivos (null si todo fue bien)
	public string Warning { get; private set; }

	public GhostStore(IGhostStorage storage)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
	}

	// Fantasma guardado del circuito, o null si no hay (o no se puede leer: queda como .bak)
	public GhostRecording Load(string circuitId)
	{
		Warning = null;
		if (!RecordBook.IsValidCircuitId(circuitId))
			return null;

		IRecordStorage storage;
		string text;
		try
		{
			storage = _storage.For(circuitId);
			text = storage.Read();
		}
		catch (Exception e)
		{
			Warning = $"ghost of '{circuitId}' could not be read ({e.Message}); no ghost";
			return null;
		}

		if (text == null)
			return null;

		if (GhostJson.TryFromJson(text, out GhostRecording ghost, out string error))
			return ghost;

		Warning = $"ghost file of '{circuitId}' is not valid ({error}); it was renamed to .bak";
		try
		{
			storage.MoveToBackup();
		}
		catch (Exception e)
		{
			Warning += $" (backup failed: {e.Message})";
		}
		return null;
	}

	// Guarda el fantasma en el archivo de su circuito (ghost.CircuitId)
	public bool Save(GhostRecording ghost)
	{
		Warning = null;
		if (ghost == null || !RecordBook.IsValidCircuitId(ghost.CircuitId))
			return false;

		try
		{
			_storage.For(ghost.CircuitId).Write(GhostJson.ToJson(ghost));
			return true;
		}
		catch (Exception e)
		{
			Warning = $"ghost of '{ghost.CircuitId}' could not be saved ({e.Message})";
			return false;
		}
	}

	public bool DeleteAll()
	{
		Warning = null;
		try
		{
			_storage.DeleteAll();
			return true;
		}
		catch (Exception e)
		{
			Warning = $"ghosts could not be deleted ({e.Message})";
			return false;
		}
	}
}
