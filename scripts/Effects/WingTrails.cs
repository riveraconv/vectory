using System;
using System.Runtime.InteropServices;
using Godot;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace Vectory.Effects;

// Estelas de las puntas de las alas de una nave (spec docs/efectos-7-estelas.md, 4): hijo de su Spaceship, pero en
// espacio de mundo (TopLevel), con una cinta por punta que se rehace cada frame de cara a la cámara. Solo dibuja: las
// reglas están en WingTrailState y WingTrailRibbon. La nave le pasa su movimiento en cada paso de física (Move); th y bt
// los toma de las llamas de sus toberas. Avanza en _Process, así la pausa la congela.
//
// Malla fija de dos superficies (una por punta) en tira de triángulos, de 2 × MaxSamples vértices: cada frame se suben
// solo los vértices vivos con RenderingServer y los que sobran se juntan en un punto (triángulos vacíos). Sin arrays de
// Godot nuevos por frame.
public partial class WingTrails : Node3D
{
	private static Shader _shader;

	private WingTrailSettings _settings = WingTrailSettings.Default;
	private WingTrailColors _colors = WingTrailColors.CyanOrange;
	private WingTrailState _state;
	private NVector3 _tip;

	private ArrayMesh _mesh;
	private ShaderMaterial _material;
	private Rid _meshRid;
	private int _maxVertices;
	private int _vertexStride, _attributeStride, _uvOffset, _uv2Offset, _customOffset;
	private WingTrailVertex[] _vertices;
	private byte[] _vertexBytes;
	private byte[] _attributeBytes;
	private readonly int[] _uploaded = new int[WingTrailState.Tips];

	private static readonly StringName SnAlong = "along";
	private static readonly StringName SnAcross = "across";
	private static readonly StringName SnPulse = "pulse";
	private static readonly StringName SnPhase = "phase";

	// Llamas de las toberas de la misma nave: de ellas salen th y bt
	public ThrusterFlames Thrusters { get; set; }

	// La nave a la que siguen las puntas
	public Node3D Ship { get; set; }

	// Antes de _Ready: ajustes, pareja de colores (7) y punta derecha del ala (la izquierda, con x cambiada de signo)
	public void Setup(WingTrailSettings settings, WingTrailColors colors, NVector3 tip)
	{
		_settings = settings ?? WingTrailSettings.Default;
		_colors = colors;
		_tip = tip;
	}

	// Otra nave (F10): las puntas en su sitio y sin la estela de la anterior
	public void SetTip(NVector3 tip)
	{
		_tip = tip;
		_state?.Reset();
	}

	// Un paso de física de la nave
	public void Move(float dt, in WingTrailMotion motion) => _state?.Move(dt, motion, _settings);

	// Recolocación: sin estela cruzando la pista
	public void ResetState() => _state?.Reset();

	public override void _Ready()
	{
		TopLevel = true;
		GlobalTransform = Transform3D.Identity;
		_state = new WingTrailState(_settings);
		_shader ??= new Shader { Code = TrailShader };
		_maxVertices = 2 * Math.Max(2, _settings.MaxSamples);
		_vertices = new WingTrailVertex[_maxVertices];

		_mesh = new ArrayMesh();
		Mesh.ArrayFormat flags = Mesh.ArrayFormat.FlagUseDynamicUpdate
			| (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift);
		for (int k = 0; k < WingTrailState.Tips; k++)
		{
			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[_maxVertices];
			arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[_maxVertices];
			arrays[(int)Mesh.ArrayType.TexUV2] = new Vector2[_maxVertices];
			arrays[(int)Mesh.ArrayType.Custom0] = new float[_maxVertices * 4];
			_mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays, null, null, flags);
		}
		// En espacio de mundo: que no se recorte nunca
		_mesh.CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f));
		_meshRid = _mesh.GetRid();

		// Cómo guarda Godot los vértices de esta superficie (posiciones en un búfer; UV, UV2 y CUSTOM0 en otro)
		var format = (RenderingServer.ArrayFormat)(long)_mesh.SurfaceGetFormat(0);
		_vertexStride = (int)RenderingServer.MeshSurfaceGetFormatVertexStride(format, _maxVertices);
		_attributeStride = (int)RenderingServer.MeshSurfaceGetFormatAttributeStride(format, _maxVertices);
		_uvOffset = (int)RenderingServer.MeshSurfaceGetFormatOffset(format, _maxVertices, (int)Mesh.ArrayType.TexUV);
		_uv2Offset = (int)RenderingServer.MeshSurfaceGetFormatOffset(format, _maxVertices, (int)Mesh.ArrayType.TexUV2);
		_customOffset = (int)RenderingServer.MeshSurfaceGetFormatOffset(format, _maxVertices, (int)Mesh.ArrayType.Custom0);
		_vertexBytes = new byte[_vertexStride * _maxVertices];
		_attributeBytes = new byte[_attributeStride * _maxVertices];

		WingTrailSettings s = _settings;
		_material = new ShaderMaterial { Shader = _shader };
		_material.SetShaderParameter(SnAlong, new Vector4(s.AlongPower, s.AlongFadeIn, s.Brightness, 0f));
		_material.SetShaderParameter(SnAcross, new Vector4(s.HaloGain, s.HaloFalloff, s.CoreGain, s.CoreFalloff));
		_material.SetShaderParameter(SnPulse, new Vector4(s.PulseSpacing, s.PulseSharpness, s.PulseGain, s.PulseFalloff));
		_material.SetShaderParameter(SnPhase, 0f);
		AddChild(new MeshInstance3D
		{
			Name = "Ribbons",
			Mesh = _mesh,
			MaterialOverride = _material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		});
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (Ship != null)
		{
			Transform3D t = Ship.GlobalTransform;
			Vector3 right = t.Basis.X.Normalized();
			Vector3 up = t.Basis.Y.Normalized();
			Vector3 left = t * new Vector3(-_tip.X, _tip.Y, _tip.Z);
			Vector3 rightTip = t * new Vector3(_tip.X, _tip.Y, _tip.Z);
			_state.Update(dt, new WingTrailFrame
			{
				TipLeft = ToNumerics(left),
				TipRight = ToNumerics(rightTip),
				Right = ToNumerics(right),
				Up = ToNumerics(up),
				Throttle = Thrusters?.Throttle ?? 0f,
				Boost = Thrusters?.BoostLevel ?? 0f
			}, _settings);
		}
		if (!IsVisibleInTree())
			return;

		// Pulsos: con el tiempo de la estela, que la pausa para
		_material.SetShaderParameter(SnPhase, WingTrailRibbon.PulsePhase(_state.Time, _settings));
		Camera3D camera = GetViewport().GetCamera3D();
		NVector3 eye = camera != null ? ToNumerics(camera.GlobalPosition) : NVector3.Zero;
		for (int k = 0; k < WingTrailState.Tips; k++)
		{
			int n = camera != null
				? WingTrailRibbon.Build(_state.Tip(k), _state.Time, eye, _colors, _settings, _vertices)
				: 0;
			Upload(k, n);
		}
	}

	// Sube los n vértices vivos de una punta; los que se subieron el frame anterior y ya no lo están, a un mismo punto
	private void Upload(int surface, int n)
	{
		int previous = _uploaded[surface];
		if (n == 0 && previous == 0)
			return;
		int count = Math.Max(n, previous);
		Span<float> positions = MemoryMarshal.Cast<byte, float>(_vertexBytes.AsSpan());
		int vertexFloats = _vertexStride / 4;
		NVector3 rest = n > 0 ? _vertices[n - 1].Position : NVector3.Zero;
		for (int i = 0; i < count; i++)
		{
			NVector3 p = i < n ? _vertices[i].Position : rest;
			int o = i * vertexFloats;
			positions[o] = p.X;
			positions[o + 1] = p.Y;
			positions[o + 2] = p.Z;
		}
		RenderingServer.MeshSurfaceUpdateVertexRegion(_meshRid, surface, 0,
			new ReadOnlySpan<byte>(_vertexBytes, 0, count * _vertexStride));

		if (n > 0)
		{
			Span<float> attributes = MemoryMarshal.Cast<byte, float>(_attributeBytes.AsSpan());
			int attributeFloats = _attributeStride / 4;
			int uv = _uvOffset / 4, uv2 = _uv2Offset / 4, custom = _customOffset / 4;
			for (int i = 0; i < n; i++)
			{
				ref WingTrailVertex v = ref _vertices[i];
				int o = i * attributeFloats;
				attributes[o + uv] = v.U;
				attributes[o + uv + 1] = v.V;
				attributes[o + uv2] = v.Distance;
				attributes[o + uv2 + 1] = v.Boost;
				attributes[o + custom] = v.Color.X;
				attributes[o + custom + 1] = v.Color.Y;
				attributes[o + custom + 2] = v.Color.Z;
				attributes[o + custom + 3] = v.Intensity;
			}
			RenderingServer.MeshSurfaceUpdateAttributeRegion(_meshRid, surface, 0,
				new ReadOnlySpan<byte>(_attributeBytes, 0, n * _attributeStride));
		}
		_uploaded[surface] = n;
	}

	private static NVector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

	// Cinta (4.3) y pulsos (5.2), traducidos de la maqueta: sin luz, en suma, por las dos caras, sin escribir profundidad
	// (sí la lee). UV = (u, v); UV2 = (D, boost de la muestra); CUSTOM0 = (color, intensidad).
	// along = (potencia del apagado, entrada en la punta, brillo, —); across = (halo, su caída, núcleo, su caída);
	// pulse = (separación, dureza, fuerza, caída de lado); phase = velocidad × t, envuelta a una vuelta.
	private const string TrailShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 along;
uniform vec4 across;
uniform vec4 pulse;
uniform float phase;
varying vec4 tint;
varying vec2 dist;

void vertex() {
	tint = CUSTOM0;
	dist = UV2;
}

void fragment() {
	float u = UV.x;
	float v = UV.y;
	float a = pow(max(0.0, 1.0 - u), along.x) * smoothstep(0.0, along.y, u);
	float c = across.x * exp(-across.y * v * v) + across.z * exp(-across.w * v * v);
	float p = dist.y * pow(max(0.0, sin(pulse.x * dist.x - phase)), pulse.y) * pulse.z * exp(-pulse.w * v * v);
	ALBEDO = tint.rgb * (c * a + p * a) * tint.a * along.z;
}
";
}
