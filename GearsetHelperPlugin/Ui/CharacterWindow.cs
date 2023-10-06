using Dalamud;

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

		if (Visible && Ui.Plugin.Config.CharacterAutoFood && CachedSet is not null)
			UpdateFoodData();
	}

	internal unsafe void Draw() {
		if (!Ui.Plugin.Config.DisplayWithCharacter) {
			CachedSet = null;
			return;
		}

		var charAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("Character", 1);
		if (charAddon is null || ! charAddon->IsVisible) {
			SelectedFood = null;
			SelectedMedicine = null;
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

	protected void UpdateFoodData(EquipmentSet? set = null, PlayerCharacter? player = null, bool update = true) {
		set ??= CachedSet;
		if (set is null)
			return;

		player ??= GetActor();
		if (player is null)
			return;

		var statuses = GetStatuses(player);
		if (statuses is not null)
			foreach (var status in statuses) {
				if (status.StatusId == 48) {
					bool hq = false;
					uint foodId = status.Param;
					if ( foodId >= 10000 ) {
						hq = true;
						foodId -= 10000;
					}

					set.UpdateFood(foodId, hq, update);
					SelectedFood = set.Food;
					break;
				}
			}
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
		if (SelectedFood is null && Ui.Plugin.Config.CharacterAutoFood)
			UpdateFoodData(set, player, false);
	}

	private static DStatus[]? GetStatuses(PlayerCharacter? player) {
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
