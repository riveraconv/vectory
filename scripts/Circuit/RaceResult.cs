using System.Collections.Generic;

namespace Vectory.Circuit
{
    /// <summary>Mejor tiempo de un sector y la vuelta en la que se hizo.</summary>
    public readonly struct SectorBest
    {
        public double Time { get; }
        public int Lap { get; }

        public SectorBest(double time, int lap)
        {
            Time = time;
            Lap = lap;
        }
    }

    /// <summary>
    /// Resultado de una carrera terminada: lo que muestra la pantalla de resultados.
    /// Lo crea RaceTracker al completar la última vuelta.
    /// </summary>
    public sealed class RaceResult
    {
        public int Position { get; }
        public int ParticipantCount { get; }
        public double BestLapTime { get; }
        public int BestLapNumber { get; }

        /// <summary>Mejor tiempo de cada sector (índice base 0), con su vuelta.</summary>
        public IReadOnlyList<SectorBest> BestSectors { get; }

        public RaceResult(int position, int participantCount, double bestLapTime, int bestLapNumber,
            IReadOnlyList<SectorBest> bestSectors)
        {
            Position = position;
            ParticipantCount = participantCount;
            BestLapTime = bestLapTime;
            BestLapNumber = bestLapNumber;
            BestSectors = bestSectors;
        }
    }
}
