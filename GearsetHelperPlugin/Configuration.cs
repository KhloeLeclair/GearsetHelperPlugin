using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace GearsetHelperPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

	public string? EtroApiKey { get; set; } = null;
	public string? EtroRefreshKey { get; set; } = null;

	public uint FoodMinIlvlDoHL { get; set; } = 200;
	public uint FoodMinIlvl { get; set; } = 500;

	public bool CharacterAutoFood { get; set; } = true;

	public bool ExamineOpen { get; set; } = false;
	public bool CharacterOpen { get; set; } = false;

	public bool DisplayWithExamine { get; set; } = true;
	public bool DisplayWithCharacter { get; set; } = true;

	public bool AttachToExamine { get; set; } = true;
	public bool AttachToCharacter { get; set; } = true;

	public int AttachSideExamine { get; set; } = 1;
	public int AttachSideCharacter { get; set; } = 1;

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
