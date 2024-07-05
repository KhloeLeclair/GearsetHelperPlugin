using Dalamud;

using Dalamud.Game.ClientState.Objects.SubKinds;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

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
		if (charAddon is null || !charAddon->IsVisible) {
			SelectedFood = null;
			SelectedMedicine = null;
			SelectedGroupBonus = 0;
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

	protected override unsafe IPlayerCharacter? GetActor() {
		// The local player is easy to deal with.
		return Ui.Plugin.ClientState.LocalPlayer;
	}

}
