using System;
using Godot;
using Vectory.UI;

namespace Vectory.Ship;

// Pinta un lienzo de calcomanía (HullDecalDrawing) una sola vez en una textura: un SubViewport que se dibuja un frame
// y se borra. Sin reglas: solo traduce primitivas a llamadas de Godot. Lo que vacía (Erase) va en un nodo aparte con
// mezcla de resta, que quita también el alfa de lo que hay debajo.
// Las texturas se guardan por contenido (DecalCanvas.Key): un lienzo ya pintado se entrega al momento, así cambiar de
// pintura en la pantalla PAINT no repite el trabajo; y como mucho se empiezan dos lienzos nuevos por frame.
public static partial class HullDecalPainter
{
	private const int OrbitronBlack = 900;
	// Orbitron no tiene cursiva: se inclina como hace el navegador con una fuente sin cursiva (unos 12°)
	private static readonly float ItalicSkew = Mathf.Tan(Mathf.DegToRad(12f));

	// Texturas ya pintadas, por clave del lienzo; las más antiguas se olvidan pasado el límite (unos 1,3 MB cada una)
	private const int CacheLimit = 48;
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Cache = new();
	private static readonly System.Collections.Generic.Queue<string> CacheOrder = new();
	// Lienzos que se están pintando, con quién espera cada uno
	private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(Node Host, Action<Texture2D> Done)>> Pending = new();

	public static void Paint(Node host, DecalCanvas canvas, Action<Texture2D> done)
	{
		string key = canvas.Key();
		if (Cache.TryGetValue(key, out Texture2D cached))
		{
			done(cached);
			return;
		}
		if (Pending.TryGetValue(key, out var waiting))
		{
			waiting.Add((host, done));
			return;
		}
		Pending[key] = new() { (host, done) };
		Render(host, canvas, key);
	}

	// Calcomanías nuevas que se empiezan a pintar en un mismo frame: el resto espera al siguiente, así una pintura con
	// cinco lienzos (las libreas de VECTOR) no carga todo su trabajo en un solo frame
	private const int MaxRendersPerFrame = 2;
	private static ulong _renderFrame;
	private static int _rendersThisFrame;

	private static async void Render(Node host, DecalCanvas canvas, string key)
	{
		while (true)
		{
			ulong frame = Engine.GetProcessFrames();
			if (frame != _renderFrame)
			{
				_renderFrame = frame;
				_rendersThisFrame = 0;
			}
			if (_rendersThisFrame < MaxRendersPerFrame)
				break;
			await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!GodotObject.IsInstanceValid(host))
			{
				RetryOrDrop(canvas, key);
				return;
			}
		}
		_rendersThisFrame++;

		var viewport = new SubViewport
		{
			Name = "DecalPainter",
			Size = new Vector2I(canvas.Width, canvas.Height),
			TransparentBg = true,
			Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once
		};
		foreach (DecalCanvasControl control in Controls(canvas))
			viewport.AddChild(control);
		host.AddChild(viewport);
		await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		if (!GodotObject.IsInstanceValid(viewport))
		{
			RetryOrDrop(canvas, key);
			return;
		}

		// Lectura síncrona: espera a la GPU, pero da exactamente la imagen de siempre. Con la caché y el reparto por frame
		// no llega a dar frames lentos. (Se probó TextureGetDataAsync: menos espera, pero el casco de HAMMER salía con
		// 62 píxeles distintos sin causa clara.)
		Finish(key, viewport.GetTexture().GetImage());
		viewport.QueueFree();
	}

	// El casco que lo pintaba ya no existe: lo pinta otro de los que esperan o, si no queda ninguno, se olvida (el
	// siguiente que lo pida lo vuelve a pintar)
	private static void RetryOrDrop(DecalCanvas canvas, string key)
	{
		if (!Pending.TryGetValue(key, out var waiting))
			return;
		waiting.RemoveAll(w => !GodotObject.IsInstanceValid(w.Host));
		if (waiting.Count == 0)
			Pending.Remove(key);
		else
			Render(waiting[0].Host, canvas, key);
	}

	private static void Finish(string key, Image image)
	{
		image.GenerateMipmaps();
		Texture2D texture = ImageTexture.CreateFromImage(image);
		Cache[key] = texture;
		CacheOrder.Enqueue(key);
		while (CacheOrder.Count > CacheLimit)
			Cache.Remove(CacheOrder.Dequeue());

		if (!Pending.Remove(key, out var waiting))
			return;
		foreach ((Node host, Action<Texture2D> done) in waiting)
		{
			if (GodotObject.IsInstanceValid(host))
				done(texture);
		}
	}

	public static Color ToColor(uint rgb, float alpha = 1f) =>
		new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

	// Negrita fingida del lienzo con Share Tech Mono, que no tiene negrita (la de los pilotos y pantallas de BLADE)
	private const float BoldEmbolden = 0.6f;

	private static Font FontFor(float size, bool italic, DecalFont which, bool bold)
	{
		var font = which switch
		{
			DecalFont.BlackOps => (FontVariation)MenuFonts.Make(MenuFonts.BlackOps, 400, size, 0f),
			DecalFont.ShareTech => (FontVariation)MenuFonts.Make(MenuFonts.Mono, 400, size, 0f),
			_ => (FontVariation)MenuFonts.Make(MenuFonts.Orbitron, OrbitronBlack, size, 0f)
		};
		if (italic)
			font.VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-ItalicSkew, 1f), Vector2.Zero);
		if (bold)
			font.VariationEmbolden = BoldEmbolden;
		return font;
	}

	// Las primitivas en orden: un nodo con todas las seguidas que pintan y uno propio para cada una que vacía
	private static System.Collections.Generic.List<DecalCanvasControl> Controls(DecalCanvas canvas)
	{
		var controls = new System.Collections.Generic.List<DecalCanvasControl>();
		var run = new System.Collections.Generic.List<DecalPrimitive>();
		foreach (DecalPrimitive p in canvas.Primitives)
		{
			if (!p.Erase)
			{
				run.Add(p);
				continue;
			}
			if (run.Count > 0)
				controls.Add(new DecalCanvasControl(canvas, run.ToArray(), false));
			run.Clear();
			controls.Add(new DecalCanvasControl(canvas, new[] { p }, true));
		}
		if (run.Count > 0 || controls.Count == 0)
			controls.Add(new DecalCanvasControl(canvas, run.ToArray(), false));
		return controls;
	}

	private sealed partial class DecalCanvasControl : Control
	{
		private readonly DecalPrimitive[] _primitives;
		private readonly bool _erase;

		public DecalCanvasControl(DecalCanvas canvas, DecalPrimitive[] primitives, bool erase)
		{
			_primitives = primitives;
			_erase = erase;
			Size = new Vector2(canvas.Width, canvas.Height);
			// Resta: el color y el alfa de debajo bajan lo que se pinte en blanco opaco, hasta 0
			if (erase)
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Sub };
		}

		public override void _Draw()
		{
			foreach (DecalPrimitive p in _primitives)
			{
				Color color = _erase ? Colors.White : ToColor(p.Color);
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
					case DecalShape.Ring:
						DrawArc(new Vector2(p.X, p.Y), p.Radius, 0f, Mathf.Tau, 128, color, p.W, true);
						break;
					case DecalShape.Polygon:
						var points = new Vector2[p.Points.Length];
						for (int i = 0; i < points.Length; i++)
							points[i] = new Vector2(p.Points[i].X, p.Points[i].Y);
						DrawColoredPolygon(points, color);
						break;
					case DecalShape.Outline:
						DrawOutline(p, color);
						break;
				}
			}
		}

		// Polilínea cerrada con su grueso; con Radius > 0, un círculo en cada esquina hace la unión redondeada
		private void DrawOutline(DecalPrimitive p, Color color)
		{
			var points = new Vector2[p.Points.Length + 1];
			for (int i = 0; i < p.Points.Length; i++)
				points[i] = new Vector2(p.Points[i].X, p.Points[i].Y);
			points[^1] = points[0];
			DrawPolyline(points, color, p.W, true);
			if (p.Radius > 0f)
			{
				for (int i = 0; i < p.Points.Length; i++)
					DrawCircle(points[i], p.W / 2f, color, true, -1f, true);
			}
		}

		// Centrado en X como textAlign = 'center' (o desde X con 'left'); si no cabe en MaxWidth, se estrecha como fillText
		private void DrawCenteredText(DecalPrimitive p, Color color)
		{
			int size = Mathf.RoundToInt(p.FontSize);
			Font font = FontFor(p.FontSize, p.Italic, p.Font, p.Bold);
			float width = font.GetStringSize(p.Text, HorizontalAlignment.Left, -1f, size).X;
			float scale = p.MaxWidth > 0f && width > p.MaxWidth ? p.MaxWidth / width : 1f;
			float baseline = p.Baseline == DecalBaseline.Middle
				? p.Y + (font.GetAscent(size) - font.GetDescent(size)) / 2f
				: p.Y;
			float left = p.Align == DecalAlign.Left ? p.X : p.X - width * scale / 2f;
			DrawSetTransform(new Vector2(left, baseline), 0f, new Vector2(scale, 1f));
			DrawString(font, Vector2.Zero, p.Text, HorizontalAlignment.Left, -1f, size, color);
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		}
	}
}
