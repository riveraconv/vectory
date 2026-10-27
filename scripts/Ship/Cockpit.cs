using Godot;
using System.Collections.Generic;
using Vectory.Core;

namespace Vectory.Ship;

// Cabina de la nave en primera persona: la cámara y el marco de líneas pegado a ella.
// Es independiente de la nave exterior (Spaceship): Player decide cuál de las dos se ve.
public partial class Cockpit : Node3D
{
	private MeshInstance3D _frame;

	public Camera3D Camera { get; private set; }

	public override void _Ready()
	{
		// Cámara de cabina (primera persona). La de tercera persona la gestiona Player.
		Camera = new Camera3D
		{
			Name = "Camera3D",
			Position = new Vector3(0f, 0.45f, -1.2f),
			Fov = 80f
		};
		AddChild(Camera);

		// Marco minimalista de la cabina, pegado a la cámara
		_frame = new MeshInstance3D { Name = "CockpitFrame", Mesh = BuildFrameMesh() };
		Camera.AddChild(_frame);

		SetFrameVisible(false);
	}

	// El marco solo se ve en primera persona
	public void SetFrameVisible(bool visible)
	{
		_frame.Visible = visible;
	}

	// Marco en forma de capó/salpicadero central con dos alas hacia los lados.
	// Se dibuja en un plano a 1 m delante de la cámara; con FOV 80 y pantalla 16:9 ese plano
	// cubre x de -1,49 a 1,49 e y de -0,84 a 0,84. El lado izquierdo es el espejo del derecho.
	private static ArrayMesh BuildFrameMesh()
	{
		const float z = -1f;
		const float Edge = 0.012f;   // separación del segundo trazo, para dar grosor a las líneas principales
		var verts = new List<Vector3>();
		var colors = new List<Color>();

		void Seg(float x1, float y1, float x2, float y2, Color c)
		{
			verts.Add(new Vector3(x1, y1, z));
			verts.Add(new Vector3(x2, y2, z));
			colors.Add(c);
			colors.Add(c);
		}

		// Línea gruesa: la línea y una copia desplazada un poco hacia dentro
		void Thick(float x1, float y1, float x2, float y2, Color c)
		{
			Seg(x1, y1, x2, y2, c);
			if (Mathf.Abs(x2 - x1) >= Mathf.Abs(y2 - y1))
			{
				Seg(x1, y1 - Edge, x2, y2 - Edge, c);
			}
			else
			{
				float dx = (x1 + x2) > 0f ? -Edge : Edge;
				Seg(x1 + dx, y1, x2 + dx, y2, c);
			}
		}

		// Dibuja la línea y su espejo respecto al eje vertical
		void Mirror(float x1, float y1, float x2, float y2, Color c, bool thick)
		{
			if (thick)
			{
				Thick(x1, y1, x2, y2, c);
				Thick(-x1, y1, -x2, y2, c);
			}
			else
			{
				Seg(x1, y1, x2, y2, c);
				Seg(-x1, y1, -x2, y2, c);
			}
		}

		// Capó exterior (cian, grueso): borde superior plano, chaflanes y laterales
		Thick(-0.26f, -0.28f, 0.26f, -0.28f, Palette.Cyan);
		Mirror(0.26f, -0.28f, 0.56f, -0.56f, Palette.Cyan, true);
		Mirror(0.56f, -0.56f, 0.50f, -0.90f, Palette.Cyan, true);

		// Contorno interior (azul): enmarca la zona donde queda la velocidad digital
		Seg(-0.20f, -0.44f, 0.20f, -0.44f, Palette.Blue);
		Mirror(0.20f, -0.44f, 0.42f, -0.62f, Palette.Blue, false);
		Mirror(0.42f, -0.62f, 0.38f, -0.90f, Palette.Blue, false);

		// Alas laterales hacia los bordes de la pantalla
		Mirror(0.26f, -0.28f, 1.49f, -0.62f, Palette.Cyan, false);
		Mirror(0.56f, -0.56f, 1.49f, -0.80f, Palette.Blue, false);

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true
		});
		return mesh;
	}
}
