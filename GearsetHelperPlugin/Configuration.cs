using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace GearsetExportPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

	public string? EtroUsername { get; set; } = null;
	public string? EtroPassword { get; set; } = null;

	public string? EtroApiKey { get; set; } = null;

	public bool ShowItems { get; set; } = false;

	public bool AttachToExamine { get; set; } = true;
	public int AttachSide { get; set; } = 1;

    // the below exist just to make saving less cumbersome

    [NonSerialized]
    private DalamudPluginInterface? pluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }
}
