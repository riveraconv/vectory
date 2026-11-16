using System;
using Godot;
using Vectory.Core;
using Vectory.Menu;

namespace Vectory.UI;

// Una cuadrícula de 5 × 4 muestras de la pantalla PAINT (spec docs/selector-pinturas.md, 4.3), sin reglas: el fondo
// fijo de cada muestra llega hecho en una textura (PaintSwatchDrawing) y aquí se dibujan encima la barra de acento, el
// dorsal, el borde (cian bajo el ratón), el rombo de la equipada y el contorno de la seleccionada. Solo se redibuja
// cuando cambia algo. Clic en una muestra -> Clicked con su posición en las 40.
public partial class PaintSwatchGridView : Control
{
	private static readonly float ItalicSkew = Mathf.Tan(Mathf.DegToRad(12f));

	// Se lanza al hacer clic en una muestra, con su posición en las 40
	public event Action<int> Clicked;

	private float _scale = 1f;
	private Vector2 _cell;          // tamaño de una muestra, en píxeles de pantalla
	private float _gap;
	private int _first;             // posición en las 40 de la primera muestra (0 lisas, 20 con marcas)
	private Texture2D[] _textures = Array.Empty<Texture2D>();
	private PaintSwatch[] _swatches = Array.Empty<PaintSwatch>();
	private Font _numberFont;

	private int _selected = -1;
	private int _equipped = -1;
	private int _hover = -1;

	// size y gap sobre 1280×720
	public void Setup(float scale, Vector2 size, float gap, int first)
	{
		_scale = scale;
		_cell = size * scale;
		_gap = gap * scale;
		_first = first;
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		CustomMinimumSize = new Vector2(
			PaintSelectLogic.Columns * _cell.X + (PaintSelectLogic.Columns - 1) * _gap,
			PaintSelectLogic.Rows * _cell.Y + (PaintSelectLogic.Rows - 1) * _gap);
		Size = CustomMinimumSize;

		// Orbitron Black en cursiva, 10 px
		var font = (FontVariation)MenuFonts.Make(MenuFonts.Orbitron, 900, 10f * scale, 0f);
		font.VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-ItalicSkew, 1f), Vector2.Zero);
		_numberFont = font;
		MouseExited += () => SetHover(-1);
	}

	// Las 20 muestras de la cuadrícula: su descripción y su fondo ya pintado
	public void SetSwatches(PaintSwatch[] swatches, Texture2D[] textures)
	{
		_swatches = swatches;
		_textures = textures;
		QueueRedraw();
	}

	// Seleccionada y equipada, como posiciones en las 40 (-1 = ninguna en esta cuadrícula)
	public void SetState(int selected, int equipped)
	{
		if (selected == _selected && equipped == _equipped)
			return;
		_selected = selected;
		_equipped = equipped;
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseMotion motion)
			SetHover(CellAt(motion.Position));
		else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
		{
			int cell = CellAt(button.Position);
			if (cell >= 0)
			{
				Clicked?.Invoke(_first + cell);
				AcceptEvent();
			}
		}
	}

	private void SetHover(int cell)
	{
		if (cell == _hover)
			return;
		_hover = cell;
		QueueRedraw();
	}

	private int CellAt(Vector2 p)
	{
		int column = (int)(p.X / (_cell.X + _gap));
		int row = (int)(p.Y / (_cell.Y + _gap));
		if (column < 0 || column >= PaintSelectLogic.Columns || row < 0 || row >= PaintSelectLogic.Rows || p.X < 0f || p.Y < 0f)
			return -1;
		// En el hueco entre muestras no hay ninguna
		if (p.X - column * (_cell.X + _gap) > _cell.X || p.Y - row * (_cell.Y + _gap) > _cell.Y)
			return -1;
		return row * PaintSelectLogic.Columns + column;
	}

	private Rect2 CellRect(int cell) => new(
		new Vector2(cell % PaintSelectLogic.Columns * (_cell.X + _gap), cell / PaintSelectLogic.Columns * (_cell.Y + _gap)), _cell);

	private float S(float px) => px * _scale;

	public override void _Draw()
	{
		for (int i = 0; i < _swatches.Length; i++)
			DrawSwatch(i);

		// La seleccionada, encima de todo: contorno de 2 px separado 2 px, con halo
		int selected = _selected - _first;
		if (selected >= 0 && selected < _swatches.Length)
		{
			Rect2 rect = CellRect(selected).Grow(S(3f));
			for (int k = 4; k >= 1; k--)
				DrawRect(rect.Grow(S(2f * k)), new Color(Palette.PaintSwatchSelectedGlow, Palette.PaintSwatchSelectedGlow.A * 0.12f), false, S(4f));
			DrawRect(rect, Palette.PaintSwatchSelected, false, Mathf.Max(1f, S(2f)));
		}
	}

	private void DrawSwatch(int i)
	{
		Rect2 rect = CellRect(i);
		PaintSwatch swatch = _swatches[i];
		if (i < _textures.Length && _textures[i] != null)
			DrawTextureRect(_textures[i], rect, false);

		// Barra de acento (VECTOR): a 4 px de los bordes, 3 px de alto, con halo
		if (swatch.AccentBar is uint accent)
		{
			Color color = ToColor(accent);
			var bar = new Rect2(rect.Position + new Vector2(S(4f), rect.Size.Y - S(7f)), new Vector2(rect.Size.X - S(8f), S(3f)));
			for (int k = 3; k >= 1; k--)
				DrawRect(bar.Grow(S(1.5f * k)), new Color(color, 0.12f));
			DrawRect(bar, color);
		}

		// Dorsal (VECTOR con librea): arriba a la derecha, Orbitron Black en cursiva
		if (swatch.Number != null)
		{
			float width = _numberFont.GetStringSize(swatch.Number, HorizontalAlignment.Left, -1f, Mathf.RoundToInt(S(10f))).X;
			var baseline = new Vector2(rect.End.X - S(3f) - width, rect.Position.Y + S(1f) + _numberFont.GetAscent(Mathf.RoundToInt(S(10f))));
			DrawString(_numberFont, baseline, swatch.Number, HorizontalAlignment.Left, -1f, Mathf.RoundToInt(S(10f)), ToColor(swatch.NumberColor));
		}

		// Borde de 1 px por dentro
		float border = Mathf.Max(1f, S(1f));
		DrawRect(new Rect2(rect.Position + Vector2.One * border * 0.5f, rect.Size - Vector2.One * border), i == _hover ? Palette.PaintSwatchHover : Palette.PaintSwatchBorder, false, border);

		// Equipada: rombo de 7 px arriba a la izquierda, con halo
		if (_first + i == _equipped)
		{
			Vector2 center = rect.Position + new Vector2(S(6.5f), S(6.5f));
			float r = S(7f) * 0.7071f;
			for (int k = 3; k >= 1; k--)
				DrawColoredPolygon(Diamond(center, r + S(1.2f * k)), new Color(Palette.PaintEquipped, 0.15f));
			DrawColoredPolygon(Diamond(center, r), Palette.PaintEquipped);
		}
	}

	private static Vector2[] Diamond(Vector2 c, float r) => new[]
	{
		c + new Vector2(0f, -r), c + new Vector2(r, 0f), c + new Vector2(0f, r), c + new Vector2(-r, 0f)
	};

	private static Color ToColor(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

	// Fondo fijo de una muestra en una textura de su tamaño en pantalla, con 2 × 2 muestras por píxel para suavizar
	// los bordes de las manchas, del corte y del distintivo
	public static ImageTexture BakeTexture(PaintSwatch swatch, Vector2 referenceSize, float scale)
	{
		int w = Math.Max(1, Mathf.RoundToInt(referenceSize.X * scale));
		int h = Math.Max(1, Mathf.RoundToInt(referenceSize.Y * scale));
		var data = new byte[w * h * 4];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				System.Numerics.Vector3 sum = System.Numerics.Vector3.Zero;
				for (int sy = 0; sy < 2; sy++)
				{
					for (int sx = 0; sx < 2; sx++)
					{
						float rx = (x + 0.25f + 0.5f * sx) / scale;
						float ry = (y + 0.25f + 0.5f * sy) / scale;
						sum += PaintSwatchDrawing.Sample(swatch, rx, ry, referenceSize.X, referenceSize.Y);
					}
				}
				sum *= 0.25f;
				int o = (y * w + x) * 4;
				data[o] = (byte)Mathf.RoundToInt(Mathf.Clamp(sum.X, 0f, 1f) * 255f);
				data[o + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp(sum.Y, 0f, 1f) * 255f);
				data[o + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp(sum.Z, 0f, 1f) * 255f);
				data[o + 3] = 255;
			}
		}
		return ImageTexture.CreateFromImage(Image.CreateFromData(w, h, false, Image.Format.Rgba8, data));
	}
}
