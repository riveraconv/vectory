using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.UI;

namespace Vectory.Menu;

// Menú principal (capa 2D sobre MenuBackground): título, opciones y versión.
// Dibuja la lista de opciones con OptionList y traduce el teclado a llamadas al MenuModel;
// la navegación vive en el modelo. Ejecuta la acción que devuelve Confirm(): RACE carga la carrera,
// QUIT cierra el juego.
//
// Las medidas (Inspector incluido) son las de la spec, sobre una pantalla de referencia de 1280×720,
// multiplicadas por UiScale.Factor.
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

	private MenuModel _model;
	private OptionList _options;
	private float _scale = 1f;

	public override void _Ready()
	{
		Layer = 2;   // por encima de la sombra izquierda del fondo (capa 1)
		_scale = UiScale.Factor;

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
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("menu_down"))
		{
			_model.MoveNext();
			_options.Refresh();
		}
		else if (Input.IsActionJustPressed("menu_up"))
		{
			_model.MovePrevious();
			_options.Refresh();
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

	private int SizePx(int referencePx) => Mathf.RoundToInt(S(referencePx));

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
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 900, S(TitleFontSize), 0.06f),
				SizePx(TitleFontSize), Palette.MenuTitle, Palette.MenuTitleGlow, S(18f))
		});

		var racingRow = new HBoxContainer { Name = "RacingRow", MouseFilter = Control.MouseFilterEnum.Ignore };
		racingRow.AddThemeConstantOverride("separation", 0);
		column.AddChild(racingRow);

		racingRow.AddChild(new Control { CustomMinimumSize = new Vector2(S(4f), 0f), MouseFilter = Control.MouseFilterEnum.Ignore });   // margen izquierdo
		racingRow.AddChild(new Label
		{
			Name = "Racing",
			Text = "RACING",
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 700, S(RacingFontSize), 0.62f),
				SizePx(RacingFontSize), Palette.MenuRacing, Palette.MenuRacingGlow, S(10f))
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
		_options = new OptionList { Name = "Options" };
		_options.Setup(OptionListStyle.Scaled(_scale, OptionFontSize, OptionRowHeight, OptionSpacing,
			SelectionBarSize, SelectedTextOffset, HintFontSize));
		_options.Position = new Vector2(S(96f), S(264f));
		root.AddChild(_options);

		_options.SetModel(_model);
		_options.Chosen += Execute;
	}

	private void BuildVersion(Control root)
	{
		string version = ProjectSettings.GetSetting("application/config/version", "").AsString();

		var label = new Label
		{
			Name = "Version",
			Text = VersionFormat.Format(version),
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, S(VersionFontSize), 0.18f),
				SizePx(VersionFontSize), Palette.Version, Colors.Transparent, 0f),
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
}
