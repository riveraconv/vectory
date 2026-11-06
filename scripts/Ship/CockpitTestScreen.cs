using Godot;

namespace Vectory.Ship;

// Imagen de prueba de una pantalla de la cabina 3D (spec docs/cabinas-3d-1-vector.md, 4.6): fondo, líneas de barrido,
// esquinas y un texto grande, para comprobar orientación, tamaño y brillo. La etapa 3 la sustituye por el HUD vivo.
public partial class CockpitTestScreen : Control
{
	private const int ScanSpacing = 3;
	private const float CornerMargin = 14f;
	private const float CornerLength = 20f;
	private const float CornerWidth = 2.5f;
	private const int FontSize = 96;

	public string Text { get; set; } = "";

	public override void _Draw()
	{
		Vector2 size = Size;
		DrawRect(new Rect2(Vector2.Zero, size), Cockpit3DView.ToColor(RacingPalette.ScreenBackground));

		Color scan = Cockpit3DView.ToColor(RacingPalette.Scan, RacingPalette.ScanOpacity);
		for (int y = 0; y < size.Y; y += ScanSpacing)
			DrawRect(new Rect2(0f, y, size.X, 1f), scan);

		Color frame = Cockpit3DView.ToColor(RacingPalette.Frame, RacingPalette.FrameOpacity);
		foreach ((float x, float y, float sx, float sy) in new[]
		{
			(CornerMargin, CornerMargin, 1f, 1f),
			(size.X - CornerMargin, CornerMargin, -1f, 1f),
			(CornerMargin, size.Y - CornerMargin, 1f, -1f),
			(size.X - CornerMargin, size.Y - CornerMargin, -1f, -1f)
		})
		{
			DrawPolyline(new[]
			{
				new Vector2(x, y + sy * CornerLength),
				new Vector2(x, y),
				new Vector2(x + sx * CornerLength, y)
			}, frame, CornerWidth);
		}

		Font font = ThemeDB.FallbackFont;
		int fontSize = Text.Length > 3 ? FontSize / 2 : FontSize;
		Vector2 textSize = font.GetStringSize(Text, HorizontalAlignment.Left, -1f, fontSize);
		var position = new Vector2((size.X - textSize.X) / 2f, (size.Y + textSize.Y * 0.5f) / 2f);
		DrawString(font, position, Text, HorizontalAlignment.Left, -1f, fontSize, Cockpit3DView.ToColor(RacingPalette.Main));
	}
}
