using Godot;
using Vectory.Core;
using Vectory.Ship;

namespace Vectory.UI;

// Piloto automático visible (spec docs/naves-6-equilibrio.md, 4.7): F7 lo activa o lo quita en la nave del jugador y,
// mientras va, AUTOPILOT sale en una esquina. Main solo lo crea en compilaciones de depuración. Sin reglas: conducir
// lo hace el Player con el piloto. Se puede pulsar también con la pausa, como el panel de ajuste.
public partial class AutopilotToggle : CanvasLayer
{
	private const int ToggleLayer = 11;   // por encima de la pausa (10), como el panel de ajuste
	private const float MarginX = 40f;    // medidas sobre 1280×720
	private const float MarginY = 242f;   // justo debajo del minimapa
	private const int FontSize = 14;

	public Player Player { get; set; }

	private Label _label;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = ToggleLayer;

		_label = new Label
		{
			Name = "AutopilotLabel",
			Text = "AUTOPILOT",
			Position = new Vector2(MarginX, MarginY) * UiScale.Factor,
			LabelSettings = MenuFonts.Settings(MenuFonts.Mono, Mathf.RoundToInt(FontSize * UiScale.Factor), Palette.Orange, Colors.Transparent, 0f),
			Visible = false
		};
		AddChild(_label);
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("debug_autopilot") || Player == null)
			return;

		Player.SetAutopilot(!Player.AutopilotActive);
		_label.Visible = Player.AutopilotActive;
		GD.Print($"[Autopilot] {(Player.AutopilotActive ? "On: this race saves no records" : "Off")}");
		GetViewport().SetInputAsHandled();
	}
}
