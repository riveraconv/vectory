using System;
using System.Collections.Generic;
using Godot;
using Vectory.Effects;
using Vectory.Settings;
using NVector3 = System.Numerics.Vector3;

namespace Vectory.Ship;

// Luces de la nave sobre su casco 3D (spec docs/efectos-8-luces.md, secciones 5, 6 y 7.1): pone en los materiales de
// las piezas vivas la intensidad y el color que da ShipLightState y dibuja los halos de las luces puntuales. Sin reglas:
// solo conecta. Lo crea ShipHull3D en carrera (tú y los rivales); el fantasma, el menú y el holograma no lo llevan.
public partial class HullLights : Node3D
{
	// Halo: cuadrado de cara a la cámara, en suma, que lee la profundidad (las piezas del casco lo tapan) y no la escribe.
	// Tamaño en INSTANCE_CUSTOM.x; color × opacidad en COLOR. Se acerca un poco a la cámara para que la pieza no lo corte.
	private const string HaloShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float pull = 0.08;
varying vec4 tint;

void vertex() {
	vec4 c = MODELVIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0);
	float s = INSTANCE_CUSTOM.x;
	vec3 p = c.xyz + normalize(-c.xyz) * min(pull, length(c.xyz) * 0.5) + vec3(VERTEX.xy * s, 0.0);
	POSITION = PROJECTION_MATRIX * vec4(p, 1.0);
	tint = COLOR;
}

void fragment() {
	vec2 d = UV * 2.0 - 1.0;
	float r2 = dot(d, d);
	float a = max(0.0, 1.0 - r2);
	a = a * a * (0.35 + 0.65 * exp(-r2 * 6.0));
	ALBEDO = tint.rgb * tint.a * a;
}
";

	private const int Stride = 20;   // 12 de la transformación + 4 del color + 4 de los datos propios

	private ShipLightState _state;
	private ShipLightSettings _settings;
	private StandardMaterial3D[] _materials;
	private float[] _written;
	private NVector3[] _writtenColor;
	private bool[] _writes;
	private int[] _haloLights;
	private float[] _buffer;
	private MultiMesh _multiMesh;
	private int[] _flickerMap;
	private readonly Dictionary<string, int> _index = new();
	private ShipLightInput _input;
	private float _opacity = 1f;

	// Toberas de la nave: de ellas salen el boost de la llama (bt) y el giro suavizado
	public ThrusterFlames Thrusters { get; set; }

	// hull: su casco ya cargado; layer: la capa visual de sus piezas
	public void Setup(ShipHull3D hull, HullManifest manifest, ShipLightSettings settings, uint layer)
	{
		List<ShipLightDef> defs = ShipLightCatalog.From(manifest);
		_settings = settings ?? ShipLightSettings.Default;
		_state = new ShipLightState(defs, _settings);
		_materials = new StandardMaterial3D[defs.Count];
		_writes = new bool[defs.Count];
		_written = new float[defs.Count];
		_writtenColor = new NVector3[defs.Count];
		var halos = new List<int>();
		for (int i = 0; i < defs.Count; i++)
		{
			ShipLightDef d = defs[i];
			_index[d.Name] = i;
			_materials[i] = hull.LightMaterial(d.Name);
			// la anticolisión, las balizas y las bocas del retro las llevan otros: aquí solo su halo
			_writes[i] = !ShipLightTypes.IsExternal(d.Type) && d.Type != ShipLightType.Retro;
			if (_materials[i] != null)
			{
				Color c = _materials[i].Emission;
				_state.SetBaseColor(i, new NVector3(c.R, c.G, c.B));
				_written[i] = _materials[i].EmissionEnergyMultiplier;
				_writtenColor[i] = new NVector3(c.R, c.G, c.B);
			}
			if (d.HasHalo)
				halos.Add(i);
		}
		_haloLights = halos.ToArray();

		IReadOnlyList<string> flicker = hull.Profile.Contract.FlickerLights;
		_flickerMap = new int[flicker.Count];
		for (int i = 0; i < flicker.Count; i++)
			_flickerMap[i] = _index.TryGetValue(flicker[i], out int k) ? k : -1;

		_buffer = new float[_haloLights.Length * Stride];
		_multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			UseCustomData = true,
			Mesh = new QuadMesh { Size = Vector2.One },
			InstanceCount = _haloLights.Length
		};
		var material = new ShaderMaterial { Shader = new Shader { Code = HaloShader } };
		AddChild(new MultiMeshInstance3D
		{
			Name = "Halos",
			Multimesh = _multiMesh,
			MaterialOverride = material,
			Layers = layer,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			// los cuadrados se agrandan en el shader: la caja tiene que cubrir la nave con su halo
			CustomAabb = new Aabb(new Vector3(-4f, -3f, -8f), new Vector3(8f, 6f, 16f))
		});
		Write();
	}

	// Lo que hace la nave en este paso (el mismo que reciben las toberas)
	public void Feed(in ThrusterInput input)
	{
		_input.Braking = input.Brake;
		_input.Speed = input.Speed;
		_input.TopSpeed = input.TopSpeed;
		_input.Drifting = input.Drifting;
	}

	// Parpadeo del daño de siempre (spec de cada casco, 6.5): la luz i del contrato apagada o no
	public void SetFlicker(int contractIndex, float factor)
	{
		if (_flickerMap == null || contractIndex >= _flickerMap.Length)
			return;
		int k = _flickerMap[contractIndex];
		if (k >= 0)
			_state.SetDamage(k, factor);
	}

	// Color de partida de una luz (la pintura de VECTOR)
	public void SetBaseColor(string piece, Color color)
	{
		if (_index.TryGetValue(piece, out int i))
			_state.SetBaseColor(i, new NVector3(color.R, color.G, color.B));
	}

	// Nave atravesable tras recolocar: los halos siguen la opacidad del casco
	public void SetOpacity(float opacity) => _opacity = opacity;

	// Recolocación: sin freno ni giro al momento
	public void ResetState()
	{
		_state?.Reset();
		Write();
	}

	public override void _Process(double delta)
	{
		if (_state == null)
			return;
		if (Thrusters != null)
		{
			_input.Boost = Thrusters.BoostLevel;
			_input.Steer = Thrusters.SmoothedSteer;
		}
		for (int i = 0; i < _state.Count; i++)
		{
			if (!_writes[i] && _materials[i] != null && ShipLightTypes.IsExternal(_state.Def(i).Type))
				_state.SetExternal(i, _materials[i].EmissionEnergyMultiplier);
		}
		_state.Update((float)delta, _input);
		Write();
	}

	// Cambiar un material cuesta (Godot rehace sus datos): solo se escribe lo que ha cambiado de verdad
	private const float Epsilon = 0.01f;

	private void Write()
	{
		for (int i = 0; i < _state.Count; i++)
		{
			StandardMaterial3D material = _materials[i];
			if (material == null || !_writes[i])
				continue;
			float intensity = _state.Intensity(i);
			if (MathF.Abs(intensity - _written[i]) > Epsilon * MathF.Max(1f, intensity))
			{
				material.EmissionEnergyMultiplier = intensity;
				_written[i] = intensity;
			}
			if (_state.Def(i).Type == ShipLightType.Brake)
			{
				NVector3 c = _state.Color(i);
				if (NVector3.DistanceSquared(c, _writtenColor[i]) > Epsilon * Epsilon)
				{
					material.Emission = new Color(c.X, c.Y, c.Z);
					_writtenColor[i] = c;
				}
			}
		}

		float bloom = ActiveSettings.Bloom * _opacity;
		float offset = _settings.HaloOffset;
		bool dirty = false;
		for (int h = 0; h < _haloLights.Length; h++)
		{
			int i = _haloLights[h];
			ShipLightDef d = _state.Def(i);
			NVector3 p = d.Center + d.HaloOutward * offset;
			NVector3 c = _state.Color(i);
			float alpha = _state.HaloOpacity(i) * bloom;
			float size = _state.HaloSize(i);
			int o = h * Stride;
			if (MathF.Abs(_buffer[o + 15] - alpha) <= Epsilon && MathF.Abs(_buffer[o + 16] - size) <= Epsilon
				&& MathF.Abs(_buffer[o + 12] - c.X) <= Epsilon && MathF.Abs(_buffer[o + 13] - c.Y) <= Epsilon
				&& MathF.Abs(_buffer[o + 14] - c.Z) <= Epsilon && _buffer[o] == 1f)
				continue;
			dirty = true;
			_buffer[o + 0] = 1f; _buffer[o + 1] = 0f; _buffer[o + 2] = 0f; _buffer[o + 3] = p.X;
			_buffer[o + 4] = 0f; _buffer[o + 5] = 1f; _buffer[o + 6] = 0f; _buffer[o + 7] = p.Y;
			_buffer[o + 8] = 0f; _buffer[o + 9] = 0f; _buffer[o + 10] = 1f; _buffer[o + 11] = p.Z;
			_buffer[o + 12] = c.X; _buffer[o + 13] = c.Y; _buffer[o + 14] = c.Z;
			_buffer[o + 15] = alpha;
			_buffer[o + 16] = size; _buffer[o + 17] = 0f; _buffer[o + 18] = 0f; _buffer[o + 19] = 0f;
		}
		if (dirty)
			RenderingServer.MultimeshSetBuffer(_multiMesh.GetRid(), _buffer);
	}
}
