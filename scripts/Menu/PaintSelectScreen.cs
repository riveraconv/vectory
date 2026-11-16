using System;
using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.Records;
using Vectory.Ship;
using Vectory.UI;

namespace Vectory.Menu;

// Pantalla PAINT, dentro de la escena del menú y sobre SELECT SHIP (spec docs/selector-pinturas.md, composición A ·
// TALLER): a la izquierda el holograma de SELECT SHIP, el mismo, sin rehacerlo ni moverlo, con el nombre de la nave y
// de la pintura; a la derecha el panel con las 40 muestras y APPLY / BACK. Las reglas viven en PaintSelectLogic,
// PaintSelectFormat y PaintSwatch; este nodo traduce teclado y ratón, lo dibuja, pinta el holograma con la muestra
// seleccionada y guarda con APPLY. Al cerrarse lanza Closed: quien la abrió vuelve a poner la pintura guardada.
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor.
public partial class PaintSelectScreen : CanvasLayer
{
	// Zona de arrastre de la nave: la de SELECT SHIP
	private static readonly Rect2 DragZone = new(0f, 250f, 836f, 420f);

	// Bloques del panel, desde arriba de su contenido (y = 52 de la pantalla)
	private const float NameRowY = 24f;
	private const float DataLineY = 60f;
	private const float PlainGroupY = 98f;
	private const float PlainGridY = 118f;
	private const float MarksGroupY = 284f;
	private const float MarksGridY = 304f;
	private const float NameTagGap = 10f;
	// Pie: la spec lo pone en x ≈ 110 porque en la maqueta la versión va en x = 28; en el juego va en x = 100, así que
	// empieza detrás de ella. Espaciado de la maqueta (0,08 em), para que quepa antes del panel.
	private const float FooterX = 196f;
	private const float FooterSpacing = 0.08f;

	[ExportGroup("Aspecto (px sobre 1280×720)")]
	[Export] public Vector2 SwatchSize = new(66f, 30f);
	[Export] public float SwatchGap = 8f;
	[Export] public int PaintNameFontSize = 24;
	[Export] public int ShipNameFontSize = 54;
	[Export] public int OptionFontSize = 22;

	// Se lanza al volver a SELECT SHIP, con APPLY o sin cambios
	public event Action Closed;

	public bool IsOpen => _root != null && _root.Visible;

	// Fondos de las muestras de cada estilo, hechos una vez por ejecución
	private static readonly Dictionary<HullStyle, Texture2D[]> SwatchTextures = new();

	private SelectScreenKit _kit;
	private float _scale = 1f;
	private ShipHologram _hologram;
	private HologramSpin _spin;
	private PaintSelectLogic _logic;
	private ulong _openedFrame;
	private int _shownIndex = -1;

	private Control _root;
	private Control _dragZone;
	private Label _header;
	private Label _shipName;
	private Label _paintLineName;
	private Label _paintName;
	private PanelContainer _tag;
	private StyleBoxFlat _tagBox;
	private Label _tagLabel;
	private Label _dataLine;
	private Label _marksGroup;
	private PaintSwatchGridView _plainGrid;
	private PaintSwatchGridView _marksGrid;
	private OptionList _options;
	private Label _footer;

	private LabelSettings _tagEquipped;
	private LabelSettings _tagPreview;

	// El holograma y su giro, los de SELECT SHIP
	public void Setup(ShipHologram hologram, HologramSpin spin)
	{
		_hologram = hologram;
		_spin = spin;
	}

	public override void _Ready()
	{
		Layer = 3;   // como SELECT SHIP; va después en el árbol, así que queda encima
		_scale = UiScale.Factor;
		_kit = new SelectScreenKit(_scale);
		Build();
		_root.Visible = false;
	}

	// Abre la pantalla con la pintura guardada de la nave seleccionada y marcada como equipada
	public void Open(GameMode mode, string circuitName, ShipEntry ship, PaintChoice saved)
	{
		if (HullPaints.StyleOf(ship.Id) is not HullStyle style)
			return;

		_logic = new PaintSelectLogic(mode, circuitName, ship.Id, style, saved);
		ShipHull3D.PrewarmPaints(ShipDefinitions.Find(ship.Id)?.Visual.Hull3D);   // camuflajes y chapas, en otro hilo
		_openedFrame = Engine.GetProcessFrames();   // el Enter que abre la pantalla no elige nada aquí
		_shownIndex = _logic.Index;

		_header.Text = PaintSelectFormat.Header(mode, circuitName);
		_header.LabelSettings = FittedSettings(_header.Text, MenuFonts.Mono, 400, 15f, 0.22f, Palette.SelectHeader,
			SelectScreenKit.PanelX - SelectScreenKit.NameX - 12f);
		_shipName.Text = ship.Name;
		_marksGroup.Text = PaintSelectFormat.MarksGroup(style);
		_footer.Text = PaintSelectFormat.Footer(style);
		_footer.LabelSettings = FittedSettings(_footer.Text, MenuFonts.Mono, 400, 13f, FooterSpacing, Palette.SelectFooter,
			SelectScreenKit.PanelX - FooterX - 12f);
		FitPaintName(style);

		Texture2D[] textures = Textures(style);
		var plain = new PaintSwatch[PaintSelectLogic.GroupSize];
		var marks = new PaintSwatch[PaintSelectLogic.GroupSize];
		for (int i = 0; i < PaintSelectLogic.GroupSize; i++)
		{
			plain[i] = PaintSwatch.For(style, i);
			marks[i] = PaintSwatch.For(style, PaintSelectLogic.GroupSize + i);
		}
		_plainGrid.SetSwatches(plain, textures[..PaintSelectLogic.GroupSize]);
		_marksGrid.SetSwatches(marks, textures[PaintSelectLogic.GroupSize..]);

		_options.SetModel(_logic.Menu);
		_root.Visible = true;
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (!IsOpen)
			return;

		_spin.Step((float)delta);
		_hologram.SetOrientation(_spin.Orientation);

		if (Engine.GetProcessFrames() == _openedFrame)
			return;

		if (Input.IsActionJustPressed("menu_back"))
			Apply(_logic.Back());
		else if (Input.IsActionJustPressed("menu_accept"))
			Apply(_logic.Accept());
		else
		{
			if (Input.IsActionJustPressed("menu_left"))
				_logic.Left();
			else if (Input.IsActionJustPressed("menu_right"))
				_logic.Right();
			else if (Input.IsActionJustPressed("menu_up"))
				_logic.Up();
			else if (Input.IsActionJustPressed("menu_down"))
				_logic.Down();
			else if (Input.IsActionJustPressed("menu_marks"))
				_logic.ToggleMarks();
			else
				return;
			Refresh();
		}
	}

	private void Apply(PaintSelectAction action)
	{
		switch (action)
		{
			case PaintSelectAction.Apply:
				SavePaint(_logic.ShipId, _logic.Selection);
				Close();
				break;

			case PaintSelectAction.Back:
				Close();
				break;
		}
	}

	private void Close()
	{
		EndDrag();
		_root.Visible = false;
		Closed?.Invoke();
	}

	// Si no se puede guardar, se avisa y se vuelve igual
	private static void SavePaint(string shipId, PaintChoice choice)
	{
		var store = new SettingsStore(new FileRecordStorage(SettingsStore.DefaultPath));
		store.Load();
		if (store.Warning != null)
			GD.PushWarning($"Settings: {store.Warning}");

		store.Settings.SetPaint(shipId, choice);
		if (!store.Save())
			GD.PushWarning($"Settings: {store.Warning}");
	}

	// ---------------------------------------------------------------
	// Vista
	// ---------------------------------------------------------------

	private void Refresh()
	{
		int index = _logic.Index;
		if (index != _shownIndex)
		{
			_shownIndex = index;
			_hologram.SetHullPaint(_logic.Style, index);
		}

		string name = PaintSelectFormat.PaintName(_logic.Style, index);
		_paintLineName.Text = name;
		_paintName.Text = name;
		_dataLine.Text = PaintSelectFormat.DataLine(_logic.Style, index);

		bool equipped = _logic.SelectionIsEquipped;
		_tagLabel.Text = PaintSelectFormat.Status(equipped);
		_tagLabel.LabelSettings = equipped ? _tagEquipped : _tagPreview;
		_tagBox.BorderColor = equipped ? Palette.PaintTagEquippedBorder : Palette.PaintTagPreviewBorder;

		_plainGrid.SetState(index, _logic.SavedIndex);
		_marksGrid.SetState(index, _logic.SavedIndex);

		_options.ShowSelection = _logic.Focus == PaintSelectFocus.Options;
		_options.Refresh();
	}

	private void OnSwatchClicked(int index)
	{
		_logic.Select(index);
		Refresh();
	}

	private void OnOptionHovered(int option)
	{
		_logic.FocusOptions(option);
		Refresh();
	}

	private Texture2D[] Textures(HullStyle style)
	{
		if (SwatchTextures.TryGetValue(style, out Texture2D[] textures))
			return textures;
		textures = new Texture2D[PaintSelectLogic.GroupSize * 2];
		for (int i = 0; i < textures.Length; i++)
			textures[i] = PaintSwatchGridView.BakeTexture(PaintSwatch.For(style, i), SwatchSize, _scale);
		return SwatchTextures[style] = textures;
	}

	// Tamaño del nombre de la pintura (hasta PaintNameFontSize) con el que el más largo de la nave y la etiqueta más
	// ancha caben en el panel; uno para las 20, así el nombre no cambia de tamaño al moverse
	private void FitPaintName(HullStyle style)
	{
		// El ancho de las cuadrículas (algo más que el contenido del panel, como en la maqueta)
		float available = _plainGrid.CustomMinimumSize.X;
		Font tagFont = MenuFonts.Make(MenuFonts.Mono, 400, _kit.S(11f), 0.18f);
		float tag = Mathf.Max(Width(tagFont, PaintSelectFormat.Equipped, _kit.S(11f)), Width(tagFont, PaintSelectFormat.Preview, _kit.S(11f)))
			+ _kit.S(14f) + 2f * Mathf.Max(1f, _kit.S(1f));

		int size = PaintNameFontSize;
		for (; size > 10; size--)
		{
			Font font = MenuFonts.Make(MenuFonts.Orbitron, 500, _kit.S(size), 0.06f);
			float widest = 0f;
			for (int i = 0; i < PaintSelectLogic.GroupSize; i++)
				widest = Mathf.Max(widest, Width(font, PaintSelectFormat.PaintName(style, i), _kit.S(size)));
			if (widest + _kit.S(NameTagGap) + tag <= available)
				break;
		}
		_paintName.LabelSettings = _kit.TextSettings(MenuFonts.Orbitron, 500, size, 0.06f, Palette.PaintName);
	}

	// Ajustes de un texto con el mayor tamaño (hasta size) con el que cabe en width (sobre 1280×720)
	private LabelSettings FittedSettings(string text, FontFile file, int weight, float size, float spacingEm, Color color, float width)
	{
		float fitted = size;
		while (fitted > 8f && Width(MenuFonts.Make(file, weight, _kit.S(fitted), spacingEm), text, _kit.S(fitted)) > _kit.S(width))
			fitted -= 1f;
		return _kit.TextSettings(file, weight, fitted, spacingEm, color);
	}

	private static float Width(Font font, string text, float size) =>
		font.GetStringSize(text, HorizontalAlignment.Left, -1f, Mathf.RoundToInt(size)).X;

	// ---------------------------------------------------------------
	// Arrastre (el de SELECT SHIP)
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
		_spin?.Release();
		if (_dragZone != null)
			_dragZone.MouseDefaultCursorShape = DragCursors.Open;
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private void Build()
	{
		_root = new Control { Name = "PaintSelectRoot", MouseFilter = Control.MouseFilterEnum.Stop };   // tapa el ratón a lo de debajo
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_tagEquipped = _kit.TextSettings(MenuFonts.Mono, 400, 11f, 0.18f, Palette.PaintTagEquipped);
		_tagPreview = _kit.TextSettings(MenuFonts.Mono, 400, 11f, 0.18f, Palette.PaintTagPreview);

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

		BuildNameBlock();
		BuildPanel();
		_footer = _kit.Footer(_root, "", FooterX);
	}

	private void BuildNameBlock()
	{
		VBoxContainer column = _kit.NameColumn(_root);

		_header = _kit.MakeLabel("", MenuFonts.Mono, 400, 15f, 0.22f, Palette.SelectHeader);
		column.AddChild(_header);

		_shipName = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = _kit.TextSettings(MenuFonts.Orbitron, 900, ShipNameFontSize, 0.08f, Palette.SelectName, Palette.SelectNameGlow, 16f)
		};
		column.AddChild(_shipName);

		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 0);
		column.AddChild(line);
		line.AddChild(_kit.MakeLabel(PaintSelectFormat.PaintPrefix, MenuFonts.Mono, 400, 14f, 0.12f, Palette.SelectLabel));
		_paintLineName = _kit.MakeLabel("", MenuFonts.Mono, 400, 14f, 0.12f, Palette.SelectValue);
		line.AddChild(_paintLineName);
	}

	private void BuildPanel()
	{
		VBoxContainer content = _kit.Panel(_root, "PaintPanel");

		// Bloques en su sitio de la maqueta, dentro de un área de alto fijo
		var area = new Control
		{
			Name = "Swatches",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0f, _kit.S(MarksGridY + PaintSelectLogic.Rows * SwatchSize.Y + (PaintSelectLogic.Rows - 1) * SwatchGap))
		};
		content.AddChild(area);

		Place(area, _kit.SectionHeader(PaintSelectFormat.Title), 0f);

		var nameRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		nameRow.AddThemeConstantOverride("separation", _kit.Px(NameTagGap));
		_paintName = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		nameRow.AddChild(_paintName);
		nameRow.AddChild(BuildTag());
		Place(area, nameRow, NameRowY);

		_dataLine = _kit.MakeLabel("", MenuFonts.Mono, 400, 12f, 0.12f, Palette.SelectLabel);
		Place(area, _dataLine, DataLineY);

		Place(area, _kit.MakeLabel(PaintSelectFormat.PlainGroup, MenuFonts.Mono, 400, 11f, 0.2f, Palette.SelectLabel), PlainGroupY);
		_plainGrid = new PaintSwatchGridView { Name = "PlainGrid" };
		_plainGrid.Setup(_scale, SwatchSize, SwatchGap, 0);
		_plainGrid.Clicked += OnSwatchClicked;
		Place(area, _plainGrid, PlainGridY);

		_marksGroup = _kit.MakeLabel("", MenuFonts.Mono, 400, 11f, 0.2f, Palette.SelectLabel);
		Place(area, _marksGroup, MarksGroupY);
		_marksGrid = new PaintSwatchGridView { Name = "MarksGrid" };
		_marksGrid.Setup(_scale, SwatchSize, SwatchGap, PaintSelectLogic.GroupSize);
		_marksGrid.Clicked += OnSwatchClicked;
		Place(area, _marksGrid, MarksGridY);

		// APPLY / BACK abajo del panel
		content.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
		_options = _kit.Options(OptionFontSize, id => Apply(_logic.Choose(id)));
		_options.Hovered += OnOptionHovered;
		content.AddChild(_options);
	}

	// Etiqueta EQUIPPED / PREVIEW: borde de 1 px, relleno 2 × 7
	private Control BuildTag()
	{
		_tagBox = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = Palette.PaintTagEquippedBorder,
			ContentMarginLeft = _kit.S(7f),
			ContentMarginRight = _kit.S(7f),
			ContentMarginTop = _kit.S(2f),
			ContentMarginBottom = _kit.S(2f)
		};
		_tagBox.SetBorderWidthAll(Mathf.Max(1, _kit.Px(1f)));

		_tag = new PanelContainer { Name = "Tag", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		_tag.AddThemeStyleboxOverride("panel", _tagBox);
		_tagLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, LabelSettings = _tagEquipped };
		_tag.AddChild(_tagLabel);
		return _tag;
	}

	private void Place(Control area, Control child, float y)
	{
		child.Position = new Vector2(0f, _kit.S(y));
		area.AddChild(child);
	}
}
