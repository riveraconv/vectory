using System;
using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Menu;

namespace Vectory.UI;

// Medidas de una lista de opciones, en píxeles finales (ya escalados)
public struct OptionListStyle
{
	public int FontSize;
	public float RowHeight;
	public int Spacing;             // separación vertical entre filas
	public Vector2 BarSize;         // barra lateral de la opción seleccionada
	public float SelectedOffset;    // desplazamiento del texto seleccionado
	public float GlowSize;          // halo del texto seleccionado
	public int HintFontSize;        // COMING SOON
	public float HintGap;           // hueco entre la opción y COMING SOON
	public bool HideDisabledHint;   // las desactivadas sin COMING SOON (p. ej. RESET RECORDS sin récords)

	// Medidas de una spec (px sobre 1280×720) multiplicadas por la escala
	public static OptionListStyle Scaled(float scale, float fontSize, float rowHeight, float spacing,
		Vector2 barSize, float selectedOffset, float hintFontSize = 13f)
	{
		return new OptionListStyle
		{
			FontSize = Mathf.RoundToInt(fontSize * scale),
			RowHeight = rowHeight * scale,
			Spacing = Mathf.RoundToInt(spacing * scale),
			BarSize = barSize * scale,
			SelectedOffset = selectedOffset * scale,
			GlowSize = 12f * scale,
			HintFontSize = Mathf.RoundToInt(hintFontSize * scale),
			HintGap = 18f * scale
		};
	}
}

// Lista vertical de opciones con el aspecto de los menús: barra lateral cian y texto desplazado
// en la seleccionada, azul grisáceo en las activas y azul atenuado + COMING SOON en las desactivadas.
// Solo DIBUJA un MenuModel: el ratón selecciona (hover) y elige (clic) a través del modelo;
// el teclado lo gestiona quien la usa, que después llama a Refresh(). Elegir lanza Chosen.
//
// La usan el menú principal, la pausa y la pantalla de resultados.
public partial class OptionList : VBoxContainer
{
	public const string ComingSoon = "COMING SOON";

	// Se lanza al hacer clic en una opción activa, con su identificador
	public event Action<string> Chosen;

	// Una fila y las piezas que cambian con la selección
	private sealed class Row
	{
		public ColorRect Bar;
		public Control Offset;
		public Label Text;
	}

	private readonly List<Row> _rows = new();
	private OptionListStyle _style;
	private MenuModel _model;

	private LabelSettings _selectedSettings;
	private LabelSettings _idleSettings;
	private LabelSettings _disabledSettings;
	private LabelSettings _hintSettings;

	public MenuModel Model => _model;

	// Mayor tamaño de fuente (px sobre 1280×720, como mucho maxSize) con el que la opción más larga,
	// seleccionada (en negrita, con la barra y el desplazamiento), cabe en maxWidth px finales.
	public static int FitFontSize(MenuModel options, int maxSize, float scale, float maxWidth,
		float barWidth = 4f, float selectedOffset = 16f)
	{
		for (int size = maxSize; size > 8; size--)
		{
			int px = Mathf.RoundToInt(size * scale);
			Font font = MenuFonts.Make(MenuFonts.Orbitron, 700, px, 0.12f);
			float widest = 0f;
			foreach (MenuOption option in options.Options)
				widest = Mathf.Max(widest, font.GetStringSize(option.Label, HorizontalAlignment.Left, -1, px).X);

			if ((barWidth + selectedOffset) * scale + widest <= maxWidth)
				return size;
		}
		return 8;
	}

	// Prepara los estilos. Llamar una vez antes de SetModel.
	public void Setup(OptionListStyle style)
	{
		_style = style;
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeConstantOverride("separation", style.Spacing);

		Font selectedFont = MenuFonts.Make(MenuFonts.Orbitron, 700, style.FontSize, 0.12f);
		Font font = MenuFonts.Make(MenuFonts.Orbitron, 500, style.FontSize, 0.12f);
		_selectedSettings = MenuFonts.Settings(selectedFont, style.FontSize, Palette.MenuSelected, Palette.MenuSelectedGlow, style.GlowSize);
		_idleSettings = MenuFonts.Settings(font, style.FontSize, Palette.MenuIdle, Colors.Transparent, 0f);
		_disabledSettings = MenuFonts.Settings(font, style.FontSize, Palette.MenuDisabled, Colors.Transparent, 0f);
		_hintSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, style.HintFontSize, 0.24f),
			style.HintFontSize, Palette.MenuHint, Colors.Transparent, 0f);
	}

	// Cambia las opciones que se muestran (por ejemplo, de la pausa a la confirmación)
	public void SetModel(MenuModel model)
	{
		_model = model;

		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
		_rows.Clear();

		for (int i = 0; i < model.Options.Count; i++)
			_rows.Add(BuildRow(model.Options[i], i));

		Refresh();
	}

	// Aplica la selección del modelo: barra y desplazamiento al instante, sin animación
	public void Refresh()
	{
		if (_model == null)
			return;

		for (int i = 0; i < _rows.Count; i++)
		{
			Row row = _rows[i];
			bool enabled = _model.Options[i].Enabled;
			bool selected = i == _model.SelectedIndex;

			row.Bar.Visible = selected;
			row.Offset.CustomMinimumSize = new Vector2(selected ? _style.SelectedOffset : 0f, 0f);
			row.Text.LabelSettings = !enabled ? _disabledSettings : selected ? _selectedSettings : _idleSettings;
		}
	}

	private Row BuildRow(MenuOption option, int index)
	{
		var row = new HBoxContainer
		{
			Name = option.Id,
			CustomMinimumSize = new Vector2(0f, _style.RowHeight),
			// Las desactivadas no reaccionan al ratón
			MouseFilter = option.Enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore,
			MouseDefaultCursorShape = option.Enabled ? CursorShape.PointingHand : CursorShape.Arrow
		};
		row.AddThemeConstantOverride("separation", 0);
		AddChild(row);

		// Hueco de la barra: se reserva en todas las filas para que el texto quede alineado
		var barSlot = new CenterContainer
		{
			CustomMinimumSize = new Vector2(_style.BarSize.X, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		var bar = new ColorRect
		{
			Color = Palette.MenuSelected,
			CustomMinimumSize = _style.BarSize,
			MouseFilter = MouseFilterEnum.Ignore
		};
		barSlot.AddChild(bar);
		row.AddChild(barSlot);

		var offset = new Control { MouseFilter = MouseFilterEnum.Ignore };
		row.AddChild(offset);

		var text = new Label
		{
			Text = option.Label,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddChild(text);

		if (!option.Enabled && !_style.HideDisabledHint)
		{
			row.AddChild(new Control { CustomMinimumSize = new Vector2(_style.HintGap, 0f), MouseFilter = MouseFilterEnum.Ignore });
			row.AddChild(new Label
			{
				Text = ComingSoon,
				LabelSettings = _hintSettings,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			});
		}
		if (option.Enabled)
		{
			row.MouseEntered += () => OnHover(index);
			row.GuiInput += e => OnRowInput(index, e);
		}

		return new Row { Bar = bar, Offset = offset, Text = text };
	}

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
			string id = _model.Confirm();
			if (id != null)
				Chosen?.Invoke(id);
		}
	}
}
