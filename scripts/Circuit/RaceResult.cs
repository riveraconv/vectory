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
    /// Lo crea RaceTracker al completar la última vuelta; con rivales, WithClassification le añade la clasificación
    /// completa (spec docs/rivales-2-clasificacion.md, 4.4), de la que sale la posición.
    /// </summary>
    public sealed class RaceResult
    {
        public int Position { get; }
        public int ParticipantCount { get; }

        /// <summary>Tiempo total de la carrera, desde la salida hasta cruzar la meta en la última vuelta.</summary>
        public double TotalTime { get; }

        public double BestLapTime { get; }
        public int BestLapNumber { get; }

        /// <summary>Mejor tiempo de cada sector (índice base 0), con su vuelta.</summary>
        public IReadOnlyList<SectorBest> BestSectors { get; }

        /// <summary>Nombre de la nave con la que se ha corrido (la elegida; vacío si no se indicó).</summary>
        public string ShipName { get; }

        /// <summary>
        /// Clasificación completa de los participantes (null sin ella). Sus tiempos estimados pasan a reales mientras los
        /// rivales terminan, aunque los resultados ya estén en pantalla.
        /// </summary>
        public RaceClassification Classification { get; }

        /// <summary>Id del jugador en Classification (su fila va resaltada).</summary>
        public int ClassificationPlayerId { get; private init; }

        public RaceResult(int position, int participantCount, double totalTime, double bestLapTime, int bestLapNumber,
            IReadOnlyList<SectorBest> bestSectors, string shipName = "", RaceClassification classification = null)
        {
            Classification = classification;
            ShipName = shipName;
            Position = position;
            ParticipantCount = participantCount;
            TotalTime = totalTime;
            BestLapTime = bestLapTime;
            BestLapNumber = bestLapNumber;
            BestSectors = bestSectors;
        }

        /// <summary>
        /// El mismo resultado con la clasificación completa: la posición y los participantes salen de ella (el jugador es
        /// playerId).
        /// </summary>
        public RaceResult WithClassification(RaceClassification classification, int playerId)
        {
            if (classification == null || classification.PositionOf(playerId) == 0)
                return this;
            return new RaceResult(classification.PositionOf(playerId), classification.Count, TotalTime, BestLapTime,
                BestLapNumber, BestSectors, ShipName, classification) { ClassificationPlayerId = playerId };
        }
    }
}
