using System.Numerics;
using System.Threading.Tasks;

using Dalamud;
using Dalamud.Interface.Colors;

using ImGuiNET;

namespace GearsetHelperPlugin.Ui;

internal class SettingsWindow {

	private readonly PluginUI Ui;

	private bool visible = false;

	private string token = string.Empty;
	private string username = string.Empty;
	private string password = string.Empty;

	private Task<Exporter.EtroLoginResponse>? LoginTask;

	private Configuration Config => Ui.Plugin.Config;

	public bool Visible {
		get => visible;
		set {
			visible = value;
		}
	}

	public SettingsWindow(PluginUI ui) {
		Ui = ui;
	}

	public void OpenSettings() {
		visible = true;
		username = string.Empty;
		password = string.Empty;
		token = Config.EtroApiKey ?? string.Empty;
	}

	public void Draw() {
		if (!Visible)
			return;

		float scale = ImGui.GetFontSize() / 17;

		ImGui.SetNextWindowSize(new Vector2(400 * scale, 100), ImGuiCond.Appearing);
		ImGui.SetNextWindowSizeConstraints(new Vector2(400 * scale, 100), new Vector2(500 * scale, float.MaxValue));

		if (ImGui.Begin(Localization.Localize("gui.settings", "Gearset Helper Settings"), ref visible, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)) {

			int ilvl = (int) Config.FoodMinIlvl;
			if (ImGui.DragInt(Localization.Localize("gui.settings.food-ilvl", "Food Min Ilvl"), ref ilvl, 10, 0, 610)) {
				Config.FoodMinIlvl = (uint) ilvl;
				Data.FoodMinIlvl = Config.FoodMinIlvl;
				Config.Save();
			}

			int ilvl_dohl = (int) Config.FoodMinIlvlDoHL;
			if (ImGui.DragInt(Localization.Localize("gui.settings.food-ilvl-dohl", "Food Min Ilvl (DoH/L)"), ref ilvl_dohl, 10, 0, 610)) {
				Config.FoodMinIlvlDoHL = (uint) ilvl_dohl;
				Data.FoodMinIlvlDoHL = Config.FoodMinIlvlDoHL;
				Config.Save();
			}

			ImGui.TextColored(ImGuiColors.DalamudGrey, Localization.Localize("gui.settings.examine", "Examine Window"));

			ImGui.Indent();

			bool display = Config.DisplayWithExamine;
			if (ImGui.Checkbox(Localization.Localize("gui.settings.enable", "Enable"), ref display)) {
				Config.DisplayWithExamine = display;
				Config.Save();
			}

			bool attach = Config.AttachToExamine;
			if (ImGui.Checkbox(Localization.Localize("gui.settings.attach", "Attach"), ref attach)) {
				Config.AttachToExamine = attach;
				Config.Save();
			}

			int side = Config.AttachSideExamine;
			if (ImGui.Combo(Localization.Localize("gui.settings.side", "Side"), ref side, Localization.Localize("gui.settings.sides", "Left\x00Right"))) {
				Config.AttachSideExamine = side;
				Config.Save();
			}

			ImGui.Unindent();

			ImGui.Spacing();

			ImGui.TextColored(ImGuiColors.DalamudGrey, "Character Window");

			ImGui.Indent();

			bool cdisplay = Config.DisplayWithCharacter;
			ImGui.PushID("character:enable");
			if (ImGui.Checkbox(Localization.Localize("gui.settings.enable", "Enable"), ref cdisplay)) {
				Config.DisplayWithCharacter = cdisplay;
				Config.Save();
			}
			ImGui.PopID();

			bool autofood = Config.CharacterAutoFood;
			ImGui.PushID("character:food");
			if (ImGui.Checkbox(Localization.Localize("gui.settings.auto-food", "Automatically Set Food"), ref autofood)) {
				Config.CharacterAutoFood = autofood;
				Config.Save();
			}
			ImGui.PopID();

			bool cattach = Config.AttachToCharacter;
			ImGui.PushID("character:attach");
			if (ImGui.Checkbox(Localization.Localize("gui.settings.attach", "Attach"), ref cattach)) {
				Config.AttachToCharacter = cattach;
				Config.Save();
			}
			ImGui.PopID();

			int cside = Config.AttachSideCharacter;
			ImGui.PushID("character:side");
			if (ImGui.Combo(Localization.Localize("gui.settings.side", "Side"), ref cside, Localization.Localize("gui.settings.sides", "Left\x00Right"))) {
				Config.AttachSideCharacter = cside;
				Config.Save();
			}
			ImGui.PopID();

			ImGui.Unindent();

			ImGui.Spacing();

			ImGui.TextColored(ImGuiColors.DalamudGrey, Localization.Localize("gui.settings.etro", "Etro Support"));
			ImGui.TextWrapped(Localization.Localize("gui.settings.etro.about", "In order to export gearsets to Etro, you need to authenticate yourself. If you use Discord to login, you'll need to enter an API key manually."));

			if (LoginTask == null) {
				bool logged_in = !string.IsNullOrEmpty(Config.EtroApiKey);

				ImGui.InputText(
					Localization.Localize("gui.settings.etro.key", "Key"),
					ref token,
					1024,
					ImGuiInputTextFlags.AutoSelectAll | (logged_in ? (ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.Password) : ImGuiInputTextFlags.None)
				);

				if (!logged_in) {
					if (ImGui.Button(Localization.Localize("gui.settings.save", "Save"))) {
						Config.EtroApiKey = string.IsNullOrEmpty(token) ? null : token;
						Config.EtroRefreshKey = null;
						Config.Save();
					}

					bool login = false;

					if (ImGui.InputText(Localization.Localize("gui.settings.username", "Username"), ref username, 1024, ImGuiInputTextFlags.EnterReturnsTrue))
						login = true;
					if (ImGui.InputText(Localization.Localize("gui.settings.password", "Password"), ref password, 1024, ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue))
						login = true;
					if (ImGui.Button(Localization.Localize("gui.settings.login", "Login")))
						login = true;

					if (login && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
						LoginTask = Ui.Plugin.Exporter.LoginEtro(username, password);

				} else {
					if (ImGui.Button(Localization.Localize("gui.settings.logout", "Log Out"))) {
						token = string.Empty;
						Config.EtroApiKey = null;
						Config.EtroRefreshKey = null;
						Config.Save();
					}
				}

			} else { 
				if (LoginTask.IsCompleted) {
					if (!string.IsNullOrEmpty(LoginTask.Result.ApiKey)) {
						Config.EtroApiKey = LoginTask.Result.ApiKey;
						Config.EtroRefreshKey = LoginTask.Result.RefreshKey;
						Config.Save();

						username = string.Empty;
						password = string.Empty;
						token = Config.EtroApiKey;
						LoginTask = null;

					} else {
						ImGui.TextColored(ImGuiColors.DalamudYellow, Localization.Localize("gui.settings.error", "Error"));
						ImGui.TextWrapped(LoginTask.Result.Error ?? "An unknown error occurred.");
						if (ImGui.Button(Localization.Localize("gui.ok", "OK")))
							LoginTask = null;
					}
				} else
					ImGui.TextColored(ImGuiColors.ParsedGrey, Localization.Localize("gui.settings.logging-in", "Logging in..."));
			}
		}
	}

}
