using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lumina.Excel;

using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin.Models;

internal record struct MeldedItem(
	uint ID,
	bool HighQuality,
	MeldedMateria[] Melds
) {

	public ExtendedItem? Row() {
		if (!Data.CheckSheets())
			return null;
		return Data.ItemSheet.GetRow(ID);
	}

};
