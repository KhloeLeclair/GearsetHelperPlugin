using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin.Models;

internal record struct MeldedItem(
	uint ID,
	bool HighQuality,
	MeldedMateria[] Melds
) {

	public Item? Row() {
		if (!Data.CheckSheets() || !Data.ItemSheet.TryGetRow(ID, out var row))
			return null;
		return row;
	}

};
