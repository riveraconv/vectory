using Godot;
using Vectory.Settings;
using Vectory.Ship;
using NVector3 = System.Numerics.Vector3;

namespace Vectory.Effects;

// Llamas de las dos toberas de una nave (spec docs/efectos-6-toberas.md, 4): hijo de su Spaceship, con una llama por boca
// (penacho y núcleo, dos conos de la misma malla que deforma el shader) y el anillo de encendido del boost. Solo dibuja:
// las reglas están en ThrusterState. La nave le pasa lo que hace en cada paso (Feed); avanza en _Process, así la pausa
// la congela. Si tiene casco 3D, le pone también la emisión de las bocas.
public partial class ThrusterFlames : Node3D
{
	private const int ConeSegments = 28;
	private const int ConeRings = 28;

	private static readonly StringName SnShape = "shape";
	private static readonly StringName SnForm = "form";
	private static readonly StringName SnGlow = "glow";
	private static readonly StringName SnCore = "col_core";
	private static readonly StringName SnMid = "col_mid";
	private static readonly StringName SnOut = "col_out";
	private static readonly StringName SnRing = "ring";
	private static readonly StringName SnHeat = "heat";

	private static ArrayMesh _cone;
	private static QuadMesh _quad;
	private static Shader _flameShader;
	private static Shader _ringShader;
	private static Shader _heatShader;

	private sealed class Nozzle
	{
		public Node3D Pivot;
		public ShaderMaterial Plume;
		public ShaderMaterial Core;
		public MeshInstance3D Ring;
		public ShaderMaterial RingMaterial;
		public MeshInstance3D Heat;
		public ShaderMaterial HeatMaterial;
		public OmniLight3D Light;
	}

	private readonly Nozzle[] _nozzles = new Nozzle[ThrusterState.Nozzles];
	private ThrusterFxSettings _settings = ThrusterFxSettings.Default;
	private ThrusterShape _shape;
	private ThrusterState _state;
	private ThrusterInput _input;
	private float _phase;
	private bool _full = true;
	private uint _lightLayer = ShipHull3D.HullLayer;

	// Casco 3D de la nave, para la emisión de sus bocas (null con el wireframe)
	public ShipHull3D Hull { get; set; }

	// La intensidad de todo × esto (el fantasma: su transparencia)
	public float Opacity { get; set; } = 1f;

	// Antes de _Ready: ajustes, forma de la nave, desfase del tiempo (para que dos naves no parpadeen igual), si lleva todo
	// (carrera) o solo la llama (fantasma y menú: sin diamantes, calor ni luz; spec 9) y la capa del casco que ilumina
	public void Setup(ThrusterFxSettings settings, ThrusterShape shape, float phase, bool full, uint lightLayer)
	{
		_settings = settings ?? ThrusterFxSettings.Default;
		_shape = shape;
		_phase = phase;
		_full = full;
		_lightLayer = lightLayer;
	}

	// Otra nave (F10): las bocas en su sitio, con su tamaño y sus colores
	public void SetShape(ThrusterShape shape)
	{
		_shape = shape;
		PlaceNozzles();
	}

	public void Feed(in ThrusterInput input) => _input = input;

	// Recolocación: al ralentí al momento
	public void ResetState()
	{
		_state?.Reset(_settings);
		_input = default;
	}

	public override void _Ready()
	{
		_state = new ThrusterState(_phase, _settings);
		_cone ??= BuildCone();
		_quad ??= new QuadMesh { Size = Vector2.One };
		_flameShader ??= new Shader { Code = FlameShader };
		_ringShader ??= new Shader { Code = RingShader };
		_heatShader ??= new Shader { Code = HeatShader };

		for (int k = 0; k < ThrusterState.Nozzles; k++)
		{
			var nozzle = new Nozzle { Pivot = new Node3D { Name = k == 0 ? "Nozzle_L" : "Nozzle_R" } };
			AddChild(nozzle.Pivot);
			nozzle.Plume = AddCone(nozzle.Pivot, "Plume");
			nozzle.Core = AddCone(nozzle.Pivot, "Core");
			nozzle.RingMaterial = new ShaderMaterial { Shader = _ringShader };
			nozzle.Ring = new MeshInstance3D
			{
				Name = "Ring",
				Mesh = _quad,
				MaterialOverride = nozzle.RingMaterial,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Visible = false
			};
			nozzle.Pivot.AddChild(nozzle.Ring);
			if (_full)
			{
				// Calor: antes que las llamas (prioridad −1), porque copia la pantalla sin lo transparente
				nozzle.HeatMaterial = new ShaderMaterial { Shader = _heatShader, RenderPriority = -1 };
				nozzle.Heat = new MeshInstance3D
				{
					Name = "Heat",
					Mesh = _quad,
					MaterialOverride = nozzle.HeatMaterial,
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
					ExtraCullMargin = 4f,
					Visible = false
				};
				nozzle.Pivot.AddChild(nozzle.Heat);
				nozzle.Light = new OmniLight3D
				{
					Name = "Light",
					Position = new Vector3(0f, 0f, _settings.LightBehind),
					OmniRange = _settings.LightRange,
					OmniAttenuation = _settings.LightAttenuation,
					ShadowEnabled = false,
					LightCullMask = _lightLayer,
					LightEnergy = 0f,
					Visible = false
				};
				nozzle.Pivot.AddChild(nozzle.Light);
			}
			_nozzles[k] = nozzle;
		}
		PlaceNozzles();
	}

	private static ShaderMaterial AddCone(Node3D parent, string name)
	{
		var material = new ShaderMaterial { Shader = _flameShader };
		parent.AddChild(new MeshInstance3D
		{
			Name = name,
			Mesh = _cone,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		});
		return material;
	}

	private void PlaceNozzles()
	{
		if (_nozzles[0] == null)
			return;
		for (int k = 0; k < ThrusterState.Nozzles; k++)
		{
			NVector3 p = _shape.Nozzle;
			_nozzles[k].Pivot.Position = new Vector3(k == 0 ? -p.X : p.X, p.Y, p.Z);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_state.Update(dt, _input, _settings);
		if (!IsVisibleInTree())
			return;

		ThrusterFxSettings s = _settings;
		float scale = _shape.Scale;
		bool heatOn = _full && ActiveSettings.Particles > ActiveSettings.ParticlesFraction(0);
		Camera3D camera = heatOn ? GetViewport().GetCamera3D() : null;
		for (int k = 0; k < ThrusterState.Nozzles; k++)
		{
			ThrusterFlame f = _state.Flame(k, _shape, s, heatOn);
			Nozzle n = _nozzles[k];
			float time = _state.Time + k * 3.7f;
			float scroll = _state.Scroll + k * 7.3f;
			float intensity = f.Brightness * Opacity;

			n.Plume.SetShaderParameter(SnShape, new Vector4(f.Length, f.Width, f.Bend, _shape.Squash));
			float diamonds = _full ? f.Diamonds * s.DiamondGlow * Opacity : 0f;
			n.Plume.SetShaderParameter(SnForm, new Vector4(s.PlumeMouthRadius * scale, s.PlumeTipRadius * scale, 0f, diamonds));
			n.Plume.SetShaderParameter(SnGlow, new Vector4(intensity, f.Flicker, scroll, time));
			n.Plume.SetShaderParameter(SnCore, ToVector(f.Core));
			n.Plume.SetShaderParameter(SnMid, ToVector(f.Mid));
			n.Plume.SetShaderParameter(SnOut, ToVector(f.Out));

			n.Core.SetShaderParameter(SnShape, new Vector4(f.Length * s.CoreLength, f.Width, f.Bend * s.CoreBend, _shape.Squash));
			n.Core.SetShaderParameter(SnForm, new Vector4(s.CoreMouthRadius * scale, s.CoreTipRadius * scale, 1f, 0f));
			n.Core.SetShaderParameter(SnGlow, new Vector4(intensity * s.CoreBrightness, f.Flicker, scroll, time));
			n.Core.SetShaderParameter(SnCore, ToVector(f.Core));
			n.Core.SetShaderParameter(SnMid, ToVector(f.Core));
			n.Core.SetShaderParameter(SnOut, ToVector(f.Mid));

			bool ring = f.RingIntensity > 0f;
			n.Ring.Visible = ring;
			if (ring)
			{
				float size = f.RingSize * scale;
				n.Ring.Transform = new Transform3D(Basis.FromScale(new Vector3(size, size * s.RingAspect, 1f)), new Vector3(0f, 0f, s.RingBehind));
				n.RingMaterial.SetShaderParameter(SnRing, new Vector4(s.RingColor.X, s.RingColor.Y, s.RingColor.Z, f.RingIntensity * Opacity));
			}

			if (_full)
			{
				// Calor: de cara a la cámara (lo hace el shader), detrás de la boca según el largo
				// y menos cuanto más lejos de la cámara (si no, una nave lejana se ve doble)
				n.Heat.Position = new Vector3(0f, 0f, s.HeatBehind + s.HeatBehindLength * f.Length);
				float strength = f.Heat;
				if (strength > 0f && camera != null)
					strength *= ThrusterState.HeatDistanceFactor(camera.GlobalPosition.DistanceTo(n.Heat.GlobalPosition), s);
				bool heat = strength > 0.01f;
				n.Heat.Visible = heat;
				if (heat)
				{
					n.HeatMaterial.SetShaderParameter(SnHeat, new Vector4(s.HeatWidth + s.HeatWidthLength * f.Length,
						s.HeatHeight + s.HeatHeightLength * f.Length, strength * s.HeatDisplacement, time));
				}

				// Luz en el casco: solo con casco 3D (el wireframe no se ilumina), del color medio (HAMMER, naranja)
				bool light = Hull != null && f.LightEnergy > 0f;
				n.Light.Visible = light;
				if (light)
				{
					n.Light.LightColor = new Color(f.Mid.X, f.Mid.Y, f.Mid.Z);
					n.Light.LightEnergy = f.LightEnergy;
				}
			}

			// La boca toma el color medio (el de la maqueta, tal cual, como color de la emisión)
			Hull?.SetNozzleGlow(k, new Color(f.Mid.X, f.Mid.Y, f.Mid.Z), f.Mouth);
		}
	}

	private static Vector3 ToVector(NVector3 v) => new(v.X, v.Y, v.Z);

	// Cono de ConeSegments × ConeRings con (v, θ) en la UV: v de 0 en la boca a 1 en la punta, θ de 0 a 2π. Las posiciones
	// las pone el shader; la caja cubre la llama más larga, doblada, con boost y encendido.
	private static ArrayMesh BuildCone()
	{
		int columns = ConeSegments + 1;
		var vertices = new Vector3[(ConeRings + 1) * columns];
		var uvs = new Vector2[vertices.Length];
		var indices = new int[ConeRings * ConeSegments * 6];
		for (int j = 0; j <= ConeRings; j++)
		{
			for (int i = 0; i <= ConeSegments; i++)
			{
				int index = j * columns + i;
				vertices[index] = new Vector3(0f, 0f, (float)j / ConeRings);
				uvs[index] = new Vector2((float)j / ConeRings, (float)i / ConeSegments * Mathf.Tau);
			}
		}
		int w = 0;
		for (int j = 0; j < ConeRings; j++)
		{
			for (int i = 0; i < ConeSegments; i++)
			{
				int a = j * columns + i;
				int b = a + columns;
				indices[w++] = a;
				indices[w++] = b;
				indices[w++] = a + 1;
				indices[w++] = a + 1;
				indices[w++] = b;
				indices[w++] = b + 1;
			}
		}
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		arrays[(int)Mesh.ArrayType.Index] = indices;
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.CustomAabb = new Aabb(new Vector3(-6f, -6f, -1f), new Vector3(12f, 12f, 16f));
		return mesh;
	}

	// Llama (4.2 y 4.3), traducida de la maqueta: sin luz, en suma, por las dos caras, sin escribir profundidad.
	// shape = (largo, ancho, doblado, achatado); form = (radio en la boca, en la punta, capa: 0 penacho / 1 núcleo, —);
	// glow = (intensidad, parpadeo, lo que ha corrido el ruido, tiempo). form.w: los diamantes (0 en el núcleo).
	private const string FlameShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 shape;
uniform vec4 form;
uniform vec4 glow;
uniform vec3 col_core;
uniform vec3 col_mid;
uniform vec3 col_out;
varying float fv;
varying float ft;
float h21(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }
float vn(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), u.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
	float a = 0.5;
	float s = 0.0;
	for (int i = 0; i < 4; i++) { s += a * vn(p); p *= 2.03; a *= 0.5; }
	return s;
}
void vertex() {
	float v = UV.x;
	float t = UV.y;
	bool core = form.z > 0.5;
	float bulge = 1.0 + 0.35 * sin(v * PI) * (core ? 0.4 : 1.0);
	float r = mix(form.x, form.y, pow(v, 0.8)) * shape.y * bulge;
	r *= 1.0 + 0.06 * sin(glow.w * 31.0 + v * 9.0);
	VERTEX = vec3(cos(t) * r + shape.z * v * v, sin(t) * r * shape.w, v * shape.x);
	NORMAL = normalize(vec3(cos(t), sin(t) * shape.w, 0.0));
	fv = v;
	ft = t;
}
void fragment() {
	bool core = form.z > 0.5;
	float v = fv;
	float n = fbm(vec2(ft * 1.6 + glow.w * 0.9, v * shape.x * 1.4 - glow.z));
	float face = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float edge = pow(face, core ? 0.8 : 1.6);
	float s = pow(1.0 - v, core ? 2.2 : 1.3) * smoothstep(0.0, 0.04, v + 0.02);
	float tear = smoothstep(0.15, 0.75, n + 0.55 - v * 0.9);
	vec3 col = mix(col_core, col_mid, smoothstep(0.02, 0.32, v));
	col = mix(col, col_out, smoothstep(0.3, 0.95, v));
	float i = glow.x * glow.y * s * edge * (0.45 + 0.75 * n) * tear;
	// Diamantes de choque (5.1): bandas quietas respecto a la boca, solo en el penacho (form.w = D × 2,2)
	i += pow(max(0.0, cos(v * shape.x * 2.4 - 0.6)), 10.0) * exp(-v * 2.2) * form.w * edge;
	if (core) i *= 1.6;
	ALBEDO = col * i;
}
";

	// Anillo de encendido (4.5): transparente en el centro, brillante hacia el 70 % del radio. ring = (color, intensidad).
	private const string RingShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 ring;
void fragment() {
	float d = length(UV - 0.5) * 2.0;
	float a = d < 0.47 ? 0.0 : d < 0.7 ? (d - 0.47) / 0.23 : d < 1.0 ? (1.0 - d) / 0.3 : 0.0;
	ALBEDO = ring.rgb * ring.a * a;
}
";

	// Calor (5.2): un plano de cara a la cámara que desplaza lo que hay detrás con dos ruidos que suben, dentro de una
	// elipse. heat = (ancho, alto, desplazamiento en coordenadas de pantalla, tiempo). Sin escribir profundidad.
	private const string HeatShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform vec4 heat;
float h21(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }
float vn(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), u.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
	float a = 0.5;
	float s = 0.0;
	for (int i = 0; i < 4; i++) { s += a * vn(p); p *= 2.03; a *= 0.5; }
	return s;
}
void vertex() {
	vec3 center = MODEL_MATRIX[3].xyz;
	vec3 world = center + INV_VIEW_MATRIX[0].xyz * VERTEX.x * heat.x + INV_VIEW_MATRIX[1].xyz * VERTEX.y * heat.y;
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world, 1.0);
}
void fragment() {
	vec2 c = UV - 0.5;
	float m = smoothstep(0.5, 0.1, length(c * vec2(1.6, 1.0)));
	float a = fbm(UV * vec2(5.0, 3.0) + vec2(0.0, heat.w * 2.6));
	float b = fbm(UV * vec2(4.0, 3.5) + vec2(4.7, heat.w * 3.1));
	vec2 o = vec2(a - 0.5, b - 0.5) * m * heat.z;
	ALBEDO = texture(screen_tex, SCREEN_UV + o).rgb;
	ALPHA = m;
}
";
}
