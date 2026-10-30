using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Indicador de daño del HUD (spec docs/danos.md, 5.1): tres segmentos horizontales en fila, alineados a la
// derecha del control menos RightInset (el hueco de las esferas de boost). Se encienden de izquierda a derecha,
// uno por nivel, todos del color del nivel:
//  - 0: los tres apagados, borde verde atenuado
//  - 1: uno amarillo con halo; los apagados con borde amarillo atenuado
//  - 2: dos rojos con halo; el apagado con borde rojo atenuado
//  - 3: los tres rojos, parpadeando (BlinkFrequency, con BlinkOpacity en la fase baja)
// Solo MUESTRA el nivel; la lógica vive en DamageModel. Con la pausa el parpadeo se congela.
public partial class DamageIndicatorView : Control
{
	private const int SegmentCount = 3;

	// Medidas en px de pantalla (el HUD ya las ha escalado). Se fijan antes de añadirlo al árbol.
	public float SegmentWidth = 33f;
	public float SegmentHeight = 12f;
	public float SegmentGap = 6f;
	public float BorderWidth = 1.5f;
	public float BlinkFrequency = 2f;   // Hz del parpadeo del nivel 3
	public float BlinkOpacity = 0.25f;  // opacidad en la fase baja del parpadeo

	private int _level;
	private float _rightInset;
	private float _blinkTime;

	// Alto que necesita el control: el segmento y su halo por arriba y por abajo
	public float RequiredHeight => SegmentHeight + 2f * GlowSize;

	private float GlowSize => SegmentHeight * 0.4f;

	public void SetLevel(int level)
	{
		level = Mathf.Clamp(level, 0, SegmentCount);
		if (level == _level)
			return;

		_level = level;
		_blinkTime = 0f;
		SetBlink(true);
		QueueRedraw();
	}

	// Distancia del borde derecho del control al último segmento
	public void SetRightInset(float inset)
	{
		if (Mathf.IsEqualApprox(inset, _rightInset))
			return;

		_rightInset = inset;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_level < SegmentCount || BlinkFrequency <= 0f)
			return;

		float period = 1f / BlinkFrequency;
		_blinkTime = (_blinkTime + (float)delta) % period;
		SetBlink(_blinkTime < period * 0.5f);
	}

	public override void _Draw()
	{
		Color on, glow, off;
		switch (_level)
		{
			case 0:
				on = Palette.DamageNone;
				glow = Colors.Transparent;
				off = Palette.DamageNoneOff;
				break;
			case 1:
				on = Palette.DamageLight;
				glow = Palette.DamageLightGlow;
				off = Palette.DamageLightOff;
				break;
			default:
				on = Palette.DamageHeavy;
				glow = Palette.DamageHeavyGlow;
				off = Palette.DamageHeavyOff;
				break;
		}

		float total = SegmentCount * SegmentWidth + (SegmentCount - 1) * SegmentGap;
		float left = Size.X - _rightInset - total;
		float top = (Size.Y - SegmentHeight) * 0.5f;

		// Primero los halos (dos capas translúcidas), luego los segmentos: así el halo de uno no tapa al anterior
		for (int i = 0; i < _level; i++)
		{
			Rect2 rect = SegmentRect(left, top, i);
			DrawRect(rect.Grow(GlowSize), new Color(glow, glow.A * 0.35f));
			DrawRect(rect.Grow(GlowSize * 0.5f), new Color(glow, glow.A * 0.5f));
		}

		for (int i = 0; i < SegmentCount; i++)
		{
			Rect2 rect = SegmentRect(left, top, i);
			if (i < _level)
				DrawRect(rect, on);
			else
				DrawRect(rect.Grow(-BorderWidth * 0.5f), off, false, BorderWidth);   // borde por dentro del rectángulo
		}
	}

	private Rect2 SegmentRect(float left, float top, int index)
	{
		return new Rect2(left + index * (SegmentWidth + SegmentGap), top, SegmentWidth, SegmentHeight);
	}

	private void SetBlink(bool high)
	{
		Modulate = new Color(1f, 1f, 1f, high ? 1f : BlinkOpacity);
	}
}
