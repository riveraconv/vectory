using System.Collections.Generic;
using Godot;
using Vectory.Circuit;
using Vectory.Radar;
using Vectory.Ship;

namespace Vectory.Rivals;

// Los rivales de una carrera de RACE (spec docs/rivales-1-parrilla.md, 3): crea una nave por entrada de la alineación,
// la coloca en su marca y la apunta en la lista de naves en carrera, que leen el radar y el minimapa. Los cascos van en
// su propia capa con un solo juego de luces, colgado del cuerpo del jugador (gira con su nave, así en la cámara exterior
// los rivales se ven iluminados como ella) y que no se apaga en la cabina, como sí lo hacen las luces del casco propio.
// Sin reglas: la alineación es de RivalLineup.
public partial class RivalField : Node3D
{
	private readonly List<RivalShip> _ships = new();
	private RaceShipList _raceShips;

	public IReadOnlyList<RivalShip> Ships => _ships;

	// Después de añadirlo al árbol: con el circuito ya generado y el jugador ya creado
	public void Setup(IReadOnlyList<RivalEntry> lineup, Track track, Player player, RaceShipList raceShips)
	{
		_raceShips = raceShips;
		Hull3DSettings hullSettings = player.BuildHull3DSettings();

		var lights = new HullLightRig { Name = "RivalHullLights", Layer = ShipHull3D.RivalLayer };
		lights.Apply(hullSettings);
		player.AddChild(lights);

		foreach (RivalEntry entry in lineup)
		{
			var ship = new RivalShip();
			ship.Setup(entry, hullSettings);
			AddChild(ship);
			ship.PlaceOnGrid(track);
			_ships.Add(ship);
			raceShips.Set(ship.ToRaceShip());
			GD.Print($"[Race] {entry.Id}: grid {entry.GridPosition}, {ShipCatalog.Find(entry.ShipDesignId)?.Name ?? entry.ShipDesignId}, "
				+ $"{HullPaints.DisplayName(HullPaints.StyleOf(entry.ShipDesignId) ?? HullStyle.Racing, entry.PaintIndex)}");
		}
	}

	// Al salir de la escena, fuera de la lista
	public override void _ExitTree()
	{
		if (_raceShips == null)
			return;
		foreach (RivalShip ship in _ships)
			_raceShips.Remove(ship.Entry.Number);
	}
}
