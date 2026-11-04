using System;
using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Piezas comunes de las pantallas de selección (SELECT CIRCUIT y SELECT SHIP): textos, panel de la derecha con su
// línea y su halo, botones ‹ ›, etiquetas con borde y pie. Medidas sobre 1280×720, multiplicadas por la escala.
public sealed class SelectScreenKit
{
	public const float NameX = 72f;
	public const float NameY = 56f;
	public const float PanelX = 836f;

	private const float PanelTop = 52f;
	private const float PanelLeft = 40f;
	private const float PanelRight = 48f;
	private const float PanelBottom = 40f;
	private const float FooterX = 300f;

	private readonly float _scale;

	public SelectScreenKit(float scale)
	{
		_scale = scale;
	}

	public float S(float referencePx) => referencePx * _scale;

	public int Px(float referencePx) => Mathf.RoundToInt(S(referencePx));

	// Ancho disponible dentro del panel, en píxeles finales
	public float PanelContentWidth => S(1280f - PanelX - PanelLeft - PanelRight);

	public LabelSettings TextSettings(FontFile file, int weight, float size, float letterSpacingEm, Color color,
		Color? glow = null, float glowSize = 0f)
	{
		int px = Px(size);
		return MenuFonts.Settings(MenuFonts.Make(file, weight, px, letterSpacingEm), px, color, glow ?? Colors.Transparent, S(glowSize));
	}

	// Texto con su fuente (peso y espaciado en em), tamaño (px sobre 1280×720), color y halo opcional
	public Label MakeLabel(string text, FontFile file, int weight, float size, float letterSpacingEm,
		Color color, Color? glow = null, float glowSize = 0f)
	{
		return new Label
		{
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = TextSettings(file, weight, size, letterSpacingEm, color, glow, glowSize)
		};
	}

	// Cabecera de una sección del panel
	public Label SectionHeader(string text) => MakeLabel(text, MenuFonts.Mono, 400, 13f, 0.22f, Palette.SelectHeader);

	// Columna del bloque del nombre, arriba a la izquierda
	public VBoxContainer NameColumn(Control parent)
	{
		var column = new VBoxContainer { Name = "NameBlock", MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", Px(10f));
		column.Position = new Vector2(S(NameX), S(NameY));
		parent.AddChild(column);
		return column;
	}

	// Etiqueta con borde de 1 px, a la derecha del nombre (TEST, COMING SOON)
	public Control Tag(string text, Color textColor, Color borderColor)
	{
		var box = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = borderColor,
			ContentMarginLeft = S(8f),
			ContentMarginRight = S(8f),
			ContentMarginTop = S(3f),
			ContentMarginBottom = S(3f)
		};
		box.SetBorderWidthAll(Mathf.Max(1, Px(1f)));

		var tag = new PanelContainer { Name = "Tag", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		tag.AddThemeStyleboxOverride("panel", box);
		Label label = MakeLabel(text, MenuFonts.Mono, 400, 13f, 0.22f, textColor);
		label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		tag.AddChild(label);
		return tag;
	}

	// Botón ‹ / › de 44×44 con borde azul de 1 px
	public Control NavButton(string arrow, Action clicked, LabelSettings settings, out Label label)
	{
		var box = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Palette.SelectNavBorder };
		box.SetBorderWidthAll(Mathf.Max(1, Px(1f)));

		var button = new PanelContainer
		{
			CustomMinimumSize = new Vector2(S(44f), S(44f)),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		button.AddThemeStyleboxOverride("panel", box);
		button.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
				clicked();
		};

		label = new Label
		{
			Text = arrow,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = settings
		};
		button.AddChild(label);
		return button;
	}

	// Panel de la derecha (desde x = 836 hasta el borde): fondo, línea cian con halo hacia la izquierda y márgenes.
	// Devuelve la columna donde va su contenido.
	public VBoxContainer Panel(Control parent, string name)
	{
		var panel = new Control
		{
			Name = name,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = S(PanelX)
		};
		parent.AddChild(panel);

		var background = new ColorRect { Color = Palette.SelectPanel, MouseFilter = Control.MouseFilterEnum.Ignore };
		background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		panel.AddChild(background);

		// Halo de la línea, hacia la izquierda
		var fade = new Gradient();
		fade.SetColor(0, new Color(Palette.SelectPanelGlow, 0f));
		fade.SetColor(1, Palette.SelectPanelGlow);
		panel.AddChild(new TextureRect
		{
			Texture = new GradientTexture2D { Gradient = fade, Width = 64, Height = 1 },
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = -S(18f),
			OffsetRight = 0f
		});
		panel.AddChild(new ColorRect
		{
			Color = Palette.SelectPanelLine,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetRight = Mathf.Max(1f, S(1f))
		});

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_top", Px(PanelTop));
		margin.AddThemeConstantOverride("margin_left", Px(PanelLeft));
		margin.AddThemeConstantOverride("margin_right", Px(PanelRight));
		margin.AddThemeConstantOverride("margin_bottom", Px(PanelBottom));
		panel.AddChild(margin);

		var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		content.AddThemeConstantOverride("separation", Px(8f));
		margin.AddChild(content);
		return content;
	}

	// Lista de opciones con el estilo de las pantallas de selección, sin COMING SOON en las desactivadas
	public OptionList Options(int fontSize, Action<string> chosen)
	{
		OptionListStyle style = OptionListStyle.Scaled(_scale, fontSize, 44f, 4f, new Vector2(4f, 30f), 16f);
		style.HideDisabledHint = true;
		var options = new OptionList { Name = "OptionList" };
		options.Setup(style);
		options.Chosen += chosen;
		return options;
	}

	// Pie, a la derecha de la versión
	public void Footer(Control parent, string text)
	{
		Label hint = MakeLabel(text, MenuFonts.Mono, 400, 13f, 0.18f, Palette.SelectFooter);
		hint.AnchorLeft = 0f;
		hint.AnchorRight = 0f;
		hint.AnchorTop = 1f;
		hint.AnchorBottom = 1f;
		hint.OffsetLeft = S(FooterX);
		hint.OffsetTop = -S(44f);
		hint.OffsetBottom = -S(44f);
		hint.GrowVertical = Control.GrowDirection.Begin;
		parent.AddChild(hint);
	}
}
