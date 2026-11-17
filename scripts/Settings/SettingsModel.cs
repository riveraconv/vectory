using System;
using System.Collections.Generic;
using Vectory.Rivals;

namespace Vectory.Settings;

// Los valores de SETTINGS (spec docs/settings.md, 9): uno por ajuste del catálogo que no está en COMING SOON y no es
// una tecla (las teclas, en la etapa 3). Lista: índice; porcentaje: pasos de 10 %; sí / no: 0 o 1. Avisa con Changed.
// Lógica pura.
public sealed class SettingsModel
{
	private readonly Dictionary<string, int> _values = new();

	public event Action<string> Changed;

	public SettingsModel()
	{
		foreach (SettingDef def in SettingsCatalog.All)
		{
			if (Holds(def))
				_values[def.Id] = def.Default;
		}
	}

	// Si el modelo lleva este ajuste (los de COMING SOON y las teclas, no)
	public static bool Holds(SettingDef def) => def != null && !def.ComingSoon && def.Kind != SettingKind.Key;

	public IEnumerable<string> Ids => _values.Keys;

	public int Get(string id) => _values.TryGetValue(id, out int value) ? value : SettingsCatalog.Find(id)?.Default ?? 0;

	// Valor válido para ese ajuste: dentro de su rango
	public static bool IsValid(SettingDef def, int value) => def != null && value >= 0 && value <= def.Max;

	// Pone un valor; false (y no cambia nada) si el ajuste no está o el valor no vale
	public bool Set(string id, int value)
	{
		SettingDef def = SettingsCatalog.Find(id);
		if (!Holds(def) || !IsValid(def, value))
			return false;
		if (_values[id] == value)
			return true;
		_values[id] = value;
		Changed?.Invoke(id);
		return true;
	}

	// A / D: un paso, sin pasar del primero ni del último (en sí / no, cambia)
	public bool Change(string id, int direction)
	{
		SettingDef def = SettingsCatalog.Find(id);
		if (!Holds(def) || direction == 0)
			return false;
		int value = Get(id);
		int next = def.Kind == SettingKind.Toggle ? 1 - value : Math.Clamp(value + Math.Sign(direction), 0, def.Max);
		return next != value && Set(id, next);
	}

	// ENTER: en una lista, el siguiente dando la vuelta; en sí / no, cambia; en un porcentaje, nada
	public bool Cycle(string id)
	{
		SettingDef def = SettingsCatalog.Find(id);
		if (!Holds(def))
			return false;
		return def.Kind switch
		{
			SettingKind.Choice => Set(id, (Get(id) + 1) % (def.Max + 1)),
			SettingKind.Toggle => Set(id, 1 - Get(id)),
			_ => false
		};
	}

	// Todos a su valor por defecto
	public void Reset()
	{
		foreach (SettingDef def in SettingsCatalog.All)
		{
			if (Holds(def))
				Set(def.Id, def.Default);
		}
	}

	public SettingsModel Clone()
	{
		var copy = new SettingsModel();
		foreach (KeyValuePair<string, int> pair in _values)
			copy._values[pair.Key] = pair.Value;
		return copy;
	}

	// Texto del valor (cabecera de la izquierda: NORMAL, 70 %, ON)
	public string ValueText(string id)
	{
		SettingDef def = SettingsCatalog.Find(id);
		if (def == null)
			return "";
		if (def.ComingSoon)
			return "COMING SOON";
		int value = Get(id);
		return def.Kind switch
		{
			SettingKind.Choice => def.Values[value],
			SettingKind.Percent => $"{value * 10} %",
			SettingKind.Toggle => value != 0 ? "ON" : "OFF",
			_ => def.Values?.Count > 0 ? def.Values[0] : ""
		};
	}

	// Lo que leen el juego y los aplicadores
	public Difficulty Difficulty => (Difficulty)Get(SettingsCatalog.Difficulty);
	public float Bloom => Fraction(SettingsCatalog.Bloom);
	public bool SpeedLines => Get(SettingsCatalog.SpeedLines) != 0;
	public float SpeedFov => Fraction(SettingsCatalog.SpeedFov);
	public float CockpitShake => Fraction(SettingsCatalog.CockpitShake);

	private float Fraction(string id) => Get(id) / (float)SettingsCatalog.PercentSteps;
}
