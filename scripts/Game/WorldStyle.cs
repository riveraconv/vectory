using Godot;
using Vectory.Core;

namespace Vectory.Game;

// Aspecto común de las escenas 3D (carrera, menú...): fondo oscuro y glow.
// Así todas las líneas brillan igual en todas las pantallas.
public static class WorldStyle
{
	public static WorldEnvironment CreateWorldEnvironment()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Palette.Background,
			GlowEnabled = true,
			GlowIntensity = 1.0f,
			GlowBloom = 0.15f,
			GlowHdrThreshold = 0.8f
		};
		return new WorldEnvironment { Name = "WorldEnvironment", Environment = env };
	}
}
