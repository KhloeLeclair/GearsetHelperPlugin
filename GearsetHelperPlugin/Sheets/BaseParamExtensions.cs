using System;

using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin.Sheets;

internal static class BaseParamExtensions {

	internal static ushort EquipSlotCategoryPct(this BaseParam param, uint index, bool shouldThrow = true) {
		// Before we just did this by reading the columns sequentially, but the
		// new Lumina changes mean we need to do this I guess?
		return index switch {
			//0 => ???
			1 => param.OneHandWeaponPercent,
			2 => param.OffHandPercent,
			3 => param.HeadPercent,
			4 => param.ChestPercent,
			5 => param.HandsPercent,
			6 => param.WaistPercent,
			7 => param.LegsPercent,
			8 => param.FeetPercent,
			9 => param.EarringPercent,
			10 => param.NecklacePercent,
			11 => param.BraceletPercent,
			12 => param.RingPercent,
			13 => param.TwoHandWeaponPercent,
			14 => param.UnderArmorPercent,
			15 => param.ChestHeadPercent,
			16 => param.ChestHeadLegsFeetPercent,
			17 => param.Unknown0, // Soul Crystal
			18 => param.LegsFeetPercent,
			19 => param.HeadChestHandsLegsFeetPercent,
			20 => param.ChestLegsGlovesPercent,
			21 => param.ChestLegsFeetPercent,
			22 => param.Unknown1, // Chest Gloves
								  //23 => ???
			_ => shouldThrow ? throw new IndexOutOfRangeException(nameof(index)) : (ushort) 0
		};
	}

}
