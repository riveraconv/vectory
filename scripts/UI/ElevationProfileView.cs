using System;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Menu;

namespace Vectory.UI;

// Gráfico del perfil de elevación de SELECT CIRCUIT: la altura del eje de la salida (izquierda) a la meta
// (derecha), líneas guía arriba y abajo con la escala (+E M / −E M), una línea discontinua en cada checkpoint
// y las etiquetas S1, S2… centradas en cada sector, debajo. Solo dibuja un TrackData.
// Medidas en píxeles finales (ya escalados).
public partial class ElevationProfileView : Control
{
	private TrackData _data;
	private float _graphHeight;
	private float _labelGap;
	private float _lineWidth;
	private int _fontSize;
	private Font _font;

	// graphSize: el gráfico (sin las etiquetas de sector); fontSize: etiquetas
	public void Setup(Vector2 graphSize, int fontSize, float scale)
	{
		_graphHeight = graphSize.Y;
		_fontSize = fontSize;
		_labelGap = 4f * scale;
		_lineWidth = Mathf.Max(1f, 1.5f * scale);
		_font = MenuFonts.Make(MenuFonts.Mono, 400, fontSize, 0.1f);
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(graphSize.X, graphSize.Y + _labelGap + fontSize);
	}

	public void SetData(TrackData data)
	{
		_data = data;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float width = Size.X;
		float height = _graphHeight;

		// Líneas guía y escala
		DrawLine(new Vector2(0f, 0f), new Vector2(width, 0f), Palette.ProfileGuide, 1f);
		DrawLine(new Vector2(0f, height), new Vector2(width, height), Palette.ProfileGuide, 1f);
		if (_data == null)
			return;

		float ascent = _font.GetAscent(_fontSize);
		DrawString(_font, new Vector2(0f, ascent + _labelGap * 0.5f), CircuitSelectFormat.ScaleTop(_data.ProfileScale),
			HorizontalAlignment.Left, -1, _fontSize, Palette.SelectLabel);
		DrawString(_font, new Vector2(0f, height - _labelGap * 0.5f - _font.GetDescent(_fontSize)), CircuitSelectFormat.ScaleBottom(_data.ProfileScale),
			HorizontalAlignment.Left, -1, _fontSize, Palette.SelectLabel);

		// Sectores: línea discontinua en cada checkpoint y etiqueta centrada en cada sector
		float[] bounds = new float[_data.CheckpointFractions.Length + 2];
		bounds[^1] = 1f;
		for (int i = 0; i < _data.CheckpointFractions.Length; i++)
		{
			float x = _data.CheckpointFractions[i] * width;
			bounds[i + 1] = _data.CheckpointFractions[i];
			DrawDashedLine(new Vector2(x, 0f), new Vector2(x, height), Palette.ProfileSector, 1f, 4f);
		}
		for (int s = 0; s < bounds.Length - 1; s++)
		{
			float center = (bounds[s] + bounds[s + 1]) * 0.5f * width;
			string label = CircuitSelectFormat.Sector(s);
			float textWidth = _font.GetStringSize(label, HorizontalAlignment.Left, -1, _fontSize).X;
			DrawString(_font, new Vector2(center - textWidth * 0.5f, height + _labelGap + ascent), label,
				HorizontalAlignment.Left, -1, _fontSize, Palette.SelectLabel);
		}

		// Perfil
		float[] profile = _data.Profile;
		if (profile.Length < 2 || _data.ProfileScale <= 0f)
			return;

		var points = new Vector2[profile.Length];
		for (int i = 0; i < profile.Length; i++)
		{
			float v = Math.Clamp(profile[i] / _data.ProfileScale, -1f, 1f);
			points[i] = new Vector2(width * i / (profile.Length - 1), height * 0.5f * (1f - v));
		}
		DrawPolyline(points, Palette.ProfileLine, _lineWidth, true);
	}
}
