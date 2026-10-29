using Godot;

namespace Vectory.UI;

// Escala de las pantallas de menú. Sus medidas están sobre una pantalla de referencia de 1280×720;
// se multiplican por la altura base del proyecto / 720 (1080 → ×1,5) y el stretch del proyecto hace el resto.
public static class UiScale
{
	public const float ReferenceHeight = 720f;

	public static float Factor
	{
		get
		{
			int baseHeight = ProjectSettings.GetSetting("display/window/size/viewport_height", 720).AsInt32();
			return baseHeight / ReferenceHeight;
		}
	}
}
