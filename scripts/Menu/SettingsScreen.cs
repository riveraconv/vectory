using System;
using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.Settings;
using Vectory.UI;

namespace Vectory.Menu;

// Pantalla SETTINGS (spec docs/settings.md, 3, 5 y 7; designs/settings.html, composición C · BRIEFING), dentro de la
// escena del menú principal: a la izquierda, el ajuste elegido en grande con su grupo, su valor, su explicación y su
// vista previa; a la derecha, el panel con la lista de todos los ajustes y RESET DEFAULTS / BACK. Cada cambio se aplica
// al momento (SettingsService) y se guarda al salir y al confirmar RESET DEFAULTS. Las reglas están en
// SettingsScreenLogic y SettingsModel; este nodo traduce teclado y ratón y lo dibuja. Al cerrarse lanza Closed.
// Medidas sobre 1280×720 × UiScale.Factor.
public partial class SettingsScreen : CanvasLayer
{
	private const float LeftX = 72f;
	private const float PanelHeaderY = 40f;
	private const float ListX = 14f;
	private const float ListY = 62f;
	private const float ListWidth = 416f;
	private const float ActionsX = 32f;
	private const float ActionsY = 628f;
	private const float ToastTime = 1.6f;

	[ExportGroup("Aspecto (px sobre 1280×720)")]
	[Export] public float RowHeight = 32f;
	[Export] public float ListHeight = 540f;
	[Export] public float ScrollMargin = 40f;
	[Export] public int NameFontSize = 50;
	[Export] public int ActionFontSize = 16;
	[Export] public int ConfirmTitleFontSize = 32;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ConfirmDimOpacity = 0.5f;
	[Export] public float DisplayCountdown = 10f;   // s de KEEP THIS DISPLAY? antes de volver a lo de antes
	[Export] public float DisplayDelay = 0.5f;      // s tras el último cambio de modo o resolución antes de preguntar

	// Fondo del menú principal: se ve la rejilla, sin la nave
	public MenuBackground Background { get; set; }

	// Miga de arriba: MAIN MENU · SETTINGS (PAUSE · SETTINGS desde la pausa, etapa 4)
	public string Crumb { get; set; } = "MAIN MENU · SETTINGS";

	// Se lanza al salir (ya guardado)
	public event Action Closed;

	public bool IsOpen => _root != null && _root.Visible;

	private SettingsScreenLogic _logic;
	private SelectScreenKit _kit;
	private float _scale = 1f;
	private ulong _openedFrame;
	private float _toastLeft;

	private Control _root;
	private Label _name;
	private Label _group;
	private Label _value;
	private RichTextLabel _description;
	private SettingsPreviewView _preview;
	private SettingsListView _list;
	private OptionList _actions;
	private MenuModel _actionModel;
	private MenuBand _confirm;
	private OptionList _confirmOptions;
	private PanelContainer _toast;
	private Label _toastText;

	// Confirmación de pantalla (spec 4.3)
	private readonly DisplayConfirm _display = new();
	private MenuBand _displayBand;
	private Label _displayCountdown;
	private OptionList _displayOptions;
	private MenuModel _displayModel;
	private (int Mode, int Resolution) _lastDisplay;
	private bool _reverting;
	private SettingsModel _watched;

	public override void _Ready()
	{
		Layer = 3;   // por encima del menú principal (capa 2) y de la sombra del fondo (capa 1)
		_scale = UiScale.Factor;
		_kit = new SelectScreenKit(_scale);
		Build();
		_root.Visible = false;
	}

	// Abre la pantalla con la primera fila elegida
	public void Open()
	{
		SettingsService.EnsureLoaded();
		_logic = new SettingsScreenLogic(SettingsService.Current);
		_logic.SelectFirst();
		Watch(_logic.Model);
		_display.Delay = DisplayDelay;
		_display.Countdown = DisplayCountdown;
		_display.Keep();
		_displayBand.Visible = false;
		_openedFrame = Engine.GetProcessFrames();   // el Enter que abre la pantalla no elige nada aquí
		_confirm.Visible = false;
		_toast.Visible = false;
		_root.Visible = true;
		Background?.SetShipVisible(false);
		UpdateView();
	}

	private void Close()
	{
		Watch(null);
		SettingsService.Save();
		_root.Visible = false;
		Background?.SetShipVisible(true);
		Closed?.Invoke();
	}

	public override void _Process(double delta)
	{
		if (!IsOpen)
			return;
		if (_toastLeft > 0f)
		{
			_toastLeft -= (float)delta;
			_toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastLeft / 0.25f, 0f, 1f));
			_toast.Visible = _toastLeft > 0f;
		}
		if (UpdateDisplayConfirm((float)delta))
			return;
		if (Engine.GetProcessFrames() == _openedFrame)
			return;

		// Con un cambio de pantalla esperando su pregunta, salir la saca ya (no se guarda sin confirmar)
		if (Input.IsActionJustPressed("menu_back") && _display.Pending)
			_display.ShowNow();
		else if (Input.IsActionJustPressed("menu_back"))
			Apply(_logic.Back());
		else if (Input.IsActionJustPressed("menu_up"))
			Apply(_logic.Up());
		else if (Input.IsActionJustPressed("menu_down"))
			Apply(_logic.Down());
		else if (Input.IsActionJustPressed("menu_left"))
			Apply(_logic.Horizontal(-1));
		else if (Input.IsActionJustPressed("menu_right"))
			Apply(_logic.Horizontal(1));
		else if (Input.IsActionJustPressed("menu_accept"))
			Apply(_logic.Accept());
	}

	private void Apply(SettingsAction action)
	{
		switch (action)
		{
			case SettingsAction.OpenConfirm:
				_confirmOptions.SetModel(_logic.ConfirmMenu);
				_confirm.Visible = true;
				break;
			case SettingsAction.CloseConfirm:
				_confirm.Visible = false;
				break;
			case SettingsAction.ResetDone:
				_confirm.Visible = false;
				SettingsService.Save();
				ShowToast("DEFAULTS RESTORED");
				break;
			case SettingsAction.Back:
				Close();
				return;
		}
		UpdateView();
	}

	private void UpdateView()
	{
		SettingDef def = _logic.Selected;
		SettingsModel model = _logic.Model;
		string id = _logic.SelectedId;
		bool action = def == null;

		_name.Text = action ? (id == SettingsScreenLogic.ResetId ? "RESET DEFAULTS" : "BACK") : def.Label;
		_group.Text = action ? "SETTINGS" : def.Group;
		_value.Text = action ? "" : "  ·  " + model.ValueText(id);
		_description.Text = Markup(action
			? id == SettingsScreenLogic.ResetId ? "Puts every setting back to its default value." : "Saves the settings and goes back to the main menu."
			: def.Description);
		_preview.Show(model, def);
		_list.Show(model, action ? null : id);

		_actions.ShowSelection = action && !_logic.Confirming;
		_actionModel.Select(id == SettingsScreenLogic.BackId ? 1 : 0);
		_actions.Refresh();
		if (_logic.Confirming)
			_confirmOptions.Refresh();
	}

	// [b]…[/b] de la explicación → el blanco azulado de lo resaltado
	private static string Markup(string text) =>
		text.Replace("[b]", $"[color=#{Palette.SettingsHighlight.ToHtml(false)}]").Replace("[/b]", "[/color]");

	private void ShowToast(string text)
	{
		_toastText.Text = text;
		_toast.Visible = true;
		_toast.Modulate = Colors.White;
		_toastLeft = ToastTime;
	}

	// ---------------------------------------------------------------
	// Ratón
	// ---------------------------------------------------------------

	private void OnRowHovered(string id)
	{
		if (Busy)
			return;
		_logic.Select(id);
		UpdateView();
	}

	private void OnClicked(string id, SettingsClick click, int value)
	{
		if (Busy)
			return;
		_logic.Select(id);
		SettingsModel model = _logic.Model;
		switch (click)
		{
			case SettingsClick.ArrowLeft:
				model.Change(id, -1);
				break;
			case SettingsClick.ArrowRight:
				model.Change(id, 1);
				break;
			case SettingsClick.Segment:
				model.Set(id, value);
				break;
			case SettingsClick.Toggle:
				model.Set(id, value);
				break;
		}
		UpdateView();
	}

	private void OnWheel(int direction)
	{
		if (Busy)
			return;
		Apply(direction < 0 ? _logic.Up() : _logic.Down());
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private float S(float px) => px * _scale;

	private void Build()
	{
		_root = new Control { Name = "SettingsRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		// Izquierda: miga, nombre, grupo · valor, explicación y vista previa
		Label crumb = _kit.MakeLabel(Crumb, MenuFonts.Mono, 400, 14f, 0.22f, Palette.Cyan);
		crumb.Position = new Vector2(S(LeftX), S(56f));
		_root.AddChild(crumb);
		_crumbLabel = crumb;

		_name = _kit.MakeLabel("", MenuFonts.Orbitron, 900, NameFontSize, 0.06f, Palette.SettingsName, Palette.SettingsNameGlow, 14f);
		_name.Position = new Vector2(S(LeftX), S(80f) - S(NameFontSize) * 0.12f);
		_root.AddChild(_name);

		var groupRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(S(74f), S(146f)) };
		groupRow.AddThemeConstantOverride("separation", 0);
		_group = _kit.MakeLabel("", MenuFonts.Mono, 400, 14f, 0.2f, Palette.SettingsGroupText);
		_value = _kit.MakeLabel("", MenuFonts.Mono, 400, 14f, 0.2f, Palette.SettingsValueText);
		groupRow.AddChild(_group);
		groupRow.AddChild(_value);
		_root.AddChild(groupRow);

		int descSize = Mathf.RoundToInt(S(14f));
		_description = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = new Vector2(S(74f), S(176f)),
			Size = new Vector2(S(640f), S(80f))
		};
		_description.AddThemeFontOverride("normal_font", MenuFonts.Make(MenuFonts.Mono, 400, descSize, 0.06f));
		_description.AddThemeFontSizeOverride("normal_font_size", descSize);
		_description.AddThemeColorOverride("default_color", Palette.SettingsGroupText);
		_description.AddThemeConstantOverride("line_separation", Mathf.RoundToInt(S(14f * 0.55f)));
		_root.AddChild(_description);

		_preview = new SettingsPreviewView { Name = "Preview", Position = new Vector2(S(LeftX), S(262f)), Size = new Vector2(S(680f), S(340f)) };
		_preview.Setup(_scale);
		_root.AddChild(_preview);

		// Derecha: panel con SETTINGS, la lista y RESET DEFAULTS / BACK
		Control panel = _kit.PanelFrame(_root, "Panel");
		Label header = _kit.SectionHeader("SETTINGS");
		header.Position = new Vector2(S(32f), S(PanelHeaderY));
		panel.AddChild(header);

		var clip = new Control
		{
			Name = "ListClip",
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = new Vector2(S(ListX), S(ListY)),
			Size = new Vector2(S(ListWidth), S(ListHeight))
		};
		panel.AddChild(clip);
		_list = new SettingsListView { Name = "List", RowHeight = RowHeight, ScrollMargin = ScrollMargin };
		_list.Setup(_scale);
		_list.Position = Vector2.Zero;
		_list.Size = clip.Size;
		clip.AddChild(_list);
		_list.RowHovered += OnRowHovered;
		_list.Clicked += OnClicked;
		_list.Wheel += OnWheel;

		_actionModel = new MenuModel(new[]
		{
			new MenuOption(SettingsScreenLogic.ResetId, "RESET DEFAULTS", true),
			new MenuOption(SettingsScreenLogic.BackId, "BACK", true)
		});
		_actions = RowOptions(ActionFontSize);
		_actions.Position = new Vector2(S(ActionsX), S(ActionsY));
		panel.AddChild(_actions);
		_actions.SetModel(_actionModel);
		_actions.Hovered += index =>
		{
			if (Busy)
				return;
			_logic.Select(_actionModel.Options[index].Id);
			UpdateView();
		};

		_kit.Footer(_root, "W/S SELECT · A/D CHANGE · ENTER · ESC BACK", 220f);

		BuildConfirm();
		BuildDisplayConfirm();
		BuildToast();
	}

	private Label _crumbLabel;

	// Con una pregunta en pantalla, el ratón no toca la lista
	private bool Busy => _logic == null || _logic.Confirming || _display.Showing;

	private void OnRowOptionChosen(string id)
	{
		if (!_display.Showing)
			Apply(_logic.Choose(id));
	}

	// Opciones en fila con el estilo de los selectores (Horizontal antes de Setup: si no, salen en columna)
	private OptionList RowOptions(int fontSize)
	{
		OptionListStyle style = OptionListStyle.Scaled(_scale, fontSize, 44f, 4f, new Vector2(4f, 30f), 16f);
		style.HideDisabledHint = true;
		var options = new OptionList { Name = "OptionList", Horizontal = true };
		options.Setup(style);
		options.Chosen += OnRowOptionChosen;
		return options;
	}

	private void BuildConfirm()
	{
		_confirm = new MenuBand { Name = "Confirm" };
		_root.AddChild(_confirm);
		_confirm.Setup(170f, ConfirmDimOpacity, 0.94f);
		_confirm.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		VBoxContainer column = _confirm.AddColumn("ConfirmColumn", 440f, 18f);
		column.AddChild(_kit.MakeLabel("RESET ALL SETTINGS?", MenuFonts.Orbitron, 900, ConfirmTitleFontSize, 0.06f, Palette.Orange,
			Palette.ConfirmTitleGlow, 14f));
		_confirmOptions = RowOptions(22);
		column.AddChild(_confirmOptions);
		_confirm.Visible = false;
	}

	// ---------------------------------------------------------------
	// Confirmación de pantalla
	// ---------------------------------------------------------------

	private void Watch(SettingsModel model)
	{
		if (_watched != null)
			_watched.Changed -= OnModelChanged;
		_watched = model;
		if (model == null)
			return;
		model.Changed += OnModelChanged;
		_lastDisplay = ((int)model.DisplayMode, model.Resolution);
	}

	// Cada cambio de modo o resolución (también con RESET DEFAULTS) pide confirmación; volver atrás no
	private void OnModelChanged(string id)
	{
		if (id != SettingsCatalog.DisplayMode && id != SettingsCatalog.Resolution)
			return;
		(int Mode, int Resolution) before = _lastDisplay;
		_lastDisplay = ((int)_watched.DisplayMode, _watched.Resolution);
		if (!_reverting)
			_display.Changed(before.Mode, before.Resolution);
	}

	// true si la pregunta está en pantalla y se lleva el teclado
	private bool UpdateDisplayConfirm(float delta)
	{
		switch (_display.Update(delta))
		{
			case DisplayConfirmEvent.Show:
				_displayModel = new MenuModel(new[] { new MenuOption("KEEP", "KEEP", true), new MenuOption("REVERT", "REVERT", true) });
				_displayOptions.SetModel(_displayModel);
				_displayBand.Visible = true;
				break;
			case DisplayConfirmEvent.Revert:
				RevertDisplay(_display.Before);
				return true;
		}
		if (!_display.Showing)
			return false;

		_displayCountdown.Text = $"REVERTING IN {_display.SecondsLeft}";
		if (Input.IsActionJustPressed("menu_back"))
			ChooseDisplay("REVERT");
		else if (Input.IsActionJustPressed("menu_left") || Input.IsActionJustPressed("menu_up"))
		{
			_displayModel.MovePrevious();
			_displayOptions.Refresh();
		}
		else if (Input.IsActionJustPressed("menu_right") || Input.IsActionJustPressed("menu_down"))
		{
			_displayModel.MoveNext();
			_displayOptions.Refresh();
		}
		else if (Input.IsActionJustPressed("menu_accept"))
			ChooseDisplay(_displayModel.Confirm());
		return true;
	}

	private void ChooseDisplay(string id)
	{
		if (!_display.Showing)
			return;
		if (id == "KEEP")
		{
			_display.Keep();
			_displayBand.Visible = false;
		}
		else
		{
			RevertDisplay(_display.Revert());
		}
	}

	private void RevertDisplay((int Mode, int Resolution) before)
	{
		_reverting = true;
		_logic.Model.Set(SettingsCatalog.DisplayMode, before.Mode);
		_logic.Model.Set(SettingsCatalog.Resolution, before.Resolution);
		_reverting = false;
		_displayBand.Visible = false;
		UpdateView();
	}

	private void BuildDisplayConfirm()
	{
		_displayBand = new MenuBand { Name = "DisplayConfirm" };
		_root.AddChild(_displayBand);
		_displayBand.Setup(200f, ConfirmDimOpacity, 0.94f);
		_displayBand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		VBoxContainer column = _displayBand.AddColumn("DisplayColumn", 440f, 14f);
		column.AddChild(_kit.MakeLabel("KEEP THIS DISPLAY?", MenuFonts.Orbitron, 900, ConfirmTitleFontSize, 0.06f, Palette.Orange,
			Palette.ConfirmTitleGlow, 14f));
		_displayCountdown = _kit.MakeLabel("", MenuFonts.Mono, 400, 15f, 0.2f, Palette.SettingsGroupText);
		column.AddChild(_displayCountdown);
		_displayOptions = RowOptions(22);
		_displayOptions.Chosen -= OnRowOptionChosen;
		_displayOptions.Chosen += ChooseDisplay;
		column.AddChild(_displayOptions);
		_displayBand.Visible = false;
	}

	private void BuildToast()
	{
		var box = new StyleBoxFlat
		{
			BgColor = Palette.SettingsToastBackground,
			BorderColor = Palette.Cyan,
			ShadowColor = Palette.SettingsToastGlow,
			ShadowSize = Mathf.RoundToInt(S(12f)),
			ContentMarginLeft = S(26f),
			ContentMarginRight = S(26f),
			ContentMarginTop = S(12f),
			ContentMarginBottom = S(12f)
		};
		box.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(S(1f))));
		_toast = new PanelContainer { Name = "Toast", MouseFilter = Control.MouseFilterEnum.Ignore };
		_toast.AddThemeStyleboxOverride("panel", box);
		_toastText = _kit.MakeLabel("", MenuFonts.Mono, 400, 15f, 0.2f, Palette.Cyan);
		_toast.AddChild(_toastText);
		_toast.AnchorLeft = 0.5f;
		_toast.AnchorRight = 0.5f;
		_toast.GrowHorizontal = Control.GrowDirection.Both;
		_toast.OffsetTop = S(330f);
		_root.AddChild(_toast);
		_toast.Visible = false;
	}
}
