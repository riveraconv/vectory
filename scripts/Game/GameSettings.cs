using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vectory.Records;
using Vectory.Settings;
using Vectory.Ship;

namespace Vectory.Game;

// Ajustes del juego que se guardan entre partidas (user://settings.json): el fantasma de TIME TRIAL, la última
// nave elegida, la pintura de cada nave y los de la pantalla SETTINGS (Options, spec docs/settings.md, 5).
public sealed class GameSettings
{
	public bool GhostEnabled { get; set; } = true;

	// Los de SETTINGS, en sus secciones (gameplay, video, effects)
	public SettingsModel Options { get; set; } = new();

	// Última nave con la que se pulsó START; null = ninguna. Se guarda tal cual: si ya no se puede elegir, lo decide
	// ShipCatalog.StartShip al arrancar.
	public string LastShipDesignId { get; set; }

	// Pintura guardada de cada nave (spec docs/selector-pinturas.md, 3.2), solo las que tienen entrada y son válidas
	private readonly Dictionary<string, PaintChoice> _shipPaints = new();

	public IReadOnlyDictionary<string, PaintChoice> ShipPaints => _shipPaints;

	// La pintura guardada de una nave o, sin entrada, la de por defecto de su catálogo; null si la nave no existe o no
	// tiene casco 3D
	public PaintChoice? PaintFor(string shipId)
	{
		if (HullPaints.StyleOf(shipId) is not HullStyle style)
			return null;
		return _shipPaints.TryGetValue(shipId, out PaintChoice choice) ? choice : HullPaints.Default(style);
	}

	// Guarda en memoria la pintura de una nave; false (y no cambia nada) si la nave o la pintura no valen
	public bool SetPaint(string shipId, PaintChoice choice)
	{
		if (HullPaints.StyleOf(shipId) is not HullStyle style || !HullPaints.HasPaint(style, choice.PaintId))
			return false;
		_shipPaints[shipId] = choice;
		return true;
	}
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

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string LastShipDesignId { get; set; }

		// Se lee a mano (ReadShipPaints): un valor raro dentro no debe hacer que el archivo entero cuente como corrupto
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public JsonElement? ShipPaints { get; set; }

		// SETTINGS (spec docs/settings.md, 5): se leen valor a valor (SettingsSerializer)
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public JsonElement? Gameplay { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public JsonElement? Video { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public JsonElement? Effects { get; set; }
	}

	private sealed class PaintEntry
	{
		public string Paint { get; set; }
		public bool Marks { get; set; }
	}

	private readonly IRecordStorage _storage;

	public GameSettings Settings { get; private set; } = new();

	// Último problema con el archivo (null si todo fue bien)
	public string Warning { get; private set; }

	// Valores de SETTINGS que no valían y han tomado su valor por defecto (spec docs/settings.md, 5)
	public List<string> OptionWarnings { get; } = new();

	// Resoluciones que caben en la pantalla (spec docs/settings.md, 4.2); -1 = todas
	public int ResolutionCount { get; set; } = -1;

	public SettingsStore(IRecordStorage storage)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
	}

	public void Load()
	{
		Warning = null;
		OptionWarnings.Clear();
		Settings = new GameSettings { Options = new SettingsModel(ResolutionCount) };

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
				Settings.LastShipDesignId = file.LastShipDesignId;
				ReadShipPaints(file.ShipPaints, Settings);
				Settings.Options = SettingsSerializer.Read(Sections(file), OptionWarnings, ResolutionCount);
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

	// Lectura tolerante de shipPaints (spec 3.2): nave desconocida o sin casco 3D, ignorada; pintura que no es de su
	// catálogo, sin entrada (la de por defecto); marks ausente o no booleano, el de la pintura por defecto
	private static void ReadShipPaints(JsonElement? element, GameSettings settings)
	{
		if (element is not JsonElement paints || paints.ValueKind != JsonValueKind.Object)
			return;
		foreach (JsonProperty ship in paints.EnumerateObject())
		{
			if (HullPaints.StyleOf(ship.Name) is not HullStyle style || ship.Value.ValueKind != JsonValueKind.Object)
				continue;
			if (!ship.Value.TryGetProperty("paint", out JsonElement paint) || paint.ValueKind != JsonValueKind.String)
				continue;
			bool marks = HullPaints.Default(style).Marks;
			if (ship.Value.TryGetProperty("marks", out JsonElement value)
				&& (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
				marks = value.GetBoolean();
			settings.SetPaint(ship.Name, new PaintChoice(paint.GetString(), marks));
		}
	}

	private static Dictionary<string, JsonElement> Sections(SettingsFile file)
	{
		var sections = new Dictionary<string, JsonElement>();
		void Add(string group, JsonElement? element)
		{
			if (element is JsonElement value)
				sections[SettingsSerializer.SectionOf(group)] = value;
		}
		Add(SettingsCatalog.Gameplay, file.Gameplay);
		Add(SettingsCatalog.Video, file.Video);
		Add(SettingsCatalog.Effects, file.Effects);
		return sections;
	}

	private static JsonElement? WriteShipPaints(GameSettings settings)
	{
		if (settings.ShipPaints.Count == 0)
			return null;
		var entries = settings.ShipPaints.OrderBy(p => p.Key, StringComparer.Ordinal)
			.ToDictionary(p => p.Key, p => new PaintEntry { Paint = p.Value.PaintId, Marks = p.Value.Marks });
		return JsonSerializer.SerializeToElement(entries, JsonOptions);
	}

	private static JsonElement? SectionOrNull(Dictionary<string, JsonElement> sections, string group) =>
		sections.TryGetValue(SettingsSerializer.SectionOf(group), out JsonElement value) ? value : null;

	public bool Save()
	{
		Warning = null;
		try
		{
			var file = new SettingsFile
			{
				Version = FormatVersion,
				GhostEnabled = Settings.GhostEnabled,
				LastShipDesignId = Settings.LastShipDesignId,
				ShipPaints = WriteShipPaints(Settings)
			};
			Dictionary<string, JsonElement> sections = SettingsSerializer.Write(Settings.Options);
			file.Gameplay = SectionOrNull(sections, SettingsCatalog.Gameplay);
			file.Video = SectionOrNull(sections, SettingsCatalog.Video);
			file.Effects = SectionOrNull(sections, SettingsCatalog.Effects);
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
