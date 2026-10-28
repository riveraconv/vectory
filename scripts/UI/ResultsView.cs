using Godot;
using Vectory.Circuit;
using Vectory.Core;

namespace Vectory.UI;

// Pantalla de resultados: panel centrado con borde wireframe cian y fondo oscuro
// semitransparente (la pista se sigue viendo detrás). Muestra la posición final,
// la mejor vuelta y el mejor tiempo de cada sector con la vuelta en que se hizo.
// Solo MUESTRA el RaceResult que le pasan; no calcula nada de la carrera.
//
// Es un MarginContainer: el panel toma el tamaño de su contenido. Anclado al centro de
// la pantalla y creciendo hacia los dos lados, queda siempre centrado.
public partial class ResultsView : MarginContainer
{
	private const float PanelWidth = 560f;
	private const int Padding = 32;
	private const float BorderWidth = 2f;

	private VBoxContainer _rows;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(PanelWidth, 0f);
		AddThemeConstantOverride("margin_left", Padding);
		AddThemeConstantOverride("margin_top", Padding);
		AddThemeConstantOverride("margin_right", Padding);
		AddThemeConstantOverride("margin_bottom", Padding);

		_rows = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		_rows.AddThemeConstantOverride("separation", 10);
		AddChild(_rows);
	}

	public void SetResult(RaceResult result)
	{
		foreach (Node child in _rows.GetChildren())
		{
			_rows.RemoveChild(child);
			child.QueueFree();
		}

		AddRow(("RACE FINISHED", 40, Palette.Cyan));
		AddRow(($"{Ordinal(result.Position)} / {result.ParticipantCount}", 60, Palette.Cyan));

		AddRow(
			($"BEST LAP  {TimeFormat.Format(result.BestLapTime)}", 28, Palette.Cyan),
			($"  ·  lap {result.BestLapNumber}", 22, Palette.BlueGray));

		AddRow(("BEST SECTORS", 20, Palette.BlueGray));
		for (int i = 0; i < result.BestSectors.Count; i++)
		{
			SectorBest sector = result.BestSectors[i];
			AddRow(
				($"S{i + 1}  ·  {TimeFormat.Format(sector.Time)}", 24, Palette.Cyan),
				($"  ·  lap {sector.Lap}", 20, Palette.BlueGray));
		}

		AddRow(("ENTER  ·  Restart race", 20, Palette.BlueGray));
	}

	public override void _Draw()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		DrawRect(rect, Palette.ResultsBackground);
		DrawRect(rect, Palette.ResultsBorder, false, BorderWidth);
	}

	// Una fila centrada con uno o varios textos seguidos (cada uno con su tamaño y color)
	private void AddRow(params (string Text, int FontSize, Color Color)[] parts)
	{
		var row = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		row.AddThemeConstantOverride("separation", 0);

		foreach (var part in parts)
		{
			row.AddChild(new Label
			{
				Text = part.Text,
				VerticalAlignment = VerticalAlignment.Bottom,
				MouseFilter = MouseFilterEnum.Ignore,
				LabelSettings = new LabelSettings { FontSize = part.FontSize, FontColor = part.Color }
			});
		}

		_rows.AddChild(row);
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
