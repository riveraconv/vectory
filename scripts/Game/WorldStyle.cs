using System;
using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Settings;

namespace Vectory.Game;

// Aspecto común de las escenas 3D (carrera, menú...): fondo oscuro y glow.
// Así todas las líneas brillan igual en todas las pantallas. BLOOM de SETTINGS multiplica el glow de todos los entornos
// creados aquí, también los que ya están en pantalla (ApplyBloom); a 0 %, sin glow.
public static class WorldStyle
{
	public const float GlowIntensity = 1.0f;

	private static readonly List<WeakReference<Godot.Environment>> Created = new();

	public static WorldEnvironment CreateWorldEnvironment()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Palette.Background,
			GlowBloom = 0.15f,
			GlowHdrThreshold = 0.8f
		};
		SetGlow(env);
		Created.Add(new WeakReference<Godot.Environment>(env));
		return new WorldEnvironment { Name = "WorldEnvironment", Environment = env };
	}

	// El BLOOM de ahora en los entornos que sigan vivos
	public static void ApplyBloom()
	{
		for (int i = Created.Count - 1; i >= 0; i--)
		{
			if (Created[i].TryGetTarget(out Godot.Environment env) && GodotObject.IsInstanceValid(env))
				SetGlow(env);
			else
				Created.RemoveAt(i);
		}
	}

	private static void SetGlow(Godot.Environment env)
	{
		env.GlowEnabled = ActiveSettings.Bloom > 0f;
		env.GlowIntensity = GlowIntensity * ActiveSettings.Bloom;
	}
}
