using Godot;
using System.Collections.Generic;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Ship;
using Vectory.UI;

namespace Vectory.Game;

// Se adjunta a un nodo raíz "Main" de tipo Node3D en una escena vacía.
// Monta WorldEnvironment (fondo oscuro + glow), una rejilla de referencia
// de fondo, el circuito (Track) y el Player, todo por código.
public partial class Main : Node3D
{
	private const float GridStep = 10f;
	private const float GridHalf = 500f;
	private const float GridY = -4f;

	private Player _player;
	private Track _track;
	private Hud _hud;
	private MeshInstance3D _grid;

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
		_player.SetTrack(_track);
		_player.PlaceAt(_track.StartPosition, _track.StartYaw);

		_hud = new Hud { Name = "Hud" };
		AddChild(_hud);
	}

	public override void _Process(double delta)
	{
		// La rejilla se recoloca de 10 en 10 m siguiendo a la nave: parece infinita
		_hud.SetSpeed(_player.Speed);

		Vector3 p = _player.GlobalPosition;
		_grid.GlobalPosition = new Vector3(
			Mathf.Snapped(p.X, GridStep),
			GridY,
			Mathf.Snapped(p.Z, GridStep));
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
