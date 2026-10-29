using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Cómo se pinta el título de la franja de fin de vuelta
public enum LapBandTitleStyle
{
	Record,   // NEW RECORD / FIRST RECORD: naranja
	Better,   // delta de la vuelta, mejor o igual que el récord: verde
	Worse     // delta de la vuelta, peor que el récord: rojo
}

// Contenido de la franja de fin de vuelta, ya calculado (la vista no sabe de récords)
public readonly record struct LapBandContent(int Lap, double LapTime, string Title, LapBandTitleStyle Style, string Subtitle);

// TIME TRIAL, arriba en el centro de la pantalla:
//  - Delta por sector: SECTOR N y la diferencia con el récord, en verde (mejor) o rojo (peor).
//  - Franja de fin de vuelta: LAP N y su tiempo | NEW RECORD (o el delta de la vuelta) y un detalle.
// Cada uno se ve VisibleTime segundos y desaparece de golpe; uno nuevo sustituye al anterior.
// Solo MUESTRA lo que le pasan. Va dentro del HUD: con la pausa se congela (y su tiempo también).
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor.
public partial class TimeTrialView : Control
{
	// Posiciones del lienzo de diseño (px sobre 1280×720)
	private const float DeltaTop = 118f;
	private const float LapBandTop = 104f;
	private const float BandSeparatorHeight = 64f;
	private const float BandGap = 40f;

	public float DeltaVisibleTime = 2.5f;
	public float LapBandVisibleTime = 2.5f;
	public float LapBandHeight = 120f;
	public int DeltaFontSize = 44;

	private float _scale = 1f;

	private VBoxContainer _delta;
	private Label _deltaCaption;
	private Label _deltaValue;
	private float _deltaTimer;

	private MenuBand _band;
	private Label _bandLap;
	private Label _bandTime;
	private Label _bandTitle;
	private Label _bandSubtitle;
	private float _bandTimer;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_scale = UiScale.Factor;

		BuildDelta();
		BuildLapBand();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_delta.Visible)
		{
			_deltaTimer -= dt;
			if (_deltaTimer <= 0f)
				_delta.Visible = false;
		}

		if (_band.Visible)
		{
			_bandTimer -= dt;
			if (_bandTimer <= 0f)
				_band.Visible = false;
		}
	}

	// Delta al cruzar un checkpoint. sector: número del sector que acaba de terminar (desde 1).
	public void ShowSectorDelta(int sector, string deltaText, bool better)
	{
		_band.Visible = false;   // un delta nuevo sustituye a lo que hubiera arriba

		_deltaCaption.Text = $"SECTOR {sector}";
		_deltaValue.Text = deltaText;
		_deltaValue.LabelSettings = DeltaSettings(Px(DeltaFontSize), better);
		_delta.Visible = true;
		_deltaTimer = DeltaVisibleTime;
	}

	// Franja al completar una vuelta. Mientras se ve, no hay delta suelto: va dentro de la franja.
	public void ShowLapBand(LapBandContent content)
	{
		_delta.Visible = false;

		_bandLap.Text = $"LAP {content.Lap}";
		_bandTime.Text = TimeFormat.Format(content.LapTime);
		_bandTitle.Text = content.Title;
		_bandTitle.LabelSettings = content.Style == LapBandTitleStyle.Record
			? MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 900, Px(26f), 0.14f), Px(26f), Palette.NewRecord, Palette.NewRecordGlow, S(14f))
			: DeltaSettings(Px(DeltaFontSize), content.Style == LapBandTitleStyle.Better);
		_bandSubtitle.Text = content.Subtitle;
		_band.Visible = true;
		_bandTimer = LapBandVisibleTime;
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private float S(float referencePx) => referencePx * _scale;

	private int Px(float referencePx) => Mathf.RoundToInt(S(referencePx));

	private void BuildDelta()
	{
		_delta = new VBoxContainer
		{
			Name = "Delta",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			OffsetTop = S(DeltaTop)
		};
		_delta.AddThemeConstantOverride("separation", Px(6f));
		AddChild(_delta);

		_deltaCaption = Caption("SECTOR 1");
		_deltaCaption.HorizontalAlignment = HorizontalAlignment.Center;
		_delta.AddChild(_deltaCaption);

		_deltaValue = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		_delta.AddChild(_deltaValue);
	}

	private void BuildLapBand()
	{
		_band = new MenuBand { Name = "LapBand", Visible = false };
		AddChild(_band);
		_band.Setup(LapBandHeight, 0f, 0.9f, LapBandTop + LapBandHeight * 0.5f);

		// Contenido centrado: [LAP N / tiempo] | [título / detalle]
		var center = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.AddThemeConstantOverride("separation", Px(BandGap));
		_band.Band.AddChild(center);

		var left = Column(BoxContainer.AlignmentMode.Center);
		_bandLap = Caption("LAP 1");
		_bandLap.HorizontalAlignment = HorizontalAlignment.Right;
		_bandTime = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 700, Px(44f), 0.06f), Px(44f),
				Palette.Cyan, Palette.PauseTitleGlow, S(14f))
		};
		left.AddChild(_bandLap);
		left.AddChild(_bandTime);
		center.AddChild(left);

		center.AddChild(new ColorRect
		{
			Color = Palette.MenuSeparator,
			CustomMinimumSize = new Vector2(Mathf.Max(1f, _scale), S(BandSeparatorHeight)),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		});

		var right = Column(BoxContainer.AlignmentMode.Center);
		_bandTitle = new Label { MouseFilter = MouseFilterEnum.Ignore };
		_bandSubtitle = new Label
		{
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, Px(16f), 0.1f), Px(16f),
				Palette.BlueGray, Colors.Transparent, 0f)
		};
		right.AddChild(_bandTitle);
		right.AddChild(_bandSubtitle);
		center.AddChild(right);
	}

	private VBoxContainer Column(BoxContainer.AlignmentMode alignment)
	{
		var column = new VBoxContainer { Alignment = alignment, MouseFilter = MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", Px(6f));
		return column;
	}

	// Etiqueta pequeña (SECTOR N, LAP N): Share Tech Mono ~15 px, azul grisáceo
	private Label Caption(string text)
	{
		return new Label
		{
			Text = text,
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, Px(15f), 0.24f), Px(15f),
				Palette.BlueGray, Colors.Transparent, 0f)
		};
	}

	private static LabelSettings DeltaSettings(int sizePx, bool better)
	{
		return MenuFonts.Settings(MenuFonts.Make(MenuFonts.Orbitron, 700, sizePx, 0.06f), sizePx,
			better ? Palette.DeltaBetter : Palette.DeltaWorse,
			better ? Palette.DeltaBetterGlow : Palette.DeltaWorseGlow, sizePx * 14f / 44f);
	}
}
