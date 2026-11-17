using Godot;
using Vectory.Settings;

namespace Vectory.Game;

// Aplica el modo de pantalla y la resolución de SETTINGS (spec docs/settings.md, 4.1 y 4.2):
//  - WINDOWED: ventana con marco del tamaño de la resolución, centrada en su pantalla; el 3D a escala 1.
//  - FULLSCREEN: la pantalla completa exclusiva de Godot.
//  - BORDERLESS: la pantalla completa de Godot, que es una ventana sin marco del tamaño de la pantalla (la de antes).
// En pantalla completa y sin bordes, la resolución es la del 3D (escala de la ventana principal); el HUD y los menús
// siguen a la de la pantalla. Solo se toca lo que cambia. Sin ventana (headless) no hace nada. Las reglas, en
// DisplayRules.
public static class DisplayApplier
{
	private static (DisplayMode Mode, int Resolution)? _applied;

	public static bool HasScreen => DisplayServer.GetName() != "headless";

	public static Vector2I ScreenSize => DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());

	// Cuántas resoluciones de la lista caben en la pantalla; -1 sin pantalla
	public static int ResolutionCount
	{
		get
		{
			if (!HasScreen)
				return -1;
			Vector2I screen = ScreenSize;
			return DisplayRules.FittingCount(screen.X, screen.Y);
		}
	}

	public static void Apply(SettingsModel model)
	{
		if (!HasScreen)
			return;
		var wanted = (model.DisplayMode, model.Resolution);
		if (_applied == wanted)
			return;
		_applied = wanted;

		int screenIndex = DisplayServer.WindowGetCurrentScreen();
		Vector2I screen = DisplayServer.ScreenGetSize(screenIndex);
		(int width, int height) = DisplayRules.Resolutions[System.Math.Clamp(model.Resolution, 0, DisplayRules.Resolutions.Length - 1)];

		switch (model.DisplayMode)
		{
			case DisplayMode.Windowed:
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
				DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
				var size = new Vector2I(width, height);
				DisplayServer.WindowSetSize(size);
				DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition(screenIndex) + (screen - size) / 2);
				break;
			case DisplayMode.Fullscreen:
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
				break;
			default:
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
				break;
		}

		if (Engine.GetMainLoop() is SceneTree tree)
			tree.Root.Scaling3DScale = DisplayRules.Scale3D(model.DisplayMode, model.Resolution, screen.X);
	}
}
