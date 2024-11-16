using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin.Models;

internal record struct MeldedMateria(
	ushort ID,
	byte Grade
) {

	public Materia? Row() {
		if (!Data.CheckSheets() || !Data.MateriaSheet.TryGetRow(ID, out var row))
			return null;
		return row;
	}

};
