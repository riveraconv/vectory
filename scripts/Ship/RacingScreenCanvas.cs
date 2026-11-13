using System.Collections.Generic;
using System.Linq;
using Godot;
using Vectory.UI;
using NVec2 = System.Numerics.Vector2;
using NVec4 = System.Numerics.Vector4;

namespace Vectory.Ship;

// Pinta una lista de primitivas de pantalla (RacingScreenDrawing, MilitaryScreenDrawing), sin reglas. El halo es una
// copia mayor y tenue.
public partial class RacingScreenCanvas : Control
{
	private const float BoldEmbolden = 0.6f;   // la negrita del lienzo del prototipo: Share Tech Mono no tiene negrita

	private const float OuterHaloSize = 1f;       // × Glow
	private const float OuterHaloOpacity = 0.10f;
	private const float InnerHaloSize = 0.5f;
	private const float InnerHaloOpacity = 0.20f;
	private const int RingPoints = 48;

	private static readonly Dictionary<(Cockpit3DFont, int, bool), Font> Fonts = new();

	private List<ScreenPrimitive> _items = new();
	private List<ScreenPrimitive> _next = new();

	// La lista que se está preparando: al terminar, Commit dice si hay que volver a pintar
	public List<ScreenPrimitive> Next => _next;

	// Fuente de los textos: Rajdhani en las pantallas de VECTOR, Share Tech Mono en las de BLADE
	public Cockpit3DFont FontFamily { get; set; } = Cockpit3DFont.Rajdhani;
	public bool Bold { get; set; }

	public RacingScreenCanvas()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	// True si lo preparado es distinto de lo que hay: redibujar es lo que cuesta
	public bool Commit()
	{
		if (_next.SequenceEqual(_items))
		{
			_next.Clear();
			return false;
		}

		(_items, _next) = (_next, _items);
		_next.Clear();
		QueueRedraw();
		return true;
	}

	public override void _Draw()
	{
		foreach (ScreenPrimitive p in _items)
		{
			Color color = ToColor(p.Color);
			switch (p.Kind)
			{
				case ScreenPrimitiveKind.FillRect:
					DrawFillRect(p, color);
					break;
				case ScreenPrimitiveKind.StrokeRect:
					DrawRect(new Rect2(ToGodot(p.A), ToGodot(p.B)), color, false, p.Width);
					break;
				case ScreenPrimitiveKind.Line:
					DrawLine(ToGodot(p.A), ToGodot(p.B), color, p.Width, true);
					break;
				case ScreenPrimitiveKind.Disc:
					if (p.Glow > 0f)
					{
						DrawCircle(ToGodot(p.A), p.Radius + p.Glow * OuterHaloSize, Faded(color, OuterHaloOpacity), true, -1f, true);
						DrawCircle(ToGodot(p.A), p.Radius + p.Glow * InnerHaloSize, Faded(color, InnerHaloOpacity), true, -1f, true);
					}
					DrawCircle(ToGodot(p.A), p.Radius, color, true, -1f, true);
					break;
				case ScreenPrimitiveKind.Ring:
					DrawArc(ToGodot(p.A), p.Radius, 0f, Mathf.Tau, RingPoints, color, p.Width, true);
					break;
				case ScreenPrimitiveKind.Text:
					DrawText(p, color);
					break;
				case ScreenPrimitiveKind.Triangle:
					DrawColoredPolygon(new[] { ToGodot(p.A), ToGodot(p.B), ToGodot(p.C) }, color);
					break;
			}
		}
	}

	private void DrawFillRect(in ScreenPrimitive p, Color color)
	{
		var rect = new Rect2(ToGodot(p.A), ToGodot(p.B));
		if (p.Glow > 0f)
		{
			DrawRect(rect.Grow(p.Glow * OuterHaloSize), Faded(color, OuterHaloOpacity));
			DrawRect(rect.Grow(p.Glow * InnerHaloSize), Faded(color, InnerHaloOpacity));
		}
		DrawRect(rect, color);
	}

	private void DrawText(in ScreenPrimitive p, Color color)
	{
		Font font = FontWith(FontFamily, Mathf.RoundToInt(p.Spacing), Bold);
		int size = Mathf.RoundToInt(p.Radius);
		float width = p.Align == ScreenTextAlign.Left ? 0f : font.GetStringSize(p.Text, HorizontalAlignment.Left, -1f, size).X;
		Vector2 position = ToGodot(p.A) - new Vector2(p.Align == ScreenTextAlign.Right ? width : width * 0.5f, 0f);
		if (p.Glow > 0f)
		{
			DrawStringOutline(font, position, p.Text, HorizontalAlignment.Left, -1f, size, Mathf.RoundToInt(p.Glow * OuterHaloSize * 2f), Faded(color, OuterHaloOpacity));
			DrawStringOutline(font, position, p.Text, HorizontalAlignment.Left, -1f, size, Mathf.RoundToInt(p.Glow * InnerHaloSize * 2f), Faded(color, InnerHaloOpacity));
		}
		DrawString(font, position, p.Text, HorizontalAlignment.Left, -1f, size, color);
	}

	// Una variación por espaciado entre letras, de la fuente de las pantallas de VECTOR
	public static Font FontWith(int spacing) => FontWith(Cockpit3DFont.Rajdhani, spacing, false);

	// Una variación por fuente, espaciado entre letras y negrita
	public static Font FontWith(Cockpit3DFont family, int spacing, bool bold)
	{
		if (Fonts.TryGetValue((family, spacing, bold), out Font font))
			return font;
		Font baseFont = family == Cockpit3DFont.Mono ? MenuFonts.Mono : MenuFonts.Rajdhani;
		font = new FontVariation
		{
			BaseFont = baseFont ?? ThemeDB.FallbackFont,
			SpacingGlyph = spacing,
			VariationEmbolden = bold ? BoldEmbolden : 0f
		};
		Fonts[(family, spacing, bold)] = font;
		return font;
	}

	private static Color Faded(Color color, float opacity) => new(color, color.A * opacity);

	private static Vector2 ToGodot(NVec2 v) => new(v.X, v.Y);

	private static Color ToColor(NVec4 c) => new(c.X, c.Y, c.Z, c.W);
}
