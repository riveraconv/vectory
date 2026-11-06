using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vectory.Autopilot;
using Vectory.Balance;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Ship;

namespace Vectory.Tools;

// Herramienta de equilibrio (spec docs/naves-6-equilibrio.md, 4.6 y 5): se abre tools/BalanceRunner.tscn en el editor y
// se ejecuta con F6, o sin ventana con --headless. No está en ningún menú. Solo conecta: lee el Inspector, corre las
// tandas de una en una para enseñar el progreso y escribe el resultado. Las reglas están en las clases puras.
//  - Normal: una tanda por nave y circuito; informe en la consola y CSV en user://balance/.
//  - Sweep: la nave y el parámetro elegidos, entre dos valores; tabla en la consola.
//  - Match: el valor con el que esa nave iguala a VECTOR; lo dice en la consola, o que no lo hay.
// Sweep y Match solo proponen números: no cambian las definiciones.
public partial class BalanceRunner : Control
{
	public enum RunMode
	{
		Normal,
		Sweep,
		Match
	}

	private const string OutputFolder = "user://balance";

	[Export] public RunMode Mode = RunMode.Normal;
	[Export] public string[] ShipIds = Array.Empty<string>();          // vacío = todas las naves
	[Export] public string[] CircuitPaths = Array.Empty<string>();     // vacío = todos los del catálogo, también los de pruebas
	[Export] public bool UseBoost;
	[Export(PropertyHint.Range, "0.5,1,0.01")] public float SpeedMargin = AutopilotSettings.DefaultSpeedMargin;

	[ExportGroup("Sweep y Match")]
	[Export(PropertyHint.Enum, "ship_02,ship_01,ship_03")] public string SweepShip = "ship_03";
	[Export] public BalanceParameter SweepParameter = BalanceParameter.MaxSpeed;
	[Export] public float SweepFrom = 0.94f;   // Match no los usa: busca en el rango permitido
	[Export] public float SweepTo = 1f;
	[Export] public float SweepStep = 0.01f;

	private readonly Queue<(BalanceCircuit Circuit, ShipDefinition Ship)> _pending = new();
	private readonly List<StintResult> _results = new();
	private readonly List<SweepRow> _rows = new();
	private List<BalanceCircuit> _circuits;
	private List<ShipDefinition> _ships;
	private Queue<float> _values;
	private BalanceSweep _sweep;
	private Label _progress;
	private int _total;
	private bool _matchShown;
	private bool _finished;

	public override void _Ready()
	{
		if (!OS.IsDebugBuild())
		{
			GetTree().Quit();
			return;
		}

		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(new ColorRect { Color = Palette.Background, AnchorRight = 1f, AnchorBottom = 1f });
		_progress = new Label { Position = new Vector2(40f, 40f) };
		_progress.AddThemeColorOverride("font_color", Palette.Cyan);
		AddChild(_progress);

		_ships = LoadShips();
		_circuits = LoadCircuits();

		if (Mode == RunMode.Normal)
		{
			foreach (BalanceCircuit circuit in _circuits)
			{
				foreach (ShipDefinition ship in _ships)
					_pending.Enqueue((circuit, ship));
			}
			_total = _pending.Count;
			_progress.Text = $"BALANCE · 0 / {_total}";
			return;
		}

		ShipDefinition swept = ShipDefinitions.Find(SweepShip);
		if (swept == null || _circuits.Count == 0)
		{
			GD.PushError($"BalanceRunner: nothing to sweep (ship '{SweepShip}', {_circuits.Count} circuits).");
			End("BALANCE · NOTHING TO DO");
			return;
		}

		_sweep = new BalanceSweep(_circuits, _ships, swept, SweepParameter, Options());
		if (Mode == RunMode.Match)
		{
			_progress.Text = $"MATCH · {SweepShip} · {SweepParameter}";
			return;
		}

		_values = new Queue<float>(BalanceSweep.Values(SweepFrom, SweepTo, SweepStep));
		_total = _values.Count;
		_progress.Text = $"SWEEP · 0 / {_total}";
	}

	private List<ShipDefinition> LoadShips()
	{
		var ships = new List<ShipDefinition>();
		foreach (string id in ShipIds.Length > 0 ? ShipIds : ShipCatalog.Ids)
		{
			ShipDefinition ship = ShipDefinitions.Find(id);
			if (ship != null)
				ships.Add(ship);
			else
				GD.PushError($"BalanceRunner: unknown ship '{id}'.");
		}
		return ships;
	}

	private List<BalanceCircuit> LoadCircuits()
	{
		var circuits = new List<BalanceCircuit>();
		foreach (string path in CircuitPaths.Length > 0 ? CircuitPaths : CircuitCatalog.AllPaths)
		{
			var definition = ResourceLoader.Exists(path) ? GD.Load<TrackDefinition>(path) : null;
			if (definition == null || !definition.IsValid(out _))
			{
				GD.PushError($"BalanceRunner: cannot load the circuit '{path}'.");
				continue;
			}
			TrackFrame frame = Track.BuildFrame(definition, Track.DefaultTrackY);
			circuits.Add(new BalanceCircuit(definition.Id, definition.DisplayName, definition.IsTest, frame, definition.TrackWidth, definition.CheckpointCount));
		}
		return circuits;
	}

	private StintOptions Options()
	{
		StintOptions options = StintOptions.Default;
		options.Pilot.UseBoost = UseBoost;
		options.Pilot.SpeedMargin = SpeedMargin;
		return options;
	}

	public override void _Process(double delta)
	{
		if (_finished)
			return;

		switch (Mode)
		{
			case RunMode.Normal:
				StepNormal();
				break;
			case RunMode.Sweep:
				StepSweep();
				break;
			case RunMode.Match:
				StepMatch();
				break;
		}
	}

	private void StepNormal()
	{
		if (_pending.Count == 0)
		{
			FinishNormal();
			return;
		}

		(BalanceCircuit circuit, ShipDefinition ship) = _pending.Dequeue();
		_results.Add(StintRunner.Run(circuit, ship, Options()));
		_progress.Text = $"BALANCE · {_results.Count} / {_total} · {circuit.Name} · {ShipCatalog.Find(ship.Id)?.Name ?? ship.Id}";
	}

	private void StepSweep()
	{
		if (_values.Count == 0)
		{
			PrintMarked(SweepReport.Text(SweepShip, SweepParameter, _rows));
			End($"SWEEP · DONE · {_rows.Count} VALUES");
			return;
		}

		_rows.Add(_sweep.Evaluate(_values.Dequeue()));
		_progress.Text = $"SWEEP · {_rows.Count} / {_total} · {_rows[_rows.Count - 1].Value:0.0000}";
	}

	// La búsqueda va en un solo frame: el primero enseña el texto y el siguiente la hace
	private void StepMatch()
	{
		if (!_matchShown)
		{
			_matchShown = true;
			return;
		}

		BalanceCircuit circuit = _circuits.FirstOrDefault(c => !c.IsTest) ?? _circuits[0];
		if (SweepShip == BalanceRules.ReferenceShipId)
		{
			GD.PushError("BalanceRunner: MATCH compares with VECTOR; choose another ship.");
			End("MATCH · CHOOSE ANOTHER SHIP");
			return;
		}
		MatchResult match = _sweep.Match(circuit);
		GD.Print(SweepReport.MatchText(SweepShip, SweepParameter, circuit.Name, match));
		End($"MATCH · DONE · {(match.Found ? $"{match.Value:0.0000}" : "NONE")}");
	}

	// Lo que se sale de los rangos, en naranja, como en el panel de ajuste
	private static void PrintMarked(string text)
	{
		foreach (string line in text.Split('\n'))
		{
			if (line.Contains(SweepReport.OutOfRangeMark))
				GD.PrintRich($"[color=#{Palette.Orange.ToHtml(false)}]{line}[/color]");
			else
				GD.Print(line);
		}
	}

	private void FinishNormal()
	{
		GD.Print(BalanceReport.Text(_results));

		DirAccess.MakeDirRecursiveAbsolute(OutputFolder);
		string path = $"{OutputFolder}/{BalanceReport.FileName(DateTime.Now)}";
		using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
		{
			if (file == null)
				GD.PushError($"BalanceRunner: cannot write '{path}': {FileAccess.GetOpenError()}");
			else
				file.StoreString(BalanceReport.Csv(_results));
		}

		string fullPath = ProjectSettings.GlobalizePath(path);
		GD.Print($"[Balance] CSV: {fullPath}");
		End($"BALANCE · DONE · {_results.Count} STINTS\n{fullPath}");
	}

	private void End(string message)
	{
		_finished = true;
		_progress.Text = message;
		if (DisplayServer.GetName() == "headless")
			GetTree().Quit();
	}
}
