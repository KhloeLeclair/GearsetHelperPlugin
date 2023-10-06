using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin;

internal static class ChatHelper {

	internal static void LinkItem(this IChatGui chat, ExtendedItem? item, bool highQuality = true) {
		if (item is null)
			return;

		var result = new SeString(new Payload[] {
			new UIForegroundPayload((ushort) (0x223 + item.Rarity * 2)),
			new UIGlowPayload((ushort) (0x224 + item.Rarity * 2)),
			new ItemPayload(item.RowId, item.CanBeHq && highQuality),
			new UIForegroundPayload(500),
			new UIGlowPayload(501),
			new TextPayload($"{(char) SeIconChar.LinkMarker}"),
			new UIForegroundPayload(0),
			new UIGlowPayload(0),
			new TextPayload(item.Name + (item.CanBeHq && highQuality ? $" {(char)SeIconChar.HighQuality}" : "")),
			new RawPayload(new byte[] {0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03}),
			new RawPayload(new byte[] {0x02, 0x13, 0x02, 0xEC, 0x03})
		});

		chat.Print(new XivChatEntry {
			Message = result,
			Type = XivChatType.Echo
		});
	}

}
