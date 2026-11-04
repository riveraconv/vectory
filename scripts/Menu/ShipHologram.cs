using System.Collections.Generic;
using Godot;
using Vectory.Core;
using Vectory.Ship;

namespace Vectory.Menu;

// Holograma de SELECT SHIP: una instancia de Spaceship sobre una rejilla con un anillo de proyección. Solo dibuja:
// la orientación se la pasan (HologramSpin).
//
// Vive en el mundo 3D del menú, lejos del fondo (FarAway), con su propia cámara, que mira de frente hacia -Z y no
// gira: los ejes de la pantalla son los del mundo. El encuadre se desplaza para que el centro de la nave caiga en
// ScreenCenter. La distancia de la cámara sale del ancho de la nave en pantalla, y el suelo, la rejilla y los
// anillos, de las medidas en píxeles de la spec. Medidas de pantalla sobre 1280×720.
public partial class ShipHologram : Node3D
{
	private const float ReferenceHalfHeight = 360f;
	private const int RingSegments = 96;
	private const int InnerRingDashes = 24;

	[Export] public Vector2 ScreenCenter = new(410f, 400f);   // centro de la nave en pantalla
	[Export] public float ShipScreenWidth = 480f;             // de punta a punta de las alas, en píxeles
	[Export] public float CameraFov = 36f;                    // vertical, en grados
	[Export(PropertyHint.Range, "0,1,0.01")] public float FarEdgeBrightness = 0.35f;   // brillo de las aristas más lejanas

	[ExportGroup("Rejilla y anillos")]
	[Export] public float HorizonScreenY = 500f;              // altura del horizonte en pantalla
	[Export] public Vector2 RingScreenRadius = new(250f, 34f);       // radios de la elipse en pantalla
	[Export] public float InnerRingScreenRadius = 170f;       // radio horizontal de la interior
	[Export] public int GridRadialLines = 17;
	[Export] public float GridRowSpacing = 1.5f;
	[Export] public float GridFadeLength = 10f;
	[Export] public float GridOpacity = 0.7f;
	[Export] public float GridNearHalfWidth = 40f;

	// Lejos del fondo del menú y de la miniatura de SELECT CIRCUIT
	private static readonly Vector3 FarAway = new(2000f, -2000f, 0f);

	private const string HologramShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;

uniform float depth_near = 10.0;
uniform float depth_far = 20.0;
uniform float far_brightness = 0.35;

varying float view_depth;

void vertex() {
	view_depth = -(MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).z;
}

void fragment() {
	float t = clamp((view_depth - depth_near) / (depth_far - depth_near), 0.0, 1.0);
	ALBEDO = COLOR.rgb * mix(1.0, far_brightness, t);
}
";

	private Node3D _pivot;
	private Spaceship _ship;
	private Camera3D _camera;

	public override void _Ready()
	{
		Position = FarAway;
		Visible = false;

		_pivot = new Node3D { Name = "Pivot" };
		AddChild(_pivot);

		_ship = new Spaceship { Name = "Spaceship" };
		_pivot.AddChild(_ship);
		Aabb bounds = _ship.Bounds;
		_ship.Position = -(bounds.Position + bounds.Size * 0.5f);   // gira alrededor del centro de su caja

		// Píxeles por unidad de tangente: a la distancia d, una unidad mide focal / d píxeles
		float focal = ReferenceHalfHeight / Mathf.Tan(Mathf.DegToRad(CameraFov) * 0.5f);
		float distance = bounds.Size.X * focal / ShipScreenWidth;

		_camera = new Camera3D { Name = "Camera3D", Near = 0.1f, Far = 200f, Position = new Vector3(0f, 0f, distance) };
		AddChild(_camera);
		ApplyLensShift();
		GetViewport().SizeChanged += ApplyLensShift;

		// Aristas lejanas más tenues, multiplicando el color (con ALPHA la nave perdería el glow)
		float radius = bounds.Size.Length() * 0.5f;
		var material = new ShaderMaterial { Shader = new Shader { Code = HologramShaderCode } };
		material.SetShaderParameter("depth_near", distance - radius);
		material.SetShaderParameter("depth_far", distance + radius);
		material.SetShaderParameter("far_brightness", FarEdgeBrightness);
		_ship.SetMaterialOverride(material);

		BuildFloor(focal, distance);
	}

	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= ApplyLensShift;
	}

	// Se ve (con su cámara) o se oculta
	public void SetActive(bool active)
	{
		Visible = active;
		if (active)
			_camera.MakeCurrent();
	}

	// Muestra u oculta la nave; la rejilla y el anillo siguen (nave COMING SOON)
	public void SetShipVisible(bool visible) => _ship.Visible = visible;

	public void SetOrientation(System.Numerics.Quaternion q) => _pivot.Quaternion = new Quaternion(q.X, q.Y, q.Z, q.W);

	// Suelo a la altura en la que un círculo bajo la nave se ve como la elipse de la spec
	private void BuildFloor(float focal, float distance)
	{
		float floorDepth = distance * RingScreenRadius.Y / RingScreenRadius.X;
		float floorY = -floorDepth;
		float pxToWorld = distance / focal;   // a la distancia de la nave

		AddChild(new MeshInstance3D
		{
			Name = "Ring",
			Mesh = BuildRing(floorY, RingScreenRadius.X * pxToWorld, InnerRingScreenRadius * pxToWorld)
		});

		// El borde lejano de la rejilla cae en HorizonScreenY
		float below = Mathf.Max(1f, HorizonScreenY - ScreenCenter.Y);
		float horizonDistance = floorDepth * focal / below;
		float horizonZ = distance - horizonDistance;

		var grid = new HorizonGridSettings
		{
			FloorY = floorY,
			HorizonZ = horizonZ,
			NearZ = distance + 1f,
			NearHalfWidth = GridNearHalfWidth,
			RadialLines = GridRadialLines,
			RowSpacing = GridRowSpacing,
			FadeLength = GridFadeLength,
			Opacity = GridOpacity
		};
		AddChild(new MeshInstance3D { Name = "Grid", Mesh = HorizonGrid.BuildMesh(grid, Palette.MenuGrid) });

		float horizonHalf = horizonDistance * ScreenCenter.X / focal;
		AddChild(new MeshInstance3D
		{
			Name = "HorizonLine",
			Mesh = HorizonGrid.BuildHorizonLine(new Vector3(-horizonHalf, floorY, horizonZ), new Vector3(horizonHalf, floorY, horizonZ),
				Palette.MenuHorizon, new[] { (0f, 0f), (0.5f, 0.5f), (1f, 0f) })
		});
	}

	private static ArrayMesh BuildRing(float y, float outer, float inner)
	{
		var verts = new List<Vector3>();
		var colors = new List<Color>();

		Vector3 Point(float radius, int k)
		{
			float a = Mathf.Tau * k / RingSegments;
			return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
		}

		int dashSegments = Mathf.Max(1, RingSegments / (InnerRingDashes * 2));
		for (int k = 0; k < RingSegments; k++)
		{
			verts.Add(Point(outer, k));
			verts.Add(Point(outer, k + 1));
			colors.Add(Palette.HologramRing);
			colors.Add(Palette.HologramRing);

			if (k / dashSegments % 2 == 0)
			{
				verts.Add(Point(inner, k));
				verts.Add(Point(inner, k + 1));
				colors.Add(Palette.HologramRingInner);
				colors.Add(Palette.HologramRingInner);
			}
		}
		return HorizonGrid.BuildLineMesh(verts, colors);
	}

	// Proyección frustum con el encuadre desplazado para que el centro de la nave caiga en ScreenCenter
	private void ApplyLensShift()
	{
		Vector2 size = GetViewport().GetVisibleRect().Size;
		float aspect = size.Y > 0f ? size.X / size.Y : 16f / 9f;

		float near = _camera.Near;
		float height = 2f * near * Mathf.Tan(Mathf.DegToRad(CameraFov) * 0.5f);
		float halfWidth = height * 0.5f * aspect;

		float fx = ScreenCenter.X / 1280f;
		float fy = ScreenCenter.Y / 720f;
		var shift = new Vector2(-(fx * 2f - 1f) * halfWidth, (fy * 2f - 1f) * height * 0.5f);
		_camera.SetFrustum(height, shift, near, _camera.Far);
	}
}
