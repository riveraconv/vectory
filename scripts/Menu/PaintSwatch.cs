using System;
using System.Collections.Generic;
using System.Numerics;
using Vectory.Ship;

namespace Vectory.Menu;

// Fondo de una muestra: degradado a 135° hacia negro, camuflaje de manchas fijas o partida en diagonal (bicolor)
public enum SwatchBackground { Gradient, Camo, Split }

// Una mancha del camuflaje de las muestras: centro y semiejes en fracción del ancho y del alto, y tono (1 claro, 2 oscuro)
public readonly record struct SwatchBlob(float X, float Y, float RadiusX, float RadiusY, int Tone);

// Lo que se dibuja en una muestra de la pantalla PAINT (spec docs/selector-pinturas.md, 4.3): un dibujo plano con los
// colores del catálogo, no un render de la nave. Colores en 0xRRGGBB, de la paleta propia de cada catálogo.
public sealed record PaintSwatch
{
	// Distintivo de BLADE y franjas de peligro de HAMMER (los de la maqueta)
	public const uint BadgeFill = 0xD7D2C0;
	public const uint BadgeBorder = 0x1D2A1A;
	public const uint HazardYellow = 0xE8B414;
	public const uint HazardBlack = 0x141412;

	// Manchas del camuflaje, iguales en todas las muestras, de abajo arriba (las de la maqueta)
	public static readonly IReadOnlyList<SwatchBlob> CamoBlobs = new SwatchBlob[]
	{
		new(0.50f, 0.48f, 0.16f, 0.22f, 2),
		new(0.22f, 0.82f, 0.20f, 0.18f, 1),
		new(0.78f, 0.22f, 0.18f, 0.14f, 2),
		new(0.72f, 0.66f, 0.26f, 0.30f, 2),
		new(0.28f, 0.30f, 0.30f, 0.22f, 1)
	};

	public required SwatchBackground Background { get; init; }

	// Color principal (o c0 del camuflaje, o el casco en las bicolor)
	public required uint Color { get; init; }

	// Camuflaje: tonos claro (c1) y oscuro (c2). Bicolor: la cabina en Light.
	public uint Light { get; init; }
	public uint Dark { get; init; }

	// Degradado: el color llega puro hasta Stop y negro en End (fracciones de la línea de 135°, End > 1).
	// Bicolor: el corte, en Stop.
	public float Stop { get; init; }
	public float End { get; init; }

	// VECTOR: barra del color de acento abajo
	public uint? AccentBar { get; init; }

	// VECTOR con librea: dos rayas verticales del color de las franjas y el dorsal arriba a la derecha
	public uint? Stripes { get; init; }
	public string Number { get; init; }
	public uint NumberColor { get; init; }

	// BLADE con marcas: círculo del distintivo arriba a la derecha
	public bool Badge { get; init; }

	// HAMMER con marcas: banda de franjas de peligro abajo
	public bool Hazard { get; init; }

	public static PaintSwatch For(HullStyle style, int index) => style switch
	{
		HullStyle.Military => ForMilitary(MilitaryPaintCatalog.Combinations[Wrap(index)]),
		HullStyle.Industrial => ForIndustrial(IndustrialPaintCatalog.Combinations[Wrap(index)]),
		_ => ForRacing(HullPaintCatalog.Combinations[Wrap(index)])
	};

	private static PaintSwatch ForRacing(HullPaintChoice choice) => new()
	{
		Background = SwatchBackground.Gradient,
		Color = choice.Paint.Color,
		Stop = 0.55f,
		End = 1.6f,
		AccentBar = choice.Paint.Accent,
		Stripes = choice.WithLivery ? choice.Paint.Livery.Stripe : null,
		Number = choice.WithLivery ? choice.Paint.Livery.Number : null,
		NumberColor = choice.Paint.Livery.NumberBackground
	};

	private static PaintSwatch ForMilitary(MilitaryPaintChoice choice) => new()
	{
		Background = choice.Paint.IsCamo ? SwatchBackground.Camo : SwatchBackground.Gradient,
		Color = choice.Paint.Base,
		Light = choice.Paint.Light,
		Dark = choice.Paint.Dark,
		Stop = 0.60f,
		End = 1.9f,
		Badge = choice.WithMarkings
	};

	private static PaintSwatch ForIndustrial(IndustrialPaintChoice choice) => new()
	{
		Background = choice.Paint.IsTwoTone ? SwatchBackground.Split : SwatchBackground.Gradient,
		Color = choice.Paint.Body,
		Light = choice.Paint.CabColor,
		Stop = choice.Paint.IsTwoTone ? 0.58f : 0.60f,
		End = 1.9f,
		Hazard = choice.WithMarkings
	};

	private static int Wrap(int index)
	{
		int n = PaintSelectLogic.GroupSize * 2;
		return (index % n + n) % n;
	}
}

// Color de cada punto de lo fijo de una muestra (fondo, rayas, distintivo y franjas de peligro), puro. El nodo lo
// rellena una vez en una textura; la barra de acento, el dorsal, el borde y las marcas de selección los dibuja aparte.
// Coordenadas en píxeles de 1280×720 desde la esquina de arriba a la izquierda de la muestra.
public static class PaintSwatchDrawing
{
	// Rayas de la librea: a un 38 % del ancho, 2 px cada una y 3 px entre ellas
	public const float StripesAt = 0.38f;
	public const float StripeWidth = 2f;
	public const float StripeGap = 3f;
	// Distintivo: 11 px, a 4 px de arriba y de la derecha, con borde de 2 px
	public const float BadgeSize = 11f;
	public const float BadgeInset = 4f;
	public const float BadgeBorderWidth = 2f;
	// Franjas de peligro: banda de 6 px abajo, franjas de 4 px a 45°
	public const float HazardHeight = 6f;
	public const float HazardStripe = 4f;

	// Color (0–1) en el punto (x, y) de una muestra de width × height
	public static Vector3 Sample(PaintSwatch swatch, float x, float y, float width, float height)
	{
		Vector3 color = Background(swatch, x, y, width, height);

		if (swatch.Stripes is uint stripes)
		{
			float left = StripesAt * width;
			if ((x >= left && x < left + StripeWidth) || (x >= left + StripeWidth + StripeGap && x < left + 2f * StripeWidth + StripeGap))
				color = Rgb(stripes);
		}

		if (swatch.Badge)
		{
			float r = BadgeSize * 0.5f;
			float cx = width - BadgeInset - r;
			float cy = BadgeInset + r;
			float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
			if (d <= r - BadgeBorderWidth)
				color = Rgb(PaintSwatch.BadgeFill);
			else if (d <= r)
				color = Rgb(PaintSwatch.BadgeBorder);
		}

		if (swatch.Hazard && y >= height - HazardHeight)
		{
			// repeating-linear-gradient(45deg): distancia a lo largo de (1, −1) / √2 desde el comienzo de la línea
			float top = height - HazardHeight;
			float length = (width + HazardHeight) * Sqrt2Half;
			float along = ((x - width * 0.5f) - (y - top - HazardHeight * 0.5f)) * Sqrt2Half + length * 0.5f;
			float phase = ((along % (2f * HazardStripe)) + 2f * HazardStripe) % (2f * HazardStripe);
			color = Rgb(phase < HazardStripe ? PaintSwatch.HazardYellow : PaintSwatch.HazardBlack);
		}

		return color;
	}

	private const float Sqrt2Half = 0.70710678f;

	// Posición (0–1) a lo largo de la línea del degradado de 135° (de arriba a la izquierda a abajo a la derecha)
	public static float Along135(float x, float y, float width, float height)
	{
		float length = (width + height) * Sqrt2Half;
		return ((x - width * 0.5f) + (y - height * 0.5f)) * Sqrt2Half / length + 0.5f;
	}

	private static Vector3 Background(PaintSwatch swatch, float x, float y, float width, float height)
	{
		switch (swatch.Background)
		{
			case SwatchBackground.Split:
				return Rgb(Along135(x, y, width, height) < swatch.Stop ? swatch.Color : swatch.Light);

			case SwatchBackground.Camo:
			{
				Vector3 color = Rgb(swatch.Color);
				foreach (SwatchBlob blob in PaintSwatch.CamoBlobs)
				{
					float dx = (x - blob.X * width) / (blob.RadiusX * width);
					float dy = (y - blob.Y * height) / (blob.RadiusY * height);
					float r = MathF.Sqrt(dx * dx + dy * dy);
					// Como la maqueta: el color hasta el 98 % del radio y se funde hasta el 100 %
					float a = r <= 0.98f ? 1f : r >= 1f ? 0f : (1f - r) / 0.02f;
					if (a > 0f)
						color = Vector3.Lerp(color, Rgb(blob.Tone == 1 ? swatch.Light : swatch.Dark), a);
				}
				return color;
			}

			default:
			{
				float t = Along135(x, y, width, height);
				Vector3 color = Rgb(swatch.Color);
				if (t <= swatch.Stop)
					return color;
				return Vector3.Lerp(color, Vector3.Zero, Math.Clamp((t - swatch.Stop) / (swatch.End - swatch.Stop), 0f, 1f));
			}
		}
	}

	public static Vector3 Rgb(uint color) =>
		new(((color >> 16) & 0xFF) / 255f, ((color >> 8) & 0xFF) / 255f, (color & 0xFF) / 255f);
}
