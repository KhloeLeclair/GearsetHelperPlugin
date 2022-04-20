using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace GearsetHelperPlugin.Sheets;

[Sheet("ItemLevel")]
public class ExtendedItemLevel : ItemLevel {

	public readonly ushort[] BaseParam = new ushort[75];

	public override void PopulateData(RowParser parser, GameData gameData, Language language) {
		base.PopulateData(parser, gameData, language);

		for (int i = 1; i < BaseParam.Length; i++) {
			BaseParam[i] = parser.ReadColumn<ushort>(i - 1);
		}
	}
}
