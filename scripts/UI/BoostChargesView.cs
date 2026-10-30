using Godot;
using Vectory.Core;

namespace Vectory.UI;

// Cargas de boost del HUD: una fila de esferas pequeñas, alineadas a la derecha del control.
// Se encienden de izquierda a derecha. Cuatro estados por esfera:
//  - disponible: naranja con halo
//  - en cooldown: naranja atenuado, sin halo (hay carga, pero todavía no se puede usar)
//  - bloqueada: rojo atenuado, sin halo (hay carga, pero los daños de nivel 3 no dejan usarla; manda sobre el cooldown)
//  - vacía: azul apagado
// Solo MUESTRA el estado; la lógica vive en ShipBoost.
public partial class BoostChargesView : Control
{
	public const float Diameter = 16f;
	public const float Gap = 10f;

	private const float GlowSize = 6f;   // cuánto sobresale el halo de la esfera encendida
	private const float BorderWidth = 1.5f;

	private int _charges;
	private int _maxCharges = 3;
	private bool _cooldownActive;
	private bool _blocked;

	// Ancho que ocupan maxCharges esferas con su separación
	public static float WidthFor(int maxCharges)
	{
		return maxCharges <= 0 ? 0f : maxCharges * Diameter + (maxCharges - 1) * Gap;
	}

	public void SetCharges(int charges, int maxCharges, bool cooldownActive, bool blocked = false)
	{
		charges = Mathf.Clamp(charges, 0, Mathf.Max(0, maxCharges));
		if (charges == _charges && maxCharges == _maxCharges && cooldownActive == _cooldownActive && blocked == _blocked)
			return;

		_charges = charges;
		_maxCharges = maxCharges;
		_cooldownActive = cooldownActive;
		_blocked = blocked;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float radius = Diameter * 0.5f;
		float left = Size.X - WidthFor(_maxCharges);

		for (int i = 0; i < _maxCharges; i++)
		{
			var center = new Vector2(left + radius + i * (Diameter + Gap), Size.Y * 0.5f);

			if (i < _charges && _blocked)
			{
				DrawCircle(center, radius, Palette.BoostBlockedFill);
				DrawArc(center, radius - BorderWidth * 0.5f, 0f, Mathf.Tau, 32, Palette.BoostBlockedBorder, BorderWidth, true);
			}
			else if (i < _charges && _cooldownActive)
			{
				DrawCircle(center, radius, Palette.BoostCooldownFill);
				DrawArc(center, radius - BorderWidth * 0.5f, 0f, Mathf.Tau, 32, Palette.BoostCooldownBorder, BorderWidth, true);
			}
			else if (i < _charges)
			{
				// Halo suave: dos capas translúcidas debajo de la esfera
				DrawCircle(center, radius + GlowSize, Palette.BoostChargeGlow);
				DrawCircle(center, radius + GlowSize * 0.5f, Palette.BoostChargeGlow);
				DrawCircle(center, radius, Palette.BoostCharge);
			}
			else
			{
				DrawCircle(center, radius, Palette.BoostEmptyFill);
				DrawArc(center, radius - BorderWidth * 0.5f, 0f, Mathf.Tau, 32, Palette.BoostEmpty, BorderWidth, true);
			}
		}
	}
}
