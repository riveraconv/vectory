using System;
using Godot;
using Vectory.UI;

namespace Vectory.Ship;

// Pinta un lienzo de calcomanía (HullDecalDrawing) una sola vez en una textura: un SubViewport que se dibuja un frame
// y se borra. Sin reglas: solo traduce primitivas a llamadas de Godot.
public static partial class HullDecalPainter
{
	private const int OrbitronBlack = 900;
	// Orbitron no tiene cursiva: se inclina como hace el navegador con una fuente sin cursiva (unos 12°)
	private static readonly float ItalicSkew = Mathf.Tan(Mathf.DegToRad(12f));

	public static async void Paint(Node host, DecalCanvas canvas, Action<Texture2D> done)
	{
		var viewport = new SubViewport
		{
			Name = "DecalPainter",
			Size = new Vector2I(canvas.Width, canvas.Height),
			TransparentBg = true,
			Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once
		};
		viewport.AddChild(new DecalCanvasControl(canvas));
		host.AddChild(viewport);
		await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		if (!GodotObject.IsInstanceValid(viewport))
			return;
		Image image = viewport.GetTexture().GetImage();
		viewport.QueueFree();
		image.GenerateMipmaps();
		done(ImageTexture.CreateFromImage(image));
	}

	public static Color ToColor(uint rgb, float alpha = 1f) =>
		new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

	private static Font FontFor(float size, bool italic)
	{
		var font = (FontVariation)MenuFonts.Make(MenuFonts.Orbitron, OrbitronBlack, size, 0f);
		if (italic)
			font.VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-ItalicSkew, 1f), Vector2.Zero);
		return font;
	}

	private sealed partial class DecalCanvasControl : Control
	{
		private readonly DecalCanvas _canvas;

		public DecalCanvasControl(DecalCanvas canvas)
		{
			_canvas = canvas;
			Size = new Vector2(canvas.Width, canvas.Height);
		}

		public override void _Draw()
		{
			foreach (DecalPrimitive p in _canvas.Primitives)
			{
				Color color = ToColor(p.Color);
				switch (p.Shape)
				{
					case DecalShape.Rect:
						DrawRect(new Rect2(p.X, p.Y, p.W, p.H), color);
						break;
					case DecalShape.RoundRect:
						var box = new StyleBoxFlat { BgColor = color, AntiAliasing = true, CornerDetail = 16 };
						box.SetCornerRadiusAll(Mathf.RoundToInt(p.Radius));
						DrawStyleBox(box, new Rect2(p.X, p.Y, p.W, p.H));
						break;
					case DecalShape.Circle:
						DrawCircle(new Vector2(p.X, p.Y), p.Radius, color, true, -1f, true);
						break;
					case DecalShape.Text:
						DrawCenteredText(p, color);
						break;
				}
			}
		}

		// Centrado en X como textAlign = 'center'; si no cabe en MaxWidth, se estrecha como fillText
		private void DrawCenteredText(DecalPrimitive p, Color color)
		{
			int size = Mathf.RoundToInt(p.FontSize);
			Font font = FontFor(p.FontSize, p.Italic);
			float width = font.GetStringSize(p.Text, HorizontalAlignment.Left, -1f, size).X;
			float scale = p.MaxWidth > 0f && width > p.MaxWidth ? p.MaxWidth / width : 1f;
			float baseline = p.Baseline == DecalBaseline.Middle
				? p.Y + (font.GetAscent(size) - font.GetDescent(size)) / 2f
				: p.Y;
			DrawSetTransform(new Vector2(p.X - width * scale / 2f, baseline), 0f, new Vector2(scale, 1f));
			DrawString(font, Vector2.Zero, p.Text, HorizontalAlignment.Left, -1f, size, color);
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		}
	}
}
