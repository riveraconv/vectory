using System;

namespace Vectory.Circuit;

// Parámetros de la parrilla de salida (en metros, salvo LateralFraction)
public struct GridSettings
{
	public float FirstDistance;    // distancia de la meta a la marca 1
	public float RowSpacing;       // separación entre filas de una misma columna
	public float EvenOffset;       // cuánto más atrás va cada marca par que la impar de delante
	public float LateralFraction;  // desplazamiento lateral de las columnas, en fracción del ancho de pista
}

// Marca de parrilla: dónde está respecto a la meta.
// Distance: metros hacia atrás desde la meta a lo largo de la línea central.
// Lateral: metros a la derecha de la línea central (negativo = izquierda).
public readonly struct GridSlot
{
	public readonly float Distance;
	public readonly float Lateral;

	public GridSlot(float distance, float lateral)
	{
		Distance = distance;
		Lateral = lateral;
	}

	public bool IsLeft => Lateral < 0f;
}

// Disposición de la parrilla: dos columnas alternadas detrás de la meta.
// Impares (1, 3, 5...) a la izquierda; pares (2, 4, 6...) a la derecha y un poco más atrás.
// Lógica pura (sin Godot).
public static class GridLayout
{
	// Marca de la posición 1..N
	public static GridSlot Slot(int position, float trackWidth, in GridSettings s)
	{
		if (position < 1)
			throw new ArgumentOutOfRangeException(nameof(position), "Las posiciones empiezan en 1.");

		int row = (position - 1) / 2;
		bool even = position % 2 == 0;

		float distance = s.FirstDistance + row * s.RowSpacing + (even ? s.EvenOffset : 0f);
		float lateral = trackWidth * s.LateralFraction * (even ? 1f : -1f);
		return new GridSlot(distance, lateral);
	}

	// Reparte las posiciones 1..count de forma aleatoria y sin repetir (Fisher-Yates).
	// El elemento i es la posición de parrilla del participante i.
	public static int[] RandomOrder(int count, Random rng)
	{
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count));
		ArgumentNullException.ThrowIfNull(rng);

		var order = new int[count];
		for (int i = 0; i < count; i++)
			order[i] = i + 1;

		for (int i = count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(order[i], order[j]) = (order[j], order[i]);
		}
		return order;
	}
}
