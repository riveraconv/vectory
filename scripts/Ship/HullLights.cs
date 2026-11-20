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
	// Chispas del parpadeo con daño (9.3) que aún no ha recogido Main, en el mundo
	private const int MaxSparks = 32;
	private readonly Vector3[] _sparkPoints = new Vector3[MaxSparks];
	private int _sparkCount;
	private readonly Dictionary<string, int> _index = new();
	private ShipLightInput _input;
	private float _opacity = 1f;
	private ShipHull3D _hull;
	// Luz de freno sobre el casco (6.3) y luz de cada tobera de retro (7.5), en la capa del casco
	private OmniLight3D _brakeLight;
	private readonly OmniLight3D[] _retroLights = new OmniLight3D[2];
	private float _brakeWritten = -1f, _retroWritten = -1f;
	private NVector3 _brakeColorWritten;

	// El estado, para las luces en pista y los faros (ShipWorldLights)
	public ShipLightState State => _state;

	// Toberas de la nave: de ellas salen el boost de la llama (bt) y el giro suavizado
	public ThrusterFlames Thrusters { get; set; }

	// hull: su casco ya cargado; layer: la capa visual de sus piezas
	// seed: elige siempre las mismas luces muertas de esta nave con daño 3
	public void Setup(ShipHull3D hull, HullManifest manifest, ShipLightSettings settings, uint layer, int seed)
	{
		_hull = hull;
		List<ShipLightDef> defs = ShipLightCatalog.From(manifest);
		_settings = settings ?? ShipLightSettings.Default;
		_state = new ShipLightState(defs, _settings, seed, new Random(seed * 7919 + 13));
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
			// la anticolisión y las balizas las llevan otros: aquí solo su halo
			_writes[i] = !ShipLightTypes.IsExternal(d.Type);
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

		if (_state.HasBrakeLights)
		{
			NVector3 p = _state.HullBrakePosition;
			_brakeLight = NewLight("BrakeLight", new Vector3(p.X, p.Y, p.Z), _settings.HullBrakeRange, _settings.HullBrakeAttenuation,
				layer);
		}
		float width = Thrusters?.RetroShape.Width ?? 1f;
		for (int k = 0; k < 2; k++)
		{
			string name = k == 0 ? "Retro_L_Point" : "Retro_R_Point";
			Vector3? forward = hull.EmptyForward(name);
			if (!forward.HasValue)
				continue;
			Vector3 at = hull.EmptyPosition(name) + forward.Value.Normalized() * _settings.RetroLightAhead * width;
			_retroLights[k] = NewLight(k == 0 ? "RetroLight_L" : "RetroLight_R", at, _settings.RetroLightRange,
				_settings.RetroLightAttenuation, layer);
		}
		Write();
	}

	// Luz sin sombras que solo toca su capa (la del casco), apagada hasta que tenga energía
	private OmniLight3D NewLight(string name, Vector3 position, float range, float attenuation, uint layer)
	{
		var light = new OmniLight3D
		{
			Name = name,
			Position = position,
			OmniRange = range,
			OmniAttenuation = attenuation,
			ShadowEnabled = false,
			LightCullMask = layer,
			LightEnergy = 0f,
			Visible = false
		};
		AddChild(light);
		return light;
	}

	// En este paso: altura sobre la pista (por encima de la de vuelo), si está en la parrilla, si es atravesable tras
	// recolocarla y si la carrera es TIME TRIAL
	public void SetContext(in ShipLightContext context)
	{
		_input.Altitude = context.Altitude;
		_input.OnGrid = context.OnGrid;
		_input.Passable = context.Passable;
		_input.TimeTrial = context.TimeTrial;
	}

	// Lo que hace la nave en este paso (el mismo que reciben las toberas)
	public void Feed(in ThrusterInput input)
	{
		_input.Braking = input.Brake;
		_input.Speed = input.Speed;
		_input.TopSpeed = input.TopSpeed;
		_input.Drifting = input.Drifting;
		_input.DamageLevel = input.DamageLevel;
	}

	// Chispas pendientes del parpadeo con daño, en el mundo; Main las suelta y las borra
	public int SparkCount => _sparkCount;
	public Vector3 SparkAt(int i) => _sparkPoints[i];
	public void ClearSparks() => _sparkCount = 0;

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
		_sparkCount = 0;
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
			_input.Power = Thrusters.Power;
			_input.Retro = Thrusters.RetroForce;
			_state.SetNozzleColors(Thrusters.MidColor, Thrusters.BoostMidColor);
		}
		for (int i = 0; i < _state.Count; i++)
		{
			if (!_writes[i] && _materials[i] != null && ShipLightTypes.IsExternal(_state.Def(i).Type))
				_state.SetExternal(i, _materials[i].EmissionEnergyMultiplier);
		}
		_state.Update((float)delta, _input);
		Write();
		WriteHullLights();
		CollectSparks();
	}

	private void CollectSparks()
	{
		ShipLightDamage damage = _state.Damage;
		if (damage.SparkCount == 0)
			return;
		Transform3D world = GlobalTransform;
		for (int k = 0; k < damage.SparkCount && _sparkCount < MaxSparks; k++)
		{
			NVector3 c = _state.Def(damage.Spark(k)).Center;
			_sparkPoints[_sparkCount++] = world * new Vector3(c.X, c.Y, c.Z);
		}
	}

	// Luz de freno en el casco, luz del retro y trampillas: solo si cambian
	private void WriteHullLights()
	{
		if (_brakeLight != null)
		{
			float e = _state.HullBrakeEnergy;
			NVector3 c = _state.HullBrakeColor;
			if (MathF.Abs(e - _brakeWritten) > Epsilon * MathF.Max(1f, e) || NVector3.DistanceSquared(c, _brakeColorWritten) > Epsilon * Epsilon)
			{
				_brakeLight.LightEnergy = e;
				_brakeLight.LightColor = new Color(c.X, c.Y, c.Z);
				_brakeLight.Visible = e > 0f;
				_brakeWritten = e;
				_brakeColorWritten = c;
			}
		}
		float retro = _state.RetroLight;
		if (MathF.Abs(retro - _retroWritten) > Epsilon * MathF.Max(1f, retro) || (retro == 0f && _retroWritten != 0f))
		{
			NVector3 mid = Thrusters?.RetroShape.Colors.Mid ?? NVector3.One;
			for (int k = 0; k < 2; k++)
			{
				if (_retroLights[k] == null)
					continue;
				_retroLights[k].LightEnergy = retro;
				_retroLights[k].LightColor = new Color(mid.X, mid.Y, mid.Z);
				_retroLights[k].Visible = retro > 0.001f;
			}
			_retroWritten = retro;
		}
		_hull?.SetRetroFlaps(_state.FlapAngle);
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
			// las bombillas, en su centro (el shader acerca el halo a la cámara); el resto, un poco hacia fuera
			NVector3 p = d.IsBulb ? d.Center : d.Center + d.HaloOutward * offset;
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
