using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SilkDev;
using System.Reflection;

namespace PharloomAtlas;

internal static class PluginInfo
{
	public const string PLUGIN_GUID="com.dakusan.pharloomatlas";
	public const string PLUGIN_NAME="Pharloom Atlas";
	public const string PLUGIN_VERSION="1.0.0";
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
		Catcher.Run($"{PluginInfo.PLUGIN_NAME} Init", () => {
			_=new Config(Config);
			_=new MonitorSaveValues();
			_=new MoreMarkers();
		});
		Window.OnNextFrame(() => {
			SaveValuesWindow.Init();
			SearchWindow.Init();
			(Logger=base.Logger).LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded");
		});
	}

	internal static string GetMyPath => FileOps.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
}