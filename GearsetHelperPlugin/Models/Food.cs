using System;
using System.Linq;
using System.Collections.Generic;

using Lumina.Excel.GeneratedSheets;
using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin.Models;

internal record Food(
	uint ItemID,
	uint FoodID,
	uint ILvl,
	bool HQ
) {

	public Dictionary<uint, FoodStat> Stats { get; } = new();

	private string? CachedStatLine { get; set; }

	public string? StatLine {
		get {
			if (CachedStatLine is null && Data.CheckSheets())
				CachedStatLine = string.Join(", ", Stats.Values.Select(x => x.Line));

			return CachedStatLine;
		}
	}

	public void UpdateStats(ItemFood? data = null) {
		data ??= FoodRow();
		Stats.Clear();

		if (data is null || data.UnkData1.Length == 0 || ! Data.CheckSheets())
			return;

		foreach (var entry in data.UnkData1) {
			if (entry.BaseParam == 0)
				continue;

			var param = Data.ParamSheet.GetRow(entry.BaseParam);
			if (param is null)
				continue;

			sbyte val = HQ ? entry.ValueHQ : entry.Value;
			short max = HQ ? entry.MaxHQ : entry.Max;

			float multiplier = entry.IsRelative ? val / 100f : 0;
			short value = entry.IsRelative ? max : val;

			string name;
			if (
				Data.TryGetStat(entry.BaseParam, out Stat? st) &&
				Data.ABBREVIATIONS.TryGetValue(st.Value, out string? abbrev) &&
				!string.IsNullOrEmpty(abbrev)
			)
				name = abbrev;
			else
				name = param.Name;

			string line;
			if (entry.IsRelative)
				line = $"{name} +{multiplier:P0} (Max {value})".Replace("%", "%%");
			else
				line = $"{name} +{value}";

			Stats[entry.BaseParam] = new FoodStat(
				StatID: entry.BaseParam,
				Line: line,
				Relative: entry.IsRelative,
				Multiplier: multiplier,
				MaxValue: value
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
	string Line,
	bool Relative,
	float Multiplier,
	short MaxValue
);
