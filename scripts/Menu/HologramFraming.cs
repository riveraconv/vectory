using System.Collections.Generic;
using System.Numerics;
using Vectory.Ship;

namespace Vectory.Menu;

// Caja de un casco. Como el holograma gira en cualquier dirección, lo que ocupa es la esfera que la envuelve.
public readonly record struct HologramBounds(Vector3 Center, Vector3 Size)
{
	public float SphereDiameter => Size.Length();

	public float SphereRadius => SphereDiameter * 0.5f;
}

// Medidas del holograma de SELECT SHIP (spec docs/naves-4-integracion.md, 3.2), sin Godot. La escala (px por metro) es
// común a todas las naves y sale del ancho en pantalla de la de referencia (Vector); la cámara no se mueve al cambiar
// de nave, y cada una se centra por el centro de su caja. Si una nave futura tuviera una esfera mayor que la de
// referencia se saldría más al girar: lo avisa una prueba, no se corrige aquí.
public static class HologramFraming
{
	public static HologramBounds BoundsOf(IReadOnlyList<ShipLine> hull)
	{
		if (hull.Count == 0)
			return new HologramBounds(Vector3.Zero, Vector3.Zero);

		Vector3 min = hull[0].A;
		Vector3 max = hull[0].A;
		foreach (ShipLine line in hull)
		{
			min = Vector3.Min(Vector3.Min(min, line.A), line.B);
			max = Vector3.Max(Vector3.Max(max, line.A), line.B);
		}
		return new HologramBounds((min + max) * 0.5f, max - min);
	}

	// Px por metro que dejan a la nave de referencia con ese ancho en pantalla (de punta a punta de las alas)
	public static float PixelsPerMeter(HologramBounds reference, float screenWidth) => screenWidth / reference.Size.X;

	// Distancia de la cámara para esa escala. focal: píxeles por unidad de tangente (a la distancia d, un metro mide focal / d)
	public static float CameraDistance(float focal, float pixelsPerMeter) => focal / pixelsPerMeter;
}
