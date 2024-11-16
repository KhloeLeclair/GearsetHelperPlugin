using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

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
		Stats.Clear();

		if (!data.HasValue || data.Value.Params.Count == 0 || !Data.CheckSheets())
			return;

		foreach (var entry in data.Value.Params) {
			if (!entry.BaseParam.IsValid || entry.BaseParam.RowId == 0)
				continue;

			var param = entry.BaseParam.Value;

			sbyte val = HQ ? entry.ValueHQ : entry.Value;
			short max = HQ ? entry.MaxHQ : entry.Max;

			float multiplier = entry.IsRelative ? val / 100f : 0;
			short value = entry.IsRelative ? max : val;

			string name;
			if (
				Data.TryGetStat(param.RowId, out Stat? st) &&
				Data.ABBREVIATIONS.TryGetValue(st.Value, out string? abbrev) &&
				!string.IsNullOrEmpty(abbrev)
			)
				name = abbrev;
			else
				name = param.Name.ToString();

			string line;
			if (entry.IsRelative)
				line = $"{name} +{multiplier:P0} (Max {value})".Replace("%", "%%");
			else
				line = $"{name} +{value}";

			Stats[param.RowId] = new FoodStat(
				StatID: param.RowId,
				Line: line,
				Relative: entry.IsRelative,
				Multiplier: multiplier,
				MaxValue: value
			);
		}
	}

	public Item? ItemRow() {
		if (!Data.CheckSheets() || !Data.ItemSheet.TryGetRow(ItemID, out var row))
			return null;
		return row;
	}

	public ItemFood? FoodRow() {
		if (!Data.CheckSheets() || !Data.FoodSheet.TryGetRow(FoodID, out var row))
			return null;
		return row;
	}

}

internal record FoodStat(
	uint StatID,
	string Line,
	bool Relative,
	float Multiplier,
	short MaxValue
);
