using System;

namespace Vectory.Circuit;

// Detecta cuándo la nave cruza un punto de la línea central del circuito yendo HACIA DELANTE.
// Lógica pura (sin Godot): trabaja solo con índices de la línea central, que es un bucle cerrado.
//
// Cada frame se le pasa el índice actual de la nave (Update) y luego se pregunta por
// cada punto de interés (Crossed). Se ignoran:
//  - los movimientos hacia atrás,
//  - los saltos grandes (por ejemplo, al recolocar la nave con PlaceAt).
public sealed class LineCrossingDetector
{
	private readonly int _sampleCount;
	private readonly int _maxStep;

	private bool _hasPrevious;
	private int _previous;
	private int _stepStart;
	private int _forwardSteps;

	// sampleCount: número de muestras de la línea central.
	// maxStep: máximo de muestras que se considera un avance normal en un frame.
	public LineCrossingDetector(int sampleCount, int maxStep = 25)
	{
		if (sampleCount < 4)
			throw new ArgumentOutOfRangeException(nameof(sampleCount), "Hacen falta al menos 4 muestras.");
		if (maxStep < 1)
			throw new ArgumentOutOfRangeException(nameof(maxStep), "El paso máximo debe ser al menos 1.");

		_sampleCount = sampleCount;
		// Más de la mitad del bucle no se puede distinguir de ir hacia atrás
		_maxStep = Math.Min(maxStep, (sampleCount - 1) / 2);
	}

	// Olvida la posición anterior (el siguiente Update solo fija el punto de partida).
	public void Reset()
	{
		_hasPrevious = false;
		_forwardSteps = 0;
	}

	public void Update(int currentIndex)
	{
		if (currentIndex < 0 || currentIndex >= _sampleCount)
			throw new ArgumentOutOfRangeException(nameof(currentIndex));

		if (!_hasPrevious)
		{
			_hasPrevious = true;
			_previous = currentIndex;
			_stepStart = currentIndex;
			_forwardSteps = 0;
			return;
		}

		int diff = Mod(currentIndex - _previous);
		_stepStart = _previous;
		_forwardSteps = (diff >= 1 && diff <= _maxStep) ? diff : 0;
		_previous = currentIndex;
	}

	// ¿Ha cruzado el punto targetIndex hacia delante en el último Update?
	public bool Crossed(int targetIndex)
	{
		if (_forwardSteps == 0)
			return false;

		int offset = Mod(targetIndex - _stepStart);
		return offset >= 1 && offset <= _forwardSteps;
	}

	private int Mod(int value)
	{
		return ((value % _sampleCount) + _sampleCount) % _sampleCount;
	}
}