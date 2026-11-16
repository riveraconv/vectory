using System;
using System.Collections.Generic;

namespace Vectory.Circuit
{
	/// <summary>
	/// Una fila de la clasificación final: puesto, id de la nave en la carrera (0 = jugador), piloto (PLAYER o apodo),
	/// nave, tiempo total (real o estimado) y mejor vuelta (null si no ha completado ninguna).
	/// </summary>
	public readonly record struct ClassificationRow(int Position, int Id, string Name, string ShipName, double TotalTime,
		bool IsEstimated, double? BestLapTime);

	/// <summary>Lo que se sabe de una nave en el momento de fijar la clasificación (al llegar el jugador).</summary>
	public readonly record struct ClassificationEntry(int Id, string Name, string ShipName, bool Finished, double TotalTime,
		double? BestLapTime);

	/// <summary>
	/// Clasificación final de RACE (spec docs/rivales-2-clasificacion.md, 4): el orden queda fijo al llegar el jugador; las
	/// naves que no han terminado reciben un tiempo estimado, que pasa a real al cruzar la meta sin cambiar su puesto.
	/// Ningún tiempo estimado contradice el orden: si saliera igual o más bajo que el de delante, queda en ese + 0,001 s.
	/// Lógica pura: sin nodos.
	/// </summary>
	public sealed class RaceClassification
	{
		/// <summary>Lo que se suma al tiempo de delante cuando una estimación contradice el orden.</summary>
		public const double MinimumGap = 0.001;

		private readonly ClassificationRow[] _rows;

		/// <param name="entries">Las naves en el orden de pista en la llegada del jugador.</param>
		/// <param name="estimates">Tiempo estimado de cada nave sin terminar (por id).</param>
		public RaceClassification(IReadOnlyList<ClassificationEntry> entries, IReadOnlyDictionary<int, double> estimates)
		{
			ArgumentNullException.ThrowIfNull(entries);
			ArgumentNullException.ThrowIfNull(estimates);
			_rows = new ClassificationRow[entries.Count];
			for (int i = 0; i < entries.Count; i++)
			{
				ClassificationEntry e = entries[i];
				double time = e.Finished ? e.TotalTime : estimates.TryGetValue(e.Id, out double t) ? t : e.TotalTime;
				_rows[i] = new ClassificationRow(i + 1, e.Id, e.Name, e.ShipName, time, !e.Finished, e.BestLapTime);
			}
			KeepEstimatesInOrder();
		}

		public IReadOnlyList<ClassificationRow> Rows => _rows;

		public int Count => _rows.Length;

		/// <summary>Cuántas filas siguen estimadas.</summary>
		public int EstimatedCount
		{
			get
			{
				int count = 0;
				foreach (ClassificationRow row in _rows)
				{
					if (row.IsEstimated)
						count++;
				}
				return count;
			}
		}

		/// <summary>Puesto de una nave (1..), 0 si no está.</summary>
		public int PositionOf(int id) => IndexOf(id) + 1;

		/// <summary>Diferencia de una fila con la primera.</summary>
		public double GapToLeader(int position) =>
			position < 1 || position > _rows.Length ? 0 : _rows[position - 1].TotalTime - _rows[0].TotalTime;

		/// <summary>
		/// Una nave estimada cruza la meta: su tiempo pasa a real y su puesto no cambia. Devuelve false si contradice el
		/// orden (llega antes que la de delante o después que una de detrás ya llegada): manda el orden de pista y quien
		/// llama lo avisa.
		/// </summary>
		public bool Arrive(int id, double totalTime, double? bestLapTime)
		{
			int index = IndexOf(id);
			if (index < 0 || !_rows[index].IsEstimated)
				return true;

			_rows[index] = _rows[index] with { TotalTime = totalTime, IsEstimated = false, BestLapTime = bestLapTime };
			bool consistent = (index == 0 || _rows[index - 1].TotalTime <= totalTime)
				&& (index == _rows.Length - 1 || _rows[index + 1].IsEstimated || totalTime <= _rows[index + 1].TotalTime);
			KeepEstimatesInOrder();
			return consistent;
		}

		/// <summary>Mejor vuelta de una nave que sigue en pista (cambia mientras termina).</summary>
		public bool UpdateBestLap(int id, double? bestLapTime)
		{
			int index = IndexOf(id);
			if (index < 0 || !_rows[index].IsEstimated || _rows[index].BestLapTime == bestLapTime)
				return false;
			_rows[index] = _rows[index] with { BestLapTime = bestLapTime };
			return true;
		}

		/// <summary>
		/// Tiempo estimado de una nave (4.2): tiempo de carrera ahora + lo que le falta / su ritmo. Ritmo: su velocidad
		/// media en la última vuelta completa; si no ha completado ninguna, la media desde el GO. Con ritmo nulo (parada),
		/// 1 m/s, para que la estimación siga siendo un número.
		/// </summary>
		public static double Estimate(double raceTimeNow, double remainingDistance, float lapLength, double? lastLapTime,
			double distanceSinceStart, double timeSinceStart)
		{
			double pace = lastLapTime is double lap && lap > 0
				? lapLength / lap
				: timeSinceStart > 0 ? distanceSinceStart / timeSinceStart : 0;
			pace = Math.Max(pace, 1.0);
			return raceTimeNow + Math.Max(0, remainingDistance) / pace;
		}

		private int IndexOf(int id)
		{
			for (int i = 0; i < _rows.Length; i++)
			{
				if (_rows[i].Id == id)
					return i;
			}
			return -1;
		}

		// Las estimaciones solo suben: nunca por debajo de la fila de delante
		private void KeepEstimatesInOrder()
		{
			for (int i = 1; i < _rows.Length; i++)
			{
				if (_rows[i].IsEstimated && _rows[i].TotalTime <= _rows[i - 1].TotalTime)
					_rows[i] = _rows[i] with { TotalTime = _rows[i - 1].TotalTime + MinimumGap };
			}
		}
	}
}
