using Godot;
using Vectory.Autopilot;
using Vectory.Core;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Effects;
using Vectory.Radar;
using Vectory.Ship;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Rivals;

// Lo que la nave de un rival toma de la del jugador, para verse igual: casco 3D, inclinación, orientación, parpadeo del
// daño y balizas de HAMMER (con los valores del Inspector del Player)
public sealed record RivalLook(Hull3DSettings Hull, BankSettings Bank, AttitudeSettings Attitude, EdgeFlickerSettings Flicker,
	IndustrialLampSettings Lamps, ThrusterFxSettings Thrusters, WingTrailSettings Trails);

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
	private RespawnBlink _blink;
	private float _blinkTime;
	private float _hullOpacity = 1f;

	// La nave que se ve, con su inclinación (de ahí nacen las chispas del derrape)
	public Transform3D HullTransform => _ship.GlobalTransform;

	// Depuración (spec docs/rivales-3-contacto.md, 9): estado de la trazada clara y pasillo encima de cada rival. Lo
	// enciende el panel de pruebas; dura hasta cerrar el juego.
	public static bool ShowLineStates { get; set; }
	private Label3D _stateLabel;
	private MeshInstance3D _corridor;
	private ImmediateMesh _corridorMesh;
	private StandardMaterial3D _corridorMaterial;

	public RivalCar Car => _car;

	// Rozaduras del casco (spec docs/efectos-5-danos-casco.md): las pone Main con los golpes de la carrera
	private readonly HullWear _hullWear = new(HullWearSettings.Default);
	private int _hullWearVersion;

	public void WearBarrier(in BarrierTouch touch, float dt)
	{
		if (!touch.Contact)
			return;
		_hullWear.Barrier(touch.Side, touch.Level, touch.IsNewImpact, dt);
		PushHullWear();
	}

	public void WearShip(System.Numerics.Vector2 point, ImpactLevel level, bool isNewImpact, float dt)
	{
		ShipVisualProfile visual = _car.Ship.Visual;
		_hullWear.ContactAt(point, new System.Numerics.Vector2(_car.Position.X, _car.Position.Z), _car.Dynamics.Yaw,
			visual.CollisionFront, visual.CollisionBack, level, isNewImpact, dt);
		PushHullWear();
	}

	private void PushHullWear()
	{
		if (_hullWear.Version == _hullWearVersion || _ship == null)
			return;
		_hullWearVersion = _hullWear.Version;
		_ship.SetHullWear(_hullWear.Zones);
	}

	// Antes de añadirlo al árbol
	public void Setup(RivalCar car, RaceSession race, RaceShipList raceShips, RivalLook look, float flickerOpacity, RespawnBlink blink)
	{
		_blink = blink;
		_car = car;
		_race = race;
		_raceShips = raceShips;
		_look = look;
		_flickerOpacity = flickerOpacity;
		Name = car.Entry.Id;
	}

	public override void _Ready()
	{
		// Después de Main (prioridad 1, su padre), que mueve los rivales con RivalField.StepPhysics
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
		_ship.EnableThrusters(_look.Thrusters, 1.7f * _car.Entry.Number);
		_ship.EnableTrails(_look.Trails, WingTrailPalette.For(definition.Id, _car.Entry.PaintIndex));
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

	// Pinta el paso que acaba de dar su RivalCar (lo mueve RivalField, con los contactos entre naves)
	public override void _PhysicsProcess(double delta)
	{
		if (_car.Respawns != _respawns)
		{
			_respawns = _car.Respawns;
			_bank.Reset();
			_ship.ResetThrusters();
			_ship.ResetTrails();
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
			ShipInput input = _car.LastInput;
			_ship.FeedThrusters(new ThrusterInput
			{
				Accelerate = input.Accelerate,
				Brake = input.Brake,
				Steer = ThrusterInput.SteerOf(input),
				Drifting = d.Drifting,
				SlipAngle = d.SlipAngle,
				Boosting = _car.Boost.IsImpulseActive,
				Speed = d.RealSpeed,
				TopSpeed = _car.Ship.Technical.MaxSpeed,
				DamageLevel = _damageLevel
			});
			_ship.FeedTrails((float)delta, new WingTrailMotion { Velocity = d.Velocity, Drifting = d.Drifting, SlipAngle = d.SlipAngle });
		}
		Publish();
	}

	// Efectos del casco: anticolisión, balizas y parpadeo. Con la pausa no avanzan.
	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		ShipPhysics physics = _car.Physics;
		if (_ship.HasHull3D && physics.HasSurface)
		{
			_ship.UpdateHullEffects(_time);
			(float boost, float damage) = IndustrialHullBeacons.Intensities(_car.Boost.IsImpulseActive, _damageLevel, true, _time, _look.Lamps);
			_ship.SetHullBeacons(boost, damage);
		}

		if (_flicker != null && _flicker.Update(dt))
			_ship.ShowEdgeFlicker(_flicker, _flickerOpacity);

		UpdateLineStateDebug();

		// Atravesable tras recolocarla: el casco parpadea
		float opacity = 1f;
		if (_car.Passable.FromRespawn)
		{
			_blinkTime += dt;
			opacity = _blink.At(_blinkTime);
		}
		else
		{
			_blinkTime = 0f;
		}
		if (opacity != _hullOpacity)
		{
			_hullOpacity = opacity;
			_ship.SetHullOpacity(opacity);
		}
	}

	// Texto del estado (CLEAR, PASS L, PASS R, FOLLOW) y pasillo de la trazada, desplazada, hasta la distancia de mirada
	private void UpdateLineStateDebug()
	{
		bool show = ShowLineStates && OS.IsDebugBuild() && _car.Phase == RivalPhase.Running;
		if (!show)
		{
			if (_stateLabel != null)
			{
				_stateLabel.Visible = false;
				_corridor.Visible = false;
			}
			return;
		}

		if (_stateLabel == null)
		{
			_stateLabel = new Label3D
			{
				Name = "LineState",
				TopLevel = true,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				NoDepthTest = true,
				FixedSize = true,
				FontSize = 32,
				PixelSize = 0.0012f,
				Shaded = false
			};
			AddChild(_stateLabel);
			_corridorMesh = new ImmediateMesh();
			_corridorMaterial = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				VertexColorUseAsAlbedo = true,
				NoDepthTest = true
			};
			_corridor = new MeshInstance3D { Name = "Corridor", TopLevel = true, Mesh = _corridorMesh };
			AddChild(_corridor);
		}

		ClearLine line = _car.Driver.ClearLine;
		(string text, Color color) = line.State switch
		{
			LineState.PassLeft => ("PASS L", Palette.RivalStatePass),
			LineState.PassRight => ("PASS R", Palette.RivalStatePass),
			LineState.Follow => ("FOLLOW", Palette.RivalStateFollow),
			LineState.Slip => ("SLIP", Palette.RivalStateSlip),
			LineState.AttackLeft => ("ATTACK L", Palette.RivalStateAttack),
			LineState.AttackRight => ("ATTACK R", Palette.RivalStateAttack),
			LineState.DefendLeft => ("DEFEND L", Palette.RivalStateDefend),
			LineState.DefendRight => ("DEFEND R", Palette.RivalStateDefend),
			_ => ("CLEAR", Palette.RivalStateClear)
		};
		_stateLabel.Visible = true;
		_stateLabel.Text = text;
		_stateLabel.Modulate = color;
		_stateLabel.GlobalPosition = GlobalPosition + Vector3.Up * 3f;

		_corridor.Visible = true;
		_corridorMesh.ClearSurfaces();
		_corridorMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _corridorMaterial);
		RacingLine racing = _car.Driver.Pilot.Line;
		Circuit.TrackFrame frame = _car.Frame;
		float start = _car.TrackDistance;
		float half = _car.Shape.HalfWidth;
		const float Step = 4f;
		for (float d = 0f; d < line.Lookahead; d += Step)
		{
			foreach (float side in new[] { -half, half })
			{
				AddCorridorPoint(frame, racing, start + d, line.Offset + side, color);
				AddCorridorPoint(frame, racing, start + d + Step, line.Offset + side, color);
			}
		}
		_corridorMesh.SurfaceEnd();
	}

	private void AddCorridorPoint(Circuit.TrackFrame frame, RacingLine racing, float distance, float lateral, Color color)
	{
		float lap = frame.LapLength;
		float s = ((distance % lap) + lap) % lap;
		Circuit.FramePoint f = frame.At(s);
		NVec3 p = f.Position + f.Right * (racing.LateralAt(s) + lateral) + f.Normal * 0.3f;
		_corridorMesh.SurfaceSetColor(color);
		_corridorMesh.SurfaceAddVertex(new Vector3(p.X, p.Y, p.Z));
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
