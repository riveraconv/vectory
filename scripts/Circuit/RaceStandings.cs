using System;
using System.Collections.Generic;

namespace Vectory.Circuit
{
	/// <summary>
	/// Estado de una nave para la clasificación, tal como lo dan su RaceTracker y su posición sobre el eje.
	/// Id: 0 el jugador, 1–7 los rivales (su número de la lista de naves). GridPosition: su marca (1 = la de delante).
	/// Running / Finished: estado de su carrera. RaceTime: su cronómetro de carrera (TotalTime). LastLapTime y BestLapTime:
	/// los de su RaceTracker (para la estimación y la clasificación final). Name y ShipName: piloto y nave, para la
	/// clasificación final.
	/// </summary>
	public readonly record struct StandingsShip(int Id, int GridPosition, int Lap, int NextCheckpoint, float TrackDistance,
		bool Running, bool Finished, double RaceTime, double? LastLapTime = null, double? BestLapTime = null,
		string Name = "", string ShipName = "");

	/// <summary>
	/// Distancia de carrera (spec docs/rivales-2-clasificacion.md, 3.2): vueltas completadas × largo + distancia desde la
	/// meta, usando solo el tramo entre el último checkpoint pasado y el siguiente. Antes del primer cruce de meta (en la
	/// parrilla, detrás de ella) sale negativa. Así ir hacia atrás o saltarse un checkpoint no adelanta a nadie.
	/// </summary>
	public static class RaceDistance
	{
		/// <param name="lap">Vuelta en curso (1..; 0 antes de la salida cuenta como la 1).</param>
		/// <param name="nextCheckpoint">Siguiente checkpoint de la vuelta (0..n; n = todos pasados, hacia la meta).</param>
		/// <param name="trackDistance">Distancia sobre el eje desde la meta, entre 0 y el largo.</param>
		public static double Compute(int lap, int nextCheckpoint, float trackDistance, IReadOnlyList<float> checkpointDistances,
			float lapLength)
		{
			ArgumentNullException.ThrowIfNull(checkpointDistances);
			int n = checkpointDistances.Count;
			double length = lapLength;
			int k = Math.Clamp(nextCheckpoint, 0, n);

			// Tramo permitido de la vuelta: del último checkpoint pasado (o, en el primer tramo, del último de la vuelta
			// anterior, ya detrás de la meta) al siguiente (o a la meta)
			double low = n == 0 ? -length : k == 0 ? checkpointDistances[n - 1] - length : checkpointDistances[k - 1];
			double high = n == 0 || k == n ? length : checkpointDistances[k];

			// De las tres copias de la posición (esta vuelta, la anterior y la siguiente), la más cercana al tramo
			double s = trackDistance;
			double best = s;
			double bestGap = Gap(s, low, high);
			foreach (double candidate in new[] { s - length, s + length })
			{
				double gap = Gap(candidate, low, high);
				if (gap < bestGap)
				{
					best = candidate;
					bestGap = gap;
				}
			}

			return (Math.Max(lap, 1) - 1) * length + Math.Clamp(best, low, high);
		}

		private static double Gap(double value, double low, double high) =>
			value < low ? low - value : value > high ? value - high : 0;
	}

	/// <summary>
	/// Marcas de tiempo de una nave (3.3): su tiempo de carrera al pasar cada Step metros de distancia de carrera,
	/// interpolado entre dos muestras. Solo cuenta lo que avanza: ir hacia atrás no borra ni repite marcas.
	/// </summary>
	public sealed class TimeMarks
	{
		public const float DefaultStep = 10f;

		private readonly double _step;
		private readonly List<double> _times = new();
		private long _firstMark;          // índice de la primera marca (marca k = k × Step metros)
		private bool _hasSample;
		private double _firstDistance, _firstTime;
		private double _lastDistance, _lastTime;   // la muestra más avanzada

		public TimeMarks(float step = DefaultStep)
		{
			if (step <= 0f)
				throw new ArgumentOutOfRangeException(nameof(step), "El paso debe ser positivo.");
			_step = step;
		}

		public int Count => _times.Count;
		public bool HasData => _hasSample;

		/// <summary>Distancia más avanzada apuntada.</summary>
		public double MaxDistance => _lastDistance;

		public void Record(double distance, double time)
		{
			if (!_hasSample)
			{
				_hasSample = true;
				_firstDistance = _lastDistance = distance;
				_firstTime = _lastTime = time;
				_firstMark = (long)Math.Ceiling(distance / _step);
				if (_firstMark * _step == distance)
					_times.Add(time);
				return;
			}

			// Quieta o hacia atrás: no hay marcas nuevas; la muestra más avanzada pasa a ser de ahora, así al volver a
			// avanzar se interpola desde este momento y no desde cuando llegó allí la primera vez
			if (distance <= _lastDistance)
			{
				_lastTime = time;
				return;
			}

			long next = _firstMark + _times.Count;
			while (next * _step <= distance)
			{
				double mark = next * _step;
				_times.Add(_lastTime + (time - _lastTime) * (mark - _lastDistance) / (distance - _lastDistance));
				next++;
			}
			_lastDistance = distance;
			_lastTime = time;
		}

		/// <summary>Tiempo de carrera con el que pasó por esa distancia, o null si aún no ha llegado o no hay dato.</summary>
		public double? TimeAt(double distance)
		{
			if (!_hasSample || distance < _firstDistance || distance > _lastDistance)
				return null;

			// Puntos conocidos: la primera muestra, las marcas y la muestra más avanzada
			double index = distance / _step;
			long k0 = (long)Math.Floor(index);
			int i0 = (int)(k0 - _firstMark);
			int i1 = i0 + 1;

			double d0, t0, d1, t1;
			if (i0 < 0)
			{
				d0 = _firstDistance;
				t0 = _firstTime;
			}
			else if (i0 < _times.Count)
			{
				d0 = k0 * _step;
				t0 = _times[i0];
			}
			else
			{
				return _lastTime;
			}

			if (i1 < _times.Count && i1 >= 0)
			{
				d1 = (k0 + 1) * _step;
				t1 = _times[i1];
			}
			else if (i1 < 0)
			{
				d1 = _firstMark * _step;
				t1 = _times.Count > 0 ? _times[0] : _lastTime;
			}
			else
			{
				d1 = _lastDistance;
				t1 = _lastTime;
			}

			if (d1 <= d0)
				return t0;
			return t0 + (t1 - t0) * (distance - d0) / (d1 - d0);
		}
	}

	/// <summary>Qué hay en el intervalo de un rival contigo.</summary>
	public enum RaceIntervalKind
	{
		/// <summary>Sin dato todavía.</summary>
		None,
		/// <summary>Segundos (positivo: el rival va delante).</summary>
		Time,
		/// <summary>Vueltas de diferencia (positivo: el rival va delante).</summary>
		Laps
	}

	/// <summary>Intervalo de un rival contigo (3.3). Positivo: va delante; negativo: detrás.</summary>
	public readonly record struct RaceInterval(RaceIntervalKind Kind, double Seconds, int Laps)
	{
		public static RaceInterval None => new(RaceIntervalKind.None, 0, 0);
		public static RaceInterval Time(double seconds) => new(RaceIntervalKind.Time, seconds, 0);
		public static RaceInterval LapsBehindOrAhead(int laps) => new(RaceIntervalKind.Laps, 0, laps);

		/// <summary>
		/// Intervalo entre el jugador y un rival a partir de sus distancias de carrera, sus tiempos y sus marcas.
		/// Rival delante: cuánto hace que pasó por donde estás tú. Detrás: cuánto hace que pasaste tú por donde está él.
		/// Una vuelta o más de diferencia en distancia: vueltas.
		/// </summary>
		public static RaceInterval Between(double playerDistance, double playerTime, TimeMarks playerMarks,
			double rivalDistance, double rivalTime, TimeMarks rivalMarks, float lapLength)
		{
			ArgumentNullException.ThrowIfNull(playerMarks);
			ArgumentNullException.ThrowIfNull(rivalMarks);
			double difference = rivalDistance - playerDistance;
			if (lapLength > 0f)
			{
				int laps = (int)Math.Floor(Math.Abs(difference) / lapLength);
				if (laps >= 1)
					return LapsBehindOrAhead(difference > 0 ? laps : -laps);
			}

			if (difference >= 0)
			{
				double? passed = rivalMarks.TimeAt(playerDistance);
				return passed is double t ? Time(Math.Max(0, playerTime - t)) : None;
			}
			double? mine = playerMarks.TimeAt(rivalDistance);
			return mine is double m ? Time(-Math.Max(0, rivalTime - m)) : None;
		}
	}

	/// <summary>
	/// Clasificación de la carrera en directo (spec docs/rivales-2-clasificacion.md, 3.2 y 3.3): con el estado de cada
	/// nave da el orden —primero las terminadas, en orden de llegada; después, de más a menos distancia de carrera; con
	/// empate, la que salió más adelante en la parrilla— y lleva las marcas de tiempo de cada una para los intervalos con
	/// el jugador. El intervalo de un rival queda fijo al llegar él, y todos al llegar el jugador. Al llegar el jugador fija
	/// la clasificación final (Final, 4.2) con las estimaciones de los demás, y desde entonces el orden es el suyo; las
	/// llegadas siguientes pasan sus tiempos a reales (4.3). La misma posición para el HUD y los resultados. Lógica pura:
	/// sin nodos.
	/// </summary>
	public sealed class RaceStandings
	{
		private sealed class Entry
		{
			public int Id;
			public int Grid;
			public double Distance;
			public double Time;
			public bool Started;
			public double StartDistance;
			public double StartTime;
			public double? LastLapTime;
			public double? BestLapTime;
			public string Name = "";
			public string ShipName = "";
			public bool Finished;
			public int FinishIndex = -1;
			public TimeMarks Marks;
			public RaceInterval? FrozenInterval;
		}

		private readonly IReadOnlyList<float> _checkpointDistances;
		private readonly float _lapLength;
		private readonly float _markStep;
		private readonly Dictionary<int, Entry> _entries = new();
		private readonly List<Entry> _ordered = new();
		private readonly List<int> _order = new();
		private int _finishedCount;

		public RaceStandings(IReadOnlyList<float> checkpointDistances, float lapLength, int playerId = 0,
			float markStep = TimeMarks.DefaultStep, int totalLaps = 3)
		{
			if (totalLaps < 1)
				throw new ArgumentOutOfRangeException(nameof(totalLaps), "Debe haber al menos 1 vuelta.");
			TotalLaps = totalLaps;
			ArgumentNullException.ThrowIfNull(checkpointDistances);
			if (lapLength <= 0f)
				throw new ArgumentOutOfRangeException(nameof(lapLength), "La vuelta debe medir algo.");
			_checkpointDistances = checkpointDistances;
			_lapLength = lapLength;
			_markStep = markStep;
			PlayerId = playerId;
		}

		public int PlayerId { get; }
		public float LapLength => _lapLength;
		public int TotalLaps { get; }

		/// <summary>Clasificación final: null hasta que llega el jugador.</summary>
		public RaceClassification Final { get; private set; }

		/// <summary>
		/// Se lanza cuando cambia la clasificación final: al fijarla y con cada llegada real (también si la llegada
		/// contradice el orden: el segundo argumento es la nave que llegó fuera de orden, o -1).
		/// </summary>
		public event Action<RaceClassification, int> FinalChanged;

		/// <summary>Ids de las naves, de la primera a la última.</summary>
		public IReadOnlyList<int> Order => _order;

		public int Count => _order.Count;

		/// <summary>Puesto (1..) de una nave; 0 si no está.</summary>
		public int PositionOf(int id) => _order.IndexOf(id) + 1;

		public double DistanceOf(int id) => _entries.TryGetValue(id, out Entry e) ? e.Distance : 0;

		public bool IsFinished(int id) => _entries.TryGetValue(id, out Entry e) && e.Finished;

		/// <summary>Recalcula el orden con el estado de este momento. Llamar cada frame.</summary>
		public void Update(IReadOnlyList<StandingsShip> ships)
		{
			ArgumentNullException.ThrowIfNull(ships);
			var arrivals = new List<Entry>();
			foreach (StandingsShip ship in ships)
			{
				if (!_entries.TryGetValue(ship.Id, out Entry entry))
				{
					entry = new Entry { Id = ship.Id, Marks = new TimeMarks(_markStep) };
					_entries.Add(ship.Id, entry);
					_ordered.Add(entry);
				}
				entry.Grid = ship.GridPosition;
				entry.Name = ship.Name ?? "";
				entry.ShipName = ship.ShipName ?? "";
				if (entry.Finished)
					continue;   // terminada: distancia y tiempo quedan los de su llegada

				entry.Distance = RaceDistance.Compute(ship.Lap, ship.NextCheckpoint, ship.TrackDistance, _checkpointDistances, _lapLength);
				entry.Time = ship.RaceTime;
				entry.LastLapTime = ship.LastLapTime;
				entry.BestLapTime = ship.BestLapTime;
				if (ship.Running || ship.Finished)
				{
					if (!entry.Started)
					{
						entry.Started = true;
						entry.StartDistance = entry.Distance;
						entry.StartTime = entry.Time;
					}
					entry.Marks.Record(entry.Distance, entry.Time);
				}
				if (ship.Finished)
				{
					entry.Finished = true;
					arrivals.Add(entry);
				}
			}

			// Llegadas de este momento: por su tiempo y, con empate, por la parrilla
			arrivals.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Grid.CompareTo(b.Grid));
			foreach (Entry arrival in arrivals)
				arrival.FinishIndex = _finishedCount++;

			// Intervalos fijos: el del rival que llega, y todos cuando llega el jugador
			bool playerArrived = arrivals.Exists(a => a.Id == PlayerId);
			foreach (Entry entry in _ordered)
			{
				if (entry.Id == PlayerId || entry.FrozenInterval != null)
					continue;
				if (playerArrived || arrivals.Contains(entry))
					entry.FrozenInterval = LiveInterval(entry);
			}

			_ordered.Sort(Compare);
			if (Final == null && playerArrived)
				FixFinal();
			else if (Final != null)
				UpdateFinal(arrivals);

			_order.Clear();
			if (Final != null)
			{
				foreach (ClassificationRow row in Final.Rows)
					_order.Add(row.Id);
			}
			else
			{
				foreach (Entry entry in _ordered)
					_order.Add(entry.Id);
			}
		}

		// Llega el jugador: el orden de pista de este momento queda fijo y los que no han terminado reciben su estimación
		private void FixFinal()
		{
			Entry player = _entries[PlayerId];
			double totalDistance = (double)TotalLaps * _lapLength;
			var entries = new List<ClassificationEntry>(_ordered.Count);
			var estimates = new Dictionary<int, double>();
			foreach (Entry e in _ordered)
			{
				entries.Add(new ClassificationEntry(e.Id, e.Name, e.ShipName, e.Finished, e.Time, e.BestLapTime));
				if (!e.Finished)
				{
					estimates[e.Id] = RaceClassification.Estimate(player.Time, totalDistance - e.Distance, _lapLength, e.LastLapTime,
						e.Started ? e.Distance - e.StartDistance : 0, e.Started ? e.Time - e.StartTime : 0);
				}
			}
			Final = new RaceClassification(entries, estimates);
			FinalChanged?.Invoke(Final, -1);
		}

		// Después de la llegada del jugador: llegadas reales y mejores vueltas de los que siguen en pista
		private void UpdateFinal(List<Entry> arrivals)
		{
			foreach (Entry arrival in arrivals)
			{
				bool consistent = Final.Arrive(arrival.Id, arrival.Time, arrival.BestLapTime);
				FinalChanged?.Invoke(Final, consistent ? -1 : arrival.Id);
			}
			foreach (Entry e in _ordered)
			{
				if (!e.Finished)
					Final.UpdateBestLap(e.Id, e.BestLapTime);
			}
		}

		/// <summary>Intervalo de una nave con el jugador (None para el propio jugador o si no hay dato).</summary>
		public RaceInterval IntervalOf(int id)
		{
			if (id == PlayerId || !_entries.TryGetValue(id, out Entry entry))
				return RaceInterval.None;
			return entry.FrozenInterval ?? LiveInterval(entry);
		}

		private RaceInterval LiveInterval(Entry rival)
		{
			if (!_entries.TryGetValue(PlayerId, out Entry player))
				return RaceInterval.None;
			return RaceInterval.Between(player.Distance, player.Time, player.Marks, rival.Distance, rival.Time, rival.Marks, _lapLength);
		}

		private static int Compare(Entry a, Entry b)
		{
			if (a.Finished != b.Finished)
				return a.Finished ? -1 : 1;
			if (a.Finished)
				return a.FinishIndex.CompareTo(b.FinishIndex);
			if (a.Distance != b.Distance)
				return b.Distance.CompareTo(a.Distance);
			return a.Grid.CompareTo(b.Grid);
		}
	}
}
