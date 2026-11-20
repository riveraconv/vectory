using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Vectory.Circuit;
using Vectory.Game;
using Vectory.Menu;
using Vectory.Records;
using Vectory.Rivals;
using Vectory.Settings;
using Vectory.Ship;
using Xunit;

namespace Vectory.Test.GameTests;

// SETTINGS (spec docs/settings.md, 10, pruebas 1–11; la 10, PARTICLES en LOW con el túnel, en TunnelVisionFxTests)
public class SettingsTests
{
	// 1
	[Fact]
	public void By_default_every_multiplier_is_1_and_the_difficulty_is_normal()
	{
		var model = new SettingsModel();
		Assert.Equal(1f, model.Bloom);
		Assert.Equal(1f, model.SpeedFov);
		Assert.Equal(1f, model.CockpitShake);
		Assert.True(model.SpeedLines);
		Assert.Equal(Difficulty.Normal, model.Difficulty);
		Assert.Equal("BORDERLESS", model.ValueText(SettingsCatalog.DisplayMode));
		Assert.Equal("1920 × 1080", model.ValueText(SettingsCatalog.Resolution));
	}

	// 2
	[Fact]
	public void A_list_stops_at_its_ends_with_left_and_right_and_wraps_with_enter()
	{
		var model = new SettingsModel();
		Assert.True(model.Change(SettingsCatalog.Difficulty, 1));
		Assert.Equal(Difficulty.Hard, model.Difficulty);
		Assert.False(model.Change(SettingsCatalog.Difficulty, 1));
		Assert.Equal(Difficulty.Hard, model.Difficulty);
		Assert.True(model.Cycle(SettingsCatalog.Difficulty));
		Assert.Equal(Difficulty.Easy, model.Difficulty);
		Assert.False(model.Change(SettingsCatalog.Difficulty, -1));
		Assert.Equal(Difficulty.Easy, model.Difficulty);

		// Sí / no: A/D y ENTER cambian
		Assert.True(model.Change(SettingsCatalog.SpeedLines, 1));
		Assert.False(model.SpeedLines);
		Assert.True(model.Cycle(SettingsCatalog.SpeedLines));
		Assert.True(model.SpeedLines);
	}

	// 3
	[Fact]
	public void A_percent_goes_from_0_to_100_in_steps_of_10()
	{
		var model = new SettingsModel();
		Assert.False(model.Change(SettingsCatalog.Bloom, 1));
		Assert.Equal("100 %", model.ValueText(SettingsCatalog.Bloom));
		for (int i = 0; i < 15; i++)
			model.Change(SettingsCatalog.Bloom, -1);
		Assert.Equal(0f, model.Bloom);
		Assert.Equal("0 %", model.ValueText(SettingsCatalog.Bloom));
		model.Change(SettingsCatalog.Bloom, 1);
		Assert.Equal(0.1f, model.Bloom, 5);
		Assert.False(model.Set(SettingsCatalog.Bloom, 11));
		Assert.False(model.Set(SettingsCatalog.Bloom, -1));
		Assert.False(model.Cycle(SettingsCatalog.Bloom));
	}

	// 4
	[Fact]
	public void Reset_puts_every_value_back_to_its_default()
	{
		var model = new SettingsModel();
		model.Set(SettingsCatalog.Difficulty, 0);
		model.Set(SettingsCatalog.Bloom, 3);
		model.Set(SettingsCatalog.SpeedLines, 0);
		model.Set(SettingsCatalog.SpeedFov, 5);
		model.Set(SettingsCatalog.CockpitShake, 0);
		model.Set(SettingsCatalog.DisplayMode, 0);
		var changed = new List<string>();
		model.Changed += changed.Add;
		model.Reset();
		var fresh = new SettingsModel();
		foreach (string id in fresh.Ids)
			Assert.Equal(fresh.Get(id), model.Get(id));
		Assert.Equal(6, changed.Count);
	}

	private static SettingsModel Custom()
	{
		var model = new SettingsModel();
		model.Set(SettingsCatalog.Difficulty, 2);
		model.Set(SettingsCatalog.DisplayMode, 0);
		model.Set(SettingsCatalog.Resolution, 1);
		model.Set(SettingsCatalog.Bloom, 7);
		model.Set(SettingsCatalog.SpeedLines, 0);
		model.Set(SettingsCatalog.SpeedFov, 4);
		model.Set(SettingsCatalog.CockpitShake, 0);
		return model;
	}

	// 5
	[Fact]
	public void Saving_and_loading_gives_the_same_values()
	{
		var storage = new MemoryRecordStorage();
		var store = new SettingsStore(storage);
		store.Load();
		store.Settings.Options = Custom();
		Assert.True(store.Save());

		var again = new SettingsStore(storage);
		again.Load();
		Assert.Null(again.Warning);
		Assert.Empty(again.OptionWarnings);
		SettingsModel expected = Custom();
		foreach (string id in expected.Ids)
			Assert.Equal(expected.Get(id), again.Settings.Options.Get(id));
		Assert.Contains("\"resolution\": \"1600x900\"", storage.Text);
	}

	// 6
	[Fact]
	public void A_missing_value_one_of_another_type_and_one_out_of_range_take_their_default_only_them()
	{
		var storage = new MemoryRecordStorage
		{
			Text = """
			{
			  "version": 1,
			  "gameplay": { "difficulty": "HARD" },
			  "video": { "displayMode": 3, "resolution": "1280x720", "bloom": 250 },
			  "effects": { "tunnelVision": 70, "speedLines": false, "particles": "HIGH", "cockpitShake": 40 }
			}
			"""
		};
		var store = new SettingsStore(storage);
		store.Load();
		Assert.Null(store.Warning);
		SettingsModel model = store.Settings.Options;
		var defaults = new SettingsModel();

		// Los tres que no valen, por defecto
		Assert.Equal(defaults.Get(SettingsCatalog.DisplayMode), model.Get(SettingsCatalog.DisplayMode));
		Assert.Equal(defaults.Get(SettingsCatalog.Bloom), model.Get(SettingsCatalog.Bloom));
		Assert.Equal(defaults.Get(SettingsCatalog.SpeedFov), model.Get(SettingsCatalog.SpeedFov));
		Assert.Equal(3, store.OptionWarnings.Count);

		// Los demás, como estaban
		Assert.Equal(Difficulty.Hard, model.Difficulty);
		Assert.Equal("1280 × 720", model.ValueText(SettingsCatalog.Resolution));
		Assert.Equal(0.7f, model.TunnelVision, 5);
		Assert.False(model.SpeedLines);
		Assert.Equal(0.4f, model.CockpitShake, 5);
	}

	// 7
	[Fact]
	public void Saving_settings_does_not_change_the_paint_keys()
	{
		var storage = new MemoryRecordStorage();
		var store = new SettingsStore(storage);
		store.Load();
		store.Settings.SetPaint(ShipCatalog.Ship01, HullPaints.ChoiceAt(HullStyle.Racing, 5));
		store.Settings.LastShipDesignId = "ship_02";
		store.Settings.GhostEnabled = false;
		store.Save();

		var again = new SettingsStore(storage);
		again.Load();
		again.Settings.Options = Custom();
		again.Save();

		var last = new SettingsStore(storage);
		last.Load();
		Assert.Equal(HullPaints.ChoiceAt(HullStyle.Racing, 5), last.Settings.PaintFor(ShipCatalog.Ship01));
		Assert.Equal("ship_02", last.Settings.LastShipDesignId);
		Assert.False(last.Settings.GhostEnabled);
		Assert.Equal(Difficulty.Hard, last.Settings.Options.Difficulty);
	}

	// 8
	[Fact]
	public void At_100_percent_the_multipliers_leave_everything_as_before_at_50_half_and_at_0_nothing()
	{
		var model = new SettingsModel();
		try
		{
			foreach ((int step, float factor) in new[] { (10, 1f), (5, 0.5f), (0, 0f) })
			{
				model.Set(SettingsCatalog.Bloom, step);
				model.Set(SettingsCatalog.TunnelVision, step);
				model.Set(SettingsCatalog.SpeedFov, step);
				model.Set(SettingsCatalog.CockpitShake, step);
				ActiveSettings.From(model);
				Assert.Equal(factor, ActiveSettings.Bloom, 5);
				Assert.Equal(factor, ActiveSettings.TunnelVision, 5);
				Assert.Equal(factor, ActiveSettings.CockpitShake, 5);
				Assert.Equal(factor, ActiveSettings.SpeedFov, 5);
				// FOV por velocidad de la cámara exterior: de 75 a 95 al 100 %, a 85 al 50 %, a 75 al 0 %
				Assert.Equal(75f + 20f * factor, ActiveSettings.ScaledMaxFov(75f, 95f), 4);
			}
		}
		finally
		{
			ActiveSettings.From(new SettingsModel());
		}
	}

	// 9
	[Fact]
	public void Speed_lines_off_removes_the_slipstream_air_lines()
	{
		var model = new SettingsModel();
		try
		{
			model.Set(SettingsCatalog.SpeedLines, 0);
			ActiveSettings.From(model);
			Assert.False(ActiveSettings.SpeedLines);
			// El Player monta las líneas de aire con 0 líneas: no hay nada que dibujar, aunque haya rebufo
			AirLineSettings settings = AirLineSettings.Default;
			settings.Count = ActiveSettings.SpeedLines ? settings.Count : 0;
			Assert.Empty(AirLines.Segments(1f, 100f, 1f, settings));
		}
		finally
		{
			ActiveSettings.From(new SettingsModel());
		}
	}

	// 11
	[Fact]
	public void Cockpit_shake_at_0_does_not_change_the_shake_from_hits()
	{
		var model = new SettingsModel();
		try
		{
			var shaken = new CameraShake(CameraShakeSettings.Default);
			shaken.Trigger(ImpactLevel.Strong);
			float amplitude = shaken.Amplitude;

			model.Set(SettingsCatalog.CockpitShake, 0);
			ActiveSettings.From(model);
			var still = new CameraShake(CameraShakeSettings.Default);
			still.Trigger(ImpactLevel.Strong);
			Assert.True(amplitude > 0f);
			Assert.Equal(amplitude, still.Amplitude);
		}
		finally
		{
			ActiveSettings.From(new SettingsModel());
		}
	}

	// 12
	[Fact]
	public void The_resolution_list_only_has_the_ones_that_fit_on_the_screen()
	{
		Assert.Equal(3, DisplayRules.FittingCount(1920, 1080));
		Assert.Equal(5, DisplayRules.FittingCount(3840, 2160));
		Assert.Equal(4, DisplayRules.FittingCount(2560, 1600));
		Assert.Equal(1, DisplayRules.FittingCount(1366, 768));
		Assert.Equal(1, DisplayRules.FittingCount(800, 600));   // siempre al menos una

		// En un 1920 × 1080, la lista acaba en 1920 × 1080 y es la de por defecto
		var model = new SettingsModel(DisplayRules.FittingCount(1920, 1080));
		Assert.Equal("1920 × 1080", model.ValueText(SettingsCatalog.Resolution));
		Assert.False(model.Change(SettingsCatalog.Resolution, 1));
		Assert.False(model.Set(SettingsCatalog.Resolution, 3));
		Assert.True(model.Cycle(SettingsCatalog.Resolution));
		Assert.Equal("1280 × 720", model.ValueText(SettingsCatalog.Resolution));

		// Escala del 3D: en pantalla completa, la resolución respecto a la pantalla; en ventana, 1
		Assert.Equal(1f, DisplayRules.Scale3D(DisplayMode.Borderless, 2, 1920));
		Assert.Equal(1280f / 1920f, DisplayRules.Scale3D(DisplayMode.Fullscreen, 0, 1920), 5);
		Assert.Equal(1f, DisplayRules.Scale3D(DisplayMode.Windowed, 0, 1920));
	}

	// 13
	[Fact]
	public void Keep_stays_and_revert_escape_or_the_10_seconds_go_back()
	{
		var confirm = new DisplayConfirm();
		confirm.Changed(2, 2);
		Assert.Equal(DisplayConfirmEvent.None, confirm.Update(0.4f));
		Assert.Equal(DisplayConfirmEvent.Show, confirm.Update(0.2f));
		Assert.Equal(10, confirm.SecondsLeft);
		confirm.Update(3.2f);
		Assert.Equal(7, confirm.SecondsLeft);
		confirm.Keep();
		Assert.False(confirm.Showing);
		Assert.Equal(DisplayConfirmEvent.None, confirm.Update(20f));

		// REVERT (o ESC, que hace lo mismo): a lo de antes
		confirm.Changed(0, 1);
		confirm.Update(0.6f);
		Assert.Equal((0, 1), confirm.Revert());
		Assert.False(confirm.Showing);

		// Sin contestar, a los 10 s
		confirm.Changed(1, 4);
		confirm.Update(0.6f);
		Assert.Equal(DisplayConfirmEvent.None, confirm.Update(9.9f));
		Assert.Equal(DisplayConfirmEvent.Revert, confirm.Update(0.2f));
		Assert.Equal((1, 4), confirm.Before);
	}

	// 14
	[Fact]
	public void Three_quick_changes_give_one_question_and_revert_goes_back_to_before_the_first()
	{
		var confirm = new DisplayConfirm();
		confirm.Changed(2, 0);
		confirm.Update(0.2f);
		confirm.Changed(2, 1);
		confirm.Update(0.2f);
		confirm.Changed(2, 2);
		Assert.Equal(DisplayConfirmEvent.None, confirm.Update(0.4f));
		Assert.Equal(DisplayConfirmEvent.Show, confirm.Update(0.2f));
		Assert.Equal(DisplayConfirmEvent.None, confirm.Update(0.5f));
		Assert.Equal((2, 0), confirm.Revert());

		// Salir con la pregunta pendiente la saca ya
		confirm.Changed(2, 1);
		confirm.ShowNow();
		Assert.Equal(DisplayConfirmEvent.Show, confirm.Update(0.01f));
	}

	// 15
	[Fact]
	public void A_saved_resolution_that_does_not_fit_becomes_the_largest_that_fits()
	{
		var storage = new MemoryRecordStorage
		{
			Text = """
			{ "version": 1, "video": { "displayMode": "WINDOWED", "resolution": "3840x2160", "bloom": 100 } }
			"""
		};
		var store = new SettingsStore(storage) { ResolutionCount = DisplayRules.FittingCount(2560, 1440) };
		store.Load();
		Assert.Equal("2560 × 1440", store.Settings.Options.ValueText(SettingsCatalog.Resolution));
		Assert.Equal(DisplayMode.Windowed, store.Settings.Options.DisplayMode);
		Assert.Single(store.OptionWarnings);
		Assert.Equal(DisplayRules.LargestFitting(2560, 1440), DisplayRules.Resolve(4, 2560, 1440));
	}

	// La pantalla: W/S saltan las de COMING SOON y dan la vuelta; RESET DEFAULTS pregunta con NO elegida
	[Fact]
	public void The_screen_skips_coming_soon_rows_wraps_and_asks_before_resetting()
	{
		var logic = new SettingsScreenLogic(new SettingsModel());
		logic.SelectFirst();
		Assert.Equal(SettingsCatalog.Difficulty, logic.SelectedId);
		var visited = new HashSet<string>();
		for (int i = 0; i < 40; i++)
		{
			logic.Down();
			visited.Add(logic.SelectedId);
		}
		Assert.Contains(SettingsCatalog.TunnelVision, visited);   // spec docs/efectos-9-vision-tunel.md: ya no es COMING SOON
		Assert.Contains(SettingsCatalog.Particles, visited);
		Assert.DoesNotContain("audioMaster", visited);
		Assert.Contains(SettingsScreenLogic.BackId, visited);

		logic.SelectFirst();
		logic.Up();
		Assert.Equal(SettingsScreenLogic.BackId, logic.SelectedId);
		logic.Up();
		Assert.Equal(SettingsScreenLogic.ResetId, logic.SelectedId);

		logic.Model.Set(SettingsCatalog.Bloom, 2);
		Assert.Equal(SettingsAction.OpenConfirm, logic.Accept());
		Assert.Equal(SettingsScreenLogic.NoId, logic.ConfirmMenu.Confirm());
		Assert.Equal(SettingsAction.CloseConfirm, logic.Accept());
		Assert.Equal(0.2f, logic.Model.Bloom, 5);

		logic.Accept();
		logic.Down();
		Assert.Equal(SettingsAction.ResetDone, logic.Accept());
		Assert.Equal(1f, logic.Model.Bloom, 5);

		// ESC sale (y en la pregunta, NO)
		Assert.Equal(SettingsAction.Back, logic.Back());
		logic.Accept();
		Assert.Equal(SettingsAction.CloseConfirm, logic.Back());
	}

	// 16
	[Fact]
	public void Changing_accelerate_to_a_free_key_gives_it_to_accelerate()
	{
		var model = new SettingsModel();
		var changed = new List<string>();
		model.Changed += changed.Add;

		RebindOutcome outcome = model.Keys.Rebind("keyAccelerate", 'E');

		Assert.Equal(RebindResult.Assigned, outcome.Result);
		Assert.Equal('E', model.Keys.Get("keyAccelerate"));
		Assert.Equal('S', model.Keys.Get("keyBrake"));
		Assert.Equal(new[] { "keyAccelerate" }, changed);
		Assert.Equal("E", model.ValueText("keyAccelerate"));
	}

	// 17
	[Fact]
	public void Changing_accelerate_to_s_swaps_it_with_brake()
	{
		var logic = new SettingsScreenLogic(new SettingsModel());
		logic.Select("keyAccelerate");

		Assert.Equal(SettingsAction.ListenKey, logic.Accept());
		Assert.Equal(SettingsAction.KeyDone, logic.KeyPressed('S'));

		Assert.Equal(RebindResult.Swapped, logic.LastRebind.Result);
		Assert.Equal("keyBrake", logic.LastRebind.Other);
		Assert.Equal('S', logic.Model.Keys.Get("keyAccelerate"));
		Assert.Equal('W', logic.Model.Keys.Get("keyBrake"));
		Assert.False(logic.Listening);
	}

	// 18
	[Fact]
	public void Escape_and_f1_to_f12_are_not_accepted_and_escape_cancels()
	{
		var logic = new SettingsScreenLogic(new SettingsModel());
		logic.Select("keyBoost");
		logic.Accept();

		for (long key = KeyBindings.F1; key <= KeyBindings.F12; key++)
		{
			Assert.Equal(SettingsAction.None, logic.KeyPressed(key));
			Assert.True(logic.Listening);
		}
		// La segunda tecla de otra acción (las flechas) tampoco: una tecla no hace dos cosas
		Assert.Equal(SettingsAction.None, logic.KeyPressed(KeyNames.Up));
		Assert.Equal(RebindResult.InUse, logic.LastRebind.Result);
		Assert.True(logic.Listening);

		Assert.Equal(SettingsAction.KeyDone, logic.KeyPressed(KeyBindings.Escape));
		Assert.Equal(RebindResult.Cancelled, logic.LastRebind.Result);
		Assert.False(logic.Listening);
		Assert.Equal(KeyNames.Shift, logic.Model.Keys.Get("keyBoost"));
	}

	// 19
	[Fact]
	public void Pause_cannot_be_changed()
	{
		var logic = new SettingsScreenLogic(new SettingsModel());
		logic.Select("keyPause");

		Assert.Equal(SettingsAction.None, logic.Accept());
		Assert.False(logic.Listening);
		Assert.Equal(RebindResult.Fixed, logic.Model.Keys.Rebind("keyPause", 'P').Result);
		Assert.Equal(KeyBindings.Escape, logic.Model.Keys.Get("keyPause"));
		Assert.Equal("ESC", logic.Model.ValueText("keyPause"));
		Assert.False(logic.Model.Keys.Set("keyPause", 'P'));
	}

	// 20
	[Fact]
	public void The_menu_keys_do_not_change_with_the_driving_keys()
	{
		Dictionary<string, List<long>> map = ProjectInputMap();

		// Las acciones de la tabla existen, con su tecla por defecto primero y la segunda detrás
		foreach (KeyAction action in KeyBindings.Actions)
		{
			Assert.True(map.ContainsKey(action.Action), action.Action);
			Assert.Equal(action.Default, map[action.Action][0]);
			if (action.Secondary != 0)
				Assert.Equal(action.Secondary, map[action.Action][1]);
			Assert.False(action.Action.StartsWith("menu_"), action.Action);
		}
		// Los menús tienen sus propias acciones, que SETTINGS no toca
		Assert.Equal(new long[] { 'W', KeyNames.Up }, map["menu_up"]);
		Assert.Equal(new long[] { 'S', KeyNames.Down }, map["menu_down"]);
	}

	[Fact]
	public void Key_names_are_godot_physical_keys()
	{
		Assert.Equal((long)Godot.Key.Escape, KeyBindings.Escape);
		Assert.Equal((long)Godot.Key.F1, KeyBindings.F1);
		Assert.Equal((long)Godot.Key.F12, KeyBindings.F12);
		Assert.Equal((long)Godot.Key.Shift, KeyNames.Shift);
		Assert.Equal((long)Godot.Key.Up, KeyNames.Up);
		Assert.Equal((long)Godot.Key.Down, KeyNames.Down);
		Assert.Equal((long)Godot.Key.Left, KeyNames.Left);
		Assert.Equal((long)Godot.Key.Right, KeyNames.Right);
		Assert.Equal((long)Godot.Key.W, 'W');
		Assert.Equal((long)Godot.Key.Space, ' ');

		foreach (Godot.Key key in new[] { Godot.Key.Enter, Godot.Key.Tab, Godot.Key.Ctrl, Godot.Key.Alt, Godot.Key.Kp0,
			Godot.Key.Kp9, Godot.Key.KpMultiply, Godot.Key.KpAdd, Godot.Key.Pageup, Godot.Key.Capslock, Godot.Key.Q, Godot.Key.Key7 })
		{
			string name = KeyNames.Name((long)key);
			Assert.DoesNotContain("#", name);
			Assert.True(KeyNames.TryParse(name, out long back), name);
			Assert.Equal((long)key, back);
		}
		Assert.Equal("KP 0", KeyNames.Name((long)Godot.Key.Kp0));
		Assert.Equal("CTRL", KeyNames.Name((long)Godot.Key.Ctrl));
		Assert.True(KeyNames.TryParse("#123456", out long raw));
		Assert.Equal(123456, raw);
		Assert.True(KeyNames.TryParse("shift", out long shift));
		Assert.Equal(KeyNames.Shift, shift);
	}

	[Fact]
	public void Keys_are_saved_loaded_and_reset_with_the_rest()
	{
		var model = new SettingsModel();
		model.Keys.Rebind("keyAccelerate", 'S');
		model.Keys.Rebind("keyBoost", 'E');

		Dictionary<string, JsonElement> sections = SettingsSerializer.Write(model);
		SettingsModel loaded = SettingsSerializer.Read(sections, new List<string>());
		foreach (KeyAction action in KeyBindings.Actions)
			Assert.Equal(model.Keys.Get(action.Id), loaded.Keys.Get(action.Id));
		Assert.Equal("E", sections["controls"].GetProperty("keyBoost").GetString());
		Assert.False(sections["controls"].TryGetProperty("keyPause", out _));
		Assert.Equal('S', model.Clone().Keys.Get("keyAccelerate"));

		loaded.Reset();
		foreach (KeyAction action in KeyBindings.Actions)
			Assert.Equal(action.Default, loaded.Keys.Get(action.Id));
	}

	[Fact]
	public void A_bad_key_in_the_file_takes_its_default_and_shared_keys_reset_them_all()
	{
		var warnings = new List<string>();
		var sections = new Dictionary<string, JsonElement>
		{
			["controls"] = JsonDocument.Parse("{ \"keyAccelerate\": \"Q\", \"keyBrake\": \"F5\", \"keySteerLeft\": 7, \"keySteerRight\": \"UP\", \"keyBoost\": \"NOPE\" }").RootElement
		};
		SettingsModel model = SettingsSerializer.Read(sections, warnings);
		Assert.Equal('Q', model.Keys.Get("keyAccelerate"));
		Assert.Equal('S', model.Keys.Get("keyBrake"));
		Assert.Equal('A', model.Keys.Get("keySteerLeft"));
		Assert.Equal('D', model.Keys.Get("keySteerRight"));
		Assert.Equal(KeyNames.Shift, model.Keys.Get("keyBoost"));
		Assert.Equal(6, warnings.Count);   // 4 que no valen + DRIFT y CAMERA, que faltan

		warnings.Clear();
		sections["controls"] = JsonDocument.Parse("{ \"keyAccelerate\": \"S\", \"keyBrake\": \"S\", \"keySteerLeft\": \"A\", \"keySteerRight\": \"D\", \"keyBoost\": \"SHIFT\", \"keyDrift\": \"SPACE\", \"keyCamera\": \"C\" }").RootElement;
		model = SettingsSerializer.Read(sections, warnings);
		Assert.Equal('W', model.Keys.Get("keyAccelerate"));
		Assert.Equal('S', model.Keys.Get("keyBrake"));
		Assert.Single(warnings);
	}

	// Las acciones de project.godot con sus teclas físicas, en orden
	private static Dictionary<string, List<long>> ProjectInputMap()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
			dir = dir.Parent;
		string text = File.ReadAllText(Path.Combine(dir!.FullName, "project.godot")).Replace("\r\n", "\n");
		var map = new Dictionary<string, List<long>>();
		foreach (Match action in Regex.Matches(text, @"^([a-z_]+)=\{\n(.*?)\n\}", RegexOptions.Multiline | RegexOptions.Singleline))
		{
			map[action.Groups[1].Value] = Regex.Matches(action.Groups[2].Value, "\"physical_keycode\":(\\d+)")
				.Select(m => long.Parse(m.Groups[1].Value)).ToList();
		}
		return map;
	}
}
