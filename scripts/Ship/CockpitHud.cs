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

// Cómo se pinta lo apagado: cambia de un estilo de cabina a otro, las reglas no.
// Sin SegmentOffColor, un segmento apagado va con el color del nivel de daño.
public readonly record struct CockpitHudLook
{
	public CockpitColor EmptyOrbColor { get; init; }
	public float EmptyOrbFill { get; init; }
	public float EmptyOrbStroke { get; init; }
	public CockpitColor? SegmentOffColor { get; init; }
	public float SegmentOffStroke { get; init; }
	public CockpitColor DisabledSegmentColor { get; init; }
	public float DisabledSegmentStroke { get; init; }

	// El de las cabinas 2D (spec docs/naves-3b-cabinas.md)
	public static readonly CockpitHudLook Default = new()
	{
		EmptyOrbColor = CockpitColor.Blue,
		EmptyOrbFill = 0.25f,
		EmptyOrbStroke = 0.7f,
		SegmentOffColor = null,
		SegmentOffStroke = 0.6f,
		DisabledSegmentColor = CockpitColor.Blue,
		DisabledSegmentStroke = CockpitHudState.DisabledDamageBorderOpacity
	};
}

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
	private readonly CockpitHudLook _look;
	private readonly CockpitFade _speedColor = new(HudTransitionTime);
	private readonly CockpitFade[] _orbFill = NewFades();
	private readonly CockpitFade[] _orbStroke = NewFades();
	private readonly CockpitFade[] _orbGlow = NewFades();
	private readonly CockpitFade[] _segmentFill = NewFades();
	private readonly CockpitFade[] _segmentStroke = NewFades();
	private readonly CockpitFade[] _segmentGlow = NewFades();
	private readonly CockpitFade _boostTint = new(BoostTintTransitionTime);
	private float _time;

	public CockpitHudState(Func<CockpitColor, Vector4> palette) : this(palette, CockpitHudLook.Default)
	{
	}

	public CockpitHudState(Func<CockpitColor, Vector4> palette, CockpitHudLook look)
	{
		_palette = palette;
		_look = look;
	}

	// Por encima de la punta o con boost
	public bool OverTop { get; private set; }

	public float SpeedFraction { get; private set; }
	public float TopFraction { get; private set; } = 396f / 496f;
	public float MaxKmh { get; private set; } = 496f;
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
		MaxKmh = max;
		SpeedFraction = Math.Clamp(input.SpeedKmh / max, 0f, 1f);
		TopFraction = Math.Clamp(input.TopKmh / max, 0f, 1f);
		SpeedText = MathF.Round(input.SpeedKmh).ToString(System.Globalization.CultureInfo.InvariantCulture);

		bool overTop = input.SpeedKmh > input.TopKmh + OverTopMarginKmh || input.BoostActive;
		OverTop = overTop;
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
				: (_look.EmptyOrbColor, _look.EmptyOrbFill, _look.EmptyOrbStroke, 0f);
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
				? WithAlpha(_palette(_look.DisabledSegmentColor), _look.DisabledSegmentStroke)
				: on ? _palette(color)
				: WithAlpha(_palette(_look.SegmentOffColor ?? color), _look.SegmentOffStroke);
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
			c.DamageSegment(x + i * (width + gap), y, width, height, i, 1f);
	}

	public static void DamageSegment(this CockpitCanvas c, float x, float y, float width, float height, int index, float strokeWidth)
	{
		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Rect,
			Position = new Vector2(x, y),
			Size = new Vector2(width, height),
			Fill = CockpitColor.Green,
			Stroke = CockpitColor.Green,
			StrokeWidth = strokeWidth,
			Slot = CockpitSlot.DamageSegment,
			SlotIndex = index
		});
	}

	// Dial: arco de fondo, tramo desde la punta hasta el final, arco de velocidad, 11 marcas, números y aguja.
	// Los números se colocan según la escala de la nave; el diseño los dibuja con los 496 de Vector.
	public static readonly int[] GaugeNumbers = { 0, 100, 200, 300, 400 };

	public static void Gauge(this CockpitCanvas c, Vector2 center, float radius, float startAngle, float endAngle)
	{
		CockpitElement Arc(CockpitSlot slot, CockpitColor color, float opacity) => new()
		{
			Kind = CockpitElementKind.Arc,
			Center = center,
			Radii = new Vector2(radius, radius),
			StartAngle = startAngle,
			EndAngle = endAngle,
			Stroke = color,
			StrokeWidth = 6f,
			Opacity = opacity,
			Slot = slot
		};

		c.Add(Arc(CockpitSlot.GaugeBack, CockpitColor.Blue, 0.35f));
		c.Add(Arc(CockpitSlot.GaugeTop, CockpitColor.Orange, 0.35f));
		c.Add(Arc(CockpitSlot.GaugeSpeed, CockpitColor.Cyan, 1f));

		var ticks = new List<Vector2>(22);
		for (int k = 0; k <= 10; k++)
		{
			float a = (startAngle + (endAngle - startAngle) * k / 10f) * MathF.PI / 180f;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			ticks.Add(center + dir * (radius + 9f));
			ticks.Add(center + dir * (radius + (k % 2 == 0 ? 18f : 13f)));
		}
		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Segments,
			Points = ticks,
			Stroke = CockpitColor.BlueGray,
			Opacity = 0.6f,
			Slot = CockpitSlot.GaugeTicks
		});

		foreach (int value in GaugeNumbers)
		{
			var number = new CockpitElement
			{
				Kind = CockpitElementKind.Text,
				Center = center,
				Radii = new Vector2(radius, radius),
				StartAngle = startAngle,
				EndAngle = endAngle,
				Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
				FontSize = 8f,
				Anchor = CockpitTextAnchor.Middle,
				Fill = CockpitColor.BlueGray,
				Opacity = 0.7f,
				Slot = CockpitSlot.GaugeNumber,
				SlotIndex = value
			};
			c.Add(number with { Position = GaugeNumberPosition(number, 496f) });
		}

		c.Add(new CockpitElement
		{
			Kind = CockpitElementKind.Segments,
			Center = center,
			Radii = new Vector2(radius, radius),
			StartAngle = startAngle,
			EndAngle = endAngle,
			Points = NeedlePoints(center, radius, startAngle),
			Stroke = CockpitColor.GlassReflection,
			StrokeWidth = 2.4f,
			Slot = CockpitSlot.GaugeNeedle
		});
	}

	// Ángulo de la aguja (grados) con la fracción de velocidad f
	public static float NeedleAngle(float startAngle, float endAngle, float f) => startAngle + (endAngle - startAngle) * f;

	private static Vector2[] NeedlePoints(Vector2 center, float radius, float angleDegrees)
	{
		float a = angleDegrees * MathF.PI / 180f;
		var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
		return new[] { center + dir * (radius - 14f), center + dir * (radius + 6f) };
	}

	private static Vector2 GaugeNumberPosition(CockpitElement e, float maxKmh)
	{
		float a = (e.StartAngle + (e.EndAngle - e.StartAngle) * e.SlotIndex / maxKmh) * MathF.PI / 180f;
		float distance = e.Radii.X + 30f;
		return new Vector2(e.Center.X + MathF.Cos(a) * distance, e.Center.Y + MathF.Sin(a) * distance + 3f);
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
			{
				float width = e.Size.X * hud.SpeedFraction;
				return e with { Size = new Vector2(width >= MinBarWidth ? width : 0f, e.Size.Y), FillRgba = hud.SpeedColor };
			}
			case CockpitSlot.SpeedBarMark:
			{
				float x = e.Position.X + e.Size.X * hud.TopFraction;
				return e with { Points = new[] { new Vector2(x, e.Points[0].Y), new Vector2(x, e.Points[1].Y) } };
			}
			case CockpitSlot.GaugeTop:
				return e with { StartAngle = e.StartAngle + (e.EndAngle - e.StartAngle) * hud.TopFraction };
			case CockpitSlot.GaugeSpeed:
				return e with
				{
					EndAngle = e.StartAngle + MathF.Max(MinSpeedArcDegrees, (e.EndAngle - e.StartAngle) * hud.SpeedFraction),
					StrokeRgba = hud.SpeedColor,
					Glow = 1f
				};
			case CockpitSlot.GaugeNumber:
				return e with { Position = GaugeNumberPosition(e, hud.MaxKmh) };
			case CockpitSlot.GaugeNeedle:
				return e with { Points = NeedlePoints(e.Center, e.Radii.X, NeedleAngle(e.StartAngle, e.EndAngle, hud.SpeedFraction)) };
			default:
				return e;
		}
	}

	// Por debajo no se ve, y un polígono tan fino no se puede triangular
	private const float MinBarWidth = 0.25f;

	// Se dibuja igual: lo vivo solo se redibuja si cambia algo. Sirve para ahorrar, no para decidir nada del juego.
	public static bool SameDrawing(CockpitElement a, CockpitElement b)
	{
		if (a == b)
			return true;
		if (ReferenceEquals(a.Points, b.Points) || a.Points.Count != b.Points.Count)
			return false;
		for (int i = 0; i < a.Points.Count; i++)
		{
			if (a.Points[i] != b.Points[i])
				return false;
		}
		return (a with { Points = b.Points }) == b;
	}

	private const float MinSpeedArcDegrees = 0.5f;   // como el diseño: el arco de velocidad nunca desaparece del todo

	private static CockpitElement Paint(CockpitElement e, CockpitHudPaint paint) =>
		e with { FillRgba = paint.Fill, StrokeRgba = paint.Stroke, Glow = paint.Glow };
}

// Qué se ve en cada cámara (spec docs/naves-3b-cabinas.md, 4.1, docs/cabinas-3d-1-vector.md, 4.7, y
// docs/cabinas-3d-2-blade.md, 4.7). Posición, vuelta y tiempos se ven siempre. La cabina 3D lleva en sus pantallas
// velocidad, boost, daño, radar y minimapa; mientras sus pantallas son de prueba (screensShowHud en false), siguen en
// el HUD de pantalla para poder conducir.
public readonly record struct CockpitViewRules(bool CockpitLayer, bool Cockpit3D, bool ScreenDriveHud, bool ScreenRadar)
{
	public static CockpitViewRules For(CameraView view, bool hasCockpit3D, bool screensShowHud = true)
	{
		if (view != CameraView.Cockpit)
			return new CockpitViewRules(false, false, true, true);
		if (!hasCockpit3D)
			return new CockpitViewRules(true, false, false, true);
		return screensShowHud
			? new CockpitViewRules(false, true, false, false)
			: new CockpitViewRules(false, true, true, true);
	}
}
