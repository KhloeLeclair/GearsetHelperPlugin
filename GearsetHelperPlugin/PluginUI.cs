using ImGuiNET;
using System;
using System.Numerics;

using GearsetHelperPlugin.Ui;

namespace GearsetHelperPlugin;

internal class PluginUI : IDisposable {

	internal Plugin Plugin { get; }

	private readonly ExamineWindow ExamineHelper;
	private readonly SettingsWindow Settings;

    public PluginUI(Plugin plugin) {
		Plugin = plugin;

		ExamineHelper = new ExamineWindow(this);
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
		ExamineHelper.Draw();
    }
}
