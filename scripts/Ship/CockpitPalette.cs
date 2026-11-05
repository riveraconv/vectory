using System;
using Godot;
using Vectory.Core;

namespace Vectory.Ship;

// Roles de color de la cabina → Palette
public static class CockpitPalette
{
	public static Color Get(CockpitColor color) => color switch
	{
		CockpitColor.Cyan => Palette.Cyan,
		CockpitColor.Blue => Palette.Blue,
		CockpitColor.Orange => Palette.Orange,
		CockpitColor.BlueGray => Palette.BlueGray,
		CockpitColor.Green => Palette.DamageNone,
		CockpitColor.Yellow => Palette.DamageLight,
		CockpitColor.Red => Palette.DamageHeavy,
		CockpitColor.Black => Palette.CockpitBlack,
		CockpitColor.Highlight => Palette.CockpitHighlight,
		CockpitColor.GlassReflection => Palette.CockpitGlassReflection,
		CockpitColor.PanelLight => Palette.CockpitPanelLight,
		CockpitColor.PanelMid => Palette.CockpitPanelMid,
		CockpitColor.PanelDark => Palette.CockpitPanelDark,
		CockpitColor.ConsoleLight => Palette.CockpitConsoleLight,
		CockpitColor.ConsoleDark => Palette.CockpitConsoleDark,
		CockpitColor.FrontLight => Palette.CockpitFrontLight,
		CockpitColor.FrontDark => Palette.CockpitFrontDark,
		CockpitColor.PillarLight => Palette.CockpitPillarLight,
		CockpitColor.NoseLight => Palette.CockpitNoseLight,
		CockpitColor.CowlLight => Palette.CockpitCowlLight,
		CockpitColor.Step => Palette.CockpitStep,
		CockpitColor.Sink => Palette.CockpitSink,
		CockpitColor.SinkWall => Palette.CockpitSinkWall,
		CockpitColor.Screen => Palette.CockpitScreen,
		CockpitColor.Vent => Palette.CockpitVent,
		CockpitColor.Screw => Palette.CockpitScrew,
		CockpitColor.DialShadow => Palette.CockpitDialShadow,
		_ => throw new ArgumentOutOfRangeException(nameof(color))
	};

	public static System.Numerics.Vector4 Rgba(CockpitColor color)
	{
		Color c = Get(color);
		return new System.Numerics.Vector4(c.R, c.G, c.B, c.A);
	}
}
