using System.Collections.Generic;
using Godot;
using Vectory.UI;

namespace Vectory.Ship;

// Capa 2D de la cabina, encima de la vista 3D y debajo del HUD (spec docs/naves-3b-cabinas.md). Solo dibuja: la cabina
// está en su CockpitLayout y las reglas del HUD y la luz en CockpitHudState. Los elementos se reparten en tramos
// seguidos para respetar el orden del diseño: lo vivo puede quedar entre piezas fijas.
public partial class CockpitLayer : CanvasLayer
{
	private const int LayerIndex = 0;   // el HUD está en la capa 1

	private readonly List<CockpitDrawRun> _fixedRuns = new();
	private readonly List<CockpitDrawRun> _liveRuns = new();
	private readonly CockpitHudState _hud = new(CockpitPalette.Rgba);
	private CockpitLayout _layout;
	private EdgeFlicker _flicker;
	private float _dimOpacity = 1f;
	private float _scale = 1f;

	public int EdgeCount => _layout.EdgeCount;

	public void Setup(CockpitLayout layout)
	{
		_layout = layout;
		Layer = LayerIndex;
		_scale = UiScale.Factor;
		_flicker = null;

		// Otra cabina (panel de ajuste): fuera los tramos de la anterior. El estado del HUD sigue.
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
		_fixedRuns.Clear();
		_liveRuns.Clear();

		CockpitDrawRun run = null;
		foreach (CockpitElement element in layout.Elements)
		{
			if (run == null || run.IsLive != element.IsLive)
			{
				run = new CockpitDrawRun { Name = $"Run{_fixedRuns.Count + _liveRuns.Count}" };
				run.Setup(this, element.IsLive);
				(element.IsLive ? _liveRuns : _fixedRuns).Add(run);
				AddChild(run);
			}
			run.Add(element);
		}
	}

	// Cada frame, también con la capa oculta, para que al cambiar de cámara no haya transiciones a medias
	public void UpdateHud(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_hud.Update(input, light, dt);
		if (!Visible)
			return;
		foreach (CockpitDrawRun run in _liveRuns)
			run.QueueRedraw();
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		_flicker = flicker;
		_dimOpacity = dimOpacity;
		foreach (CockpitDrawRun run in _fixedRuns)
			run.QueueRedraw();
	}

	public void DrawElement(CanvasItem item, CockpitElement element)
	{
		CockpitElement resolved = element.IsLive ? CockpitHudPieces.Resolve(element, _hud) : element;
		bool dimmed = element.Edge >= 0 && _flicker != null && _flicker.IsDimmed(element.Edge);
		CockpitPainter.Draw(item, resolved, _scale, dimmed, _dimOpacity);
	}
}
