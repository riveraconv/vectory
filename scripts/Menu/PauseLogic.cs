namespace Vectory.Menu;

public enum PauseState { Closed, Open, Confirm }

// Qué se está confirmando en el estado Confirm
public enum PauseTarget { None, Restart, BackToMenu }

// Qué tiene que hacer quien usa la pausa tras una llamada
public enum PauseAction { None, Close, Restart, GoToMenu }

// Lógica de la pausa de carrera (sin Godot): estados, opciones y confirmación.
// No cambia de escena ni congela nada: devuelve qué hay que hacer y el nodo lo ejecuta.
//
//   Closed --ESC--> Open (CONTINUE seleccionada)
//   Open: CONTINUE o ESC -> Closed; RESTART / BACK TO MAIN MENU -> Confirm (NO seleccionado),
//         o directamente Restart / GoToMenu si la carrera ya ha terminado.
//   Confirm: NO o ESC -> Open con la opción de origen seleccionada; YES -> Restart / GoToMenu.
//
// La navegación de cada lista es un MenuModel (Menu): la pausa o la confirmación, según el estado.
public sealed class PauseLogic
{
	public const string ContinueId = "CONTINUE";
	public const string RestartId = "RESTART";
	public const string BackToMenuId = "BACK_TO_MENU";
	public const string NoId = "NO";
	public const string YesId = "YES";

	private static readonly MenuOption[] PauseOptions =
	{
		new(ContinueId, "CONTINUE", true),
		new(RestartId, "RESTART", true),
		new(BackToMenuId, "BACK TO MAIN MENU", true)
	};

	private static readonly MenuOption[] ConfirmOptions =
	{
		new(NoId, "NO", true),
		new(YesId, "YES", true)
	};

	private MenuModel _pauseMenu = new(PauseOptions);

	public PauseState State { get; private set; } = PauseState.Closed;

	// Qué se confirma (None fuera del estado Confirm)
	public PauseTarget Confirming { get; private set; } = PauseTarget.None;

	// La carrera ya ha terminado: RESTART y BACK TO MAIN MENU no piden confirmación
	public bool RaceFinished { get; set; }

	// Lista que se está mostrando: la de la pausa o la de la confirmación (null si está cerrada)
	public MenuModel Menu { get; private set; }

	// ESC: abre la pausa, la cierra (= CONTINUE) o sale de la confirmación (= NO)
	public PauseAction TogglePause()
	{
		switch (State)
		{
			case PauseState.Closed:
				_pauseMenu = new MenuModel(PauseOptions);   // al abrir, siempre CONTINUE
				Menu = _pauseMenu;
				State = PauseState.Open;
				return PauseAction.None;

			case PauseState.Open:
				return Close();

			default:
				return Choose(NoId);
		}
	}

	// Elige una opción de la lista actual (por su identificador)
	public PauseAction Choose(string id)
	{
		if (State == PauseState.Open)
		{
			switch (id)
			{
				case ContinueId:
					return Close();
				case RestartId:
					return RaceFinished ? PauseAction.Restart : StartConfirm(PauseTarget.Restart);
				case BackToMenuId:
					return RaceFinished ? PauseAction.GoToMenu : StartConfirm(PauseTarget.BackToMenu);
			}
		}
		else if (State == PauseState.Confirm)
		{
			switch (id)
			{
				case NoId:
					return BackToPause();
				case YesId:
					return Confirming == PauseTarget.Restart ? PauseAction.Restart : PauseAction.GoToMenu;
			}
		}

		return PauseAction.None;
	}

	private PauseAction Close()
	{
		State = PauseState.Closed;
		Confirming = PauseTarget.None;
		Menu = null;
		return PauseAction.Close;
	}

	private PauseAction StartConfirm(PauseTarget target)
	{
		State = PauseState.Confirm;
		Confirming = target;
		Menu = new MenuModel(ConfirmOptions);   // NO seleccionado
		return PauseAction.None;
	}

	// Vuelve a la pausa con la opción que se estaba confirmando seleccionada
	private PauseAction BackToPause()
	{
		_pauseMenu.Select(IndexOf(Confirming == PauseTarget.Restart ? RestartId : BackToMenuId));
		State = PauseState.Open;
		Confirming = PauseTarget.None;
		Menu = _pauseMenu;
		return PauseAction.None;
	}

	private static int IndexOf(string id)
	{
		for (int i = 0; i < PauseOptions.Length; i++)
		{
			if (PauseOptions[i].Id == id)
				return i;
		}
		return -1;
	}
}
