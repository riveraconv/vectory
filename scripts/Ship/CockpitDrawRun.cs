using System.Collections.Generic;
using Godot;

namespace Vectory.Ship;

// Un tramo seguido de elementos de la cabina. Godot guarda lo que se dibuja en _Draw hasta el siguiente QueueRedraw:
// un tramo fijo solo se redibuja cuando parpadea una de sus aristas y uno vivo, cuando cambia alguno de sus valores.
public partial class CockpitDrawRun : Control
{
	private CockpitElement[] _elements;
	private CockpitElement[] _resolved;
	private CockpitLayer _layer;

	public void Setup(CockpitLayer layer, CockpitRun plan)
	{
		_layer = layer;
		_elements = new CockpitElement[plan.Elements.Count];
		for (int i = 0; i < _elements.Length; i++)
			_elements[i] = plan.Elements[i];
		_resolved = (CockpitElement[])_elements.Clone();
		MouseFilter = MouseFilterEnum.Ignore;
	}

	// Tramo vivo: valores de ahora. Sin cambios, sigue valiendo lo que Godot tiene guardado.
	public void Refresh(CockpitHudState hud)
	{
		bool changed = false;
		for (int i = 0; i < _elements.Length; i++)
		{
			CockpitElement now = CockpitHudPieces.Resolve(_elements[i], hud);
			if (CockpitHudPieces.SameDrawing(now, _resolved[i]))
				continue;
			_resolved[i] = now;
			changed = true;
		}
		if (changed)
			QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (CockpitElement element in _resolved)
			_layer.DrawElement(this, element);
	}
}
