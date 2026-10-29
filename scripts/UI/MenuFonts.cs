using Godot;

namespace Vectory.UI;

// Fuentes de los menús (menú principal, pausa, resultados): Orbitron (variable, eje de peso)
// y Share Tech Mono. Se cargan una vez; si falta un archivo, se usa la fuente por defecto.
// El HUD de carrera no las usa.
public static class MenuFonts
{
	private const string OrbitronPath = "res://assets/fonts/Orbitron-Variable.ttf";
	private const string MonoPath = "res://assets/fonts/ShareTechMono-Regular.ttf";

	private static FontFile _orbitron;
	private static FontFile _mono;
	private static bool _loaded;

	public static FontFile Orbitron
	{
		get { EnsureLoaded(); return _orbitron; }
	}

	public static FontFile Mono
	{
		get { EnsureLoaded(); return _mono; }
	}

	// Fuente con un peso (eje wght; se ignora si la fuente no lo tiene) y espaciado entre letras
	// en em, como en las specs. sizePx es el tamaño final en píxeles (ya escalado).
	public static Font Make(FontFile file, int weight, float sizePx, float letterSpacingEm)
	{
		var variation = new FontVariation
		{
			BaseFont = file ?? ThemeDB.FallbackFont,
			SpacingGlyph = Mathf.RoundToInt(sizePx * letterSpacingEm)
		};

		if (file != null)
		{
			long wght = TextServerManager.GetPrimaryInterface().NameToTag("wght");
			variation.VariationOpentype = new Godot.Collections.Dictionary { { wght, weight } };
		}

		return variation;
	}

	// Ajustes de texto con halo: una sombra sin desplazamiento del color del halo.
	// glowPx es el tamaño del halo de la spec (ya escalado); Colors.Transparent = sin halo.
	public static LabelSettings Settings(Font font, int fontSizePx, Color color, Color glow, float glowPx)
	{
		return new LabelSettings
		{
			Font = font,
			FontSize = fontSizePx,
			FontColor = color,
			ShadowColor = glow,
			ShadowSize = Mathf.RoundToInt(glowPx * 0.5f),
			ShadowOffset = Vector2.Zero
		};
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
			return;

		_orbitron = Load(OrbitronPath);
		_mono = Load(MonoPath);
		_loaded = true;
	}

	private static FontFile Load(string path)
	{
		var font = ResourceLoader.Exists(path) ? GD.Load<FontFile>(path) : null;
		if (font == null)
			GD.PushWarning($"MenuFonts: no se encuentra la fuente '{path}'; se usa la fuente por defecto.");
		return font;
	}
}
