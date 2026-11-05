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

// Herramienta de equilibrio (spec docs/naves-6-equilibrio.md, 4.6): se abre tools/BalanceRunner.tscn en el editor y se
// ejecuta con F6, o sin ventana con --headless. No está en ningún menú. Solo conecta: lee el Inspector, corre las
// tandas con StintRunner, de una en una para enseñar el progreso, y escribe el informe en la consola y en
// user://balance/. Las reglas están en las clases puras.
public partial class BalanceRunner : Control
{
	private const string OutputFolder = "user://balance";

	[Export] public string[] ShipIds = Array.Empty<string>();          // vacío = todas las naves
	[Export] public string[] CircuitPaths = Array.Empty<string>();     // vacío = todos los del catálogo, también los de pruebas
	[Export] public bool UseBoost;
	[Export(PropertyHint.Range, "0.5,1,0.01")] public float SpeedMargin = AutopilotSettings.DefaultSpeedMargin;

	private readonly Queue<(BalanceCircuit Circuit, ShipDefinition Ship)> _pending = new();
	private readonly List<StintResult> _results = new();
	private Label _progress;
	private int _total;
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

		IEnumerable<string> shipIds = ShipIds.Length > 0 ? ShipIds : ShipCatalog.Ids;
		var ships = new List<ShipDefinition>();
		foreach (string id in shipIds)
		{
			ShipDefinition ship = ShipDefinitions.Find(id);
			if (ship != null)
				ships.Add(ship);
			else
				GD.PushError($"BalanceRunner: unknown ship '{id}'.");
		}

		IEnumerable<string> paths = CircuitPaths.Length > 0 ? CircuitPaths : CircuitCatalog.AllPaths;
		foreach (string path in paths)
		{
			BalanceCircuit circuit = LoadCircuit(path);
			if (circuit == null)
				continue;
			foreach (ShipDefinition ship in ships)
				_pending.Enqueue((circuit, ship));
		}
		_total = _pending.Count;
		_progress.Text = $"BALANCE · 0 / {_total}";
	}

	private static BalanceCircuit LoadCircuit(string path)
	{
		var definition = ResourceLoader.Exists(path) ? GD.Load<TrackDefinition>(path) : null;
		if (definition == null || !definition.IsValid(out _))
		{
			GD.PushError($"BalanceRunner: cannot load the circuit '{path}'.");
			return null;
		}
		TrackFrame frame = Track.BuildFrame(definition, Track.DefaultTrackY);
		return new BalanceCircuit(definition.Id, definition.DisplayName, definition.IsTest, frame, definition.TrackWidth, definition.CheckpointCount);
	}

	public override void _Process(double delta)
	{
		if (_finished)
			return;

		if (_pending.Count == 0)
		{
			Finish();
			return;
		}

		(BalanceCircuit circuit, ShipDefinition ship) = _pending.Dequeue();
		StintOptions options = StintOptions.Default;
		options.Pilot.UseBoost = UseBoost;
		options.Pilot.SpeedMargin = SpeedMargin;
		_results.Add(StintRunner.Run(circuit, ship, options));
		_progress.Text = $"BALANCE · {_results.Count} / {_total} · {circuit.Name} · {ShipCatalog.Find(ship.Id)?.Name ?? ship.Id}";
	}

	private void Finish()
	{
		_finished = true;
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
		_progress.Text = $"BALANCE · DONE · {_results.Count} STINTS\n{fullPath}";

		if (DisplayServer.GetName() == "headless")
			GetTree().Quit();
	}
}
