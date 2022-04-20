using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace GearsetHelperPlugin.Sheets;

[Sheet("Item")]
public class ExtendedItem : Item {

	public byte LevelSyncFlag { get; set; }

	public LazyRow<ExtendedClassJobCategory>? ExtendedClassJobCategory { get; set; }

	public UnkData59Obj[] BaseParam => UnkData59;
	public UnkData73Obj[] BaseParamSpecial => UnkData73;

	public override void PopulateData(RowParser parser, GameData gameData, Language language) {
		base.PopulateData(parser, gameData, language);

		ExtendedClassJobCategory = new LazyRow<ExtendedClassJobCategory>(gameData, ClassJobCategory.Row, language);
		LevelSyncFlag = parser.ReadColumn<byte>(89);
	}
}
