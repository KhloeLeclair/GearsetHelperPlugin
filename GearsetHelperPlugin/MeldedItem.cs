using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using GearsetExportPlugin.Sheets;

namespace GearsetExportPlugin;


internal enum Stat {
	STR = 1,
	DEX = 2,
	VIT = 3,
	INT = 4,
	MND = 5,
	PIE = 6,

	HP = 7,
	MP = 8,
	TP = 9,
	GP = 10,
	CP = 11,

	PhysDMG = 12,
	MagDMG = 13,

	TEN = 19,
	DEF = 21,

	DH = 22,
	CRT = 27,
	DET = 44,
	SKS = 45,
	SPS = 46,

	Craftsmanship = 70,
	Control = 71,
	Gathering = 72,
	Perception = 73
}


internal static class BaseStats {
	internal static Dictionary<uint, int> Lvl90_Stats = new() {
		[(int) Stat.STR] = 390,
		[(int) Stat.DEX] = 390,
		[(int) Stat.VIT] = 390,
		[(int) Stat.INT] = 390,
		[(int) Stat.MND] = 390,
		[(int) Stat.PIE] = 390,

		[(int) Stat.HP] = 3000,
		[(int) Stat.MP] = 10000,

		[(int) Stat.TEN] = 400,
		[(int) Stat.DH] = 400,
		[(int) Stat.CRT] = 400,
		[(int) Stat.DET] = 390,
		[(int) Stat.SKS] = 400,
		[(int) Stat.SPS] = 400,

		[(int) Stat.Craftsmanship] = 0,
		[(int) Stat.Control] = 0,
		[(int) Stat.CP] = 180,

		[(int) Stat.Gathering] = 0,
		[(int) Stat.Perception] = 0,
		[(int) Stat.GP] = 400,

		[(int) Stat.DEF] = 0
	};
}


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

	public ClassJobCategory? Category { get; set; }

	public string? PlayerName { get; set; }

	public uint Class { get; set; }

	public byte Level { get; set; }

	public uint ItemLevel { get; set; }

	public int Unmelded { get; set; }

	public List<MeldedItem> Items { get; }

	public Dictionary<uint, ExtendedBaseParam> Params { get; } = new();

	public Dictionary<uint, ItemStat> Stats { get; } = new();

	public Dictionary<uint, int> Materia { get; } = new();

	public Gearset(List<MeldedItem>? items = null) {
		Items = items ?? new();
	}

}


internal class ItemStat {

	public uint StatID { get; }

	public int Base { get; set; }
	public int Delta { get; set; }

	public int Waste { get; set; }

	public int Limit { get; set; }

	public ItemStat(uint id) {
		StatID = id;
	}

	public int Full => Base + Delta;
	public int Value => Base + Delta - Waste;

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
