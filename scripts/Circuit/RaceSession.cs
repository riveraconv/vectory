using System;
using System.Collections.Generic;
using Godot;
using Vectory.Core;

namespace Vectory.Circuit;

// Ciclo de una carrera: conecta el circuito (Track) con la lógica pura (StartSequence y RaceTracker).
// Cada frame recibe la posición de la nave, detecta qué checkpoints y meta ha cruzado y se lo
// comunica al RaceTracker.
//
// Dos formas de empezar:
//  - ForRace: salida desde parrilla con semáforo y cuenta atrás (StartSequence). Sin control hasta el GO.
//    Estados: PreStart (cuenta atrás) → Running (desde el GO) → Finished (última vuelta completada).
//  - ForTimeTrial: salida lanzada y vueltas libres. Primero READY? (ReadySequence), sin control; en el
//    GO! se toma el control (y quien monta la escena lanza la nave). El cronómetro (RaceTracker.Start)
//    arranca al cruzar la meta por primera vez en el sentido de la marcha.
//
// Además del HUD, los resultados se escriben en la consola de Godot ([Race]).
// El RaceTracker se crea al primer Update, cuando el Track ya ha generado su geometría.
public sealed class RaceSession
{
	private readonly Track _track;
	private readonly int _participantCount;
	private readonly int _totalLaps;
	private readonly bool _flyingStart;

	private RaceTracker _tracker;
	private LineCrossingDetector _detector;
	private bool _warned;

	// Al cruzar un checkpoint en orden: índice del checkpoint y tiempo de vuelta en ese momento
	public event Action<int, double> CheckpointPassed;

	// Al completar una vuelta: número de vuelta, tiempo y tiempos de paso de esa vuelta
	public event Action<int, double, IReadOnlyList<double>> LapCompleted;

	// Al terminar la carrera (solo con vueltas limitadas): resultado
	public event Action<RaceResult> RaceFinished;

	private RaceSession(Track track, int participantCount, int totalLaps)
	{
		_track = track ?? throw new ArgumentNullException(nameof(track));
		_participantCount = Math.Max(1, participantCount);
		_totalLaps = totalLaps;
	}

	private RaceSession(Track track, StartSettings startSettings, int participantCount, int totalLaps)
		: this(track, participantCount, totalLaps)
	{
		StartSequence = new StartSequence(startSettings);
		StartSequence.Started += OnStart;
	}

	private RaceSession(Track track, ReadySettings readySettings)
		: this(track, 1, RaceTracker.UnlimitedLaps)
	{
		_flyingStart = true;
		ReadySequence = new ReadySequence(readySettings);
		ReadySequence.Started += () => GD.Print("[Race] GO! (flying start)");
	}

	// Carrera desde parrilla, con semáforo y el número de vueltas por defecto
	public static RaceSession ForRace(Track track, StartSettings startSettings, int participantCount)
	{
		return new RaceSession(track, startSettings, participantCount, 3);
	}

	// Contrarreloj: READY? / GO!, salida lanzada, vueltas libres y un solo participante
	public static RaceSession ForTimeTrial(Track track, ReadySettings readySettings)
	{
		return new RaceSession(track, readySettings);
	}

	// Semáforo y cuenta atrás (para el HUD). null en TIME TRIAL.
	public StartSequence StartSequence { get; }

	// READY? / GO! de la salida lanzada (para el HUD y para lanzar la nave). null en RACE.
	public ReadySequence ReadySequence { get; }

	// Estado de la carrera (vueltas, tiempos...) para el HUD. Es null hasta el primer Update.
	public RaceTracker Tracker => _tracker;

	public int ParticipantCount => _participantCount;

	public bool IsFlyingStart => _flyingStart;

	public RaceState State => _tracker?.State ?? RaceState.PreStart;

	// La nave acepta input en carrera; con salida lanzada, desde el GO! (también antes de cruzar la meta)
	public bool CanDrive => _flyingStart ? ReadySequence.HasStarted : State == RaceState.Running;

	public void Update(Vector3 position, float delta)
	{
		if (!EnsureInitialized())
			return;

		// La salida desde parrilla se da dentro de Update (evento Started → OnStart);
		// la salida lanzada, igual (ReadySequence.Started)
		StartSequence?.Update(delta);
		ReadySequence?.Update(delta);

		_detector.Update(_track.NearestIndex(position));

		// Salida lanzada: el cronómetro arranca al cruzar la meta hacia delante
		if (_flyingStart && _tracker.State == RaceState.PreStart)
		{
			if (_detector.Crossed(Track.FinishIndex))
				OnStart();
			return;
		}

		if (_tracker.State != RaceState.Running)
			return;

		_tracker.Tick(delta);

		for (int i = 0; i < _track.CheckpointIndices.Count; i++)
		{
			if (_detector.Crossed(_track.CheckpointIndices[i]))
				_tracker.CheckpointCrossed(i);
		}

		if (_detector.Crossed(Track.FinishIndex))
			_tracker.FinishLineCrossed();
	}

	private void OnStart()
	{
		if (_tracker == null)
			return;

		_tracker.Start();
		GD.Print(_flyingStart ? "[Race] Lap 1 started" : "[Race] GO");
	}

	private bool EnsureInitialized()
	{
		if (_tracker != null)
			return true;

		// El Track aún no ha generado su línea central
		if (_track.Centerline == null)
			return false;

		if (_track.CheckpointIndices.Count == 0)
		{
			if (!_warned)
			{
				GD.PushWarning("RaceSession: the track has no checkpoints; laps cannot be counted.");
				_warned = true;
			}
			return false;
		}

		_tracker = new RaceTracker(_track.CheckpointIndices.Count, _totalLaps, _participantCount);
		_detector = new LineCrossingDetector(_track.Centerline.Count);

		_tracker.CheckpointPassed += index =>
			CheckpointPassed?.Invoke(index, _tracker.CurrentLapTime);

		_tracker.SectorCompleted += (lap, sector, time, isBest) =>
			GD.Print($"[Race] Lap {lap} - Sector {sector + 1}/{_tracker.SectorCount}: {TimeFormat.Format(time)}{(isBest ? " (best sector)" : "")}");

		_tracker.LapCompleted += (lap, time, isBest) =>
		{
			string total = _tracker.HasLapLimit ? $"/{_tracker.TotalLaps}" : "";
			GD.Print($"[Race] Lap {lap}{total}: {TimeFormat.Format(time)}{(isBest ? " (best lap)" : "")}");
			LapCompleted?.Invoke(lap, time, _tracker.LastLapSplits);
		};

		_tracker.RaceFinished += totalTime =>
		{
			GD.Print($"[Race] Race finished. Total time: {TimeFormat.Format(totalTime)}");
			RaceFinished?.Invoke(_tracker.Result);
		};

		return true;
	}
}
