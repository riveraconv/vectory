namespace Vectory.Settings;

// Los multiplicadores de SETTINGS que lee el juego en cada paso (spec docs/settings.md, 4). La base sigue saliendo del
// Inspector y esto solo la multiplica: con todo al 100 % el juego se ve exactamente igual que antes. Los pone
// SettingsService al cargar y con cada cambio. Lógica pura: solo datos.
public static class ActiveSettings
{
	public static float Bloom { get; set; } = 1f;          // glow de todas las escenas y de la cabina 3D
	public static bool SpeedLines { get; set; } = true;    // líneas de aire del rebufo (y las de velocidad, cuando existan)
	public static float SpeedFov { get; set; } = 1f;       // FOV extra por velocidad y del boost
	public static float CockpitShake { get; set; } = 1f;   // vibración por velocidad y boost (no la sacudida por golpes)
	public static float Particles { get; set; } = 1f;      // chispas y humo de los choques: 0,25 / 0,5 / 1

	// Fracción de PARTICLES (spec docs/efectos-1-choques.md, 7): LOW 25 %, MEDIUM 50 %, HIGH 100 %
	public static float ParticlesFraction(int level) => level switch { 0 => 0.25f, 1 => 0.5f, _ => 1f };

	public static void From(SettingsModel model)
	{
		Bloom = model.Bloom;
		SpeedLines = model.SpeedLines;
		SpeedFov = model.SpeedFov;
		CockpitShake = model.CockpitShake;
		Particles = ParticlesFraction(model.Particles);
	}

	// FOV con el extra por velocidad multiplicado: de baseFov a baseFov + (maxFov − baseFov) × SpeedFov
	public static float ScaledMaxFov(float baseFov, float maxFov) => baseFov + (maxFov - baseFov) * SpeedFov;
}
