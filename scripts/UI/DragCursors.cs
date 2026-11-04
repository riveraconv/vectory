using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Cursores de mano abierta y cerrada del arrastre del holograma. Godot no trae esas formas en Windows, así que se
// dibujan aquí y sustituyen a dos formas que el juego no usa para nada más: Drag (abierta) y CanDrop (cerrada).
public static class DragCursors
{
	public const Control.CursorShape Open = Control.CursorShape.Drag;
	public const Control.CursorShape Closed = Control.CursorShape.CanDrop;

	private static readonly string[] OpenHand =
	{
		"........##..........",
		".....##.##.##.......",
		".....##.##.##.##....",
		".....##.##.##.##....",
		".....##.##.##.##....",
		".....##.##.##.##....",
		".##..##.##.##.##....",
		".###.###########....",
		"..###############...",
		"...##############...",
		"...##############...",
		"....#############...",
		"....############....",
		".....###########....",
		"......#########.....",
		"......#########....."
	};

	private static readonly string[] ClosedHand =
	{
		"....................",
		"....................",
		"....................",
		"....................",
		".....##.##.##.......",
		"....############....",
		"....#############...",
		"..###############...",
		"..###############...",
		"...##############...",
		"....#############...",
		"....############....",
		".....###########....",
		"......#########.....",
		"......#########.....",
		"...................."
	};

	public static void Install()
	{
		Set(OpenHand, Open);
		Set(ClosedHand, Closed);
	}

	private static void Set(string[] mask, Control.CursorShape shape)
	{
		int height = mask.Length;
		int width = mask[0].Length;
		const int Margin = 1;   // para el contorno

		Image image = Image.CreateEmpty(width + 2 * Margin, height + 2 * Margin, false, Image.Format.Rgba8);

		// Contorno: los píxeles alrededor de la mano
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (mask[y][x] != '#')
					continue;
				for (int oy = -1; oy <= 1; oy++)
				{
					for (int ox = -1; ox <= 1; ox++)
						image.SetPixel(x + Margin + ox, y + Margin + oy, Palette.DragCursorOutline);
				}
			}
		}
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (mask[y][x] == '#')
					image.SetPixel(x + Margin, y + Margin, Palette.DragCursorFill);
			}
		}

		Input.SetCustomMouseCursor(image, (Input.CursorShape)shape, new Vector2(width * 0.5f, height * 0.5f));
	}
}
