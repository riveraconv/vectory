using System;

namespace Vectory.Ship;

// Una marca del cristal de la cabina: sitio en el cristal (0–1, 0 arriba a la izquierda), tamaño (en altos del cristal;
// 0 = apagada: radio de una picada o una mancha, largo de una grieta) y semilla de su forma
public readonly record struct GlassMark(float X, float Y, float Size, float Seed);

// Ajustes del cristal dañado (spec docs/efectos-5-danos-casco.md, 6). Lógica pura: datos.
public sealed class CockpitGlassSettings
{
	// Zona del cristal donde caen las picadas (y donde empiezan las grietas): casi todo el cristal, y más hacia los bordes
	// que hacia el centro (Spread), para no tapar lo que se mira
	public float MinX = 0.04f, MaxX = 0.96f;
	public float MinY = 0.04f, MaxY = 0.80f;
	public float SpreadX = 1f, SpreadY = 0.5f;   // 0 = reparto uniforme, 1 = empujado del todo hacia los bordes
	// Picadas por nivel (1, 2, 3) y su radio, entre estos dos
	public int[] Chips = { 3, 6, 10 };
	public float ChipMin = 0.006f, ChipMax = 0.014f;
	// Grietas largas por nivel, con su largo: salen de las primeras picadas y crecen con el daño por el mismo camino
	public float[] Crack1 = { 0.22f };
	public float[] Crack2 = { 0.42f, 0.22f };
	public float[] Crack3 = { 0.65f, 0.42f, 0.30f };
	// Manchas por nivel
	public float[] Stain1 = Array.Empty<float>();
	public float[] Stain2 = { 0.10f };
	public float[] Stain3 = { 0.14f, 0.10f, 0.08f };
	public float CrackOpacity = 0.45f;
	public float StainOpacity = 0.3f;

	public static CockpitGlassSettings Default => new();

	// Por fuera (casco 3D): más opacas, porque el cristal curvo y brillante refleja el entorno y se las comía
	public static CockpitGlassSettings Hull => new() { CrackOpacity = 0.85f, StainOpacity = 0.35f };
}

// Picadas, grietas largas y manchas del cristal de la cabina según el nivel de daño (spec docs/efectos-5-danos-casco.md,
// 6): en sitios sorteados una vez por carrera con su semilla; al subir de nivel las que había siguen en su sitio, las
// grietas se alargan por el mismo camino y salen más. Lógica pura.
public static class CockpitGlassDamage
{
	public const int MaxChips = 10;
	public const int MaxCracks = 3;
	public const int MaxStains = 3;
	private const int MaxLevel = 3;

	public static int ChipCount(int level, CockpitGlassSettings s) =>
		level <= 0 ? 0 : Math.Min(MaxChips, s.Chips[Math.Min(level, s.Chips.Length) - 1]);

	public static GlassMark Chip(int level, int index, int seed, CockpitGlassSettings s)
	{
		if (index < 0 || index >= ChipCount(level, s))
			return default;
		uint h = Hash((uint)seed, (uint)index);
		return new GlassMark(Lerp(s.MinX, s.MaxX, Spread(Unit(h), s.SpreadX)), Lerp(s.MinY, s.MaxY, Spread(Unit(h >> 16), s.SpreadY)),
			Lerp(s.ChipMin, s.ChipMax, Unit(Hash(h, 3u))), SeedOf(h));
	}

	// Una grieta sale de la picada con su mismo índice
	public static GlassMark Crack(int level, int index, int seed, CockpitGlassSettings s)
	{
		float[] sizes = SizesOf(level, s.Crack1, s.Crack2, s.Crack3);
		if (index < 0 || index >= sizes.Length || index >= MaxCracks)
			return default;
		GlassMark chip = Chip(MaxLevel, index, seed, s);
		return new GlassMark(chip.X, chip.Y, sizes[index], SeedOf(Hash((uint)seed, (uint)(64 + index))));
	}

	public static GlassMark Stain(int level, int index, int seed, CockpitGlassSettings s)
	{
		float[] sizes = SizesOf(level, s.Stain1, s.Stain2, s.Stain3);
		if (index < 0 || index >= sizes.Length || index >= MaxStains)
			return default;
		uint h = Hash((uint)seed, (uint)(128 + index));
		return new GlassMark(Lerp(s.MinX, s.MaxX, Spread(Unit(h), s.SpreadX)), Lerp(s.MinY, s.MaxY, Spread(Unit(h >> 16), s.SpreadY)),
			sizes[index], SeedOf(h));
	}

	private static float[] SizesOf(int level, float[] one, float[] two, float[] three) => level switch
	{
		1 => one,
		2 => two,
		>= 3 => three,
		_ => Array.Empty<float>()
	};

	private static float Unit(uint h) => (h & 0xFFFF) / 65535f;

	// Aleja del centro un valor 0–1: con amount 1, la raíz de la distancia al centro (más cerca de los bordes)
	public static float Spread(float u, float amount)
	{
		float t = u * 2f - 1f;
		float pushed = MathF.Sign(t) * MathF.Sqrt(MathF.Abs(t));
		return 0.5f + 0.5f * (t + (pushed - t) * Math.Clamp(amount, 0f, 1f));
	}
	private static float Lerp(float a, float b, float t) => a + (b - a) * t;
	private static float SeedOf(uint h) => (Hash(h, 7u) & 0xFFF) / 64f;

	private static uint Hash(uint a, uint b)
	{
		unchecked
		{
			uint h = a * 2654435761u ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2));
			h ^= h >> 15;
			h *= 2246822519u;
			h ^= h >> 13;
			h *= 3266489917u;
			h ^= h >> 16;
			return h;
		}
	}
}
