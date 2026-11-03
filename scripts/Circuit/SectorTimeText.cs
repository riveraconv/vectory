using Vectory.Core;
using Vectory.Records;

namespace Vectory.Circuit;

// Tiempo de un sector recién cerrado, ya en texto (panel del checkpoint y HUD; spec docs/elementos-circuito.md, 4.3):
// "S1", el tiempo con el formato del HUD y la diferencia con el mejor sector anterior de esa nave
// (null si aún no tenía ninguno). IsBetter: mejora (o iguala) ese mejor tiempo.
public readonly struct SectorTimeText
{
	public readonly string Label;
	public readonly string Time;
	public readonly string Delta;
	public readonly bool IsBetter;

	private SectorTimeText(string label, string time, string delta, bool isBetter)
	{
		Label = label;
		Time = time;
		Delta = delta;
		IsBetter = isBetter;
	}

	public bool HasDelta => Delta != null;

	// sectorIndex en base 0 (se muestra S1, S2…)
	public static SectorTimeText Build(int sectorIndex, double time, double? previousBest)
	{
		string label = $"S{sectorIndex + 1}";
		if (!previousBest.HasValue)
			return new SectorTimeText(label, TimeFormat.Format(time), null, false);

		double delta = DeltaFormat.Delta(time, previousBest.Value);
		return new SectorTimeText(label, TimeFormat.Format(time), DeltaFormat.Format(delta), DeltaFormat.IsBetter(delta));
	}
}
