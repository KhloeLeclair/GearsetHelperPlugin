using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace GearsetHelperPlugin.Sheets;

[Sheet("Item")]
public class ExtendedItem : Item {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public LazyRow<ExtendedClassJobCategory> ExtendedClassJobCategory { get; set; }
	public LazyRow<ExtendedItemLevel> ExtendedItemLevel { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	public ItemUnkData59Obj[] BaseParam => UnkData59;
	public ItemUnkData73Obj[] BaseParamSpecial => UnkData73;

	public override void PopulateData(RowParser parser, GameData gameData, Language language) {
		base.PopulateData(parser, gameData, language);

		ExtendedClassJobCategory = new LazyRow<ExtendedClassJobCategory>(gameData, ClassJobCategory.Row, language);
		ExtendedItemLevel = new LazyRow<ExtendedItemLevel>(gameData, LevelItem.Row, language);
	}
}
