using Godot;
using Vectory.Records;
using Vectory.Settings;

namespace Vectory.Game;

// SETTINGS en el juego (spec docs/settings.md, 5 y 9): carga los ajustes al arrancar (desde el menú principal o desde
// la carrera ejecutada sola), los aplica y los guarda junto a lo demás de settings.json. Aplicar: los multiplicadores
// que lee el juego (ActiveSettings), el glow de las escenas, la dificultad de la siguiente carrera y el modo de
// pantalla y la resolución (DisplayApplier). Sin reglas: los
// valores y su validación están en SettingsModel y SettingsSerializer.
public static class SettingsService
{
	private static bool _loaded;

	// Los valores de ahora (también los que aún no se han guardado)
	public static SettingsModel Current { get; private set; } = new();

	// La primera vez: lee el archivo y aplica. Avisa en la consola de lo que no valía.
	public static void EnsureLoaded()
	{
		if (_loaded)
			return;
		_loaded = true;
		SettingsStore store = NewStore();
		store.ResolutionCount = DisplayApplier.ResolutionCount;
		store.Load();
		if (store.Warning != null)
			GD.PushWarning($"[Settings] {store.Warning}");
		foreach (string warning in store.OptionWarnings)
			GD.PushWarning($"[Settings] {warning}");
		Use(store.Settings.Options);
	}

	// Pasa a usar estos valores y los aplica (sin guardar)
	public static void Use(SettingsModel model)
	{
		if (Current != null)
			Current.Changed -= OnChanged;
		Current = model ?? new SettingsModel();
		Current.Changed += OnChanged;
		Apply();
	}

	// Guarda los de ahora junto a lo demás del archivo (pintura, fantasma, última nave), sin tocarlo
	public static bool Save()
	{
		SettingsStore store = NewStore();
		store.Load();
		store.Settings.Options = Current.Clone();
		bool saved = store.Save();
		if (!saved)
			GD.PushWarning($"[Settings] {store.Warning}");
		return saved;
	}

	private static void OnChanged(string id) => Apply();

	private static void Apply()
	{
		ActiveSettings.From(Current);
		GameSession.Difficulty = Current.Difficulty;
		WorldStyle.ApplyBloom();
		DisplayApplier.Apply(Current);
	}

	private static SettingsStore NewStore() => new(new FileRecordStorage(SettingsStore.DefaultPath));
}
