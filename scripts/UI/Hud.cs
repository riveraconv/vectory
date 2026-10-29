using System;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Menu;

namespace Vectory.UI;

// HUD de la demo. Solo MUESTRA datos: no calcula reglas de conducción ni de carrera.
// Se crea por código desde Main, que le pasa los datos cada frame. Colores de Palette.
//
//   Arriba izquierda:  posición / participantes (TIME TRIAL: TIME TRIAL y el circuito)
//   Arriba derecha:    vuelta (LAP 2/3, LAP 4, OUT LAP), tiempo de vuelta y mejor tiempo
//   Abajo derecha:     cargas de boost (esferas), velocidad (km/h) y su barra
//   Arriba centro:     RACE: semáforo y cuenta atrás; TIME TRIAL: delta por sector y franja de fin de vuelta
//   Centro:            pantalla de resultados (al terminar la carrera)
//
// Es una capa 2D fija a la pantalla: en cabina queda pegada al marco de la cabina.
public partial class Hud : CanvasLayer
{
	private const float Margin = 40f;       // separación a los bordes de la pantalla
	private const float BarWidth = 260f;
	private const float BarHeight = 10f;
	private const string NoTime = "--:--.---";

	// Semáforo y texto de salida: centrados en horizontal, en el tercio superior de la pantalla
	private const float StartLightsTop = 150f;
	private const float StartLightsWidth = 900f;   // ancho de sobra para READY? / GO! en grande
	private const string ReadyText = "READY?";
	private const string GoText = "GO!";

	// Tamaño del texto de salida (cuenta atrás, READY?, GO!), px sobre 1280×720. Se fija antes de añadir el HUD.
	public int StartTextFontSize = StartLightsView.DefaultTextFontSize;

	private enum Corner { TopLeft, TopRight, BottomRight }

	private Label _positionLabel;
	private Label _lapLabel;
	private Label _lapTimeLabel;
	private Label _bestLapLabel;
	private Label _speedLabel;
	private ColorRect _speedFill;
	private BoostChargesView _boostCharges;
	private StartLightsView _startLights;
	private ResultsView _results;
	private VBoxContainer _timeTrialHeader;
	private Label _circuitLabel;
	private TimeTrialView _timeTrial;

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
		BuildStartLights(root);
		BuildTimeTrial(root);
		BuildResults(root);
	}

	// ---------------------------------------------------------------
	// Datos que recibe del juego
	// ---------------------------------------------------------------

	// Velocidad en m/s. Actualiza el número (km/h) y la barra, que va de 0 a barMaxSpeed.
	public void SetSpeed(float metersPerSecond, float barMaxSpeed)
	{
		_speedLabel.Text = Mathf.RoundToInt(metersPerSecond * 3.6f).ToString();

		float speedFraction = barMaxSpeed > 0f ? metersPerSecond / barMaxSpeed : 0f;
		SetBar(_speedFill, speedFraction);
	}

	// Cargas de boost disponibles y máximas, y si el cooldown está activo (las atenúa)
	public void SetBoostCharges(int charges, int maxCharges, bool cooldownActive)
	{
		_boostCharges.SetCharges(charges, maxCharges, cooldownActive);
	}

	// Bloque de vuelta, ya calculado por quien gestiona la carrera:
	// texto de vuelta (LAP 2/3, LAP 4, OUT LAP), tiempo de la vuelta actual y mejor tiempo (null = sin tiempo).
	public void SetLapInfo(string lapText, double lapTime, double? bestTime)
	{
		_lapLabel.Text = lapText;
		_lapTimeLabel.Text = TimeFormat.Format(lapTime);
		_bestLapLabel.Text = $"BEST {(bestTime.HasValue ? TimeFormat.Format(bestTime.Value) : NoTime)}";
	}

	// TIME TRIAL: arriba a la izquierda, TIME TRIAL y el nombre del circuito en lugar de la posición
	public void SetTimeTrialHeader(string circuitName)
	{
		_positionLabel.Visible = false;
		_timeTrialHeader.Visible = true;
		_circuitLabel.Text = circuitName.ToUpperInvariant();
	}

	// Delta por sector y franja de fin de vuelta de TIME TRIAL
	public TimeTrialView TimeTrial => _timeTrial;

	// RACE: semáforo y cuenta atrás. Se ven hasta que la salida se ha dado y el GO! ha desaparecido.
	public void SetStartSequence(StartSequence start)
	{
		if (start == null)
			return;

		bool visible = !start.HasStarted || start.ShowGo;
		_startLights.Visible = visible;
		if (visible)
		{
			string text = start.ShowGo ? GoText : start.CountdownNumber?.ToString() ?? "";
			_startLights.SetState(start.LightsOn, start.LightCount, text, start.ShowGo);
		}
	}

	// TIME TRIAL: READY? hasta la salida y GO! justo después, sin semáforo, en el mismo sitio
	public void SetReadySequence(ReadySequence ready)
	{
		if (ready == null)
			return;

		bool visible = ready.ShowReady || ready.ShowGo;
		_startLights.Visible = visible;
		if (visible)
			_startLights.SetState(0, 0, ready.ShowGo ? GoText : ReadyText, ready.ShowGo);
	}

	// Se lanza al hacer clic en una opción de la pantalla de resultados, con su identificador
	public event Action<string> ResultsOptionChosen;

	// Muestra la pantalla de resultados de una carrera terminada, con su lista de opciones y qué
	// récords de RACE ha batido. bandHeight y optionFontSize en px sobre 1280×720 (ver ResultsView).
	public void ShowResults(RaceResult result, MenuModel options, float bandHeight, int optionFontSize,
		bool newTotalRecord, bool newLapRecord)
	{
		if (_results.Visible || result == null)
			return;

		_results.SetResult(result, options, bandHeight, optionFontSize, newTotalRecord, newLapRecord);
		_results.Visible = true;
	}

	// Vuelve a dibujar la selección de las opciones de resultados (tras moverla con el teclado)
	public void RefreshResultsOptions()
	{
		_results.RefreshOptions();
	}

	// Posición del jugador y total de participantes
	public void SetRacePosition(int position, int total)
	{
		_positionLabel.Text = $"POSITION {position}/{total}";
	}

	// ---------------------------------------------------------------
	// Construcción
	// ---------------------------------------------------------------

	private void BuildPositionBlock(Control root)
	{
		_positionLabel = AddLabel(root, "POSITION 1/1", 40, Palette.Cyan,
			HorizontalAlignment.Left, VerticalAlignment.Center,
			Corner.TopLeft, Margin, 30f, 400f, 50f);

		// TIME TRIAL: en el mismo sitio, oculto en RACE (Share Tech Mono, medidas sobre 1280×720)
		float scale = UiScale.Factor;
		_timeTrialHeader = new VBoxContainer
		{
			Name = "TimeTrialHeader",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = new Vector2(Margin, 30f)
		};
		_timeTrialHeader.AddThemeConstantOverride("separation", Mathf.RoundToInt(4f * scale));
		root.AddChild(_timeTrialHeader);

		int titleSize = Mathf.RoundToInt(22f * scale);
		_timeTrialHeader.AddChild(new Label
		{
			Text = "TIME TRIAL",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, titleSize, 0.16f), titleSize,
				Palette.Cyan, Colors.Transparent, 0f)
		});

		int nameSize = Mathf.RoundToInt(14f * scale);
		_circuitLabel = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = MenuFonts.Settings(MenuFonts.Make(MenuFonts.Mono, 400, nameSize, 0.16f), nameSize,
				Palette.BlueGray, Colors.Transparent, 0f)
		};
		_timeTrialHeader.AddChild(_circuitLabel);
	}

	// Delta y franja de fin de vuelta de TIME TRIAL, a pantalla completa (solo dibuja arriba en el centro)
	private void BuildTimeTrial(Control root)
	{
		_timeTrial = new TimeTrialView { Name = "TimeTrial" };
		_timeTrial.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(_timeTrial);
	}

	private void BuildLapBlock(Control root)
	{
		_lapLabel = AddLabel(root, "LAP -/-", 28, Palette.Cyan,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.TopRight, Margin, 30f, 320f, 38f);

		_lapTimeLabel = AddLabel(root, TimeFormat.Format(0), 34, Palette.Cyan,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.TopRight, Margin, 70f, 320f, 42f);

		_bestLapLabel = AddLabel(root, $"BEST {NoTime}", 22, Palette.BlueGray,
			HorizontalAlignment.Right, VerticalAlignment.Center,
			Corner.TopRight, Margin, 114f, 320f, 30f);
	}

	// De abajo hacia arriba: barra de velocidad, número y unidad, cargas de boost
	private void BuildSpeedBlock(Control root)
	{
		AddBar(root, Palette.Cyan, 36f, out _speedFill);

		_speedLabel = AddLabel(root, "0", 56, Palette.Cyan,
			HorizontalAlignment.Right, VerticalAlignment.Bottom,
			Corner.BottomRight, Margin + 80f, 52f, BarWidth - 80f, 64f);

		AddLabel(root, "KM/H", 20, Palette.BlueGray,
			HorizontalAlignment.Right, VerticalAlignment.Bottom,
			Corner.BottomRight, Margin, 52f, 76f, 64f);

		// Esferas justo encima de la velocidad, alineadas a la derecha del bloque
		_boostCharges = new BoostChargesView
		{
			Name = "BoostCharges",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		Place(_boostCharges, Corner.BottomRight, Margin, 124f, BarWidth, BoostChargesView.Diameter);
		root.AddChild(_boostCharges);
	}

	private void BuildStartLights(Control root)
	{
		_startLights = new StartLightsView
		{
			Name = "StartLights",
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetLeft = -StartLightsWidth * 0.5f,
			OffsetRight = StartLightsWidth * 0.5f,
			OffsetTop = StartLightsTop,
			OffsetBottom = StartLightsTop + StartLightsView.HeightFor(StartTextFontSize),
			TextFontSize = StartTextFontSize
		};
		root.AddChild(_startLights);
	}

	// A pantalla completa: la franja de resultados atenúa toda la carrera y va centrada en vertical.
	// Se añade la última, así queda por encima del resto del HUD.
	private void BuildResults(Control root)
	{
		_results = new ResultsView { Name = "Results", Visible = false };
		_results.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(_results);
		_results.OptionChosen += id => ResultsOptionChosen?.Invoke(id);
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
}
