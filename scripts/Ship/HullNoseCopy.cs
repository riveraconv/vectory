using System.Collections.Generic;
using Godot;

namespace Vectory.Ship;

// Copia del morro del casco 3D dentro de la cabina (spec docs/cabinas-3d-4-morros.md, 4.2). La crea la cabina 3D con las
// piezas de su modelo y la pinta el mismo ShipHull3D que pinta el casco: misma pintura, mismas calcomanías, mismo daño
// y mismas luces vivas. Va en el mundo de la cabina, así que su pasada del daño necesita sus propios ejes (WorldToHull).
// Se ilumina como el casco en persecución: su juego de luces en la capa de la copia y el entorno de la carrera en la
// cámara de su pasada (la maqueta la ilumina igual).
public sealed class HullNoseCopy
{
	// Casco del que sale ("hull_vector"): el nombre de su .glb
	public string Hull { get; init; } = "";

	// Cada pieza de la copia con el nombre que tiene en el .glb del casco
	public IReadOnlyList<(MeshInstance3D Mesh, string HullPiece)> Pieces { get; init; } = new List<(MeshInstance3D, string)>();

	// Del mundo de la cabina a los ejes del casco: un punto de la copia está en el casco en él + el ojo
	public Transform3D WorldToHull { get; init; } = Transform3D.Identity;

	// El ojo en los ejes del casco: un punto p del casco está en la copia en p − Eye
	public Vector3 Eye { get; init; }

	// Intensidad de los reflejos de sus materiales respecto a los del casco (spec 4.3)
	public float Reflections { get; init; } = 1f;

	// Capa visual de la copia, dónde van sus luces (en los ejes de la nave) y la cámara de su pasada
	public uint Layer { get; init; }
	public Node3D LightRoot { get; init; }
	public Camera3D Camera { get; init; }

	public bool Matches(ShipHull3DProfile profile) => profile != null && profile.ModelPath.GetFile().GetBaseName() == Hull;
}
