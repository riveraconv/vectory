using Godot;

namespace Vectory.Ship;

// Cristal dañado de la cabina 3D y de la cabina de los cascos 3D por fuera (spec docs/efectos-5-danos-casco.md, 6 y 7): una pasada de shader detrás del material de la
// mancha del cristal (Glass_Smudge), puesta al cargar la cabina para que se compile entonces y el primer golpe no dé un
// tirón. Pinta picadas, grietas largas irregulares y manchas en los ejes del propio cristal (así van pegadas a él), con
// los sitios de CockpitGlassDamage. Sin reglas.
public static class CockpitGlass
{
	public const string GlassPiece = "Glass_Smudge";

	private static readonly StringName ChipParam = "chip";
	private static readonly StringName CrackParam = "crack";
	private static readonly StringName StainParam = "stain";
	private static readonly StringName CrackOpacityParam = "crack_opacity";
	private static readonly StringName StainOpacityParam = "stain_opacity";
	private static readonly StringName CenterParam = "glass_center";
	private static readonly StringName SizeParam = "glass_size";

	private static Shader _shader;
	private static Shader _hullShader;
	private static readonly StringName AxisUParam = "axis_u";
	private static readonly StringName AxisVParam = "axis_v";

	// Cabecera de la cabina: el cristal se mide en los ejes de su pieza, con su lado largo en x y el otro en su plano
	private const string CockpitHead = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec3 glass_center;
uniform vec3 glass_size = vec3(1.0);
uniform vec3 crack_color : source_color = vec3(0.30, 0.30, 0.31);
varying vec2 glass_uv;

void vertex() {
	vec3 up = vec3(0.0, NORMAL.z, -NORMAL.y);
	up = length(up) > 0.0001 ? normalize(up) : vec3(0.0, 1.0, 0.0);
	if (up.y < 0.0) up = -up;
	float h = max(length(glass_size.yz), 0.0001);
	vec3 d = VERTEX - glass_center;
	glass_uv = vec2(d.x / max(glass_size.x, 0.0001) + 0.5, 0.5 - dot(d, up) / h);
}
";

	// Cabecera de los cascos 3D (vista exterior): el cristal, curvo, se proyecta sobre el plano de sus dos medidas más
	// largas (en glass_size van esas dos medidas: x y luego y)
	private const string HullHead = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec3 glass_center;
uniform vec3 glass_size = vec3(1.0);
uniform vec3 axis_u = vec3(1.0, 0.0, 0.0);
uniform vec3 axis_v = vec3(0.0, 0.0, 1.0);
// Por fuera las grietas se ven claras: dispersan la luz que les da
uniform vec3 crack_color : source_color = vec3(0.72, 0.74, 0.76);
varying vec2 glass_uv;

void vertex() {
	vec3 d = VERTEX - glass_center;
	glass_uv = vec2(dot(d, axis_u) / max(glass_size.x, 0.0001) + 0.5, 0.5 - dot(d, axis_v) / max(glass_size.y, 0.0001));
}
";

	// Lo común: picadas, grietas y manchas en glass_uv
	private const string Body = @"
uniform vec4 chip[10];
uniform vec4 crack[3];
uniform vec4 stain[3];
uniform float crack_opacity = 0.45;
uniform float stain_opacity = 0.3;
uniform vec3 stain_color : source_color = vec3(0.30, 0.27, 0.24);
// Cada tramo de una grieta es una cara de la fractura, inclinada a su manera: así coge la luz de la cabina (la principal,
// la del boost, la de los daños y la de las chispas) a trozos, y no como una pegatina
uniform float facet_tilt = 0.7;
uniform float crack_roughness = 0.18;
uniform float crack_specular = 0.9;

float hash(float n) { return fract(sin(n) * 43758.5453123); }
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123); }
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), u.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.07; a *= 0.5; }
	return v;
}

// Distancia de p al tramo a-b, y en t dónde cae a lo largo (0-1)
float seg_dist(vec2 p, vec2 a, vec2 b, out float t) {
	vec2 ab = b - a;
	t = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-8), 0.0, 1.0);
	return length(p - a - ab * t);
}

// Línea de ancho w (en px) a distancia d, con un brillo de refracción alrededor
float line(float d, float w, float px) {
	float core = 1.0 - smoothstep(w * 0.5 * px, (w * 0.5 + 1.0) * px, d);
	float glint = (1.0 - smoothstep(0.0, px * 4.0, d)) * 0.18;
	return max(core, glint);
}

// Picada: centro machacado de borde irregular, medio anillo de ojo de buey y grietillas cortas alrededor
float chip_at(vec2 q, vec4 c, float aspect, float px, inout vec2 facet, inout float best) {
	if (c.z <= 0.0) return 0.0;
	vec2 d = q - c.xy * vec2(aspect, 1.0);
	float r = length(d);
	if (r > c.z * 4.5) return 0.0;
	float a = atan(d.y, d.x);
	float edge = c.z * (0.5 + 0.5 * noise(vec2(a * 1.6 + c.w, c.w)));
	float core = (1.0 - smoothstep(edge, edge + px, r)) * (0.5 + 0.5 * noise(d / c.z * 7.0 + c.w));
	float ring = (1.0 - smoothstep(px * 0.5, px * 1.5, abs(r - c.z * 1.5))) * step(0.5, noise(vec2(a * 1.3, c.w + 3.0))) * 0.55;
	float rays = 0.0;
	for (int i = 0; i < 6; i++) {
		float fi = float(i);
		float ang = (fi + 0.8 * hash(c.w + fi * 2.3)) * TAU / 6.0;
		float len = c.z * (1.6 + 2.6 * hash(c.w + fi * 5.9));
		float da = abs(mod(a - ang + 3.14159265, TAU) - 3.14159265);
		float dist = r * sin(min(da, 1.5707963));
		float on = step(r, len) * step(da, 1.5);
		rays = max(rays, on * (1.0 - smoothstep(px * 0.4, px * 1.4, dist)) * (1.0 - 0.6 * r / len));
	}
	float v = max(core, max(ring, rays * 0.85));
	if (v > best) {
		best = v;
		facet = vec2(cos(a * 3.0 + c.w), sin(a * 2.0 + c.w));
	}
	return v;
}

// Grieta larga: una línea quebrada que sale de su picada, se estrecha y se apaga hacia la punta y suelta ramas
float crack_at(vec2 q, vec4 c, float aspect, float px, inout vec2 facet, inout float top) {
	if (c.z <= 0.0) return 0.0;
	vec2 p = c.xy * vec2(aspect, 1.0);
	if (length(q - p) > c.z + 0.05) return 0.0;
	// Hacia el centro del cristal, con un desvío al azar: desde los bordes, la grieta cruza el cristal
	vec2 to_center = vec2(aspect * 0.5, 0.5) - p;
	float ang = atan(to_center.y, to_center.x) + (hash(c.w) - 0.5) * 1.8;
	float travelled = 0.0;
	float best = 0.0;
	for (int i = 0; i < 28; i++) {
		float rest = c.z - travelled;
		if (rest <= 0.0) break;
		float fi = float(i);
		float seg = 0.018 + 0.022 * hash(c.w + fi * 1.7);
		ang += (hash(c.w + fi * 3.3) - 0.5) * 1.1;
		float len = min(seg, rest);
		vec2 n = p + vec2(cos(ang), sin(ang)) * len;
		float t;
		float d = seg_dist(q, p, n, t);
		float along = (travelled + t * len) / c.z;
		// A trozos se ve más y a trozos casi nada, como una grieta real según le da la luz
		float vis = 0.3 + 0.7 * smoothstep(0.3, 0.7, noise(vec2((travelled + t * len) * 22.0, c.w)));
		float v = line(d, mix(1.7, 0.7, along), px) * mix(1.0, 0.45, along) * vis;
		if (v > top) {
			top = v;
			facet = vec2(hash(c.w + fi * 11.3), hash(c.w + fi * 13.7)) * 2.0 - 1.0;
		}
		best = max(best, v);
		// Rama hacia un lado, más corta y más fina
		if (hash(c.w + fi * 9.1) > 0.8 && rest > 0.08) {
			vec2 bp = n;
			float ba = ang + (hash(c.w + fi * 4.4) > 0.5 ? 0.8 : -0.8);
			float bstep = rest * (0.04 + 0.05 * hash(c.w + fi * 2.2));
			for (int k = 0; k < 6; k++) {
				float fk = float(k);
				ba += (hash(c.w + fi * 7.0 + fk * 1.3) - 0.5) * 1.0;
				vec2 bn = bp + vec2(cos(ba), sin(ba)) * bstep;
				float bt;
				float bd = seg_dist(q, bp, bn, bt);
				float bv = line(bd, 0.9, px) * mix(0.75, 0.35, (fk + bt) / 6.0);
				if (bv > top) {
					top = bv;
					facet = vec2(hash(c.w + fi * 5.1 + fk), hash(c.w + fi * 6.3 + fk)) * 2.0 - 1.0;
				}
				best = max(best, bv);
				bp = bn;
			}
		}
		p = n;
		travelled += len;
	}
	return best;
}

// Mancha: borde irregular de ruido, más densa en el centro
float stain_at(vec2 q, vec4 s, float aspect) {
	if (s.z <= 0.0) return 0.0;
	vec2 d = (q - s.xy * vec2(aspect, 1.0)) / s.z;
	float r = length(d);
	if (r > 1.4) return 0.0;
	float n = fbm(d * 2.2 + vec2(s.w, s.w * 0.7));
	float edge = 1.0 - smoothstep(0.2, 1.0, r + (n - 0.5) * 0.6);
	float body = edge * (0.65 + 0.35 * fbm(d * 9.0 + s.w));
	// Salpicaduras alrededor
	float speck = step(0.8, noise(d * 26.0 + s.w * 3.0)) * (1.0 - smoothstep(0.5, 1.35, r)) * 0.8;
	return max(body, speck);
}

void fragment() {
	float aspect = glass_size.x / max(length(glass_size.yz), 0.0001);
	vec2 q = glass_uv * vec2(aspect, 1.0);
	float px = max(length(fwidth(q)) * 0.7071, 1e-5);
	float c = 0.0;
	float s = 0.0;
	vec2 facet = vec2(0.0);
	float top = 0.0;
	for (int i = 0; i < 10; i++)
		c = max(c, chip_at(q, chip[i], aspect, px, facet, top));
	for (int i = 0; i < 3; i++) {
		c = max(c, crack_at(q, crack[i], aspect, px, facet, top));
		s = max(s, stain_at(q, stain[i], aspect));
	}
	float ca = clamp(c, 0.0, 1.0) * crack_opacity;
	float sa = s * stain_opacity;
	float alpha = ca + sa * (1.0 - ca);
	if (alpha <= 0.001) discard;
	float on_crack = ca / max(alpha, 0.0001);
	ALBEDO = mix(stain_color, crack_color, on_crack);
	ALPHA = alpha;
	NORMAL = normalize(NORMAL + vec3(facet * facet_tilt * on_crack, 0.0));
	ROUGHNESS = mix(0.9, crack_roughness, on_crack);
	SPECULAR = mix(0.1, crack_specular, on_crack);
	METALLIC = 0.0;
}
";

	// Pone la pasada detrás del material del cristal; null si la cabina no tiene cristal con mancha
	public static ShaderMaterial Attach(MeshInstance3D glass)
	{
		if (glass?.Mesh == null || glass.GetActiveMaterial(0) is not BaseMaterial3D material)
			return null;
		_shader ??= new Shader { Code = CockpitHead + Body };
		var pass = new ShaderMaterial { Shader = _shader, RenderPriority = 1 };
		Aabb box = glass.Mesh.GetAabb();
		pass.SetShaderParameter(CenterParam, box.GetCenter());
		pass.SetShaderParameter(SizeParam, box.Size);
		material.NextPass = pass;
		Apply(pass, 0, 0, CockpitGlassSettings.Default);
		return pass;
	}

	// Casco 3D: pone la pasada al final de la cadena de cada material del cristal (detrás de la iridiscencia, si la
	// tiene), proyectado sobre el plano de sus dos medidas más largas. Devuelve las pasadas puestas.
	public static void AttachHull(MeshInstance3D glass, System.Collections.Generic.List<ShaderMaterial> passes)
	{
		if (glass?.Mesh == null)
			return;
		_hullShader ??= new Shader { Code = HullHead + Body };
		Aabb box = glass.Mesh.GetAabb();
		Vector3 size = box.Size;
		// El eje más corto es el de su grosor (el que mira hacia fuera): se proyecta sobre los otros dos
		int thin = size.X <= size.Y && size.X <= size.Z ? 0 : size.Y <= size.Z ? 1 : 2;
		Vector3 u = thin == 0 ? Vector3.Back : Vector3.Right;
		Vector3 v = thin == 1 ? Vector3.Back : Vector3.Up;
		float su = thin == 0 ? size.Z : size.X;
		float sv = thin == 1 ? size.Z : size.Y;
		for (int i = 0; i < glass.GetSurfaceOverrideMaterialCount(); i++)
		{
			if (glass.GetSurfaceOverrideMaterial(i) is not BaseMaterial3D material)
				continue;
			var pass = new ShaderMaterial { Shader = _hullShader, RenderPriority = 1 };
			pass.SetShaderParameter(CenterParam, box.GetCenter());
			pass.SetShaderParameter(SizeParam, new Vector3(su, sv, 0f));
			pass.SetShaderParameter(AxisUParam, u);
			pass.SetShaderParameter(AxisVParam, v);
			Material last = material;
			while (last.NextPass != null)
				last = last.NextPass;
			last.NextPass = pass;
			Apply(pass, 0, 0, CockpitGlassSettings.Hull);
			passes.Add(pass);
		}
	}

	// Solo al cambiar de nivel: no corre cada frame
	public static void Apply(ShaderMaterial pass, int level, int seed, CockpitGlassSettings s)
	{
		if (pass == null)
			return;
		var chips = new Vector4[CockpitGlassDamage.MaxChips];
		for (int i = 0; i < chips.Length; i++)
			chips[i] = ToVector(CockpitGlassDamage.Chip(level, i, seed, s));
		var cracks = new Vector4[CockpitGlassDamage.MaxCracks];
		for (int i = 0; i < cracks.Length; i++)
			cracks[i] = ToVector(CockpitGlassDamage.Crack(level, i, seed, s));
		var stains = new Vector4[CockpitGlassDamage.MaxStains];
		for (int i = 0; i < stains.Length; i++)
			stains[i] = ToVector(CockpitGlassDamage.Stain(level, i, seed, s));
		pass.SetShaderParameter(ChipParam, chips);
		pass.SetShaderParameter(CrackParam, cracks);
		pass.SetShaderParameter(StainParam, stains);
		pass.SetShaderParameter(CrackOpacityParam, s.CrackOpacity);
		pass.SetShaderParameter(StainOpacityParam, s.StainOpacity);
	}

	private static Vector4 ToVector(GlassMark m) => new(m.X, m.Y, m.Size, m.Seed);
}
