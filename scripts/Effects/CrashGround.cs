using System;
using System.Numerics;
using Vectory.Circuit;

namespace Vectory.Effects;

// Superficie de la pista en un punto, para los rebotes de las chispas: punto del eje, ejes, lateral y altura sobre la
// superficie a lo largo de Up
public readonly record struct GroundPoint(float Distance, Vector3 AxisPoint, Vector3 Up, Vector3 Right, float Lateral, float Height);

// Suelo y barreras para las chispas (spec docs/efectos-1-choques.md, 10). hint: distancia sobre el eje de la última
// consulta de esa chispa, así cada consulta es local y barata.
public interface ICrashGround
{
	float BarrierOffset { get; }
	GroundPoint Locate(Vector3 position, float hint);
}

// El de la pista, con su marco 3D: desde el hint, unas pocas iteraciones a lo largo de la tangente en planta (las
// chispas se mueven poco entre frames). Lógica pura.
public sealed class TrackCrashGround : ICrashGround
{
	private readonly TrackFrame _frame;

	public TrackCrashGround(TrackFrame frame, float barrierOffset)
	{
		_frame = frame ?? throw new ArgumentNullException(nameof(frame));
		BarrierOffset = barrierOffset;
	}

	public float BarrierOffset { get; }

	public GroundPoint Locate(Vector3 position, float hint)
	{
		FramePoint f = _frame.At(hint);
		for (int i = 0; i < 4; i++)
		{
			Vector3 rel = position - f.Position;
			float ds = rel.X * f.PlanTangent.X + rel.Z * f.PlanTangent.Z;
			if (MathF.Abs(ds) < 1e-3f)
				break;
			f = _frame.At(f.Distance + ds);
		}
		Vector3 r = position - f.Position;
		return new GroundPoint(f.Distance, f.Position, f.Normal, f.Right, Vector3.Dot(r, f.Right), Vector3.Dot(r, f.Normal));
	}
}

// Suelo plano en y = 0 con barreras en x = ±offset: para las pruebas y la maqueta (eje hacia −Z)
public sealed class FlatCrashGround : ICrashGround
{
	public FlatCrashGround(float barrierOffset) => BarrierOffset = barrierOffset;

	public float BarrierOffset { get; }

	public GroundPoint Locate(Vector3 position, float hint) =>
		new(-position.Z, new Vector3(0f, 0f, position.Z), Vector3.UnitY, Vector3.UnitX, position.X, position.Y);
}
