using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Game;
using Vectory.Ghost;
using Vectory.Records;
using Vectory.UI;

namespace Vectory.Menu;

// Pantalla SELECT CIRCUIT, dentro de la escena del menú principal (spec docs/selector-circuito.md):
// a la izquierda el nombre del circuito y su miniatura 3D girando (CircuitMiniature); a la derecha el panel
// de telemetría con los datos del trazado, el perfil de elevación, los récords del modo y SHIP SELECTION / BACK.
// Las reglas viven en CircuitSelectLogic, TrackAnalysis y CircuitSelectFormat; este nodo traduce el input,
// lee los récords y fantasmas con los almacenes de siempre y lo dibuja. SHIP SELECTION deja el modo y el circuito
// en GameSession y lanza ShipSelectRequested (se abre SELECT SHIP); BACK / ESC lanza Closed (MainMenu reaparece).
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor.
public partial class CircuitSelectScreen : CanvasLayer
{
	public const string FooterHint = "← → CIRCUIT · ↑ ↓ OPTION · ENTER SELECT";

	// Posiciones del lienzo de diseño (px sobre 1280×720)
	private const float SectionGap = 14f;
	private const float RecordLabelWidth = 120f;

	[ExportGroup("Aspecto (px sobre 1280×720)")]
	[Export] public int NameFontSize = 54;
	[Export] public int OptionFontSize = 22;
	[Export] public Vector2 ProfileSize = new(356f, 70f);
	[Export] public int ProfileSamples = TrackAnalysis.DefaultProfileSamples;
	[Export] public float ProfileMinScale = TrackAnalysis.DefaultMinProfileScale;   // m

	[ExportGroup("Miniatura")]
	[Export] public float MiniatureRotationPeriod = 50f;   // segundos por vuelta
	[Export] public float MiniatureTilt = 62f;             // grados respecto a la vista cenital
	[Export] public float MiniatureSize = 700f;            // px sobre 1280×720

	// Se lanza al volver al menú principal, con el modo de la pantalla
	public event Action<GameMode> Closed;

	// Se lanza con SHIP SELECTION, con el modo y el nombre del circuito: toca abrir SELECT SHIP
	public event Action<GameMode, string> ShipSelectRequested;

	public bool IsOpen => _root != null && _root.Visible;

	// Fondo del menú: se oculta mientras la pantalla está abierta (lo asigna MainMenu)
	public MenuBackground Background { get; set; }

	// Un circuito del catálogo con sus datos (se calculan al verlo por primera vez con la pantalla abierta)
	private sealed class Circuit
	{
		public string Path;
		public TrackDefinition Definition;
		public bool Computed;
		public List<System.Numerics.Vector3> Centerline;
		public TrackData Data;
		public int CheckpointCount;
		public string Signature;
		public bool? GhostAvailable;
	}

	private readonly List<Circuit> _circuits = new();
	private RecordStore _store;
	private GhostStore _ghosts;
	private CircuitSelectLogic _logic;
	private SelectScreenKit _kit;
	private float _scale = 1f;
	private ulong _openedFrame;

	private Control _root;
	private CircuitMiniature _miniature;
	private Label _header;
	private Label _name;
	private Control _testTag;
	private Label _description;
	private Label _counter;
	private Label _arrowLeft;
	private Label _arrowRight;
	private Control _buttonLeft;
	private Control _buttonRight;
	private readonly Dictionary<string, Label> _dataValues = new();
	private ElevationProfileView _profile;
	private Label _recordsHeader;
	private Label _record1Label;
	private Label _record1Value;
	private Label _record2Label;
	private Label _record2Value;
	private OptionList _options;

	private LabelSettings _valueSettings;
	private LabelSettings _emptyValueSettings;
	private LabelSettings _arrowActive;
	private LabelSettings _arrowInactive;

	public override void _Ready()
	{
		Layer = 3;   // por encima del menú principal (capa 2) y de la sombra del fondo (capa 1)
		_scale = UiScale.Factor;
		_kit = new SelectScreenKit(_scale);

		foreach (string path in CircuitCatalog.Paths)
		{
			var definition = ResourceLoader.Exists(path) ? GD.Load<TrackDefinition>(path) : null;
			if (definition == null)
				GD.PushWarning($"CircuitSelectScreen: no se encuentra el circuito '{path}'.");
			_circuits.Add(new Circuit { Path = path, Definition = definition });
		}

		_store = new RecordStore(new FileRecordStorage());
		_ghosts = new GhostStore(new FileGhostStorage());

		_miniature = new CircuitMiniature
		{
			Name = "Miniature",
			RotationPeriod = MiniatureRotationPeriod,
			TiltDegrees = MiniatureTilt,
			ScreenSize = MiniatureSize
		};
		AddChild(_miniature);

		Build();
		_root.Visible = false;
	}

	// Abre la pantalla en un modo: lee los récords, empieza en el circuito recordado (GameSession) con SHIP SELECTION seleccionada
	public void Open(GameMode mode)
	{
		_store.Load();
		if (_store.Warning != null)
			GD.PushWarning($"Records: {_store.Warning}");

		// Los datos se calculan de nuevo en cada apertura (récords y fantasmas pueden haber cambiado)
		foreach (Circuit circuit in _circuits)
		{
			circuit.Computed = false;
			circuit.GhostAvailable = null;
		}

		_logic = new CircuitSelectLogic(mode, CircuitCatalog.Paths, GameSession.CircuitPath);
		_options.SetModel(_logic.Menu);
		_openedFrame = Engine.GetProcessFrames();   // el Enter que abre la pantalla no elige nada aquí

		Background?.SetSceneryVisible(false);
		_miniature.SetActive(true);
		_root.Visible = true;
		UpdateView();
	}

	public override void _Process(double delta)
	{
		if (!IsOpen || Engine.GetProcessFrames() == _openedFrame)
			return;

		if (Input.IsActionJustPressed("menu_back"))
			Apply(_logic.Back());
		else if (Input.IsActionJustPressed("menu_left"))
			ChangeCircuit(-1);
		else if (Input.IsActionJustPressed("menu_right"))
			ChangeCircuit(1);
		else if (Input.IsActionJustPressed("menu_down"))
		{
			_logic.Menu.MoveNext();
			_options.Refresh();
		}
		else if (Input.IsActionJustPressed("menu_up"))
		{
			_logic.Menu.MovePrevious();
			_options.Refresh();
		}
		else if (Input.IsActionJustPressed("menu_accept"))
			Apply(_logic.Choose(_logic.Menu.Confirm()));
	}

	private void ChangeCircuit(int step)
	{
		if (!_logic.CanChangeCircuit)
			return;

		if (step > 0)
			_logic.NextCircuit();
		else
			_logic.PreviousCircuit();
		UpdateView();
	}

	private void Apply(CircuitSelectAction action)
	{
		switch (action)
		{
			case CircuitSelectAction.Start:
				GameSession.Mode = _logic.Mode;
				GameSession.CircuitPath = _logic.CircuitPath;
				_root.Visible = false;
				_miniature.SetActive(false);   // el fondo del menú sigue oculto: SELECT SHIP pone su cámara
				ShipSelectRequested?.Invoke(_logic.Mode, DisplayName(_circuits[_logic.CircuitIndex]));
				break;

			case CircuitSelectAction.BackToMenu:
				// Se recuerda el circuito que se estaba viendo para la próxima vez
				GameSession.CircuitPath = _logic.CircuitPath;
				_root.Visible = false;
				_miniature.SetActive(false);
				Background?.SetSceneryVisible(true);
				Closed?.Invoke(_logic.Mode);
				break;
		}
	}

	// ---------------------------------------------------------------
	// Datos
	// ---------------------------------------------------------------

	private void Compute(Circuit circuit)
	{
		if (circuit.Computed)
			return;

		circuit.Computed = true;
		circuit.Centerline = null;
		circuit.Data = null;

		TrackDefinition definition = circuit.Definition;
		if (definition == null)
			return;
		if (!definition.IsValid(out string error))
		{
			GD.PushWarning($"CircuitSelectScreen: el circuito '{definition.DisplayName}' no es válido: {error}");
			return;
		}

		// El mismo eje que la carrera (a la altura de la pista de Track, con la altura del circuito), sin montar
		// la escena. Los checkpoints van por el eje en planta, como en la carrera.
		TrackFrame frame = Track.BuildFrame(definition, Track.DefaultTrackY);
		circuit.Centerline = new List<System.Numerics.Vector3>(frame.Points);
		float[] cumulative = CenterlineBuilder.CumulativeDistances(circuit.Centerline);
		List<int> checkpoints = CenterlineBuilder.CheckpointIndices(Track.BuildCenterline(definition, Track.DefaultTrackY),
			definition.CheckpointCount);
		circuit.CheckpointCount = checkpoints.Count;
		circuit.Signature = Track.ComputeSignature(frame, definition.TrackWidth);
		circuit.Data = TrackAnalysis.Analyze(circuit.Centerline, definition.TrackWidth,
			checkpoints.Select(i => cumulative[i]).ToArray(), Track.DefaultGridSlots,
			ProfileSamples, ProfileMinScale, frame.MaxAbsBank);
	}

	// Mismas reglas que la pausa de TIME TRIAL: fantasma guardado válido para el circuito y su récord
	private bool GhostAvailable(Circuit circuit)
	{
		if (circuit.GhostAvailable.HasValue)
			return circuit.GhostAvailable.Value;

		bool available = false;
		string id = circuit.Definition?.Id ?? "";
		if (circuit.Data != null && RecordBook.IsValidCircuitId(id))
		{
			GhostRecording ghost = _ghosts.Load(id);
			if (_ghosts.Warning != null)
				GD.PushWarning($"Ghost: {_ghosts.Warning}");
			available = GhostValidity.IsValid(ghost, id, circuit.CheckpointCount, circuit.Signature,
				_store.Book.GetTimeTrial(id)?.LapTime);
		}

		circuit.GhostAvailable = available;
		return available;
	}

	// ---------------------------------------------------------------
	// Vista
	// ---------------------------------------------------------------

	private void UpdateView()
	{
		Circuit circuit = _circuits[_logic.CircuitIndex];
		Compute(circuit);
		TrackDefinition definition = circuit.Definition;
		TrackData data = circuit.Data;
		GameMode mode = _logic.Mode;

		_header.Text = CircuitSelectFormat.Header(mode);
		_name.Text = DisplayName(circuit).ToUpperInvariant();
		_testTag.Visible = definition != null && definition.IsTest;
		_description.Text = data != null ? CircuitSelectFormat.Description(definition.IsTest, data.Clockwise) : "";
		_counter.Text = CircuitSelectFormat.Counter(_logic.CircuitIndex, _logic.CircuitCount);

		bool arrows = _logic.CanChangeCircuit;
		foreach (Label arrow in new[] { _arrowLeft, _arrowRight })
			arrow.LabelSettings = arrows ? _arrowActive : _arrowInactive;
		foreach (Control button in new[] { _buttonLeft, _buttonRight })
		{
			button.MouseFilter = arrows ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
			button.MouseDefaultCursorShape = arrows ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
		}

		_miniature.ShowCircuit(circuit.Centerline, definition?.TrackWidth ?? 0f);

		// TRACK DATA
		string none = CircuitSelectFormat.None;
		SetData("LENGTH", data != null ? CircuitSelectFormat.Length(data.Length) : none);
		SetData("LAPS", CircuitSelectFormat.Laps(mode, RaceSession.RaceLaps));
		SetData("TURNS", data != null ? CircuitSelectFormat.Count(data.Turns) : none);
		SetData("LONGEST STRAIGHT", data != null ? CircuitSelectFormat.Straight(data.LongestStraight) : none);
		SetData("TRACK WIDTH", data != null ? CircuitSelectFormat.Width(data.TrackWidth) : none);
		SetData("SECTORS", data != null ? CircuitSelectFormat.Count(data.Sectors) : none);
		SetData("MAX BANKING", data != null ? CircuitSelectFormat.Banking(data.MaxBanking) : none);
		SetData("GRID", data != null ? CircuitSelectFormat.Grid(mode, data.GridSlots) : none);

		_profile.SetData(data);

		// RECORDS del modo
		string id = definition?.Id ?? "";
		_recordsHeader.Text = CircuitSelectFormat.RecordsHeader(mode);
		if (mode == GameMode.TimeTrial)
		{
			_record1Label.Text = "BEST LAP";
			SetTime(_record1Value, _store.Book.GetTimeTrial(id)?.LapTime);
			_record2Label.Text = "GHOST";
			bool ghost = GhostAvailable(circuit);
			_record2Value.Text = CircuitSelectFormat.Ghost(ghost);
			_record2Value.LabelSettings = ghost ? _valueSettings : _emptyValueSettings;
		}
		else
		{
			RaceRecord race = _store.Book.GetRace(id);
			_record1Label.Text = "TOTAL TIME";
			SetTime(_record1Value, race?.TotalTime?.Time);
			_record2Label.Text = "BEST LAP";
			SetTime(_record2Value, race?.BestLap?.Time);
		}

		_options.Refresh();
	}

	private static string DisplayName(Circuit circuit) =>
		circuit.Definition?.DisplayName ?? circuit.Path.GetFile().GetBaseName();

	private void SetData(string label, string value) => _dataValues[label].Text = value;

	private void SetTime(Label label, double? time)
	{
		label.Text = CircuitSelectFormat.Time(time);
		label.LabelSettings = time.HasValue ? _valueSettings : _emptyValueSettings;
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private float S(float referencePx) => _kit.S(referencePx);

	private int Px(float referencePx) => _kit.Px(referencePx);

	private void Build()
	{
		_root = new Control { Name = "CircuitSelectRoot", MouseFilter = Control.MouseFilterEnum.Stop };   // tapa el ratón al menú de debajo
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_valueSettings = _kit.TextSettings(MenuFonts.Orbitron, 500, 19f, 0.06f, Palette.SelectValue);
		_emptyValueSettings = _kit.TextSettings(MenuFonts.Orbitron, 500, 19f, 0.06f, Palette.RecordsEmptyTime);
		_arrowActive = _kit.TextSettings(MenuFonts.Orbitron, 500, 22f, 0f, Palette.SelectArrow);
		_arrowInactive = _kit.TextSettings(MenuFonts.Orbitron, 500, 22f, 0f, Palette.SelectArrowDisabled);

		BuildNameBlock();
		BuildPanel();
		_kit.Footer(_root, FooterHint);
	}

	private void BuildNameBlock()
	{
		VBoxContainer column = _kit.NameColumn(_root);

		_header = MakeLabel("", MenuFonts.Mono, 400, 15f, 0.22f, Palette.SelectHeader);
		column.AddChild(_header);

		var nameRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		nameRow.AddThemeConstantOverride("separation", Px(16f));
		column.AddChild(nameRow);

		_name = MakeLabel("", MenuFonts.Orbitron, 900, NameFontSize, 0.08f, Palette.SelectName, Palette.SelectNameGlow, 16f);
		nameRow.AddChild(_name);

		_testTag = _kit.Tag(CircuitSelectFormat.TestTag, Palette.SelectTestTag, Palette.SelectTestTagBorder);
		nameRow.AddChild(_testTag);

		_description = MakeLabel("", MenuFonts.Mono, 400, 14f, 0.12f, Palette.SelectLabel);
		column.AddChild(_description);

		var nav = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		nav.AddThemeConstantOverride("separation", Px(14f));
		column.AddChild(nav);

		_buttonLeft = _kit.NavButton("‹", () => ChangeCircuit(-1), _arrowInactive, out _arrowLeft);
		nav.AddChild(_buttonLeft);
		_counter = MakeLabel("", MenuFonts.Mono, 400, 14f, 0.12f, Palette.SelectLabel);
		_counter.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		nav.AddChild(_counter);
		_buttonRight = _kit.NavButton("›", () => ChangeCircuit(1), _arrowInactive, out _arrowRight);
		nav.AddChild(_buttonRight);
	}

	private void BuildPanel()
	{
		VBoxContainer content = _kit.Panel(_root, "TelemetryPanel");

		// TRACK DATA
		content.AddChild(SectionHeader("TRACK DATA"));
		var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
		grid.AddThemeConstantOverride("h_separation", Px(16f));
		grid.AddThemeConstantOverride("v_separation", Px(10f));
		content.AddChild(grid);
		foreach (string label in new[] { "LENGTH", "LAPS", "TURNS", "LONGEST STRAIGHT", "TRACK WIDTH", "SECTORS", "MAX BANKING", "GRID" })
			grid.AddChild(DataCell(label));

		// ELEVATION PROFILE
		content.AddChild(Gap());
		content.AddChild(SectionHeader("ELEVATION PROFILE"));
		_profile = new ElevationProfileView { Name = "ElevationProfile" };
		_profile.Setup(ProfileSize * _scale, Px(11f), _scale);
		content.AddChild(_profile);

		// RECORDS
		content.AddChild(Gap());
		_recordsHeader = SectionHeader("RECORDS");
		content.AddChild(_recordsHeader);
		(_record1Label, _record1Value) = RecordRow(content);
		(_record2Label, _record2Value) = RecordRow(content);

		// Opciones abajo del panel
		content.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

		int optionSize = OptionList.FitFontSize(CircuitSelectLogic.CreateMenu(), OptionFontSize, _scale, _kit.PanelContentWidth);
		_options = _kit.Options(optionSize, id => Apply(_logic.Choose(id)));
		content.AddChild(_options);
	}

	private Control Gap() => new() { CustomMinimumSize = new Vector2(0f, S(SectionGap - 8f)), MouseFilter = Control.MouseFilterEnum.Ignore };

	private Label SectionHeader(string text) => _kit.SectionHeader(text);

	// Dato: línea azul vertical de 2 px, etiqueta y valor debajo
	private Control DataCell(string label)
	{
		var cell = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		cell.AddThemeConstantOverride("separation", Px(10f));
		cell.AddChild(new ColorRect
		{
			Color = Palette.SelectDataBar,
			CustomMinimumSize = new Vector2(Mathf.Max(1f, S(2f)), 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		});

		var texts = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		texts.AddThemeConstantOverride("separation", Px(2f));
		cell.AddChild(texts);
		texts.AddChild(MakeLabel(label, MenuFonts.Mono, 400, 11f, 0.14f, Palette.SelectLabel));

		Label value = MakeLabel(CircuitSelectFormat.None, MenuFonts.Orbitron, 500, 17f, 0.06f, Palette.SelectValue);
		texts.AddChild(value);
		_dataValues[label] = value;
		return cell;
	}

	private (Label Label, Label Value) RecordRow(VBoxContainer content)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 0);
		content.AddChild(row);

		Label label = MakeLabel("", MenuFonts.Mono, 400, 12f, 0.18f, Palette.SelectLabel);
		label.CustomMinimumSize = new Vector2(S(RecordLabelWidth), 0f);
		label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);

		var value = new Label { Text = RecordFormat.NoTime, MouseFilter = Control.MouseFilterEnum.Ignore, LabelSettings = _emptyValueSettings };
		row.AddChild(value);
		return (label, value);
	}

	private Label MakeLabel(string text, FontFile file, int weight, float size, float letterSpacingEm,
		Color color, Color? glow = null, float glowSize = 0f) =>
		_kit.MakeLabel(text, file, weight, size, letterSpacingEm, color, glow, glowSize);
}
