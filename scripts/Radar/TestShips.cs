using System;
using Godot;
using Vectory.Circuit;
using Vectory.Core;
using Vectory.Ship;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Radar;

// Naves de prueba del radar y el minimapa (spec docs/radar-minimapa.md, 3.2): cascos de VECTOR en magenta, sin colisión ni
// efecto en la carrera. Main solo lo crea en compilaciones de depuración. La tecla F6 pasa por 0, 2, 4 y 6, también con la
// pausa, y el número dura hasta cerrar el juego, como el panel de ajuste. El cálculo está en TestShipFleet.
public partial class TestShips : Node3D
{
	private static int _count;

	private readonly Spaceship[] _hulls = new Spaceship[TestShipPattern.Specs.Count];

	private TestShipFleet _fleet;
	private Player _player;
	private Track _track;
	private float _maxLateral;

	public event Action<int> CountChanged;

	public int Count => _fleet?.Count ?? _count;

	public void Setup(RaceShipList ships, Player player, Track track)
	{
		_player = player;
		_track = track;
		_fleet = new TestShipFleet(ships, _count);
		_maxLateral = track.TrackWidth * 0.5f - ShipDefinitions.Default.Visual.CollisionHalfWidth;
	}

	public override void _Ready()
	{
		// Procesa siempre para leer la tecla con la pausa; el movimiento se para a mano (ver _PhysicsProcess)
		ProcessMode = ProcessModeEnum.Always;
		// Después de que se mueva la nave del jugador (Player tiene prioridad 0)
		ProcessPhysicsPriority = 1;

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Palette.Magenta
		};
		for (int i = 0; i < _hulls.Length; i++)
		{
			_hulls[i] = new Spaceship { Name = $"TestShip{i + 1}", Visible = false };
			AddChild(_hulls[i]);
			_hulls[i].SetMaterialOverride(material);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("debug_test_ships") || _fleet == null)
			return;

		_count = TestShipPattern.NextCount(_fleet.Count);
		_fleet.SetCount(_count);
		for (int i = _count; i < _hulls.Length; i++)
			_hulls[i].Visible = false;

		GD.Print($"[TestShips] {_count} test ships");
		CountChanged?.Invoke(_count);
		GetViewport().SetInputAsHandled();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_fleet == null || _fleet.Count == 0 || GetTree().Paused)
			return;

		Vector3 position = _player.GlobalPosition;
		SurfacePoint player = _track.Frame.SurfaceUnder(position.X, position.Z, position.Y);
		_fleet.Update((float)delta, _track.Frame, player.Distance, player.Lateral, Track.ShipHover, _maxLateral);

		for (int i = 0; i < _fleet.Count; i++)
		{
			RaceShip ship = _fleet.Ship(i);
			Vector3 forward = ToGodot(ship.Forward);
			Vector3 up = ToGodot(ship.Up);
			_hulls[i].GlobalTransform = new Transform3D(new Basis(forward.Cross(up), up, -forward), ToGodot(ship.Position));
			_hulls[i].Visible = true;
		}
	}

	private static Vector3 ToGodot(NVec3 v) => new(v.X, v.Y, v.Z);
}
