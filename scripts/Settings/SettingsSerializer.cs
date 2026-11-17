using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vectory.Settings;

// SETTINGS ↔ secciones del archivo de ajustes (spec docs/settings.md, 5): una por grupo (gameplay, video, effects),
// valor a valor. Lista: su texto ("NORMAL", "1920x1080"); porcentaje: 0–100 en pasos de 10; sí / no: true / false.
// Un valor que falta, de otro tipo o fuera de rango toma su valor por defecto, solo él, y se apunta en Warnings.
// Lógica pura.
public static class SettingsSerializer
{
	// Clave de cada grupo en el archivo
	public static string SectionOf(string group) => group.ToLowerInvariant();

	// Texto de un valor de lista en el archivo: sin espacios y con x ("1920 × 1080" → "1920x1080")
	public static string FileText(string label) => label.Replace(" ", "").Replace("×", "x");

	// Las secciones con los valores del modelo (las que tienen alguno)
	public static Dictionary<string, JsonElement> Write(SettingsModel model)
	{
		var sections = new Dictionary<string, JsonObject>();
		foreach (SettingDef def in SettingsCatalog.All.Where(SettingsModel.Holds))
		{
			string section = SectionOf(def.Group);
			if (!sections.TryGetValue(section, out JsonObject obj))
				sections[section] = obj = new JsonObject();
			int value = model.Get(def.Id);
			obj[def.Id] = def.Kind switch
			{
				SettingKind.Choice => JsonValue.Create(FileText(def.Values[value])),
				SettingKind.Percent => JsonValue.Create(value * 10),
				SettingKind.Toggle => JsonValue.Create(value != 0),
				_ => null
			};
		}
		return sections.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));
	}

	// Un modelo con lo que haya en las secciones; lo que falte o no valga, por defecto, con su aviso
	// resolutionCount: cuántas resoluciones caben en la pantalla (-1 = todas); una guardada que no cabe pasa a la mayor
	// que cabe (spec 4.2)
	public static SettingsModel Read(IReadOnlyDictionary<string, JsonElement> sections, List<string> warnings, int resolutionCount = -1)
	{
		var model = new SettingsModel(resolutionCount);
		foreach (SettingDef def in SettingsCatalog.All.Where(SettingsModel.Holds))
		{
			string section = SectionOf(def.Group);
			// Sin la sección (un archivo de antes de SETTINGS), todo por defecto sin avisar
			if (sections == null || !sections.TryGetValue(section, out JsonElement obj))
				continue;
			if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(def.Id, out JsonElement element))
			{
				warnings?.Add($"{section}.{def.Id} missing; using the default");
				continue;
			}
			if (TryValue(def, element, out int value, out string problem))
			{
				if (def.Id == SettingsCatalog.Resolution && value > model.MaxOf(def))
				{
					warnings?.Add($"{section}.{def.Id} {def.Values[value]} does not fit on this screen; using {def.Values[model.MaxOf(def)]}");
					value = model.MaxOf(def);
				}
				model.Set(def.Id, value);
			}
			else
				warnings?.Add($"{section}.{def.Id} {problem}; using the default");
		}
		return model;
	}

	private static bool TryValue(SettingDef def, JsonElement element, out int value, out string problem)
	{
		value = def.Default;
		problem = null;
		switch (def.Kind)
		{
			case SettingKind.Choice:
				if (element.ValueKind != JsonValueKind.String)
				{
					problem = "is not a text";
					return false;
				}
				string text = element.GetString() ?? "";
				int index = def.Values.Select(FileText).ToList().FindIndex(v => string.Equals(v, text, StringComparison.OrdinalIgnoreCase));
				if (index < 0)
				{
					problem = $"'{text}' is not a valid value";
					return false;
				}
				value = index;
				return true;

			case SettingKind.Percent:
				if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int percent))
				{
					problem = "is not a whole number";
					return false;
				}
				if (percent < 0 || percent > 100 || percent % 10 != 0)
				{
					problem = $"{percent} is out of range (0–100 in steps of 10)";
					return false;
				}
				value = percent / 10;
				return true;

			case SettingKind.Toggle:
				if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
				{
					problem = "is not true or false";
					return false;
				}
				value = element.GetBoolean() ? 1 : 0;
				return true;

			default:
				problem = "is not stored";
				return false;
		}
	}
}
