using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Core;
using Vectory.Effects;
using Vectory.Ghost;
using Vectory.Menu;
using Vectory.Radar;
using Vectory.Records;
using Vectory.Rivals;
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
// RESTART RACE / BACK TO MAIN MENU. Daños acumulativos (DamageModel): los impactos medios y fuertes contra la
// barrera suben el nivel, que baja la punta y el giro, bloquea el boost, hace parpadear la nave y se ve en el HUD.
// TIME TRIAL: READY? (nave quieta), en el GO! salida lanzada antes de la meta, y vueltas libres; delta por sector y franja de fin
// de vuelta contra el récord de TIME TRIAL, que se guarda al batirlo. Nave fantasma con la vuelta récord:
// se graba cada vuelta (GhostRecorder) y, si es récord, pasa a ser el fantasma (user://ghosts/); GHOST: ON / OFF
// en la pausa (user://settings.json).
// ESC abre la pausa (PauseMenu) en cualquier momento. Las reglas de récords viven en RecordBook.
public partial class Main : Node3D
{
	// Nombres de acciones y de parámetros de shader creados una vez: con un texto, cada llamada crea un StringName
	// nuevo (con finalizador), y en cada frame eso alargaba las pausas del recolector de .NET
	private static readonly StringName SnMenuAccept = "menu_accept";
	private static readonly StringName SnMenuDown = "menu_down";
	private static readonly StringName SnMenuLeft = "menu_left";
	private static readonly StringName SnMenuRight = "menu_right";
	private static readonly StringName SnMenuUp = "menu_up";

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
	[Export] public float ResultsBandHeight = 440f;   // alto de la franja de resultados (px sobre 1280×720)
	[Export] public int ResultsOptionFontSize = 22;   // opciones RESTART RACE / BACK TO MAIN MENU, en fila (máximo: se reduce si no caben)
	[Export] public int ResultsShipNameFontSize = 16; // nombre de la nave, bajo la posición (px sobre 1280×720)
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

	[ExportGroup("Fantasma")]
	[Export] public int GhostSampleRate = GhostRecorder.DefaultSampleRate;   // muestras por segundo de la grabación

	[ExportGroup("Daños")]
	[Export] public int DamageMediumPoints = DamageSettings.DefaultMediumPoints;      // puntos por impacto medio
	[Export] public int DamageStrongPoints = DamageSettings.DefaultStrongPoints;      // puntos por impacto fuerte
	[Export] public int DamageLevel1Points = DamageSettings.DefaultLevel1Points;      // puntos para cada nivel
	[Export] public int DamageLevel2Points = DamageSettings.DefaultLevel2Points;
	[Export] public int DamageLevel3Points = DamageSettings.DefaultLevel3Points;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DamageLevel2TopSpeed = DamageSettings.DefaultLevel2TopSpeedFactor;   // factor de la velocidad punta
	[Export(PropertyHint.Range, "0,1,0.01")] public float DamageLevel3TopSpeed = DamageSettings.DefaultLevel3TopSpeedFactor;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DamageLevel3Turn = DamageSettings.DefaultLevel3TurnFactor;       // factor del giro
	[Export] public float DamageSegmentWidth = 22f;    // indicador del HUD (px sobre 1280×720)
	[Export] public float DamageSegmentHeight = 8f;
	[Export] public float DamageSegmentGap = 4f;
	[Export] public float DamageIndicatorGap = 24f;    // separación entre el indicador y las esferas de boost
	[Export] public float DamageBlinkFrequency = 2f;   // Hz del parpadeo del indicador con nivel 3
	// Golpes entre naves (spec docs/rivales-3-contacto.md, 4): puntos según el papel; el roce no suma
	[Export] public int ContactMediumRammer = ContactDamageSettings.DefaultMediumRammer;       // medio: el que embiste
	[Export] public int ContactMediumReceiver = ContactDamageSettings.DefaultMediumReceiver;   // medio: el que recibe
	[Export] public int ContactMediumMutual = ContactDamageSettings.DefaultMediumMutual;       // medio mutuo: cada uno
	[Export] public int ContactStrongRammer = ContactDamageSettings.DefaultStrongRammer;
	[Export] public int ContactStrongReceiver = ContactDamageSettings.DefaultStrongReceiver;
	[Export] public int ContactStrongMutual = ContactDamageSettings.DefaultStrongMutual;

	[ExportGroup("Radar")]
	[Export] public float RadarRange = RadarSettings.DefaultRange;                                 // alcance (m)
	[Export] public float RadarCloseDistance = RadarSettings.DefaultCloseDistance;                 // nave pegada por debajo (m)
	[Export] public float RadarMaxHeightDifference = RadarSettings.DefaultMaxHeightDifference;     // más diferencia de altura, no sale (m)
	[Export] public float RadarSweepPeriod = RadarSettings.DefaultSweepPeriod;                     // segundos por vuelta del barrido
	[Export] public float RadarEchoFade = RadarSettings.DefaultEchoFade;                           // segundos en apagarse un eco
	[Export] public float RadarDiameter = 120f;                                                    // diámetro en el HUD (px sobre 1280×720)

	[ExportGroup("Minimapa")]
	[Export] public float MinimapWindowRadius = 450f;   // metros de circuito del centro al borde
	[Export] public float MinimapDiameter = 112f;       // diámetro en el HUD (px sobre 1280×720)
	[Export] public float MinimapGap = 32f;             // de la línea de BEST al borde de arriba del minimapa (px sobre 1280×720)

	[ExportGroup("Clasificación")]
	[Export] public float StandingsRowHeight = 22f;                                     // alto de fila del panel (px sobre 1280×720)
	[Export(PropertyHint.Range, "0,1,0.01")] public float StandingsFadeOne = 0.62f;     // opacidad de las filas a un puesto
	[Export(PropertyHint.Range, "0,1,0.01")] public float StandingsFadeTwo = 0.30f;     // y a dos
	[Export] public float IntervalRefresh = 0.25f;                                      // segundos entre refrescos de los intervalos
	[Export] public float StandingsMarkStep = TimeMarks.DefaultStep;                    // metros entre marcas de tiempo de cada nave

	[ExportGroup("Rivales")]
	[Export] public int RivalCount = 7;   // como mucho, una por marca libre de la parrilla (participantes − 1)
	[Export(PropertyHint.Range, "-15,5,0.5")] public float RivalPaceOffset = 0f;   // puntos sobre los márgenes de cada piloto (ajuste de depuración)
	[Export(PropertyHint.Range, "0.1,1,0.01")] public float RivalCooldownPace = RivalDriver.DefaultCooldownPace;   // vuelta de enfriamiento: fracción de su objetivo

	[ExportGroup("Recolocación")]
	[Export] public float StuckSpeed = RespawnSettings.DefaultStuckSpeedKmh;          // km/h: por debajo, y tocando la barrera, la nave está atascada
	[Export] public float StuckTime = RespawnSettings.DefaultStuckTime;               // segundos seguidos atascada antes de recolocarla
	[Export] public float OutsideTrackMargin = RespawnSettings.DefaultOutsideMargin;  // metros más allá de la barrera para considerarla fuera de la pista
	[Export] public float PassableAfterRespawn = PassableState.DefaultRespawnTime;   // segundos sin chocar con otras naves tras recolocarla

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

	// Barreras: destello en el punto de contacto y recolocación automática
	private BarrierFlash _barrierFlash;
	private RespawnDetector _respawn;

	// Daños (solo RACE; null en TIME TRIAL)
	private DamageModel _damage;
	private int _playerRespawns;   // para borrar su humo de daños al recolocarla

	// Las demás naves de la carrera (rivales y naves de prueba; el fantasma no entra) y el radar que las lee
	private readonly RaceShipList _raceShips = new();

	// Alineación de los rivales y sus naves (solo RACE; null en TIME TRIAL)
	private IReadOnlyList<RivalEntry> _lineup;
	private RivalField _rivals;
	private CollisionEffects _collisionFx;
	private RadarSweep _radar;

	// Clasificación (solo RACE; null en TIME TRIAL): el jugador es el 0 y cada rival, su número
	private const int PlayerStandingsId = 0;
	private readonly OvertakeCounter _overtakes = new();
	private bool _reportPrinted;
	private RaceStandings _standings;
	private int _playerGridPosition;
	private readonly List<StandingsShip> _standingsShips = new();
	private readonly Dictionary<int, string> _standingsNames = new();
	private readonly Dictionary<int, string> _standingsShipNames = new();
	private readonly Dictionary<int, string> _intervalTexts = new();
	private float _intervalRefreshLeft;

	// Fantasma (solo TIME TRIAL; null en RACE)
	private GhostShip _ghostShip;
	private GhostRecorder _recorder;
	private GhostStore _ghosts;
	private SettingsStore _settings;
	private GhostRecording _ghost;   // fantasma válido del circuito (null si no hay)
	private string _trackSignature;

	public override void _Ready()
	{
		// Los ajustes de SETTINGS (ejecutada sola desde el editor, sin pasar por el menú)
		SettingsService.EnsureLoaded();

		// El fantasma graba la nave después de que se mueva en cada paso de física (Player tiene prioridad 0)
		ProcessPhysicsPriority = 1;

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
		LoadSettings();
		ApplySavedPaints();

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
			SetupGhost();
		}
		else
		{
			// Una marca de parrilla por participante. Las posiciones se reparten al azar sin repetir: la primera es la del
			// jugador y las demás, de los rivales, que salen del mismo generador (alineación: nave y pintura)
			var rng = new System.Random();
			int[] gridOrder = GridLayout.RandomOrder(participants, rng);
			_playerGridPosition = gridOrder[0];
			_lineup = RivalLineup.Build(gridOrder, rng, RivalLineup.CountFor(_mode, participants, RivalCount));
			float noseDistance = _player.Definition.Visual.NoseDistance;
			_track.GetGridStart(gridOrder[0], noseDistance, out Vector3 startPosition, out float startYaw);
			_player.PlaceAt(startPosition, startYaw);
			// La nave de la sesión: Definition no cambia aunque se use F10 en la carrera
			string shipName = ShipCatalog.Find(_player.Definition.Id)?.Name ?? "";
			_race = RaceSession.ForRace(_track, BuildStartSettings(), participants, shipName);
			_track.Elements.ShowGrid(gridOrder[0], noseDistance);
		}
		_player.SetRaceSession(_race);

		// Elementos del circuito: meta y checkpoints destellan al cruzarlos, el panel muestra el tiempo del sector
		// y el semáforo y la parrilla siguen la secuencia de salida (null en TIME TRIAL: semáforo apagado)
		_track.Elements.SetStartSequence(_race.StartSequence);
		_race.FinishLineCrossed += _track.Elements.FlashFinish;
		_race.CheckpointLineCrossed += _track.Elements.FlashCheckpoint;
		_race.SectorTimed += OnSectorTimed;

		_barrierFlash = new BarrierFlash(_track.LapLength, _track.BarrierFlashSettings);
		_respawn = new RespawnDetector(BuildRespawnSettings());

		_race.CheckpointPassed += OnCheckpointPassed;
		_race.LapCompleted += OnLapCompleted;
		_race.RaceFinished += OnRaceFinished;

		_hud = new Hud
		{
			Name = "Hud",
			StartTextFontSize = StartTextFontSize,
			DamageSegmentWidth = DamageSegmentWidth,
			DamageSegmentHeight = DamageSegmentHeight,
			DamageSegmentGap = DamageSegmentGap,
			DamageIndicatorGap = DamageIndicatorGap,
			DamageBlinkFrequency = DamageBlinkFrequency,
			RadarDiameter = RadarDiameter,
			MinimapDiameter = MinimapDiameter,
			MinimapGap = MinimapGap,
			StandingsRowHeight = StandingsRowHeight,
			StandingsFadeOne = StandingsFadeOne,
			StandingsFadeTwo = StandingsFadeTwo
		};
		AddChild(_hud);
		_hud.ResultsOptionChosen += ExecuteResultsOption;

		_radar = new RadarSweep(new RadarSettings
		{
			Range = RadarRange,
			CloseDistance = RadarCloseDistance,
			MaxHeightDifference = RadarMaxHeightDifference,
			SweepPeriod = RadarSweepPeriod,
			EchoFade = RadarEchoFade
		});
		_hud.SetRadar(_radar);
		var minimapPath = new MinimapPath(_track.Frame, _track.TrackWidth);
		_hud.SetMinimap(minimapPath, MinimapWindowRadius, _raceShips.Ships);
		_player.SetCockpitRadar(_radar, minimapPath, MinimapWindowRadius, _raceShips.Ships);

		// Velocidad, boost, daño, radar y minimapa: en pantalla o en la cabina, según la cámara y la nave
		ApplyViewRules();
		_player.ViewChanged += _ => ApplyViewRules();

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
			SetupDamage();
		}

		// Rivales (solo RACE): en su marca, en el radar y en el minimapa
		if (_lineup != null && _lineup.Count > 0)
		{
			_rivals = new RivalField { Name = "Rivals" };
			AddChild(_rivals);
			_rivals.Setup(_lineup, _track, _player, _race, _raceShips, BuildRivalContext(), RivalPaceOffset / 100f, RivalCooldownPace,
				BuildContactDamageSettings(), PassableAfterRespawn);
		}
		if (_mode == GameMode.Race)
			SetupStandings();

		// Chispas, destellos y humo de los choques (spec docs/efectos-1-choques.md); en la escena, para afinarlos
		_collisionFx = GetNodeOrNull<CollisionEffects>("CollisionEffects");
		if (_collisionFx == null)
		{
			_collisionFx = new CollisionEffects { Name = "CollisionEffects" };
			AddChild(_collisionFx);
		}
		_collisionFx.Setup(_track);

		// Panel de ajuste para pruebas: no existe en una exportación de release
		if (OS.IsDebugBuild())
		{
			var tuningPanel = new TuningLabPanel { Name = "TuningLabPanel", Player = _player, Rivals = _rivals };
			AddChild(tuningPanel);
			AddChild(new AutopilotToggle { Name = "AutopilotToggle", Player = _player });
			AddChild(new DebugOrbitCamera { Name = "DebugOrbitCamera", Player = _player });

			var testShips = new TestShips { Name = "TestShips" };
			AddChild(testShips);
			testShips.Setup(_raceShips, _player, _track);
			testShips.CountChanged += tuningPanel.SetTestShipCount;
			tuningPanel.SetTestShipCount(testShips.Count);
		}
	}

	private void ApplyViewRules()
	{
		CockpitViewRules rules = _player.ViewRules;
		_hud.SetDriveHudVisible(rules.ScreenDriveHud);
		_hud.SetRadarVisible(rules.ScreenRadar);
	}

	public override void _Process(double delta)
	{
		// HUD: velocidad, boost, semáforo y estado de la carrera (no hay si el circuito no se generó)
		if (_race != null)
		{
			_hud.SetSpeed(_player.Speed, _player.BoostedTopSpeed);
			_hud.SetBoostCharges(_player.BoostCharges, _player.BoostMaxCharges, _player.BoostCooldownActive, _player.BoostBlocked);
			_hud.SetSlipstream(_player.Slipstream.Strength);
			_hud.SetStartSequence(_race.StartSequence);
			_hud.SetReadySequence(_race.ReadySequence);
			UpdateLapInfo();
			UpdateStandings((float)delta);
			UpdateGhost();
			_radar.Update((float)delta, ToNumerics(_player.GlobalPosition), _player.PlanForward, _raceShips.Ships);
			var self = new System.Numerics.Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
			_hud.SetMinimapPose(self, _player.PlanForward);
			_player.SetCockpitMinimapPose(self, _player.PlanForward);
			_pause.RaceFinished = _race.State == RaceState.Finished;
			UpdateResults((float)delta);
			// Si una opción de los resultados ha cambiado de escena, esta ya no está en el árbol
			if (!IsInsideTree())
				return;
		}

		// La rejilla se recoloca de 10 en 10 m siguiendo a la nave: parece infinita. Va 2 m por debajo del
		// punto más bajo de la pista (en las planas, a GridY de siempre), así no corta las hondonadas.
		Vector3 p = _player.GlobalPosition;
		float gridY = _track.IsBuilt ? Mathf.Min(GridY, _track.LowestSurfaceY - 2f) : GridY;
		_grid.GlobalPosition = new Vector3(
			Mathf.Snapped(p.X, GridStep),
			gridY,
			Mathf.Snapped(p.Z, GridStep));
	}

	// Después de mover la nave: barreras (destello, sacudida y recolocación) y, en TIME TRIAL,
	// muestras de la vuelta en curso para el fantasma
	public override void _PhysicsProcess(double delta)
	{
		if (_race == null)
			return;

		// Rivales y choques entre naves (solo RACE), antes de la barrera del jugador: un empujón contra ella también cuenta
		if (_rivals != null)
		{
			_rivals.StepPhysics(_race.State != RaceState.PreStart, (float)delta);
			UpdatePlayerShipContacts();
		}

		UpdateBarrierContact((float)delta);
		UpdateCollisionEffects((float)delta);

		if (_recorder != null && _recorder.IsRecording && _race.State == RaceState.Running)
			SampleGhost();
	}

	// Efectos de los choques de este paso (solo visual): contra la barrera, el jugador y los rivales; entre naves, los
	// contactos del paso (solo RACE); los aterrizajes de todas; y el humo de daños de todas (solo RACE tiene daño). Luz y temblor de la cabina con los tuyos.
	private void UpdateCollisionEffects(float delta)
	{
		if (_collisionFx == null)
			return;
		_player.FeelBarrier(_player.LastBarrierTouch);
		_collisionFx.SetReference(_player.PlanVelocity);
		_collisionFx.BarrierContact(_player.Id, _player.LastBarrierTouch, _player.PlanForward, _player.PlanVelocity, delta);
		Vector3 p = _player.GlobalPosition;
		_collisionFx.DriftShip(_player.Id, _player.Drifting, _player.IsAirborne, _player.SlipAngle,
			new System.Numerics.Vector3(p.X, p.Y, p.Z), _player.PlanForward, _player.PlanVelocity, _player.HullTransform,
			_player.CollisionHalfWidth, _player.DriftContact, delta);
		LandingSettings landing = _player.LandingThresholds;
		if (_player.JustLanded)
			_collisionFx.Landing(_player.LandingSpeed, landing, _player.HullTransform, _player.PlanVelocity, _player.ExteriorProfile);
		System.Numerics.Vector2 pv = _player.PlanVelocity;
		_collisionFx.DamageShip(_player.Id, _damage?.Level ?? 0, _playerRespawns, _player.HullTransform,
			new System.Numerics.Vector3(pv.X, _player.VerticalSpeed, pv.Y), _player.ExteriorProfile,
			_player.View != CameraView.Cockpit, delta);
		if (_rivals != null)
		{
			for (int ri = 0; ri < _rivals.Ships.Count; ri++)
			{
				RivalShip ship = _rivals.Ships[ri];
				RivalCar car = ship.Car;
				_collisionFx.BarrierContact(car.Id, car.LastTouch, car.Dynamics.Forward, car.Dynamics.Velocity, delta);
				// Los rivales no derrapan nunca (su piloto no pulsa la tecla); si un día lo hacen, ya dejan marcas
				_collisionFx.DriftShip(car.Id, car.Dynamics.Drifting, car.Physics.Vertical.Airborne, car.Dynamics.SlipAngle,
					car.Position, car.Dynamics.Forward, car.Dynamics.Velocity, ship.HullTransform,
					car.Ship.Visual.CollisionHalfWidth, car.Ship.Visual.DriftContact, delta);
				if (car.Physics.Vertical.Landed)
					_collisionFx.Landing(car.Physics.Vertical.LandingSpeed, landing, ship.HullTransform, car.Dynamics.Velocity,
						car.Ship.Visual);
				System.Numerics.Vector2 rv = car.Dynamics.Velocity;
				_collisionFx.DamageShip(car.Id, car.Damage.Level, car.Respawns, ship.HullTransform,
					new System.Numerics.Vector3(rv.X, car.Physics.Vertical.VerticalSpeed, rv.Y), car.Ship.Visual, true, delta);
			}
			for (int ci = 0; ci < _rivals.Contacts.Events.Count; ci++)
			{
				ShipContactEvent e = _rivals.Contacts.Events[ci];
				_collisionFx.ShipContact(e, ContactVelocity(e), delta);
				if (e.A == _player.Id || e.B == _player.Id)
					_player.FeelShip(e.Point, e.Level, e.IsNewImpact, e.Graze, e.Facing);
			}
		}
		_player.StepCrashFeel(delta);
	}

	// La velocidad que heredan los efectos de un contacto entre naves: la del que embiste, o la media
	private System.Numerics.Vector2 ContactVelocity(in ShipContactEvent e)
	{
		System.Numerics.Vector2 a = PlanVelocityOf(e.A);
		System.Numerics.Vector2 b = PlanVelocityOf(e.B);
		if (e.IsNewImpact && e.RoleA == ContactRole.Rammer)
			return a;
		if (e.IsNewImpact && e.RoleB == ContactRole.Rammer)
			return b;
		return (a + b) * 0.5f;
	}

	private System.Numerics.Vector2 PlanVelocityOf(int id)
	{
		if (id == _player.Id)
			return _player.PlanVelocity;
		for (int ri = 0; ri < _rivals.Ships.Count; ri++)
			if (_rivals.Ships[ri].Car.Id == id)
				return _rivals.Ships[ri].Car.Dynamics.Velocity;
		return System.Numerics.Vector2.Zero;
	}

	// Contacto de la nave con la barrera en este paso: destello en el lado tocado, sacudida en los
	// impactos medios y fuertes, y recolocación si se atasca o sale de la pista. Solo se recoloca con
	// la nave bajo control (en carrera, y en TIME TRIAL desde el GO!), no en la previa ni al terminar.
	private void UpdateBarrierContact(float delta)
	{
		BarrierTouch touch = _player.LastBarrierTouch;

		_barrierFlash.Update(delta);
		if (touch.Contact)
		{
			if (touch.IsNewImpact)
			{
				_barrierFlash.Impact(touch.Side, touch.AxisDistance, touch.Level);
				_player.ShakeCamera(touch.Level);
				// Daños: solo cuentan los impactos en carrera (ni en la previa ni con la carrera terminada)
				if (_race.State == RaceState.Running)
					_damage?.AddImpact(touch.Level);
			}
			else
			{
				_barrierFlash.Graze(touch.Side, touch.AxisDistance);
			}
		}
		// El del jugador y los de los rivales
		_track.SetBarrierFlashes(_rivals == null ? new[] { _barrierFlash } : _rivals.Flashes.Prepend(_barrierFlash));

		bool canRespawn = _race.CanDrive && _race.State != RaceState.Finished;
		if (!canRespawn)
		{
			_respawn.Reset();
			return;
		}

		// El atasco cuenta contra la barrera o contra otra nave
		bool touchedShip = _rivals != null && _rivals.Contacts.Touched(_player.Id);
		if (_respawn.Update(delta, touch.Contact || touchedShip, _player.Speed, touch.Lateral, _player.IsAirborne))
		{
			_track.GetRespawn(_player.GlobalPosition, out Vector3 position, out float yaw);
			_player.Respawn(position, yaw);
			_collisionFx?.CutDrift(_player.Id);
			_playerRespawns++;
			GD.Print("[Race] Ship repositioned on the track");
		}
	}

	// Golpes del jugador con otras naves en este paso: sacudida como con la barrera (medio y fuerte) y daño según su papel
	// (solo en carrera)
	private void UpdatePlayerShipContacts()
	{
		for (int ci = 0; ci < _rivals.Contacts.Events.Count; ci++)
		{
			ShipContactEvent e = _rivals.Contacts.Events[ci];
			if (!e.IsNewImpact || (e.A != _player.Id && e.B != _player.Id))
				continue;
			ContactRole role = e.A == _player.Id ? e.RoleA : e.RoleB;
			_player.ShakeCamera(e.Level);
			if (_race.State == RaceState.Running)
				_damage?.AddPoints(_rivals.ContactPoints(role, e.Level));
		}
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
			// La posición sale de la clasificación final (la misma del panel); sin ella, la del propio tracker
			RaceResult result = _race.Tracker.Result?.WithClassification(_standings?.Final, PlayerStandingsId);
			_hud.ShowResults(result, _resultsMenu, ResultsBandHeight, ResultsOptionFontSize, ResultsShipNameFontSize,
				_raceRecords.NewTotalTime, _raceRecords.NewBestLap);
			_resultsShown = true;
			return;   // el Enter de este frame no elige todavía
		}

		// Opciones en fila: también ← →
		if (Input.IsActionJustPressed(SnMenuDown) || Input.IsActionJustPressed(SnMenuRight))
		{
			_resultsMenu.MoveNext();
			_hud.RefreshResultsOptions();
		}
		else if (Input.IsActionJustPressed(SnMenuUp) || Input.IsActionJustPressed(SnMenuLeft))
		{
			_resultsMenu.MovePrevious();
			_hud.RefreshResultsOptions();
		}
		else if (Input.IsActionJustPressed(SnMenuAccept))
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

		float lapFraction = CockpitInstruments.LapFraction(_player.TrackDistance, _track.LapLength);

		if (_mode == GameMode.TimeTrial)
		{
			bool outLap = tracker == null || tracker.State == RaceState.PreStart;
			string lapText = outLap ? "OUT LAP" : $"LAP {tracker.CurrentLap}";
			_hud.SetLapInfo(lapText, outLap ? 0 : tracker.CurrentLapTime, TimeTrialRecord()?.LapTime);
			_player.SetCockpitLap(lapText, lapFraction);
			return;
		}

		if (tracker == null)
			return;

		// Antes de arrancar CurrentLap vale 0; se muestra como vuelta 1
		int lap = Math.Max(1, tracker.CurrentLap);
		string text = $"LAP {lap}/{tracker.TotalLaps}";
		_hud.SetLapInfo(text, tracker.CurrentLapTime, tracker.BestLapTime);
		_player.SetCockpitLap(text, lapFraction);
	}

	private TimeTrialRecord TimeTrialRecord() => _records.Book.GetTimeTrial(_track.CircuitId);

	// Clasificación de RACE (spec docs/rivales-2-clasificacion.md, 3): el jugador y los rivales; ni las naves de prueba
	// ni el fantasma
	private void SetupStandings()
	{
		_standings = new RaceStandings(_track.CheckpointDistances, _track.LapLength, PlayerStandingsId, StandingsMarkStep,
			RaceSession.RaceLaps);
		_standings.FinalChanged += OnFinalClassificationChanged;
		_standingsNames[PlayerStandingsId] = PilotCatalog.PlayerName;
		// La nave de la sesión, como en los resultados (F10 no la cambia)
		_standingsShipNames[PlayerStandingsId] = ShipCatalog.Find(_player.Definition.Id)?.Name ?? "";
		if (_rivals != null)
		{
			for (int ri = 0; ri < _rivals.Ships.Count; ri++)
			{
				RivalShip ship = _rivals.Ships[ri];
				_standingsNames[ship.Car.Entry.Number] = ship.Car.Entry.Pilot.Nickname;
				_standingsShipNames[ship.Car.Entry.Number] = ShipCatalog.Find(ship.Car.Ship.Id)?.Name ?? ship.Car.Ship.Id;
			}
		}
		UpdateStandings(0f);
	}

	// Cada frame: el orden cambia al momento; los intervalos, cada IntervalRefresh s, para que no tiemblen
	private void UpdateStandings(float delta)
	{
		if (_standings == null)
			return;

		_standingsShips.Clear();
		RaceTracker tracker = _race.Tracker;
		if (tracker != null)
			_standingsShips.Add(ToStandings(PlayerStandingsId, _playerGridPosition, tracker, _player.TrackDistance));
		if (_rivals != null)
		{
			for (int ri = 0; ri < _rivals.Ships.Count; ri++)
			{
				RivalShip ship = _rivals.Ships[ri];
				RivalCar car = ship.Car;
				_standingsShips.Add(ToStandings(car.Entry.Number, car.Entry.GridPosition, car.Tracker, car.TrackDistance));
			}
		}
		_standings.Update(_standingsShips);
		_overtakes.Update(_standings.Order, IsRacing);
		PrintReportWhenAllFinished();

		_intervalRefreshLeft -= delta;
		if (_intervalRefreshLeft <= 0f || _intervalTexts.Count == 0)
		{
			_intervalRefreshLeft = IntervalRefresh;
			foreach (int id in _standings.Order)
			{
				if (id != PlayerStandingsId)
					_intervalTexts[id] = StandingsFormat.Interval(_standings.IntervalOf(id));
			}
		}

		StandingsRow?[] rows = StandingsFormat.Slots(_standings.Order, PlayerStandingsId,
			id => _standingsNames.TryGetValue(id, out string name) ? name : "",
			id => _intervalTexts.TryGetValue(id, out string text) ? text : StandingsFormat.NoData);
		_hud.SetStandings(rows, _standings.Count);
	}

	// Corriendo de verdad (para los adelantamientos): ni en la previa ni terminada
	// Con bucles for sobre las naves: un foreach sobre la lista crea un enumerador en cada vuelta, y esto va en cada frame
	private bool IsRacing(int id)
	{
		if (id == PlayerStandingsId)
			return _race.Tracker?.State == RaceState.Running;
		for (int ri = 0; ri < _rivals.Ships.Count; ri++)
		{
			RivalShip ship = _rivals.Ships[ri];
			if (ship.Car.Entry.Number == id)
				return ship.Car.Phase == RivalPhase.Running;
		}
		return false;
	}

	// Informe de carrera (spec docs/rivales-4-personalidades.md, 8) en la consola, una vez, cuando han terminado todos
	// los rivales; el puesto es el de la clasificación en ese momento
	private void PrintReportWhenAllFinished()
	{
		if (_reportPrinted || _rivals == null || _rivals.Ships.Count == 0)
			return;
		for (int ri = 0; ri < _rivals.Ships.Count; ri++)
		{
			RivalShip ship = _rivals.Ships[ri];
			if (ship.Car.Phase != RivalPhase.Finished)
				return;
		}
		_reportPrinted = true;

		var rows = new List<RaceReportRow>();
		IReadOnlyList<int> order = _standings.Order;
		int PositionOf(int id)
		{
			for (int i = 0; i < order.Count; i++)
			{
				if (order[i] == id)
					return i + 1;
			}
			return order.Count + 1;
		}
		rows.Add(new RaceReportRow(PositionOf(PlayerStandingsId), PilotCatalog.PlayerName, "—",
			_standingsShipNames.GetValueOrDefault(PlayerStandingsId, ""), _overtakes.Made(PlayerStandingsId),
			_overtakes.Suffered(PlayerStandingsId), null, null, null, _damage?.Points ?? 0, _damage?.Level ?? 0, null, null, null, null));
		for (int ri = 0; ri < _rivals.Ships.Count; ri++)
		{
			RivalShip ship = _rivals.Ships[ri];
			RivalCar car = ship.Car;
			int id = car.Entry.Number;
			PilotErrors errors = car.Driver.Errors;
			rows.Add(new RaceReportRow(PositionOf(id), car.Entry.Pilot.Nickname, PilotProfiles.ScreenName(car.Entry.Pilot.Profile),
				_standingsShipNames.GetValueOrDefault(id, ""), _overtakes.Made(id), _overtakes.Suffered(id), car.Rams, car.Received,
				car.Mutual, car.Damage.Points, car.Damage.Level, car.Driver.Boosts, errors.LateBrakings, errors.WideExits, errors.BadStarts));
		}
		GD.Print(RaceReport.Text(rows));
	}

	private StandingsShip ToStandings(int id, int grid, RaceTracker tracker, float trackDistance) =>
		new(id, grid, tracker.CurrentLap, tracker.NextCheckpointIndex, trackDistance,
			tracker.State == RaceState.Running, tracker.State == RaceState.Finished, tracker.TotalTime,
			tracker.LastLapTime, tracker.BestLapTime, _standingsNames.GetValueOrDefault(id, ""), _standingsShipNames.GetValueOrDefault(id, ""));

	// Clasificación final (spec docs/rivales-2-clasificacion.md, 4): en la consola al fijarla con tu llegada y con cada
	// llegada de un rival, que pasa de estimado a real. Hasta la etapa 3 los resultados solo enseñan la posición.
	private void OnFinalClassificationChanged(RaceClassification final, int movedId)
	{
		if (movedId >= 0)
		{
			GD.Print($"[Race] {_standingsNames.GetValueOrDefault(movedId, movedId.ToString())} finished in position "
				+ $"{final.PositionOf(movedId)}, not where it was at your arrival");
		}
		var text = new System.Text.StringBuilder();
		text.Append($"[Race] Classification ({final.EstimatedCount} estimated):");
		foreach (ClassificationRow row in final.Rows)
		{
			string best = row.BestLapTime is double lap ? TimeFormat.Format(lap) : NoTimeText;
			string gap = row.Position == 1 ? "" : $"  +{TimeFormat.Format(final.GapToLeader(row.Position))}";
			text.Append('\n');
			text.Append($"  {row.Position}  {row.Name,-8}{row.ShipName,-7}{TimeFormat.Format(row.TotalTime)}"
				+ $"{(row.IsEstimated ? " EST" : "    ")}{gap}  best {best}");
		}
		GD.Print(text.ToString());
	}

	private const string NoTimeText = "--:--.---";

	// Sector cerrado: tiempo y diferencia con el mejor anterior de la nave, en el panel del checkpoint (si el sector
	// termina en uno; el último termina en la meta) y en el HUD
	private void OnSectorTimed(int sector, double time, double? previousBest)
	{
		SectorTimeText text = SectorTimeText.Build(sector, time, previousBest);
		if (sector < _track.CheckpointIndices.Count)
			_track.Elements.ShowCheckpointTime(sector, text);
		_hud.ShowSectorTime(text);
	}

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

		GhostRecording lapGhost = _recorder.Finish(lapTime, ToNumerics(_player.GlobalPosition), ToNumerics(_player.ShipRotation));

		if (!RecordBook.IsValidCircuitId(_track.CircuitId))
		{
			_hud.TimeTrial.ShowLapBand(new LapBandContent(lap, lapTime, "LAP COMPLETE", LapBandTitleStyle.Record,
				"RECORDS OFF  ·  CIRCUIT HAS NO ID"));
			return;
		}

		// Con el piloto automático, esta carrera no guarda récord ni fantasma
		if (_player.AutopilotUsed)
		{
			_hud.TimeTrial.ShowLapBand(new LapBandContent(lap, lapTime, "LAP COMPLETE", LapBandTitleStyle.Record,
				"RECORDS OFF  ·  AUTOPILOT"));
			return;
		}

		TimeTrialLapResult result = _records.Book.SubmitTimeTrialLap(_track.CircuitId, lapTime, splits, DateTime.Now, TuningLabPanel.DrivenShipId(GameSession.ShipDesignId));
		LapBandContent content;
		if (result.IsNewRecord)
		{
			string detail = result.PreviousLapTime.HasValue
				? $"{DeltaFormat.Format(lapTime - result.PreviousLapTime.Value)}  ·  PREVIOUS {TimeFormat.Format(result.PreviousLapTime.Value)}"
				: "FIRST RECORD";
			content = new LapBandContent(lap, lapTime, "NEW RECORD", LapBandTitleStyle.Record, detail);
			SaveRecords();
			SaveGhost(lapGhost);
		}
		else
		{
			content = ComparedLapBand(lap, lapTime, result.PreviousLapTime ?? lapTime);
		}

		_hud.TimeTrial.ShowLapBand(content);
	}

	// Franja de una vuelta que no es récord nuevo: su delta contra el mejor tiempo
	private static LapBandContent ComparedLapBand(int lap, double lapTime, double best)
	{
		double delta = DeltaFormat.Delta(lapTime, best);
		return new LapBandContent(lap, lapTime, DeltaFormat.Format(delta),
			DeltaFormat.IsBetter(delta) ? LapBandTitleStyle.Better : LapBandTitleStyle.Worse,
			$"BEST {TimeFormat.Format(best)}");
	}

	// RACE terminada: tiempo total y mejor vuelta contra los récords de RACE (una carrera
	// abandonada desde la pausa no llega aquí y no guarda nada)
	private void OnRaceFinished(RaceResult result)
	{
		if (_mode != GameMode.Race || result == null)
			return;

		_damage?.Stop();
		_player.Passable.Finished = true;

		// Con el piloto automático, esta carrera no guarda récords
		if (_player.AutopilotUsed)
			return;

		_raceRecords =_records.Book.SubmitRace(_track.CircuitId, result.TotalTime, result.BestLapTime, DateTime.Now, TuningLabPanel.DrivenShipId(GameSession.ShipDesignId));
		if (_raceRecords.NewTotalTime || _raceRecords.NewBestLap)
			SaveRecords();
	}

	// ---------------------------------------------------------------
	// Daños (RACE)
	// ---------------------------------------------------------------

	// Cada carrera empieza sin daño (RESTART recarga la escena y crea un modelo nuevo)
	private void SetupDamage()
	{
		_damage = new DamageModel(BuildDamageSettings());
		_damage.LevelChanged += OnDamageLevelChanged;
		_hud.ShowDamageIndicator();
		_player.CockpitDamageEnabled = true;
	}

	// Reglas del daño del grupo Daños: las del jugador y las de los rivales
	private DamageSettings BuildDamageSettings()
	{
		return new DamageSettings
		{
			MediumPoints = DamageMediumPoints,
			StrongPoints = DamageStrongPoints,
			Level1Points = DamageLevel1Points,
			Level2Points = DamageLevel2Points,
			Level3Points = DamageLevel3Points,
			Level2TopSpeedFactor = DamageLevel2TopSpeed,
			Level3TopSpeedFactor = DamageLevel3TopSpeed,
			Level3TurnFactor = DamageLevel3Turn
		};
	}

	private RespawnSettings BuildRespawnSettings()
	{
		return new RespawnSettings
		{
			BarrierOffset = _track.BarrierOffset,
			StuckSpeed = StuckSpeed / 3.6f,
			StuckTime = StuckTime,
			OutsideMargin = OutsideTrackMargin
		};
	}

	// Lo que comparten los rivales: el circuito y las mismas reglas que el jugador (física, daño, recolocación y destello)
	private ContactDamageSettings BuildContactDamageSettings()
	{
		return new ContactDamageSettings
		{
			MediumRammer = ContactMediumRammer,
			MediumReceiver = ContactMediumReceiver,
			MediumMutual = ContactMediumMutual,
			StrongRammer = ContactStrongRammer,
			StrongReceiver = ContactStrongReceiver,
			StrongMutual = ContactStrongMutual
		};
	}

	private RivalRaceContext BuildRivalContext()
	{
		return new RivalRaceContext
		{
			Frame = _track.Frame,
			TrackWidth = _track.TrackWidth,
			BarrierOffset = _track.BarrierOffset,
			LapLength = _track.LapLength,
			CheckpointIndices = _track.CheckpointIndices,
			Laps = RaceSession.RaceLaps,
			Grid = _track.GridSettings,
			Hover = Track.ShipHover,
			Physics = _player.BuildPhysicsSettings(),
			Damage = BuildDamageSettings(),
			Respawn = BuildRespawnSettings(),
			Flash = _track.BarrierFlashSettings,
			StepTime = 1f / Engine.PhysicsTicksPerSecond,
			Difficulty = GameSession.Difficulty
		};
	}

	// Reparte el nivel nuevo: factores y boost a la nave, parpadeo de sus aristas e indicador del HUD
	private void OnDamageLevelChanged(int level)
	{
		_player.TopSpeedFactor = _damage.TopSpeedFactor;
		_player.TurnRateFactor = _damage.TurnFactor;
		_player.BoostAllowed = _damage.BoostAllowed;
		_player.SetFlickerLevel(level);
		_hud.SetDamageLevel(level);
		GD.Print($"[Race] Damage level {level} ({_damage.Points} points)");
	}

	// ---------------------------------------------------------------
	// Ajustes y pintura
	// ---------------------------------------------------------------

	private void LoadSettings()
	{
		_settings = new SettingsStore(new FileRecordStorage(SettingsStore.DefaultPath));
		_settings.Load();
		if (_settings.Warning != null)
			GD.PushWarning($"Settings: {_settings.Warning}");
	}

	// Pintura guardada de cada nave (spec docs/selector-pinturas.md, 3.3): la de la nave elegida se ve al momento y la
	// de las demás queda puesta para cuando F10 cambie a ellas
	private void ApplySavedPaints()
	{
		foreach (ShipDefinition definition in ShipDefinitions.All)
		{
			if (HullPaints.StyleOf(definition.Id) is not HullStyle style || _settings.Settings.PaintFor(definition.Id) is not PaintChoice choice)
				continue;
			_player.SetHullPaint(style, HullPaints.IndexOf(style, choice));
		}
	}

	// ---------------------------------------------------------------
	// Fantasma (TIME TRIAL)
	// ---------------------------------------------------------------

	// Ajustes, grabador, nave fantasma y fantasma guardado del circuito (si sigue siendo válido)
	private void SetupGhost()
	{
		_ghosts = new GhostStore(new FileGhostStorage());
		_recorder = new GhostRecorder(Math.Max(1, GhostSampleRate));
		_trackSignature = _track.Signature;

		GhostRecording saved = _ghosts.Load(_track.CircuitId);
		if (_ghosts.Warning != null)
			GD.PushWarning($"Ghost: {_ghosts.Warning}");
		if (saved != null)
		{
			if (GhostValidity.IsValid(saved, _track.CircuitId, _track.CheckpointIndices.Count, _trackSignature, TimeTrialRecord()?.LapTime))
				_ghost = saved;
			else
				GD.Print("[Ghost] The saved ghost does not match this circuit or its record; no ghost until a new record.");
		}

		// Igual que Track y Player, se usa el de la escena si existe (así se ajusta en el Inspector)
		_ghostShip = GetNodeOrNull<GhostShip>("GhostShip");
		if (_ghostShip == null)
		{
			_ghostShip = new GhostShip { Name = "GhostShip" };
			AddChild(_ghostShip);
		}
		if (_ghost != null)
			ShowGhostShipHull();

		_pause.EnableGhostOption(_settings.Settings.GhostEnabled, () => _ghost != null);
		_pause.GhostToggled += OnGhostToggled;
		_race.LapStarted += OnLapStarted;
	}

	// Cada vuelta se graba desde cero, aunque el fantasma esté en OFF
	private void OnLapStarted(int lap)
	{
		_recorder.Begin();
		SampleGhost();
	}

	private void SampleGhost()
	{
		_recorder.Sample(_race.Tracker.CurrentLapTime, ToNumerics(_player.GlobalPosition), ToNumerics(_player.ShipRotation));
	}

	// Vuelta récord: su grabación pasa a ser el fantasma del circuito y se guarda ya
	private void SaveGhost(GhostRecording lapGhost)
	{
		if (lapGhost == null)
			return;

		_ghost = lapGhost with
		{
			CircuitId = _track.CircuitId,
			TrackSignature = _trackSignature,
			CheckpointCount = _track.CheckpointIndices.Count,
			ShipDesignId = TuningLabPanel.DrivenShipId(GameSession.ShipDesignId)
		};
		ShowGhostShipHull();
		if (_ghosts.Save(_ghost))
			GD.Print($"[Ghost] New ghost saved ({_ghost.Samples.Count} samples)");
		else
			GD.PushWarning($"Ghost: {_ghosts.Warning}");
	}

	// El casco del fantasma es el de la nave con la que se hizo la vuelta, no el de la que corre ahora
	private void ShowGhostShipHull()
	{
		_ghostShip.SetVisual(_ghost.Ship(message => GD.PushWarning(message)).Visual);
	}

	private void OnGhostToggled(bool enabled)
	{
		_settings.Settings.GhostEnabled = enabled;
		if (!_settings.Save())
			GD.PushWarning($"Settings: {_settings.Warning}");
	}

	// El fantasma se ve con el ajuste en ON, con fantasma válido y durante una vuelta cronometrada;
	// su pose sale del tiempo de vuelta actual (así sigue sincronizado tras la pausa)
	private void UpdateGhost()
	{
		if (_ghostShip == null)
			return;

		RaceTracker tracker = _race.Tracker;
		if (!_settings.Settings.GhostEnabled || _ghost == null || tracker == null || tracker.State != RaceState.Running)
		{
			_ghostShip.HideGhost();
			return;
		}

		_ghostShip.ShowAt(_ghost, tracker.CurrentLapTime, _player.GlobalPosition);
	}

	private static System.Numerics.Vector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

	private static System.Numerics.Quaternion ToNumerics(Quaternion q) => new(q.X, q.Y, q.Z, q.W);

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
