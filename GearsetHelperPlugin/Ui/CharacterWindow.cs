using System;
using System.Linq;
using System.Collections.Generic;

using Dalamud;
using Dalamud.Logging;

using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

using GearsetHelperPlugin.Models;

using DStatus = Dalamud.Game.ClientState.Statuses.Status;

namespace GearsetHelperPlugin.Ui;

internal class CharacterWindow : BaseWindow {

	protected override string Name => Localization.Localize("gui.character", "Character");

	internal CharacterWindow(PluginUI ui) : base(ui) {
		Visible = Ui.Plugin.Config.CharacterOpen;
	}

	public override void Dispose(bool disposing) {
		
	}

	protected override void OnVisibleChange() {
		Ui.Plugin.Config.CharacterOpen = Visible;
		Ui.Plugin.Config.Save();
	}

	internal unsafe void Draw() {
		if (!Ui.Plugin.Config.DisplayWithCharacter) {
			CachedSet = null;
			return;
		}

		var charAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("Character", 1);
		if (charAddon is null || ! charAddon->IsVisible) {
			CachedSet = null;
			return;
		}

		var root = charAddon->RootNode;
		if (root is null)
			return;

		DrawWindow(
			Ui.Plugin.Config.AttachToCharacter,
			Ui.Plugin.Config.AttachSideCharacter,
			charAddon->X,
			charAddon->Y,
			(ushort) (root->Width * charAddon->Scale)
		);
	}

	protected override unsafe InventoryContainer* GetInventoryContainer() {
		return InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
	}

	protected override bool HasEquipment() {
		return true;
	}

	protected override void UpdatePlayerData(EquipmentSet set) {

		var player = GetActor();
		if (player is null)
			return;

		set.UpdatePlayer(
			name: player.Name.ToString(),
			race: player.Customize[(int) CustomizeIndex.Race],
			gender: player.Customize[(int) CustomizeIndex.Gender],
			tribe: player.Customize[(int) CustomizeIndex.Tribe],
			level: player.Level
		);

		// Check our food, and maybe select it.
		if (SelectedFood is null && Ui.Plugin.Config.CharacterAutoFood) {
			var statuses = GetStatuses(player);
			if (statuses is not null)
				foreach (var status in statuses) {
					if (status.StatusId == 48) {
						set.UpdateFood((uint) status.StackCount, false);
						SelectedFood = set.Food;
						break;
					}
				}
		}
	}

	private DStatus[]? GetStatuses(PlayerCharacter? player) {
		if (player is null)
			return null;

		var list = player.StatusList;
		if (list is null)
			return null;

		int count = 0;
		for (int i = 0; i < list.Length; i++) {
			var status = list[i];
			if (status is not null && status.StatusId != 0)
				count++;
		}

		DStatus[] result = new DStatus[count];
		int j = 0;
		for(int i = 0; i < list.Length; i++) {
			var status = list[i];
			if (status is not null && status.StatusId != 0)
				result[j++] = status;
		}

		return result;
	}

	private unsafe PlayerCharacter? GetActor() {
		// The local player is easy to deal with.
		return Ui.Plugin.ClientState.LocalPlayer;
	}

}
