using Godot;
using Vectory.Core;
using Vectory.Game;
using Vectory.Ship;

namespace Vectory.Menu;

// Fondo del menú principal: escena 3D real con la nave del juego vista de frente,
// la rejilla de horizonte, la línea del horizonte y la sombra izquierda para que el menú se lea.
// Todo estático. La nave es la misma clase Spaceship de la carrera, sin copiar su geometría.
//
// La nave está en el origen con el morro hacia -Z; la cámara está delante (Z negativa)
// mirando hacia +Z, así que la izquierda de la pantalla es +X del mundo.
public partial class MenuBackground : Node3D
{
	[ExportGroup("Cámara")]
	[Export] public Vector3 CameraPosition = new(0f, 2.2f, -12f);
	[Export] public Vector3 CameraTarget = new(0f, 0.1f, 0f);
	[Export] public float CameraFov = 45f;          // vertical, en grados
	// Dónde queda el centro de la nave en horizontal (0 = borde izquierdo, 1 = derecho; 940 de 1280 ≈ 0,734).
	// Se consigue desplazando el encuadre (como un objetivo descentrable), sin mover ni girar la cámara:
	// la nave se sigue viendo de frente y el horizonte queda recto.
	[Export(PropertyHint.Range, "0,1,0.001")] public float ShipScreenX = 0.734f;

	[ExportGroup("Rejilla")]
	[Export] public float FloorY = 0f;              // suelo: a la altura de la punta del morro
	[Export] public float HorizonZ = -4f;           // borde lejano de la rejilla: a la altura del morro, así la nave se apoya en el horizonte
	[Export] public float GridNearZ = -14f;         // borde cercano (detrás de la cámara)
	[Export] public float GridNearHalfWidth = 60f;
	[Export] public int GridRadialLines = 13;
	[Export] public float GridRowSpacing = 1f;
	[Export] public float GridFadeLength = 6f;
	[Export] public float GridOpacity = 0.7f;
	[Export] public float HorizonHalfLength = 40f;  // mitad del largo de la línea del horizonte

	[ExportGroup("Sombra izquierda")]
	[Export] public float ShadeWidth = 0.594f;      // fracción del ancho de pantalla (760 de 1280)

	private Camera3D _camera;

	public override void _Ready()
	{
		AddChild(WorldStyle.CreateWorldEnvironment());

		_camera = new Camera3D { Name = "Camera3D" };
		AddChild(_camera);
		_camera.LookAtFromPosition(CameraPosition, CameraTarget, Vector3.Up);
		_camera.MakeCurrent();
		ApplyLensShift();
		GetViewport().SizeChanged += ApplyLensShift;

		AddChild(new Spaceship { Name = "Spaceship" });

		var grid = new HorizonGridSettings
		{
			FloorY = FloorY,
			HorizonZ = HorizonZ,
			NearZ = GridNearZ,
			NearHalfWidth = GridNearHalfWidth,
			RadialLines = GridRadialLines,
			RowSpacing = GridRowSpacing,
			FadeLength = GridFadeLength,
			Opacity = GridOpacity
		};
		AddChild(new MeshInstance3D { Name = "HorizonGrid", Mesh = HorizonGrid.BuildMesh(grid, Palette.MenuGrid) });

		// Línea del horizonte: tenue a la izquierda de la pantalla (+X), más visible a la derecha.
		var horizon = HorizonGrid.BuildHorizonLine(
			new Vector3(HorizonHalfLength, FloorY, HorizonZ),
			new Vector3(-HorizonHalfLength, FloorY, HorizonZ),
			Palette.MenuHorizon,
			new[] { (0f, 0f), (0.55f, 0.7f), (1f, 0.25f) });
		AddChild(new MeshInstance3D { Name = "HorizonLine", Mesh = horizon });

		AddChild(BuildLeftShade());
	}

	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= ApplyLensShift;
	}

	// Proyección frustum con el FOV vertical de CameraFov y el encuadre desplazado en horizontal
	// para que el eje de la cámara (donde está la nave) caiga en ShipScreenX. Depende del aspecto
	// de la ventana, por eso se recalcula al cambiar su tamaño.
	private void ApplyLensShift()
	{
		Vector2 size = GetViewport().GetVisibleRect().Size;
		float aspect = size.Y > 0f ? size.X / size.Y : 16f / 9f;

		float near = _camera.Near;
		float height = 2f * near * Mathf.Tan(Mathf.DegToRad(CameraFov) * 0.5f);
		float halfWidth = height * 0.5f * aspect;

		// Desplazar el frustum hacia la izquierda lleva lo que hay en el eje hacia la derecha de la pantalla
		float shiftX = -(ShipScreenX * 2f - 1f) * halfWidth;
		_camera.SetFrustum(height, new Vector2(shiftX, 0f), near, _camera.Far);
	}

	// Degradado del color de fondo, casi opaco a la izquierda y transparente hacia el centro.
	// Va en su propia capa 2D: por encima del 3D y por debajo del texto del menú.
	private CanvasLayer BuildLeftShade()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(Palette.MenuShade, 0.96f));
		gradient.SetColor(1, new Color(Palette.MenuShade, 0f));
		gradient.AddPoint(0.45f, new Color(Palette.MenuShade, 0.85f));

		var shade = new TextureRect
		{
			Name = "LeftShade",
			Texture = new GradientTexture2D { Gradient = gradient, Width = 256, Height = 4 },
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = ShadeWidth,
			AnchorBottom = 1f
		};

		var layer = new CanvasLayer { Name = "ShadeLayer", Layer = 1 };
		layer.AddChild(shade);
		return layer;
	}
}
