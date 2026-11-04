using System;
using System.Collections.Generic;
using Vectory.Game;
using Vectory.Ship;

namespace Vectory.Menu;

// Qué tiene que hacer quien usa SELECT SHIP tras una llamada
public enum ShipSelectAction { None, Start, BackToCircuitSelect }

// Lógica de la pantalla SELECT SHIP (sin Godot): modo, circuito (para el texto), nave que se ve y START / BACK.
// No carga escenas ni toca GameSession: devuelve qué hay que hacer.
//
//   Al abrirse: la nave recordada si es elegible; si no, la de por defecto, con START seleccionada.
//   Nave anterior / siguiente con vuelta, pasando por las no elegibles (START desactivada y BACK seleccionada).
//   START -> Start (con ShipId); BACK o ESC -> BackToCircuitSelect.
public sealed class ShipSelectLogic
{
	public const string StartId = "START";
	public const string BackId = "BACK";

	private readonly IReadOnlyList<ShipEntry> _ships;

	public GameMode Mode { get; }

	public string CircuitName { get; }

	public int ShipCount => _ships.Count;

	public int ShipIndex { get; private set; }

	public ShipEntry Ship => _ships[ShipIndex];

	public string ShipId => Ship.Id;

	public bool CanChangeShip => ShipCount > 1;

	// START / BACK de la nave actual. Cambia con la nave: quien dibuja las opciones debe volver a leerlo.
	public MenuModel Menu { get; private set; }

	// ships: las del catálogo, en orden; currentId: la recordada (GameSession); defaultId: la de por defecto
	public ShipSelectLogic(GameMode mode, string circuitName, IReadOnlyList<ShipEntry> ships, string currentId, string defaultId)
	{
		if (ships == null)
			throw new ArgumentNullException(nameof(ships));
		if (ships.Count < 1)
			throw new ArgumentOutOfRangeException(nameof(ships), "Debe haber al menos 1 nave.");

		Mode = mode;
		CircuitName = circuitName;
		_ships = ships;

		int current = IndexOf(currentId);
		int start = current >= 0 && ships[current].Selectable ? current : IndexOf(defaultId);
		ShipIndex = Math.Max(0, start);
		Menu = BuildMenu();
	}

	public void NextShip() => MoveShip(1);

	public void PreviousShip() => MoveShip(-1);

	// ESC
	public ShipSelectAction Back() => ShipSelectAction.BackToCircuitSelect;

	// Elige una opción (por su identificador)
	public ShipSelectAction Choose(string id)
	{
		return id switch
		{
			StartId when Ship.Selectable => ShipSelectAction.Start,
			BackId => ShipSelectAction.BackToCircuitSelect,
			_ => ShipSelectAction.None
		};
	}

	private void MoveShip(int step)
	{
		if (!CanChangeShip)
			return;

		ShipIndex = ((ShipIndex + step) % ShipCount + ShipCount) % ShipCount;
		Menu = BuildMenu();
	}

	private int IndexOf(string id)
	{
		for (int i = 0; i < _ships.Count; i++)
		{
			if (_ships[i].Id == id)
				return i;
		}
		return -1;
	}

	// MenuModel selecciona la primera activa: START en una elegible y BACK en las demás
	private MenuModel BuildMenu() => new(new[]
	{
		new MenuOption(StartId, "START", Ship.Selectable),
		new MenuOption(BackId, "BACK", true)
	});
}
