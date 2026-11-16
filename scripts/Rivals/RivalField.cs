using System.Collections.Generic;
using System.Linq;
using Godot;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Radar;
using Vectory.Ship;

namespace Vectory.Rivals;

// Los rivales de una carrera de RACE (spec docs/rivales-1-parrilla.md, 3 y 4): monta la caché de trazadas (solo las naves
// de la parrilla), crea la carrera pura de cada rival (RivalCar) y su nave (RivalShip), y los apunta en la lista de naves
// en carrera, que leen el radar y el minimapa. Los cascos van en su propia capa con un solo juego de luces, colgado del
// cuerpo del jugador (gira con su nave, así en la cámara exterior los rivales se ven iluminados como ella) y que no se
// apaga en la cabina, como sí lo hacen las luces del casco propio. Sin reglas: la alineación es de RivalLineup y la
// conducción, de RivalCar.
public partial class RivalField : Node3D
{
	private readonly List<RivalShip> _ships = new();
	private RaceShipList _raceShips;

	public IReadOnlyList<RivalShip> Ships => _ships;

	// Ritmo de todos los rivales: el margen de velocidad del piloto en las curvas
	public float Pace { get; private set; } = RivalDriver.DefaultPace;

	// Las cargas de los destellos de la barrera de cada rival, para pintarlos con el del jugador
	public IEnumerable<BarrierFlash> Flashes => _ships.Select(s => s.Car.Flash);

	// Después de añadirlo al árbol: con el circuito ya generado, el jugador ya creado y la carrera ya montada
	public void Setup(IReadOnlyList<RivalEntry> lineup, Track track, Player player, RaceSession race, RaceShipList raceShips,
		RivalRaceContext context, float pace, float cooldownPace)
	{
		_raceShips = raceShips;
		Pace = pace;
		Hull3DSettings hullSettings = player.BuildHull3DSettings();
		var look = new RivalLook(hullSettings, player.BuildBankSettings(), player.BuildAttitudeSettings(),
			player.BuildFlickerSettings(), player.BuildIndustrialLampSettings());

		var lights = new HullLightRig { Name = "RivalHullLights", Layer = ShipHull3D.RivalLayer };
		lights.Apply(hullSettings);
		player.AddChild(lights);

		ulong start = Time.GetTicksUsec();
		var pilots = new PilotCache(context.Frame, context.TrackWidth, context.Physics, AutopilotSettings.Default);
		ShipDefinition[] definitions = lineup.Select(e => ShipDefinitions.Resolve(e.ShipDesignId, message => GD.PushWarning(message))).ToArray();
		pilots.Prepare(definitions);
		for (int i = 0; i < lineup.Count; i++)
		{
			RivalEntry entry = lineup[i];
			ShipDefinition definition = definitions[i];
			var car = new RivalCar(entry, definition, pilots, context, pace);
			car.Driver.CooldownPace = cooldownPace;
			var ship = new RivalShip();
			ship.Setup(car, race, raceShips, look, player.FlickerOpacity);
			AddChild(ship);
			_ships.Add(ship);
			GD.Print($"[Race] {entry.Id}: grid {entry.GridPosition}, {ShipCatalog.Find(entry.ShipDesignId)?.Name ?? entry.ShipDesignId}, "
				+ $"{HullPaints.DisplayName(HullPaints.StyleOf(entry.ShipDesignId) ?? HullStyle.Racing, entry.PaintIndex)}");
		}
		GD.Print($"[Race] Rival pilots ready: {pilots.ComputedShips.Count} ships in {(Time.GetTicksUsec() - start) / 1000.0:0} ms");
	}

	// Cambia el ritmo de todos los rivales al momento (panel de pruebas)
	public void SetPace(float pace)
	{
		Pace = pace;
		foreach (RivalShip ship in _ships)
			ship.Car.Driver.SetPace(pace);
	}

	// Al salir de la escena, fuera de la lista
	public override void _ExitTree()
	{
		if (_raceShips == null)
			return;
		foreach (RivalShip ship in _ships)
			_raceShips.Remove(ship.Car.Entry.Number);
	}
}
