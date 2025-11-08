using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace NoClip;

internal static class PluginInfo
{
	public const string PLUGIN_GUID="com.dakusan.noclip";
	public const string PLUGIN_NAME="NoClip";
	public const string PLUGIN_VERSION="1.2.1";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.dakusan.silkdev", BepInDependency.DependencyFlags.HardDependency)]
internal class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource? Logger;

	//Unity passthrough functions
	private void Awake() => Init();

	//Initialize the plugin
	private void Init()
	{
		new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
		SilkDev.Catcher.Run($"{PluginInfo.PLUGIN_NAME} Init", () => {
			_=new Config(Config);
			_=new NCActivate();
			(Logger=base.Logger).LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded");
		});
	}
}