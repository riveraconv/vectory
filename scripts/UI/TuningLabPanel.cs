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
// vive en un campo estático para sobrevivir a RESTART y dura hasta cerrar el juego.
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

	// Se ha usado el laboratorio en esta ejecución: no se guardan récords ni fantasma
	public static bool Used => _lab is { Touched: true };

	// La nave a la que se aplican los cambios
	public Player Player { get; set; }

	private sealed record Row(ShipParameter Parameter, Label Name, HSlider Slider, Label Percent, Label Value);

	private readonly List<Row> _rows = new();
	private PanelContainer _root;
	private Label _shipLabel;
	private LabelSettings _percentSettings;
	private LabelSettings _percentOverSettings;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = PanelLayer;

		_lab ??= new TuningLab(ShipDefinitions.All, GameSession.ShipDesignId);

		Build();
		_root.Visible = false;
		Refresh();

		// Tras RESTART, la nave sigue con lo que hubiera en el panel
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

	private void ApplyToPlayer()
	{
		string id = _lab.Current.Id;
		Player?.ApplyTuningLab(_lab.Tuning(id), _lab.Current.Visual.CollisionHalfWidth);
	}

	private void Refresh()
	{
		string id = _lab.Current.Id;
		string halfWidth = _lab.Current.Visual.CollisionHalfWidth.ToString("0.0", CultureInfo.InvariantCulture);
		_shipLabel.Text = $"{ShipCatalog.Find(id).Name} · COLLISION {halfWidth} M";

		// Mismo orden que ShipMultipliers.Parameters
		IReadOnlyList<ShipStat> stats = ShipStats.Compute(_lab.Tuning(id), ShipDefinitions.Default.Technical);
		bool editable = TuningLab.IsEditable(id);
		for (int i = 0; i < _rows.Count; i++)
		{
			Row row = _rows[i];
			int percent = _lab.Percent(id, row.Parameter);
			row.Name.Text = stats[i].Label;
			row.Slider.SetValueNoSignal(percent);
			row.Slider.Editable = editable;
			row.Percent.Text = TuningLab.PercentText(percent);
			row.Percent.LabelSettings = _lab.IsOverLimit(id, row.Parameter) ? _percentOverSettings : _percentSettings;
			row.Value.Text = stats[i].Value.Length > 0 ? $"{stats[i].Value} · {stats[i].Segments}" : $"· {stats[i].Segments}";
		}
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
			_rows.Add(BuildRow(grid, parameter, label, value, scale));

		column.AddChild(new Label { Text = "F9 PANEL · F10 SHIP · F11 DUMP · F12 RESET", LabelSettings = label });
	}

	private Row BuildRow(GridContainer grid, ShipParameter parameter, LabelSettings label, LabelSettings value, float scale)
	{
		var name = new Label { LabelSettings = label, CustomMinimumSize = new Vector2(LabelWidth * scale, 0f) };
		grid.AddChild(name);

		var slider = new HSlider
		{
			MinValue = TuningLab.MinPercent,
			MaxValue = TuningLab.MaxPercent,
			Step = 1,
			Scrollable = false,
			FocusMode = Control.FocusModeEnum.None,   // las flechas son para conducir
			CustomMinimumSize = new Vector2(SliderWidth * scale, 0f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		slider.ValueChanged += changed => OnSliderChanged(parameter, changed);
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

		return new Row(parameter, name, slider, percent, data);
	}
}
