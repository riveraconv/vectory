using System;
using System.Collections.Generic;
using System.Numerics;

namespace Vectory.Ship;

// Lo que da el juego para el HUD de cabina
public struct CockpitHudInput
{
	public float SpeedKmh;
	public float TopKmh;           // punta base de la nave, sin daño
	public float MaxKmh;           // punta + impulso del boost
	public bool BoostActive;
	public int Charges;
	public bool Cooldown;
	public int DamageLevel;        // 0–3
	public bool DamageEnabled;     // false en TIME TRIAL
}

// Intensidad de cada efecto de luz: multiplica el valor del diseño. A 0, se apaga.
public struct CockpitLightSettings
{
	public float BoostTint;
	public float DamageTint;
	public float ScreenGlow;

	public static CockpitLightSettings Default => new() { BoostTint = 1f, DamageTint = 1f, ScreenGlow = 1f };
}

// Color o valor que pasa a otro en un tiempo fijo cuando cambia su objetivo
public sealed class CockpitFade
{
	private readonly float _duration;
	private Vector4 _from;
	private Vector4 _target;
	private float _time;
	private bool _started;

	public CockpitFade(float duration)
	{
		_duration = duration;
	}

	public Vector4 Value { get; private set; }

	public void Update(Vector4 target, float dt)
	{
		if (!_started)
		{
			_started = true;
			_from = _target = Value = target;
			_time = _duration;
			return;
		}

		if (target != _target)
		{
			_from = Value;
			_target = target;
			_time = 0f;
		}

		_time = MathF.Min(_duration, _time + dt);
		Value = _duration > 0f ? Vector4.Lerp(_from, _target, _time / _duration) : _target;
	}
}

// Una esfera de boost o un segmento de daño, ya con su transición
public readonly record struct CockpitHudPaint(Vector4 Fill, Vector4 Stroke, float Glow);

// Estado del HUD de cabina y su luz (spec docs/naves-3b-cabinas.md, 4.2 y 5), común a todas las naves. Lógica pura;
// la paleta se inyecta porque los colores viven en Palette, que depende de Godot.
public sealed class CockpitHudState
{
	public const float HudTransitionTime = 0.25f;
	public const float BoostTintTransitionTime = 0.3f;
	public const float BlinkPeriod = 0.5f;          // daño 3: 100 % / 25 %, a 2 Hz
	public const float BlinkDimOpacity = 0.25f;
	public const float DamagePulsePeriod = 1f;      // tinte rojo con daño 3: de 0,45 a 0,20 y vuelta
	public const float DamagePulseHigh = 0.45f;
	public const float DamagePulseLow = 0.2f;
	public const float DamageTintLevel2 = 0.15f;
	public const float BoostTintActive = 0.55f;
	public const float BoostTintOverTop = 0.25f;
	public const float ScreenGlowBase = 0.35f;      // + este mismo valor × fracción de velocidad
	public const float DisabledDamageBorderOpacity = 0.4f;   // TIME TRIAL: borde azul tenue
	public const float OverTopMarginKmh = 1f;       // margen del diseño para que la punta exacta no cambie el color

	public const int SlotCount = 3;

	private readonly Func<CockpitColor, Vector4> _palette;
	private readonly CockpitFade _speedColor = new(HudTransitionTime);
	private readonly CockpitFade[] _orbFill = NewFades();
	private readonly CockpitFade[] _orbStroke = NewFades();
	private readonly CockpitFade[] _orbGlow = NewFades();
	private readonly CockpitFade[] _segmentFill = NewFades();
	private readonly CockpitFade[] _segmentStroke = NewFades();
	private readonly CockpitFade[] _segmentGlow = NewFades();
	private readonly CockpitFade _boostTint = new(BoostTintTransitionTime);
	private float _time;

	public CockpitHudState(Func<CockpitColor, Vector4> palette)
	{
		_palette = palette;
	}

	public float SpeedFraction { get; private set; }
	public float TopFraction { get; private set; } = 396f / 496f;
	public string SpeedText { get; private set; } = "0";
	public Vector4 SpeedColor => _speedColor.Value;
	public string BoostText { get; private set; } = "";
	public string DamageText { get; private set; } = "";
	public Vector4 DamageTextColor { get; private set; }
	public float ScreenGlow { get; private set; }
	public float BoostTint => _boostTint.Value.X;
	public float DamageTint { get; private set; }
	public bool SegmentsBlinking { get; private set; }

	public CockpitHudPaint Orb(int i) => new(_orbFill[i].Value, _orbStroke[i].Value, _orbGlow[i].Value.X);

	public CockpitHudPaint Segment(int i)
	{
		float blink = SegmentsBlinking && _time % BlinkPeriod >= BlinkPeriod * 0.5f ? BlinkDimOpacity : 1f;
		return new CockpitHudPaint(WithAlpha(_segmentFill[i].Value, blink), WithAlpha(_segmentStroke[i].Value, blink), _segmentGlow[i].Value.X * blink);
	}

	// Avanza dt segundos. La pausa no lo llama: queda todo congelado.
	public void Update(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		_time += dt;

		float max = input.MaxKmh > 0f ? input.MaxKmh : 1f;
		SpeedFraction = Math.Clamp(input.SpeedKmh / max, 0f, 1f);
		TopFraction = Math.Clamp(input.TopKmh / max, 0f, 1f);
		SpeedText = MathF.Round(input.SpeedKmh).ToString(System.Globalization.CultureInfo.InvariantCulture);

		bool overTop = input.SpeedKmh > input.TopKmh + OverTopMarginKmh || input.BoostActive;
		_speedColor.Update(_palette(overTop ? CockpitColor.Orange : CockpitColor.Cyan), dt);

		UpdateBoost(input, dt);
		UpdateDamage(input, light, dt);

		ScreenGlow = (ScreenGlowBase + ScreenGlowBase * SpeedFraction) * light.ScreenGlow;
		float tint = input.BoostActive ? BoostTintActive : overTop ? BoostTintOverTop : 0f;
		_boostTint.Update(new Vector4(tint * light.BoostTint, 0f, 0f, 0f), dt);
	}

	private void UpdateBoost(in CockpitHudInput input, float dt)
	{
		bool blocked = input.DamageEnabled && input.DamageLevel >= DamageModel.MaxLevel;
		for (int i = 0; i < SlotCount; i++)
		{
			bool has = i < input.Charges;
			(CockpitColor color, float fill, float stroke, float glow) =
				has && blocked ? (CockpitColor.Red, 0.3f, 0.55f, 0f)
				: has && input.Cooldown ? (CockpitColor.Orange, 0.3f, 0.55f, 0f)
				: has ? (CockpitColor.Orange, 1f, 1f, 1f)
				: (CockpitColor.Blue, 0.25f, 0.7f, 0f);
			_orbFill[i].Update(WithAlpha(_palette(color), fill), dt);
			_orbStroke[i].Update(WithAlpha(_palette(color), stroke), dt);
			_orbGlow[i].Update(new Vector4(glow, 0f, 0f, 0f), dt);
		}

		BoostText = blocked && input.Charges > 0 ? "LOCKED"
			: input.BoostActive ? "ACTIVE"
			: input.Cooldown && input.Charges > 0 ? "COOLDOWN"
			: input.Charges > 0 ? "READY"
			: "CHARGING";
	}

	private void UpdateDamage(in CockpitHudInput input, in CockpitLightSettings light, float dt)
	{
		int level = input.DamageEnabled ? Math.Clamp(input.DamageLevel, 0, DamageModel.MaxLevel) : 0;
		CockpitColor color = level == 0 ? CockpitColor.Green : level == 1 ? CockpitColor.Yellow : CockpitColor.Red;

		for (int i = 0; i < SlotCount; i++)
		{
			bool on = i < level;
			Vector4 fill = WithAlpha(_palette(color), on ? 1f : 0f);
			Vector4 stroke = !input.DamageEnabled
				? WithAlpha(_palette(CockpitColor.Blue), DisabledDamageBorderOpacity)
				: WithAlpha(_palette(color), on ? 1f : 0.6f);
			_segmentFill[i].Update(fill, dt);
			_segmentStroke[i].Update(stroke, dt);
			_segmentGlow[i].Update(new Vector4(on ? 1f : 0f, 0f, 0f, 0f), dt);
		}

		SegmentsBlinking = level == DamageModel.MaxLevel;
		DamageText = !input.DamageEnabled ? "" : new[] { "NOMINAL", "LIGHT", "HEAVY", "CRITICAL" }[level];
		DamageTextColor = _palette(color);

		float tint = level >= DamageModel.MaxLevel
			? DamagePulseLow + (DamagePulseHigh - DamagePulseLow) * (0.5f + 0.5f * MathF.Cos(MathF.Tau * _time / DamagePulsePeriod))
			: level == 2 ? DamageTintLevel2 : 0f;
		DamageTint = tint * light.DamageTint;
	}

	private static CockpitFade[] NewFades()
	{
		var fades = new CockpitFade[SlotCount];
		for (int i = 0; i < SlotCount; i++)
			fades[i] = new CockpitFade(HudTransitionTime);
		return fades;
	}

	private static Vector4 WithAlpha(Vector4 color, float factor) => new(color.X, color.Y, color.Z, color.W * factor);
}

// Piezas vivas del HUD de cabina: su sitio en el diseño (designs/lib.js) y qué valor toman de CockpitHudState. Lógica pura.
public static class CockpitHudPieces
{
	public static void SpeedText(this CockpitCanvas c, float x, float y, float size, CockpitTextAnchor anchor)
	{
		c.Text(x, y, "0", size, 0f, anchor, CockpitColor.Cyan, 1f, CockpitSlot.SpeedNumber);
	}

	public static void BoostOrbs(this CockpitCanvas c, float x, float y, float radius, float gap)
	{
		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			float cx = x + i * (2f * radius + gap);
			c.Circle(cx, y, radius + 3f, null, CockpitColor.Blue, 1f, 0.5f);
			c.Add(new CockpitElement
			{
				Kind = CockpitElementKind.Circle,
				Center = new Vector2(cx, y),
				Radii = new Vector2(radius, radius),
				Fill = CockpitColor.Orange,
				Stroke = CockpitColor.Orange,
				StrokeWidth = 1.2f,
				Slot = CockpitSlot.BoostOrb,
				SlotIndex = i
			});
		}
	}

	public static void DamageSegments(this CockpitCanvas c, float x, float y, float width, float height, float gap)
	{
		for (int i = 0; i < CockpitHudState.SlotCount; i++)
		{
			c.Add(new CockpitElement
			{
				Kind = CockpitElementKind.Rect,
				Position = new Vector2(x + i * (width + gap), y),
				Size = new Vector2(width, height),
				Fill = CockpitColor.Green,
				Stroke = CockpitColor.Green,
				Slot = CockpitSlot.DamageSegment,
				SlotIndex = i
			});
		}
	}

	public static void DamageText(this CockpitCanvas c, float x, float y, CockpitTextAnchor anchor)
	{
		c.Text(x, y, "", 10f, 2f, anchor, CockpitColor.Green, 0.9f, CockpitSlot.DamageText);
	}

	// Barra de velocidad con marcas. La marca naranja va en la punta de la nave; el diseño la dibuja con los 396 / 496 de Vector.
	public static void SpeedBar(this CockpitCanvas c, float x, float y, float w, float h)
	{
		c.Rect(x, y, w, h, CockpitColor.Blue, opacity: 0.3f);
		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Rect,
			Position = new Vector2(x, y),
			Size = new Vector2(w, h),
			Fill = CockpitColor.Cyan,
			Slot = CockpitSlot.SpeedBar
		});

		var ticks = new List<Vector2>(22);
		for (int i = 0; i <= 10; i++)
		{
			float xx = x + w * i / 10f;
			ticks.Add(new Vector2(xx, y + h + 2f));
			ticks.Add(new Vector2(xx, y + h + (i % 5 == 0 ? 7f : 4f)));
		}
		c.Segments(ticks, CockpitColor.BlueGray, 1f, 0.6f);

		float markX = x + w * 396f / 496f;
		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Polyline,
			Points = new[] { new Vector2(markX, y - 3f), new Vector2(markX, y + h + 3f) },
			Size = new Vector2(w, 0f),
			Position = new Vector2(x, 0f),
			Stroke = CockpitColor.Orange,
			Opacity = 0.7f,
			Slot = CockpitSlot.SpeedBarMark
		});
	}

	// El elemento con los valores de ahora. Los fijos no cambian.
	public static CockpitElement Resolve(CockpitElement e, CockpitHudState hud)
	{
		switch (e.Slot)
		{
			case CockpitSlot.ScreenGlow:
				return e with { Opacity = hud.ScreenGlow };
			case CockpitSlot.BoostTint:
				return e with { Opacity = hud.BoostTint };
			case CockpitSlot.DamageTint:
				return e with { Opacity = hud.DamageTint };
			case CockpitSlot.BoostOrb:
				return Paint(e, hud.Orb(e.SlotIndex));
			case CockpitSlot.DamageSegment:
				return Paint(e, hud.Segment(e.SlotIndex));
			case CockpitSlot.DamageText:
				return e with { Text = hud.DamageText, FillRgba = hud.DamageTextColor };
			case CockpitSlot.BoostText:
				return e with { Text = hud.BoostText };
			case CockpitSlot.SpeedNumber:
				return e with { Text = hud.SpeedText, FillRgba = hud.SpeedColor, Glow = 1f };
			case CockpitSlot.SpeedBar:
				return e with { Size = new Vector2(e.Size.X * hud.SpeedFraction, e.Size.Y), FillRgba = hud.SpeedColor };
			case CockpitSlot.SpeedBarMark:
			{
				float x = e.Position.X + e.Size.X * hud.TopFraction;
				return e with { Points = new[] { new Vector2(x, e.Points[0].Y), new Vector2(x, e.Points[1].Y) } };
			}
			default:
				return e;
		}
	}

	private static CockpitElement Paint(CockpitElement e, CockpitHudPaint paint) =>
		e with { FillRgba = paint.Fill, StrokeRgba = paint.Stroke, Glow = paint.Glow };
}

// Qué se ve en cada cámara (spec docs/naves-3b-cabinas.md, 4.1). Posición, vuelta y tiempos se ven siempre.
public readonly record struct CockpitViewRules(bool CockpitLayer, bool ScreenDriveHud)
{
	public static CockpitViewRules For(CameraView view) => view == CameraView.Cockpit
		? new CockpitViewRules(true, false)
		: new CockpitViewRules(false, true);
}
