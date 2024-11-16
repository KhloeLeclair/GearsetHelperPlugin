using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin;

internal static class ChatHelper {

	internal static void LinkItem(this IChatGui chat, Item item, bool highQuality = true) {
		LinkItem(chat, item.RowId, highQuality && item.CanBeHq);
	}

	internal static void LinkItem(this IChatGui chat, uint itemId, bool highQuality) {

		var sb = new SeStringBuilder()
			.AddItemLink(itemId, highQuality);

		chat.Print(new XivChatEntry {
			Message = sb.Build(),
			Type = XivChatType.Echo
		});
	}

}
