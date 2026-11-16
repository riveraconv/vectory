using System.Collections.Generic;
using Godot;
using Vectory.Circuit;
using Vectory.Core;

namespace Vectory.UI;

// Tabla de la clasificación en la franja de resultados (spec docs/rivales-2-clasificacion.md, 7.1; maqueta
// designs/clasificacion-resultados.html, versión B, función table con tiempos totales y etiqueta EST): cabecera
// CLASSIFICATION con N PROVISIONAL / FINAL, ocho filas con puesto, piloto, nave y tiempo total, la del jugador resaltada,
// los estimados atenuados con EST y un destello al pasar a real. Lee la clasificación (que cambia mientras los rivales
// terminan) cada frame y solo redibuja si cambia algo o si hay algo animado. Se congela con la pausa (el HUD no procesa).
// Medidas sobre 1280×720, escaladas con UiScale; el origen del nodo queda 8 px a la izquierda de la columna de la tabla.
public partial class ClassificationTableView : Control
{
	public const float TableWidth = 340f;
	public const float RowHeight = 32f;
	private const float Inset = 8f;              // la franja del jugador sobresale 8 px por cada lado
	private const float HeaderBaseline = 14f;
	private const float FlashTime = 0.6f;
	private const float PulsePeriod = 0.18f;     // el punto de PROVISIONAL late con sen(t / 0,18)

	private RaceClassification _classification;
	private int _playerId;
	private ClassificationRow[] _shown = System.Array.Empty<ClassificationRow>();
	private readonly Dictionary<int, float> _flashes = new();
	private float _time;
	private Font _mono;
	private Font _orbitron;

	// Alto total (px sobre 1280×720): cabecera y ocho filas
	public static float HeightFor(int rows) => HeaderBaseline + 14f + rows * RowHeight + 8f;

	public void Setup(RaceClassification classification, int playerId)
	{
		_classification = classification;
		_playerId = playerId;
		float u = UiScale.Factor;
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(TableWidth + 2f * Inset, HeightFor(classification.Count)) * u;
		_shown = Snapshot();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_classification == null)
			return;
		float dt = (float)delta;
		_time += dt;

		ClassificationRow[] now = Snapshot();
		bool changed = now.Length != _shown.Length;
		for (int i = 0; !changed && i < now.Length; i++)
			changed = now[i] != _shown[i];
		if (changed)
		{
			// Las filas que pasan de estimadas a reales destellan
			var wasEstimated = new HashSet<int>();
			foreach (ClassificationRow row in _shown)
			{
				if (row.IsEstimated)
					wasEstimated.Add(row.Id);
			}
			foreach (ClassificationRow row in now)
			{
				if (!row.IsEstimated && wasEstimated.Contains(row.Id))
					_flashes[row.Id] = FlashTime;
			}
			_shown = now;
		}

		var ids = new List<int>(_flashes.Keys);
		foreach (int id in ids)
		{
			float left = _flashes[id] - dt;
			if (left <= 0f)
				_flashes.Remove(id);
			else
				_flashes[id] = left;
		}

		// El punto de PROVISIONAL late y los destellos se apagan: mientras tanto, cada frame
		if (changed || ids.Count > 0 || _classification.EstimatedCount > 0)
			QueueRedraw();
	}

	private ClassificationRow[] Snapshot()
	{
		var rows = new ClassificationRow[_classification.Count];
		for (int i = 0; i < rows.Length; i++)
			rows[i] = _classification.Rows[i];
		return rows;
	}

	public override void _Draw()
	{
		if (_classification == null)
			return;
		_mono ??= MenuFonts.Make(MenuFonts.Mono, 400, 15f * UiScale.Factor, 0f);
		_orbitron ??= MenuFonts.Make(MenuFonts.Orbitron, 500, 15f * UiScale.Factor, 0.06f);

		float x = Inset;
		float w = TableWidth;
		float y = HeaderBaseline;

		// Cabecera: CLASSIFICATION y, a la derecha, N PROVISIONAL (con su punto) o FINAL
		Text("CLASSIFICATION", x, y, 14f, Palette.BlueGray, 1f, 0.22f, false);
		int pending = 0;
		foreach (ClassificationRow row in _shown)
		{
			if (row.IsEstimated)
				pending++;
		}
		string state = pending > 0 ? $"{pending} PROVISIONAL" : "FINAL";
		Color stateColor = pending > 0 ? Palette.ClassificationProvisional : Palette.ClassificationFinal;
		float stateWidth = Text(state, x + w, y, 12f, stateColor, pending > 0 ? 0.9f : 0.8f, 0.16f, true);
		if (pending > 0)
		{
			float alpha = 0.45f + 0.45f * Mathf.Sin(_time / PulsePeriod);
			DrawCircle(new Vector2(x + w - stateWidth - 10f, y - 4f) * UiScale.Factor, 3f * UiScale.Factor,
				new Color(Palette.ClassificationProvisional, alpha));
		}

		for (int i = 0; i < _shown.Length; i++)
			DrawRow(_shown[i], i, x, y + 14f + (i + 1) * RowHeight, w);
	}

	private void DrawRow(ClassificationRow row, int index, float x, float baseline, float w)
	{
		float u = UiScale.Factor;
		bool player = row.Id == _playerId;
		var band = new Rect2((x - Inset) * u, (baseline - RowHeight + 6f) * u, (w + 2f * Inset) * u, (RowHeight - 2f) * u);
		if (player)
		{
			DrawRect(band, Palette.StandingsPlayerStrip);
			DrawRect(new Rect2(band.Position, new Vector2(2f * u, band.Size.Y)), Palette.Cyan);
		}
		if (_flashes.TryGetValue(row.Id, out float flash))
			DrawRect(band, new Color(Palette.ClassificationArrival, 0.28f * flash / FlashTime));

		Text(row.Position.ToString(), x + 16f, baseline, 15f, player ? Palette.Cyan : Palette.Magenta, 1f, 0f, true, true);
		Text(row.Name, x + 30f, baseline, 15f, player ? Palette.Cyan : Palette.StandingsName, 1f, 0f, false, player);
		Text(row.ShipName, x + 30f + 92f, baseline, 11f, Palette.BlueGray, 0.7f, 0.12f, false);

		// Tiempo total: el primero en Orbitron; el del jugador y el primero en cian; los estimados atenuados y con EST
		string time = TimeFormat.Format(row.TotalTime);
		Color color = index == 0 || player ? Palette.Cyan : Palette.StandingsName;
		float right = x + w - (row.IsEstimated ? 34f : 0f);
		float opacity = row.IsEstimated ? 0.6f : 1f;
		if (index == 0)
			OrbitronText(time, right, baseline, color, opacity);
		else
			Text(time, right, baseline, 15f, color, opacity, 0f, true);

		if (row.IsEstimated)
		{
			float tx = x + w - 28f;
			float ty = baseline - 11f;
			DrawRect(new Rect2((tx + 0.5f) * u, (ty + 0.5f) * u, 28f * u, 13f * u), new Color(Palette.ClassificationEstimated, 0.6f), false,
				Mathf.Max(1f, u));
			float estWidth = Measure("EST", 10f, 0.1f);
			Text("EST", tx + 14f + estWidth * 0.5f, baseline - 1f, 10f, Palette.ClassificationEstimated, 0.8f, 0.1f, true);
		}
	}

	// Texto en Share Tech Mono con la línea de base en (x, y) sobre 1280×720 y espaciado en em; alignRight: x es su borde
	// derecho. glow: halo tenue. Devuelve su ancho (px sobre 1280×720).
	private float Text(string text, float x, float y, float size, Color color, float opacity, float spacingEm, bool alignRight,
		bool glow = false)
	{
		float u = UiScale.Factor;
		int px = Mathf.RoundToInt(size * u);
		float spacing = spacingEm * size * u;
		float width = MeasurePx(_mono, text, px, spacing);
		float cursor = alignRight ? x * u - width : x * u;
		foreach (char c in text)
		{
			string glyph = c.ToString();
			var position = new Vector2(cursor, y * u);
			if (glow)
				DrawStringOutline(_mono, position, glyph, HorizontalAlignment.Left, -1, px, Mathf.RoundToInt(4f * u), new Color(color, 0.22f * opacity));
			DrawString(_mono, position, glyph, HorizontalAlignment.Left, -1, px, new Color(color, opacity));
			cursor += _mono.GetStringSize(glyph, HorizontalAlignment.Left, -1, px).X + spacing;
		}
		return width / u;
	}

	private void OrbitronText(string text, float right, float y, Color color, float opacity)
	{
		float u = UiScale.Factor;
		int px = Mathf.RoundToInt(15f * u);
		float width = _orbitron.GetStringSize(text, HorizontalAlignment.Left, -1, px).X;
		DrawString(_orbitron, new Vector2(right * u - width, y * u), text, HorizontalAlignment.Left, -1, px, new Color(color, opacity));
	}

	private float Measure(string text, float size, float spacingEm)
	{
		float u = UiScale.Factor;
		return MeasurePx(_mono, text, Mathf.RoundToInt(size * u), spacingEm * size * u) / u;
	}

	private static float MeasurePx(Font font, string text, int px, float spacing)
	{
		float width = 0f;
		foreach (char c in text)
			width += font.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, px).X + spacing;
		return Mathf.Max(0f, width - spacing);
	}
}
