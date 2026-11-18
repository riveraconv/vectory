using Godot;
using Vectory.Core;
using Vectory.Effects;
using Vectory.Ship;
using NQuat = System.Numerics.Quaternion;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Ghost;

// Nave fantasma de TIME TRIAL: la misma geometría que la nave (Spaceship) con todas sus líneas en
// azul grisáceo translúcido (Palette.GhostLine), sin emisión ni halo. Sin colisión, físicas ni input.
// Solo pinta: Main le pasa la grabación y el tiempo de vuelta, y aquí se lee la pose (GhostPlayback)
// y la opacidad según la distancia a la nave del jugador (GhostFade).
public partial class GhostShip : Node3D
{
	[ExportGroup("Fantasma")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float Opacity = 0.35f;   // opacidad normal (lejos de la nave)
	[Export] public float FadeStartDistance = 20f;   // metros a los que empieza a desvanecerse
	[Export] public float FadeEndDistance = 6f;      // metros a los que ya es invisible

	private Spaceship _ship;
	private StandardMaterial3D _material;

	// Para la llama de sus toberas (spec docs/efectos-6-toberas.md, 9): punta de su nave y su velocidad, sacada de la pose
	private float _topSpeed = ShipDefinitions.Default.Technical.MaxSpeed;
	private Vector3 _lastPosition;
	private double _lastLapTime = -1.0;
	private float _speed;

	public override void _Ready()
	{
		Visible = false;

		_ship = new Spaceship { Name = "Spaceship" };
		_ship.EnableThrusters(ThrusterFxSettings.Default, 0.5f);
		AddChild(_ship);

		// Sin colores de vértice ni emisión: un solo color translúcido, por debajo del umbral del glow
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = Palette.GhostLine
		};
		_ship.SetMaterialOverride(_material);
	}

	// La nave con la que se hizo la grabación (la de su archivo, no la de la sesión)
	public void SetVisual(ShipVisualProfile visual) => _ship.SetVisual(visual);

	// La nave de la grabación: su casco y su punta (para la llama)
	public void SetShip(ShipDefinition ship)
	{
		_ship.SetVisual(ship.Visual);
		_topSpeed = ship.Technical.MaxSpeed;
	}

	// Coloca el fantasma donde estaba la grabación en ese tiempo de vuelta. Si la grabación ya ha
	// terminado, o está demasiado cerca de la nave del jugador, no se ve.
	public void ShowAt(GhostRecording recording, double lapTime, Vector3 playerPosition)
	{
		if (!GhostPlayback.TryGetPose(recording, lapTime, out NVec3 position, out NQuat rotation))
		{
			Visible = false;
			return;
		}

		GlobalPosition = new Vector3(position.X, position.Y, position.Z);
		Quaternion = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
		FeedThrusters(lapTime);

		float opacity = GhostFade.Opacity(GlobalPosition.DistanceTo(playerPosition), Opacity, FadeStartDistance, FadeEndDistance);
		_material.AlbedoColor = new Color(Palette.GhostLine, opacity);
		_ship.SetThrusterOpacity(opacity);
		Visible = opacity > 0f;
	}

	// Velocidad de la pose entre dos tiempos de vuelta (con la pausa, o al volver a empezar la vuelta, se queda la de
	// antes) y, con ella, la llama: con gas siempre, sin boost, giro ni derrape (spec docs/efectos-6-toberas.md, 9)
	private void FeedThrusters(double lapTime)
	{
		double dt = lapTime - _lastLapTime;
		if (_lastLapTime >= 0.0 && dt > 1e-4 && dt < 0.5)
			_speed = _lastPosition.DistanceTo(GlobalPosition) / (float)dt;
		_lastLapTime = lapTime;
		_lastPosition = GlobalPosition;
		_ship.FeedThrusters(new ThrusterInput { Accelerate = true, Speed = _speed, TopSpeed = _topSpeed });
	}

	public void HideGhost()
	{
		Visible = false;
	}
}
