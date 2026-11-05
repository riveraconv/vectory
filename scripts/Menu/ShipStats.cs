using System;
using System.Collections.Generic;
using Vectory.Ship;

namespace Vectory.Menu;

// Un dato de SHIP DATA: etiqueta, valor ya formateado y segmentos encendidos de su barra
public readonly record struct ShipStat(string Label, string Value, int Segments);

// Los 5 datos de SHIP DATA (lógica pura). Los valores son los base de la nave y cada barra la compara con la
// nave de referencia, siempre con un parámetro en el que más es mejor.
public static class ShipStats
{
	public const int SegmentCount = 10;
	public const int ReferenceSegments = 5;

	public const string TopSpeed = "TOP SPEED";
	public const string Acceleration = "ACCELERATION";
	public const string Handling = "HANDLING";
	public const string Grip = "GRIP";
	public const string Braking = "BRAKING";

	private const double SegmentsPerRatio = 50.0;   // ±10 % respecto a la referencia = ±5 segmentos

	private const float KmhPerMs = 3.6f;
	private const float IntegrationStep = 1f / 240f;
	private const float MaxIntegrationTime = 60f;   // tope por si la nave no llega nunca a esa velocidad

	public static IReadOnlyList<ShipStat> Compute(ShipTuning ship, ShipTuning reference)
	{
		float shipTime = SecondsTo(ship, ShipSelectFormat.ReferenceSpeedKmh);
		float referenceTime = SecondsTo(reference, ShipSelectFormat.ReferenceSpeedKmh);

		return new[]
		{
			new ShipStat(TopSpeed, ShipSelectFormat.TopSpeed(ship.MaxSpeed), Segments(ship.MaxSpeed, reference.MaxSpeed)),
			// Por tiempo, para que cuente la forma de la curva: menos tiempo que la referencia = más segmentos
			new ShipStat(Acceleration, ShipSelectFormat.Acceleration(shipTime), Segments(referenceTime, shipTime)),
			new ShipStat(Handling, ShipSelectFormat.Handling(ship.TurnRate), Segments(ship.TurnRate, reference.TurnRate)),
			// LateralGrip no tiene una unidad que entienda el jugador: solo barra
			new ShipStat(Grip, "", Segments(ship.LateralGrip, reference.LateralGrip)),
			new ShipStat(Braking, ShipSelectFormat.Braking(ship.BrakeDeceleration), Segments(ship.BrakeDeceleration, reference.BrakeDeceleration))
		};
	}

	// Segundos con W a fondo desde parada hasta esa velocidad, integrando la dinámica real (con la curva, sin pista ni daño)
	public static float SecondsTo(in ShipTuning ship, float speedKmh)
	{
		var input = new ShipInput { Accelerate = true };
		var dynamics = new ShipDynamics();
		dynamics.Reset(0f);

		float target = speedKmh / KmhPerMs;
		float time = 0f;
		while (dynamics.Speed < target && time < MaxIntegrationTime)
		{
			float before = dynamics.Speed;
			dynamics.Step(input, ship, IntegrationStep);
			time += IntegrationStep;

			// El instante del cruce dentro del último paso: si no, el tiempo sale hasta un paso de más y cambia el redondeo
			if (dynamics.Speed >= target && dynamics.Speed > before)
				time -= IntegrationStep * (dynamics.Speed - target) / (dynamics.Speed - before);
		}
		return time;
	}

	// Nave sin datos todavía (COMING SOON): "—" y barras apagadas
	public static IReadOnlyList<ShipStat> Empty()
	{
		string none = ShipSelectFormat.None;
		return new[]
		{
			new ShipStat(TopSpeed, none, 0),
			new ShipStat(Acceleration, none, 0),
			new ShipStat(Handling, none, 0),
			new ShipStat(Grip, none, 0),
			new ShipStat(Braking, none, 0)
		};
	}

	// Segmentos encendidos = 5 + redondeo(50 × (valor / referencia − 1)), entre 0 y 10
	public static int Segments(float value, float reference)
	{
		if (reference <= 0f)
			return ReferenceSegments;

		double offset = Math.Round(SegmentsPerRatio * ((double)value / reference - 1.0), MidpointRounding.AwayFromZero);
		return (int)Math.Clamp(ReferenceSegments + offset, 0.0, SegmentCount);
	}
}
