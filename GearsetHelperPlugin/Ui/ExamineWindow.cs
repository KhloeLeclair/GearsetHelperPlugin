using System;
using System.Linq;
using System.Collections.Generic;

using Dalamud;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

using GearsetHelperPlugin.Models;

using DStatus = Dalamud.Game.ClientState.Statuses.Status;

namespace GearsetHelperPlugin.Ui;

internal class ExamineWindow : BaseWindow {

	private uint examineLoadStage = 4;

	protected override string Name => Localization.Localize("gui.examine", "Examine");

	internal ExamineWindow(PluginUI ui) : base(ui) {
		Ui.Plugin.Functions.ExamineOnRefresh += ExamineRefreshed;

		Visible = Ui.Plugin.Config.ExamineOpen;
	}

	public override void Dispose(bool disposing) {
		Ui.Plugin.Functions.ExamineOnRefresh -= ExamineRefreshed;
	}

	private void ExamineRefreshed(ushort menuId, int val, uint loadStage) {
		// Just save the load state so our draw call knows if data is loaded or not.
		if (loadStage == 1 || loadStage > examineLoadStage)
			examineLoadStage = loadStage;
	}

	protected override void OnVisibleChange() {
		Ui.Plugin.Config.ExamineOpen = Visible;
		Ui.Plugin.Config.Save();

		if (Visible && Ui.Plugin.Config.CharacterAutoFood && CachedSet is not null)
			UpdateFoodData();
	}

	internal unsafe void Draw() {
		if (!Ui.Plugin.Config.DisplayWithExamine) {
			CachedSet = null;
			SelectedFood = null;
			SelectedMedicine = null;
			return;
		}

		var examineAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("CharacterInspect", 1);
		if (examineAddon == null || !examineAddon->IsVisible) {
			CachedSet = null;
			SelectedFood = null;
			SelectedMedicine = null;
			return;
		}

		var root = examineAddon->RootNode;
		if (root is null)
			return;

		DrawWindow(
			Ui.Plugin.Config.AttachToExamine,
			Ui.Plugin.Config.AttachSideExamine,
			examineAddon->X,
			examineAddon->Y,
			(ushort) (root->Width * examineAddon->Scale)
		);
	}


	protected override unsafe InventoryContainer* GetInventoryContainer() {
		return InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
	}

	protected override bool HasEquipment() {
		return examineLoadStage >= 4;
	}

	protected override unsafe PlayerCharacter? GetActor() {
		// TODO: Rewrite this entire method, and probably factor it out.

		var examineAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("CharacterInspect", 1);
		if (examineAddon == null || !examineAddon->IsVisible)
			return null;

		Lazy<Dictionary<string, PlayerCharacter>> players = new(() => {
			var rawPlayers = Ui.Plugin.ObjectTable
				.Where(obj => obj is PlayerCharacter && obj.IsValid())
				.Cast<PlayerCharacter>();

			var result = new Dictionary<string, PlayerCharacter>();

			foreach (var entry in rawPlayers) {
				string name = entry.Name.TextValue;
				if (!result.ContainsKey(name))
					result[name] = entry;
			}

			return result;
		});

		var nodeList = examineAddon->UldManager.NodeList;
		ushort count = examineAddon->UldManager.NodeListCount;

		for (ushort i = 0; i < count; i++) {
			var obj = nodeList[i];
			if (obj == null)
				continue;

			if (!obj->IsVisible || obj->Type != NodeType.Text)
				continue;

			var txt = obj->GetAsAtkTextNode();
			if (txt == null)
				continue;

			string? result = txt->NodeText.ToString();

			if (!string.IsNullOrEmpty(result) && players.Value.TryGetValue(result, out PlayerCharacter? player))
				return player;
		}

		return null;
	}
}
