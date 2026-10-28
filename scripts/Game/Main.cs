using Godot;
using System.Collections.Generic;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Ship;
using Vectory.UI;

namespace Vectory.Game;

// Se adjunta a un nodo raíz "Main" de tipo Node3D en una escena vacía.
// Monta WorldEnvironment (fondo oscuro + glow), una rejilla de referencia
// de fondo, el circuito (Track), el Player, la carrera (RaceSession) y el HUD.
//
// Ciclo de carrera: la nave sale de una posición de parrilla aleatoria, espera al semáforo
// y, al terminar la última vuelta, se muestra la pantalla de resultados. Enter
// (acción "restart_race") recarga la escena: nueva carrera, nueva posición de parrilla.
public partial class Main : Node3D
{
	private const float GridStep = 10f;
	private const float GridHalf = 500f;
	private const float GridY = -4f;

	[ExportGroup("Salida")]
	[Export] public float FirstLightDelay = 1f;   // segundos antes de la primera luz
	[Export] public float LightInterval = 1f;     // segundos entre luces
	[Export] public int LightCount = 5;           // número de luces
	[Export] public float GoDuration = 1f;        // segundos con el GO visible

	[ExportGroup("Resultados")]
	[Export] public float ResultsDelay = 1.5f;    // segundos desde la meta hasta la pantalla de resultados

	private Player _player;
	private Track _track;
	private RaceSession _race;
	private Hud _hud;
	private MeshInstance3D _grid;

	private float _finishedTime;   // segundos desde que terminó la carrera
	private bool _resultsShown;

	public override void _Ready()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Palette.Background,
			GlowEnabled = true,
			GlowIntensity = 1.0f,
			GlowBloom = 0.15f,
			GlowHdrThreshold = 0.8f
		};
		AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = env });

		_grid = new MeshInstance3D { Name = "Grid", Mesh = BuildGridMesh() };
		AddChild(_grid);

		// Si Track y Player ya están en la escena (así se editan sus parámetros en el
		// Inspector) se usan tal cual; si no, se crean por código.
		_track = GetNodeOrNull<Track>("Track");
		if (_track == null)
		{
			_track = new Track { Name = "Track" };
			AddChild(_track);
		}

		_player = GetNodeOrNull<Player>("Player");
		if (_player == null)
		{
			_player = new Player { Name = "Player" };
			AddChild(_player);
		}

		// Sin circuito generado (TrackDefinition ausente o no válido; Track ya ha dado el error)
		// no hay parrilla, límites ni carrera: no se monta nada que dependa de él.
		if (!_track.IsBuilt)
		{
			GD.PushError("Main: el circuito no se ha generado; no se monta la carrera.");
			return;
		}

		_player.SetTrack(_track);

		// Una marca de parrilla por participante. Las posiciones se reparten al azar sin repetir;
		// sin rivales todavía, solo se usa la del jugador (la primera del reparto).
		int participants = _track.GridSlots;
		int[] gridOrder = GridLayout.RandomOrder(participants, new System.Random());
		_track.GetGridStart(gridOrder[0], Player.NoseDistance, out Vector3 startPosition, out float startYaw);
		_player.PlaceAt(startPosition, startYaw);

		_race = new RaceSession(_track, BuildStartSettings(), participants);
		_player.SetRaceSession(_race);

		_hud = new Hud { Name = "Hud" };
		AddChild(_hud);

		// Todavía no hay rivales ni posición en vivo: el jugador va primero de todos los participantes
		_hud.SetRacePosition(1, participants);
	}

	public override void _Process(double delta)
	{
		// HUD: velocidad, boost, semáforo y estado de la carrera (no hay si el circuito no se generó)
		if (_race != null)
		{
			_hud.SetSpeed(_player.Speed, _player.BoostedTopSpeed);
			_hud.SetBoostCharges(_player.BoostCharges, _player.BoostMaxCharges, _player.BoostCooldownActive);
			_hud.SetStartSequence(_race.StartSequence);
			_hud.SetRace(_race.Tracker);
			UpdateResults((float)delta);
		}

		// La rejilla se recoloca de 10 en 10 m siguiendo a la nave: parece infinita
		Vector3 p = _player.GlobalPosition;
		_grid.GlobalPosition = new Vector3(
			Mathf.Snapped(p.X, GridStep),
			GridY,
			Mathf.Snapped(p.Z, GridStep));
	}

	// La pantalla de resultados aparece ResultsDelay segundos después de cruzar la meta.
	// Con ella en pantalla, Enter repite la carrera.
	private void UpdateResults(float delta)
	{
		if (_race.State != RaceState.Finished)
			return;

		if (!_resultsShown)
		{
			_finishedTime += delta;
			if (_finishedTime < ResultsDelay)
				return;

			_hud.ShowResults(_race.Tracker.Result);
			_resultsShown = true;
		}

		if (Input.IsActionJustPressed("restart_race"))
			GetTree().ReloadCurrentScene();
	}

	private StartSettings BuildStartSettings()
	{
		return new StartSettings
		{
			FirstLightDelay = FirstLightDelay,
			LightInterval = LightInterval,
			LightCount = LightCount,
			GoDuration = GoDuration
		};
	}

	private static ArrayMesh BuildGridMesh()
	{
		var verts = new List<Vector3>();

		for (float x = -GridHalf; x <= GridHalf; x += GridStep)
		{
			verts.Add(new Vector3(x, 0f, -GridHalf));
			verts.Add(new Vector3(x, 0f, GridHalf));
		}
		for (float z = -GridHalf; z <= GridHalf; z += GridStep)
		{
			verts.Add(new Vector3(-GridHalf, 0f, z));
			verts.Add(new Vector3(GridHalf, 0f, z));
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Palette.BlueDim
		});
		return mesh;
	}
}
