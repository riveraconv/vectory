using Godot;
using System.Collections.Generic;

namespace Vectory.Menu;

// Parámetros de la rejilla de horizonte (suelo de líneas estilo años 80)
public struct HorizonGridSettings
{
	public float FloorY;          // altura del suelo
	public float HorizonZ;        // Z del borde lejano (la línea del horizonte)
	public float NearZ;           // Z del borde cercano (detrás de la cámara, para que no se vea el corte)
	public float NearHalfWidth;   // mitad del ancho de la rejilla en el borde cercano
	public int RadialLines;       // líneas longitudinales (convergen en el horizonte)
	public float RowSpacing;      // separación entre líneas transversales, en metros
	public float FadeLength;      // metros desde el horizonte hasta que la rejilla se ve entera
	public float Opacity;         // opacidad máxima de la rejilla (cerca)
}

// Genera la rejilla de horizonte como UN solo mesh de líneas, con color por vértice:
// - Las longitudinales salen en abanico desde un punto del horizonte hacia la cámara.
// - Las transversales van a distancias iguales en el mundo, así la perspectiva las junta al fondo.
// - Se desvanece hacia el horizonte (alfa 0 allí, Opacity a FadeLength metros).
// Reutilizable: no depende del menú.
public static class HorizonGrid
{
	public static ArrayMesh BuildMesh(HorizonGridSettings s, Color color)
	{
		var verts = new List<Vector3>();
		var colors = new List<Color>();
		float depth = s.HorizonZ - s.NearZ;   // la cámara mira hacia +Z: el horizonte queda más lejos que NearZ

		Color ColorAt(float z)
		{
			float fromHorizon = Mathf.Abs(s.HorizonZ - z);
			float t = s.FadeLength > 0f ? Mathf.Clamp(fromHorizon / s.FadeLength, 0f, 1f) : 1f;
			return new Color(color, s.Opacity * t);
		}

		void Line(Vector3 a, Vector3 b)
		{
			verts.Add(a);
			colors.Add(ColorAt(a.Z));
			verts.Add(b);
			colors.Add(ColorAt(b.Z));
		}

		// Longitudinales: del punto de fuga en el horizonte (x = 0) hacia el borde cercano.
		// Se parten en tramos para que el desvanecimiento siga la distancia y no solo los extremos.
		var vanish = new Vector3(0f, s.FloorY, s.HorizonZ);
		int lines = Mathf.Max(2, s.RadialLines);
		const int Segments = 8;
		for (int i = 0; i < lines; i++)
		{
			float x = Mathf.Lerp(-s.NearHalfWidth, s.NearHalfWidth, i / (float)(lines - 1));
			var near = new Vector3(x, s.FloorY, s.NearZ);
			for (int k = 0; k < Segments; k++)
				Line(vanish.Lerp(near, k / (float)Segments), vanish.Lerp(near, (k + 1) / (float)Segments));
		}

		// Transversales: cada RowSpacing metros desde el horizonte hacia la cámara.
		// Su ancho sigue al abanico de las longitudinales en esa profundidad.
		if (s.RowSpacing > 0f && Mathf.Abs(depth) > 0f)
		{
			for (float d = s.RowSpacing; d <= Mathf.Abs(depth); d += s.RowSpacing)
			{
				float z = s.HorizonZ - Mathf.Sign(depth) * d;
				float halfWidth = s.NearHalfWidth * d / Mathf.Abs(depth);
				Line(new Vector3(-halfWidth, s.FloorY, z), new Vector3(halfWidth, s.FloorY, z));
			}
		}

		return BuildLineMesh(verts, colors);
	}

	// Línea del horizonte: una línea recta a lo ancho, con la opacidad repartida a lo largo
	// (stops = pares posición 0..1 → opacidad). Se parte en tramos para seguir el degradado.
	public static ArrayMesh BuildHorizonLine(Vector3 from, Vector3 to, Color color, (float pos, float alpha)[] stops)
	{
		var verts = new List<Vector3>();
		var colors = new List<Color>();

		for (int i = 0; i < stops.Length - 1; i++)
		{
			verts.Add(from.Lerp(to, stops[i].pos));
			colors.Add(new Color(color, stops[i].alpha));
			verts.Add(from.Lerp(to, stops[i + 1].pos));
			colors.Add(new Color(color, stops[i + 1].alpha));
		}

		return BuildLineMesh(verts, colors);
	}

	private static ArrayMesh BuildLineMesh(List<Vector3> verts, List<Color> colors)
	{
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		});
		return mesh;
	}
}
