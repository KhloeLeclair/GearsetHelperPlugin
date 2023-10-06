using System.IO;

using Dalamud;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin;

public class Plugin : IDalamudPlugin
{
	internal const string PluginName = "Gearset Helper";
	public string Name => PluginName;

	[PluginService]
	internal DalamudPluginInterface Interface { get; init; }

	[PluginService]
	internal IChatGui ChatGui { get; init; }

	[PluginService]
	internal IClientState ClientState { get; init; }

	[PluginService]
	internal ICommandManager CommandManager { get; init; }

	[PluginService]
	internal IDataManager DataManager { get; init; }

	[PluginService]
	internal IFramework Framework { get; init; }

	[PluginService]
	internal IGameGui GameGui { get; init; }

	[PluginService]
	internal IObjectTable ObjectTable { get; init; }

	[PluginService]
	internal ISigScanner SigScanner { get; init; }

	[PluginService]
	internal IGameInteropProvider GameInteropProvider { get; init; }

	[PluginService]
	internal IPluginLog PluginLog { get; init; }

	[PluginService]
	internal ITextureProvider TextureProvider { get; init; }

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
		Data.FoodHQOnly = Config.FoodHQOnly;
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
