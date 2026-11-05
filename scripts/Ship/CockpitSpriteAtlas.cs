using System.Collections.Generic;
using Godot;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.Ship;

// Textura común con lo menudo de una cabina (remaches, tornillos, pilotos y ranuras), ya dibujado una vez. Godot agrupa
// en una sola llamada de dibujo los rectángulos seguidos de una misma textura; dibujados como círculos y líneas con
// suavizado, cada uno costaría varias. Qué entra aquí lo decide CockpitSpriteKey.
public sealed class CockpitSpriteAtlas
{
	private const int Supersample = 2;
	private const int Padding = 1;
	private const int RowWidth = 256;

	private readonly Dictionary<CockpitSpriteKey, Rect2I> _cells = new();
	private readonly float _scale;

	public CockpitSpriteAtlas(IEnumerable<CockpitElement> elements, float scale)
	{
		_scale = scale;

		var keys = new List<CockpitSpriteKey>();
		foreach (CockpitElement element in elements)
		{
			if (CockpitSpriteKey.TryFrom(element, out CockpitSpriteKey key) && !keys.Contains(key))
				keys.Add(key);
		}

		// Filas de ancho fijo
		int x = 0, y = 0, rowHeight = 0;
		foreach (CockpitSpriteKey key in keys)
		{
			Vector2I size = CellSize(key);
			if (x + size.X + Padding > RowWidth)
			{
				x = 0;
				y += rowHeight + Padding;
				rowHeight = 0;
			}
			_cells[key] = new Rect2I(x, y, size);
			x += size.X + Padding;
			rowHeight = Mathf.Max(rowHeight, size.Y);
		}

		Image image = Image.CreateEmpty(RowWidth, Mathf.Max(1, y + rowHeight), false, Image.Format.Rgba8);
		foreach ((CockpitSpriteKey key, Rect2I cell) in _cells)
			Paint(image, key, cell);
		Texture = ImageTexture.CreateFromImage(image);
	}

	public Texture2D Texture { get; }

	// Rectángulo en pantalla y en la textura de un elemento, si es de los que van aquí
	public bool TryGetRects(CockpitElement e, System.Numerics.Matrix3x2 toScreen, out Rect2 destination, out Rect2 source)
	{
		destination = default;
		source = default;
		if (!CockpitSpriteKey.TryFrom(e, out CockpitSpriteKey key) || !_cells.TryGetValue(key, out Rect2I cell))
			return false;

		NVec2 center = NVec2.Transform(key.IsLine ? (e.Points[0] + e.Points[1]) * 0.5f : e.Center, toScreen);
		Vector2 size = new Vector2(cell.Size.X, cell.Size.Y) / Supersample;
		destination = new Rect2(new Vector2(center.X, center.Y) - size * 0.5f, size);
		source = new Rect2(cell.Position, cell.Size);
		return true;
	}

	private Vector2I CellSize(CockpitSpriteKey key)
	{
		float pixels = _scale * Supersample;
		if (key.IsLine)
		{
			int width = Mathf.CeilToInt((key.Length + 2f) * pixels);
			int height = Mathf.CeilToInt((key.StrokeWidth + 2f) * pixels);
			return new Vector2I(Even(width), Even(height));
		}
		int side = Mathf.CeilToInt((key.Radius + key.StrokeWidth * 0.5f + 1f) * 2f * pixels);
		return new Vector2I(Even(side), Even(side));
	}

	private static int Even(int value) => value + (value & 1);

	private void Paint(Image image, CockpitSpriteKey key, Rect2I cell)
	{
		float pixels = _scale * Supersample;
		Color fill = key.Fill is { } f ? WithAlpha(CockpitPalette.Get(f), key.FillOpacity) : Colors.Transparent;
		Color stroke = key.Stroke is { } s ? WithAlpha(CockpitPalette.Get(s), key.StrokeOpacity) : Colors.Transparent;
		float strokeHalf = key.StrokeWidth * 0.5f * pixels;

		Color outer = key.Stroke != null ? stroke : fill;   // el color de los bordes transparentes, para que no oscurezcan al filtrar
		Vector2 center = new Vector2(cell.Size.X, cell.Size.Y) * 0.5f;

		for (int py = 0; py < cell.Size.Y; py++)
		{
			for (int px = 0; px < cell.Size.X; px++)
			{
				Vector2 d = new Vector2(px + 0.5f, py + 0.5f) - center;
				Color color;
				if (key.IsLine)
				{
					float along = Coverage(key.Length * 0.5f * pixels - Mathf.Abs(d.X));
					float across = LineCoverage(strokeHalf - Mathf.Abs(d.Y));
					color = WithAlpha(stroke, along * across);
				}
				else
				{
					float radius = key.Radius * pixels;
					float distance = d.Length();
					float inside = radius - distance;
					Color inner = WithAlpha(fill, key.Stroke != null ? Coverage(inside) : LineCoverage(inside));
					color = key.Stroke != null
						? Over(WithAlpha(stroke, Coverage(strokeHalf - Mathf.Abs(distance - radius))), inner)
						: inner;
				}
				if (color.A <= 0f)
					color = WithAlpha(outer, 0f);
				image.SetPixel(cell.Position.X + px, cell.Position.Y + py, color);
			}
		}
	}

	private static float Coverage(float distanceInside) => Mathf.Clamp(distanceInside + 0.5f, 0f, 1f);

	// El suavizado de Godot en líneas y círculos sin borde: opacos hasta el borde y, por fuera, se desvanecen en un píxel de pantalla
	private static float LineCoverage(float distanceInside) => Mathf.Clamp(distanceInside / Supersample + 1f, 0f, 1f);

	private static Color WithAlpha(Color c, float factor) => new(c.R, c.G, c.B, c.A * factor);

	// a encima de b
	private static Color Over(Color a, Color b)
	{
		float alpha = a.A + b.A * (1f - a.A);
		if (alpha <= 0f)
			return Colors.Transparent;
		return new Color(
			(a.R * a.A + b.R * b.A * (1f - a.A)) / alpha,
			(a.G * a.A + b.G * b.A * (1f - a.A)) / alpha,
			(a.B * a.A + b.B * b.A * (1f - a.A)) / alpha,
			alpha);
	}
}
