using System;
using Vectory.Circuit;
using Vectory.Ship;

namespace Vectory.Autopilot;

// Perfil de velocidad de una nave en su trazada (spec docs/naves-6-equilibrio.md, 4.3). En cada punto, la velocidad de
// la tabla para su radio (con el margen de curva y el efecto del peralte), como mucho la punta con su margen de recta
// y la pendiente. Luego se recorre hacia delante con su curva de aceleración, para no pedir más de lo que puede
// acelerar, y hacia atrás con su frenada (la parte que use el piloto), para empezar a frenar a tiempo.
// Márgenes de un piloto (spec docs/rivales-4-personalidades.md, 3.1): el de curva multiplica la velocidad de la tabla
// donde manda la curva y el de recta, la punta donde manda la punta. Con recta 1 y frenada 1 sale lo de la spec 6. El
// paso de uno a otro es continuo (el límite es el menor de los dos) y lo suavizan las pasadas de aceleración y frenada.
// Lógica pura: no depende de Godot.
public sealed class SpeedProfile
{
	// Fracción de la frenada real con la que se calcula: la velocidad real va un poco por detrás de la del motor
	private const float BrakingShare = 0.9f;

	private const float MinRadiusCurvature = 1e-4f;

	public float[] Target { get; }       // m/s en cada punto de la trazada
	public float[] CurveLimit { get; }   // m/s: el de la curva y la punta, antes de recorrerlo
	public float Braking { get; }        // m/s² con los que se ha calculado

	private SpeedProfile(float[] target, float[] curveLimit, float braking)
	{
		Target = target;
		CurveLimit = curveLimit;
		Braking = braking;
	}

	// Sin límite: solo para la calibración, que decide la velocidad por su cuenta
	public static SpeedProfile Free(int count, float braking)
	{
		var target = new float[count];
		Array.Fill(target, float.MaxValue);
		return new SpeedProfile(target, target, braking);
	}

	// curveMargin: fracción de la velocidad de curva; straightMargin: fracción de la punta; brakingShare: fracción de
	// la frenada con la que se calcula (sobre BrakingShare). El perfil del impulso del boost lleva extraTop (m/s sobre la
	// punta donde manda la punta) y va sin accelerationPass: solo límites y frenadas, porque acelera más de lo que sabe la
	// curva de aceleración.
	public static SpeedProfile Build(RacingLine line, TrackFrame frame, in ShipTuning tuning, CurveTable table,
		in ShipPhysicsSettings physics, float curveMargin, float straightMargin = 1f, float brakingShare = 1f,
		bool accelerationPass = true, float extraTop = 0f)
	{
		int count = line.Count;
		float ds = line.PointSpacing;
		var limit = new float[count];
		var slopeFactor = new float[count];
		for (int i = 0; i < count; i++)
		{
			FramePoint f = frame.At(ds * i);
			float curvature = line.Curvature[i];
			slopeFactor[i] = SlopeEffect.Factor(MathF.Atan(f.Slope), physics.Slope);
			float top = tuning.MaxSpeed * slopeFactor[i];
			float straightTop = top * straightMargin + extraTop * slopeFactor[i];

			float radius = 1f / MathF.Max(MathF.Abs(curvature), MinRadiusCurvature);
			// Con el giro que da el peralte a favor (o quita en contra); el agarre apenas cambia el radio que se aguanta
			float turnFactor = BankGrip.TurnFactor(f.Bank, curvature, true, physics.BankGrip);
			float inCurve = table.SpeedFor(radius);
			limit[i] = inCurve >= tuning.MaxSpeed ? straightTop : MathF.Min(straightTop, inCurve * curveMargin * turnFactor);
		}

		// Desde el punto más lento, que no depende de lo de antes: una vuelta entera en cada sentido basta
		int slowest = 0;
		for (int i = 1; i < count; i++)
		{
			if (limit[i] < limit[slowest])
				slowest = i;
		}

		var target = (float[])limit.Clone();
		for (int k = 0; accelerationPass && k < count; k++)
		{
			int i = line.Wrap(slowest + k);
			int next = line.Wrap(i + 1);
			float v = target[i];
			float acceleration = tuning.Acceleration * slopeFactor[i]
				* AccelerationCurve.Factor(v, tuning.MaxSpeed * slopeFactor[i], tuning.AccelCurve, tuning.MinThrust);
			target[next] = MathF.Min(target[next], MathF.Sqrt(v * v + 2f * acceleration * ds));
		}

		float braking = tuning.BrakeDeceleration * BrakingShare * brakingShare;
		for (int k = 0; k < count; k++)
		{
			int i = line.Wrap(slowest - k);
			int previous = line.Wrap(i - 1);
			float v = target[i];
			target[previous] = MathF.Min(target[previous], MathF.Sqrt(v * v + 2f * braking * ds));
		}

		return new SpeedProfile(target, limit, braking);
	}
}
