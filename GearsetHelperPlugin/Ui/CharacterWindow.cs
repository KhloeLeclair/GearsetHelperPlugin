using System;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

using GearsetHelperPlugin.Models;

namespace GearsetHelperPlugin.Ui;

internal class CharacterWindow : BaseWindow {

	protected override string Name => "Character";

	internal CharacterWindow(PluginUI ui) : base(ui) {

	}

	public override void Dispose(bool disposing) {
		
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
	}

	private unsafe PlayerCharacter? GetActor() {
		// TODO: Rewrite this entire method, and probably factor it out.

		return Ui.Plugin.ClientState.LocalPlayer;

		var charAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("Character", 1);
		if (charAddon == null || !charAddon->IsVisible)
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

		var nodeList = charAddon->UldManager.NodeList;
		ushort count = charAddon->UldManager.NodeListCount;

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
