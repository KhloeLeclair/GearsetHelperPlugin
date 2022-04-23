using System.IO;

using Dalamud;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;

using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin;

public class Plugin : IDalamudPlugin
{
	internal const string PluginName = "Gearset Helper";
	public string Name => PluginName;

	[PluginService]
	internal DalamudPluginInterface Interface { get; init; }

	[PluginService]
	internal ChatGui ChatGui { get; init; }

	[PluginService]
	internal ClientState ClientState { get; init; }

	[PluginService]
	internal CommandManager CommandManager { get; init; }

	[PluginService]
	internal DataManager DataManager { get; init; }

	[PluginService]
	internal Framework Framework { get; init; }

	[PluginService]
	internal GameGui GameGui { get; init; }

	[PluginService]
	internal ObjectTable ObjectTable { get; init; }

	[PluginService]
	internal SigScanner SigScanner { get; init; }

	internal GameFunctions Functions { get; }

	internal Localization Localization { get; }

	internal Configuration Config { get; }
	internal PluginUI Ui { get; }
	internal Exporter Exporter { get; }

	#pragma warning disable 8618
	public Plugin() {

		string i18n_path = Path.Join(
			Path.GetDirectoryName(Interface!.AssemblyLocation.FullName),
			"i18n"
		);

		Localization = new Localization(i18n_path);
		Localization.SetupWithUiCulture();

		Config = Interface!.GetPluginConfig() as Configuration ?? new Configuration();
		Config.Initialize(Interface);

		Data.FoodMinIlvl = Config.FoodMinIlvl;
		Data.FoodMinIlvlDoHL = Config.FoodMinIlvlDoHL;
		Data.LoadSheets(DataManager!.Excel);
		Data.LoadFoodAsync();

		Functions = new GameFunctions(this);
		Exporter = new Exporter(this);
		Ui = new PluginUI(this);

	}
	#pragma warning restore 8618

	public void Dispose()
    {
		Ui.Dispose();
		Exporter.Dispose();
		Functions.Dispose();
    }

}
