using System.Linq;
using Godot;
using Vectory.Circuit;
using Vectory.Core;

namespace Vectory.UI;

// Panel de clasificación del HUD (spec docs/rivales-2-clasificacion.md, 3.4; diseño designs/clasificacion-hud.html,
// drawColumn con panel): cinco huecos fijos con el jugador en el del medio, puesto, apodo e intervalo en Share Tech Mono,
// fondo con degradado vertical, línea cian a la izquierda y franja detrás de la fila del jugador. Medidas sobre 1280×720,
// escaladas con UiScale; se dibuja en coordenadas de pantalla (el nodo va en el origen). Sin reglas: recibe las filas ya
// hechas (StandingsFormat.Slots) y solo redibuja si cambian.
public partial class StandingsPanelView : Control
{
	// Medidas de la página (px sobre 1280×720)
	public float ColumnX = 52f;          // borde izquierdo de la columna
	public float PlayerBaseline = 80f;   // línea de base de la fila del jugador
	public float RowHeight = 22f;
	public float FadeOne = 0.62f;        // opacidad a un puesto del jugador
	public float FadeTwo = 0.30f;        // a dos

	private const float PositionFontSize = 15f;
	private const float TotalFontSize = 10f;
	private const float NameFontSize = 15f;
	private const float IntervalFontSize = 13f;
	private const float PositionOffset = 14f;   // de la columna al borde derecho del puesto
	private const float NameOffset = 30f;       // del puesto al apodo (22 hasta su columna + 8)
	private const float NameWidth = 72f;
	private const float IntervalGap = 64f;      // del final del apodo al borde derecho del intervalo

	private StandingsRow?[] _rows = new StandingsRow?[StandingsFormat.SlotCount];
	private int _total;
	private Font _font;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_font = MenuFonts.Make(MenuFonts.Mono, 400, 15f * UiScale.Factor, 0f);
	}

	// Filas de los cinco huecos (null = vacío) y cuántos participantes hay (el /8 de la fila del jugador)
	public void SetRows(StandingsRow?[] rows, int total)
	{
		if (total == _total && rows.SequenceEqual(_rows))
			return;
		_rows = rows.ToArray();
		_total = total;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_font == null)
			return;
		float u = UiScale.Factor;
		float xPos = ColumnX + PositionOffset;
		float xName = xPos + NameOffset;
		float xInterval = xPos + 22f + NameWidth + IntervalGap;
		float right = xInterval + 4f;
		float left = ColumnX - 12f;
		float top = PlayerBaseline - 2.5f * RowHeight - 4f;
		float bottom = PlayerBaseline + 2.5f * RowHeight - 4f;

		// Fondo: transparente arriba y abajo, 66 % entre el 30 % y el 70 % del alto
		Color background = Palette.Background;
		DrawGradient(left, top, right - left + 12f, bottom - top, background, 0f, 0.66f, 0.66f, 0f, u);
		// Línea de la izquierda: 0 → 80 % en el medio → 0 (en la página no lleva tramo constante)
		DrawGradientLine(left, top, 2f, bottom - top, Palette.Cyan, 0.8f, u);
		// Franja del jugador
		DrawRect(new Rect2((left + 2f) * u, (PlayerBaseline - 15f) * u, (right - left + 10f) * u, 21f * u), Palette.StandingsPlayerStrip);

		foreach (StandingsRow? slot in _rows)
		{
			if (slot is not StandingsRow row)
				continue;
			float y = PlayerBaseline + row.Offset * RowHeight;
			float opacity = Mathf.Abs(row.Offset) switch { 0 => 1f, 1 => FadeOne, _ => FadeTwo };
			if (row.IsPlayer)
			{
				Text(row.Position.ToString(), xPos, y, PositionFontSize, Palette.Cyan, 1f, true, true);
				Text($"/{_total}", xPos + 2f, y - 7f, TotalFontSize, Palette.BlueGray, 0.85f, false, false);
				Text(row.Name, xName, y, NameFontSize, Palette.Cyan, 1f, false, true);
			}
			else
			{
				Text(row.Position.ToString(), xPos, y, PositionFontSize, Palette.Magenta, opacity, true, true);
				Text(row.Name, xName, y, NameFontSize, Palette.StandingsName, opacity, false, false);
				Interval(row.Interval, xInterval, y, opacity);
			}
		}
	}

	// Texto con la línea de base en (x, y) sobre 1280×720; alignRight: x es su borde derecho. glow: halo tenue
	private float Text(string text, float x, float y, float size, Color color, float opacity, bool alignRight, bool glow)
	{
		float u = UiScale.Factor;
		int px = Mathf.RoundToInt(size * u);
		float width = _font.GetStringSize(text, HorizontalAlignment.Left, -1, px).X;
		var position = new Vector2(alignRight ? x * u - width : x * u, y * u);
		if (glow)
			DrawStringOutline(_font, position, text, HorizontalAlignment.Left, -1, px, Mathf.RoundToInt(4f * u),
				new Color(color, 0.22f * opacity));
		DrawString(_font, position, text, HorizontalAlignment.Left, -1, px, new Color(color, opacity));
		return width;
	}

	// Intervalo alineado a la derecha. Share Tech Mono no trae ▲ ni ▼: se dibujan como triángulos, en el hueco de un carácter
	private void Interval(string text, float right, float y, float opacity)
	{
		if (string.IsNullOrEmpty(text))
			return;
		bool ahead = text.StartsWith(StandingsFormat.AheadMark);
		bool behind = text.StartsWith(StandingsFormat.BehindMark);
		if (!ahead && !behind)
		{
			Text(text, right, y, IntervalFontSize, Palette.BlueGray, opacity, true, false);
			return;
		}

		float u = UiScale.Factor;
		string rest = text.Substring(StandingsFormat.AheadMark.Length).TrimStart();
		float restWidth = Text(rest, right, y, IntervalFontSize, Palette.BlueGray, opacity, true, false) / u;
		int px = Mathf.RoundToInt(IntervalFontSize * u);
		float advance = _font.GetStringSize("0", HorizontalAlignment.Left, -1, px).X / u;
		float cellLeft = right - restWidth - 2f * advance;
		float cx = cellLeft + advance * 0.5f;
		float half = advance * 0.42f;
		float topY = y - IntervalFontSize * 0.62f;
		float bottomY = y - IntervalFontSize * 0.08f;
		Vector2[] triangle = ahead
			? new[] { new Vector2(cx, topY), new Vector2(cx + half, bottomY), new Vector2(cx - half, bottomY) }
			: new[] { new Vector2(cx - half, topY), new Vector2(cx + half, topY), new Vector2(cx, bottomY) };
		for (int i = 0; i < triangle.Length; i++)
			triangle[i] *= u;
		DrawColoredPolygon(triangle, new Color(Palette.BlueGray, opacity));
	}

	// Rectángulo con degradado vertical: opacidades en 0, 30 %, 70 % y 100 % del alto
	private void DrawGradient(float x, float y, float w, float h, Color color, float a0, float a1, float a2, float a3, float u)
	{
		float[] stops = { 0f, 0.3f, 0.7f, 1f };
		float[] alphas = { a0, a1, a2, a3 };
		for (int i = 0; i < 3; i++)
		{
			float y0 = (y + h * stops[i]) * u;
			float y1 = (y + h * stops[i + 1]) * u;
			var c0 = new Color(color, alphas[i]);
			var c1 = new Color(color, alphas[i + 1]);
			DrawPolygon(
				new[] { new Vector2(x * u, y0), new Vector2((x + w) * u, y0), new Vector2((x + w) * u, y1), new Vector2(x * u, y1) },
				new[] { c0, c0, c1, c1 });
		}
	}

	// Línea con degradado vertical: transparente en los extremos y peak en el medio
	private void DrawGradientLine(float x, float y, float w, float h, Color color, float peak, float u)
	{
		for (int i = 0; i < 2; i++)
		{
			float y0 = (y + h * 0.5f * i) * u;
			float y1 = (y + h * 0.5f * (i + 1)) * u;
			var c0 = new Color(color, i == 0 ? 0f : peak);
			var c1 = new Color(color, i == 0 ? peak : 0f);
			DrawPolygon(
				new[] { new Vector2(x * u, y0), new Vector2((x + w) * u, y0), new Vector2((x + w) * u, y1), new Vector2(x * u, y1) },
				new[] { c0, c0, c1, c1 });
		}
	}
}
