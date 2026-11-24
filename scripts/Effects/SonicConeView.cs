using Godot;
using Vectory.Core;
using Vectory.Settings;
using Vectory.Ship;

namespace Vectory.Effects;

// Cono de vapor (spec docs/efectos-9-vision-tunel.md, 5; designs/efectos-cono-sonico.html, mkCone, variante A). Dos mallas
// fijas con el mismo shader: la de persecución cuelga de la nave y se abre hacia atrás; la de cabina cuelga de su cámara,
// con la punta delante y el ojo dentro, y se ve por el parabrisas. Van en el mundo: la visión túnel las estira y la cabina
// 3D y el HUD quedan encima.
//
// Todo va en el shader; por frame solo cambian unos parámetros, y solo si cambian. Las mallas están siempre puestas: con
// opacidad 0 el shader las encoge a un punto y no pintan nada. Sin reglas: los valores salen de SonicConeCurves. Se
// procesa también en pausa (un cambio de SETTINGS o de cámara se ve al momento).
public partial class SonicConeView : Node
{
	private static readonly StringName SnOpacity = "opacity";
	private static readonly StringName SnFlow = "flow";
	private static readonly StringName SnColor = "line_color";
	private static readonly StringName SnInside = "inside";
	private static readonly StringName SnFloor = "floor_y";

	// Medidas de la maqueta, con VECTOR (semiancho 2,63 m, morro a 4 m); las otras naves las escalan
	private const float BaseHalfWidth = 2.63f;
	private const float BaseFront = 4f;
	private const float MinScale = 0.75f;
	private const float Flatten = 0.72f;
	private const float TopRadius = 0.8f;   // arriba del todo, la campana de persecución va más pegada a la nave
	private const int Segments = 72;

	// Un solo shader para el jugador y los rivales
	private static Shader _sharedShader;
	public static Shader SharedShader => _sharedShader ??= new Shader { Code = ShaderCode };

	private const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float opacity = 0.0;
uniform float flow = 0.0;
uniform vec3 line_color = vec3(0.52, 0.89, 1.0);
uniform float inside = 0.0;
uniform float floor_y = -1.0;   // el suelo, en los ejes de la nave
uniform vec2 near_fade = vec2(-2.0, -1.0);   // m de la cámara: invisible / completo (rivales); por defecto, sin fundido
varying float height;
varying vec3 vpos;
varying vec3 vnor;

// El ruido se repite cada 256 celdas en z
float hash(vec3 p) {
	p.z = mod(p.z, 256.0);
	return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = mix(mix(hash(i), hash(i + vec3(1.0, 0.0, 0.0)), f.x),
		mix(hash(i + vec3(0.0, 1.0, 0.0)), hash(i + vec3(1.0, 1.0, 0.0)), f.x), f.y);
	float b = mix(mix(hash(i + vec3(0.0, 0.0, 1.0)), hash(i + vec3(1.0, 0.0, 1.0)), f.x),
		mix(hash(i + vec3(0.0, 1.0, 1.0)), hash(i + vec3(1.0, 1.0, 1.0)), f.x), f.y);
	return mix(a, b, f.z);
}

void vertex() {
	height = VERTEX.y - floor_y;
	if (opacity <= 0.0) {
		VERTEX = vec3(0.0);
	}
	vpos = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vnor = MODELVIEW_NORMAL_MATRIX * NORMAL;
}

void fragment() {
	float t = UV.y;   // 0 punta, 1 boca
	float a = UV.x * 6.2831853;
	vec2 c = vec2(cos(a), sin(a));
	float n = 0.65 * noise(vec3(c * 3.2, t * 3.0 - flow)) + 0.35 * noise(vec3(c * 9.0, t * 5.0 - flow * 1.7));
	float shape;
	if (inside > 0.5) {
		float q = length(vpos.xy) / max(0.01, -vpos.z);
		shape = smoothstep(0.28, 1.0, q) * (1.0 - smoothstep(0.97, 1.0, t));
	} else {
		float rim = 1.0 - abs(dot(normalize(vnor), normalize(vpos)));
		shape = mix(0.18, 1.0, pow(max(rim, 0.0), 1.8)) * smoothstep(0.1, 0.45, t) * (1.0 - smoothstep(0.65, 1.0, t))
			* smoothstep(0.02, 0.8, height) * smoothstep(near_fade.x, near_fade.y, length(vpos));
	}
	ALBEDO = line_color * smoothstep(0.15, 1.0, n) * shape * opacity;
}
";

	private MeshInstance3D _cockpitCone;
	private MeshInstance3D _chaseCone;
	private ShaderMaterial _cockpitMaterial;
	private ShaderMaterial _chaseMaterial;

	private SonicConeSettings _settings = SonicConeSettings.Default;
	private float _amount;
	private float _boost;
	private float _flow;
	private CameraView _view = CameraView.Chase;

	private float _shownCockpit = float.NaN;
	private float _shownChase = float.NaN;
	private float _shownFlow = float.NaN;
	private float _shownMix = float.NaN;

	// Monta las dos mallas. floorY: el suelo en los ejes de la nave.
	public void Setup(Node3D ship, Camera3D cockpitCamera, float floorY)
	{
		ProcessMode = ProcessModeEnum.Always;
		Shader shader = SharedShader;
		_cockpitMaterial = new ShaderMaterial { Shader = shader };
		_cockpitMaterial.SetShaderParameter(SnInside, 1f);
		_chaseMaterial = new ShaderMaterial { Shader = shader };
		_chaseMaterial.SetShaderParameter(SnInside, 0f);
		_chaseMaterial.SetShaderParameter(SnFloor, floorY);
		_cockpitCone = Build("SonicConeCockpit", BuildMesh(CockpitProfile(), 1f, 1f), _cockpitMaterial);
		_chaseCone = Build("SonicConeChase", null, _chaseMaterial);
		cockpitCamera.AddChild(_cockpitCone);
		ship.AddChild(_chaseCone);
	}

	// Tamaño del cono de persecución según la nave (en depuración puede cambiar en carrera)
	public void SetShip(ShipVisualProfile visual)
	{
		_chaseCone.Mesh = BuildChaseMesh(visual);
	}

	// Campana de persecución a la escala de una nave (también la de los rivales)
	public static ArrayMesh BuildChaseMesh(ShipVisualProfile visual)
	{
		float width = Mathf.Max(MinScale, visual.CollisionHalfWidth / BaseHalfWidth);
		return BuildMesh(ChaseProfile(width, visual.CollisionFront / BaseFront), Flatten, TopRadius);
	}

	public void SetSettings(SonicConeSettings settings)
	{
		_settings = settings;
	}

	// Cantidad suavizada, boost (0–1), recorrido del vapor y cámara activa
	public void SetSource(float amount, float boost, float flow, CameraView view)
	{
		_amount = amount;
		_boost = boost;
		_flow = flow;
		_view = view;
	}

	public override void _Process(double delta)
	{
		SonicConeFrame frame = SonicConeCurves.Frame(_amount, _boost, _flow, _view, ActiveSettings.SpeedLines, _settings);
		if (Changed(frame.CockpitOpacity, ref _shownCockpit))
			_cockpitMaterial.SetShaderParameter(SnOpacity, frame.CockpitOpacity);
		if (Changed(frame.ChaseOpacity, ref _shownChase))
			_chaseMaterial.SetShaderParameter(SnOpacity, frame.ChaseOpacity);
		if (frame.CockpitOpacity <= 0f && frame.ChaseOpacity <= 0f)
			return;   // sin verse, el resto da igual: se pone al día cuando vuelva
		if (Changed(frame.Flow, ref _shownFlow))
		{
			_cockpitMaterial.SetShaderParameter(SnFlow, frame.Flow);
			_chaseMaterial.SetShaderParameter(SnFlow, frame.Flow);
		}
		if (Changed(frame.BoostMix, ref _shownMix))
		{
			Color c = Palette.SonicVapor.Lerp(Palette.SonicVaporBoost, frame.BoostMix).SrgbToLinear();
			var color = new Vector3(c.R, c.G, c.B);
			_cockpitMaterial.SetShaderParameter(SnColor, color);
			_chaseMaterial.SetShaderParameter(SnColor, color);
		}
	}

	private static MeshInstance3D Build(string name, Mesh mesh, Material material) => new()
	{
		Name = name,
		Mesh = mesh,
		MaterialOverride = material,
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
	};

	// Puntos (z, radio) de la punta a la boca. Delante es −z. Cabina: de 60 m a 1,8 m por delante del ojo.
	private static Vector2[] CockpitProfile()
	{
		var p = new Vector2[61];
		for (int i = 0; i < p.Length; i++)
		{
			float d = 60f * Mathf.Pow(1.8f / 60f, i / 60f);
			p[i] = new Vector2(-d, 5.2f * (62f - d) / 60f);
		}
		return p;
	}

	private static Vector2[] ChaseProfile(float width, float length)
	{
		var p = new Vector2[25];
		for (int i = 0; i < p.Length; i++)
		{
			float k = i / 24f;
			p[i] = new Vector2((-2.4f + 6.2f * k) * length, (0.5f + 3.5f * Mathf.Pow(k, 1.5f)) * width);
		}
		return p;
	}

	private static ArrayMesh BuildMesh(Vector2[] profile, float flatten, float topRadius)
	{
		int rows = profile.Length;
		int columns = Segments + 1;
		var vertices = new Vector3[rows * columns];
		var normals = new Vector3[rows * columns];
		var uvs = new Vector2[rows * columns];
		var indices = new int[(rows - 1) * Segments * 6];
		for (int i = 0; i < rows; i++)
		{
			Vector2 slope = (profile[Mathf.Min(rows - 1, i + 1)] - profile[Mathf.Max(0, i - 1)]).Normalized();
			for (int j = 0; j < columns; j++)
			{
				float angle = j * Mathf.Tau / Segments;
				float cos = Mathf.Cos(angle);
				float sin = Mathf.Sin(angle);
				// De los lados (1) a lo más alto (topRadius), sin escalón; la mitad de abajo no cambia
				float radius = profile[i].Y * (sin > 0f ? Mathf.Lerp(1f, topRadius, sin * sin) : 1f);
				int v = i * columns + j;
				vertices[v] = new Vector3(cos * radius, sin * radius * flatten, profile[i].X);
				normals[v] = new Vector3(cos * slope.X, sin * slope.X, -slope.Y);
				uvs[v] = new Vector2(j / (float)Segments, i / (float)(rows - 1));
			}
		}
		int n = 0;
		for (int i = 0; i < rows - 1; i++)
		{
			for (int j = 0; j < Segments; j++)
			{
				int a = i * columns + j;
				int b = a + columns;
				indices[n++] = a;
				indices[n++] = b;
				indices[n++] = a + 1;
				indices[n++] = a + 1;
				indices[n++] = b;
				indices[n++] = b + 1;
			}
		}
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		arrays[(int)Mesh.ArrayType.Index] = indices;
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	private static bool Changed(float value, ref float shown)
	{
		if (value == shown)
			return false;
		shown = value;
		return true;
	}
}
