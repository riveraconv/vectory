using Godot;
using System.Collections.Generic;
using Vectory.Core;
using Vectory.Game;

namespace Vectory.Menu;

// Menú principal (capa 2D sobre MenuBackground): título, opciones y versión.
// Solo DIBUJA el MenuModel y traduce teclado y ratón a sus llamadas; la navegación vive en el modelo.
// Ejecuta la acción que devuelve Confirm(): RACE carga la carrera, QUIT cierra el juego.
//
// Las medidas (Inspector incluido) son las de la spec, sobre una pantalla de referencia de 1280×720.
// Se multiplican por la altura base del proyecto / 720 (1080 → ×1,5) y el stretch del proyecto
// hace el resto, así escalan con cualquier resolución.
public partial class MainMenu : CanvasLayer
{
	public const string RaceId = "RACE";
	public const string QuitId = "QUIT";

	// Opciones del menú, en orden. Activar una es cambiar Enabled y conectar su acción en Execute.
	private static readonly MenuOption[] Options =
	{
		new(RaceId, "RACE", true),
		new("CHAMPIONSHIP", "CHAMPIONSHIP", false),
		new("TIME_TRIAL", "TIME TRIAL", false),
		new("HANGAR", "HANGAR", false),
		new("RECORDS", "RECORDS", false),
		new("SETTINGS", "SETTINGS", false),
		new(QuitId, "QUIT", true)
	};

	private const float ReferenceHeight = 720f;
	private const string OrbitronPath = "res://assets/fonts/Orbitron-Variable.ttf";
	private const string MonoPath = "res://assets/fonts/ShareTechMono-Regular.ttf";

	[Export(PropertyHint.File, "*.tscn")] public string RaceScenePath = SceneChanger.RaceScene;

	[ExportGroup("Tamaños (px sobre 1280×720)")]
	[Export] public int TitleFontSize = 84;
	[Export] public int RacingFontSize = 18;
	[Export] public int OptionFontSize = 28;
	[Export] public int HintFontSize = 13;
	[Export] public int VersionFontSize = 14;
	[Export] public float OptionSpacing = 6f;          // separación vertical entre filas
	[Export] public float OptionRowHeight = 48f;
	[Export] public Vector2 SelectionBarSize = new(4f, 36f);
	[Export] public float SelectedTextOffset = 20f;

	// Una fila del menú y las piezas que cambian con la selección
	private sealed class Row
	{
		public ColorRect Bar;
		public Control Offset;
		public Label Text;
	}

	private readonly List<Row> _rows = new();
	private MenuModel _model;
	private float _scale = 1f;

	private FontFile _orbitron;
	private FontFile _mono;
	private Font _optionSelectedFont;
	private Font _optionFont;
	private LabelSettings _selectedSettings;
	private LabelSettings _idleSettings;
	private LabelSettings _disabledSettings;

	public override void _Ready()
	{
		Layer = 2;   // por encima de la sombra izquierda del fondo (capa 1)

		int baseHeight = ProjectSettings.GetSetting("display/window/size/viewport_height", 720).AsInt32();
		_scale = baseHeight / ReferenceHeight;

		_orbitron = LoadFont(OrbitronPath);
		_mono = LoadFont(MonoPath);

		_model = new MenuModel(Options);

		var root = new Control
		{
			Name = "MenuRoot",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		BuildTitle(root);
		BuildOptions(root);
		BuildVersion(root);

		Refresh();
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("menu_down"))
		{
			_model.MoveNext();
			Refresh();
		}
		else if (Input.IsActionJustPressed("menu_up"))
		{
			_model.MovePrevious();
			Refresh();
		}
		else if (Input.IsActionJustPressed("menu_accept"))
		{
			Execute(_model.Confirm());
		}
	}

	// Acción de cada opción activa
	private void Execute(string id)
	{
		switch (id)
		{
			case RaceId:
				SceneChanger.ChangeTo(this, RaceScenePath);
				break;
			case QuitId:
				SceneChanger.Quit(this);
				break;
		}
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private float S(float referencePx) => referencePx * _scale;

	private void BuildTitle(Control root)
	{
		// Líneas de velocidad a la izquierda de VECTORY, alineadas a la derecha pegadas al título
		AddLine(root, 12f, 10f, 68f, 2f, Palette.MenuSpeedLine);
		AddLine(root, 34f, 26f, 68f, 2f, Palette.Blue);
		AddLine(root, 22f, 42f, 68f, 1.5f, Palette.MenuSpeedLineDim);

		var column = new VBoxContainer { Name = "Title", MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", Mathf.RoundToInt(S(10f)));
		column.Position = new Vector2(S(96f), S(88f));
		root.AddChild(column);

		column.AddChild(new Label
		{
			Name = "Vectory",
			Text = "VECTORY",
			LabelSettings = MakeSettings(MakeFont(_orbitron, 900, TitleFontSize, 0.06f), TitleFontSize,
				Palette.MenuTitle, Palette.MenuTitleGlow, 18f)
		});

		var racingRow = new HBoxContainer { Name = "RacingRow", MouseFilter = Control.MouseFilterEnum.Ignore };
		racingRow.AddThemeConstantOverride("separation", 0);
		column.AddChild(racingRow);

		racingRow.AddChild(new Control { CustomMinimumSize = new Vector2(S(4f), 0f), MouseFilter = Control.MouseFilterEnum.Ignore });   // margen izquierdo
		racingRow.AddChild(new Label
		{
			Name = "Racing",
			Text = "RACING",
			LabelSettings = MakeSettings(MakeFont(_orbitron, 700, RacingFontSize, 0.62f), RacingFontSize,
				Palette.MenuRacing, Palette.MenuRacingGlow, 10f)
		});
		racingRow.AddChild(new Control { CustomMinimumSize = new Vector2(S(18f), 0f), MouseFilter = Control.MouseFilterEnum.Ignore });   // hueco hasta la línea

		// Línea cian que se desvanece hacia la derecha
		var fade = new Gradient();
		fade.SetColor(0, new Color(Palette.Cyan, 0.9f));
		fade.SetColor(1, new Color(Palette.Cyan, 0f));
		racingRow.AddChild(new TextureRect
		{
			Name = "RacingLine",
			Texture = new GradientTexture2D { Gradient = fade, Width = 128, Height = 1 },
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			CustomMinimumSize = new Vector2(S(300f), S(1.5f)),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
	}

	// Una línea de velocidad: x1..x2 e y en el sistema del SVG de referencia (72×60 a 84 px a la izquierda del título)
	private void AddLine(Control root, float x1, float y, float x2, float thickness, Color color)
	{
		float originX = 96f - 84f;
		float originY = 88f + 14f;
		root.AddChild(new ColorRect
		{
			Color = color,
			Position = new Vector2(S(originX + x1), S(originY + y - thickness * 0.5f)),
			Size = new Vector2(S(x2 - x1), S(thickness)),
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
	}

	private void BuildOptions(Control root)
	{
		_optionSelectedFont = MakeFont(_orbitron, 700, OptionFontSize, 0.12f);
		_optionFont = MakeFont(_orbitron, 500, OptionFontSize, 0.12f);
		_selectedSettings = MakeSettings(_optionSelectedFont, OptionFontSize, Palette.MenuSelected, Palette.MenuSelectedGlow, 12f);
		_idleSettings = MakeSettings(_optionFont, OptionFontSize, Palette.MenuIdle, Colors.Transparent, 0f);
		_disabledSettings = MakeSettings(_optionFont, OptionFontSize, Palette.MenuDisabled, Colors.Transparent, 0f);
		var hintSettings = MakeSettings(MakeFont(_mono, 400, HintFontSize, 0.24f), HintFontSize, Palette.MenuHint, Colors.Transparent, 0f);

		var list = new VBoxContainer { Name = "Options", MouseFilter = Control.MouseFilterEnum.Ignore };
		list.AddThemeConstantOverride("separation", Mathf.RoundToInt(S(OptionSpacing)));
		list.Position = new Vector2(S(96f), S(264f));
		root.AddChild(list);

		for (int i = 0; i < _model.Options.Count; i++)
		{
			MenuOption option = _model.Options[i];

			var row = new HBoxContainer
			{
				Name = option.Id,
				CustomMinimumSize = new Vector2(0f, S(OptionRowHeight)),
				// Las desactivadas no reaccionan al ratón
				MouseFilter = option.Enabled ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore,
				MouseDefaultCursorShape = option.Enabled ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow
			};
			row.AddThemeConstantOverride("separation", 0);
			list.AddChild(row);

			// Hueco de la barra: se reserva en todas las filas para que el texto quede alineado
			var barSlot = new CenterContainer
			{
				CustomMinimumSize = new Vector2(S(SelectionBarSize.X), 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			var bar = new ColorRect
			{
				Color = Palette.MenuSelected,
				CustomMinimumSize = new Vector2(S(SelectionBarSize.X), S(SelectionBarSize.Y)),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			barSlot.AddChild(bar);
			row.AddChild(barSlot);

			var offset = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddChild(offset);

			var text = new Label
			{
				Text = option.Label,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			row.AddChild(text);

			if (!option.Enabled)
			{
				row.AddChild(new Control { CustomMinimumSize = new Vector2(S(18f), 0f), MouseFilter = Control.MouseFilterEnum.Ignore });
				row.AddChild(new Label
				{
					Text = "COMING SOON",
					LabelSettings = hintSettings,
					SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
					MouseFilter = Control.MouseFilterEnum.Ignore
				});
			}
			else
			{
				int index = i;
				row.MouseEntered += () => OnHover(index);
				row.GuiInput += e => OnRowInput(index, e);
			}

			_rows.Add(new Row { Bar = bar, Offset = offset, Text = text });
		}
	}

	private void BuildVersion(Control root)
	{
		string version = ProjectSettings.GetSetting("application/config/version", "").AsString();

		var label = new Label
		{
			Name = "Version",
			Text = VersionFormat.Format(version),
			LabelSettings = MakeSettings(MakeFont(_mono, 400, VersionFontSize, 0.18f), VersionFontSize, Palette.Version, Colors.Transparent, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 0f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetLeft = S(100f),
			OffsetTop = -S(44f),
			OffsetBottom = -S(44f),
			GrowVertical = Control.GrowDirection.Begin
		};
		root.AddChild(label);
	}

	// ---------------------------------------------------------------
	// Selección
	// ---------------------------------------------------------------

	private void OnHover(int index)
	{
		_model.Select(index);
		Refresh();
	}

	private void OnRowInput(int index, InputEvent e)
	{
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			_model.Select(index);
			Refresh();
			Execute(_model.Confirm());
		}
	}

	// Aplica el estado del modelo a las filas: barra y desplazamiento al instante, sin animación
	private void Refresh()
	{
		for (int i = 0; i < _rows.Count; i++)
		{
			Row row = _rows[i];
			bool enabled = _model.Options[i].Enabled;
			bool selected = i == _model.SelectedIndex;

			row.Bar.Visible = selected;
			row.Offset.CustomMinimumSize = new Vector2(selected ? S(SelectedTextOffset) : 0f, 0f);
			row.Text.LabelSettings = !enabled ? _disabledSettings : selected ? _selectedSettings : _idleSettings;
		}
	}

	// ---------------------------------------------------------------
	// Fuentes
	// ---------------------------------------------------------------

	private static FontFile LoadFont(string path)
	{
		var font = ResourceLoader.Exists(path) ? GD.Load<FontFile>(path) : null;
		if (font == null)
			GD.PushWarning($"MainMenu: no se encuentra la fuente '{path}'; se usa la fuente por defecto.");
		return font;
	}

	// Fuente con un peso (eje wght de Orbitron) y espaciado entre letras en em (como en la spec)
	private Font MakeFont(FontFile file, int weight, int referenceSize, float letterSpacingEm)
	{
		var variation = new FontVariation
		{
			BaseFont = file ?? ThemeDB.FallbackFont,
			SpacingGlyph = Mathf.RoundToInt(S(referenceSize) * letterSpacingEm)
		};

		if (file != null)
		{
			long wght = TextServerManager.GetPrimaryInterface().NameToTag("wght");
			variation.VariationOpentype = new Godot.Collections.Dictionary { { wght, weight } };
		}

		return variation;
	}

	// Ajustes de texto con halo: una sombra sin desplazamiento del color del halo
	private LabelSettings MakeSettings(Font font, int referenceSize, Color color, Color glow, float glowReferenceSize)
	{
		return new LabelSettings
		{
			Font = font,
			FontSize = Mathf.RoundToInt(S(referenceSize)),
			FontColor = color,
			ShadowColor = glow,
			ShadowSize = Mathf.RoundToInt(S(glowReferenceSize) * 0.5f),
			ShadowOffset = Vector2.Zero
		};
	}
}
