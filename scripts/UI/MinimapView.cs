using System.Collections.Generic;
using Godot;
using Vectory.Radar;
using NVec2 = System.Numerics.Vector2;

namespace Vectory.UI;

// Minimapa del HUD de pantalla: pinta lo que da MinimapDrawing, sin reglas. El origen del nodo es el centro del círculo.
// Como en el radar, la parte fija se dibuja una vez y la viva en un hijo. Main le pasa la pose cada frame: con la pausa no
// llega ninguna y el mapa se queda como está.
public partial class MinimapView : Control
{
	private readonly List<RadarPrimitive> _base = new();
	private readonly MinimapPicture _picture = new();

	private MinimapPath _path;
	private MinimapStyle _style;
	private IReadOnlyList<RaceShip> _ships;
	private float _windowRadius;
	private Control _liveLayer;
	private NVec2 _self;
	private NVec2 _forward = new(0f, -1f);

	public void Setup(MinimapPath path, MinimapStyle style, float windowRadius, IReadOnlyList<RaceShip> ships)
	{
		_path = path;
		_style = style;
		_windowRadius = windowRadius;
		_ships = ships;
		MouseFilter = MouseFilterEnum.Ignore;

		_base.Clear();
		MinimapDrawing.Base(_base, style);
		QueueRedraw();

		_liveLayer = new Control { Name = "Live", MouseFilter = MouseFilterEnum.Ignore };
		_liveLayer.Draw += DrawLive;
		AddChild(_liveLayer);
	}

	// self y planForward en (X, Z) del mundo
	public void SetPose(NVec2 self, NVec2 planForward)
	{
		_self = self;
		_forward = planForward;
		if (_liveLayer != null && IsVisibleInTree())
			_liveLayer.QueueRedraw();
	}

	public override void _Draw()
	{
		RadarPainter.Draw(this, _base, null);
	}

	private void DrawLive()
	{
		MinimapDrawing.Live(_picture, _style, _path, _self, _forward, _windowRadius, _ships);
		RadarPainter.DrawRuns(_liveLayer, _picture, _picture.Band);
		RadarPainter.DrawRuns(_liveLayer, _picture, _picture.Line);
		RadarPainter.Draw(_liveLayer, _picture.Primitives, null);
	}
}
