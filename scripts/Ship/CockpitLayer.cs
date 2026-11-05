using System.Collections.Generic;
using Godot;
using Vectory.UI;

namespace Vectory.Ship;

// Capa 2D de la cabina, encima de la vista 3D y debajo del HUD (spec docs/naves-3b-cabinas.md). Solo dibuja: la cabina
// está en su CockpitLayout y las reglas del HUD y la luz en CockpitHudState.
//
// Lo que más cuesta es volver a dibujar, no enseñar lo ya dibujado: por eso los elementos van en tramos pequeños y
// cada tramo solo se redibuja si cambia algo suyo. Y por eso, en la cámara exterior, la capa no se oculta sino que se
// aparta de la pantalla: Godot descarta lo que queda fuera sin coste, mientras que al volver a enseñar algo oculto
// lo dibuja entero otra vez.
public partial class CockpitLayer : CanvasLayer
{
	private const int LayerIndex = 0;   // el HUD está en la capa 1
	private static readonly Vector2 HiddenOffset = new(0f, 100000f);

	private readonly List<CockpitDrawRun> _liveRuns = new();
	private readonly HashSet<CockpitDrawRun> _pendingRuns = new();
	private readonly CockpitHudState _hud = new(CockpitPalette.Rgba);
	private CockpitLayout _layout;
	private CockpitSpriteAtlas _atlas;
	private CockpitDrawRun[] _edgeRun = System.Array.Empty<CockpitDrawRun>();
	private bool[] _dimmed = System.Array.Empty<bool>();
	private float _dimOpacity = 1f;
	private float _scale = 1f;
	private bool _shown = true;

	public int EdgeCount => _layout.EdgeCount;

	public void Setup(CockpitLayout layout)
	{
		_layout = layout;
		Layer = LayerIndex;
		_scale = UiScale.Factor;
		_atlas = new CockpitSpriteAtlas(layout.Elements, _scale);

		// Otra cabina (panel de ajuste): fuera los tramos de la anterior. El estado del HUD sigue.
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
		_liveRuns.Clear();
		_pendingRuns.Clear();
		_edgeRun = new CockpitDrawRun[layout.EdgeCount];
		_dimmed = new bool[layout.EdgeCount];

		int index = 0;
		foreach (CockpitRun plan in CockpitRunPlan.Split(layout.Elements))
		{
			var run = new CockpitDrawRun { Name = $"Run{index++}" };
			run.Setup(this, plan);
			AddChild(run);

			if (plan.IsLive)
				_liveRuns.Add(run);
			foreach (CockpitElement element in plan.Elements)
			{
				if (element.Edge >= 0)
					_edgeRun[element.Edge] = run;
			}
		}
	}

	// En la cabina, en su sitio; en la cámara exterior, fuera de la pantalla
	public void SetShown(bool shown)
	{
		_shown = shown;
		Offset = shown ? Vector2.Zero : HiddenOffset;
		if (!shown)
			return;

		foreach (CockpitDrawRun run in _pendingRuns)
			run.QueueRedraw();
		_pendingRuns.Clear();
	}

	// Cada frame, también en exterior, para que al cambiar de cámara no haya transiciones a medias
	public void UpdateHud(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_hud.Update(input, light, dt);
		if (!_shown)
			return;
		foreach (CockpitDrawRun run in _liveRuns)
			run.Refresh(_hud);
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity. Solo se redibujan los tramos donde cambió una.
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		_dimOpacity = dimOpacity;
		foreach (int edge in flicker.AffectedEdges)
		{
			if (edge >= _dimmed.Length)
				continue;
			bool dimmed = flicker.IsDimmed(edge);
			if (dimmed == _dimmed[edge])
				continue;

			_dimmed[edge] = dimmed;
			if (_shown)
				_edgeRun[edge].QueueRedraw();
			else
				_pendingRuns.Add(_edgeRun[edge]);
		}
	}

	public void DrawElement(CanvasItem item, CockpitElement element)
	{
		bool dimmed = element.Edge >= 0 && _dimmed[element.Edge];
		CockpitPainter.Draw(item, element, _scale, dimmed, _dimOpacity, _atlas);
	}
}
