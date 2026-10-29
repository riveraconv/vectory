using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Franja horizontal de los menús sobre la carrera (pausa y resultados): atenuado de toda la pantalla,
// franja de lado a lado centrada en vertical con fondo oscuro, líneas cian arriba y abajo con halo,
// y separadores verticales. El contenido se añade a Band, en coordenadas de la franja.
//
// Medidas sobre 1280×720 (por defecto centrada en y = 360, el centro de la pantalla), multiplicadas
// por UiScale.Factor. Se coloca a pantalla completa (ocupa todo su padre).
// La usan la pausa, los resultados y la franja de fin de vuelta de TIME TRIAL (sin atenuado).
public partial class MenuBand : Control
{
	private const float ScreenCenterY = 360f;
	private const float LineGlowHeight = 18f;     // halo de las líneas de la franja

	private float _scale = 1f;
	private float _height;
	private float _centerY = ScreenCenterY;
	private readonly System.Collections.Generic.List<(ColorRect Rect, float Height)> _separators = new();

	// Contenedor de la franja: lo que se añada aquí va en coordenadas de la franja (0 = borde de arriba)
	public Control Band { get; private set; }

	// Alto actual de la franja, en píxeles finales
	public float BandHeight => _height;

	// height: alto de la franja (px sobre 1280×720). Opacidades del atenuado (0 = sin atenuado) y del
	// fondo de la franja. centerY: centro vertical de la franja (por defecto, el de la pantalla).
	public void Setup(float height, float dimOpacity, float bandOpacity, float centerY = ScreenCenterY)
	{
		_scale = UiScale.Factor;
		_centerY = centerY;
		MouseFilter = MouseFilterEnum.Ignore;
		// Ya está en el árbol: hay que fijar también los offsets, o conservaría su tamaño anterior (0)
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		if (dimOpacity > 0f)
		{
			var dim = new ColorRect { Name = "Dim", Color = new Color(Palette.PauseDim, dimOpacity), MouseFilter = MouseFilterEnum.Ignore };
			dim.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(dim);
		}

		Band = new Control
		{
			Name = "Band",
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f
		};
		AddChild(Band);

		var fill = new ColorRect { Name = "Fill", Color = new Color(Palette.PauseBand, bandOpacity), MouseFilter = MouseFilterEnum.Ignore };
		fill.SetAnchorsPreset(LayoutPreset.FullRect);
		Band.AddChild(fill);

		AddEdgeLine(true);
		AddEdgeLine(false);

		SetHeight(height * _scale);
	}

	// Cambia el alto de la franja (px finales) manteniéndola centrada en su centro vertical.
	// Los separadores siguen centrados.
	public void SetHeight(float heightPx)
	{
		_height = heightPx;
		float center = _centerY * _scale;
		Band.OffsetTop = center - heightPx * 0.5f;
		Band.OffsetBottom = center + heightPx * 0.5f;

		foreach (var (rect, h) in _separators)
			rect.Position = new Vector2(rect.Position.X, (heightPx - h) * 0.5f);
	}

	// Separador vertical de 1 px centrado en la franja (x y alto en px sobre 1280×720)
	public void AddSeparator(float x, float height)
	{
		float h = height * _scale;
		var rect = new ColorRect
		{
			Name = "Separator",
			Color = Palette.MenuSeparator,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(x * _scale, (_height - h) * 0.5f),
			Size = new Vector2(Mathf.Max(1f, _scale), h)
		};
		Band.AddChild(rect);
		_separators.Add((rect, h));
	}

	// Columna de contenido centrada en vertical en la franja, empezando en x (px sobre 1280×720)
	public VBoxContainer AddColumn(string name, float x, float separation)
	{
		var column = new VBoxContainer
		{
			Name = name,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = x * _scale,
			OffsetRight = x * _scale,   // el ancho sale del contenido
			OffsetTop = 0f,
			OffsetBottom = 0f
		};
		column.AddThemeConstantOverride("separation", Mathf.RoundToInt(separation * _scale));
		Band.AddChild(column);
		return column;
	}

	// Línea fina cian en un borde de la franja, con un halo que se desvanece hacia fuera
	private void AddEdgeLine(bool top)
	{
		float glow = LineGlowHeight * _scale;
		var fade = new Gradient();
		fade.SetColor(0, top ? new Color(Palette.PauseLineGlow, 0f) : Palette.PauseLineGlow);
		fade.SetColor(1, top ? Palette.PauseLineGlow : new Color(Palette.PauseLineGlow, 0f));

		Band.AddChild(new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = fade,
				Width = 1,
				Height = 32,
				FillFrom = new Vector2(0f, 0f),
				FillTo = new Vector2(0f, 1f)
			},
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = top ? 0f : 1f,
			AnchorBottom = top ? 0f : 1f,
			OffsetTop = top ? -glow : 0f,
			OffsetBottom = top ? 0f : glow
		});

		float thickness = Mathf.Max(1f, _scale);
		Band.AddChild(new ColorRect
		{
			Color = Palette.PauseLine,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = top ? 0f : 1f,
			AnchorBottom = top ? 0f : 1f,
			OffsetTop = top ? 0f : -thickness,
			OffsetBottom = top ? thickness : 0f
		});
	}
}
