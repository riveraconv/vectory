using System;
using System.Collections.Generic;
using Godot;
using Vectory.Settings;
using NVector3 = System.Numerics.Vector3;

namespace Vectory.Ship;

// Luces de la nave que no van en el casco (spec docs/efectos-8-luces.md, 7.3 y 7.4): las cuatro luces en pista, los
// focos de los faros y su haz visible. Hijo de su Spaceship (no del casco, que en cabina va oculto: desde la cabina tus
// faros y tu luz en pista se ven). Sin reglas: lo que pone sale del ShipLightState de HullLights.
//
// La luz en pista y los focos solo iluminan la capa TrackLightLayer, que hoy no tiene nada: el suelo de pista y del túnel
// que reciba luz lo hará otra spec (v1.2, sección 7.2). Hasta entonces se dejan preparados y no se ven; el haz, sí.
public partial class ShipWorldLights : Node3D
{
	// Capa que iluminan la luz en pista y los focos: la del suelo que reciba luz (ninguno todavía)
	public const uint TrackLightLayer = 1u << 1;

	private const string BeamShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform vec4 beam = vec4(1.0, 1.0, 1.0, 0.0);   // color y opacidad
uniform float fade_power = 2.4;

void fragment() {
	float along = pow(clamp(1.0 - UV.y, 0.0, 1.0), fade_power);
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	ALBEDO = beam.rgb * beam.a * along * smoothstep(0.05, 0.6, facing);
}
";

	private static readonly StringName SnBeam = "beam";
	private const float Epsilon = 0.01f;

	private HullLights _source;
	private ShipLightSettings _settings;
	private readonly OmniLight3D[] _track = new OmniLight3D[4];
	private readonly float[] _trackWritten = { -1f, -1f, -1f, -1f };
	private readonly List<ShipSpotDef> _spots = new();
	private readonly List<SpotLight3D> _spotLights = new();
	private readonly List<MeshInstance3D> _beams = new();
	private readonly List<NVector3> _beamAxes = new();
	private float[] _spotWritten;
	private float[] _beamWritten;

	// Vista de cabina de tu nave: el haz, a la mitad
	public bool CockpitView { get; set; }

	// source: las luces del casco de la nave; trackY: la y de la pista en los ejes de la nave (la altura de vuelo, negativa)
	public void Setup(HullLights source, ShipLightSettings settings, HullManifest manifest, HullStyle style, float trackY)
	{
		_source = source;
		_settings = settings ?? ShipLightSettings.Default;

		(NVector3 left, NVector3 right, NVector3 rear, NVector3 nozzle) = ShipTrackLights.Positions(style);
		_track[0] = NewOmni("TrackLeft", left, _settings.TrackRange);
		_track[1] = NewOmni("TrackRight", right, _settings.TrackRange);
		_track[2] = NewOmni("TrackRear", rear, _settings.TrackRange);
		_track[3] = NewOmni("TrackNozzle", nozzle, _settings.TrackNozzleRange);

		_spots.AddRange(ShipSpots.From(manifest, style));
		var material = new ShaderMaterial { Shader = new Shader { Code = BeamShader } };
		material.SetShaderParameter("fade_power", _settings.BeamFadePower);
		foreach (ShipSpotDef spot in _spots)
		{
			NVector3 at = ShipSpots.LightPosition(spot, _settings);
			NVector3 target = ShipSpots.Target(spot, _settings, trackY);
			var light = new SpotLight3D
			{
				Name = spot.Work ? "WorkSpot" : "HeadSpot",
				SpotRange = _settings.SpotRange,
				SpotAttenuation = _settings.SpotAttenuation,
				SpotAngle = Mathf.RadToDeg(spot.HalfAngle),
				ShadowEnabled = false,
				LightCullMask = TrackLightLayer,
				LightColor = new Color(_settings.SpotColor.X, _settings.SpotColor.Y, _settings.SpotColor.Z),
				LightEnergy = 0f,
				Visible = false
			};
			light.Transform = new Transform3D(Basis.Identity, ToGodot(at)).LookingAt(ToGodot(target), Vector3.Up);
			AddChild(light);
			_spotLights.Add(light);

			NVector3 axis = ShipSpots.BeamAxis(spot, _settings, trackY);
			var beam = new MeshInstance3D
			{
				Name = "Beam",
				Mesh = BuildCone(_settings.BeamStartRadius, ShipSpots.BeamEndRadius(spot, _settings), _settings.BeamLength),
				MaterialOverride = material,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Visible = false
			};
			beam.Transform = new Transform3D(BasisAlong(ToGodot(axis)), ToGodot(spot.Lens));
			AddChild(beam);
			_beams.Add(beam);
			_beamAxes.Add(axis);
		}
		_spotWritten = new float[_spots.Count];
		_beamWritten = new float[_spots.Count];
		Array.Fill(_spotWritten, -1f);
		Array.Fill(_beamWritten, -1f);
	}

	private OmniLight3D NewOmni(string name, NVector3 position, float range)
	{
		var light = new OmniLight3D
		{
			Name = name,
			Position = ToGodot(position),
			OmniRange = range,
			OmniAttenuation = _settings.TrackAttenuation,
			ShadowEnabled = false,
			LightCullMask = TrackLightLayer,
			LightEnergy = 0f,
			Visible = false
		};
		AddChild(light);
		return light;
	}

	public override void _Process(double delta)
	{
		ShipLightState state = _source?.State;
		if (state == null || !IsInstanceValid(_source))
			return;
		float scale = _settings.TrackEnergyScale;
		WriteOmni(0, state.TrackLeft * scale, state.TrackLeftColor);
		WriteOmni(1, state.TrackRight * scale, state.TrackRightColor);
		WriteOmni(2, state.TrackRear * scale, state.TrackRearColor);
		WriteOmni(3, state.TrackNozzle * scale, state.TrackNozzleColor);

		Camera3D camera = GetViewport().GetCamera3D();
		NVector3 eye = camera != null ? ToNumerics(ToLocal(camera.GlobalPosition)) : new NVector3(0f, 0f, 1e4f);
		float without = _settings.BeamWithoutBloom;
		float bloom = without + (1f - without) * Mathf.Clamp(ActiveSettings.Bloom, 0f, 1f);
		float view = (CockpitView ? _settings.BeamCockpit : 1f) * bloom;
		for (int i = 0; i < _spots.Count; i++)
		{
			ShipSpotDef spot = _spots[i];
			float energy = spot.Intensity * _settings.SpotEnergyScale * state.HeadlightFactor;
			if (MathF.Abs(energy - _spotWritten[i]) > Epsilon * MathF.Max(1f, energy))
			{
				_spotLights[i].LightEnergy = energy;
				_spotLights[i].Visible = energy > 0f;
				_spotWritten[i] = energy;
			}
			float alpha = ShipSpots.BeamOpacity(spot, _settings) * state.HeadlightFactor * view
				* ShipSpots.BeamCameraFactor(eye, spot.Lens, _beamAxes[i], spot.HalfAngle, _settings);
			if (MathF.Abs(alpha - _beamWritten[i]) > 0.0005f)
			{
				NVector3 c = _settings.SpotColor;
				_beams[i].SetInstanceShaderParameter(SnBeam, new Vector4(c.X, c.Y, c.Z, alpha));
				_beams[i].Visible = alpha > 0.0005f;
				_beamWritten[i] = alpha;
			}
		}
	}

	private void WriteOmni(int i, float energy, NVector3 color)
	{
		if (MathF.Abs(energy - _trackWritten[i]) <= Epsilon * MathF.Max(1f, energy))
			return;
		_track[i].LightEnergy = energy;
		_track[i].LightColor = new Color(color.X, color.Y, color.Z);
		_track[i].Visible = energy > 0.001f;
		_trackWritten[i] = energy;
	}

	// Base con su +Z a lo largo de axis
	private static Basis BasisAlong(Vector3 axis)
	{
		Vector3 z = axis.Normalized();
		Vector3 up = Mathf.Abs(z.Y) < 0.999f ? Vector3.Up : Vector3.Right;
		Vector3 x = up.Cross(z).Normalized();
		return new Basis(x, z.Cross(x), z);
	}

	// Cono abierto a lo largo de +Z, de r0 en z 0 a r1 en z length; UV.y de 0 en la lente a 1 al final
	private static ArrayMesh BuildCone(float r0, float r1, float length)
	{
		const int segments = 24, rings = 8;
		var vertices = new Vector3[(segments + 1) * (rings + 1)];
		var normals = new Vector3[vertices.Length];
		var uvs = new Vector2[vertices.Length];
		float slope = (r1 - r0) / length;
		for (int j = 0; j <= rings; j++)
		{
			float v = (float)j / rings;
			float r = Mathf.Lerp(r0, r1, v);
			for (int i = 0; i <= segments; i++)
			{
				float a = Mathf.Tau * i / segments;
				var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
				int k = j * (segments + 1) + i;
				vertices[k] = radial * r + new Vector3(0f, 0f, v * length);
				normals[k] = (radial - new Vector3(0f, 0f, slope)).Normalized();
				uvs[k] = new Vector2((float)i / segments, v);
			}
		}
		var indices = new int[segments * rings * 6];
		int n = 0;
		for (int j = 0; j < rings; j++)
		{
			for (int i = 0; i < segments; i++)
			{
				int a = j * (segments + 1) + i, b = a + segments + 1;
				indices[n++] = a; indices[n++] = b; indices[n++] = a + 1;
				indices[n++] = a + 1; indices[n++] = b; indices[n++] = b + 1;
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

	private static Vector3 ToGodot(NVector3 v) => new(v.X, v.Y, v.Z);
	private static NVector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);
}
