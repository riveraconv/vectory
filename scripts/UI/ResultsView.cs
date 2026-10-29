using System;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Menu;

namespace Vectory.UI;

// Pantalla de resultados: franja horizontal como la de la pausa (MenuBand), con tres columnas:
//   izquierda: RACE FINISHED y la posición final (1st / 8)
//   centro:    tiempo total, mejor vuelta (NEW RECORD si baten el récord de RACE) y mejor tiempo
//              de cada sector, con la vuelta en que se hizo
//   derecha:   opciones RESTART RACE / BACK TO MAIN MENU (OptionList)
// Solo MUESTRA el RaceResult y el MenuModel que le pasan: la acción elegida la ejecuta Main.
//
// Medidas sobre 1280×720, multiplicadas por UiScale.Factor. Ocupa toda la pantalla.
// Si los sectores no caben en el alto de la franja, la franja crece (centrada) en vez de recortar.
public partial class ResultsView : Control
{
	// Posiciones del lienzo de diseño (px sobre 1280×720)
	private const float DimOpacity = 0.45f;
	private const float BandOpacity = 0.9f;
	private const float ResultLeft = 96f;
	private const float TimesLeft = 520f;
	private const float OptionsLeft = 900f;
	private const float RightMargin = 40f;         // hueco mínimo entre las opciones y el borde derecho
	private const float SeparatorHeight = 244f;
	private const float VerticalPadding = 20f;     // hueco mínimo arriba y abajo del contenido antes de que la franja crezca
	private const float SectorNameWidth = 44f;
	private const float SectorTimeWidth = 130f;

	// Se lanza al hacer clic en una opción, con su identificador
	public event Action<string> OptionChosen;

	private float _scale = 1f;
	private MenuBand _band;
	private OptionList _options;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_scale = UiScale.Factor;
	}

	// bandHeight: alto de la franja (px sobre 1280×720). optionFontSize: tamaño máximo de las opciones;
	// si BACK TO MAIN MENU no cabe antes del borde derecho, se reduce.
	// newTotalRecord / newLapRecord: el tiempo total o la mejor vuelta baten el récord de RACE del circuito
	public void SetResult(RaceResult result, MenuModel options, float bandHeight, int optionFontSize,
		bool newTotalRecord = false, bool newLapRecord = false)
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		_band = new MenuBand { Name = "MenuBand" };
		AddChild(_band);
		_band.Setup(bandHeight, DimOpacity, BandOpacity);
		_band.AddSeparator(470f, SeparatorHeight);
		_band.AddSeparator(860f, SeparatorHeight);

		BuildResultColumn(result);
		BuildTimesColumn(result, newTotalRecord, newLapRecord);
		BuildOptionsColumn(options, optionFontSize);

		// El tamaño mínimo de las columnas se conoce cuando ya están montadas
		CallDeferred(MethodName.FitBandHeight);
	}

	// Vuelve a dibujar la selección de las opciones (tras moverla con el teclado)
	public void RefreshOptions()
	{
		_options?.Refresh();
	}

	private float S(float referencePx) => referencePx * _scale;

	private int Px(float referencePx) => Mathf.RoundToInt(S(referencePx));

	// ---------------------------------------------------------------
	// Columnas
	// ---------------------------------------------------------------

	private void BuildResultColumn(RaceResult result)
	{
		VBoxContainer column = _band.AddColumn("Result", ResultLeft, 18f);

		column.AddChild(MakeLabel("RACE FINISHED", MenuFonts.Orbitron, 900, 34f, 0.08f, Palette.Cyan, Palette.MenuTitleGlow, 14f));

		var position = Row(0f);
		position.AddChild(MakeLabel(Ordinal(result.Position), MenuFonts.Orbitron, 700, 80f, 0.04f, Palette.Cyan, Palette.PauseTitleGlow, 16f));
		position.AddChild(MakeLabel($" / {result.ParticipantCount}", MenuFonts.Orbitron, 500, 36f, 0.04f, Palette.BlueGray));
		column.AddChild(position);
	}

	private void BuildTimesColumn(RaceResult result, bool newTotalRecord, bool newLapRecord)
	{
		VBoxContainer column = _band.AddColumn("Times", TimesLeft, 8f);

		// NEW RECORD va en la línea de la etiqueta: junto al tiempo de la mejor vuelta (con "lap N")
		// no cabe en la columna y pisaría las opciones
		column.AddChild(CaptionRow("TOTAL TIME", newTotalRecord));
		column.AddChild(MakeLabel(TimeFormat.Format(result.TotalTime), MenuFonts.Orbitron, 500, 30f, 0.06f, Palette.Cyan));

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, S(6f)), MouseFilter = MouseFilterEnum.Ignore });
		column.AddChild(CaptionRow("BEST LAP", newLapRecord));

		var bestLap = Row(14f);
		bestLap.AddChild(MakeLabel(TimeFormat.Format(result.BestLapTime), MenuFonts.Orbitron, 500, 30f, 0.06f, Palette.Cyan));
		bestLap.AddChild(MakeLabel($"lap {result.BestLapNumber}", MenuFonts.Mono, 400, 16f, 0.08f, Palette.BlueGray));
		column.AddChild(bestLap);

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, S(14f)), MouseFilter = MouseFilterEnum.Ignore });
		column.AddChild(Caption("BEST SECTORS"));

		// Una fila por sector, con sus tres columnas alineadas entre filas (anchos fijos)
		var sectors = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		sectors.AddThemeConstantOverride("separation", Px(4f));
		for (int i = 0; i < result.BestSectors.Count; i++)
		{
			SectorBest sector = result.BestSectors[i];
			var row = Row(0f);

			Label name = MakeLabel($"S{i + 1}", MenuFonts.Mono, 400, 15f, 0.22f, Palette.BlueGray);
			name.CustomMinimumSize = new Vector2(S(SectorNameWidth), 0f);
			row.AddChild(name);

			Label time = MakeLabel(TimeFormat.Format(sector.Time), MenuFonts.Orbitron, 500, 20f, 0.06f, Palette.Cyan);
			time.CustomMinimumSize = new Vector2(S(SectorTimeWidth), 0f);
			row.AddChild(time);

			row.AddChild(MakeLabel($"lap {sector.Lap}", MenuFonts.Mono, 400, 15f, 0.08f, Palette.BlueGray));
			sectors.AddChild(row);
		}
		column.AddChild(sectors);
	}

	private void BuildOptionsColumn(MenuModel options, int optionFontSize)
	{
		VBoxContainer column = _band.AddColumn("Options", OptionsLeft, 0f);

		_options = new OptionList { Name = "OptionList" };
		_options.Setup(OptionListStyle.Scaled(_scale, FitOptionFontSize(options, optionFontSize),
			46f, 4f, new Vector2(4f, 32f), 16f));
		column.AddChild(_options);
		_options.SetModel(options);
		_options.Chosen += id => OptionChosen?.Invoke(id);
	}

	// Mayor tamaño (como mucho maxSize) con el que las opciones caben antes del borde derecho de la pantalla
	private int FitOptionFontSize(MenuModel options, int maxSize)
	{
		int size = OptionList.FitFontSize(options, maxSize, _scale, S(1280f - OptionsLeft - RightMargin));
		if (size < maxSize)
			GD.Print($"ResultsView: opciones reducidas a {size} px para que quepan en pantalla.");
		return size;
	}

	// Si alguna columna no cabe en la franja (circuitos con muchos sectores), la franja crece centrada
	private void FitBandHeight()
	{
		float needed = 0f;
		foreach (Node child in _band.Band.GetChildren())
		{
			if (child is VBoxContainer column)
				needed = Mathf.Max(needed, column.GetCombinedMinimumSize().Y);
		}

		needed += 2f * S(VerticalPadding);
		if (needed > _band.BandHeight)
			_band.SetHeight(needed);
	}

	// ---------------------------------------------------------------
	// Piezas
	// ---------------------------------------------------------------

	// Etiqueta de sección con, si toca, NEW RECORD a su derecha
	private Control CaptionRow(string text, bool newRecord)
	{
		if (!newRecord)
			return Caption(text);

		var row = Row(14f);
		row.AddChild(Caption(text));
		row.AddChild(MakeLabel("NEW RECORD", MenuFonts.Orbitron, 900, 16f, 0.14f, Palette.NewRecord, Palette.NewRecordGlow, 10f));
		return row;
	}

	// Etiqueta pequeña de sección (TOTAL TIME, BEST LAP, BEST SECTORS)
	private Label Caption(string text)
	{
		return MakeLabel(text, MenuFonts.Mono, 400, 14f, 0.22f, Palette.BlueGray);
	}

	// Fila de textos alineados por abajo, con un hueco (px sobre 1280×720) entre ellos
	private HBoxContainer Row(float gap)
	{
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", Px(gap));
		return row;
	}

	// Texto con su fuente (peso y espaciado en em), tamaño (px sobre 1280×720), color y halo opcional
	private Label MakeLabel(string text, FontFile file, int weight, float size, float letterSpacingEm,
		Color color, Color? glow = null, float glowSize = 0f)
	{
		int px = Px(size);
		return new Label
		{
			Text = text,
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(file, weight, px, letterSpacingEm), px,
				color, glow ?? Colors.Transparent, S(glowSize))
		};
	}

	private static string Ordinal(int n)
	{
		int lastTwo = n % 100;
		if (lastTwo >= 11 && lastTwo <= 13)
			return $"{n}th";

		return (n % 10) switch
		{
			1 => $"{n}st",
			2 => $"{n}nd",
			3 => $"{n}rd",
			_ => $"{n}th"
		};
	}
}
