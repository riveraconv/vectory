using Godot;

namespace Vectory.Ship;

// Material de líneas con parpadeo por arista (daños): pinta el color de los vértices tal cual, como el
// material normal de la nave, y baja la opacidad de las aristas apagadas. Cada vértice lleva en UV.x el
// número de su arista; una textura de 1 píxel de alto por arista dice cuáles están apagadas (1) y se
// actualiza solo cuando cambia algo, sin rehacer la malla.
// La "opacidad" se aplica multiplicando el color (sobre el fondo oscuro se ve igual y el glow baja con él):
// con ALPHA el material pasa a la cola de transparentes y toda la nave pierde glow, no solo esas aristas.
// La usan Spaceship y Cockpit; la lógica del parpadeo vive en EdgeFlicker.
public class EdgeFlickerMaterial
{
	private const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;

uniform sampler2D edge_state : filter_nearest, repeat_disable;
uniform float dim_opacity = 0.2;

void fragment() {
	float dimmed = texelFetch(edge_state, ivec2(int(UV.x + 0.5), 0), 0).r;
	ALBEDO = COLOR.rgb * mix(1.0, dim_opacity, step(0.5, dimmed));
}
";

	private static Shader _shader;

	private readonly Image _image;
	private readonly ImageTexture _texture;

	public EdgeFlickerMaterial(int edgeCount)
	{
		_shader ??= new Shader { Code = ShaderCode };

		_image = Image.CreateEmpty(Mathf.Max(1, edgeCount), 1, false, Image.Format.R8);
		_image.Fill(Colors.Black);
		_texture = ImageTexture.CreateFromImage(_image);

		Material = new ShaderMaterial { Shader = _shader };
		Material.SetShaderParameter("edge_state", _texture);
	}

	public ShaderMaterial Material { get; }

	// Opacidad de una arista apagada (0–1)
	public void SetDimOpacity(float opacity)
	{
		Material.SetShaderParameter("dim_opacity", Mathf.Clamp(opacity, 0f, 1f));
	}

	// Copia a la textura qué aristas están apagadas ahora
	public void Apply(EdgeFlicker flicker)
	{
		int count = Mathf.Min(flicker.EdgeCount, _image.GetWidth());
		for (int i = 0; i < count; i++)
			_image.SetPixel(i, 0, flicker.IsDimmed(i) ? Colors.White : Colors.Black);
		_texture.Update(_image);
	}
}
