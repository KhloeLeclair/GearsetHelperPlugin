using System;
using System.Collections.Generic;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin;


internal class MeldedItem {

	public uint ItemID { get; }

	public bool HQ { get; }

	public MeldedMateria[] Melds { get; }

	public uint Level { get; set; }

	public Dictionary<uint, ItemStat> Stats { get; } = new();

	public MeldedItem(uint itemID, bool hq, MeldedMateria[] melds) {
		ItemID = itemID;
		HQ = hq;
		Melds = melds;
	}

	public ExtendedItem? GetItem(ExcelSheet<ExtendedItem> sheet) {
		return sheet.GetRow(ItemID);
	}
}

internal struct MeldedMateria {

	public ushort ID { get; }
	public byte Grade { get; }

	public MeldedMateria(ushort id, byte grade) {
		ID = id;
		Grade = grade;
	}

	public Materia? GetMateria(ExcelSheet<Materia> sheet) {
		return sheet.GetRow(ID);
	}

}


internal class Gearset {

	public Gearset(List<MeldedItem>? items = null) {
		Items = items ?? new();
	}

	// Player Information

	public string? PlayerName { get; set; }

	public byte? Tribe { get; set; }

	public uint Class { get; set; }
	public uint ActualClass { get; set; }

	public byte Level { get; set; }

	// Item Information

	public bool HasCrystal { get; set; }

	public int Unmelded { get; set; }

	public List<MeldedItem> Items { get; }

	public Dictionary<uint, ExtendedBaseParam> Params { get; } = new();

	public Dictionary<uint, ItemStat> Stats { get; } = new();

	public Dictionary<uint, int> Materia { get; } = new();

}


internal class ItemStat {

	public uint StatID { get; }

	public int Base { get; set; }
	public int Gear { get; set; }
	public int Delta { get; set; }

	public int Waste { get; set; }

	public int Limit { get; set; }

	public ItemStat(uint id) {
		StatID = id;
	}

	public int Full => Base + Gear + Delta;
	public int Value => Base + Gear + Delta - Waste;

	public int Remaining => Limit - Value;

	public void UpdateWaste() {
		if (Full > Limit && Limit > 0) {
			Waste = Full - Limit;
		} else
			Waste = 0;
	}

	public ExtendedBaseParam? GetParam(ExcelSheet<ExtendedBaseParam> sheet) {
		return sheet.GetRow(StatID);
	}

}
