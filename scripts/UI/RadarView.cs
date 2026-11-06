using System.Collections.Generic;
using Godot;
using Vectory.Radar;

namespace Vectory.UI;

// Radar del HUD de pantalla: pinta las primitivas de RadarDrawing, sin reglas. El origen del nodo es el centro del círculo.
// La parte fija (fondo, anillos, rótulo) se dibuja una vez en este nodo; la viva, cada frame en un hijo: redibujar es lo que
// cuesta. Con el árbol en pausa no procesa, así que el haz y los ecos se quedan quietos.
public partial class RadarView : Control
{
	private readonly List<RadarPrimitive> _base = new();
	private readonly List<RadarPrimitive> _live = new();

	private RadarSweep _sweep;
	private RadarStyle _style;
	private Control _liveLayer;
	private Font _font;

	public void Setup(RadarSweep sweep, RadarStyle style, Font font = null)
	{
		_sweep = sweep;
		_style = style;
		_font = font ?? MenuFonts.Mono ?? ThemeDB.FallbackFont;
		MouseFilter = MouseFilterEnum.Ignore;

		_base.Clear();
		RadarDrawing.Base(_base, style, sweep.Settings.Range);
		QueueRedraw();

		_liveLayer = new Control { Name = "Live", MouseFilter = MouseFilterEnum.Ignore };
		_liveLayer.Draw += DrawLive;
		AddChild(_liveLayer);
	}

	public override void _Draw()
	{
		RadarPainter.Draw(this, _base, _font);
	}

	public override void _Process(double delta)
	{
		if (_liveLayer != null && IsVisibleInTree())
			_liveLayer.QueueRedraw();
	}

	private void DrawLive()
	{
		_live.Clear();
		RadarDrawing.Live(_live, _style, _sweep);
		RadarPainter.Draw(_liveLayer, _live, _font);
	}
}
