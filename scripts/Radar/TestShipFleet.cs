using System;
using System.Collections.Generic;
using Vectory.Circuit;

namespace Vectory.Radar;

// Una fila de la tabla de las naves de prueba (spec docs/radar-minimapa.md, 3.2). Las cercanas oscilan alrededor del
// jugador; las lejanas (IsFar) se le acercan o se alejan a una velocidad constante respecto a él.
public readonly record struct TestShipSpec(
	float Base, float Amplitude, float Frequency, float Phase,
	float LateralAmplitude, float LateralFrequency, float LateralPhase,
	float RelativeSpeed = 0f, bool IsFar = false);

// Dónde va cada nave de prueba respecto al jugador, sin física
public static class TestShipPattern
{
	public const float OverlapAlong = 7f;      // m por delante o por detrás: por debajo, quedaría encima del jugador...
	public const float OverlapLateral = 3.8f;  // ...si además está a menos de estos m de lado, y se aparta hasta aquí

	public static readonly IReadOnlyList<TestShipSpec> Specs = new TestShipSpec[]
	{
		new(35f, 45f, 0.23f, 0f, 6f, 0.31f, 1f),
		new(-25f, 35f, 0.19f, 2f, 7f, 0.27f, 4f),
		new(5f, 20f, 0.37f, 4f, -6f, 0.40f, 2f),
		new(110f, 70f, 0.11f, 1f, 5f, 0.22f, 0f),
		new(920f, 0f, 0f, 0f, 4f, 0.20f, 1f, -1f, true),
		new(-1550f, 0f, 0f, 0f, -4f, 0.25f, 3f, 1.5f, true)
	};

	// La tecla va pasando por 0, 2, 4 y 6
	public static int NextCount(int count) => count >= Specs.Count ? 0 : count + 2;

	// Distancia sobre la pista (sin dar la vuelta: TrackFrame lo hace) y desplazamiento lateral a los t segundos.
	// maxLateral limita el lateral a la pista.
	public static void Compute(in TestShipSpec spec, float t, float playerDistance, float playerLateral, float maxLateral,
		out float distance, out float lateral)
	{
		lateral = spec.LateralAmplitude * MathF.Sin(spec.LateralFrequency * t + spec.LateralPhase);

		if (spec.IsFar)
		{
			distance = playerDistance + spec.Base + spec.RelativeSpeed * t;
		}
		else
		{
			distance = playerDistance + spec.Base + spec.Amplitude * MathF.Sin(spec.Frequency * t + spec.Phase);
			if (MathF.Abs(distance - playerDistance) < OverlapAlong && MathF.Abs(lateral - playerLateral) < OverlapLateral)
			{
				float side = lateral >= playerLateral ? 1f : -1f;
				lateral = playerLateral + side * OverlapLateral;
			}
		}

		lateral = Math.Clamp(lateral, -maxLateral, maxLateral);
	}
}

// Las naves de prueba activas: lleva su reloj y las escribe en la lista de naves en carrera, colocadas con el marco de la
// pista (así siguen pendientes y peraltes). No afectan a la carrera: no cuentan para nada más que el radar y el minimapa.
public sealed class TestShipFleet
{
	private const int FirstId = 1000;   // los rivales tendrán sus propios Id por debajo

	private readonly RaceShipList _ships;
	private readonly RaceShip[] _current = new RaceShip[TestShipPattern.Specs.Count];

	public int Count { get; private set; }
	public float Time { get; private set; }

	// La nave i tal como quedó en el último Update, para colocar su casco
	public RaceShip Ship(int index) => _current[index];

	public TestShipFleet(RaceShipList ships, int count = 0)
	{
		_ships = ships;
		SetCount(count);
	}

	// Las que sobran salen de la lista; el reloj sigue, así el patrón no da saltos al cambiar el número
	public void SetCount(int count)
	{
		for (int i = count; i < Count; i++)
			_ships.Remove(FirstId + i);
		Count = count;
	}

	public void Update(float delta, TrackFrame frame, float playerDistance, float playerLateral, float hover, float maxLateral)
	{
		Time += delta;
		for (int i = 0; i < Count; i++)
		{
			TestShipSpec spec = TestShipPattern.Specs[i];
			TestShipPattern.Compute(in spec, Time, playerDistance, playerLateral, maxLateral, out float distance, out float lateral);
			FramePoint point = frame.At(distance);
			_current[i] = new RaceShip(FirstId + i, point.PointAt(lateral, hover), point.Tangent, point.Normal);
			_ships.Set(in _current[i]);
		}
	}
}
