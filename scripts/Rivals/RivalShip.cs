using Godot;
using Vectory.Circuit;
using Vectory.Radar;
using Vectory.Ship;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Rivals;

// Lo que la nave de un rival toma de la del jugador, para verse igual: casco 3D, inclinación, orientación, parpadeo del
// daño y balizas de HAMMER (con los valores del Inspector del Player)
public sealed record RivalLook(Hull3DSettings Hull, BankSettings Bank, AttitudeSettings Attitude, EdgeFlickerSettings Flicker,
	IndustrialLampSettings Lamps);

// Nave de un rival (spec docs/rivales-1-parrilla.md, 3.3 y 4): pinta la carrera de su RivalCar, que lleva toda la lógica.
// Su casco 3D con su pintura, en la capa de los rivales (ShipHull3D.RivalLayer, iluminada por un juego de luces común),
// con la inclinación al girar y la orientación con la pista del jugador, el daño visible (quemaduras, humo y luces que
// titilan), el brillo de levitación, la anticolisión de BLADE y las balizas de HAMMER. Nada de lo que es solo del jugador:
// ni cámaras, ni cabina, ni HUD, ni sacudida. Se apunta en la lista de naves en carrera en cada paso.
public partial class RivalShip : Node3D
{
	private readonly ShipBank _bank = new();
	private readonly ShipAttitude _attitude = new();
	private readonly System.Random _flickerRandom = new();
	private Spaceship _ship;
	private RivalCar _car;
	private RaceSession _race;
	private RaceShipList _raceShips;
	private RivalLook _look;
	private EdgeFlicker _flicker;
	private float _flickerOpacity;
	private float _time;
	private int _damageLevel;
	private int _respawns;

	public RivalCar Car => _car;

	// Antes de añadirlo al árbol
	public void Setup(RivalCar car, RaceSession race, RaceShipList raceShips, RivalLook look, float flickerOpacity)
	{
		_car = car;
		_race = race;
		_raceShips = raceShips;
		_look = look;
		_flickerOpacity = flickerOpacity;
		Name = car.Entry.Id;
	}

	public override void _Ready()
	{
		// Después de la nave del jugador (prioridad 0), que es la que da el GO en su paso
		ProcessPhysicsPriority = 1;

		ShipDefinition definition = _car.Ship;
		_ship = new Spaceship
		{
			Name = "Spaceship",
			Visual = definition.Visual,
			Use3DHull = true,
			Hull3DLayer = ShipHull3D.RivalLayer,
			Hull3DOwnLights = false
		};
		_ship.SetHull3DSettings(_look.Hull);
		if (definition.Visual.Hull3D != null)
			_ship.SetHullPaint(definition.Visual.Hull3D.Style, _car.Entry.PaintIndex);
		AddChild(_ship);

		_car.Damage.LevelChanged += OnDamageLevelChanged;
		string id = _car.Entry.Id;
		_car.Tracker.LapCompleted += (lap, time, _) => GD.Print($"[Race] {id} lap {lap}: {Core.TimeFormat.Format(time)}");
		_car.Tracker.RaceFinished += total => GD.Print($"[Race] {id} finished: {Core.TimeFormat.Format(total)}");
		SettleOnSurface();
		Publish();
	}

	public override void _PhysicsProcess(double delta)
	{
		_car.Step(_race.State != RaceState.PreStart);

		if (_car.Respawns != _respawns)
		{
			_respawns = _car.Respawns;
			_ship.ClearHullSmoke();
			_bank.Reset();
			SettleOnSurface();
		}
		else
		{
			ShipDynamics d = _car.Dynamics;
			float top = _car.Ship.Technical.MaxSpeed * _car.Damage.TopSpeedFactor;
			float speedRatio = top > 0f ? d.RealSpeed / top : 0f;
			_bank.Update(d.Steer, speedRatio, d.Drifting, _look.Bank, (float)delta);
			ShipPhysics physics = _car.Physics;
			if (physics.HasSurface)
			{
				_attitude.Update(physics.Vertical.Grounded, physics.Vertical.Landed, physics.Surface.Frame.Normal, d.Yaw,
					physics.Vertical.VerticalSpeed, d.RealSpeed, _look.Attitude, (float)delta);
			}
			ApplyTransform();
		}
		Publish();
	}

	// Efectos del casco: brillo de levitación, anticolisión, balizas y parpadeo. Con la pausa no avanzan.
	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		ShipPhysics physics = _car.Physics;
		if (_ship.HasHull3D && physics.HasSurface)
		{
			SurfacePoint surface = physics.Surface;
			Transform3D glow = ShipHull3D.GlowTransform(surface, GlobalPosition, _car.Dynamics.Yaw, _ship.Visual.Hull3D.Glow.CenterZ);
			float strength = HoverGlow.Strength(_time, _look.Hull.HoverGlowIntensity) * HoverGlow.AirFade(surface.Height - Track.ShipHover);
			_ship.UpdateHullEffects(glow, strength, _car.Dynamics.RealSpeed, _time);
			(float boost, float damage) = IndustrialHullBeacons.Intensities(_car.Boost.IsImpulseActive, _damageLevel, true, _time, _look.Lamps);
			_ship.SetHullBeacons(boost, damage);
		}

		if (_flicker != null && _flicker.Update(dt))
			_ship.ShowEdgeFlicker(_flicker, _flickerOpacity);
	}

	private void OnDamageLevelChanged(int level)
	{
		_damageLevel = level;
		_ship.SetHullDamage(level, false);
		_flicker ??= new EdgeFlicker(_ship.EdgeCount, _look.Flicker, _flickerRandom);
		_flicker.SetLevel(level);
		_ship.ShowEdgeFlicker(_flicker, _flickerOpacity);
	}

	// En el suelo y con la orientación de la superficie al momento (al colocarla y al recolocarla)
	private void SettleOnSurface()
	{
		ShipPhysics physics = _car.Physics;
		if (physics.HasSurface)
		{
			ShipAttitude.SurfaceAngles(physics.Surface.Frame.Normal, _car.Dynamics.Yaw, out float pitch, out float roll);
			_attitude.Reset(pitch, roll);
		}
		else
		{
			_attitude.Reset();
		}
		ApplyTransform();
	}

	// Cuerpo con rumbo, cabeceo y ladeo (orden YXZ de Godot, como el del jugador) e inclinación al girar en el casco
	private void ApplyTransform()
	{
		NVec3 p = _car.Position;
		GlobalPosition = new Vector3(p.X, p.Y, p.Z);
		Rotation = new Vector3(_attitude.Pitch, _car.Dynamics.Yaw, _attitude.Roll);
		_ship.Rotation = new Vector3(0f, 0f, _bank.Angle);
	}

	// Para la lista de naves en carrera (radar y minimapa)
	private void Publish()
	{
		Basis basis = GlobalBasis;
		_raceShips.Set(new RaceShip(_car.Entry.Number, ToNumerics(GlobalPosition), ToNumerics(-basis.Z), ToNumerics(basis.Y)));
	}

	private static NVec3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);
}
