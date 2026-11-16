using System;
using Vectory.Game;
using Vectory.Ship;

namespace Vectory.Menu;

// Qué tiene que hacer quien usa la pantalla PAINT tras una llamada
public enum PaintSelectAction { None, Apply, Back }

// Dónde está la selección: en las muestras o en APPLY / BACK
public enum PaintSelectFocus { Swatches, Options }

// Lógica de la pantalla PAINT (spec docs/selector-pinturas.md, 4.6), sin Godot: nave, su catálogo, la pintura guardada,
// la muestra seleccionada (una de las 40: 0–19 lisas y 20–39 con marcas, en dos cuadrículas de 5 × 4) y APPLY / BACK.
// No guarda ni pinta nada: devuelve qué hay que hacer.
//
//   Al abrirse: la pintura guardada, con la selección en las muestras.
//   ← / →: muestra anterior / siguiente dentro de su cuadrícula, con vuelta (de la 20 a la 1).
//   ↑ / ↓: fila de arriba / abajo; de la última fila de lisas a la primera de marcas (misma columna) y al revés; desde
//   la última fila de marcas, ↓ pasa a APPLY. En las opciones, ↑ / ↓ entre APPLY y BACK, y ↑ desde APPLY vuelve a la
//   muestra seleccionada.
//   M: lisa ↔ con marcas de la misma pintura. Enter en las muestras: Apply. ESC: Back.
public sealed class PaintSelectLogic
{
	public const string ApplyId = "APPLY";
	public const string BackId = "BACK";

	public const int Columns = 5;
	public const int Rows = 4;
	public const int GroupSize = Columns * Rows;

	public GameMode Mode { get; }

	public string CircuitName { get; }

	public string ShipId { get; }

	public HullStyle Style { get; }

	// La guardada (la equipada) y su posición
	public PaintChoice Saved { get; }

	public int SavedIndex { get; }

	// Muestra seleccionada (0–39): la que se ve en la nave
	public int Index { get; private set; }

	public PaintChoice Selection => HullPaints.ChoiceAt(Style, Index);

	public PaintSelectFocus Focus { get; private set; } = PaintSelectFocus.Swatches;

	public bool SelectionIsEquipped => Index == SavedIndex;

	// APPLY / BACK. La opción seleccionada solo cuenta con la selección en las opciones.
	public MenuModel Menu { get; } = new(new[]
	{
		new MenuOption(ApplyId, "APPLY", true),
		new MenuOption(BackId, "BACK", true)
	});

	public PaintSelectLogic(GameMode mode, string circuitName, string shipId, HullStyle style, PaintChoice saved)
	{
		Mode = mode;
		CircuitName = circuitName;
		ShipId = shipId;
		Style = style;
		if (HullPaints.Count(style) != GroupSize * 2)
			throw new ArgumentException("El catálogo debe tener 40 combinaciones.", nameof(style));

		int index = HullPaints.IndexOf(style, saved);
		SavedIndex = index >= 0 ? index : HullPaints.DefaultIndex(style);
		Saved = HullPaints.ChoiceAt(style, SavedIndex);
		Index = SavedIndex;
	}

	public bool IsEquipped(int index) => index == SavedIndex;

	public void Left() => MoveInGroup(-1);

	public void Right() => MoveInGroup(1);

	public void Up()
	{
		if (Focus == PaintSelectFocus.Options)
		{
			if (Menu.SelectedIndex <= 0)
				Focus = PaintSelectFocus.Swatches;
			else
				Menu.Select(Menu.SelectedIndex - 1);
			return;
		}

		(bool marks, int row, int column) = Cell(Index);
		if (row > 0)
			Index = IndexOf(marks, row - 1, column);
		else if (marks)
			Index = IndexOf(false, Rows - 1, column);
	}

	public void Down()
	{
		if (Focus == PaintSelectFocus.Options)
		{
			if (Menu.SelectedIndex < Menu.Options.Count - 1)
				Menu.Select(Menu.SelectedIndex + 1);
			return;
		}

		(bool marks, int row, int column) = Cell(Index);
		if (row < Rows - 1)
			Index = IndexOf(marks, row + 1, column);
		else if (!marks)
			Index = IndexOf(true, 0, column);
		else
			FocusOptions(0);
	}

	// M: la misma pintura, lisa o con marcas. La selección no cambia de sitio (muestras u opciones).
	public void ToggleMarks() => Index = (Index + GroupSize) % (GroupSize * 2);

	// Clic en una muestra
	public void Select(int index)
	{
		if (index < 0 || index >= GroupSize * 2)
			return;
		Index = index;
		Focus = PaintSelectFocus.Swatches;
	}

	// Ratón sobre una opción
	public void FocusOptions(int option)
	{
		Focus = PaintSelectFocus.Options;
		Menu.Select(option);
	}

	// Enter / Espacio: en las muestras, APPLY; en las opciones, la seleccionada
	public PaintSelectAction Accept() =>
		Focus == PaintSelectFocus.Swatches ? PaintSelectAction.Apply : Choose(Menu.Confirm());

	// ESC
	public PaintSelectAction Back() => PaintSelectAction.Back;

	public PaintSelectAction Choose(string id) => id switch
	{
		ApplyId => PaintSelectAction.Apply,
		BackId => PaintSelectAction.Back,
		_ => PaintSelectAction.None
	};

	private void MoveInGroup(int step)
	{
		if (Focus != PaintSelectFocus.Swatches)
			return;
		int group = Index / GroupSize * GroupSize;
		Index = group + ((Index - group + step) % GroupSize + GroupSize) % GroupSize;
	}

	private static (bool Marks, int Row, int Column) Cell(int index)
	{
		int inGroup = index % GroupSize;
		return (index >= GroupSize, inGroup / Columns, inGroup % Columns);
	}

	private static int IndexOf(bool marks, int row, int column) => (marks ? GroupSize : 0) + row * Columns + column;
}
