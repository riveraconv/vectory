using System.Collections.Generic;
using System.Globalization;
using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.Menu;
using Vectory.Rivals;
using Vectory.Ship;

namespace Vectory.UI;

// Panel de ajuste para pruebas (spec docs/naves-2-tecnico.md, 4). Main solo lo crea en compilaciones de depuración.
// Dibuja el panel, traduce teclas y deslizadores y pasa el resultado al Player; las reglas están en TuningLab, que
// vive en un campo estático para sobrevivir a RESTART y dura hasta cerrar el juego. Al entrar a una carrera con una nave
// distinta de la elegida la última vez, pasa a ella; RESTART no la cambia (spec docs/naves-4-integracion.md, 4.1).
// F9 panel · F10 cambiar de nave · F11 volcar a la consola · F12 valores de arranque.
// En RACE, bloque RIVALS (spec docs/rivales-1-parrilla.md, 4.5): una fila por rival y el ritmo de todos (85–100 %), que
// se aplica al momento y dura hasta cerrar el juego, sin guardarse en disco.
// Sigue funcionando con la pausa (por encima de ella), para poder ajustar con la carrera parada.
public partial class TuningLabPanel : CanvasLayer
{
	private const int PanelLayer = 11;    // por encima de la pausa (10)
	private const float PanelX = 40f;     // medidas sobre 1280×720
	private const float PanelY = 110f;
	private const float SliderWidth = 170f;
	private const float PercentWidth = 56f;
	private const float ValueWidth = 136f;
	private const float LabelWidth = 112f;

	private static TuningLab _lab;
	private static string _lastChosenId;
	// Pintura del casco 3D (F5 / Mayúsculas + F5): dura hasta cerrar el juego, también con RESTART; no se guarda
	// Pintura elegida con F5 en cada estilo de casco 3D (combinación de su catálogo); dura hasta cerrar el juego
	private static readonly Dictionary<HullStyle, int> _paintIndices = new();

	// La nave que se conduce de verdad, para guardarla con los récords y el fantasma
	public static string DrivenShipId(string chosenId) => _lab?.DrivenShipId(chosenId) ?? chosenId;

	// La nave a la que se aplican los cambios
	public Player Player { get; set; }

	// Los rivales de la carrera (null en TIME TRIAL: sin bloque RIVALS)
	public RivalField Rivals { get; set; }

	private const int MinRivalPace = -15;   // puntos sobre los márgenes de cada piloto
	private const int MaxRivalPace = 5;
	private const float RivalsRefreshTime = 0.25f;   // s entre repasos de las filas
	private static float? _rivalPace;   // el del deslizador, si se ha movido
	private static bool _contactEnabled = true;   // CONTACT: dura hasta cerrar el juego, no se guarda
	private static bool _errorsEnabled = true;    // ERRORS (spec docs/rivales-4-personalidades.md, 8): ídem
	private CheckBox _contactCheck;
	private Button _difficultyButton;
	private CheckBox _statesCheck;
	private Row _rivalPaceRow;
	private Label _rivalsLabel;
	private float _rivalsRefresh;

	private const float SecondSpeedKmh = 300f;   // la segunda velocidad de la fila ACCEL CURVE

	private sealed record Row(Label Name, HSlider Slider, Label Percent, Label Value);

	private readonly List<Row> _rows = new();   // mismo orden que ShipMultipliers.Parameters
	private Row _curveRow;
	private Row _minThrustRow;
	private PanelContainer _root;
	private Label _shipLabel;
	private CheckBox _linearCheck;
	private Label _testShipsLabel;
	private Label _paintLabel;
	private LabelSettings _percentSettings;
	private LabelSettings _percentOverSettings;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = PanelLayer;

		_lab ??= new TuningLab(ShipDefinitions.All, GameSession.ShipDesignId);
		if (GameSession.ShipDesignId != _lastChosenId)
		{
			_lab.MoveTo(GameSession.ShipDesignId);
			_lastChosenId = GameSession.ShipDesignId;
		}

		if (_rivalPace is float pace)
			Rivals?.SetPace(pace);
		if (Rivals != null)
			Rivals.Contacts.Enabled = _contactEnabled;
		Rivals?.SetErrorsEnabled(_errorsEnabled);

		Build();
		_root.Visible = false;
		Refresh();
		RefreshRivals();

		// Con el panel ya usado, la nave sigue con lo que hubiera en él (también tras RESTART)
		if (_lab.Touched)
			ApplyToPlayer();
		foreach (KeyValuePair<HullStyle, int> paint in _paintIndices)
			Player?.SetHullPaint(paint.Key, paint.Value);
	}

	// Estilo del casco 3D de la nave que se conduce; null si no tiene (HAMMER)
	private HullStyle? CurrentHullStyle => _lab.Current.Visual.Hull3D?.Style;

	// La de F5 si se ha usado con ese estilo; si no, la que lleva la nave (la guardada)
	private int PaintIndex(HullStyle style) =>
		_paintIndices.TryGetValue(style, out int index) ? index : Player?.HullPaint(style) ?? HullPaints.DefaultIndex(style);

	// Recorre las 40 combinaciones de pintura del casco 3D de la nave que se conduce (specs docs/cascos-3d-1-vector.md
	// y docs/cascos-3d-2-blade.md, 6.2). Sin casco 3D no hace nada.
	private void StepPaint(int delta)
	{
		if (CurrentHullStyle is not HullStyle style)
			return;
		int index = HullPaints.Step(style, PaintIndex(style), delta);
		_paintIndices[style] = index;
		Player?.SetHullPaint(style, index);
		Refresh();
		GD.Print($"[TuningLab] Paint: {HullPaints.DisplayName(style, index)} ({index + 1}/{HullPaints.Count(style)})");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("debug_tuning_toggle"))
		{
			_root.Visible = !_root.Visible;
		}
		else if (@event.IsActionPressed("debug_tuning_cycle"))
		{
			_lab.Cycle();
			ApplyToPlayer();
			Refresh();
			GD.Print($"[TuningLab] Ship: {ShipCatalog.Find(_lab.Current.Id).Name}");
		}
		// Mayúsculas + F5 antes que F5: las dos con las teclas exactas
		else if (@event.IsActionPressed("debug_hull_paint_previous", false, true))
		{
			StepPaint(-1);
		}
		else if (@event.IsActionPressed("debug_hull_paint_next", false, true))
		{
			StepPaint(1);
		}
		else if (@event.IsActionPressed("debug_tuning_dump"))
		{
			GD.Print(_lab.DumpText());
		}
		else if (@event.IsActionPressed("debug_tuning_reset"))
		{
			_lab.Reset(_lab.Current.Id);
			if (_lab.Touched)
				ApplyToPlayer();
			Refresh();
			GD.Print($"[TuningLab] Reset: {ShipCatalog.Find(_lab.Current.Id).Name}");
		}
		else
		{
			return;
		}
		GetViewport().SetInputAsHandled();
	}

	// Filas de los rivales, unas veces por segundo con el panel abierto (también con la pausa: no cambian)
	public override void _Process(double delta)
	{
		if (Rivals == null || !_root.Visible)
			return;
		_rivalsRefresh -= (float)delta;
		if (_rivalsRefresh > 0f)
			return;
		_rivalsRefresh = RivalsRefreshTime;
		RefreshRivals();
	}

	private void OnRivalPaceChanged(double value)
	{
		float pace = Mathf.RoundToInt(value) / 100f;
		_rivalPace = pace;
		Rivals?.SetPace(pace);
		RefreshRivals();
		GD.Print($"[TuningLab] Rival pace offset: {Signed(Mathf.RoundToInt(value))} points");
	}

	private static string StateName(Autopilot.LineState state) => state switch
	{
		Autopilot.LineState.PassLeft => "PASS L",
		Autopilot.LineState.PassRight => "PASS R",
		Autopilot.LineState.Follow => "FOLLOW",
		Autopilot.LineState.Slip => "SLIP",
		Autopilot.LineState.AttackLeft => "ATTACK L",
		Autopilot.LineState.AttackRight => "ATTACK R",
		Autopilot.LineState.DefendLeft => "DEFEND L",
		Autopilot.LineState.DefendRight => "DEFEND R",
		_ => "CLEAR"
	};

	private static string Signed(int points) => points > 0 ? "+" + points : points < 0 ? "−" + (-points) : "±0";

	// Choques entre naves: sin ellos se vuelven a atravesar, para comparar
	private void OnContactToggled(bool enabled)
	{
		_contactEnabled = enabled;
		if (Rivals != null)
			Rivals.Contacts.Enabled = enabled;
		GD.Print($"[TuningLab] Ship contact: {(enabled ? "ON" : "OFF")}");
	}

	// La dificultad de la carrera siguiente: la de esta no cambia
	private void OnDifficultyPressed()
	{
		GameSession.Difficulty = DifficultyRules.Next(GameSession.Difficulty);
		RefreshDifficulty();
		GD.Print($"[TuningLab] Difficulty for the next race: {DifficultyRules.ScreenName(GameSession.Difficulty)}");
	}

	private void RefreshDifficulty()
	{
		if (_difficultyButton == null || Rivals == null)
			return;
		string now = DifficultyRules.ScreenName(Rivals.Difficulty);
		string next = DifficultyRules.ScreenName(GameSession.Difficulty);
		_difficultyButton.Text = Rivals.Difficulty == GameSession.Difficulty
			? $"DIFFICULTY {now} · CLICK TO CHANGE"
			: $"DIFFICULTY {now} · NEXT RACE {next} (RESTART)";
	}

	// Errores de los rivales: sin ellos, para probar
	private void OnErrorsToggled(bool enabled)
	{
		_errorsEnabled = enabled;
		Rivals?.SetErrorsEnabled(enabled);
		GD.Print($"[TuningLab] Rival errors: {(enabled ? "ON" : "OFF")}");
	}

	private void RefreshRivals()
	{
		if (Rivals == null)
			return;
		int pace = Mathf.RoundToInt(Rivals.PaceOffset * 100f);
		_rivalPaceRow.Slider.SetValueNoSignal(pace);
		_rivalPaceRow.Percent.Text = Signed(pace) + " PT";

		var text = new System.Text.StringBuilder();
		foreach (RivalShip ship in Rivals.Ships)
		{
			RivalCar car = ship.Car;
			HullStyle style = HullPaints.StyleOf(car.Ship.Id) ?? HullStyle.Racing;
			string lap = car.Tracker.State == Circuit.RaceState.Finished ? "FIN"
				: $"{car.Tracker.CurrentLap}/{car.Tracker.TotalLaps}";
			if (text.Length > 0)
				text.Append('\n');
			RivalDriver driver = car.Driver;
			text.Append($"{car.Entry.Pilot.Nickname,-8}{PilotProfiles.ScreenName(car.Entry.Pilot.Profile),-13}{ShipCatalog.Find(car.Ship.Id)?.Name ?? car.Ship.Id,-7}"
				+ $"{HullPaints.DisplayName(style, car.Entry.PaintIndex),-30}"
				+ $"LAP {lap,-4} DMG {car.Damage.Level} {car.Damage.Points,2}P  HIT {car.Contacts,-3} RSP {car.Respawns,-2} "
				+ $"SHIP {car.Rams}/{car.Received}/{car.Mutual}  "
				+ string.Create(CultureInfo.InvariantCulture,
					$"STR {driver.StraightMargin * 100f:0.0} CRV {driver.CurveMargin * 100f:0.0} BRK {driver.Traits.Braking:0.00} REA {driver.ReactionTime:0.00}  "
					+ $"{StateName(driver.ClearLine.State),-8} R {car.Slipstream.Strength:0.00} BST {car.Boost.Charges} {PilotProfiles.ScreenName(driver.Traits.Boost),-8} USED {driver.Boosts} ABT {driver.ClearLine.Aborts}  "
					+ $"ERR LB {driver.Errors.LateBrakings} WX {driver.Errors.WideExits} BS {driver.Errors.BadStarts}"));
		}
		text.Append($"\nSHIP = RAMS / RECEIVED / MUTUAL · RIVAL VS RIVAL {Rivals.RivalHits}");
		text.Append(string.Create(CultureInfo.InvariantCulture, $"\nYOUR SLIPSTREAM R {Player?.Slipstream.Strength ?? 0f:0.00}"));
		Contact.GrazeLossMeter graze = Rivals.PlayerGraze;
		string kind = graze.Kind switch { Contact.GrazeKind.Side => "SIDE", Contact.GrazeKind.Rear => "REAR", _ => "—" };
		text.Append($"\nYOUR GRAZE {kind,-5} LOST {graze.Total * 3.6f:0.0} KM/H IN {graze.Window:0} S");
		_rivalsLabel.Text = text.ToString();
	}

	// Cuántas naves de prueba hay (F6): las lleva TestShips, aquí solo se enseña
	public void SetTestShipCount(int count)
	{
		_testShipsLabel.Text = $"TEST SHIPS {count} · F6";
	}

	private void OnSliderChanged(ShipParameter parameter, double value)
	{
		if (_lab.SetPercent(_lab.Current.Id, parameter, Mathf.RoundToInt(value)))
			ApplyToPlayer();
		Refresh();
	}

	private void OnCurveChanged(double value)
	{
		if (_lab.SetAccelCurve(_lab.Current.Id, (float)value))
			ApplyToPlayer();
		Refresh();
	}

	private void OnMinThrustChanged(double value)
	{
		if (_lab.SetMinThrustPercent(Mathf.RoundToInt(value)))
			ApplyToPlayer();
		Refresh();
	}

	private void OnLinearToggled(bool linear)
	{
		if (_lab.SetLinearAcceleration(linear))
			ApplyToPlayer();
		Refresh();
	}

	private void ApplyToPlayer()
	{
		string id = _lab.Current.Id;
		Player?.ApplyTuningLab(_lab.Tuning(id), _lab.Current.Visual);
	}

	private void Refresh()
	{
		string id = _lab.Current.Id;
		string halfWidth = _lab.Current.Visual.CollisionHalfWidth.ToString("0.00", CultureInfo.InvariantCulture);
		_shipLabel.Text = $"{ShipCatalog.Find(id).Name} · COLLISION {halfWidth} M";
		_paintLabel.Text = CurrentHullStyle is HullStyle style
			? $"PAINT {HullPaints.DisplayName(style, PaintIndex(style))} · {PaintIndex(style) + 1}/{HullPaints.Count(style)} · F5"
			: "PAINT — · F5";
		_linearCheck.SetPressedNoSignal(_lab.LinearAcceleration);

		// Mismo orden que ShipMultipliers.Parameters. La referencia es Vector tal como está en el panel.
		ShipTuning tuning = _lab.Tuning(id);
		IReadOnlyList<ShipStat> stats = ShipStats.Compute(tuning, _lab.Tuning(ShipDefinitions.Default.Id));
		bool editable = TuningLab.IsEditable(id);
		for (int i = 0; i < _rows.Count; i++)
		{
			Row row = _rows[i];
			ShipParameter parameter = ShipMultipliers.Parameters[i];
			int percent = _lab.Percent(id, parameter);
			row.Name.Text = stats[i].Label;
			row.Slider.SetValueNoSignal(percent);
			row.Slider.Editable = editable;
			row.Percent.Text = TuningLab.PercentText(percent);
			row.Percent.LabelSettings = _lab.IsOverLimit(id, parameter) ? _percentOverSettings : _percentSettings;
			row.Value.Text = stats[i].Value.Length > 0 ? $"{stats[i].Value} · {stats[i].Segments}" : $"· {stats[i].Segments}";
		}

		float curve = _lab.AccelCurve(id);
		_curveRow.Slider.SetValueNoSignal(curve);
		_curveRow.Percent.Text = curve.ToString("0.0", CultureInfo.InvariantCulture);
		_curveRow.Percent.LabelSettings = _lab.IsCurveOutOfRange(id) ? _percentOverSettings : _percentSettings;
		_curveRow.Value.Text = ShipSelectFormat.AccelerationTimes(
			ShipStats.SecondsTo(tuning, ShipSelectFormat.ReferenceSpeedKmh),
			ShipStats.SecondsTo(tuning, SecondSpeedKmh));

		_minThrustRow.Slider.SetValueNoSignal(_lab.MinThrustPercent);
		_minThrustRow.Percent.Text = _lab.MinThrustPercent.ToString(CultureInfo.InvariantCulture) + " %";
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private void Build()
	{
		float scale = UiScale.Factor;
		int fontSize = Mathf.RoundToInt(13f * scale);
		Font font = MenuFonts.Make(MenuFonts.Mono, 400, fontSize, 0f);

		LabelSettings Text(Color color) => MenuFonts.Settings(font, fontSize, color, Colors.Transparent, 0f);

		LabelSettings header = Text(Palette.SelectHeader);
		LabelSettings label = Text(Palette.SelectLabel);
		LabelSettings value = Text(Palette.SelectValue);
		_percentSettings = value;
		_percentOverSettings = Text(Palette.Orange);

		var style = new StyleBoxFlat
		{
			BgColor = Palette.SelectPanel,
			BorderColor = Palette.SelectPanelLine,
			BorderWidthLeft = Mathf.RoundToInt(2f * scale)
		};
		style.SetContentMarginAll(12f * scale);

		_root = new PanelContainer
		{
			Name = "TuningLabRoot",
			Position = new Vector2(PanelX, PanelY) * scale,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_root.AddThemeStyleboxOverride("panel", style);
		AddChild(_root);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", Mathf.RoundToInt(6f * scale));
		_root.AddChild(column);

		column.AddChild(new Label { Text = "TUNING LAB", LabelSettings = header });
		_shipLabel = new Label { LabelSettings = value };
		column.AddChild(_shipLabel);

		var grid = new GridContainer { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(8f * scale));
		grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(4f * scale));
		column.AddChild(grid);

		foreach (ShipParameter parameter in ShipMultipliers.Parameters)
			_rows.Add(BuildRow(grid, TuningLab.MinPercent, TuningLab.MaxPercent, 1, changed => OnSliderChanged(parameter, changed), label, value, scale));

		_curveRow = BuildRow(grid, TuningLab.MinCurve, TuningLab.MaxCurve, TuningLab.CurveStep, OnCurveChanged, label, value, scale);
		_curveRow.Name.Text = "ACCEL CURVE";
		_minThrustRow = BuildRow(grid, TuningLab.MinThrustMinPercent, TuningLab.MinThrustMaxPercent, 1, OnMinThrustChanged, label, value, scale);
		_minThrustRow.Name.Text = "MIN THRUST";

		_linearCheck = new CheckBox
		{
			Text = "LINEAR ACCEL (OLD)",
			FocusMode = Control.FocusModeEnum.None   // las flechas y el Espacio son para conducir
		};
		_linearCheck.AddThemeFontOverride("font", font);
		_linearCheck.AddThemeFontSizeOverride("font_size", fontSize);
		_linearCheck.AddThemeColorOverride("font_color", Palette.SelectLabel);
		_linearCheck.AddThemeColorOverride("font_hover_color", Palette.SelectValue);
		_linearCheck.AddThemeColorOverride("font_pressed_color", Palette.SelectValue);
		_linearCheck.AddThemeColorOverride("font_hover_pressed_color", Palette.SelectValue);
		_linearCheck.Toggled += OnLinearToggled;
		column.AddChild(_linearCheck);

		_testShipsLabel = new Label { LabelSettings = value };
		column.AddChild(_testShipsLabel);
		_paintLabel = new Label { LabelSettings = value };
		column.AddChild(_paintLabel);

		if (Rivals != null)
		{
			column.AddChild(new Label { Text = "RIVALS", LabelSettings = header });
			var rivalGrid = new GridContainer { Columns = 4 };
			rivalGrid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(8f * scale));
			column.AddChild(rivalGrid);
			_rivalPaceRow = BuildRow(rivalGrid, MinRivalPace, MaxRivalPace, 1, OnRivalPaceChanged, label, value, scale);
			_rivalPaceRow.Name.Text = "PACE";
			_contactCheck = new CheckBox
			{
				Text = "CONTACT",
				ButtonPressed = _contactEnabled,
				FocusMode = Control.FocusModeEnum.None
			};
			_contactCheck.AddThemeFontOverride("font", font);
			_contactCheck.AddThemeFontSizeOverride("font_size", fontSize);
			_contactCheck.AddThemeColorOverride("font_color", Palette.SelectLabel);
			_contactCheck.AddThemeColorOverride("font_hover_color", Palette.SelectValue);
			_contactCheck.AddThemeColorOverride("font_pressed_color", Palette.SelectValue);
			_contactCheck.AddThemeColorOverride("font_hover_pressed_color", Palette.SelectValue);
			_contactCheck.Toggled += OnContactToggled;
			column.AddChild(_contactCheck);
			var errorsCheck = new CheckBox
			{
				Text = "ERRORS",
				ButtonPressed = _errorsEnabled,
				FocusMode = Control.FocusModeEnum.None
			};
			errorsCheck.AddThemeFontOverride("font", font);
			errorsCheck.AddThemeFontSizeOverride("font_size", fontSize);
			errorsCheck.AddThemeColorOverride("font_color", Palette.SelectLabel);
			errorsCheck.AddThemeColorOverride("font_hover_color", Palette.SelectValue);
			errorsCheck.AddThemeColorOverride("font_pressed_color", Palette.SelectValue);
			errorsCheck.AddThemeColorOverride("font_hover_pressed_color", Palette.SelectValue);
			errorsCheck.Toggled += OnErrorsToggled;
			column.AddChild(errorsCheck);
			// DIFFICULTY (spec docs/rivales-4-personalidades.md, 6.2): vale para la carrera siguiente (RESTART)
			_difficultyButton = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
			_difficultyButton.AddThemeFontOverride("font", font);
			_difficultyButton.AddThemeFontSizeOverride("font_size", fontSize);
			_difficultyButton.AddThemeColorOverride("font_color", Palette.SelectLabel);
			_difficultyButton.AddThemeColorOverride("font_hover_color", Palette.SelectValue);
			_difficultyButton.AddThemeColorOverride("font_pressed_color", Palette.SelectValue);
			_difficultyButton.Pressed += OnDifficultyPressed;
			column.AddChild(_difficultyButton);
			RefreshDifficulty();
			_statesCheck = new CheckBox
			{
				Text = "LINE STATES",
				ButtonPressed = RivalShip.ShowLineStates,
				FocusMode = Control.FocusModeEnum.None
			};
			_statesCheck.AddThemeFontOverride("font", font);
			_statesCheck.AddThemeFontSizeOverride("font_size", fontSize);
			_statesCheck.AddThemeColorOverride("font_color", Palette.SelectLabel);
			_statesCheck.AddThemeColorOverride("font_hover_color", Palette.SelectValue);
			_statesCheck.AddThemeColorOverride("font_pressed_color", Palette.SelectValue);
			_statesCheck.AddThemeColorOverride("font_hover_pressed_color", Palette.SelectValue);
			_statesCheck.Toggled += enabled => RivalShip.ShowLineStates = enabled;
			column.AddChild(_statesCheck);
			_rivalsLabel = new Label { LabelSettings = value };
			column.AddChild(_rivalsLabel);
		}

		column.AddChild(new Label { Text = "F9 PANEL · F10 SHIP · F11 DUMP · F12 RESET", LabelSettings = label });
	}

	private static Row BuildRow(GridContainer grid, double min, double max, double step, System.Action<double> changed, LabelSettings label, LabelSettings value, float scale)
	{
		var name = new Label { LabelSettings = label, CustomMinimumSize = new Vector2(LabelWidth * scale, 0f) };
		grid.AddChild(name);

		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Scrollable = false,
			FocusMode = Control.FocusModeEnum.None,   // las flechas son para conducir
			CustomMinimumSize = new Vector2(SliderWidth * scale, 0f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		slider.ValueChanged += newValue => changed(newValue);
		grid.AddChild(slider);

		var percent = new Label
		{
			LabelSettings = value,
			CustomMinimumSize = new Vector2(PercentWidth * scale, 0f),
			HorizontalAlignment = HorizontalAlignment.Right
		};
		grid.AddChild(percent);

		var data = new Label { LabelSettings = value, CustomMinimumSize = new Vector2(ValueWidth * scale, 0f) };
		grid.AddChild(data);

		return new Row(name, slider, percent, data);
	}
}
