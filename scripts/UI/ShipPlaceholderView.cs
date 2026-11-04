using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Marco de una nave COMING SOON en SELECT SHIP: rectángulo discontinuo con cuatro esquinas alrededor y dos textos
// dentro. Solo pinta. Rect en píxeles finales, relativo a su padre.
public partial class ShipPlaceholderView : Control
{
	private Vector2 _size;
	private float _corner;
	private float _cornerGap;
	private float _dash;
	private float _width;

	public void Setup(Rect2 rect, float corner, float cornerGap, float dash, float width, Control text)
	{
		_size = rect.Size;
		_corner = corner;
		_cornerGap = cornerGap;
		_dash = dash;
		_width = width;

		MouseFilter = MouseFilterEnum.Ignore;
		Position = rect.Position;
		Size = rect.Size;

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.AddChild(text);
		AddChild(center);
	}

	public override void _Draw()
	{
		Vector2 a = Vector2.Zero;
		var b = new Vector2(_size.X, 0f);
		Vector2 c = _size;
		var d = new Vector2(0f, _size.Y);

		DrawDashedLine(a, b, Palette.ShipPlaceholderFrame, _width, _dash);
		DrawDashedLine(b, c, Palette.ShipPlaceholderFrame, _width, _dash);
		DrawDashedLine(c, d, Palette.ShipPlaceholderFrame, _width, _dash);
		DrawDashedLine(d, a, Palette.ShipPlaceholderFrame, _width, _dash);

		Corner(a + new Vector2(-_cornerGap, -_cornerGap), 1f, 1f);
		Corner(b + new Vector2(_cornerGap, -_cornerGap), -1f, 1f);
		Corner(c + new Vector2(_cornerGap, _cornerGap), -1f, -1f);
		Corner(d + new Vector2(-_cornerGap, _cornerGap), 1f, -1f);
	}

	private void Corner(Vector2 p, float dirX, float dirY)
	{
		DrawLine(p, p + new Vector2(dirX * _corner, 0f), Palette.ShipPlaceholderFrame, _width);
		DrawLine(p, p + new Vector2(0f, dirY * _corner), Palette.ShipPlaceholderFrame, _width);
	}
}
