using BepInEx;
using SilkDev;

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
	internal static new BepInEx.Logging.ManualLogSource? Logger;

	//Unity passthrough functions
	private void Awake() => Init();

	//Initialize the plugin
	private void Init()
	{
		//Add loggers
		#if DEBUG
			Log.AddLogger(
				"PAtlas-Debug",
				new AsyncLogger(FileOps.PathCombine(FileOps.GetPluginPath, "PAtlas-Debug.log")) { DuplicateWindowSeconds=10 }
			);
		#endif
		Log.AddLogger("PAtlas-Error", ErrorLog);

		new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
		Catcher.Run($"{PluginInfo.PLUGIN_NAME} Config Init", () => new Config(Config));
		SetupErrorLog();
		Catcher.Run($"{PluginInfo.PLUGIN_NAME} Init", () => {
			_=new MonitorSaveValues();
			_=new MoreMarkers();
		});
		SilkDev.Windows.Window.OnNextFrame(() => {
			SaveValuesWindow.Init();
			SearchWindow.Init();
			(Logger=base.Logger).LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded");
		});
	}

	//Error log
	private static readonly AsyncLogger ErrorLog=new(null, FilterErrorLog) { DuplicateWindowSeconds=10 };
	internal const string PAtlasErrorLogName="PAtlas-Error.log";
	private Config C => PharloomAtlas.Config.C;
	private void SetupErrorLog()
	{
		void SetLogFilename() => ErrorLog.LogFilePath=(C.UseErrorLog ? FileOps.PathCombine(FileOps.GetPluginPath, PAtlasErrorLogName) : null);
		C.UseErrorLog.SettingChanged += (_, _) => SetLogFilename();
		SetLogFilename();
	}
	private static string? FilterErrorLog(string LogLine) =>
		LogLine.StartsWith("[Error]") ? LogLine : null;
}