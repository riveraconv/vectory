using System;
using System.Collections.Generic;

namespace Vectory.Menu;

// Navegación de un menú vertical: qué opción está seleccionada y cuál se elige.
// Lógica pura (sin Godot): el nodo del menú traduce teclado y ratón a estas llamadas.
//
// - La selección salta las opciones desactivadas y da la vuelta al llegar a un extremo.
// - Al crearse, se selecciona la primera opción activa.
// - Si no hay ninguna activa, SelectedIndex es -1 y nada hace efecto.
public sealed class MenuModel
{
	private readonly MenuOption[] _options;

	public IReadOnlyList<MenuOption> Options { get; }

	// Índice de la opción seleccionada (-1 si no hay ninguna activa)
	public int SelectedIndex { get; private set; } = -1;

	public MenuModel(IEnumerable<MenuOption> options)
	{
		if (options == null)
			throw new ArgumentNullException(nameof(options));

		_options = new List<MenuOption>(options).ToArray();
		Options = Array.AsReadOnly(_options);

		for (int i = 0; i < _options.Length; i++)
		{
			if (_options[i].Enabled)
			{
				SelectedIndex = i;
				break;
			}
		}
	}

	public void MoveNext()
	{
		Move(1);
	}

	public void MovePrevious()
	{
		Move(-1);
	}

	// Selecciona una opción concreta (ratón). Si está desactivada o no existe, no cambia nada.
	public void Select(int index)
	{
		if (index < 0 || index >= _options.Length || !_options[index].Enabled)
			return;

		SelectedIndex = index;
	}

	// Identificador de la opción elegida, o null si no hay ninguna activa seleccionada
	public string Confirm()
	{
		if (SelectedIndex < 0)
			return null;

		return _options[SelectedIndex].Id;
	}

	// Avanza en la dirección indicada hasta la siguiente opción activa, dando la vuelta
	private void Move(int step)
	{
		if (SelectedIndex < 0)
			return;

		int n = _options.Length;
		int index = SelectedIndex;
		for (int i = 0; i < n; i++)
		{
			index = ((index + step) % n + n) % n;
			if (_options[index].Enabled)
			{
				SelectedIndex = index;
				return;
			}
		}
	}
}
