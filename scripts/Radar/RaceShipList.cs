using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Radar;

// Una nave en carrera que no es la del jugador: su posición y su orientación
public readonly struct RaceShip
{
	public readonly int Id;
	public readonly Vector3 Position;
	public readonly Vector3 Forward;
	public readonly Vector3 Up;

	public RaceShip(int id, Vector3 position, Vector3 forward, Vector3 up)
	{
		Id = id;
		Position = position;
		Forward = forward;
		Up = up;
	}
}

// Las demás naves de la carrera (spec docs/radar-minimapa.md, 3.1): hoy las de prueba; mañana, los rivales. No sabe nada
// del HUD. El fantasma no entra nunca, ni la nave del jugador. Cada nave se identifica por su Id, así varias fuentes
// pueden escribir en la misma lista sin pisarse.
public sealed class RaceShipList
{
	private readonly List<RaceShip> _ships = new();

	public IReadOnlyList<RaceShip> Ships => _ships;

	// Añade la nave o actualiza la que tenga ese Id
	public void Set(in RaceShip ship)
	{
		for (int i = 0; i < _ships.Count; i++)
		{
			if (_ships[i].Id == ship.Id)
			{
				_ships[i] = ship;
				return;
			}
		}
		_ships.Add(ship);
	}

	public void Remove(int id)
	{
		for (int i = 0; i < _ships.Count; i++)
		{
			if (_ships[i].Id == id)
			{
				_ships.RemoveAt(i);
				return;
			}
		}
	}
}
