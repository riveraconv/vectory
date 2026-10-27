using System;
using System.Collections.Generic;

namespace Vectory.Circuit
{
    /// <summary>
    /// Lógica de carrera: vueltas, sectores, cronómetro y mejores tiempos.
    /// No depende de Godot ni de la geometría del circuito.
    /// Quien detecte los cruces llama a CheckpointCrossed / FinishLineCrossed
    /// y en cada frame a Tick.
    ///
    /// Sectores: cada vuelta se divide en CheckpointCount + 1 sectores.
    ///   Sector 0: meta -> checkpoint 0
    ///   Sector i: checkpoint i-1 -> checkpoint i
    ///   Último sector (CheckpointCount): último checkpoint -> meta
    /// (los índices son base 0; el que los muestre debe sumar 1).
    /// </summary>
    public sealed class RaceTracker
    {
        private readonly double?[] _bestSectorTimes;

        public int CheckpointCount { get; }
        public int TotalLaps { get; }

        /// <summary>Sectores por vuelta (checkpoints + 1, porque la meta también limita sectores).</summary>
        public int SectorCount => CheckpointCount + 1;

        public int CurrentLap { get; private set; }
        public int NextCheckpointIndex { get; private set; }

        /// <summary>Sector en el que está la nave ahora (índice base 0).</summary>
        public int CurrentSectorIndex => NextCheckpointIndex;

        public double CurrentLapTime { get; private set; }
        public double CurrentSectorTime { get; private set; }
        public double TotalTime { get; private set; }

        public double? LastLapTime { get; private set; }
        public double? BestLapTime { get; private set; }

        /// <summary>Tiempo del último sector completado.</summary>
        public double? LastSectorTime { get; private set; }

        /// <summary>Mejor tiempo de cada sector en esta carrera (null si aún no se ha completado).</summary>
        public IReadOnlyList<double?> BestSectorTimes { get; }

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }

        /// <summary>Se lanza al pasar un checkpoint en el orden correcto (índice del checkpoint).</summary>
        public event Action<int> CheckpointPassed;

        /// <summary>
        /// Se lanza al completar un sector (vuelta, índice de sector, tiempo, ¿es el mejor de ese sector?).
        /// Va antes que LapCompleted cuando el sector es el último de la vuelta.
        /// </summary>
        public event Action<int, int, double, bool> SectorCompleted;

        /// <summary>Se lanza al completar una vuelta (número de vuelta, tiempo, ¿es la mejor?).</summary>
        public event Action<int, double, bool> LapCompleted;

        /// <summary>Se lanza al completar la última vuelta (tiempo total).</summary>
        public event Action<double> RaceFinished;

        public RaceTracker(int checkpointCount, int totalLaps = 3)
        {
            if (checkpointCount < 1)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), "Debe haber al menos 1 checkpoint.");
            if (totalLaps < 1)
                throw new ArgumentOutOfRangeException(nameof(totalLaps), "Debe haber al menos 1 vuelta.");

            CheckpointCount = checkpointCount;
            TotalLaps = totalLaps;

            _bestSectorTimes = new double?[checkpointCount + 1];
            BestSectorTimes = Array.AsReadOnly(_bestSectorTimes);
        }

        /// <summary>Inicia (o reinicia) la carrera. Llamar al acabar la cuenta atrás.</summary>
        public void Start()
        {
            CurrentLap = 1;
            NextCheckpointIndex = 0;
            CurrentLapTime = 0;
            CurrentSectorTime = 0;
            TotalTime = 0;
            LastLapTime = null;
            BestLapTime = null;
            LastSectorTime = null;
            Array.Clear(_bestSectorTimes, 0, _bestSectorTimes.Length);
            IsFinished = false;
            IsRunning = true;
        }

        /// <summary>Avanza el cronómetro. Pasar el delta de tiempo de cada frame, en segundos.</summary>
        public void Tick(double delta)
        {
            if (!IsRunning || delta <= 0)
                return;

            CurrentLapTime += delta;
            CurrentSectorTime += delta;
            TotalTime += delta;
        }

        /// <summary>
        /// Registra el cruce de un checkpoint. Solo cuenta si es el siguiente
        /// en orden; cualquier otro se ignora. Completa el sector que termina ahí.
        /// </summary>
        public void CheckpointCrossed(int index)
        {
            if (!IsRunning || index != NextCheckpointIndex)
                return;

            int lap = CurrentLap;
            bool sectorIsBest = RegisterSector(index, out double sectorTime);

            NextCheckpointIndex++;

            CheckpointPassed?.Invoke(index);
            SectorCompleted?.Invoke(lap, index, sectorTime, sectorIsBest);
        }

        /// <summary>
        /// Registra el cruce de la meta. Solo completa el último sector y la vuelta
        /// si se han pasado todos los checkpoints en orden.
        /// </summary>
        public void FinishLineCrossed()
        {
            if (!IsRunning || NextCheckpointIndex < CheckpointCount)
                return;

            int completedLap = CurrentLap;
            int lastSector = CheckpointCount;
            bool sectorIsBest = RegisterSector(lastSector, out double sectorTime);

            double lapTime = CurrentLapTime;
            bool lapIsBest = BestLapTime == null || lapTime < BestLapTime.Value;

            LastLapTime = lapTime;
            if (lapIsBest)
                BestLapTime = lapTime;

            bool raceOver = completedLap >= TotalLaps;
            if (raceOver)
            {
                IsRunning = false;
                IsFinished = true;
            }
            else
            {
                CurrentLap++;
                NextCheckpointIndex = 0;
                CurrentLapTime = 0;
            }

            SectorCompleted?.Invoke(completedLap, lastSector, sectorTime, sectorIsBest);
            LapCompleted?.Invoke(completedLap, lapTime, lapIsBest);

            if (raceOver)
                RaceFinished?.Invoke(TotalTime);
        }

        // Cierra el sector actual: guarda su tiempo, actualiza el mejor y reinicia el contador.
        private bool RegisterSector(int sectorIndex, out double sectorTime)
        {
            sectorTime = CurrentSectorTime;

            bool isBest = _bestSectorTimes[sectorIndex] == null || sectorTime < _bestSectorTimes[sectorIndex].Value;
            if (isBest)
                _bestSectorTimes[sectorIndex] = sectorTime;

            LastSectorTime = sectorTime;
            CurrentSectorTime = 0;
            return isBest;
        }
    }
}