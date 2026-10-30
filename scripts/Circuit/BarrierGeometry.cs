using System;

namespace Vectory.Circuit;

// Geometría de las barreras (spec docs/barreras-y-colisiones.md, 2.1): dónde van las verticales
// a lo largo del eje y a qué alturas van las líneas horizontales.
// Lógica pura: no depende de Godot. Track la usa para construir la malla.
public static class BarrierGeometry
{
	// Desplazamiento lateral de la barrera respecto al eje: medio ancho de pista + separación al borde
	public static float Offset(float trackWidth, float gap)
	{
		return trackWidth * 0.5f + gap;
	}

	// Número de tramos entre verticales: el entero más cercano a vuelta / separación (al menos 1)
	public static int PostCount(float lapLength, float spacing)
	{
		if (lapLength <= 0f || spacing <= 0f)
			return 0;
		return Math.Max(1, (int)MathF.Round(lapLength / spacing));
	}

	// Separación ajustada para que quepa un número entero de tramos en la vuelta
	public static float AdjustedSpacing(float lapLength, float spacing)
	{
		int count = PostCount(lapLength, spacing);
		return count > 0 ? lapLength / count : 0f;
	}

	// Distancias (medidas sobre el eje desde la meta) de cada vertical. La primera está en 0;
	// la del cierre de la vuelta sería la misma que la primera, así que no se repite.
	public static float[] PostDistances(float lapLength, float spacing)
	{
		int count = PostCount(lapLength, spacing);
		var distances = new float[count];
		float step = count > 0 ? lapLength / count : 0f;
		for (int i = 0; i < count; i++)
			distances[i] = i * step;
		return distances;
	}

	// Alturas de las líneas horizontales (spec v1.1): base, borde interior de la franja inferior,
	// línea media, borde interior de la franja superior y remate.
	// height: altura total; bottomBand / topBand: alto de cada franja; middle: altura de la línea media.
	public static BarrierLineHeights LineHeights(float height, float bottomBand, float topBand, float middle)
	{
		return new BarrierLineHeights(0f, bottomBand, middle, height - topBand, height);
	}
}

// Alturas (m sobre la pista) de las líneas horizontales de la barrera
public readonly struct BarrierLineHeights
{
	public readonly float Base;           // borde exterior de la franja inferior (grueso)
	public readonly float BottomBandTop;  // borde interior de la franja inferior
	public readonly float Middle;         // línea media del relleno
	public readonly float TopBandBottom;  // borde interior de la franja superior
	public readonly float Top;            // remate: borde exterior de la franja superior (grueso)

	public BarrierLineHeights(float @base, float bottomBandTop, float middle, float topBandBottom, float top)
	{
		Base = @base;
		BottomBandTop = bottomBandTop;
		Middle = middle;
		TopBandBottom = topBandBottom;
		Top = top;
	}
}
