using BepInEx;
using SilkDev.Events;
using SilkDev.Textures;
using SilkDev.Windows;

namespace SilkDev.Internal;

internal static class PluginInfo
{
	public const string PLUGIN_GUID="com.dakusan.silkdev";
	public const string PLUGIN_NAME="Silk Developer Helpers";
	public const string PLUGIN_VERSION="2.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class Plugin : BaseUnityPlugin
{
	internal static Plugin Self=null!; //Singleton
	internal static new BepInEx.Logging.ManualLogSource? Logger;

	//Unity passthrough functions
	private void Awake() => Init();
	private void OnGUI() => Catcher.Run($"{nameof(Window.Handle_OnGUI)}", Window.Handle_OnGUI);
	private void Update() => Catcher.Run($"{nameof(GameEvents.Handle_Update)}", GameEvents.Handle_Update);

	//Initialize the plugin
	private void Init()
	{
		Misc.InitSingleton(this, ref Self);

		Log.InitBeforeConfig(Logger=base.Logger);
		_=new Config(Config);
		Log.InitAfterConfig();

		Catcher.Run("Dev Init", static () => {
			DevInput.Mouse.Visibility.Init();
			new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
			Window.OnNextFrame(static () => new DevInput.BlockInput());
			ExtractAllTextures.Init(Internal.Config.C.RunExtractAllTextures);
			ExtractSpritesWindow.Init();
			Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded");
		});
	}
}