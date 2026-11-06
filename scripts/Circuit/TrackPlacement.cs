using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Circuit;

// Dónde colocar una nave sobre la pista: parrilla, salida lanzada y recolocación. Lo usan Track y lo que simule una
// nave sin escena. Lógica pura: no depende de Godot.
public static class TrackPlacement
{
	// Marca de la posición 1..N: centrada en ella, con el morro (a noseDistance del origen de la nave) justo detrás
	// de la línea transversal, mirando en el sentido de la marcha, a la altura de vuelo sobre la superficie.
	public static void GridStart(TrackFrame frame, int position, float noseDistance, float trackWidth, in GridSettings grid,
		float hover, out Vector3 shipPosition, out float yaw)
	{
		const float NoseGap = 0.5f;

		GridSlot slot = GridLayout.Slot(position, trackWidth, grid);
		PointBehindFinish(frame, slot.Distance, out Vector3 point, out Vector3 forward);
		Vector3 right = Normalized(Vector3.Cross(forward, Vector3.UnitY));

		float back = noseDistance + NoseGap;
		float x = point.X + right.X * slot.Lateral - forward.X * back;
		float z = point.Z + right.Z * slot.Lateral - forward.Z * back;
		float y = frame.PointAt(frame.LapLength - slot.Distance - back, slot.Lateral, 0f).Y + hover;

		shipPosition = new Vector3(x, y, z);
		yaw = MathF.Atan2(-forward.X, -forward.Z);
	}

	// Salida lanzada: sobre el eje, centrada en la pista, "distance" metros antes de la meta (medidos sobre la
	// línea central) y mirando en el sentido de la marcha.
	public static void FlyingStart(TrackFrame frame, float distance, float hover, out Vector3 shipPosition, out float yaw)
	{
		PointBehindFinish(frame, distance, out Vector3 point, out Vector3 forward);
		float y = frame.PointAt(frame.LapLength - MathF.Max(0f, distance), 0f, 0f).Y + hover;
		shipPosition = new Vector3(point.X, y, point.Z);
		yaw = MathF.Atan2(-forward.X, -forward.Z);
	}

	// Recolocación: en el punto del eje bajo la posición, centrada, a la altura de vuelo sobre la superficie y
	// mirando en el sentido de la marcha
	public static void Respawn(TrackFrame frame, Vector3 position, float hover, out Vector3 shipPosition, out float yaw)
	{
		FramePoint f = frame.SurfaceUnder(position.X, position.Z, position.Y).Frame;
		shipPosition = new Vector3(f.Position.X, f.Position.Y + hover, f.Position.Z);
		yaw = MathF.Atan2(-f.PlanTangent.X, -f.PlanTangent.Z);
	}

	// Punto de la línea central a "distance" metros por detrás de la meta (en sentido contrario al de la marcha)
	// y la dirección de la marcha en ese punto. Sigue la curva del circuito.
	private static void PointBehindFinish(TrackFrame frame, float distance, out Vector3 point, out Vector3 forward)
	{
		IReadOnlyList<Vector3> plan = frame.Plan;
		int n = plan.Count;
		int i = 0;
		float remaining = MathF.Max(0f, distance);

		for (int step = 0; step < n; step++)
		{
			int prev = (i - 1 + n) % n;
			Vector3 a = plan[i];
			Vector3 b = plan[prev];
			float segment = Length(b - a);

			if (remaining <= segment)
			{
				point = a + (b - a) * (segment > 0f ? remaining / segment : 0f);
				forward = Normalized(a - b);
				return;
			}

			remaining -= segment;
			i = prev;
		}

		// Más largo que el circuito entero: se queda en la meta
		point = plan[0];
		forward = Normalized(plan[1] - plan[n - 1]);
	}

	private static float Length(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

	private static Vector3 Normalized(Vector3 v)
	{
		float length = Length(v);
		return length > 0f ? new Vector3(v.X / length, v.Y / length, v.Z / length) : Vector3.Zero;
	}
}
