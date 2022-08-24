using System;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Dalamud.Hooking;
using Dalamud.Logging;

namespace GearsetHelperPlugin;

internal class GameFunctions : IDisposable {

	private readonly Plugin Plugin;

	private delegate byte ExamineRefreshedDelegate(IntPtr basePtr, IntPtr a2, IntPtr loadPtr);

	private readonly Hook<ExamineRefreshedDelegate>? _examineRefreshHook;

	internal delegate void ExamineOnRefreshDelegate(ushort baseId, int a2, uint loadingStage);

	internal event ExamineOnRefreshDelegate? ExamineOnRefresh;

	public GameFunctions(Plugin plugin) {
		Plugin = plugin;

		var erPtr = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 49 8B D8 48 8B F9 4D 85 C0 0F 84 ?? ?? ?? ?? 85 D2");
		_examineRefreshHook = Hook<ExamineRefreshedDelegate>.FromAddress(erPtr, ExamineRefreshed); // new Hook<ExamineRefreshedDelegate>(erPtr, ExamineRefreshed);
		_examineRefreshHook.Enable();
	}

	public void Dispose() {
		_examineRefreshHook?.Dispose();
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
}
