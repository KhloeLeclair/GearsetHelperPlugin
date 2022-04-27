using System;

using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin.Models;

internal record StatData(
	uint ID
) {
	public int Base { get; set; }
	public int Gear { get; set; }
	public int Delta { get; set; }
	public int Waste { get; set; }
	public int Limit { get; set; } = -1;

	public int Food { get; set; }

	public int Full => Base + Gear + Delta;
	public int Extra => Gear + Delta - Waste;
	public int ValueNoFood => Base + Gear + Delta - Waste;
	public int Remaining => Math.Max(0, Limit - ValueNoFood);
	public int Value => ValueNoFood + Food;

	public int PreviousTier { get; set; }
	public int NextTier { get; set; }

	public void UpdateWaste() {
		if (Full > Limit && Limit >= 0)
			Waste = Full - Limit;
		else
			Waste = 0;
	}

	public void UpdateTiers(int level) {
		// Do we have a coefficient for this stat? If we don't, we can't
		// calculate tiers.
		if (!Data.COEFFICIENTS.TryGetValue(ID, out int coefficient))
			return;

		float perTier = (float) level / coefficient;

		// Tiers are calculated based on the number of extra points
		// we have beyond the base value.
		int total = Value - Base;
		int tiers = (int) Math.Floor(total / perTier);

		PreviousTier = (int) Math.Ceiling(tiers * perTier) - total;
		NextTier = (int) Math.Ceiling((tiers + 1) * perTier) - total;
	}

	public ExtendedBaseParam? Row() {
		if (!Data.CheckSheets())
			return null;
		return Data.ParamSheet.GetRow(ID);
	}
}
