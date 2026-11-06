using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Autopilot;

// Tabla radio → velocidad máxima de una nave
public sealed class CurveTable
{
	public IReadOnlyList<float> Radii { get; }
	public IReadOnlyList<float> Speeds { get; }

	public CurveTable(IReadOnlyList<float> radii, IReadOnlyList<float> speeds)
	{
		Radii = radii;
		Speeds = speeds;
	}

	// Entre radios, en línea recta; por debajo del primero, proporcional al radio; por encima del último, la del último
	public float SpeedFor(float radius)
	{
		if (radius <= Radii[0])
			return Speeds[0] * MathF.Max(0f, radius) / Radii[0];

		for (int i = 1; i < Radii.Count; i++)
		{
			if (radius <= Radii[i])
			{
				float t = (radius - Radii[i - 1]) / (Radii[i] - Radii[i - 1]);
				return Speeds[i - 1] + (Speeds[i] - Speeds[i - 1]) * t;
			}
		}
		return Speeds[Speeds.Count - 1];
	}
}

// Calibración de curva de una nave (spec docs/naves-6-equilibrio.md, 4.3): con la física real, en pista plana, la
// velocidad máxima a la que mantiene cada radio sin salirse más de 0,5 m de la línea. Ya incluye su giro, su agarre
// y su deslizamiento. Lógica pura: no depende de Godot.
public static class CurveCalibration
{
	public const float LineErrorLimit = 0.5f;   // m

	private static readonly float[] Radii = { 30f, 40f, 50f, 60f, 70f, 80f, 90f, 100f, 125f, 150f, 200f, 300f, 450f, 600f };

	private const float SettleTime = 3f;       // s hasta que se estabiliza el giro: el error se mide después
	private const float HoldTime = 5f;         // s que tiene que aguantar
	private const float MinSpeed = 5f;         // m/s
	private const int BisectionSteps = 9;
	private const float PointSpacing = 4f;     // m entre puntos del círculo

	public static CurveTable Measure(in ShipTuning tuning, in ShipPhysicsSettings physics, float stepTime)
	{
		var speeds = new float[Radii.Length];
		float best = MinSpeed;
		for (int i = 0; i < Radii.Length; i++)
		{
			// A mayor radio nunca menos velocidad, aunque la medida tenga algo de ruido
			best = MathF.Max(best, MaxSpeedFor(Radii[i], tuning, physics, stepTime, best));
			speeds[i] = best;
		}
		return new CurveTable(Radii, speeds);
	}

	private static float MaxSpeedFor(float radius, in ShipTuning tuning, in ShipPhysicsSettings physics, float stepTime, float atLeast)
	{
		TrackFrame frame = Circle(radius);
		RacingLine line = RacingLine.Center(frame);

		float top = tuning.MaxSpeed;
		if (Holds(frame, line, top, tuning, physics, stepTime))
			return top;

		float low = atLeast;
		float high = top;
		for (int i = 0; i < BisectionSteps; i++)
		{
			float middle = (low + high) * 0.5f;
			if (Holds(frame, line, middle, tuning, physics, stepTime))
				low = middle;
			else
				high = middle;
		}
		return low;
	}

	// La nave da vueltas al círculo a "speed" con la misma dirección que en carrera: ¿se mantiene en la línea?
	private static bool Holds(TrackFrame frame, RacingLine line, float speed, in ShipTuning tuning, in ShipPhysicsSettings physics, float stepTime)
	{
		var driver = new AutopilotDriver(line, SpeedProfile.Free(line.Count, tuning.BrakeDeceleration), tuning, physics.BankGrip, stepTime);
		var ship = new ShipPhysics();
		ship.SetTrack(frame, frame.LapLength);
		FramePoint start = frame.At(0f);
		Vector3 position = start.Position + Vector3.UnitY * physics.Vertical.Hover;
		ship.Place(position, MathF.Atan2(-start.PlanTangent.X, -start.PlanTangent.Z), speed, physics.Boost);

		int settle = (int)(SettleTime / stepTime);
		int total = settle + (int)(HoldTime / stepTime);
		for (int i = 0; i < total; i++)
		{
			ShipDynamics d = ship.Dynamics;
			ShipInput input = driver.Drive(new PilotState(new Vector2(position.X, position.Z), d.Velocity, d.Yaw, d.Steer, d.Speed, ship.Surface.Distance, 0f));
			input.Brake = false;
			input.Accelerate = d.Speed < speed;
			ship.Move(ref position, input, false, true, tuning, 0f, physics, stepTime);

			if (i >= settle && MathF.Abs(ship.Surface.Lateral) > LineErrorLimit)
				return false;
		}
		return true;
	}

	// Círculo plano en sentido horario visto desde arriba, como los circuitos
	private static TrackFrame Circle(float radius)
	{
		int count = Math.Max(32, (int)MathF.Round(2f * MathF.PI * radius / PointSpacing));
		var points = new List<Vector3>(count);
		for (int i = 0; i < count; i++)
		{
			float angle = 2f * MathF.PI * i / count;
			points.Add(new Vector3(radius * MathF.Cos(angle), 0f, radius * MathF.Sin(angle)));
		}
		return new TrackFrame(points);
	}
}
