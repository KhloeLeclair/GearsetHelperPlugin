using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace GearsetHelperPlugin.Sheets;

[Sheet("ClassJobCategory")]
public class ExtendedClassJobCategory : BaseParam {

	public readonly bool[] Classes = new bool[41];

	public override void PopulateData(RowParser parser, GameData gameData, Language language) {
		base.PopulateData(parser, gameData, language);

		for (int i = 0; i < Classes.Length; i++)
			Classes[i] = parser.ReadColumn<bool>(i + 1);
	}
}
