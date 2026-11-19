namespace Vectory.Ship;

// Reglas de la copia del morro del casco en la cabina (spec docs/cabinas-3d-4-morros.md, 4.2). Lógica pura.
public static class HullNoseCopyRules
{
	public const string Prefix = "Nose_";

	// Una pieza de la copia comparte el material de su pieza del casco si es viva (luz que se enciende y titila) o una
	// calcomanía (su dibujo llega después), y no lleva pasadas que dependan de los ejes del casco en su mundo (el daño).
	// Las demás llevan una copia, con la pasada del daño de la copia.
	public static bool SharesMaterial(bool emissive, bool transparent, bool hasPass) => !hasPass && (emissive || transparent);

	// Nombre en la cabina de una pieza del casco
	public static string CopyName(string hullPiece) => Prefix + hullPiece;
}
