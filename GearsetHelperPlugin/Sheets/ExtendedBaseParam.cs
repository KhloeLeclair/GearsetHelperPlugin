using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace GearsetExportPlugin.Sheets;

[Sheet("BaseParam")]
public class ExtendedBaseParam : BaseParam {

	public readonly ushort[] EquipSlotCategoryPct = new ushort[22];

	public override void PopulateData(RowParser parser, GameData gameData, Language language) {
		base.PopulateData(parser, gameData, language);

		for(int i = 1; i < EquipSlotCategoryPct.Length; i++) {
			EquipSlotCategoryPct[i] = parser.ReadColumn<ushort>(i + 3);
		}
	}
}
