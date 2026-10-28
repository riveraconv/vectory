using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Semáforo de salida tipo F1 del HUD: una fila de luces y, debajo, el número de la
// cuenta atrás o el GO. Encendida = magenta con halo; apagada = azul atenuado (como
// las cargas de boost vacías). Solo MUESTRA el estado; la lógica vive en StartSequence.
public partial class StartLightsView : Control
{
	public const float Diameter = 28f;
	public const float Gap = 14f;
	public const float LabelHeight = 96f;   // alto reservado para el número / GO bajo las luces

	private const float GlowSize = 8f;      // cuánto sobresale el halo de la luz encendida
	private const float BorderWidth = 1.5f;
	private const float LabelGap = 12f;     // separación entre las luces y el número
	private const int FontSize = 72;

	private int _lightsOn;
	private int _lightCount = 5;
	private Label _label;
	private LabelSettings _countdownSettings;
	private LabelSettings _goSettings;

	public static float Height => Diameter + LabelGap + LabelHeight;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		_countdownSettings = new LabelSettings { FontSize = FontSize, FontColor = Palette.Countdown };
		_goSettings = new LabelSettings { FontSize = FontSize, FontColor = Palette.Go };

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
			OffsetBottom = Diameter + LabelGap + LabelHeight
		};
		AddChild(_label);
	}

	// number: número de la cuenta atrás (null = ninguno). showGo: mostrar GO en su lugar.
	public void SetState(int lightsOn, int lightCount, int? number, bool showGo)
	{
		if (lightsOn != _lightsOn || lightCount != _lightCount)
		{
			_lightsOn = lightsOn;
			_lightCount = lightCount;
			QueueRedraw();
		}

		if (showGo)
		{
			_label.Text = "GO";
			_label.LabelSettings = _goSettings;
		}
		else
		{
			_label.Text = number.HasValue ? number.Value.ToString() : "";
			_label.LabelSettings = _countdownSettings;
		}
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
