using Godot;
using Vectory.Core;
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

	public override void _Ready()
	{
		Visible = false;

		_ship = new Spaceship { Name = "Spaceship" };
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

		float opacity = GhostFade.Opacity(GlobalPosition.DistanceTo(playerPosition), Opacity, FadeStartDistance, FadeEndDistance);
		_material.AlbedoColor = new Color(Palette.GhostLine, opacity);
		Visible = opacity > 0f;
	}

	public void HideGhost()
	{
		Visible = false;
	}
}
