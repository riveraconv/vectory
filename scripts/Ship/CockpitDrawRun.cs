using System.Collections.Generic;
using Godot;

namespace Vectory.Ship;

// Un tramo seguido de elementos de la cabina. Godot guarda lo que se dibuja en _Draw hasta el siguiente QueueRedraw:
// un tramo fijo solo se redibuja cuando parpadea una arista y uno vivo, cada frame.
public partial class CockpitDrawRun : Control
{
	private readonly List<CockpitElement> _elements = new();
	private CockpitLayer _layer;

	public bool IsLive { get; private set; }

	public void Setup(CockpitLayer layer, bool isLive)
	{
		_layer = layer;
		IsLive = isLive;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void Add(CockpitElement element)
	{
		_elements.Add(element);
	}

	public override void _Draw()
	{
		foreach (CockpitElement element in _elements)
			_layer.DrawElement(this, element);
	}
}
