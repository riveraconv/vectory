using Godot;

namespace Vectory.Circuit;

// Datos de un circuito concreto: lo que cambia de un circuito a otro.
// Cada circuito es un archivo .tres con este recurso; Track.cs lo lee y genera la pista.
//
// Reglas para un circuito nuevo:
// - Los puntos de control se miden sobre la imagen del circuito, en píxeles,
//   en sentido horario visto desde arriba.
// - El PRIMER punto es la línea de salida/meta.
// - Detrás de la meta (los últimos puntos) tiene que haber recta suficiente
//   para la parrilla (unos 70 m con los valores por defecto).
[GlobalClass]
public partial class TrackDefinition : Resource
{
	[Export] public string DisplayName = "Circuito";

	// Puntos de control en píxeles de la imagen, en sentido horario visto desde arriba,
	// empezando en la línea de salida/meta.
	[Export] public Vector2[] ControlPoints = System.Array.Empty<Vector2>();

	// Conversión de píxeles de la imagen a metros
	[Export] public float MetersPerPixel = 1.4f;

	// Píxel de la imagen que corresponde al origen del mundo
	[Export] public Vector2 ImageCenter = Vector2.Zero;

	// Sentido de carrera (false = el mismo circuito al revés, manteniendo la salida)
	[Export] public bool Clockwise = true;

	// Ancho de la pista en metros
	[Export] public float TrackWidth = 28f;

	// Número de checkpoints repartidos por el circuito (sin contar la meta)
	[Export] public int CheckpointCount = 5;

	// Comprueba que los datos permiten generar un circuito
	public bool IsValid(out string error)
	{
		if (ControlPoints == null || ControlPoints.Length < 4)
		{
			error = "hacen falta al menos 4 puntos de control";
			return false;
		}
		if (MetersPerPixel <= 0f)
		{
			error = "MetersPerPixel debe ser mayor que 0";
			return false;
		}
		if (TrackWidth <= 0f)
		{
			error = "TrackWidth debe ser mayor que 0";
			return false;
		}
		if (CheckpointCount < 0)
		{
			error = "CheckpointCount no puede ser negativo";
			return false;
		}

		error = null;
		return true;
	}
}
