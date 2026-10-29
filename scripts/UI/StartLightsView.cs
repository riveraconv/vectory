using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Salida del HUD: una fila de luces tipo F1 (solo en RACE) y, debajo, el texto de salida:
// el número de la cuenta atrás o READY? en cian, y GO! en naranja.
// Luces: encendida = magenta con halo; apagada = azul atenuado (como las cargas de boost vacías).
// Texto: Orbitron Black con halo de su color, como los títulos de los menús (TextFontSize sobre 1280×720).
// Sin luces (TIME TRIAL) el texto queda en el mismo sitio en vertical.
// Solo MUESTRA el estado; la lógica vive en StartSequence / ReadySequence.
public partial class StartLightsView : Control
{
	public const float Diameter = 28f;
	public const float Gap = 14f;
	public const int DefaultTextFontSize = 56;   // px sobre 1280×720

	private const float GlowSize = 8f;      // cuánto sobresale el halo de la luz encendida
	private const float BorderWidth = 1.5f;
	private const float LabelGap = 12f;     // separación entre las luces y el texto

	private int _lightsOn;
	private int _lightCount = 5;
	private Label _label;
	private LabelSettings _countdownSettings;
	private LabelSettings _goSettings;

	// Alto total (luces + texto) para un tamaño de texto (px sobre 1280×720)
	public static float HeightFor(int textFontSize) => Diameter + LabelGap + LabelHeightFor(textFontSize);

	private static float LabelHeightFor(int textFontSize) => textFontSize * UiScale.Factor * 1.5f;

	// Tamaño del texto de salida (px sobre 1280×720). Se fija antes de añadir la vista al árbol.
	public int TextFontSize = DefaultTextFontSize;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		int px = Mathf.RoundToInt(TextFontSize * UiScale.Factor);
		Font font = MenuFonts.Make(MenuFonts.Orbitron, 900, px, 0.1f);
		float glow = 18f * UiScale.Factor;
		_countdownSettings = MenuFonts.Settings(font, px, Palette.Countdown, Palette.CountdownGlow, glow);
		_goSettings = MenuFonts.Settings(font, px, Palette.Go, Palette.GoGlow, glow);

		_label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = _countdownSettings,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetLeft = 0f,
			OffsetRight = 0f,
			OffsetTop = Diameter + LabelGap,
			OffsetBottom = Diameter + LabelGap + LabelHeightFor(TextFontSize)
		};
		AddChild(_label);
	}

	// lightsOn / lightCount: luces del semáforo (0 luces = sin semáforo).
	// text: número, READY? o GO! ("" = nada). go: pintarlo como el GO! (naranja).
	public void SetState(int lightsOn, int lightCount, string text, bool go)
	{
		if (lightsOn != _lightsOn || lightCount != _lightCount)
		{
			_lightsOn = lightsOn;
			_lightCount = lightCount;
			QueueRedraw();
		}

		_label.Text = text;
		_label.LabelSettings = go ? _goSettings : _countdownSettings;
	}

	public override void _Draw()
	{
		float radius = Diameter * 0.5f;
		float width = _lightCount * Diameter + (_lightCount - 1) * Gap;
		float left = (Size.X - width) * 0.5f;

		for (int i = 0; i < _lightCount; i++)
		{
			var center = new Vector2(left + radius + i * (Diameter + Gap), radius);

			if (i < _lightsOn)
			{
				// Halo suave: dos capas translúcidas debajo de la luz
				DrawCircle(center, radius + GlowSize, Palette.StartLightGlow);
				DrawCircle(center, radius + GlowSize * 0.5f, Palette.StartLightGlow);
				DrawCircle(center, radius, Palette.StartLightOn);
			}
			else
			{
				DrawCircle(center, radius, Palette.StartLightOffFill);
				DrawArc(center, radius - BorderWidth * 0.5f, 0f, Mathf.Tau, 32, Palette.StartLightOff, BorderWidth, true);
			}
		}
	}
}
