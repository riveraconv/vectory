using Godot;
using Vectory.Circuit;
using Vectory.Radar;
using Vectory.Ship;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Rivals;

// Nave de un rival (spec docs/rivales-1-parrilla.md, 3.2 y 3.3): su casco 3D con su pintura, en la capa de los rivales
// (ShipHull3D.RivalLayer, iluminada por un juego de luces común), colocada en su marca como el jugador: centrada, con el
// morro detrás de la línea, mirando en el sentido de la marcha, parada, en el suelo y alineada con la superficie. Nada de
// lo que es solo del jugador: ni cámaras, ni cabina, ni HUD, ni sacudida. Etapa 1: se queda quieta.
// Este nodo solo conecta piezas: la colocación es la de ShipPhysics y la orientación, la de ShipAttitude.
public partial class RivalShip : Node3D
{
	private readonly ShipPhysics _physics = new();
	private Spaceship _ship;
	private float _hoverTime;
	private Hull3DSettings _hullSettings = new();

	public RivalEntry Entry { get; private set; }
	public ShipDefinition Definition { get; private set; }

	// Antes de añadirlo al árbol
	public void Setup(RivalEntry entry, Hull3DSettings hullSettings)
	{
		Entry = entry;
		Definition = ShipDefinitions.Resolve(entry.ShipDesignId, message => GD.PushWarning(message));
		_hullSettings = hullSettings;
		Name = entry.Id;
	}

	public override void _Ready()
	{
		_ship = new Spaceship
		{
			Name = "Spaceship",
			Visual = Definition.Visual,
			Use3DHull = true,
			Hull3DLayer = ShipHull3D.RivalLayer,
			Hull3DOwnLights = false
		};
		_ship.SetHull3DSettings(_hullSettings);
		if (Definition.Visual.Hull3D != null)
			_ship.SetHullPaint(Definition.Visual.Hull3D.Style, Entry.PaintIndex);
		AddChild(_ship);
	}

	// En su marca de parrilla, parada y sin daño
	public void PlaceOnGrid(Track track)
	{
		track.GetGridStart(Entry.GridPosition, Definition.Visual.NoseDistance, out Vector3 position, out float yaw);
		_physics.SetTrack(track.Frame, track.BarrierOffset);
		_physics.Place(ToNumerics(position), yaw, 0f, ShipPhysicsSettings.Default.Boost);
		GlobalPosition = position;

		float pitch = 0f, roll = 0f;
		if (_physics.HasSurface)
			ShipAttitude.SurfaceAngles(_physics.Surface.Frame.Normal, yaw, out pitch, out roll);
		// Rumbo, cabeceo y ladeo en el orden YXZ de Godot, como el cuerpo del jugador
		Rotation = new Vector3(pitch, yaw, roll);
	}

	// Para la lista de naves en carrera (radar y minimapa)
	public RaceShip ToRaceShip()
	{
		Basis basis = GlobalBasis;
		return new RaceShip(Entry.Number, ToNumerics(GlobalPosition), ToNumerics(-basis.Z), ToNumerics(basis.Y));
	}

	// Brillo de levitación y tiempo de la nave (anticolisión de BLADE). Con la pausa no avanza.
	public override void _Process(double delta)
	{
		if (!_ship.HasHull3D || !_physics.HasSurface)
			return;
		_hoverTime += (float)delta;
		SurfacePoint surface = _physics.Surface;
		Transform3D glow = ShipHull3D.GlowTransform(surface, GlobalPosition, _physics.Dynamics.Yaw, _ship.Visual.Hull3D.Glow.CenterZ);
		float strength = HoverGlow.Strength(_hoverTime, _hullSettings.HoverGlowIntensity) * HoverGlow.AirFade(surface.Height - Track.ShipHover);
		_ship.UpdateHullEffects(glow, strength, _physics.Dynamics.RealSpeed, _hoverTime);
	}

	private static NVec3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);
}
