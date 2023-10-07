using System;

using Dalamud.Interface.Windowing;

using GearsetHelperPlugin.Ui;

namespace GearsetHelperPlugin;

internal class PluginUI : IDisposable {

	internal Plugin Plugin { get; }

	private readonly ExamineWindow ExamineWindow;
	private readonly CharacterWindow CharacterWindow;

	private readonly SettingsWindow Settings;

	public WindowSystem WindowSystem = new("GearsetHelperPlugin");

    public PluginUI(Plugin plugin) {
		Plugin = plugin;

		ExamineWindow = new ExamineWindow(this);
		CharacterWindow = new CharacterWindow(this);
		Settings = new SettingsWindow(this);

		WindowSystem.AddWindow(Settings);

		Plugin.Interface.UiBuilder.Draw += Draw;
		Plugin.Interface.UiBuilder.OpenConfigUi += OpenConfig;
	}

    public void Dispose() {
		WindowSystem.RemoveAllWindows();

		Settings.Dispose();

		Plugin.Interface.UiBuilder.Draw -= Draw;
		Plugin.Interface.UiBuilder.OpenConfigUi -= OpenConfig;
	}

	private void OpenConfig() {
		Settings.OpenSettings();
	}

    private void Draw() {

		WindowSystem.Draw();

		ExamineWindow.Draw();
		CharacterWindow.Draw();
    }
}
