using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Barra de un dato de SHIP DATA: N segmentos, encendidos (relleno cian con halo) o apagados (solo borde). Solo pinta.
public partial class ShipStatBarView : Control
{
	private int _count = 10;
	private int _lit;
	private bool _empty;
	private Vector2 _segment = new(30f, 7f);
	private float _gap = 3f;
	private float _glow = 2f;
	private float _border = 1f;

	// Medidas en píxeles finales. Llamar una vez al crearla.
	public void Setup(int count, Vector2 segment, float gap, float glow, float border)
	{
		_count = count;
		_segment = segment;
		_gap = gap;
		_glow = glow;
		_border = border;
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(count * segment.X + (count - 1) * gap, segment.Y);
	}

	// empty: nave sin datos todavía, con los bordes más tenues
	public void SetValue(int lit, bool empty)
	{
		_lit = lit;
		_empty = empty;
		QueueRedraw();
	}

	public override void _Draw()
	{
		for (int i = 0; i < _count; i++)
		{
			var rect = new Rect2(i * (_segment.X + _gap), 0f, _segment);
			if (i < _lit)
			{
				DrawRect(rect.Grow(_glow), Palette.ShipBarGlow);
				DrawRect(rect, Palette.ShipBarOn);
			}
			else
			{
				DrawRect(rect, _empty ? Palette.ShipBarEmpty : Palette.ShipBarOff, false, _border);
			}
		}
	}
}
