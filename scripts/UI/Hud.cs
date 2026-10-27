using System;
using Godot;
using Vectory.Circuit;
using Vectory.Core;

namespace Vectory.UI;

// HUD de la demo. Solo MUESTRA datos: no calcula reglas de conducción ni de carrera.
// Se crea por código desde Main, que le pasa los datos cada frame. Colores de Palette.
//
//   Arriba izquierda:  posición / participantes
//   Arriba derecha:    vuelta actual / total, tiempo de vuelta y récord de vuelta
//   Abajo derecha:     velocidad (km/h) con su barra y boost con su barra
//
// Es una capa 2D fija a la pantalla: en cabina queda pegada al marco de la cabina.
public partial class Hud : CanvasLayer
{
	private const float Margin = 40f;       // separación a los bordes de la pantalla
	private const float BarWidth = 260f;
	private const float BarHeight = 10f;
	private const string NoTime = "--:--.---";

	private enum Corner { TopLeft, TopRight, BottomRight }

	private Label _positionLabel;
	private Label _lapLabel;
	private Label _lapTimeLabel;
	private Label _bestLapLabel;
	private Label _speedLabel;
	private ColorRect _speedFill;
	private ColorRect _boostFill;

	public override void _Ready()
	{
		// Raíz que ocupa toda la pantalla; todo el HUD cuelga de ella
		var root = new Control
		{
			Name = "HudRoot",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetTop = 0f,
			OffsetRight = 0f,
			OffsetBottom = 0f
		};
		AddChild(root);

		BuildPositionBlock(root);
		BuildLapBlock(root);
		BuildSpeedBlock(root);
	}

	// ---------------------------------------------------------------
	// Datos que recibe del juego
	// ---------------------------------------------------------------

	// Velocidad en m/s. Actualiza el número (km/h) y las dos barras:
	//  - barra de velocidad: de 0 a boostMaxSpeed
	//  - barra de boost: vacía hasta normalMaxSpeed, llena en boostMaxSpeed
	public void SetSpeed(float metersPerSecond, float normalMaxSpeed, float boostMaxSpeed)
	{
		_speedLabel.Text = Mathf.RoundToInt(metersPerSecond * 3.6f).ToString();

		float speedFraction = boostMaxSpeed > 0f ? metersPerSecond / boostMaxSpeed : 0f;
		SetBar(_speedFill, speedFraction);

		float boostRange = boostMaxSpeed - normalMaxSpeed;
		float boostFraction = boostRange > 0f ? (metersPerSecond - normalMaxSpeed) / boostRange : 0f;
		SetBar(_boostFill, boostFraction);
	}

	// Estado de la carrera. Si todavía no existe (null), se dejan los valores por defecto.
	public void SetRace(RaceTracker race)
	{
		if (race == null)
			return;

		// Antes de arrancar CurrentLap vale 0; se muestra como vuelta 1
		int lap = Math.Max(1, race.CurrentLap);
		_lapLabel.Text = $"VUELTA {lap}/{race.TotalLaps}";
		_lapTimeLabel.Text = FormatTime(race.CurrentLapTime);
		_bestLapLabel.Text = $"MEJOR {(race.BestLapTime.HasValue ? FormatTime(race.BestLapTime.Value) : NoTime)}";
	}

	// Posición del jugador y total de participantes
	public void SetRacePosition(int position, int total)
	{
		_positionLabel.Text = $"POS {position}/{total}";
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private void BuildPositionBlock(Control root)
	{
		_positionLabel = AddLabel(root, "POS 1/1", 40, Palette.Cyan,
			HorizontalAlignment.Left, VerticalAlignment.Center,
			Corner.TopLeft, Margin, 30f, 260f, 50f);
	}

	private void BuildLapBlock(Control root)
	{
		_lapLabel = AddLabel(root, "VUELTA -/-", 28, Palette.Cyan,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.TopRight, Margin, 30f, 320f, 38f);

		_lapTimeLabel = AddLabel(root, FormatTime(0), 34, Palette.Cyan,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.TopRight, Margin, 70f, 320f, 42f);

		_bestLapLabel = AddLabel(root, $"MEJOR {NoTime}", 22, Palette.BlueGray,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.TopRight, Margin, 114f, 320f, 30f);
	}

	// De abajo hacia arriba: barra de boost, etiqueta BOOST, barra de velocidad, número y unidad
	private void BuildSpeedBlock(Control root)
	{
		AddBar(root, Palette.Orange, 36f, out _boostFill);

		AddLabel(root, "BOOST", 18, Palette.BlueGray,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.BottomRight, Margin, 52f, BarWidth, 24f);

		AddBar(root, Palette.Cyan, 92f, out _speedFill);

		_speedLabel = AddLabel(root, "0", 56, Palette.Cyan,
			HorizontalAlignment.Right, VerticalAlignment.Bottom,
			Corner.BottomRight, Margin + 80f, 108f, BarWidth - 80f, 64f);

		AddLabel(root, "KM/H", 20, Palette.BlueGray,
			HorizontalAlignment.Right, VerticalAlignment.Bottom,
			Corner.BottomRight, Margin, 108f, 76f, 64f);
	}

	private static Label AddLabel(Control parent, string text, int fontSize, Color color,
		HorizontalAlignment horizontal, VerticalAlignment vertical,
		Corner corner, float marginX, float marginY, float width, float height)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = horizontal,
			VerticalAlignment = vertical,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = new LabelSettings { FontSize = fontSize, FontColor = color }
		};
		Place(label, corner, marginX, marginY, width, height);
		parent.AddChild(label);
		return label;
	}

	// Barra de progreso en la esquina inferior derecha: fondo tenue + relleno de color
	private static void AddBar(Control parent, Color fillColor, float marginY, out ColorRect fill)
	{
		var track = new ColorRect
		{
			Color = Palette.BlueDim,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		Place(track, Corner.BottomRight, Margin, marginY, BarWidth, BarHeight);
		parent.AddChild(track);

		// El relleno arranca con ancho 0 y crece con OffsetRight (ver SetBar)
		fill = new ColorRect
		{
			Color = fillColor,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 0f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetTop = 0f,
			OffsetRight = 0f,
			OffsetBottom = 0f
		};
		track.AddChild(fill);
	}

	// Coloca un control a una distancia (marginX, marginY) de una esquina de la pantalla
	private static void Place(Control c, Corner corner, float marginX, float marginY, float width, float height)
	{
		bool right = corner != Corner.TopLeft;
		bool bottom = corner == Corner.BottomRight;

		c.AnchorLeft = right ? 1f : 0f;
		c.AnchorRight = right ? 1f : 0f;
		c.AnchorTop = bottom ? 1f : 0f;
		c.AnchorBottom = bottom ? 1f : 0f;

		if (right)
		{
			c.OffsetRight = -marginX;
			c.OffsetLeft = -marginX - width;
		}
		else
		{
			c.OffsetLeft = marginX;
			c.OffsetRight = marginX + width;
		}

		if (bottom)
		{
			c.OffsetBottom = -marginY;
			c.OffsetTop = -marginY - height;
		}
		else
		{
			c.OffsetTop = marginY;
			c.OffsetBottom = marginY + height;
		}
	}

	// fraction entre 0 y 1
	private static void SetBar(ColorRect fill, float fraction)
	{
		fill.OffsetRight = BarWidth * Mathf.Clamp(fraction, 0f, 1f);
	}

	private static string FormatTime(double seconds)
	{
		var t = TimeSpan.FromSeconds(seconds);
		return $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds:000}";
	}
}
