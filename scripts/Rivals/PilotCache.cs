using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vectory.Autopilot;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Rivals;

// Trazada y calibración de una nave en un circuito: lo caro de montar su piloto
public sealed record PilotData(RacingLine Line, CurveTable Table);

// Caché de trazadas de una carrera (spec docs/rivales-1-parrilla.md, 4.1 y 6): por nave, en el circuito de la carrera, se
// calculan una vez, la primera vez que se piden, y las comparten los rivales que llevan esa nave. Así solo se calculan
// las naves que hay en la parrilla. Lógica pura.
public sealed class PilotCache
{
	private readonly TrackFrame _frame;
	private readonly float _trackWidth;
	private readonly ShipPhysicsSettings _physics;
	private readonly AutopilotSettings _settings;
	private readonly Dictionary<string, PilotData> _data = new();

	public PilotCache(TrackFrame frame, float trackWidth, in ShipPhysicsSettings physics, in AutopilotSettings settings)
	{
		_frame = frame;
		_trackWidth = trackWidth;
		_physics = physics;
		_settings = settings;
	}

	public TrackFrame Frame => _frame;
	public ShipPhysicsSettings Physics => _physics;
	public AutopilotSettings Settings => _settings;

	// Naves ya calculadas, por id
	public IReadOnlyCollection<string> ComputedShips => _data.Keys;

	public PilotData Get(ShipDefinition ship)
	{
		if (_data.TryGetValue(ship.Id, out PilotData data))
			return data;

		data = Compute(ship);
		_data[ship.Id] = data;
		return data;
	}

	// Calcula de una vez las naves que faltan, cada una en su hilo (no comparten nada): con tres naves, la carga de la
	// carrera espera lo que tarda una
	public void Prepare(IEnumerable<ShipDefinition> ships)
	{
		ShipDefinition[] missing = ships.Where(s => !_data.ContainsKey(s.Id)).GroupBy(s => s.Id).Select(g => g.First()).ToArray();
		var computed = new PilotData[missing.Length];
		Parallel.For(0, missing.Length, i => computed[i] = Compute(missing[i]));
		for (int i = 0; i < missing.Length; i++)
			_data[missing[i].Id] = computed[i];
	}

	private PilotData Compute(ShipDefinition ship)
	{
		RacingLine line = RacingLine.Build(_frame, _trackWidth, ship.Visual.CollisionHalfWidth, _settings.BarrierMargin);
		CurveTable table = CurveCalibration.Measure(ship.Technical, _physics, _settings.StepTime);
		return new PilotData(line, table);
	}
}
