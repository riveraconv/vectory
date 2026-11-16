using Vectory.Game;
using Vectory.Ship;

namespace Vectory.Menu;

// Textos de la pantalla PAINT (spec docs/selector-pinturas.md, 4.2, 4.4 y 4.5; lógica pura, en inglés)
public static class PaintSelectFormat
{
	public const string Title = "PAINT";
	public const string PaintPrefix = "PAINT · ";
	public const string PlainGroup = "PLAIN";
	public const string Equipped = "EQUIPPED";
	public const string Preview = "PREVIEW";

	private const string Separator = " · ";

	// "RACE · CIRCUIT 01 · SELECT SHIP · PAINT"
	public static string Header(GameMode mode, string circuitName) => ShipSelectFormat.Header(mode, circuitName) + Separator + Title;

	// Nombre del grupo con marcas de cada nave
	public static string MarksGroup(HullStyle style) => style switch
	{
		HullStyle.Military => "MARKINGS",
		HullStyle.Industrial => "FLEET MARKINGS",
		_ => "RACING LIVERY"
	};

	// Nombre de la pintura de una de las 40 combinaciones, sin las marcas: "GRAPHITE", "ORANGE · WHITE CAB"
	public static string PaintName(HullStyle style, int index) => style switch
	{
		HullStyle.Military => MilitaryPaintCatalog.Combinations[Wrap(index)].Paint.ScreenName,
		HullStyle.Industrial => IndustrialPaintCatalog.Combinations[Wrap(index)].Paint.ScreenName,
		_ => HullPaintCatalog.Combinations[Wrap(index)].Paint.ScreenName
	};

	// "07 / 40": la posición en las 40 combinaciones
	public static string Counter(int index) => $"{Wrap(index) + 1:00} / {PaintSelectLogic.GroupSize * 2}";

	// Línea de datos: "SATIN · NO. 07 · 21 / 40", "CAMO · MATTE · MARKINGS · 22 / 40", "TWO-TONE · 17 / 40"
	public static string DataLine(HullStyle style, int index)
	{
		int i = Wrap(index);
		string parts = style switch
		{
			HullStyle.Military => MilitaryLine(MilitaryPaintCatalog.Combinations[i]),
			HullStyle.Industrial => IndustrialLine(IndustrialPaintCatalog.Combinations[i]),
			_ => RacingLine(HullPaintCatalog.Combinations[i])
		};
		return parts + Separator + Counter(i);
	}

	public static string Status(bool equipped) => equipped ? Equipped : Preview;

	// "← → ↑ ↓ PAINT · M RACING LIVERY · ENTER APPLY · ESC BACK · DRAG ROTATE"
	public static string Footer(HullStyle style) =>
		$"← → ↑ ↓ PAINT · M {MarksGroup(style)} · ENTER APPLY · ESC BACK · DRAG ROTATE";

	private static string RacingLine(HullPaintChoice choice) =>
		choice.WithLivery
			? choice.Paint.Finish.ScreenName + Separator + "NO. " + choice.Paint.Livery.Number
			: choice.Paint.Finish.ScreenName;

	private static string MilitaryLine(MilitaryPaintChoice choice)
	{
		string finish = (choice.Paint.Satin ? MilitaryFinish.Satin : MilitaryFinish.Matte).ScreenName;
		string line = choice.Paint.IsCamo ? "CAMO" + Separator + finish : finish;
		return choice.WithMarkings ? line + Separator + MarksGroup(HullStyle.Military) : line;
	}

	private static string IndustrialLine(IndustrialPaintChoice choice)
	{
		string line = choice.Paint.IsTwoTone ? "TWO-TONE" : "INDUSTRIAL";
		return choice.WithMarkings ? line + Separator + MarksGroup(HullStyle.Industrial) : line;
	}

	private static int Wrap(int index)
	{
		int n = PaintSelectLogic.GroupSize * 2;
		return (index % n + n) % n;
	}
}
