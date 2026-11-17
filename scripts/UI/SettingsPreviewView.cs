using System;
using Godot;
using Vectory.Core;
using Vectory.Settings;

namespace Vectory.UI;

// La vista previa de SETTINGS (spec docs/settings.md, 3.4; designs/settings.html, preview y trackPreview): un recuadro
// con PREVIEW y, según la fila elegida, las tres barras de la dificultad, la pantalla del modo y la resolución, la tecla
// en grande con la lista de todas, o una pista en perspectiva con una nave rival que cambia con BLOOM, SPEED LINES,
// SPEED FOV y COCKPIT SHAKE. En 2D, sin escena 3D. Solo lee el modelo. Medidas sobre 1280×720 × la escala.
public partial class SettingsPreviewView : Control
{
	private float _scale = 1f;
	private SettingsModel _model;
	private SettingDef _def;
	private Font _label, _orbitron, _orbitronBold, _mono;
	private float _time;

	private float S(float px) => px * _scale;

	public void Setup(float scale)
	{
		_scale = scale;
		_label = MenuFonts.Make(MenuFonts.Mono, 400, S(11f), 0.2f);
		_mono = MenuFonts.Make(MenuFonts.Mono, 400, S(12f), 0.16f);
		_orbitron = MenuFonts.Make(MenuFonts.Orbitron, 500, S(14f), 0.14f);
		_orbitronBold = MenuFonts.Make(MenuFonts.Orbitron, 700, S(40f), 0.02f);
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
	}

	// def: la fila elegida (null en RESET DEFAULTS y BACK: sin vista previa)
	public void Show(SettingsModel model, SettingDef def)
	{
		_model = model;
		_def = def;
		Visible = def != null;
		QueueRedraw();
	}

	// Con vibración, la pista tiembla: se redibuja cada frame
	public override void _Process(double delta)
	{
		if (!Visible || _model == null || !ShowsTrack || _model.CockpitShake <= 0f)
			return;
		_time += (float)delta;
		QueueRedraw();
	}

	private bool ShowsTrack => _def != null && !_def.ComingSoon && (_def.Group == SettingsCatalog.Effects || _def.Id == SettingsCatalog.Bloom);

	public override void _Draw()
	{
		if (_def == null || _model == null)
			return;
		DrawRect(new Rect2(Vector2.Zero, Size), Palette.SettingsPreviewBackground);

		if (_def.ComingSoon)
			Centered(_label, "COMING SOON", Size * 0.5f, Mathf.RoundToInt(S(14f)), Palette.SettingsComingSoon);
		else if (_def.Id == SettingsCatalog.Difficulty)
			DrawDifficulty();
		else if (_def.Id is SettingsCatalog.DisplayMode or SettingsCatalog.Resolution)
			DrawDisplay();
		else if (_def.Kind == SettingKind.Key)
			DrawKeys();
		else if (ShowsTrack)
			DrawTrack();

		DrawRect(new Rect2(Vector2.Zero, Size), Palette.SettingsPreviewBorder, false, Mathf.Max(1f, S(1f)));
		DrawString(_label, new Vector2(S(12f), S(9f) + S(11f)), "PREVIEW", HorizontalAlignment.Left, -1, Mathf.RoundToInt(S(11f)),
			Palette.SettingsPreviewLabel);
	}

	private void DrawDifficulty()
	{
		int chosen = _model.Get(SettingsCatalog.Difficulty);
		float w = Size.X, h = Size.Y;
		for (int k = 0; k < 3; k++)
		{
			bool on = k <= chosen;
			float cx = w / 2f + (k - 1) * S(170f);
			float barHeight = S(50f + k * 30f);
			float bottom = h / 2f - S(70f) + S(110f);
			var bar = new Rect2(cx - S(22f), bottom - barHeight, S(44f), barHeight);
			if (on)
			{
				DrawRect(bar.Grow(S(4f)), new Color(Palette.SettingsPreviewGlow, 0.3f));
				DrawRect(bar, new Color(Palette.Cyan, 0.85f));
			}
			else
			{
				DrawRect(bar, Palette.SettingsPreviewBorder, false, Mathf.Max(1f, S(1f)));
			}
			Color text = k == chosen ? Palette.Cyan : Palette.BlueGray;
			Centered(_orbitron, SettingsCatalog.Difficulties[k], new Vector2(cx, bottom + S(24f)), Mathf.RoundToInt(S(14f)), text);
		}
		Centered(_mono, "RIVALS: PACE · MISTAKES · ATTACK · DEFENCE", new Vector2(w / 2f, h - S(28f)), Mathf.RoundToInt(S(12f)),
			Palette.SettingsPreviewDim);
	}

	private void DrawDisplay()
	{
		int mode = _model.Get(SettingsCatalog.DisplayMode);
		string resolution = SettingsCatalog.Resolutions[_model.Get(SettingsCatalog.Resolution)];
		float w = Size.X, h = Size.Y;
		DrawRect(new Rect2((w - S(460f)) / 2f, (h - S(270f)) / 2f, S(460f), S(270f)), new Color(Palette.Blue, 0.5f), false, Mathf.Max(1f, S(1f)));
		bool windowed = mode == 0;
		float sw = S(windowed ? 300f : 440f), sh = S(windowed ? 170f : 248f);
		var screen = new Rect2((w - sw) / 2f, (h - sh) / 2f, sw, sh);
		DrawRect(screen.Grow(S(3f)), new Color(Palette.SettingsPreviewGlow, 0.25f), false, S(4f));
		DrawRect(screen, Palette.Cyan, false, Mathf.Max(1f, S(1f)));
		if (windowed)
			DrawLine(screen.Position + new Vector2(0f, S(16f)), screen.Position + new Vector2(sw, S(16f)), new Color(Palette.Cyan, 0.5f), Mathf.Max(1f, S(1f)));
		Vector2 c = screen.GetCenter();
		Centered(MenuFonts.Make(MenuFonts.Orbitron, 500, S(22f), 0.1f), resolution, c + new Vector2(0f, -S(6f)), Mathf.RoundToInt(S(22f)), Palette.SettingsHighlight);
		Centered(_mono, SettingsCatalog.DisplayModes[mode], c + new Vector2(0f, S(22f)), Mathf.RoundToInt(S(12f)), Palette.BlueGray);
	}

	private void DrawKeys()
	{
		float h = Size.Y;
		string key = _def.Values[0];
		var box = new Rect2(S(60f), h / 2f - S(50f), S(150f), S(100f));
		DrawRect(box.Grow(S(4f)), new Color(Palette.SettingsPreviewGlow, 0.3f), false, S(6f));
		DrawRect(box, Palette.Cyan, false, S(2f));
		int size = Mathf.RoundToInt(S(key.Length > 2 ? 24f : 40f));
		Centered(MenuFonts.Make(MenuFonts.Orbitron, 700, size, 0.02f), key, box.GetCenter(), size, Palette.SettingsHighlight);

		float y = h / 2f - S(118f);
		int listSize = Mathf.RoundToInt(S(13f));
		Font font = MenuFonts.Make(MenuFonts.Mono, 400, listSize, 0.14f);
		foreach (SettingDef def in SettingsCatalog.All)
		{
			if (def.Kind != SettingKind.Key)
				continue;
			Color color = def.Id == _def.Id ? Palette.Cyan : Palette.SettingsPreviewDim;
			y += S(26f);
			DrawString(font, new Vector2(S(300f), y), def.Label, HorizontalAlignment.Left, -1, listSize, color);
			DrawString(font, new Vector2(S(300f), y), def.Values[0], HorizontalAlignment.Right, S(250f), listSize, color);
		}
	}

	// Pista en perspectiva con una nave rival delante (trackPreview)
	private void DrawTrack()
	{
		float w = Size.X, h = Size.Y;
		float bloom = _model.Bloom, fov = _model.SpeedFov, shake = _model.CockpitShake;
		var offset = Vector2.Zero;
		if (shake > 0f)
		{
			float amp = S(2.2f) * shake;
			offset = new Vector2(MathF.Sin(_time * 71f) * amp, MathF.Cos(_time * 53f) * amp * 0.8f);
		}
		float vx = w / 2f + offset.X, vy = S(120f - fov * 18f) + offset.Y, spread = 1f + fov * 0.22f;
		float bottom = h + offset.Y;
		float Bx(float k) => vx + S(k * 310f) * spread;

		void Line(Vector2 a, Vector2 b, Color color, float width)
		{
			if (bloom > 0f)
				DrawLine(a, b, new Color(color, color.A * 0.35f * bloom), width + S(6f) * bloom, true);
			DrawLine(a, b, new Color(color, color.A * (0.75f + 0.25f * bloom)), width, true);
		}

		Line(new Vector2(vx - S(6f), vy), new Vector2(Bx(-1f), bottom), Palette.Cyan, S(2f));
		Line(new Vector2(vx + S(6f), vy), new Vector2(Bx(1f), bottom), Palette.Cyan, S(2f));
		Line(new Vector2(vx - S(2f), vy), new Vector2(vx - S(110f) * spread, bottom), Palette.SettingsPreviewLane, S(1.4f));
		Line(new Vector2(vx + S(2f), vy), new Vector2(vx + S(110f) * spread, bottom), Palette.SettingsPreviewLane, S(1.4f));
		for (float t = 0.08f; t < 1f; t += 0.13f)
		{
			float yy = vy + (bottom - vy) * t * t;
			float half = S(6f) + (S(310f) * spread - S(6f)) * t * t;
			Line(new Vector2(vx - half, yy), new Vector2(vx + half, yy), new Color(Palette.Cyan, 0.25f + 0.5f * t), S(1.4f));
		}
		Line(new Vector2(0f, vy), new Vector2(w, vy), Palette.SettingsPreviewHorizon, S(1.4f));

		// Nave rival delante
		var rival = new[] { new Vector2(vx - S(26f), vy + S(70f)), new Vector2(vx + S(26f), vy + S(70f)), new Vector2(vx, vy + S(50f)), new Vector2(vx - S(26f), vy + S(70f)) };
		if (bloom > 0f)
			DrawPolyline(rival, new Color(Palette.SettingsPreviewRival, 0.35f * bloom), S(1.6f) + S(6f) * bloom, true);
		DrawPolyline(rival, Palette.SettingsPreviewRival, S(1.6f), true);

		// Líneas de velocidad (las mismas de la maqueta: semilla fija)
		if (_model.SpeedLines)
		{
			uint seed = 7;
			float Rnd()
			{
				seed = (uint)((seed * 16807UL) % 2147483647UL);
				return seed / 2147483647f;
			}
			for (int k = 0; k < 26; k++)
			{
				float a = Rnd() * MathF.PI * 2f, r0 = S(150f + Rnd() * 90f), len = S(30f + Rnd() * 50f);
				var p1 = new Vector2(vx + MathF.Cos(a) * r0, h / 2f + offset.Y + MathF.Sin(a) * r0 * 0.6f);
				var p2 = new Vector2(vx + MathF.Cos(a) * (r0 + len), h / 2f + offset.Y + MathF.Sin(a) * (r0 + len) * 0.6f);
				DrawLine(p1, p2, Palette.SettingsPreviewSpeedLine, Mathf.Max(1f, S(1f)), true);
			}
		}
	}

	private void Centered(Font font, string text, Vector2 center, int size, Color color)
	{
		Vector2 measure = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
		DrawString(font, new Vector2(center.X - measure.X / 2f, center.Y + size * 0.36f), text, HorizontalAlignment.Left, -1, size, color);
	}
}
