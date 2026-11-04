using System;
using System.Collections.Generic;

namespace Vectory.Ship;

// Los parámetros de ShipTuning que cambian de una nave a otra (spec docs/naves-2-tecnico.md, 2.2).
// Mismo orden que los datos de ShipStats: el panel de ajuste los recorre a la vez.
public enum ShipParameter
{
	MaxSpeed,
	Acceleration,
	TurnRate,
	LateralGrip,
	BrakeDeceleration
}

// Perfil técnico de una nave respecto al de Vector: un multiplicador por parámetro, 1 por defecto. Los parámetros
// sin multiplicador valen lo mismo que en Vector. Lógica pura.
public sealed record ShipMultipliers
{
	// Ningún multiplicador de una definición puede salir de 1 ± este valor
	public const float BalanceLimit = 0.10f;
	private const float LimitTolerance = 0.0001f;   // para que 1,10 no pase del tope por el redondeo del float

	public static readonly IReadOnlyList<ShipParameter> Parameters = Enum.GetValues<ShipParameter>();

	public static readonly ShipMultipliers One = new();

	public float MaxSpeed { get; init; } = 1f;
	public float Acceleration { get; init; } = 1f;
	public float TurnRate { get; init; } = 1f;
	public float LateralGrip { get; init; } = 1f;
	public float BrakeDeceleration { get; init; } = 1f;

	public float this[ShipParameter parameter] => parameter switch
	{
		ShipParameter.MaxSpeed => MaxSpeed,
		ShipParameter.Acceleration => Acceleration,
		ShipParameter.TurnRate => TurnRate,
		ShipParameter.LateralGrip => LateralGrip,
		ShipParameter.BrakeDeceleration => BrakeDeceleration,
		_ => throw new ArgumentOutOfRangeException(nameof(parameter))
	};

	public ShipMultipliers With(ShipParameter parameter, float value) => parameter switch
	{
		ShipParameter.MaxSpeed => this with { MaxSpeed = value },
		ShipParameter.Acceleration => this with { Acceleration = value },
		ShipParameter.TurnRate => this with { TurnRate = value },
		ShipParameter.LateralGrip => this with { LateralGrip = value },
		ShipParameter.BrakeDeceleration => this with { BrakeDeceleration = value },
		_ => throw new ArgumentOutOfRangeException(nameof(parameter))
	};

	// El perfil de la nave a partir del de referencia
	public ShipTuning Apply(ShipTuning reference)
	{
		reference.MaxSpeed *= MaxSpeed;
		reference.Acceleration *= Acceleration;
		reference.TurnRate *= TurnRate;
		reference.LateralGrip *= LateralGrip;
		reference.BrakeDeceleration *= BrakeDeceleration;
		return reference;
	}

	public static bool IsWithinLimit(float multiplier) => MathF.Abs(multiplier - 1f) <= BalanceLimit + LimitTolerance;
}
