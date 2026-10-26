using Godot;
using Vectory.Core;

namespace Vectory.UI;

// HUD mínimo de la demo: velocidad digital abajo en el centro.
// Se crea por código desde Main. Colores según la paleta del documento maestro.
public partial class Hud : CanvasLayer
{
	private Label _speedLabel;

	public override void _Ready()
	{
		_speedLabel = MakeLabel(56, Palette.Cyan, -135f, -65f);
		_speedLabel.Text = "0";
		AddChild(_speedLabel);

		var unitLabel = MakeLabel(20, Palette.BlueGray, -65f, -30f);
		unitLabel.Text = "KM/H";
		AddChild(unitLabel);
	}

	// Recibe la velocidad en m/s y la muestra en km/h
	public void SetSpeed(float metersPerSecond)
	{
		_speedLabel.Text = Mathf.RoundToInt(metersPerSecond * 3.6f).ToString();
	}

	// Etiqueta centrada, anclada abajo y a todo el ancho de la pantalla
	private static Label MakeLabel(int fontSize, Color color, float offsetTop, float offsetBottom)
	{
		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = new LabelSettings { FontSize = fontSize, FontColor = color },
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetRight = 0f,
			OffsetTop = offsetTop,
			OffsetBottom = offsetBottom
		};
		return label;
	}
}
