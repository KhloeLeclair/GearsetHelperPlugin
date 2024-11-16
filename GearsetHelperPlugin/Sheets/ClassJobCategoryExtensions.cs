using System;

using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin.Sheets;

internal static class ClassJobCategoryExtensions {

	internal static bool Classes(this ClassJobCategory cat, uint index) {
		return index switch {
			0 => cat.ADV,
			1 => cat.GLA,
			2 => cat.PGL,
			3 => cat.MRD,
			4 => cat.LNC,
			5 => cat.ARC,
			6 => cat.CNJ,
			7 => cat.THM,
			8 => cat.CRP,
			9 => cat.BSM,
			10 => cat.ARM,
			11 => cat.GSM,
			12 => cat.LTW,
			13 => cat.WVR,
			14 => cat.ALC,
			15 => cat.CUL,
			16 => cat.MIN,
			17 => cat.BTN,
			18 => cat.FSH,
			19 => cat.PLD,
			20 => cat.MNK,
			21 => cat.WAR,
			22 => cat.DRG,
			23 => cat.BRD,
			24 => cat.WHM,
			25 => cat.BLM,
			26 => cat.ACN,
			27 => cat.SMN,
			28 => cat.SCH,
			29 => cat.ROG,
			30 => cat.NIN,
			31 => cat.MCH,
			32 => cat.DRK,
			33 => cat.AST,
			34 => cat.SAM,
			35 => cat.RDM,
			36 => cat.BLU,
			37 => cat.GNB,
			38 => cat.DNC,
			39 => cat.RPR,
			40 => cat.SGE,
			41 => cat.VPR,
			42 => cat.PCT,
			_ => throw new IndexOutOfRangeException("invalid index")
		};
	}

}
