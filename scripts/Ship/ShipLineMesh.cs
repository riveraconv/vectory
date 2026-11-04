using Godot;
using System.Collections.Generic;
using System.Linq;
using Vectory.Core;

namespace Vectory.Ship;

// Pasa las líneas de una nave (ShipVisualProfile) a una malla de Godot. Cada línea lleva sus propios dos vértices
// y en UV.x el número de su arista, para que el material de los daños pueda apagar aristas sueltas.
public static class ShipLineMesh
{
	public static ArrayMesh Build(IReadOnlyList<ShipLine> lines, out int edgeCount)
	{
		var verts = new Vector3[lines.Count * 2];
		var colors = new Color[lines.Count * 2];
		var uvs = new Vector2[lines.Count * 2];
		for (int i = 0; i < lines.Count; i++)
		{
			ShipLine line = lines[i];
			Color color = line.Color == ShipLineColor.Blue ? Palette.Blue : Palette.Cyan;
			verts[i * 2] = ToGodot(line.A);
			verts[i * 2 + 1] = ToGodot(line.B);
			colors[i * 2] = color;
			colors[i * 2 + 1] = color;
			uvs[i * 2] = new Vector2(line.Edge, 0f);
			uvs[i * 2 + 1] = new Vector2(line.Edge, 0f);
		}
		edgeCount = lines.Count == 0 ? 0 : lines.Max(l => l.Edge) + 1;

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts;
		arrays[(int)Mesh.ArrayType.Color] = colors;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true
		});
		return mesh;
	}

	public static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
