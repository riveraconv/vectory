using System.Collections.Generic;
using Godot;
using Vectory.Settings;

namespace Vectory.Game;

// Aplica las teclas de CONTROLS al mapa de entrada (spec docs/settings.md, 6): en cada acción de la tabla cambia la
// primera tecla y deja las demás (las flechas) como están. Las acciones de los menús no se tocan. Si una acción no
// existe en el mapa, avisa una vez. Solo lo que cambia. Las reglas, en KeyBindings.
public static class InputApplier
{
	private static readonly Dictionary<string, long> _applied = new();
	private static readonly HashSet<string> _warned = new();

	public static void Apply(KeyBindings keys)
	{
		foreach (KeyAction action in KeyBindings.Actions)
		{
			if (action.Fixed)
				continue;
			long key = keys.Get(action.Id);
			if (_applied.TryGetValue(action.Action, out long done) && done == key)
				continue;
			if (!InputMap.HasAction(action.Action))
			{
				if (_warned.Add(action.Action))
					GD.PushWarning($"[Settings] input action '{action.Action}' does not exist");
				continue;
			}
			SetFirstKey(action.Action, key);
			_applied[action.Action] = key;
		}
	}

	private static void SetFirstKey(StringName action, long key)
	{
		var events = new List<InputEvent>(InputMap.ActionGetEvents(action));
		int first = events.FindIndex(e => e is InputEventKey);
		var replacement = new InputEventKey { PhysicalKeycode = (Key)key, Device = -1 };   // -1: cualquier teclado, como las del proyecto
		if (first >= 0 && events[first] is InputEventKey current && (long)current.PhysicalKeycode == key)
			return;
		if (first >= 0)
			events[first] = replacement;
		else
			events.Insert(0, replacement);
		InputMap.ActionEraseEvents(action);
		foreach (InputEvent e in events)
			InputMap.ActionAddEvent(action, e);
	}
}
