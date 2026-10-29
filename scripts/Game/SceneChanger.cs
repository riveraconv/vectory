using Godot;

namespace Vectory.Game;

// Único sitio donde se cambia de escena o se cierra el juego.
// Menú, carrera y las pantallas que vengan (pausa, resultados...) pasan por aquí.
public static class SceneChanger
{
	public const string MainMenuScene = "res://scenes/MainMenu.tscn";
	public const string RaceScene = "res://Main.tscn";

	// Carga otra escena. Si la ruta no existe, avisa y se queda en la actual.
	// Quita la pausa antes: si no, la escena nueva arrancaría congelada.
	public static void ChangeTo(Node from, string scenePath)
	{
		if (string.IsNullOrEmpty(scenePath) || !ResourceLoader.Exists(scenePath))
		{
			GD.PushError($"SceneChanger: no existe la escena '{scenePath}'.");
			return;
		}

		from.GetTree().Paused = false;
		Error err = from.GetTree().ChangeSceneToFile(scenePath);
		if (err != Error.Ok)
			GD.PushError($"SceneChanger: no se pudo cargar '{scenePath}' ({err}).");
	}

	// Vuelve a cargar la escena actual (repetir carrera), sin pausa
	public static void Reload(Node from)
	{
		from.GetTree().Paused = false;
		from.GetTree().ReloadCurrentScene();
	}

	public static void Quit(Node from)
	{
		from.GetTree().Quit();
	}
}
