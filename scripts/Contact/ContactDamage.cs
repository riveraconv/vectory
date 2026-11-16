using Vectory.Circuit;

namespace Vectory.Contact;

// Puntos de daño de un golpe entre naves según el papel (spec docs/rivales-3-contacto.md, 4). El roce no suma.
public struct ContactDamageSettings
{
	// Valores por defecto, los del grupo Daños de Main
	public const int DefaultMediumRammer = 1;
	public const int DefaultMediumReceiver = 0;
	public const int DefaultMediumMutual = 1;
	public const int DefaultStrongRammer = 3;
	public const int DefaultStrongReceiver = 1;
	public const int DefaultStrongMutual = 3;

	public int MediumRammer;
	public int MediumReceiver;
	public int MediumMutual;
	public int StrongRammer;
	public int StrongReceiver;
	public int StrongMutual;

	public static ContactDamageSettings Default => new()
	{
		MediumRammer = DefaultMediumRammer,
		MediumReceiver = DefaultMediumReceiver,
		MediumMutual = DefaultMediumMutual,
		StrongRammer = DefaultStrongRammer,
		StrongReceiver = DefaultStrongReceiver,
		StrongMutual = DefaultStrongMutual
	};
}

// Reparto del daño: papel + nivel → puntos. Lógica pura: no depende de Godot.
public static class ContactDamage
{
	public static int Points(ImpactLevel level, ContactRole role, in ContactDamageSettings s) => level switch
	{
		ImpactLevel.Medium => role switch
		{
			ContactRole.Rammer => s.MediumRammer,
			ContactRole.Receiver => s.MediumReceiver,
			_ => s.MediumMutual
		},
		ImpactLevel.Strong => role switch
		{
			ContactRole.Rammer => s.StrongRammer,
			ContactRole.Receiver => s.StrongReceiver,
			_ => s.StrongMutual
		},
		_ => 0
	};
}
