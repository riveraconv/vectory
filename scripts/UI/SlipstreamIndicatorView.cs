using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Indicador del rebufo (spec docs/rivales-4-personalidades.md, 4.2): tres chevrons › › › alineados a la derecha del control;
// se enciende uno por cada tercio de r, en cian con el halo suave del HUD. Con r = 0 no dibuja nada. Solo muestra:
// el rebufo lo calcula Slipstream.
public partial class SlipstreamIndicatorView : Control
{
	public const int Count = 3;

	public float ChevronWidth = 7f;     // px
	public float ChevronGap = 5f;       // px entre chevrons
	public float LineWidth = 2f;        // px
	public float GlowWidth = 5f;        // px del trazo del halo

	private int _lit;

	// Ancho que ocupan los tres chevrons
	public float RequiredWidth => Count * ChevronWidth + (Count - 1) * ChevronGap;

	// Cuántos se encienden con una fuerza r de 0 a 1: uno por cada tercio empezado
	public static int LitFor(float strength) => strength <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(strength * Count - 1e-4f), 1, Count);

	public void SetStrength(float strength)
	{
		int lit = LitFor(strength);
		if (lit == _lit)
			return;
		_lit = lit;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_lit == 0)
			return;

		float left = Size.X - RequiredWidth;
		float h = Size.Y;
		for (int i = 0; i < _lit; i++)
		{
			float x = left + i * (ChevronWidth + ChevronGap);
			var points = new[] { new Vector2(x, 0f), new Vector2(x + ChevronWidth, h * 0.5f), new Vector2(x, h) };
			DrawPolyline(points, Palette.SlipstreamGlow, GlowWidth, true);
			DrawPolyline(points, Palette.SlipstreamOn, LineWidth, true);
		}
	}
}
