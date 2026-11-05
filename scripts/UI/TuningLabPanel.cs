using System.Collections.Generic;
using System.Globalization;
using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.Menu;
using Vectory.Ship;

namespace Vectory.UI;

// Panel de ajuste para pruebas (spec docs/naves-2-tecnico.md, 4). Main solo lo crea en compilaciones de depuración.
// Dibuja el panel, traduce teclas y deslizadores y pasa el resultado al Player; las reglas están en TuningLab, que
// vive en un campo estático para sobrevivir a RESTART y dura hasta cerrar el juego. Al entrar a una carrera con una nave
// distinta de la elegida la última vez, pasa a ella; RESTART no la cambia (spec docs/naves-4-integracion.md, 4.1).
// F9 panel · F10 cambiar de nave · F11 volcar a la consola · F12 valores de arranque.
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

	// La nave que se conduce de verdad, para guardarla con los récords y el fantasma
	public static string DrivenShipId(string chosenId) => _lab?.DrivenShipId(chosenId) ?? chosenId;

	// La nave a la que se aplican los cambios
	public Player Player { get; set; }

	private const float SecondSpeedKmh = 300f;   // la segunda velocidad de la fila ACCEL CURVE

	private sealed record Row(Label Name, HSlider Slider, Label Percent, Label Value);

	private readonly List<Row> _rows = new();   // mismo orden que ShipMultipliers.Parameters
	private Row _curveRow;
	private Row _minThrustRow;
	private PanelContainer _root;
	private Label _shipLabel;
	private CheckBox _linearCheck;
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

		Build();
		_root.Visible = false;
		Refresh();

		// Con el panel ya usado, la nave sigue con lo que hubiera en él (también tras RESTART)
		if (_lab.Touched)
			ApplyToPlayer();
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
		string halfWidth = _lab.Current.Visual.CollisionHalfWidth.ToString("0.0", CultureInfo.InvariantCulture);
		_shipLabel.Text = $"{ShipCatalog.Find(id).Name} · COLLISION {halfWidth} M";
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
