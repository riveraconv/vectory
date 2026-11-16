using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vectory.Circuit
{
	/// <summary>Una fila del panel de clasificación del HUD.</summary>
	/// <param name="Offset">Puestos respecto al jugador: negativo encima (va delante), 0 el jugador.</param>
	public readonly record struct StandingsRow(int Position, string Name, string Interval, bool IsPlayer, int Offset);

	/// <summary>
	/// Textos y huecos del panel de clasificación (spec docs/rivales-2-clasificacion.md, 3.3 y 3.4). Lógica pura.
	/// </summary>
	public static class StandingsFormat
	{
		/// <summary>Huecos por encima y por debajo del jugador: 2 + él + 2.</summary>
		public const int SlotsAround = 2;
		public const int SlotCount = SlotsAround * 2 + 1;

		public const string NoData = "—";
		public const string AheadMark = "▲";
		public const string BehindMark = "▼";

		/// <summary>▲ 0.84 (delante), ▼ 1.20 (detrás), 1:02.31 desde 60 s, +1 LAP / −2 LAPS, — sin dato.</summary>
		public static string Interval(RaceInterval interval)
		{
			switch (interval.Kind)
			{
				case RaceIntervalKind.Time:
					string mark = interval.Seconds >= 0 ? AheadMark : BehindMark;
					return $"{mark} {Seconds(Math.Abs(interval.Seconds))}";
				case RaceIntervalKind.Laps:
					int laps = Math.Abs(interval.Laps);
					// + = le sacas tú la vuelta (va detrás); − = te la saca él
					string sign = interval.Laps > 0 ? "−" : "+";
					return $"{sign}{laps} {(laps == 1 ? "LAP" : "LAPS")}";
				default:
					return NoData;
			}
		}

		/// <summary>Segundos con dos decimales: 0.84, 12.30; desde un minuto, 1:02.31.</summary>
		public static string Seconds(double seconds)
		{
			long hundredths = (long)Math.Round(Math.Max(0, seconds) * 100, MidpointRounding.AwayFromZero);
			long minutes = hundredths / 6000;
			long rest = hundredths % 6000;
			string secondsText = (rest / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
			if (minutes == 0)
				return secondsText;
			return $"{minutes}:{(rest < 1000 ? "0" : "")}{secondsText}";
		}

		/// <summary>
		/// Los cinco huecos del panel: el jugador en el del medio, con los dos que van delante encima y los dos de detrás
		/// debajo; null en los huecos sin nadie (yendo 1º, 2º, 7º u 8º la lista no se recoloca).
		/// </summary>
		/// <param name="order">Ids de la clasificación, del primero al último.</param>
		/// <param name="name">Nombre de cada id (PLAYER o el apodo).</param>
		/// <param name="interval">Texto del intervalo de cada rival.</param>
		public static StandingsRow?[] Slots(IReadOnlyList<int> order, int playerId, Func<int, string> name, Func<int, string> interval)
		{
			ArgumentNullException.ThrowIfNull(order);
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(interval);
			var slots = new StandingsRow?[SlotCount];
			int me = -1;
			for (int i = 0; i < order.Count; i++)
			{
				if (order[i] == playerId)
				{
					me = i;
					break;
				}
			}
			if (me < 0)
				return slots;

			for (int offset = -SlotsAround; offset <= SlotsAround; offset++)
			{
				int index = me + offset;
				if (index < 0 || index >= order.Count)
					continue;
				int id = order[index];
				bool isPlayer = id == playerId;
				slots[offset + SlotsAround] = new StandingsRow(index + 1, name(id), isPlayer ? "" : interval(id), isPlayer, offset);
			}
			return slots;
		}
	}
}
