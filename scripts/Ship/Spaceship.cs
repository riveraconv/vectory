using Godot;
using System.Collections.Generic;
using Vectory.Core;

namespace Vectory.Ship;

// Nave wireframe de VECTORY generada 100% por código (solo el aspecto exterior;
// la cabina vive en Cockpit).
// Eje de avance: -Z (convención de Godot). Morro en z = -4, motores atrás en z > 0.
public partial class Spaceship : Node3D
{

	private readonly List<Vector3> _verts = new();
	private readonly List<Color> _colors = new();
	private readonly List<int> _indices = new();

	private MeshInstance3D _meshInstance;
	private EdgeFlickerMaterial _flickerMaterial;

	// Número de aristas (líneas) de la nave: cada una puede parpadear por su cuenta con los daños
	public int EdgeCount => _indices.Count / 2;

	public override void _Ready()
	{
		BuildGeometry();

		_meshInstance = new MeshInstance3D { Name = "MeshInstance3D", Mesh = BuildMesh() };
		AddChild(_meshInstance);
	}

	// La nave exterior solo se ve en tercera persona
	public void SetExteriorVisible(bool visible)
	{
		_meshInstance.Visible = visible;
	}

	// Sustituye el material de todas las líneas (la nave fantasma lo pinta de un solo color translúcido).
	// null vuelve a los colores normales. Solo después de _Ready (al estar ya en el árbol).
	public void SetMaterialOverride(Material material)
	{
		_meshInstance.MaterialOverride = material;
	}

	// Daños: las aristas apagadas del parpadeo bajan a dimOpacity. La primera llamada cambia el material
	// de la nave por el de parpadeo; sin llamarla (menú, fantasma) la nave conserva su material de siempre.
	public void ShowEdgeFlicker(EdgeFlicker flicker, float dimOpacity)
	{
		if (_flickerMaterial == null)
		{
			_flickerMaterial = new EdgeFlickerMaterial(EdgeCount);
			_meshInstance.SetSurfaceOverrideMaterial(0, _flickerMaterial.Material);
		}
		_flickerMaterial.SetDimOpacity(dimOpacity);
		_flickerMaterial.Apply(flicker);
	}

	// ---------------------------------------------------------------
	// Helpers de geometría
	// ---------------------------------------------------------------

	private int V(Vector3 p, Color c)
	{
		_verts.Add(p);
		_colors.Add(c);
		return _verts.Count - 1;
	}

	private void L(int a, int b)
	{
		_indices.Add(a);
		_indices.Add(b);
	}

	private int[] Ring(float cx, float cy, float z, float r, int seg, Color c)
	{
		var ring = new int[seg];
		for (int k = 0; k < seg; k++)
		{
			float a = Mathf.Tau * k / seg;
			ring[k] = V(new Vector3(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, z), c);
		}
		for (int k = 0; k < seg; k++)
			L(ring[k], ring[(k + 1) % seg]);
		return ring;
	}

	private void Connect(int[] a, int[] b)
	{
		for (int k = 0; k < a.Length; k++)
			L(a[k], b[k]);
	}

	// ---------------------------------------------------------------
	// Modelo
	// ---------------------------------------------------------------

	private void BuildGeometry()
	{
		BuildFuselage();
		foreach (float s in new[] { 1f, -1f })
		{
			BuildWing(s);
			BuildEngine(s);
		}
	}

	private void BuildFuselage()
	{
		float[] zs     = { -2.5f, -0.5f, 1.5f, 2.6f };
		float[] halfW  = {  0.5f,  0.9f, 0.8f, 0.7f };
		float[] top    = {  0.35f, 0.6f, 0.55f, 0.5f };
		float[] bottom = { -0.2f, -0.3f, -0.3f, -0.3f };
		float[] side   = {  0.05f, 0.1f, 0.1f, 0.1f };

		int nose = V(new Vector3(0f, 0f, -4f), Palette.Cyan);

		int[][] rings = new int[zs.Length][];
		for (int i = 0; i < zs.Length; i++)
		{
			rings[i] = new[]
			{
				V(new Vector3(0f, top[i], zs[i]), Palette.Cyan),
				V(new Vector3(halfW[i], side[i], zs[i]), Palette.Cyan),
				V(new Vector3(0f, bottom[i], zs[i]), Palette.Cyan),
				V(new Vector3(-halfW[i], side[i], zs[i]), Palette.Cyan)
			};

			for (int k = 0; k < 4; k++)
				L(rings[i][k], rings[i][(k + 1) % 4]);

			if (i == 0)
			{
				for (int k = 0; k < 4; k++)
					L(nose, rings[0][k]);
			}
			else
			{
				Connect(rings[i - 1], rings[i]);
			}
		}
	}

	private void BuildWing(float s)
	{
		int rootFront = V(new Vector3(s * 0.9f, 0.1f, -0.5f), Palette.Cyan);
		int rootRear  = V(new Vector3(s * 0.8f, 0.1f, 2.0f), Palette.Cyan);
		int tipFront  = V(new Vector3(s * 3.4f, -0.05f, 2.2f), Palette.Cyan);
		int tipRear   = V(new Vector3(s * 3.4f, -0.05f, 2.7f), Palette.Cyan);

		L(rootFront, tipFront);
		L(tipFront, tipRear);
		L(tipRear, rootRear);
		L(rootRear, rootFront);
		L(rootFront, tipRear);

		// Aleta vertical en la punta del ala
		int finTop = V(new Vector3(s * 3.4f, 0.7f, 2.7f), Palette.Cyan);
		L(tipFront, finTop);
		L(finTop, tipRear);
	}

	private void BuildEngine(float s)
	{
		float cx = s * 0.75f;
		float cy = 0.1f;
		const int seg = 6;

		int[] front  = Ring(cx, cy, 0.8f, 0.4f, seg, Palette.Cyan);
		int[] back   = Ring(cx, cy, 2.8f, 0.4f, seg, Palette.Cyan);
		Connect(front, back);

		// Tobera con brillo azul
		int[] nozzle = Ring(cx, cy, 3.2f, 0.28f, seg, Palette.Blue);
		Connect(back, nozzle);

		int core = V(new Vector3(cx, cy, 3.0f), Palette.Blue);
		for (int k = 0; k < seg; k++)
			L(core, nozzle[k]);
	}

	// Cada línea lleva sus propios dos vértices (sin compartirlos con otras) y en UV.x el número de su
	// arista, para que el material de los daños pueda apagar aristas sueltas. El aspecto no cambia.
	private ArrayMesh BuildMesh()
	{
		int count = _indices.Count;
		var verts = new Vector3[count];
		var colors = new Color[count];
		var uvs = new Vector2[count];
		for (int i = 0; i < count; i++)
		{
			verts[i] = _verts[_indices[i]];
			colors[i] = _colors[_indices[i]];
			uvs[i] = new Vector2(i / 2, 0f);
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts;
		arrays[(int)Mesh.ArrayType.Color] = colors;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true
		};
		mesh.SurfaceSetMaterial(0, mat);

		return mesh;
	}
}
