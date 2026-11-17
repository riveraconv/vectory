using System;
using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Settings;

namespace Vectory.UI;

// Dónde ha hecho clic el ratón en una fila de SETTINGS
public enum SettingsClick
{
	Row,
	ArrowLeft,
	ArrowRight,
	Segment,    // con Value = segmento (1–10)
	Toggle,     // con Value = 0 (OFF) o 1 (ON)
	Key
}

// La lista de SETTINGS del panel derecho (spec docs/settings.md, 3.1 y 3.2; designs/settings.html, LAYOUTS.C): cabecera
// de cada grupo y una fila de 32 px por ajuste, con su nombre a la izquierda y su control a la derecha (‹ VALOR ›,
// 10 segmentos con el porcentaje, OFF / ON o la tecla). La elegida, en cian con barra y degradado; las de COMING SOON,
// atenuadas. Se desplaza para que la elegida se vea siempre, con un margen. Solo dibuja y traduce el ratón: los valores
// los lleva SettingsModel. Medidas sobre 1280×720 × la escala; va dentro de un recorte del alto de la lista.
public partial class SettingsListView : Control
{
	private const float GroupHeight = 30f;
	private const float PadLeft = 14f;
	private const float PadRight = 10f;
	private const float ChoiceWidth = 108f;
	private const float ControlGap = 7f;
	private const float SegmentSize = 9f;
	private const float SegmentGap = 3f;
	private const float PercentWidth = 44f;
	private const float KeyMinWidth = 62f;

	private readonly record struct Entry(SettingDef Def, string Group, float Top, float Height);
	private readonly record struct Hit(string Id, SettingsClick Click, int Value, Rect2 Rect);

	private readonly List<Entry> _entries = new();
	private readonly List<Hit> _hits = new();
	private float _scale = 1f;
	private Font _label;
	private Font _value;
	private Font _small;
	private int _labelSize, _valueSize, _smallSize, _toggleSize;
	private SettingsModel _model;
	private string _selected;
	private bool _listening;
	private float _scroll;
	private GradientTexture2D _selectedFade;

	public float RowHeight { get; set; } = 32f;
	public float ScrollMargin { get; set; } = 40f;

	public event Action<string> RowHovered;
	public event Action<string, SettingsClick, int> Clicked;
	public event Action<int> Wheel;   // −1 arriba, +1 abajo

	private float S(float px) => px * _scale;

	public void Setup(float scale)
	{
		_scale = scale;
		_labelSize = Mathf.RoundToInt(S(13f));
		_valueSize = Mathf.RoundToInt(S(13f));
		_smallSize = Mathf.RoundToInt(S(11f));
		_toggleSize = Mathf.RoundToInt(S(11f));
		_label = MenuFonts.Make(MenuFonts.Orbitron, 500, _labelSize, 0.12f);
		_value = MenuFonts.Make(MenuFonts.Mono, 400, _valueSize, 0.14f);
		_small = MenuFonts.Make(MenuFonts.Mono, 400, _smallSize, 0.22f);
		MouseFilter = MouseFilterEnum.Stop;
		var fade = new Gradient();
		fade.SetColor(0, Palette.SettingsRowGradient);
		fade.SetColor(1, new Color(Palette.SettingsRowGradient, 0f));
		fade.SetOffset(1, 0.85f);
		_selectedFade = new GradientTexture2D { Gradient = fade, Width = 64, Height = 1 };

		float y = 0f;
		string group = null;
		foreach (SettingDef def in SettingsCatalog.All)
		{
			if (def.Group != group)
			{
				group = def.Group;
				_entries.Add(new Entry(null, group, y, S(GroupHeight)));
				y += S(GroupHeight);
			}
			_entries.Add(new Entry(def, group, y, S(RowHeight)));
			y += S(RowHeight);
		}
		ContentHeight = y;
	}

	public float ContentHeight { get; private set; }

	// Valores y fila elegida (null: ninguna, en RESET DEFAULTS o BACK); desplaza para que la elegida se vea.
	// listening: la elegida espera su tecla nueva (PRESS A KEY)
	public void Show(SettingsModel model, string selectedId, bool listening = false)
	{
		_model = model;
		_selected = selectedId;
		_listening = listening;
		foreach (Entry e in _entries)
		{
			if (e.Def == null || e.Def.Id != selectedId)
				continue;
			float margin = S(ScrollMargin);
			if (e.Top - _scroll < margin)
				_scroll = Mathf.Max(0f, e.Top - margin);
			if (e.Top + e.Height - _scroll > Size.Y - margin)
				_scroll = e.Top + e.Height - Size.Y + margin;
			_scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, ContentHeight - Size.Y));
		}
		QueueRedraw();
	}

	// PRESS A KEY parpadea (0,6 s, dos pasos, como la página)
	private const float WaitBlinkPeriod = 0.6f;
	private float _waitTime;
	private bool _waitOn = true;

	public override void _Process(double delta)
	{
		if (!_listening)
		{
			_waitTime = 0f;
			_waitOn = true;
			return;
		}
		_waitTime += (float)delta;
		bool on = _waitTime % WaitBlinkPeriod < WaitBlinkPeriod * 0.5f;
		if (on == _waitOn)
			return;
		_waitOn = on;
		QueueRedraw();
	}

	public override void _Draw()
	{
		_hits.Clear();
		if (_model == null)
			return;
		float width = Size.X;
		foreach (Entry e in _entries)
		{
			float top = e.Top - _scroll;
			if (top + e.Height < 0f || top > Size.Y)
				continue;
			if (e.Def == null)
				DrawGroup(e.Group, top, e.Height);
			else
				DrawRow(e.Def, top, e.Height, width);
		}
	}

	private void DrawGroup(string group, float top, float height)
	{
		float baseline = top + height - S(6f);
		DrawString(_small, new Vector2(S(PadLeft), baseline), group, HorizontalAlignment.Left, -1, _smallSize, Palette.SettingsGroupHeader);
		if (SettingsCatalog.GroupComingSoon(group))
		{
			float x = S(PadLeft) + _small.GetStringSize(group, HorizontalAlignment.Left, -1, _smallSize).X + S(12f);
			DrawString(_small, new Vector2(x, baseline), "COMING SOON", HorizontalAlignment.Left, -1, _smallSize, Palette.SettingsComingSoon);
		}
	}

	private void DrawRow(SettingDef def, float top, float height, float width)
	{
		bool selected = def.Id == _selected;
		float alpha = def.ComingSoon ? 0.45f : 1f;
		if (selected)
		{
			DrawTextureRect(_selectedFade, new Rect2(0f, top, width, height), false);
			Color bar = Palette.SettingsRowSelected;
			DrawRect(new Rect2(-S(2f), top + S(3f), S(7f), height - S(6f)), new Color(bar, 0.25f));
			DrawRect(new Rect2(0f, top + S(5f), S(3f), height - S(10f)), bar);
		}

		float center = top + height * 0.5f;
		float labelBaseline = center + _labelSize * 0.36f;
		Color labelColor = selected ? Palette.SettingsRowSelected : Faded(Palette.SettingsRow, alpha);
		DrawString(_label, new Vector2(S(PadLeft), labelBaseline), def.Label, HorizontalAlignment.Left, -1, _labelSize, labelColor);

		float right = width - S(PadRight);
		if (def.ComingSoon)
		{
			DrawRight(_small, "COMING SOON", right, center, _smallSize, Palette.SettingsComingSoon);
			return;
		}
		_hits.Add(new Hit(def.Id, SettingsClick.Row, 0, new Rect2(0f, top, width, height)));

		switch (def.Kind)
		{
			case SettingKind.Choice:
				DrawChoice(def, right, center, selected);
				break;
			case SettingKind.Percent:
				DrawPercent(def, right, center);
				break;
			case SettingKind.Toggle:
				DrawToggle(def, right, center);
				break;
			case SettingKind.Key:
				DrawKey(def, right, center);
				break;
		}
	}

	private void DrawChoice(SettingDef def, float right, float center, bool selected)
	{
		int value = _model.Get(def.Id);
		Color arrow = selected ? Palette.SettingsRowSelected : Palette.SettingsArrow;
		float arrowWidth = _value.GetStringSize("›", HorizontalAlignment.Left, -1, _valueSize).X + S(8f);
		float x = right - arrowWidth;
		if (value < def.Max)
		{
			DrawCentered(_value, "›", x, arrowWidth, center, _valueSize, arrow);
			AddHit(def.Id, SettingsClick.ArrowRight, 0, x, center, arrowWidth);
		}
		x -= S(ControlGap) + S(ChoiceWidth);
		DrawCentered(_value, def.Values[value], x, S(ChoiceWidth), center, _valueSize, Palette.SettingsRowValue);
		x -= S(ControlGap) + arrowWidth;
		if (value > 0)
		{
			DrawCentered(_value, "‹", x, arrowWidth, center, _valueSize, arrow);
			AddHit(def.Id, SettingsClick.ArrowLeft, 0, x, center, arrowWidth);
		}
	}

	private void DrawPercent(SettingDef def, float right, float center)
	{
		int value = _model.Get(def.Id);
		DrawRight(_value, $"{value * 10} %", right, center, _valueSize, Palette.SettingsRowValue);
		float x = right - S(PercentWidth) - S(ControlGap) - SettingsCatalog.PercentSteps * (S(SegmentSize) + S(SegmentGap)) + S(SegmentGap);
		float size = S(SegmentSize);
		for (int k = 1; k <= SettingsCatalog.PercentSteps; k++)
		{
			var rect = new Rect2(x, center - size * 0.5f, size, size);
			if (k <= value)
			{
				DrawRect(rect.Grow(S(1.5f)), new Color(Palette.SettingsSegmentGlow, 0.35f));
				DrawRect(rect, Palette.SettingsRowSelected);
			}
			else
			{
				DrawRect(rect, Palette.SettingsSegmentOff, false, Mathf.Max(1f, S(1f)));
			}
			_hits.Add(new Hit(def.Id, SettingsClick.Segment, k, rect.Grow(S(1.5f))));
			x += size + S(SegmentGap);
		}
	}

	private void DrawToggle(SettingDef def, float right, float center)
	{
		bool on = _model.Get(def.Id) != 0;
		float h = _toggleSize + S(4f);
		float onWidth = _value.GetStringSize("ON", HorizontalAlignment.Left, -1, _toggleSize).X + S(18f);
		float offWidth = _value.GetStringSize("OFF", HorizontalAlignment.Left, -1, _toggleSize).X + S(18f);
		var onRect = new Rect2(right - onWidth, center - h * 0.5f, onWidth, h);
		var offRect = new Rect2(onRect.Position.X - offWidth, onRect.Position.Y, offWidth, h);
		DrawPill(offRect, "OFF", !on);
		DrawPill(onRect, "ON", on);
		_hits.Add(new Hit(def.Id, SettingsClick.Toggle, 0, offRect));
		_hits.Add(new Hit(def.Id, SettingsClick.Toggle, 1, onRect));
	}

	private void DrawPill(Rect2 rect, string text, bool chosen)
	{
		if (chosen)
			DrawRect(rect, Palette.SettingsRowSelected);
		else
			DrawRect(rect, Palette.SettingsSegmentOff, false, Mathf.Max(1f, S(1f)));
		DrawCentered(_value, text, rect.Position.X, rect.Size.X, rect.GetCenter().Y, _toggleSize,
			chosen ? Palette.SettingsToggleOnText : Palette.SettingsRowValue);
	}

	private void DrawKey(SettingDef def, float right, float center)
	{
		bool fixedKey = KeyBindings.Find(def.Id)?.Fixed ?? true;
		bool waiting = _listening && def.Id == _selected;
		string key = waiting ? "PRESS A KEY" : _model.ValueText(def.Id);
		float w = Mathf.Max(S(KeyMinWidth), _value.GetStringSize(key, HorizontalAlignment.Left, -1, _valueSize).X + S(16f));
		float h = _valueSize + S(4f);
		var rect = new Rect2(right - w, center - h * 0.5f, w, h);
		_hits.Add(new Hit(def.Id, SettingsClick.Key, 0, rect));
		if (waiting && !_waitOn)
			return;   // el paso apagado del parpadeo
		Color border = waiting ? Palette.SettingsKeyWaiting : fixedKey ? Palette.SettingsKeyFixed : Palette.SettingsKeyBorder;
		Color text = waiting ? Palette.SettingsKeyWaiting : fixedKey ? Palette.SettingsKeyFixedText : Palette.SettingsRowValue;
		DrawRect(rect, border, false, Mathf.Max(1f, S(1f)));
		DrawCentered(_value, key, rect.Position.X, w, center, _valueSize, text);
	}

	private void AddHit(string id, SettingsClick click, int value, float x, float center, float width) =>
		_hits.Add(new Hit(id, click, value, new Rect2(x, center - S(RowHeight) * 0.5f, width, S(RowHeight))));

	private void DrawRight(Font font, string text, float right, float center, int size, Color color)
	{
		float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
		DrawString(font, new Vector2(right - w, center + size * 0.36f), text, HorizontalAlignment.Left, -1, size, color);
	}

	private void DrawCentered(Font font, string text, float x, float width, float center, int size, Color color) =>
		DrawString(font, new Vector2(x, center + size * 0.36f), text, HorizontalAlignment.Center, width, size, color);

	private static Color Faded(Color c, float alpha) => new(c, c.A * alpha);

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			string id = RowAt(motion.Position);
			if (id != null && id != _selected)
				RowHovered?.Invoke(id);
		}
		else if (@event is InputEventMouseButton { Pressed: true } button)
		{
			if (button.ButtonIndex == MouseButton.WheelUp)
				Wheel?.Invoke(-1);
			else if (button.ButtonIndex == MouseButton.WheelDown)
				Wheel?.Invoke(1);
			else if (button.ButtonIndex == MouseButton.Left)
			{
				// Los controles van después de su fila en la lista: el último que contiene el punto es el más concreto
				for (int i = _hits.Count - 1; i >= 0; i--)
				{
					if (_hits[i].Rect.HasPoint(button.Position))
					{
						Clicked?.Invoke(_hits[i].Id, _hits[i].Click, _hits[i].Value);
						break;
					}
				}
			}
		}
	}

	private string RowAt(Vector2 position)
	{
		foreach (Hit hit in _hits)
		{
			if (hit.Click == SettingsClick.Row && hit.Rect.HasPoint(position))
				return hit.Id;
		}
		return null;
	}
}
