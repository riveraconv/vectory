using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Radar;

// Parámetros del radar (spec docs/radar-minimapa.md, 8). Los valores de arranque también son los del Inspector.
public sealed class RadarSettings
{
	public const float DefaultRange = 120f;
	public const float DefaultCloseDistance = 12f;
	public const float DefaultMaxHeightDifference = 15f;
	public const float DefaultSweepPeriod = 1.6f;
	public const float DefaultEchoFade = 1.6f;

	public float Range { get; init; } = DefaultRange;                               // m
	public float CloseDistance { get; init; } = DefaultCloseDistance;               // m: nave pegada por debajo
	public float MaxHeightDifference { get; init; } = DefaultMaxHeightDifference;   // m: más diferencia, otro nivel del circuito
	public float SweepPeriod { get; init; } = DefaultSweepPeriod;                   // s por vuelta del haz
	public float EchoFade { get; init; } = DefaultEchoFade;                         // s en apagarse un eco
}

// Una nave en los ejes del jugador: X hacia su derecha e Y hacia su frente, en metros
public readonly record struct RadarContact(float X, float Y, float Distance)
{
	// Ángulo en el sentido del haz: 0 delante, π/2 a la derecha, de 0 a 2π
	public float SweepAngle
	{
		get
		{
			float angle = MathF.Atan2(X, Y);
			return angle < 0f ? angle + MathF.Tau : angle;
		}
	}
}

// Proyección a los ejes del jugador (spec 4.1). Va en planta, con el rumbo de la nave, y la altura se mide en vertical: con el
// plano de la nave, un peralte haría desaparecer naves de la misma pista.
public static class RadarProjection
{
	// planForward: el frente del jugador en planta (X, Z del mundo), unitario
	public static RadarContact Project(Vector3 self, Vector2 planForward, Vector3 other)
	{
		float dx = other.X - self.X;
		float dz = other.Z - self.Z;
		// Derecha en planta = (-frente.Z, frente.X), la misma de ShipDynamics
		float x = dx * -planForward.Y + dz * planForward.X;
		float y = dx * planForward.X + dz * planForward.Y;
		return new RadarContact(x, y, MathF.Sqrt(x * x + y * y));
	}

	// Si la nave sale en el radar: dentro del alcance y en el mismo nivel
	public static bool IsVisible(in RadarContact contact, float heightDifference, RadarSettings settings)
	{
		return contact.Distance <= settings.Range && MathF.Abs(heightDifference) <= settings.MaxHeightDifference;
	}
}

// Eco de una nave: dónde estaba al pasar el haz. No se mueve; solo se apaga.
public readonly record struct RadarEcho(float X, float Y, bool IsClose, float Age);

// Aviso de nave pegada: su dirección en pantalla (atan2(−y, x)) y su opacidad
public readonly record struct RadarWarning(float Angle, float Opacity);

// Estado del radar de barrido (spec 4.2): ángulo del haz, ecos y avisos de nave pegada. Un solo estado, sea cual sea
// dónde se dibuje. Sin llamar a Update (pausa) todo queda congelado.
public sealed class RadarSweep
{
	private readonly List<RadarEcho> _echoes = new();
	private readonly List<RadarWarning> _warnings = new();

	public RadarSettings Settings { get; }

	public float Angle { get; private set; }   // haz: 0 delante, en sentido horario, de 0 a 2π
	public IReadOnlyList<RadarEcho> Echoes => _echoes;
	public IReadOnlyList<RadarWarning> Warnings => _warnings;

	public RadarSweep(RadarSettings settings = null)
	{
		Settings = settings ?? new RadarSettings();
	}

	public float EchoOpacity(in RadarEcho echo) => MathF.Max(0f, 1f - echo.Age / Settings.EchoFade);

	public void Update(float delta, Vector3 self, Vector2 planForward, IReadOnlyList<RaceShip> ships)
	{
		AgeEchoes(delta);

		float swept = delta * MathF.Tau / Settings.SweepPeriod;
		float previous = Angle;
		float angle = previous + swept;
		if (angle >= MathF.Tau)
			angle %= MathF.Tau;
		Angle = angle;

		_warnings.Clear();
		for (int i = 0; i < ships.Count; i++)
		{
			Vector3 position = ships[i].Position;
			RadarContact contact = RadarProjection.Project(self, planForward, position);
			if (!RadarProjection.IsVisible(in contact, position.Y - self.Y, Settings))
				continue;

			bool isClose = contact.Distance < Settings.CloseDistance;
			if (isClose)
				_warnings.Add(new RadarWarning(MathF.Atan2(-contact.Y, contact.X),
					0.4f + 0.6f * (1f - contact.Distance / Settings.CloseDistance)));

			if (Crossed(contact.SweepAngle, previous, angle, swept))
				_echoes.Add(new RadarEcho(contact.X, contact.Y, isClose, 0f));
		}
	}

	private void AgeEchoes(float delta)
	{
		for (int i = _echoes.Count - 1; i >= 0; i--)
		{
			RadarEcho echo = _echoes[i];
			float age = echo.Age + delta;
			if (age >= Settings.EchoFade)
				_echoes.RemoveAt(i);
			else
				_echoes[i] = echo with { Age = age };
		}
	}

	// Si el haz ha pasado por ese ángulo en este paso (el inicio no cuenta, el final sí)
	private static bool Crossed(float angle, float previous, float current, float swept)
	{
		if (swept >= MathF.Tau)
			return true;
		return previous <= current
			? angle > previous && angle <= current
			: angle > previous || angle <= current;
	}
}
