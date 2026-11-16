using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Contact;
using Vectory.Ship;
using Vectory.Test.CircuitTest;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Choques entre naves (spec docs/rivales-3-contacto.md, 12, pruebas 1–17): detección, respuesta, papeles, daño y atravesables
public class ShipContactTests
{
	private const float Step = 1f / 60f;
	private const float Kmh = 1f / 3.6f;
	private static readonly ShipContactSettings Settings = ShipContactSettings.Default;

	private static readonly ShipShape VectorShape = ShipShape.From(ShipDefinitions.Find(ShipCatalog.Ship01).Visual);
	private static readonly ShipShape BladeShape = ShipShape.From(ShipDefinitions.Find(ShipCatalog.Ship02).Visual);

	// Óvalo de pruebas, para lo que necesita barrera
	private static readonly Lazy<(TrackFrame Frame, float BarrierOffset)> Oval = new(() =>
	{
		TrackFile file = TrackFile.Load("test_oval");
		return (file.Frame(), BarrierGeometry.Offset(file.TrackWidth, BarrierGeometry.DefaultGap));
	});

	// Nave de prueba: su física (en el óvalo o sin circuito) y su posición
	private sealed class FakeShip : IContactShip
	{
		private Vector3 _position;

		public FakeShip(int id, ShipShape shape, Vector3 position, float yaw = 0f, float speed = 0f, bool onTrack = false)
		{
			Id = id;
			Shape = shape;
			if (onTrack)
				Physics.SetTrack(Oval.Value.Frame, Oval.Value.BarrierOffset);
			Physics.Place(position, yaw, speed, ShipPhysicsSettings.Default.Boost);
			_position = position;
			PreviousPosition = position;
			// Un primer paso sin moverla, para la barrera y el semiancho
			Physics.AfterMove(ref _position, Physics.Dynamics.Velocity, false, shape.HalfWidth, ShipPhysicsSettings.Default, Step);
		}

		public int Id { get; }
		public ShipShape Shape { get; }
		public ShipPhysics Physics { get; } = new();
		public Vector3 Position => _position;
		public Vector3 PreviousPosition { get; set; }
		public PassableState Passable { get; } = new();
		public bool CanCollide { get; set; } = true;

		public void MoveBy(Vector2 delta)
		{
			_position.X += delta.X;
			_position.Z += delta.Y;
			Physics.AfterContactMove(ref _position);
		}

		// Un paso conduciendo a fondo en línea recta (sin circuito)
		public void Drive(in ShipTuning tuning)
		{
			PreviousPosition = _position;
			Physics.Move(ref _position, new ShipInput { Accelerate = true }, false, true, tuning, Shape.HalfWidth,
				ShipPhysicsSettings.Default, Step);
		}

		public void Set(Vector3 position, Vector2 velocity)
		{
			_position = position;
			PreviousPosition = position;
			Physics.Dynamics.SetVelocity(velocity);
		}
	}

	private static ContactBody Body(Vector2 position, ShipShape shape, float height = 2f, float yaw = 0f) =>
		new(position, position, yaw, height, shape);

	// Hacia delante con yaw = 0 es -Z: en planta, (0, -1)
	private static readonly Vector2 Ahead = new(0f, -1f);

	// Alcance: las dos naves van hacia delante y la normal también (roce por detrás, si es roce)
	private static ContactResult RearEnd(float rearKmh, float frontKmh, bool continuing = false, bool cooldown = false) =>
		ContactResponse.Resolve(Ahead * (rearKmh * Kmh), Ahead * (frontKmh * Kmh), new ContactHit(Ahead, 0.2f), false, false,
			continuing, cooldown, Settings, Step, Ahead, Ahead);

	// Puerta con puerta: las dos hacia delante y la normal de lado; closingKmh de una hacia la otra
	private static readonly Vector2 Side = new(1f, 0f);

	private static ContactResult SideBySide(float closingKmh, bool continuing = false) =>
		ContactResponse.Resolve(Ahead * (200f * Kmh) + Side * (closingKmh * Kmh), Ahead * (200f * Kmh), new ContactHit(Side, 0.2f),
			false, false, continuing, false, Settings, Step, Ahead, Ahead);

	// 1
	[Fact]
	public void Separated_rectangles_do_not_touch_and_overlapping_ones_give_normal_and_depth()
	{
		Assert.False(ShipContactDetector.Detect(Body(Vector2.Zero, VectorShape), Body(new Vector2(6f, 0f), VectorShape), 1.5f, out _));

		Assert.True(ShipContactDetector.Detect(Body(Vector2.Zero, VectorShape), Body(new Vector2(5f, 0f), VectorShape), 1.5f,
			out ContactHit hit));
		Assert.Equal(1f, hit.Normal.X, 4);
		Assert.Equal(0f, hit.Normal.Y, 4);
		Assert.Equal(2f * VectorShape.HalfWidth - 5f, hit.Depth, 4);

		// La normal va de la primera hacia la segunda
		Assert.True(ShipContactDetector.Detect(Body(new Vector2(5f, 0f), VectorShape), Body(Vector2.Zero, VectorShape), 1.5f, out hit));
		Assert.Equal(-1f, hit.Normal.X, 4);
	}

	// 2
	[Fact]
	public void Overlapping_ships_two_meters_apart_in_height_do_not_touch()
	{
		Assert.False(ShipContactDetector.Detect(Body(Vector2.Zero, VectorShape, 2f), Body(new Vector2(4f, 0f), VectorShape, 4f), 1.5f, out _));
		Assert.True(ShipContactDetector.Detect(Body(Vector2.Zero, VectorShape, 2f), Body(new Vector2(4f, 0f), VectorShape, 3f), 1.5f, out _));
	}

	// 3
	[Fact]
	public void A_ship_that_would_jump_past_another_in_one_step_touches_it()
	{
		// De 20 m por detrás a 20 m por delante en un paso, con la otra quieta en medio
		var fast = new ContactBody(new Vector2(0f, -20f), new Vector2(0f, 20f), 0f, 2f, BladeShape);
		ContactBody still = Body(Vector2.Zero, BladeShape);

		Assert.True(ShipContactDetector.Detect(fast, still, 1.5f, out ContactHit hit));
		// La normal de entonces (de la que venía por detrás hacia la de delante) y lo bastante para volver detrás
		Assert.True(Vector2.Dot(hit.Normal, Ahead) > 0.99f);
		Assert.True(hit.Depth > 20f);
	}

	// 4
	[Fact]
	public void A_wider_ship_touches_sooner_side_by_side_and_a_longer_one_sooner_from_behind()
	{
		var side = new Vector2(4f, 0f);
		Assert.True(ShipContactDetector.Detect(Body(Vector2.Zero, VectorShape), Body(side, VectorShape), 1.5f, out _));
		Assert.False(ShipContactDetector.Detect(Body(Vector2.Zero, BladeShape), Body(side, BladeShape), 1.5f, out _));

		var behind = new Vector2(0f, 8f);
		Assert.True(BladeShape.Front + BladeShape.Back > VectorShape.Front + VectorShape.Back);
		Assert.True(ShipContactDetector.Detect(Body(behind, BladeShape), Body(Vector2.Zero, BladeShape), 1.5f, out _));
		Assert.False(ShipContactDetector.Detect(Body(behind, VectorShape), Body(Vector2.Zero, VectorShape), 1.5f, out _));
	}

	// 5
	[Theory]
	[InlineData(20f, ImpactLevel.Graze)]
	[InlineData(60f, ImpactLevel.Medium)]
	[InlineData(120f, ImpactLevel.Strong)]
	[InlineData(30f, ImpactLevel.Medium)]
	[InlineData(90f, ImpactLevel.Strong)]
	public void The_closing_speed_gives_the_level(float closingKmh, ImpactLevel level)
	{
		ContactResult r = RearEnd(200f + closingKmh, 200f);
		Assert.Equal(level, r.Level);
		Assert.Equal(level != ImpactLevel.Graze, r.IsNewImpact);
	}

	// 6
	[Fact]
	public void From_behind_the_rear_ship_loses_and_the_front_one_gains()
	{
		ContactResult medium = RearEnd(260f, 200f);
		Assert.True(Vector2.Dot(medium.A.VelocityChange, Ahead) < 0f);
		Assert.True(Vector2.Dot(medium.B.VelocityChange, Ahead) > 0f);

		// Roce (restitución 0): después van a la misma velocidad por la normal
		ContactResult graze = RearEnd(215f, 200f);
		float rear = Vector2.Dot(Ahead * (215f * Kmh) + graze.A.VelocityChange, Ahead);
		float front = Vector2.Dot(Ahead * (200f * Kmh) + graze.B.VelocityChange, Ahead);
		Assert.Equal(front, rear, 4);
	}

	[Fact]
	public void The_push_along_the_nose_also_reaches_the_engine_and_a_side_push_does_not()
	{
		var d = new ShipDynamics();
		d.Reset(0f, 50f);
		d.ApplyContact(Ahead * 5f, 1f, 0f);
		Assert.Equal(55f, d.Speed, 3);
		Assert.Equal(55f, Vector2.Dot(d.Velocity, Ahead), 3);

		d.Reset(0f, 50f);
		d.ApplyContact(new Vector2(4f, 0f), 1f, 0f);
		Assert.Equal(50f, d.Speed, 3);
		Assert.Equal(4f, d.Velocity.X, 3);
		Assert.Equal(0f, d.Yaw, 5);   // el morro no gira
	}

	// 7
	[Fact]
	public void After_separating_no_ship_overlaps_or_is_inside_the_barrier()
	{
		(TrackFrame frame, float barrier) = Oval.Value;
		FramePoint p = frame.At(200f);
		float yaw = MathF.Atan2(-p.Tangent.X, -p.Tangent.Z);
		Vector3 Lateral(float d) => p.Position + p.PlanRight * d + new Vector3(0f, Track.ShipHover, 0f);

		// Separación a medias
		var a = new FakeShip(0, VectorShape, Lateral(0f), yaw, 50f, true);
		var b = new FakeShip(1, VectorShape, Lateral(4f), yaw, 50f, true);
		var solver = new ShipContactSolver();
		solver.Solve(new IContactShip[] { a, b }, Step);
		Assert.False(Overlaps(a, b));

		// Con A contra la barrera derecha, B (a su izquierda) se lleva toda la separación
		float limit = barrier - VectorShape.HalfWidth;
		a = new FakeShip(0, VectorShape, Lateral(limit + 0.01f), yaw, 50f, true);
		Assert.True(a.Physics.LastBarrierTouch.Contact);
		Vector3 aBefore = a.Position;
		b = new FakeShip(1, VectorShape, Lateral(limit - 4f), yaw, 50f, true);
		solver = new ShipContactSolver();
		solver.Solve(new IContactShip[] { a, b }, Step);
		Assert.False(Overlaps(a, b));
		Assert.Equal(aBefore.X, a.Position.X, 3);
		Assert.Equal(aBefore.Z, a.Position.Z, 3);
		Assert.True(MathF.Abs(a.Physics.Surface.Lateral) <= limit + 1e-3f);
		Assert.True(MathF.Abs(b.Physics.Surface.Lateral) <= limit + 1e-3f);
	}

	private static bool Overlaps(FakeShip a, FakeShip b)
	{
		bool overlap = ShipContactDetector.Overlap(new Vector2(a.Position.X, a.Position.Z), a.Physics.Dynamics.Yaw, a.Shape,
			new Vector2(b.Position.X, b.Position.Z), b.Physics.Dynamics.Yaw, b.Shape, out ContactHit hit);
		return overlap && hit.Depth > 1e-3f;
	}

	// 8
	[Fact]
	public void Ships_already_moving_apart_do_not_hit()
	{
		ContactResult r = RearEnd(200f, 260f);
		Assert.False(r.IsNewImpact);
		Assert.Equal(0f, r.ClosingSpeed);
		Assert.Equal(Vector2.Zero, r.A.VelocityChange);
		Assert.Equal(1f, r.A.SpeedScale);
		Assert.NotEqual(Vector2.Zero, r.A.Move);   // solo separación
	}

	// 9
	[Theory]
	[InlineData(0.1f, 1)]
	[InlineData(0.5f, 2)]
	public void Two_medium_hits_of_the_same_pair_count_once_within_the_wait(float gap, int expected)
	{
		var a = new FakeShip(0, VectorShape, new Vector3(0f, 2f, 7f));
		var b = new FakeShip(1, VectorShape, Vector3.UnitY * 2f);
		var solver = new ShipContactSolver();
		var ships = new IContactShip[] { a, b };
		int impacts = 0;

		int steps = (int)MathF.Round(gap / Step);
		for (int hitIndex = 0; hitIndex < 2; hitIndex++)
		{
			a.Set(new Vector3(0f, 2f, 7f), Ahead * (60f * Kmh));
			b.Set(Vector3.UnitY * 2f, Vector2.Zero);
			solver.Solve(ships, Step);
			foreach (ShipContactEvent e in solver.Events)
				impacts += e.IsNewImpact ? 1 : 0;

			// Separadas hasta el siguiente golpe
			a.Set(new Vector3(0f, 2f, 40f), Vector2.Zero);
			for (int i = 0; i < steps - 1; i++)
				solver.Solve(ships, Step);
		}
		Assert.Equal(expected, impacts);
	}

	// 10
	[Fact]
	public void A_strong_hit_starts_the_correction_for_both()
	{
		var a = new FakeShip(0, VectorShape, new Vector3(0f, 2f, 7f));
		var b = new FakeShip(1, VectorShape, Vector3.UnitY * 2f);
		a.Set(a.Position, Ahead * (150f * Kmh));
		new ShipContactSolver().Solve(new IContactShip[] { a, b }, Step);
		Assert.True(a.Physics.Recovery.IsActive);
		Assert.True(b.Physics.Recovery.IsActive);

		ContactResult medium = RearEnd(260f, 200f);
		Assert.False(medium.A.StartRecovery);
		Assert.False(medium.B.StartRecovery);
	}

	// 11
	[Fact]
	public void From_behind_the_rear_ship_rams()
	{
		ContactResult r = RearEnd(300f, 240f);
		Assert.Equal(ContactRole.Rammer, r.A.Role);
		Assert.Equal(ContactRole.Receiver, r.B.Role);
	}

	// 12
	[Fact]
	public void Close_contributions_are_mutual()
	{
		Assert.Equal((ContactRole.Mutual, ContactRole.Mutual), ContactResponse.Roles(10f, 9f, Settings.MutualMargin));
		Assert.Equal((ContactRole.Rammer, ContactRole.Receiver), ContactResponse.Roles(10f, 6f, Settings.MutualMargin));
		Assert.Equal((ContactRole.Receiver, ContactRole.Rammer), ContactResponse.Roles(-3f, 6f, Settings.MutualMargin));
		Assert.Equal((ContactRole.Mutual, ContactRole.Mutual), ContactResponse.Roles(7f, 10f, Settings.MutualMargin));
	}

	// 13
	[Fact]
	public void Damage_points_follow_the_role()
	{
		ContactDamageSettings s = ContactDamageSettings.Default;
		Assert.Equal(1, ContactDamage.Points(ImpactLevel.Medium, ContactRole.Rammer, s));
		Assert.Equal(0, ContactDamage.Points(ImpactLevel.Medium, ContactRole.Receiver, s));
		Assert.Equal(1, ContactDamage.Points(ImpactLevel.Medium, ContactRole.Mutual, s));
		Assert.Equal(3, ContactDamage.Points(ImpactLevel.Strong, ContactRole.Rammer, s));
		Assert.Equal(1, ContactDamage.Points(ImpactLevel.Strong, ContactRole.Receiver, s));
		Assert.Equal(3, ContactDamage.Points(ImpactLevel.Strong, ContactRole.Mutual, s));
		Assert.Equal(0, ContactDamage.Points(ImpactLevel.Graze, ContactRole.Rammer, s));

		var damage = new DamageModel(DamageSettings.Default);
		damage.AddPoints(3);
		Assert.Equal(1, damage.Level);
		damage.AddImpact(ImpactLevel.Strong);
		Assert.Equal(6, damage.Points);
		damage.Stop();
		damage.AddPoints(3);
		Assert.Equal(6, damage.Points);
	}

	// 14
	[Fact]
	public void Only_the_rammer_loses_extra_speed_in_a_medium_hit()
	{
		ContactResult r = RearEnd(260f, 200f);
		Assert.Equal(1f - Settings.MediumSpeedLoss, r.A.SpeedScale, 5);
		Assert.Equal(1f, r.B.SpeedScale);

		ContactResult strong = RearEnd(320f, 200f);
		Assert.Equal(1f - Settings.StrongSpeedLoss, strong.A.SpeedScale, 5);
		Assert.Equal(1f, strong.B.SpeedScale);

		// Roce de lado: las dos igual, al entrar y mientras dure
		ContactResult graze = SideBySide(15f);
		Assert.Equal(GrazeKind.Side, graze.Graze);
		Assert.Equal(1f - Settings.GrazeEntryLoss, graze.A.SpeedScale, 5);
		Assert.Equal(graze.A.SpeedScale, graze.B.SpeedScale);
		ContactResult sliding = SideBySide(15f, continuing: true);
		Assert.Equal(Settings.GrazeFriction * Step, sliding.A.SpeedLoss, 5);
		Assert.Equal(sliding.A.SpeedLoss, sliding.B.SpeedLoss);

		// Roce por detrás (v1.1): sin pérdidas, ni al entrar ni mientras dure
		ContactResult rear = RearEnd(215f, 200f);
		Assert.Equal(GrazeKind.Rear, rear.Graze);
		Assert.Equal(1f, rear.A.SpeedScale);
		Assert.Equal(1f, rear.B.SpeedScale);
		ContactResult glued = RearEnd(215f, 200f, continuing: true);
		Assert.Equal(0f, glued.A.SpeedLoss);
		Assert.Equal(0f, glued.B.SpeedLoss);
	}

	// 14b
	[Fact]
	public void The_angle_between_the_normal_and_the_heading_tells_rear_from_side()
	{
		float angle = Settings.SideGrazeAngle;
		Vector2 At(float degrees) => new(MathF.Sin(degrees * MathF.PI / 180f), -MathF.Cos(degrees * MathF.PI / 180f));

		Assert.Equal(GrazeKind.Rear, ContactResponse.Kind(At(5f), Ahead, Ahead, angle));
		Assert.Equal(GrazeKind.Rear, ContactResponse.Kind(-At(5f), Ahead, Ahead, angle));   // la normal puede ir hacia atrás
		Assert.Equal(GrazeKind.Side, ContactResponse.Kind(At(80f), Ahead, Ahead, angle));
		Assert.Equal(GrazeKind.Rear, ContactResponse.Kind(At(45f), Ahead, Ahead, angle));    // 45° justos: por detrás
		Assert.Equal(GrazeKind.Side, ContactResponse.Kind(At(46f), Ahead, Ahead, angle));
		// Con la dirección media de las dos
		Assert.Equal(GrazeKind.Rear, ContactResponse.Kind(Ahead, At(20f), At(-20f), angle));
		// Sin dirección (enfrentadas), de lado
		Assert.Equal(GrazeKind.Side, ContactResponse.Kind(Ahead, Ahead, -Ahead, angle));
	}

	// 14c
	[Fact]
	public void Glued_behind_another_ship_at_300_kmh_for_5_s_nobody_slows_down()
	{
		float frontAlone = DriveGlued(false, out _);
		float front = DriveGlued(true, out float rear);

		// Al de delante no le frena nada (el empujón, si acaso, le da algo) y el de detrás va a su velocidad
		Assert.True(front >= frontAlone - 0.01f, $"front {front * 3.6f:0.0} km/h, alone {frontAlone * 3.6f:0.0} km/h");
		Assert.Equal(front * 3.6f, rear * 3.6f, 0);
	}

	// Dos VECTOR en línea recta sin circuito: la de delante, a 300 km/h; la de detrás, pegada y un poco más rápida
	private static float DriveGlued(bool contact, out float rearSpeed)
	{
		ShipTuning fast = ShipDefinitions.Find(ShipCatalog.Ship01).Technical;
		ShipTuning steady = fast;
		steady.MaxSpeed = 300f * Kmh;
		var front = new FakeShip(1, VectorShape, new Vector3(0f, 2f, -(VectorShape.Front + VectorShape.Back + 0.05f)), 0f, steady.MaxSpeed);
		var rear = new FakeShip(0, VectorShape, Vector3.UnitY * 2f, 0f, steady.MaxSpeed);
		var solver = new ShipContactSolver { Enabled = contact };
		var ships = new IContactShip[] { rear, front };
		int touching = 0;
		for (int i = 0; i < 300; i++)
		{
			front.Drive(steady);
			rear.Drive(fast);
			solver.Solve(ships, Step);
			touching += solver.Touched(0) ? 1 : 0;
		}
		if (contact)
			Assert.True(touching > 200, $"only {touching} steps touching");
		rearSpeed = rear.Physics.Dynamics.RealSpeed;
		return front.Physics.Dynamics.RealSpeed;
	}

	// 14d
	[Fact]
	public void A_side_graze_for_1_s_costs_both_the_entry_loss_and_about_20_kmh()
	{
		const float speed = 200f;
		float before = speed * Kmh;
		float afterEntry = before * (1f - Settings.GrazeEntryLoss);
		var d = new ShipDynamics();
		d.Reset(0f, before);
		for (int i = 0; i < 60; i++)
		{
			ContactResult r = SideBySide(15f, continuing: i > 0);
			d.ApplyContact(Vector2.Zero, r.A.SpeedScale, r.A.SpeedLoss);
		}
		// El primer paso es la entrada; la fricción, los otros 59 (unos 20 km/h en el segundo)
		Assert.Equal((afterEntry - 59 * Settings.GrazeFriction * Step) * 3.6f, d.Speed * 3.6f, 2);
		Assert.InRange((afterEntry - d.Speed) * 3.6f, 19f, 20f);
	}

	[Fact]
	public void The_graze_meter_keeps_the_last_5_seconds()
	{
		var meter = new GrazeLossMeter();
		for (int i = 0; i < 60; i++)
			meter.Add(Step, 0.1f, GrazeKind.Side);
		Assert.Equal(6f, meter.Total, 3);
		Assert.Equal(GrazeKind.Side, meter.Kind);
		for (int i = 0; i < 4 * 60; i++)
			meter.Add(Step, 0f, GrazeKind.Rear);
		Assert.Equal(6f, meter.Total, 3);
		Assert.Equal(GrazeKind.Rear, meter.Kind);
		for (int i = 0; i < 61; i++)
			meter.Add(Step, 0f, GrazeKind.None);
		Assert.Equal(0f, meter.Total, 3);
	}

	// 15
	[Fact]
	public void A_finished_ship_hits_nobody_but_still_hits_the_barrier()
	{
		var a = new FakeShip(0, VectorShape, new Vector3(0f, 2f, 6f));
		var b = new FakeShip(1, VectorShape, Vector3.UnitY * 2f);
		a.Set(a.Position, Ahead * (150f * Kmh));
		b.Passable.Finished = true;
		var solver = new ShipContactSolver();
		solver.Solve(new IContactShip[] { a, b }, Step);
		Assert.Empty(solver.Events);
		Assert.False(solver.Touched(0));

		(TrackFrame frame, float barrier) = Oval.Value;
		FramePoint p = frame.At(200f);
		float yaw = MathF.Atan2(-p.Tangent.X, -p.Tangent.Z);
		var finished = new FakeShip(2, VectorShape, p.Position + p.PlanRight * barrier + Vector3.UnitY * Track.ShipHover, yaw, 30f, true);
		finished.Passable.Finished = true;
		Assert.True(finished.Physics.LastBarrierTouch.Contact);
	}

	// 16
	[Fact]
	public void A_respawned_ship_is_passable_for_two_seconds_and_while_it_still_overlaps()
	{
		var state = new PassableState();
		Assert.False(state.IsPassable);
		state.Respawned();
		for (int i = 0; i < 119; i++)
			state.Update(Step, false);
		Assert.True(state.IsPassable);
		state.Update(Step, false);
		state.Update(Step, false);
		Assert.False(state.IsPassable);

		state.Respawned();
		for (int i = 0; i < 125; i++)
			state.Update(Step, true);
		Assert.True(state.FromRespawn);
		state.Update(Step, false);
		Assert.False(state.IsPassable);

		var blink = RespawnBlink.Default;
		Assert.Equal(0.4f, blink.At(0.01f));
		Assert.Equal(1f, blink.At(0.2f));
		Assert.Equal(0.4f, blink.At(0.26f));
	}

	// 17
	[Fact]
	public void Stuck_against_another_ship_respawns_once()
	{
		var a = new FakeShip(0, VectorShape, new Vector3(0f, 2f, 7f));
		var b = new FakeShip(1, VectorShape, Vector3.UnitY * 2f);
		var solver = new ShipContactSolver();
		var detector = new RespawnDetector(new RespawnSettings
		{
			BarrierOffset = 20.5f,
			StuckSpeed = RespawnSettings.DefaultStuckSpeedKmh * Kmh,
			StuckTime = RespawnSettings.DefaultStuckTime,
			OutsideMargin = RespawnSettings.DefaultOutsideMargin
		});

		int respawns = 0;
		for (int i = 0; i < 200; i++)
		{
			// Empuja contra la otra a 5 km/h, sin avanzar
			a.Set(new Vector3(0f, 2f, 7f), Ahead * (5f * Kmh));
			b.Set(Vector3.UnitY * 2f, Vector2.Zero);
			solver.Solve(new IContactShip[] { a, b }, Step);
			if (detector.Update(Step, solver.Touched(0), 5f * Kmh, 0f))
				respawns++;
		}
		Assert.Equal(1, respawns);
	}

	[Fact]
	public void Collision_lengths_match_the_hull_manifests()
	{
		AssertLength(ShipCatalog.Ship01, "hull_vector", m => m.Bounds);
		AssertLength(ShipCatalog.Ship02, "hull_blade", m => m.BoundsWithoutProbe);
		AssertLength(ShipCatalog.Ship03, "hull_hammer", m => m.Bounds);
	}

	private static void AssertLength(string shipId, string hull, Func<HullManifest, CockpitManifestBounds> bounds)
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
			dir = dir.Parent;
		HullManifest manifest = HullManifest.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "assets", "ships", hull + ".manifest.json")))!;
		CockpitManifestBounds box = bounds(manifest);
		ShipVisualProfile visual = ShipDefinitions.Find(shipId).Visual;
		Assert.Equal(-box.Min[2], visual.CollisionFront, 2);
		Assert.Equal(box.Max[2], visual.CollisionBack, 2);
		Assert.Equal(box.Max[0], visual.CollisionHalfWidth, 2);
	}
}
