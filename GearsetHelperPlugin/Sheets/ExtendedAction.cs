using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using ExcelAction = Lumina.Excel.GeneratedSheets.Action;

namespace GearsetHelperPlugin.Sheets;

[Sheet("Action")]
public class ExtendedAction : ExcelAction {

	public LazyRow<ActionTransient> ActionTransient { get; set; } = null!;

	public override void PopulateData(RowParser parser, GameData gameData, Language language) {
		base.PopulateData(parser, gameData, language);

		ActionTransient = new LazyRow<ActionTransient>(gameData, RowId, Language.English);
	}

}
