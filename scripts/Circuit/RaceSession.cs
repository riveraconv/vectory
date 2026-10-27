using System;
using Godot;

namespace Vectory.Circuit;

// Conecta el circuito (Track) con la lógica de carrera (RaceTracker).
// Cada frame recibe la posición y la velocidad de la nave, detecta qué checkpoints
// y meta ha cruzado y se lo comunica al RaceTracker.
//
// Además del HUD, los resultados se escriben en la consola de Godot ([Race]).
// El RaceTracker se crea al primer Update, cuando el Track ya ha generado su geometría.
public sealed class RaceSession
{
	// Velocidad (m/s) a partir de la cual arranca el cronómetro.
	// Se sustituirá por Start() cuando haya cuenta atrás.
	private const float StartSpeed = 0.5f;

	private readonly Track _track;

	private RaceTracker _tracker;
	private LineCrossingDetector _detector;
	private bool _started;
	private bool _warned;

	public RaceSession(Track track)
	{
		_track = track ?? throw new ArgumentNullException(nameof(track));
	}

	// Estado de la carrera (para el HUD más adelante). Es null hasta el primer Update.
	public RaceTracker Tracker => _tracker;

	// Vuelve a empezar: se descarta el estado y se recrea en el siguiente Update.
	public void Reset()
	{
		_tracker = null;
		_detector = null;
		_started = false;
	}

	public void Update(Vector3 position, float speed, float delta)
	{
		if (!EnsureInitialized())
			return;

		if (!_started && speed > StartSpeed)
		{
			_started = true;
			_tracker.Start();
			GD.Print("[Race] Timer started");
		}

		_detector.Update(_track.NearestIndex(position));

		if (!_started)
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

		_tracker = new RaceTracker(_track.CheckpointIndices.Count);
		_detector = new LineCrossingDetector(_track.Centerline.Count);
		_started = false;

		_tracker.SectorCompleted += (lap, sector, time, isBest) =>
			GD.Print($"[Race] Lap {lap} - Sector {sector + 1}/{_tracker.SectorCount}: {FormatTime(time)}{(isBest ? " (best sector)" : "")}");

		_tracker.LapCompleted += (lap, time, isBest) =>
			GD.Print($"[Race] Lap {lap}/{_tracker.TotalLaps}: {FormatTime(time)}{(isBest ? " (best lap)" : "")}");

		_tracker.RaceFinished += totalTime =>
			GD.Print($"[Race] Race finished. Total time: {FormatTime(totalTime)}");

		return true;
	}

	private static string FormatTime(double seconds)
	{
		var t = TimeSpan.FromSeconds(seconds);
		return $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds:000}";
	}
}