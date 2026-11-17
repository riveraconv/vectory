using System;
using System.Collections.Generic;
using Vectory.Rivals;
using Xunit;

namespace Vectory.Test.RivalsTests;

// Errores de la IA e informe de carrera (spec docs/rivales-4-personalidades.md, 10, pruebas 17–19)
public class PilotErrorTests
{
	private const int Curves = 6;

	// Una tirada que siempre sale (una por curva, en todas)
	private static PilotErrors Always(int seed) => new(1f, new Random(seed))
	{
		Settings = PilotErrorSettings.Default with { RatePerLap = Curves, BadStartChance = 1f }
	};

	// 17
	[Fact]
	public void With_the_same_seed_the_same_errors()
	{
		var a = new PilotErrors(1.5f, new Random(42));
		var b = new PilotErrors(1.5f, new Random(42));
		for (int i = 0; i < 200; i++)
			Assert.Equal(a.ForCurve(Curves, i % 3 == 0, false), b.ForCurve(Curves, i % 3 == 0, false));
		Assert.Equal(a.StartDelay(), b.StartDelay());
		Assert.True(a.LateBrakings + a.WideExits > 0);

		// Mitad y mitad, con lo de la tabla
		PilotErrors always = Always(7);
		for (int i = 0; i < 400; i++)
		{
			CurveError error = always.ForCurve(Curves, false, false);
			Assert.NotEqual(ErrorKind.None, error.Kind);
			Assert.InRange(error.WideMeters, 2f, 4f);
			Assert.InRange(error.Loss, 0.3f, 0.8f);
			if (error.Kind == ErrorKind.LateBraking)
				Assert.InRange(error.LateMeters, 5f, 15f);
		}
		Assert.InRange(always.LateBrakings, 160, 240);
		Assert.Equal(400, always.LateBrakings + always.WideExits);
		Assert.InRange(always.StartDelay(), 0.3f, 0.8f);
		Assert.Equal(1, always.BadStarts);
	}

	// 18
	[Fact]
	public void With_a_ship_alongside_there_is_never_an_error()
	{
		PilotErrors errors = Always(3);
		for (int i = 0; i < 500; i++)
			Assert.Equal(CurveError.None, errors.ForCurve(Curves, true, true));
		Assert.Equal(0, errors.LateBrakings + errors.WideExits);

		// Las tiradas siguen corriendo: lo de después no depende de quién iba al lado
		PilotErrors other = Always(3);
		for (int i = 0; i < 500; i++)
			other.ForCurve(Curves, true, false);
		Assert.Equal(errors.ForCurve(Curves, false, false), other.ForCurve(Curves, false, false));
	}

	// 19
	[Fact]
	public void Under_pressure_the_rate_is_one_and_a_half_and_with_errors_off_there_are_none()
	{
		var errors = new PilotErrors(1f, new Random(1));
		Assert.Equal(0.25f / Curves, errors.CurveChance(Curves, false), 5);
		Assert.Equal(1.5f * 0.25f / Curves, errors.CurveChance(Curves, true), 5);
		Assert.Equal(2f * 0.25f / Curves, new PilotErrors(2f, new Random(1)).CurveChance(Curves, false), 5);

		// Contando, bajo presión salen alrededor de 1,5 veces más
		var calm = new PilotErrors(1f, new Random(9));
		var pressed = new PilotErrors(1f, new Random(9));
		for (int i = 0; i < 60000; i++)
		{
			calm.ForCurve(Curves, false, false);
			pressed.ForCurve(Curves, true, false);
		}
		double ratio = (double)(pressed.LateBrakings + pressed.WideExits) / (calm.LateBrakings + calm.WideExits);
		Assert.InRange(ratio, 1.4, 1.6);

		// ERRORS desactivado: ninguno, ni en la salida
		PilotErrors off = Always(5);
		off.Enabled = false;
		for (int i = 0; i < 500; i++)
			Assert.Equal(CurveError.None, off.ForCurve(Curves, true, false));
		Assert.Equal(0f, off.StartDelay());
		Assert.Equal(0, off.BadStarts);
	}

	// Informe de carrera: un adelantamiento cuenta cuando dos naves que corren se cruzan
	[Fact]
	public void Overtakes_count_when_two_racing_ships_swap_places()
	{
		var counter = new OvertakeCounter();
		var racing = new HashSet<int> { 0, 1, 2 };
		counter.Update(new[] { 1, 0, 2 }, racing.Contains);
		counter.Update(new[] { 0, 1, 2 }, racing.Contains);
		Assert.Equal(1, counter.Made(0));
		Assert.Equal(1, counter.Suffered(1));

		// Con una terminada, no cuenta
		racing.Remove(2);
		counter.Update(new[] { 0, 2, 1 }, racing.Contains);
		Assert.Equal(0, counter.Made(2));
		Assert.Equal(0, counter.Suffered(1) - 1);

		string text = RaceReport.Text(new[]
		{
			new RaceReportRow(2, "RAZOR", "SPRINTER", "BLADE", 3, 1, 1, 0, 0, 3, 1, 2, 1, 0, 0),
			new RaceReportRow(1, "PLAYER", "—", "VECTOR", 2, 0, null, null, null, 0, 0, null, null, null, null)
		});
		Assert.Contains("1 PLAYER", text);
		Assert.Contains("OVT 3/1", text);
		Assert.Contains("ERR 1/0/0", text);
		Assert.True(text.IndexOf("PLAYER", StringComparison.Ordinal) < text.IndexOf("RAZOR", StringComparison.Ordinal));
	}
}
