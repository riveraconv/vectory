using System.Collections.Generic;
using System.Linq;
using Godot;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Contact;
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
	private readonly List<IContactShip> _contactShips = new();
	private readonly List<TrafficShip> _traffic = new();
	private RaceShipList _raceShips;
	private IContactShip _player;
	private ContactDamageSettings _contactDamage = ContactDamageSettings.Default;

	// Choques entre naves (spec docs/rivales-3-contacto.md): el jugador y los rivales
	public ShipContactSolver Contacts { get; } = new();

	// Golpes medios y fuertes entre dos rivales en esta carrera (para ver si se comen por detrás)
	public int RivalHits { get; private set; }

	// Tu nave: tipo de roce de este paso y lo perdido por roce en los últimos 5 s (panel de pruebas)
	public GrazeLossMeter PlayerGraze { get; } = new();

	public IReadOnlyList<RivalShip> Ships => _ships;

	// Ajuste global de depuración sobre los márgenes de todos los pilotos (fracción; 0 = los de cada piloto)
	public float PaceOffset { get; private set; }

	// Las cargas de los destellos de la barrera de cada rival, para pintarlos con el del jugador
	public IEnumerable<BarrierFlash> Flashes => _ships.Select(s => s.Car.Flash);

	// Después de añadirlo al árbol: con el circuito ya generado, el jugador ya creado y la carrera ya montada
	public void Setup(IReadOnlyList<RivalEntry> lineup, Track track, Player player, RaceSession race, RaceShipList raceShips,
		RivalRaceContext context, float paceOffset, float cooldownPace, ContactDamageSettings contactDamage, float passableTime)
	{
		_raceShips = raceShips;
		_player = player;
		_contactDamage = contactDamage;
		Contacts.Settings = player.BuildShipContactSettings();
		player.Passable.RespawnTime = passableTime;
		PaceOffset = paceOffset;
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
			var car = new RivalCar(entry, definition, pilots, context, paceOffset);
			car.Driver.CooldownPace = cooldownPace;
			car.Passable.RespawnTime = passableTime;
			var ship = new RivalShip();
			ship.Setup(car, race, raceShips, look, player.FlickerOpacity, player.BuildRespawnBlink());
			AddChild(ship);
			_ships.Add(ship);
			GD.Print($"[Race] {entry.Id} {entry.Pilot.Nickname} ({PilotProfiles.ScreenName(entry.Pilot.Profile)}): grid {entry.GridPosition}, "
				+ $"{ShipCatalog.Find(entry.ShipDesignId)?.Name ?? entry.ShipDesignId}, "
				+ $"{HullPaints.DisplayName(HullPaints.StyleOf(entry.ShipDesignId) ?? HullStyle.Racing, entry.PaintIndex)}");
		}
		GD.Print($"[Race] Rival pilots ready: {pilots.ComputedShips.Count} ships in {(Time.GetTicksUsec() - start) / 1000.0:0} ms");

		_contactShips.Add(player);
		foreach (RivalShip ship in _ships)
			_contactShips.Add(ship.Car);
		WarnGridOverlaps();
	}

	// Un paso de física de la carrera, después del del jugador: mueve los rivales, resuelve los contactos entre todas las
	// naves (con la barrera otra vez) y termina el paso de cada rival (destello, daño y recolocación). El daño, la
	// sacudida y la recolocación del jugador los pone Main con Contacts. Con la pausa no se llama.
	public void StepPhysics(bool raceStarted, float dt)
	{
		// Todas las naves al empezar el paso, para la trazada clara: así no importa en qué orden se mueven
		_traffic.Clear();
		foreach (IContactShip ship in _contactShips)
			_traffic.Add(RivalCar.TrafficOf(ship));
		foreach (RivalShip ship in _ships)
			ship.Car.Move(raceStarted, _traffic);

		Contacts.Solve(_contactShips, dt);
		UpdatePlayerGraze(dt);
		foreach (ShipContactEvent e in Contacts.Events)
		{
			if (!e.IsNewImpact)
				continue;
			OnImpact(e.A, e.RoleA, e.Level);
			OnImpact(e.B, e.RoleB, e.Level);
			if (e.A != _player.Id && e.B != _player.Id)
				RivalHits++;
			GD.Print($"[Race] Contact {e.Level.ToString().ToLowerInvariant()}: {Describe(e)} ({e.ClosingSpeed * 3.6f:0} km/h)");
		}

		foreach (RivalShip ship in _ships)
			ship.Car.AfterContacts(Contacts.Touched(ship.Car.Id));
	}

	private void UpdatePlayerGraze(float dt)
	{
		float loss = 0f;
		GrazeKind kind = GrazeKind.None;
		foreach (ShipContactEvent e in Contacts.Events)
		{
			if (e.Graze == GrazeKind.None || (e.A != _player.Id && e.B != _player.Id))
				continue;
			loss += e.A == _player.Id ? e.GrazeLossA : e.GrazeLossB;
			if (kind != GrazeKind.Side)
				kind = e.Graze;
		}
		PlayerGraze.Add(dt, loss, kind);
	}

	// Puntos de un golpe para una nave, según su papel (los del jugador los suma Main)
	public int ContactPoints(ContactRole role, ImpactLevel level) => ContactDamage.Points(level, role, _contactDamage);

	private void OnImpact(int id, ContactRole role, ImpactLevel level)
	{
		RivalCar car = CarOf(id);
		car?.OnShipImpact(role, ContactPoints(role, level));
	}

	private RivalCar CarOf(int id)
	{
		foreach (RivalShip ship in _ships)
		{
			if (ship.Car.Id == id)
				return ship.Car;
		}
		return null;
	}

	private string NameOf(int id) => CarOf(id)?.Entry.Pilot.Nickname ?? PilotCatalog.PlayerName;

	private string Describe(ShipContactEvent e)
	{
		if (e.RoleA == ContactRole.Mutual)
			return $"{NameOf(e.A)} and {NameOf(e.B)}, mutual";
		return e.RoleA == ContactRole.Rammer ? $"{NameOf(e.A)} rams {NameOf(e.B)}" : $"{NameOf(e.B)} rams {NameOf(e.A)}";
	}

	// En la previa no se comprueba nada; si con los anchos dos marcas de la parrilla se solapan, se avisa
	private void WarnGridOverlaps()
	{
		for (int i = 0; i < _contactShips.Count; i++)
		{
			for (int j = i + 1; j < _contactShips.Count; j++)
			{
				IContactShip a = _contactShips[i];
				IContactShip b = _contactShips[j];
				if (ShipContactDetector.Overlap(Plan(a.Position), a.Physics.Dynamics.Yaw, a.Shape, Plan(b.Position), b.Physics.Dynamics.Yaw,
					b.Shape, out _))
					GD.PushWarning($"[Race] Grid slots of {NameOf(a.Id)} and {NameOf(b.Id)} overlap");
			}
		}
	}

	private static System.Numerics.Vector2 Plan(System.Numerics.Vector3 p) => new(p.X, p.Z);

	// Cambia el ajuste global de ritmo de todos los rivales al momento (panel de pruebas)
	public void SetPace(float paceOffset)
	{
		PaceOffset = paceOffset;
		foreach (RivalShip ship in _ships)
			ship.Car.Driver.SetPace(paceOffset);
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
