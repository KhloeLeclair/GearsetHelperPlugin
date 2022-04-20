using ImGuiNET;
using System;
using System.Numerics;

using GearsetExportPlugin.Ui.Helpers;

namespace GearsetExportPlugin;

// It is good to have this be disposable in general, in case you ever need it
// to do any cleanup
internal class PluginUI : IDisposable {

	internal Plugin Plugin { get; }

    private bool settingsVisible = false;
    public bool SettingsVisible
    {
        get { return settingsVisible; }
        set { settingsVisible = value; }
    }

	private ExamineHelper ExamineHelper { get; }

    // passing in the image here just for simplicity
    public PluginUI(Plugin plugin) {
		Plugin = plugin;

		ExamineHelper = new ExamineHelper(this);

		Plugin.Interface.UiBuilder.Draw += Draw;
		Plugin.Interface.UiBuilder.OpenConfigUi += OpenConfig;
	}

    public void Dispose() {
		Plugin.Interface.UiBuilder.Draw -= Draw;
		Plugin.Interface.UiBuilder.OpenConfigUi -= OpenConfig;
	}

	private void OpenConfig() {
		SettingsVisible = true;
	}

    private void Draw() {
        DrawSettingsWindow();

		ExamineHelper.Draw();
    }

    public void DrawSettingsWindow()
    {
        if (!SettingsVisible)
            return;

        ImGui.SetNextWindowSize(new Vector2(425, 250), ImGuiCond.Appearing);
        if (ImGui.Begin($"{Plugin.Name} Settings", ref settingsVisible, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {

			ImGui.TextWrapped("In order to export gearsets to Etro, you need to get your Token as described in Etro's API documentation.");

			ImGui.Spacing();

			/*
			string username = Plugin.Config.EtroUsername ?? string.Empty;
			string password = Plugin.Config.EtroPassword ?? string.Empty;

			if (ImGui.InputText("Etro Username", ref username, 1024, ImGuiInputTextFlags.EnterReturnsTrue)) {
				Plugin.Config.EtroUsername = string.IsNullOrEmpty(username) ? null : username;
				Plugin.Config.Save();
			}

			if (ImGui.InputText("Etro Password", ref password, 1024, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.Password)) {
				Plugin.Config.EtroPassword = string.IsNullOrEmpty(password) ? null : password;
				Plugin.Config.Save();
			}*/

			// can't ref a property, so use a local copy
			string token = Plugin.Config.EtroApiKey ?? string.Empty;
			if (ImGui.InputText("Etro API Key", ref token, 1024, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue)) {
				Plugin.Config.EtroApiKey = string.IsNullOrEmpty(token) ? null : token;
				Plugin.Config.Save();
				Plugin.Exporter.ClearError();
			}

			ImGui.Spacing();

			bool showItems = Plugin.Config.DisplayItemsDebug;
			if (ImGui.Checkbox("(DEBUG) Show Items Section", ref showItems)) {
				Plugin.Config.DisplayItemsDebug = showItems;
				Plugin.Config.Save();
			}

        }
        ImGui.End();
    }
}
