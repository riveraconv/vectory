using System;
using System.Collections.Generic;
using Vectory.Game;

namespace Vectory.Menu;

// Qué tiene que hacer quien usa SELECT CIRCUIT tras una llamada
public enum CircuitSelectAction { None, Start, BackToMenu }

// Lógica de la pantalla SELECT CIRCUIT (sin Godot): modo, circuito que se ve y opciones SHIP SELECTION / BACK.
// No carga escenas ni toca GameSession: devuelve qué hay que hacer.
//
//   Al abrirse: el circuito recordado (si está en la lista) o el primero, con SHIP SELECTION seleccionada.
//   Circuito anterior / siguiente con vuelta (sin efecto con un solo circuito).
//   SHIP SELECTION -> Start (con CircuitPath); BACK o ESC -> BackToMenu.
public sealed class CircuitSelectLogic
{
	public const string StartId = "START";
	public const string BackId = "BACK";
	public const string StartLabel = "SHIP SELECTION";

	private readonly IReadOnlyList<string> _paths;

	public GameMode Mode { get; }

	public int CircuitCount => _paths.Count;

	// Circuito que se muestra (índice en la lista de circuitos)
	public int CircuitIndex { get; private set; }

	// Ruta del TrackDefinition del circuito que se muestra
	public string CircuitPath => _paths[CircuitIndex];

	public bool CanChangeCircuit => CircuitCount > 1;

	// SHIP SELECTION / BACK
	public MenuModel Menu { get; }

	// circuitPaths: los del catálogo, en orden; currentPath: el recordado (GameSession), o null
	public CircuitSelectLogic(GameMode mode, IReadOnlyList<string> circuitPaths, string currentPath)
	{
		if (circuitPaths == null)
			throw new ArgumentNullException(nameof(circuitPaths));
		if (circuitPaths.Count < 1)
			throw new ArgumentOutOfRangeException(nameof(circuitPaths), "Debe haber al menos 1 circuito.");

		Mode = mode;
		_paths = circuitPaths;

		for (int i = 0; i < circuitPaths.Count; i++)
		{
			if (circuitPaths[i] == currentPath)
			{
				CircuitIndex = i;
				break;
			}
		}

		Menu = CreateMenu();
	}

	// Las opciones de la pantalla, con la primera seleccionada
	public static MenuModel CreateMenu() => new(new[]
	{
		new MenuOption(StartId, StartLabel, true),
		new MenuOption(BackId, "BACK", true)
	});

	public void NextCircuit() => MoveCircuit(1);

	public void PreviousCircuit() => MoveCircuit(-1);

	// ESC
	public CircuitSelectAction Back() => CircuitSelectAction.BackToMenu;

	// Elige una opción (por su identificador)
	public CircuitSelectAction Choose(string id)
	{
		return id switch
		{
			StartId => CircuitSelectAction.Start,
			BackId => CircuitSelectAction.BackToMenu,
			_ => CircuitSelectAction.None
		};
	}

	private void MoveCircuit(int step)
	{
		if (!CanChangeCircuit)
			return;

		CircuitIndex = ((CircuitIndex + step) % CircuitCount + CircuitCount) % CircuitCount;
	}
}
