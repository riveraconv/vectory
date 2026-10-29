using System;
using Godot;
using System.Collections.Generic;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Menu;
using Vectory.Records;
using Vectory.Ship;
using Vectory.UI;

namespace Vectory.Game;

// Se adjunta a un nodo raíz "Main" de tipo Node3D en una escena vacía.
// Monta WorldEnvironment (fondo oscuro + glow), una rejilla de referencia
// de fondo, el circuito (Track), el Player, la carrera (RaceSession), el HUD y la pausa,
// según el modo que haya dejado el menú en GameSession (RACE si la escena se ejecuta sola).
//
// RACE: la nave sale de una posición de parrilla aleatoria, espera al semáforo y, al terminar
// la última vuelta, se guardan los récords de RACE y se muestra la pantalla de resultados con
// RESTART RACE / BACK TO MAIN MENU.
// TIME TRIAL: READY? (nave quieta), en el GO! salida lanzada antes de la meta, y vueltas libres; delta por sector y franja de fin
// de vuelta contra el récord de TIME TRIAL, que se guarda al batirlo.
// ESC abre la pausa (PauseMenu) en cualquier momento. Las reglas de récords viven en RecordBook.
public partial class Main : Node3D
{
	private const float GridStep = 10f;
	private const float GridHalf = 500f;
	private const float GridY = -4f;

	private const string RestartRaceId = "RESTART_RACE";
	private const string BackToMenuId = "BACK_TO_MENU";

	private static readonly MenuOption[] ResultsOptions =
	{
		new(RestartRaceId, "RESTART RACE", true),
		new(BackToMenuId, "BACK TO MAIN MENU", true)
	};

	[ExportGroup("Salida")]
	[Export] public float FirstLightDelay = 1f;   // segundos antes de la primera luz
	[Export] public float LightInterval = 1f;     // segundos entre luces
	[Export] public int LightCount = 5;           // número de luces
	[Export] public float GoDuration = 1f;        // segundos con el GO! visible
	[Export] public int StartTextFontSize = 56;   // cuenta atrás, READY? y GO! (px sobre 1280×720; RACE y TIME TRIAL)

	[ExportGroup("Resultados")]
	[Export] public float ResultsDelay = 1.5f;    // segundos desde la meta hasta la pantalla de resultados
	[Export] public float ResultsBandHeight = 340f;   // alto de la franja de resultados (px sobre 1280×720)
	[Export] public int ResultsOptionFontSize = 24;   // opciones RESTART RACE / BACK TO MAIN MENU (máximo: se reduce si no caben)
	[Export(PropertyHint.File, "*.tscn")] public string MainMenuScenePath = SceneChanger.MainMenuScene;

	[ExportGroup("Time Trial")]
	[Export] public float FlyingStartDistance = 150f;     // metros antes de la meta (sobre la línea central)
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlyingStartSpeed = 0.6f;   // fracción de la velocidad punta (sin boost), aplicada en el GO!
	[Export] public float ReadyDuration = 5f;             // segundos con READY? antes de la salida
	[Export] public float ReadyGoDuration = 1f;           // segundos con el GO! visible
	[Export] public float DeltaVisibleTime = 2.5f;        // segundos con el delta por sector visible
	[Export] public float LapBandVisibleTime = 2.5f;      // segundos con la franja de fin de vuelta visible
	[Export] public float LapBandHeight = 120f;           // alto de la franja de fin de vuelta (px sobre 1280×720)
	[Export] public int DeltaFontSize = 44;               // tamaño del delta (px sobre 1280×720)

	private Player _player;
	private Track _track;
	private RaceSession _race;
	private Hud _hud;
	private PauseMenu _pause;
	private MenuModel _resultsMenu;
	private MeshInstance3D _grid;

	private GameMode _mode;
	private RecordStore _records;
	private RaceRecordResult _raceRecords;   // récords de RACE batidos por la carrera terminada

	private float _finishedTime;   // segundos desde que terminó la carrera
	private bool _resultsShown;

	public override void _Ready()
	{
		AddChild(WorldStyle.CreateWorldEnvironment());

		_grid = new MeshInstance3D { Name = "Grid", Mesh = BuildGridMesh() };
		AddChild(_grid);

		// Si Track y Player ya están en la escena (así se editan sus parámetros en el
		// Inspector) se usan tal cual; si no, se crean por código.
		_track = GetNodeOrNull<Track>("Track");
		if (_track == null)
		{
			_track = new Track { Name = "Track" };
			AddChild(_track);
		}

		_player = GetNodeOrNull<Player>("Player");
		if (_player == null)
		{
			_player = new Player { Name = "Player" };
			AddChild(_player);
		}

		// Pausa (ESC): igual que Track y Player, se usa la de la escena si existe
		_pause = GetNodeOrNull<PauseMenu>("PauseMenu");
		if (_pause == null)
		{
			_pause = new PauseMenu { Name = "PauseMenu", MainMenuScenePath = MainMenuScenePath };
			AddChild(_pause);
		}
		// Al continuar, las teclas pulsadas durante la pausa no deben llegar a la nave
		_pause.Resumed += _player.BlockInputUntilReleased;

		// Sin circuito generado (TrackDefinition ausente o no válido; Track ya ha dado el error)
		// no hay parrilla, límites ni carrera: no se monta nada que dependa de él.
		if (!_track.IsBuilt)
		{
			GD.PushError("Main: el circuito no se ha generado; no se monta la carrera.");
			return;
		}

		_player.SetTrack(_track);
		_mode = GameSession.Mode;

		_records = new RecordStore(new FileRecordStorage());
		_records.Load();
		if (_records.Warning != null)
			GD.PushWarning($"Records: {_records.Warning}");
		if (!RecordBook.IsValidCircuitId(_track.CircuitId))
			GD.PushWarning($"Main: the circuit '{_track.DisplayName}' has no Id; records will not be saved.");

		int participants = _track.GridSlots;
		if (_mode == GameMode.TimeTrial)
		{
			// Salida lanzada: centrada en la pista, antes de la meta y ya en movimiento
			// Salida lanzada: quieta en su punto con READY?; en el GO! sale ya en movimiento
			_track.GetFlyingStart(FlyingStartDistance, out Vector3 startPosition, out float startYaw);
			_player.PlaceAt(startPosition, startYaw);
			_race = RaceSession.ForTimeTrial(_track, new ReadySettings
			{
				ReadyDuration = ReadyDuration,
				GoDuration = ReadyGoDuration
			});
			_race.ReadySequence.Started += () => _player.Launch(_player.MaxSpeed * FlyingStartSpeed);
		}
		else
		{
			// Una marca de parrilla por participante. Las posiciones se reparten al azar sin repetir;
			// sin rivales todavía, solo se usa la del jugador (la primera del reparto).
			int[] gridOrder = GridLayout.RandomOrder(participants, new System.Random());
			_track.GetGridStart(gridOrder[0], Player.NoseDistance, out Vector3 startPosition, out float startYaw);
			_player.PlaceAt(startPosition, startYaw);
			_race = RaceSession.ForRace(_track, BuildStartSettings(), participants);
		}
		_player.SetRaceSession(_race);

		_race.CheckpointPassed += OnCheckpointPassed;
		_race.LapCompleted += OnLapCompleted;
		_race.RaceFinished += OnRaceFinished;

		_hud = new Hud { Name = "Hud", StartTextFontSize = StartTextFontSize };
		AddChild(_hud);
		_hud.ResultsOptionChosen += ExecuteResultsOption;

		if (_mode == GameMode.TimeTrial)
		{
			_hud.SetTimeTrialHeader(_track.DisplayName);
			TimeTrialView view = _hud.TimeTrial;
			view.DeltaVisibleTime = DeltaVisibleTime;
			view.LapBandVisibleTime = LapBandVisibleTime;
			view.LapBandHeight = LapBandHeight;
			view.DeltaFontSize = DeltaFontSize;
		}
		else
		{
			// Todavía no hay rivales ni posición en vivo: el jugador va primero de todos los participantes
			_hud.SetRacePosition(1, participants);
		}
	}

	public override void _Process(double delta)
	{
		// HUD: velocidad, boost, semáforo y estado de la carrera (no hay si el circuito no se generó)
		if (_race != null)
		{
			_hud.SetSpeed(_player.Speed, _player.BoostedTopSpeed);
			_hud.SetBoostCharges(_player.BoostCharges, _player.BoostMaxCharges, _player.BoostCooldownActive);
			_hud.SetStartSequence(_race.StartSequence);
			_hud.SetReadySequence(_race.ReadySequence);
			UpdateLapInfo();
			_pause.RaceFinished = _race.State == RaceState.Finished;
			UpdateResults((float)delta);
		}

		// La rejilla se recoloca de 10 en 10 m siguiendo a la nave: parece infinita
		Vector3 p = _player.GlobalPosition;
		_grid.GlobalPosition = new Vector3(
			Mathf.Snapped(p.X, GridStep),
			GridY,
			Mathf.Snapped(p.Z, GridStep));
	}

	// La pantalla de resultados aparece ResultsDelay segundos después de cruzar la meta, con
	// RESTART RACE (seleccionada) y BACK TO MAIN MENU. Con la pausa abierta no se llega aquí
	// (Main está congelado), así que la lista de resultados no responde.
	private void UpdateResults(float delta)
	{
		if (_race.State != RaceState.Finished)
			return;

		if (!_resultsShown)
		{
			_finishedTime += delta;
			if (_finishedTime < ResultsDelay)
				return;

			_resultsMenu = new MenuModel(ResultsOptions);
			_hud.ShowResults(_race.Tracker.Result, _resultsMenu, ResultsBandHeight, ResultsOptionFontSize,
				_raceRecords.NewTotalTime, _raceRecords.NewBestLap);
			_resultsShown = true;
			return;   // el Enter de este frame no elige todavía
		}

		if (Input.IsActionJustPressed("menu_down"))
		{
			_resultsMenu.MoveNext();
			_hud.RefreshResultsOptions();
		}
		else if (Input.IsActionJustPressed("menu_up"))
		{
			_resultsMenu.MovePrevious();
			_hud.RefreshResultsOptions();
		}
		else if (Input.IsActionJustPressed("menu_accept"))
		{
			ExecuteResultsOption(_resultsMenu.Confirm());
		}
	}

	private void ExecuteResultsOption(string id)
	{
		switch (id)
		{
			case RestartRaceId:
				SceneChanger.Reload(this);
				break;
			case BackToMenuId:
				SceneChanger.ChangeTo(this, MainMenuScenePath);
				break;
		}
	}

	// Bloque de vuelta del HUD según el modo:
	// RACE: LAP 2/3, tiempo de vuelta y mejor vuelta de esta carrera.
	// TIME TRIAL: OUT LAP hasta cruzar la meta, luego LAP N; BEST es el récord de TIME TRIAL del circuito.
	private void UpdateLapInfo()
	{
		RaceTracker tracker = _race.Tracker;

		if (_mode == GameMode.TimeTrial)
		{
			bool outLap = tracker == null || tracker.State == RaceState.PreStart;
			string lapText = outLap ? "OUT LAP" : $"LAP {tracker.CurrentLap}";
			_hud.SetLapInfo(lapText, outLap ? 0 : tracker.CurrentLapTime, TimeTrialRecord()?.LapTime);
			return;
		}

		if (tracker == null)
			return;

		// Antes de arrancar CurrentLap vale 0; se muestra como vuelta 1
		int lap = Math.Max(1, tracker.CurrentLap);
		_hud.SetLapInfo($"LAP {lap}/{tracker.TotalLaps}", tracker.CurrentLapTime, tracker.BestLapTime);
	}

	private TimeTrialRecord TimeTrialRecord() => _records.Book.GetTimeTrial(_track.CircuitId);

	// TIME TRIAL: delta del checkpoint contra el tiempo de paso del récord (si hay récord con
	// tiempos de paso válidos para los checkpoints actuales)
	private void OnCheckpointPassed(int index, double lapTime)
	{
		if (_mode != GameMode.TimeTrial)
			return;

		TimeTrialRecord record = TimeTrialRecord();
		if (record == null || !record.SplitsMatch(_track.CheckpointIndices.Count))
			return;

		double delta = DeltaFormat.Delta(lapTime, record.Splits[index]);
		_hud.TimeTrial.ShowSectorDelta(index + 1, DeltaFormat.Format(delta), DeltaFormat.IsBetter(delta));
	}

	// TIME TRIAL: al completar una vuelta se compara con el récord, se guarda si lo bate
	// y se muestra la franja de fin de vuelta
	private void OnLapCompleted(int lap, double lapTime, IReadOnlyList<double> splits)
	{
		if (_mode != GameMode.TimeTrial)
			return;

		if (!RecordBook.IsValidCircuitId(_track.CircuitId))
		{
			_hud.TimeTrial.ShowLapBand(new LapBandContent(lap, lapTime, "LAP COMPLETE", LapBandTitleStyle.Record,
				"RECORDS OFF  ·  CIRCUIT HAS NO ID"));
			return;
		}

		TimeTrialLapResult result = _records.Book.SubmitTimeTrialLap(_track.CircuitId, lapTime, splits, DateTime.Now);
		LapBandContent content;
		if (result.IsNewRecord)
		{
			string detail = result.PreviousLapTime.HasValue
				? $"{DeltaFormat.Format(lapTime - result.PreviousLapTime.Value)}  ·  PREVIOUS {TimeFormat.Format(result.PreviousLapTime.Value)}"
				: "FIRST RECORD";
			content = new LapBandContent(lap, lapTime, "NEW RECORD", LapBandTitleStyle.Record, detail);
			SaveRecords();
		}
		else
		{
			double previous = result.PreviousLapTime ?? lapTime;
			double delta = DeltaFormat.Delta(lapTime, previous);
			content = new LapBandContent(lap, lapTime, DeltaFormat.Format(delta),
				DeltaFormat.IsBetter(delta) ? LapBandTitleStyle.Better : LapBandTitleStyle.Worse,
				$"BEST {TimeFormat.Format(previous)}");
		}

		_hud.TimeTrial.ShowLapBand(content);
	}

	// RACE terminada: tiempo total y mejor vuelta contra los récords de RACE (una carrera
	// abandonada desde la pausa no llega aquí y no guarda nada)
	private void OnRaceFinished(RaceResult result)
	{
		if (_mode != GameMode.Race || result == null)
			return;

		_raceRecords = _records.Book.SubmitRace(_track.CircuitId, result.TotalTime, result.BestLapTime, DateTime.Now);
		if (_raceRecords.NewTotalTime || _raceRecords.NewBestLap)
			SaveRecords();
	}

	private void SaveRecords()
	{
		if (!_records.Save())
			GD.PushWarning($"Records: {_records.Warning}");
	}

	private StartSettings BuildStartSettings()
	{
		return new StartSettings
		{
			FirstLightDelay = FirstLightDelay,
			LightInterval = LightInterval,
			LightCount = LightCount,
			GoDuration = GoDuration
		};
	}

	private static ArrayMesh BuildGridMesh()
	{
		var verts = new List<Vector3>();

		for (float x = -GridHalf; x <= GridHalf; x += GridStep)
		{
			verts.Add(new Vector3(x, 0f, -GridHalf));
			verts.Add(new Vector3(x, 0f, GridHalf));
		}
		for (float z = -GridHalf; z <= GridHalf; z += GridStep)
		{
			verts.Add(new Vector3(-GridHalf, 0f, z));
			verts.Add(new Vector3(GridHalf, 0f, z));
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Palette.BlueDim
		});
		return mesh;
	}
}
