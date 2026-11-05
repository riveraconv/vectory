using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Vectory.Ship;

// Estado del panel de ajuste para pruebas (spec docs/naves-2-tecnico.md, 4): multiplicadores en vivo por nave, nave
// actual y si se ha tocado algo. Dura toda la ejecución del juego y no se guarda en disco. Una vez tocado, Touched no
// vuelve a false. Los récords y el fantasma se guardan igual: sirven para comparar naves al equilibrarlas. Lógica pura.
public sealed class TuningLab
{
	public const int MinPercent = -20;
	public const int MaxPercent = 20;

	// Rangos de las filas de la curva (spec docs/naves-5-curva-aceleracion.md, 4.2)
	public const float MinCurve = 1f;
	public const float MaxCurve = 4f;
	public const float CurveStep = 0.1f;
	public const int MinThrustMinPercent = 0;
	public const int MinThrustMaxPercent = 30;

	private readonly IReadOnlyList<ShipDefinition> _ships;
	private readonly Dictionary<string, ShipMultipliers> _live = new();
	private readonly Dictionary<string, float> _curves = new();
	private int _minThrustPercent = DefaultMinThrustPercent;
	private int _current;

	private static int DefaultMinThrustPercent => (int)MathF.Round(AccelerationCurve.DefaultMinThrust * 100f);

	// Las naves en el orden de Cycle; la actual es currentId o, si no está en la lista, la primera
	public TuningLab(IReadOnlyList<ShipDefinition> ships, string currentId)
	{
		_ships = ships;
		foreach (ShipDefinition ship in ships)
		{
			_live[ship.Id] = ship.Multipliers;
			_curves[ship.Id] = ship.Technical.AccelCurve;
		}

		for (int i = 0; i < ships.Count; i++)
		{
			if (ships[i].Id == currentId)
				_current = i;
		}
	}

	public ShipDefinition Current => _ships[_current];

	public bool Touched { get; private set; }

	// La nave que se conduce: con el panel usado, la suya (también tras F10); si no, la elegida en el selector
	public string DrivenShipId(string chosenId) => Touched ? Current.Id : chosenId;

	// Vector es la referencia: no se puede cambiar
	public static bool IsEditable(string shipId) => shipId != ShipDefinitions.Default.Id;

	public ShipMultipliers Multipliers(string shipId) => _live[shipId];

	// Casilla LINEAR ACCEL (OLD): las tres naves aceleran como antes de la curva, para comparar (spec docs/naves-5-curva-aceleracion.md, 3.4)
	public bool LinearAcceleration { get; private set; }

	public ShipTuning Tuning(string shipId)
	{
		ShipTuning tuning = _live[shipId].Apply(ShipDefinitions.Default.Technical);
		tuning.AccelCurve = LinearAcceleration ? AccelerationCurve.Linear : _curves[shipId];
		tuning.MinThrust = _minThrustPercent / 100f;
		return tuning;
	}

	// Exponente de la curva de una nave. A diferencia de sus porcentajes, el de Vector también se puede mover.
	public float AccelCurve(string shipId) => _curves[shipId];

	public bool IsCurveOutOfRange(string shipId) => !AccelerationCurve.IsWithinRange(_curves[shipId]);

	// Cambia la curva de una nave, limitada al rango del panel y a su paso. Devuelve si ha cambiado algo.
	public bool SetAccelCurve(string shipId, float curve)
	{
		curve = MathF.Round(Math.Clamp(curve, MinCurve, MaxCurve) / CurveStep) * CurveStep;
		if (MathF.Abs(curve - _curves[shipId]) < CurveStep / 2f)
			return false;

		_curves[shipId] = curve;
		Touched = true;
		return true;
	}

	// Empuje mínimo, común a todas las naves
	public int MinThrustPercent => _minThrustPercent;

	public bool SetMinThrustPercent(int percent)
	{
		percent = Math.Clamp(percent, MinThrustMinPercent, MinThrustMaxPercent);
		if (percent == _minThrustPercent)
			return false;

		_minThrustPercent = percent;
		Touched = true;
		return true;
	}

	// Activa o quita la casilla. Devuelve si ha cambiado algo.
	public bool SetLinearAcceleration(bool linear)
	{
		if (linear == LinearAcceleration)
			return false;

		LinearAcceleration = linear;
		Touched = true;
		return true;
	}

	public int Percent(string shipId, ShipParameter parameter) => ToPercent(_live[shipId][parameter]);

	public bool IsOverLimit(string shipId, ShipParameter parameter) => !ShipMultipliers.IsWithinLimit(_live[shipId][parameter]);

	// Cambia un parámetro, en porcentaje respecto a Vector (se limita al rango del panel). Devuelve si ha cambiado algo.
	public bool SetPercent(string shipId, ShipParameter parameter, int percent)
	{
		if (!IsEditable(shipId))
			return false;

		percent = Math.Clamp(percent, MinPercent, MaxPercent);
		if (percent == Percent(shipId, parameter))
			return false;

		_live[shipId] = _live[shipId].With(parameter, 1f + percent / 100f);
		Touched = true;
		return true;
	}

	// Siguiente nave: Vector → Blade → Hammer → Vector
	public void Cycle()
	{
		_current = (_current + 1) % _ships.Count;
		Touched = true;
	}

	// Pasa a la nave elegida en el selector: conserva los valores retocados de todas y no toca Touched, porque elegir
	// nave no es usar el panel. Un id que no está en la lista no cambia nada.
	public void MoveTo(string shipId)
	{
		for (int i = 0; i < _ships.Count; i++)
		{
			if (_ships[i].Id == shipId)
				_current = i;
		}
	}

	// Devuelve una nave a los multiplicadores y la curva de su definición, y el empuje mínimo a su valor. No baja Touched.
	public void Reset(string shipId)
	{
		ShipDefinition ship = _ships.First(s => s.Id == shipId);
		_live[shipId] = ship.Multipliers;
		_curves[shipId] = ship.Technical.AccelCurve;
		_minThrustPercent = DefaultMinThrustPercent;
	}

	// "+8 %", "-6 %", "0 %"
	public static string PercentText(int percent) =>
		(percent > 0 ? "+" : "") + percent.ToString(CultureInfo.InvariantCulture) + " %";

	// El texto de F11: una línea por nave, lista para copiar a su definición, con un aviso en las que pasan del tope
	public string DumpText()
	{
		var text = new StringBuilder("[TuningLab] Multipliers");
		foreach (ShipDefinition ship in _ships)
		{
			ShipMultipliers m = _live[ship.Id];
			IEnumerable<string> values = ShipMultipliers.Parameters.Select(p =>
				$"{p} = {m[p].ToString("0.00", CultureInfo.InvariantCulture)}f");
			string[] over = ShipMultipliers.Parameters.Where(p => IsOverLimit(ship.Id, p)).Select(p => p.ToString()).ToArray();

			text.Append('\n').Append(ship.Id).Append(": new ShipMultipliers { ").Append(string.Join(", ", values)).Append(" }");
			if (over.Length > 0)
				text.Append("  // OVER LIMIT: ").Append(string.Join(", ", over));
		}

		text.Append("\n[TuningLab] Acceleration curve");
		foreach (ShipDefinition ship in _ships)
		{
			text.Append('\n').Append(ship.Id).Append(": AccelCurve = ")
				.Append(_curves[ship.Id].ToString("0.0", CultureInfo.InvariantCulture)).Append('f');
			if (IsCurveOutOfRange(ship.Id))
				text.Append("  // OUT OF RANGE");
		}
		text.Append("\nMinThrust = ").Append((_minThrustPercent / 100f).ToString("0.00", CultureInfo.InvariantCulture)).Append('f');
		return text.ToString();
	}

	private static int ToPercent(float multiplier) => (int)MathF.Round((multiplier - 1f) * 100f);
}
