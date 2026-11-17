using System;
using System.Collections.Generic;
using System.Linq;

namespace Vectory.Settings;

// Una acción de conducción que se ve en CONTROLS (spec docs/settings.md, 6.1): id de la fila (y del archivo), acción del
// mapa de entrada, tecla por defecto (la primera del mapa) y la segunda tecla, que no cambia (0 si no tiene). Fixed: no
// se puede cambiar (PAUSE). Las teclas son códigos físicos de Godot (Key) como long, sin depender de Godot.
public sealed record KeyAction(string Id, string Action, long Default, long Secondary = 0, bool Fixed = false);

// Qué ha pasado al pulsar una tecla esperando la nueva (spec 6.2)
public enum RebindResult
{
	Assigned,     // la tiene la acción
	Swapped,      // la tenía otra acción de la tabla: se han intercambiado (Other)
	Unchanged,    // ya era la suya
	Cancelled,    // ESC: se deja la que había
	Reserved,     // F1–F12: no vale; sigue esperando
	InUse,        // es la segunda tecla fija de otra acción (Other): no vale; sigue esperando
	Fixed         // la acción no se puede cambiar
}

public readonly record struct RebindOutcome(RebindResult Result, string Other = null)
{
	// Si sigue esperando otra tecla
	public bool KeepWaiting => Result is RebindResult.Reserved or RebindResult.InUse;
}

// Teclas de conducción de SETTINGS (spec 6): la primera tecla de cada acción de la tabla. Cambiarla a la de otra acción
// las intercambia; ESC cancela; F1–F12 (depuración) no valen; PAUSE es siempre ESC. Las segundas teclas (las flechas) y
// las de los menús no se tocan nunca: una tecla que es la segunda de otra acción no vale, así ninguna tecla hace dos
// cosas. Lógica pura.
public sealed class KeyBindings
{
	public const long Escape = 4194305;
	public const long F1 = 4194332;
	public const long F12 = 4194343;

	public static IReadOnlyList<KeyAction> Actions { get; } = Array.AsReadOnly(new[]
	{
		new KeyAction("keyAccelerate", "accelerate", 'W', KeyNames.Up),
		new KeyAction("keyBrake", "brake", 'S', KeyNames.Down),
		new KeyAction("keySteerLeft", "steer_left", 'A', KeyNames.Left),
		new KeyAction("keySteerRight", "steer_right", 'D', KeyNames.Right),
		new KeyAction("keyBoost", "boost", KeyNames.Shift),
		new KeyAction("keyDrift", "drift", ' '),
		new KeyAction("keyCamera", "camera", 'C'),
		new KeyAction("keyPause", "pause", Escape, Fixed: true)
	});

	public static KeyAction Find(string id) => Actions.FirstOrDefault(a => a.Id == id);

	// F1–F12, reservadas para depuración
	public static bool IsReserved(long key) => key >= F1 && key <= F12;

	private readonly Dictionary<string, long> _keys = new();

	public event Action<string> Changed;

	public KeyBindings()
	{
		foreach (KeyAction action in Actions)
			_keys[action.Id] = action.Default;
	}

	public long Get(string id) => _keys.TryGetValue(id, out long key) ? key : 0;

	// La acción de la tabla cuya primera tecla es esta (null si ninguna)
	public string OwnerOf(long key) => Actions.FirstOrDefault(a => _keys[a.Id] == key)?.Id;

	// Pulsar una tecla esperando la nueva de una acción
	public RebindOutcome Rebind(string id, long key)
	{
		KeyAction action = Find(id);
		if (action == null || action.Fixed)
			return new RebindOutcome(RebindResult.Fixed);
		if (key == Escape)
			return new RebindOutcome(RebindResult.Cancelled);
		if (IsReserved(key))
			return new RebindOutcome(RebindResult.Reserved);
		long current = _keys[id];
		if (key == current)
			return new RebindOutcome(RebindResult.Unchanged);
		KeyAction secondaryOwner = Actions.FirstOrDefault(a => a.Id != id && a.Secondary != 0 && a.Secondary == key);
		if (secondaryOwner != null)
			return new RebindOutcome(RebindResult.InUse, secondaryOwner.Id);

		string other = OwnerOf(key);
		_keys[id] = key;
		Changed?.Invoke(id);
		if (other == null)
			return new RebindOutcome(RebindResult.Assigned);
		_keys[other] = current;
		Changed?.Invoke(other);
		return new RebindOutcome(RebindResult.Swapped, other);
	}

	// Al cargar: una tecla que valga para esa acción (ni ESC, ni F1–F12, ni la segunda de otra); sin intercambiar
	public bool IsValidFor(string id, long key)
	{
		KeyAction action = Find(id);
		if (action == null || key <= 0)
			return false;
		if (action.Fixed)
			return key == action.Default;
		return key != Escape && !IsReserved(key) && !Actions.Any(a => a.Id != id && a.Secondary == key);
	}

	public bool Set(string id, long key)
	{
		if (!IsValidFor(id, key))
			return false;
		if (_keys[id] != key)
		{
			_keys[id] = key;
			Changed?.Invoke(id);
		}
		return true;
	}

	// Si hay dos acciones con la misma tecla (un archivo tocado a mano)
	public bool HasDuplicates => _keys.Values.Distinct().Count() != _keys.Count;

	public void Reset()
	{
		foreach (KeyAction action in Actions)
		{
			if (_keys[action.Id] == action.Default)
				continue;
			_keys[action.Id] = action.Default;
			Changed?.Invoke(action.Id);
		}
	}

	public void CopyFrom(KeyBindings other)
	{
		foreach (KeyAction action in Actions)
			_keys[action.Id] = other.Get(action.Id);
	}
}

// Nombre en pantalla y en el archivo de una tecla física de Godot (Key): "W", "SHIFT", "SPACE", "UP"… Las que no están en
// la tabla, "#código". Lógica pura.
public static class KeyNames
{
	public const long Special = 1L << 22;
	public const long Left = Special + 15;
	public const long Up = Special + 16;
	public const long Right = Special + 17;
	public const long Down = Special + 18;
	public const long Shift = Special + 21;

	private static readonly Dictionary<long, string> Names = BuildNames();
	private static readonly Dictionary<string, long> Codes =
		Names.ToDictionary(p => p.Value, p => p.Key, StringComparer.OrdinalIgnoreCase);

	private static Dictionary<long, string> BuildNames()
	{
		var names = new Dictionary<long, string>
		{
			[' '] = "SPACE", ['\''] = "'", [','] = ",", ['-'] = "-", ['.'] = ".", ['/'] = "/", [';'] = ";", ['='] = "=",
			['['] = "[", ['\\'] = "\\", [']'] = "]", ['`'] = "`",
			[Special + 1] = "ESC", [Special + 2] = "TAB", [Special + 4] = "BACKSPACE", [Special + 5] = "ENTER",
			[Special + 6] = "KP ENTER", [Special + 7] = "INSERT", [Special + 8] = "DELETE", [Special + 13] = "HOME",
			[Special + 14] = "END", [Left] = "LEFT", [Up] = "UP", [Right] = "RIGHT", [Down] = "DOWN",
			[Special + 19] = "PAGE UP", [Special + 20] = "PAGE DOWN", [Shift] = "SHIFT", [Special + 22] = "CTRL",
			[Special + 23] = "META", [Special + 24] = "ALT", [Special + 25] = "CAPS LOCK",
			[Special + 129] = "KP *", [Special + 130] = "KP /", [Special + 131] = "KP -", [Special + 132] = "KP .",
			[Special + 133] = "KP +"
		};
		for (char c = 'A'; c <= 'Z'; c++)
			names[c] = c.ToString();
		for (char c = '0'; c <= '9'; c++)
			names[c] = c.ToString();
		for (int i = 0; i < 12; i++)
			names[KeyBindings.F1 + i] = $"F{i + 1}";
		for (int i = 0; i <= 9; i++)
			names[Special + 134 + i] = $"KP {i}";
		return names;
	}

	public static string Name(long key) => Names.TryGetValue(key, out string name) ? name : $"#{key}";

	// El código de un nombre (sin distinguir mayúsculas); también "#código"
	public static bool TryParse(string text, out long key)
	{
		key = 0;
		if (string.IsNullOrWhiteSpace(text))
			return false;
		text = text.Trim();
		if (Codes.TryGetValue(text, out key))
			return true;
		return text.StartsWith('#') && long.TryParse(text.AsSpan(1), out key) && key > 0;
	}
}
