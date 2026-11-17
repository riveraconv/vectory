using System.Collections.Generic;
using System.Linq;
using Vectory.Settings;

namespace Vectory.Menu;

// Lo que pide la pantalla SETTINGS al nodo que la dibuja
public enum SettingsAction
{
	None,
	Changed,         // un valor ha cambiado (ya está en el modelo)
	OpenConfirm,     // RESET DEFAULTS: pregunta
	CloseConfirm,    // NO o ESC en la pregunta
	ResetDone,       // YES: todo por defecto (ya en el modelo); hay que guardar y avisar
	Back,            // BACK o ESC: hay que guardar y salir
	ListenKey,       // ENTER en una tecla: PRESS A KEY
	KeyDone          // se ha pulsado la tecla nueva (o ESC): ver LastRebind
}

// SETTINGS sin nodos (spec docs/settings.md, 3.3 y 7.3): una fila por ajuste del catálogo y, al final, RESET DEFAULTS y
// BACK. La selección es un MenuModel: salta las de COMING SOON y da la vuelta. A/D cambian el valor de la fila sin dar
// la vuelta; ENTER en una lista da la vuelta, en sí / no cambia y en RESET DEFAULTS y BACK los elige. ENTER en una tecla
// espera la nueva (spec 6.2): la siguiente tecla pulsada va a KeyPressed; ESC cancela y F1–F12 se ignoran. RESET
// DEFAULTS pide NO / YES con NO elegida. Lógica pura.
public sealed class SettingsScreenLogic
{
	public const string ResetId = "RESET";
	public const string BackId = "BACK";
	public const string NoId = "NO";
	public const string YesId = "YES";

	public SettingsModel Model { get; }
	public MenuModel Menu { get; }
	public MenuModel ConfirmMenu { get; private set; }
	public bool Confirming { get; private set; }

	// Esperando la tecla nueva de la fila elegida (PRESS A KEY)
	public bool Listening { get; private set; }

	// Lo que pasó con la última tecla pulsada esperando
	public RebindOutcome LastRebind { get; private set; }

	public SettingsScreenLogic(SettingsModel model)
	{
		Model = model;
		var options = SettingsCatalog.All.Select(d => new MenuOption(d.Id, d.Label, !d.ComingSoon)).ToList();
		options.Add(new MenuOption(ResetId, "RESET DEFAULTS", true));
		options.Add(new MenuOption(BackId, "BACK", true));
		Menu = new MenuModel(options);
	}

	// La fila elegida: su ajuste (null en RESET DEFAULTS y BACK) y su id
	public string SelectedId => Menu.Options[Menu.SelectedIndex].Id;
	public SettingDef Selected => SettingsCatalog.Find(SelectedId);

	// Al entrar, la primera fila (DIFFICULTY)
	public void SelectFirst() => Menu.Select(0);

	public void Select(string id)
	{
		IReadOnlyList<MenuOption> options = Menu.Options;
		for (int i = 0; i < options.Count; i++)
		{
			if (options[i].Id == id)
				Menu.Select(i);
		}
	}

	public SettingsAction Up()
	{
		if (Confirming)
			ConfirmMenu.MovePrevious();
		else
			Menu.MovePrevious();
		return SettingsAction.None;
	}

	public SettingsAction Down()
	{
		if (Confirming)
			ConfirmMenu.MoveNext();
		else
			Menu.MoveNext();
		return SettingsAction.None;
	}

	// A / D (o las flechas): el valor de la fila; en RESET DEFAULTS / BACK y en la pregunta, de una opción a la otra
	public SettingsAction Horizontal(int direction)
	{
		if (Confirming)
		{
			if (direction < 0) ConfirmMenu.MovePrevious(); else ConfirmMenu.MoveNext();
			return SettingsAction.None;
		}
		if (Selected == null)
		{
			Select(SelectedId == ResetId ? BackId : ResetId);
			return SettingsAction.None;
		}
		return Model.Change(SelectedId, direction) ? SettingsAction.Changed : SettingsAction.None;
	}

	// ENTER / ESPACIO
	public SettingsAction Accept()
	{
		if (Confirming)
			return Choose(ConfirmMenu.Options[ConfirmMenu.SelectedIndex].Id);
		return Choose(SelectedId);
	}

	// Elegir una opción por su id (también con el ratón)
	public SettingsAction Choose(string id)
	{
		if (Confirming)
		{
			Confirming = false;
			if (id != YesId)
				return SettingsAction.CloseConfirm;
			Model.Reset();
			return SettingsAction.ResetDone;
		}

		switch (id)
		{
			case ResetId:
				Confirming = true;
				ConfirmMenu = new MenuModel(new[] { new MenuOption(NoId, "NO", true), new MenuOption(YesId, "YES", true) });
				return SettingsAction.OpenConfirm;
			case BackId:
				return SettingsAction.Back;
			default:
				Select(id);
				if (Selected?.Kind == SettingKind.Key)
				{
					KeyAction action = KeyBindings.Find(id);
					if (action == null || action.Fixed)
						return SettingsAction.None;
					Listening = true;
					return SettingsAction.ListenKey;
				}
				return Model.Cycle(id) ? SettingsAction.Changed : SettingsAction.None;
		}
	}

	// La tecla pulsada esperando la nueva (código físico de Godot). Con F1–F12 o una tecla que no vale, sigue esperando.
	public SettingsAction KeyPressed(long key)
	{
		if (!Listening)
			return SettingsAction.None;
		LastRebind = Model.Keys.Rebind(SelectedId, key);
		if (LastRebind.KeepWaiting)
			return SettingsAction.None;
		Listening = false;
		return SettingsAction.KeyDone;
	}

	// ESC: en la pregunta, NO; si no, BACK
	public SettingsAction Back()
	{
		if (Confirming)
		{
			Confirming = false;
			return SettingsAction.CloseConfirm;
		}
		return SettingsAction.Back;
	}
}
