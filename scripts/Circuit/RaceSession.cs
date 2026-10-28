using System;
using Godot;
using Vectory.Core;

namespace Vectory.Circuit;

// Ciclo de una carrera: secuencia de salida (semáforo y cuenta atrás) y lógica de carrera.
// Conecta el circuito (Track) con la lógica pura (StartSequence y RaceTracker): cada frame
// recibe la posición de la nave, avanza la secuencia de salida y, tras el GO, detecta qué
// checkpoints y meta ha cruzado y se lo comunica al RaceTracker.
//
// Estados: PreStart (cuenta atrás) → Running (desde el GO) → Finished (última vuelta completada).
// Además del HUD, los resultados se escriben en la consola de Godot ([Race]).
// El RaceTracker se crea al primer Update, cuando el Track ya ha generado su geometría.
public sealed class RaceSession
{
	private readonly Track _track;
	private readonly int _participantCount;

	private RaceTracker _tracker;
	private LineCrossingDetector _detector;
	private bool _warned;

	public RaceSession(Track track, StartSettings startSettings, int participantCount)
	{
		_track = track ?? throw new ArgumentNullException(nameof(track));
		_participantCount = Math.Max(1, participantCount);

		StartSequence = new StartSequence(startSettings);
		StartSequence.Started += OnStart;
	}

	// Semáforo y cuenta atrás (para el HUD)
	public StartSequence StartSequence { get; }

	// Estado de la carrera (vueltas, tiempos...) para el HUD. Es null hasta el primer Update.
	public RaceTracker Tracker => _tracker;

	public int ParticipantCount => _participantCount;

	public RaceState State => _tracker?.State ?? RaceState.PreStart;

	// La nave solo acepta input en carrera
	public bool CanDrive => State == RaceState.Running;

	public void Update(Vector3 position, float delta)
	{
		if (!EnsureInitialized())
			return;

		// La salida se da dentro de Update (evento Started → OnStart)
		StartSequence.Update(delta);

		_detector.Update(_track.NearestIndex(position));

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
		GD.Print("[Race] GO");
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

		_tracker = new RaceTracker(_track.CheckpointIndices.Count, participantCount: _participantCount);
		_detector = new LineCrossingDetector(_track.Centerline.Count);

		_tracker.SectorCompleted += (lap, sector, time, isBest) =>
			GD.Print($"[Race] Lap {lap} - Sector {sector + 1}/{_tracker.SectorCount}: {TimeFormat.Format(time)}{(isBest ? " (best sector)" : "")}");

		_tracker.LapCompleted += (lap, time, isBest) =>
			GD.Print($"[Race] Lap {lap}/{_tracker.TotalLaps}: {TimeFormat.Format(time)}{(isBest ? " (best lap)" : "")}");

		_tracker.RaceFinished += totalTime =>
			GD.Print($"[Race] Race finished. Total time: {TimeFormat.Format(totalTime)}");

		return true;
	}
}
