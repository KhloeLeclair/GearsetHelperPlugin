using ImGuiNET;
using System;
using System.Numerics;

using GearsetHelperPlugin.Ui;

namespace GearsetHelperPlugin;

internal class PluginUI : IDisposable {

	internal Plugin Plugin { get; }

	private readonly ExamineWindow ExamineWindow;
	private readonly CharacterWindow CharacterWindow;

	private readonly SettingsWindow Settings;

    public PluginUI(Plugin plugin) {
		Plugin = plugin;

		ExamineWindow = new ExamineWindow(this);
		CharacterWindow = new CharacterWindow(this);
		Settings = new SettingsWindow(this);

		Plugin.Interface.UiBuilder.Draw += Draw;
		Plugin.Interface.UiBuilder.OpenConfigUi += OpenConfig;
	}

    public void Dispose() {
		Plugin.Interface.UiBuilder.Draw -= Draw;
		Plugin.Interface.UiBuilder.OpenConfigUi -= OpenConfig;
	}

	private void OpenConfig() {
		Settings.OpenSettings();
	}

    private void Draw() {
        Settings.Draw();
		ExamineWindow.Draw();
		CharacterWindow.Draw();
    }
}
