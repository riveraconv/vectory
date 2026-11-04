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

	public static IReadOnlyList<ShipStat> Compute(ShipTuning ship, ShipTuning reference)
	{
		return new[]
		{
			new ShipStat(TopSpeed, ShipSelectFormat.TopSpeed(ship.MaxSpeed), Segments(ship.MaxSpeed, reference.MaxSpeed)),
			new ShipStat(Acceleration, ShipSelectFormat.Acceleration(ship.Acceleration), Segments(ship.Acceleration, reference.Acceleration)),
			new ShipStat(Handling, ShipSelectFormat.Handling(ship.TurnRate), Segments(ship.TurnRate, reference.TurnRate)),
			// LateralGrip no tiene una unidad que entienda el jugador: solo barra
			new ShipStat(Grip, "", Segments(ship.LateralGrip, reference.LateralGrip)),
			new ShipStat(Braking, ShipSelectFormat.Braking(ship.BrakeDeceleration), Segments(ship.BrakeDeceleration, reference.BrakeDeceleration))
		};
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
