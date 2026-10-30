using System;
using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.UI;

namespace Vectory.Menu;

// Pausa de la carrera (ESC): franja horizontal con PAUSED y CONTINUE / RESTART / BACK TO MAIN MENU,
// y la confirmación (NO / YES) en la misma franja. Las reglas viven en PauseLogic; este nodo
// traduce el input, dibuja el estado, congela el árbol (GetTree().Paused) y cambia de escena.
// En TIME TRIAL (EnableGhostOption) añade GHOST: ON / OFF; el cambio se avisa con GhostToggled.
//
// ProcessMode = Always: sigue funcionando con el árbol en pausa. Todo lo demás de la carrera
// (nave, carrera, HUD, cámara, resultados) se para.
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor (como el menú principal).
public partial class PauseMenu : CanvasLayer
{
	private const string PausedText = "PAUSED";
	private const string PausedHint = "ESC  ·  CONTINUE";
	private const string ConfirmHint = "CURRENT RACE PROGRESS WILL BE LOST";
	private const string ConfirmRestartTitle = "RESTART RACE?";
	private const string ConfirmBackTitle = "BACK TO MAIN MENU?";

	// Posiciones del lienzo de diseño (px sobre 1280×720; la franja va centrada en vertical)
	private const float TextLeft = 96f;
	private const float SeparatorX = 500f;
	private const float SeparatorHeight = 156f;
	private const float GhostSeparatorHeight = 204f;   // con cuatro opciones (TIME TRIAL)
	private const float OptionsLeft = 560f;
	private const float TitleGap = 12f;           // entre el título y el texto secundario
	private const float TitleMargin = 24f;        // hueco mínimo entre el título y el separador

	[Export(PropertyHint.File, "*.tscn")] public string MainMenuScenePath = SceneChanger.MainMenuScene;

	[ExportGroup("Aspecto (px sobre 1280×720)")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float DimOpacity = 0.45f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float BandOpacity = 0.9f;
	[Export] public float BandHeight = 260f;
	[Export] public int PausedFontSize = 56;
	[Export] public int ConfirmTitleFontSize = 40;   // tamaño máximo: si el título no cabe antes del separador, se reduce
	[Export] public int SecondaryFontSize = 16;
	[Export] public int OptionFontSize = 28;

	// Se lanza al cerrar la pausa para seguir con la carrera
	public event Action Resumed;

	// Se lanza al cambiar GHOST: ON / OFF (nuevo valor del ajuste); la pausa sigue abierta
	public event Action<bool> GhostToggled;

	// La carrera ya ha terminado: RESTART y BACK TO MAIN MENU no piden confirmación
	public bool RaceFinished
	{
		get => _logic.RaceFinished;
		set => _logic.RaceFinished = value;
	}

	public bool IsOpen => _logic.State != PauseState.Closed;

	private PauseLogic _logic = new();
	private Func<bool> _ghostAvailable;
	private float _scale = 1f;

	private Control _root;
	private MenuBand _band;
	private Label _title;
	private Label _hint;
	private OptionList _options;
	private LabelSettings _pausedSettings;
	private LabelSettings _confirmSettings;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = 10;   // por encima del HUD y de los resultados
		_scale = UiScale.Factor;

		Build();
		UpdateView();
	}

	// TIME TRIAL: añade GHOST: ON / OFF con el valor guardado. ghostAvailable dice, cada vez que se abre
	// la pausa, si hay fantasma válido (si no, la opción sale desactivada).
	public void EnableGhostOption(bool ghostEnabled, Func<bool> ghostAvailable)
	{
		_logic = new PauseLogic(hasGhostOption: true) { GhostEnabled = ghostEnabled, RaceFinished = _logic.RaceFinished };
		_ghostAvailable = ghostAvailable;
		_band.SetSeparatorHeight(0, GhostSeparatorHeight);
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("pause"))
		{
			if (_logic.State == PauseState.Closed)
				_logic.GhostAvailable = _ghostAvailable?.Invoke() ?? false;
			Apply(_logic.TogglePause());
			return;
		}

		if (_logic.State == PauseState.Closed)
			return;

		if (Input.IsActionJustPressed("menu_down"))
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
		{
			Apply(_logic.Choose(_logic.Menu.Confirm()));
		}
	}

	// Ejecuta lo que pide la lógica y actualiza la franja
	private void Apply(PauseAction action)
	{
		switch (action)
		{
			case PauseAction.Restart:
				SceneChanger.Reload(this);
				return;
			case PauseAction.GoToMenu:
				SceneChanger.ChangeTo(this, MainMenuScenePath);
				return;
		}

		bool open = _logic.State != PauseState.Closed;
		GetTree().Paused = open;
		UpdateView();

		if (action == PauseAction.Close)
			Resumed?.Invoke();
		else if (action == PauseAction.ToggleGhost)
			GhostToggled?.Invoke(_logic.GhostEnabled);
	}

	// ---------------------------------------------------------------
	// Vista
	// ---------------------------------------------------------------

	private void UpdateView()
	{
		_root.Visible = _logic.State != PauseState.Closed;
		if (!_root.Visible)
			return;

		if (_logic.State == PauseState.Confirm)
		{
			string title = _logic.Confirming == PauseTarget.Restart ? ConfirmRestartTitle : ConfirmBackTitle;
			_title.LabelSettings = FitTitle(title);
			_title.Text = title;
			_hint.Text = ConfirmHint;
		}
		else
		{
			_title.LabelSettings = _pausedSettings;
			_title.Text = PausedText;
			_hint.Text = PausedHint;
		}

		if (_options.Model != _logic.Menu)
			_options.SetModel(_logic.Menu);
		else
			_options.Refresh();
	}

	// Ajustes del título de confirmación: ConfirmTitleFontSize, o menos si no cabe antes del separador
	private LabelSettings FitTitle(string title)
	{
		float maxWidth = S(SeparatorX - TextLeft - TitleMargin);
		int size = Mathf.RoundToInt(S(ConfirmTitleFontSize));
		Font font = MenuFonts.Make(MenuFonts.Orbitron, 900, size, 0.08f);

		while (size > 8 && font.GetStringSize(title, HorizontalAlignment.Left, -1, size).X > maxWidth)
		{
			size--;
			font = MenuFonts.Make(MenuFonts.Orbitron, 900, size, 0.08f);
		}

		if (size == _confirmSettings?.FontSize)
			return _confirmSettings;

		_confirmSettings = MenuFonts.Settings(font, size, Palette.ConfirmTitle, Palette.ConfirmTitleGlow, S(14f));
		return _confirmSettings;
	}

	private float S(float referencePx) => referencePx * _scale;

	private void Build()
	{
		_root = new Control { Name = "PauseRoot", MouseFilter = Control.MouseFilterEnum.Stop };   // tapa el ratón a lo de debajo
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		// Atenuado de toda la carrera y franja de lado a lado
		var band = new MenuBand { Name = "MenuBand" };
		_root.AddChild(band);
		band.Setup(BandHeight, DimOpacity, BandOpacity);
		_band = band;

		// Bloque izquierdo: título y texto secundario, centrados en vertical
		VBoxContainer left = band.AddColumn("Text", TextLeft, TitleGap);

		int pausedSize = Mathf.RoundToInt(S(PausedFontSize));
		_pausedSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 900, pausedSize, 0.08f),
			pausedSize, Palette.PauseTitle, Palette.PauseTitleGlow, S(16f));
		_title = new Label { Name = "Title", MouseFilter = Control.MouseFilterEnum.Ignore };
		left.AddChild(_title);

		int secondarySize = Mathf.RoundToInt(S(SecondaryFontSize));
		_hint = new Label
		{
			Name = "Hint",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, secondarySize, 0.18f),
				secondarySize, Palette.PauseHint, Colors.Transparent, 0f)
		};
		left.AddChild(_hint);

		band.AddSeparator(SeparatorX, SeparatorHeight);

		// Opciones, centradas en vertical en la franja (mismo aspecto que el menú principal)
		VBoxContainer column = band.AddColumn("OptionsColumn", OptionsLeft, 0f);
		_options = new OptionList { Name = "Options" };
		OptionListStyle style = OptionListStyle.Scaled(_scale, OptionFontSize, 48f, 6f, new Vector2(4f, 36f), 20f);
		style.HideDisabledHint = true;   // GHOST sin fantasma: desactivada, sin COMING SOON
		_options.Setup(style);
		_options.Chosen += id => Apply(_logic.Choose(id));
		column.AddChild(_options);
	}
}