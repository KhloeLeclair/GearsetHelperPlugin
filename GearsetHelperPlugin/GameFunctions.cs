using System;
using System.Collections.Generic;

using Dalamud.Game.Text.SeStringHandling;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Dalamud.Hooking;
using Dalamud.Logging;

namespace GearsetHelperPlugin;

internal class GameFunctions : IDisposable {

	private readonly Plugin Plugin;

	private delegate byte SearchInfoDownloadedDelegate(IntPtr data, IntPtr a2, IntPtr searchInfoPtr, IntPtr a4);

	private readonly Hook<SearchInfoDownloadedDelegate>? _searchInfoDownloadedHook;

	internal delegate void ReceiveSearchInfoEventDelegate(uint objectId, SeString info);

	internal event ReceiveSearchInfoEventDelegate? ReceiveSearchInfo;


	private delegate byte ExamineRefreshedDelegate(IntPtr basePtr, IntPtr a2, IntPtr loadPtr);

	private readonly Hook<ExamineRefreshedDelegate>? _examineRefreshHook;

	internal delegate void ExamineOnRefreshDelegate(ushort baseId, int a2, uint loadingStage);

	internal event ExamineOnRefreshDelegate? ExamineOnRefresh;

	public GameFunctions(Plugin plugin) {
		Plugin = plugin;

		var erPtr = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 49 8B D8 48 8B F9 4D 85 C0 0F 84 ?? ?? ?? ?? 85 D2");
		_examineRefreshHook = new Hook<ExamineRefreshedDelegate>(erPtr, ExamineRefreshed);
		_examineRefreshHook.Enable();

		var sidPtr = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 49 8B E8 8B DA");
		_searchInfoDownloadedHook = new Hook<SearchInfoDownloadedDelegate>(sidPtr, SearchInfoDownloaded);
		_searchInfoDownloadedHook.Enable();
	}

	public void Dispose() {
		_examineRefreshHook?.Dispose();
		_searchInfoDownloadedHook?.Dispose();
	}

	private unsafe byte ExamineRefreshed(IntPtr basePtr, IntPtr a2, IntPtr loadPtr) {
		byte result = _examineRefreshHook!.Original(basePtr, a2, loadPtr);

		try {
			AtkUnitBase* baseAs = (AtkUnitBase*) basePtr;

			ushort id = ((AtkUnitBase*) basePtr)->ID;
			uint load = ((AtkValue*) loadPtr)->UInt;

			ExamineOnRefresh?.Invoke(id, (int) a2, load);

		} catch (Exception ex) {
			PluginLog.LogError($"Error in ExamineRefreshed Hook. Details:\n{ex}");
		}

		return result;
	}

	private unsafe byte SearchInfoDownloaded(IntPtr data, IntPtr a2, IntPtr searchInfoPtr, IntPtr a4) {
		byte result = _searchInfoDownloadedHook!.Original(data, a2, searchInfoPtr, a4);

		try {
			uint actorId = *(uint*) (data + 48);

			List<byte> bytes = new();
			byte* ptr = (byte*) data;
			while(*ptr != 0) {
				bytes.Add(*ptr);
				ptr += 1;
			}

			var searchInfo = SeString.Parse(bytes.ToArray());

			ReceiveSearchInfo?.Invoke(actorId, searchInfo);

		} catch (Exception ex) {
			PluginLog.LogError($"Error in SearchInfoDownloaded Hook. Details:\n{ex}");
		}

		return result;
	}

}
