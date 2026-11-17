using System.Collections.Generic;
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

// SETTINGS (spec docs/settings.md, 10, pruebas 1–11; la 10, PARTICLES, no aplica: está en COMING SOON)
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
			  "effects": { "speedLines": false, "cockpitShake": 40 }
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
				model.Set(SettingsCatalog.SpeedFov, step);
				model.Set(SettingsCatalog.CockpitShake, step);
				ActiveSettings.From(model);
				Assert.Equal(factor, ActiveSettings.Bloom, 5);
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
		Assert.DoesNotContain(SettingsCatalog.TunnelVision, visited);
		Assert.DoesNotContain(SettingsCatalog.Particles, visited);
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
}
