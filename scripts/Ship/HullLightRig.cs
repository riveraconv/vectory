using Godot;

namespace Vectory.Ship;

// Las tres luces direccionales de los cascos 3D (principal con sombra, contraluz y relleno, las del prototipo), que solo
// tocan una capa visual y solo proyectan la sombra de esa capa. Cada casco del jugador y del holograma lleva las suyas;
// los rivales comparten un solo juego en su propia capa (spec docs/rivales-1-parrilla.md), porque Godot tiene un tope de
// luces direccionales y, en la misma capa, las de cada casco iluminarían todos los demás.
public partial class HullLightRig : Node3D
{
	private const float ShadowMaxDistance = 40f;

	// Desde dónde llegan (hacia el centro de la nave) y color
	private static readonly Vector3 KeySource = new(4f, 10f, -5f);
	private static readonly Vector3 RimSource = new(-6f, 2.5f, 9f);
	private static readonly Vector3 FillSource = new(8f, 3f, 2f);
	private static readonly Color KeyColor = Color.FromHtml("#FFF1E0");
	private static readonly Color RimColor = Color.FromHtml("#FF9D50");
	private static readonly Color FillColor = Color.FromHtml("#5F8CFF");

	private DirectionalLight3D _key;
	private DirectionalLight3D _rim;
	private DirectionalLight3D _fill;
	private Hull3DSettings _settings = new();

	// Capa visual que iluminan (y cuya sombra proyectan)
	public uint Layer { get; set; } = ShipHull3D.HullLayer;

	// Colgadas de su padre (giran con la nave) o fijas en el mundo, giradas media vuelta (holograma). Antes de _Ready.
	public bool FollowParent { get; set; } = true;

	public override void _Ready()
	{
		_key = NewLight("Key", KeySource, KeyColor, true);
		_rim = NewLight("Rim", RimSource, RimColor, false);
		_fill = NewLight("Fill", FillSource, FillColor, false);
		Apply(_settings);
	}

	public void Apply(Hull3DSettings settings)
	{
		_settings = settings;
		if (_key == null)
			return;
		_key.LightEnergy = settings.KeyEnergy;
		_rim.LightEnergy = settings.RimEnergy;
		_fill.LightEnergy = settings.FillEnergy;
	}

	private DirectionalLight3D NewLight(string name, Vector3 source, Color color, bool shadow)
	{
		var light = new DirectionalLight3D
		{
			Name = name,
			LightColor = color,
			LightCullMask = Layer,
			ShadowCasterMask = Layer,
			ShadowEnabled = shadow,
			ShadowBlur = 1.5f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
			DirectionalShadowMaxDistance = ShadowMaxDistance
		};
		// Fijas en el mundo: no siguen el giro de la nave. La cámara del holograma mira hacia −Z, así que el juego de luces
		// del prototipo se gira media vuelta para que la principal llegue del lado de la cámara
		light.TopLevel = !FollowParent;
		if (!FollowParent)
			source = new Vector3(-source.X, source.Y, -source.Z);
		AddChild(light);
		// Mira desde la fuente hacia el centro, en el espacio de la nave (o del mundo, si no la sigue)
		light.Transform = Transform3D.Identity.LookingAt(-source, Vector3.Up);
		return light;
	}
}
