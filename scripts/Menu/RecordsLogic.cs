using System;

namespace Vectory.Menu;

public enum RecordsState { Viewing, ConfirmReset }

// Qué tiene que hacer quien usa la pantalla RECORDS tras una llamada
public enum RecordsAction { None, BackToMenu, ResetRecords }

// Lógica de la pantalla RECORDS (sin Godot): circuito que se ve, opciones y confirmación del borrado.
// No lee ni borra archivos ni cambia de escena: devuelve qué hay que hacer.
//
//   Viewing: BACK o ESC -> BackToMenu; RESET RECORDS -> ConfirmReset (NO seleccionado).
//            RESET RECORDS está desactivada si no hay ningún récord.
//            Circuito anterior / siguiente con vuelta (sin efecto con un solo circuito).
//   ConfirmReset: NO o ESC -> Viewing con RESET RECORDS seleccionada; YES -> ResetRecords y
//            vuelve a Viewing sin récords (BACK seleccionada, RESET desactivada).
//            No se cambia de circuito.
public sealed class RecordsLogic
{
	public const string BackId = "BACK";
	public const string ResetId = "RESET_RECORDS";
	public const string NoId = "NO";
	public const string YesId = "YES";

	private static readonly MenuOption[] ConfirmOptions =
	{
		new(NoId, "NO", true),
		new(YesId, "YES", true)
	};

	public RecordsState State { get; private set; } = RecordsState.Viewing;

	public int CircuitCount { get; }

	// Circuito que se muestra (índice en la lista de circuitos)
	public int CircuitIndex { get; private set; }

	// Hay algún récord guardado (de cualquier circuito y modo): activa RESET RECORDS
	public bool HasAnyRecord { get; private set; }

	// Lista que se está mostrando: BACK / RESET RECORDS, o NO / YES en la confirmación
	public MenuModel Menu { get; private set; }

	// Se puede cambiar de circuito ahora (más de uno y sin confirmación abierta)
	public bool CanChangeCircuit => CircuitCount > 1 && State == RecordsState.Viewing;

	public RecordsLogic(int circuitCount, bool hasAnyRecord)
	{
		if (circuitCount < 1)
			throw new ArgumentOutOfRangeException(nameof(circuitCount), "Debe haber al menos 1 circuito.");

		CircuitCount = circuitCount;
		HasAnyRecord = hasAnyRecord;
		Menu = ViewingMenu();
	}

	public void NextCircuit() => MoveCircuit(1);

	public void PreviousCircuit() => MoveCircuit(-1);

	// ESC: en Viewing vuelve al menú; en la confirmación es NO
	public RecordsAction Back()
	{
		return State == RecordsState.Viewing ? RecordsAction.BackToMenu : Choose(NoId);
	}

	// Elige una opción de la lista actual (por su identificador)
	public RecordsAction Choose(string id)
	{
		if (State == RecordsState.Viewing)
		{
			switch (id)
			{
				case BackId:
					return RecordsAction.BackToMenu;
				case ResetId when HasAnyRecord:
					State = RecordsState.ConfirmReset;
					Menu = new MenuModel(ConfirmOptions);   // NO seleccionado
					return RecordsAction.None;
			}
		}
		else
		{
			switch (id)
			{
				case NoId:
					State = RecordsState.Viewing;
					Menu = ViewingMenu();
					Menu.Select(1);   // RESET RECORDS
					return RecordsAction.None;
				case YesId:
					State = RecordsState.Viewing;
					HasAnyRecord = false;
					Menu = ViewingMenu();
					return RecordsAction.ResetRecords;
			}
		}

		return RecordsAction.None;
	}

	private MenuModel ViewingMenu()
	{
		return new MenuModel(new[]
		{
			new MenuOption(BackId, "BACK", true),
			new MenuOption(ResetId, "RESET RECORDS", HasAnyRecord)
		});
	}

	private void MoveCircuit(int step)
	{
		if (!CanChangeCircuit)
			return;

		CircuitIndex = ((CircuitIndex + step) % CircuitCount + CircuitCount) % CircuitCount;
	}
}
