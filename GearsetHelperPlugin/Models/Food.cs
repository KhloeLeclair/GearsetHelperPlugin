using System.Collections.Generic;

using Lumina.Excel.GeneratedSheets;
using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin.Models;

internal record Food(
	uint ItemID,
	uint FoodID,
	uint ILvl
) {

	public Dictionary<uint, FoodStat> Stats { get; } = new();

	private string? CachedStatLine { get; set; }

	public string? StatLine {
		get {
			if (CachedStatLine is null && Data.CheckSheets()) {
				List<string> bits = new();

				foreach (var stat in Stats.Values) {
					var param = Data.ParamSheet.GetRow(stat.StatID);
					if (param is null)
						continue;

					if (stat.Relative)
						bits.Add($"{param.Name} +{stat.Multiplier:P0} (Max {stat.MaxValue})");
					else
						bits.Add($"{param.Name} +{stat.MaxValue}");
				}

				CachedStatLine = string.Join(", ", bits).Replace("%", "%%");
			}

			return CachedStatLine;
		}
	}


	public void UpdateStats(ItemFood? data = null) {
		data ??= FoodRow();
		Stats.Clear();

		if (data is null || data.UnkData1.Length == 0)
			return;

		foreach (var entry in data.UnkData1) {
			if (entry.BaseParam == 0)
				continue;

			Stats[entry.BaseParam] = new FoodStat(
				StatID: entry.BaseParam,
				Relative: entry.IsRelative,
				Multiplier: !entry.IsRelative ? 0 : entry.ValueHQ / 100f,
				MaxValue: !entry.IsRelative ? entry.ValueHQ : entry.MaxHQ
			);
		}
	}

	public ExtendedItem? ItemRow() {
		if (!Data.CheckSheets())
			return null;
		return Data.ItemSheet.GetRow(ItemID);
	}

	public ItemFood? FoodRow() {
		if (!Data.CheckSheets())
			return null;
		return Data.FoodSheet.GetRow(FoodID);
	}

}

internal record FoodStat(
	uint StatID,
	bool Relative,
	float Multiplier,
	short MaxValue
);
