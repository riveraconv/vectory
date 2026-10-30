namespace Vectory.Ghost;

// Desvanecimiento del fantasma por cercanía (spec ghost 4.1), para que no tape la vista.
// Lógica pura: a fadeStart metros o más, opacidad normal; a fadeEnd o menos, invisible; en medio, lineal.
public static class GhostFade
{
	public static float Opacity(float distance, float baseOpacity, float fadeStart, float fadeEnd)
	{
		if (distance <= fadeEnd)
			return 0f;
		if (distance >= fadeStart)
			return baseOpacity;
		return baseOpacity * (distance - fadeEnd) / (fadeStart - fadeEnd);
	}
}
