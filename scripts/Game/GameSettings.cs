using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vectory.Records;

namespace Vectory.Game;

// Ajustes del juego que se guardan entre partidas (user://settings.json). De momento solo el fantasma
// de TIME TRIAL; aquí irán los demás cuando exista SETTINGS.
public sealed class GameSettings
{
	public bool GhostEnabled { get; set; } = true;
}

// Carga y guarda los ajustes con las mismas reglas que los récords: lectura tolerante (lo que falte
// toma su valor por defecto), escritura segura y .bak si el archivo está corrupto. Nunca lanza excepciones.
public sealed class SettingsStore
{
	public const string DefaultPath = "user://settings.json";
	public const int FormatVersion = 1;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	private sealed class SettingsFile
	{
		[JsonRequired] public int Version { get; set; }
		public bool? GhostEnabled { get; set; }
	}

	private readonly IRecordStorage _storage;

	public GameSettings Settings { get; private set; } = new();

	// Último problema con el archivo (null si todo fue bien)
	public string Warning { get; private set; }

	public SettingsStore(IRecordStorage storage)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
	}

	public void Load()
	{
		Warning = null;
		Settings = new GameSettings();

		string text;
		try
		{
			text = _storage.Read();
		}
		catch (Exception e)
		{
			Warning = $"settings could not be read ({e.Message}); using defaults";
			return;
		}

		if (text == null)
			return;

		string error;
		try
		{
			SettingsFile file = JsonSerializer.Deserialize<SettingsFile>(text, JsonOptions);
			if (file != null && file.Version >= 1 && file.Version <= FormatVersion)
			{
				Settings.GhostEnabled = file.GhostEnabled ?? true;
				return;
			}
			error = file == null ? "empty file" : $"unknown format version {file.Version}";
		}
		catch (Exception e) when (e is JsonException || e is NotSupportedException || e is ArgumentException)
		{
			error = e.Message;
		}

		Warning = $"settings file is not valid ({error}); it was renamed to .bak and defaults are used";
		try
		{
			_storage.MoveToBackup();
		}
		catch (Exception e)
		{
			Warning += $" (backup failed: {e.Message})";
		}
	}

	public bool Save()
	{
		Warning = null;
		try
		{
			var file = new SettingsFile { Version = FormatVersion, GhostEnabled = Settings.GhostEnabled };
			_storage.Write(JsonSerializer.Serialize(file, JsonOptions));
			return true;
		}
		catch (Exception e)
		{
			Warning = $"settings could not be saved ({e.Message})";
			return false;
		}
	}
}
