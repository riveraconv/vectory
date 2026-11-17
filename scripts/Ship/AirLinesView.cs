using System.Collections.Generic;
using Godot;
using Vectory.Core;

namespace Vectory.Ship;

// Las líneas de aire del rebufo en la cabina (spec docs/rivales-4-personalidades.md, 4.2): hijo de la cabina, en el ojo.
// Pinta lo que da AirLines como líneas finas, sin luz y en suma, en cian. La cabina 3D va encima del mundo, así que se
// ven por el cristal y la tapan los pilares. Solo en la vista de cabina y con rebufo. Sin reglas.
public partial class AirLinesView : MeshInstance3D
{
	private ImmediateMesh _mesh;
	private StandardMaterial3D _material;
	private bool _empty = true;

	public override void _Ready()
	{
		_mesh = new ImmediateMesh();
		Mesh = _mesh;
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		CastShadow = ShadowCastingSetting.Off;
		Visible = false;
	}

	// shown: vista de cabina; strength: r del rebufo; speed: m/s; time: s que lleva corriendo
	public void UpdateLines(bool shown, float strength, float speed, float time, in AirLineSettings settings)
	{
		if (_mesh == null)
			return;
		bool visible = shown && strength > 0f;
		Visible = visible;
		if (!visible)
		{
			if (!_empty)
			{
				_mesh.ClearSurfaces();
				_empty = true;
			}
			return;
		}

		List<AirLineSegment> segments = AirLines.Segments(strength, speed, time, settings);
		_mesh.ClearSurfaces();
		_empty = segments.Count == 0;
		if (_empty)
			return;

		_mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _material);
		Color color = Palette.SlipstreamAirLine;
		foreach (AirLineSegment segment in segments)
		{
			// En suma: el brillo multiplica el color
			var c = new Color(color.R * segment.Brightness, color.G * segment.Brightness, color.B * segment.Brightness, 1f);
			_mesh.SurfaceSetColor(c);
			_mesh.SurfaceAddVertex(new Vector3(segment.A.X, segment.A.Y, segment.A.Z));
			_mesh.SurfaceSetColor(c);
			_mesh.SurfaceAddVertex(new Vector3(segment.B.X, segment.B.Y, segment.B.Z));
		}
		_mesh.SurfaceEnd();
	}
}
