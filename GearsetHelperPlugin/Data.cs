using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using GearsetHelperPlugin.Models;
using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin;

internal record StatAtLevel(
	int Main,
	int Sub
);

internal record StatOrTank(
	int Main,
	int Tank
);

internal record StatType(
	Stat Main,
	Stat Sub
);

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
	MDF = 24,

	DH = 22,
	CRT = 27,
	DET = 44,
	SKS = 45,
	SPS = 46,

	MainAttribute = 55,
	SecondaryAttribute = 56,

	Craftsmanship = 70,
	Control = 71,
	Gathering = 72,
	Perception = 73,
}


internal static partial class Data {

	#region Stat Abbreviations

	internal static readonly Dictionary<Stat, string> ABBREVIATIONS = new() {
		{ Stat.STR, "STR" },
		{ Stat.DEX, "DEX" },
		{ Stat.VIT, "VIT" },
		{ Stat.INT, "INT" },
		{ Stat.MND, "MND" },
		{ Stat.PIE, "PIE" },

		{ Stat.HP, "HP" },
		{ Stat.MP, "MP" },
		{ Stat.TP, "TP" },
		{ Stat.GP, "GP" },
		{ Stat.CP, "CP" },

		{ Stat.TEN, "TEN" },
		{ Stat.DH, "DH" },
		{ Stat.CRT, "CRT" },
		{ Stat.DET, "DET" },
		{ Stat.SKS, "SKS" },
		{ Stat.SPS, "SPS" }
	};

	internal static bool TryGetStat(uint input, [NotNullWhen(true)] out Stat? stat) {
		if (Enum.TryParse(typeof(Stat), input.ToString(), out object? result) && result is Stat s) {
			stat = s;
			return true;
		}

		stat = null;
		return false;
	}

	#endregion

	#region Sheet Access

	internal static ExcelSheet<ExtendedAction>? ActionSheet { get; set; }
	internal static ExcelSheet<ActionTransient>? ActionTransSheet { get; set; }
	internal static ExcelSheet<ExtendedItem>? ItemSheet { get; set; }
	internal static ExcelSheet<ExtendedBaseParam>? ParamSheet { get; set; }
	internal static ExcelSheet<ParamGrow>? GrowSheet { get; set; }
	internal static ExcelSheet<ExtendedItemLevel>? LevelSheet { get; set; }
	internal static ExcelSheet<Materia>? MateriaSheet { get; set; }
	internal static ExcelSheet<Race>? RaceSheet { get; set; }
	internal static ExcelSheet<Tribe>? TribeSheet { get; set; }
	internal static ExcelSheet<ClassJob>? ClassSheet { get; set; }
	internal static ExcelSheet<ItemAction>? ItemActionSheet { get; set; }
	internal static ExcelSheet<ItemFood>? FoodSheet { get; set; }

	[MemberNotNullWhen(true, nameof(ActionSheet), nameof(ActionTransSheet), nameof(ItemSheet), nameof(ItemActionSheet), nameof(FoodSheet), nameof(ParamSheet), nameof(GrowSheet), nameof(LevelSheet), nameof(MateriaSheet), nameof(RaceSheet), nameof(TribeSheet), nameof(ClassSheet))]
	internal static bool CheckSheets(ExcelModule? excel = null) {
		if (ActionSheet == null || ActionTransSheet == null || ItemSheet == null || ItemActionSheet == null || FoodSheet == null || ParamSheet == null || GrowSheet == null || LevelSheet == null || MateriaSheet == null || RaceSheet == null || TribeSheet == null || ClassSheet == null) {
			if (excel is not null) {
				LoadSheets(excel);
				return CheckSheets(null);

			} else
				return false;
		}

		return true;
	}

	internal static void LoadSheets(ExcelModule excel) {
		ActionSheet = excel.GetSheet<ExtendedAction>();
		ActionTransSheet = excel.GetSheet<ActionTransient>();
		ItemSheet = excel.GetSheet<ExtendedItem>();
		ItemActionSheet = excel.GetSheet<ItemAction>();
		FoodSheet = excel.GetSheet<ItemFood>();
		ParamSheet = excel.GetSheet<ExtendedBaseParam>();
		GrowSheet = excel.GetSheet<ParamGrow>();
		LevelSheet = excel.GetSheet<ExtendedItemLevel>();
		MateriaSheet = excel.GetSheet<Materia>();
		TribeSheet = excel.GetSheet<Tribe>();
		RaceSheet = excel.GetSheet<Race>();
		ClassSheet = excel.GetSheet<ClassJob>();

		_Food = null;
		FoodLoaded = false;
	}

	#endregion

	#region Food

	internal static bool FoodHQOnly = false;

	internal static bool IsFoodLoaded => FoodLoaded;

	private static bool LoadingFood = false;
	private static bool FoodLoaded = false;

	private static uint _FoodMinIlvlDoHL = 0;
	internal static uint FoodMinIlvlDoHL {
		get => _FoodMinIlvlDoHL;
		set {
			_FoodMinIlvlDoHL = value;
		}
	}

	private static uint _FoodMinIlvl = 0;
	internal static uint FoodMinIlvl {
		get => _FoodMinIlvl;
		set {
			_FoodMinIlvl = value;
		}
	}

	private static List<Food>? _Food;

	internal static List<Food> Food {
		get {
			if (_Food is null)
				LoadFood();

			return _Food;
		}
	}

	private static List<Food>? _Medicine;
	internal static List<Food> Medicine {
		get {
			if (_Medicine is null)
				LoadFood();

			return _Medicine;
		}
	}

	[MemberNotNull(nameof(_Food), nameof(_Medicine))]
	private static void LoadFood() {
		FoodLoaded = true;
		_Food = new List<Food>();
		_Medicine = new List<Food>();

		LoadFood(_Food, _Medicine);
	}

	internal static Task? LoadFoodAsync() {
		return Task.Run(async () => {
			if (LoadingFood)
				return;

			LoadingFood = true;

			try {
				var Food = new List<Food>();
				var Medicine = new List<Food>();

				LoadFood(Food, Medicine);

				if (!FoodLoaded) {
					_Food = Food;
					_Medicine = Medicine;
				} else
					Plugin.INSTANCE.Logger.Warning("Started loading food while LoadFoodAsync was running?");

				FoodLoaded = true;

			} catch (Exception ex) {
				Plugin.INSTANCE.Logger.Error($"Encountered an error while loading food in a thread. Details:\n{ex}");
			}

			LoadingFood = false;
		});
	}

	private static void LoadFood(List<Food> foodList, List<Food> medicineList) { 
		if (!CheckSheets())
			return;

		var sw = new Stopwatch();
		sw.Start();

		foreach(ExtendedItem item in ItemSheet) {
			if (item.ItemUICategory.Row != 46 && item.ItemUICategory.Row != 44)
				continue;

			ItemAction? action = item.ItemAction.Value;
			if (action is null)
				continue;

			if (action.DataHQ[0] == 48) {
				// Food
				ItemFood? food = FoodSheet.GetRow(action.DataHQ[1]);
				if (food is not null) {
					var fdata = new Food(item.RowId, food.RowId, item.LevelItem.Row, true);
					fdata.UpdateStats(food);
					foodList.Add(fdata);
				}
			}

			if (action.Data[0] == 48) {
				// Food (NQ)
				ItemFood? food = FoodSheet.GetRow(action.Data[1]);
				if (food is not null) {
					var fdata = new Food(item.RowId, food.RowId, item.LevelItem.Row, false);
					fdata.UpdateStats(food);
					foodList.Add(fdata);
				}
			}

			if (action.DataHQ[0] == 49) {
				// Medicine
				ItemFood? food = FoodSheet.GetRow(action.DataHQ[1]);
				if (food is not null) {
					var fdata = new Food(item.RowId, food.RowId, item.LevelItem.Row, true);
					fdata.UpdateStats(food);
					medicineList.Add(fdata);
				}
			}

			if (action.Data[0] == 49) {
				// Medicine (NQ)
				ItemFood? food = FoodSheet.GetRow(action.Data[1]);
				if (food is not null) {
					var fdata = new Food(item.RowId, food.RowId, item.LevelItem.Row, false);
					fdata.UpdateStats(food);
					medicineList.Add(fdata);
				}
			}
		}

		sw.Stop();

		Plugin.INSTANCE.Logger.Info($"Found {foodList.Count} food and {medicineList.Count} medicine in {sw.ElapsedMilliseconds}ms");
	}

	#endregion

	public static int GetBaseStatAtLevel(Stat stat, uint level) {
		if (stat == Stat.GP)
			return 400;
		if (stat == Stat.CP)
			return 180;

		if (LevelStats.TryGetValue(level, out var stats)) {
			if (MAIN_STATS.Contains(stat))
				return stats.Main;

			if (SUB_STATS.Contains(stat))
				return stats.Sub;
		}

		return 0;
	}

	internal static readonly Stat[] MAIN_STATS = new Stat[] {
		Stat.STR,
		Stat.DEX,
		Stat.VIT,
		Stat.INT,
		Stat.MND,
		Stat.PIE,
		Stat.DET
	};

	internal static readonly Stat[] SUB_STATS = new Stat[] {
		Stat.TEN,
		Stat.DH,
		Stat.CRT,
		Stat.SKS,
		Stat.SPS
	};

	internal static readonly Dictionary<uint, int> COEFFICIENTS = new() {
		[(int) Stat.CRT] = 200,
		[(int) Stat.DET] = 140,
		[(int) Stat.DH]  = 550,
		[(int) Stat.SKS] = 130,
		[(int) Stat.SPS] = 130,

		[(int) Stat.TEN] = 100,
		[(int) Stat.PIE] = 150,

		[(int) Stat.DEF] = 15,
		[(int) Stat.MDF] = 15,
	};

	internal static readonly Dictionary<uint, StatAtLevel> LevelStats = new() {
		[00] = new (Main: 0, Sub: 0),
		[01] = new (Main: 20, Sub: 56),
		[02] = new (Main: 21, Sub: 57),
		[03] = new (Main: 22, Sub: 60),
		[04] = new (Main: 24, Sub: 62),
		[05] = new (Main: 26, Sub: 65),
		[06] = new (Main: 27, Sub: 68),
		[07] = new (Main: 29, Sub: 70),
		[08] = new (Main: 31, Sub: 73),
		[09] = new (Main: 33, Sub: 76),
		[10] = new (Main: 35, Sub: 78),
		[11] = new (Main: 36, Sub: 82),
		[12] = new (Main: 38, Sub: 85),
		[13] = new (Main: 41, Sub: 89),
		[14] = new (Main: 44, Sub: 93),
		[15] = new (Main: 46, Sub: 96),
		[16] = new (Main: 49, Sub: 100),
		[17] = new (Main: 52, Sub: 104),
		[18] = new (Main: 54, Sub: 109),
		[19] = new (Main: 57, Sub: 113),
		[20] = new (Main: 60, Sub: 116),
		[21] = new (Main: 63, Sub: 122),
		[22] = new (Main: 67, Sub: 127),
		[23] = new (Main: 71, Sub: 133),
		[24] = new (Main: 74, Sub: 138),
		[25] = new (Main: 78, Sub: 144),
		[26] = new (Main: 81, Sub: 150),
		[27] = new (Main: 85, Sub: 155),
		[28] = new (Main: 89, Sub: 162),
		[29] = new (Main: 92, Sub: 168),
		[30] = new (Main: 97, Sub: 173),
		[31] = new (Main: 101, Sub: 181),
		[32] = new (Main: 106, Sub: 188),
		[33] = new (Main: 110, Sub: 194),
		[34] = new (Main: 115, Sub: 202),
		[35] = new (Main: 119, Sub: 209),
		[36] = new (Main: 124, Sub: 215),
		[37] = new (Main: 128, Sub: 223),
		[38] = new (Main: 134, Sub: 229),
		[39] = new (Main: 139, Sub: 236),
		[40] = new (Main: 144, Sub: 244),
		[41] = new (Main: 150, Sub: 253),
		[42] = new (Main: 155, Sub: 263),
		[43] = new (Main: 161, Sub: 272),
		[44] = new (Main: 166, Sub: 283),
		[45] = new (Main: 171, Sub: 292),
		[46] = new (Main: 177, Sub: 302),
		[47] = new (Main: 183, Sub: 311),
		[48] = new (Main: 189, Sub: 322),
		[49] = new (Main: 196, Sub: 331),
		[50] = new (Main: 202, Sub: 341),
		[51] = new (Main: 204, Sub: 342),
		[52] = new (Main: 205, Sub: 344),
		[53] = new (Main: 207, Sub: 345),
		[54] = new (Main: 209, Sub: 346),
		[55] = new (Main: 210, Sub: 347),
		[56] = new (Main: 212, Sub: 349),
		[57] = new (Main: 214, Sub: 350),
		[58] = new (Main: 215, Sub: 351),
		[59] = new (Main: 217, Sub: 352),
		[60] = new (Main: 218, Sub: 354),
		[61] = new (Main: 224, Sub: 355),
		[62] = new (Main: 228, Sub: 356),
		[63] = new (Main: 236, Sub: 357),
		[64] = new (Main: 244, Sub: 358),
		[65] = new (Main: 252, Sub: 359),
		[66] = new (Main: 260, Sub: 360),
		[67] = new (Main: 268, Sub: 361),
		[68] = new (Main: 276, Sub: 362),
		[69] = new (Main: 284, Sub: 363),
		[70] = new (Main: 292, Sub: 364),
		[71] = new (Main: 296, Sub: 365),
		[72] = new (Main: 300, Sub: 366),
		[73] = new (Main: 305, Sub: 367),
		[74] = new (Main: 310, Sub: 368),
		[75] = new (Main: 315, Sub: 370),
		[76] = new (Main: 320, Sub: 372),
		[77] = new (Main: 325, Sub: 374),
		[78] = new (Main: 330, Sub: 376),
		[79] = new (Main: 335, Sub: 378),
		[80] = new (Main: 340, Sub: 380),
		[81] = new (Main: 345, Sub: 382),
		[82] = new (Main: 350, Sub: 384),
		[83] = new (Main: 355, Sub: 386),
		[84] = new (Main: 360, Sub: 388),
		[85] = new (Main: 365, Sub: 390),
		[86] = new (Main: 370, Sub: 392),
		[87] = new (Main: 375, Sub: 394),
		[88] = new (Main: 380, Sub: 396),
		[89] = new (Main: 385, Sub: 398),
		[90] = new (Main: 390, Sub: 400),
	};
}
