using System;
using System.Collections.Generic;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Ghost;
using Vectory.Records;
using Vectory.UI;

namespace Vectory.Menu;

// Pantalla RECORDS, dentro de la escena del menú principal (encima de su fondo 3D):
// franja de tres columnas con el circuito y su selector, los récords de TIME TRIAL y RACE de ese
// circuito, y las opciones BACK / RESET RECORDS (con confirmación NO / YES en la misma franja).
// Las reglas viven en RecordsLogic y RecordFormat; este nodo traduce el input, lee y borra los
// récords con el RecordStore de siempre (RESET borra también los fantasmas) y lo dibuja. Al cerrarse lanza Closed (MainMenu reaparece).
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor.
public partial class RecordsScreen : CanvasLayer
{
	private const string ConfirmTitle = "RESET\nRECORDS?";
	private const string ConfirmHint = "ALL CIRCUITS AND MODES\nCANNOT BE UNDONE";

	// Posiciones del lienzo de diseño (px sobre 1280×720)
	private const float LeftX = 96f;
	private const float CenterX = 490f;
	private const float RightX = 950f;
	private const float RightMargin = 40f;
	private const float SeparatorHeight = 244f;
	private const float LabelWidth = 116f;
	private const float TimeWidth = 150f;

	[ExportGroup("Aspecto (px sobre 1280×720)")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float DimOpacity = 0.5f;
	[Export] public float BandHeight = 340f;
	[Export] public int TitleFontSize = 52;
	[Export] public int ConfirmTitleFontSize = 36;
	[Export] public int OptionFontSize = 22;   // máximo: se reduce si RESET RECORDS no cabe

	// Se lanza al volver al menú principal
	public event Action Closed;

	public bool IsOpen => _root != null && _root.Visible;

	// Un circuito de la lista: su Id (para los récords) y su nombre visible
	private readonly record struct CircuitInfo(string Id, string Name);

	// Fila de récord: tiempo y fecha (la etiqueta no cambia)
	private sealed class RecordRow
	{
		public Label Time;
		public Label Date;
		public LabelSettings TimeSettings;
	}

	private readonly List<CircuitInfo> _circuits = new();
	private RecordStore _store;
	private GhostStore _ghosts;
	private RecordsLogic _logic;
	private float _scale = 1f;
	private ulong _openedFrame;

	private Control _root;
	private VBoxContainer _viewLeft;
	private VBoxContainer _confirmLeft;
	private Label _circuitName;
	private Label _circuitCount;
	private Label _arrowLeft;
	private Label _arrowRight;
	private RecordRow _timeTrialLap;
	private RecordRow _raceTotal;
	private RecordRow _raceLap;
	private OptionList _options;

	private LabelSettings _emptyTimeSmall;
	private LabelSettings _emptyTimeLarge;
	private LabelSettings _arrowActive;
	private LabelSettings _arrowInactive;

	public override void _Ready()
	{
		Layer = 3;   // por encima del menú principal (capa 2) y de la sombra del fondo (capa 1)
		_scale = UiScale.Factor;

		foreach (string path in CircuitCatalog.Paths)
		{
			var definition = ResourceLoader.Exists(path) ? GD.Load<TrackDefinition>(path) : null;
			if (definition == null)
				GD.PushWarning($"RecordsScreen: no se encuentra el circuito '{path}'.");
			_circuits.Add(new CircuitInfo(definition?.Id ?? "", definition?.DisplayName ?? path.GetFile().GetBaseName()));
		}

		_store = new RecordStore(new FileRecordStorage());
		_ghosts = new GhostStore(new FileGhostStorage());
		Build();
		_root.Visible = false;
	}

	// Abre la pantalla: lee los récords y empieza en el primer circuito con BACK seleccionada
	public void Open()
	{
		_store.Load();
		if (_store.Warning != null)
			GD.PushWarning($"Records: {_store.Warning}");

		_logic = new RecordsLogic(_circuits.Count, _store.Book.HasAnyRecord);
		_openedFrame = Engine.GetProcessFrames();   // el Enter que abre la pantalla no elige nada aquí
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

	private void Apply(RecordsAction action)
	{
		switch (action)
		{
			case RecordsAction.BackToMenu:
				_root.Visible = false;
				Closed?.Invoke();
				return;

			case RecordsAction.ResetRecords:
				_store.Book.Clear();
				if (!_store.Save())
					GD.PushWarning($"Records: {_store.Warning}");
				if (!_ghosts.DeleteAll())
					GD.PushWarning($"Ghost: {_ghosts.Warning}");
				break;
		}

		UpdateView();
	}

	// ---------------------------------------------------------------
	// Vista
	// ---------------------------------------------------------------

	private void UpdateView()
	{
		bool confirming = _logic.State == RecordsState.ConfirmReset;
		_viewLeft.Visible = !confirming;
		_confirmLeft.Visible = confirming;

		CircuitInfo circuit = _circuits[_logic.CircuitIndex];
		_circuitName.Text = circuit.Name.ToUpperInvariant();
		_circuitCount.Text = $"{_logic.CircuitIndex + 1} / {_logic.CircuitCount}";

		bool arrows = _circuits.Count > 1;
		_arrowLeft.LabelSettings = arrows ? _arrowActive : _arrowInactive;
		_arrowRight.LabelSettings = arrows ? _arrowActive : _arrowInactive;
		_arrowLeft.MouseFilter = arrows ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		_arrowRight.MouseFilter = arrows ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;

		TimeTrialRecord timeTrial = _store.Book.GetTimeTrial(circuit.Id);
		RaceRecord race = _store.Book.GetRace(circuit.Id);
		SetRow(_timeTrialLap, timeTrial?.LapTime, timeTrial?.Date, _emptyTimeLarge);
		SetRow(_raceTotal, race?.TotalTime?.Time, race?.TotalTime?.Date, _emptyTimeSmall);
		SetRow(_raceLap, race?.BestLap?.Time, race?.BestLap?.Date, _emptyTimeSmall);

		if (_options.Model != _logic.Menu)
			_options.SetModel(_logic.Menu);
		else
			_options.Refresh();
	}

	private static void SetRow(RecordRow row, double? time, string date, LabelSettings emptySettings)
	{
		row.Time.Text = RecordFormat.Time(time);
		row.Time.LabelSettings = time.HasValue ? row.TimeSettings : emptySettings;
		row.Date.Text = RecordFormat.Date(time.HasValue, date);
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private float S(float referencePx) => referencePx * _scale;

	private int Px(float referencePx) => Mathf.RoundToInt(S(referencePx));

	private void Build()
	{
		_root = new Control { Name = "RecordsRoot", MouseFilter = Control.MouseFilterEnum.Stop };   // tapa el ratón al menú de debajo
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var band = new MenuBand { Name = "MenuBand" };
		_root.AddChild(band);
		band.Setup(BandHeight, DimOpacity, 0.9f);
		band.AddSeparator(450f, SeparatorHeight);
		band.AddSeparator(915f, SeparatorHeight);

		_emptyTimeSmall = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 500, Px(20f), 0.06f), Px(20f), Palette.RecordsEmptyTime, Colors.Transparent, 0f);
		_emptyTimeLarge = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 500, Px(24f), 0.06f), Px(24f), Palette.RecordsEmptyTime, Colors.Transparent, 0f);
		_arrowActive = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 500, Px(22f), 0f), Px(22f), Palette.RecordsArrow, Colors.Transparent, 0f);
		_arrowInactive = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 500, Px(22f), 0f), Px(22f), Palette.MenuDisabled, Colors.Transparent, 0f);

		BuildLeft(band);
		BuildCenter(band);
		BuildRight(band);
	}

	private void BuildLeft(MenuBand band)
	{
		// RECORDS y el selector de circuito
		_viewLeft = band.AddColumn("Circuit", LeftX, 16f);
		_viewLeft.AddChild(MakeLabel("RECORDS", MenuFonts.Orbitron, 900, TitleFontSize, 0.08f, Palette.MenuTitle, Palette.PauseTitleGlow, 16f));

		var selector = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		selector.AddThemeConstantOverride("separation", Px(6f));
		_viewLeft.AddChild(selector);

		var circuitRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		circuitRow.AddThemeConstantOverride("separation", Px(14f));
		selector.AddChild(circuitRow);

		_arrowLeft = new Label { Text = "‹", MouseFilter = Control.MouseFilterEnum.Ignore, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		_arrowLeft.GuiInput += e => OnArrowInput(e, -1);
		_circuitName = MakeLabel("", MenuFonts.Orbitron, 700, 20f, 0.1f, Palette.BlueGray);
		_circuitName.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		_arrowRight = new Label { Text = "›", MouseFilter = Control.MouseFilterEnum.Ignore, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		_arrowRight.GuiInput += e => OnArrowInput(e, 1);
		circuitRow.AddChild(_arrowLeft);
		circuitRow.AddChild(_circuitName);
		circuitRow.AddChild(_arrowRight);

		_circuitCount = MakeLabel("1 / 1", MenuFonts.Mono, 400, 13f, 0.1f, Palette.BlueGray);
		selector.AddChild(_circuitCount);

		// Confirmación: título naranja en dos líneas y el aviso
		_confirmLeft = band.AddColumn("Confirm", LeftX, 16f);
		_confirmLeft.Visible = false;
		_confirmLeft.AddChild(MakeLabel(ConfirmTitle, MenuFonts.Orbitron, 900, ConfirmTitleFontSize, 0.08f, Palette.ConfirmTitle, Palette.ConfirmTitleGlow, 14f));
		_confirmLeft.AddChild(MakeLabel(ConfirmHint, MenuFonts.Mono, 400, 15f, 0.18f, Palette.PauseHint));
	}

	private void BuildCenter(MenuBand band)
	{
		VBoxContainer column = band.AddColumn("Records", CenterX, 16f);

		VBoxContainer timeTrial = Block(column, "TIME TRIAL");
		_timeTrialLap = AddRow(timeTrial, "BEST LAP", 24f);

		VBoxContainer race = Block(column, "RACE");
		_raceTotal = AddRow(race, "TOTAL TIME", 20f);
		_raceLap = AddRow(race, "BEST LAP", 20f);
	}

	private void BuildRight(MenuBand band)
	{
		VBoxContainer column = band.AddColumn("Options", RightX, 0f);

		// El tamaño se ajusta con la lista más larga (BACK / RESET RECORDS)
		var sizing = new MenuModel(new[] { new MenuOption("BACK", "BACK", true), new MenuOption("RESET", "RESET RECORDS", true) });
		int fontSize = OptionList.FitFontSize(sizing, OptionFontSize, _scale, S(1280f - RightX - RightMargin));
		if (fontSize < OptionFontSize)
			GD.Print($"RecordsScreen: opciones reducidas a {fontSize} px para que quepan en pantalla.");

		OptionListStyle style = OptionListStyle.Scaled(_scale, fontSize, 44f, 4f, new Vector2(4f, 30f), 16f);
		style.HideDisabledHint = true;

		_options = new OptionList { Name = "OptionList" };
		_options.Setup(style);
		_options.Chosen += id => Apply(_logic.Choose(id));
		column.AddChild(_options);
	}

	// Bloque con cabecera (TIME TRIAL / RACE) y sus filas
	private VBoxContainer Block(VBoxContainer column, string header)
	{
		var block = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		block.AddThemeConstantOverride("separation", Px(6f));
		block.AddChild(MakeLabel(header, MenuFonts.Mono, 400, 15f, 0.22f, Palette.RecordsHeader));
		column.AddChild(block);
		return block;
	}

	// Fila: etiqueta | tiempo | fecha, con anchos fijos para que las columnas se alineen entre filas
	private RecordRow AddRow(VBoxContainer block, string label, float timeSize)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 0);
		block.AddChild(row);

		Label name = MakeLabel(label, MenuFonts.Mono, 400, 13f, 0.22f, Palette.BlueGray);
		name.CustomMinimumSize = new Vector2(S(LabelWidth), 0f);
		row.AddChild(name);

		Label time = MakeLabel(RecordFormat.NoTime, MenuFonts.Orbitron, 500, timeSize, 0.06f, Palette.Cyan);
		time.CustomMinimumSize = new Vector2(S(TimeWidth), 0f);
		row.AddChild(time);

		Label date = MakeLabel(RecordFormat.NoRecord, MenuFonts.Mono, 400, 14f, 0.1f, Palette.BlueGray);
		row.AddChild(date);

		return new RecordRow { Time = time, Date = date, TimeSettings = time.LabelSettings };
	}

	private void OnArrowInput(InputEvent e, int step)
	{
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			ChangeCircuit(step);
	}

	// Texto con su fuente (peso y espaciado en em), tamaño (px sobre 1280×720), color y halo opcional
	private Label MakeLabel(string text, FontFile file, int weight, float size, float letterSpacingEm,
		Color color, Color? glow = null, float glowSize = 0f)
	{
		int px = Px(size);
		return new Label
		{
			Text = text,
			SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(file, weight, px, letterSpacingEm), px,
				color, glow ?? Colors.Transparent, S(glowSize))
		};
	}
}
