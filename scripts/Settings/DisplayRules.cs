using System;

namespace Vectory.Settings;

// Modo de pantalla (spec docs/settings.md, 4.1), en el orden de la lista de SETTINGS
public enum DisplayMode
{
	Windowed,     // ventana con marco, del tamaño de la resolución, centrada
	Fullscreen,   // pantalla completa exclusiva
	Borderless    // ventana sin marco del tamaño de la pantalla (lo de antes de SETTINGS)
}

// Resoluciones de SETTINGS (spec 4.2): la lista fija de menor a mayor; solo salen las que caben en la pantalla del
// jugador. En ventana, es su tamaño; en pantalla completa y sin bordes, la del 3D: escala = resolución / pantalla, y el
// HUD y los menús siguen a la de la pantalla. Lógica pura.
public static class DisplayRules
{
	public static readonly (int Width, int Height)[] Resolutions =
	{
		(1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160)
	};

	// Cuántas de la lista caben en una pantalla (al menos la primera, para que siempre haya una)
	public static int FittingCount(int screenWidth, int screenHeight)
	{
		int count = 0;
		foreach ((int w, int h) in Resolutions)
		{
			if (w <= screenWidth && h <= screenHeight)
				count++;
		}
		return Math.Max(1, count);
	}

	// Índice de la mayor que cabe
	public static int LargestFitting(int screenWidth, int screenHeight) => FittingCount(screenWidth, screenHeight) - 1;

	// La resolución guardada si cabe; si no (otro monitor), la mayor que cabe
	public static int Resolve(int index, int screenWidth, int screenHeight) =>
		Math.Clamp(index, 0, LargestFitting(screenWidth, screenHeight));

	// Escala del 3D en pantalla completa y sin bordes (en ventana, 1): la de la resolución respecto a la pantalla,
	// como mucho 1
	public static float Scale3D(DisplayMode mode, int index, int screenWidth)
	{
		if (mode == DisplayMode.Windowed || screenWidth <= 0)
			return 1f;
		int width = Resolutions[Math.Clamp(index, 0, Resolutions.Length - 1)].Width;
		return Math.Clamp(width / (float)screenWidth, 0.1f, 1f);
	}
}

// Lo que pide la confirmación de pantalla en un paso
public enum DisplayConfirmEvent
{
	None,
	Show,     // sacar KEEP THIS DISPLAY?
	Revert    // se acabó la cuenta atrás: volver a lo de antes
}

// Confirmación de pantalla (spec 4.3): al cambiar el modo o la resolución, el cambio se aplica al momento y, Delay después
// del último cambio, sale KEEP THIS DISPLAY? con una cuenta atrás de Countdown. KEEP se queda; REVERT, ESC o el fin de la
// cuenta atrás vuelven al modo y la resolución de antes del primer cambio. Lógica pura: el tiempo llega con Update.
public sealed class DisplayConfirm
{
	public float Delay { get; set; } = 0.5f;
	public float Countdown { get; set; } = 10f;

	private float _wait;

	// Esperando a sacar la pregunta (aún se puede seguir cambiando) y la pregunta en pantalla
	public bool Pending { get; private set; }
	public bool Showing { get; private set; }

	// Lo que había antes del primer cambio: modo y resolución
	public (int Mode, int Resolution) Before { get; private set; }

	public float Remaining { get; private set; }

	// Segundos de la cuenta atrás, de segundo en segundo
	public int SecondsLeft => (int)MathF.Ceiling(Math.Max(0f, Remaining));

	// Un cambio de modo o resolución; before: lo que había justo antes de este cambio
	public void Changed(int beforeMode, int beforeResolution)
	{
		if (Showing)
			return;
		if (!Pending)
			Before = (beforeMode, beforeResolution);
		Pending = true;
		_wait = Delay;
	}

	public DisplayConfirmEvent Update(float dt)
	{
		if (Pending)
		{
			_wait -= dt;
			if (_wait > 0f)
				return DisplayConfirmEvent.None;
			Pending = false;
			Showing = true;
			Remaining = Countdown;
			return DisplayConfirmEvent.Show;
		}
		if (Showing)
		{
			Remaining -= dt;
			if (Remaining > 0f)
				return DisplayConfirmEvent.None;
			Showing = false;
			return DisplayConfirmEvent.Revert;
		}
		return DisplayConfirmEvent.None;
	}

	// Sacar la pregunta ya, sin esperar (al intentar salir de SETTINGS con un cambio sin confirmar)
	public void ShowNow()
	{
		if (Pending)
			_wait = 0f;
	}

	// KEEP: se queda
	public void Keep()
	{
		Showing = false;
		Pending = false;
	}

	// REVERT o ESC: hay que volver a Before
	public (int Mode, int Resolution) Revert()
	{
		Showing = false;
		Pending = false;
		return Before;
	}
}
