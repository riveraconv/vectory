using System;

namespace Vectory.Circuit;

// Destello de un nivel de impacto: radio sobre el eje (m), intensidad máxima (0..1) y duración (s)
public struct FlashLevelSettings
{
	public float Radius;
	public float Intensity;
	public float Duration;   // en el roce: lo que tarda en apagarse al separarse
}

public struct BarrierFlashSettings
{
	public FlashLevelSettings Graze;
	public FlashLevelSettings Medium;
	public FlashLevelSettings Strong;

	// Valores de la spec (tabla de la sección 5)
	public static BarrierFlashSettings Default => new()
	{
		Graze = new FlashLevelSettings { Radius = 3f, Intensity = 0.4f, Duration = 0.2f },
		Medium = new FlashLevelSettings { Radius = 7f, Intensity = 1f, Duration = 0.4f },
		Strong = new FlashLevelSettings { Radius = 10f, Intensity = 1f, Duration = 0.6f }
	};
}

// Destello naranja en la barrera (spec docs/barreras-y-colisiones.md, 5). Uno por lado:
// un impacto nuevo en un lado sustituye al anterior de ese lado. La intensidad es máxima en el
// punto de impacto y baja en línea recta hasta 0 en el radio (distancias medidas sobre el eje,
// dando la vuelta por la meta).
//  - Medio y fuerte: se apagan en línea recta durante su duración.
//  - Roce: se mantiene mientras se llame a Graze en cada paso (sigue a la nave) y se apaga en su
//    duración al dejar de rozar. No tapa un destello más fuerte: lo sustituye cuando este ya ha
//    bajado de la intensidad del roce.
// Update primero (envejece lo que hay) y después Impact / Graze del paso actual.
// Lógica pura: no depende de Godot.
public sealed class BarrierFlash
{
	private enum Kind { None, Graze, Impact }

	private struct SideState
	{
		public Kind Kind;
		public FlashLevelSettings Level;
		public float Center;
		public float Age;
		public bool Touched;   // roce renovado desde el último Update
	}

	private readonly float _lapLength;
	private readonly BarrierFlashSettings _settings;
	private readonly SideState[] _sides = new SideState[2];

	public BarrierFlash(float lapLength, BarrierFlashSettings settings)
	{
		_lapLength = MathF.Max(0f, lapLength);
		_settings = settings;
	}

	public void Update(float dt)
	{
		for (int i = 0; i < 2; i++)
		{
			ref SideState s = ref _sides[i];
			switch (s.Kind)
			{
				case Kind.Impact:
					s.Age += dt;
					if (s.Age >= s.Level.Duration)
						s.Kind = Kind.None;
					break;
				case Kind.Graze:
					if (s.Touched)
					{
						s.Age = 0f;
					}
					else
					{
						s.Age += dt;
						if (s.Age >= s.Level.Duration)
							s.Kind = Kind.None;
					}
					break;
			}
			s.Touched = false;
		}
	}

	// Impacto en un punto del eje (m desde la meta). Un roce se trata como Graze.
	public void Impact(BarrierSide side, float distance, ImpactLevel level)
	{
		if (level == ImpactLevel.Graze)
		{
			Graze(side, distance);
			return;
		}

		_sides[(int)side] = new SideState
		{
			Kind = Kind.Impact,
			Level = level == ImpactLevel.Strong ? _settings.Strong : _settings.Medium,
			Center = Wrap(distance),
			Age = 0f
		};
	}

	// Roce en este paso, en un punto del eje
	public void Graze(BarrierSide side, float distance)
	{
		ref SideState s = ref _sides[(int)side];
		if (s.Kind == Kind.Impact && PeakOf(s) > _settings.Graze.Intensity)
			return;

		s.Kind = Kind.Graze;
		s.Level = _settings.Graze;
		s.Center = Wrap(distance);
		s.Age = 0f;
		s.Touched = true;
	}

	public bool IsActive(BarrierSide side) => _sides[(int)side].Kind != Kind.None;

	// Punto de impacto (m sobre el eje), radio e intensidad en ese punto ahora mismo
	public float Center(BarrierSide side) => _sides[(int)side].Center;
	public float Radius(BarrierSide side) => _sides[(int)side].Level.Radius;
	public float Peak(BarrierSide side) => PeakOf(_sides[(int)side]);

	// Intensidad (0..1) del destello de un lado a una distancia dada del eje
	public float IntensityAt(BarrierSide side, float distance)
	{
		SideState s = _sides[(int)side];
		float peak = PeakOf(s);
		if (peak <= 0f || s.Level.Radius <= 0f)
			return 0f;

		float d = MathF.Abs(Wrap(distance) - s.Center);
		if (_lapLength > 0f)
			d = MathF.Min(d, _lapLength - d);
		return peak * MathF.Max(0f, 1f - d / s.Level.Radius);
	}

	private static float PeakOf(in SideState s)
	{
		if (s.Kind == Kind.None || s.Level.Duration <= 0f)
			return 0f;
		return s.Level.Intensity * MathF.Max(0f, 1f - s.Age / s.Level.Duration);
	}

	private float Wrap(float distance)
	{
		if (_lapLength <= 0f)
			return distance;
		float d = distance % _lapLength;
		return d < 0f ? d + _lapLength : d;
	}
}
