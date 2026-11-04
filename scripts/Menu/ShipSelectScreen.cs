using System;
using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.Ship;
using Vectory.UI;

namespace Vectory.Menu;

// Pantalla SELECT SHIP, dentro de la escena del menú principal (spec docs/selector-naves.md): a la izquierda el
// nombre de la nave y su holograma 3D (ShipHologram); a la derecha el panel con SHIP DATA, SHARED SYSTEMS y
// START / BACK. Las reglas viven en ShipSelectLogic, ShipStats, ShipSelectFormat y HologramSpin; este nodo traduce
// teclado y ratón y lo dibuja. START deja la nave en GameSession y carga la carrera; BACK / ESC lanza Closed.
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor.
public partial class ShipSelectScreen : CanvasLayer
{
	// Zona de arrastre de la nave
	private static readonly Rect2 DragZone = new(0f, 250f, 836f, 420f);

	// Placeholder de una nave COMING SOON
	private static readonly Vector2 PlaceholderCenter = new(410f, 390f);
	private static readonly Vector2 PlaceholderSize = new(460f, 120f);

	private const float DataRowSpacing = 14f;
	private const float SystemLabelWidth = 120f;

	[Export(PropertyHint.File, "*.tscn")] public string RaceScenePath = SceneChanger.RaceScene;

	[ExportGroup("Aspecto (px sobre 1280×720)")]
	[Export] public int NameFontSize = 54;
	[Export] public int OptionFontSize = 22;
	[Export] public float ShipScreenWidth = 480f;

	[ExportGroup("Holograma")]
	[Export] public float AutoRotationPeriod = 20f;     // segundos por vuelta
	[Export] public float InitialTilt = 22f;            // grados
	[Export] public float InitialYaw = 143f;            // grados, sobre el eje vertical
	[Export] public float DragSensitivity = 0.01f;      // radianes por píxel (sobre 1280×720)
	[Export] public float InertiaDecay = 0.4f;          // segundos
	[Export] public float AutoResumeDelay = 3f;         // segundos sin arrastrar hasta que vuelve el giro automático
	[Export] public float AutoResumeRamp = 1f;          // segundos de su rampa
	[Export(PropertyHint.Range, "0,1,0.01")] public float FarEdgeBrightness = 0.35f;

	// Se lanza al volver a SELECT CIRCUIT, con el modo de la pantalla
	public event Action<GameMode> Closed;

	public bool IsOpen => _root != null && _root.Visible;

	private sealed class StatRow
	{
		public Label Value;
		public ShipStatBarView Bar;
	}

	private SelectScreenKit _kit;
	private float _scale = 1f;
	private ShipSelectLogic _logic;
	private HologramSpin _spin;
	private ulong _openedFrame;

	private Control _root;
	private ShipHologram _hologram;
	private Control _dragZone;
	private Control _placeholder;
	private Label _header;
	private Label _name;
	private Control _comingSoonTag;
	private Label _description;
	private Label _counter;
	private readonly List<StatRow> _statRows = new();
	private readonly List<Label> _arrows = new();
	private readonly List<Control> _navButtons = new();
	private Label _boostValue;
	private Label _damageValue;
	private OptionList _options;

	private LabelSettings _nameSettings;
	private LabelSettings _nameDisabledSettings;
	private LabelSettings _statValueSettings;
	private LabelSettings _statEmptySettings;
	private LabelSettings _damageOnSettings;
	private LabelSettings _damageOffSettings;
	private LabelSettings _arrowActive;
	private LabelSettings _arrowInactive;

	public override void _Ready()
	{
		Layer = 3;   // como SELECT CIRCUIT: por encima del menú principal
		_scale = UiScale.Factor;
		_kit = new SelectScreenKit(_scale);

		_spin = new HologramSpin(new HologramSpinSettings
		{
			InitialYaw = Mathf.DegToRad(InitialYaw),
			InitialTilt = Mathf.DegToRad(InitialTilt),
			AutoPeriod = AutoRotationPeriod,
			DragSensitivity = DragSensitivity,
			InertiaDecay = InertiaDecay,
			AutoResumeDelay = AutoResumeDelay,
			AutoResumeRamp = AutoResumeRamp,
			StillRelease = HologramSpinSettings.Default.StillRelease,
			VelocitySmoothing = HologramSpinSettings.Default.VelocitySmoothing
		});

		_hologram = new ShipHologram { Name = "Hologram", ShipScreenWidth = ShipScreenWidth, FarEdgeBrightness = FarEdgeBrightness };
		AddChild(_hologram);
		_hologram.SetOrientation(_spin.Orientation);

		DragCursors.Install();
		Build();
		_root.Visible = false;
	}

	// Abre la pantalla: la nave recordada (GameSession) si es elegible, con START seleccionada.
	// La orientación del holograma se conserva de la última vez.
	public void Open(GameMode mode, string circuitName)
	{
		_logic = new ShipSelectLogic(mode, circuitName, ShipCatalog.Entries, GameSession.ShipDesignId, ShipCatalog.Default);
		_options.SetModel(_logic.Menu);
		_openedFrame = Engine.GetProcessFrames();   // el Enter que abre la pantalla no elige nada aquí

		_hologram.SetActive(true);
		_root.Visible = true;
		UpdateView();
	}

	public override void _Process(double delta)
	{
		if (!IsOpen)
			return;

		// Con una nave COMING SOON el holograma se queda como estaba: al volver sigue en la misma orientación
		if (_logic.Ship.Selectable)
		{
			_spin.Step((float)delta);
			_hologram.SetOrientation(_spin.Orientation);
		}

		if (Engine.GetProcessFrames() == _openedFrame)
			return;

		if (Input.IsActionJustPressed("menu_back"))
			Apply(_logic.Back());
		else if (Input.IsActionJustPressed("menu_left"))
			ChangeShip(-1);
		else if (Input.IsActionJustPressed("menu_right"))
			ChangeShip(1);
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

	private void ChangeShip(int step)
	{
		if (!_logic.CanChangeShip)
			return;

		EndDrag();
		if (step > 0)
			_logic.NextShip();
		else
			_logic.PreviousShip();

		_options.SetModel(_logic.Menu);
		UpdateView();
	}

	private void Apply(ShipSelectAction action)
	{
		switch (action)
		{
			case ShipSelectAction.Start:
				EndDrag();
				GameSession.Mode = _logic.Mode;
				GameSession.ShipDesignId = _logic.ShipId;
				SceneChanger.ChangeTo(this, RaceScenePath);
				break;

			case ShipSelectAction.BackToCircuitSelect:
				EndDrag();
				_root.Visible = false;
				_hologram.SetActive(false);
				Closed?.Invoke(_logic.Mode);
				break;
		}
	}

	// ---------------------------------------------------------------
	// Arrastre
	// ---------------------------------------------------------------

	private void OnDragZoneInput(InputEvent e)
	{
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
		{
			if (button.Pressed)
			{
				_spin.BeginDrag();
				_dragZone.MouseDefaultCursorShape = DragCursors.Closed;
			}
			else
			{
				EndDrag();
			}
		}
		else if (e is InputEventMouseMotion motion && _spin.IsDragging)
		{
			_spin.Drag(motion.Relative.X / _scale, motion.Relative.Y / _scale);
		}
	}

	private void EndDrag()
	{
		_spin.Release();
		_dragZone.MouseDefaultCursorShape = DragCursors.Open;
	}

	// ---------------------------------------------------------------
	// Vista
	// ---------------------------------------------------------------

	private void UpdateView()
	{
		ShipEntry ship = _logic.Ship;
		bool selectable = ship.Selectable;

		_header.Text = ShipSelectFormat.Header(_logic.Mode, _logic.CircuitName);
		_name.Text = ship.Name;
		_name.LabelSettings = selectable ? _nameSettings : _nameDisabledSettings;
		_comingSoonTag.Visible = !selectable;
		_description.Text = ship.Description;
		_counter.Text = ShipSelectFormat.Counter(_logic.ShipIndex, _logic.ShipCount);

		bool arrows = _logic.CanChangeShip;
		foreach (Label arrow in _arrows)
			arrow.LabelSettings = arrows ? _arrowActive : _arrowInactive;
		foreach (Control button in _navButtons)
		{
			button.MouseFilter = arrows ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
			button.MouseDefaultCursorShape = arrows ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
		}

		// Holograma o placeholder
		_hologram.SetShipVisible(selectable);
		_hologram.SetOrientation(_spin.Orientation);
		_placeholder.Visible = !selectable;
		_dragZone.MouseFilter = selectable ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		_dragZone.MouseDefaultCursorShape = DragCursors.Open;

		// SHIP DATA: el perfil de cada nave se compara con Vector (el jugador no lo ve)
		IReadOnlyList<ShipStat> stats = selectable
			? ShipStats.Compute(ship.Tuning.Value, VectorTuning.Values)
			: ShipStats.Empty();
		for (int i = 0; i < _statRows.Count; i++)
		{
			_statRows[i].Value.Text = stats[i].Value;
			_statRows[i].Value.LabelSettings = selectable ? _statValueSettings : _statEmptySettings;
			_statRows[i].Bar.SetValue(stats[i].Segments, !selectable);
		}

		// SHARED SYSTEMS
		_boostValue.Text = ShipSelectFormat.Boost(VectorTuning.BoostMaxCharges, VectorTuning.BoostSpeedGainKmh);
		_damageValue.Text = ShipSelectFormat.Damage(_logic.Mode, DamageModel.MaxLevel);
		_damageValue.LabelSettings = _logic.Mode == GameMode.TimeTrial ? _damageOffSettings : _damageOnSettings;

		_options.Refresh();
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private void Build()
	{
		_root = new Control { Name = "ShipSelectRoot", MouseFilter = Control.MouseFilterEnum.Stop };   // tapa el ratón al menú de debajo
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_nameSettings = _kit.TextSettings(MenuFonts.Orbitron, 900, NameFontSize, 0.08f, Palette.SelectName, Palette.SelectNameGlow, 16f);
		_nameDisabledSettings = _kit.TextSettings(MenuFonts.Orbitron, 900, NameFontSize, 0.08f, Palette.ShipNameDisabled);
		_statValueSettings = _kit.TextSettings(MenuFonts.Orbitron, 500, 15f, 0.06f, Palette.SelectValue);
		_statEmptySettings = _kit.TextSettings(MenuFonts.Orbitron, 500, 15f, 0.06f, Palette.ShipValueEmpty);
		_damageOnSettings = _statValueSettings;
		_damageOffSettings = _kit.TextSettings(MenuFonts.Orbitron, 500, 15f, 0.06f, Palette.ShipDamageOff);
		_arrowActive = _kit.TextSettings(MenuFonts.Orbitron, 500, 22f, 0f, Palette.SelectArrow);
		_arrowInactive = _kit.TextSettings(MenuFonts.Orbitron, 500, 22f, 0f, Palette.SelectArrowDisabled);

		BuildDragZone();
		BuildPlaceholder();
		BuildNameBlock();
		BuildPanel();
		_kit.Footer(_root, ShipSelectFormat.FooterHint);
	}

	private void BuildDragZone()
	{
		_dragZone = new Control
		{
			Name = "DragZone",
			Position = DragZone.Position * _scale,
			Size = DragZone.Size * _scale,
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = DragCursors.Open
		};
		_dragZone.GuiInput += OnDragZoneInput;
		_root.AddChild(_dragZone);
	}

	private void BuildPlaceholder()
	{
		var texts = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		texts.AddThemeConstantOverride("separation", _kit.Px(6f));
		Label title = _kit.MakeLabel(ShipSelectFormat.InDevelopment, MenuFonts.Mono, 400, 15f, 0.22f, Palette.ShipPlaceholderTitle);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		texts.AddChild(title);
		Label hint = _kit.MakeLabel(ShipSelectFormat.ComingSoon, MenuFonts.Mono, 400, 12f, 0.22f, Palette.ShipPlaceholderHint);
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		texts.AddChild(hint);

		var view = new ShipPlaceholderView { Name = "Placeholder" };
		view.Setup(new Rect2((PlaceholderCenter - PlaceholderSize * 0.5f) * _scale, PlaceholderSize * _scale),
			_kit.S(30f), _kit.S(10f), _kit.S(8f), Mathf.Max(1f, _kit.S(1f)), texts);
		_root.AddChild(view);
		_placeholder = view;
	}

	private void BuildNameBlock()
	{
		VBoxContainer column = _kit.NameColumn(_root);

		_header = _kit.MakeLabel("", MenuFonts.Mono, 400, 15f, 0.22f, Palette.SelectHeader);
		column.AddChild(_header);

		var nameRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		nameRow.AddThemeConstantOverride("separation", _kit.Px(16f));
		column.AddChild(nameRow);

		_name = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, LabelSettings = _nameSettings };
		nameRow.AddChild(_name);

		_comingSoonTag = _kit.Tag(ShipSelectFormat.ComingSoon, Palette.ShipTag, Palette.ShipTagBorder);
		nameRow.AddChild(_comingSoonTag);

		_description = _kit.MakeLabel("", MenuFonts.Mono, 400, 14f, 0.12f, Palette.SelectLabel);
		column.AddChild(_description);

		var nav = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		nav.AddThemeConstantOverride("separation", _kit.Px(14f));
		column.AddChild(nav);

		nav.AddChild(NavButton("‹", -1));
		_counter = _kit.MakeLabel("", MenuFonts.Mono, 400, 14f, 0.12f, Palette.SelectLabel);
		_counter.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		nav.AddChild(_counter);
		nav.AddChild(NavButton("›", 1));
	}

	private Control NavButton(string arrow, int step)
	{
		Control button = _kit.NavButton(arrow, () => ChangeShip(step), _arrowInactive, out Label label);
		_arrows.Add(label);
		_navButtons.Add(button);
		return button;
	}

	private void BuildPanel()
	{
		VBoxContainer content = _kit.Panel(_root, "ShipPanel");

		// SHIP DATA
		content.AddChild(_kit.SectionHeader("SHIP DATA"));
		var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		rows.AddThemeConstantOverride("separation", _kit.Px(DataRowSpacing));
		content.AddChild(rows);
		foreach (string label in new[] { ShipStats.TopSpeed, ShipStats.Acceleration, ShipStats.Handling, ShipStats.Grip, ShipStats.Braking })
			rows.AddChild(StatRowControl(label));

		// SHARED SYSTEMS
		content.AddChild(new Control { CustomMinimumSize = new Vector2(0f, _kit.S(12f)), MouseFilter = Control.MouseFilterEnum.Ignore });
		content.AddChild(_kit.SectionHeader("SHARED SYSTEMS"));
		_boostValue = SystemRow(content, "BOOST", _kit.TextSettings(MenuFonts.Orbitron, 500, 15f, 0.06f, Palette.ShipBoost));
		_damageValue = SystemRow(content, "DAMAGE", _damageOnSettings);

		// Opciones abajo del panel
		content.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
		_options = _kit.Options(OptionFontSize, id => Apply(_logic.Choose(id)));
		content.AddChild(_options);
	}

	// Dato: línea azul de 2 px; arriba etiqueta y valor, debajo la barra
	private Control StatRowControl(string label)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", _kit.Px(10f));
		row.AddChild(new ColorRect
		{
			Color = Palette.SelectDataBar,
			CustomMinimumSize = new Vector2(Mathf.Max(1f, _kit.S(2f)), 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		});

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", _kit.Px(4f));
		row.AddChild(column);

		// Con alto fijo: "—" no mide lo mismo que los valores, y el panel no debe moverse al cambiar de nave
		var texts = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0f, _kit.S(20f)) };
		column.AddChild(texts);
		Label name = _kit.MakeLabel(label, MenuFonts.Mono, 400, 11f, 0.14f, Palette.SelectLabel);
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		texts.AddChild(name);
		var value = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, LabelSettings = _statValueSettings, VerticalAlignment = VerticalAlignment.Center };
		value.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		texts.AddChild(value);

		var bar = new ShipStatBarView();
		bar.Setup(ShipStats.SegmentCount, new Vector2(_kit.S(30f), _kit.S(7f)), _kit.S(3f), _kit.S(2f), Mathf.Max(1f, _kit.S(1f)));
		bar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		column.AddChild(bar);

		_statRows.Add(new StatRow { Value = value, Bar = bar });
		return row;
	}

	private Label SystemRow(VBoxContainer content, string label, LabelSettings valueSettings)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 0);
		content.AddChild(row);

		Label name = _kit.MakeLabel(label, MenuFonts.Mono, 400, 12f, 0.18f, Palette.SelectLabel);
		name.CustomMinimumSize = new Vector2(_kit.S(SystemLabelWidth), 0f);
		name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(name);

		var value = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, LabelSettings = valueSettings };
		row.AddChild(value);
		return value;
	}
}
