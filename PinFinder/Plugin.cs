using BepInEx;
using SilkDev;

namespace PinFinder;

internal static class PluginInfo
{
	public const string PLUGIN_GUID="com.dakusan.pinfinder";
	public const string PLUGIN_NAME="PinFinder";
	public const string PLUGIN_VERSION="1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.dakusan.silkdev", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.dakusan.noclip", BepInDependency.DependencyFlags.HardDependency)]
internal class Plugin : BaseUnityPlugin
{
	internal static new BepInEx.Logging.ManualLogSource? Logger;

	//Unity passthrough functions
	private void Awake() => Init();

	//Initialize the plugin
	private void Init()
	{
		new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
		Config MyConfig=null!;
		Catcher.Run($"{PluginInfo.PLUGIN_NAME} Init", () => {
			MyConfig=new Config(Config);

			//Keyboard Shortcuts
			SilkDev.Events.GameEvents.OnUpdate += () => Misc.IFF(
				MyConfig.Key_StartPinProcess.IsDown(),
				() => MyConfig.StartPinFindingProcess.V=!MyConfig.StartPinFindingProcess
			);
			(Logger=base.Logger).LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded");
		});

		FindPins.Init();
	}
}