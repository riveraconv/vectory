using System;
using System.Numerics;

namespace Vectory.Menu;

// Parámetros del giro del holograma de SELECT SHIP (ángulos en radianes). Los valores se editan en el
// Inspector de ShipSelectScreen.
public struct HologramSpinSettings
{
	public float InitialYaw;          // giro inicial sobre el eje vertical
	public float InitialTilt;         // inclinación inicial, para que se vea por arriba
	public float AutoPeriod;          // segundos por vuelta del giro automático
	public float DragSensitivity;     // radianes por píxel de arrastre
	public float InertiaDecay;        // constante de tiempo de la inercia al soltar, en segundos
	public float AutoResumeDelay;     // segundos tras soltar hasta que vuelve el giro automático
	public float AutoResumeRamp;      // segundos de su rampa de 0 a su velocidad
	public float StillRelease;        // segundos con el ratón quieto a partir de los que soltar no deja inercia
	public float VelocitySmoothing;   // constante de tiempo del cálculo de la velocidad de arrastre, en segundos

	// Valores de la spec
	public static HologramSpinSettings Default => new()
	{
		InitialYaw = 143f * MathF.PI / 180f,
		InitialTilt = 22f * MathF.PI / 180f,
		AutoPeriod = 20f,
		DragSensitivity = 0.01f,
		InertiaDecay = 0.4f,
		AutoResumeDelay = 3f,
		AutoResumeRamp = 1f,
		StillRelease = 0.08f,
		VelocitySmoothing = 0.05f
	};
}

// Orientación del holograma de SELECT SHIP (lógica pura): giro automático, arrastre libre en esfera, inercia y
// vuelta suave al giro automático. Todos los giros son respecto a la pantalla: se aplican por la izquierda sobre
// la orientación, con X a la derecha, Y hacia arriba y Z hacia quien mira (la cámara del holograma no gira).
public sealed class HologramSpin
{
	private const float MinVelocity = 1e-3f;

	private readonly HologramSpinSettings _settings;
	private Vector2 _pendingDrag;    // radianes arrastrados desde el último Step
	private float _sinceMove;        // segundos sin mover el ratón mientras se arrastra
	private float _idle;             // segundos desde que se soltó

	public HologramSpin(HologramSpinSettings settings)
	{
		_settings = settings;
		Orientation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitX, settings.InitialTilt)
			* Quaternion.CreateFromAxisAngle(Vector3.UnitY, settings.InitialYaw));
		_idle = settings.AutoResumeDelay + settings.AutoResumeRamp;   // de entrada gira a velocidad completa
	}

	public Quaternion Orientation { get; private set; }

	public bool IsDragging { get; private set; }

	// Velocidad de la inercia (o de arrastre, mientras se arrastra) en rad/s: X sobre el eje vertical de la
	// pantalla (arrastre horizontal) e Y sobre el horizontal (arrastre vertical)
	public Vector2 Velocity { get; private set; }

	// Fracción de la velocidad del giro automático que hay ahora (0–1)
	public float AutoFactor
	{
		get
		{
			if (IsDragging)
				return 0f;
			if (_settings.AutoResumeRamp <= 0f)
				return _idle >= _settings.AutoResumeDelay ? 1f : 0f;
			return Math.Clamp((_idle - _settings.AutoResumeDelay) / _settings.AutoResumeRamp, 0f, 1f);
		}
	}

	public void BeginDrag()
	{
		IsDragging = true;
		Velocity = Vector2.Zero;
		_pendingDrag = Vector2.Zero;
		_sinceMove = 0f;
	}

	// Movimiento del ratón en píxeles, con Y hacia abajo. Sin BeginDrag no hace nada.
	public void Drag(float dx, float dy)
	{
		if (!IsDragging)
			return;

		Vector2 angle = new Vector2(dx, dy) * _settings.DragSensitivity;
		Rotate(angle);
		_pendingDrag += angle;
	}

	// Conserva la velocidad que llevaba, salvo que el ratón llevara quieto un rato
	public void Release()
	{
		if (!IsDragging)
			return;

		IsDragging = false;
		_idle = 0f;
		if (_sinceMove > _settings.StillRelease)
			Velocity = Vector2.Zero;
	}

	public void Step(float delta)
	{
		if (delta <= 0f)
			return;

		if (IsDragging)
		{
			TrackDragVelocity(delta);
			return;
		}

		StepInertia(delta);
		StepAuto(delta);
	}

	private void TrackDragVelocity(float delta)
	{
		if (_pendingDrag == Vector2.Zero)
		{
			_sinceMove += delta;
			return;
		}

		float blend = _settings.VelocitySmoothing > 0f ? 1f - MathF.Exp(-delta / _settings.VelocitySmoothing) : 1f;
		Velocity = Vector2.Lerp(Velocity, _pendingDrag / delta, blend);
		_pendingDrag = Vector2.Zero;
		_sinceMove = 0f;
	}

	// Decaimiento exponencial, integrado exacto (no depende del tamaño del paso)
	private void StepInertia(float delta)
	{
		if (Velocity == Vector2.Zero)
			return;

		float tau = _settings.InertiaDecay;
		if (tau <= 0f)
		{
			Velocity = Vector2.Zero;
			return;
		}

		float decay = MathF.Exp(-delta / tau);
		Rotate(Velocity * tau * (1f - decay));
		Velocity *= decay;
		if (Velocity.Length() < MinVelocity)
			Velocity = Vector2.Zero;
	}

	// Integral exacta de la rampa, así un paso largo da lo mismo que muchos cortos
	private void StepAuto(float delta)
	{
		float before = AutoTime(_idle);
		_idle += delta;
		if (_settings.AutoPeriod > 0f)
			Rotate(new Vector2(MathF.Tau / _settings.AutoPeriod * (AutoTime(_idle) - before), 0f));
	}

	// Segundos equivalentes a velocidad completa acumulados a los t segundos de soltar
	private float AutoTime(float t)
	{
		float active = t - _settings.AutoResumeDelay;
		if (active <= 0f)
			return 0f;

		float ramp = _settings.AutoResumeRamp;
		return active < ramp ? active * active / (2f * ramp) : active - ramp * 0.5f;
	}

	private void Rotate(Vector2 angle)
	{
		Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle.Y)
			* Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle.X);
		Orientation = Quaternion.Normalize(rotation * Orientation);
	}
}
