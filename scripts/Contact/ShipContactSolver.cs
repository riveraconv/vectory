using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Contact;

// Una nave que choca con las demás: el jugador y cada rival. Quien la implementa mueve su posición y vuelve a pasar la
// barrera (ShipPhysics.AfterContactMove) en MoveBy.
public interface IContactShip
{
	int Id { get; }                     // orden fijo de las parejas: 0 el jugador, 1–7 los rivales
	ShipShape Shape { get; }
	ShipPhysics Physics { get; }
	Vector3 Position { get; }
	Vector3 PreviousPosition { get; }   // al empezar el paso de física
	PassableState Passable { get; }
	bool CanCollide { get; }            // conduce (en la previa nadie choca)
	Slipstream Slipstream { get; }      // su rebufo (spec docs/rivales-4-personalidades.md, 4.1): lo calcula quien lleva la carrera
	void MoveBy(Vector2 delta);
}

// Un contacto entre dos naves en un paso (A tiene el id menor). Graze: en un roce, de lado o por detrás; GrazeLossA / B: m/s que le ha quitado el roce a cada una.
public readonly record struct ShipContactEvent(int A, int B, ImpactLevel Level, bool IsNewImpact, ContactRole RoleA, ContactRole RoleB,
	float ClosingSpeed, GrazeKind Graze = GrazeKind.None, float GrazeLossA = 0f, float GrazeLossB = 0f);

// Paso de contactos entre naves (spec docs/rivales-3-contacto.md, 3.2): después de mover todas (barrera incluida), recorre
// las parejas por orden de id, descarta las lejanas, detecta, separa, aplica el golpe y vuelve a pasar la barrera de las
// que ha movido. Lleva la espera de cada pareja y si estaba en contacto (el roce es continuo). Las atravesables no chocan,
// pero su solape cuenta para que lo sigan siendo. El daño, la sacudida y la consola los pone quien lo usa, con Events.
// Lógica pura: no depende de Godot.
public sealed class ShipContactSolver
{
	private struct PairState
	{
		public double CooldownUntil;   // tiempo del solver hasta el que no cuenta otro golpe medio o fuerte
		public bool InContact;
	}

	private readonly Dictionary<long, PairState> _pairs = new();
	private readonly List<ShipContactEvent> _events = new();
	private readonly HashSet<int> _touching = new();
	private readonly HashSet<int> _overlapping = new();
	private IContactShip[] _order = Array.Empty<IContactShip>();
	private static readonly Comparison<IContactShip> ById = (x, y) => x.Id.CompareTo(y.Id);
	private double _time;

	public ShipContactSettings Settings { get; set; } = ShipContactSettings.Default;

	// Con false (panel de pruebas) las naves se vuelven a atravesar
	public bool Enabled { get; set; } = true;

	// Contactos del último paso
	public IReadOnlyList<ShipContactEvent> Events => _events;

	// Si la nave tocó a otra en el último paso (para el atasco)
	public bool Touched(int id) => _touching.Contains(id);

	public void Solve(IReadOnlyList<IContactShip> ships, float dt)
	{
		_events.Clear();
		_touching.Clear();
		_overlapping.Clear();

		_time += dt;

		if (_order.Length != ships.Count)
			_order = new IContactShip[ships.Count];
		for (int i = 0; i < ships.Count; i++)
			_order[i] = ships[i];
		Array.Sort(_order, ById);

		if (Enabled)
		{
			for (int i = 0; i < _order.Length; i++)
			{
				for (int j = i + 1; j < _order.Length; j++)
					SolvePair(_order[i], _order[j], dt);
			}
		}

		foreach (IContactShip ship in _order)
			ship.Passable.Update(dt, _overlapping.Contains(ship.Id));
	}

	private void SolvePair(IContactShip a, IContactShip b, float dt)
	{
		long key = ((long)a.Id << 32) | (uint)b.Id;
		_pairs.TryGetValue(key, out PairState state);

		ContactHit hit = default;
		bool hasHit = a.CanCollide && b.CanCollide && Detect(a, b, out hit);
		if (!hasHit)
		{
			state.InContact = false;
			_pairs[key] = state;
			return;
		}

		if (a.Passable.IsPassable || b.Passable.IsPassable)
		{
			_overlapping.Add(a.Id);
			_overlapping.Add(b.Id);
			state.InContact = false;
			_pairs[key] = state;
			return;
		}

		ShipContactSettings s = Settings;
		ShipDynamics da = a.Physics.Dynamics;
		ShipDynamics db = b.Physics.Dynamics;
		ContactResult r = ContactResponse.Resolve(da.Velocity, db.Velocity, hit,
			Blocked(a, -hit.Normal), Blocked(b, hit.Normal), state.InContact, _time < state.CooldownUntil - 1e-6, s, dt,
			da.Forward, db.Forward);
		float lossA = r.Graze == GrazeKind.None ? 0f : GrazeLoss(da.RealSpeed, r.A);
		float lossB = r.Graze == GrazeKind.None ? 0f : GrazeLoss(db.RealSpeed, r.B);

		state.InContact = true;
		if (r.IsNewImpact)
			state.CooldownUntil = _time + s.PairCooldown;
		_pairs[key] = state;

		Apply(a, r.A, s);
		Apply(b, r.B, s);
		_touching.Add(a.Id);
		_touching.Add(b.Id);
		_events.Add(new ShipContactEvent(a.Id, b.Id, r.Level, r.IsNewImpact, r.A.Role, r.B.Role, r.ClosingSpeed, r.Graze, lossA, lossB));
	}

	// Lo que el roce le quita a una nave (m/s): la fracción de entrada y la fricción, sin el empujón
	private static float GrazeLoss(float speed, in ContactShipEffect e) => speed * (1f - e.SpeedScale) + e.SpeedLoss;

	private bool Detect(IContactShip a, IContactShip b, out ContactHit hit)
	{
		hit = default;
		Vector3 pa = a.Position;
		Vector3 pb = b.Position;
		float cull = Settings.CullDistance;
		float dx = pb.X - pa.X;
		float dz = pb.Z - pa.Z;
		if (dx * dx + dz * dz > cull * cull)
			return false;
		return ShipContactDetector.Detect(Body(a), Body(b), Settings.MaxHeightDifference, out hit);
	}

	private static ContactBody Body(IContactShip ship)
	{
		Vector3 p = ship.Position;
		Vector3 previous = ship.PreviousPosition;
		ShipPhysics physics = ship.Physics;
		float height = physics.HasSurface ? p.Y - physics.Surface.SurfaceY : p.Y;
		return new ContactBody(new Vector2(p.X, p.Z), new Vector2(previous.X, previous.Z), physics.Dynamics.Yaw, height, ship.Shape);
	}

	// La nave toca la barrera hacia donde la separación la movería
	private static bool Blocked(IContactShip ship, Vector2 move)
	{
		ShipPhysics physics = ship.Physics;
		BarrierTouch touch = physics.LastBarrierTouch;
		if (!touch.Contact || !physics.HasSurface)
			return false;
		Vector3 right = physics.Surface.Frame.PlanRight;
		float sign = touch.Side == BarrierSide.Right ? 1f : -1f;
		return Vector2.Dot(move, new Vector2(right.X, right.Z) * sign) > 0f;
	}

	private static void Apply(IContactShip ship, in ContactShipEffect e, in ShipContactSettings s)
	{
		ship.Physics.ApplyShipContact(e.VelocityChange, e.SpeedScale, e.SpeedLoss, e.EndDrift, e.StartRecovery,
			s.RecoveryDuration, s.RecoveryControl);
		if (e.Move != Vector2.Zero)
			ship.MoveBy(e.Move);
	}
}
